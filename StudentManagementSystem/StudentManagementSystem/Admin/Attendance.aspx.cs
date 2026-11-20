using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Admin
{
    /// <summary>
    /// Attendance overview: filterable session list, present ratios, shortage
    /// list against the configurable threshold, and a per-session roll call.
    /// </summary>
    public partial class Attendance : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, SessionList, DetailList, SelectedInfo, SummaryCards, ShortageList;
        protected string CourseOptions, FacultyOptions, FilterQuery, ExportQuery, FromDate, ToDate;
        protected int SelectedId, Threshold = 75, SessionCount;
        private int _courseId, _facultyId;

        protected void Page_Load(object sender, EventArgs e)
        {
            FlashHtml = PopFlash();
            int.TryParse(Request.QueryString["session"], out SelectedId);
            int.TryParse(Request.QueryString["courseId"], out _courseId);
            int.TryParse(Request.QueryString["facultyId"], out _facultyId);
            FromDate = (Request.QueryString["from"] ?? "").Trim();
            ToDate = (Request.QueryString["to"] ?? "").Trim();
            Threshold = Setting("Attendance:ShortagePercent", 75);
            BuildOptions();
            BuildFilterQuery();
            BuildSummary();
            BuildList();
            BuildShortage();
            if (SelectedId > 0) BuildDetail();
        }

        private static int Setting(string key, int fallback)
        {
            try
            {
                object v = SqlHelper.ExecuteScalar("SELECT SettingValue FROM dbo.AppSettings WHERE SettingKey=@K",
                    p => p.AddWithValue("@K", key));
                int n;
                return v != null && v != DBNull.Value && int.TryParse(Convert.ToString(v), out n) ? n : fallback;
            }
            catch (Exception) { return fallback; }
        }

        private void BuildOptions()
        {
            var c = new StringBuilder("<option value='0'>All courses</option>");
            foreach (DataRow r in AcademicsRepo.Courses().Rows)
                c.Append("<option value='").Append(r["CourseId"]).Append("'")
                 .Append(Convert.ToInt32(r["CourseId"]) == _courseId ? " selected" : "").Append(">")
                 .Append(E(r["CourseCode"])).Append(" - ").Append(E(r["CourseName"])).Append("</option>");
            CourseOptions = c.ToString();

            var f = new StringBuilder("<option value='0'>All faculty</option>");
            foreach (DataRow r in SqlHelper.FillDataTable(
                "SELECT DISTINCT u.UserId, u.FullName FROM dbo.Users u " +
                "JOIN dbo.UserRoles ur ON ur.UserId=u.UserId JOIN dbo.Roles r ON r.RoleId=ur.RoleId " +
                "WHERE r.RoleName='Faculty' ORDER BY u.FullName").Rows)
                f.Append("<option value='").Append(r["UserId"]).Append("'")
                 .Append(Convert.ToInt32(r["UserId"]) == _facultyId ? " selected" : "").Append(">")
                 .Append(E(r["FullName"])).Append("</option>");
            FacultyOptions = f.ToString();
        }

        private void BuildFilterQuery()
        {
            var q = new StringBuilder();
            if (_courseId > 0) q.Append("&courseId=").Append(_courseId);
            if (_facultyId > 0) q.Append("&facultyId=").Append(_facultyId);
            if (FromDate.Length > 0) q.Append("&from=").Append(HttpUtility.UrlEncode(FromDate));
            if (ToDate.Length > 0) q.Append("&to=").Append(HttpUtility.UrlEncode(ToDate));
            FilterQuery = q.ToString();
            ExportQuery = q.ToString().Replace("&facultyId=", "&facultyId=");
        }

        private void FillFilterParams(System.Data.SqlClient.SqlParameterCollection p)
        {
            p.AddWithValue("@CourseId", _courseId);
            p.AddWithValue("@FacultyId", _facultyId);
            p.AddWithValue("@From", FromDate);
            p.AddWithValue("@To", ToDate);
        }

        private const string Where =
            "WHERE (@CourseId=0 OR a.CourseId=@CourseId) AND (@FacultyId=0 OR a.FacultyUserId=@FacultyId) " +
            "AND (@From='' OR a.SessionDate >= @From) AND (@To='' OR a.SessionDate <= @To) ";

        private void BuildSummary()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT COUNT(DISTINCT a.AttnSessionId) AS Sessions, COUNT(att.AttnId) AS Marked, " +
                "ISNULL(SUM(CASE WHEN att.AttnStatus IN ('P','L','E') THEN 1 ELSE 0 END),0) AS Present, " +
                "ISNULL(SUM(CASE WHEN att.AttnStatus='A' THEN 1 ELSE 0 END),0) AS Absent " +
                "FROM dbo.AttendanceSessions a LEFT JOIN dbo.Attendance att ON att.AttnSessionId=a.AttnSessionId " + Where,
                FillFilterParams);
            DataRow r = dt.Rows[0];
            int sessions = Convert.ToInt32(r["Sessions"]);
            int marked = Convert.ToInt32(r["Marked"]);
            int present = Convert.ToInt32(r["Present"]);
            int absent = Convert.ToInt32(r["Absent"]);
            decimal pct = marked == 0 ? 0 : Math.Round(present * 100m / marked, 1);
            SessionCount = sessions;

            var sb = new StringBuilder("<div class='bento'>");
            Kpi(sb, "Sessions", sessions.ToString("N0"), "bi-calendar2-check");
            Kpi(sb, "Roll calls marked", marked.ToString("N0"), "bi-check2-square");
            Kpi(sb, "Present ratio", pct.ToString("0.0") + "%", "bi-graph-up-arrow");
            Kpi(sb, "Absences", absent.ToString("N0"), "bi-person-dash");
            sb.Append("</div>");
            SummaryCards = sb.ToString();
        }

        private static void Kpi(StringBuilder sb, string label, string value, string icon)
        {
            sb.Append("<div class='c-3'><div class='stat-card'><div class='stat-head'>").Append(label).Append("</div><div class='stat-body'><div class='num'>").Append(value).Append("</div></div></div></div>");
        }

        private void BuildList()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP 200 a.AttnSessionId, a.SessionDate, a.PeriodNo, c.CourseCode, sub.SubjectName, u.FullName AS FacultyName, " +
                "COUNT(att.AttnId) AS MarkedCount, ISNULL(SUM(CASE WHEN att.AttnStatus IN ('P','L','E') THEN 1 ELSE 0 END),0) AS PresentCount " +
                "FROM dbo.AttendanceSessions a JOIN dbo.Courses c ON c.CourseId=a.CourseId " +
                "JOIN dbo.Subjects sub ON sub.SubjectId=a.SubjectId JOIN dbo.Users u ON u.UserId=a.FacultyUserId " +
                "LEFT JOIN dbo.Attendance att ON att.AttnSessionId=a.AttnSessionId " + Where +
                "GROUP BY a.AttnSessionId, a.SessionDate, a.PeriodNo, c.CourseCode, sub.SubjectName, u.FullName " +
                "ORDER BY a.SessionDate DESC, a.PeriodNo DESC", FillFilterParams);

            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Date</th><th>Period</th><th>Subject</th><th>Course</th><th>Faculty</th><th style='width:150px'>Attendance</th><th></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='7' class='msg-empty'>No sessions match these filters.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                int marked = Convert.ToInt32(r["MarkedCount"]);
                int present = Convert.ToInt32(r["PresentCount"]);
                int pct = marked == 0 ? 0 : (int)Math.Round(present * 100m / marked);
                sb.Append("<tr><td class='mono'><b>").Append(((DateTime)r["SessionDate"]).ToString("yyyy-MM-dd")).Append("</b></td>");
                sb.Append("<td>P").Append(E(r["PeriodNo"])).Append("</td><td>").Append(E(r["SubjectName"])).Append("</td>");
                sb.Append("<td>").Append(E(r["CourseCode"])).Append("</td><td>").Append(E(r["FacultyName"])).Append("</td>");
                sb.Append("<td><div class='d-flex align-items-center gap-2'><div class='dbar ").Append(pct < Threshold ? "bad" : "ok").Append("'><span style='width:").Append(Math.Max(2, pct)).Append("%'></span></div>");
                sb.Append("<span class='mono small'>").Append(present).Append("/").Append(marked).Append("</span></div></td>");
                sb.Append("<td><a class='imbtn' href='/Admin/Attendance.aspx?session=").Append(r["AttnSessionId"]).Append(FilterQuery).Append("'>Open</a></td></tr>");
            }
            sb.Append("</tbody></table>");
            SessionList = sb.ToString();
        }

        private void BuildShortage()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP 12 u.FullName, st.EnrollNumber, c.CourseCode, " +
                "100.0 * SUM(CASE WHEN att.AttnStatus IN ('P','L','E') THEN 1 ELSE 0 END) / NULLIF(COUNT(*),0) AS Pct " +
                "FROM dbo.Attendance att JOIN dbo.AttendanceSessions a ON a.AttnSessionId=att.AttnSessionId " +
                "JOIN dbo.Enrollments e ON e.EnrollId=att.EnrollId JOIN dbo.Students st ON st.StudentId=e.StudentId " +
                "JOIN dbo.Users u ON u.UserId=st.UserId JOIN dbo.Courses c ON c.CourseId=st.CourseId " + Where +
                "GROUP BY u.FullName, st.EnrollNumber, c.CourseCode HAVING 100.0 * SUM(CASE WHEN att.AttnStatus IN ('P','L','E') THEN 1 ELSE 0 END) / NULLIF(COUNT(*),0) < @Threshold " +
                "ORDER BY Pct",
                p => { FillFilterParams(p); p.AddWithValue("@Threshold", Threshold); });

            var sb = new StringBuilder();
            if (dt.Rows.Count == 0)
                sb.Append("<div class='empty-state'><i class='bi bi-shield-check'></i><h3>No shortage</h3><p>Every student in this slice is at or above ")
                  .Append(Threshold).Append("%.</p></div>");
            else
            {
                sb.Append("<table class='table table-sm'><thead><tr><th>Student</th><th>Course</th><th>%</th></tr></thead><tbody>");
                foreach (DataRow r in dt.Rows)
                {
                    decimal pct = Convert.ToDecimal(r["Pct"]);
                    sb.Append("<tr><td><span class='row-title'>").Append(E(r["FullName"])).Append("</span><div class='row-sub mono'>").Append(E(r["EnrollNumber"])).Append("</div></td>");
                    sb.Append("<td>").Append(E(r["CourseCode"])).Append("</td>");
                    sb.Append("<td><span class='badge badge-bad'>").Append(pct.ToString("0.0")).Append("%</span></td></tr>");
                }
                sb.Append("</tbody></table>");
                sb.Append("<div class='small text-muted mt-2'>Threshold is configurable in Settings (Attendance shortage %).</div>");
            }
            ShortageList = sb.ToString();
        }

        private void BuildDetail()
        {
            DataRow s = AttendanceRepo.OneSession(SelectedId);
            if (s == null) { DetailList = "<p class='msg-empty'>Session not found.</p>"; SelectedInfo = "not found"; return; }
            SelectedInfo = ((DateTime)s["SessionDate"]).ToString("yyyy-MM-dd") + " P" + s["PeriodNo"] + " " + s["SubjectCode"] + " - " + s["FacultyName"];
            DataTable rows = SqlHelper.FillDataTable(
                "SELECT st.EnrollNumber, u.FullName, at.AttnStatus, at.Remarks " +
                "FROM dbo.Attendance at " +
                "JOIN dbo.Enrollments e ON e.EnrollId = at.EnrollId " +
                "JOIN dbo.Students st ON st.StudentId = e.StudentId " +
                "JOIN dbo.Users u ON u.UserId = st.UserId " +
                "WHERE at.AttnSessionId = @Id ORDER BY st.EnrollNumber",
                p => p.AddWithValue("@Id", SelectedId));
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Enrollment</th><th>Student</th><th>Status</th><th>Remarks</th></tr></thead><tbody>");
            if (rows.Rows.Count == 0) sb.Append("<tr><td colspan='4' class='msg-empty'>No attendance rows saved for this session.</td></tr>");
            foreach (DataRow r in rows.Rows)
            {
                string stt = (r["AttnStatus"] as string ?? "").Trim();
                string cls = stt == "P" ? "badge-ok" : stt == "A" ? "badge-bad" : "badge-warn";
                string word = stt == "P" ? "Present" : stt == "A" ? "Absent" : stt == "L" ? "Leave (exempt)" : stt == "E" ? "Authorised" : stt;
                sb.Append("<tr><td class='mono'>").Append(E(r["EnrollNumber"])).Append("</td><td>").Append(E(r["FullName"])).Append("</td>");
                sb.Append("<td><span class='badge ").Append(cls).Append("'>").Append(word).Append("</span></td>");
                sb.Append("<td class='small text-muted'>").Append(E(r["Remarks"])).Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            DetailList = sb.ToString();
        }
    }
}
