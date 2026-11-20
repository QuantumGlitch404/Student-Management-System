using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Admin
{
    /// <summary>Exam creation, publishing (usp_CalculateExamResults), result sheets.</summary>
    public partial class Exams : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, ExamTable, CourseOptions, SubjectOptions, ResultSheet;
        protected string SummaryCards, CourseFilterOptions, ExportQuery, Status, Q;
        protected int SheetExamId;
        private int _courseId;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            int.TryParse(Request.QueryString["sheet"], out SheetExamId);
            int.TryParse(Request.QueryString["courseId"], out _courseId);
            Status = (Request.QueryString["status"] ?? "").Trim().ToLowerInvariant();
            if (Status != "published" && Status != "draft") Status = "";
            Q = (Request.QueryString["q"] ?? "").Trim();
            if (Q.Length > 60) Q = Q.Substring(0, 60);

            BuildTable();
            BuildOptions();
            BuildFilterUrl();
            BuildSummary();
            if (SheetExamId > 0) BuildSheet();
        }

        private void HandlePost()
        {
            string error = null;
            try
            {
                switch (Request.Form["op"])
                {
                    case "newexam":
                        int id = MarksRepo.CreateExam(
                            (Request.Form["name"] ?? "").Trim(),
                            int.Parse(Request.Form["courseId"]),
                            int.Parse(Request.Form["subjectId"]),
                            CurrentUser.UserId,
                            decimal.Parse(Request.Form["maxMarks"] ?? "100"),
                            decimal.Parse(Request.Form["passPct"] ?? "33"),
                            Request.Form["examDate"] ?? "",
                            int.Parse(Request.Form["weightType"] ?? "1"));
                        Flash("success", "Exam #" + id + " created. Faculty can now enter marks.");
                        break;

                    case "publish":
                        {
                            int examId = int.Parse(Request.Form["examId"]);
                            int entered = SqlHelper.ExecuteScalarInt(
                                "SELECT COUNT(*) FROM dbo.Marks WHERE ExamId = @Id",
                                p => p.AddWithValue("@Id", examId));
                            if (entered == 0) { Flash("warning", "No marks entered for that exam yet."); break; }
                            MarksRepo.CalculateResults(examId);
                            SqlHelper.ExecuteNonQuery(
                                "UPDATE dbo.Exams SET IsPublished = 1, PublishedAtUtc = SYSUTCDATETIME() WHERE ExamId = @Id",
                                p => p.AddWithValue("@Id", examId));
                            AuditService.Log("ExamPublished", "Exams", examId);
                            Flash("success", "Results calculated for exam #" + examId + " and visible to students.");
                            SheetExamId = examId;
                            break;
                        }

                    case "unpublish":
                        {
                            int examId = int.Parse(Request.Form["examId"]);
                            SqlHelper.ExecuteNonQuery(
                                "UPDATE dbo.Exams SET IsPublished = 0, PublishedAtUtc = NULL WHERE ExamId = @Id",
                                p => p.AddWithValue("@Id", examId));
                            AuditService.Log("ExamUnpublished", "Exams", examId);
                            Flash("warning", "Exam #" + examId + " moved back to draft. Students can no longer see it.");
                            break;
                        }

                    case "deleteexam":
                        MarksRepo.DeleteExam(int.Parse(Request.Form["examId"]));
                        Flash("info", "Exam deleted (marks and results cascade).");
                        break;

                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }

            Redirect("/Admin/Exams.aspx" + (error != null ? "?err=" + HttpUtility.UrlEncode(error) : ""));
        }

        private void BuildOptions()
        {
            CourseOptions = Options("CourseId", "CourseName", AcademicsRepo.Courses(), "");
            SubjectOptions = Options("SubjectId", "SubjectName", AcademicsRepo.Subjects(), "");

            var c = new StringBuilder("<option value='0'>All courses</option>");
            foreach (DataRow r in AcademicsRepo.Courses().Rows)
                c.Append("<option value='").Append(r["CourseId"]).Append("'")
                 .Append(Convert.ToInt32(r["CourseId"]) == _courseId ? " selected" : "").Append(">")
                 .Append(E(r["CourseCode"])).Append(" - ").Append(E(r["CourseName"])).Append("</option>");
            CourseFilterOptions = c.ToString();
        }

        private void BuildFilterUrl()
        {
            var q = new StringBuilder();
            if (_courseId > 0) q.Append("&courseId=").Append(_courseId);
            if (Status.Length > 0) q.Append("&status=").Append(Status);
            if (Q.Length > 0) q.Append("&q=").Append(HttpUtility.UrlEncode(Q));
            ExportQuery = _courseId > 0 ? "&courseId=" + _courseId : "";
        }

        private DataTable FilteredExams()
        {
            DataTable dt = MarksRepo.Exams(_courseId > 0 ? (int?)_courseId : null, null);
            for (int i = dt.Rows.Count - 1; i >= 0; i--)
            {
                DataRow r = dt.Rows[i];
                bool published = Convert.ToBoolean(r["IsPublished"]);
                if (Status == "published" && !published) { dt.Rows.RemoveAt(i); continue; }
                if (Status == "draft" && published) { dt.Rows.RemoveAt(i); continue; }
                if (Q.Length > 0)
                {
                    string hay = Convert.ToString(r["ExamName"]) + " " + Convert.ToString(r["SubjectName"]) + " " + Convert.ToString(r["SubjectCode"]);
                    if (hay.IndexOf(Q, StringComparison.OrdinalIgnoreCase) < 0) dt.Rows.RemoveAt(i);
                }
            }
            return dt;
        }

        private void BuildSummary()
        {
            DataTable dt = FilteredExams();
            int published = 0, drafts = 0, marks = 0;
            foreach (DataRow r in dt.Rows)
            {
                if (Convert.ToBoolean(r["IsPublished"])) published++; else drafts++;
                marks += Convert.ToInt32(r["MarksCount"]);
            }

            DataTable res = SqlHelper.FillDataTable(
                "SELECT COUNT(*) AS Total, ISNULL(AVG(r.PercentScore),0) AS AvgPct, " +
                "ISNULL(SUM(CASE WHEN r.IsPass=1 THEN 1 ELSE 0 END),0) AS Passed " +
                "FROM dbo.Results r JOIN dbo.Exams e ON e.ExamId=r.ExamId WHERE r.PublishedAtUtc IS NOT NULL " +
                "AND (@CourseId=0 OR e.CourseId=@CourseId)",
                p => p.AddWithValue("@CourseId", _courseId));
            DataRow s = res.Rows[0];
            int total = Convert.ToInt32(s["Total"]);
            decimal passRate = total == 0 ? 0 : Math.Round(Convert.ToInt32(s["Passed"]) * 100m / total, 1);

            var sb = new StringBuilder("<div class='bento'>");
            Kpi(sb, "Exams", dt.Rows.Count.ToString("N0"), "bi-journal-text");
            Kpi(sb, "Published", published.ToString("N0"), "bi-check2-circle");
            Kpi(sb, "Marks captured", marks.ToString("N0"), "bi-pencil-square");
            Kpi(sb, "Pass rate", passRate.ToString("0.0") + "%", "bi-graph-up-arrow");
            sb.Append("</div>");
            SummaryCards = sb.ToString();
        }

        private static void Kpi(StringBuilder sb, string label, string value, string icon)
        {
            sb.Append("<div class='c-3'><div class='stat-card'><div class='stat-head'>").Append(label).Append("</div><div class='stat-body'><div class='num'>").Append(value).Append("</div></div></div></div>");
        }

        private void BuildTable()
        {
            DataTable dt = FilteredExams();
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Exam</th><th>Course / Subject</th><th>Max</th><th>Marks entered</th><th>Status</th><th style='width:250px'>Actions</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>No exam matches these filters.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                bool published = Convert.ToBoolean(r["IsPublished"]);
                sb.Append("<tr><td><b>").Append(E(r["ExamName"])).Append("</b><div class='row-sub mono'>#").Append(E(r["ExamId"]));
                if (r["ExamDate"] != DBNull.Value && r["ExamDate"] != null)
                    sb.Append(" &middot; ").Append(((DateTime)r["ExamDate"]).ToString("yyyy-MM-dd"));
                sb.Append("</div></td>");
                sb.Append("<td>").Append(E(r["CourseCode"])).Append(" / ").Append(E(r["SubjectCode"])).Append("</td>");
                sb.Append("<td class='mono'>").Append(E(r["MaxMarks"])).Append("</td>");
                sb.Append("<td class='mono'>").Append(E(r["MarksCount"])).Append("</td>");
                sb.Append("<td><span class='badge ").Append(published ? "badge-ok" : "badge-warn").Append("'>")
                  .Append(published ? "Published" : "Draft").Append("</span></td>");
                sb.Append("<td>");
                if (published)
                {
                    sb.Append("<a class='imbtn' href='/Admin/Exams.aspx?sheet=").Append(E(r["ExamId"])).Append("'>Sheet</a> ");
                    sb.Append(ImbForm("unpublish", new[] { new[] { "examId", E(r["ExamId"]) } }, "Withdraw", true));
                }
                else
                {
                    sb.Append(ImbForm("publish", new[] { new[] { "examId", E(r["ExamId"]) } }, "Publish results", false));
                }
                sb.Append(ImbForm("deleteexam", new[] { new[] { "examId", E(r["ExamId"]) } }, "Delete", true));
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            ExamTable = sb.ToString();
        }

        private void BuildSheet()
        {
            DataTable dt = MarksRepo.ResultSheet(SheetExamId);
            int pass = 0, fail = 0;
            decimal sum = 0;
            var grades = new System.Collections.Generic.SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in dt.Rows)
            {
                if (Convert.ToBoolean(r["IsPass"])) pass++; else fail++;
                if (r["PercentScore"] != DBNull.Value) sum += Convert.ToDecimal(r["PercentScore"]);
                string g = Convert.ToString(r["Grade"]);
                if (g.Length > 0) grades[g] = (grades.ContainsKey(g) ? grades[g] : 0) + 1;
            }

            var sb = new StringBuilder();
            if (dt.Rows.Count > 0)
            {
                sb.Append("<div class='d-flex flex-wrap gap-3 mb-3'>");
                sb.Append("<span class='badge badge-ok'>").Append(pass).Append(" passed</span>");
                sb.Append("<span class='badge badge-bad'>").Append(fail).Append(" failed</span>");
                sb.Append("<span class='badge badge-info'>average ").Append((sum / dt.Rows.Count).ToString("0.0")).Append("%</span>");
                foreach (var g in grades) sb.Append("<span class='badge badge-off mono'>").Append(E(g.Key)).Append(": ").Append(g.Value).Append("</span>");
                sb.Append("</div>");
            }
            sb.Append("<table class='table table-sm'><thead><tr><th>Enrollment</th><th>Student</th><th>Marks</th><th>Percent</th><th>Grade</th><th>Result</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>No result rows. Publish first.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                bool p = Convert.ToBoolean(r["IsPass"]);
                sb.Append("<tr><td class='mono'>").Append(E(r["EnrollNumber"])).Append("</td><td>").Append(E(r["FullName"])).Append("</td>");
                sb.Append("<td class='mono'>").Append(E(r["MarksObtained"])).Append(" / ").Append(E(r["MaxMarks"])).Append("</td>");
                sb.Append("<td>").Append(E(r["PercentScore"])).Append("%</td>");
                sb.Append("<td><b>").Append(E(r["Grade"])).Append("</b></td>");
                sb.Append("<td><span class='badge ").Append(p ? "badge-ok" : "badge-bad").Append("'>").Append(p ? "Pass" : "Fail").Append("</span></td></tr>");
            }
            sb.Append("</tbody></table>");
            ResultSheet = sb.ToString();
        }

        private string ImbForm(string op, string[][] fields, string label, bool confirm)
        {
            var sb = new StringBuilder();
            sb.Append("<form method='post' action='/Admin/Exams.aspx' style='display:inline'")
              .Append(confirm ? " data-confirm='Change this exam? Published results may become unavailable.'" : "").Append(">");
            sb.Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='").Append(Attr(op)).Append("'/>");
            foreach (string[] f in fields)
                sb.Append("<input type='hidden' name='").Append(f[0]).Append("' value='").Append(f[1]).Append("'/>");
            sb.Append("<button class='imbtn'>").Append(label).Append("</button></form> ");
            return sb.ToString();
        }
    }
}
