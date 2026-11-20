using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Reporting;

namespace StudentManagementSystem.Handlers
{
    /// <summary>
    /// Authorised data export in CSV, XLSX, PDF and JSON.
    ///
    /// Scopes are role-gated server-side and every query is parameterised.
    /// PDFs are real structured documents (title, filters, repeated table
    /// headers, wrapped cells, page numbers), not screenshots or renamed JSON.
    ///
    ///   Admin  : students faculty courses enrollments payments fees attendance
    ///            results complaints announcements audit notifications
    ///   Faculty: students (assigned courses) attendance (own sessions)
    ///            results (assigned courses)
    ///   Student: myresults myattendance mypayments marksheet notifications
    /// </summary>
    public class Export : IHttpHandler, System.Web.SessionState.IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        private class Filters
        {
            public int CourseId, SessionId, UserId, PaymentId;
            public string From = "", To = "", Status = "", Term = "", Action = "", Type = "";
            public string User = "", Role = "", Entity = "", Ip = "";
        }

        public void ProcessRequest(HttpContext ctx)
        {
            var res = ctx.Response;
            if (!CurrentUser.IsAuthenticated)
            {
                res.StatusCode = 401;
                res.ContentType = "text/html";
                res.Write("<html><body style='font-family:sans-serif;padding:40px'><h1>401</h1><p>Sign in to export data.</p></body></html>");
                return;
            }

            string scope = (ctx.Request.QueryString["scope"] ?? "").Trim().ToLowerInvariant();
            string format = (ctx.Request.QueryString["format"] ?? "pdf").Trim().ToLowerInvariant();
            if (format != "csv" && format != "xlsx" && format != "pdf" && format != "json") format = "pdf";
            Filters f = ReadFilters(ctx);

            string role = CurrentUser.RoleName;
            DataTable data;
            try { data = Build(scope, role, f); }
            catch (Exception ex)
            {
                Global.WriteLog("Export", "scope " + scope + " failed", ex);
                res.StatusCode = 500;
                res.ContentType = "text/html";
                res.Write("<html><body style='font-family:sans-serif;padding:40px'><h1>Export failed</h1>" +
                          "<p>The export could not be produced. The problem was logged.</p></body></html>");
                return;
            }

            if (data == null)
            {
                res.StatusCode = 403;
                res.ContentType = "text/html";
                res.Write("<html><body style='font-family:sans-serif;padding:40px'><h1>403</h1>" +
                          "<p>Unknown report, or your role may not export it.</p></body></html>");
                return;
            }

            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");
            LogJob(scope, format, data.Rows.Count);
            AuditService.Log("DataExport", "ExportJobs", null, scope + " " + format);

            res.Clear();
            res.Buffer = true;
            string fileName = scope + "_" + stamp + "." + format;
            res.AddHeader("Content-Disposition", "attachment; filename=\"" + fileName + "\"");
            res.AddHeader("X-Content-Type-Options", "nosniff");

            switch (format)
            {
                case "xlsx":
                    res.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    using (var ms = new System.IO.MemoryStream())
                    {
                        XlsxWriter.Write(data, scope, ms);
                        res.BinaryWrite(ms.ToArray());
                    }
                    break;

                case "pdf":
                    res.ContentType = "application/pdf";
                    res.BinaryWrite(BuildPdf(data, scope, f));
                    break;

                case "json":
                    res.ContentType = "application/json; charset=utf-8";
                    res.ContentEncoding = Encoding.UTF8;
                    res.Write(ToJson(data));
                    break;

                default:
                    res.ContentType = "text/csv; charset=utf-8";
                    res.ContentEncoding = Encoding.UTF8;
                    res.BinaryWrite(new byte[] { 0xEF, 0xBB, 0xBF });
                    res.Write(ToCsv(data));
                    break;
            }
        }

