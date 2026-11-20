using System;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>Polls the topbar bell for unread notification count.</summary>
    public class NotifyBadge : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            if (!CurrentUser.IsAuthenticated)
            {
                ctx.Response.Write("{\"unread\":0}");
                return;
            }
            int n = 0;
            try { n = CommsRepo.UnreadCount(CurrentUser.UserId); }
            catch (Exception) { n = 0; }
            ctx.Response.Write("{\"unread\":" + n + "}");
        }
    }
}
