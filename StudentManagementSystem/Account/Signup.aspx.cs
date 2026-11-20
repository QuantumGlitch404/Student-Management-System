using System;
using System.Data.SqlClient;
using System.Web;
using System.Web.UI.WebControls;
using StudentManagementSystem.Data;
using StudentManagementSystem.Integrations;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Account
{
    /// <summary>
    /// Self-service signup. Students are created with the default "Student" role
    /// and must verify by email (or by administrator action when email is off).
    /// </summary>
    public partial class Signup : System.Web.UI.Page
    {
        /// <summary>Exposed to the client so the password-length check matches the server.</summary>
        public int MinPasswordLength
        {
            get { return AppConfig.GetInt("MinPasswordLength", 8); }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (CurrentUser.IsAuthenticated) Response.Redirect(AuthTicket.DashboardFor(CurrentUser.RoleName), true);
            if (IsPostBack) return;

            int minLength = AppConfig.GetInt("MinPasswordLength", 8);
            lblPassword.Text = "Password";
            divMinLength.InnerHtml = "Use at least " + minLength + " characters.";
        }

        protected void cvPasswordLength_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = (args.Value != null && args.Value.Length >= AppConfig.GetInt("MinPasswordLength", 8));
        }

        protected void cvConsent_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = chkConsent.Checked;
        }

        protected void btnCreate_Click(object sender, EventArgs e)
        {
            if (!IsValid) return;

            string fullName = txtFullName.Text.Trim();
            string email = txtEmail.Text.Trim().ToLowerInvariant();
            string phone = txtPhone.Text.Trim();
            string password = txtPassword.Text;

            if (UsersRepo.EmailExists(email))
            {
                ShowAlert("danger", "This email is already registered. Sign in or reset your password.");
                return;
            }

            string policy = MailRepo.GetSetting("Registration.Policy", "pending");
            if (string.Equals(policy, "existing", StringComparison.OrdinalIgnoreCase))
            {
                ShowAlert("info", "Online registration is closed. Ask the campus office to create your account, then register with the same email address.");
                return;
            }
            bool autoApprove = string.Equals(policy, "auto", StringComparison.OrdinalIgnoreCase);

            int userId;
            try
            {
                userId = UsersRepo.CreateUser(
                    fullName, email, string.IsNullOrEmpty(phone) ? null : phone,
                    PasswordHasher.Hash(password), "Student", emailVerified: false,
                    approvalStatus: autoApprove ? "approved" : "pending", isActive: autoApprove);
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                ShowAlert("danger", "This email or phone is already registered.");
                return;
            }
            catch (Exception ex)
            {
                Global.WriteLog("Signup", "create failed", ex);
                ShowAlert("danger", "Account creation failed. Please try again shortly.");
                return;
            }

            AuditService.Log("Signup", "Users", userId, autoApprove ? "self-service/auto" : "self-service/pending");

            if (AppConfig.EmailEnabled)
            {
                string fail = OtpSender.SendViaEmail(userId, fullName, email, "signup");
                Session[PendingOtpState.SessionKey] = new PendingOtpState
                {
                    UserId = userId,
                    Purpose = "signup",
                    Channel = "email",
                    Email = email,
                    FullName = fullName,
                    CreatedUtc = DateTime.UtcNow
                };
                if (fail != null)
                {
                    // honest degradation: the account exists but the code could not be sent
                    ShowAlert("warning", fail + " Your account is created but unverified - you can sign in once it is verified by an administrator.");
                    return;
                }
                Response.Redirect("~/Account/Verify.aspx?purpose=signup", true);
                return;
            }

            // email disabled: a real account exists; verification/approval is done by an administrator.
            ShowAlert("info",
                "Account created. " + (autoApprove
                    ? "Email sending is not configured on this installation, so an administrator must verify your account before you can sign in."
                    : "An administrator must approve your account before you can sign in."));
        }

        private void ShowAlert(string kind, string text)
        {
            litAlert.Text = "<div class='alert alert-" + kind + " py-2 px-3 small'>" + HttpUtility.HtmlEncode(text) + "</div>";
        }
    }
}
