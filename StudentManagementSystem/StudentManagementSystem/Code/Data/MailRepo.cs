using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace StudentManagementSystem.Data
{
    /// <summary>
    /// Free in-app mail centre. Stores account settings (password encrypted with
    /// DPAPI, machine scope), templates, a throttled bulk queue and a local cache
    /// of received messages. SMTP uses the BCL only; no paid service is required.
    /// </summary>
    public static class MailRepo
    {
        // ---------------------------------------------------------------- accounts

        public static DataTable Accounts()
        {
            return SqlHelper.FillDataTable(
                "SELECT AccountId, Label, EmailAddress, DisplayName, SmtpHost, SmtpPort, SmtpSsl, SmtpUser, " +
                "ImapHost, ImapPort, ImapSsl, ImapUser, DailyLimit, PerMinute, IsDefault, IsActive, LastSyncUtc, LastError " +
                "FROM dbo.MailAccounts ORDER BY IsDefault DESC, Label");
        }

        public static DataRow Account(int id)
        {
            DataTable dt = SqlHelper.FillDataTable("SELECT * FROM dbo.MailAccounts WHERE AccountId = @Id",
                p => p.AddWithValue("@Id", id));
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        public static DataRow DefaultAccount()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP 1 * FROM dbo.MailAccounts WHERE IsActive = 1 ORDER BY IsDefault DESC, AccountId");
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        public static int SaveAccount(int id, string label, string email, string display, string smtpHost, int smtpPort,
            bool smtpSsl, string smtpUser, string smtpPassword, string imapHost, int imapPort, bool imapSsl,
            string imapUser, string imapPassword, int dailyLimit, int perMinute, bool isDefault, bool isActive)
        {
            string smtpEnc = string.IsNullOrEmpty(smtpPassword) ? null : Protect(smtpPassword);
            string imapEnc = string.IsNullOrEmpty(imapPassword) ? null : Protect(imapPassword);
            if (id > 0)
            {
                SqlHelper.ExecuteNonQuery(
                    "UPDATE dbo.MailAccounts SET Label=@Label, EmailAddress=@Email, DisplayName=@Display, " +
                    "SmtpHost=@SH, SmtpPort=@SP, SmtpSsl=@SS, SmtpUser=@SU, ImapHost=@IH, ImapPort=@IP, ImapSsl=@IS, " +
                    "ImapUser=@IU, DailyLimit=@DL, PerMinute=@PM, IsDefault=@Def, IsActive=@Active, " +
                    "SmtpPasswordEnc=COALESCE(@SPwd, SmtpPasswordEnc), ImapPasswordEnc=COALESCE(@IPv, ImapPasswordEnc) " +
                    "WHERE AccountId=@Id",
                    p =>
                    {
                        AddAccountParams(p, label, email, display, smtpHost, smtpPort, smtpSsl, smtpUser, imapHost, imapPort, imapSsl, imapUser, dailyLimit, perMinute, isDefault, isActive);
                        p.AddWithValue("@SPwd", (object)smtpEnc ?? DBNull.Value);
                        p.AddWithValue("@IPv", (object)imapEnc ?? DBNull.Value);
                        p.AddWithValue("@Id", id);
                    });
            }
            else
            {
                id = SqlHelper.ExecuteScalarInt(
                    "INSERT INTO dbo.MailAccounts (Label, EmailAddress, DisplayName, SmtpHost, SmtpPort, SmtpSsl, SmtpUser, " +
                    "SmtpPasswordEnc, ImapHost, ImapPort, ImapSsl, ImapUser, ImapPasswordEnc, DailyLimit, PerMinute, IsDefault, IsActive) " +
                    "OUTPUT INSERTED.AccountId VALUES (@Label,@Email,@Display,@SH,@SP,@SS,@SU,@SPwd,@IH,@IP,@IS,@IU,@IPv,@DL,@PM,@Def,@Active)",
                    p =>
                    {
                        AddAccountParams(p, label, email, display, smtpHost, smtpPort, smtpSsl, smtpUser, imapHost, imapPort, imapSsl, imapUser, dailyLimit, perMinute, isDefault, isActive);
                        p.AddWithValue("@SPwd", (object)smtpEnc ?? DBNull.Value);
                        p.AddWithValue("@IPv", (object)imapEnc ?? DBNull.Value);
                    });
            }
            if (isDefault)
                SqlHelper.ExecuteNonQuery("UPDATE dbo.MailAccounts SET IsDefault = CASE WHEN AccountId=@Id THEN 1 ELSE 0 END",
                    p => p.AddWithValue("@Id", id));
            return id;
        }

        private static void AddAccountParams(SqlParameterCollection p, string label, string email, string display,
            string smtpHost, int smtpPort, bool smtpSsl, string smtpUser, string imapHost, int imapPort, bool imapSsl,
            string imapUser, int dailyLimit, int perMinute, bool isDefault, bool isActive)
        {
            p.AddWithValue("@Label", label ?? "");
            p.AddWithValue("@Email", email ?? "");
            p.AddWithValue("@Display", (object)display ?? DBNull.Value);
            p.AddWithValue("@SH", (object)smtpHost ?? DBNull.Value);
            p.AddWithValue("@SP", smtpPort <= 0 ? 587 : smtpPort);
            p.AddWithValue("@SS", smtpSsl ? 1 : 0);
            p.AddWithValue("@SU", (object)smtpUser ?? DBNull.Value);
            p.AddWithValue("@IH", (object)imapHost ?? DBNull.Value);
            p.AddWithValue("@IP", imapPort <= 0 ? 993 : imapPort);
            p.AddWithValue("@IS", imapSsl ? 1 : 0);
            p.AddWithValue("@IU", (object)imapUser ?? DBNull.Value);
            p.AddWithValue("@DL", dailyLimit <= 0 ? 400 : dailyLimit);
            p.AddWithValue("@PM", perMinute <= 0 ? 20 : perMinute);
            p.AddWithValue("@Def", isDefault ? 1 : 0);
            p.AddWithValue("@Active", isActive ? 1 : 0);
        }

        /// <summary>Syncs one configured mailbox now and returns a human summary.</summary>
        public static string SyncInbox(int accountId)
        {
            DataRow a = accountId > 0 ? Account(accountId) : DefaultAccount();
            if (a == null) return "Add a mail account first.";
            string host = Convert.ToString(a["ImapHost"]);
            if (string.IsNullOrWhiteSpace(host)) return "This account has no IMAP host configured.";
            try
            {
                int count = Integrations.ImapMailbox.Sync(
                    host,
                    a["ImapPort"] == DBNull.Value ? 993 : Convert.ToInt32(a["ImapPort"]),
                    a["ImapSsl"] != DBNull.Value && Convert.ToBoolean(a["ImapSsl"]),
                    Convert.ToString(a["ImapUser"]),
                    Unprotect(Convert.ToString(a["ImapPasswordEnc"])),
                    (from, to, subject, html, text, folder, uid) =>
                        StoreReceived(Convert.ToInt32(a["AccountId"]), from, to, subject, html, text, folder, uid),
                    25);
                SqlHelper.ExecuteNonQuery(
                    "UPDATE dbo.MailAccounts SET LastSyncUtc=SYSUTCDATETIME(), LastError=NULL WHERE AccountId=@Id",
                    p => p.AddWithValue("@Id", a["AccountId"]));
                return count + " message(s) received.";
            }
            catch (Exception ex)
            {
                SqlHelper.ExecuteNonQuery(
                    "UPDATE dbo.MailAccounts SET LastError=@E, LastSyncUtc=SYSUTCDATETIME() WHERE AccountId=@Id",
                    p => { p.AddWithValue("@E", Short(ex.Message, 480)); p.AddWithValue("@Id", a["AccountId"]); });
                Global.WriteLog("MailSync", "IMAP sync failed", ex);
                return "IMAP sync failed: " + ex.Message;
            }
        }

        /// <summary>Background tick: sync each active IMAP mailbox when its interval has elapsed.</summary>
        public static void SyncDue()
        {
            int minutes = 2;
            try { minutes = Math.Max(1, Convert.ToInt32(GetSetting("Mail.AutoSyncMinutes", "2"))); }
            catch (Exception) { }
            DataTable accounts;
            try
            {
                accounts = SqlHelper.FillDataTable(
                    "SELECT AccountId FROM dbo.MailAccounts WHERE IsActive=1 AND ImapHost IS NOT NULL AND ImapHost<>'' " +
                    "AND (LastSyncUtc IS NULL OR DATEADD(MINUTE,@M,LastSyncUtc) <= SYSUTCDATETIME())",
                    p => p.AddWithValue("@M", minutes));
            }
            catch (Exception) { return; }
            foreach (DataRow r in accounts.Rows) SyncInbox(Convert.ToInt32(r["AccountId"]));
        }

        public static void DeleteAccount(int id)
        {
            SqlHelper.ExecuteNonQuery("DELETE FROM dbo.MailAccounts WHERE AccountId=@Id", p => p.AddWithValue("@Id", id));
        }

        /// <summary>Sends one test message using the account (or the Web.config SMTP fallback).</summary>
        public static string TestAccount(int id, string to)
        {
            DataRow a = id > 0 ? Account(id) : DefaultAccount();
            return SendNow(a, to, "Mail centre self-test",
                "<p>SMTP works. This message was sent from the SMS mail centre.</p>");
        }

        // ---------------------------------------------------------------- queue

        public static DataTable Queue(int take)
        {
            return SqlHelper.FillDataTable(
                "SELECT TOP (@Take) q.QueueId, q.Subject, q.Status, q.ScheduledUtc, q.IsBulk, q.TotalRecipients, " +
                "q.SentCount, q.FailedCount, q.Attempts, q.LastError, q.CreatedAtUtc, a.EmailAddress AS AccountEmail " +
                "FROM dbo.MailQueue q LEFT JOIN dbo.MailAccounts a ON a.AccountId=q.AccountId " +
                "ORDER BY q.QueueId DESC",
                p => p.AddWithValue("@Take", take <= 0 ? 50 : take));
        }

        public static DataTable Recipients(int queueId)
        {
            return SqlHelper.FillDataTable(
                "SELECT RecipientId, Email, DisplayName, Status, Error, SentAtUtc FROM dbo.MailRecipients " +
                "WHERE QueueId=@Q ORDER BY RecipientId",
                p => p.AddWithValue("@Q", queueId));
        }

        public static int Enqueue(int accountId, string subject, string bodyHtml, IEnumerable<KeyValuePair<string, string>> people,
            int createdBy, DateTime? scheduleUtc, bool bulk)
        {
            var clean = new List<KeyValuePair<string, string>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var person in people ?? new List<KeyValuePair<string, string>>())
            {
                string email = (person.Key ?? "").Trim();
                if (email.Length < 3 || email.IndexOf('@') < 1) continue;
                if (!seen.Add(email)) continue;
                clean.Add(new KeyValuePair<string, string>(email, person.Value));
            }
            if (clean.Count == 0) throw new Exception("Add at least one valid recipient.");

            DateTime when = scheduleUtc.HasValue && scheduleUtc.Value > DateTime.UtcNow ? scheduleUtc.Value : DateTime.UtcNow;
            using (SqlConnection conn = ConnectionManager.CreateOpenConnection())
            using (var tx = conn.BeginTransaction())
            {
                int id;
                using (var cmd = new SqlCommand(
                    "INSERT INTO dbo.MailQueue (AccountId, Subject, BodyHtml, CreatedBy, Status, ScheduledUtc, IsBulk, TotalRecipients) " +
                    "OUTPUT INSERTED.QueueId VALUES (@A,@S,@B,@U,'queued',@When,@Bulk,@Total)", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@A", accountId > 0 ? (object)accountId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@S", subject ?? "");
                    cmd.Parameters.AddWithValue("@B", bodyHtml ?? "");
                    cmd.Parameters.AddWithValue("@U", createdBy > 0 ? (object)createdBy : DBNull.Value);
                    cmd.Parameters.AddWithValue("@When", when);
                    cmd.Parameters.AddWithValue("@Bulk", bulk ? 1 : 0);
                    cmd.Parameters.AddWithValue("@Total", clean.Count);
                    id = Convert.ToInt32(cmd.ExecuteScalar());
                }
                foreach (var person in clean)
                {
                    bool suppressed = IsSuppressed(person.Key);
                    using (var cmd = new SqlCommand(
                        "INSERT INTO dbo.MailRecipients (QueueId, Email, DisplayName, Status, UnsubscribeToken) " +
                        "VALUES (@Q,@E,@N,@Status,@Tok)", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Q", id);
                        cmd.Parameters.AddWithValue("@E", person.Key);
                        cmd.Parameters.AddWithValue("@N", (object)person.Value ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Status", suppressed ? "suppressed" : "queued");
                        cmd.Parameters.AddWithValue("@Tok", RandomToken(20));
                        cmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();
                return id;
            }
        }

        public static int EnqueueToAudience(int accountId, string subject, string bodyHtml, string audience, int createdBy,
            DateTime? scheduleUtc, bool bulk)
        {
            return Enqueue(accountId, subject, bodyHtml, ResolveAudience(audience), createdBy, scheduleUtc, bulk);
        }

        public static List<KeyValuePair<string, string>> ResolveAudience(string audience)
        {
            var list = new List<KeyValuePair<string, string>>();
            string role = "";
            string course = "";
            string a = (audience ?? "students").Trim();
            if (a.StartsWith("role:", StringComparison.OrdinalIgnoreCase)) role = a.Substring(5);
            else if (a.StartsWith("course:", StringComparison.OrdinalIgnoreCase)) course = a.Substring(7);
            else if (a == "faculty") role = "Faculty";
            else if (a == "students") role = "Student";
            else if (a == "all") role = "";
            else return list;

            string sql =
                "SELECT DISTINCT u.Email, u.FullName FROM dbo.Users u " +
                "LEFT JOIN dbo.Students s ON s.UserId=u.UserId " +
                "LEFT JOIN dbo.Courses c ON c.CourseId=s.CourseId " +
                "WHERE u.IsActive=1 AND (@Role='' OR u.RoleName=@Role) AND (@Course='' OR c.CourseCode=@Course) " +
                "AND u.Email IS NOT NULL";
            DataTable dt = SqlHelper.FillDataTable(sql, p =>
            {
                p.AddWithValue("@Role", role);
                p.AddWithValue("@Course", course);
            });
            foreach (DataRow r in dt.Rows)
                list.Add(new KeyValuePair<string, string>(Convert.ToString(r["Email"]), Convert.ToString(r["FullName"])));
            return list;
        }

        public static void Pause(int id, bool paused)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.MailQueue SET Status=@S WHERE QueueId=@Id AND Status IN ('queued','sending','paused','failed')",
                p => { p.AddWithValue("@S", paused ? "paused" : "queued"); p.AddWithValue("@Id", id); });
        }

        public static void Cancel(int id)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.MailQueue SET Status='cancelled', CompletedAtUtc=SYSUTCDATETIME() WHERE QueueId=@Id AND Status NOT IN ('sent','cancelled')",
                p => p.AddWithValue("@Id", id));
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.MailRecipients SET Status='failed', Error='campaign cancelled' WHERE QueueId=@Id AND Status='queued'",
                p => p.AddWithValue("@Id", id));
        }

        private static int _busy;

        /// <summary>Called by the background timer. Sends queued mail, honouring per-minute throttling.</summary>
        public static void ProcessDue(int maxCampaigns)
        {
            if (System.Threading.Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
            try
            {
                DataTable due = SqlHelper.FillDataTable(
                    "SELECT TOP (@N) * FROM dbo.MailQueue WHERE Status='queued' AND ScheduledUtc<=SYSUTCDATETIME() ORDER BY QueueId",
                    p => p.AddWithValue("@N", Math.Max(1, maxCampaigns)));
                foreach (DataRow q in due.Rows) SendCampaign(Convert.ToInt32(q["QueueId"]));
            }
            catch (Exception ex) { Global.WriteLog("MailQueue", "processor failed", ex); }
            finally { System.Threading.Interlocked.Exchange(ref _busy, 0); }
        }

        public static string SendCampaign(int queueId)
        {
            DataRow q = SqlHelper.FillDataTable("SELECT * FROM dbo.MailQueue WHERE QueueId=@Id",
                p => p.AddWithValue("@Id", queueId)).Rows.Count == 0
                ? null
                : SqlHelper.FillDataTable("SELECT * FROM dbo.MailQueue WHERE QueueId=@Id",
                    p => p.AddWithValue("@Id", queueId)).Rows[0];
            if (q == null) return "not found";
            if (Convert.ToString(q["Status"]) == "cancelled") return "cancelled";

            int accountId = q["AccountId"] == DBNull.Value ? 0 : Convert.ToInt32(q["AccountId"]);
            DataRow account = accountId > 0 ? Account(accountId) : DefaultAccount();
            int perMinute = account == null || account["PerMinute"] == DBNull.Value ? 20 : Convert.ToInt32(account["PerMinute"]);
            if (perMinute <= 0) perMinute = 20;

            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.MailQueue SET Status='sending', Attempts=Attempts+1 WHERE QueueId=@Id",
                p => p.AddWithValue("@Id", queueId));

            int sent = 0, failed = 0;
            string lastError = null;
            DataTable people = SqlHelper.FillDataTable(
                "SELECT TOP (@N) * FROM dbo.MailRecipients WHERE QueueId=@Q AND Status='queued' ORDER BY RecipientId",
                p => { p.AddWithValue("@Q", queueId); p.AddWithValue("@N", perMinute); });
            var started = DateTime.UtcNow;
            foreach (DataRow r in people.Rows)
            {
                int rid = Convert.ToInt32(r["RecipientId"]);
                string email = Convert.ToString(r["Email"]);
                string err = SendNow(account, email, Convert.ToString(q["Subject"]), Render(Convert.ToString(q["BodyHtml"]), r));
                if (err == null)
                {
                    sent++;
                    SqlHelper.ExecuteNonQuery(
                        "UPDATE dbo.MailRecipients SET Status='sent', SentAtUtc=SYSUTCDATETIME(), Error=NULL WHERE RecipientId=@Id",
                        p => p.AddWithValue("@Id", rid));
                }
                else
                {
                    failed++;
                    lastError = err;
                    SqlHelper.ExecuteNonQuery(
                        "UPDATE dbo.MailRecipients SET Status='failed', Error=@E WHERE RecipientId=@Id",
                        p => { p.AddWithValue("@E", Short(err, 280)); p.AddWithValue("@Id", rid); });
                }
            }

            int remaining = SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.MailRecipients WHERE QueueId=@Q AND Status='queued'",
                p => p.AddWithValue("@Q", queueId));
            string status = remaining == 0 ? (failed > 0 && sent == 0 ? "failed" : "sent") : "queued";
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.MailQueue SET Status=@S, SentCount=SentCount+@Sent, FailedCount=FailedCount+@Failed, " +
                "LastError=@Err, ScheduledUtc=CASE WHEN @Remain>0 THEN DATEADD(MINUTE,1,SYSUTCDATETIME()) ELSE ScheduledUtc END, " +
                "CompletedAtUtc=CASE WHEN @Remain=0 THEN SYSUTCDATETIME() ELSE NULL END WHERE QueueId=@Id",
                p =>
                {
                    p.AddWithValue("@S", status);
                    p.AddWithValue("@Sent", sent);
                    p.AddWithValue("@Failed", failed);
                    p.AddWithValue("@Err", (object)lastError ?? DBNull.Value);
                    p.AddWithValue("@Remain", remaining);
                    p.AddWithValue("@Id", queueId);
                });
            return sent + " sent, " + failed + " failed, " + remaining + " remaining";
        }

        private static string Render(string html, DataRow recipient)
        {
            string name = Convert.ToString(recipient["DisplayName"]);
            string email = Convert.ToString(recipient["Email"]);
            string html2 = (html ?? "")
                .Replace("{{name}}", HttpUtility.HtmlEncode(string.IsNullOrEmpty(name) ? email : name))
                .Replace("{{email}}", HttpUtility.HtmlEncode(email))
                .Replace("{{institution}}", HttpUtility.HtmlEncode(GetSetting("Mail.FromName", "Campus Office")))
                .Replace("{{baseurl}}", HttpUtility.HtmlEncode(Lib.AppConfig.BaseUrl));
            string token = Convert.ToString(recipient.Table.Columns.Contains("UnsubscribeToken") ? recipient["UnsubscribeToken"] : null);
            if (!string.IsNullOrEmpty(token))
                html2 += "<p style='color:#777;font-size:12px'><a href='" + HttpUtility.HtmlEncode(Lib.AppConfig.BaseUrl.TrimEnd('/')) +
                    "/Handlers/Unsubscribe.ashx?t=" + HttpUtility.UrlEncode(token) + "'>Unsubscribe from bulk mail</a></p>";
            return html2;
        }

        private static string SendNow(DataRow account, string to, string subject, string html)
        {
            if (string.IsNullOrWhiteSpace(to)) return "empty recipient";
            string host, user, pass, from, display;
            int port; bool ssl;
            if (account != null)
            {
                host = Convert.ToString(account["SmtpHost"]);
                port = account["SmtpPort"] == DBNull.Value ? 587 : Convert.ToInt32(account["SmtpPort"]);
                ssl = account["SmtpSsl"] != DBNull.Value && Convert.ToBoolean(account["SmtpSsl"]);
                user = Convert.ToString(account["SmtpUser"]);
                pass = Unprotect(Convert.ToString(account["SmtpPasswordEnc"]));
                from = Convert.ToString(account["EmailAddress"]);
                display = Convert.ToString(account["DisplayName"]);
            }
            else
            {
                host = Lib.AppConfig.Get("Email:SmtpHost", "");
                port = Lib.AppConfig.GetInt("Email:SmtpPort", 587);
                ssl = Lib.AppConfig.GetBool("Email:EnableSsl", true);
                user = Lib.AppConfig.Get("Email:User", "");
                pass = Lib.AppConfig.Get("Email:Password", "");
                from = Lib.AppConfig.Get("Email:FromAddress", "");
                display = Lib.AppConfig.Get("Email:FromName", from);
            }
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
                return "SMTP is not configured. Add a mail account first.";
            try
            {
                using (var msg = new MailMessage())
                {
                    msg.From = new MailAddress(from, string.IsNullOrWhiteSpace(display) ? from : display);
                    msg.To.Add(new MailAddress(to));
                    msg.Subject = subject ?? "";
                    msg.SubjectEncoding = Encoding.UTF8;
                    msg.BodyEncoding = Encoding.UTF8;
                    msg.IsBodyHtml = true;
                    msg.Body = html ?? "";
                    using (var client = new SmtpClient(host, port))
                    {
                        client.EnableSsl = ssl;
                        client.Timeout = 20000;
                        if (!string.IsNullOrWhiteSpace(user)) client.Credentials = new NetworkCredential(user, pass);
                        client.Send(msg);
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Exception root = ex;
                while (root.InnerException != null) root = root.InnerException;
                Global.WriteLog("MailSend", "send to " + to + " failed", ex);
                return root.Message;
            }
        }

        // ---------------------------------------------------------------- templates

        public static DataTable Templates()
        {
            return SqlHelper.FillDataTable("SELECT * FROM dbo.MailTemplates WHERE IsActive=1 ORDER BY Name");
        }

        public static int SaveTemplate(int id, string name, string subject, string body)
        {
            if (id > 0)
            {
                SqlHelper.ExecuteNonQuery(
                    "UPDATE dbo.MailTemplates SET Name=@N, Subject=@S, BodyHtml=@B WHERE TemplateId=@Id",
                    p => { p.AddWithValue("@N", name ?? ""); p.AddWithValue("@S", subject ?? ""); p.AddWithValue("@B", body ?? ""); p.AddWithValue("@Id", id); });
                return id;
            }
            return SqlHelper.ExecuteScalarInt(
                "INSERT INTO dbo.MailTemplates (Name, Subject, BodyHtml) OUTPUT INSERTED.TemplateId VALUES (@N,@S,@B)",
                p => { p.AddWithValue("@N", name ?? ""); p.AddWithValue("@S", subject ?? ""); p.AddWithValue("@B", body ?? ""); });
        }

        public static void DeleteTemplate(int id)
        {
            SqlHelper.ExecuteNonQuery("UPDATE dbo.MailTemplates SET IsActive=0 WHERE TemplateId=@Id",
                p => p.AddWithValue("@Id", id));
        }

        // ---------------------------------------------------------------- messages

        public static DataTable Messages(string folder, string search, int take)
        {
            return SqlHelper.FillDataTable(
                "SELECT TOP (@N) MessageId, Direction, FromAddress, ToAddress, Subject, Folder, IsRead, IsStarred, ReceivedUtc " +
                "FROM dbo.MailMessages WHERE (@Folder='' OR Folder=@Folder) AND (@Q='' OR Subject LIKE '%'+@Q+'%' OR FromAddress LIKE '%'+@Q+'%') " +
                "ORDER BY ReceivedUtc DESC",
                p =>
                {
                    p.AddWithValue("@N", take <= 0 ? 50 : take);
                    p.AddWithValue("@Folder", folder ?? "");
                    p.AddWithValue("@Q", search ?? "");
                });
        }

        public static DataRow Message(int id)
        {
            DataTable dt = SqlHelper.FillDataTable("SELECT * FROM dbo.MailMessages WHERE MessageId=@Id",
                p => p.AddWithValue("@Id", id));
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        public static void MarkMessage(int id, bool? read, bool? starred, string folder)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.MailMessages SET IsRead=COALESCE(@R,IsRead), IsStarred=COALESCE(@S,IsStarred), " +
                "Folder=COALESCE(@F,Folder) WHERE MessageId=@Id",
                p =>
                {
                    p.AddWithValue("@R", read.HasValue ? (object)(read.Value ? 1 : 0) : DBNull.Value);
                    p.AddWithValue("@S", starred.HasValue ? (object)(starred.Value ? 1 : 0) : DBNull.Value);
                    p.AddWithValue("@F", string.IsNullOrEmpty(folder) ? (object)DBNull.Value : folder);
                    p.AddWithValue("@Id", id);
                });
        }

        public static int UnreadCount()
        {
            try { return SqlHelper.ExecuteScalarInt("SELECT COUNT(*) FROM dbo.MailMessages WHERE Direction='in' AND IsRead=0 AND Folder='INBOX'"); }
            catch (Exception) { return 0; }
        }

        public static int StoreReceived(int accountId, string from, string to, string subject, string html, string text,
            string folder, string uid)
        {
            if (!string.IsNullOrEmpty(uid) && SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.MailMessages WHERE ExternalUid=@U AND AccountId=@A",
                p => { p.AddWithValue("@U", uid); p.AddWithValue("@A", accountId); }) > 0) return 0;
            return SqlHelper.ExecuteScalarInt(
                "INSERT INTO dbo.MailMessages (AccountId, Direction, FromAddress, ToAddress, Subject, BodyHtml, BodyText, Folder, ExternalUid, ReceivedUtc) " +
                "OUTPUT INSERTED.MessageId VALUES (@A,'in',@F,@T,@S,@H,@X,@Folder,@U,SYSUTCDATETIME())",
                p =>
                {
                    p.AddWithValue("@A", accountId > 0 ? (object)accountId : DBNull.Value);
                    p.AddWithValue("@F", (object)Short(from, 190) ?? DBNull.Value);
                    p.AddWithValue("@T", (object)Short(to, 390) ?? DBNull.Value);
                    p.AddWithValue("@S", (object)Short(subject, 290) ?? DBNull.Value);
                    p.AddWithValue("@H", (object)html ?? DBNull.Value);
                    p.AddWithValue("@X", (object)text ?? DBNull.Value);
                    p.AddWithValue("@Folder", string.IsNullOrEmpty(folder) ? "INBOX" : folder);
                    p.AddWithValue("@U", (object)uid ?? DBNull.Value);
                });
        }

        // ---------------------------------------------------------------- suppressions / settings

        public static bool IsSuppressed(string email)
        {
            try
            {
                return SqlHelper.ExecuteScalarInt("SELECT COUNT(*) FROM dbo.MailSuppressions WHERE Email=@E",
                    p => p.AddWithValue("@E", (email ?? "").Trim())) > 0;
            }
            catch (Exception) { return false; }
        }

        public static void Suppress(string email, string reason)
        {
            SqlHelper.ExecuteNonQuery(
                "IF NOT EXISTS (SELECT 1 FROM dbo.MailSuppressions WHERE Email=@E) INSERT INTO dbo.MailSuppressions (Email, Reason) VALUES (@E,@R)",
                p => { p.AddWithValue("@E", (email ?? "").Trim()); p.AddWithValue("@R", (object)reason ?? DBNull.Value); });
        }

        public static DataTable Suppressions()
        {
            return SqlHelper.FillDataTable("SELECT TOP 200 * FROM dbo.MailSuppressions ORDER BY CreatedAtUtc DESC");
        }

        public static string GetSetting(string key, string fallback)
        {
            try
            {
                object v = SqlHelper.ExecuteScalar("SELECT SettingValue FROM dbo.InstitutionSettings WHERE SettingKey=@K",
                    p => p.AddWithValue("@K", key));
                return v == null || v == DBNull.Value ? fallback : Convert.ToString(v);
            }
            catch (Exception) { return fallback; }
        }

        public static void SetSetting(string key, string value, int userId)
        {
            SqlHelper.ExecuteNonQuery(
                "MERGE dbo.InstitutionSettings AS t USING (SELECT @K AS K, @V AS V) s ON t.SettingKey=s.K " +
                "WHEN MATCHED THEN UPDATE SET SettingValue=s.V, UpdatedBy=@U, UpdatedAtUtc=SYSUTCDATETIME() " +
                "WHEN NOT MATCHED THEN INSERT (SettingKey, SettingValue, UpdatedBy) VALUES (s.K, s.V, @U);",
                p => { p.AddWithValue("@K", key); p.AddWithValue("@V", (object)value ?? DBNull.Value); p.AddWithValue("@U", userId > 0 ? (object)userId : DBNull.Value); });
        }

        // ---------------------------------------------------------------- helpers

        private static string Protect(string plain)
        {
            byte[] data = Encoding.UTF8.GetBytes(plain ?? "");
            byte[] enc = ProtectedData.Protect(data, null, DataProtectionScope.LocalMachine);
            return Convert.ToBase64String(enc);
        }

        private static string Unprotect(string enc)
        {
            if (string.IsNullOrEmpty(enc)) return "";
            try
            {
                byte[] data = Convert.FromBase64String(enc);
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, null, DataProtectionScope.LocalMachine));
            }
            catch (Exception) { return ""; }
        }

        private static string RandomToken(int bytes)
        {
            var data = new byte[bytes];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(data);
            return Convert.ToBase64String(data).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        private static string Short(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}

