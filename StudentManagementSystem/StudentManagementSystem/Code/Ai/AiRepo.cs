using System;
using System.Collections.Generic;
using System.Data;
using StudentManagementSystem.Data;

namespace StudentManagementSystem.Ai
{
    public class AiConversation
    {
        public int ConversationId;
        public string Title;
        public bool IsPinned;
        public DateTime CreatedAtUtc;
        public DateTime LastActivityUtc;
        public int MessageCount;
    }

    public class AiMessage
    {
        public int MessageId;
        public string Role;
        public string Content;
        public DateTime CreatedAtUtc;
        public string MetaJson;
    }

    /// <summary>
    /// Local AI conversation storage. Every query is pegged to the owning
    /// UserId: a guessed conversation id from another account simply returns
    /// null, so the assistant can never read or delete someone else's chats.
    /// No credentials or password material is stored here.
    /// </summary>
    public static class AiRepo
    {
        public static List<AiConversation> List(int userId, string search, int limit)
        {
            int take = limit <= 0 ? 60 : Math.Min(limit, 200);
            string term = (search ?? "").Trim();
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP (" + take + ") c.ConversationId, c.Title, c.IsPinned, c.CreatedAtUtc, c.LastActivityUtc, " +
                "(SELECT COUNT(*) FROM dbo.AiMessages m WHERE m.ConversationId = c.ConversationId) AS MessageCount " +
                "FROM dbo.AiConversations c WHERE c.UserId = @UserId " +
                "AND (@Term = '' OR c.Title LIKE '%' + @Term + '%' OR EXISTS " +
                "   (SELECT 1 FROM dbo.AiMessages m2 WHERE m2.ConversationId = c.ConversationId AND m2.Content LIKE '%' + @Term + '%')) " +
                "ORDER BY c.IsPinned DESC, c.LastActivityUtc DESC",
                p =>
                {
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@Term", term);
                });

