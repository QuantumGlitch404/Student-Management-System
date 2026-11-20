using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>
    /// "Download my data": the signed-in user's own profile, preferences and
    /// recent notifications as a JSON attachment. Deliberately scoped to
    /// CurrentUser.UserId - there is no id parameter to tamper with.
    /// </summary>
    public class MyData : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            if (!CurrentUser.IsAuthenticated)
            {
                ctx.Response.Redirect("~/Account/Login.aspx?ReturnUrl=" + HttpUtility.UrlEncode(ctx.Request.RawUrl), true);
                return;
            }

            int userId = CurrentUser.UserId;
            UserRecord me = UsersRepo.FindById(userId);
            if (me == null)
            {
                ctx.Response.StatusCode = 404;
                return;
            }

            UserPrefs prefs;
            try { prefs = PrefsRepo.Get(userId); } catch (Exception) { prefs = new UserPrefs { UserId = userId }; }
            DataTable notes;
            try { notes = CommsRepo.InboxVisible(userId, prefs, "all", "", 1, 50); }
            catch (Exception) { notes = new DataTable(); }

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"exportedAtUtc\": \"").Append(DateTime.UtcNow.ToString("o")).Append("Z\",\n");
            sb.Append("  \"profile\": {\n");
            sb.Append("    \"userId\": ").Append(me.UserId).Append(",\n");
            sb.Append("    \"fullName\": \"").Append(Js(me.FullName)).Append("\",\n");
            sb.Append("    \"email\": \"").Append(Js(me.Email)).Append("\",\n");
            sb.Append("    \"phone\": \"").Append(Js(me.Phone)).Append("\",\n");
            sb.Append("    \"role\": \"").Append(Js(me.RoleName)).Append("\",\n");
            sb.Append("    \"emailVerified\": ").Append(me.EmailVerified ? "true" : "false").Append(",\n");
            sb.Append("    \"twoFactorEnabled\": ").Append(me.TwoFactorEnabled ? "true" : "false").Append(",\n");
            sb.Append("    \"lastLoginAtUtc\": ").Append(me.LastLoginAtUtc.HasValue ? "\"" + me.LastLoginAtUtc.Value.ToString("o") + "Z\"" : "null").Append("\n");
            sb.Append("  },\n");
            sb.Append("  \"preferences\": {\n");
            sb.Append("    \"theme\": \"").Append(Js(prefs.Theme)).Append("\",\n");
            sb.Append("    \"fontScale\": ").Append(prefs.FontScale).Append(",\n");
            sb.Append("    \"motion\": \"").Append(Js(prefs.Motion)).Append("\",\n");
            sb.Append("    \"dateFormat\": \"").Append(Js(prefs.DateFormat)).Append("\",\n");
            sb.Append("    \"tablePageSize\": ").Append(prefs.TablePageSize).Append(",\n");
            sb.Append("    \"reportFormat\": \"").Append(Js(prefs.ReportFormat)).Append("\",\n");
            sb.Append("    \"disabledNotificationTypes\": \"").Append(Js(prefs.DisabledNotifTypes)).Append("\"\n");
            sb.Append("  },\n");
            sb.Append("  \"notifications\": [\n");
            bool first = true;
            foreach (DataRow r in notes.Rows)
            {
                if (!first) sb.Append(",\n");
                first = false;
                sb.Append("    {\"title\": \"").Append(Js(r["Title"]))
                  .Append("\", \"type\": \"").Append(Js(r["NType"]))
                  .Append("\", \"createdAtUtc\": \"").Append(Convert.ToDateTime(r["CreatedAtUtc"]).ToString("o")).Append("Z\"")
                  .Append(", \"read\": ").Append(Convert.ToBoolean(r["IsRead"]) ? "true" : "false").Append("}");
            }
            sb.Append("\n  ]\n}\n");

            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.AddHeader("Content-Disposition", "attachment; filename=\"sms-my-data-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".json\"");
            ctx.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            ctx.Response.Write(sb.ToString());
            AuditService.Log("PersonalDataExported", "Users", userId);
        }

        private static string Js(object value)
        {
            return HttpUtility.JavaScriptStringEncode(value == null ? "" : value.ToString());
        }
    }
}
