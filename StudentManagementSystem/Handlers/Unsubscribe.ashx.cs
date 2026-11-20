using System;
using System.Data;
using System.Web;
using StudentManagementSystem.Data;

namespace StudentManagementSystem.Handlers
{
    /// <summary>One-click bulk-mail unsubscribe. Token is random per recipient.</summary>
    public class Unsubscribe : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            string token = (ctx.Request.QueryString["t"] ?? "").Trim();
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.Write("<html><head><meta name='viewport' content='width=device-width,initial-scale=1'/><title>Unsubscribe</title></head><body style='font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:60px auto;padding:24px;color:#222'>");
            if (token.Length < 10)
            {
                ctx.Response.Write("<h1 style='font-size:20px'>Invalid link</h1><p>This unsubscribe link is incomplete.</p>");
            }
            else
            {
                DataTable dt = SqlHelper.FillDataTable(
                    "SELECT TOP 1 Email FROM dbo.MailRecipients WHERE UnsubscribeToken=@T",
                    p => p.AddWithValue("@T", token));
                if (dt.Rows.Count == 0)
                    ctx.Response.Write("<h1 style='font-size:20px'>Already handled</h1><p>This address is not subscribed to bulk mail.</p>");
                else
                {
                    string email = Convert.ToString(dt.Rows[0]["Email"]);
                    MailRepo.Suppress(email, "unsubscribe link");
                    ctx.Response.Write("<h1 style='font-size:20px'>Unsubscribed</h1><p>" +
                        HttpUtility.HtmlEncode(email) + " will no longer receive bulk mail. Essential account email is unaffected.</p>");
                }
            }
            ctx.Response.Write("</body></html>");
        }
    }
}
