using System;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Integrations;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Account
{
    /// <summary>
    /// Password reset request. The email body is only sent when Email:Enabled;
    /// responses never disclose whether an account exists (enumeration-safe).
    /// </summary>
    public partial class Forgot : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (CurrentUser.IsAuthenticated) Response.Redirect(AuthTicket.DashboardFor(CurrentUser.RoleName), true);
        }

        protected void btnSend_Click(object sender, EventArgs e)
        {
            if (!IsValid) return;

            string email = txtEmail.Text.Trim().ToLowerInvariant();
            bool emailConfigured = AppConfig.EmailEnabled;

            if (emailConfigured)
            {
                UserRecord user = UsersRepo.FindByEmail(email);
                if (user != null && user.IsActive)
                {
                    string rawToken = ResetTokenRepo.CreateAndReturnRaw(user.UserId);
                    string link = AppConfig.BaseUrl.TrimEnd('/') + "/Account/Reset.aspx?token=" + HttpUtility.UrlEncode(rawToken);

                    string body =
                        "<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:0 auto;\">" +
                        "<h2 style=\"color:#1a2949;margin-bottom:4px;\">Password reset</h2>" +
                        "<p>Hello " + HttpUtility.HtmlEncode(user.FullName) + ",</p>" +
                        "<p>We received a request to reset your Student Management System password. " +
                        "Use the button below within <strong>60 minutes</strong>.</p>" +
                        "<p style=\"text-align:center;margin:24px 0;\">" +
                        "<a href=\"" + link + "\" style=\"background:#1a2949;color:#fff;padding:10px 22px;border-radius:8px;text-decoration:none;\">Set a new password</a></p>" +
                        "<p class=\"small\" style=\"color:#6c757d;\">If you did not request this, you can ignore this message - " +
                        "your current password keeps working.</p></div>";

                    var mail = new SmtpMailService();
                    if (!mail.Send(email, "Password reset - Student Management System", body))
                    {
                        ShowAlert("danger", "We could not send the reset email. Please try again later.");
                        return;
                    }
                    AuditService.Log("PasswordResetRequested", "Users", user.UserId);
                }
            }

            phForm.Visible = false;
            ShowAlert(emailConfigured ? "success" : "info",
                emailConfigured
                    ? "If that email belongs to an active account, a reset link is on its way. Check your inbox and junk folder."
                    : "Password reset by email is not configured on this installation. Please contact your administrator.");
        }

        private void ShowAlert(string kind, string text)
        {
            litAlert.Text = "<div class='alert alert-" + kind + " py-2 px-3 small'>" + HttpUtility.HtmlEncode(text) + "</div>";
        }
    }
}
