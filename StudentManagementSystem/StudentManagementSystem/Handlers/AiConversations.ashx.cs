using System;
using System.Collections.Generic;
using System.Web;
using System.Web.SessionState;
using StudentManagementSystem.Ai;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>
    /// Conversation management: list/search (GET) and new/rename/pin/delete/
    /// clear (POST). Every operation is scoped to the signed-in user.
    /// </summary>
    public class AiConversations : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            bool isPost = string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase);
            if (!AiHttp.Guard(ctx, false)) return;

            if (!isPost)
            {
                string search = (ctx.Request.QueryString["q"] ?? "").Trim();
                var list = AiRepo.List(CurrentUser.UserId, search, 80);
                var items = new List<Dictionary<string, object>>();
                foreach (AiConversation c in list)
                {
                    items.Add(AiHttp.Dict(
                        "id", c.ConversationId,
                        "title", c.Title,
                        "pinned", c.IsPinned,
                        "messages", c.MessageCount,
                        "last", c.LastActivityUtc.ToString("o")));
                }
                AiHttp.Write(ctx, AiHttp.Dict("ok", true, "items", items));
                return;
            }

            string op = (ctx.Request.Form["op"] ?? "").Trim().ToLowerInvariant();
            int id;
            int.TryParse(ctx.Request.Form["id"], out id);
            string error = null;
            object payload = null;

            try
            {
                switch (op)
                {
                    case "new":
                        {
                            int newId = AiRepo.Create(CurrentUser.UserId, "New conversation");
                            payload = AiHttp.Dict("id", newId);
                            break;
                        }
                    case "rename":
                        {
                            string title = AiHttp.Str(ctx, "title", 100);
                            if (title.Length == 0) throw new Exception("A conversation title cannot be empty.");
                            if (!AiRepo.Rename(CurrentUser.UserId, id, title)) throw new Exception("Conversation not found.");
                            payload = AiHttp.Dict("title", AiRepo.Get(CurrentUser.UserId, id).Title);
                            break;
                        }
                    case "pin":
                        {
                            bool pinned = ctx.Request.Form["pinned"] == "1";
                            if (!AiRepo.SetPinned(CurrentUser.UserId, id, pinned)) throw new Exception("Conversation not found.");
                            payload = AiHttp.Dict("pinned", pinned);
                            break;
                        }
                    case "delete":
                        if (!AiRepo.Delete(CurrentUser.UserId, id)) throw new Exception("Conversation not found.");
                        payload = AiHttp.Dict("deleted", true);
                        break;
                    case "clear":
                        if (!AiRepo.Clear(CurrentUser.UserId, id)) throw new Exception("Conversation not found.");
                        payload = AiHttp.Dict("cleared", true);
                        break;
                    case "deleteall":
                        AiRepo.DeleteAllForUser(CurrentUser.UserId);
                        payload = AiHttp.Dict("deleted", true);
                        break;
                    default:
                        throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            if (error != null)
            {
                AiHttp.Write(ctx, AiHttp.Dict("ok", false, "error", error));
                return;
            }
            var result = AiHttp.Dict("ok", true);
            if (payload is Dictionary<string, object>)
                foreach (var kv in (Dictionary<string, object>)payload) result[kv.Key] = kv.Value;
            AiHttp.Write(ctx, result);
        }
    }
}