using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;

namespace StudentManagementSystem.Data
{
    /// <summary>
    /// Thin, reusable ADO.NET facade used by every repository. It always:
    ///  - opens/disposes SqlConnection via `using`
    ///  - uses parameterised SqlCommand (protection against SQL injection)
    ///  - maps common SqlException numbers to friendly messages
    ///  - supports transactions (SqlTransaction) for multi-table writes
    ///
    /// Two equivalent call shapes are supported so stored procedures read naturally:
    ///     SqlHelper.FillDataTable(sql, p => p.AddWithValue("@Id", 1));
    ///     SqlHelper.FillDataTable("dbo.usp_X", CommandType.StoredProcedure, p => ...);
    /// </summary>
    public static class SqlHelper
    {
        public static Exception FriendlyError(Exception ex)
        {
            var sqlEx = ex as SqlException;
            if (sqlEx == null) return ex.InnerException ?? ex;
            switch (sqlEx.Number)
            {
                case 2601:
                case 2627: return new Exception("A record with the same unique value already exists (duplicate entry).", sqlEx);
                case 547: return new Exception("The operation violates a referential constraint. Check related records first.", sqlEx);
                case 4060: return new Exception("Database not reachable. Run the scripts in the Database folder.", sqlEx);
                case 205:
                default: return new Exception("Database error. Please try again later.", sqlEx);
            }
        }

        // ---------------- ExecuteNonQuery ----------------

        /// <summary>Executes non-query SQL; returns affected row count.</summary>
        public static int ExecuteNonQuery(string sql, Action<SqlParameterCollection> addParams = null,
            CommandType type = CommandType.Text, SqlConnection connection = null, SqlTransaction transaction = null)
        {
            return ExecuteNonQueryCore(sql, addParams, type, connection, transaction);
        }

        /// <summary>Stored-procedure form: (name, CommandType, parameters).</summary>
        public static int ExecuteNonQuery(string sql, CommandType type, Action<SqlParameterCollection> addParams = null)
        {
            return ExecuteNonQueryCore(sql, addParams, type, null, null);
        }

        private static int ExecuteNonQueryCore(string sql, Action<SqlParameterCollection> addParams,
            CommandType type, SqlConnection connection, SqlTransaction transaction)
        {
            bool ownsConnection = connection == null;
            SqlConnection conn = connection ?? ConnectionManager.CreateOpenConnection();
            try
            {
                using (var cmd = new SqlCommand(sql, conn) { CommandType = type })
                {
                    if (transaction != null) cmd.Transaction = transaction;
                    if (addParams != null) addParams(cmd.Parameters);
                    return cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                if (ownsConnection) conn.Dispose();
            }
        }

        // ---------------- ExecuteScalar ----------------

        /// <summary>Executes scalar query returning object (caller knows the type).</summary>
        public static object ExecuteScalar(string sql, Action<SqlParameterCollection> addParams = null,
            CommandType type = CommandType.Text, SqlConnection connection = null, SqlTransaction transaction = null)
        {
            bool owns = connection == null;
            SqlConnection conn = connection ?? ConnectionManager.CreateOpenConnection();
            try
            {
                using (var cmd = new SqlCommand(sql, conn) { CommandType = type })
                {
                    if (transaction != null) cmd.Transaction = transaction;
                    if (addParams != null) addParams(cmd.Parameters);
                    return cmd.ExecuteScalar();
                }
            }
            finally { if (owns) conn.Dispose(); }
        }

        /// <summary>Stored-procedure form: (name, CommandType, parameters).</summary>
        public static object ExecuteScalar(string sql, CommandType type, Action<SqlParameterCollection> addParams = null)
        {
            return ExecuteScalar(sql, addParams, type);
        }

        public static int ExecuteScalarInt(string sql, Action<SqlParameterCollection> addParams = null,
            CommandType type = CommandType.Text)
        {
            object r = ExecuteScalar(sql, addParams, type);
            return r == null || r == DBNull.Value ? 0 : int.Parse(r.ToString(), CultureInfo.InvariantCulture);
        }

        // ---------------- ExecuteReader ----------------

        /// <summary>Opens a SqlDataReader. IMPORTANT: the caller must dispose it (closes the connection).</summary>
        public static SqlDataReader ExecuteReader(string sql, Action<SqlParameterCollection> addParams = null,
            CommandType type = CommandType.Text, SqlConnection connection = null)
        {
            SqlConnection conn = connection ?? ConnectionManager.CreateOpenConnection();
            try
            {
                var cmd = new SqlCommand(sql, conn) { CommandType = type };
                if (addParams != null) addParams(cmd.Parameters);
                return cmd.ExecuteReader(CommandBehavior.CloseConnection);
            }
            catch
            {
                conn.Dispose();
                throw;
            }
        }

        // ---------------- DataSet / DataTable (SqlDataAdapter) ----------------

        /// <summary>Fills a DataSet via SqlDataAdapter (multi-result stored procedures).</summary>
        public static DataSet FillDataSet(string sql, Action<SqlParameterCollection> addParams = null,
            CommandType type = CommandType.Text)
        {
            using (var conn = ConnectionManager.CreateOpenConnection())
            using (var cmd = new SqlCommand(sql, conn) { CommandType = type })
            {
                if (addParams != null) addParams(cmd.Parameters);
                using (var da = new SqlDataAdapter(cmd))
                {
                    var ds = new DataSet("SMSData");
                    da.Fill(ds);
                    return ds;
                }
            }
        }

        /// <summary>Stored-procedure form: (name, CommandType, parameters).</summary>
        public static DataSet FillDataSet(string sql, CommandType type, Action<SqlParameterCollection> addParams = null)
        {
            return FillDataSet(sql, addParams, type);
        }

        /// <summary>Fills a single DataTable.</summary>
        public static DataTable FillDataTable(string sql, Action<SqlParameterCollection> addParams = null,
            CommandType type = CommandType.Text)
        {
            DataSet ds = FillDataSet(sql, addParams, type);
            if (ds.Tables.Count == 0) return new DataTable();
            // Detach: a DataTable that still belongs to its DataSet cannot be added
            // to another DataSet (DataTable already belongs to another DataSet).
            DataTable table = ds.Tables[0];
            ds.Tables.Remove(table);
            return table;
        }

        /// <summary>Stored-procedure form: (name, CommandType, parameters).</summary>
        public static DataTable FillDataTable(string sql, CommandType type, Action<SqlParameterCollection> addParams = null)
        {
            return FillDataTable(sql, addParams, type);
        }

        /// <summary>Helper: build a SqlParameter from typed value.</summary>
        public static SqlParameter P(string name, object value, SqlDbType type, int size = 0)
        {
            return new SqlParameter(name, value == null ? (object)DBNull.Value : value) { SqlDbType = type, Size = size };
        }
    }
}
