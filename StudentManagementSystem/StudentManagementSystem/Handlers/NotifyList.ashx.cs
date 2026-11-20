using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>
    /// JSON feed for the topbar bell. Only the current user's recipient rows are
    /// ever read; the user's notification preferences remove switched-off
    /// categories here as well as in the inbox page and the badge count.
    /// </summary>
    public class NotifyList : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.Cache.SetCacheability(HttpCacheability.NoCache);

            if (!CurrentUser.IsAuthenticated)
            {
                ctx.Response.Write("{\"unread\":0,\"items\":[]}");
                return;
            }

            int userId = CurrentUser.UserId;
            int unread = 0;
            var sb = new StringBuilder("{\"unread\":");
            try
            {
                UserPrefs prefs = PrefsRepo.Get(userId);
                DataTable dt = CommsRepo.InboxVisible(userId, prefs, false, 1, 8);
                unread = CommsRepo.UnreadCountVisible(userId, prefs);
                sb.Append(unread).Append(",\"items\":[");
                bool first = true;
                foreach (DataRow r in dt.Rows)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append("{\"id\":").Append(Convert.ToInt32(r["NotificationId"]))
                      .Append(",\"title\":\"").Append(Js(r["Title"]))
                      .Append("\",\"message\":\"").Append(Js(r["Message"]))
                      .Append("\",\"type\":\"").Append(Js(r["NType"]))
                      .Append("\",\"createdAt\":\"").Append(Convert.ToDateTime(r["CreatedAtUtc"]).ToString("o")).Append('Z')
                      .Append("\",\"unread\":").Append(Convert.ToBoolean(r["IsRead"]) ? "false" : "true")
                      .Append('}');
                }
                sb.Append("]}");
            }
            catch (Exception)
            {
                sb.Clear();
                sb.Append("{\"unread\":0,\"items\":[]}");
            }
            ctx.Response.Write(sb.ToString());
        }

        private static string Js(object value)
        {
            return HttpUtility.JavaScriptStringEncode(value == null ? "" : value.ToString());
        }
    }
}
