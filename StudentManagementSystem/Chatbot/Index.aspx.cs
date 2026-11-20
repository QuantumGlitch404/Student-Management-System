using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using StudentManagementSystem.Ai;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Chatbot
{
    /// <summary>
    /// Full-page offline assistant. The page is server-rendered so history is
    /// visible before any script runs; ai.js handles sending, Markdown and
    /// conversation actions against /Handlers/AiChat.ashx and
    /// /Handlers/AiConversations.ashx.
    /// </summary>
    public partial class Index : SmsPage
    {
        protected string ConversationListHtml = "";
        protected string MessagesHtml = "";
        protected string DisabledHtml = "";
        protected string ActiveTitle = "New conversation";
        protected string StatusShort = "Local model";
        protected string StatusDotClass = "";
        protected bool ContextEnabled = true;
        protected int ActiveId;

        protected void Page_Load(object sender, EventArgs e)
        {
            UserPrefs prefs = PrefsRepo.Get(CurrentUser.UserId);
            ContextEnabled = prefs.AiContext;

            int id;
            int.TryParse(Request.QueryString["id"], out id);

            List<AiConversation> conversations = new List<AiConversation>();
            AiConversation active = null;
            try
            {
                if (!prefs.AiEnabled)
                {
                    DisabledHtml = "<div class='alert alert-warning d-flex gap-2 align-items-start mb-3'><i class='bi bi-slash-circle mt-1'></i><div><strong>The assistant is switched off for your account.</strong>" +
                                   " Turn it back on in <a href='/Account/Settings.aspx?tab=ai'>Settings &rsaquo; AI assistant</a>. Conversations already stored are kept.</div></div>";
                }
                conversations = AiRepo.List(CurrentUser.UserId, "", 80);
                if (id > 0) active = AiRepo.Get(CurrentUser.UserId, id);
            }
            catch (Exception ex)
            {
                DisabledHtml = "<div class='alert alert-danger mb-3'>The assistant storage is not ready: " + E(SqlHelper.FriendlyError(ex).Message) +
                               " Run the database setup (Database/10_Premium_AI.sql) and reload.</div>";
            }

            AiStatusInfo status;
            try { status = AiService.Status(); }
            catch (Exception) { status = new AiStatusInfo { State = "error", Error = "Status unavailable" }; }

            StatusDotClass = prefs.AiEnabled && status.ModelPresent && status.RuntimePresent
                ? (status.Ready ? "ok" : "off") : "off";
            StatusShort = !prefs.AiEnabled ? "Assistant disabled"
                : !status.ModelPresent ? "Model missing"
                : !status.RuntimePresent ? "Runtime missing"
                : status.Ready ? "Local model ready" : "Model starts on first question";

            if (active != null)
            {
                ActiveId = active.ConversationId;
                ActiveTitle = active.Title;
                try { MessagesHtml = RenderMessages(AiRepo.Messages(CurrentUser.UserId, active.ConversationId, 60)); }
                catch (Exception ex) { MessagesHtml = "<div class='alert alert-danger m-3'>" + E(ex.Message) + "</div>"; }
                if (string.IsNullOrWhiteSpace(MessagesHtml)) MessagesHtml = WelcomeHtml();
            }
            else
            {
                ActiveTitle = "New conversation";
                MessagesHtml = WelcomeHtml();
            }

            ConversationListHtml = RenderConversations(conversations, ActiveId);
        }

        private string RenderConversations(List<AiConversation> items, int activeId)
        {
            if (items.Count == 0) return "<li class='ai-empty-convs'>No conversations yet. Start with a question.</li>";
            var sb = new StringBuilder();
            foreach (AiConversation c in items)
            {
                sb.Append("<li class='ai-conv").Append(c.ConversationId == activeId ? " active" : "").Append("' data-id='").Append(c.ConversationId)
                  .Append("' data-title='").Append(Attr(c.Title)).Append("' data-pinned='").Append(c.IsPinned ? "1" : "0").Append("'>");
                sb.Append("<span class='ai-conv-ico'><i class='bi ").Append(c.IsPinned ? "bi-pin-angle-fill" : "bi-chat-square-text").Append("'></i></span>");
                sb.Append("<span class='ai-conv-body'><span class='ai-conv-title'>").Append(E(c.Title)).Append("</span>");
                sb.Append("<span class='ai-conv-time'>").Append(E(Relative(c.LastActivityUtc))).Append(" &middot; ").Append(c.MessageCount).Append(" msg</span></span>");
                sb.Append("<button class='ai-conv-menu' type='button' aria-label='Conversation actions' aria-haspopup='true'><i class='bi bi-three-dots'></i></button>");
                sb.Append("<div class='ai-conv-menu-pop' role='menu'>");
                sb.Append("<button type='button' data-act='rename'><i class='bi bi-pencil'></i> Rename</button>");
                sb.Append("<button type='button' data-act='pin'><i class='bi bi-pin-angle'></i> ").Append(c.IsPinned ? "Unpin" : "Pin").Append("</button>");
                sb.Append("<button type='button' data-act='delete' class='danger'><i class='bi bi-trash3'></i> Delete</button>");
                sb.Append("</div></li>");
            }
            return sb.ToString();
        }

        private string RenderMessages(List<AiMessage> messages)
        {
            if (messages.Count == 0) return "";
            var sb = new StringBuilder();
            string lastAssistantId = null;
            foreach (AiMessage m in messages) if (m.Role == "assistant") lastAssistantId = m.MessageId.ToString();

            foreach (AiMessage m in messages)
            {
                bool me = m.Role == "user";
                sb.Append("<div class='ai-msg ").Append(me ? "me" : "bot").Append("' data-msg='").Append(m.MessageId).Append("'>");
                sb.Append("<div class='ai-avatar'>").Append(me ? E(Initials()) : "AI").Append("</div>");
                sb.Append("<div class='ai-bubble-wrap'>");
                sb.Append("<div class='ai-who'>").Append(me ? "You" : "SMS Assistant").Append(" &middot; ").Append(E(Relative(m.CreatedAtUtc))).Append("</div>");
                sb.Append("<div class='ai-bubble' data-raw='").Append(Attr(m.Content)).Append("' style='white-space:pre-wrap'>").Append(E(m.Content)).Append("</div>");
                sb.Append("<div class='ai-actions'>");
                sb.Append("<button type='button' class='ai-act' data-copy='1'><i class='bi bi-clipboard'></i> Copy</button>");
                if (!me && m.MessageId.ToString() == lastAssistantId)
                    sb.Append("<button type='button' class='ai-act' data-regen='1'><i class='bi bi-arrow-clockwise'></i> Regenerate</button>");
                sb.Append("</div>");
                if (!string.IsNullOrEmpty(m.MetaJson))
                    sb.Append("<div class='ai-meta'>").Append(E(MetaText(m.MetaJson))).Append("</div>");
                sb.Append("</div></div>");
            }
            return sb.ToString();
        }

        private string WelcomeHtml()
        {
            var sb = new StringBuilder();
            sb.Append("<div class='ai-welcome'>");
            sb.Append("<div class='ai-orb'><i class='bi bi-stars'></i></div>");
            sb.Append("<h2>How can I help?</h2>");
            sb.Append("<p>I can explain this application, read the page you are on and look up your own authorisation-scoped records. I am a small offline model: I will tell you when I do not know.</p>");
            sb.Append("<div class='ai-suggests'>");
            foreach (string[] s in Suggestions())
                sb.Append("<button type='button' class='ai-suggest' data-suggest='").Append(Attr(s[0])).Append("'><i class='bi ").Append(s[1]).Append("'></i><span>").Append(E(s[0])).Append("</span></button>");
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private string[][] Suggestions()
        {
            switch (CurrentUser.RoleName)
            {
                case "Admin":
                    return new[]
                    {
                        new[] { "How many active students are enrolled?", "bi-people" },
                        new[] { "Summarise fee collection this month", "bi-cash-coin" },
                        new[] { "How do I verify a self-registered account?", "bi-person-check" },
                        new[] { "What can I filter in the audit log?", "bi-clock-history" }
                    };
                case "Faculty":
                    return new[]
                    {
                        new[] { "How do I take attendance for a class?", "bi-clipboard-check" },
                        new[] { "How are results published?", "bi-journal-check" },
                        new[] { "Which complaints are still open?", "bi-flag" },
                        new[] { "What is on my dashboard?", "bi-speedometer2" }
                    };
                default:
                    return new[]
                    {
                        new[] { "What is my attendance percentage?", "bi-clipboard-check" },
                        new[] { "What fees are still unpaid?", "bi-cash-coin" },
                        new[] { "Explain my latest results", "bi-journal-check" },
                        new[] { "How do I raise a complaint?", "bi-flag" }
                    };
            }
        }

        private string Initials()
        {
            string n = CurrentUser.FullName ?? "?";
            string[] parts = n.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            return (parts[0].Substring(0, 1) + (parts.Length > 1 ? parts[parts.Length - 1].Substring(0, 1) : "")).ToUpperInvariant();
        }

        private static string MetaText(string json)
        {
            try
            {
                int ms = ExtractInt(json, "\"ms\":");
                int completion = ExtractInt(json, "\"completion\":");
                if (ms <= 0) return "";
                return (ms / 1000.0).ToString("0.0") + "s" + (completion > 0 ? " \u00b7 " + completion + " tokens" : "") + " \u00b7 local";
            }
            catch (Exception) { return ""; }
        }

        private static int ExtractInt(string json, string key)
        {
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return 0;
            i += key.Length;
            int end = i;
            while (end < json.Length && char.IsDigit(json[end])) end++;
            int n;
            return int.TryParse(json.Substring(i, end - i), out n) ? n : 0;
        }

        private static string Relative(DateTime utc)
        {
            TimeSpan d = DateTime.UtcNow - utc;
            if (d.TotalMinutes < 1) return "just now";
            if (d.TotalMinutes < 60) return (int)d.TotalMinutes + "m ago";
            if (d.TotalHours < 24) return (int)d.TotalHours + "h ago";
            if (d.TotalDays < 7) return (int)d.TotalDays + "d ago";
            return utc.ToLocalTime().ToString("dd MMM yyyy");
        }
    }
}