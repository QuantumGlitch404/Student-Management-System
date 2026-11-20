using System;
using System.Data;
using System.Text;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Student
{
    /// <summary>Student home - one usp_GetStudentDashboard call feeds every card.</summary>
    public partial class Index : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Student" }; } }

        protected string FlashHtml, StatCards, NoticeList, Grades, FeesDue, Announcements, Upcoming, Greeting, TaskHtml;

        protected void Page_Load(object sender, EventArgs e)
        {
            FlashHtml = PopFlash();
            Greeting = (CurrentUser.FullName ?? "My dashboard").Trim();
            BuildTasks();
            BuildUpcoming();
            DataSet ds;
            try { ds = SqlHelper.FillDataSet("dbo.usp_GetStudentDashboard", CommandType.StoredProcedure, p => p.AddWithValue("@StudentUserId", CurrentUser.UserId)); }
            catch (Exception) { StatCards = "<p class='msg-empty'>No student record is linked to this account yet. Contact the office.</p>"; Grades = FeesDue = Announcements = ""; return; }

            BuildStats(ds);
            BuildFees(ds);
            BuildGrades(ds);
            BuildNotices(ds);
            BuildAnnouncements();
        }

        private void StatCard(StringBuilder sb, string icon, string num, string label)
        {
            string head = label ?? "";
            string sub = "";
            int cut = head.IndexOf(" (", StringComparison.Ordinal);
            if (cut > 0)
            {
                sub = head.Substring(cut + 2).TrimEnd(')');
                head = head.Substring(0, cut);
            }
            sb.Append("<div class='c-3'><div class='stat-card'><div class='stat-head'>" + E(head) + "</div>")
              .Append("<div class='stat-body'><div class='num'>" + E(num) + "</div>")
              .Append(sub.Length == 0 ? "" : "<div class='stat-delta'>" + E(sub) + "</div>")
              .Append("</div></div></div>");
        }

        private void BuildStats(DataSet ds)
        {
            var sb = new StringBuilder("<div class='bento'>");
            if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
            {
                DataRow a = ds.Tables[0].Rows[0];
                // SUM()/ROUND() return NULL when the student has no attendance rows yet
                // (and the minimum setting may be absent), so read them null-safely.
                decimal pct = Dec(a, "AttendancePercent");
                decimal min = Dec(a, "MinimumRequired");
                StatCard(sb, "bi-clipboard-check", pct.ToString("0.#") + "%", "Attendance (minimum " + min.ToString("0.#") + "%)");
            }
            else StatCard(sb, "bi-clipboard-check", "-", "Attendance");

            if (ds.Tables.Count > 3 && ds.Tables[3].Rows.Count > 0)
                StatCard(sb, "bi-flag", Convert.ToInt32(ds.Tables[3].Rows[0]["OpenComplaints"]).ToString(), "Open complaints");
            else StatCard(sb, "bi-flag", "0", "Open complaints");

            if (ds.Tables.Count > 4)
            {
                int due = 0;
                foreach (DataRow r in ds.Tables[4].Rows) if (Convert.ToInt32(r["PaidCount"]) == 0) due++;
                StatCard(sb, "bi-cash-coin", due.ToString(), "Fee heads unpaid");
            }
            else StatCard(sb, "bi-cash-coin", "-", "Fees");

            int published = ds.Tables.Count > 2 ? ds.Tables[2].Rows.Count : 0;
            StatCard(sb, "bi-award", published.ToString(), "Published results");
            int unread = 0;
            if (ds.Tables.Count > 5) foreach (DataRow r in ds.Tables[5].Rows) if (!Convert.ToBoolean(r["IsRead"])) unread++;
            StatCard(sb, "bi-bell", unread.ToString(), "Unread notifications");

            sb.Append("</div>");
            StatCards = sb.ToString();
        }

        private static decimal Dec(DataRow row, string column)
        {
            if (row == null || !row.Table.Columns.Contains(column)) return 0m;
            object value = row[column];
            return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
        }

        private void BuildFees(DataSet ds)
        {
            DataTable fees = ds.Tables.Count > 4 ? ds.Tables[4] : new DataTable();
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Fee head</th><th>Amount</th><th>Status</th><th></th></tr></thead><tbody>");
            if (fees.Rows.Count == 0) sb.Append("<tr><td colspan='4' class='msg-empty'>No fee head for your course yet.</td></tr>");
            foreach (DataRow r in fees.Rows)
            {
                bool paid = Convert.ToInt32(r["PaidCount"]) > 0;
                sb.Append("<tr><td>" + E(r["FeeName"]) + "</td><td>" + Convert.ToDecimal(r["Amount"]).ToString("N2") + "</td>");
                sb.Append("<td><span class='badge " + (paid ? "badge-ok" : "badge-warn") + "'>" + (paid ? "Paid" : "Due") + "</span></td>");
                sb.Append("<td>" + (paid ? "" : "<a class='imbtn' href='/Student/Fees.aspx'>Pay now</a>") + "</td></tr>");
            }
            sb.Append("</tbody></table>");
            FeesDue = sb.ToString();
        }

        private void BuildGrades(DataSet ds)
        {
            DataTable results = ds.Tables.Count > 2 ? ds.Tables[2] : new DataTable();
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Exam</th><th>Subject</th><th>Marks</th><th>Grade</th><th>Result</th></tr></thead><tbody>");
            if (results.Rows.Count == 0) sb.Append("<tr><td colspan='5' class='msg-empty'>No published result yet.</td></tr>");
            foreach (DataRow r in results.Rows)
            {
                bool pass = Convert.ToBoolean(r["IsPass"]);
                sb.Append("<tr><td>" + E(r["ExamName"]) + "</td><td>" + E(r["SubjectName"]) + "</td>");
                sb.Append("<td>" + (r["MarksObtained"] == DBNull.Value ? "-" : Convert.ToDecimal(r["MarksObtained"]).ToString("0.##")) +
                    " / " + Convert.ToDecimal(r["MaxMarks"]).ToString("0.##") + "</td>");
                sb.Append("<td><b>" + E(r["Grade"]) + "</b></td>");
                sb.Append("<td><span class='badge " + (pass ? "badge-ok" : "badge-bad") + "'>" + (pass ? "Pass" : "Fail") + "</span></td></tr>");
            }
            sb.Append("</tbody></table>");
            Grades = sb.ToString();
        }

        private void BuildNotices(DataSet ds)
        {
            DataTable notices = ds.Tables.Count > 5 ? ds.Tables[5] : new DataTable();
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Title</th><th>When</th></tr></thead><tbody>");
            if (notices.Rows.Count == 0) sb.Append("<tr><td colspan='2' class='msg-empty'>No notification.</td></tr>");
            foreach (DataRow r in notices.Rows)
            {
                bool unread = !Convert.ToBoolean(r["IsRead"]);
                // NotifyGo marks it read and opens its own destination; the row itself
                // never trusts ActionUrl for the redirect decision.
                string href = r.Table.Columns.Contains("NotificationId")
                    ? "/Handlers/NotifyGo.ashx?id=" + E(r["NotificationId"])
                    : "/Student/Notifications.aspx";
                sb.Append("<tr><td>" + (unread ? "<b>" : "") +
                    "<a class='text-decoration-none' style='color:inherit' href='" + Attr(href) + "'>" + E(r["Title"]) + "</a>" +
                    (unread ? "</b> <span class='badge badge-warn'>new</span>" : "") + "</td>");
                sb.Append("<td class='small'>" + ((DateTime)r["CreatedAtUtc"]).ToString("yyyy-MM-dd HH:mm") + "</td></tr>");
            }
            sb.Append("</tbody></table>");
            NoticeList = sb.ToString();
        }

        private void BuildTasks()
        {
            var sb = new StringBuilder("<div class='tile mb-3'><div class='tile__head'>Quick tasks</div><div class='tile__body'><div class='task-grid'>");
            Task(sb, "/Student/Fees.aspx", "bi-credit-card", "Pay fees");
            Task(sb, "/Student/Results.aspx", "bi-award", "View results");
            Task(sb, "/Student/OnlineTests.aspx", "bi-patch-question", "Online tests");
            Task(sb, "/Timetable/Index.aspx", "bi-calendar3", "Timetable");
            Task(sb, "/Student/Complaints.aspx", "bi-life-preserver", "Raise a complaint");
            Task(sb, "/Student/Profile.aspx", "bi-person", "Update profile");
            sb.Append("</div></div></div>"); TaskHtml = sb.ToString();
        }
        private static void Task(StringBuilder sb, string href, string icon, string label)
        {
            sb.Append("<a class='task-btn' href='").Append(href).Append("'><i class='bi ").Append(icon).Append("'></i><span>").Append(label).Append("</span></a>");
        }
        private void BuildUpcoming()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP 6 e.ExamName, sub.SubjectName, e.WeightType AS ExamType, e.ExamDate, e.MaxMarks, e.IsPublished " +
                "FROM dbo.Exams e JOIN dbo.Subjects sub ON sub.SubjectId=e.SubjectId " +
                "JOIN dbo.Students st ON st.CourseId=e.CourseId " +
                "WHERE st.UserId=@Me AND e.ExamDate >= CAST(GETDATE() AS date) ORDER BY e.ExamDate",
                p => p.AddWithValue("@Me", CurrentUser.UserId));
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Date</th><th>Examination</th><th>Subject</th><th>Type</th><th>Max marks</th><th>Status</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>No upcoming examination is scheduled for your course.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                bool published = r["IsPublished"] != DBNull.Value && Convert.ToBoolean(r["IsPublished"]);
                sb.Append("<tr><td class='mono'>" + ((DateTime)r["ExamDate"]).ToString("yyyy-MM-dd") + "</td>");
                sb.Append("<td><b>" + E(r["ExamName"]) + "</b></td><td>" + E(r["SubjectName"]) + "</td>");
                sb.Append("<td>" + E(r["ExamType"]) + "</td><td>" + E(r["MaxMarks"]) + "</td>");
                sb.Append("<td><span class='badge " + (published ? "badge-ok" : "badge-warn") + "'>" + (published ? "Published" : "Scheduled") + "</span></td></tr>");
            }
            sb.Append("</tbody></table>");
            Upcoming = sb.ToString();
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

