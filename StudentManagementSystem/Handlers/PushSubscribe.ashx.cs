using System;
using System.Web;
using System.Web.Script.Serialization;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>
    /// Stores / removes a browser Web Push subscription for the signed-in user.
    /// Only reachable to authenticated users; the client is /Content/js/push.js.
    /// </summary>
    public class PushSubscribe : IHttpHandler, System.Web.SessionState.IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            var res = ctx.Response;
            res.ContentType = "application/json; charset=utf-8";
            res.Cache.SetCacheability(HttpCacheability.NoCache);

            if (!CurrentUser.IsAuthenticated)
            {
                res.StatusCode = 401;
                res.Write("{\"ok\":false,\"error\":\"sign in first\"}");
                return;
            }
            if (!AppConfig.PushEnabled)
            {
                res.StatusCode = 503;
                res.Write("{\"ok\":false,\"error\":\"push is disabled on this server\"}");
                return;
            }
            if (ctx.Request.HttpMethod != "POST")
            {
                res.StatusCode = 405;
                res.Write("{\"ok\":false,\"error\":\"POST only\"}");
                return;
            }

            try
            {
                string body;
                using (var reader = new System.IO.StreamReader(ctx.Request.InputStream))
                    body = reader.ReadToEnd();
                if (body.Length == 0 || body.Length > 4000)
                {
                    res.StatusCode = 400;
                    res.Write("{\"ok\":false,\"error\":\"invalid payload\"}");
                    return;
                }

                var json = new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string, object>>(body);
                string endpoint = json.ContainsKey("endpoint") ? json["endpoint"] as string : null;
                string p256dh = null, auth = null;
                if (json.ContainsKey("keys") && json["keys"] is System.Collections.Generic.Dictionary<string, object>)
                {
                    var keys = (System.Collections.Generic.Dictionary<string, object>)json["keys"];
                    if (keys.ContainsKey("p256dh")) p256dh = keys["p256dh"] as string;
                    if (keys.ContainsKey("auth")) auth = keys["auth"] as string;
                }

                if (string.IsNullOrWhiteSpace(endpoint) || !endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    || endpoint.Length > 500 || string.IsNullOrWhiteSpace(p256dh) || string.IsNullOrWhiteSpace(auth))
                {
                    res.StatusCode = 400;
                    res.Write("{\"ok\":false,\"error\":\"invalid subscription\"}");
                    return;
                }

                string op = ctx.Request.QueryString["op"] ?? "subscribe";
                if (op == "unsubscribe") CommsRepo.DeletePush(CurrentUser.UserId, endpoint);
                else CommsRepo.UpsertPush(CurrentUser.UserId, endpoint, p256dh, auth,
                        (ctx.Request.UserAgent ?? "").Length > 190 ? ctx.Request.UserAgent.Substring(0, 190) : ctx.Request.UserAgent);

                res.Write("{\"ok\":true}");
            }
            catch (Exception ex)
            {
                Global.WriteLog("Push", "subscribe failed", ex);
                res.StatusCode = 400;
                res.Write("{\"ok\":false,\"error\":\"could not store subscription\"}");
            }
        }
    }
}
