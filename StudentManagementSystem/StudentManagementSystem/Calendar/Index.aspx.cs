using System;
using System.Data;
using System.Globalization;
using System.Text;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Campus
{
    /// <summary>Role-aware academic calendar with admin event management and ICS export.</summary>
    public partial class CalendarPage : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin", "Faculty", "Student" }; } }

        protected string FlashHtml, Intro, EventList, StatsHtml, AdminTools;

        private bool IsAdmin { get { return CurrentUser.IsInRole("Admin"); } }
        private string Zone;
        private bool Clock24;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            Zone = Fmt.UserZoneId(CurrentUser.UserId);
            try { Clock24 = PrefsRepo.Get(CurrentUser.UserId).ClockFormat != "12"; }
            catch (Exception) { Clock24 = true; }

            Intro = IsAdmin
                ? "Publish holidays, exams, deadlines and campus events to the right audience. Every event is stored in UTC and shown in each person's timezone."
                : "Holidays, exams, deadlines and campus events for your role, shown in your saved timezone.";

            DateTime from = DateTime.UtcNow.Date;
            DateTime to = from.AddDays(180);
            DataTable dt = CampusRepo.CalendarForUser(CurrentUser.UserId, CurrentUser.RoleName, from, to);
            EventList = BuildEventList(dt);
            StatsHtml = BuildStats(dt);
            if (IsAdmin) BuildAdminTools();
        }

        private void HandlePost()
        {
            string error = null;
            try
            {
                if (!IsAdmin) throw new Exception("Only an administrator can change the calendar.");
                switch ((Request.Form["op"] ?? "").Trim())
                {
                    case "saveevent":
                        SaveEvent();
                        break;
                    case "toggleevent":
                        CampusRepo.SetEventActive(FormInt("eventId"), Request.Form["active"] == "1");
                        Flash("info", "Calendar event updated.");
                        break;
                    case "deleteevent":
                        CampusRepo.DeleteEvent(FormInt("eventId"));
                        Flash("info", "Calendar event deleted.");
                        break;
                    default:
                        throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }
            if (error != null) Flash("danger", error);
            Redirect("/Calendar/Index.aspx");
        }

        private void SaveEvent()
        {
            int eventId = FormInt("eventId");
            DateTime? starts = ReadLocal(Request.Form["starts"]);
            DateTime? ends = ReadLocal(Request.Form["ends"]);
            if (!starts.HasValue || !ends.HasValue) throw new Exception("Choose the event start and end.");
            bool allDay = Request.Form["allDay"] == "1";
            if (allDay)
            {
                if (ends.Value.Date < starts.Value.Date) throw new Exception("The end date must be on or after the start date.");
                starts = starts.Value.Date;
                ends = ends.Value.Date.AddDays(1).AddTicks(-1);
            }
            else if (ends.Value <= starts.Value)
            {
                throw new Exception("The end time must be after the start time.");
            }

            DateTime startsUtc = Fmt.ToUtc(starts.Value, Zone);
            DateTime endsUtc = Fmt.ToUtc(ends.Value, Zone);
            string title = (Request.Form["title"] ?? "").Trim();
            string description = (Request.Form["description"] ?? "").Trim();
            string category = CampusRepo.NormalizeCategory(Request.Form["category"]);
            string audience = CampusRepo.NormalizeAudience(Request.Form["audience"]);
            int? courseId = FormIntNull("courseId");
            int? semester = FormIntNull("semester");
            string location = (Request.Form["location"] ?? "").Trim();

            if (eventId > 0)
            {
                CampusRepo.UpdateEvent(eventId, title, description, category, audience, courseId, semester, startsUtc,
                    endsUtc, allDay, location, CurrentUser.UserId);
                Flash("success", "Calendar event updated.");
            }
            else
            {
                CampusRepo.CreateEvent(title, description, category, audience, courseId, semester, startsUtc, endsUtc,
                    allDay, location, CurrentUser.UserId);
                Flash("success", "Calendar event published and the audience notified.");
            }
        }

        private DataRow EditRow()
        {
            if (!IsAdmin) return null;
            int id = FormIntQuery("edit");
            return id > 0 ? CampusRepo.Event(id) : null;
        }

        private string BuildEventList(DataTable dt)
        {
            if (dt.Rows.Count == 0) return "<p class='msg-empty'>No events are scheduled in this window.</p>";
            int highlight = FormIntQuery("event");
            var sb = new StringBuilder();
            string currentDate = null;
            string[] months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

            foreach (DataRow r in dt.Rows)
            {
                DateTime startLocal = Fmt.ToLocal(Convert.ToDateTime(r["StartsAtUtc"]), Zone);
                DateTime endLocal = Fmt.ToLocal(Convert.ToDateTime(r["EndsAtUtc"]), Zone);
                string dateKey = startLocal.ToString("yyyy-MM-dd");
                if (dateKey != currentDate)
                {
                    if (currentDate != null) sb.Append("</div></div>");
                    currentDate = dateKey;
                    sb.Append("<div class='cal-group'><div class='cal-date'><span>")
                      .Append(startLocal.Day).Append(" ").Append(months[startLocal.Month - 1]).Append(" ").Append(startLocal.Year)
                      .Append("</span><span class='cal-date__day'>").Append(startLocal.ToString("dddd")).Append("</span></div><div class='cal-items'>");
                }

                int eventId = Convert.ToInt32(r["EventId"]);
                bool allDay = Convert.ToBoolean(r["IsAllDay"]);
                string category = Convert.ToString(r["Category"]);
                string time = allDay ? "All day" : TimeRange(startLocal, endLocal);
                sb.Append("<div class='cal-item").Append(eventId == highlight ? " is-highlight" : "").Append("' id='event-").Append(eventId).Append("'>");
                sb.Append("<div class='cal-item__time mono'>").Append(E(time)).Append("</div>");
                sb.Append("<div class='cal-item__body'><div class='cal-item__title'><a href='#event-").Append(eventId).Append("'>")
                  .Append(E(r["Title"])).Append("</a> <span class='badge ").Append(CategoryBadge(category)).Append("'>")
                  .Append(E(CategoryLabel(category))).Append("</span></div>");
                if (r["Description"] != DBNull.Value && Convert.ToString(r["Description"]).Trim().Length > 0)
                    sb.Append("<p class='cal-item__desc'>").Append(E(r["Description"])).Append("</p>");
                sb.Append("<div class='cal-item__meta'><span>").Append(E(AudienceLabel(Convert.ToString(r["Audience"]))));
                if (r["CourseCode"] != DBNull.Value) sb.Append(" · ").Append(E(r["CourseCode"]));
                if (r["Location"] != DBNull.Value) sb.Append(" · ").Append(E(r["Location"]));
                sb.Append("</span></div></div></div>");
            }
            if (currentDate != null) sb.Append("</div></div>");
            return sb.ToString();
        }

        private string TimeRange(DateTime start, DateTime end)
        {
            string format = Clock24 ? "HH:mm" : "h:mm tt";
            if (start.Date == end.Date) return start.ToString(format) + "–" + end.ToString(format);
            return start.ToString("dd MMM " + format) + " – " + end.ToString("dd MMM " + format);
        }

        private string BuildStats(DataTable dt)
        {
            int next30 = 0, exams = 0, holidays = 0;
            DateTime cutoff = DateTime.UtcNow.AddDays(30);
            foreach (DataRow r in dt.Rows)
            {
                if (Convert.ToDateTime(r["StartsAtUtc"]) <= cutoff) next30++;
                string cat = Convert.ToString(r["Category"]);
                if (cat == "exam") exams++;
                if (cat == "holiday") holidays++;
            }
            return "<div class='tt-stats'>" +
                   "<div class='tt-stat'><span class='tt-stat__value'>" + next30 + "</span><span class='tt-stat__label'>Next 30 days</span></div>" +
                   "<div class='tt-stat'><span class='tt-stat__value'>" + exams + "</span><span class='tt-stat__label'>Exam events</span></div>" +
                   "<div class='tt-stat'><span class='tt-stat__value'>" + holidays + "</span><span class='tt-stat__label'>Holidays</span></div>" +
                   "</div>";
        }

        private void BuildAdminTools()
        {
            DataRow edit = EditRow();
            DataTable courses = SqlHelper.FillDataTable(
                "SELECT CourseId, CourseCode + N' · ' + CourseName AS Label FROM dbo.Courses WHERE IsActive=1 ORDER BY CourseCode");

            DateTime defaultStart = Fmt.ToLocal(DateTime.UtcNow, Zone).Date.AddDays(1).AddHours(9);
            DateTime defaultEnd = defaultStart.AddHours(1);
            string starts = edit == null ? defaultStart.ToString("yyyy-MM-ddTHH:mm") :
                Fmt.ToLocal(Convert.ToDateTime(edit["StartsAtUtc"]), Zone).ToString("yyyy-MM-ddTHH:mm");
            string ends = edit == null ? defaultEnd.ToString("yyyy-MM-ddTHH:mm") :
                Fmt.ToLocal(Convert.ToDateTime(edit["EndsAtUtc"]), Zone).ToString("yyyy-MM-ddTHH:mm");
            bool allDay = edit != null && Convert.ToBoolean(edit["IsAllDay"]);
            string title = edit == null ? "" : Convert.ToString(edit["Title"]);
            string description = edit == null ? "" : Convert.ToString(edit["Description"]);
            string location = edit == null ? "" : Convert.ToString(edit["Location"]);
            string selCategory = edit == null ? "event" : Convert.ToString(edit["Category"]);
            string selAudience = edit == null ? "all" : Convert.ToString(edit["Audience"]);
            string selCourse = edit == null || edit["CourseId"] == DBNull.Value ? "" : Convert.ToString(edit["CourseId"]);
            string selSemester = edit == null || edit["SemesterNumber"] == DBNull.Value ? "" : Convert.ToString(edit["SemesterNumber"]);

            var sb = new StringBuilder();
            sb.Append("<div class='bento mt-3'><div class='c-5'><div class='tile'><div class='tile__head'>")
              .Append(edit == null ? "Publish a calendar event" : "Edit calendar event").Append("</div><div class='tile__body'>");
            sb.Append("<form method='post' action='/Calendar/Index.aspx' class='tt-form'>").Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='saveevent'/><input type='hidden' name='eventId' value='")
              .Append(edit == null ? "0" : Convert.ToString(edit["EventId"])).Append("'/>");
            sb.Append("<div class='bento'>");
            sb.Append("<div class='c-12'><label class='form-label' for='calTitle'>Title</label><input class='form-control form-control-sm' id='calTitle' name='title' maxlength='160' required value='")
              .Append(Attr(title)).Append("'/></div>");
            sb.Append("<div class='c-6'><label class='form-label' for='calCategory'>Category</label><select class='form-select form-select-sm' id='calCategory' name='category'>")
              .Append(ValueOptions(CampusRepo.EventCategories, selCategory, CategoryLabel)).Append("</select></div>");
            sb.Append("<div class='c-6'><label class='form-label' for='calAudience'>Audience</label><select class='form-select form-select-sm' id='calAudience' name='audience'>")
              .Append(ValueOptions(CampusRepo.EventAudiences, selAudience, AudienceLabel)).Append("</select></div>");
            sb.Append("<div class='c-6'><label class='form-label' for='calCourse'>Course (for course audience)</label><select class='form-select form-select-sm' id='calCourse' name='courseId'>")
              .Append("<option value=''>No specific course</option>").Append(Options("CourseId", "Label", courses, selCourse)).Append("</select></div>");
            sb.Append("<div class='c-6'><label class='form-label' for='calSemester'>Semester (optional)</label><select class='form-select form-select-sm' id='calSemester' name='semester'>")
              .Append(NumberOptions(1, 8, selSemester, true)).Append("</select></div>");
            sb.Append("<div class='c-6'><label class='form-label' for='calStarts'>Starts (your timezone)</label><input type='datetime-local' class='form-control form-control-sm' id='calStarts' name='starts' required value='")
              .Append(Attr(starts)).Append("'/><div class='form-text small'>Times are in <strong>").Append(E(Zone)).Append("</strong> (").Append(Clock24 ? "24-hour" : "12-hour").Append(")</div></div>");
            sb.Append("<div class='c-6'><label class='form-label' for='calEnds'>Ends</label><input type='datetime-local' class='form-control form-control-sm' id='calEnds' name='ends' required value='")
              .Append(Attr(ends)).Append("'/></div>");
            sb.Append("<div class='c-12'><label class='form-label' for='calLocation'>Location</label><input class='form-control form-control-sm' id='calLocation' name='location' maxlength='160' value='")
              .Append(Attr(location)).Append("'/></div>");
            sb.Append("<div class='c-12'><label class='form-check small'><input class='form-check-input' type='checkbox' name='allDay' value='1'")
              .Append(allDay ? " checked='checked'" : "").Append("/> All-day event</label></div>");
            sb.Append("<div class='c-12'><label class='form-label' for='calDescription'>Description</label><textarea class='form-control form-control-sm' id='calDescription' name='description' rows='3' maxlength='2000'>")
              .Append(E(description)).Append("</textarea></div>");
            sb.Append("</div>");
            sb.Append("<div class='d-flex flex-wrap gap-2 mt-2'><button class='btn btn-sms btn-sm'><i class='bi bi-check2'></i> ")
              .Append(edit == null ? "Publish event" : "Save changes").Append("</button>");
            if (edit != null) sb.Append("<a class='btn btn-outline-sms btn-sm' href='/Calendar/Index.aspx'>Cancel edit</a>");
            sb.Append("</div></form></div></div></div><div class='c-7'><div class='tile'><div class='tile__head'>All calendar events</div>")
              .Append("<div class='tile__body sms-table-wrap table-wrap'>").Append(AdminEventsTable(CampusRepo.EventsAdmin(200))).Append("</div></div></div></div>");
            AdminTools = sb.ToString();
        }

        private string AdminEventsTable(DataTable dt)
        {
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th scope='col'>Event</th><th scope='col'>When</th><th scope='col'>Audience</th><th scope='col'>Status</th><th scope='col'>Actions</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='5' class='msg-empty'>No events yet.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                bool active = Convert.ToBoolean(r["IsActive"]);
                DateTime start = Fmt.ToLocal(Convert.ToDateTime(r["StartsAtUtc"]), Zone);
                DateTime end = Fmt.ToLocal(Convert.ToDateTime(r["EndsAtUtc"]), Zone);
                bool allDay = Convert.ToBoolean(r["IsAllDay"]);
                sb.Append("<tr><td><b>").Append(E(r["Title"])).Append("</b><div class='small text-muted'>")
                  .Append(E(CategoryLabel(Convert.ToString(r["Category"])))).Append("</div></td>");
                sb.Append("<td class='small'>").Append(start.ToString("yyyy-MM-dd"));
                if (allDay) sb.Append("<div class='text-muted'>All day</div>");
                else sb.Append("<div class='mono'>").Append(E(TimeRange(start, end))).Append("</div>");
                sb.Append("</td><td class='small'>").Append(E(AudienceLabel(Convert.ToString(r["Audience"]))));
                if (r["CourseCode"] != DBNull.Value) sb.Append("<div class='text-muted'>").Append(E(r["CourseCode"])).Append("</div>");
                sb.Append("</td><td>").Append(active ? "<span class='badge badge-ok'>Active</span>" : "<span class='badge badge-off'>Hidden</span>").Append("</td>");
                sb.Append("<td><div class='d-flex flex-wrap gap-1'><a class='btn btn-outline-sms btn-sm' href='/Calendar/Index.aspx?edit=")
                  .Append(r["EventId"]).Append("'>Edit</a>");
                sb.Append("<form method='post' action='/Calendar/Index.aspx' class='d-inline'>").Append(CsrfField())
                  .Append("<input type='hidden' name='op' value='toggleevent'/><input type='hidden' name='eventId' value='").Append(r["EventId"])
                  .Append("'/><input type='hidden' name='active' value='").Append(active ? "0" : "1")
                  .Append("'/><button class='btn btn-outline-sms btn-sm'>").Append(active ? "Hide" : "Show").Append("</button></form>");
                sb.Append("<form method='post' action='/Calendar/Index.aspx' class='d-inline' onsubmit=\"return confirm('Delete this calendar event?');\">")
                  .Append(CsrfField()).Append("<input type='hidden' name='op' value='deleteevent'/><input type='hidden' name='eventId' value='")
                  .Append(r["EventId"]).Append("'/><button class='btn btn-outline-sms btn-sm'>Delete</button></form></div></td></tr>");
            }
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private static string ValueOptions(string[] values, string selected, Func<string, string> label)
        {
            var sb = new StringBuilder();
            foreach (string value in values)
                sb.Append("<option value='").Append(value).Append("'").Append(value == selected ? " selected='selected'" : "")
                  .Append(">").Append(E(label(value))).Append("</option>");
            return sb.ToString();
        }

        private static string NumberOptions(int min, int max, string selected, bool allowBlank)
        {
            var sb = new StringBuilder();
            if (allowBlank) sb.Append("<option value=''>Any semester</option>");
            for (int i = min; i <= max; i++)
                sb.Append("<option value='").Append(i).Append("'").Append(i.ToString() == selected ? " selected='selected'" : "")
                  .Append(">").Append(i).Append("</option>");
            return sb.ToString();
        }

        private static string CategoryLabel(string value)
        {
            switch (value)
            {
                case "holiday": return "Holiday";
                case "exam": return "Exam";
                case "deadline": return "Deadline";
                case "academic": return "Academic";
                case "other": return "Other";
                default: return "Event";
            }
        }

        private static string AudienceLabel(string value)
        {
            switch (value)
            {
                case "students": return "Students";
                case "faculty": return "Faculty";
                case "admin": return "Administrators";
                case "course": return "Course";
                default: return "Everyone";
            }
        }

        private static string CategoryBadge(string value)
        {
            switch (value)
            {
                case "exam": return "badge-warn";
                case "deadline": return "badge-bad";
                case "holiday": return "badge-info";
                case "academic": return "badge-info";
                default: return "badge-ok";
            }
        }

        private static DateTime? ReadLocal(string value)
        {
            DateTime dt;
            string raw = (value ?? "").Trim();
            string[] formats = { "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd" };
            if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return dt;
            return null;
        }

        private int FormInt(string key)
        {
            int value;
            return int.TryParse(Request.Form[key], out value) ? value : 0;
        }

        private int? FormIntNull(string key)
        {
            int value;
            return int.TryParse(Request.Form[key], out value) && value > 0 ? (int?)value : null;
        }

        private int FormIntQuery(string key)
        {
            int value;
            return int.TryParse(Request.QueryString[key], out value) ? value : 0;
        }
    }
}
