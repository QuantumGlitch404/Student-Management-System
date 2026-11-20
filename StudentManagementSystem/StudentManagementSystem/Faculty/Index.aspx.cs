using System;
using System.Data;
using System.Text;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Faculty
{
    /// <summary>Faculty home: assignments + today's sessions + announcements.</summary>
    public partial class Index : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Faculty" }; } }

        protected string FlashHtml, StatCards, AssignmentTable, TodaySessions, Announcements, TaskHtml;

        protected void Page_Load(object sender, EventArgs e)
        {
            FlashHtml = PopFlash();
            BuildTasks();
            BuildAssignments();
            BuildToday();
            BuildAnnouncements();
        }

        private DataRow MyFaculty()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT FacultyId FROM dbo.Faculty WHERE UserId = @UserId", p => p.AddWithValue("@UserId", CurrentUser.UserId));
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        private static string N(object v)
        {
            return v == null || v == DBNull.Value ? "0" : Convert.ToString(v);
        }

        private void BuildTasks()
        {
            var sb = new StringBuilder("<div class='tile mb-3'><div class='tile__head'>Quick tasks</div><div class='tile__body'><div class='task-grid'>");
            Task(sb, "/Faculty/Attendance.aspx", "bi-calendar-check", "Mark attendance");
            Task(sb, "/Faculty/Marks.aspx", "bi-pencil-square", "Enter marks");
            Task(sb, "/Faculty/Assignments.aspx", "bi-journal-text", "Assignments");
            Task(sb, "/Faculty/Complaints.aspx", "bi-life-preserver", "Student complaints");
            Task(sb, "/Faculty/Announcements.aspx", "bi-megaphone", "Class announcement");
            Task(sb, "/Timetable/Index.aspx", "bi-calendar3", "My timetable");
            sb.Append("</div></div></div>"); TaskHtml = sb.ToString();
        }
        private static void Task(StringBuilder sb, string href, string icon, string label)
        {
            sb.Append("<a class='task-btn' href='").Append(href).Append("'><i class='bi ").Append(icon).Append("'></i><span>").Append(label).Append("</span></a>");
        }
        private void BuildAssignments()
        {
            DataTable dt = AcademicsRepo.FacultyAssignments(CurrentUser.UserId);
            object pending = SqlHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.Complaints WHERE AssignedTo = @UserId AND Status IN ('open','in_progress')",
                p => p.AddWithValue("@UserId", CurrentUser.UserId));
            object taught = SqlHelper.ExecuteScalar(
                "SELECT COUNT(DISTINCT en.StudentId) FROM dbo.FacultyAssignments fa " +
                "JOIN dbo.Faculty f ON f.FacultyId=fa.FacultyId " +
                "JOIN dbo.Enrollments en ON en.CourseId=fa.CourseId AND en.SessionId=fa.SessionId " +
                "WHERE f.UserId=@UserId",
                p => p.AddWithValue("@UserId", CurrentUser.UserId));
            object recent = SqlHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.AttendanceSessions WHERE FacultyUserId=@UserId AND SessionDate >= DATEADD(day,-30,CAST(GETDATE() AS date))",
                p => p.AddWithValue("@UserId", CurrentUser.UserId));

            var sb = new StringBuilder();
            sb.Append("<div class='bento'>");
            sb.Append("<div class='c-3'><div class='stat-card'><div class='stat-head'>Assignments</div><div class='stat-body'><div class='num'>" + dt.Rows.Count + "</div></div></div></div>");
            sb.Append("<div class='c-3'><div class='stat-card'><div class='stat-head'>Students taught</div><div class='stat-body'><div class='num'>" + N(taught) + "</div></div></div></div>");
            sb.Append("<div class='c-3'><div class='stat-card'><div class='stat-head'>Sessions (30 days)</div><div class='stat-body'><div class='num'>" + N(recent) + "</div></div></div></div>");
            sb.Append("<div class='c-3'><div class='stat-card'><div class='stat-head'>Open complaints</div><div class='stat-body'><div class='num'>" + N(pending) + "</div></div></div></div>");
            sb.Append("</div>");
            StatCards = sb.ToString();

            var tb = new StringBuilder();
            tb.Append("<table class='table table-sm'><thead><tr><th>Subject</th><th>Course</th><th>Session</th><th>Sem</th><th></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) tb.Append("<tr><td colspan='5' class='msg-empty'>No assignment yet - ask an admin to map you to course subjects.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                tb.Append("<tr><td><b>" + E(r["SubjectCode"]) + "</b> " + E(r["SubjectName"]) + "</td>");
                tb.Append("<td>" + E(r["CourseCode"]) + " " + E(r["CourseName"]) + "</td>");
                tb.Append("<td>" + E(r["SessionName"]) + "</td><td>Sem " + E(r["SemesterNumber"]) + "</td>");
                tb.Append("<td><a class='imbtn' href='/Faculty/Attendance.aspx?assignment=" + E(r["AssignmentId"]) + "'>Attendance</a> " +
                    "<a class='imbtn' href='/Faculty/Marks.aspx?assignment=" + E(r["AssignmentId"]) + "'>Marks</a></td></tr>");
            }
            tb.Append("</tbody></table>");
            AssignmentTable = tb.ToString();
        }

        private void BuildToday()
        {
            DataTable dt = AttendanceRepo.Sessions(CurrentUser.UserId, 20);
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Date</th><th>Period</th><th>Subject</th><th>Marked</th><th></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='5' class='msg-empty'>No session yet.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                sb.Append("<tr><td><b>" + ((DateTime)r["SessionDate"]).ToString("yyyy-MM-dd") + "</b></td>");
                sb.Append("<td>P" + E(r["PeriodNo"]) + "</td><td>" + E(r["SubjectName"]) + "</td>");
                sb.Append("<td>" + E(r["PresentCount"]) + "/" + E(r["MarkedCount"]) + "</td>");
                sb.Append("<td><a class='imbtn' href='/Faculty/Attendance.aspx?session=" + E(r["AttnSessionId"]) + "'>Open</a></td></tr>");
            }
            sb.Append("</tbody></table>");
            TodaySessions = sb.ToString();
        }

        private void BuildAnnouncements()
        {
            DataTable dt = CommsRepo.AnnouncementsVisible(CurrentUser.RoleName, CurrentUser.UserId);
            var sb = new StringBuilder();
            bool any = false;
            foreach (DataRow r in dt.Rows)
            {
                any = true;
                bool pinned = r["IsPinned"] != DBNull.Value && Convert.ToBoolean(r["IsPinned"]);
                bool acked = r["HasAcked"] != DBNull.Value && Convert.ToBoolean(r["HasAcked"]);
                sb.Append("<div class='announce-item" + (pinned ? " pinned" : "") + "'>");
                sb.Append("<div class='announce-head'><b>" + (pinned ? "<i class='bi bi-pin-angle-fill'></i> " : "") + E(r["AnnTitle"]) + "</b>");
                sb.Append("<span class='badge badge-info'>" + E(r["Category"]) + "</span></div>");
                sb.Append("<div class='small announce-body'>" + E(r["AnnBody"]) + "</div>");
                sb.Append("<div class='small text-muted'>" + E(r["AuthorName"]) + " - " + ((DateTime)r["CreatedAtUtc"]).ToString("yyyy-MM-dd") + "</div>");
                if (acked)
                    sb.Append("<div class='small text-success'><i class='bi bi-check2-circle'></i> Acknowledged</div>");
                else
                    sb.Append("<form method='post' action='/Handlers/AckAnnouncement.ashx' class='d-inline'>" + CsrfField() +
                        "<input type='hidden' name='annId' value='" + E(r["AnnId"]) + "'/>" +
                        "<input type='hidden' name='return' value='" + Attr(Request.RawUrl) + "'/>" +
                        "<button class='imbtn'>Acknowledge</button></form>");
                sb.Append("</div>");
            }
            if (!any) sb.Append("<p class='msg-empty mb-0'>No announcement right now.</p>");
            Announcements = sb.ToString();
        }

    }
}

