using System;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Lib
{
    /// <summary>
    /// Audit trail. Records who did what and when (never passwords/OTPs).
    /// Insert failures are swallowed - auditing must never break a transaction
    /// that already succeeded for the user.
    /// </summary>
    public static class AuditService
    {
        public static void Log(string action, string entityTable = null, int? entityId = null, string detailJson = null)
        {
            Log(action, entityTable, entityId, detailJson, null, null, null, null);
        }

        /// <summary>Readable audit row with category, the affected person and before/after values.</summary>
        public static void Log(string action, string entityTable, int? entityId, string detailJson,
            string category, int? targetUserId, string oldValue, string newValue)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.AuditLogs (UserId, Action, EntityTable, EntityId, DetailJson, ClientIp, Category, TargetUserId, OldValue, NewValue) " +
                    "VALUES (@UserId, @Action, @EntityTable, @EntityId, @DetailJson, @ClientIp, @Category, @TargetUserId, @OldValue, @NewValue)",
                    p =>
                    {
                        p.AddWithValue("@UserId", CurrentUser.UserId > 0 ? (object)CurrentUser.UserId : DBNull.Value);
                        p.AddWithValue("@Action", action ?? "unknown");
                        p.AddWithValue("@EntityTable", (object)entityTable ?? DBNull.Value);
                        p.AddWithValue("@EntityId", (object)entityId ?? DBNull.Value);
                        p.AddWithValue("@DetailJson", (object)detailJson ?? DBNull.Value);
                        p.AddWithValue("@ClientIp", HttpUtility.HtmlEncode(IpAddress()));
                        p.AddWithValue("@Category", (object)(string.IsNullOrEmpty(category) ? CategoryFor(action) : category) ?? DBNull.Value);
                        p.AddWithValue("@TargetUserId", (object)targetUserId ?? DBNull.Value);
                        p.AddWithValue("@OldValue", (object)Trim(oldValue) ?? DBNull.Value);
                        p.AddWithValue("@NewValue", (object)Trim(newValue) ?? DBNull.Value);
                    });
            }
            catch (Exception ex) { Global.WriteLog("Audit", "insert failed", ex); }
        }

        private static string Trim(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length > 400 ? value.Substring(0, 397) + "..." : value;
        }

        private static string CategoryFor(string action)
        {
            string a = (action ?? "").ToLowerInvariant();
            if (a.Contains("login") || a.Contains("password") || a.Contains("2fa") || a.Contains("otp") || a.Contains("security") || a.Contains("role")) return "Account";
            if (a.Contains("profile") || a.Contains("student") || a.Contains("faculty") || a.Contains("user") || a.Contains("email")) return "Profile";
            if (a.Contains("fee") || a.Contains("payment") || a.Contains("receipt")) return "Fees";
            if (a.Contains("timetable") || a.Contains("attendance") || a.Contains("mark") || a.Contains("exam") || a.Contains("test")) return "Academics";
            if (a.Contains("mail") || a.Contains("announcement") || a.Contains("notification")) return "Mail";
            if (a.Contains("leave") || a.Contains("complaint")) return "Requests";
            return "Account";
        }

        public static void LogLoginFailed(string email)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.AuditLogs (UserId, Action, EntityTable, ClientIp) VALUES (NULL, 'LoginFailed', 'Users', @ClientIp); " +
                    "UPDATE dbo.Users SET FailedLoginCount = FailedLoginCount + 1, LockoutEndUtc = CASE WHEN FailedLoginCount + 1 >= @MaxFailures THEN DATEADD(MINUTE, @LockMinutes, SYSUTCDATETIME()) ELSE LockoutEndUtc END WHERE Email = @Email;",
                    p =>
                    {
                        p.AddWithValue("@MaxFailures", AppConfig.GetInt("Login:LockoutAfterFailures", 5));
                        p.AddWithValue("@LockMinutes", AppConfig.GetInt("Login:LockoutMinutes", 15));
                        p.AddWithValue("@Email", email);
                        p.AddWithValue("@ClientIp", IpAddress());
                    });
            }
            catch (Exception ex) { Global.WriteLog("Audit", "login-fail log failed", ex); }
        }

        public static void LogSystemError(Exception ex, string path)
        {
            Global.WriteLog("PageError", path, ex);
        }

        public static string IpAddress()
        {
            var req = System.Web.HttpContext.Current?.Request;
            if (req == null) return "unknown";
            var forwarded = req.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (!string.IsNullOrEmpty(forwarded)) return forwarded.Split(',')[0].Trim();
            return req.UserHostAddress ?? "unknown";
        }
    }
}
