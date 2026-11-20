using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Integrations;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Admin
{
    /// <summary>Integration status, AppSettings editor, chatbot FAQ library, email self-test.</summary>
    public partial class Settings : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, ConfigTable, AppSettingRows, FaqTable, AdminEmail, SystemTable, IntegrationsHtml;

        private static readonly string[] KeysToEdit =
            { "MinPasswordLength", "MinAttendancePercent", "OtpExpiryMinutes", "OtpMaxAttempts", "OtpResendCooldownSeconds", "LockoutAfterFailures", "LockoutMinutes", "InstitutionTimezone" };

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            BuildConfig();
            BuildIntegrations();
            BuildAppSettings();
            BuildFaq();
            BuildSystem();
            AdminEmail = (CurrentUser.RoleName == "Admin" ? UsersRepo.FindById(CurrentUser.UserId).Email : "");
        }

        private void HandlePost()
        {
            string error = null;
            try
            {
                switch (Request.Form["op"])
                {
                    case "savesettings":
                        foreach (string key in KeysToEdit)
                        {
                            string val = (Request.Form["set_" + key] ?? "").Trim();
                            if (val.Length == 0) continue;
                            if (key == "InstitutionTimezone")
                            {
                                val = PrefsRepo.NormalizeTimezone(val);
                                if (val == null) throw new Exception("Enter a valid IANA timezone, for example Asia/Kolkata or UTC.");
                            }
                            SqlHelper.ExecuteNonQuery(
                                "MERGE INTO dbo.AppSettings AS t USING (SELECT @Key AS Key1, @Value AS Value1, @By AS By1) s " +
                                "ON t.SettingKey = s.Key1 " +
                                "WHEN MATCHED THEN UPDATE SET SettingValue = s.Value1, ModifiedBy = s.By1, ModifiedAtUtc = SYSUTCDATETIME() " +
                                "WHEN NOT MATCHED THEN INSERT (SettingKey, SettingValue, ModifiedBy) VALUES (s.Key1, s.Value1, s.By1);",
                                p => { p.AddWithValue("@Key", key); p.AddWithValue("@Value", val); p.AddWithValue("@By", CurrentUser.UserId); });
                        }
                        AuditService.Log("SettingsSaved", "AppSettings", null);
                        Flash("success", "Settings saved.");
                        break;

                    case "saveintegration":
                        {
                            string provider = (Request.Form["provider"] ?? "demo").Trim().ToLowerInvariant();
                            if (provider != "demo" && provider != "razorpay") throw new Exception("Choose a valid payment provider.");
                            MailRepo.SetSetting("Payments.Provider", provider, CurrentUser.UserId);
                            MailRepo.SetSetting("Payments.DemoEnabled", Request.Form["demoEnabled"] == "1" ? "1" : "0", CurrentUser.UserId);
                            string policy = (Request.Form["registrationPolicy"] ?? "pending").Trim().ToLowerInvariant();
                            if (policy != "auto" && policy != "pending" && policy != "existing") throw new Exception("Choose a valid registration policy.");
                            MailRepo.SetSetting("Registration.Policy", policy, CurrentUser.UserId);
                            MailRepo.SetSetting("Mail.FromName", (Request.Form["fromName"] ?? "").Trim(), CurrentUser.UserId);
                            AuditService.Log("IntegrationsSaved", "InstitutionSettings", null, provider);
                            Flash("success", "Integration settings saved.");
                            break;
                        }

                    case "savecredentials":
                        SaveCredentials();
                        break;

                    case "testemail":
                        {
                            var mail = new SmtpMailService();
                            if (!mail.IsConfigured) { Flash("warning", "Email delivery is not configured (Email:Enabled = false)."); break; }
                            string to = (Request.Form["email"] ?? "").Trim();
                            bool sent = mail.Send(to, "Student Management System self-test",
                                "<p>This is a test message from your SMS installation. SMTP works.</p>");
                            Flash(sent ? "success" : "danger", sent ? "Test email delivered to " + to + "." : "SMTP send failed - check the log and credentials.");
                            AuditService.Log("EmailSelfTest", "Users", CurrentUser.UserId, to);
                            break;
                        }

                    case "testnotification":
                        {
                            string audience = (Request.Form["audience"] ?? "user:" + CurrentUser.UserId).Trim();
                            int nid = CommsRepo.CreateNotification("Test notification from Settings",
                                "If you can see this in the ticker, in-app delivery works. This is a test and safe to dismiss.",
                                "system", "/Admin/Notifications.aspx", CurrentUser.UserId, audience, null,
                                "testnotif:" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true);
                            Flash("success", "Test notification #" + nid + " queued for " + audience + ".");
                            AuditService.Log("NotificationSelfTest", "Notifications", nid, audience);
                            break;
                        }

                    case "upsertfaq":
                        CommsRepo.UpsertFaq(int.Parse(Request.Form["faqId"] ?? "0"),
                            (Request.Form["question"] ?? "").Trim(),
                            (Request.Form["answer"] ?? "").Trim(),
                            (Request.Form["keywords"] ?? "").Trim(),
                            (Request.Form["roleScope"] ?? "all").Trim(), 0);
                        Flash("success", "FAQ entry saved.");
                        break;

                    case "togglefaq":
                        CommsRepo.ToggleFaq(int.Parse(Request.Form["faqId"]));
                        Flash("info", "FAQ visibility toggled.");
                        break;

                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }
            Redirect("/Admin/Settings.aspx" + (error != null ? "?err=" + HttpUtility.UrlEncode(error) : ""));
        }

        private void BuildConfig()
        {
            var sb = new StringBuilder();
            AddConfigRow(sb, "Email sending", AppConfig.EmailEnabled, "Email:Enabled");
            AddConfigRow(sb, "SMS delivery", AppConfig.Get("Sms:ApiKey", "") != "", "Sms:ApiKey");
            AddConfigRow(sb, "Google sign-in", AppConfig.GoogleAuthEnabled, "GoogleAuth:Enabled");
            AddConfigRow(sb, "Razorpay payments", AppConfig.RazorpayEnabled, "Razorpay:Enabled");
            AddConfigRow(sb, "Web push", AppConfig.PushEnabled, "Push:Enabled");
            AddConfigRow(sb, "LLM chatbot", AppConfig.Get("Chatbot:GroqApiKey", "") != "", "Chatbot:GroqApiKey");
            sb.Append("<div class='small text-muted mt-2'>Secrets are never rendered. Values below come from the encrypted credential vault; Web.config is only a fallback.</div>");
            ConfigTable = sb.ToString();
        }

        private void BuildIntegrations()
        {
            string provider = MailRepo.GetSetting("Payments.Provider", "demo");
            string demo = MailRepo.GetSetting("Payments.DemoEnabled", "0");
            string policy = MailRepo.GetSetting("Registration.Policy", "pending");
            var sb = new StringBuilder();
            sb.Append("<form method='post' action='/Admin/Settings.aspx' class='form-grid'>").Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='saveintegration'/>");
            sb.Append("<div class='c-6'><label class='field-label'>Payment provider</label><select name='provider' class='form-select form-select-sm'>");
            sb.Append("<option value='demo'").Append(provider == "demo" ? " selected" : "").Append(">Demo (no money moves)</option>");
            sb.Append("<option value='razorpay'").Append(provider == "razorpay" ? " selected" : "").Append(">Razorpay</option>");
            sb.Append("</select></div>");
            sb.Append("<div class='c-6'><label class='field-label'>Registration policy</label><select name='registrationPolicy' class='form-select form-select-sm'>");
            sb.Append("<option value='auto'").Append(policy == "auto" ? " selected" : "").Append(">Auto-approve verified students</option>");
            sb.Append("<option value='pending'").Append(policy == "pending" ? " selected" : "").Append(">Pending administrator approval (default)</option>");
            sb.Append("<option value='existing'").Append(policy == "existing" ? " selected" : "").Append(">Existing accounts only</option>");
            sb.Append("</select></div>");
            sb.Append("<div class='c-6'><label class='checkline'><input type='checkbox' name='demoEnabled' value='1'")
              .Append(demo == "1" ? " checked" : "").Append("/> DEMO dev switch: allow demo payment (no money moves)</label></div>");
            sb.Append("<div class='c-6'><label class='field-label'>Mail sender name</label>");
            sb.Append("<input name='fromName' class='form-control form-control-sm' value='").Append(Attr(MailRepo.GetSetting("Mail.FromName", "Campus Office"))).Append("'/></div>");
            sb.Append("<div class='c-12'><button class='btn btn-sms btn-sm'>Save integration settings</button></div>");
            sb.Append("</form>");
            sb.Append("<hr/>");
            sb.Append("<div class='small'><strong>Google sign-in:</strong> ").Append(AppConfig.GoogleAuthEnabled ? "enabled" : "not enabled").Append(". ");
            sb.Append("Set GoogleAuth:Enabled, ClientId and ClientSecret in Web.config and add this redirect URI: <code>")
              .Append(E(AppConfig.BaseUrl.TrimEnd('/'))).Append("/Handlers/GoogleOAuth.ashx</code>. PKCE and state are used.</div>");
            sb.Append("<div class='small mt-2'><strong>Razorpay webhook:</strong> <code>")
              .Append(E(AppConfig.BaseUrl.TrimEnd('/'))).Append("/Handlers/RazorpayWebhook.ashx</code>. ");
            sb.Append("When webhooks are unavailable the payment status is confirmed by the signed checkout callback instead.</div>");
            sb.Append("<div class='small mt-2'><strong>Mail centre:</strong> <a href='/Admin/MailCenter.aspx?tab=setup'>connect a mailbox</a> - SMTP and IMAP credentials are stored encrypted.</div>");
            sb.Append(BuildCredentials());
            IntegrationsHtml = sb.ToString();
        }

        private string BuildCredentials()
        {
            string emailState = SecretStore.Has("Email:Password") ? "configured" : "not set";
            string googleState = SecretStore.Has("GoogleAuth:ClientSecret") ? "configured" : "not set";
            string razorState = SecretStore.Has("Razorpay:KeySecret") ? "configured" : "not set";
            string webhookState = SecretStore.Has("Razorpay:WebhookSecret") ? "configured" : "not set";
            var sb = new StringBuilder();
            sb.Append("<hr/><h3 class='h6 mt-3'>Secure credential vault</h3>");
            sb.Append("<p class='small text-muted'>Secrets are DPAPI-encrypted in the database. Leave a secret field blank to keep the current value. Nothing is ever rendered back.</p>");
            sb.Append("<form method='post' action='/Admin/Settings.aspx' class='form-grid'>").Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='savecredentials'/>");

            sb.Append("<div class='c-12'><label class='field-label'>Public base URL</label>");
            sb.Append("<input name='baseUrl' class='form-control form-control-sm' value='").Append(Attr(AppConfig.BaseUrl)).Append("' placeholder='http://localhost:8080/'/>");
            sb.Append("<div class='form-text'>Used in email links, Google OAuth and Razorpay webhooks. Include the scheme and a trailing slash.</div></div>");

            sb.Append("<div class='c-6'><label class='field-label'>Gmail address</label>");
            sb.Append("<input name='gmailAddress' type='email' class='form-control form-control-sm' value='").Append(Attr(AppConfig.Get("Email:User", ""))).Append("' autocomplete='off'/></div>");
            sb.Append("<div class='c-6'><label class='field-label'>Gmail app password <span class='badge badge-").Append(SecretStore.Has("Email:Password") ? "ok'>" + emailState : "off'>" + emailState).Append("</span></label>");
            sb.Append("<input name='gmailPassword' type='password' class='form-control form-control-sm' autocomplete='new-password' placeholder='leave blank to keep current'/></div>");
            sb.Append("<div class='c-6'><label class='checkline'><input type='checkbox' name='enableEmail' value='1'").Append(AppConfig.EmailEnabled ? " checked" : "").Append("/> Enable SMTP email</label></div>");
            sb.Append("<div class='c-6'><label class='field-label'>Sender name</label><input name='fromName' class='form-control form-control-sm' value='").Append(Attr(AppConfig.Get("Email:FromName", "Student Management System"))).Append("'/></div>");

            sb.Append("<div class='c-6'><label class='field-label'>Google OAuth client id <span class='badge badge-").Append(SecretStore.Has("GoogleAuth:ClientId") ? "ok'>" + googleState : "off'>" + googleState).Append("</span></label>");
            sb.Append("<input name='googleClientId' class='form-control form-control-sm' autocomplete='off'/></div>");
            sb.Append("<div class='c-6'><label class='field-label'>Google OAuth client secret</label><input name='googleClientSecret' type='password' class='form-control form-control-sm' autocomplete='new-password' placeholder='leave blank to keep current'/></div>");
            sb.Append("<div class='c-6'><label class='checkline'><input type='checkbox' name='enableGoogle' value='1'").Append(AppConfig.GoogleAuthEnabled ? " checked" : "").Append("/> Enable Google sign-in</label></div>");

            sb.Append("<div class='c-6'><label class='field-label'>Razorpay TEST key id <span class='badge badge-").Append(SecretStore.Has("Razorpay:KeyId") ? "ok'>" + razorState : "off'>" + razorState).Append("</span></label>");
            sb.Append("<input name='razorpayKeyId' class='form-control form-control-sm' autocomplete='off'/></div>");
            sb.Append("<div class='c-6'><label class='field-label'>Razorpay TEST key secret</label><input name='razorpayKeySecret' type='password' class='form-control form-control-sm' autocomplete='new-password' placeholder='leave blank to keep current'/></div>");
            sb.Append("<div class='c-6'><label class='field-label'>Razorpay webhook secret <span class='badge badge-").Append(SecretStore.Has("Razorpay:WebhookSecret") ? "ok'>" + webhookState : "off'>" + webhookState).Append("</span></label>");
            sb.Append("<input name='webhookSecret' type='password' class='form-control form-control-sm' autocomplete='new-password' placeholder='leave blank to keep or auto-generate'/></div>");
            sb.Append("<div class='c-6'><label class='checkline'><input type='checkbox' name='enableRazorpay' value='1'").Append(AppConfig.RazorpayEnabled ? " checked" : "").Append("/> Enable Razorpay payments</label></div>");

            sb.Append("<div class='c-12'><button class='btn btn-sms btn-sm'>Save encrypted credentials</button> ");
            sb.Append("<span class='small text-muted ms-2'>Demo payment stays off by default; enable it only under the DEMO dev switch below.</span></div>");
            sb.Append("</form>");
            sb.Append("<div class='small mt-3'><strong>Webhook URL</strong>: <code>").Append(E(AppConfig.BaseUrl.TrimEnd('/'))).Append("/Handlers/RazorpayWebhook.ashx</code>");
            sb.Append(" &middot; <strong>Google redirect</strong>: <code>").Append(E(AppConfig.BaseUrl.TrimEnd('/'))).Append("/Account/GoogleCallback</code></div>");
            sb.Append("<div class='small mt-2'>Need a public webhook without a cloud account? Run <code>tools\\start-tunnel.cmd</code> and paste the printed HTTPS URL into the base URL.</div>");
            return sb.ToString();
        }

        private void SaveCredentials()
        {
            string baseUrl = (Request.Form["baseUrl"] ?? "").Trim();
            if (baseUrl.Length > 0)
            {
                Uri parsed;
                if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out parsed) ||
                    (parsed.Scheme != "http" && parsed.Scheme != "https"))
                    throw new Exception("Enter a valid http or https base URL.");
                if (!baseUrl.EndsWith("/")) baseUrl += "/";
                SecretStore.Set("App:BaseUrl", baseUrl, false, CurrentUser.UserId, "Public base URL");
            }

            string address = (Request.Form["gmailAddress"] ?? "").Trim();
            string password = Request.Form["gmailPassword"] ?? "";
            string fromName = (Request.Form["fromName"] ?? "").Trim();
            if (fromName.Length > 0)
            {
                SecretStore.Set("Email:FromName", fromName, false, CurrentUser.UserId, "Sender display name");
                MailRepo.SetSetting("Mail.FromName", fromName, CurrentUser.UserId);
            }
            if (address.Length > 0)
            {
                SecretStore.Set("Email:FromAddress", address, false, CurrentUser.UserId, "From address");
                SecretStore.Set("Email:User", address, true, CurrentUser.UserId, "SMTP username");
                SecretStore.Set("Email:SmtpHost", "smtp.gmail.com", false, CurrentUser.UserId, "SMTP host");
                SecretStore.Set("Email:SmtpPort", "587", false, CurrentUser.UserId, "SMTP port");
                SecretStore.Set("Email:EnableSsl", "true", false, CurrentUser.UserId, "SMTP STARTTLS");
            }
            if (password.Length > 0)
                SecretStore.Set("Email:Password", password, true, CurrentUser.UserId, "Gmail app password");
            bool emailOn = Request.Form["enableEmail"] == "1" && SecretStore.Has("Email:Password") &&
                SecretStore.Has("Email:FromAddress");
            SecretStore.Set("Email:Enabled", emailOn ? "true" : "false", false, CurrentUser.UserId, "SMTP enabled");

            if (address.Length > 0 && password.Length > 0)
            {
                int accountId = SqlHelper.ExecuteScalarInt(
                    "SELECT TOP 1 AccountId FROM dbo.MailAccounts WHERE EmailAddress=@E",
                    p => p.AddWithValue("@E", address));
                MailRepo.SaveAccount(accountId, "Gmail", address, fromName.Length > 0 ? fromName : "Student Management System",
                    "smtp.gmail.com", 587, true, address, password,
                    "imap.gmail.com", 993, true, address, password, 400, 20, true, true);
            }

            string googleId = (Request.Form["googleClientId"] ?? "").Trim();
            string googleSecret = Request.Form["googleClientSecret"] ?? "";
            if (googleId.Length > 0) SecretStore.Set("GoogleAuth:ClientId", googleId, true, CurrentUser.UserId, "Google OAuth client id");
            if (googleSecret.Length > 0) SecretStore.Set("GoogleAuth:ClientSecret", googleSecret, true, CurrentUser.UserId, "Google OAuth client secret");
            bool googleOn = Request.Form["enableGoogle"] == "1" && SecretStore.Has("GoogleAuth:ClientId") &&
                SecretStore.Has("GoogleAuth:ClientSecret");
            SecretStore.Set("GoogleAuth:Enabled", googleOn ? "true" : "false", false, CurrentUser.UserId, "Google sign-in enabled");

            string razorId = (Request.Form["razorpayKeyId"] ?? "").Trim();
            string razorSecret = Request.Form["razorpayKeySecret"] ?? "";
            string webhook = Request.Form["webhookSecret"] ?? "";
            if (razorId.Length > 0) SecretStore.Set("Razorpay:KeyId", razorId, true, CurrentUser.UserId, "Razorpay key id");
            if (razorSecret.Length > 0) SecretStore.Set("Razorpay:KeySecret", razorSecret, true, CurrentUser.UserId, "Razorpay key secret");
            bool razorOn = Request.Form["enableRazorpay"] == "1" && SecretStore.Has("Razorpay:KeyId") &&
                SecretStore.Has("Razorpay:KeySecret");
            if (razorOn && webhook.Length == 0 && !SecretStore.Has("Razorpay:WebhookSecret"))
                webhook = GenerateWebhookSecret();
            if (webhook.Length > 0) SecretStore.Set("Razorpay:WebhookSecret", webhook, true, CurrentUser.UserId, "Razorpay webhook secret");
            SecretStore.Set("Razorpay:Enabled", razorOn ? "true" : "false", false, CurrentUser.UserId, "Razorpay enabled");
            if (razorOn) MailRepo.SetSetting("Payments.Provider", "razorpay", CurrentUser.UserId);
            MailRepo.SetSetting("Payments.DemoEnabled", "0", CurrentUser.UserId);

            SecretStore.Invalidate(null);
            AuditService.Log("CredentialsSaved", "AppSecrets", null, "encrypted integration settings updated");
            Flash(emailOn && googleOn && razorOn ? "success" : "info",
                "Encrypted credentials saved. " + (razorOn ? "Razorpay webhook secret is stored; set the same value in the Razorpay dashboard. " : "") +
                "Use the test buttons above to verify delivery.");
        }

        private static string GenerateWebhookSecret()
        {
            var data = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(data);
            return Convert.ToBase64String(data).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        private void AddConfigRow(StringBuilder sb, string label, bool ok, string key)
        {
            sb.Append("<div class='d-flex justify-content-between border-bottom py-2'><span>" + E(label) +
                "<span class='small text-muted ms-2'>" + key + "</span></span>");
            sb.Append("<span class='badge " + (ok ? "badge-ok" : "badge-off") + "'>" + (ok ? "configured" : "off") + "</span></div>");
        }

        private void BuildAppSettings()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT SettingKey, SettingValue, Description FROM dbo.AppSettings ORDER BY SettingKey");
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm settings-appsettings'><thead><tr><th>Key</th><th>Value</th><th>Description</th></tr></thead><tbody>");
            foreach (DataRow r in dt.Rows)
            {
                string key = r["SettingKey"] as string ?? "";
                string val = r["SettingValue"] as string ?? "";
                sb.Append("<tr><td><b>" + E(key) + "</b></td>");
                if (Array.IndexOf(KeysToEdit, key) >= 0)
                    sb.Append("<td><input class='form-control form-control-sm' name='set_" + E(key) + "' value='" + Attr(val) + "'/>");
                else
                    sb.Append("<td>" + E(val));
                sb.Append("</td><td class='small'>" + E(r["Description"]) + "</td></tr>");
            }
            sb.Append("</tbody></table>");
            AppSettingRows = sb.ToString();
        }

        /// <summary>
        /// Read-only operational facts: version, database size and row counts, the
        /// configured session timeout, backup policy and the audit-log entry point.
        /// Nothing here writes to the database; administrative actions stay on their
        /// own pages behind the same role gate.
        /// </summary>
        private void BuildSystem()
        {
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm mb-0'><tbody>");

            string version = "1.0.0";
            try
            {
                var asm = typeof(Settings).Assembly;
                version = asm.GetName().Version.ToString();
            }
            catch (Exception) { }

            SysRow(sb, "Application", "Student Management System (ASP.NET Web Forms, .NET " +
                SafeGet(() => Environment.Version.ToString()) + ")");
            SysRow(sb, "Assembly version", E(version));

            try
            {
                DataTable t = SqlHelper.FillDataTable(
                    "SELECT (SELECT COUNT(*) FROM dbo.Users) AS Users, " +
                    "(SELECT COUNT(*) FROM dbo.Students) AS Students, " +
                    "(SELECT COUNT(*) FROM dbo.Notifications) AS Notifications, " +
                    "(SELECT COUNT(*) FROM dbo.Complaints WHERE Status IN ('open','in_progress')) AS OpenComplaints");
                if (t.Rows.Count > 0)
                {
                    DataRow r = t.Rows[0];
                    SysRow(sb, "Accounts / students", E(r["Users"]) + " users, " + E(r["Students"]) + " student profiles");
                    SysRow(sb, "Notifications / open tickets", E(r["Notifications"]) + " sent, " + E(r["OpenComplaints"]) + " open");
                }
            }
            catch (Exception ex) { SysRow(sb, "Row counts", E(SqlHelper.FriendlyError(ex).Message)); }

            try
            {
                object size = SqlHelper.ExecuteScalar(
                    "SELECT CAST(SUM(size) * 8.0 / 1024 AS DECIMAL(10,1)) FROM sys.database_files WHERE type = 0");
                SysRow(sb, "Database size", size == null || size == DBNull.Value ? "unknown" : size + " MB");
            }
            catch (Exception) { SysRow(sb, "Database size", "unavailable"); }

            try
            {
                var section = System.Configuration.ConfigurationManager.GetSection("system.web/authentication") as System.Web.Configuration.AuthenticationSection;
                int timeout = 60;
                if (section != null && section.Forms != null) timeout = (int)section.Forms.Timeout.TotalMinutes;
                SysRow(sb, "Session timeout", timeout + " minutes (Web.config, sliding)");
            }
            catch (Exception) { SysRow(sb, "Session timeout", "configured in Web.config"); }

            SysRow(sb, "Backups", "Run with SQL Server tooling (Database/07_BackupRestore.sql). The app records no automated backup status.");
            sb.Append("<tr><td><b>Audit log</b></td><td><a href='/Admin/Audit.aspx'>Open the audit log</a></td></tr>");
            sb.Append("<tr><td><b>Academic configuration</b></td><td><a href='/Admin/Sessions.aspx'>Sessions</a> &middot; <a href='/Admin/Academics.aspx'>Courses and subjects</a></td></tr>");
            sb.Append("<tr><td><b>Notification defaults</b></td><td>In-app delivery is always on; users choose categories on their own settings page. Email and push stay off until configured.</td></tr>");
            sb.Append("</tbody></table>");
            SystemTable = sb.ToString();
        }

        private static string SafeGet(Func<string> f)
        {
            try { return f(); } catch (Exception) { return "4.8"; }
        }

        private static void SysRow(StringBuilder sb, string label, string value)
        {
            sb.Append("<tr><td style='width:220px'><b>").Append(HttpUtility.HtmlEncode(label)).Append("</b></td><td>").Append(value).Append("</td></tr>");
        }

        private void BuildFaq()
        {
            DataTable dt = CommsRepo.FaqForAdmin();
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Question</th><th>Scope</th><th>Status</th><th></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='4' class='msg-empty'>No FAQ entry. Chatbot will tell users so.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                bool isActive = Convert.ToBoolean(r["IsActive"]);
                sb.Append("<tr><td><b>" + E(r["Question"]) + "</b><div class='small text-muted'>" + Snippet(r["Answer"] as string, 100) + "</div></td>");
                sb.Append("<td>" + E(r["RoleScope"]) + "</td>");
                sb.Append("<td><span class='badge " + (isActive ? "badge-ok" : "badge-off") + "'>" + (isActive ? "on" : "off") + "</span></td>");
                sb.Append("<td>" + ImbForm("togglefaq", new[] { new[] { "faqId", E(r["FaqId"]) } }, isActive ? "Disable" : "Enable") + "</td></tr>");
            }
            sb.Append("</tbody></table>");
            FaqTable = sb.ToString();
        }

        private string ImbForm(string op, string[][] fields, string label)
        {
            var sb = new StringBuilder();
            sb.Append("<form method='post' action='/Admin/Settings.aspx' style='display:inline'>");
            sb.Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='").Append(Attr(op)).Append("'/>");
            foreach (string[] f in fields)
                sb.Append("<input type='hidden' name='").Append(f[0]).Append("' value='").Append(f[1]).Append("'/>");
            sb.Append("<button class='imbtn'>").Append(label).Append("</button></form>");
            return sb.ToString();
        }

        protected static string Snippet(string text, int len)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= len ? System.Web.HttpUtility.HtmlEncode(text)
                : System.Web.HttpUtility.HtmlEncode(text.Substring(0, len - 3)) + "...";
        }
    }
}



