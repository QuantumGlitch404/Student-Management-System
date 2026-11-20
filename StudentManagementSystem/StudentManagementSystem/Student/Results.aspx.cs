using System;
using System.Data;
using System.Text;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Student
{
    /// <summary>Published, grade-backed results for the signed-in student (usp_GetStudentResultSheet).</summary>
    public partial class Results : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Student" }; } }

        protected string SheetTable, FlashHtml;

        protected void Page_Load(object sender, EventArgs e)
        {
            FlashHtml = PopFlash();
            DataTable dt;
            try
            {
                dt = SqlHelper.FillDataSet("dbo.usp_GetStudentResultSheet", CommandType.StoredProcedure,
                    p => p.AddWithValue("@StudentUserId", CurrentUser.UserId)).Tables[0];
            }
            catch (Exception) { dt = new DataTable(); }

            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Exam</th><th>Subject</th><th>Date</th><th>Marks</th><th>Percent</th><th>Grade</th><th>Result</th><th>Published</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='8' class='msg-empty'>No published result yet. Grades appear here after a faculty publishes an exam.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                bool pass = Convert.ToBoolean(r["IsPass"]);
                sb.Append("<tr><td><b>" + E(r["ExamName"]) + "</b></td>");
                sb.Append("<td>" + E(r["SubjectCode"]) + " " + E(r["SubjectName"]) + "</td>");
                sb.Append("<td>" + (r["ExamDate"] == DBNull.Value ? "-" : Convert.ToDateTime(r["ExamDate"]).ToString("yyyy-MM-dd")) + "</td>");
                sb.Append("<td>" + Convert.ToDecimal(r["MarksObtained"]).ToString("0.##") + " / " + Convert.ToDecimal(r["MaxMarks"]).ToString("0.##") + "</td>");
                sb.Append("<td>" + Convert.ToDecimal(r["PercentScore"]).ToString("0.##") + "%</td>");
                sb.Append("<td><b>" + E(r["Grade"]) + "</b></td>");
                sb.Append("<td><span class='badge " + (pass ? "badge-ok" : "badge-bad") + "'>" + (pass ? "Pass" : "Fail") + "</span></td>");
                sb.Append("<td class='small'>" + (r["PublishedAtUtc"] == DBNull.Value || r["PublishedAtUtc"] == null
                    ? "-" : Convert.ToDateTime(r["PublishedAtUtc"]).ToString("yyyy-MM-dd")) + "</td></tr>");
            }
            sb.Append("</tbody></table>");
            SheetTable = sb.ToString();
        }
    }
}
