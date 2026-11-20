using System;
using System.Data;
using System.Text;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Student
{
    /// <summary>Subject-wise attendance summary + every marked session (read only).</summary>
    public partial class Attendance : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Student" }; } }

        protected string SubjectTable, DetailTable, FlashHtml;

        protected void Page_Load(object sender, EventArgs e)
        {
            FlashHtml = PopFlash();
            BuildSubject();
            BuildDetail();
        }

        private void BuildSubject()
        {
            DataTable dt;
            try
            {
                DataSet ds = SqlHelper.FillDataSet("dbo.usp_GetStudentDashboard", CommandType.StoredProcedure,
                    p => p.AddWithValue("@StudentUserId", CurrentUser.UserId));
                dt = ds.Tables.Count > 1 ? ds.Tables[1] : new DataTable();
            }
            catch (Exception) { dt = new DataTable(); }

            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Subject</th><th>Held</th><th>Attended</th><th>Percent</th><th>Status</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='5' class='msg-empty'>No attendance record yet.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                decimal pct = Convert.ToDecimal(r["PercentPresent"]);
                string cls = pct >= 75 ? "badge-ok" : pct >= 60 ? "badge-warn" : "badge-bad";
                sb.Append("<tr><td><b>" + E(r["SubjectCode"]) + "</b> " + E(r["SubjectName"]) + "</td>");
                sb.Append("<td>" + E(r["Held"]) + "</td><td>" + E(r["Attended"]) + "</td>");
                sb.Append("<td>" + pct.ToString("0.#") + "%</td>");
                sb.Append("<td><span class='badge " + cls + "'>" + (pct >= 75 ? "Good" : pct >= 60 ? "Watch" : "Low") + "</span></td></tr>");
            }
            sb.Append("</tbody></table>");
            SubjectTable = sb.ToString();
        }

        private void BuildDetail()
        {
            DataTable dt;
            try
            {
                dt = SqlHelper.FillDataSet("dbo.usp_GetStudentAttendanceDetail", CommandType.StoredProcedure,
                    p => p.AddWithValue("@StudentUserId", CurrentUser.UserId)).Tables[0];
            }
            catch (Exception) { dt = new DataTable(); }

            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Date</th><th>Period</th><th>Subject</th><th>Status</th><th>Remarks</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='5' class='msg-empty'>Nothing marked yet.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                string status = (r["AttnStatus"] as string ?? "");
                string cls = status == "P" ? "badge-ok" : status == "A" ? "badge-bad" : "badge-info";
                string word = status == "P" ? "Present" : status == "A" ? "Absent" : status == "L" ? "Leave" : status == "E" ? "Excused" : status;
                sb.Append("<tr><td>" + ((DateTime)r["SessionDate"]).ToString("yyyy-MM-dd") + "</td>");
                sb.Append("<td>" + E(r["PeriodNo"]) + "</td>");
                sb.Append("<td><b>" + E(r["SubjectCode"]) + "</b> " + E(r["SubjectName"]) + "</td>");
                sb.Append("<td><span class='badge " + cls + "'>" + word + "</span></td>");
                sb.Append("<td class='small'>" + E(r["Remarks"] ?? "") + "</td></tr>");
            }
            sb.Append("</tbody></table>");
            DetailTable = sb.ToString();
        }
    }
}
