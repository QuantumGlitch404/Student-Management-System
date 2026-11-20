using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Admin
{
    /// <summary>Admin/faculty mail centre: accounts, composer, queue, inbox and templates.</summary>
    public partial class MailCenter : SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin", "Faculty" }; } }

        protected string FlashHtml, TabsHtml, BodyHtml;
        private bool IsAdmin { get { return CurrentUser.RoleName == "Admin"; } }

        private static readonly string[] Tabs = { "compose", "queue", "inbox", "templates", "setup" };

        private string Tab()
        {
            string t = (Request.QueryString["tab"] ?? "compose").Trim().ToLowerInvariant();
            return Array.IndexOf(Tabs, t) >= 0 ? t : "compose";
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            BuildTabs();
            BuildBody();
        }

        private void BuildTabs()
        {
            string current = Tab();
            var sb = new StringBuilder();
            foreach (string tab in Tabs)
            {
                if (tab == "setup" && !IsAdmin) continue;
                sb.Append("<a role='tab' aria-selected='").Append(tab == current ? "true" : "false").Append("' class='tab").Append(tab == current ? " active" : "").Append("' href='?tab=")
                  .Append(tab).Append("'>").Append(Cap(tab)).Append("</a>");
            }
            TabsHtml = sb.ToString();
        }

        private void HandlePost()
        {
            string error = null;
            string redirectTab = Tab();
            try
            {
                switch ((Request.Form["op"] ?? "").Trim())
                {
                    case "saveaccount":
                        {
                            RequireAdmin();
                            int id = ParseInt(Request.Form["id"]);
                            int smtpPort = ParseInt(Request.Form["smtpPort"], 587);
                            int imapPort = ParseInt(Request.Form["imapPort"], 993);
                            int daily = ParseInt(Request.Form["dailyLimit"], 400);
                            int perMin = ParseInt(Request.Form["perMinute"], 20);
                            MailRepo.SaveAccount(id,
                                Trim(Request.Form["label"]), Trim(Request.Form["email"]), Trim(Request.Form["display"]),
                                Trim(Request.Form["smtpHost"]), smtpPort, FormBool("smtpSsl"),
                                Trim(Request.Form["smtpUser"]), Request.Form["smtpPassword"],
                                Trim(Request.Form["imapHost"]), imapPort, FormBool("imapSsl"),
                                Trim(Request.Form["imapUser"]), Request.Form["imapPassword"],
                                daily, perMin, FormBool("isDefault"), FormBool("isActive"));
                            Flash("success", "Mail account saved.");
                            redirectTab = "setup";
                            break;
                        }
                    case "deleteaccount":
                        RequireAdmin();
                        MailRepo.DeleteAccount(ParseInt(Request.Form["id"]));
                        Flash("info", "Mail account removed.");
                        redirectTab = "setup";
                        break;
                    case "testaccount":
                        {
                            RequireAdmin();
                            string to = Trim(Request.Form["to"]);
                            string fail = MailRepo.TestAccount(ParseInt(Request.Form["id"]), to);
                            Flash(fail == null ? "success" : "danger", fail == null
                                ? "Test message delivered to " + to + "."
                                : "Send failed: " + fail);
                            redirectTab = "setup";
                            break;
                        }
                    case "send":
                        {
                            string subject = Trim(Request.Form["subject"]);
                            if (subject.Length == 0) throw new Exception("Add a subject.");
                            string body = ComposeHtml(Request.Form["body"]);
                            if (body.Length == 0) throw new Exception("Write a message.");
                            int accountId = ParseInt(Request.Form["accountId"]);
                            DateTime? when = ParseLocal(Request.Form["schedule"]);
                            int queueId;
                            string manual = Trim(Request.Form["to"]);
                            if (manual.Length > 0)
                            {
                                queueId = MailRepo.Enqueue(accountId, subject, body, ParsePeople(manual),
                                    CurrentUser.UserId, when, FormBool("bulk"));
                            }
                            else
                            {
                                queueId = MailRepo.EnqueueToAudience(accountId, subject, body,
                                    Trim(Request.Form["audience"]), CurrentUser.UserId, when, FormBool("bulk"));
                            }
                            if (when.HasValue && when.Value > DateTime.UtcNow.AddMinutes(1))
                                Flash("success", "Campaign #" + queueId + " scheduled for " + when.Value.ToString("yyyy-MM-dd HH:mm") + ".");
                            else
                            {
                                string result = MailRepo.SendCampaign(queueId);
                                Flash("success", "Campaign #" + queueId + " processed. " + result);
                            }
                            AuditService.Log("MailCampaign", "MailQueue", queueId, subject);
                            redirectTab = "queue";
                            break;
                        }
                    case "pause":
                    case "resume":
                    case "cancel":
                        {
                            int id = ParseInt(Request.Form["id"]);
                            if (Request.Form["op"] == "cancel") MailRepo.Cancel(id);
                            else MailRepo.Pause(id, Request.Form["op"] == "pause");
                            Flash("info", "Campaign updated.");
                            redirectTab = "queue";
                            break;
                        }
                    case "sendnow":
                        {
                            int id = ParseInt(Request.Form["id"]);
                            string result = MailRepo.SendCampaign(id);
                            Flash("success", "Campaign #" + id + ": " + result);
                            redirectTab = "queue";
                            break;
                        }
                    case "sync":
                        {
                            string result = MailRepo.SyncInbox(ParseInt(Request.Form["id"]));
                            Flash(result.StartsWith("IMAP sync failed", StringComparison.OrdinalIgnoreCase) ? "danger" : "success", result);
                            redirectTab = "inbox";
                            break;
                        }
                    case "savetemplate":
                        {
                            string name = Trim(Request.Form["name"]);
                            if (name.Length == 0) throw new Exception("Template name is required.");
                            MailRepo.SaveTemplate(ParseInt(Request.Form["templateId"]), name,
                                Trim(Request.Form["subject"]), Request.Form["body"]);
                            Flash("success", "Template saved.");
                            redirectTab = "templates";
                            break;
                        }
                    case "deletetemplate":
                        MailRepo.DeleteTemplate(ParseInt(Request.Form["templateId"]));
                        Flash("info", "Template removed.");
                        redirectTab = "templates";
                        break;
                    case "mark":
                        {
                            int id = ParseInt(Request.Form["messageId"]);
                            MailRepo.MarkMessage(id, Request.Form["read"] == "1" ? true : (bool?)null,
                                Request.Form["star"] == "1" ? true : (bool?)null, Trim(Request.Form["folder"]));
                            redirectTab = "inbox";
                            break;
                        }
                    case "savesettings":
                        {
                            RequireAdmin();
                            MailRepo.SetSetting("Mail.FromName", Trim(Request.Form["fromName"]), CurrentUser.UserId);
                            MailRepo.SetSetting("Mail.DailyLimit", Trim(Request.Form["dailyLimit"]), CurrentUser.UserId);
                            MailRepo.SetSetting("Mail.PerMinute", Trim(Request.Form["perMinute"]), CurrentUser.UserId);
                            MailRepo.SetSetting("Mail.AutoSyncMinutes", Trim(Request.Form["autoSync"]), CurrentUser.UserId);
                            Flash("success", "Mail centre settings saved.");
                            redirectTab = "setup";
                            break;
                        }
                    case "suppress":
                        MailRepo.Suppress(Trim(Request.Form["email"]), "added by admin");
                        Flash("info", "Address added to the suppression list.");
                        redirectTab = "setup";
                        break;
                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex)
            {
                error = SqlHelper.FriendlyError(ex).Message;
                Global.WriteLog("MailCenter", "operation failed", ex);
            }
            Redirect("/Admin/MailCenter.aspx?tab=" + redirectTab +
                (error == null ? "" : "&err=" + HttpUtility.UrlEncode(error)));
        }

        private void BuildBody()
        {
            switch (Tab())
            {
                case "queue": BodyHtml = QueueSection(); break;
                case "inbox": BodyHtml = InboxSection(); break;
                case "templates": BodyHtml = TemplatesSection(); break;
                case "setup": BodyHtml = IsAdmin ? SetupSection() : ComposeSection(); break;
                default: BodyHtml = ComposeSection(); break;
            }
        }

        private string ComposeSection()
        {
            DataTable accounts = MailRepo.Accounts();
            var sb = new StringBuilder();
            sb.Append("<div class='bento'><div class='c-8'><div class='tile'><div class='tile__head'>Compose message</div><div class='tile__body'>");
            sb.Append("<form method='post' action='/Admin/MailCenter.aspx?tab=compose'>").Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='send'/>");
            sb.Append("<div class='form-grid'>");
            sb.Append("<div class='c-6'><label class='field-label' for='mcAccount'>Send from</label><select id='mcAccount' name='accountId' class='form-select form-select-sm'>");
            if (accounts.Rows.Count == 0)
                sb.Append("<option value='0'>No mailbox configured</option>");
            foreach (DataRow a in accounts.Rows)
                sb.Append("<option value='").Append(a["AccountId"]).Append("'>")
                  .Append(E(a["DisplayName"])).Append(" &lt;").Append(E(a["EmailAddress"])).Append("&gt;</option>");
            sb.Append("</select></div>");
            sb.Append("<div class='c-6'><label class='field-label' for='mcAudience'>Audience</label><select id='mcAudience' name='audience' class='form-select form-select-sm'>");
            sb.Append("<option value='students'>All students</option><option value='faculty'>All faculty</option><option value='all'>Everyone</option>");
            sb.Append("<option value='role:Admin'>Administrators</option>");
            DataTable courses = SafeCourses();
            foreach (DataRow c in courses.Rows)
                sb.Append("<option value='course:").Append(E(c["CourseCode"])).Append("'>Course ").Append(E(c["CourseCode"])).Append("</option>");
            sb.Append("</select></div>");
            sb.Append("<div class='c-12'><label class='field-label' for='mcTo'>Specific recipients (optional, comma separated)</label>");
            sb.Append("<input id='mcTo' name='to' class='form-control form-control-sm' placeholder='name@example.com, second@example.com'/></div>");
            sb.Append("<div class='c-12'><label class='field-label' for='mcSubject'>Subject</label>");
            sb.Append("<input id='mcSubject' name='subject' required class='form-control form-control-sm' maxlength='200'/></div>");
            sb.Append("<div class='c-12'><label class='field-label' for='mcBody'>Message</label>");
            sb.Append("<textarea id='mcBody' name='body' required rows='10' class='form-control form-control-sm' placeholder='Hello {{name}},&#10;&#10;...'></textarea>");
            sb.Append("<p class='field-hint'>Merge fields: {{name}}, {{email}}, {{institution}}, {{baseurl}}.</p></div>");
            sb.Append("<div class='c-6'><label class='field-label' for='mcSchedule'>Schedule (optional, local time)</label>");
            sb.Append("<input id='mcSchedule' name='schedule' type='datetime-local' class='form-control form-control-sm'/></div>");
            sb.Append("<div class='c-6'><label class='checkline'><input type='checkbox' name='bulk' value='1' checked/> Bulk campaign (adds unsubscribe link)</label></div>");
            sb.Append("<div class='c-12'><button class='btn btn-sms btn-sm'>Send now or schedule</button></div>");
            sb.Append("</div></form></div></div></div>");
            sb.Append("<div class='c-4'><div class='tile mb-3'><div class='tile__head'>Using a template</div><div class='tile__body'>");
            sb.Append("<p class='small text-muted'>Start from a saved template, then edit it in the composer.</p>");
            foreach (DataRow t in MailRepo.Templates().Rows)
            {
                sb.Append("<form method='post' action='/Admin/MailCenter.aspx?tab=compose' class='mb-2'>").Append(CsrfField());
                sb.Append("<input type='hidden' name='op' value='send'/>");
                sb.Append("<input type='hidden' name='accountId' value='0'/>");
                sb.Append("<input type='hidden' name='audience' value='students'/>");
                sb.Append("<input type='hidden' name='subject' value='").Append(Attr(t["Subject"])).Append("'/>");
                sb.Append("<input type='hidden' name='body' value='").Append(Attr(t["BodyHtml"])).Append("'/>");
                sb.Append("<button class='btn btn-outline-sms btn-sm w-100 text-start'>").Append(E(t["Name"])).Append("</button>");
                sb.Append("</form>");
            }
            sb.Append("<a class='btn btn-link btn-sm' href='?tab=templates'>Manage templates</a>");
            sb.Append("</div></div><div class='tile'><div class='tile__head'>Delivery notes</div><div class='tile__body small text-muted'>");
            sb.Append("<p>Messages are sent in small batches to protect your mailbox reputation. Queue status is on the Queue tab.</p>");
            sb.Append("<p>If SMTP is not configured yet, open Setup and add your Gmail, Outlook or other mailbox with an app password.</p>");
            sb.Append("</div></div></div></div>");
            return sb.ToString();
        }

        private string QueueSection()
        {
            DataTable dt = MailRepo.Queue(100);
            var sb = new StringBuilder("<div class='tile'><div class='tile__head'>Campaign queue</div><div class='tile__body sms-table-wrap table-wrap'>");
            sb.Append("<table class='table table-sm'><thead><tr><th>#</th><th>Subject</th><th>Status</th><th>Progress</th><th>When</th><th></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>Nothing queued yet. Compose your first message.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                int id = Convert.ToInt32(r["QueueId"]);
                string status = Convert.ToString(r["Status"]);
                sb.Append("<tr><td>").Append(id).Append("</td><td>").Append(E(r["Subject"])).Append("</td>");
                sb.Append("<td><span class='badge ").Append(status == "sent" ? "badge-ok" : status == "failed" ? "badge-off" : "badge-warn").Append("'>")
                  .Append(E(status)).Append("</span></td>");
                sb.Append("<td>").Append(E(r["SentCount"])).Append(" / ").Append(E(r["TotalRecipients"]))
                  .Append(Convert.ToInt32(r["FailedCount"]) > 0 ? " (" + E(r["FailedCount"]) + " failed)" : "").Append("</td>");
                sb.Append("<td class='small'>").Append(E(Convert.ToDateTime(r["CreatedAtUtc"]).ToString("yyyy-MM-dd HH:mm"))).Append("</td>");
                sb.Append("<td class='text-end'>");
                if (status != "sent" && status != "cancelled")
                {
                    sb.Append(FormButton("sendnow", id, "Send now", "imbtn")).Append(" ");
                    if (status == "paused") sb.Append(FormButton("resume", id, "Resume", "imbtn"));
                    else sb.Append(FormButton("pause", id, "Pause", "imbtn"));
                    sb.Append(" ").Append(FormButton("cancel", id, "Cancel", "imbtn", "Cancel this campaign?"));
                }
                sb.Append(" <a class='imbtn' href='?tab=queue&amp;view=").Append(id).Append("'>Recipients</a></td></tr>");
            }
            sb.Append("</tbody></table>");
            int view = ParseInt(Request.QueryString["view"]);
            if (view > 0)
            {
                sb.Append("<h2 class='h6 mt-3'>Recipients for campaign #").Append(view).Append("</h2>");
                sb.Append("<table class='table table-sm'><thead><tr><th>Email</th><th>Name</th><th>Status</th><th>Error</th></tr></thead><tbody>");
                foreach (DataRow p in MailRepo.Recipients(view).Rows)
                    sb.Append("<tr><td>").Append(E(p["Email"])).Append("</td><td>").Append(E(p["DisplayName"]))
                      .Append("</td><td>").Append(E(p["Status"])).Append("</td><td class='small text-danger'>").Append(E(p["Error"])).Append("</td></tr>");
                sb.Append("</tbody></table>");
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private string InboxSection()
        {
            string q = Trim(Request.QueryString["q"]);
            DataTable dt = MailRepo.Messages("", q, 100);
            var sb = new StringBuilder();
            sb.Append("<div class='toolbar mb-3'><form method='get' class='toolbar'><input type='hidden' name='tab' value='inbox'/>");
            sb.Append("<input name='q' value='").Append(Attr(q)).Append("' class='form-control form-control-sm toolbar__grow' placeholder='Search mail'/>");
            sb.Append("<button class='btn btn-outline-sms btn-sm'>Search</button></form>");
            sb.Append(FormButton("sync", 0, "Sync now", "btn btn-sms btn-sm ms-2")).Append("</div>");
            sb.Append("<div class='tile'><div class='tile__head'>Inbox</div><div class='tile__body sms-table-wrap table-wrap'>");
            sb.Append("<table class='table table-sm'><thead><tr><th></th><th>From</th><th>Subject</th><th>Received</th><th></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0)
                sb.Append("<tr><td colspan='5' class='msg-empty'>No received mail yet. Add an IMAP account in Setup and use Sync now.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                int id = Convert.ToInt32(r["MessageId"]);
                sb.Append("<tr").Append(Convert.ToBoolean(r["IsRead"]) ? "" : " class='fw-semibold'").Append(">");
                sb.Append("<td>").Append(Convert.ToBoolean(r["IsStarred"]) ? "<span aria-label='Starred'>*</span>" : "").Append("</td>");
                sb.Append("<td>").Append(E(r["FromAddress"])).Append("</td>");
                sb.Append("<td><a href='?tab=inbox&amp;msg=").Append(id).Append("'>").Append(E(r["Subject"])).Append("</a></td>");
                sb.Append("<td class='small'>").Append(E(Convert.ToDateTime(r["ReceivedUtc"]).ToString("yyyy-MM-dd HH:mm"))).Append("</td>");
                sb.Append("<td>").Append(FormButton("mark", id, "Mark read", "imbtn", null, "read", "1")).Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            int msg = ParseInt(Request.QueryString["msg"]);
            DataRow m = msg > 0 ? MailRepo.Message(msg) : null;
            if (m != null)
            {
                MailRepo.MarkMessage(msg, true, null, null);
                sb.Append("<div class='tile mt-3'><div class='tile__head'>").Append(E(m["Subject"])).Append("</div><div class='tile__body'>");
                sb.Append("<p class='small text-muted'>From ").Append(E(m["FromAddress"])).Append(" to ").Append(E(m["ToAddress"])).Append("</p>");
                string html = Convert.ToString(m["BodyHtml"]);
                sb.Append("<div class='mail-body'>").Append(string.IsNullOrEmpty(html)
                    ? "<pre class='mail-text'>" + HttpUtility.HtmlEncode(Convert.ToString(m["BodyText"])) + "</pre>"
                    : Sanitize(html)).Append("</div></div></div>");
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private string TemplatesSection()
        {
            DataTable dt = MailRepo.Templates();
            var sb = new StringBuilder("<div class='bento'><div class='c-7'><div class='tile'><div class='tile__head'>Templates</div><div class='tile__body'>");
            foreach (DataRow r in dt.Rows)
            {
                sb.Append("<div class='mail-template'><div><strong>").Append(E(r["Name"])).Append("</strong><div class='small text-muted'>")
                  .Append(E(r["Subject"])).Append("</div></div>");
                sb.Append(FormButton("deletetemplate", 0, "Delete", "imbtn", "Delete this template?", "templateId", Convert.ToString(r["TemplateId"])));
                sb.Append("</div>");
            }
            if (dt.Rows.Count == 0) sb.Append("<p class='msg-empty'>No templates yet.</p>");
            sb.Append("</div></div></div>");
            sb.Append("<div class='c-5'><div class='tile'><div class='tile__head'>New or edit template</div><div class='tile__body'>");
            sb.Append("<form method='post' action='/Admin/MailCenter.aspx?tab=templates'>").Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='savetemplate'/><input type='hidden' name='templateId' value='0'/>");
            sb.Append("<label class='field-label'>Name</label><input name='name' required class='form-control form-control-sm mb-2' maxlength='80'/>");
            sb.Append("<label class='field-label'>Subject</label><input name='subject' required class='form-control form-control-sm mb-2' maxlength='200'/>");
            sb.Append("<label class='field-label'>Body</label><textarea name='body' required rows='9' class='form-control form-control-sm mb-2'></textarea>");
            sb.Append("<button class='btn btn-sms btn-sm'>Save template</button></form>");
            sb.Append("</div></div></div></div>");
            return sb.ToString();
        }

        private string SetupSection()
        {
            DataTable accounts = MailRepo.Accounts();
            var sb = new StringBuilder("<div class='bento'>");
            sb.Append("<div class='c-7'><div class='tile mb-3'><div class='tile__head'>Mailbox accounts</div><div class='tile__body'>");
            sb.Append("<table class='table table-sm'><thead><tr><th>Label</th><th>Address</th><th>SMTP</th><th>Default</th><th>Last sync</th><th></th></tr></thead><tbody>");
            if (accounts.Rows.Count == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>No accounts yet. Add one below.</td></tr>");
            foreach (DataRow a in accounts.Rows)
            {
                int id = Convert.ToInt32(a["AccountId"]);
                sb.Append("<tr><td>").Append(E(a["Label"])).Append("</td><td>").Append(E(a["EmailAddress"])).Append("</td>");
                sb.Append("<td class='small'>").Append(E(a["SmtpHost"])).Append(":").Append(E(a["SmtpPort"])).Append("</td>");
                sb.Append("<td>").Append(Convert.ToBoolean(a["IsDefault"]) ? "Yes" : "").Append("</td>");
                sb.Append("<td class='small'>").Append(a["LastSyncUtc"] == DBNull.Value ? "Never" : E(Convert.ToDateTime(a["LastSyncUtc"]).ToString("yyyy-MM-dd HH:mm"))).Append("</td>");
                string adminEmail = "";
                try { adminEmail = UsersRepo.FindById(CurrentUser.UserId).Email; } catch (Exception) { adminEmail = ""; }
                sb.Append("<td>").Append(FormButton("testaccount", id, "Test", "imbtn", null, "to", adminEmail)).Append(" ");
                sb.Append(FormButton("sync", id, "Sync", "imbtn")).Append(" ");
                sb.Append(FormButton("deleteaccount", id, "Delete", "imbtn", "Remove this account?")).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div></div>");
            sb.Append("<div class='tile'><div class='tile__head'>Add or update account</div><div class='tile__body'>");
            sb.Append("<form method='post' action='/Admin/MailCenter.aspx?tab=setup' class='form-grid'>").Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='saveaccount'/><input type='hidden' name='id' value='0'/>");
            sb.Append("<div class='c-6'><label class='field-label'>Label</label><input name='label' required class='form-control form-control-sm' placeholder='Admissions desk'/></div>");
            sb.Append("<div class='c-6'><label class='field-label'>Email address</label><input name='email' type='email' required class='form-control form-control-sm'/></div>");
            sb.Append("<div class='c-6'><label class='field-label'>Display name</label><input name='display' class='form-control form-control-sm'/></div>");
            sb.Append("<div class='c-6'><label class='checkline'><input type='checkbox' name='isDefault' value='1'/> Default account</label></div>");
            sb.Append("<div class='c-6'><label class='field-label'>SMTP host</label><input name='smtpHost' class='form-control form-control-sm' placeholder='smtp.gmail.com'/></div>");
            sb.Append("<div class='c-3'><label class='field-label'>Port</label><input name='smtpPort' value='587' class='form-control form-control-sm'/></div>");
            sb.Append("<div class='c-3'><label class='checkline'><input type='checkbox' name='smtpSsl' value='1' checked/> SSL</label></div>");
            sb.Append("<div class='c-6'><label class='field-label'>SMTP user</label><input name='smtpUser' class='form-control form-control-sm' autocomplete='off'/></div>");
            sb.Append("<div class='c-6'><label class='field-label'>SMTP app password</label><input name='smtpPassword' type='password' class='form-control form-control-sm' autocomplete='new-password'/></div>");
            sb.Append("<div class='c-6'><label class='field-label'>IMAP host</label><input name='imapHost' class='form-control form-control-sm' placeholder='imap.gmail.com'/></div>");
            sb.Append("<div class='c-3'><label class='field-label'>Port</label><input name='imapPort' value='993' class='form-control form-control-sm'/></div>");
            sb.Append("<div class='c-3'><label class='checkline'><input type='checkbox' name='imapSsl' value='1' checked/> SSL</label></div>");
            sb.Append("<div class='c-6'><label class='field-label'>IMAP user</label><input name='imapUser' class='form-control form-control-sm' autocomplete='off'/></div>");
            sb.Append("<div class='c-6'><label class='field-label'>IMAP app password</label><input name='imapPassword' type='password' class='form-control form-control-sm' autocomplete='new-password'/></div>");
            sb.Append("<div class='c-3'><label class='field-label'>Daily limit</label><input name='dailyLimit' value='400' class='form-control form-control-sm'/></div>");
            sb.Append("<div class='c-3'><label class='field-label'>Per minute</label><input name='perMinute' value='20' class='form-control form-control-sm'/></div>");
            sb.Append("<div class='c-6'><label class='checkline'><input type='checkbox' name='isActive' value='1' checked/> Active</label></div>");
            sb.Append("<div class='c-12'><button class='btn btn-sms btn-sm'>Save account</button></div>");
            sb.Append("</form></div></div></div>");
            sb.Append("<div class='c-5'><div class='tile mb-3'><div class='tile__head'>Sending rules</div><div class='tile__body'>");
            sb.Append("<form method='post' action='/Admin/MailCenter.aspx?tab=setup'>").Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='savesettings'/>");
            sb.Append("<label class='field-label'>From name</label><input name='fromName' class='form-control form-control-sm mb-2' value='").Append(Attr(MailRepo.GetSetting("Mail.FromName", "Campus Office"))).Append("'/>");
            sb.Append("<label class='field-label'>Daily limit</label><input name='dailyLimit' class='form-control form-control-sm mb-2' value='").Append(Attr(MailRepo.GetSetting("Mail.DailyLimit", "400"))).Append("'/>");
            sb.Append("<label class='field-label'>Per minute</label><input name='perMinute' class='form-control form-control-sm mb-2' value='").Append(Attr(MailRepo.GetSetting("Mail.PerMinute", "20"))).Append("'/>");
            sb.Append("<label class='field-label'>Receive every (minutes)</label><input name='autoSync' class='form-control form-control-sm mb-2' value='").Append(Attr(MailRepo.GetSetting("Mail.AutoSyncMinutes", "2"))).Append("'/>");
            sb.Append("<button class='btn btn-sms btn-sm'>Save rules</button></form></div></div>");
            sb.Append("<div class='tile'><div class='tile__head'>Suppression list</div><div class='tile__body small'>");
            sb.Append("<form method='post' action='/Admin/MailCenter.aspx?tab=setup' class='toolbar mb-2'>").Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='suppress'/><input name='email' type='email' required class='form-control form-control-sm toolbar__grow' placeholder='address@example.com'/>");
            sb.Append("<button class='btn btn-outline-sms btn-sm'>Suppress</button></form>");
            foreach (DataRow s in MailRepo.Suppressions().Rows)
                sb.Append("<div class='mono small'>").Append(E(s["Email"])).Append("</div>");
            sb.Append("</div></div></div></div>");
            sb.Append("<div class='c-12'><div class='tile'><div class='tile__head'>How to connect a free mailbox</div><div class='tile__body small text-muted'>");
            sb.Append("<p>Gmail: enable 2-step verification, create an App password (Google Account > Security > App passwords), then use smtp.gmail.com:587 and imap.gmail.com:993 with that 16-character password.</p>");
            sb.Append("<p>Outlook: smtp.office365.com:587 and outlook.office365.com:993. Yahoo: smtp.mail.yahoo.com:465 (SSL) and imap.mail.yahoo.com:993. Zoho: smtp.zoho.in:587 and imap.zoho.in:993.</p>");
            sb.Append("<p>Passwords are encrypted with Windows DPAPI before they touch the database.</p>");
            sb.Append("</div></div></div></div>");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- helpers

        private DataTable SafeCourses()
        {
            try { return SqlHelper.FillDataTable("SELECT CourseCode FROM dbo.Courses ORDER BY CourseCode"); }
            catch (Exception) { return new DataTable(); }
        }

        private static DateTime? ParseLocal(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            DateTime dt;
            if (!DateTime.TryParse(raw, out dt)) return null;
            return DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();
        }

        private static IEnumerable<KeyValuePair<string, string>> ParsePeople(string raw)
        {
            var list = new List<KeyValuePair<string, string>>();
            foreach (string part in (raw ?? "").Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string email = part.Trim();
                int lt = email.LastIndexOf('<');
                string name = null;
                if (lt >= 0 && email.EndsWith(">"))
                {
                    name = email.Substring(0, lt).Trim().Trim('"');
                    email = email.Substring(lt + 1).TrimEnd('>').Trim();
                }
                list.Add(new KeyValuePair<string, string>(email, name));
            }
            return list;
        }

        private static string ComposeHtml(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string raw = text.Trim();
            if (raw.IndexOf("<", StringComparison.Ordinal) >= 0) return Sanitize(raw);
            string safe = HttpUtility.HtmlEncode(raw);
            return "<p>" + safe.Replace("\r\n", "\n").Replace("\n\n", "</p><p>").Replace("\n", "<br/>") + "</p>";
        }

        private static string Sanitize(string html)
        {
            string s = RegexSafe(html ?? "");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"(?is)<(script|style|iframe|object|embed|form|input|button|link|meta)[^>]*>.*?</\1>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"(?is)<(script|style|iframe|object|embed|form|input|button|link|meta)[^>]*/?>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"(?i)\son[a-z]+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"(?i)(href|src)\s*=\s*(""|')?\s*javascript:[^""'\s>]*", "$1=\"#\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return s;
        }

        private static string RegexSafe(string value) { return value; }

        private string FormButton(string op, int id, string label, string css, string confirm = null, string extraName = null, string extraValue = null)
        {
            var sb = new StringBuilder("<form method='post' action='/Admin/MailCenter.aspx' style='display:inline'>");
            sb.Append(CsrfField()).Append("<input type='hidden' name='op' value='").Append(Attr(op)).Append("'/>");
            if (id > 0) sb.Append("<input type='hidden' name='id' value='").Append(id).Append("'/>");
            if (!string.IsNullOrEmpty(extraName))
                sb.Append("<input type='hidden' name='").Append(Attr(extraName)).Append("' value='").Append(Attr(extraValue)).Append("'/>");
            sb.Append("<button class='").Append(Attr(css)).Append("' type='submit'")
              .Append(string.IsNullOrEmpty(confirm) ? "" : " data-confirm='" + Attr(confirm) + "'").Append(">")
              .Append(E(label)).Append("</button></form>");
            return sb.ToString();
        }

        private void RequireAdmin()
        {
            if (!IsAdmin) throw new Exception("Only an administrator can change mail accounts or institution settings.");
        }

        private static string Trim(string s) { return (s ?? "").Trim(); }
        private static int ParseInt(string s, int fallback = 0) { int n; return int.TryParse(s, out n) ? n : fallback; }
        private static bool FormBool(string name) { return HttpContext.Current.Request.Form[name] == "1"; }
        private static string Cap(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }
    }
}



