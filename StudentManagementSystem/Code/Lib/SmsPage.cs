using System;
using System.Text;
using System.Web;
using System.Web.UI;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Lib
{
    /// <summary>
    /// Base page for every authenticated page.
    ///   * OnPreInit: anonymous -> redirect to login (ReturnUrl preserved)
    ///   * OnLoad: role gate (AllowedRoles) + CSRF check for POST requests
    ///   * Helpers: E() html-encode, Flash() one-shot alert, CsrfField() hidden input,
    ///     Options() renders an &lt;option&gt; list from a DataTable.
    ///
    /// Module pages deliberately avoid WebForms server controls (no ViewState postbacks
    /// for tables): they render plain HTML from server data and handle POSTs with the
    /// Post-Redirect-Get pattern. This keeps pages light and prevents ViewState abuse.
    /// </summary>
    public class SmsPage : Page
    {
        /// <summary>Roles allowed on this page. Empty = any authenticated role.</summary>
        protected virtual string[] AllowedRoles { get { return null; } }

        protected override void OnPreInit(EventArgs e)
        {
            base.OnPreInit(e);

            // Pages redirect with ?err=<message> after a failed action, but nothing
            // rendered it, so validation/DB errors were invisible. Seed a one-shot
            // flash here (not in OnLoad) so Page_Load's PopFlash() picks it up.
            string qsError = Request.QueryString["err"];
            if (Request.HttpMethod == "GET" && !string.IsNullOrEmpty(qsError))
                Flash("danger", qsError);

            if (CurrentUser.IsAuthenticated) return;
            Response.Redirect("~/Account/Login.aspx?ReturnUrl=" +
                HttpUtility.UrlEncode(Request.AppRelativeCurrentExecutionFilePath), true);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (AllowedRoles != null && AllowedRoles.Length > 0 && !CurrentUser.IsInAnyRole(AllowedRoles))
            {
                Response.Redirect("~/Account/Error.aspx?code=403", true);
                return;
            }

            if (Request.HttpMethod == "POST") EnsureCsrfValid();
        }

        // ---------------- HTML-safe helpers ----------------

        protected static string E(object value)
        {
            return value == null ? "" : HttpUtility.HtmlEncode(value.ToString());
        }

        protected static string Attr(object value)
        {
            return HttpUtility.HtmlAttributeEncode(value == null ? "" : value.ToString());
        }

        // ---------------- Flash messages (one-shot, Post-Redirect-Get) ----------------

        public static void Flash(string kind, string message)
        {
            var ctx = HttpContext.Current;
            if (ctx == null || ctx.Session == null) return;
            ctx.Session["Flash:" + kind] = HttpUtility.HtmlEncode(message);
        }

        protected string PopFlash()
        {
            var sb = new StringBuilder();
            foreach (string kind in new[] { "success", "danger", "warning", "info" })
            {
                string key = "Flash:" + kind;
                if (!string.IsNullOrEmpty(Session[key] as string))
                {
                    sb.Append("<div class='alert alert-").Append(kind).Append(" alert-dismissible py-2 px-3 small'>")
                      .Append(Session[key]).Append("<button type='button' class='close' data-dismiss='alert' data-bs-dismiss='alert' aria-label='Close'>&times;</button></div>");
                    Session.Remove(key);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Ends the current request with a 302 without throwing ThreadAbortException.
        /// Using endResponse:true would raise ThreadAbort, which every caller's
        /// catch (Exception) block would mistake for a real failure and turn into
        /// a "Thread was being aborted" error flash. The response body is
        /// suppressed and the pipeline is completed instead.
        /// </summary>
        protected static void Redirect(string url)
        {
            HttpResponse response = HttpContext.Current.Response;
            response.Redirect(url, false);
            response.SuppressContent = true;
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }

        // ---------------- CSRF (per-request hidden token) ----------------

        protected string Csrf()
        {
            string token = Session["AntiForgeryToken"] as string;
            if (string.IsNullOrEmpty(token)) token = RandomString(24);
            Session["AntiForgeryToken"] = token;
            return token;
        }

        protected string CsrfField()
        {
            return "<input type='hidden' name='__csrf' value='" + Attr(Csrf()) + "'/>";
        }

        private void EnsureCsrfValid()
        {
            string sent = Request.Form["__csrf"] ?? Request.QueryString["__csrf"];
            string stored = Session["AntiForgeryToken"] as string;
            if (string.IsNullOrEmpty(stored) || sent != stored)
            {
                Response.StatusCode = 403;
                Response.ContentType = "text/html";
                Response.Write("<html><body style='font-family:sans-serif;padding:40px'><h1 style='color:#1a2949'>403</h1><p>Huh. Anti-forgery token mismatch. Return to the page and try again.</p></body></html>");
                Response.End();
            }
        }

        // ---------------- small render helpers used by lists ----------------

        /// <summary>Renders &lt;option&gt; entries. selectedValue matched as string.</summary>
        protected string Options(string valueField, string textField, System.Data.DataTable table, string selected)
        {
            var sb = new StringBuilder();
            foreach (System.Data.DataRow row in table.Rows)
            {
                string val = row[valueField] == DBNull.Value ? "" : row[valueField].ToString();
                string txt = row[textField] == DBNull.Value ? val : row[textField].ToString();
                sb.Append("<option value='").Append(Attr(val)).Append("'")
                  .Append(val == selected ? " selected='selected'" : "")
                  .Append(">").Append(E(txt)).Append("</option>");
            }
            return sb.ToString();
        }

        protected static string RandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var bytes = new byte[length];
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                rng.GetBytes(bytes);
            }
            var sb = new StringBuilder(length);
            foreach (byte b in bytes) sb.Append(chars[b % chars.Length]);
            return sb.ToString();
        }

        protected override void OnError(EventArgs e)
        {
            AuditService.LogSystemError(Server.GetLastError(), HttpContext.Current?.Request?.Path ?? "");
            base.OnError(e);
        }
    }
}
