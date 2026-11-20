using System;
using System.Data;
using System.Text;
using System.Web;
using System.Web.SessionState;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>Role-filtered iCalendar export of the academic calendar.</summary>
    public class CalendarIcs : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            if (!CurrentUser.IsAuthenticated)
            {
                ctx.Response.StatusCode = 401;
                ctx.Response.ContentType = "text/plain";
                ctx.Response.Write("Sign in to download the calendar.");
                return;
            }

            string role = CurrentUser.RoleName;
            if (role != "Admin" && role != "Faculty" && role != "Student")
            {
                ctx.Response.StatusCode = 403;
                ctx.Response.ContentType = "text/plain";
                ctx.Response.Write("Your role cannot export the calendar.");
                return;
            }

            string zone = Fmt.UserZoneId(CurrentUser.UserId);
            DateTime from = DateTime.UtcNow.Date.AddDays(-30);
            DateTime to = from.AddDays(400);
            DataTable dt = CampusRepo.CalendarForUser(CurrentUser.UserId, role, from, to);

            var sb = new StringBuilder();
            sb.Append("BEGIN:VCALENDAR\r\n");
            sb.Append("VERSION:2.0\r\n");
            sb.Append("PRODID:-//Student Management System//Academic Calendar//EN\r\n");
            sb.Append("CALSCALE:GREGORIAN\r\n");
            sb.Append("METHOD:PUBLISH\r\n");
            sb.Append("X-WR-CALNAME:SMS academic calendar\r\n");
            sb.Append("X-WR-TIMEZONE:").Append(Escape(zone)).Append("\r\n");

            string stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
            foreach (DataRow r in dt.Rows)
            {
                DateTime startUtc = Convert.ToDateTime(r["StartsAtUtc"]);
                DateTime endUtc = Convert.ToDateTime(r["EndsAtUtc"]);
                bool allDay = Convert.ToBoolean(r["IsAllDay"]);
                int id = Convert.ToInt32(r["EventId"]);

                sb.Append("BEGIN:VEVENT\r\n");
                sb.Append("UID:calendar-event-").Append(id).Append("@student-management-system\r\n");
                sb.Append("DTSTAMP:").Append(stamp).Append("\r\n");
                if (allDay)
                {
                    DateTime localStart = Fmt.ToLocal(startUtc, zone).Date;
                    DateTime localEnd = Fmt.ToLocal(endUtc, zone).Date.AddDays(1);
                    sb.Append("DTSTART;VALUE=DATE:").Append(localStart.ToString("yyyyMMdd")).Append("\r\n");
                    sb.Append("DTEND;VALUE=DATE:").Append(localEnd.ToString("yyyyMMdd")).Append("\r\n");
                }
                else
                {
                    sb.Append("DTSTART:").Append(Utc(startUtc)).Append("\r\n");
                    sb.Append("DTEND:").Append(Utc(endUtc)).Append("\r\n");
                }
                sb.Append("SUMMARY:").Append(Escape(Convert.ToString(r["Title"]))).Append("\r\n");
                if (r["Description"] != DBNull.Value)
                    sb.Append("DESCRIPTION:").Append(Escape(Convert.ToString(r["Description"]))).Append("\r\n");
                if (r["Location"] != DBNull.Value)
                    sb.Append("LOCATION:").Append(Escape(Convert.ToString(r["Location"]))).Append("\r\n");
                sb.Append("CATEGORIES:").Append(Escape(Convert.ToString(r["Category"]))).Append("\r\n");
                sb.Append("STATUS:CONFIRMED\r\n");
                sb.Append("END:VEVENT\r\n");
            }
            sb.Append("END:VCALENDAR\r\n");

            ctx.Response.Clear();
            ctx.Response.Buffer = true;
            ctx.Response.ContentType = "text/calendar; charset=utf-8";
            ctx.Response.AddHeader("Content-Disposition", "attachment; filename=sms-calendar.ics");
            ctx.Response.Write(sb.ToString());
            AuditService.Log("CalendarExported", "CalendarEvents", null, role + " ICS");
            ctx.ApplicationInstance.CompleteRequest();
        }

        private static string Utc(DateTime value)
        {
            DateTime utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return utc.ToString("yyyyMMddTHHmmssZ");
        }

        private static string Escape(string value)
        {
            string text = (value ?? "").Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,");
            return text.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");
        }
    }
}
