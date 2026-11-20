using System;
using System.Data.SqlClient;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Integrations;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Account
{
    /// <summary>
    /// Email + password sign-in.
    ///   * lockout window enforced after N failures
    ///   * when 2FA is enabled and email is deliverable the second step is an OTP
    ///   * unverified signups are pushed back into the verify flow
    /// </summary>
    public partial class Login : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (CurrentUser.IsAuthenticated)
            {
                Response.Redirect(AuthTicket.DashboardFor(CurrentUser.RoleName), true);
                return;
            }

            if (IsPostBack) return;

            ViewState["ReturnUrl"] = Request.QueryString["ReturnUrl"];

            string mode = Request.QueryString["m"];
            if (mode == "verified") ShowAlert("success", "Your account is verified. Sign in to continue.");
            else if (mode == "loggedout") ShowAlert("info", "You are signed out.");
            else if (mode == "reset") ShowAlert("success", "Password updated. Sign in with the new password.");
            else if (mode == "denied") ShowAlert("danger", "Access denied for that page.");

            // GoogleOAuth.ashx redirects back here with the reason sign-in could not finish.
            string googleError = Request.QueryString["gerr"];
            if (!string.IsNullOrEmpty(googleError)) ShowAlert("danger", googleError);

            phGoogle.Visible = AppConfig.GoogleAuthEnabled;
        }

        protected void btnSignIn_Click(object sender, EventArgs e)
        {
            if (!IsValid) return;

            string email = txtEmail.Text.Trim().ToLowerInvariant();
            string password = txtPassword.Text;

            UserRecord user;
            try { user = UsersRepo.FindByEmail(email); }
            catch (Exception ex)
            {
                Global.WriteLog("Login", "lookup failed", ex);
                ShowAlert("danger", "The database is unreachable right now. Please try again shortly.");
                return;
            }

            if (user == null || !PasswordHasher.Verify(password, user.PasswordHash))
            {
                if (user != null) AuditService.LogLoginFailed(email);
                else AuditService.Log("LoginFailed", "Users", null, "unknown email");
                ShowAlert("danger", "Invalid email or password.");
                return;
            }

            if (UsersRepo.IsLocked(user))
            {
                ShowAlert("danger", "Too many failed attempts. Your account is temporarily locked. Try again later.");
                return;
            }

            if (string.Equals(user.ApprovalStatus, "pending", StringComparison.OrdinalIgnoreCase))
            {
                ShowAlert("warning", "This account is waiting for administrator approval. You will be able to sign in once it is approved.");
                return;
            }
            if (string.Equals(user.ApprovalStatus, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                ShowAlert("warning", "This registration was not approved. Contact the campus office for help.");
                return;
            }
            if (!user.IsActive)
            {
                ShowAlert("warning", "This account is deactivated. Contact your administrator.");
                return;
            }

            if (!user.EmailVerified)
            {
                Session[PendingOtpState.SessionKey] = new PendingOtpState
                {
                    UserId = user.UserId,
                    Purpose = "signup",
                    Channel = "email",
                    Email = user.Email,
                    FullName = user.FullName,
                    CreatedUtc = DateTime.UtcNow
                };
                Response.Redirect("~/Account/Verify.aspx?purpose=signup", true);
                return;
            }

            // optional 2FA: only when enabled on the account AND email configured
            if (user.TwoFactorEnabled && AppConfig.EmailEnabled)
            {
                string fail = OtpSender.SendViaEmail(user.UserId, user.FullName, user.Email, "login");
                if (fail != null) { ShowAlert("danger", fail); return; }

                Session[PendingOtpState.SessionKey] = new PendingOtpState
                {
                    UserId = user.UserId,
                    Purpose = "login",
                    Channel = "email",
                    Email = user.Email,
                    FullName = user.FullName,
                    CreatedUtc = DateTime.UtcNow
                };
                Response.Redirect("~/Account/Verify.aspx?purpose=login", true);
                return;
            }

            CompleteSignIn(user);
        }

        private void CompleteSignIn(UserRecord user)
        {
            UsersRepo.MarkLoginSuccess(user.UserId);
            AuthTicket.Issue(user, chkRemember.Checked);
            AuditService.Log("Login", "Users", user.UserId, AuditService.IpAddress());

            string target = null;
            string requested = ViewState["ReturnUrl"] as string;
            if (!string.IsNullOrEmpty(requested) && AuthTicket.IsLocalUrl(requested)) target = requested;
            if (string.IsNullOrEmpty(target)) target = AuthTicket.DashboardFor(user.RoleName);
            Response.Redirect(target, true);
        }

        private void ShowAlert(string kind, string text)
        {
            litAlert.Text = "<div class='alert alert-" + kind + " py-2 px-3 small'>" + HttpUtility.HtmlEncode(text) + "</div>";
        }
    }
}
