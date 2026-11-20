using System;
using System.Data;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Integrations
{
    /// <summary>
    /// Drains dbo.NotificationOutbox rows of channel 'email' through SmtpMailService.
    ///
    /// Contract:
    ///  * nothing is sent when Email:Enabled is false or SMTP is unconfigured - rows
    ///    simply stay queued, and the UI never claims an email was delivered;
    ///  * a row is marked 'sent' only when the SMTP server accepted every recipient;
    ///  * failures are retried (Attempts &lt; 3, 5-minute backoff) and then marked 'failed'
    ///    with LastError, so a broken SMTP setup is visible instead of silent;
    ///  * the flush is best-effort and never throws into the page that created the
    ///    notification.
    ///
    /// Called after a notification is committed, and can also be called from a
    /// scheduled task (sqlcmd/PowerShell) to drain older rows.
    /// </summary>
    public static class NotificationDispatcher
    {
        private const int MaxAttempts = 3;

        public static int FlushQueued(int max)
        {
            if (max <= 0) return 0;
            if (!AppConfig.EmailEnabled) return 0;

            var mail = new SmtpMailService();
            if (!mail.IsConfigured) return 0;

            DataTable due;
            try
            {
                due = SqlHelper.FillDataTable(
                    "SELECT TOP (@Max) OutboxId, NotificationId, Attempts " +
                    "FROM dbo.NotificationOutbox " +
                    "WHERE Channel = 'email' AND Status = 'queued' AND Attempts < @MaxAttempts " +
                    "  AND (NextAttemptAtUtc IS NULL OR NextAttemptAtUtc <= SYSUTCDATETIME()) " +
                    "ORDER BY OutboxId",
                    p =>
                    {
                        p.AddWithValue("@Max", max);
                        p.AddWithValue("@MaxAttempts", MaxAttempts);
                    });
            }
            catch (Exception ex)
            {
                Global.WriteLog("Notify", "outbox read failed", ex);
                return 0;
            }

            int sent = 0;
            foreach (DataRow row in due.Rows)
            {
                int outboxId = Convert.ToInt32(row["OutboxId"]);
                int notificationId = Convert.ToInt32(row["NotificationId"]);
                int attempts = Convert.ToInt32(row["Attempts"]);

                try
                {
                    DataRow note = OneNotification(notificationId);
                    if (note == null)
                    {
                        // Notification was deleted; nothing to deliver.
                        MarkSent(outboxId);
                        continue;
                    }

                    DataTable recipients = Recipients(notificationId);
                    if (recipients.Rows.Count == 0)
                    {
                        MarkSent(outboxId);
                        continue;
                    }

                    string title = Convert.ToString(note["Title"]);
                    string message = Convert.ToString(note["Message"]);
                    string actionUrl = note["ActionUrl"] == DBNull.Value ? null : Convert.ToString(note["ActionUrl"]);

                    bool anyFailed = false;
                    string lastError = null;

                    foreach (DataRow recipient in recipients.Rows)
                    {
                        string email = Convert.ToString(recipient["Email"]);
                        if (string.IsNullOrWhiteSpace(email)) continue;

                        string html = BuildHtml(Convert.ToString(recipient["FullName"]), title, message, actionUrl);
                        if (!mail.Send(email, title, html))
                        {
                            anyFailed = true;
                            lastError = "SMTP did not accept the message for one or more recipients";
                        }
                    }

                    if (anyFailed) MarkFailed(outboxId, attempts + 1, lastError);
                    else { MarkSent(outboxId); sent++; }
                }
                catch (Exception ex)
                {
                    Global.WriteLog("Notify", "flush failed for outbox " + outboxId, ex);
                    MarkFailed(outboxId, attempts + 1, ex.Message);
                }
            }

            return sent;
        }

        private static DataRow OneNotification(int notificationId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT Title, Message, ActionUrl FROM dbo.Notifications WHERE NotificationId = @Id",
                p => p.AddWithValue("@Id", notificationId));
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        private static DataTable Recipients(int notificationId)
        {
            return SqlHelper.FillDataTable(
                "SELECT u.Email, u.FullName FROM dbo.NotificationRecipients r " +
                "JOIN dbo.Users u ON u.UserId = r.UserId " +
                "WHERE r.NotificationId = @Id AND u.IsActive = 1 AND u.Email IS NOT NULL",
                p => p.AddWithValue("@Id", notificationId));
        }

        private static void MarkSent(int outboxId)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.NotificationOutbox SET Status = 'sent', SentAtUtc = SYSUTCDATETIME(), " +
                "LastError = NULL, NextAttemptAtUtc = NULL WHERE OutboxId = @Id",
                p => p.AddWithValue("@Id", outboxId));
        }

        private static void MarkFailed(int outboxId, int attempts, string error)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.NotificationOutbox SET Attempts = @Attempts, " +
                "Status = CASE WHEN @Attempts >= @MaxAttempts THEN 'failed' ELSE 'queued' END, " +
                "LastError = LEFT(@Err, 500), " +
                "NextAttemptAtUtc = DATEADD(MINUTE, 5 * @Attempts, SYSUTCDATETIME()) " +
                "WHERE OutboxId = @Id",
                p =>
                {
                    p.AddWithValue("@Attempts", attempts);
                    p.AddWithValue("@MaxAttempts", MaxAttempts);
                    p.AddWithValue("@Err", (object)error ?? DBNull.Value);
                    p.AddWithValue("@Id", outboxId);
                });
        }

        private static string BuildHtml(string fullName, string title, string message, string actionUrl)
        {
            string app = AppConfig.Get("AppTitle", "Student Management System");
            string baseUrl = AppConfig.Get("App:BaseUrl", "");
            string html = "<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto;padding:24px\">" +
                          "<h2 style=\"margin:0 0 6px;color:#1a2949\">" + HttpUtility.HtmlEncode(app) + "</h2>" +
                          "<p style=\"color:#444\">Hello " + HttpUtility.HtmlEncode(string.IsNullOrEmpty(fullName) ? "user" : fullName) + ",</p>" +
                          "<h3 style=\"margin:16px 0 6px;color:#12405e\">" + HttpUtility.HtmlEncode(title ?? "") + "</h3>" +
                          "<p style=\"color:#444;white-space:pre-line\">" + HttpUtility.HtmlEncode(message ?? "") + "</p>";

            if (!string.IsNullOrEmpty(actionUrl))
            {
                string link = actionUrl;
                if (!link.StartsWith("http", StringComparison.OrdinalIgnoreCase) && baseUrl.Length > 0)
                    link = baseUrl.TrimEnd('/') + "/" + link.TrimStart('/');

                html += "<p><a href=\"" + HttpUtility.HtmlAttributeEncode(link) +
                        "\" style=\"background:#12405e;color:#fff;padding:10px 16px;border-radius:6px;text-decoration:none\">Open in the portal</a></p>";
            }

            html += "<p style=\"color:#8a94a6;font-size:12px;margin-top:24px\">This message was sent by " +
                    HttpUtility.HtmlEncode(app) + ". Do not reply to this address.</p></div>";
            return html;
        }
    }
}
