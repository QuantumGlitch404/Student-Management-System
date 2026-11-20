using System;
using System.Web;
using System.Web.Security;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Security
{
    /// <summary>State carried between "we just sent a code" and the Verify page.</summary>
    public class PendingOtpState
    {
        public const string SessionKey = "PendingOtpState";

        public int UserId;
        public string Purpose;    // signup | login
        public string Channel;    // email
        public string Email;
        public string FullName;
        public DateTime CreatedUtc;
    }

    /// <summary>
    /// Issues the Forms Authentication ticket. Cookie contract:
    ///   Name = FullName, UserData = "userId|roleName".
    /// The session timeout is aligned with App:SessionTimeoutMinutes.
    /// </summary>
    public static class AuthTicket
    {
        public static void Issue(UserRecord user, bool rememberMe)
        {
            int minutes = AppConfig.GetInt("SessionTimeoutMinutes", 60);
            object stamp = SqlHelper.ExecuteScalar("SELECT AuthStamp FROM dbo.Users WHERE UserId = @Id",
                p => p.AddWithValue("@Id", user.UserId));
            var ticket = new FormsAuthenticationTicket(
                version: 3,
                name: user.FullName,
                issueDate: DateTime.Now,
                expiration: DateTime.Now.AddMinutes(minutes),
                isPersistent: rememberMe,
                userData: user.UserId + "|" + user.RoleName + "|" + Convert.ToString(stamp),
                cookiePath: FormsAuthentication.FormsCookiePath);

            string encrypted = FormsAuthentication.Encrypt(ticket);
            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted)
            {
                HttpOnly = true,
                Secure = HttpContext.Current.Request.IsSecureConnection,
                Path = FormsAuthentication.FormsCookiePath,
                SameSite = SameSiteMode.Lax
            };
            if (rememberMe) cookie.Expires = ticket.Expiration;

            HttpContext.Current.Response.Cookies.Add(cookie);
        }

        /// <summary>Default landing page for a role; callers still honour ReturnUrl.</summary>
        public static string DashboardFor(string roleName)
        {
            switch (roleName)
            {
                case "Admin": return "~/Admin/Index.aspx";
                case "Faculty": return "~/Faculty/Index.aspx";
                case "Student": return "~/Student/Index.aspx";
                default: return "~/Account/Login.aspx";
            }
        }

        public static void Clear()
        {
            FormsAuthentication.SignOut();
            var ctx = HttpContext.Current;
            if (ctx != null && ctx.Session != null) { ctx.Session.Clear(); ctx.Session.Abandon(); }

            var expired = new HttpCookie(FormsAuthentication.FormsCookieName, string.Empty)
            {
                Expires = DateTime.Now.AddDays(-1),
                Path = FormsAuthentication.FormsCookiePath,
                HttpOnly = true
            };
            if (ctx != null) ctx.Response.Cookies.Add(expired);
        }

        /// <summary>View-safe redirect guard: accepts only app-local URLs.</summary>
        public static bool IsLocalUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            return (url.StartsWith("/") && !url.StartsWith("//") && !url.StartsWith("/\\")) ||
                   (url.StartsWith("~/") && !url.StartsWith("~//"));
        }
    }
}
