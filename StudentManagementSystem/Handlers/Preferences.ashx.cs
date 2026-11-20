using System;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>
    /// Persists theme / text-size / motion for the signed-in user and always
    /// refreshes the anonymous cookie mirror. Values are validated against the
    /// same allow-lists the settings page uses, so a hand-crafted request cannot
    /// store an out-of-range scale or an unknown theme.
    /// </summary>
    public class Preferences : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.Cache.SetCacheability(HttpCacheability.NoCache);

            if (!string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = 405;
                ctx.Response.Write("{\"ok\":false}");
                return;
            }

            HttpRequest req = ctx.Request;
            string theme = PrefsRepo.NormalizeTheme(req.Form["theme"] ?? ThemeFromCookie(ctx));
            int font = 100;
            int parsed;
            if (int.TryParse(req.Form["font"], out parsed)) font = PrefsRepo.NormalizeScale(parsed);
            else
            {
                var c = ctx.Request.Cookies[PrefsRepo.CookieFont];
                if (c != null && int.TryParse(c.Value, out parsed)) font = PrefsRepo.NormalizeScale(parsed);
            }
            string motion = PrefsRepo.NormalizeMotion(req.Form["motion"] ?? MotionFromCookie(ctx));
            int zoom = 0;
            if (int.TryParse(req.Form["zoom"], out parsed)) zoom = PrefsRepo.NormalizeZoom(parsed);
            else
            {
                var zc = ctx.Request.Cookies[PrefsRepo.CookieZoom];
                if (zc != null && int.TryParse(zc.Value, out parsed)) zoom = PrefsRepo.NormalizeZoom(parsed);
            }
            string clock = (req.Form["clock"] ?? "").Trim();

            PrefsRepo.WriteCookie(ctx, theme, font, motion, zoom);

            if (CurrentUser.IsAuthenticated)
            {
                try
                {
                    PrefsRepo.SaveAppearance(CurrentUser.UserId, theme, font, motion);
                    if (zoom > 0) PrefsRepo.SaveZoom(CurrentUser.UserId, zoom);
                    if (clock.Length > 0)
                    {
                        UserPrefs prefs = PrefsRepo.Get(CurrentUser.UserId);
                        PrefsRepo.SaveRegional(CurrentUser.UserId, prefs.TimezoneId, clock, prefs.ReminderLeadMinutes);
                    }
                }
                catch (Exception)
                {
                    // The cookie still holds the choice; report so the client can retry later.
                    ctx.Response.StatusCode = 200;
                    ctx.Response.Write("{\"ok\":false,\"stored\":\"cookie\"}");
                    return;
                }
            }

            ctx.Response.Write("{\"ok\":true,\"theme\":\"" + theme + "\",\"font\":" + font +
                               ",\"motion\":\"" + motion + "\",\"zoom\":" + (zoom > 0 ? zoom : 100) +
                               ",\"clock\":\"" + PrefsRepo.NormalizeClockFormat(clock.Length > 0 ? clock : "24") + "\"}");
        }

        private static string ThemeFromCookie(HttpContext ctx)
        {
            var c = ctx.Request.Cookies[PrefsRepo.CookieTheme];
            return c == null ? "light" : c.Value;
        }

        private static string MotionFromCookie(HttpContext ctx)
        {
            var c = ctx.Request.Cookies[PrefsRepo.CookieMotion];
            return c == null ? "full" : c.Value;
        }
    }
}
