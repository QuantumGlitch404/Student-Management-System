using System;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>
    /// Raises one deduplicated request per 24 h so an administrator completes a
    /// blank student/faculty profile. Never exposes another user's data.
    /// </summary>
    public class ProfileNotify : IHttpHandler, System.Web.SessionState.IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            string returnTo = SafeReturn(ctx.Request.Form["return"] ?? ctx.Request.QueryString["return"]);
            if (!CurrentUser.IsAuthenticated)
            {
                ctx.Response.Redirect("~/Account/Login.aspx", true);
                return;
            }
            if (ctx.Request.HttpMethod != "POST")
            {
                ctx.Response.StatusCode = 405;
                ctx.Response.Write("POST required.");
                return;
            }
            string csrf = ctx.Request.Form["__csrf"];
            if (string.IsNullOrEmpty(csrf) || csrf != Convert.ToString(ctx.Session["AntiForgeryToken"]))
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.Write("Invalid request token.");
                return;
            }

            try
            {
                PeopleRepo.RaiseAdminRequest(CurrentUser.UserId, "profile-setup",
                    CurrentUser.FullName + " (" + CurrentUser.RoleName + ") needs their profile completed.");
                SmsPage.Flash("success", "The administrator has been notified. You can keep using the common pages meanwhile.");
            }
            catch (Exception ex)
            {
                Global.WriteLog("ProfileNotify", "raise failed", ex);
                SmsPage.Flash("danger", "We could not notify the administrator. Please try again.");
            }
            ctx.Response.Redirect(returnTo, true);
        }

        private static string SafeReturn(string value)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0 || value[0] != '/' || value.StartsWith("//") || value.StartsWith("/\\")) return "/";
            return value;
        }
    }
}
