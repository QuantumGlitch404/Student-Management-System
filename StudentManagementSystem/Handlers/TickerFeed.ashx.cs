using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>JSON feed for the campus notification ticker.</summary>
    public class TickerFeed : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.Cache.SetCacheability(HttpCacheability.NoCache);

            if (!CurrentUser.IsAuthenticated)
            {
                ctx.Response.Write("{\"ok\":false,\"unread\":0,\"items\":[]}");
                return;
            }

            int userId = CurrentUser.UserId;
            UserPrefs prefs = PrefsRepo.Get(userId);
            string zone = prefs.TimezoneId ?? Fmt.InstitutionZoneId;
            var sb = new StringBuilder("{\"ok\":true");
            try
            {
                DataTable dt = CommsRepo.TickerFeed(userId, prefs, 6);
                int unread = CommsRepo.TickerCount(userId, prefs);
                sb.Append(",\"unread\":").Append(unread)
                  .Append(",\"enabled\":").Append(prefs.TickerEnabled ? "true" : "false")
                  .Append(",\"speed\":\"").Append(Js(prefs.TickerSpeed))
                  .Append("\",\"timezone\":\"").Append(Js(zone))
                  .Append("\",\"clock\":\"").Append(Js(prefs.ClockFormat))
                  .Append("\",\"serverNowUtc\":\"").Append(Fmt.IsoUtc(DateTime.UtcNow))
                  .Append("\",\"items\":[");
                bool first = true;
                foreach (DataRow row in dt.Rows)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append("{\"id\":").Append(Convert.ToInt32(row["NotificationId"]))
                      .Append(",\"title\":\"").Append(Js(row["Title"]))
                      .Append("\",\"message\":\"").Append(Js(row["Message"]))
                      .Append("\",\"type\":\"").Append(Js(row["NType"]))
                      .Append("\",\"url\":\"").Append(Js(row["ActionUrl"]))
                      .Append("\",\"createdAt\":\"").Append(Fmt.IsoUtc(Convert.ToDateTime(row["CreatedAtUtc"])))
                      .Append("\",\"snoozedUntil\":");
                    if (row["SnoozedUntilUtc"] == DBNull.Value) sb.Append("null");
                    else sb.Append('"').Append(Fmt.IsoUtc(Convert.ToDateTime(row["SnoozedUntilUtc"]))).Append('"');
                    sb.Append('}');
                }
                sb.Append("]}");
            }
            catch (Exception)
            {
                sb.Clear();
                sb.Append("{\"ok\":false,\"unread\":0,\"items\":[]}");
            }
            ctx.Response.Write(sb.ToString());
        }

        private static string Js(object value)
        {
            return HttpUtility.JavaScriptStringEncode(value == null ? "" : value.ToString());
        }
    }
}
