using System;
using System.Text;
using System.Web;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Data;
using System.Data;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace StudentManagementSystem.Help
{
    /// <summary>Task-first help, tailored to the signed-in role.</summary>
    public partial class HelpCenter : SmsPage
    {
        protected string RoleGuideHtml, SearchHtml, ArticleHtml, Q, Lang, FlashHtml, FaqHtml;

        protected void Page_Load(object sender, EventArgs e)
        {
            FlashHtml = PopFlash();
            Q = (Request.QueryString["q"] ?? "").Trim();
            string requested = (Request.QueryString["lang"] ?? "").Trim().ToLowerInvariant();
            if (requested == "hi" || requested == "hinglish" || requested == "en")
            {
                Lang = requested;
                var c = new HttpCookie("sms_help_lang", Lang) { Expires = DateTime.UtcNow.AddYears(1), HttpOnly = true };
                Response.Cookies.Set(c);
            }
            else
            {
                Lang = (Request.Cookies["sms_help_lang"] != null ? Request.Cookies["sms_help_lang"].Value : "en");
                if (Lang != "hi" && Lang != "hinglish") Lang = "en";
            }
            BuildSearch();
            BuildArticle();
            BuildFaqs();
            var sb = new StringBuilder("<div class='bento mt-3'>");
            if (CurrentUser.RoleName == "Admin") AdminGuide(sb);
            else if (CurrentUser.RoleName == "Faculty") FacultyGuide(sb);
            else StudentGuide(sb);
            sb.Append("</div>");
            RoleGuideHtml = sb.ToString();
        }

        protected string LangSel(string code) { return Lang == code ? " selected" : ""; }

        private void BuildSearch()
        {
            if (Q.Length == 0) { SearchHtml = ""; return; }
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP 40 a.ArticleId, a.Slug, a.TitleEn, a.SummaryEn, c.TitleEn AS CategoryName FROM dbo.HelpArticles a " +
                "LEFT JOIN dbo.HelpCategories c ON c.CategoryId=a.CategoryId " +
                "LEFT JOIN dbo.HelpTranslations t ON t.ArticleId=a.ArticleId AND t.LanguageCode=@Lang " +
                "WHERE a.IsPublished=1 AND (a.RoleName=@Role OR a.RoleName='Common') AND (a.SearchText LIKE @Q OR a.TitleEn LIKE @Q OR t.Title LIKE @Q OR t.Summary LIKE @Q OR t.Tips LIKE @Q) " +
                "ORDER BY a.SortOrder, a.TitleEn",
                p => { p.AddWithValue("@Role", CurrentUser.RoleName); p.AddWithValue("@Q", "%" + Q + "%"); p.AddWithValue("@Lang", Lang); });
            var sb = new StringBuilder("<div class='tile mb-3'><div class='tile__head'>").Append(dt.Rows.Count).Append(" result(s) for &ldquo;").Append(E(Q)).Append("&rdquo;</div><div class='tile__body'>");
            if (dt.Rows.Count == 0) sb.Append("<p class='msg-empty mb-0'>No article matched. Ask the administrator and we will write one.</p>");
            foreach (DataRow r in dt.Rows)
                sb.Append("<a class='d-block py-2 border-bottom' href='/Help/Index.aspx?slug=").Append(Attr(r["Slug"])).Append("&lang=").Append(Lang).Append("'><b>").Append(E(r["TitleEn"])).Append("</b><div class='small text-muted'>").Append(E(r["CategoryName"])).Append(" - ").Append(E(r["SummaryEn"])).Append("</div></a>");
            sb.Append("</div></div>");
            SearchHtml = sb.ToString();
        }

        private void BuildFaqs()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP 8 * FROM dbo.HelpFaq WHERE IsPublished=1 AND (RoleName=@Role OR RoleName='Common') ORDER BY SortOrder, FaqId",
                p => p.AddWithValue("@Role", CurrentUser.RoleName));
            var sb = new StringBuilder("<div class='tile mb-3'><div class='tile__head'>Frequently asked questions</div><div class='tile__body'>");
            if (dt.Rows.Count == 0) sb.Append("<p class='msg-empty mb-0'>No FAQs yet.</p>");
            foreach (DataRow r in dt.Rows)
            {
                string q = Lang == "hi" ? Convert.ToString(r["QuestionHi"]) : Lang == "hinglish" ? Convert.ToString(r["QuestionHinglish"]) : Convert.ToString(r["QuestionEn"]);
                string a = Lang == "hi" ? Convert.ToString(r["AnswerHi"]) : Lang == "hinglish" ? Convert.ToString(r["AnswerHinglish"]) : Convert.ToString(r["AnswerEn"]);
                sb.Append("<details class='py-2 border-bottom'><summary class='fw-semibold'>").Append(E(q)).Append("</summary><p class='small mt-2 mb-0'>").Append(E(a)).Append("</p></details>");
            }
            sb.Append("</div></div>");
            FaqHtml = sb.ToString();
        }

        private void BuildArticle()
        {
            string slug = (Request.QueryString["slug"] ?? "").Trim();
            if (slug.Length == 0) { ArticleHtml = ""; return; }
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT a.*, t.Title AS TTitle, t.Summary AS TSummary, t.StepsJson AS TSteps, t.Tips AS TTips, t.Reviewed FROM dbo.HelpArticles a " +
                "LEFT JOIN dbo.HelpTranslations t ON t.ArticleId=a.ArticleId AND t.LanguageCode=@Lang " +
                "WHERE a.Slug=@Slug AND a.IsPublished=1 AND (a.RoleName=@Role OR a.RoleName='Common')",
                p => { p.AddWithValue("@Slug", slug); p.AddWithValue("@Lang", Lang); p.AddWithValue("@Role", CurrentUser.RoleName); });
            if (dt.Rows.Count == 0) { ArticleHtml = "<div class='alert alert-warning small'>Article not found.</div>"; return; }
            DataRow r = dt.Rows[0];
            bool translated = r["TTitle"] != DBNull.Value;
            string title = translated ? Convert.ToString(r["TTitle"]) : Convert.ToString(r["TitleEn"]);
            string summary = translated ? Convert.ToString(r["TSummary"]) : Convert.ToString(r["SummaryEn"]);
            string stepsJson = translated ? Convert.ToString(r["TSteps"]) : Convert.ToString(r["StepsJson"]);
            string tips = translated ? Convert.ToString(r["TTips"]) : Convert.ToString(r["TipsEn"]);
            var sb = new StringBuilder("<div class='tile mb-3'><div class='tile__body'>");
            sb.Append("<a class='small' href='/Help/Index.aspx?lang=").Append(Lang).Append("'>&larr; All help</a>");
            sb.Append("<h2 class='h4 mt-2'>").Append(E(title)).Append("</h2><p class='text-muted'>").Append(E(summary)).Append("</p>");
            if (!translated && Lang != "en") sb.Append("<div class='alert alert-info small'>This article is not translated yet. Showing English.</div>");
            List<string> steps = new List<string>();
            try { steps.AddRange(new JavaScriptSerializer().Deserialize<string[]>(stepsJson ?? "[]")); } catch { }
            if (steps.Count > 0)
            {
                sb.Append("<ol class='small'>");
                foreach (string step in steps) sb.Append("<li class='mb-1'>").Append(E(step)).Append("</li>");
                sb.Append("</ol>");
            }
            if (!string.IsNullOrWhiteSpace(tips)) sb.Append("<div class='profile-empty'>Tip: ").Append(E(tips)).Append("</div>");
            sb.Append("<div class='d-flex gap-2 mt-3'><a class='btn btn-outline-sms btn-sm' href='javascript:window.print()'>Print</a></div>");
            sb.Append("</div></div>");
            ArticleHtml = sb.ToString();
        }

        private void AdminGuide(StringBuilder sb)
        {
            Card(sb, "Add a student or staff member",
                "Open Students and users, choose Add person, fill the name and email. The person receives a temporary password or a verification email when mail is configured.",
                "/Admin/Users.aspx", "Open Students and users");
            Card(sb, "Collect a fee",
                "Open Payments for the ledger. Record cash or cheque from the same page, or let students pay online when a gateway is configured.",
                "/Admin/Payments.aspx", "Open Payments");
            Card(sb, "Send a notice by email",
                "Open Mail centre, choose an audience or paste addresses, write the message and send now or schedule it. Delivery progress is on the Queue tab.",
                "/Admin/MailCenter.aspx", "Open Mail centre");
            Card(sb, "Publish an online test",
                "Open Online tests, choose the subject, add questions from the bank, set the window and publish. Proctoring warnings are configurable per test.",
                "/Admin/OnlineTests.aspx", "Open Online tests");
            Card(sb, "Connect Google or a payment gateway",
                "Open Settings, scroll to Integrations. Mail accounts are under Mail centre > Setup. Provider keys stay in Web.config and are never rendered.",
                "/Admin/Settings.aspx", "Open Settings");
        }

        private void FacultyGuide(StringBuilder sb)
        {
            Card(sb, "Take attendance for today",
                "Open Take attendance. Pick the class and date, mark the default present list, change exceptions, then save once.",
                "/Faculty/Attendance.aspx", "Take attendance");
            Card(sb, "Enter marks",
                "Open Marks and results, choose the exam and subject, enter marks inline and save. Students see results after the admin publishes them.",
                "/Faculty/Marks.aspx", "Enter marks");
            Card(sb, "Email a class",
                "Open Mail centre > Compose and choose the course audience. The message is queued and sent in batches.",
                "/Admin/MailCenter.aspx?tab=compose", "Compose email");
            Card(sb, "Answer a student concern",
                "Open Complaint replies, pick an open ticket, reply in the thread and set it to resolved when done.",
                "/Faculty/Complaints.aspx", "Open complaint replies");
        }

        private void StudentGuide(StringBuilder sb)
        {
            Card(sb, "Check today's classes",
                "Your dashboard lists today's timetable and the next deadline. The full week is on My timetable.",
                "/Student/Index.aspx", "Open my dashboard");
            Card(sb, "See attendance and results",
                "Attendance shows subject-wise percentages and the classes you missed. Results shows published marks and grades.",
                "/Student/Attendance.aspx", "Check attendance");
            Card(sb, "Pay a fee",
                "Open Fees and payments, find the outstanding fee and choose Pay online. In demo mode the receipt is clearly marked DEMO and no money moves.",
                "/Student/Fees.aspx", "Open Fees and payments");
            Card(sb, "Update contact details or password",
                "Open Settings > Account. You can change your password, link Google and review active sessions.",
                "/Account/Settings.aspx?tab=account", "Open account settings");
            Card(sb, "Ask for help",
                "Use Complaints to raise a ticket with the office, or the AI assistant for questions about using the app.",
                "/Student/Complaints.aspx", "Raise a complaint");
        }

        private static void Card(StringBuilder sb, string title, string body, string url, string linkText)
        {
            sb.Append("<div class='c-4'><div class='tile h-100'><div class='tile__head'>").Append(HttpUtility.HtmlEncode(title))
              .Append("</div><div class='tile__body'><p>").Append(HttpUtility.HtmlEncode(body))
              .Append("</p><a class='btn btn-outline-sms btn-sm' href='").Append(HttpUtility.HtmlAttributeEncode(url))
              .Append("'>").Append(HttpUtility.HtmlEncode(linkText)).Append("</a></div></div></div>");
        }
    }
}

