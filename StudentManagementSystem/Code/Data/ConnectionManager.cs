using System;
using System.Configuration;
using System.Data.SqlClient;

namespace StudentManagementSystem.Data
{
    /// <summary>
    /// Single source of SQL Server connection strings (college mandate: config-based).
    /// </summary>
    public static class ConnectionManager
    {
        /// <summary>Returns a NEW open SqlConnection (caller must dispose via using).</summary>
        public static SqlConnection CreateOpenConnection(bool open = true)
        {
            string cs = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            var conn = new SqlConnection(cs);
            if (open) conn.Open();
            return conn;
        }

        /// <summary>Quick health probe used by dashboards/status pages.</summary>
        public static bool IsDatabaseReachable()
        {
            try
            {
                using (var conn = CreateOpenConnection())
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM sys.tables", conn))
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
            catch { return false; }
        }
    }
}
