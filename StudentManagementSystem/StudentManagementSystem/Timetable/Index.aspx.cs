using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

using System.Data;

namespace StudentManagementSystem.Campus
{
    /// <summary>Role-aware weekly timetable with admin scheduling and change reporting.</summary>
    public partial class TimetablePage : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin", "Faculty", "Student" }; } }

        protected string FlashHtml, Intro, WeekGridHtml, AdminToolsHtml;
        protected string ContextBarHtml;

        // PlaceHolder controls from markup
        protected PlaceHolder phContextBar;
        protected PlaceHolder phCourseList;
        protected PlaceHolder phLeftFooter;
        protected PlaceHolder phWeekGrid;
        protected PlaceHolder phAdminTools;

        private bool IsAdmin { get { return CurrentUser.IsInRole("Admin"); } }
        private bool IsFaculty { get { return CurrentUser.IsInRole("Faculty"); } }

        // Helper methods from base page
        private int FormInt(string key) { string v = Request.Form[key]; return string.IsNullOrEmpty(v) ? 0 : int.Parse(v); }
        private int? FormIntNull(string key) { string v = Request.Form[key]; return string.IsNullOrEmpty(v) ? (int?)null : int.Parse(v); }
        private int FormIntQuery(string key) { string v = Request.QueryString[key]; return string.IsNullOrEmpty(v) ? 0 : int.Parse(v); }
        private DateTime? ReadDate(string s) { if (string.IsNullOrWhiteSpace(s)) return null; return DateTime.TryParse(s, out DateTime d) ? d : (DateTime?)null; }

