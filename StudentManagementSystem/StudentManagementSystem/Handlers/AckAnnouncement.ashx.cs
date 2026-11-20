using System;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>Records that the signed-in user read an announcement, then returns to the page they came from.</summary>
    public class AckAnnouncement : IHttpHandler, System.Web.SessionState.IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            string returnTo = SafeReturn(ctx.Request.Form["return"] ?? ctx.Request.QueryString["return"]);
            if (!CurrentUser.IsAuthenticated)
            {
                ctx.Response.Redirect("~/Account/Login.aspx?next=" + HttpUtility.UrlEncode(returnTo), true);
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

            int annId;
            if (int.TryParse(ctx.Request.Form["annId"], out annId) && annId > 0)
            {
                try { CommsRepo.AcknowledgeAnnouncement(annId, CurrentUser.UserId); }
                catch (Exception ex) { Global.WriteLog("Announce", "ack failed", ex); }
            }
            ctx.Response.Redirect(returnTo, true);
        }

        /// <summary>Only allows local application paths, never an open redirect.</summary>
        private static string SafeReturn(string value)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0 || value[0] != '/' || value.StartsWith("//") || value.StartsWith("/\\")) return "/";
            return value;
        }
    }
}
