using System;
using System.Data;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>
    /// Opens one notification: verifies the row belongs to the signed-in user,
    /// marks it read, then redirects to its destination.
    ///
    /// Destination safety: ActionUrl comes from the database, but it is still
    /// treated as untrusted. Only a single leading slash, no backslash, no
    /// scheme/host and no protocol-relative form is accepted, and the path must
    /// be inside the caller's own role area (or the shared/account pages).
    /// Anything else falls back to that role's inbox instead of redirecting.
    /// </summary>
    public class NotifyGo : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            if (!CurrentUser.IsAuthenticated)
            {
                ctx.Response.Redirect("~/Account/Login.aspx?ReturnUrl=" +
                    HttpUtility.UrlEncode(ctx.Request.RawUrl), true);
                return;
            }

            int userId = CurrentUser.UserId;
            string role = CurrentUser.RoleName;
            string fallback = InboxFor(role);

            int id;
            if (!int.TryParse(ctx.Request.QueryString["id"], out id) || id <= 0)
            {
                ctx.Response.Redirect(fallback, true);
                return;
            }

            DataRow row = CommsRepo.GetForUser(id, userId);
            if (row == null)
            {
                // Not yours (or gone). Never reveal which of the two it was.
                ctx.Response.StatusCode = 404;
                ctx.Response.Redirect(fallback + (fallback.Contains("?") ? "&" : "?") + "err=" +
                    HttpUtility.UrlEncode("That notification is not available for this account."), true);
                return;
            }

            try { CommsRepo.MarkRead(id, userId); }
            catch (Exception) { /* reading it must still work if the update fails */ }

            string target = SafeTarget(row["ActionUrl"] as string, role) ?? fallback;
            ctx.Response.Redirect(target, true);
        }

        private static string InboxFor(string role)
        {
            switch (role)
            {
                case "Admin": return "/Notifications/Index.aspx";
                case "Faculty": return "/Notifications/Index.aspx";
                default: return "/Student/Notifications.aspx";
            }
        }

        /// <summary>Returns a safe relative path or null.</summary>
        internal static string SafeTarget(string url, string role)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            url = url.Trim();

            if (!url.StartsWith("/", StringComparison.Ordinal)) return null;
            if (url.StartsWith("//", StringComparison.Ordinal)) return null;
            if (url.IndexOf('\\') >= 0) return null;
            if (url.IndexOf("://", StringComparison.Ordinal) >= 0) return null;
            if (url.IndexOfAny(new[] { '\r', '\n', '\t', '"', '\'' }) >= 0) return null;

            string path = url.Split('?')[0];
            if (!path.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".ashx", StringComparison.OrdinalIgnoreCase)) return null;

            if (path.StartsWith("/Notifications/", StringComparison.OrdinalIgnoreCase)) return url;
            if (path.StartsWith("/Account/", StringComparison.OrdinalIgnoreCase)) return url;

            switch (role)
            {
                case "Admin":
                    if (path.StartsWith("/Admin/", StringComparison.OrdinalIgnoreCase)) return url;
                    break;
                case "Faculty":
                    if (path.StartsWith("/Faculty/", StringComparison.OrdinalIgnoreCase)) return url;
                    break;
                default:
                    if (path.StartsWith("/Student/", StringComparison.OrdinalIgnoreCase)) return url;
                    break;
            }
            return null;
        }
    }
}
