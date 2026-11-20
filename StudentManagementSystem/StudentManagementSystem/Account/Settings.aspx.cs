using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using StudentManagementSystem.Ai;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Account
{
    /// <summary>
    /// Personal settings for every role: appearance, accessibility, notification
    /// categories, academic/regional defaults, account details and privacy.
    ///
    /// Appearance values are validated with the same allow-lists as the AJAX
    /// handler. Administrator-only system settings stay on /Admin/Settings.aspx.
    /// </summary>
    public partial class Settings : SmsPage
    {
        protected string FlashHtml, SectionHtml;

        private const string BaseUrl = "/Account/Settings.aspx";

        private static readonly string[] Tabs =
            { "appearance", "accessibility", "notifications", "reminders", "ai", "academic", "account", "privacy", "data" };

        protected string TabClass(string tab)
        {
            return string.Equals(Tab(), tab, StringComparison.OrdinalIgnoreCase) ? " active" : "";
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            Build();
        }

        private string Tab()
        {
            string t = (Request.QueryString["tab"] ?? "appearance").Trim().ToLowerInvariant();
            return Array.IndexOf(Tabs, t) >= 0 ? t : "appearance";
        }

        // ------------------------------------------------------------ posts

        private void HandlePost()
        {
            string error = null;
            try
            {
                switch ((Request.Form["op"] ?? "").Trim())
                {
                    case "appearance":
                        {
                            string theme = PrefsRepo.NormalizeTheme(Request.Form["theme"]);
                            int font = PrefsRepo.NormalizeScale(ParseInt(Request.Form["fontScale"], 100));
                            string motion = PrefsRepo.NormalizeMotion(Request.Form["motion"]);
                            int zoom = PrefsRepo.NormalizeZoom(ParseInt(Request.Form["zoom"], 100));
                            PrefsRepo.SaveAppearance(CurrentUser.UserId, theme, font, motion);
                            PrefsRepo.SaveZoom(CurrentUser.UserId, zoom);
                            PrefsRepo.WriteCookie(Context, theme, font, motion, zoom);
                            Flash("success", "Appearance saved.");
                            break;
                        }

                    case "accessibility":
                        {
                            bool contrast = Request.Form["highContrast"] == "1";
                            bool collapsed = Request.Form["sidebarCollapsed"] == "1";
                            PrefsRepo.SaveAccessibility(CurrentUser.UserId, contrast, collapsed);
                            Flash("success", "Accessibility options saved.");
                            break;
                        }

                    case "notifications":
                        {
                            var disabled = new List<string>();
                            foreach (string type in PrefsRepo.NotifTypes)
                                if (Request.Form["notif_" + type] != "1") disabled.Add(type);
                            PrefsRepo.SaveNotifTypes(CurrentUser.UserId, disabled);
                            PrefsRepo.SaveNotificationExtras(CurrentUser.UserId, Request.Form["quietHours"], Request.Form["notifSound"] == "1");
                            PrefsRepo.SaveTicker(CurrentUser.UserId, Request.Form["tickerEnabled"] == "1", Request.Form["tickerSpeed"]);
                            Flash("success", "Notification preferences saved.");
                            break;
                        }

                    case "remindercreate":
                        {
                            DateTime localDue;
                            string raw = (Request.Form["dueLocal"] ?? "").Trim();
                            if (!DateTime.TryParse(raw, out localDue))
                                throw new Exception("Choose a valid reminder date and time.");
                            UserPrefs prefs = PrefsRepo.Get(CurrentUser.UserId);
                            ReminderRepo.Create(CurrentUser.UserId, Request.Form["reminderTitle"], Request.Form["reminderBody"],
                                Request.Form["reminderUrl"], localDue,
                                prefs.TimezoneId ?? Fmt.InstitutionZoneId,
                                Request.Form["reminderRepeat"], ParseInt(Request.Form["reminderLead"], prefs.ReminderLeadMinutes));
                            Flash("success", "Reminder saved. It will appear in the ticker when due.");
                            break;
                        }

                    case "remindercancel":
                        {
                            int id = ParseInt(Request.Form["reminderId"], 0);
                            if (id <= 0 || !ReminderRepo.Cancel(id, CurrentUser.UserId))
                                throw new Exception("That reminder could not be cancelled.");
                            Flash("success", "Reminder cancelled.");
                            break;
                        }

                    case "ai":
                        {
                            PrefsRepo.SaveAi(CurrentUser.UserId,
                                Request.Form["aiEnabled"] == "1",
                                Request.Form["aiContext"] == "1",
                                Request.Form["aiLength"]);
                            Flash("success", "Assistant preferences saved.");
                            break;
                        }

                    case "clearai":
                        {
                            AiRepo.DeleteAllForUser(CurrentUser.UserId);
                            AuditService.Log("AiHistoryCleared", "AiConversations", null);
                            Flash("info", "Your local assistant conversations were deleted. No other account was affected.");
                            break;
                        }

                    case "academic":
                        {
                            PrefsRepo.SaveAcademic(CurrentUser.UserId,
                                Request.Form["dateFormat"], ParseInt(Request.Form["tablePageSize"], 20),
                                Request.Form["reportFormat"], Request.Form["academicYear"], Request.Form["semester"],
                                Request.Form["defaultDashboard"]);
                            PrefsRepo.SaveRegional(CurrentUser.UserId, Request.Form["timezoneId"],
                                Request.Form["clockFormat"], ParseInt(Request.Form["reminderLeadMinutes"], 15));
                            Flash("success", "Academic and regional preferences saved.");
                            break;
                        }

                    case "phone":
                        {
                            string phone = (Request.Form["phone"] ?? "").Trim();
                            if (phone.Length > 0 && !Regex.IsMatch(phone, @"^[0-9+\- ]{7,15}$"))
                                throw new Exception("Enter a valid mobile number (7-15 digits).");
                            UsersRepo.UpdateProfile(CurrentUser.UserId, CurrentUser.FullName, phone.Length == 0 ? null : phone);
                            AuditService.Log("ProfilePhoneUpdated", "Users", CurrentUser.UserId);
                            Flash("success", "Mobile number updated.");
                            break;
                        }

                    case "email":
                        {
                            string newEmail = (Request.Form["email"] ?? "").Trim().ToLowerInvariant();
                            string password = Request.Form["currentPassword"] ?? "";
                            if (!Regex.IsMatch(newEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                                throw new Exception("Enter a valid email address.");
                            UserRecord me = UsersRepo.FindById(CurrentUser.UserId);
                            if (me == null || !PasswordHasher.Verify(password, me.PasswordHash))
                                throw new Exception("Your current password is not correct.");
                            if (string.Equals(newEmail, me.Email, StringComparison.OrdinalIgnoreCase))
                                throw new Exception("That is already your email address.");
                            if (UsersRepo.EmailExists(newEmail))
                                throw new Exception("That email address is already registered.");
                            UsersRepo.UpdateEmail(CurrentUser.UserId, newEmail);
                            AuditService.Log("EmailChanged", "Users", CurrentUser.UserId);
                            Flash("warning", "Email updated. Until an administrator verifies the new address, some features may be limited.");
                            break;
                        }

                    case "deleterequest":
                        {
                            AuditService.Log("AccountDeletionRequested", "Users", CurrentUser.UserId);
                            Flash("info", "Your deletion request was recorded. An administrator will review it; the account is not removed automatically.");
                            break;
                        }

                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }

            string tab = Tab();
            if (Request.Form["op"] == "accessibility") tab = "accessibility";
            else if (Request.Form["op"] == "notifications") tab = "notifications";
            else if (Request.Form["op"] == "remindercreate" || Request.Form["op"] == "remindercancel") tab = "reminders";
            else if (Request.Form["op"] == "ai" || Request.Form["op"] == "clearai") tab = "ai";
            else if (Request.Form["op"] == "academic") tab = "academic";
            else if (Request.Form["op"] == "phone" || Request.Form["op"] == "email") tab = "account";
            else if (Request.Form["op"] == "deleterequest") tab = "privacy";

            Redirect(BaseUrl + "?tab=" + tab + (error != null ? "&err=" + HttpUtility.UrlEncode(error) : ""));
        }

        private static int ParseInt(string value, int fallback)
        {
            int n;
            return int.TryParse(value, out n) ? n : fallback;
        }

        // ------------------------------------------------------------ render

        private void Build()
        {
            switch (Tab())
            {
                case "accessibility": SectionHtml = AccessibilitySection(); break;
                case "notifications": SectionHtml = NotificationsSection(); break;
                case "reminders": SectionHtml = RemindersSection(); break;
                case "ai": SectionHtml = AiSection(); break;
                case "academic": SectionHtml = AcademicSection(); break;
                case "account": SectionHtml = AccountSection(); break;
                case "privacy": SectionHtml = PrivacySection(); break;
                case "data": SectionHtml = DataSection(); break;
                default: SectionHtml = AppearanceSection(); break;
            }
        }

        private string AppearanceSection()
        {
            UserPrefs p = PrefsRepo.Get(CurrentUser.UserId);
            var sb = new StringBuilder();
            sb.Append(Card("Appearance", "Theme, text size, zoom and motion are stored with your account.",
                "<form class='settings-appearance-form' method='post' action='" + BaseUrl + "?tab=appearance'>" + CsrfField() +
                "<input type='hidden' name='op' value='appearance'/>" +
                Row("Theme", "Pick a theme. It previews immediately; save the form to keep it.",
                    ThemeChoices(p.Theme)) +
                Row("Text size", "Scales body copy from 85% to 140% without changing spacing or control geometry.",
                    Select("fontScale", FontOptions(p.FontScale), p.FontScale.ToString()) +
                    "<div class='settings-note mt-1'>The A&minus; / A / A+ controls in the top bar change the same value.</div>") +
                Row("Interface zoom", "Zooms the whole interface from 80% to 150%, scaling type and spacing together. The - / % / + control in the top bar uses the same value.",
                    Select("zoom", ZoomOptions(p.Zoom), p.Zoom.ToString())) +
                Row("Motion", "Reduced motion disables entrance and hover animation but keeps every control usable.",
                    Choice("motion", new[] { "full", "reduced" }, new[] { "Full", "Reduced" }, p.Motion)) +
                "<div class='settings-savebar' data-settings-savebar hidden>" +
                "<span class='settings-savebar-text'>Unsaved changes</span>" +
                "<div class='settings-savebar-actions'>" +
                "<button class='btn btn-outline-sms btn-sm' type='button' data-settings-discard>Discard</button>" +
                "<button class='btn btn-sms btn-sm' type='submit'>Save appearance</button>" +
                "</div></div></form>"));
            return sb.ToString();
        }

        private static string ThemeChoices(string current)
        {
            if (current != "dark" && current != "eye") current = "light";
            var sb = new StringBuilder();
            sb.Append("<div class='theme-card-grid' role='radiogroup' aria-label='Theme'>");
            ThemeChoice(sb, current, "light", "Light", "Warm paper, ink borders, full contrast.");
            ThemeChoice(sb, current, "dark", "Dark", "Low-glare ink surfaces for night work.");
            ThemeChoice(sb, current, "eye", "Eye", "Warm parchment with softened contrast.");
            sb.Append("</div>");
            return sb.ToString();
        }

        private static void ThemeChoice(StringBuilder sb, string current, string value, string label, string description)
        {
            bool selected = string.Equals(current, value, StringComparison.Ordinal);
            sb.Append("<label class='theme-card'><input type='radio' name='theme' value='").Append(value).Append("'")
              .Append(selected ? " checked='checked'" : "").Append(" data-settings-theme/>")
              .Append("<span class='theme-preview theme-preview-").Append(value).Append("' aria-hidden='true'>")
              .Append("<span class='tp-side'></span><span class='tp-main'><span class='tp-bar'></span>")
              .Append("<span class='tp-block'></span><span class='tp-line'></span></span></span>")
              .Append("<span class='theme-card-title'>").Append(label).Append("</span>")
              .Append("<span class='theme-card-desc'>").Append(description).Append("</span></label>");
        }

        private string Select(string name, string optionsHtml, string selected)
        {
            string setting = name == "fontScale" ? " data-setting='textScale'" : name == "zoom" ? " data-setting='zoom'" : "";
            return "<select class='form-select form-select-sm settings-select' name='" + name + "'" + setting + ">" + optionsHtml + "</select>";
        }

        private string FontOptions(int selected)
        {
            var sb = new StringBuilder();
            for (int v = 85; v <= 140; v += 5)
                sb.Append("<option value='").Append(v).Append("'")
                  .Append(v == selected ? " selected='selected'" : "").Append(">").Append(v).Append("%</option>");
            return sb.ToString();
        }

        private string ZoomOptions(int selected)
        {
            var sb = new StringBuilder();
            for (int v = 80; v <= 150; v += 10)
                sb.Append("<option value='").Append(v).Append("'")
                  .Append(v == selected ? " selected='selected'" : "").Append(">").Append(v).Append("%</option>");
            return sb.ToString();
        }

        /// <summary>Radio pills: one value is always submitted, so saving can never reset a setting.</summary>
        private string Choice(string name, string[] values, string[] labels, string selected)
        {
            var sb = new StringBuilder("<div class='settings-choice' role='radiogroup' aria-label='" + HttpUtility.HtmlAttributeEncode(name) + "'>");
            for (int i = 0; i < values.Length; i++)
            {
                string id = "ch_" + name + "_" + values[i];
                bool on = string.Equals(values[i], selected, StringComparison.OrdinalIgnoreCase);
                sb.Append("<input class='choice-input' type='radio' name='").Append(name).Append("' id='").Append(id)
                  .Append("' value='").Append(values[i]).Append("'")
                  .Append(name == "motion" ? " data-setting='motion'" : "")
                  .Append(on ? " checked='checked'" : "").Append("/>");
                sb.Append("<label class='choice-pill" + (on ? " is-on" : "") + "' for='").Append(id).Append("'>")
                  .Append(labels[i]).Append("</label>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private string AccessibilitySection()
        {
            UserPrefs p = PrefsRepo.Get(CurrentUser.UserId);
            var sb = new StringBuilder();
            sb.Append(Card("Accessibility", "These options are stored per account and work in both themes.",
                "<form method='post' action='" + BaseUrl + "?tab=accessibility'>" + CsrfField() +
                "<input type='hidden' name='op' value='accessibility'/>" +
                SwitchRow("High contrast", "Stronger borders and darker secondary text for low-vision use.", "highContrast", p.HighContrast) +
                SwitchRow("Collapse the sidebar", "Icon-only navigation leaves more room for content. Applies at desktop widths.", "sidebarCollapsed", p.SidebarCollapsed) +
                "<div class='settings-row'><div><div class='settings-label'>Keyboard shortcuts</div>" +
                "<div class='settings-desc'>Tab moves through every control with a visible focus ring; Escape closes the notification panel and dialog overlays; Enter submits the focused form.</div></div></div>" +
                "<div class='settings-row'><div><div class='settings-label'>Screen readers</div>" +
                "<div class='settings-desc'>Icons carry accessible names, validation messages are announced (role=\"alert\"), and unread notifications are marked for assistive technology.</div></div></div>" +
                "<div class='pt-3'><button class='btn btn-sms btn-sm' type='submit'>Save accessibility options</button></div></form>"));
            return sb.ToString();
        }

        private string NotificationsSection()
        {
            UserPrefs p = PrefsRepo.Get(CurrentUser.UserId);
            var disabled = PrefsRepo.DisabledSet(p);
            var sb = new StringBuilder();
            var rows = new StringBuilder();
            var info = new Dictionary<string, string>
            {
                { "general", "General announcements and office messages." },
                { "academic", "Attendance, marks, results and exam updates." },
                { "payment", "Fee reminders and payment receipts." },
                { "complaint", "Replies and status changes on your tickets." },
                { "system", "Account and security notices. Recommended to keep on." }
            };
            foreach (string type in PrefsRepo.NotifTypes)
            {
                bool on = !disabled.Contains(type);
                rows.Append(SwitchRow(Cap(type) + " notifications", info[type], "notif_" + type, on));
            }
            sb.Append(Card("Notification categories", "Choose what appears in your bell and notification centre. Email and push delivery need provider configuration on this installation and are not active by default.",
                "<form method='post' action='" + BaseUrl + "?tab=notifications'>" + CsrfField() +
                "<input type='hidden' name='op' value='notifications'/>" + rows +
                SwitchRow("Campus ticker", "Show unread notifications in the scrolling band below the top bar. Hover or focus pauses it; the preview dialog can mark read or snooze.", "tickerEnabled", p.TickerEnabled) +
                Row("Ticker speed", "Slow is readable for long messages; fast is useful for short campus updates.",
                    Choice("tickerSpeed", PrefsRepo.TickerSpeeds, new[] { "Slow", "Normal", "Fast" }, p.TickerSpeed)) +
                "<div class='settings-row'><div><div class='settings-label'>Quiet hours</div>" +
                "<div class='settings-desc'>Optional, for example 22:00-07:00 in your timezone. Reminder notifications due during these hours are held until the window ends; ticker display is unaffected.</div></div>" +
                "<input name='quietHours' class='form-control form-control-sm' style='max-width:140px' maxlength='20' placeholder='22:00-07:00' value='" + Attr(p.QuietHours) + "'/></div>" +
                SwitchRow("Notification sound", "Play a short tone when an unread badge increases while this tab is open.", "notifSound", p.NotifSound) +
                "<div class='alert alert-info small mt-3 mb-0'>Email notifications: " +
                (AppConfig.EmailEnabled ? "configured on this server." : "not configured on this server, so no email is sent.") +
                " Push notifications: " + (AppConfig.PushEnabled ? "enabled." : "not configured.") + "</div>" +
                "<div class='pt-3'><button class='btn btn-sms btn-sm' type='submit'>Save notification preferences</button></div></form>"));
            sb.Append("<div class='mt-3'><a class='btn btn-outline-sms btn-sm' href='/Notifications/Index.aspx'>Open notification centre</a> " +
                      "<a class='btn btn-outline-sms btn-sm' href='/Student/Notifications.aspx'>Student inbox</a></div>");
            return sb.ToString();
        }

        private string RemindersSection()
        {
            UserPrefs p = PrefsRepo.Get(CurrentUser.UserId);
            string zone = p.TimezoneId ?? Fmt.InstitutionZoneId;

            string repeatOptions = "";
            foreach (string rule in new[] { "none", "daily", "weekly", "monthly" })
                repeatOptions += "<option value='" + rule + "'>" +
                                 (rule == "none" ? "Once" : Cap(rule)) + "</option>";

            string leadOptions = "";
            foreach (int lead in PrefsRepo.ReminderLeads)
                leadOptions += "<option value='" + lead + "'" + (lead == p.ReminderLeadMinutes ? " selected='selected'" : "") + ">" +
                               (lead == 0 ? "At the time" : lead + " minutes before") + "</option>";

            var list = new StringBuilder();
            DataTable dt = ReminderRepo.Upcoming(CurrentUser.UserId, 50);
            if (dt.Rows.Count == 0)
            {
                list.Append("<div class='settings-row'><div><div class='settings-label'>No reminders yet</div>" +
                            "<div class='settings-desc'>Create one below. When it is due it becomes a notification in your bell and the campus ticker.</div></div></div>");
            }
            else
            {
                foreach (DataRow row in dt.Rows)
                {
                    int id = Convert.ToInt32(row["ReminderId"]);
                    string status = Convert.ToString(row["Status"]);
                    string repeat = Convert.ToString(row["RepeatRule"]);
                    string when = Fmt.Stamp(Convert.ToDateTime(row["DueAtUtc"]), zone, p.DateFormat + " HH:mm");
                    list.Append("<div class='settings-row'><div><div class='settings-label'>" + E(row["Title"]) + "</div>" +
                        "<div class='settings-desc'>" + E(when) + " &middot; " + E(zone) + " &middot; " +
                        (repeat == "none" ? "once" : E(repeat)) + " &middot; " + E(status) + "</div></div>");
                    if (status == "pending")
                    {
                        list.Append("<form method='post' action='" + BaseUrl + "?tab=reminders'>" + CsrfField() +
                            "<input type='hidden' name='op' value='remindercancel'/>" +
                            "<input type='hidden' name='reminderId' value='" + id + "'/>" +
                            "<button class='btn btn-outline-sms btn-sm' type='submit' data-confirm='Cancel this reminder?'>Cancel</button></form>");
                    }
                    else list.Append("<span class='badge badge-info'>" + E(status) + "</span>");
                    list.Append("</div>");
                }
            }

            var sb = new StringBuilder();
            sb.Append(Card("Reminders", "Personal reminders scheduled in " + zone + ". Times are converted to UTC before they are stored.",
                "<details class='settings-disclosure'><summary>Create a reminder</summary>" +
                "<form method='post' action='" + BaseUrl + "?tab=reminders'>" + CsrfField() +
                "<input type='hidden' name='op' value='remindercreate'/>" +
                Row("Title", "Short and specific, for example Review marks moderation.", "<input name='reminderTitle' class='form-control form-control-sm' maxlength='160' required style='min-width:260px'/>") +
                Row("Details", "Optional message shown in the notification preview.", "<textarea name='reminderBody' class='form-control form-control-sm' rows='2' maxlength='1000' style='min-width:260px'></textarea>") +
                Row("Date and time", "Enter the time in " + zone + ". The reminder lead time is applied first.", "<input name='dueLocal' type='datetime-local' class='form-control form-control-sm' required/>") +
                Row("Repeat", "Repeating reminders are rescheduled after each delivery.", "<select name='reminderRepeat' class='form-select form-select-sm' style='max-width:180px'>" + repeatOptions + "</select>") +
                Row("Lead time", "How early the notification should appear.", "<select name='reminderLead' class='form-select form-select-sm' style='max-width:200px'>" + leadOptions + "</select>") +
                Row("Optional link", "Must be a local path such as /Student/Results.aspx.", "<input name='reminderUrl' class='form-control form-control-sm' maxlength='300' placeholder='/Student/Results.aspx' style='min-width:260px'/>") +
                "<div class='pt-3'><button class='btn btn-sms btn-sm' type='submit'>Save reminder</button></div></form></details>"));
            sb.Append(Card("Your reminders", "Newest first. Pending reminders can be cancelled; sent and cancelled reminders stay for reference.", list.ToString()));
            return sb.ToString();
        }

        private string AiSection()
        {
            UserPrefs p = PrefsRepo.Get(CurrentUser.UserId);
            AiStatusInfo s;
            try { s = AiService.Status(); }
            catch (Exception) { s = new AiStatusInfo { State = "error", Error = "Status unavailable" }; }
            int chats = AiRepo.CountForUser(CurrentUser.UserId);

            string stateText = !s.Enabled ? "Disabled by administrator"
                : !s.ModelPresent ? "Model file missing"
                : !s.RuntimePresent ? "Runtime missing"
                : s.Ready ? "Running and ready" : "Installed, starts on first question";
            string size = s.ModelBytes > 0 ? (s.ModelBytes / (1024.0 * 1024.0)).ToString("0") + " MB" : "not found";

            string modelFileName = "Ektome-Qwen1.5-0.5B-Chat-PristinelyUncensored.Q2_K.gguf";
            var status = new StringBuilder();
            status.Append("<div class='ai-status-grid'>");
            status.Append("<div class='ai-status-item'><div class='k'>State</div><div class='v'>" + E(stateText) + "</div></div>");
            status.Append("<div class='ai-status-item'><div class='k'>Model</div><div class='v'>Local model</div></div>");
            status.Append("<div class='ai-status-item'><div class='k'>Model size</div><div class='v'>" + E(size) + "</div></div>");
            status.Append("<div class='ai-status-item'><div class='k'>Runtime</div><div class='v'>llama.cpp &middot; CPU &middot; port " + s.Port + "</div></div>");
            status.Append("<div class='ai-status-item'><div class='k'>Storage</div><div class='v'>AI/model/" + E(modelFileName) + "</div></div>");
            status.Append("<div class='ai-status-item'><div class='k'>Your conversations</div><div class='v'>" + chats + "</div></div>");
            status.Append("</div>");
            if (!string.IsNullOrEmpty(s.Error))
                status.Append("<div class='alert alert-warning small mt-2 mb-0'>" + E(s.Error) + "</div>");

            var sb = new StringBuilder();
            sb.Append(Card("AI assistant", "The assistant answers from the bundled local model. No cloud API key is used and questions stay on this computer.",
                "<form method='post' action='" + BaseUrl + "?tab=ai'>" + CsrfField() +
                "<input type='hidden' name='op' value='ai'/>" +
                SwitchRow("Enable the assistant", "Turn the assistant panel and full page off for your account without deleting history.", "aiEnabled", p.AiEnabled) +
                SwitchRow("Current page context", "Allow the assistant to receive the headings, filters and visible rows of the page you are on. It never receives other users' records.", "aiContext", p.AiContext) +
                Row("Response length", "Short is best for quick facts; detailed allows a longer explanation.",
                    Choice("aiLength", new[] { "short", "balanced", "detailed" }, new[] { "Short", "Balanced", "Detailed" }, p.AiResponseLength)) +
                "<div class='settings-note mt-2'>The local model is a 0.5B-parameter Qwen1.5 model. It is useful for this application's data and navigation, but it is not a large general-purpose assistant and may be wrong on complex or unrelated questions. It is instructed to say when it does not know.</div>" +
                "<div class='pt-3'><button class='btn btn-sms btn-sm' type='submit'>Save assistant preferences</button> " +
                "<a class='btn btn-outline-sms btn-sm' href='/Chatbot/Index.aspx'>Open assistant</a></div></form>"));

            sb.Append(Card("Local model status", "Model and runtime bundled with this installation. Only the administrator can change server-level settings.", status.ToString()));

            sb.Append(Card("Conversation history", "Conversations are stored in the application database under your account and are removed when you delete them.",
                "<div class='settings-row'><div><div class='settings-label'>" + chats + " conversation(s)</div>" +
                "<div class='settings-desc'>Stored locally. No message content is sent to an external service.</div></div>" +
                "<a class='btn btn-outline-sms btn-sm' href='/Chatbot/Index.aspx'>Manage conversations</a></div>" +
                "<form method='post' action='" + BaseUrl + "?tab=ai' class='settings-row'>" + CsrfField() +
                "<input type='hidden' name='op' value='clearai'/>" +
                "<div><div class='settings-label'>Clear all assistant history</div><div class='settings-desc'>Deletes every conversation and message for your account. This cannot be undone.</div></div>" +
                "<button class='btn btn-outline-sms btn-sm' type='submit' data-confirm='Delete all of your assistant conversations?' formnovalidate>Clear history</button></form>"));
            return sb.ToString();
        }

        private string AcademicSection()
        {
            UserPrefs p = PrefsRepo.Get(CurrentUser.UserId);
            var sb = new StringBuilder();
            string dateOptions = "";
            foreach (string f in PrefsRepo.DateFormats)
                dateOptions += "<option value='" + f + "'" + (f == p.DateFormat ? " selected='selected'" : "") + ">" + f + "</option>";
            string pageOptions = "";
            foreach (int s in PrefsRepo.PageSizes)
                pageOptions += "<option value='" + s + "'" + (s == p.TablePageSize ? " selected='selected'" : "") + ">" + s + " rows</option>";
            string reportOptions = "";
            foreach (string f in PrefsRepo.ReportFormats)
                reportOptions += "<option value='" + f + "'" + (f == p.ReportFormat ? " selected='selected'" : "") + ">" + f.ToUpperInvariant() + "</option>";

            var zoneOptions = new StringBuilder();
            zoneOptions.Append("<option value=''>Institution default (").Append(E(Fmt.InstitutionZoneId)).Append(")</option>");
            string selectedZone = p.TimezoneId ?? "";
            foreach (string zone in Fmt.TimeZoneIds())
                zoneOptions.Append("<option value='").Append(Attr(zone)).Append("'")
                           .Append(string.Equals(zone, selectedZone, StringComparison.Ordinal) ? " selected='selected'" : "")
                           .Append(">").Append(E(zone)).Append("</option>");

            string leadOptions = "";
            foreach (int lead in PrefsRepo.ReminderLeads)
                leadOptions += "<option value='" + lead + "'" + (lead == p.ReminderLeadMinutes ? " selected='selected'" : "") + ">" +
                               (lead == 0 ? "At the time" : lead + " minutes before") + "</option>";

            string sessions = "<option value=''>Any</option>";
            try
            {
                DataTable dt = AcademicsRepo.Sessions();
                foreach (DataRow r in dt.Rows)
                {
                    string name = r["SessionName"] as string ?? "";
                    sessions += "<option value='" + Attr(name) + "'" + (name == p.AcademicYear ? " selected" : "") + ">" + E(name) + "</option>";
                }
            }
            catch (Exception) { /* sessions table unavailable: keep the free-text fallback */ }

            sb.Append(Card("Academic and regional", "Defaults used by lists, exports and notification timestamps.",
                "<form method='post' action='" + BaseUrl + "?tab=academic'>" + CsrfField() +
                "<input type='hidden' name='op' value='academic'/>" +
                Row("Preferred academic year", "Pre-selects the session on pages that offer one.", "<select name='academicYear' class='form-select form-select-sm' style='max-width:200px'>" + sessions + "</select>") +
                Row("Preferred semester", "Free text, for example Spring or Odd.", "<input name='semester' class='form-control form-control-sm' style='max-width:160px' maxlength='20' value='" + Attr(p.Semester) + "'/>") +
                Row("Date format", "Used for notification and activity timestamps.", "<select name='dateFormat' class='form-select form-select-sm' style='max-width:200px'>" + dateOptions + "</select>") +
                Row("Table page size", "Rows per page in the notification centre and other paged lists.", "<select name='tablePageSize' class='form-select form-select-sm' style='max-width:160px'>" + pageOptions + "</select>") +
                Row("Default report format", "Preselected export format where an export offers a choice.", "<select name='reportFormat' class='form-select form-select-sm' style='max-width:160px'>" + reportOptions + "</select>") +
                Row("Timezone", "IANA timezone used for timestamps, reminders and online test schedules. Instants are stored in UTC; this only changes display.", "<select name='timezoneId' class='form-select form-select-sm' style='max-width:280px'>" + zoneOptions + "</select>") +
                Row("Clock", "Used by the live topbar clock and notification timestamps.", Choice("clockFormat", PrefsRepo.ClockFormats, new[] { "24-hour", "12-hour" }, p.ClockFormat)) +
                Row("Reminder lead", "Default lead time offered when creating a personal reminder.", "<select name='reminderLeadMinutes' class='form-select form-select-sm' style='max-width:200px'>" + leadOptions + "</select>") +
                "<div class='pt-3'><button class='btn btn-sms btn-sm' type='submit'>Save academic preferences</button></div></form>"));
            return sb.ToString();
        }

        private string AccountSection()
        {
            UserRecord me = UsersRepo.FindById(CurrentUser.UserId) ?? new UserRecord();
            var sb = new StringBuilder();
            sb.Append(Card("Account details", "Your name is managed in your profile; email changes are re-verified.",
                "<div class='settings-row'><div><div class='settings-label'>" + E(me.FullName) + "</div>" +
                "<div class='settings-desc'>" + E(me.RoleName) + " &middot; " + E(me.Email) + " &middot; " +
                (me.EmailVerified ? "email verified" : "email not verified") + "</div></div>" +
                "<div class='settings-choice'><a class='btn btn-outline-sms btn-sm' href='" + ProfileUrl() + "'>Open profile</a></div></div>" +

                "<form method='post' action='" + BaseUrl + "?tab=account' class='settings-row' style='align-items:flex-end'>" + CsrfField() +
                "<input type='hidden' name='op' value='phone'/>" +
                "<div><div class='settings-label'>Mobile number</div><div class='settings-desc'>Optional, 7-15 digits.</div>" +
                "<input name='phone' class='form-control form-control-sm mt-1' style='max-width:220px' value='" + Attr(me.Phone) + "' maxlength='15'/></div>" +
                "<button class='btn btn-sms btn-sm' type='submit'>Save mobile</button></form>" +

                "<form method='post' action='" + BaseUrl + "?tab=account'>" + CsrfField() +
                "<input type='hidden' name='op' value='email'/>" +
                Row("Change email", "Requires your current password. The new address is marked unverified until an administrator approves it.",
                    "<div class='d-flex flex-column gap-2' style='min-width:260px'>" +
                    "<input name='email' type='email' required class='form-control form-control-sm' placeholder='new@example.com' maxlength='150' autocomplete='email'/>" +
                    "<input name='currentPassword' type='password' required class='form-control form-control-sm' placeholder='current password' autocomplete='current-password'/>" +
                    "</div>") +
                "<div class='pt-3'><button class='btn btn-sms btn-sm' type='submit'>Update email</button></div></form>") +
                Card("Password", "Change your password from the profile page, which verifies your current password and applies the configured complexity rules.",
                "<a class='btn btn-outline-sms btn-sm' href='" + ProfileUrl() + "'>Change password</a>"));
            return sb.ToString();
        }

        private string PrivacySection()
        {
            UserRecord me = UsersRepo.FindById(CurrentUser.UserId) ?? new UserRecord();
            UserPrefs prefs = PrefsRepo.Get(CurrentUser.UserId);
            string zone = prefs.TimezoneId ?? Fmt.InstitutionZoneId;
            var sb = new StringBuilder();
            sb.Append(Card("Privacy and security", "What this installation stores about your account.",
                "<div class='settings-row'><div><div class='settings-label'>Last sign-in</div><div class='settings-desc'>" +
                (me.LastLoginAtUtc.HasValue ? E(Fmt.Stamp(me.LastLoginAtUtc.Value, zone)) : "Not recorded yet") +
                "</div></div></div>" +
                "<div class='settings-row'><div><div class='settings-label'>Two-factor authentication</div><div class='settings-desc'>" +
                (me.TwoFactorEnabled ? "Enabled for this account." : "Not enabled. A one-time code is sent by email when enabled; email delivery must be configured first.") +
                "</div></div></div>" +
                "<div class='settings-row'><div><div class='settings-label'>Active sessions</div><div class='settings-desc'>This installation issues a single browser session per sign-in; it is ended by signing out or by the configured session timeout. A device list is not stored.</div></div></div>" +
                "<div class='settings-row'><div><div class='settings-label'>Login activity</div><div class='settings-desc'>Sign-ins and failed attempts are written to the audit log (admin-visible).</div></div></div>" +
                "<form method='post' action='" + BaseUrl + "?tab=privacy' class='settings-row'>" + CsrfField() +
                "<input type='hidden' name='op' value='deleterequest'/>" +
                "<div><div class='settings-label'>Request account deletion</div><div class='settings-desc'>Records a request for an administrator to review. Nothing is deleted automatically, and academic records may have to be retained by policy.</div></div>" +
                "<button class='btn btn-outline-sms btn-sm' type='submit' data-confirm='Record an account deletion request for administrator review?' formnovalidate>Request deletion</button></form>"));
            return sb.ToString();
        }

        private string DataSection()
        {
            UserPrefs p = PrefsRepo.Get(CurrentUser.UserId);
            var sb = new StringBuilder();
            sb.Append(Card("Data and export", "Download your own account data, or review export defaults.",
                "<div class='settings-row'><div><div class='settings-label'>My data</div>" +
                "<div class='settings-desc'>Exports your profile, preferences and recent notifications as JSON. No other user's data is included.</div></div>" +
                "<a class='btn btn-sms btn-sm' href='/Handlers/MyData.ashx'>Download my data</a></div>" +
                "<div class='settings-row'><div><div class='settings-label'>Report format</div><div class='settings-desc'>Current default: " +
                E(p.ReportFormat.ToUpperInvariant()) + ". Change it under Academic and regional.</div></div></div>" +
                "<div class='settings-row'><div><div class='settings-label'>Administrative backups</div><div class='settings-desc'>Database backups are restricted to administrators and are not exposed here.</div></div></div>"));
            return sb.ToString();
        }

        // ------------------------------------------------------------ helpers

        private string ProfileUrl()
        {
            switch (CurrentUser.RoleName)
            {
                case "Admin": return "/Admin/Users.aspx";
                case "Faculty": return "/Faculty/Profile.aspx";
                default: return "/Student/Profile.aspx";
            }
        }

        private static string Card(string title, string subtitle, string body)
        {
            return "<div class='tile settings-section'><div class='tile__head'>" + HttpUtility.HtmlEncode(title) + "</div>" +
                   "<div class='tile__body'><p class='settings-note'>" + HttpUtility.HtmlEncode(subtitle) + "</p>" + body + "</div></div>";
        }

        private static string Row(string label, string description, string control)
        {
            return "<div class='settings-row'><div><div class='settings-label'>" + HttpUtility.HtmlEncode(label) + "</div>" +
                   "<div class='settings-desc'>" + HttpUtility.HtmlEncode(description) + "</div></div>" +
                   "<div>" + control + "</div></div>";
        }

        private static string SwitchRow(string label, string description, string name, bool on)
        {
            return "<div class='settings-row'><div><div class='settings-label'>" + HttpUtility.HtmlEncode(label) + "</div>" +
                   "<div class='settings-desc'>" + HttpUtility.HtmlEncode(description) + "</div></div>" +
                   "<div class='form-check form-switch'><input class='form-check-input' type='checkbox' role='switch' name='" + name +
                   "' value='1' id='sw_" + name + "'" + (on ? " checked='checked'" : "") + "/>" +
                   "<label class='visually-hidden' for='sw_" + name + "'>" + HttpUtility.HtmlEncode(label) + "</label></div></div>";
        }

        private static string Cap(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}

