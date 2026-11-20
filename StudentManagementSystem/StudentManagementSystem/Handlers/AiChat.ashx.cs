using System;
using System.Collections.Generic;
using System.Data;
using System.Web;
using System.Web.SessionState;
using StudentManagementSystem.Ai;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>
    /// One assistant turn. POST conversationId, message, page, pageTitle,
    /// context (and optional reuse=1 for regenerate). The conversation is
    /// always looked up with the signed-in UserId; a foreign id is a 404.
    /// </summary>
    public class AiChat : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            if (!AiHttp.Guard(ctx, true)) return;
            ctx.Response.ContentType = "application/json; charset=utf-8";

            UserPrefs prefs = PrefsRepo.Get(CurrentUser.UserId);
            if (!prefs.AiEnabled)
            {
                AiHttp.Write(ctx, AiHttp.Dict("ok", false, "error", "The assistant is switched off in your settings. Enable it under Settings > AI assistant."));
                return;
            }
            if (!AiService.MasterEnabled())
            {
                AiHttp.Write(ctx, AiHttp.Dict("ok", false, "error", "The assistant is disabled on this installation."));
                return;
            }

            int conversationId;
            int.TryParse(ctx.Request.Form["conversationId"], out conversationId);
            bool reuse = ctx.Request.Form["reuse"] == "1";
            string message = AiHttp.Str(ctx, "message", 2000);
            string page = AiHttp.Str(ctx, "page", 200);
            string pageTitle = AiHttp.Str(ctx, "pageTitle", 120);
            string clientContext = AiHttp.Str(ctx, "context", 2400);

            if (conversationId > 0 && AiRepo.Get(CurrentUser.UserId, conversationId) == null)
            {
                ctx.Response.StatusCode = 404;
                AiHttp.Write(ctx, AiHttp.Dict("ok", false, "error", "That conversation does not belong to your account."));
                return;
            }

            if (reuse)
            {
                if (conversationId <= 0)
                {
                    AiHttp.Write(ctx, AiHttp.Dict("ok", false, "error", "There is no previous question to regenerate."));
                    return;
                }
                List<AiMessage> previous = AiRepo.Messages(CurrentUser.UserId, conversationId, 20);
                string lastUser = null;
                foreach (AiMessage m in previous) if (m.Role == "user") lastUser = m.Content;
                if (string.IsNullOrEmpty(lastUser))
                {
                    AiHttp.Write(ctx, AiHttp.Dict("ok", false, "error", "There is no previous question to regenerate."));
                    return;
                }
                message = lastUser;
            }

            if (message.Length == 0)
            {
                AiHttp.Write(ctx, AiHttp.Dict("ok", false, "error", "Type a question first."));
                return;
            }

            try
            {
                if (conversationId <= 0)
                {
                    conversationId = AiRepo.Create(CurrentUser.UserId, message);
                    AiRepo.TitleIfDefault(CurrentUser.UserId, conversationId, message);
                }
                AiConversation conv = AiRepo.Get(CurrentUser.UserId, conversationId);
                if (conv == null)
                {
                    ctx.Response.StatusCode = 404;
                    AiHttp.Write(ctx, AiHttp.Dict("ok", false, "error", "Conversation not found."));
                    return;
                }

                int userMessageId = 0;
                if (!reuse)
                {
                    userMessageId = AiRepo.AddMessage(conversationId, "user", message, null);
                    AiRepo.TitleIfDefault(CurrentUser.UserId, conversationId, message);
                }

                // History: last 10 messages, current question included, each capped.
                List<AiMessage> history = AiRepo.Messages(CurrentUser.UserId, conversationId, 12);
                var turns = new List<AiTurn>();
                string facts;
                string system = AiContext.BuildPrompt(CurrentUser.FullName, CurrentUser.RoleName, page, pageTitle,
                    prefs.AiContext ? clientContext : "(page context disabled by the user)",
                    message, CurrentUser.UserId, prefs.AiResponseLength, out facts);
                turns.Add(new AiTurn("system", system));
                foreach (AiMessage m in history)
                {
                    string text = m.Content ?? "";
                    if (text.Length > 1600) text = text.Substring(0, 1600) + "...";
                    turns.Add(new AiTurn(m.Role == "user" ? "user" : "assistant", text));
                }

                AiResult result = AiService.Complete(turns, AiContext.MaxTokens(prefs.AiResponseLength), 180);
                if (!result.Ok)
                {
                    AiHttp.Write(ctx, AiHttp.Dict(
                        "ok", false,
                        "error", result.Error,
                        "conversationId", conversationId,
                        "userMessageId", userMessageId,
                        "elapsedMs", result.ElapsedMs));
                    return;
                }

                string meta = "{\"model\":\"Ektome-Qwen1.5-0.5B-Q2_K\",\"ms\":" + result.ElapsedMs +
                              ",\"prompt\":" + result.PromptTokens + ",\"completion\":" + result.CompletionTokens + "}";
                int assistantId = AiRepo.AddMessage(conversationId, "assistant", result.Text, meta);

                AiConversation updated = AiRepo.Get(CurrentUser.UserId, conversationId);
                AiHttp.Write(ctx, AiHttp.Dict(
                    "ok", true,
                    "conversationId", conversationId,
                    "title", updated != null ? updated.Title : "",
                    "userMessageId", userMessageId,
                    "assistantMessageId", assistantId,
                    "answer", result.Text,
                    "elapsedMs", result.ElapsedMs,
                    "promptTokens", result.PromptTokens,
                    "completionTokens", result.CompletionTokens));
            }
            catch (Exception ex)
            {
                Global.WriteLog("AiChat", "turn failed", ex);
                AiHttp.Write(ctx, AiHttp.Dict("ok", false, "error", "The assistant could not complete this turn: " + SqlHelper.FriendlyError(ex).Message));
            }
        }
    }
}