        private DataRow EditRow()
        {
            if (!IsAdmin) return null;
            int id = FormIntQuery("edit");
            return id > 0 ? CampusRepo.Entry(id) : null;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
                FlashHtml = PopFlash();

                Intro = IsAdmin
                    ? "Plan the weekly grid, catch faculty, room and section clashes before publishing, and record substitutions or cancellations."
                    : IsFaculty
                        ? "Your teaching week, including room changes and any substitutions recorded by the academic office."
                        : "Your course timetable for the current semester, with every recorded substitution or cancellation.";

                BuildContextBar();
                BuildCourseList();
                BuildWeekGrid();
                BuildChanges();
                if (IsAdmin) BuildAdminTools();
            }
            catch (Exception ex)
            {
                Global.WriteLog("Timetable", "Page_Load error: " + ex.Message + "\n" + ex.StackTrace, ex);
                throw;
            }
        }

        private DataTable LoadWeek()
        {
            if (IsAdmin) return CampusRepo.Entries(null, null, null, null, true);
            if (IsFaculty) return CampusRepo.Entries(null, null, null, CurrentUser.UserId, true);
            return CampusRepo.EntriesForStudent(CurrentUser.UserId);
        }

        private void HandlePost()
        {
            string error = null;
            try
            {
                if (!IsAdmin) throw new Exception("Only an administrator can change the timetable.");
                switch ((Request.Form["op"] ?? "").Trim())
                {
                    case "saveentry": SaveEntry(); break;
                    case "toggleentry":
                        CampusRepo.SetEntryActive(FormInt("entryId"), Request.Form["active"] == "1");
                        Flash("info", "Timetable entry updated.");
                        break;
                    case "deleteentry":
                        CampusRepo.DeleteEntry(FormInt("entryId"));
                        Flash("info", "Timetable entry deleted.");
                        break;
                    case "addchange": AddChange(); break;
                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }
            if (error != null) Flash("danger", error);
            Redirect("/Timetable/Index.aspx");
        }

        private void SaveEntry()
        {
            int entryId = FormInt("entryId");
            DateTime? from = ReadDate(Request.Form["from"]);
            DateTime? to = ReadDate(Request.Form["to"]);
            if (!from.HasValue) throw new Exception("Choose the date this timetable starts.");
            if (to.HasValue && to.Value < from.Value) throw new Exception("The end date must be on or after the start date.");

            int courseId = FormInt("courseId");
            int semester = FormInt("semester");
            string section = (Request.Form["section"] ?? "").Trim();
            int subjectId = FormInt("subjectId");
            int facultyId = FormInt("facultyId");
            int? roomId = FormIntNull("roomId");
            int periodId = FormInt("periodId");
            int day = FormInt("day");
            string notes = (Request.Form["notes"] ?? "").Trim();

            if (entryId > 0)
            {
                CampusRepo.UpdateEntry(entryId, courseId, semester, section, subjectId, facultyId, roomId, periodId,
                    day, null, from.Value, to, notes, CurrentUser.UserId);
                Flash("success", "Timetable entry updated.");
            }
            else
            {
                CampusRepo.CreateEntry(courseId, semester, section, subjectId, facultyId, roomId, periodId, day, null,
                    from.Value, to, notes, CurrentUser.UserId);
                Flash("success", "Timetable entry added.");
            }
        }

        private void AddChange()
        {
            int entryId = FormInt("changeEntryId");
            DateTime? date = ReadDate(Request.Form["changeDate"]);
            if (!date.HasValue) throw new Exception("Choose the class date.");
            string kind = (Request.Form["kind"] ?? "").Trim();
            int? substitute = FormIntNull("substituteId");
            CampusRepo.AddChange(entryId, date.Value, kind, substitute, Request.Form["reason"], CurrentUser.UserId);
            Flash("success", kind == "cancellation" ? "Cancellation recorded and students notified." : "Substitution recorded and students notified.");
        }

        private void BuildContextBar()
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("<div class='context-bar__label'>Course</div>");
                var courses = SqlHelper.FillDataTable(
                    "SELECT CourseId, CourseCode + N' \u2014 ' + CourseName AS Label FROM dbo.Courses WHERE IsActive=1 ORDER BY CourseCode");
                var ddlCourse = new DropDownList { ID = "ctxCourse", CssClass = "context-bar__select form-select form-select-sm" };
                ddlCourse.Items.Add(new ListItem("All courses", ""));
                foreach (DataRow r in courses.Rows) ddlCourse.Items.Add(new ListItem(Convert.ToString(r["Label"]), Convert.ToString(r["CourseId"])));
                ddlCourse.AutoPostBack = true;
                ddlCourse.SelectedIndexChanged += (s, e) => { Redirect("/Timetable/Index.aspx?courseId=" + ddlCourse.SelectedValue); };
                sb.Append("<div class='context-bar__select-wrap'>").Append(RenderControl(ddlCourse)).Append("</div>");

                if (IsAdmin || IsFaculty)
                {
                    sb.Append("<div class='context-bar__label'>Semester</div>");
                    var ddlSem = new DropDownList { ID = "ctxSem", CssClass = "context-bar__select form-select form-select-sm" };
                    for (int i = 1; i <= 8; i++) ddlSem.Items.Add(new ListItem("Semester " + i, i.ToString()));
                    ddlSem.AutoPostBack = true;
                    ddlSem.SelectedIndexChanged += (s, e) => { Redirect("/Timetable/Index.aspx?courseId=" + Request.QueryString["courseId"] + "&semester=" + ddlSem.SelectedValue); };
                    sb.Append("<div class='context-bar__select-wrap'>").Append(RenderControl(ddlSem)).Append("</div>");
                }

                if (IsAdmin)
                {
                    sb.Append("<div class='context-bar__actions'>");
                    var btnAdd = new Button { Text = "Add entry", CssClass = "btn btn-sms btn-sm", CausesValidation = false };
                    btnAdd.Click += (s, e) => { Redirect("/Timetable/Index.aspx?op=add"); };
                    sb.Append(RenderControl(btnAdd)).Append("</div>");
                }

                ContextBarHtml = sb.ToString();
                phContextBar.Controls.Add(new Literal { Text = ContextBarHtml });
            }
            catch (Exception ex)
            {
                Global.WriteLog("Timetable", "BuildContextBar error: " + ex.Message, ex);
                phContextBar.Controls.Add(new Literal { Text = "<div class='alert alert-danger'>Context bar error: " + E(ex.Message) + "</div>" });
            }
        }

        private void BuildCourseList()
        {
            var sb = new StringBuilder();
            DataTable entries = LoadWeek();

            var groups = DataTableExtensions.AsEnumerable(entries)
                .GroupBy(r => new
                {
                    CourseId = GetInt(r, "CourseId"),
                    CourseCode = GetString(r, "CourseCode"),
                    CourseName = GetString(r, "CourseName"),
                    SectionCode = GetString(r, "SectionCode"),
                    Semester = GetInt(r, "SemesterNumber"),
                });

            var selectedCourseId = Request.QueryString["courseId"];
            var selectedSemester = Request.QueryString["semester"];

            foreach (var g in groups.OrderBy(x => x.Key.CourseCode).ThenBy(x => x.Key.SectionCode))
            {
                bool isSelected = g.Key.CourseId.ToString() == selectedCourseId &&
                    (string.IsNullOrEmpty(Request.QueryString["semester"]) || g.Key.Semester.ToString() == Request.QueryString["semester"]);

                int count = g.Count();
                int conflictCount = g.Count(r => r.Table.Columns.Contains("HasConflict") && r["HasConflict"] != DBNull.Value && Convert.ToBoolean(r["HasConflict"]));

                var url = "/Timetable/Index.aspx?courseId=" + g.Key.CourseId + "&semester=" + g.Key.Semester;

                sb.Append("<a href='").Append(url).Append("' class='tt-course-card ").Append(isSelected ? " is-selected" : "").Append("' data-split-select='").Append(g.Key.CourseId).Append("' data-split-target='#rightBody' data-split-url='").Append(url).Append("'>");
                sb.Append("<div class='tt-course-card__header'><span class='tt-course-card__code'>").Append(E(g.Key.CourseCode)).Append("</span>");
                sb.Append("<span class='tt-course-card__section'>").Append(E(g.Key.SectionCode)).Append("</span></div>");
                sb.Append("<div class='tt-course-card__name'>").Append(E(g.Key.CourseName)).Append("</div>");
                sb.Append("<div class='tt-course-card__meta'>");
                sb.Append("<span class='tt-meta'><i class='bi bi-calendar3'></i> Sem ").Append(g.Key.Semester).Append("</span>");
                sb.Append("<span class='tt-meta'><i class='bi bi-grid'></i> ").Append(g.Count()).Append(" slots</span>");
                if (conflictCount > 0) sb.Append("<span class='tt-meta text-danger'><i class='bi bi-exclamation-triangle'></i> ").Append(conflictCount).Append(" conflicts</span>");
                sb.Append("</div></a>");
            }

            if (sb.Length == 0)
            {
                phCourseList.Controls.Add(new Literal { Text = "<div class='tile__empty'><div class='tile__empty-icon'>📅</div><p>No courses configured yet.</p></div>" });
            }
            else
            {
                phCourseList.Controls.Add(new Literal { Text = sb.ToString() });
            }

            if (IsAdmin)
            {
                var btn = new HyperLink { Text = "+ Add new course timetable", NavigateUrl = "/Admin/Academics.aspx", CssClass = "btn btn-outline-sms btn-sm w-100 mt-2" };
                phLeftFooter.Controls.Add(new Literal { Text = RenderControl(btn) });
            }
        }

        private void BuildWeekGrid()
        {
            DataRow edit = EditRow();
            DataTable entries = LoadWeek();

            var sbGrid = new StringBuilder();
            sbGrid.Append("<div class='tt-grid-wrap'><table class='table table-sm tt-table' role='grid' aria-label='Weekly timetable'>");
            sbGrid.Append("<thead><tr><th scope='col'>Day</th><th scope='col'>Time</th><th scope='col'>Subject</th><th scope='col'>Section</th><th scope='col'>Faculty</th><th scope='col'>Room</th><th scope='col'>Validity</th></tr></thead><tbody>");

            DataTable week = LoadWeek();
            if (week.Rows.Count == 0)
            {
                sbGrid.Append("<tr><td colspan='7' class='msg-empty'>No timetable entries for the selected scope.</td></tr>");
            }
            else
            {
                DateTime today = Fmt.ToLocal(DateTime.UtcNow, Fmt.InstitutionZoneId).Date;
                int todayNumber = today.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)today.DayOfWeek;
                string[] dayNames = { "", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

                var sorted = DataTableExtensions.AsEnumerable(entries)
                .OrderBy(r => GetInt(r, "DayOfWeek"))
                .ThenBy(r => GetInt(r, "PeriodOrder"));

                foreach (DataRow r in sorted)
                {
                    int day = GetInt(r, "DayOfWeek");
                    bool isToday = day == todayNumber;
                    bool hasConflict = r.Table.Columns.Contains("HasConflict") && r["HasConflict"] != DBNull.Value && Convert.ToBoolean(r["HasConflict"]);

                    sbGrid.Append("<tr class='").Append(hasConflict ? "tt-row-conflict " : "").Append(isToday ? "tt-row-today " : "").Append("'>");
                    sbGrid.Append("<td><span class='tt-day'>").Append(dayNames[day]).Append("</span>");
                    if (isToday) sbGrid.Append(" <span class='badge badge-info'>Today</span>");
                    sbGrid.Append("</td>");

                    sbGrid.Append("<td class='mono tt-time'>").Append(E(PeriodRange(r))).Append("</td>");

                    sbGrid.Append("<td><b>").Append(E(GetString(r, "SubjectCode"))).Append("</b>");
                    sbGrid.Append("<div class='small text-muted'>").Append(E(GetString(r, "SubjectName"))).Append("</div>");
                    if (r["Notes"] != DBNull.Value && Convert.ToString(r["Notes"]).Trim().Length > 0)
                        sbGrid.Append("<div class='small tt-note'>").Append(E(GetString(r, "Notes"))).Append("</div>");
                    sbGrid.Append("</td>");

                    sbGrid.Append("<td>").Append(E(GetString(r, "SectionCode"))).Append("</td>");
                    sbGrid.Append("<td>").Append(E(GetString(r, "FacultyName"))).Append("</td>");
                    sbGrid.Append("<td>").Append(r["RoomCode"] == DBNull.Value ? "<span class='text-muted'>Unassigned</span>" : "<b>" + E(GetString(r, "RoomCode")) + "</b>").Append("</td>");

                    sbGrid.Append("<td class='small mono'>").Append(DateText(r["EffectiveFrom"]));
                    sbGrid.Append(" to ").Append(r["EffectiveTo"] == DBNull.Value ? "open" : DateText(r["EffectiveTo"]));
                    sbGrid.Append("</td></tr>");
                }
            }
            sbGrid.Append("</tbody></table></div>");
            WeekGridHtml = sbGrid.ToString();
            phWeekGrid.Controls.Add(new Literal { Text = sbGrid.ToString() });
        }

        private void BuildChanges()
        {
            DateTime today = Fmt.ToLocal(DateTime.UtcNow, Fmt.InstitutionZoneId).Date;
            DataTable dt = CampusRepo.ChangesForUser(CurrentUser.UserId, CurrentUser.RoleName, today.AddDays(-14), today.AddDays(60));
            var sb = new StringBuilder();
            sb.Append("<div class='tile mt-3'><div class='tile__head'>Schedule changes</div><div class='tile__body sms-table-wrap table-wrap'>");
            sb.Append("<table class='table table-sm'><thead><tr><th scope='col'>Date</th><th scope='col'>Class</th><th scope='col'>Change</th><th scope='col'>Detail</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='4' class='msg-empty'>No schedule changes recorded.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                string kind = Convert.ToString(r["ChangeKind"]);
                string cls = kind == "cancellation" ? "badge-bad" : "badge-warn";
                string label = kind == "cancellation" ? "Cancelled" : "Substitute";
                sb.Append("<tr><td class='mono'>").Append(DateText(r["ChangeDate"])).Append("</td>");
                sb.Append("<td><b>").Append(E(r["CourseCode"])).Append("</b> ").Append(E(r["SubjectCode"]))
                  .Append("<div class='small text-muted'>").Append(E(r["PeriodName"])).Append("</div></td>");
                sb.Append("<td><span class='badge ").Append(cls).Append("'>").Append(label).Append("</span></td>");
                sb.Append("<td class='small'>");
                if (r["SubstituteName"] != DBNull.Value) sb.Append(E(r["SubstituteName"])).Append(". ");
                sb.Append(E(r["Reason"] ?? "")).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div></div>");
            phCourseList.Controls.Add(new Literal { Text = sb.ToString() });
        }

        private void BuildAdminTools()
        {
            DataRow edit = EditRow();
            DataTable courses = SqlHelper.FillDataTable("SELECT CourseId, CourseCode + N' \u2014 ' + CourseName AS Label FROM dbo.Courses WHERE IsActive=1 ORDER BY CourseCode");
            DataTable subjects = SqlHelper.FillDataTable("SELECT SubjectId, SubjectCode + N' \u2014 ' + SubjectName AS Label FROM dbo.Subjects WHERE IsActive=1 ORDER BY SubjectCode");
            DataTable faculty = SqlHelper.FillDataTable("SELECT u.UserId, u.FullName + N' \u2014 ' + u.Email AS Label FROM dbo.Users u JOIN dbo.UserRoles ur ON ur.UserId=u.UserId JOIN dbo.Roles r ON r.RoleId=ur.RoleId WHERE r.RoleName='Faculty' AND u.IsActive=1 ORDER BY u.FullName");
            DataTable rooms = CampusRepo.Rooms();
            DataTable periods = CampusRepo.Periods();

            string selCourse = edit == null ? "" : Convert.ToString(edit["CourseId"]);
            string selSubject = edit == null ? "" : Convert.ToString(edit["SubjectId"]);
            string selFaculty = edit == null ? "" : Convert.ToString(edit["FacultyUserId"]);
            string selRoom = edit == null ? "" : Convert.ToString(edit["RoomId"]);
            string selPeriod = edit == null ? "" : Convert.ToString(edit["PeriodId"]);
            string selDay = edit == null ? "" : Convert.ToString(edit["DayOfWeek"]);
            string selSemester = edit == null ? "1" : Convert.ToString(edit["SemesterNumber"]);
            string section = edit == null ? "A" : Convert.ToString(edit["SectionCode"]);
            string from = edit == null ? Fmt.ToLocal(DateTime.UtcNow, Fmt.InstitutionZoneId).ToString("yyyy-MM-dd") : DateText(edit["EffectiveFrom"]);
            string to = edit == null || edit["EffectiveTo"] == DBNull.Value ? "" : DateText(edit["EffectiveTo"]);
            string notes = edit == null ? "" : Convert.ToString(edit["Notes"]);

            var sb = new StringBuilder();
            sb.Append("<div class='tile mt-3'><div class='tile__head d-flex justify-content-between'><span>");
            sb.Append(edit == null ? "Add timetable entry" : "Edit timetable entry").Append("</span>");
            if (edit != null)
            {
                var btnDel = new Button { Text = "Delete", CssClass = "btn btn-outline-bad btn-sm", CausesValidation = false };
                btnDel.Click += (s, e) => { CampusRepo.DeleteEntry(FormInt("entryId")); Flash("info", "Deleted."); Redirect("/Timetable/Index.aspx"); };
                sb.Append(" ").Append(RenderControl(btnDel));
            }
            sb.Append("</span></div><div class='tile__body'>");

            sb.Append("<form method='post' action='/Timetable/Index.aspx' class='tt-form'>").Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='saveentry'/><input type='hidden' name='entryId' value='").Append(edit == null ? "0" : Convert.ToString(edit["EntryId"])).Append("'/>");

            sb.Append("<div class='bento'>");
            sb.Append("<div class='c-6'><label class='form-label' for='ttCourse'>Course</label><select class='form-select form-select-sm' id='ttCourse' name='courseId' required>")
              .Append(Picker("CourseId", "Label", SqlHelper.FillDataTable("SELECT CourseId, CourseCode + N' \u2014 ' + CourseName AS Label FROM dbo.Courses WHERE IsActive=1 ORDER BY CourseCode"), selCourse, "Choose course")).Append("</select></div>");
            sb.Append("<div class='c-6'><label class='form-label' for='ttSubject'>Subject</label><select class='form-select form-select-sm' id='ttSubject' name='subjectId' required>")
              .Append(Picker("SubjectId", "Label", SqlHelper.FillDataTable("SELECT SubjectId, SubjectCode + N' \u2014 ' + SubjectName AS Label FROM dbo.Subjects WHERE IsActive=1 ORDER BY SubjectCode"), selSubject, "Choose subject")).Append("</select></div>");
            sb.Append("<div class='c-4'><label class='form-label' for='ttSemester'>Semester</label><select class='form-select form-select-sm' id='ttSemester' name='semester'>")
              .Append(NumberOptions(1, 8, (edit == null ? "1" : Convert.ToString(edit["SemesterNumber"])))).Append("</select></div>");
            sb.Append("<div class='c-4'><label class='form-label' for='ttSection'>Section</label><input class='form-control form-control-sm' id='ttSection' name='section' maxlength='20' required value='").Append(Attr(section)).Append("'/></div>");
            sb.Append("<div class='c-4'><label class='form-label' for='ttFaculty'>Faculty</label><select class='form-select form-select-sm' id='ttFaculty' name='facultyId' required>")
              .Append(Picker("UserId", "Label", SqlHelper.FillDataTable("SELECT u.UserId, u.FullName + N' \u2014 ' + u.Email AS Label FROM dbo.Users u JOIN dbo.UserRoles ur ON ur.UserId=u.UserId JOIN dbo.Roles r ON r.RoleId=ur.RoleId WHERE r.RoleName='Faculty' AND u.IsActive=1 ORDER BY u.FullName"), selFaculty, "Choose faculty")).Append("</select></div>");
            sb.Append("<div class='c-6'><label class='form-label' for='ttRoom'>Room</label><select class='form-select form-select-sm' id='ttRoom' name='roomId'>")
              .Append(Picker("RoomId", "RoomCode", CampusRepo.Rooms(), selRoom, "No room assigned")).Append("</select></div>");
            sb.Append("<div class='c-6'><label class='form-label' for='ttPeriod'>Period</label><select class='form-select form-select-sm' id='ttPeriod' name='periodId' required>")
              .Append(Picker("PeriodId", "PeriodName", CampusRepo.Periods(), selPeriod, "Choose period")).Append("</select></div>");
            sb.Append("<div class='c-6'><label class='form-label' for='ttDay'>Weekday</label><select class='form-select form-select-sm' id='ttDay' name='day' required>")
              .Append(PickerDays(selDay)).Append("</select></div>");

            sb.Append("<div class='c-6'><label class='form-label' for='ttFrom'>Valid from</label><input type='date' class='form-control form-control-sm' id='ttFrom' name='from' value='").Append(Attr(from)).Append("' required/></div>");
            sb.Append("<div class='c-6'><label class='form-label' for='ttTo'>Valid to (optional)</label><input type='date' class='form-control form-control-sm' id='ttTo' name='to' value='").Append(Attr(to)).Append("'/></div>");
            sb.Append("<div class='c-12'><label class='form-label' for='ttNotes'>Notes</label><textarea class='form-control form-control-sm' id='ttNotes' name='notes' rows='2'>").Append(E(notes)).Append("</textarea></div>");

            sb.Append("<div class='c-12'><div class='d-flex justify-content-end gap-2 mt-2'>");
            sb.Append("<button type='submit' class='btn btn-sms'>").Append(edit == null ? "Add entry" : "Save changes").Append("</button>");
            sb.Append("<a class='btn btn-outline-sms' href='/Timetable/Index.aspx'>Cancel</a></div></div></form></div></div>");

            phAdminTools.Controls.Add(new Literal { Text = sb.ToString() });
        }

        private string RenderControl(Control ctrl)
        {
            using (var sw = new System.IO.StringWriter())
            using (var hw = new HtmlTextWriter(sw))
            {
                ctrl.RenderControl(hw);
                return sw.ToString();
            }
        }

        private string PeriodRange(DataRow r)
        {
            string start = "";
            string end = "";
            if (r.Table.Columns.Contains("StartTime") && r["StartTime"] != DBNull.Value)
            {
                var ts = r["StartTime"] as TimeSpan?;
                if (ts.HasValue) start = ts.Value.ToString(@"hh\:mm");
                else start = Convert.ToDateTime(r["StartTime"]).ToString("HH:mm", CultureInfo.InvariantCulture);
            }
            if (r.Table.Columns.Contains("EndTime") && r["EndTime"] != DBNull.Value)
            {
                var ts = r["EndTime"] as TimeSpan?;
                if (ts.HasValue) end = ts.Value.ToString(@"hh\:mm");
                else end = Convert.ToDateTime(r["EndTime"]).ToString("HH:mm", CultureInfo.InvariantCulture);
            }
            return start + (string.IsNullOrEmpty(end) ? "" : " \u2013 " + end);
        }

        private string DateText(object v)
        {
            if (v == null || v == DBNull.Value) return "";
            return Fmt.Stamp(Convert.ToDateTime(v), Fmt.InstitutionZoneId, "yyyy-MM-dd");
        }

        private int GetInt(DataRow r, string column)
        {
            if (r.Table.Columns.Contains(column) && r[column] != DBNull.Value)
            {
                var val = r[column];
                if (val is int) return (int)val;
                if (val is short) return (short)val;
                if (val is byte) return (byte)val;
                if (val is long) return (int)(long)val;
                int.TryParse(Convert.ToString(val), out int result);
                return result;
            }
            return 0;
        }

        private string GetString(DataRow r, string column)
        {
            if (r.Table.Columns.Contains(column) && r[column] != DBNull.Value)
                return Convert.ToString(r[column]);
            return "";
        }

        private string Picker(string valueField, string textField, DataTable dt, string selected, string placeholder)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(placeholder)) sb.Append("<option value=''>").Append(placeholder).Append("</option>");
            foreach (DataRow r in dt.Rows) sb.Append("<option value='").Append(Attr(Convert.ToString(r[valueField]))).Append("'").Append(string.Equals(selected, Convert.ToString(r[valueField]), StringComparison.OrdinalIgnoreCase) ? " selected" : "").Append(">").Append(E(Convert.ToString(r[textField]))).Append("</option>");
            return sb.ToString();
        }

        private string NumberOptions(int from, int to, string selected)
        {
            var sb = new StringBuilder();
            for (int i = from; i <= to; i++) sb.Append("<option value='").Append(i).Append("'").Append(i.ToString() == selected ? " selected" : "").Append(">").Append(i).Append("</option>");
            return sb.ToString();
        }

        private string PickerDays(string selected)
        {
            string[] names = { "", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            var sb = new StringBuilder();
            sb.Append("<option value=''>Choose day</option>");
            for (int i = 1; i <= 7; i++) sb.Append("<option value='").Append(i).Append("'").Append(i.ToString() == selected ? " selected" : "").Append(">").Append(names[i]).Append("</option>");
            return sb.ToString();
        }
    }
}