using System;
using System.Data;
using System.Text;
using System.Web;
using System.Web.Hosting;
using System.IO;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>Unauthenticated liveness probe: app, local SQL Server, migration count, optional sub-systems.</summary>
    public class Health : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            bool db = ConnectionManager.IsDatabaseReachable();
            int tables = 0, migrations = 0;
            string version = "unknown", dbName = "";
            if (db)
            {
                try
                {
                    tables = SqlHelper.ExecuteScalarInt("SELECT COUNT(*) FROM sys.tables");
                    dbName = Convert.ToString(SqlHelper.ExecuteScalar("SELECT DB_NAME()"));
                    try
                    {
                        migrations = SqlHelper.ExecuteScalarInt("SELECT COUNT(*) FROM dbo.SchemaMigrations");
                        version = Convert.ToString(SqlHelper.ExecuteScalar("SELECT TOP 1 ScriptName FROM dbo.SchemaMigrations ORDER BY MigrationId DESC"));
                    }
                    catch (Exception) { version = "SchemaMigrations missing"; }
                }
                catch (Exception) { /* keep liveness true; details remain empty */ }
            }
            var sb = new StringBuilder();
            sb.Append("{\"ok\":").Append(db ? "true" : "false");
            sb.Append(",\"app\":\"StudentManagementSystem\"");
            string build = AppConfig.Get("App:Build", "");
            if (string.IsNullOrWhiteSpace(build) || build == "dev")
            {
                try
                {
                    string stamp = HostingEnvironment.MapPath("~/BUILD-STAMP.txt");
                    if (!string.IsNullOrEmpty(stamp) && File.Exists(stamp)) build = File.ReadAllText(stamp).Trim();
                }
                catch (Exception) { }
            }
            if (string.IsNullOrWhiteSpace(build)) build = "dev";
            sb.Append(",\"build\":\"").Append(HttpUtility.JavaScriptStringEncode(build)).Append("\"");
            sb.Append(",\"database\":{\"reachable\":").Append(db ? "true" : "false");
            sb.Append(",\"name\":\"").Append(HttpUtility.JavaScriptStringEncode(dbName)).Append("\"");
            sb.Append(",\"tables\":").Append(tables);
            sb.Append(",\"migrations\":").Append(migrations);
            sb.Append(",\"latest\":\"").Append(HttpUtility.JavaScriptStringEncode(version ?? "")).Append("\"}");
            sb.Append(",\"timeUtc\":\"").Append(DateTime.UtcNow.ToString("o")).Append("\"");
            sb.Append("}");
            ctx.Response.StatusCode = db ? 200 : 503;
            ctx.Response.Write(sb.ToString());
        }
    }
}

