using System;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace StudentManagementSystem.Setup
{
    /// <summary>
    /// Install-time helper run by StudentManagementSystem-Setup.exe.
    ///
    ///   SmsSetup.exe createdb  -scripts "C:\...\Database" [-server "(LocalDB)\MSSQLLocalDB"] [-database StudentSMS]
    ///   SmsSetup.exe configure -webconfig "C:\...\Web.config" [-port 8080] [-server "(LocalDB)\MSSQLLocalDB"] [-database StudentSMS]
    ///   SmsSetup.exe check     [-server "(LocalDB)\MSSQLLocalDB"] [-database StudentSMS]
    ///
    /// Exit codes: 0 = success, 1 = bad arguments, 2 = SQL/IO failure.
    /// Writes a plain-text report to stdout so the installer log shows the reason.
    /// </summary>
    internal static class Program
    {
        private static string _server = @"(LocalDB)\MSSQLLocalDB";
        private static string _database = "StudentSMS";
        private static string _scripts;
        private static string _webConfig;
        private static int _port = 8080;

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0) return Usage();

                string mode = args[0].ToLowerInvariant();
                for (int i = 1; i < args.Length; i++)
                {
                    string a = args[i];
                    string v = (i + 1 < args.Length) ? args[i + 1] : null;
                    switch (a.ToLowerInvariant())
                    {
                        case "-scripts": _scripts = v; i++; break;
                        case "-webconfig": _webConfig = v; i++; break;
                        case "-server": _server = v; i++; break;
                        case "-database": _database = v; i++; break;
                        case "-port": _port = int.Parse(v, CultureInfo.InvariantCulture); i++; break;
                        default: Console.WriteLine("Unknown argument: " + a); return Usage();
                    }
                }

                switch (mode)
                {
                    case "createdb": return CreateDatabase();
                    case "configure": return Configure();
                    case "check": return Check();
                    default: return Usage();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAILED: " + ex.GetType().Name + ": " + ex.Message);
                return 2;
            }
        }

        private static int Usage()
        {
            Console.WriteLine("SmsSetup createdb|configure|check [-scripts dir] [-webconfig file] [-server name] [-database name] [-port n]");
            return 1;
        }

        // ------------------------------------------------------------------ createdb

        private static int CreateDatabase()
        {
            if (string.IsNullOrEmpty(_scripts) || !Directory.Exists(_scripts))
            {
                Console.WriteLine("FAILED: script folder not found: " + _scripts);
                return 2;
            }

            bool fresh;
            using (var conn = new SqlConnection(ConnectionString("master")))
            {
                conn.Open();
                Console.WriteLine("Connected to " + _server + ".");
                // Never touch [db].sys.* before the database exists.
                fresh = !DatabaseExists(conn, _database) || !TableExists(conn, _database, "dbo", "Users");
            }

            // Re-installing over an existing database must keep the data. Only the
            // idempotent scripts re-run; schema/sample scripts are for a fresh install.
            string[] order = fresh
                ? new[]
                {
                    "01_CreateDatabase.sql", "02_Tables.sql", "03_Indexes.sql",
                    "04_Seed.sql", "05_SampleData.sql", "06_StoredProcedures.sql",
                    "09_Preferences.sql", "10_Premium_AI.sql", "11_Module_Upgrade.sql",
                    "12_CampusPlatform.sql", "13_OnlineTests.sql", "14_CampusModules.sql", "15_Proctoring.sql", "16_OnlineTestsSeed.sql", "17_MailCenter.sql", "18_RegistrationApproval.sql", "19_PaymentsUniqueFix.sql", "20_AppSecrets.sql", "21_BackfillStudentProfiles.sql", "22_SchemaMigrations.sql", "23_PeopleRequestsActivity.sql", "24_FeesTimetable.sql", "25_HelpSessionsMail.sql", "26_HelpSeed.sql", "27_RepairProfiles.sql", "28_PeopleExtras.sql", "29_FeeStructureStatus.sql", "30_HelpFaq.sql"
                }
                : new[]
                {
                    "04_Seed.sql", "06_StoredProcedures.sql", "09_Preferences.sql",
                    "10_Premium_AI.sql", "11_Module_Upgrade.sql", "12_CampusPlatform.sql", "13_OnlineTests.sql",
                    "14_CampusModules.sql", "15_Proctoring.sql", "16_OnlineTestsSeed.sql", "17_MailCenter.sql", "18_RegistrationApproval.sql", "19_PaymentsUniqueFix.sql", "20_AppSecrets.sql", "21_BackfillStudentProfiles.sql", "22_SchemaMigrations.sql", "23_PeopleRequestsActivity.sql", "24_FeesTimetable.sql", "25_HelpSessionsMail.sql", "26_HelpSeed.sql", "27_RepairProfiles.sql", "28_PeopleExtras.sql", "29_FeeStructureStatus.sql", "30_HelpFaq.sql"
                };

            Console.WriteLine(fresh
                ? "Creating a new " + _database + " database."
                : "Existing " + _database + " database found; keeping data and refreshing seed/procedures.");

            using (var conn = new SqlConnection(ConnectionString("master")))
            {
                conn.Open();

                foreach (string file in order)
                {
                    string path = Path.Combine(_scripts, file);
                    if (!File.Exists(path))
                    {
                        Console.WriteLine("FAILED: missing script " + path);
                        return 2;
                    }

                    string sql = File.ReadAllText(path);
                    string[] batches = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    int batchNo = 0;
                    foreach (string batch in batches)
                    {
                        if (batch.Trim().Length == 0) continue;
                        batchNo++;
                        try
                        {
                            using (var cmd = new SqlCommand(batch, conn) { CommandTimeout = 120 })
                                cmd.ExecuteNonQuery();
                        }
                        catch (SqlException ex)
                        {
                            Console.WriteLine("FAILED: " + file + " batch " + batchNo + ": " + ex.Message);
                            return 2;
                        }
                    }
                    Console.WriteLine("  applied " + file);
                }
            }

            using (var check = new SqlConnection(ConnectionString(_database)))
            {
                check.Open();
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Users", check))
                {
                    int users = Convert.ToInt32(cmd.ExecuteScalar());
                    Console.WriteLine("Database " + _database + " ready (" + users + " user accounts).");
                }
            }
            return 0;
        }

        // ------------------------------------------------------------------ configure

        private static int Configure()
        {
            if (string.IsNullOrEmpty(_webConfig) || !File.Exists(_webConfig))
            {
                Console.WriteLine("FAILED: Web.config not found: " + _webConfig);
                return 2;
            }

            var doc = new XmlDocument();
            doc.Load(_webConfig);

            XmlNode conn = doc.SelectSingleNode("/configuration/connectionStrings/add[@name='DefaultConnection']");
            if (conn == null || conn.Attributes == null)
            {
                Console.WriteLine("FAILED: DefaultConnection not found in Web.config");
                return 2;
            }
            conn.Attributes["connectionString"].Value = ConnectionString(_database);

            XmlNode baseUrl = doc.SelectSingleNode("/configuration/appSettings/add[@key='App:BaseUrl']");
            if (baseUrl == null || baseUrl.Attributes == null)
            {
                Console.WriteLine("FAILED: App:BaseUrl not found in Web.config");
                return 2;
            }
            baseUrl.Attributes["value"].Value = "http://localhost:" + _port + "/";

            // The published site is a finished install: never ship debug compilation.
            XmlNode compilation = doc.SelectSingleNode("/configuration/system.web/compilation");
            if (compilation != null && compilation.Attributes != null && compilation.Attributes["debug"] != null)
                compilation.Attributes["debug"].Value = "false";

            doc.Save(_webConfig);
            Console.WriteLine("Web.config updated: " + _database + " on " + _server + ", base URL http://localhost:" + _port + "/");
            return 0;
        }

        // ------------------------------------------------------------------ check

        private static int Check()
        {
            using (var conn = new SqlConnection(ConnectionString(_database)))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT DB_NAME()", conn))
                    Console.WriteLine("Database reachable: " + cmd.ExecuteScalar());
            }
            return 0;
        }

        private static bool DatabaseExists(SqlConnection master, string database)
        {
            using (var cmd = new SqlCommand("SELECT DB_ID(@name)", master))
            {
                cmd.Parameters.AddWithValue("@name", database);
                object id = cmd.ExecuteScalar();
                return id != null && id != DBNull.Value && Convert.ToInt32(id) > 0;
            }
        }

        private static bool TableExists(SqlConnection master, string database, string schema, string table)
        {
            using (var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM [" + database + "].sys.objects o " +
                "JOIN [" + database + "].sys.schemas s ON s.schema_id = o.schema_id " +
                "WHERE o.name = @t AND s.name = @s AND o.type = 'U'", master))
            {
                cmd.Parameters.AddWithValue("@t", table);
                cmd.Parameters.AddWithValue("@s", schema);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private static string ConnectionString(string database)
        {
            var sb = new StringBuilder();
            sb.Append("Data Source=").Append(_server).Append(';');
            sb.Append("Initial Catalog=").Append(database).Append(';');
            sb.Append("Integrated Security=True;");
            sb.Append("MultipleActiveResultSets=True;");
            sb.Append("Connect Timeout=30;");
            return sb.ToString();
        }
    }
}









