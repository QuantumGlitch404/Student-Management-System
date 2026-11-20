using System;
using System.Net;
using System.Net.Mail;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Integrations
{
    /// <summary>
    /// SMTP email sender (Gmail app-password / any SMTP). Uses BCL only.
    /// Credentials come from the encrypted AppSecrets store, with Web.config
    /// appSettings as the untouched fallback for a fresh install.
    ///
    /// Send() never throws: it returns false when email is unconfigured or the
    /// SMTP server rejects the message, so callers can degrade honestly instead
    /// of showing a fake "email sent" message.
    /// </summary>
    public class SmtpMailService
    {
        /// <summary>False when SMTP is unconfigured (Email:Enabled=false).</summary>
        public bool IsConfigured
        {
            get
            {
                return AppConfig.EmailEnabled &&
                    !string.IsNullOrWhiteSpace(AppConfig.Get("Email:SmtpHost", "")) &&
                    !string.IsNullOrWhiteSpace(AppConfig.Get("Email:FromAddress", ""));
            }
        }

        public bool Send(string to, string subject, string htmlBody)
        {
            if (!IsConfigured) return false;
            if (string.IsNullOrWhiteSpace(to)) return false;

            try
            {
                string host = AppConfig.Get("Email:SmtpHost", "");
                int port = AppConfig.GetInt("Email:SmtpPort", 587);
                string from = AppConfig.Get("Email:FromAddress", "");
                string display = AppConfig.Get("Email:FromName", from);
                string user = AppConfig.Get("Email:User", "");
                string pass = AppConfig.Get("Email:Password", "");

                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from)) return false;

                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(from, string.IsNullOrWhiteSpace(display) ? from : display);
                    message.To.Add(new MailAddress(to));
                    message.Subject = subject ?? "";
                    message.SubjectEncoding = System.Text.Encoding.UTF8;
                    message.BodyEncoding = System.Text.Encoding.UTF8;
                    message.IsBodyHtml = true;
                    message.Body = htmlBody ?? "";
                    using (var client = new SmtpClient(host, port))
                    {
                        client.EnableSsl = AppConfig.GetBool("Email:EnableSsl", true);
                        client.Timeout = 15000;
                        if (!string.IsNullOrEmpty(user))
                            client.Credentials = new NetworkCredential(user, pass);
                        client.Send(message);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Global.WriteLog("Email", "send to " + to + " failed", ex);
                return false;
            }
        }

        public string BuildOtpTemplate(string fullName, string purposeText, string otp, int minutes)
        {
            string title = AppConfig.Get("AppTitle", "Student Management System");
            string titleEnc = System.Web.HttpUtility.HtmlEncode(title);
            string nameEnc = System.Web.HttpUtility.HtmlEncode(string.IsNullOrEmpty(fullName) ? "user" : fullName);
            string purposeEnc = System.Web.HttpUtility.HtmlEncode(purposeText ?? "");
            string otpEnc = System.Web.HttpUtility.HtmlEncode(otp ?? "");
            return $@"<div style=""font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:auto;padding:24px;"">
<h2 style=""margin:0 0 8px;color:#1a2949"">{titleEnc}</h2>
<p style=""color:#444"">Hello {nameEnc},</p>
<p style=""color:#444"">{purposeEnc} Use this code within {minutes} minutes:</p>
<p style=""font-size:32px;letter-spacing:5px;margin:18px 0;padding:12px 0;text-align:center;background:#eef1f7;color:#12405e;border-radius:8px;font-weight:700"">{otpEnc}</p>
<p style=""color:#8a94a6;font-size:12px"">If you did not request this, ignore this email. Codes are single-use and expire automatically.</p>
</div>";
        }
    }
}
