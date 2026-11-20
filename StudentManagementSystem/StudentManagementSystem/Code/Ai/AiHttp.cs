using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Script.Serialization;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Ai
{
    /// <summary>Small JSON/CSRF helpers shared by the AI endpoints.</summary>
    public static class AiHttp
    {
        public static bool Guard(HttpContext ctx, bool postOnly)
        {
            if (!CurrentUser.IsAuthenticated)
            {
                ctx.Response.StatusCode = 401;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.Write("{\"ok\":false,\"error\":\"Sign in to use the assistant.\"}");
                return false;
            }
            if (postOnly && !string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = 405;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.Write("{\"ok\":false,\"error\":\"POST required.\"}");
                return false;
            }
            if (postOnly)
            {
                string sent = ctx.Request.Headers["X-CSRF"] ?? ctx.Request.Form["__csrf"];
                string stored = ctx.Session == null ? null : ctx.Session["AntiForgeryToken"] as string;
                if (string.IsNullOrEmpty(stored) || sent != stored)
                {
                    ctx.Response.StatusCode = 403;
                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    ctx.Response.Write("{\"ok\":false,\"error\":\"Your session token expired. Reload the page.\"}");
                    return false;
                }
            }
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            return true;
        }

        public static void Write(HttpContext ctx, object value)
        {
            var ser = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
            ctx.Response.Write(ser.Serialize(value));
        }

        public static Dictionary<string, object> Dict(params object[] pairs)
        {
            var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                d[Convert.ToString(pairs[i])] = pairs[i + 1];
            return d;
        }

        public static string Str(HttpContext ctx, string name, int max)
        {
            string v = (ctx.Request.Form[name] ?? "").Trim();
            return v.Length > max ? v.Substring(0, max) : v;
        }
    }
}