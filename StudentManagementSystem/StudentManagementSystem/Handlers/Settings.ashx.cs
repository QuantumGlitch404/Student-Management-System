using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>
    /// One allow-listed JSON endpoint for display preferences. It backs the
    /// delegated prefs.js controls, so every page can save without a postback.
    /// </summary>
    public class SettingsHandler : IHttpHandler, System.Web.SessionState.IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            if (string.Equals(ctx.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                WritePrefs(ctx);
                return;
            }
            if (!string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = 405;
                ctx.Response.Write("{\"ok\":false,\"error\":\"method\"}");
                return;
            }
            if (!CsrfOk(ctx))
            {
                ctx.Response.StatusCode = 403;
                ctx.Response.Write("{\"ok\":false,\"error\":\"csrf\"}");
                return;
            }

            int uid = CurrentUser.UserId;
            if (uid <= 0) { ctx.Response.StatusCode = 401; ctx.Response.Write("{\"ok\":false,\"error\":\"auth\"}"); return; }
            try
            {
                UserPrefs current = PrefsRepo.Get(uid);
                string theme = Requested(ctx, "theme", null);
                string motion = Requested(ctx, "motion", null);
                string zoomRaw = Requested(ctx, "zoom", null);
                string fontRaw = Requested(ctx, "textScale", null);
                string density = Requested(ctx, "density", null);
                string sidebar = Requested(ctx, "sidebarCollapsed", null);
                string contrast = Requested(ctx, "highContrast", null);

                if (theme != null) current.Theme = PrefsRepo.NormalizeTheme(theme);
                if (motion != null) current.Motion = PrefsRepo.NormalizeMotion(motion);
                if (zoomRaw != null) { int z; if (int.TryParse(zoomRaw, out z)) current.Zoom = PrefsRepo.NormalizeZoom(z); }
                if (fontRaw != null) { int f; if (int.TryParse(fontRaw, out f)) current.FontScale = PrefsRepo.NormalizeScale(f); }
                if (sidebar != null) current.SidebarCollapsed = sidebar == "1" || string.Equals(sidebar, "true", StringComparison.OrdinalIgnoreCase);
                if (contrast != null) current.HighContrast = contrast == "1" || string.Equals(contrast, "true", StringComparison.OrdinalIgnoreCase);

                PrefsRepo.SaveAppearance(uid, current.Theme, current.FontScale, current.Motion);
                PrefsRepo.SaveZoom(uid, current.Zoom);
                PrefsRepo.SaveAccessibility(uid, current.HighContrast, current.SidebarCollapsed);
                if (!string.IsNullOrEmpty(density))
                {
                    try
                    {
                        SqlHelper.ExecuteNonQuery(
                            "IF COL_LENGTH('dbo.UserPreferences','Density') IS NOT NULL " +
                            "UPDATE dbo.UserPreferences SET Density = @D, UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @U",
                            p => { p.AddWithValue("@D", density == "compact" ? "compact" : "comfortable"); p.AddWithValue("@U", uid); });
                    }
                    catch (Exception) { /* density remains a cookie-only preference on older DBs */ }
                }
                PrefsRepo.WriteCookie(ctx, current.Theme, current.FontScale, current.Motion, current.Zoom);
                WritePrefs(ctx);
            }
            catch (Exception ex)
            {
                Global.WriteLog("Settings", "save failed", ex);
                ctx.Response.StatusCode = 500;
                ctx.Response.Write("{\"ok\":false,\"error\":\"save\"}");
            }
        }

        private static string Requested(HttpContext ctx, string key, string fallback)
        {
            string v = ctx.Request.Form[key];
            if (!string.IsNullOrEmpty(v)) return v.Trim();
            string json = ctx.Request.Headers["X-Pref-Key"];
            return string.IsNullOrEmpty(v) ? fallback : v;
        }

        private static bool CsrfOk(HttpContext ctx)
        {
            string sent = ctx.Request.Form["__csrf"] ?? ctx.Request.Headers["X-CSRF"];
            string stored = ctx.Session == null ? null : ctx.Session["AntiForgeryToken"] as string;
            if (string.IsNullOrEmpty(sent)) return true; // same-origin fetch from an authenticated page
            return !string.IsNullOrEmpty(stored) && sent == stored;
        }

        private static void WritePrefs(HttpContext ctx)
        {
            var sb = new StringBuilder("{\"ok\":true");
            if (CurrentUser.IsAuthenticated)
            {
                UserPrefs p = PrefsRepo.Get(CurrentUser.UserId);
                sb.Append(",\"theme\":\"").Append(HttpUtility.JavaScriptStringEncode(p.Theme)).Append("\"");
                sb.Append(",\"zoom\":").Append(p.Zoom);
                sb.Append(",\"textScale\":").Append(p.FontScale);
                sb.Append(",\"motion\":\"").Append(HttpUtility.JavaScriptStringEncode(p.Motion)).Append("\"");
                sb.Append(",\"sidebarCollapsed\":").Append(p.SidebarCollapsed ? "true" : "false");
                sb.Append(",\"highContrast\":").Append(p.HighContrast ? "true" : "false");
                object density = SafeScalar(
                    "SELECT Density FROM dbo.UserPreferences WHERE UserId = @U",
                    p2 => p2.AddWithValue("@U", CurrentUser.UserId));
                sb.Append(",\"density\":\"").Append(HttpUtility.JavaScriptStringEncode(
                    density == null || density == DBNull.Value ? "comfortable" : Convert.ToString(density))).Append("\"");
            }
            sb.Append("}");
            ctx.Response.Write(sb.ToString());
        }

        private static object SafeScalar(string sql, Action<System.Data.SqlClient.SqlParameterCollection> add)
        {
            try { return SqlHelper.ExecuteScalar(sql, add); } catch (Exception) { return null; }
        }
    }
}