        private static Filters ReadFilters(HttpContext ctx)
        {
            var f = new Filters();
            int.TryParse(ctx.Request.QueryString["courseId"], out f.CourseId);
            int.TryParse(ctx.Request.QueryString["sessionId"], out f.SessionId);
            int.TryParse(ctx.Request.QueryString["userId"], out f.UserId);
            f.From = (ctx.Request.QueryString["from"] ?? "").Trim();
            f.To = (ctx.Request.QueryString["to"] ?? "").Trim();
            f.Status = (ctx.Request.QueryString["status"] ?? "").Trim();
            f.Term = (ctx.Request.QueryString["q"] ?? "").Trim();
            f.Action = (ctx.Request.QueryString["action"] ?? "").Trim();
            f.Type = (ctx.Request.QueryString["type"] ?? "").Trim();
            f.User = (ctx.Request.QueryString["user"] ?? "").Trim();
            f.Role = (ctx.Request.QueryString["role"] ?? "").Trim();
            f.Entity = (ctx.Request.QueryString["entity"] ?? "").Trim();
            f.Ip = (ctx.Request.QueryString["ip"] ?? "").Trim();
            int.TryParse(ctx.Request.QueryString["paymentId"], out f.PaymentId);
            // Defensive: dates must look like dates before reaching SQL.
            if (!System.Text.RegularExpressions.Regex.IsMatch(f.From, @"^\d{4}-\d{2}-\d{2}$")) f.From = "";
            if (!System.Text.RegularExpressions.Regex.IsMatch(f.To, @"^\d{4}-\d{2}-\d{2}$")) f.To = "";
            return f;
        }