            var list = new List<AiConversation>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new AiConversation
                {
                    ConversationId = Convert.ToInt32(r["ConversationId"]),
                    Title = r["Title"] as string ?? "Conversation",
                    IsPinned = Convert.ToBoolean(r["IsPinned"]),
                    CreatedAtUtc = Convert.ToDateTime(r["CreatedAtUtc"]),
                    LastActivityUtc = Convert.ToDateTime(r["LastActivityUtc"]),
                    MessageCount = Convert.ToInt32(r["MessageCount"])
                });
            }
            return list;
        }

        public static AiConversation Get(int userId, int conversationId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT ConversationId, Title, IsPinned, CreatedAtUtc, LastActivityUtc FROM dbo.AiConversations " +
                "WHERE ConversationId = @Id AND UserId = @UserId",
                p => { p.AddWithValue("@Id", conversationId); p.AddWithValue("@UserId", userId); });
            if (dt.Rows.Count == 0) return null;
            DataRow r = dt.Rows[0];
            return new AiConversation
            {
                ConversationId = Convert.ToInt32(r["ConversationId"]),
                Title = r["Title"] as string ?? "Conversation",
                IsPinned = Convert.ToBoolean(r["IsPinned"]),
                CreatedAtUtc = Convert.ToDateTime(r["CreatedAtUtc"]),
                LastActivityUtc = Convert.ToDateTime(r["LastActivityUtc"])
            };
        }

        public static int Create(int userId, string title)
        {
            string clean = CleanTitle(title);
            return Convert.ToInt32(SqlHelper.ExecuteScalar(
                "INSERT INTO dbo.AiConversations (UserId, Title) VALUES (@UserId, @Title); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                p => { p.AddWithValue("@UserId", userId); p.AddWithValue("@Title", clean); }));
        }

        public static bool Rename(int userId, int conversationId, string title)
        {
            string clean = CleanTitle(title);
            int rows = SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.AiConversations SET Title = @Title, LastActivityUtc = SYSUTCDATETIME() " +
                "WHERE ConversationId = @Id AND UserId = @UserId",
                p => { p.AddWithValue("@Title", clean); p.AddWithValue("@Id", conversationId); p.AddWithValue("@UserId", userId); });
            return rows > 0;
        }

        public static bool SetPinned(int userId, int conversationId, bool pinned)
        {
            int rows = SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.AiConversations SET IsPinned = @Pin WHERE ConversationId = @Id AND UserId = @UserId",
                p => { p.AddWithValue("@Pin", pinned ? 1 : 0); p.AddWithValue("@Id", conversationId); p.AddWithValue("@UserId", userId); });
            return rows > 0;
        }

        public static bool Delete(int userId, int conversationId)
        {
            int rows = SqlHelper.ExecuteNonQuery(
                "DELETE FROM dbo.AiConversations WHERE ConversationId = @Id AND UserId = @UserId",
                p => { p.AddWithValue("@Id", conversationId); p.AddWithValue("@UserId", userId); });
            return rows > 0;
        }

        public static bool Clear(int userId, int conversationId)
        {
            if (Get(userId, conversationId) == null) return false;
            SqlHelper.ExecuteNonQuery(
                "DELETE FROM dbo.AiMessages WHERE ConversationId = @Id",
                p => p.AddWithValue("@Id", conversationId));
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.AiConversations SET LastActivityUtc = SYSUTCDATETIME() WHERE ConversationId = @Id AND UserId = @UserId",
                p => { p.AddWithValue("@Id", conversationId); p.AddWithValue("@UserId", userId); });
            return true;
        }

        public static List<AiMessage> Messages(int userId, int conversationId, int limit)
        {
            if (Get(userId, conversationId) == null) return new List<AiMessage>();
            int take = limit <= 0 ? 60 : Math.Min(limit, 200);
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP (" + take + ") MessageId, Role, Content, CreatedAtUtc, MetaJson FROM (" +
                " SELECT TOP (" + take + ") MessageId, Role, Content, CreatedAtUtc, MetaJson " +
                " FROM dbo.AiMessages WHERE ConversationId = @Id ORDER BY MessageId DESC) t ORDER BY MessageId ASC",
                p => p.AddWithValue("@Id", conversationId));

            var list = new List<AiMessage>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new AiMessage
                {
                    MessageId = Convert.ToInt32(r["MessageId"]),
                    Role = r["Role"] as string ?? "assistant",
                    Content = r["Content"] as string ?? "",
                    CreatedAtUtc = Convert.ToDateTime(r["CreatedAtUtc"]),
                    MetaJson = r["MetaJson"] as string
                });
            }
            return list;
        }

        /// <summary>The last N turns, oldest first, for the model prompt.</summary>
        public static List<AiMessage> RecentForPrompt(int userId, int conversationId, int turns)
        {
            var all = Messages(userId, conversationId, turns * 2);
            return all;
        }

        public static int AddMessage(int conversationId, string role, string content, string metaJson)
        {
            int id = Convert.ToInt32(SqlHelper.ExecuteScalar(
                "INSERT INTO dbo.AiMessages (ConversationId, Role, Content, MetaJson) VALUES (@Id, @Role, @Content, @Meta); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                p =>
                {
                    p.AddWithValue("@Id", conversationId);
                    p.AddWithValue("@Role", role == "user" ? "user" : "assistant");
                    p.AddWithValue("@Content", content ?? "");
                    p.AddWithValue("@Meta", string.IsNullOrEmpty(metaJson) ? (object)DBNull.Value : metaJson);
                }));
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.AiConversations SET LastActivityUtc = SYSUTCDATETIME() WHERE ConversationId = @Id",
                p => p.AddWithValue("@Id", conversationId));
            return id;
        }

        /// <summary>Names a new conversation after its first question.</summary>
        public static void TitleIfDefault(int userId, int conversationId, string firstQuestion)
        {
            string title = CleanTitle(firstQuestion);
            if (title.Length == 0) return;
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.AiConversations SET Title = @Title WHERE ConversationId = @Id AND UserId = @UserId " +
                "AND (Title = N'New conversation' OR Title = N'' OR Title IS NULL)",
                p => { p.AddWithValue("@Title", title); p.AddWithValue("@Id", conversationId); p.AddWithValue("@UserId", userId); });
        }

        public static int CountForUser(int userId)
        {
            try
            {
                return SqlHelper.ExecuteScalarInt(
                    "SELECT COUNT(*) FROM dbo.AiConversations WHERE UserId = @UserId",
                    p => p.AddWithValue("@UserId", userId));
            }
            catch (Exception) { return 0; }
        }

        public static void DeleteAllForUser(int userId)
        {
            SqlHelper.ExecuteNonQuery("DELETE FROM dbo.AiConversations WHERE UserId = @UserId",
                p => p.AddWithValue("@UserId", userId));
        }

        private static string CleanTitle(string raw)
        {
            string t = (raw ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            if (t.Length == 0) return "New conversation";
            return t.Length > 70 ? t.Substring(0, 67) + "..." : t;
        }
    }
}