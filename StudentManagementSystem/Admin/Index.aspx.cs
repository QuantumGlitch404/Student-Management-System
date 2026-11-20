using System;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Admin
{
    public partial class Index : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, StatCards, TrendChart, FinanceCard, RiskTable, ExamTable,
            RecentAudit, CourseSplit, AnnouncementList, ExportUrl, FromDate, ToDate,
            SessionOptions, SemesterOptions, EnrollmentWindowLabel, Greeting, TaskHtml;
        protected int Threshold = 75;

        private DateTime _from, _to;
        private int _sessionId, _semester;

        protected void Page_Load(object sender, EventArgs e)
        {
            FlashHtml = PopFlash();
            BuildTasks();
            ReadFilters();
            { DateTime userNow = Fmt.ToLocal(DateTime.UtcNow, Fmt.CurrentZoneId()); Greeting = userNow.Hour < 12 ? "morning" : userNow.Hour < 17 ? "afternoon" : "evening"; }
            BuildFilters();
            BuildStats();
            BuildTrend();
            BuildFinance();
            BuildRisk();
            BuildExams();
            BuildAudit();
            BuildCourseSplit();
            BuildAnnouncements();
            ExportUrl = "/Handlers/Export.ashx?scope=dashboard&format=pdf&from=" + _from.ToString("yyyy-MM-dd") +
                        "&to=" + _to.ToString("yyyy-MM-dd") + "&sessionId=" + _sessionId + "&semester=" + _semester;
        }

        private void ReadFilters()
        {
            DateTime parsed;
            _from = DateTime.TryParseExact(Request.QueryString["from"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
                ? parsed : Fmt.ToLocal(DateTime.UtcNow, Fmt.CurrentZoneId()).Date.AddDays(-29);
            _to = DateTime.TryParseExact(Request.QueryString["to"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
                ? parsed : Fmt.ToLocal(DateTime.UtcNow, Fmt.CurrentZoneId()).Date;
            if (_to < _from) { DateTime t = _from; _from = _to; _to = t; }
            FromDate = _from.ToString("yyyy-MM-dd");
            ToDate = _to.ToString("yyyy-MM-dd");
            int.TryParse(Request.QueryString["sessionId"], out _sessionId);
            int.TryParse(Request.QueryString["semester"], out _semester);
        }

        private void BuildTasks()
        {
            var sb = new StringBuilder("<div class='tile mb-3'><div class='tile__head'>Quick tasks</div><div class='tile__body'><div class='task-grid'>");
            Task(sb, "/Admin/Payments.aspx", "bi-cash-stack", "Review payments");
            Task(sb, "/Admin/Attendance.aspx", "bi-calendar-check", "Attendance overview");
            Task(sb, "/Admin/Exams.aspx", "bi-journal-text", "Publish results");
            Task(sb, "/Admin/OnlineTests.aspx", "bi-patch-question", "Online tests");
            Task(sb, "/Admin/Announcements.aspx", "bi-megaphone", "Send announcement");
            Task(sb, "/Admin/Users.aspx", "bi-person-plus", "Add a user");
            sb.Append("</div></div></div>"); TaskHtml = sb.ToString();
        }
        private static void Task(StringBuilder sb, string href, string icon, string label)
        {
            sb.Append("<a class='task-btn' href='").Append(href).Append("'><i class='bi ").Append(icon).Append("'></i><span>").Append(label).Append("</span></a>");
        }

        private void BuildFilters()
        {
            var sessions = new StringBuilder("<option value='0'>All years</option>");
            try
            {
                foreach (DataRow r in AcademicsRepo.Sessions().Rows)
                    sessions.Append("<option value='").Append(r["SessionId"]).Append("'")
                        .Append(Convert.ToInt32(r["SessionId"]) == _sessionId ? " selected" : "").Append(">")
                        .Append(E(r["SessionName"])).Append("</option>");
            }
            catch (Exception) { /* sessions are optional */ }
            SessionOptions = sessions.ToString();

            var sem = new StringBuilder("<option value='0'>All semesters</option>");
            for (int i = 1; i <= 8; i++)
                sem.Append("<option value='").Append(i).Append("'").Append(i == _semester ? " selected" : "").Append(">Semester ").Append(i).Append("</option>");
            SemesterOptions = sem.ToString();

            try
            {
                object setting = SqlHelper.ExecuteScalar("SELECT SettingValue FROM dbo.AppSettings WHERE SettingKey='Attendance:ShortagePercent'");
                if (setting != null && setting != DBNull.Value) int.TryParse(setting.ToString(), out Threshold);
            }
            catch (Exception) { Threshold = 75; }
        }

        private DataTable Metrics()
        {
            return SqlHelper.FillDataTable(
                "SELECT " +
                "(SELECT COUNT(*) FROM dbo.Students s JOIN dbo.Users u ON u.UserId=s.UserId WHERE u.IsActive=1 AND (@SessionId=0 OR s.SessionId=@SessionId) AND (@Semester=0 OR s.SemesterNumber=@Semester)) AS Students, " +
                "(SELECT COUNT(*) FROM dbo.Faculty) AS Faculty, " +
                "(SELECT COUNT(*) FROM dbo.Courses WHERE IsActive=1) AS Courses, " +
                "(SELECT COUNT(*) FROM dbo.Complaints WHERE Status IN ('open','in_progress')) AS OpenComplaints, " +
                "(SELECT COUNT(*) FROM dbo.NotificationRecipients WHERE UserId=@Me AND IsRead=0) AS Unread, " +
                "(SELECT COUNT(*) FROM dbo.Exams WHERE IsPublished=0) AS DraftExams, " +
                "(SELECT COUNT(*) FROM dbo.AttendanceSessions WHERE SessionDate BETWEEN @From AND @To) AS Sessions, " +
                "(SELECT COUNT(*) FROM dbo.Payments WHERE CreatedAtUtc >= @From AND CreatedAtUtc < DATEADD(day,1,@To)) AS Payments, " +
                "(SELECT ISNULL(SUM(Amount),0) FROM dbo.Payments WHERE PayStatus='captured' AND CapturedAtUtc >= @From AND CapturedAtUtc < DATEADD(day,1,@To)) AS Collected",
                p =>
                {
                    p.AddWithValue("@From", _from);
                    p.AddWithValue("@To", _to);
                    p.AddWithValue("@SessionId", _sessionId);
                    p.AddWithValue("@Semester", _semester);
                    p.AddWithValue("@Me", CurrentUser.UserId);
                });
        }

        private void BuildStats()
        {
            DataRow r = Metrics().Rows[0];
            var sb = new StringBuilder("<div class='grid-kpi'>");
            Stat(sb, "tone-mint", "bi-mortarboard", Convert.ToInt32(r["Students"]).ToString("N0"), "Active students", "filtered cohort");
            Stat(sb, "tone-peach", "bi-person-badge", Convert.ToInt32(r["Faculty"]).ToString("N0"), "Faculty", "teaching accounts");
            Stat(sb, "tone-butter", "bi-journal-bookmark", Convert.ToInt32(r["Courses"]).ToString("N0"), "Active courses", "programmes");
            Stat(sb, "tone-sky", "bi-clipboard-check", Convert.ToInt32(r["Sessions"]).ToString("N0"), "Attendance sessions", "in selected period");
            Stat(sb, "tone-teal", "bi-journal-check", Convert.ToInt32(r["DraftExams"]).ToString("N0"), "Exams to publish", "awaiting results");
            Stat(sb, "tone-coral", "bi-cash-coin", Convert.ToInt32(r["Payments"]).ToString("N0"), "Payments", "created in period");
            Stat(sb, "tone-lilac", "bi-flag", Convert.ToInt32(r["OpenComplaints"]).ToString("N0"), "Open complaints", "needs a response");
            Stat(sb, "tone-rose", "bi-bell", Convert.ToInt32(r["Unread"]).ToString("N0"), "Unread notifications", "for you");
            sb.Append("</div>");
            StatCards = sb.ToString();
        }

        private void Stat(StringBuilder sb, string tone, string icon, string num, string label, string delta)
        {
            sb.Append("<article class='kpi' data-attention='").Append(tone == "tone-coral" || tone == "tone-rose" ? "true" : "false").Append("'>")
              .Append("<header class='kpi__head'>").Append(E(label)).Append("</header>")
              .Append("<div class='kpi__body'><div class='kpi__value'>").Append(E(num))
              .Append("</div><p class='kpi__note'>").Append(E(delta)).Append("</p></div></article>");
        }

        private void BuildTrend()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT CONVERT(char(7), s.AdmissionDate, 120) AS Ym, COUNT(*) AS C FROM dbo.Students s " +
                "WHERE s.AdmissionDate >= DATEADD(month, -5, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)) " +
                "AND (@SessionId=0 OR s.SessionId=@SessionId) AND (@Semester=0 OR s.SemesterNumber=@Semester) " +
                "GROUP BY CONVERT(char(7), s.AdmissionDate, 120)",
                p => { p.AddWithValue("@SessionId", _sessionId); p.AddWithValue("@Semester", _semester); });

            var counts = new int[6];
            var labels = new string[6];
            DateTime userToday = Fmt.ToLocal(DateTime.UtcNow, Fmt.CurrentZoneId()).Date; DateTime month = new DateTime(userToday.Year, userToday.Month, 1).AddMonths(-5);
            for (int i = 0; i < 6; i++)
            {
                labels[i] = month.ToString("MMM", CultureInfo.InvariantCulture);
                string key = month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                foreach (DataRow r in dt.Rows)
                    if (string.Equals(Convert.ToString(r["Ym"]), key, StringComparison.Ordinal))
                        counts[i] = Convert.ToInt32(r["C"]);
                month = month.AddMonths(1);
            }
            int max = 1;
            foreach (int c in counts) if (c > max) max = c;
            int total = 0;
            foreach (int c in counts) total += c;

            if (total == 0)
            {
                TrendChart = "<div class='empty-state'><i class='bi bi-graph-up-arrow'></i><h3>No enrollment activity yet</h3><p>New admissions in the last six months will appear here.</p></div>";
                EnrollmentWindowLabel = "last 6 months";
                return;
            }

            var sb = new StringBuilder();
            sb.Append("<div class='mchart' role='img' aria-label='Enrollment trend for the last six months'>");
            for (int i = 0; i < 6; i++)
            {
                int height = Math.Max(4, (int)Math.Round(counts[i] * 100.0 / max));
                sb.Append("<div class='bar' style='height:").Append(height).Append("%' data-label='")
                  .Append(labels[i]).Append(": ").Append(counts[i]).Append(" student(s)'></div>");
            }
            sb.Append("</div><div class='mchart-axis'>");
            foreach (string l in labels) sb.Append("<span>").Append(l).Append("</span>");
            sb.Append("</div>");
            sb.Append("<div class='d-flex justify-content-between mt-2 small text-muted'><span><span class='legend-dot'></span>")
              .Append(total).Append(" new students in six months</span><span>peak ").Append(max).Append(" / month</span></div>");
            TrendChart = sb.ToString();
            EnrollmentWindowLabel = "last 6 months";
        }

        private void BuildFinance()
        {
            DataRow r = Metrics().Rows[0];
            decimal collected = r["Collected"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Collected"]);
            DataTable outstanding = SqlHelper.FillDataTable(
                "SELECT COUNT(*) AS Heads, ISNULL(SUM(f.Amount),0) AS Amount FROM dbo.Fees f " +
                "JOIN dbo.Students s ON s.CourseId=f.CourseId JOIN dbo.Users u ON u.UserId=s.UserId " +
                "WHERE f.IsActive=1 AND u.IsActive=1 AND NOT EXISTS (SELECT 1 FROM dbo.Payments p " +
                "WHERE p.UserId=s.UserId AND p.FeeId=f.FeeId AND p.PayStatus='captured')");
            int heads = Convert.ToInt32(outstanding.Rows[0]["Heads"]);
            decimal due = Convert.ToDecimal(outstanding.Rows[0]["Amount"]);
            decimal total = collected + due;
            int rate = total <= 0 ? 0 : (int)Math.Round(collected * 100m / total);

            var sb = new StringBuilder();
            sb.Append("<div class='finance-summary'><div><div class='finance-amount'>").Append(collected.ToString("N0"))
              .Append("<span>INR</span></div><div class='finance-caption'>Collected in the selected period</div></div>")
              .Append("<div class='finance-chip'><span class='badge badge-ok'>").Append(rate).Append("% collected</span></div></div>");
            sb.Append("<div class='dbar mt-3' role='img' aria-label='Collection rate ").Append(rate).Append(" percent'><span style='width:").Append(Math.Min(100, Math.Max(2, rate))).Append("%'></span></div>");
            sb.Append("<div class='finance-metrics'>");
            sb.Append("<div class='ai-status-item'><div class='k'>Outstanding</div><div class='v'>").Append(due.ToString("N0")).Append(" INR</div></div>");
            sb.Append("<div class='ai-status-item'><div class='k'>Unpaid fee heads</div><div class='v'>").Append(heads).Append("</div></div>");
            sb.Append("<div class='ai-status-item'><div class='k'>Payments in period</div><div class='v'>").Append(Convert.ToInt32(r["Payments"])).Append("</div></div>");
            sb.Append("<div class='ai-status-item'><div class='k'>Open complaints</div><div class='v'>").Append(Convert.ToInt32(r["OpenComplaints"])).Append("</div></div>");
            sb.Append("</div>");
            sb.Append("<div class='finance-actions'><a class='btn btn-outline-sms btn-sm' href='/Admin/Payments.aspx'>Payments</a>")
              .Append("<a class='btn btn-outline-sms btn-sm' href='/Admin/Fees.aspx'>Fee structure</a></div>");
            FinanceCard = sb.ToString();
        }

        private void BuildRisk()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP 8 * FROM (SELECT u.FullName, s.EnrollNumber, c.CourseCode, " +
                "100.0 * SUM(CASE WHEN a.AttnStatus IN ('P','L','E') THEN 1 ELSE 0 END) / NULLIF(COUNT(*),0) AS Pct " +
                "FROM dbo.Attendance a JOIN dbo.Enrollments e ON e.EnrollId=a.EnrollId " +
                "JOIN dbo.Students s ON s.StudentId=e.StudentId JOIN dbo.Users u ON u.UserId=s.UserId " +
                "JOIN dbo.Courses c ON c.CourseId=s.CourseId " +
                "WHERE (@SessionId=0 OR s.SessionId=@SessionId) AND (@Semester=0 OR s.SemesterNumber=@Semester) " +
                "GROUP BY u.FullName, s.EnrollNumber, c.CourseCode) t WHERE t.Pct < @Threshold ORDER BY t.Pct",
                p =>
                {
                    p.AddWithValue("@Threshold", Threshold);
                    p.AddWithValue("@SessionId", _sessionId);
                    p.AddWithValue("@Semester", _semester);
                });
            var sb = new StringBuilder();
            if (dt.Rows.Count == 0)
            {
                sb.Append("<div class='empty-state'><i class='bi bi-shield-check'></i><h3>No students below the threshold</h3><p>Attendance is at or above ").Append(Threshold).Append("% for the filtered cohort.</p></div>");
            }
            else
            {
                sb.Append("<table class='table table-sm'><thead><tr><th>Student</th><th>Course</th><th style='width:120px'>Attendance</th></tr></thead><tbody>");
                foreach (DataRow r in dt.Rows)
                {
                    decimal pct = Convert.ToDecimal(r["Pct"]);
                    sb.Append("<tr><td><span class='row-title'>").Append(E(r["FullName"])).Append("</span><div class='row-sub mono'>")
                      .Append(E(r["EnrollNumber"])).Append("</div></td><td>").Append(E(r["CourseCode"]))
                      .Append("</td><td><div class='d-flex align-items-center gap-2'><div class='dbar bad'><span style='width:")
                      .Append(Math.Max(2, (int)pct)).Append("%'></span></div><span class='mono'>").Append(pct.ToString("0.0")).Append("%</span></div></td></tr>");
                }
                sb.Append("</tbody></table>");
            }
            RiskTable = sb.ToString();
        }

        private void BuildExams()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP 5 e.ExamName, e.ExamDate, e.IsPublished, sub.SubjectName, c.CourseCode FROM dbo.Exams e " +
                "JOIN dbo.Subjects sub ON sub.SubjectId=e.SubjectId JOIN dbo.Courses c ON c.CourseId=e.CourseId " +
                "WHERE e.ExamDate >= CAST(GETDATE() AS date) ORDER BY e.ExamDate",
                null);
            var sb = new StringBuilder();
            if (dt.Rows.Count == 0)
            {
                sb.Append("<div class='empty-state'><i class='bi bi-calendar2-week'></i><h3>No upcoming examinations</h3><p>Schedule exams from the Exams and results page.</p></div>");
            }
            else
            {
                sb.Append("<table class='table table-sm'><thead><tr><th>Date</th><th>Examination</th><th>Status</th></tr></thead><tbody>");
                foreach (DataRow r in dt.Rows)
                {
                    bool published = Convert.ToBoolean(r["IsPublished"]);
                    sb.Append("<tr><td><span class='mono'>").Append(((DateTime)r["ExamDate"]).ToString("dd MMM")).Append("</span></td>")
                      .Append("<td><span class='row-title'>").Append(E(r["ExamName"])).Append("</span><div class='row-sub'>")
                      .Append(E(r["SubjectName"])).Append(" &middot; ").Append(E(r["CourseCode"])).Append("</div></td>")
                      .Append("<td><span class='badge ").Append(published ? "badge-ok" : "badge-warn").Append("'>")
                      .Append(published ? "Published" : "Draft").Append("</span></td></tr>");
                }
                sb.Append("</tbody></table>");
            }
            ExamTable = sb.ToString();
        }

        private void BuildAudit()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP 6 a.AuditId, a.Action, a.EntityTable, a.DetailJson, a.ClientIp, a.CreatedAtUtc, u.FullName " +
                "FROM dbo.AuditLogs a LEFT JOIN dbo.Users u ON u.UserId=a.UserId " +
                "WHERE a.CreatedAtUtc >= @From AND a.CreatedAtUtc < DATEADD(day,1,@To) ORDER BY a.AuditId DESC",
                p => { p.AddWithValue("@From", _from); p.AddWithValue("@To", _to); });
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm table-audit'>");
            sb.Append("<thead><tr><th>When</th><th>User</th><th>Action</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='3' class='msg-empty'>No activity in this period.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                string who = r["FullName"] == DBNull.Value ? "" : Convert.ToString(r["FullName"]);
                if (string.IsNullOrWhiteSpace(who)) who = "System";
                sb.Append("<tr><td class='text-muted' style='white-space:nowrap'>" + E(When(r)) + "</td>");
                sb.Append("<td class='row-who'>" + E(who) + "</td>");
                sb.Append("<td><span class='mono'>" + E(r["Action"]) + "</span><div class='row-sub'>" + E(Truncate(r["DetailJson"] as string, 70)) + "</div></td></tr>");
            }
            sb.Append("</tbody></table>");
            RecentAudit = sb.ToString();
        }

        private void BuildCourseSplit()
        {
            DataTable dt;
            try { dt = SqlHelper.FillDataSet("dbo.usp_GetCourseDistribution", CommandType.StoredProcedure, null).Tables[0]; }
            catch (Exception)
            {
                dt = SqlHelper.FillDataTable(
                    "SELECT c.CourseCode, c.CourseName, COUNT(st.StudentId) AS StudentCount FROM dbo.Courses c " +
                    "LEFT JOIN dbo.Students st ON st.CourseId=c.CourseId GROUP BY c.CourseCode, c.CourseName ORDER BY c.CourseCode");
            }
            int total = 0;
            var counts = new int[dt.Rows.Count];
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                object countValue = dt.Columns.Contains("EnrolledStudents") ? dt.Rows[i]["EnrolledStudents"] : dt.Rows[i]["StudentCount"];
                counts[i] = countValue == DBNull.Value ? 0 : Convert.ToInt32(countValue);
                total += counts[i];
            }
            if (total == 0) total = 1;

            var sb = new StringBuilder("<table class='table table-sm table-course'><thead><tr><th>Course</th><th>Students</th><th style='width:90px'></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='3' class='msg-empty'>No courses yet.</td></tr>");
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                DataRow r = dt.Rows[i];
                sb.Append("<tr><td><span class='row-title'>").Append(E(r["CourseCode"])).Append("</span><div class='row-sub'>")
                  .Append(E(r["CourseName"])).Append("</div></td><td class='mono'>").Append(counts[i])
                  .Append("</td><td><div class='dbar'><span style='width:").Append(Math.Round(counts[i] * 100.0 / total)).Append("%'></span></div></td></tr>");
            }
            sb.Append("</tbody></table>");
            CourseSplit = sb.ToString();
        }

        private void BuildAnnouncements()
        {
            DataTable dt = CommsRepo.AnnouncementsVisible(CurrentUser.RoleName);
            var sb = new StringBuilder();
            int shown = 0;
            foreach (DataRow r in dt.Rows)
            {
                if (shown++ >= 3) break;
                bool pinned = r.Table.Columns.Contains("IsPinned") && Convert.ToBoolean(r["IsPinned"]);
                sb.Append("<div class='d-flex gap-2 border-bottom pb-2 mb-2'>");
                sb.Append("<div class='snp-ic'><i class='bi ").Append(pinned ? "bi-pin-angle-fill" : "bi-megaphone").Append("'></i></div>");
                sb.Append("<div><span class='row-title'>").Append(E(r["AnnTitle"])).Append("</span>");
                if (pinned) sb.Append(" <span class='badge badge-info'>Pinned</span>");
                sb.Append("<div class='row-sub'>").Append(E(Truncate(Convert.ToString(r["AnnBody"]), 180))).Append("</div>");
                sb.Append("<div class='row-sub mono'>").Append(E(r["AuthorName"])).Append(" &middot; ")
                  .Append(((DateTime)r["CreatedAtUtc"]).ToString("yyyy-MM-dd")).Append("</div></div></div>");
            }
            if (shown == 0) sb.Append("<div class='empty-state'><i class='bi bi-megaphone'></i><h3>No active announcements</h3><p>Publish one to reach students and faculty.</p></div>");
            AnnouncementList = sb.ToString();
        }

        private static string When(DataRow r)
        {
            object v = r["CreatedAtUtc"];
            return v == DBNull.Value ? "" : ((DateTime)v).ToString("dd MMM HH:mm");
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length <= max ? s : s.Substring(0, max - 3) + "...";
        }
    }
}


