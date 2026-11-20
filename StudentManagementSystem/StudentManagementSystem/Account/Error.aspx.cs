using System;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Account
{
    /// <summary>
    /// Friendly landing for unhandled errors (customErrors redirect target).
    /// Deliberately shows no stack or exception detail.
    /// </summary>
    public partial class Error : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Session.Remove(PendingOtpState.SessionKey);
            Response.TrySkipIisCustomErrors = true;

            int code;
            int.TryParse(Request.QueryString["code"], out code);
            bool signedIn = CurrentUser.IsAuthenticated;

            if (code == 403)
            {
                Response.StatusCode = 403;
                pMark.InnerText = "403";
                pTitle.InnerText = "Access denied";
                pSub.InnerText = "Your account does not have a role that opens this page. Use the navigation to continue.";
                pPrimary.HRef = signedIn ? "/Default.aspx" : "/Account/Login.aspx";
                pPrimary.InnerText = signedIn ? "Open your dashboard" : "Back to sign in";
            }
            else if (code == 404)
            {
                Response.StatusCode = 404;
                pMark.InnerText = "404";
                pTitle.InnerText = "Page not found";
                pSub.InnerText = "The address may be mistyped, or the page may have moved. Use the navigation to continue.";
                pPrimary.HRef = signedIn ? "/Default.aspx" : "/Account/Login.aspx";
                pPrimary.InnerText = signedIn ? "Open your dashboard" : "Back to sign in";
            }
            else
            {
                pMark.InnerText = "!";
                pTitle.InnerText = "Something went wrong";
                pSub.InnerText = "The page could not complete. The problem has been logged. Use the buttons below to continue.";
                pPrimary.HRef = signedIn ? "/Default.aspx" : "/Account/Login.aspx";
                pPrimary.InnerText = signedIn ? "Open your dashboard" : "Back to sign in";
            }

            pSecondary.HRef = signedIn ? "/Account/Login.aspx" : "/Default.aspx";
            pSecondary.InnerText = signedIn ? "Sign in with another account" : "Open the home page";

            string path = Request.QueryString["aspxerrorpath"];
            if (!string.IsNullOrEmpty(path) && path.Length > 120) path = path.Substring(0, 120);
            pDetail.InnerHtml = string.IsNullOrEmpty(path)
                ? ""
                : "Requested page: " + System.Web.HttpUtility.HtmlEncode(path);
        }
    }
}
