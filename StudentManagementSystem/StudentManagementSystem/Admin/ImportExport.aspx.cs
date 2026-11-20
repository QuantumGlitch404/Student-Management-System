using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Net.Mail;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using StudentManagementSystem.Data;
using StudentManagementSystem.Integrations;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Admin
{
    /// <summary>CSV/JSON import for people and academic master data, plus export links and job records.</summary>
    public partial class ImportExport : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, Result, JobsTable, ExportLinks, TemplateLinks, ImportKindOptions;

        private const int MaxRows = 1000;

        private static readonly string[][] Kinds =
        {
            new[] { "students", "Students" },
            new[] { "faculty", "Faculty" },
            new[] { "courses", "Courses" },
            new[] { "subjects", "Subjects" },
            new[] { "enrollments", "Enrollments" }
        };

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.QueryString["template"] != null)
            {
                WriteTemplate();
                return;
            }
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            BuildTemplateLinks();
            BuildKindOptions();
            BuildJobs();
            BuildExports();
        }

        private static string KindLabel(string kind)
        {
            foreach (string[] k in Kinds)
                if (string.Equals(k[0], kind, StringComparison.OrdinalIgnoreCase)) return k[1];
            return "Students";
        }

        private static bool IsKind(string kind)
        {
            foreach (string[] k in Kinds)
                if (string.Equals(k[0], kind, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void BuildKindOptions()
        {
            var sb = new StringBuilder();
            foreach (string[] k in Kinds)
                sb.Append("<option value='").Append(k[0]).Append("'>").Append(k[1]).Append("</option>");
            ImportKindOptions = sb.ToString();
        }

        private void BuildTemplateLinks()
        {
            var sb = new StringBuilder();
            foreach (string[] k in Kinds)
                sb.Append("<a class='imbtn' href='/Admin/ImportExport.aspx?template=").Append(k[0])
                  .Append("'>").Append(k[1]).Append(" CSV</a>");
            TemplateLinks = sb.ToString();
        }

        /// <summary>Serves a CSV template that matches the importer's expected headings.</summary>
        private void WriteTemplate()
        {
            string kind = (Request.QueryString["template"] ?? "students").Trim().ToLowerInvariant();
            if (!IsKind(kind)) kind = "students";
            string body;
            switch (kind)
            {
                case "faculty":
                    body = "FullName,Email,Phone,DeptCode,Designation\r\n" +
                           "Meera Iyer,meera.iyer@example.edu,9876500124,CS,Assistant Professor\r\n";
                    break;
                case "courses":
                    body = "CourseCode,CourseName,TotalSemesters,TotalCredits,Description\r\n" +
                           "BSC-CS,Bachelor of Science in Computer Science,6,180,Four-year honours programme\r\n";
                    break;
                case "subjects":
                    body = "SubjectCode,SubjectName,DefaultMaxMarks,DefaultPassPercent,WeeklyHours\r\n" +
                           "CS101,Programming Fundamentals,100,40,4\r\n";
                    break;
                case "enrollments":
                    body = "EnrollNumber,CourseCode,SessionName,Semester\r\n" +
                           "SMS2026001,BCA,2026-27,1\r\n";
                    break;
                default:
                    body = "FullName,Email,Phone,EnrollNumber,CourseCode,SessionName,Semester,DateOfBirth,GuardianName\r\n" +
                           "Asha Rao,asha.rao@example.edu,9876500123,SMS2026001,BCA,2026-27,1,2005-04-12,Ramesh Rao\r\n";
                    break;
            }
            Response.Clear();
            Response.ContentType = "text/csv";
            Response.AddHeader("Content-Disposition", "attachment; filename=" + kind + "-template.csv");
            Response.Write(body);
            AuditService.Log("TemplateDownloaded", "ImportJobs", 0, kind + ".csv");
            Response.SuppressContent = false;
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }

        /// <summary>Download links for every admin export scope, in each supported format.</summary>
        private void BuildExports()
        {
            var scopes = new[]
            {
                new[] { "dashboard", "Dashboard summary" }, new[] { "students", "Students" },
                new[] { "faculty", "Faculty" }, new[] { "courses", "Courses" },
                new[] { "enrollments", "Enrollments" }, new[] { "fees", "Fee structure" },
                new[] { "payments", "Payments" }, new[] { "attendance", "Attendance sessions" },
                new[] { "results", "Published results" }, new[] { "complaints", "Complaints" },
                new[] { "announcements", "Announcements" }, new[] { "audit", "Audit log" }
            };
            var sb = new StringBuilder();
            foreach (var scope in scopes)
            {
                sb.Append("<div class='d-flex flex-wrap gap-2 align-items-center mt-2'><span class='small me-2' style='min-width:150px'>")
                  .Append(scope[1]).Append(":</span>");
                foreach (var format in new[] { "csv", "xlsx", "pdf", "json" })
                {
                    string label = format == "xlsx" ? "Excel" : format.ToUpperInvariant();
                    sb.Append("<a class='imbtn' href='/Handlers/Export.ashx?scope=").Append(scope[0])
                      .Append("&amp;format=").Append(format).Append("'>").Append(label).Append("</a>");
                }
                sb.Append("</div>");
            }
            ExportLinks = sb.ToString();
        }

        private void HandlePost()
        {
            string error = null;
            try
            {
                if (Request.Form["op"] != "import") throw new Exception("Unknown operation.");
                if (Request.Files["file"] == null || Request.Files["file"].ContentLength == 0)
                    throw new Exception("Choose a CSV or JSON file first.");

                HttpPostedFile file = (HttpPostedFile)Request.Files[0];
                string name = Path.GetFileName(file.FileName ?? "import.csv");
                bool json = name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                if (!json && !name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Only .csv and .json files are accepted.");
                if (file.ContentLength > 4 * 1024 * 1024)
                    throw new Exception("File larger than 4 MB.");

                string kind = (Request.Form["kind"] ?? "students").Trim().ToLowerInvariant();
                if (!IsKind(kind)) throw new Exception("Choose an import type.");
                bool dryRun = Request.Form["preview"] == "1";

                ImportResult r = ImportFile(file.InputStream, name, json, kind, dryRun);
                Result = "<div class='alert " + (r.Failed == 0 ? "alert-success" : "alert-warning") + " small'>" +
                    (dryRun ? "Validation finished (dry run, nothing written): " : "Import finished: ") +
                    r.Ok + " row(s) " + (dryRun ? "valid" : "imported") + ", " + r.Failed + " failed, " +
                    r.Total + " processed.<br/>" +
                    "<pre class='mb-0' style='font-size:11.5px;white-space:pre-wrap;'>" +
                    HttpUtility.HtmlEncode(r.Summary) + "</pre></div>";
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }

            if (error != null) Flash("danger", error);
        }

        private class ImportResult
        {
            public int Total;
            public int Ok;
            public int Failed;
            public readonly List<string> Errors = new List<string>();
            public string Summary { get { return Errors.Count == 0 ? "No validation errors." : string.Join(Environment.NewLine, Errors); } }
        }

        private ImportResult ImportFile(Stream stream, string fileName, bool json, string kind, bool dryRun)
        {
            var result = new ImportResult();
            string[] headers;
            List<string[]> rows;
            if (json) ParseJson(stream, out headers, out rows);
            else ParseCsv(stream, out headers, out rows);

            if (headers.Length == 0) throw new Exception("The file has no header row.");
            if (rows.Count == 0) throw new Exception("The file has no data rows.");
            if (rows.Count > MaxRows) result.Errors.Add("Only the first " + MaxRows + " rows were processed.");

            switch (kind)
            {
                case "faculty": ImportFaculty(headers, rows, dryRun, result); break;
                case "courses": ImportCourses(headers, rows, dryRun, result); break;
                case "subjects": ImportSubjects(headers, rows, dryRun, result); break;
                case "enrollments": ImportEnrollments(headers, rows, dryRun, result); break;
                default: ImportStudents(headers, rows, dryRun, result); break;
            }

            if (!dryRun)
            {
                int total = result.Total, ok = result.Ok, failed = result.Failed;
                string summary = result.Summary;
                int jobId = SqlHelper.ExecuteScalarInt(
                    "INSERT INTO dbo.ImportJobs (UploadedBy, FileName, EntityTarget, RowsTotal, RowsOk, RowsError, ErrorsSummary, CompletedAtUtc) " +
                    "VALUES (@By, @File, @Target, @Total, @Ok, @Err, @Summary, SYSUTCDATETIME()); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    p =>
                    {
                        p.AddWithValue("@By", CurrentUser.UserId);
                        p.AddWithValue("@File", fileName.Length > 200 ? fileName.Substring(0, 200) : fileName);
                        p.AddWithValue("@Target", kind);
                        p.AddWithValue("@Total", total);
                        p.AddWithValue("@Ok", ok);
                        p.AddWithValue("@Err", failed);
                        p.AddWithValue("@Summary", summary.Length > 1900 ? summary.Substring(0, 1900) : summary);
                    });
                AuditService.Log(KindLabel(kind) + "Imported", "ImportJobs", jobId, ok + " ok / " + failed + " err");
            }
            return result;
        }

        private void ImportStudents(string[] headers, List<string[]> rows, bool dryRun, ImportResult result)
        {
            int iName = Index(headers, "FullName"), iEmail = Index(headers, "Email"), iPhone = Index(headers, "Phone"),
                iEnroll = Index(headers, "EnrollNumber"), iCourse = Index(headers, "CourseCode"),
                iSession = Index(headers, "SessionName"), iSem = Index(headers, "Semester"),
                iDob = Index(headers, "DateOfBirth"), iGuardian = Index(headers, "GuardianName");
            if (iName < 0 || iEmail < 0 || iEnroll < 0 || iCourse < 0)
                throw new Exception("Student headers need FullName, Email, EnrollNumber and CourseCode.");

            DataTable courses = AcademicsRepo.Courses();
            DataTable sessions = AcademicsRepo.Sessions();
            int activeSession = AcademicsRepo.ActiveSessionId();
            if (activeSession <= 0) throw new Exception("No active academic session exists.");
            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenEnrolls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int rowNumber = 1;
            foreach (string[] fields in rows)
            {
                rowNumber++;
                result.Total++;
                if (result.Total > MaxRows) break;
                string name = Get(fields, iName), email = Get(fields, iEmail).ToLowerInvariant(),
                    enroll = Get(fields, iEnroll), courseCode = Get(fields, iCourse).ToUpperInvariant();
                if (name.Length == 0 || email.Length == 0 || enroll.Length == 0)
                { Fail(result, rowNumber, "missing name/email/enrollment."); continue; }
                if (!IsEmail(email)) { Fail(result, rowNumber, "invalid email address."); continue; }
                if (!seenEmails.Add(email)) { Fail(result, rowNumber, "duplicate email in this file."); continue; }
                if (!seenEnrolls.Add(enroll)) { Fail(result, rowNumber, "duplicate enrollment number in this file."); continue; }

                int courseId = FindCourse(courses, courseCode);
                if (courseId == 0) { Fail(result, rowNumber, "unknown course '" + courseCode + "'."); continue; }
                int sessionId = activeSession;
                string sessionName = Get(fields, iSession);
                if (sessionName.Length > 0)
                {
                    sessionId = FindSession(sessions, sessionName);
                    if (sessionId == 0) { Fail(result, rowNumber, "unknown session '" + sessionName + "'."); continue; }
                }

                int semester = Int(Get(fields, iSem), 1, 1, 99);
                string dob = Date(Get(fields, iDob));
                if (Get(fields, iDob).Length > 0 && dob == null)
                { Fail(result, rowNumber, "invalid date of birth."); continue; }

                if (dryRun)
                {
                    if (EmailExists(email)) { Fail(result, rowNumber, "email already registered."); continue; }
                    if (EnrollExists(enroll)) { Fail(result, rowNumber, "enrollment already exists."); continue; }
                    result.Ok++;
                    continue;
                }

                string tempPassword = "SMS@" + PasswordHasher.GenerateUrlToken(4).Substring(0, 6) + "#1";
                try
                {
                    int userId = StudentsRepo.CreateStudent(name, email, Get(fields, iPhone),
                        PasswordHasher.Hash(tempPassword), enroll, courseId, sessionId, semester,
                        dob, Get(fields, iGuardian), null, "import");
                    object studentIdObj = SqlHelper.ExecuteScalar(
                        "SELECT StudentId FROM dbo.Students WHERE UserId = @UserId",
                        p => p.AddWithValue("@UserId", userId));
                    if (studentIdObj != null && studentIdObj != DBNull.Value)
                        AcademicsRepo.EnrollStudent(Convert.ToInt32(studentIdObj), courseId, sessionId);
                    SendCredentials(email, name, tempPassword);
                    result.Ok++;
                }
                catch (Exception ex) { Fail(result, rowNumber, SqlHelper.FriendlyError(ex).Message); }
            }
        }

        private void ImportFaculty(string[] headers, List<string[]> rows, bool dryRun, ImportResult result)
        {
            int iName = Index(headers, "FullName"), iEmail = Index(headers, "Email"), iPhone = Index(headers, "Phone"),
                iDept = Index(headers, "DeptCode"), iDesignation = Index(headers, "Designation");
            if (iName < 0 || iEmail < 0 || iDept < 0)
                throw new Exception("Faculty headers need FullName, Email and DeptCode.");
            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int rowNumber = 1;
            foreach (string[] fields in rows)
            {
                rowNumber++;
                result.Total++;
                if (result.Total > MaxRows) break;
                string name = Get(fields, iName), email = Get(fields, iEmail).ToLowerInvariant(), dept = Get(fields, iDept);
                if (name.Length == 0 || email.Length == 0 || dept.Length == 0)
                { Fail(result, rowNumber, "missing name/email/department."); continue; }
                if (!IsEmail(email)) { Fail(result, rowNumber, "invalid email address."); continue; }
                if (!seenEmails.Add(email)) { Fail(result, rowNumber, "duplicate email in this file."); continue; }
                if (dryRun)
                {
                    if (EmailExists(email)) { Fail(result, rowNumber, "email already registered."); continue; }
                    result.Ok++;
                    continue;
                }
                string tempPassword = "SMS@" + PasswordHasher.GenerateUrlToken(4).Substring(0, 6) + "#1";
                try
                {
                    StudentsRepo.CreateFaculty(name, email, Get(fields, iPhone), PasswordHasher.Hash(tempPassword),
                        dept, Get(fields, iDesignation));
                    SendCredentials(email, name, tempPassword);
                    result.Ok++;
                }
                catch (Exception ex) { Fail(result, rowNumber, SqlHelper.FriendlyError(ex).Message); }
            }
        }

        private void ImportCourses(string[] headers, List<string[]> rows, bool dryRun, ImportResult result)
        {
            int iCode = Index(headers, "CourseCode"), iName = Index(headers, "CourseName"),
                iSem = Index(headers, "TotalSemesters"), iCredits = Index(headers, "TotalCredits"),
                iDesc = Index(headers, "Description");
            if (iCode < 0 || iName < 0)
                throw new Exception("Course headers need CourseCode and CourseName.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int rowNumber = 1;
            foreach (string[] fields in rows)
            {
                rowNumber++;
                result.Total++;
                if (result.Total > MaxRows) break;
                string code = Get(fields, iCode).ToUpperInvariant(), name = Get(fields, iName);
                if (code.Length == 0 || name.Length == 0)
                { Fail(result, rowNumber, "missing course code/name."); continue; }
                if (!seen.Add(code)) { Fail(result, rowNumber, "duplicate course code in this file."); continue; }
                int semesters = Int(Get(fields, iSem), 6, 1, 20), credits = Int(Get(fields, iCredits), 180, 1, 1000);
                if (dryRun)
                {
                    if (SqlHelper.ExecuteScalarInt("SELECT COUNT(*) FROM dbo.Courses WHERE CourseCode=@C",
                        p => p.AddWithValue("@C", code)) > 0)
                    { Fail(result, rowNumber, "course code already exists."); continue; }
                    result.Ok++;
                    continue;
                }
                try
                {
                    AcademicsRepo.CreateCourse(code, name, Get(fields, iDesc), semesters, credits);
                    result.Ok++;
                }
                catch (Exception ex) { Fail(result, rowNumber, SqlHelper.FriendlyError(ex).Message); }
            }
        }

        private void ImportSubjects(string[] headers, List<string[]> rows, bool dryRun, ImportResult result)
        {
            int iCode = Index(headers, "SubjectCode"), iName = Index(headers, "SubjectName"),
                iMax = Index(headers, "DefaultMaxMarks"), iPass = Index(headers, "DefaultPassPercent"),
                iHours = Index(headers, "WeeklyHours");
            if (iCode < 0 || iName < 0)
                throw new Exception("Subject headers need SubjectCode and SubjectName.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int rowNumber = 1;
            foreach (string[] fields in rows)
            {
                rowNumber++;
                result.Total++;
                if (result.Total > MaxRows) break;
                string code = Get(fields, iCode).ToUpperInvariant(), name = Get(fields, iName);
                if (code.Length == 0 || name.Length == 0)
                { Fail(result, rowNumber, "missing subject code/name."); continue; }
                if (!seen.Add(code)) { Fail(result, rowNumber, "duplicate subject code in this file."); continue; }
                decimal max = Decimal(Get(fields, iMax), 100), pass = Decimal(Get(fields, iPass), 40);
                int hours = Int(Get(fields, iHours), 4, 0, 40);
                if (max <= 0 || pass < 0 || pass > 100)
                { Fail(result, rowNumber, "marks/pass percentage out of range."); continue; }
                if (dryRun)
                {
                    if (SqlHelper.ExecuteScalarInt("SELECT COUNT(*) FROM dbo.Subjects WHERE SubjectCode=@C",
                        p => p.AddWithValue("@C", code)) > 0)
                    { Fail(result, rowNumber, "subject code already exists."); continue; }
                    result.Ok++;
                    continue;
                }
                try
                {
                    AcademicsRepo.CreateSubject(code, name, max, pass, hours);
                    result.Ok++;
                }
                catch (Exception ex) { Fail(result, rowNumber, SqlHelper.FriendlyError(ex).Message); }
            }
        }

        private void ImportEnrollments(string[] headers, List<string[]> rows, bool dryRun, ImportResult result)
        {
            int iEnroll = Index(headers, "EnrollNumber"), iCourse = Index(headers, "CourseCode"),
                iSession = Index(headers, "SessionName"), iSem = Index(headers, "Semester");
            if (iEnroll < 0 || iCourse < 0 || iSession < 0)
                throw new Exception("Enrollment headers need EnrollNumber, CourseCode and SessionName.");
            DataTable courses = AcademicsRepo.Courses();
            DataTable sessions = AcademicsRepo.Sessions();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int rowNumber = 1;
            foreach (string[] fields in rows)
            {
                rowNumber++;
                result.Total++;
                if (result.Total > MaxRows) break;
                string enroll = Get(fields, iEnroll), courseCode = Get(fields, iCourse).ToUpperInvariant(),
                    sessionName = Get(fields, iSession);
                if (enroll.Length == 0 || courseCode.Length == 0 || sessionName.Length == 0)
                { Fail(result, rowNumber, "missing enrollment/course/session."); continue; }
                string key = enroll + "|" + courseCode + "|" + sessionName;
                if (!seen.Add(key)) { Fail(result, rowNumber, "duplicate enrollment row in this file."); continue; }

                DataRow student = FindStudent(enroll);
                if (student == null) { Fail(result, rowNumber, "unknown enrollment number '" + enroll + "'."); continue; }
                int courseId = FindCourse(courses, courseCode);
                if (courseId == 0) { Fail(result, rowNumber, "unknown course '" + courseCode + "'."); continue; }
                int sessionId = FindSession(sessions, sessionName);
                if (sessionId == 0) { Fail(result, rowNumber, "unknown session '" + sessionName + "'."); continue; }
                int studentId = Convert.ToInt32(student["StudentId"]);
                int semester = Int(Get(fields, iSem), Convert.ToInt32(student["SemesterNumber"]), 1, 99);

                if (dryRun)
                {
                    if (EnrollmentExists(studentId, courseId, sessionId))
                    { Fail(result, rowNumber, "enrollment already exists."); continue; }
                    result.Ok++;
                    continue;
                }
                try
                {
                    SqlHelper.ExecuteNonQuery(
                        "UPDATE dbo.Students SET CourseId=@Course, SessionId=@Session, SemesterNumber=@Sem WHERE StudentId=@Id",
                        p =>
                        {
                            p.AddWithValue("@Course", courseId);
                            p.AddWithValue("@Session", sessionId);
                            p.AddWithValue("@Sem", semester);
                            p.AddWithValue("@Id", studentId);
                        });
                    if (!AcademicsRepo.EnrollStudent(studentId, courseId, sessionId))
                    { Fail(result, rowNumber, "enrollment already exists."); continue; }
                    result.Ok++;
                }
                catch (Exception ex) { Fail(result, rowNumber, SqlHelper.FriendlyError(ex).Message); }
            }
        }

        private static void Fail(ImportResult result, int row, string message)
        {
            result.Failed++;
            result.Errors.Add("row " + row + ": " + message);
        }

        private static void ParseCsv(Stream stream, out string[] headers, out List<string[]> rows)
        {
            headers = new string[0];
            rows = new List<string[]>();
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                string header = reader.ReadLine();
                if (header == null) return;
                if (header.Length > 0 && header[0] == '\uFEFF') header = header.Substring(1);
                headers = ParseCsvLine(header);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim().Length == 0) continue;
                    rows.Add(ParseCsvLine(line));
                }
            }
        }

        private static void ParseJson(Stream stream, out string[] headers, out List<string[]> rows)
        {
            rows = new List<string[]>();
            string text;
            using (var reader = new StreamReader(stream, Encoding.UTF8, true)) text = reader.ReadToEnd();
            if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
            object parsed;
            try { parsed = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.DeserializeObject(text); }
            catch (Exception) { throw new Exception("The JSON file is not valid. Use a JSON array of flat objects."); }
            var objects = parsed as object[];
            if (objects == null) throw new Exception("JSON must be an array of objects.");
            var order = new List<string>();
            var maps = new List<Dictionary<string, object>>();
            foreach (object item in objects)
            {
                var map = item as Dictionary<string, object>;
                if (map == null) throw new Exception("Every JSON array item must be an object.");
                maps.Add(map);
                foreach (string key in map.Keys)
                    if (!order.Exists(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase))) order.Add(key);
            }
            headers = order.ToArray();
            foreach (var map in maps)
            {
                var values = new string[headers.Length];
                for (int i = 0; i < headers.Length; i++)
                {
                    object value;
                    values[i] = map.TryGetValue(headers[i], out value) && value != null
                        ? (value is DateTime ? ((DateTime)value).ToString("yyyy-MM-dd") : Convert.ToString(value, CultureInfo.InvariantCulture))
                        : "";
                }
                rows.Add(values);
            }
        }

        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = !inQuotes;
                }
                else if (ch == ',' && !inQuotes) { result.Add(sb.ToString()); sb.Length = 0; }
                else sb.Append(ch);
            }
            result.Add(sb.ToString());
            return result.ToArray();
        }

        private static int Index(string[] headers, string name)
        {
            for (int i = 0; i < headers.Length; i++)
                if (string.Equals((headers[i] ?? "").Trim(), name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        private static string Get(string[] fields, int index)
        {
            return index >= 0 && index < fields.Length ? (fields[index] ?? "").Trim() : "";
        }

        private static int Int(string value, int fallback, int min, int max)
        {
            int parsed;
            if (!int.TryParse((value ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                parsed = fallback;
            return parsed < min ? min : parsed > max ? max : parsed;
        }

        private static decimal Decimal(string value, decimal fallback)
        {
            decimal parsed;
            return decimal.TryParse((value ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed)
                ? parsed : fallback;
        }

        private static string Date(string value)
        {
            string text = (value ?? "").Trim();
            if (text.Length == 0) return "";
            DateTime dt;
            if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt) ||
                DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return null;
        }

        private static bool IsEmail(string value)
        {
            try { return new MailAddress(value).Address == value; }
            catch (Exception) { return false; }
        }

        private static int FindCourse(DataTable courses, string code)
        {
            foreach (DataRow r in courses.Rows)
                if (string.Equals((r["CourseCode"] as string ?? "").Trim(), code, StringComparison.OrdinalIgnoreCase))
                    return Convert.ToInt32(r["CourseId"]);
            return 0;
        }

        private static int FindSession(DataTable sessions, string name)
        {
            foreach (DataRow r in sessions.Rows)
                if (string.Equals((r["SessionName"] as string ?? "").Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return Convert.ToInt32(r["SessionId"]);
            return 0;
        }

        private static DataRow FindStudent(string enroll)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT StudentId, SemesterNumber FROM dbo.Students WHERE EnrollNumber=@N",
                p => p.AddWithValue("@N", enroll));
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        private static bool EmailExists(string email)
        {
            return SqlHelper.ExecuteScalarInt("SELECT COUNT(*) FROM dbo.Users WHERE Email=@E",
                p => p.AddWithValue("@E", email)) > 0;
        }

        private static bool EnrollExists(string enroll)
        {
            return SqlHelper.ExecuteScalarInt("SELECT COUNT(*) FROM dbo.Students WHERE EnrollNumber=@N",
                p => p.AddWithValue("@N", enroll)) > 0;
        }

        private static bool EnrollmentExists(int studentId, int courseId, int sessionId)
        {
            return SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.Enrollments WHERE StudentId=@S AND CourseId=@C AND SessionId=@Se",
                p =>
                {
                    p.AddWithValue("@S", studentId);
                    p.AddWithValue("@C", courseId);
                    p.AddWithValue("@Se", sessionId);
                }) > 0;
        }

        private static void SendCredentials(string email, string name, string tempPassword)
        {
            if (!AppConfig.EmailEnabled) return;
            try
            {
                new SmtpMailService().Send(email, "Your Student Management System account",
                    "<p>Hello " + HttpUtility.HtmlEncode(name) + ",</p>" +
                    "<p>Your account was created. Sign in with " + HttpUtility.HtmlEncode(email) +
                    " and password <b>" + HttpUtility.HtmlEncode(tempPassword) + "</b>. Change it after the first sign-in.</p>");
            }
            catch (Exception ex) { Global.WriteLog("ImportExport", "credential email failed for " + email, ex); }
        }

        private void BuildJobs()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP 20 j.JobId, j.FileName, j.EntityTarget, j.RowsTotal, j.RowsOk, j.RowsError, j.CompletedAtUtc, u.FullName " +
                "FROM dbo.ImportJobs j JOIN dbo.Users u ON u.UserId = j.UploadedBy ORDER BY j.JobId DESC");
            var sb = new StringBuilder();
            string zone = PrefsRepo.Get(CurrentUser.UserId).TimezoneId ?? Fmt.InstitutionZoneId;
            sb.Append("<table class='table table-sm'><thead><tr><th>File</th><th>Rows</th><th>OK</th><th>Err</th><th>By</th><th>When</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>No import has run yet.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                sb.Append("<tr><td><b>" + E(r["FileName"]) + "</b><div class='small text-muted'>" + E(r["EntityTarget"]) + "</div></td>");
                sb.Append("<td>" + E(r["RowsTotal"]) + "</td><td>" + E(r["RowsOk"]) + "</td><td>" + E(r["RowsError"]) + "</td>");
                sb.Append("<td>" + E(r["FullName"]) + "</td>");
                sb.Append("<td class='small'>" + (r["CompletedAtUtc"] == DBNull.Value || r["CompletedAtUtc"] == null ? "running" :
                    Fmt.Stamp((DateTime)r["CompletedAtUtc"], zone, "yyyy-MM-dd HH:mm")) + "</td></tr>");
            }
            sb.Append("</tbody></table>");
            JobsTable = sb.ToString();
        }
    }
}

