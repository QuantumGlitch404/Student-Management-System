using System;
using System.Web.UI;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;
using System.Web;

namespace StudentManagementSystem.Account
{
    /// <summary>
    /// Signed-out shell. Appearance comes from the cookie mirror written by the
    /// signed-in shell and by the theme controls on this master itself, so the
    /// login/registration pages open in the same theme and text size the user
    /// last chose, without any database lookup.
    /// </summary>
    public partial class AccountMaster : System.Web.UI.MasterPage
    {
        protected string ThemeAttr = "light";
        protected string ThemePref = "light";
        protected string FontAttr = "100";
        protected string MotionAttr = "full";
        protected string ContrastAttr = "normal";
        protected string ZoomAttr = "100";
        protected string AuthSideClass = "";
        protected string AuthCardClass = "";
        protected string AuthShellClass = "";
        protected string BuildStamp = "BUILD DEV";

        protected void Page_Load(object sender, EventArgs e)
        {
            litYear.InnerHtml = DateTime.UtcNow.Year.ToString();
            if (Request.Path.EndsWith("/Signup.aspx", StringComparison.OrdinalIgnoreCase))
            {
                AuthShellClass = "auth-shell-wide";
                AuthSideClass = "auth-side-wide";
                AuthCardClass = "auth-signup";
            }

            UserPrefs prefs = PrefsRepo.FromCookies(Context);
            BuildStamp = ReadBuildStamp();
            ThemePref = prefs.Theme;
            ThemeAttr = prefs.Theme == "dark" ? "dark" : prefs.Theme == "eye" ? "eye" : "light";
            FontAttr = prefs.FontScale.ToString();
            ZoomAttr = prefs.Zoom.ToString();
            MotionAttr = prefs.Motion;
            ContrastAttr = prefs.HighContrast ? "high" : "normal";
        }

        private static string ReadBuildStamp()
        {
            try
            {
                string configured = AppConfig.Get("Build:Stamp", "");
                if (!string.IsNullOrWhiteSpace(configured)) return configured.Trim();
                string file = System.IO.Path.Combine(HttpRuntime.AppDomainAppPath, "BUILD-STAMP.txt");
                if (System.IO.File.Exists(file)) return System.IO.File.ReadAllText(file).Trim();
            }
            catch (Exception) { }
            return "BUILD DEV";
        }
    }
}



