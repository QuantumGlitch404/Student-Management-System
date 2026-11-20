using System;
using System.Web;
using System.Web.SessionState;
using StudentManagementSystem.Ai;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>Read-only model/runtime status used by the chat header and settings.</summary>
    public class AiStatus : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            if (!AiHttp.Guard(ctx, false)) return;
            AiService.Touch();
            AiStatusInfo s = AiService.Status();
            // Lazy start: the status poll pre-warms the local runtime so the
            // first real question does not pay the whole model load time.
            if (s.Enabled && s.ModelPresent && s.RuntimePresent && !s.Ready)
            {
                string startError;
                AiService.EnsureReady(1500, out startError);
                s = AiService.Status();
            }
            AiHttp.Write(ctx, AiHttp.Dict(
                "ok", true,
                "enabled", s.Enabled,
                "modelPresent", s.ModelPresent,
                "runtimePresent", s.RuntimePresent,
                "ready", s.Ready,
                "state", s.State,
                "model", s.ModelName,
                "modelBytes", s.ModelBytes,
                "runtime", s.RuntimeVersion,
                "port", s.Port,
                "error", s.Error,
                "conversations", AiRepo.CountForUser(CurrentUser.UserId)));
        }
    }
}