using System;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Integrations;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Account
{
    /// <summary>
    /// Second step for both signup verification and 2FA sign-in. The pending
    /// state lives in the session (never in the query string) and the code is
    /// validated against the persisted hash - never the raw value.
    /// </summary>
    public partial class Verify : System.Web.UI.Page
    {
        private PendingOtpState _state;

        protected void Page_Load(object sender, EventArgs e)
        {
            _state = Session[PendingOtpState.SessionKey] as PendingOtpState;
            if (_state == null)
            {
                Response.Redirect("~/Account/Login.aspx", true);
                return;
            }
            if (_state.Purpose != "signup" && _state.Purpose != "login")
            {
                Response.Redirect("~/Account/Login.aspx", true);
                return;
            }

            if (IsPostBack) return;

            string target = Request.QueryString["purpose"];
            if (!string.IsNullOrEmpty(target) && target != _state.Purpose)
            {
                Response.Redirect("~/Account/Verify.aspx?purpose=" + _state.Purpose, true);
                return;
            }

            ShowIntro();
        }

        private void ShowIntro()
        {
            pIntro.InnerHtml = "We sent a 6-digit code to <strong>" + HttpUtility.HtmlEncode(MaskEmail(_state.Email))
                + "</strong>. It expires after " + AppConfig.GetInt("Otp:ExpiryMinutes", 10) + " minutes.";

            bool emailConfigured = AppConfig.EmailEnabled;
            pNote.InnerHtml = emailConfigured
                ? "Tip: check your junk folder if the code does not arrive within a few minutes."
                : "Email delivery is not configured on this installation. If you cannot complete verification here, "
                  + "ask an administrator to verify your account from the Admin panel.";
            if (!emailConfigured) { btnResend.Enabled = false; btnResend.CssClass += " disabled"; btnVerify.Enabled = false; }
        }

        protected void btnVerify_Click(object sender, EventArgs e)
        {
            if (!IsValid) return;
            string code = txtOtp.Text.Trim();
            string error = OtpRepo.Validate(_state.UserId, _state.Purpose, code);
            if (error != null) { ShowAlert("danger", error); return; }

            if (_state.Purpose == "signup")
            {
                UsersRepo.VerifyEmail(_state.UserId);
                AuditService.Log("EmailVerified", "Users", _state.UserId);
            }

            UserRecord user = UsersRepo.FindById(_state.UserId);
            Session.Remove(PendingOtpState.SessionKey);

            if (user == null) { ShowAlert("danger", "Account not found. Please sign up again."); return; }

            if (string.Equals(user.ApprovalStatus, "pending", StringComparison.OrdinalIgnoreCase))
            {
                ShowAlert("success", "Your email is verified. The campus office must approve the account before your first sign-in.");
                btnVerify.Enabled = false;
                btnResend.Enabled = false;
                return;
            }
            if (string.Equals(user.ApprovalStatus, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                ShowAlert("danger", "This registration was not approved. Contact the campus office if you think that is a mistake.");
                btnVerify.Enabled = false;
                btnResend.Enabled = false;
                return;
            }

            UsersRepo.MarkLoginSuccess(user.UserId);
            AuthTicket.Issue(user, false);
            AuditService.Log("Login", "Users", user.UserId, "verified");

            string target = _state.Purpose == "signup"
                ? "~/Student/Index.aspx"
                : AuthTicket.DashboardFor(user.RoleName);
            Response.Redirect(target, true);
        }

        protected void btnResend_Click(object sender, EventArgs e)
        {
            if (OtpRepo.IsCoolingDown(_state.UserId, _state.Purpose))
            {
                ShowAlert("info", "A code was just sent. Wait about a minute before requesting another one.");
                return;
            }

            string fail = OtpSender.SendViaEmail(_state.UserId, _state.FullName, _state.Email, _state.Purpose);
            if (fail != null) { ShowAlert("danger", fail); return; }
            ShowAlert("success", "A new code was sent to " + HttpUtility.HtmlEncode(MaskEmail(_state.Email)) + ".");
        }

        private static string MaskEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return "";
            int at = email.IndexOf('@');
            if (at <= 0) return email;
            string name = email.Substring(0, at);
            string show = name.Length <= 2 ? name.Substring(0, 1) : name.Substring(0, 2) + "..." + name.Substring(name.Length - 1);
            return show + email.Substring(at);
        }

        private void ShowAlert(string kind, string htmlEncodedText)
        {
            litAlert.Text = "<div class='alert alert-" + kind + " py-2 px-3 small'>" + htmlEncodedText + "</div>";
        }
    }
}