        /// <summary>Returns rows for a scope, or null when the role may not see it.</summary>
        private static DataTable Build(string scope, string role, Filters f)
        {
            bool admin = role == "Admin";
            bool faculty = role == "Faculty";
            bool student = role == "Student";
            int me = CurrentUser.UserId;

            switch (scope)
            {
                case "users":
                case "students":
                    if (!admin && !faculty) return null;
                    return Q(
                        "SELECT TOP 10000 s.EnrollNumber, u.FullName, u.Email, u.Phone, c.CourseCode, c.CourseName, " +
                        "s.SemesterNumber, ses.SessionName, CASE WHEN u.IsActive=1 THEN 'Active' ELSE 'Inactive' END AS Status, " +
                        "CASE WHEN u.EmailVerified=1 THEN 'Yes' ELSE 'No' END AS Verified, u.CreatedAtUtc " +
                        "FROM dbo.Students s JOIN dbo.Users u ON u.UserId=s.UserId " +
                        "JOIN dbo.Courses c ON c.CourseId=s.CourseId JOIN dbo.AcademicSessions ses ON ses.SessionId=s.SessionId " +
                        "WHERE (@CourseId=0 OR s.CourseId=@CourseId) AND (@SessionId=0 OR s.SessionId=@SessionId) " +
                        "AND (@Term='' OR u.FullName LIKE '%'+@Term+'%' OR u.Email LIKE '%'+@Term+'%' OR s.EnrollNumber LIKE '%'+@Term+'%') " +
                        "AND (@Status='' OR (@Status='active' AND u.IsActive=1) OR (@Status='inactive' AND u.IsActive=0)) " +
                        (faculty ? "AND s.CourseId IN (SELECT fa.CourseId FROM dbo.FacultyAssignments fa JOIN dbo.Faculty fx ON fx.FacultyId=fa.FacultyId WHERE fx.UserId=@Me) " : "") +
                        "ORDER BY c.CourseCode, s.EnrollNumber",
                        P("@CourseId", f.CourseId, "@SessionId", f.SessionId, "@Term", f.Term, "@Status", f.Status.ToLowerInvariant(), "@Me", me));

                case "dashboard":
                    if (!admin) return null;
                    return Q(
                        "SELECT 'Active students' AS Metric, CAST((SELECT COUNT(*) FROM dbo.Students s JOIN dbo.Users u ON u.UserId=s.UserId WHERE u.IsActive=1 AND (@SessionId=0 OR s.SessionId=@SessionId) AND (@Semester=0 OR s.SemesterNumber=@Semester)) AS NVARCHAR(40)) AS Value " +
                        "UNION ALL SELECT 'Faculty accounts', CAST((SELECT COUNT(*) FROM dbo.Faculty) AS NVARCHAR(40)) " +
                        "UNION ALL SELECT 'Active courses', CAST((SELECT COUNT(*) FROM dbo.Courses WHERE IsActive=1) AS NVARCHAR(40)) " +
                        "UNION ALL SELECT 'Attendance sessions in period', CAST((SELECT COUNT(*) FROM dbo.AttendanceSessions WHERE SessionDate BETWEEN @From AND @To) AS NVARCHAR(40)) " +
                        "UNION ALL SELECT 'Open complaints', CAST((SELECT COUNT(*) FROM dbo.Complaints WHERE Status IN ('open','in_progress')) AS NVARCHAR(40)) " +
                        "UNION ALL SELECT 'Exams awaiting publication', CAST((SELECT COUNT(*) FROM dbo.Exams WHERE IsPublished=0) AS NVARCHAR(40)) " +
                        "UNION ALL SELECT 'Payments in period', CAST((SELECT COUNT(*) FROM dbo.Payments WHERE CreatedAtUtc>=@From AND CreatedAtUtc<DATEADD(day,1,@To)) AS NVARCHAR(40)) " +
                        "UNION ALL SELECT 'Fees collected in period (INR)', CAST((SELECT ISNULL(SUM(Amount),0) FROM dbo.Payments WHERE PayStatus='captured' AND CapturedAtUtc>=@From AND CapturedAtUtc<DATEADD(day,1,@To)) AS NVARCHAR(40)) " +
                        "UNION ALL SELECT 'Students below attendance threshold', CAST((SELECT COUNT(*) FROM (SELECT e.StudentId, 100.0*SUM(CASE WHEN a.AttnStatus IN ('P','L','E') THEN 1 ELSE 0 END)/NULLIF(COUNT(*),0) AS Pct FROM dbo.Attendance a JOIN dbo.Enrollments e ON e.EnrollId=a.EnrollId GROUP BY e.StudentId) t WHERE t.Pct < 75) AS NVARCHAR(40))",
                        P("@From", f.From == "" ? "1900-01-01" : f.From, "@To", f.To == "" ? "2100-01-01" : f.To,
                          "@SessionId", f.SessionId, "@Semester", 0));

                case "faculty":
                    if (!admin) return null;
                    return Q(
                        "SELECT u.FullName, u.Email, u.Phone, fa.DeptCode, fa.Designation, fa.JoinedOn, " +
                        "CASE WHEN u.IsActive=1 THEN 'Active' ELSE 'Inactive' END AS Status, u.LastLoginAtUtc " +
                        "FROM dbo.Faculty fa JOIN dbo.Users u ON u.UserId=fa.UserId " +
                        "WHERE (@Term='' OR u.FullName LIKE '%'+@Term+'%' OR u.Email LIKE '%'+@Term+'%' OR fa.DeptCode LIKE '%'+@Term+'%') " +
                        "ORDER BY fa.DeptCode, u.FullName",
                        P("@Term", f.Term));

                case "courses":
                    return admin ? Q("SELECT CourseCode, CourseName, TotalSemesters, TotalCredits, CASE WHEN IsActive=1 THEN 'Active' ELSE 'Inactive' END AS Status FROM dbo.Courses ORDER BY CourseCode") : null;

                case "fees":
                    if (!admin) return null;
                    return Q(
                        "SELECT c.CourseCode, f.FeeCode, f.FeeName, f.Amount, CASE WHEN f.IsActive=1 THEN 'Active' ELSE 'Inactive' END AS Status " +
                        "FROM dbo.Fees f JOIN dbo.Courses c ON c.CourseId=f.CourseId " +
                        "WHERE (@CourseId=0 OR f.CourseId=@CourseId) ORDER BY c.CourseCode, f.FeeName",
                        P("@CourseId", f.CourseId));

                case "payments":
                    if (!admin) return null;
                    return Q(
                        "SELECT p.PaymentId, u.FullName AS Student, u.Email, p.Purpose, f.FeeCode, p.Amount, p.Currency, p.PayStatus AS Status, " +
                        "p.PayMethod, p.ReceiptNo, p.GatewayPaymentId, p.CreatedAtUtc, p.CapturedAtUtc " +
                        "FROM dbo.Payments p JOIN dbo.Users u ON u.UserId=p.UserId LEFT JOIN dbo.Fees f ON f.FeeId=p.FeeId " +
                        "WHERE (@Status='' OR p.PayStatus=@Status) AND (@From='' OR CAST(p.CreatedAtUtc AS date)>=@From) " +
                        "AND (@To='' OR CAST(p.CreatedAtUtc AS date)<=@To) " +
                        "AND (@Term='' OR u.FullName LIKE '%'+@Term+'%' OR p.ReceiptNo LIKE '%'+@Term+'%' OR p.GatewayPaymentId LIKE '%'+@Term+'%') " +
                        "ORDER BY p.PaymentId DESC",
                        P("@Status", f.Status, "@From", f.From, "@To", f.To, "@Term", f.Term));

                case "attendance":
                    if (admin || faculty)
                        return Q(
                            "SELECT a.AttnSessionId, a.SessionDate, a.PeriodNo, c.CourseCode, sub.SubjectName, u.FullName AS Faculty, " +
                            "COUNT(att.AttnId) AS Marked, SUM(CASE WHEN att.AttnStatus IN ('P','L','E') THEN 1 ELSE 0 END) AS Present, " +
                            "SUM(CASE WHEN att.AttnStatus='A' THEN 1 ELSE 0 END) AS Absent " +
                            "FROM dbo.AttendanceSessions a JOIN dbo.Courses c ON c.CourseId=a.CourseId " +
                            "JOIN dbo.Subjects sub ON sub.SubjectId=a.SubjectId JOIN dbo.Users u ON u.UserId=a.FacultyUserId " +
                            "LEFT JOIN dbo.Attendance att ON att.AttnSessionId=a.AttnSessionId " +
                            "WHERE (@From='' OR a.SessionDate>=@From) AND (@To='' OR a.SessionDate<=@To) " +
                            "AND (@CourseId=0 OR a.CourseId=@CourseId) " +
                            (faculty ? "AND a.FacultyUserId=@Me " : "") +
                            "GROUP BY a.AttnSessionId, a.SessionDate, a.PeriodNo, c.CourseCode, sub.SubjectName, u.FullName " +
                            "ORDER BY a.SessionDate DESC, a.AttnSessionId DESC",
                            P("@From", f.From, "@To", f.To, "@CourseId", f.CourseId, "@Me", me));
                    return null;

                case "results":
                    if (admin || faculty)
                        return Q(
                            "SELECT st.EnrollNumber, su.FullName AS Student, c.CourseCode, e.ExamName, sub.SubjectName, e.ExamDate, " +
                            "r.MarksObtained, r.MaxMarks, r.PercentScore, r.Grade, CASE WHEN r.IsPass=1 THEN 'Pass' ELSE 'Fail' END AS Result " +
                            "FROM dbo.Results r JOIN dbo.Exams e ON e.ExamId=r.ExamId JOIN dbo.Subjects sub ON sub.SubjectId=e.SubjectId " +
                            "JOIN dbo.Courses c ON c.CourseId=e.CourseId JOIN dbo.Enrollments en ON en.EnrollId=r.EnrollId " +
                            "JOIN dbo.Students st ON st.StudentId=en.StudentId JOIN dbo.Users su ON su.UserId=st.UserId " +
                            "WHERE r.PublishedAtUtc IS NOT NULL AND (@CourseId=0 OR e.CourseId=@CourseId) " +
                            (faculty ? "AND e.CourseId IN (SELECT fa.CourseId FROM dbo.FacultyAssignments fa JOIN dbo.Faculty fx ON fx.FacultyId=fa.FacultyId WHERE fx.UserId=@Me) " : "") +
                            "ORDER BY e.ExamDate DESC, st.EnrollNumber",
                            P("@CourseId", f.CourseId, "@Me", me));
                    return null;

                case "onlinetests":
                    if (!admin) return null;
                    return Q(
                        "SELECT t.TestId, t.Title, c.CourseCode, s.SubjectCode, st.EnrollNumber, u.FullName AS Student, " +
                        "a.StartedAtUtc, a.SubmittedAtUtc, a.Score, a.MaxScore, a.Status, a.Violations, a.IntegrityNote " +
                        "FROM dbo.OnlineTestAttempts a JOIN dbo.OnlineTests t ON t.TestId=a.TestId " +
                        "JOIN dbo.Courses c ON c.CourseId=t.CourseId JOIN dbo.Subjects s ON s.SubjectId=t.SubjectId " +
                        "JOIN dbo.Students st ON st.StudentId=a.StudentId JOIN dbo.Users u ON u.UserId=st.UserId " +
                        "WHERE (@Term='' OR t.Title LIKE '%'+@Term+'%' OR u.FullName LIKE '%'+@Term+'%' OR st.EnrollNumber LIKE '%'+@Term+'%') " +
                        "ORDER BY a.AttemptId DESC",
                        P("@Term", f.Term));

                case "complaints":
                    if (!admin) return null;
                    return Q(
                        "SELECT cmp.ComplaintNo, cmp.Subject, cat.CatName AS Category, cmp.Priority, cmp.Status, " +
                        "u.FullName AS RaisedBy, a.FullName AS AssignedTo, cmp.CreatedAtUtc, cmp.ResolvedAtUtc " +
                        "FROM dbo.Complaints cmp JOIN dbo.ComplaintCategories cat ON cat.CategoryId=cmp.CategoryId " +
                        "JOIN dbo.Users u ON u.UserId=cmp.RaisedBy LEFT JOIN dbo.Users a ON a.UserId=cmp.AssignedTo " +
                        "WHERE (@Status='' OR cmp.Status=@Status) AND (@From='' OR CAST(cmp.CreatedAtUtc AS date)>=@From) " +
                        "AND (@To='' OR CAST(cmp.CreatedAtUtc AS date)<=@To) " +
                        "AND (@Term='' OR cmp.ComplaintNo LIKE '%'+@Term+'%' OR cmp.Subject LIKE '%'+@Term+'%') " +
                        "ORDER BY cmp.CreatedAtUtc DESC",
                        P("@Status", f.Status, "@From", f.From, "@To", f.To, "@Term", f.Term));

                case "announcements":
                    if (!admin) return null;
                    return Q(
                        "SELECT a.AnnTitle AS Title, a.Category, a.Audience, a.PublishFrom, a.PublishTo, " +
                        "CASE WHEN a.IsPinned=1 THEN 'Yes' ELSE 'No' END AS Pinned, CASE WHEN a.IsActive=1 THEN 'Active' ELSE 'Hidden' END AS Status, " +
                        "u.FullName AS CreatedBy, a.CreatedAtUtc " +
                        "FROM dbo.Announcements a JOIN dbo.Users u ON u.UserId=a.CreatedBy ORDER BY a.IsPinned DESC, a.PublishFrom DESC",
                        null);

                case "notifications":
                    return Q(
                        "SELECT n.CreatedAtUtc, n.NType AS Category, n.Title, n.Message, " +
                        "CASE WHEN nr.IsRead=1 THEN 'Read' ELSE 'Unread' END AS Status, nr.ReadAtUtc " +
                        "FROM dbo.NotificationRecipients nr JOIN dbo.Notifications n ON n.NotificationId=nr.NotificationId " +
                        "WHERE nr.UserId=@Me AND (@Type='' OR n.NType=@Type) ORDER BY n.CreatedAtUtc DESC",
                        P("@Me", me, "@Type", f.Type));

                case "audit":
                    if (!admin) return null;
                    return Q(
                        "SELECT TOP 10000 a.AuditId, a.[Action], a.EntityTable, a.EntityId, a.DetailJson AS Detail, a.ClientIp, " +
                        "u.FullName AS UserName, a.CreatedAtUtc FROM dbo.AuditLogs a LEFT JOIN dbo.Users u ON u.UserId=a.UserId " +
                        "WHERE (@Action='' OR a.[Action] LIKE '%'+@Action+'%') AND (@Term='' OR u.FullName LIKE '%'+@Term+'%' OR a.DetailJson LIKE '%'+@Term+'%') " +
                        "AND (@User='' OR u.FullName LIKE '%'+@User+'%') AND (@Role='' OR EXISTS (SELECT 1 FROM dbo.UserRoles ur JOIN dbo.Roles rr ON rr.RoleId=ur.RoleId WHERE ur.UserId=a.UserId AND rr.RoleName=@Role)) " +
                        "AND (@Entity='' OR a.EntityTable=@Entity) AND (@Ip='' OR a.ClientIp LIKE '%'+@Ip+'%') " +
                        "AND (@From='' OR CAST(a.CreatedAtUtc AS date)>=@From) AND (@To='' OR CAST(a.CreatedAtUtc AS date)<=@To) " +
                        "ORDER BY a.AuditId DESC",
                        P("@Action", f.Action, "@Term", f.Term, "@From", f.From, "@To", f.To, "@User", f.User, "@Role", f.Role, "@Entity", f.Entity, "@Ip", f.Ip));

                case "myactivity":
                    if (!student && !faculty) return null;
                    return Q(
                        "SELECT TOP 5000 a.AuditId, a.[Action], a.EntityTable, a.EntityId, a.DetailJson AS Detail, " +
                        "a.CreatedAtUtc FROM dbo.AuditLogs a " +
                        "WHERE a.UserId=@Me AND (@Action='' OR a.[Action] LIKE '%'+@Action+'%') " +
                        "AND (@Term='' OR a.[Action] LIKE '%'+@Term+'%' OR a.DetailJson LIKE '%'+@Term+'%') " +
                        "AND (@Entity='' OR a.EntityTable=@Entity) " +
                        "AND (@From='' OR CAST(a.CreatedAtUtc AS date)>=@From) AND (@To='' OR CAST(a.CreatedAtUtc AS date)<=@To) " +
                        "ORDER BY a.AuditId DESC",
                        P("@Me", me, "@Action", f.Action, "@Term", f.Term, "@From", f.From, "@To", f.To, "@Entity", f.Entity));

                case "receipt":
                    {
                        bool own = student && f.PaymentId > 0;
                        if (!admin && !own) return null;
                        return Q(
                            "SELECT p.ReceiptNo, p.PaymentId, u.FullName AS Student, u.Email, p.Purpose, f.FeeCode, p.Amount, p.Currency, " +
                            "p.PayStatus AS Status, p.PayMethod, p.GatewayOrderId AS [Order], p.GatewayPaymentId AS [Payment ref], " +
                            "p.CreatedAtUtc, p.CapturedAtUtc FROM dbo.Payments p JOIN dbo.Users u ON u.UserId=p.UserId " +
                            "LEFT JOIN dbo.Fees f ON f.FeeId=p.FeeId WHERE p.PaymentId=@Id AND (@Own=0 OR p.UserId=@Me)",
                            P("@Id", f.PaymentId, "@Own", own ? 1 : 0, "@Me", me));
                    }

                case "marksheet":
                    if (!student) return null;
                    return Q("EXEC dbo.usp_GetStudentResultSheet @StudentUserId=@Me", P("@Me", me));

                case "myresults":
                    return student ? Q("EXEC dbo.usp_GetStudentResultSheet @StudentUserId=@Me", P("@Me", me)) : null;
                case "myattendance":
                    return student ? Q("EXEC dbo.usp_GetStudentAttendanceDetail @StudentUserId=@Me", P("@Me", me)) : null;
                case "mypayments":
                    return student ? Q(
                        "SELECT p.PaymentId, p.Purpose, f.FeeCode, p.Amount, p.Currency, p.PayStatus AS Status, p.PayMethod, " +
                        "p.ReceiptNo, p.CreatedAtUtc, p.CapturedAtUtc FROM dbo.Payments p LEFT JOIN dbo.Fees f ON f.FeeId=p.FeeId " +
                        "WHERE p.UserId=@Me ORDER BY p.PaymentId DESC", P("@Me", me)) : null;

                default:
                    return null;
            }
        }

