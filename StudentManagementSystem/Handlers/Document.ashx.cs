using System;
using System.Data;
using System.IO;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>Private document download. Owner or administrator only.</summary>
    public class Document : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext context)
        {
            if (!CurrentUser.IsAuthenticated) { context.Response.StatusCode = 401; return; }
            int docId;
            if (!int.TryParse(context.Request.QueryString["doc"], out docId) || docId <= 0) { context.Response.StatusCode = 400; return; }

            DataTable dt = SqlHelper.FillDataTable(
                "SELECT UserId, FileName, ContentType, StoredName FROM dbo.UserDocuments WHERE DocId=@Id AND IsDeleted=0",
                p => p.AddWithValue("@Id", docId));
            if (dt.Rows.Count == 0) { context.Response.StatusCode = 404; return; }

            int ownerId = Convert.ToInt32(dt.Rows[0]["UserId"]);
            if (!CurrentUser.IsInRole("Admin") && CurrentUser.UserId != ownerId) { context.Response.StatusCode = 403; return; }

            string stored = Convert.ToString(dt.Rows[0]["StoredName"]);
            if (stored.IndexOfAny(new[] { '/', '\\', ':' }) >= 0) { context.Response.StatusCode = 400; return; }
            string path = context.Server.MapPath("~/App_Data/documents/" + ownerId + "/" + stored);
            if (!File.Exists(path)) { context.Response.StatusCode = 404; return; }

            context.Response.Clear();
            context.Response.ContentType = Convert.ToString(dt.Rows[0]["ContentType"] ?? "application/octet-stream");
            context.Response.AddHeader("Content-Disposition", "attachment; filename=\"" + Convert.ToString(dt.Rows[0]["FileName"]).Replace("\"", "") + "\"");
            context.Response.AddHeader("X-Content-Type-Options", "nosniff");
            context.Response.TransmitFile(path);
        }
    }
}
