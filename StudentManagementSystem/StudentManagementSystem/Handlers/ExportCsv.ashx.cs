using System;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>Admin-only CSV exports: students | faculty | enrollments | payments | attendance | audit.</summary>
    public class ExportCsv : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            var res = ctx.Response;
            if (!CurrentUser.IsAuthenticated || CurrentUser.RoleName != "Admin")
            {
                res.StatusCode = 403;
                res.ContentType = "text/html";
                res.Write("<html><body style='font-family:sans-serif;padding:40px'><h1>403</h1><p>Admins only.</p></body></html>");
                return;
            }
            string scope = (ctx.Request.QueryString["scope"] ?? "").Trim().ToLowerInvariant();
            DataTable dt;
            switch (scope)
            {
                case "students": dt = StudentsRepo.Search("", null, 1); break;
                case "faculty": dt = StudentsRepo.FacultyList(); break;
                case "enrollments": dt = AcademicsRepo.EnrollmentList("", null); break;
                case "payments": dt = FinanceRepo.Payments(null, ""); break;
                case "attendance": dt = AttendanceRepo.Sessions(null, 2000); break;
                case "audit": dt = SqlHelper.FillDataTable(
                    "SELECT TOP 10000 AuditId, [Action], EntityTable, EntityId, DetailJson, ClientIp, " +
                    "u.FullName AS UserName, CreatedAtUtc FROM dbo.AuditLogs al " +
                    "LEFT JOIN dbo.Users u ON u.UserId = al.UserId ORDER BY al.AuditId DESC"); break;
                default:
                    res.StatusCode = 400;
                    res.ContentType = "text/html";
                    res.Write("<html><body style='font-family:sans-serif;padding:40px'><h1>400</h1><p>Unknown scope. Use students | faculty | enrollments | payments | attendance | audit.</p></body></html>");
                    return;
            }

            LogJob(scope);
            AuditService.Log("DataExport", "ExportJobs", null, scope);
            res.Buffer = true;
            res.ContentType = "text/csv; charset=utf-8";
            res.AddHeader("Content-Disposition",
                "attachment; filename=\"" + scope + "_" + DateTime.UtcNow.ToString("yyyyMMddHHmm") + ".csv\"");
            res.ContentEncoding = Encoding.UTF8;
            res.BinaryWrite(new byte[] { 0xEF, 0xBB, 0xBF }); // BOM so Excel reads UTF-8

            var html = new StringBuilder();
            foreach (DataColumn col in dt.Columns)
                html.Append(Csv(col.ColumnName)).Append(",");
            html.Append("\n");
            foreach (DataRow row in dt.Rows)
            {
                foreach (DataColumn col in dt.Columns)
                {
                    object v = row[col];
                    html.Append(Csv(v == DBNull.Value ? "" : v.ToString())).Append(",");
                }
                html.Append("\n");
            }
            res.Write(html.ToString());
        }

        private static string Csv(string v)
        {
            if (v == null) return "";
            v = v.Replace("\"", "\"\"");
            if (v.Contains(",") || v.Contains("\"") || v.Contains("\n") || v.Contains("\r")) return "\"" + v + "\"";
            return v;
        }

        private static void LogJob(string scope)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "IF OBJECT_ID('dbo.ExportJobs') IS NOT NULL " +
                    "INSERT INTO dbo.ExportJobs (RequestedBy, Scope, Format, RowsReturned) VALUES (@U, @T, 'CSV', 0)",
                    p => { p.AddWithValue("@T", scope); p.AddWithValue("@U", CurrentUser.UserId); });
            }
            catch (Exception) { /* job logging must never kill the export */ }
        }
    }
}
