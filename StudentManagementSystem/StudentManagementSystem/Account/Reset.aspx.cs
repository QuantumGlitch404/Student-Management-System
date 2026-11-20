using System;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Account
{
    /// <summary>
    /// Consumes a single-use reset token (SHA256-hashed at rest, 60-minute life)
    /// and saves the replacement password.
    /// </summary>
    public partial class Reset : System.Web.UI.Page
    {
        private string RawToken
        {
            get { return Request.QueryString["token"]; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;
            if (string.IsNullOrEmpty(RawToken))
            {
                phForm.Visible = false;
                phInvalid.Visible = true;
                return;
            }
            string hash = TokenHash(RawToken);
            bool valid = Convert.ToInt32(SqlHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.PasswordResetTokens WHERE TokenHash = @Hash AND UsedAtUtc IS NULL AND ExpiresAtUtc > SYSUTCDATETIME()",
                p => p.AddWithValue("@Hash", hash))) > 0;
            if (!valid)
            {
                phForm.Visible = false;
                phInvalid.Visible = true;
            }
        }

        private static string TokenHash(string raw) { return PasswordHasher.Sha256Hex(raw); }

        protected void btnReset_Click(object sender, EventArgs e)
        {
            string password = txtPassword.Text;
            string confirm = txtConfirm.Text;

            if (password.Length < AppConfig.GetInt("MinPasswordLength", 8))
            {
                ShowAlert("danger", "The password must be at least " + AppConfig.GetInt("MinPasswordLength", 8) + " characters.");
                return;
            }
            if (password != confirm)
            {
                ShowAlert("danger", "The two passwords do not match.");
                return;
            }

            string error = ResetTokenRepo.Consume(RawToken, PasswordHasher.Hash(password));
            if (error != null)
            {
                phForm.Visible = false;
                phInvalid.Visible = true;
                return;
            }

            AuditService.Log("PasswordReset", "Users", null, "self-service");
            Response.Redirect("~/Account/Login.aspx?m=reset", true);
        }

        private void ShowAlert(string kind, string text)
        {
            litAlert.Text = "<div class='alert alert-" + kind + " py-2 px-3 small'>" + System.Web.HttpUtility.HtmlEncode(text) + "</div>";
        }
    }
}
