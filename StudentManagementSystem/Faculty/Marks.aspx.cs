using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Faculty
{
    /// <summary>Exams scoped to the signed-in faculty's assignments: create, enter marks, publish.</summary>
    public partial class Marks : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Faculty" }; } }

        protected string FlashHtml, ExamTable, AssignmentOptions, MarksTable, SheetTable, OpenInfo;
        protected int OpenExamId;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            BuildAssignmentOptions();
            int.TryParse(Request.QueryString["exam"], out OpenExamId);
            BuildExamTable();
            if (OpenExamId > 0) { BuildMarks(); BuildSheet(); }
        }

        private void HandlePost()
        {
            string error = null;
            try
            {
                switch (Request.Form["op"])
                {
                    case "newexam":
                        {
                            DataRow a = AssignmentById(int.Parse(Request.Form["assignmentId"]));
                            if (a == null) throw new Exception("Pick one of your assignments.");
                            int examId = MarksRepo.CreateExam((Request.Form["name"] ?? "").Trim(),
                                Convert.ToInt32(a["CourseId"]), Convert.ToInt32(a["SubjectId"]), CurrentUser.UserId,
                                decimal.Parse(Request.Form["maxMarks"] ?? "100"),
                                decimal.Parse(Request.Form["passPct"] ?? "33"),
                                Request.Form["examDate"] ?? "", 1);
                            Flash("success", "Exam #" + examId + " created for " + a["SubjectCode"] + ".");
                            OpenExamId = examId;
                            break;
                        }
                    case "save":
                        {
                            int examId = int.Parse(Request.Form["examId"]);
                            DataRow exam = MarksRepo.OneExam(examId);
                            if (exam == null || !OwnsExam(exam)) throw new Exception("That exam is not yours.");
                            if (Convert.ToBoolean(exam["IsPublished"])) throw new Exception("Exam already published; results locked.");

                            var marks = new System.Collections.Generic.Dictionary<int, decimal>();
                            foreach (string key in Request.Form.AllKeys)
                            {
                                if (key == null || !key.StartsWith("mk_")) continue;
                                int enrollId;
                                if (!int.TryParse(key.Substring(3), out enrollId)) continue;
                                decimal value;
                                if (!decimal.TryParse(Request.Form[key], out value)) continue;
                                marks[enrollId] = value;
                            }
                            if (marks.Count == 0) throw new Exception("Nothing entered.");
                            MarksRepo.SaveMarks(examId, CurrentUser.UserId, marks);
                            Flash("success", "Marks saved for " + marks.Count + " student(s).");
                            OpenExamId = examId;
                            break;
                        }
                    case "publish":
                        {
                            int examId = int.Parse(Request.Form["examId"]);
                            DataRow exam = MarksRepo.OneExam(examId);
                            if (exam == null || !OwnsExam(exam)) throw new Exception("That exam is not yours.");
                            int rows = SqlHelper.ExecuteScalarInt("SELECT COUNT(*) FROM dbo.Marks WHERE ExamId = @Id", p => p.AddWithValue("@Id", examId));
                            if (rows == 0) { Flash("warning", "Enter marks first."); }
                            else
                            {
                                MarksRepo.CalculateResults(examId);
                                SqlHelper.ExecuteNonQuery(
                                    "UPDATE dbo.Exams SET IsPublished = 1, PublishedAtUtc = SYSUTCDATETIME() WHERE ExamId = @Id",
                                    p => p.AddWithValue("@Id", examId));
                                Flash("success", "Results published; students can now see them.");
                            }
                            OpenExamId = examId;
                            break;
                        }
                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }
            // Nothing rendered an ?err= parameter, so the reason a save/publish failed
            // was invisible; show it through the one-shot flash like every other page.
            if (error != null) Flash("danger", error);
            Redirect(OpenExamId > 0 ? "/Faculty/Marks.aspx?exam=" + OpenExamId : "/Faculty/Marks.aspx");
        }

        private DataRow AssignmentById(int assignmentId)
        {
            foreach (DataRow r in AcademicsRepo.FacultyAssignments(CurrentUser.UserId).Rows)
                if (Convert.ToInt32(r["AssignmentId"]) == assignmentId) return r;
            return null;
        }

        private bool OwnsExam(DataRow exam)
        {
            foreach (DataRow r in AcademicsRepo.FacultyAssignments(CurrentUser.UserId).Rows)
                if (Convert.ToInt32(r["SubjectId"]) == Convert.ToInt32(exam["SubjectId"]) &&
                    Convert.ToInt32(r["CourseId"]) == Convert.ToInt32(exam["CourseId"])) return true;
            return false;
        }

        private void BuildAssignmentOptions()
        {
            DataTable dt = AcademicsRepo.FacultyAssignments(CurrentUser.UserId);
            var sb = new StringBuilder();
            foreach (DataRow r in dt.Rows)
                sb.Append("<option value='" + E(r["AssignmentId"]) + "'>" + E(r["SubjectCode"]) + " " + E(r["SubjectName"]) +
                    " - " + E(r["CourseCode"]) + " Sem " + E(r["SemesterNumber"]) + "</option>");
            if (dt.Rows.Count == 0) sb.Append("<option value=''>No assignment yet</option>");
            AssignmentOptions = sb.ToString();
        }

        private void BuildExamTable()
        {
            DataTable assigns = AcademicsRepo.FacultyAssignments(CurrentUser.UserId);
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Exam</th><th>Subject</th><th>Max</th><th>Entered</th><th>Status</th><th style='width:220px'>Actions</th></tr></thead><tbody>");
            bool any = false;
            foreach (DataRow a in assigns.Rows)
            {
                DataTable exams = MarksRepo.Exams(Convert.ToInt32(a["CourseId"]), Convert.ToInt32(a["SubjectId"]));
                foreach (DataRow r in exams.Rows)
                {
                    any = true;
                    bool published = Convert.ToBoolean(r["IsPublished"]);
                    sb.Append("<tr><td><b>" + E(r["ExamName"]) + "</b><div class='small text-muted'>#" + E(r["ExamId"]) + "</div></td>");
                    sb.Append("<td>" + E(r["SubjectCode"]) + " (" + E(r["CourseCode"]) + ")</td>");
                    sb.Append("<td>" + E(r["MaxMarks"]) + "</td><td>" + E(r["MarksCount"]) + "</td>");
                    sb.Append("<td><span class='badge " + (published ? "badge-ok" : "badge-warn") + "'>" + (published ? "Published" : "Draft") + "</span></td>");
                    sb.Append("<td>");
                    sb.Append("<a class='imbtn' href='/Faculty/Marks.aspx?exam=" + E(r["ExamId"]) + "'>Open</a> ");
                    sb.Append(published ? "" : ImbForm("publish", new[] { new[] { "examId", E(r["ExamId"]) } }, "Publish"));
                    sb.Append("</td></tr>");
                }
            }
            if (!any) sb.Append("<tr><td colspan='6' class='msg-empty'>No exam yet for your assignments.</td></tr>");
            sb.Append("</tbody></table>");
            ExamTable = sb.ToString();
        }

        private void BuildMarks()
        {
            DataRow exam = MarksRepo.OneExam(OpenExamId);
            if (exam == null) { MarksTable = "<p class='msg-empty'>Exam not found.</p>"; return; }
            if (!OwnsExam(exam)) { MarksTable = "<p class='msg-empty'>That exam belongs to another faculty.</p>"; return; }
            OpenInfo = exam["ExamName"] + " (" + exam["SubjectCode"] + ", max " + exam["MaxMarks"] + ")";

            DataTable roster = MarksRepo.MarksRoster(OpenExamId);
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Enrollment</th><th>Student</th><th style='width:140px'>Marks (0-" + E(exam["MaxMarks"]) + ")</th></tr></thead><tbody>");
            if (roster.Rows.Count == 0) sb.Append("<tr><td colspan='3' class='msg-empty'>No student enrolled in that course/semester.</td></tr>");
            foreach (DataRow r in roster.Rows)
            {
                string existing = r["MarksObtained"] == DBNull.Value || r["MarksObtained"] == null ? "" :
                    Convert.ToDecimal(r["MarksObtained"]).ToString("0.##");
                sb.Append("<tr><td><b>" + E(r["EnrollNumber"]) + "</b></td><td>" + E(r["FullName"]) + "</td>");
                sb.Append("<td><input type='number' name='mk_" + E(r["EnrollId"]) + "' value='" + E(existing) + "' min='0' max='" + Attr(exam["MaxMarks"]) + "' class='form-control form-control-sm'/></td></tr>");
            }
            sb.Append("</tbody></table>");
            MarksTable = sb.ToString();
        }

        private void BuildSheet()
        {
            DataRow exam = MarksRepo.OneExam(OpenExamId);
            if (exam == null || !Convert.ToBoolean(exam["IsPublished"])) { SheetTable = null; return; }
            DataTable dt = MarksRepo.ResultSheet(OpenExamId);
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Enrollment</th><th>Student</th><th>Marks</th><th>Percent</th><th>Grade</th><th>Result</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>No rows.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                bool pass = Convert.ToBoolean(r["IsPass"]);
                sb.Append("<tr><td>" + E(r["EnrollNumber"]) + "</td><td>" + E(r["FullName"]) + "</td>");
                sb.Append("<td>" + E(r["MarksObtained"]) + "</td><td>" + E(r["PercentScore"]) + "%</td>");
                sb.Append("<td><b>" + E(r["Grade"]) + "</b></td>");
                sb.Append("<td><span class='badge " + (pass ? "badge-ok" : "badge-bad") + "'>" + (pass ? "Pass" : "Fail") + "</span></td></tr>");
            }
            sb.Append("</tbody></table>");
            SheetTable = sb.ToString();
        }

        private string ImbForm(string op, string[][] fields, string label)
        {
            var sb = new StringBuilder();
            sb.Append("<form method='post' action='/Faculty/Marks.aspx' style='display:inline'>");
            sb.Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='").Append(Attr(op)).Append("'/>");
            foreach (string[] f in fields)
                sb.Append("<input type='hidden' name='").Append(f[0]).Append("' value='").Append(f[1]).Append("'/>");
            sb.Append("<button class='imbtn'>").Append(label).Append("</button></form> ");
            return sb.ToString();
        }
    }
}