        private static object[] P(params object[] pairs) { return pairs; }

        private static DataTable Q(string sql, params object[] pairs)
        {
            return SqlHelper.FillDataTable(sql, cmd =>
            {
                if (pairs == null) return;
                for (int i = 0; i + 1 < pairs.Length; i += 2)
                {
                    object v = pairs[i + 1];
                    cmd.AddWithValue(Convert.ToString(pairs[i]), v == null ? DBNull.Value : v);
                }
            });
        }

        // ------------------------------------------------------------- formats

        private static string ToCsv(DataTable dt)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Csv(dt.Columns[i].ColumnName));
            }
            sb.Append("\r\n");
            foreach (DataRow row in dt.Rows)
            {
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    object v = row[i];
                    sb.Append(Csv(v == DBNull.Value ? "" : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)));
                }
                sb.Append("\r\n");
            }
            return sb.ToString();
        }

        private static string Csv(string value)
        {
            if (value == null) return "";
            value = value.Replace("\"", "\"\"");
            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value + "\"" : value;
        }

        private static string ToJson(DataTable dt)
        {
            var sb = new StringBuilder("[");
            for (int r = 0; r < dt.Rows.Count; r++)
            {
                if (r > 0) sb.Append(',');
                sb.Append('{');
                for (int c = 0; c < dt.Columns.Count; c++)
                {
                    if (c > 0) sb.Append(',');
                    object v = dt.Rows[r][c];
                    sb.Append(JsonString(dt.Columns[c].ColumnName)).Append(':');
                    if (v == null || v == DBNull.Value) sb.Append("null");
                    else if (v is int || v is long || v is short || v is byte) sb.Append(Convert.ToInt64(v));
                    else if (v is decimal || v is double || v is float)
                        sb.Append(Convert.ToDecimal(v, System.Globalization.CultureInfo.InvariantCulture)
                            .ToString(System.Globalization.CultureInfo.InvariantCulture));
                    else if (v is bool) sb.Append((bool)v ? "true" : "false");
                    else if (v is DateTime) sb.Append(JsonString(((DateTime)v).ToString("yyyy-MM-ddTHH:mm:ssZ")));
                    else sb.Append(JsonString(v.ToString()));
                }
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string JsonString(string value)
        {
            if (value == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append(' ');
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        // ---------------------------------------------------------------- PDF

        private static byte[] BuildPdf(DataTable dt, string scope, Filters f)
        {
            bool landscape = dt.Columns.Count > 6;
            var report = new PdfReport(TitleFor(scope), SubtitleFor(scope), FilterLine(f), landscape, PrefsRepo.Get(CurrentUser.UserId).TimezoneId ?? Fmt.InstitutionZoneId);
            report.Summary.Add("Records: " + dt.Rows.Count.ToString("N0"));
            report.Summary.Add("Report: " + TitleFor(scope));
            if (!string.IsNullOrEmpty(f.From) || !string.IsNullOrEmpty(f.To))
                report.Summary.Add("Date range: " + (string.IsNullOrEmpty(f.From) ? "start" : f.From) + " to " + (string.IsNullOrEmpty(f.To) ? "today" : f.To));
            if (!string.IsNullOrEmpty(f.Status)) report.Summary.Add("Status filter: " + f.Status);
            if (!string.IsNullOrEmpty(f.Type)) report.Summary.Add("Category filter: " + f.Type);
            report.Summary.Add("Requested by: " + CurrentUser.FullName + " (" + CurrentUser.RoleName + ")");
            report.AddTable(dt, TitleFor(scope));
            return report.Build();
        }

        private static string FilterLine(Filters f)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(f.From) || !string.IsNullOrEmpty(f.To)) parts.Add("Period " + (f.From.Length == 0 ? "all" : f.From) + " to " + (f.To.Length == 0 ? "today" : f.To));
            if (!string.IsNullOrEmpty(f.Status)) parts.Add("status=" + f.Status);
            if (!string.IsNullOrEmpty(f.Term)) parts.Add("search=" + f.Term);
            if (!string.IsNullOrEmpty(f.Action)) parts.Add("action=" + f.Action);
            if (!string.IsNullOrEmpty(f.Type)) parts.Add("category=" + f.Type);
            if (f.CourseId > 0) parts.Add("courseId=" + f.CourseId);
            if (!string.IsNullOrEmpty(f.User)) parts.Add("user=" + f.User);
            if (!string.IsNullOrEmpty(f.Role)) parts.Add("role=" + f.Role);
            if (!string.IsNullOrEmpty(f.Entity)) parts.Add("entity=" + f.Entity);
            if (!string.IsNullOrEmpty(f.Ip)) parts.Add("ip=" + f.Ip);
            return parts.Count == 0 ? "No filters applied" : string.Join("  \u00b7  ", parts.ToArray());
        }

        private static string TitleFor(string scope)
        {
            switch (scope)
            {
                case "students": case "users": return "Student records";
                case "faculty": return "Faculty records";
                case "courses": return "Course catalogue";
                case "fees": return "Fee structure";
                case "payments": return "Payment history";
                case "attendance": return "Attendance sessions";
                case "results": return "Published results";
                case "complaints": return "Complaint register";
                case "announcements": return "Announcement history";
                case "notifications": return "My notification history";
                case "audit": return "Audit log";
                case "myactivity": return "My activity";
                case "marksheet": return "Student marksheet";
                case "myresults": return "My results";
                case "myattendance": return "My attendance";
                case "mypayments": return "My payments";
                case "receipt": return "Payment receipt";
                case "dashboard": return "Dashboard summary";
                default: return scope;
            }
        }

        private static string SubtitleFor(string scope)
        {
            switch (scope)
            {
                case "payments": return "Captured, pending and failed payment records with references and receipts.";
                case "receipt": return "Official payment receipt for the selected transaction.";
                case "dashboard": return "Key academic and finance indicators for the selected period.";
                case "attendance": return "Session-level attendance summary: marked, present and absent counts.";
                case "audit": return "Append-only administrative activity log. Records cannot be edited in the application.";
                case "myactivity": return "Your own account and record events, newest first.";
                case "marksheet": return "Published examination results for the signed-in student.";
                case "students": case "users": return "Enrolled students with course, session and verification status.";
                case "complaints": return "Tickets with priority, status, ownership and resolution dates.";
                default: return "Structured export generated by Student Management System.";
            }
        }

        private static void LogJob(string scope, string format, int rows)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.ExportJobs (RequestedBy, Scope, Format, RowsReturned) VALUES (@U, @S, @F, @R)",
                    p =>
                    {
                        p.AddWithValue("@U", CurrentUser.UserId);
                        p.AddWithValue("@S", scope.Length > 40 ? scope.Substring(0, 40) : scope);
                        p.AddWithValue("@F", format.ToUpperInvariant());
                        p.AddWithValue("@R", rows);
                    });
            }
            catch (Exception) { /* job logging must never break an export */ }
        }
    }
}

