using System;
using System.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Data
{
    /// <summary>Personal reminders. Due times are stored in UTC and converted
    /// from the user's IANA timezone when created.</summary>
    public static class ReminderRepo
    {
        public static DataTable Upcoming(int userId, int limit = 50)
        {
            return SqlHelper.FillDataTable(
                "SELECT TOP (@Take) ReminderId, Title, Body, ActionUrl, DueAtUtc, RepeatRule, Status, SentAtUtc, SnoozedUntilUtc " +
                "FROM dbo.Reminders WHERE UserId = @UserId ORDER BY " +
                "CASE WHEN Status = 'pending' THEN 0 ELSE 1 END, " +
                "CASE WHEN Status = 'pending' THEN DueAtUtc END ASC, DueAtUtc DESC",
                p =>
                {
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@Take", limit < 1 ? 20 : (limit > 100 ? 100 : limit));
                });
        }

        public static int Create(int userId, string title, string body, string actionUrl,
            DateTime localDue, string zoneId, string repeatRule, int leadMinutes)
        {
            title = (title ?? "").Trim();
            if (title.Length == 0) throw new Exception("A reminder needs a title.");
            if (title.Length > 160) title = title.Substring(0, 160);
            body = (body ?? "").Trim();
            if (body.Length > 1000) body = body.Substring(0, 1000);
            actionUrl = SafeUrl(actionUrl);
            repeatRule = NormalizeRepeat(repeatRule);
            leadMinutes = PrefsRepo.NormalizeLead(leadMinutes);

            DateTime dueUtc = Fmt.ToUtc(localDue, zoneId).AddMinutes(-leadMinutes);
            if (dueUtc <= DateTime.UtcNow.AddMinutes(-1))
                throw new Exception("The reminder time must be in the future.");

            return SqlHelper.ExecuteScalarInt(
                "INSERT dbo.Reminders (UserId, Title, Body, ActionUrl, DueAtUtc, RepeatRule, Status, UniqueKey) " +
                "VALUES (@UserId, @Title, @Body, @ActionUrl, @DueAtUtc, @Repeat, 'pending', @UniqueKey); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                p =>
                {
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@Title", title);
                    p.AddWithValue("@Body", body.Length == 0 ? (object)DBNull.Value : body);
                    p.AddWithValue("@ActionUrl", actionUrl == null ? (object)DBNull.Value : actionUrl);
                    p.AddWithValue("@DueAtUtc", dueUtc);
                    p.AddWithValue("@Repeat", repeatRule);
                    p.AddWithValue("@UniqueKey", "reminder:" + Guid.NewGuid().ToString("N"));
                });
        }

        public static bool Cancel(int reminderId, int userId)
        {
            return SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Reminders SET Status = 'cancelled' " +
                "WHERE ReminderId = @Id AND UserId = @UserId AND Status = 'pending'",
                p => { p.AddWithValue("@Id", reminderId); p.AddWithValue("@UserId", userId); }) > 0;
        }

        public static string NormalizeRepeat(string value)
        {
            string v = (value ?? "").Trim().ToLowerInvariant();
            return v == "daily" || v == "weekly" || v == "monthly" ? v : "none";
        }

        private static string SafeUrl(string value)
        {
            string v = (value ?? "").Trim();
            if (v.Length == 0) return null;
            if (!v.StartsWith("/", StringComparison.Ordinal) || v.StartsWith("//", StringComparison.Ordinal)) return null;
            if (v.Length > 300) v = v.Substring(0, 300);
            return v;
        }
    }
}
