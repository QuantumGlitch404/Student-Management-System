# -*- coding: utf-8 -*-
import io

p = "StudentManagementSystem/Code/Data/CommsRepo.cs"
s = io.open(p, encoding="utf-8-sig").read()
start = s.index("        // ---------------- announcements ----------------")
end = s.index("        // ---------------- push subscriptions")

new = u'''        // ---------------- announcements ----------------
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

'''
s = s[:start] + new + s[end:]
io.open(p, "w", encoding="utf-8-sig").write(s)
print("comms repo announcements replaced")
