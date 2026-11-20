using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Admin
{
    /// <summary>Question bank, test assembly and proctoring review for online tests.</summary>
    public partial class OnlineTests : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, SummaryCards, TestTable, QuestionTable, TestDetailHtml;
        protected string CourseOptions, SubjectOptions, QuestionSubjectOptions, QuestionFilterOptions, CategoryFilterOptions;

        private int SelectedTestId;
        private int _subjectId;
        private int _categoryId;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            int.TryParse(Request.QueryString["test"], out SelectedTestId);
            int.TryParse(Request.QueryString["subjectId"], out _subjectId);
            int.TryParse(Request.QueryString["categoryId"], out _categoryId);
            BuildOptions();
            BuildSummary();
            BuildTestTable();
            BuildQuestionTable();
            if (SelectedTestId > 0) BuildDetail();
        }

        private void HandlePost()
        {
            string error = null;
            int redirectTest = 0;
            try
            {
                switch (Request.Form["op"])
                {
                    case "newtest":
                        {
                            bool opensOk, closesOk;
                            DateTime? opens = ParseLocal(Request.Form["opensAt"], out opensOk);
                            DateTime? closes = ParseLocal(Request.Form["closesAt"], out closesOk);
                            if (!opensOk || !closesOk) throw new Exception("Enter valid opening and closing times.");
                            int id = OnlineTestsRepo.CreateTest(
                                int.Parse(Request.Form["courseId"]), int.Parse(Request.Form["subjectId"]),
                                (Request.Form["title"] ?? "").Trim(), (Request.Form["instructions"] ?? "").Trim(),
                                ParseInt(Request.Form["duration"], 30), opens, closes,
                                ParseInt(Request.Form["attempts"], 1), ParseDecimal(Request.Form["passPct"], 40),
                                Request.Form["shuffle"] == "1", Request.Form["proctor"] == "1", CurrentUser.UserId,
                                ParseInt(Request.Form["warningLimit"], 1));
                            AuditService.Log("TestCreated", "OnlineTests", id, (Request.Form["title"] ?? "").Trim());
                            Flash("success", "Online test #" + id + " created. Attach questions, then publish.");
                            redirectTest = id;
                            break;
                        }

                    case "edittest":
                        {
                            int testId = int.Parse(Request.Form["testId"]);
                            bool opensOk, closesOk;
                            DateTime? opens = ParseLocal(Request.Form["opensAt"], out opensOk);
                            DateTime? closes = ParseLocal(Request.Form["closesAt"], out closesOk);
                            if (!opensOk || !closesOk) throw new Exception("Enter valid opening and closing times.");
                            OnlineTestsRepo.UpdateTest(testId,
                                (Request.Form["title"] ?? "").Trim(), (Request.Form["instructions"] ?? "").Trim(),
                                ParseInt(Request.Form["duration"], 30), opens, closes,
                                ParseInt(Request.Form["attempts"], 1), ParseDecimal(Request.Form["passPct"], 40),
                                Request.Form["shuffle"] == "1", Request.Form["proctor"] == "1",
                                ParseInt(Request.Form["warningLimit"], 1));
                            AuditService.Log("TestUpdated", "OnlineTests", testId);
                            Flash("success", "Test settings updated.");
                            redirectTest = testId;
                            break;
                        }

                    case "publish":
                    case "unpublish":
                        {
                            int testId = int.Parse(Request.Form["testId"]);
                            string warning = OnlineTestsRepo.SetPublished(testId, Request.Form["op"] == "publish");
                            if (warning != null) Flash("warning", warning);
                            else Flash("success", Request.Form["op"] == "publish" ? "Test published to its course." : "Test moved back to draft.");
                            redirectTest = testId;
                            break;
                        }

                    case "deletetest":
                        {
                            int testId = int.Parse(Request.Form["testId"]);
                            int attempts = SqlHelper.ExecuteScalarInt(
                                "SELECT COUNT(*) FROM dbo.OnlineTestAttempts WHERE TestId = @Id",
                                p => p.AddWithValue("@Id", testId));
                            if (attempts > 0) throw new Exception("This test has attempts and cannot be deleted.");
                            OnlineTestsRepo.DeleteTest(testId);
                            Flash("info", "Test deleted.");
                            break;
                        }

                    case "newquestion":
                        {
                            int questionId = OnlineTestsRepo.CreateQuestion(
                                int.Parse(Request.Form["questionSubject"]),
                                (Request.Form["prompt"] ?? "").Trim(),
                                (Request.Form["optionA"] ?? "").Trim(),
                                (Request.Form["optionB"] ?? "").Trim(),
                                (Request.Form["optionC"] ?? "").Trim(),
                                (Request.Form["optionD"] ?? "").Trim(),
                                (Request.Form["correct"] ?? "A").Trim(),
                                (Request.Form["explanation"] ?? "").Trim(),
                                (Request.Form["difficulty"] ?? "medium").Trim(),
                                CurrentUser.UserId);
                            AuditService.Log("QuestionCreated", "QuestionBank", questionId);
                            Flash("success", "Question #" + questionId + " added to the bank.");
                            _subjectId = int.Parse(Request.Form["questionSubject"]);
                            break;
                        }

                    case "bulkquestions":
                        {
                            string raw = Request.Form["bulk"] ?? "";
                            DataTable subjects = AcademicsRepo.Subjects();
                            int ok = 0, bad = 0;
                            foreach (string line in raw.Replace("\r", "").Split('\n'))
                            {
                                string row = (line ?? "").Trim();
                                if (row.Length == 0 || row.StartsWith("#")) continue;
                                string[] f = row.Split('|');
                                if (f.Length < 8) { bad++; continue; }
                                string code = f[0].Trim();
                                int subjectId = 0;
                                foreach (DataRow s in subjects.Rows)
                                    if (string.Equals(Convert.ToString(s["SubjectCode"]), code, StringComparison.OrdinalIgnoreCase))
                                    { subjectId = Convert.ToInt32(s["SubjectId"]); break; }
                                if (subjectId == 0) { bad++; continue; }
                                try
                                {
                                    OnlineTestsRepo.CreateQuestion(subjectId, f[1].Trim(), f[2].Trim(), f[3].Trim(),
                                        f[4].Trim(), f[5].Trim(), f[6].Trim(), "", f[7].Trim(), CurrentUser.UserId);
                                    ok++;
                                }
                                catch (Exception) { bad++; }
                            }
                            AuditService.Log("QuestionsBulkImported", "QuestionBank", null, ok + " imported, " + bad + " skipped");
                            Flash(bad == 0 ? "success" : "warning", ok + " question(s) imported, " + bad + " line(s) skipped.");
                            break;
                        }

                    case "togglequestion":
                        OnlineTestsRepo.ToggleQuestion(int.Parse(Request.Form["questionId"]));
                        Flash("info", "Question visibility toggled.");
                        break;

                    case "addquestion":
                        {
                            int testId = int.Parse(Request.Form["testId"]);
                            OnlineTestsRepo.AddQuestion(testId, int.Parse(Request.Form["questionId"]),
                                ParseDecimal(Request.Form["points"], 1));
                            Flash("success", "Question attached to the test.");
                            redirectTest = testId;
                            break;
                        }

                    case "removequestion":
                        {
                            int testId = int.Parse(Request.Form["testId"]);
                            OnlineTestsRepo.RemoveQuestion(testId, int.Parse(Request.Form["questionId"]));
                            Flash("warning", "Question removed from the test.");
                            redirectTest = testId;
                            break;
                        }

                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }

            string url = "/Admin/OnlineTests.aspx";
            if (redirectTest > 0) url += "?test=" + redirectTest;
            else if (_subjectId > 0) url += "?subjectId=" + _subjectId;
            if (error != null) url += (url.Contains("?") ? "&" : "?") + "err=" + HttpUtility.UrlEncode(error);
            Redirect(url);
        }

        private void BuildOptions()
        {
            DataTable courses = AcademicsRepo.Courses();
            DataTable subjects = AcademicsRepo.Subjects();
            CourseOptions = Options("CourseId", "CourseName", courses, "0");
            SubjectOptions = Options("SubjectId", "SubjectName", subjects, "0");
            QuestionSubjectOptions = Options("SubjectId", "SubjectName", subjects, _subjectId.ToString());

            var f = new StringBuilder("<option value='0'>All subjects</option>");
            foreach (DataRow r in subjects.Rows)
                f.Append("<option value='").Append(r["SubjectId"]).Append("'")
                 .Append(Convert.ToInt32(r["SubjectId"]) == _subjectId ? " selected" : "").Append(">")
                 .Append(E(r["SubjectCode"])).Append(" - ").Append(E(r["SubjectName"])).Append("</option>");
            QuestionFilterOptions = f.ToString();

            var cats = new StringBuilder("<option value='0'>All categories</option>");
            foreach (DataRow r in OnlineTestsRepo.Categories().Rows)
                cats.Append("<option value='").Append(r["CategoryId"]).Append("'")
                    .Append(Convert.ToInt32(r["CategoryId"]) == _categoryId ? " selected" : "").Append(">")
                    .Append(E(r["CategoryName"])).Append("</option>");
            CategoryFilterOptions = cats.ToString();
        }

        private void BuildSummary()
        {
            DataTable tests = OnlineTestsRepo.AdminTests();
            int published = 0, questions = 0, attempts = 0;
            foreach (DataRow r in tests.Rows)
            {
                if (Convert.ToBoolean(r["IsPublished"])) published++;
                questions += Convert.ToInt32(r["QuestionCount"]);
                attempts += Convert.ToInt32(r["AttemptCount"]);
            }
            int bank = SqlHelper.ExecuteScalarInt("SELECT COUNT(*) FROM dbo.QuestionBank");
            var sb = new StringBuilder("<div class='bento'>");
            Kpi(sb, "Tests", tests.Rows.Count.ToString("N0"));
            Kpi(sb, "Published", published.ToString("N0"));
            Kpi(sb, "Question bank", bank.ToString("N0"));
            Kpi(sb, "Attempts", attempts.ToString("N0"));
            sb.Append("</div>");
            SummaryCards = sb.ToString();
        }

        private static void Kpi(StringBuilder sb, string label, string value)
        {
            sb.Append("<div class='c-3'><div class='kpi'><div class='kpi__head'>").Append(label)
              .Append("</div><div class='kpi__body'><div class='kpi__value'>").Append(value).Append("</div></div></div></div>");
        }

        private void BuildTestTable()
        {
            DataTable dt = OnlineTestsRepo.AdminTests();
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Test</th><th>Course / Subject</th><th>Window</th><th>Questions</th><th>Attempts</th><th>Status</th><th>Actions</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='7' class='msg-empty'>No online test yet. Create one above.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                int id = Convert.ToInt32(r["TestId"]);
                bool published = Convert.ToBoolean(r["IsPublished"]);
                sb.Append("<tr").Append(id == SelectedTestId ? " class='is-selected'" : "").Append(">");
                sb.Append("<td><a href='/Admin/OnlineTests.aspx?test=").Append(id).Append("'><b>").Append(E(r["Title"]))
                  .Append("</b></a><div class='row-sub mono'>#").Append(id).Append(" &middot; ")
                  .Append(Convert.ToInt32(r["DurationMinutes"])).Append(" min &middot; ").Append(E(r["MaxAttempts"])).Append(" attempt(s)</div></td>");
                sb.Append("<td>").Append(E(r["CourseCode"])).Append(" / ").Append(E(r["SubjectCode"])).Append("</td>");
                sb.Append("<td class='small'>").Append(Window(r)).Append("</td>");
                sb.Append("<td class='mono'>").Append(E(r["QuestionCount"])).Append(" / ").Append(E(r["MaxScore"])).Append(" pts</td>");
                sb.Append("<td class='mono'>").Append(E(r["AttemptCount"])).Append("</td>");
                sb.Append("<td><span class='badge ").Append(published ? "badge-ok" : "badge-warn").Append("'>")
                  .Append(published ? "Published" : "Draft").Append("</span> <span class='small text-muted'>")
                  .Append(Availability(r)).Append("</span></td>");
                sb.Append("<td>");
                sb.Append("<a class='imbtn' href='/Admin/OnlineTests.aspx?test=").Append(id).Append("'>Open</a> ");
                sb.Append(ImbForm(published ? "unpublish" : "publish", new[] { new[] { "testId", id.ToString() } }, published ? "Unpublish" : "Publish"));
                sb.Append(ImbForm("deletetest", new[] { new[] { "testId", id.ToString() } }, "Delete", true));
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            TestTable = sb.ToString();
        }

        private void BuildQuestionTable()
        {
            DataTable dt = OnlineTestsRepo.Questions(_subjectId > 0 ? (int?)_subjectId : null,
                                                     _categoryId > 0 ? (int?)_categoryId : null);
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Question</th><th>Subject</th><th>Category</th><th>Difficulty</th><th>Correct</th><th>Used by</th><th>Status</th><th></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='8' class='msg-empty'>No question matches this filter.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                bool active = Convert.ToBoolean(r["IsActive"]);
                sb.Append("<tr><td><b>").Append(E(Snippet(Convert.ToString(r["Prompt"]), 100))).Append("</b>");
                if (r["Explanation"] != DBNull.Value && r["Explanation"] != null)
                    sb.Append("<div class='small text-muted'>").Append(E(Snippet(Convert.ToString(r["Explanation"]), 100))).Append("</div>");
                sb.Append("</td><td class='mono'>").Append(E(r["SubjectCode"])).Append("</td>");
                sb.Append("<td class='small'>").Append(E(r["CategoryName"])).Append("</td>");
                sb.Append("<td>").Append(E(r["Difficulty"])).Append("</td>");
                sb.Append("<td class='mono'>").Append(E(r["CorrectOption"])).Append("</td>");
                sb.Append("<td class='mono'>").Append(E(r["TestCount"])).Append("</td>");
                sb.Append("<td><span class='badge ").Append(active ? "badge-ok" : "badge-off").Append("'>").Append(active ? "active" : "off").Append("</span></td>");
                sb.Append("<td>").Append(ImbForm("togglequestion", new[] { new[] { "questionId", E(r["QuestionId"]) } }, active ? "Disable" : "Enable")).Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            QuestionTable = sb.ToString();
        }

        private void BuildDetail()
        {
            DataTable all = OnlineTestsRepo.AdminTests();
            DataRow test = null;
            foreach (DataRow r in all.Rows) if (Convert.ToInt32(r["TestId"]) == SelectedTestId) { test = r; break; }
            if (test == null) { SelectedTestId = 0; TestDetailHtml = ""; return; }

            bool published = Convert.ToBoolean(test["IsPublished"]);
            string zone = Fmt.InstitutionZoneId;
            DataTable attached = OnlineTestsRepo.TestQuestions(SelectedTestId);
            var attachedIds = new HashSet<int>();
            var sb = new StringBuilder();
            sb.Append("<div class='c-7'><div class='tile'><div class='tile__head d-flex justify-content-between align-items-center'><span>Questions &middot; ")
              .Append(E(test["Title"])).Append("</span><span class='d-flex gap-2'>")
              .Append(ImbForm(published ? "unpublish" : "publish", new[] { new[] { "testId", SelectedTestId.ToString() } }, published ? "Unpublish" : "Publish"))
              .Append("</span></div><div class='tile__body'>");
            sb.Append("<p class='small text-muted'>").Append(E(test["CourseCode"])).Append(" / ").Append(E(test["SubjectCode"]))
              .Append(" &middot; ").Append(Window(test)).Append(" &middot; ").Append(E(test["DurationMinutes"]))
              .Append(" min &middot; pass ").Append(E(test["PassPercent"])).Append("% &middot; ")
              .Append(Convert.ToBoolean(test["ShuffleQuestions"]) ? "shuffled" : "fixed order").Append("</p>");
            if (test["Instructions"] != DBNull.Value && test["Instructions"] != null)
                sb.Append("<p class='small'>").Append(E(test["Instructions"])).Append("</p>");
            sb.Append("<div class='sms-table-wrap table-wrap'><table class='table table-sm'><thead><tr><th>#</th><th>Question</th><th>Points</th><th>Correct</th><th>Difficulty</th><th></th></tr></thead><tbody>");
            int n = 0;
            foreach (DataRow r in attached.Rows)
            {
                n++;
                int qid = Convert.ToInt32(r["QuestionId"]);
                attachedIds.Add(qid);
                sb.Append("<tr><td class='mono'>").Append(n).Append("</td><td>").Append(E(Snippet(Convert.ToString(r["Prompt"]), 120))).Append("</td>");
                sb.Append("<td class='mono'>").Append(E(r["Points"])).Append("</td><td class='mono'>").Append(E(r["CorrectOption"])).Append("</td>");
                sb.Append("<td>").Append(E(r["Difficulty"])).Append("</td><td>")
                  .Append(ImbForm("removequestion", new[] { new[] { "testId", SelectedTestId.ToString() }, new[] { "questionId", qid.ToString() } }, "Remove", true))
                  .Append("</td></tr>");
            }
            if (attached.Rows.Count == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>No question attached yet.</td></tr>");
            sb.Append("</tbody></table></div>");

            DataTable bank = OnlineTestsRepo.Questions(Convert.ToInt32(test["SubjectId"]));
            var opts = new StringBuilder();
            foreach (DataRow q in bank.Rows)
            {
                int qid = Convert.ToInt32(q["QuestionId"]);
                if (!Convert.ToBoolean(q["IsActive"]) || attachedIds.Contains(qid)) continue;
                opts.Append("<option value='").Append(qid).Append("'>#").Append(qid).Append(" &middot; ")
                    .Append(E(Snippet(Convert.ToString(q["Prompt"]), 90))).Append("</option>");
            }
            sb.Append("<form method='post' action='/Admin/OnlineTests.aspx' class='d-flex flex-wrap gap-2 align-items-end mt-3'>")
              .Append(CsrfField()).Append("<input type='hidden' name='op' value='addquestion'/>")
              .Append("<input type='hidden' name='testId' value='").Append(SelectedTestId).Append("'/>")
              .Append("<div class='flex-grow-1'><label class='form-label small'>Add a bank question</label><select name='questionId' class='form-select form-select-sm' required>");
            if (opts.Length == 0) sb.Append("<option value=''>No unattached active question in this subject</option>");
            else sb.Append(opts);
            sb.Append("</select></div><div><label class='form-label small'>Points</label><input name='points' type='number' min='0.01' step='0.01' value='1' class='form-control form-control-sm' style='width:100px'/></div><button class='btn btn-sms btn-sm'>Attach</button></form>");

            string opensVal = test["OpensAtUtc"] == DBNull.Value ? "" : Fmt.ToLocal(Convert.ToDateTime(test["OpensAtUtc"]), zone).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
            string closesVal = test["ClosesAtUtc"] == DBNull.Value ? "" : Fmt.ToLocal(Convert.ToDateTime(test["ClosesAtUtc"]), zone).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
            sb.Append("<details class='mt-3'><summary class='small'><b>Edit test settings</b></summary>")
              .Append("<form method='post' action='/Admin/OnlineTests.aspx' class='form-grid mt-2'>").Append(CsrfField())
              .Append("<input type='hidden' name='op' value='edittest'/><input type='hidden' name='testId' value='").Append(SelectedTestId).Append("'/>")
              .Append("<div class='c-8'><label class='form-label small'>Title</label><input name='title' required maxlength='160' class='form-control form-control-sm' value='").Append(E(test["Title"])).Append("'/></div>")
              .Append("<div class='c-2'><label class='form-label small'>Duration (min)</label><input name='duration' type='number' min='1' max='480' class='form-control form-control-sm' value='").Append(E(test["DurationMinutes"])).Append("'/></div>")
              .Append("<div class='c-2'><label class='form-label small'>Attempts</label><input name='attempts' type='number' min='1' max='10' class='form-control form-control-sm' value='").Append(E(test["MaxAttempts"])).Append("'/></div>")
              .Append("<div class='c-2'><label class='form-label small'>Pass %</label><input name='passPct' type='number' min='0' max='100' step='0.01' class='form-control form-control-sm' value='").Append(E(test["PassPercent"])).Append("'/></div>")
              .Append("<div class='c-2'><label class='form-label small'>Warnings before auto-submit</label><input name='warningLimit' type='number' min='0' max='10' class='form-control form-control-sm' value='").Append(E(test["WarningLimit"])).Append("'/></div>")
              .Append("<div class='c-3'><label class='form-label small'>Opens (local)</label><input name='opensAt' type='datetime-local' class='form-control form-control-sm' value='").Append(opensVal).Append("'/></div>")
              .Append("<div class='c-3'><label class='form-label small'>Closes (local)</label><input name='closesAt' type='datetime-local' class='form-control form-control-sm' value='").Append(closesVal).Append("'/></div>")
              .Append("<div class='c-12'><label class='form-label small'>Instructions</label><textarea name='instructions' rows='2' maxlength='2000' class='form-control form-control-sm'>").Append(E(test["Instructions"])).Append("</textarea></div>")
              .Append("<div class='c-4 d-flex align-items-end gap-3'>")
              .Append("<label class='form-check small'><input class='form-check-input' type='checkbox' name='shuffle' value='1'")
              .Append(Convert.ToBoolean(test["ShuffleQuestions"]) ? " checked" : "").Append("/> Shuffle questions</label>")
              .Append("<label class='form-check small'><input class='form-check-input' type='checkbox' name='proctor' value='1'")
              .Append(Convert.ToBoolean(test["ProctoringEnabled"]) ? " checked" : "").Append("/> Proctoring</label>")
              .Append("</div>")
              .Append("<div class='c-8 d-flex align-items-end justify-content-end'><button class='btn btn-sms btn-sm'>Save test settings</button></div>")
              .Append("</form></details>");
            sb.Append("</div></div></div>");

            DataTable attempts = OnlineTestsRepo.TestAttempts(SelectedTestId);
            sb.Append("<div class='c-5'><div class='tile'><div class='tile__head d-flex justify-content-between'><span>Attempts</span><a class='btn btn-outline-sms btn-sm' href='/Handlers/Export.ashx?scope=onlinetests&format=csv'>Export results</a></div><div class='tile__body sms-table-wrap table-wrap'>");
            sb.Append("<table class='table table-sm'><thead><tr><th>Student</th><th>Status</th><th>Score</th><th>Flags</th></tr></thead><tbody>");
            if (attempts.Rows.Count == 0) sb.Append("<tr><td colspan='4' class='msg-empty'>No attempt yet.</td></tr>");
            foreach (DataRow a in attempts.Rows)
            {
                sb.Append("<tr><td><b>").Append(E(a["FullName"])).Append("</b><div class='small text-muted mono'>").Append(E(a["EnrollNumber"])).Append("</div></td>");
                sb.Append("<td><span class='badge ").Append(StatusBadge(Convert.ToString(a["Status"]))).Append("'>").Append(E(a["Status"])).Append("</span>");
                sb.Append("<div class='small text-muted'>").Append(Fmt.Stamp(Convert.ToDateTime(a["StartedAtUtc"]), zone, "yyyy-MM-dd HH:mm")).Append("</div></td>");
                sb.Append("<td class='mono'>").Append(a["Score"] == DBNull.Value ? "-" : E(a["Score"])).Append(" / ").Append(E(a["MaxScore"])).Append("</td>");
                sb.Append("<td class='mono'>").Append(E(a["Violations"])).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div></div></div>");

            DataTable events = OnlineTestsRepo.TestEvents(SelectedTestId);
            sb.Append("<div class='c-12'><div class='tile'><div class='tile__head d-flex justify-content-between'><span>Proctoring events</span><span class='small text-muted'>Newest first, last 50.</span></div><div class='tile__body sms-table-wrap table-wrap'>");
            sb.Append("<table class='table table-sm'><thead><tr><th>When</th><th>Student</th><th>Attempt</th><th>Event</th><th>Detail</th></tr></thead><tbody>");
            if (events.Rows.Count == 0) sb.Append("<tr><td colspan='5' class='msg-empty'>No proctoring event recorded.</td></tr>");
            foreach (DataRow ev in events.Rows)
            {
                sb.Append("<tr><td class='small mono'>").Append(Fmt.Stamp(Convert.ToDateTime(ev["OccurredAtUtc"]), zone, "yyyy-MM-dd HH:mm:ss")).Append("</td>");
                sb.Append("<td>").Append(E(ev["FullName"])).Append("<div class='small text-muted mono'>").Append(E(ev["EnrollNumber"])).Append("</div></td>");
                sb.Append("<td class='mono'>#").Append(E(ev["AttemptId"])).Append("</td>");
                sb.Append("<td><span class='badge badge-warn'>").Append(E(ev["EventType"])).Append("</span></td>");
                sb.Append("<td class='small'>").Append(E(ev["Detail"])).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div></div></div>");
            TestDetailHtml = sb.ToString();
        }

        private static string StatusBadge(string status)
        {
            if (status == "submitted") return "badge-ok";
            if (status == "expired" || status == "auto_submitted") return "badge-bad";
            return "badge-info";
        }

        private static string Window(DataRow r)
        {
            string zone = Fmt.InstitutionZoneId;
            string opens = r["OpensAtUtc"] == DBNull.Value ? "any time" : Fmt.Stamp(Convert.ToDateTime(r["OpensAtUtc"]), zone, "yyyy-MM-dd HH:mm");
            string closes = r["ClosesAtUtc"] == DBNull.Value ? "no close" : Fmt.Stamp(Convert.ToDateTime(r["ClosesAtUtc"]), zone, "yyyy-MM-dd HH:mm");
            return opens + " to " + closes;
        }

        private static string Availability(DataRow r)
        {
            DateTime now = DateTime.UtcNow;
            DateTime? opens = r["OpensAtUtc"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["OpensAtUtc"]);
            DateTime? closes = r["ClosesAtUtc"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ClosesAtUtc"]);
            if (opens.HasValue && now < opens.Value) return "upcoming";
            if (closes.HasValue && now >= closes.Value) return "closed";
            return "open now";
        }

        private static DateTime? ParseLocal(string value, out bool valid)
        {
            valid = true;
            if (string.IsNullOrWhiteSpace(value)) return null;
            DateTime local;
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out local))
            {
                valid = false;
                return null;
            }
            return Fmt.ToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Fmt.InstitutionZoneId);
        }

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            return int.TryParse((value ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static decimal ParseDecimal(string value, decimal fallback)
        {
            decimal parsed;
            return decimal.TryParse((value ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        protected static string Snippet(string text, int length)
        {
            text = text ?? "";
            return text.Length <= length ? text : text.Substring(0, length - 3) + "...";
        }

        private string ImbForm(string op, string[][] fields, string label, bool confirm = false)
        {
            var sb = new StringBuilder();
            sb.Append("<form method='post' action='/Admin/OnlineTests.aspx' style='display:inline'")
              .Append(confirm ? " data-confirm='Remove this record?'" : "").Append(">");
            sb.Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='").Append(Attr(op)).Append("'/>");
            foreach (string[] f in fields)
                sb.Append("<input type='hidden' name='").Append(f[0]).Append("' value='").Append(f[1]).Append("'/>");
            sb.Append("<button class='imbtn'>").Append(label).Append("</button></form> ");
            return sb.ToString();
        }
    }
}
