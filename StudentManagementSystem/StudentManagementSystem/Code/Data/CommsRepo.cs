using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using StudentManagementSystem.Data;
using StudentManagementSystem.Integrations;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Data
{
    /// <summary>Notifications, announcements, push subscriptions, complaints, chat FAQ.</summary>
    public static class CommsRepo
    {
        // ---------------- notifications ----------------
        /// <summary>
        /// Creates a notification + recipient rows in one transaction.
        /// audience: "all" (active users) | "students" | "faculty" | "user:<userId>" | "course:<courseId>"
        /// </summary>
        public static int CreateNotification(string title, string message, string ntype, string actionUrl,
            int createdBy, string audience)
        {
            return CreateNotification(title, message, ntype, actionUrl, createdBy, audience, null);
        }

        /// <param name="relatedEntityId">Optional id of the record the notification is about.</param>
        public static int CreateNotification(string title, string message, string ntype, string actionUrl,
            int createdBy, string audience, int? relatedEntityId, string sourceKey = null, bool isTicker = true)
        {
            string recipientIds = "";
            if (audience == "all")
                recipientIds = "SELECT UserId FROM dbo.Users WHERE IsActive = 1";
            else if (audience == "students")
                recipientIds = "SELECT ur.UserId FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.RoleId = ur.RoleId WHERE r.RoleName = 'Student'";
            else if (audience == "faculty")
                recipientIds = "SELECT ur.UserId FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.RoleId = ur.RoleId WHERE r.RoleName = 'Faculty'";
            else if (audience == "admin")
                recipientIds = "SELECT ur.UserId FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.RoleId = ur.RoleId WHERE r.RoleName = 'Admin'";
            else if (audience.StartsWith("course:"))
            {
                int courseId = int.Parse(audience.Substring("course:".Length));
                recipientIds = "SELECT st.UserId FROM dbo.Students st WHERE st.CourseId = " + courseId + " AND st.UserId IN (SELECT UserId FROM dbo.Users WHERE IsActive = 1)";
            }
            else if (audience.StartsWith("user:"))
            {
                recipientIds = "SELECT @TargetId";
            }
            if (recipientIds.Length == 0) throw new Exception("Unknown notification audience: " + audience);

            int notificationId = -1;
            using (var conn = ConnectionManager.CreateOpenConnection())
            {
                SqlTransaction tx = conn.BeginTransaction();
                try
                {
                    if (!string.IsNullOrWhiteSpace(sourceKey))
                    {
                        using (var cmd = new SqlCommand(
                            "SELECT NotificationId FROM dbo.Notifications WITH (UPDLOCK, HOLDLOCK) WHERE SourceKey = @SourceKey", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@SourceKey", sourceKey.Trim());
                            object existing = cmd.ExecuteScalar();
                            if (existing != null && existing != DBNull.Value)
                            {
                                tx.Commit();
                                return Convert.ToInt32(existing);
                            }
                        }
                    }

                    using (var cmd = new SqlCommand(
                        "INSERT INTO dbo.Notifications (Title, Message, NType, ActionUrl, CreatedBy, RelatedEntityId, SourceKey, IsTicker) " +
                        "VALUES (@Title, @Message, @NType, @ActionUrl, @CreatedBy, @RelatedEntityId, @SourceKey, @IsTicker); " +
                        "SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        cmd.Parameters.AddWithValue("@Message", message);
                        cmd.Parameters.AddWithValue("@NType", string.IsNullOrEmpty(ntype) ? "general" : ntype);
                        cmd.Parameters.AddWithValue("@ActionUrl", (object)actionUrl ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@RelatedEntityId", (object)relatedEntityId ?? DBNull.Value);
                        // 0 means "the system" (e.g. a payment webhook). CreatedBy is a
                        // nullable FK, so store NULL instead of a non-existent user id.
                        cmd.Parameters.AddWithValue("@CreatedBy", createdBy > 0 ? (object)createdBy : DBNull.Value);
                        cmd.Parameters.AddWithValue("@SourceKey", string.IsNullOrWhiteSpace(sourceKey) ? (object)DBNull.Value : sourceKey.Trim());
                        cmd.Parameters.AddWithValue("@IsTicker", isTicker ? 1 : 0);
                        notificationId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    if (audience.StartsWith("user:"))
                    {
                        using (var cmd = new SqlCommand(
                            "INSERT INTO dbo.NotificationRecipients (NotificationId, UserId) " +
                            "SELECT @Nid, u.UserId FROM (SELECT @TargetId AS UserId) u " +
                            "WHERE EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @TargetId AND IsActive = 1)", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@Nid", notificationId);
                            cmd.Parameters.AddWithValue("@TargetId", int.Parse(audience.Substring("user:".Length)));
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        using (var cmd = new SqlCommand(
                            "INSERT INTO dbo.NotificationRecipients (NotificationId, UserId) " +
                            "SELECT @Nid, UserId FROM (" + recipientIds + ") AS targets", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@Nid", notificationId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    // Queue an email copy for this notification. The row stays 'queued'
                    // when email is disabled/unconfigured, so nothing is falsely reported
                    // as delivered, and it is drained once SMTP is configured.
                    if (AppConfig.EmailEnabled)
                    {
                        using (var cmd = new SqlCommand(
                            "INSERT INTO dbo.NotificationOutbox (NotificationId, Channel, Status) " +
                            "SELECT @Nid, 'email', 'queued' WHERE EXISTS (" +
                            "  SELECT 1 FROM dbo.NotificationRecipients r " +
                            "  JOIN dbo.Users u ON u.UserId = r.UserId " +
                            "  WHERE r.NotificationId = @Nid AND u.Email IS NOT NULL)", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@Nid", notificationId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                    AuditService.Log("NotificationSent", "Notifications", notificationId, audience);

                    // Best effort, after the commit: a slow or broken SMTP server must not
                    // roll back a notification that is already valid in the database.
                    if (AppConfig.EmailEnabled)
                    {
                        try { NotificationDispatcher.FlushQueued(5); }
                        catch (Exception ex) { Global.WriteLog("Notify", "flush after create failed", ex); }
                    }

                    return notificationId;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public static DataTable Inbox(int userId, bool unreadOnly, int limit)
        {
            return SqlHelper.FillDataTable(
                "SELECT TOP (@Limit) n.NotificationId, n.Title, n.Message, n.NType, n.ActionUrl, n.CreatedAtUtc, nr.IsRead, nr.ReadAtUtc " +
                "FROM dbo.NotificationRecipients nr " +
                "JOIN dbo.Notifications n ON n.NotificationId = nr.NotificationId " +
                "WHERE nr.UserId = @UserId " +
                "  AND (@UnreadOnly = 0 OR nr.IsRead = 0) " +
                "ORDER BY n.CreatedAtUtc DESC",
                p => { p.AddWithValue("@Limit", limit); p.AddWithValue("@UserId", userId); p.AddWithValue("@UnreadOnly", unreadOnly ? 1 : 0); });
        }

        public static int UnreadCount(int userId)
        {
            return SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.NotificationRecipients WHERE UserId = @UserId AND IsRead = 0",
                p => p.AddWithValue("@UserId", userId));
        }

        public static void MarkRead(int notificationId, int userId)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.NotificationRecipients SET IsRead = 1, ReadAtUtc = SYSUTCDATETIME() " +
                "WHERE NotificationId = @Nid AND UserId = @UserId AND IsRead = 0",
                p => { p.AddWithValue("@Nid", notificationId); p.AddWithValue("@UserId", userId); });
        }

        public static void MarkAllRead(int userId)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.NotificationRecipients SET IsRead = 1, ReadAtUtc = SYSUTCDATETIME() " +
                "WHERE UserId = @UserId AND IsRead = 0",
                p => p.AddWithValue("@UserId", userId));
        }

        // ---------------- filtered / paged inbox (respects user preferences) ----------------

        /// <summary>
        /// One page of the user's notifications, newest first, with the categories
        /// they switched off in Settings removed. filter: "all" | "unread" | "read";
        /// type: "" (any) or one of general/academic/payment/complaint/system.
        /// </summary>
        public static DataTable InboxVisible(int userId, UserPrefs prefs, bool unreadOnly, int page, int pageSize)
        {
            return InboxVisible(userId, prefs, unreadOnly ? "unread" : "all", "", "", page, pageSize);
        }

        public static DataTable InboxVisible(int userId, UserPrefs prefs, string filter, string type, int page, int pageSize)
        {
            return InboxVisible(userId, prefs, filter, type, "", page, pageSize);
        }

        /// <summary>One page of the inbox. filter is all|unread|read|archived; q searches title and message.</summary>
        public static DataTable InboxVisible(int userId, UserPrefs prefs, string filter, string type, string q, int page, int pageSize)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 20 : (pageSize > 100 ? 100 : pageSize);
            var disabled = DisabledTypes(prefs);
            string sql = "SELECT n.NotificationId, n.Title, n.Message, n.NType, n.ActionUrl, n.CreatedAtUtc, nr.IsRead, nr.ReadAtUtc, nr.IsArchived " +
                "FROM dbo.NotificationRecipients nr " +
                "JOIN dbo.Notifications n ON n.NotificationId = nr.NotificationId " +
                "WHERE nr.UserId = @UserId " +
                "  AND (@UnreadOnly = 0 OR nr.IsRead = 0) " +
                "  AND (@ReadOnly = 0 OR nr.IsRead = 1) " +
                "  AND (@Type = '' OR n.NType = @Type) " +
                "  AND (@Q = N'' OR n.Title LIKE N'%' + @Q + N'%' OR n.Message LIKE N'%' + @Q + N'%') " +
                "  AND ((@Archived = 0 AND nr.IsArchived = 0) OR (@Archived = 1 AND nr.IsArchived = 1)) " + DisabledClause(disabled) +
                "ORDER BY n.CreatedAtUtc DESC, n.NotificationId DESC " +
                "OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            return SqlHelper.FillDataTable(sql, p =>
            {
                p.AddWithValue("@UserId", userId);
                p.AddWithValue("@UnreadOnly", string.Equals(filter, "unread", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
                p.AddWithValue("@ReadOnly", string.Equals(filter, "read", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
                p.AddWithValue("@Type", (type ?? "").Trim().ToLowerInvariant());
                p.AddWithValue("@Q", (q ?? "").Trim());
                p.AddWithValue("@Archived", string.Equals(filter, "archived", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
                p.AddWithValue("@Skip", (page - 1) * pageSize);
                p.AddWithValue("@Take", pageSize);
                AddDisabledParams(p, disabled);
            });
        }

        public static int InboxCountVisible(int userId, UserPrefs prefs, string filter, string type)
        {
            return InboxCountVisible(userId, prefs, filter, type, "");
        }

        public static int InboxCountVisible(int userId, UserPrefs prefs, string filter, string type, string q)
        {
            var disabled = DisabledTypes(prefs);
            return SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.NotificationRecipients nr " +
                "JOIN dbo.Notifications n ON n.NotificationId = nr.NotificationId " +
                "WHERE nr.UserId = @UserId " +
                "  AND (@UnreadOnly = 0 OR nr.IsRead = 0) " +
                "  AND (@ReadOnly = 0 OR nr.IsRead = 1) " +
                "  AND (@Type = '' OR n.NType = @Type) " +
                "  AND (@Q = N'' OR n.Title LIKE N'%' + @Q + N'%' OR n.Message LIKE N'%' + @Q + N'%') " +
                "  AND ((@Archived = 0 AND nr.IsArchived = 0) OR (@Archived = 1 AND nr.IsArchived = 1)) " + DisabledClause(disabled),
                p =>
                {
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@UnreadOnly", string.Equals(filter, "unread", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
                    p.AddWithValue("@ReadOnly", string.Equals(filter, "read", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
                    p.AddWithValue("@Type", (type ?? "").Trim().ToLowerInvariant());
                    p.AddWithValue("@Q", (q ?? "").Trim());
                    p.AddWithValue("@Archived", string.Equals(filter, "archived", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
                    AddDisabledParams(p, disabled);
                });
        }

        /// <summary>Unread, non-archived, non-snoozed ticker items not dismissed by this user.</summary>
        public static DataTable TickerFeed(int userId, UserPrefs prefs, int limit)
        {
            limit = limit < 1 ? 5 : (limit > 20 ? 20 : limit);
            var disabled = DisabledTypes(prefs);
            return SqlHelper.FillDataTable(
                "SELECT TOP (@Take) n.NotificationId, n.Title, n.Message, n.NType, n.ActionUrl, n.CreatedAtUtc, nr.SnoozedUntilUtc " +
                "FROM dbo.NotificationRecipients nr " +
                "JOIN dbo.Notifications n ON n.NotificationId = nr.NotificationId " +
                "WHERE nr.UserId = @UserId AND nr.IsRead = 0 AND nr.IsArchived = 0 AND n.IsTicker = 1 " +
                "  AND (nr.SnoozedUntilUtc IS NULL OR nr.SnoozedUntilUtc <= SYSUTCDATETIME()) " +
                "  AND NOT EXISTS (SELECT 1 FROM dbo.TickerDismissals d WHERE d.UserId = @UserId AND d.NotificationId = n.NotificationId) " +
                DisabledClause(disabled) +
                "ORDER BY n.CreatedAtUtc DESC, n.NotificationId DESC",
                p =>
                {
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@Take", limit);
                    AddDisabledParams(p, disabled);
                });
        }

        public static int TickerCount(int userId, UserPrefs prefs)
        {
            var disabled = DisabledTypes(prefs);
            return SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.NotificationRecipients nr " +
                "JOIN dbo.Notifications n ON n.NotificationId = nr.NotificationId " +
                "WHERE nr.UserId = @UserId AND nr.IsRead = 0 AND nr.IsArchived = 0 AND n.IsTicker = 1 " +
                "  AND (nr.SnoozedUntilUtc IS NULL OR nr.SnoozedUntilUtc <= SYSUTCDATETIME()) " +
                "  AND NOT EXISTS (SELECT 1 FROM dbo.TickerDismissals d WHERE d.UserId = @UserId AND d.NotificationId = n.NotificationId) " +
                DisabledClause(disabled),
                p => { p.AddWithValue("@UserId", userId); AddDisabledParams(p, disabled); });
        }

        public static void Snooze(int notificationId, int userId, int minutes)
        {
            minutes = minutes < 5 ? 5 : (minutes > 1440 ? 1440 : minutes);
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.NotificationRecipients SET SnoozedUntilUtc = DATEADD(MINUTE, @Minutes, SYSUTCDATETIME()) " +
                "WHERE NotificationId = @Nid AND UserId = @UserId",
                p =>
                {
                    p.AddWithValue("@Nid", notificationId);
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@Minutes", minutes);
                });
        }

        public static void DismissTicker(int notificationId, int userId)
        {
            SqlHelper.ExecuteNonQuery(
                "IF NOT EXISTS (SELECT 1 FROM dbo.TickerDismissals WHERE UserId = @UserId AND NotificationId = @Nid) " +
                "INSERT dbo.TickerDismissals (UserId, NotificationId) VALUES (@UserId, @Nid)",
                p => { p.AddWithValue("@Nid", notificationId); p.AddWithValue("@UserId", userId); });
        }

        public static void Archive(int notificationId, int userId, bool archived)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.NotificationRecipients SET IsArchived = @Archived " +
                "WHERE NotificationId = @Nid AND UserId = @UserId",
                p => { p.AddWithValue("@Nid", notificationId); p.AddWithValue("@UserId", userId); p.AddWithValue("@Archived", archived ? 1 : 0); });
        }

        /// <summary>Unread count that matches what the inbox is allowed to show.</summary>
        public static int UnreadCountVisible(int userId, UserPrefs prefs)
        {
            var disabled = DisabledTypes(prefs);
            return SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.NotificationRecipients nr " +
                "JOIN dbo.Notifications n ON n.NotificationId = nr.NotificationId " +
                "WHERE nr.UserId = @UserId AND nr.IsRead = 0 " + DisabledClause(disabled),
                p => { p.AddWithValue("@UserId", userId); AddDisabledParams(p, disabled); });
        }

        /// <summary>One notification, only if the given user is a recipient. Null otherwise.</summary>
        public static DataRow GetForUser(int notificationId, int userId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT n.NotificationId, n.Title, n.Message, n.NType, n.ActionUrl, n.CreatedAtUtc, nr.IsRead " +
                "FROM dbo.NotificationRecipients nr " +
                "JOIN dbo.Notifications n ON n.NotificationId = nr.NotificationId " +
                "WHERE nr.NotificationId = @Nid AND nr.UserId = @UserId",
                p => { p.AddWithValue("@Nid", notificationId); p.AddWithValue("@UserId", userId); });
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public static DataTable InboxTypes(int userId)
        {
            return SqlHelper.FillDataTable(
                "SELECT DISTINCT n.NType FROM dbo.NotificationRecipients nr " +
                "JOIN dbo.Notifications n ON n.NotificationId = nr.NotificationId " +
                "WHERE nr.UserId = @UserId ORDER BY n.NType",
                p => p.AddWithValue("@UserId", userId));
        }

        private static List<string> DisabledTypes(UserPrefs prefs)
        {
            var list = new List<string>();
            if (prefs == null || string.IsNullOrEmpty(prefs.DisabledNotifTypes)) return list;
            foreach (string raw in prefs.DisabledNotifTypes.Split(','))
            {
                string t = raw.Trim().ToLowerInvariant();
                if (Array.IndexOf(PrefsRepo.NotifTypes, t) >= 0 && !list.Contains(t)) list.Add(t);
            }
            return list;
        }

        private static string DisabledClause(List<string> disabled)
        {
            if (disabled.Count == 0) return "";
            var parts = new List<string>();
            for (int i = 0; i < disabled.Count; i++) parts.Add("@OffType" + i);
            return " AND n.NType NOT IN (" + string.Join(", ", parts) + ") ";
        }

        private static void AddDisabledParams(SqlParameterCollection p, List<string> disabled)
        {
            for (int i = 0; i < disabled.Count; i++) p.AddWithValue("@OffType" + i, disabled[i]);
        }

        // ---------------- announcements ----------------
        private const string AnnSelect =
            "SELECT TOP 100 a.AnnId, a.AnnTitle, a.AnnBody, a.Audience, a.Category, a.IsPinned, a.PublishFrom, a.PublishTo, " +
            "a.TargetCourseId, a.TargetSemester, a.IsActive, u.FullName AS AuthorName, a.CreatedAtUtc, c.CourseCode AS TargetCourse, " +
            "(SELECT COUNT(*) FROM dbo.AnnouncementAcks k WHERE k.AnnId = a.AnnId) AS AckCount, " +
            "CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.AnnouncementAcks k WHERE k.AnnId = a.AnnId AND k.UserId = @UserId) THEN 1 ELSE 0 END AS BIT) AS HasAcked " +
            "FROM dbo.Announcements a JOIN dbo.Users u ON u.UserId = a.CreatedBy " +
            "LEFT JOIN dbo.Courses c ON c.CourseId = a.TargetCourseId ";

        public static DataTable AnnouncementsVisible(string role)
        {
            return AnnouncementsVisible(role, 0);
        }

        /// <summary>Announcements the signed-in user may see, honouring role, course and semester targeting.</summary>
        public static DataTable AnnouncementsVisible(string role, int userId)
        {
            return SqlHelper.FillDataTable(
                AnnSelect +
                "WHERE a.IsActive = 1 " +
                "  AND a.PublishFrom <= CAST(SYSUTCDATETIME() AS DATE) " +
                "  AND (a.PublishTo IS NULL OR a.PublishTo >= CAST(SYSUTCDATETIME() AS DATE)) " +
                "  AND (a.Audience = 'all' " +
                "       OR (a.Audience = 'students' AND @Role IN ('Student','Admin')) " +
                "       OR (a.Audience = 'faculty' AND @Role IN ('Faculty','Admin'))) " +
                "  AND (a.TargetCourseId IS NULL OR @Role = 'Admin' OR a.TargetCourseId = (SELECT st.CourseId FROM dbo.Students st WHERE st.UserId = @UserId)) " +
                "  AND (a.TargetSemester IS NULL OR @Role = 'Admin' OR a.TargetSemester = (SELECT st.SemesterNumber FROM dbo.Students st WHERE st.UserId = @UserId)) " +
                "ORDER BY a.IsPinned DESC, a.PublishFrom DESC, a.CreatedAtUtc DESC",
                p => { p.AddWithValue("@Role", role ?? ""); p.AddWithValue("@UserId", userId); });
        }

        public static DataTable AnnouncementsAll()
        {
            return SqlHelper.FillDataTable(
                AnnSelect + "ORDER BY a.IsPinned DESC, a.CreatedAtUtc DESC",
                p => p.AddWithValue("@UserId", 0));
        }

        public static DataRow OneAnnouncement(int annId)
        {
            DataTable dt = SqlHelper.FillDataTable(AnnSelect + "WHERE a.AnnId = @Id",
                p => { p.AddWithValue("@UserId", 0); p.AddWithValue("@Id", annId); });
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public static void CreateAnnouncement(string title, string body, string audience, int createdBy, string publishTo)
        {
            CreateAnnouncement(title, body, audience, "general", createdBy, DateTime.UtcNow.ToString("yyyy-MM-dd"), publishTo, null, null, false);
        }

        public static void CreateAnnouncement(string title, string body, string audience, string category,
            int createdBy, string publishFrom, string publishTo, int? targetCourseId, int? targetSemester, bool pinned)
        {
            int id = SqlHelper.ExecuteScalarInt(
                "INSERT INTO dbo.Announcements (AnnTitle, AnnBody, Audience, Category, PublishFrom, PublishTo, TargetCourseId, TargetSemester, IsPinned, CreatedBy) " +
                "OUTPUT INSERTED.AnnId VALUES (@Title, @Body, @Audience, @Category, " +
                "CASE WHEN @From = '' THEN CAST(SYSUTCDATETIME() AS DATE) ELSE CAST(@From AS DATE) END, " +
                "CASE WHEN @To = '' THEN NULL ELSE CAST(@To AS DATE) END, @Course, @Semester, @Pinned, @CreatedBy)",
                p =>
                {
                    p.AddWithValue("@Title", title); p.AddWithValue("@Body", body);
                    p.AddWithValue("@Audience", audience); p.AddWithValue("@Category", category ?? "general");
                    p.AddWithValue("@From", publishFrom ?? ""); p.AddWithValue("@To", publishTo ?? "");
                    p.AddWithValue("@Course", (object)targetCourseId ?? DBNull.Value);
                    p.AddWithValue("@Semester", (object)targetSemester ?? DBNull.Value);
                    p.AddWithValue("@Pinned", pinned ? 1 : 0);
                    p.AddWithValue("@CreatedBy", createdBy);
                });
            AuditService.Log("AnnouncementCreated", "Announcements", id, audience + "/" + category);
            NotifyAudience(title, body, audience, targetCourseId, createdBy, id);
        }

        public static void UpdateAnnouncement(int annId, string title, string body, string audience, string category,
            string publishFrom, string publishTo, int? targetCourseId, int? targetSemester, bool pinned)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Announcements SET AnnTitle=@Title, AnnBody=@Body, Audience=@Audience, Category=@Category, " +
                "PublishFrom=CASE WHEN @From='' THEN PublishFrom ELSE CAST(@From AS DATE) END, " +
                "PublishTo=CASE WHEN @To='' THEN NULL ELSE CAST(@To AS DATE) END, " +
                "TargetCourseId=@Course, TargetSemester=@Semester, IsPinned=@Pinned WHERE AnnId=@Id",
                p =>
                {
                    p.AddWithValue("@Title", title); p.AddWithValue("@Body", body);
                    p.AddWithValue("@Audience", audience); p.AddWithValue("@Category", category ?? "general");
                    p.AddWithValue("@From", publishFrom ?? ""); p.AddWithValue("@To", publishTo ?? "");
                    p.AddWithValue("@Course", (object)targetCourseId ?? DBNull.Value);
                    p.AddWithValue("@Semester", (object)targetSemester ?? DBNull.Value);
                    p.AddWithValue("@Pinned", pinned ? 1 : 0);
                    p.AddWithValue("@Id", annId);
                });
            AuditService.Log("AnnouncementUpdated", "Announcements", annId);
        }

        private static void NotifyAudience(string title, string body, string audience, int? courseId, int createdBy, int annId)
        {
            try
            {
                string target = audience == "students" ? "students" : audience == "faculty" ? "faculty" : "all";
                if (audience == "students" && courseId.HasValue) target = "course:" + courseId.Value;
                CreateNotification("Announcement: " + title, body.Length > 300 ? body.Substring(0, 297) + "..." : body,
                    "academic", "/Student/Notifications.aspx", createdBy, target, annId);
            }
            catch (Exception ex) { Global.WriteLog("Announce", "notification fan-out failed", ex); }
        }

        public static void ToggleAnnouncementPin(int annId)
        {
            SqlHelper.ExecuteNonQuery("UPDATE dbo.Announcements SET IsPinned = 1 - IsPinned WHERE AnnId = @Id",
                p => p.AddWithValue("@Id", annId));
            AuditService.Log("AnnouncementPinned", "Announcements", annId);
        }

        public static void ToggleAnnouncement(int annId)
        {
            SqlHelper.ExecuteNonQuery("UPDATE dbo.Announcements SET IsActive = 1 - IsActive WHERE AnnId = @Id",
                p => p.AddWithValue("@Id", annId));
            AuditService.Log("AnnouncementVisibility", "Announcements", annId);
        }

        public static void DeleteAnnouncement(int annId)
        {
            SqlHelper.ExecuteNonQuery("DELETE FROM dbo.Announcements WHERE AnnId = @Id", p => p.AddWithValue("@Id", annId));
            AuditService.Log("AnnouncementDeleted", "Announcements", annId);
        }

        public static void AcknowledgeAnnouncement(int annId, int userId)
        {
            SqlHelper.ExecuteNonQuery(
                "IF NOT EXISTS (SELECT 1 FROM dbo.AnnouncementAcks WHERE AnnId=@Id AND UserId=@U) " +
                "INSERT INTO dbo.AnnouncementAcks (AnnId, UserId) VALUES (@Id, @U)",
                p => { p.AddWithValue("@Id", annId); p.AddWithValue("@U", userId); });
        }

        // ---------------- push subscriptions (stored; WC delivery config-gated) ----------------
        public static void UpsertPush(int userId, string endpoint, string p256dh, string auth, string ua)
        {
            bool exists = SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.PushSubscriptions WHERE Endpoint = @Endpoint",
                p => p.AddWithValue("@Endpoint", endpoint)) > 0;
            if (exists)
                SqlHelper.ExecuteNonQuery(
                    "UPDATE dbo.PushSubscriptions SET UserId = @UserId, P256dh = @P256dh, AuthSecret = @Auth, UserAgent = @Ua, IsExpired = 0 WHERE Endpoint = @Endpoint",
                    p =>
                    {
                        p.AddWithValue("@UserId", userId); p.AddWithValue("@P256dh", p256dh);
                        p.AddWithValue("@Auth", auth); p.AddWithValue("@Ua", ua ?? ""); p.AddWithValue("@Endpoint", endpoint);
                    });
            else
                SqlHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.PushSubscriptions (UserId, Endpoint, P256dh, AuthSecret, UserAgent) " +
                    "VALUES (@UserId, @Endpoint, @P256dh, @Auth, @Ua)",
                    p =>
                    {
                        p.AddWithValue("@UserId", userId); p.AddWithValue("@Endpoint", endpoint);
                        p.AddWithValue("@P256dh", p256dh); p.AddWithValue("@Auth", auth); p.AddWithValue("@Ua", ua ?? "");
                    });
            AuditService.Log("PushSubscribe", "PushSubscriptions", userId);
        }

        public static void DeletePush(int userId, string endpoint)
        {
            SqlHelper.ExecuteNonQuery(
                "DELETE FROM dbo.PushSubscriptions WHERE UserId = @UserId AND Endpoint = @Endpoint",
                p => { p.AddWithValue("@UserId", userId); p.AddWithValue("@Endpoint", endpoint); });
        }

        // ---------------- complaints ----------------
        public static DataTable ComplaintCategories()
        {
            return SqlHelper.FillDataTable(
                "SELECT CategoryId, CatName FROM dbo.ComplaintCategories WHERE IsActive = 1 ORDER BY CategoryId");
        }

        public static DataTable Complaints(int? raisedBy, int? assignedTo, string status, string term, int limit)
        {
            return Complaints(raisedBy, assignedTo, status, "", "", term, limit);
        }

        /// <summary>Filtered ticket list. Priority/category are optional; every filter is parameterised.</summary>
        public static DataTable Complaints(int? raisedBy, int? assignedTo, string status, string priority, string category, string term, int limit)
        {
            return SqlHelper.FillDataTable(
                "SELECT TOP (@Limit) cp.ComplaintId, cp.ComplaintNo, cp.RaisedBy, cp.CategoryId, cp.Subject, cp.Description, cp.Status, cp.Priority, " +
                "cp.CreatedAtUtc, cp.UpdatedAtUtc, cp.ResolvedAtUtc, cp.ResolutionNote, c.CatName, u.FullName AS RaisedByName, ua.FullName AS AssignedName " +
                "FROM dbo.Complaints cp " +
                "JOIN dbo.ComplaintCategories c ON c.CategoryId = cp.CategoryId " +
                "JOIN dbo.Users u ON u.UserId = cp.RaisedBy " +
                "LEFT JOIN dbo.Users ua ON ua.UserId = cp.AssignedTo " +
                "WHERE (@RaisedBy IS NULL OR cp.RaisedBy = @RaisedBy) " +
                "  AND (@AssignedTo IS NULL OR cp.AssignedTo = @AssignedTo) " +
                "  AND (@Status = '' OR cp.Status = @Status) " +
                "  AND (@Priority = '' OR cp.Priority = @Priority) " +
                "  AND (@Category = '' OR c.CatName = @Category) " +
                "  AND (@Term = '' OR cp.Subject LIKE '%' + @Term + '%' OR cp.ComplaintNo LIKE '%' + @Term + '%' OR cp.Description LIKE '%' + @Term + '%') " +
                "ORDER BY CASE cp.Priority WHEN 'urgent' THEN 1 WHEN 'high' THEN 2 WHEN 'normal' THEN 3 ELSE 4 END, cp.CreatedAtUtc DESC",
                p =>
                {
                    p.AddWithValue("@Limit", limit);
                    p.AddWithValue("@RaisedBy", (object)raisedBy ?? DBNull.Value);
                    p.AddWithValue("@AssignedTo", (object)assignedTo ?? DBNull.Value);
                    p.AddWithValue("@Status", status ?? "");
                    p.AddWithValue("@Priority", priority ?? "");
                    p.AddWithValue("@Category", category ?? "");
                    p.AddWithValue("@Term", term ?? "");
                });
        }

        public static DataTable ComplaintSummary()
        {
            return SqlHelper.FillDataTable(
                "SELECT COUNT(*) AS Total, " +
                "SUM(CASE WHEN Status IN ('open','assigned','in_progress','waiting','reopened') THEN 1 ELSE 0 END) AS OpenCount, " +
                "SUM(CASE WHEN Priority IN ('high','urgent') AND Status NOT IN ('resolved','closed') THEN 1 ELSE 0 END) AS Escalated, " +
                "SUM(CASE WHEN Status IN ('resolved','closed') THEN 1 ELSE 0 END) AS ClosedCount, " +
                "ISNULL(AVG(CASE WHEN ResolvedAtUtc IS NOT NULL THEN DATEDIFF(hour, CreatedAtUtc, ResolvedAtUtc) * 1.0 END), 0) AS AvgHours " +
                "FROM dbo.Complaints");
        }

        public static DataTable ComplaintEvents(int complaintId)
        {
            return SqlHelper.FillDataTable(
                "SELECT ev.EventId, ev.EventType, ev.OldValue, ev.NewValue, ev.CreatedAtUtc, u.FullName AS ActorName " +
                "FROM dbo.ComplaintEvents ev LEFT JOIN dbo.Users u ON u.UserId = ev.ActorId " +
                "WHERE ev.ComplaintId = @Id ORDER BY ev.EventId",
                p => p.AddWithValue("@Id", complaintId));
        }

        public static DataTable StaffForAssignment()
        {
            return SqlHelper.FillDataTable(
                "SELECT DISTINCT u.UserId, u.FullName, r.RoleName FROM dbo.Users u " +
                "JOIN dbo.UserRoles ur ON ur.UserId = u.UserId JOIN dbo.Roles r ON r.RoleId = ur.RoleId " +
                "WHERE u.IsActive = 1 AND r.RoleName IN ('Admin','Faculty') ORDER BY u.FullName");
        }

        private static void LogEvent(int complaintId, int actorId, string eventType, string oldValue, string newValue)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.ComplaintEvents (ComplaintId, ActorId, EventType, OldValue, NewValue) " +
                    "VALUES (@Id, @Actor, @Type, @Old, @New)",
                    p =>
                    {
                        p.AddWithValue("@Id", complaintId);
                        p.AddWithValue("@Actor", actorId > 0 ? (object)actorId : DBNull.Value);
                        p.AddWithValue("@Type", eventType);
                        p.AddWithValue("@Old", (object)oldValue ?? DBNull.Value);
                        p.AddWithValue("@New", (object)newValue ?? DBNull.Value);
                    });
            }
            catch (Exception ex) { Global.WriteLog("Complaint", "event log failed", ex); }
        }

        public static void AssignComplaint(int complaintId, int staffUserId, int actorId)
        {
            object old = SqlHelper.ExecuteScalar("SELECT AssignedTo FROM dbo.Complaints WHERE ComplaintId=@Id", p => p.AddWithValue("@Id", complaintId));
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Complaints SET AssignedTo=@Staff, Status=CASE WHEN Status='open' THEN 'assigned' ELSE Status END, UpdatedAtUtc=SYSUTCDATETIME() WHERE ComplaintId=@Id",
                p => { p.AddWithValue("@Staff", staffUserId); p.AddWithValue("@Id", complaintId); });
            LogEvent(complaintId, actorId, "assigned", old == null || old == DBNull.Value ? "unassigned" : old.ToString(), staffUserId.ToString());
            AuditService.Log("ComplaintAssigned", "Complaints", complaintId, "staff=" + staffUserId);
        }

        public static void SetComplaintPriority(int complaintId, string priority, int actorId)
        {
            if (priority != "low" && priority != "normal" && priority != "high" && priority != "urgent") return;
            object old = SqlHelper.ExecuteScalar("SELECT Priority FROM dbo.Complaints WHERE ComplaintId=@Id", p => p.AddWithValue("@Id", complaintId));
            SqlHelper.ExecuteNonQuery("UPDATE dbo.Complaints SET Priority=@P, UpdatedAtUtc=SYSUTCDATETIME() WHERE ComplaintId=@Id",
                p => { p.AddWithValue("@P", priority); p.AddWithValue("@Id", complaintId); });
            LogEvent(complaintId, actorId, "priority", old == null ? "" : old.ToString(), priority);
        }

        public static DataTable MyComplaints(int userId)
        {
            return Complaints(userId, null, "", "", "", "", 100);
        }

        public static DataRow OneComplaint(int complaintId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT cp.*, c.CatName, u.FullName AS RaisedByName, u.Email AS RaisedByEmail, ua.FullName AS AssignedName " +
                "FROM dbo.Complaints cp " +
                "JOIN dbo.ComplaintCategories c ON c.CategoryId = cp.CategoryId " +
                "JOIN dbo.Users u ON u.UserId = cp.RaisedBy " +
                "LEFT JOIN dbo.Users ua ON ua.UserId = cp.AssignedTo " +
                "WHERE cp.ComplaintId = @Id",
                p => p.AddWithValue("@Id", complaintId));
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public static DataTable ComplaintThread(int complaintId)
        {
            return SqlHelper.FillDataTable(
                "SELECT t.ThreadId, t.AuthorId, t.Message, t.IsInternal, t.CreatedAtUtc, u.FullName AS AuthorName, r.RoleName AS AuthorRole " +
                "FROM dbo.ComplaintThreads t " +
                "JOIN dbo.Users u ON u.UserId = t.AuthorId " +
                "LEFT JOIN dbo.UserRoles ur ON ur.UserId = u.UserId " +
                "LEFT JOIN dbo.Roles r ON r.RoleId = ur.RoleId " +
                "WHERE t.ComplaintId = @Id ORDER BY t.CreatedAtUtc",
                p => p.AddWithValue("@Id", complaintId));
        }

        public static int CreateComplaint(int userId, int categoryId, string subject, string description)
        {
            string complaintNo = new ComplaintNumber().Next();
            int id = SqlHelper.ExecuteScalarInt(
                "INSERT INTO dbo.Complaints (ComplaintNo, RaisedBy, CategoryId, Subject, Description) " +
                "OUTPUT INSERTED.ComplaintId VALUES (@No, @RaisedBy, @Category, @Subject, @Description)",
                p =>
                {
                    p.AddWithValue("@No", complaintNo);
                    p.AddWithValue("@RaisedBy", userId);
                    p.AddWithValue("@Category", categoryId);
                    p.AddWithValue("@Subject", subject);
                    p.AddWithValue("@Description", description);
                });
            AuditService.Log("ComplaintCreated", "Complaints", id, complaintNo);
            return id;
        }

        public static void AddThread(int complaintId, int authorId, string message, bool internalNote)
        {
            SqlHelper.ExecuteNonQuery(
                "INSERT INTO dbo.ComplaintThreads (ComplaintId, AuthorId, Message, IsInternal) " +
                "VALUES (@ComplaintId, @AuthorId, @Message, @Internal); " +
                "UPDATE dbo.Complaints SET UpdatedAtUtc = SYSUTCDATETIME() WHERE ComplaintId = @ComplaintId",
                p =>
                {
                    p.AddWithValue("@ComplaintId", complaintId);
                    p.AddWithValue("@AuthorId", authorId);
                    p.AddWithValue("@Message", message);
                    p.AddWithValue("@Internal", internalNote ? 1 : 0);
                });

            // Tell the student when the office answers (never for internal staff notes).
            if (internalNote) return;
            try
            {
                DataRow c = OneComplaint(complaintId);
                if (c == null) return;
                int raisedBy = Convert.ToInt32(c["RaisedBy"]);
                if (raisedBy == authorId) return;
                CreateNotification("Reply on ticket " + c["ComplaintNo"],
                    "A new reply was added to your ticket \"" + c["Subject"] + "\".",
                    "complaint", "/Student/Complaint.aspx?id=" + complaintId,
                    0, "user:" + raisedBy, complaintId);
            }
            catch (Exception) { /* notification is best-effort; the reply is already stored */ }
        }

        /// <summary>Badge CSS class for a complaint status (shared by admin/faculty/student views).</summary>
        public static string StatusClass(string status)
        {
            switch (status)
            {
                case "open": return "badge-warn";
                case "in_progress": return "badge-info";
                case "resolved": return "badge-ok";
                case "closed": return "badge-off";
                default: return "badge-off";
            }
        }

        public static void UpdateComplaint(int complaintId, string status, int? assignedTo)
        {
            UpdateComplaint(complaintId, status, assignedTo, null, 0);
        }

        /// <summary>
        /// Applies a status (and optional assignee/resolution note) and records the
        /// transition in ComplaintEvents. Reopening clears ResolvedAtUtc.
        /// </summary>
        public static void UpdateComplaint(int complaintId, string status, int? assignedTo, string resolutionNote, int actorId)
        {
            string[] allowed = { "open", "assigned", "in_progress", "waiting", "resolved", "closed", "reopened" };
            if (Array.IndexOf(allowed, status) < 0) throw new Exception("Unknown complaint status.");
            DataRow before = OneComplaint(complaintId);
            if (before == null) throw new Exception("Complaint not found.");

            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Complaints SET Status = @Status, " +
                "AssignedTo = CASE WHEN @AssignedTo IS NULL THEN AssignedTo ELSE @AssignedTo END, " +
                "ResolutionNote = CASE WHEN @Note = '' THEN ResolutionNote ELSE @Note END, " +
                "UpdatedAtUtc = SYSUTCDATETIME(), " +
                "ResolvedAtUtc = CASE WHEN @Status IN ('resolved','closed') THEN SYSUTCDATETIME() ELSE NULL END " +
                "WHERE ComplaintId = @Id",
                p =>
                {
                    p.AddWithValue("@Status", status);
                    p.AddWithValue("@AssignedTo", (object)assignedTo ?? DBNull.Value);
                    p.AddWithValue("@Note", resolutionNote ?? "");
                    p.AddWithValue("@Id", complaintId);
                });

            string oldStatus = Convert.ToString(before["Status"]);
            if (oldStatus != status) LogEvent(complaintId, actorId, status == "reopened" ? "reopened" : "status", oldStatus, status);
            if (!string.IsNullOrEmpty(resolutionNote)) LogEvent(complaintId, actorId, "note", null, resolutionNote);
            AuditService.Log("ComplaintUpdated", "Complaints", complaintId, status);

            int raisedBy = Convert.ToInt32(before["RaisedBy"]);
            if (actorId != raisedBy)
            {
                try
                {
                    CreateNotification("Ticket " + before["ComplaintNo"] + " is now " + status.Replace('_', ' '),
                        "Your complaint \"" + before["Subject"] + "\" changed status to " + status + ".",
                        "complaint", "/Student/Complaint.aspx?id=" + complaintId, 0, "user:" + raisedBy, complaintId);
                }
                catch (Exception ex) { Global.WriteLog("Complaint", "status notification failed", ex); }
            }
        }

        // ---------------- chat logs / FAQ ----------------
        public static void LogChat(int? userId, string role, string question, string answer, string source)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.ChatLogs (UserId, UserRole, Question, Answer, Source) " +
                    "VALUES (@UserId, @Role, @Question, @Answer, @Source)",
                    p =>
                    {
                        p.AddWithValue("@UserId", (object)userId ?? DBNull.Value);
                        p.AddWithValue("@Role", (object)role ?? DBNull.Value);
                        p.AddWithValue("@Question", question);
                        p.AddWithValue("@Answer", (object)answer ?? DBNull.Value);
                        p.AddWithValue("@Source", source);
                    });
            }
            catch (Exception) { /* chat log must never break chat */ }
        }

        public static string FindFaq(string keywords, string role)
        {
            string score1 = null;
            DataTable faqs = SqlHelper.FillDataTable(
                "SELECT Question, Answer, Keywords, RoleScope FROM dbo.FrequentlyAsked WHERE IsActive = 1 ORDER BY SortOrder");
            string kws = (keywords ?? "").ToLowerInvariant();
            foreach (DataRow row in faqs.Rows)
            {
                // Seed rows store the scope lowercase (student/faculty/all), so the
                // comparison must ignore case; keywords are comma-separated.
                string scope = (row["RoleScope"] as string ?? "all").Trim();
                if (!string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(scope, role, StringComparison.OrdinalIgnoreCase)) continue;
                string keys = ((row["Keywords"] as string) ?? "").ToLowerInvariant();
                if (keys.Length > 0 && kws.Length > 0)
                {
                    foreach (string raw in keys.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string k = raw.Trim();
                        if (k.Length > 2 && kws.Contains(k))
                        {
                            score1 = row["Answer"] as string;
                            break;
                        }
                    }
                }
                if (score1 != null) break;
            }
            return score1;
        }

        /// <summary>builds complaint numbers like CMP-2026-0042 (year, then sequence)</summary>
        private class ComplaintNumber
        {
            public string Next()
            {
                int year = DateTime.UtcNow.Year;
                int next = SqlHelper.ExecuteScalarInt(
                    "SELECT ISNULL(MAX(CAST(RIGHT(ComplaintNo, 4) AS INT)), 0) + 1 " +
                    "FROM dbo.Complaints WHERE ComplaintNo LIKE 'CMP-" + year + "-%';");
                return string.Format("CMP-{0}-{1:D4}", year, next);
            }
        }

        public static DataTable FaqForAdmin()
        {
            return SqlHelper.FillDataTable(
                "SELECT FaqId, Question, Answer, Keywords, RoleScope, SortOrder, IsActive FROM dbo.FrequentlyAsked ORDER BY SortOrder");
        }

        public static void UpsertFaq(int faqId, string question, string answer, string keywords, string roleScope, int sortOrder)
        {
            if (faqId > 0)
                SqlHelper.ExecuteNonQuery(
                    "UPDATE dbo.FrequentlyAsked SET Question = @Q, Answer = @A, Keywords = @K, RoleScope = @S, SortOrder = @O WHERE FaqId = @FaqId",
                    p =>
                    {
                        p.AddWithValue("@Q", question); p.AddWithValue("@A", answer); p.AddWithValue("@K", keywords ?? "");
                        p.AddWithValue("@S", roleScope ?? "all"); p.AddWithValue("@O", sortOrder); p.AddWithValue("@FaqId", faqId);
                    });
            else
                SqlHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.FrequentlyAsked (Question, Answer, Keywords, RoleScope, SortOrder) VALUES (@Q, @A, @K, @S, @O)",
                    p =>
                    {
                        p.AddWithValue("@Q", question); p.AddWithValue("@A", answer); p.AddWithValue("@K", keywords ?? "");
                        p.AddWithValue("@S", roleScope ?? "all"); p.AddWithValue("@O", sortOrder);
                    });
        }

        public static void ToggleFaq(int faqId)
        {
            SqlHelper.ExecuteNonQuery("UPDATE dbo.FrequentlyAsked SET IsActive = 1 - IsActive WHERE FaqId = @Id",
                p => p.AddWithValue("@Id", faqId));
        }
    }
}
