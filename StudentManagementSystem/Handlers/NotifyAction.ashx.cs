using System;
using System.Web;
using System.Web.SessionState;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>
    /// State-changing notification actions (mark one / mark all read) from the
    /// bell panel. POST only, CSRF token checked against the session, and every
    /// update is scoped to CurrentUser.UserId, so a user can never change another
    /// user's read status.
    /// </summary>
    public class NotifyAction : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.Cache.SetCacheability(HttpCacheability.NoCache);

            if (!CurrentUser.IsAuthenticated)
            {
                ctx.Response.StatusCode = 401;
                ctx.Response.Write("{\"ok\":false,\"error\":\"auth\"}");
                return;
            }

            if (!string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = 405;
                ctx.Response.Write("{\"ok\":false,\"error\":\"method\"}");
                return;
            }

            string sent = ctx.Request.Form["__csrf"];
            string stored = ctx.Session == null ? null : ctx.Session["AntiForgeryToken"] as string;
            if (string.IsNullOrEmpty(stored) || !string.Equals(sent, stored, StringComparison.Ordinal))
            {
                ctx.Response.StatusCode = 403;
                ctx.Response.Write("{\"ok\":false,\"error\":\"csrf\"}");
                return;
            }

            int userId = CurrentUser.UserId;
            string op = (ctx.Request.Form["op"] ?? "").Trim();
            try
            {
                switch (op)
                {
                    case "markall":
                        CommsRepo.MarkAllRead(userId);
                        break;
                    case "markone":
                    case "snooze":
                    case "dismiss":
                        {
                            int id;
                            if (!int.TryParse(ctx.Request.Form["id"], out id) || id <= 0)
                            {
                                ctx.Response.StatusCode = 400;
                                ctx.Response.Write("{\"ok\":false,\"error\":\"id\"}");
                                return;
                            }
                            if (op == "markone") CommsRepo.MarkRead(id, userId);
                            else if (op == "snooze")
                            {
                                int minutes;
                                if (!int.TryParse(ctx.Request.Form["minutes"], out minutes)) minutes = 60;
                                CommsRepo.Snooze(id, userId, minutes);
                            }
                            else CommsRepo.DismissTicker(id, userId);
                            break;
                        }
                    default:
                        ctx.Response.StatusCode = 400;
                        ctx.Response.Write("{\"ok\":false,\"error\":\"op\"}");
                        return;
                }

                int unread = CommsRepo.UnreadCountVisible(userId, PrefsRepo.Get(userId));
                ctx.Response.Write("{\"ok\":true,\"unread\":" + unread + "}");
            }
            catch (Exception)
            {
                ctx.Response.StatusCode = 500;
                ctx.Response.Write("{\"ok\":false,\"error\":\"server\"}");
            }
        }
    }
}
