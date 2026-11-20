using System;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Student
{
    /// <summary>Student test list, timed runner and scored result.</summary>
    public partial class OnlineTests : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Student" }; } }

        protected string FlashHtml, PageBody;
        private int _studentId;

        protected void Page_Load(object sender, EventArgs e)
        {
            FlashHtml = PopFlash();
            DataRow student = OnlineTestsRepo.Student(CurrentUser.UserId);
            if (student == null)
            {
                PageBody = "<div class='tile'><div class='tile__body'><p class='msg-empty'>Your student profile is not linked to this login. Contact the registrar.</p></div></div>";
                return;
            }
            _studentId = Convert.ToInt32(student["StudentId"]);

            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }

            int attemptId;
            if (int.TryParse(Request.QueryString["attempt"], out attemptId) && attemptId > 0)
            {
                LoadAttempt(attemptId);
                return;
            }
            BuildList();
        }

        private void HandlePost()
        {
            string error = null;
            int redirectAttempt = 0;
            try
            {
                switch (Request.Form["op"])
                {
                    case "start":
                        {
                            int testId = int.Parse(Request.Form["testId"]);
                            redirectAttempt = OnlineTestsRepo.StartAttempt(testId, _studentId);
                            Flash("info", "Timer started. Answers autosave as you work.");
                            break;
                        }
                    case "submit":
                        {
                            redirectAttempt = int.Parse(Request.Form["attemptId"]);
                            DataRow attempt = OnlineTestsRepo.Attempt(redirectAttempt, _studentId);
                            if (attempt == null) throw new Exception("Attempt not found.");
                            OnlineTestsRepo.SubmitAttempt(redirectAttempt, _studentId);
                            Flash("success", "Test submitted and scored.");
                            break;
                        }
                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }

            string url = redirectAttempt > 0 ? "/Student/OnlineTests.aspx?attempt=" + redirectAttempt : "/Student/OnlineTests.aspx";
            if (error != null) url += (url.Contains("?") ? "&" : "?") + "err=" + HttpUtility.UrlEncode(error);
            Redirect(url);
        }

        private void BuildList()
        {
            DataTable dt = OnlineTestsRepo.TestsForStudent(_studentId);
            string zone = Fmt.InstitutionZoneId;
            DateTime now = DateTime.UtcNow;
            var sb = new StringBuilder();
            sb.Append("<div class='page-head'><div><h1>Online tests</h1><p>Published tests for your course. Start when you are ready; the timer cannot be paused.</p></div></div>");
            sb.Append("<div class='bento'><div class='c-12'><div class='tile'><div class='tile__head'>Available tests</div><div class='tile__body sms-table-wrap table-wrap'>");
            sb.Append("<table class='table table-sm'><thead><tr><th>Test</th><th>Summary</th><th>Window</th><th>Attempts</th><th>Best</th><th></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>No online test has been published for your course yet.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                int testId = Convert.ToInt32(r["TestId"]);
                int attempts = Convert.ToInt32(r["Attempts"]);
                int maxAttempts = Convert.ToInt32(r["MaxAttempts"]);
                int activeId = r["ActiveAttemptId"] == DBNull.Value ? 0 : Convert.ToInt32(r["ActiveAttemptId"]);
                int lastId = r["LastAttemptId"] == DBNull.Value ? 0 : Convert.ToInt32(r["LastAttemptId"]);
                DateTime? opens = r["OpensAtUtc"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["OpensAtUtc"]);
                DateTime? closes = r["ClosesAtUtc"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ClosesAtUtc"]);
                bool open = (!opens.HasValue || now >= opens.Value) && (!closes.HasValue || now < closes.Value);

                sb.Append("<tr><td><b>").Append(E(r["Title"])).Append("</b><div class='row-sub mono'>")
                  .Append(E(r["CourseCode"])).Append(" / ").Append(E(r["SubjectCode"])).Append(" &middot; ")
                  .Append(E(r["QuestionCount"])).Append(" questions &middot; ").Append(E(r["DurationMinutes"])).Append(" min</div></td>");
                sb.Append("<td class='small'>").Append(E(r["SubjectName"])).Append("<div class='text-muted'>")
                  .Append(E(r["MaxScore"])).Append(" points &middot; pass ").Append(E(r["PassPercent"])).Append("%</div></td>");
                sb.Append("<td class='small mono'>").Append(opens.HasValue ? Fmt.Stamp(opens.Value, zone, "yyyy-MM-dd HH:mm") : "any time")
                  .Append("<div class='text-muted'>to ").Append(closes.HasValue ? Fmt.Stamp(closes.Value, zone, "yyyy-MM-dd HH:mm") : "no close").Append("</div></td>");
                sb.Append("<td class='mono'>").Append(attempts).Append(" / ").Append(maxAttempts).Append("</td>");
                sb.Append("<td class='mono'>").Append(r["BestScore"] == DBNull.Value ? "-" : E(r["BestScore"])).Append("</td>");
                sb.Append("<td>");
                if (activeId > 0)
                    sb.Append("<a class='btn btn-sms btn-sm' href='/Student/OnlineTests.aspx?attempt=").Append(activeId).Append("'>Resume</a>");
                else if (attempts >= maxAttempts)
                    sb.Append(lastId > 0 ? "<a class='btn btn-outline-sms btn-sm' href='/Student/OnlineTests.aspx?attempt=" + lastId + "'>View result</a>" : "<span class='badge badge-off'>Used</span>");
                else if (open)
                    sb.Append("<form method='post' action='/Student/OnlineTests.aspx' style='display:inline'>").Append(CsrfField())
                      .Append("<input type='hidden' name='op' value='start'/><input type='hidden' name='testId' value='").Append(testId)
                      .Append("'/><button class='btn btn-sms btn-sm'>Start test</button></form>");
                else
                    sb.Append("<span class='badge ").Append(opens.HasValue && now < opens.Value ? "badge-warn" : "badge-off").Append("'>")
                      .Append(opens.HasValue && now < opens.Value ? "Opens later" : "Closed").Append("</span>");
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table></div></div></div></div>");
            PageBody = sb.ToString();
        }

        private void LoadAttempt(int attemptId)
        {
            DataRow a = OnlineTestsRepo.Attempt(attemptId, _studentId);
            if (a == null)
            {
                PageBody = "<div class='page-head'><div><h1>Online tests</h1></div></div><div class='tile'><div class='tile__body'><p class='msg-empty'>That attempt was not found for your account.</p><a class='btn btn-outline-sms btn-sm' href='/Student/OnlineTests.aspx'>Back to tests</a></div></div>";
                return;
            }

            string status = Convert.ToString(a["Status"]);
            DateTime expires = Convert.ToDateTime(a["ExpiresAtUtc"]);
            if (status == "in_progress" && expires <= DateTime.UtcNow)
            {
                OnlineTestsRepo.SubmitAttempt(attemptId, _studentId);
                Flash("warning", "Time ran out. Your saved answers were submitted and scored.");
                Redirect("/Student/OnlineTests.aspx?attempt=" + attemptId);
                return;
            }

            if (status == "in_progress") BuildRunner(a);
            else BuildResult(a);
        }

        private void BuildRunner(DataRow a)
        {
            int attemptId = Convert.ToInt32(a["AttemptId"]);
            DataTable questions = OnlineTestsRepo.RunnerQuestions(attemptId);
            string zone = Fmt.InstitutionZoneId;
            var sb = new StringBuilder();
            sb.Append("<div class='page-head'><div><h1>").Append(E(a["Title"])).Append("</h1><p>")
              .Append(E(a["CourseCode"])).Append(" / ").Append(E(a["SubjectCode"])).Append(" &middot; ")
              .Append(Convert.ToInt32(a["DurationMinutes"])).Append(" minute limit &middot; started ")
              .Append(Fmt.Stamp(Convert.ToDateTime(a["StartedAtUtc"]), zone, "HH:mm")).Append("</p></div>");
            sb.Append("<div class='exam-timer'><span class='small text-muted'>Time left</span><b id='examTimer' data-expires='")
              .Append(Convert.ToDateTime(a["ExpiresAtUtc"]).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture))
              .Append("'>--:--</b></div></div>");

            sb.Append("<div class='tile mb-3'><div class='tile__body d-flex flex-wrap gap-4 align-items-center'>");
            sb.Append("<span class='small'><b id='examAnswered'>0</b> of ").Append(questions.Rows.Count).Append(" answered</span>");
            sb.Append("<span class='small text-muted' id='examSaveState'>Autosave ready</span>");
            if (Convert.ToBoolean(a["ProctoringEnabled"]))
                sb.Append("<span class='badge badge-warn'><i class='bi bi-eye'></i> Proctoring on <b id='examFlags'>")
                  .Append(E(a["Violations"])).Append(" flags</b></span>");
            if (!string.IsNullOrEmpty(Convert.ToString(a["Instructions"])))
                sb.Append("<span class='small'>").Append(E(a["Instructions"])).Append("</span>");
            sb.Append("</div></div>");

            sb.Append("<form id='testForm' method='post' action='/Student/OnlineTests.aspx' data-confirm='Submit this test now? You cannot change answers after submitting.' ")
              .Append("data-attempt='").Append(attemptId).Append("' data-proctoring='").Append(Convert.ToBoolean(a["ProctoringEnabled"]) ? "1" : "0")
              .Append("' data-save-url='/Handlers/OnlineTestAction.ashx'>").Append(CsrfField())
              .Append("<input type='hidden' name='op' value='submit'/><input type='hidden' name='attemptId' value='").Append(attemptId).Append("'/>");
            sb.Append("<div class='exam-paper'>");
            int n = 0;
            foreach (DataRow q in questions.Rows)
            {
                n++;
                int qid = Convert.ToInt32(q["QuestionId"]);
                string selected = Convert.ToString(q["SelectedOption"]);
                sb.Append("<section class='tile exam-question' data-qid='").Append(qid).Append("'><div class='tile__body'>");
                sb.Append("<div class='d-flex justify-content-between gap-3'><div class='mono small text-muted'>Question ").Append(n)
                  .Append("</div><div class='mono small'>").Append(E(q["Points"])).Append(" pt</div></div>");
                sb.Append("<h2 class='exam-question__prompt'>").Append(E(q["Prompt"])).Append("</h2>");
                foreach (string letter in new[] { "A", "B", "C", "D" })
                {
                    string text = Convert.ToString(q["Option" + letter]);
                    if (string.IsNullOrEmpty(text)) continue;
                    sb.Append("<label class='exam-option'><input type='radio' name='q_").Append(qid).Append("' value='").Append(letter)
                      .Append("' data-qid='").Append(qid).Append("' data-letter='").Append(letter).Append("'")
                      .Append(selected == letter ? " checked" : "").Append("/><span class='exam-option__key'>").Append(letter)
                      .Append("</span><span>").Append(E(text)).Append("</span></label>");
                }
                sb.Append("</div></section>");
            }
            sb.Append("</div><div class='d-flex justify-content-between align-items-center mt-3'>");
            sb.Append("<span class='small text-muted'>Answers save automatically. Keep this tab open.</span>");
            sb.Append("<button class='btn btn-sms' type='submit'><i class='bi bi-check2-circle'></i> Submit test</button></div></form>");
            PageBody = sb.ToString();
        }

        private void BuildResult(DataRow a)
        {
            int attemptId = Convert.ToInt32(a["AttemptId"]);
            DataTable questions = OnlineTestsRepo.ResultQuestions(attemptId);
            string zone = Fmt.InstitutionZoneId;
            decimal score = a["Score"] == DBNull.Value ? 0 : Convert.ToDecimal(a["Score"]);
            decimal max = a["MaxScore"] == DBNull.Value ? 0 : Convert.ToDecimal(a["MaxScore"]);
            decimal passPct = Convert.ToDecimal(a["PassPercent"]);
            decimal percent = max <= 0 ? 0 : Math.Round(score / max * 100m, 2);
            bool pass = percent >= passPct;
            string status = Convert.ToString(a["Status"]);

            var sb = new StringBuilder();
            sb.Append("<div class='page-head'><div><h1>").Append(E(a["Title"])).Append("</h1><p>")
              .Append(E(a["CourseCode"])).Append(" / ").Append(E(a["SubjectCode"])).Append(" &middot; submitted ")
              .Append(a["SubmittedAtUtc"] == DBNull.Value ? "-" : Fmt.Stamp(Convert.ToDateTime(a["SubmittedAtUtc"]), zone, "yyyy-MM-dd HH:mm"))
              .Append("</p></div><a class='btn btn-outline-sms btn-sm' href='/Student/OnlineTests.aspx'>Back to tests</a></div>");

            sb.Append("<div class='bento'>");
            sb.Append("<div class='c-4'><div class='kpi'><div class='kpi__head'>Score</div><div class='kpi__body'><div class='kpi__value'>")
              .Append(score.ToString("0.##", CultureInfo.InvariantCulture)).Append(" <span class='small text-muted'>/ ")
              .Append(max.ToString("0.##", CultureInfo.InvariantCulture)).Append("</span></div></div></div></div>");
            sb.Append("<div class='c-4'><div class='kpi'><div class='kpi__head'>Percentage</div><div class='kpi__body'><div class='kpi__value'>")
              .Append(percent.ToString("0.##", CultureInfo.InvariantCulture)).Append("%</div></div></div></div>");
            sb.Append("<div class='c-4'><div class='kpi'><div class='kpi__head'>Result</div><div class='kpi__body'><div class='kpi__value'>")
              .Append("<span class='badge ").Append(pass && status == "submitted" ? "badge-ok" : "badge-bad").Append("'>")
              .Append(status == "expired" ? "Expired" : status == "auto_submitted" ? "Auto-submitted" : (pass ? "Pass" : "Fail")).Append("</span>")
              .Append("</div><div class='small text-muted'>Pass mark ").Append(passPct.ToString("0.##", CultureInfo.InvariantCulture)).Append("%</div></div></div></div>");
            sb.Append("</div>");
            string integrityNote = Convert.ToString(a["IntegrityNote"]);
            if (!string.IsNullOrEmpty(integrityNote))
                sb.Append("<div class='tz-notice'><strong>Integrity note</strong><span>").Append(E(integrityNote)).Append("</span></div>");

            sb.Append("<div class='tile mt-3'><div class='tile__head d-flex justify-content-between'><span>Answer review</span><span class='small text-muted'>Proctoring flags: ")
              .Append(E(a["Violations"])).Append("</span></div><div class='tile__body sms-table-wrap table-wrap'>");
            sb.Append("<table class='table table-sm'><thead><tr><th>Question</th><th>Your answer</th><th>Correct</th><th>Points</th></tr></thead><tbody>");
            int n = 0;
            foreach (DataRow q in questions.Rows)
            {
                n++;
                string chosen = Convert.ToString(q["SelectedOption"]);
                string correct = Convert.ToString(q["CorrectOption"]);
                bool isCorrect = q["IsCorrect"] != DBNull.Value && Convert.ToBoolean(q["IsCorrect"]);
                sb.Append("<tr class='").Append(isCorrect ? "is-correct" : "is-wrong").Append("'><td><b>").Append(n).Append(". ")
                  .Append(E(Snippet(Convert.ToString(q["Prompt"]), 160))).Append("</b>");
                if (q["Explanation"] != DBNull.Value && q["Explanation"] != null)
                    sb.Append("<div class='small text-muted'>").Append(E(Convert.ToString(q["Explanation"]))).Append("</div>");
                sb.Append("</td><td>").Append(AnswerText(q, chosen)).Append("</td><td>").Append(AnswerText(q, correct)).Append("</td>");
                sb.Append("<td class='mono'>").Append(q["PointsAwarded"] == DBNull.Value ? "0" : E(q["PointsAwarded"]))
                  .Append(" / ").Append(E(q["Points"])).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div></div>");
            PageBody = sb.ToString();
        }

        private static string AnswerText(DataRow q, string letter)
        {
            if (string.IsNullOrEmpty(letter)) return "<span class='text-muted'>Not answered</span>";
            string text = Convert.ToString(q["Option" + letter]);
            return "<span class='mono'>" + letter + "</span> " + E(text);
        }

        protected static string Snippet(string text, int length)
        {
            text = text ?? "";
            return text.Length <= length ? text : text.Substring(0, length - 3) + "...";
        }
    }
}
