using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Integrations;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Admin
{
    /// <summary>In-app notifications broadcast (course/all/role), optional email delivery.</summary>
    public partial class Notifications : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, RecentTable, CourseOptions;

        protected bool EmailEnabled
        {
            get { return AppConfig.EmailEnabled; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            BuildRecent();
            CourseOptions = Options("CourseId", "CourseName", AcademicsRepo.Courses(), "");
        }

        private void HandlePost()
        {
            string error = null;
            try
            {
                if (Request.Form["op"] != "send") throw new Exception("Unknown operation.");
                string audience = (Request.Form["audience"] ?? "all").Trim();
                if (audience == "course:")
                    audience = "course:" + int.Parse(Request.Form["courseId"]);

                string title = (Request.Form["title"] ?? "").Trim();
                string message = (Request.Form["message"] ?? "").Trim();
                string ntype = (Request.Form["ntype"] ?? "general").Trim();
                string actionUrl = (Request.Form["actionUrl"] ?? "").Trim();
                if (title.Length == 0 || message.Length == 0) throw new Exception("Title and message are required.");

                int notifId = CommsRepo.CreateNotification(title, message, ntype,
                    actionUrl.Length > 0 ? actionUrl : null, CurrentUser.UserId, audience);

                int emailed = 0;
                if (Request.Form["alsoEmail"] == "1" && AppConfig.EmailEnabled)
                    emailed = EmailCopies(notifId);

                Flash("success", "Notification #" + notifId + " delivered in-app."
                    + (emailed > 0 ? " " + emailed + " email copies queued/sent." : ""));
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }

            Redirect("/Admin/Notifications.aspx" + (error != null ? "?err=" + HttpUtility.UrlEncode(error) : ""));
        }

        private int EmailCopies(int notifId)
        {
            DataTable meta = SqlHelper.FillDataTable(
                "SELECT Title, Message FROM dbo.Notifications WHERE NotificationId = @Nid",
                p => p.AddWithValue("@Nid", notifId));
            DataRow n = meta.Rows.Count > 0 ? meta.Rows[0] : null;
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT u.Email, u.FullName FROM dbo.NotificationRecipients nr JOIN dbo.Users u ON u.UserId = nr.UserId " +
                "WHERE nr.NotificationId = @Nid",
                p => p.AddWithValue("@Nid", notifId));
            var mail = new SmtpMailService();
            string subject = "Student Management System - " + (n != null ? (n["Title"] as string ?? "") : "Notification");
            string body =
                "<div style='font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:0 auto;'>" +
                "<h2 style='color:#1a2949'>" + System.Web.HttpUtility.HtmlEncode(n != null ? n["Title"] as string : "") + "</h2>" +
                "<p style='white-space:pre-line;color:#2b3a55'>" + System.Web.HttpUtility.HtmlEncode(n != null ? n["Message"] as string : "") + "</p>" +
                "<p class='small' style='color:#6c757d'>You receive this because you are enrolled as a user of the Student Management System.</p></div>";
            int sent = 0;
            foreach (DataRow r in dt.Rows)
            {
                string email = (r["Email"] as string ?? "").Trim();
                if (email.Length == 0) continue;
                if (mail.Send(email, subject, body)) sent++;
            }
            return sent;
        }

        private void BuildRecent()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP 40 n.NotificationId, n.Title, n.NType, n.CreatedAtUtc, u.FullName AS AuthorName, " +
                "(SELECT COUNT(*) FROM dbo.NotificationRecipients nr WHERE nr.NotificationId = n.NotificationId) AS Recipients, " +
                "(SELECT COUNT(*) FROM dbo.NotificationRecipients nr2 WHERE nr2.NotificationId = n.NotificationId AND nr2.IsRead = 1) AS Reads " +
                "FROM dbo.Notifications n LEFT JOIN dbo.Users u ON u.UserId = n.CreatedBy " +
                "ORDER BY n.CreatedAtUtc DESC");
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Title</th><th>Type</th><th>Sent to</th><th>Read</th><th>By</th><th>When</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>Nothing sent yet.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                sb.Append("<tr><td><b>" + E(r["Title"]) + "</b></td><td><span class='badge badge-info'>" + E(r["NType"]) + "</span></td>");
                sb.Append("<td>" + E(r["Recipients"]) + "</td><td>" + E(r["Reads"]) + "</td>");
                sb.Append("<td>" + E(r["AuthorName"]) + "</td>");
                sb.Append("<td class='small'>" + ((DateTime)r["CreatedAtUtc"]).ToString("yyyy-MM-dd HH:mm") + "</td></tr>");
            }
            sb.Append("</tbody></table>");
            RecentTable = sb.ToString();
        }
    }
}
