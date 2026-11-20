using System;
using System.Data;
using System.Web;
using System.Web.SessionState;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>Autosave and proctoring events for a student's live test attempt.</summary>
    public class OnlineTestAction : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.Cache.SetCacheability(HttpCacheability.NoCache);

            if (!CurrentUser.IsAuthenticated) { Fail(ctx, 401, "auth"); return; }
            if (!CurrentUser.IsInRole("Student")) { Fail(ctx, 403, "role"); return; }
            if (!string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)) { Fail(ctx, 405, "method"); return; }

            string sent = ctx.Request.Form["__csrf"];
            string stored = ctx.Session == null ? null : ctx.Session["AntiForgeryToken"] as string;
            if (string.IsNullOrEmpty(stored) || !string.Equals(sent, stored, StringComparison.Ordinal))
            {
                Fail(ctx, 403, "csrf");
                return;
            }

            DataRow student = OnlineTestsRepo.Student(CurrentUser.UserId);
            if (student == null) { Fail(ctx, 403, "student"); return; }
            int studentId = Convert.ToInt32(student["StudentId"]);

            int attemptId, questionId;
            if (!int.TryParse(ctx.Request.Form["attemptId"], out attemptId) || attemptId <= 0)
            {
                Fail(ctx, 400, "attempt");
                return;
            }

            string op = (ctx.Request.Form["op"] ?? "").Trim();
            try
            {
                if (op == "save")
                {
                    if (!int.TryParse(ctx.Request.Form["questionId"], out questionId) || questionId <= 0)
                    {
                        Fail(ctx, 400, "question");
                        return;
                    }
                    bool saved = OnlineTestsRepo.SaveAnswer(attemptId, studentId, questionId, ctx.Request.Form["option"]);
                    if (!saved)
                    {
                        Fail(ctx, 409, "closed");
                        return;
                    }
                    ctx.Response.Write("{\"ok\":true,\"saved\":true}");
                    return;
                }

                if (op == "event")
                {
                    string type = (ctx.Request.Form["type"] ?? "unknown").Trim();
                    string detail = (ctx.Request.Form["detail"] ?? "").Trim();
                    ProctorResult result = OnlineTestsRepo.ProctorEvent(attemptId, studentId, type, detail);
                    ctx.Response.Write("{\"ok\":true,\"violations\":" + result.Violations +
                                       ",\"warningLimit\":" + result.WarningLimit +
                                       ",\"action\":\"" + result.Action + "\"" +
                                       ",\"autoSubmitted\":" + (result.AutoSubmitted ? "true" : "false") + "}");
                    return;
                }

                Fail(ctx, 400, "op");
            }
            catch (Exception)
            {
                Fail(ctx, 500, "server");
            }
        }

        private static void Fail(HttpContext ctx, int status, string error)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.Write("{\"ok\":false,\"error\":\"" + error + "\"}");
        }
    }
}
