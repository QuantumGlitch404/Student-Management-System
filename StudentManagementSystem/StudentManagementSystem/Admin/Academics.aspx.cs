using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Admin
{
    /// <summary>Courses / subjects / course-subject map / enrollments.</summary>
    public partial class Academics : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, CourseTable, SubjectTable, MapTable, EnrollmentTable, SummaryCards, FacultyOptions,
            SubjectOptions, SemesterOptions, SessionOptions, CourseIdNow;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            CourseIdNow = Request.QueryString["course"];
            SessionOptions = Options("SessionId", "SessionName", AcademicsRepo.Sessions(),
                Convert.ToString(AcademicsRepo.ActiveSessionId()));
            BuildTables();
        }

        // ---------------- actions ----------------
        private void HandlePost()
        {
            string error = null;
            try
            {
                switch (Request.Form["op"])
                {
                    case "newcourse":
                        AcademicsRepo.CreateCourse((Request.Form["code"] ?? "").Trim(),
                            (Request.Form["name"] ?? "").Trim(), Request.Form["desc"],
                            int.Parse(Request.Form["semesters"] ?? "6"),
                            int.Parse(Request.Form["credits"] ?? "120"));
                        Flash("success", "Course saved.");
                        break;

                    case "togglecourse":
                        SqlHelper.ExecuteNonQuery(
                            "UPDATE dbo.Courses SET IsActive = 1 - IsActive WHERE CourseId = @CourseId",
                            p => p.AddWithValue("@CourseId", int.Parse(Request.Form["courseId"])));
                        AuditService.Log("CourseToggled", "Courses", int.Parse(Request.Form["courseId"]));
                        Flash("info", "Course status toggled.");
                        break;

                    case "togglesubject":
                        SqlHelper.ExecuteNonQuery(
                            "UPDATE dbo.Subjects SET IsActive = 1 - IsActive WHERE SubjectId = @SubjectId",
                            p => p.AddWithValue("@SubjectId", int.Parse(Request.Form["subjectId"])));
                        AuditService.Log("SubjectToggled", "Subjects", int.Parse(Request.Form["subjectId"]));
                        Flash("info", "Subject status toggled.");
                        break;

                    case "newsubject":
                        AcademicsRepo.CreateSubject((Request.Form["code"] ?? "").Trim(),
                            (Request.Form["name"] ?? "").Trim(),
                            decimal.Parse(Request.Form["maxMarks"] ?? "100"),
                            decimal.Parse(Request.Form["passPct"] ?? "33"),
                            int.Parse(Request.Form["hours"] ?? "0"));
                        Flash("success", "Subject saved.");
                        break;

                    case "mapsubject":
                        bool ok = AcademicsRepo.MapSubject(int.Parse(Request.Form["courseId"]),
                            int.Parse(Request.Form["subjectId"]), int.Parse(Request.Form["semester"]));
                        Flash(ok ? "success" : "warning",
                            ok ? "Subject mapped." : "Already mapped at that semester.");
                        break;

                    case "unmapsubject":
                        AcademicsRepo.UnmapSubject(int.Parse(Request.Form["courseId"]),
                            int.Parse(Request.Form["subjectId"]), int.Parse(Request.Form["semester"]));
                        Flash("info", "Subject removed from the course.");
                        break;

                    case "enroll":
                        DoEnroll();
                        break;

                    case "toggleenroll":
                        bool active = Request.Form["value"] == "1";
                        AcademicsRepo.SetEnrollmentActive(int.Parse(Request.Form["enrollId"]), active);
                        Flash("info", "Enrollment " + (active ? "activated." : "deactivated."));
                        break;

                    case "assignfaculty":
                        AcademicsRepo.AssignFaculty(int.Parse(Request.Form["facultyId"]),
                            int.Parse(Request.Form["courseId"]), int.Parse(Request.Form["subjectId"]),
                            int.Parse(Request.Form["sessionId"]), int.Parse(Request.Form["semester"]));
                        Flash("success", "Faculty assigned.");
                        break;

                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex)
            {
                error = SqlHelper.FriendlyError(ex).Message;
            }
            RedirectBack(error);
        }

        private void DoEnroll()
        {
            DataTable matches = SqlHelper.FillDataTable(
                "SELECT TOP 5 st.StudentId, u.FullName FROM dbo.Students st JOIN dbo.Users u ON u.UserId = st.UserId " +
                "WHERE u.FullName = @Term OR st.EnrollNumber = @Term OR u.Email = @Term",
                p => p.AddWithValue("@Term", (Request.Form["term"] ?? "").Trim()));
            if (matches.Rows.Count == 0)
                throw new Exception("No student matches '" + (Request.Form["term"] ?? "") + "'. Use the exact name, enrollment or email.");
            if (matches.Rows.Count > 1)
                throw new Exception("That identifier matches " + matches.Rows.Count + " students; refine it.");

            bool ok = AcademicsRepo.EnrollStudent(
                Convert.ToInt32(matches.Rows[0]["StudentId"]),
                int.Parse(Request.Form["courseId"]),
                int.Parse(Request.Form["sessionId"]));
            Flash(ok ? "success" : "warning",
                ok ? "Student enrolled in the course."
                   : "That student already has an enrollment for that course and session.");
        }

        private void RedirectBack(string error)
        {
            var q = new StringBuilder();
            if (error != null) q.Append("?err=").Append(HttpUtility.UrlEncode(error));
            foreach (string key in Request.Form.AllKeys)
                if (key == "courseId" || key == "course")
                    q.Append(q.ToString().Contains("?") ? "&" : "?").Append(key).Append('=').Append(Request.Form[key] ?? "");
            foreach (string key in Request.QueryString.AllKeys)
                if (!string.IsNullOrEmpty(key) && key != "err")
                    q.Append(q.ToString().Contains("?") ? "&" : "?").Append(key).Append('=').Append(Request.QueryString[key] ?? "");
            Redirect("/Admin/Academics.aspx" + q.ToString());
        }

        // ---------------- lists ----------------
        private void BuildTables()
        {
            DataTable courses = AcademicsRepo.Courses();
            DataTable subjects = AcademicsRepo.Subjects();

            CourseTable = ListTable(courses, "Course");
            SubjectTable = ListTable(subjects, "Subject");

            SubjectOptions = Options("SubjectId", "SubjectName", subjects, "");
            var sb = new StringBuilder();
            for (int i = 1; i <= 8; i++) sb.Append("<option value='" + i + "'>Semester " + i + "</option>");
            SemesterOptions = sb.ToString();

            int courseId = ParseIntForm() ?? ParseIntQuery() ?? (courses.Rows.Count > 0 ? Convert.ToInt32(courses.Rows[0]["CourseId"]) : 0);
            CourseIdNow = courseId.ToString();
            if (courseId > 0)
            {
                MapTable = MapList(AcademicsRepo.CourseSubjects(courseId), courseId);
            }

            DataTable enrolls = AcademicsRepo.EnrollmentList(null, null);
            int activeEnrollments = 0;
            foreach (DataRow r in enrolls.Rows) if (Convert.ToBoolean(r["IsActive"])) activeEnrollments++;
            BuildFacultyOptions();
            var k = new StringBuilder("<div class='bento'>");
            Kpi(k, "Courses", courses.Rows.Count.ToString("N0"), "bi-journal-bookmark");
            Kpi(k, "Subjects", subjects.Rows.Count.ToString("N0"), "bi-book");
            Kpi(k, "Active enrollments", activeEnrollments.ToString("N0"), "bi-person-check");
            Kpi(k, "Mapped in course", courseId > 0 && MapTable != null ? CountRows(MapTable) : "0", "bi-diagram-3");
            k.Append("</div>");
            SummaryCards = k.ToString();

            var es = new StringBuilder();
            es.Append("<table class='table table-sm'><thead><tr><th>Student</th><th>Course</th><th>Session</th><th>Sem</th><th>Status</th><th>Actions</th></tr></thead><tbody>");
            if (enrolls.Rows.Count == 0) es.Append("<tr><td colspan='6' class='msg-empty'>No enrollments.</td></tr>");
            foreach (DataRow r in enrolls.Rows)
            {
                bool isActive = Convert.ToBoolean(r["IsActive"]);
                es.Append("<tr><td>" + E(r["FullName"]) + " <span class='small text-muted'>" + E(r["EnrollNumber"]) + "</span></td>" +
                          "<td>" + E(r["CourseCode"]) + "</td><td>" + E(r["SessionName"]) + "</td><td>Sem " + E(r["SemesterNumber"]) + "</td>" +
                          "<td><span class='badge " + (isActive ? "badge-ok" : "badge-off") + "'>" + (isActive ? "Active" : "Inactive") + "</span></td>" +
                          "<td>" + ImbForm("toggleenroll", new[] {
                              new[] { "enrollId", E(r["EnrollId"]) }, new[] { "value", isActive ? "0" : "1" }
                          }, isActive ? "Deactivate" : "Activate") + "</td></tr>");
            }
            es.Append("</tbody></table>");
            EnrollmentTable = es.ToString();
        }

        private void BuildFacultyOptions()
        {
            var f = new StringBuilder("<option value='0'>Choose faculty</option>");
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT f.FacultyId, u.FullName FROM dbo.Faculty f JOIN dbo.Users u ON u.UserId=f.UserId " +
                "WHERE u.IsActive=1 ORDER BY u.FullName");
            foreach (DataRow r in dt.Rows)
                f.Append("<option value='").Append(r["FacultyId"]).Append("'>").Append(E(r["FullName"])).Append("</option>");
            FacultyOptions = f.ToString();
        }

        private static string CountRows(string tableHtml)
        {
            if (string.IsNullOrEmpty(tableHtml)) return "0";
            int n = 0, i = 0;
            while ((i = tableHtml.IndexOf("<tr>", i, StringComparison.OrdinalIgnoreCase)) >= 0) { n++; i += 4; }
            return Math.Max(0, n - 1).ToString("N0");
        }

        private static void Kpi(StringBuilder sb, string label, string value, string icon)
        {
            sb.Append("<div class='c-3'><div class='stat-card'><div class='stat-head'>").Append(label).Append("</div><div class='stat-body'><div class='num'>").Append(value).Append("</div></div></div></div>");
        }

        private string ListTable(DataTable dt, string kind)
        {
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Code</th><th>Name</th><th>Info</th><th>Status</th><th></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='5' class='msg-empty'>None yet.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                bool isActive = dt.Columns.Contains("IsActive") && Convert.ToBoolean(r["IsActive"]);
                string toggleSql = kind == "Course"
                    ? "UPDATE dbo.Courses SET IsActive = 1 - IsActive WHERE CourseId = @Id"
                    : "UPDATE dbo.Subjects SET IsActive = 1 - IsActive WHERE SubjectId = @Id";
                string idField = kind == "Course" ? "CourseId" : "SubjectId";
                sb.Append("<tr><td><b>" + E(r[kind + "Code"]) + "</b></td><td>" + E(r[kind + "Name"]) + "</td><td>");
                if (kind == "Course")
                    sb.Append("<span class='small'>Semesters " + E(r["TotalSemesters"]) + ", Credits " + E(r["TotalCredits"]) +
                        ", " + E(r["StudentCount"]) + " students</span>");
                else
                    sb.Append("<span class='small'>Max " + E(r["DefaultMaxMarks"]) + ", pass " + E(r["DefaultPassPercent"]) + "%, " +
                        E(r["CourseCount"]) + " courses</span>");
                sb.Append("</td><td><span class='badge " + (isActive ? "badge-ok" : "badge-off") + "'>" + (isActive ? "Active" : "Inactive") + "</span></td>");
                sb.Append("<td>" + ImbForm("toggle" + kind.ToLowerInvariant(),
                        new[] { new[] { kind == "Course" ? "courseId" : "subjectId", E(r[idField]) } },
                        isActive ? "Deactivate" : "Activate") + "</td></tr>");
            }
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private string MapList(DataTable mapped, int courseId)
        {
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm mb-2'><thead><tr><th>Subject</th><th>Semester</th><th>Weekly hours</th><th>Actions</th></tr></thead><tbody>");
            if (mapped.Rows.Count == 0) sb.Append("<tr><td colspan='4' class='msg-empty'>No subject mapped to this course yet.</td></tr>");
            foreach (DataRow r in mapped.Rows)
            {
                sb.Append("<tr><td><b>" + E(r["SubjectCode"]) + "</b> " + E(r["SubjectName"]) + "</td>" +
                          "<td>Sem " + E(r["SemesterNumber"]) + "</td><td>" + E(r["WeeklyHours"]) + "</td>" +
                          "<td>" + ImbForm("unmapsubject", new[] {
                              new[] { "courseId", courseId.ToString() },
                              new[] { "subjectId", E(r["SubjectId"]) },
                              new[] { "semester", E(r["SemesterNumber"]) }
                          }, "Remove") + "</td></tr>");
            }
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        // ---------------- helpers ----------------
        protected string CourseOptions(bool forFilter)
        {
            DataTable courses = AcademicsRepo.Courses();
            return Options("CourseId", "CourseName", courses, forFilter ? CourseIdNow : "");
        }

        private string ImbForm(string op, string[][] fields, string label)
        {
            var sb = new StringBuilder();
            sb.Append("<form method='post' action='/Admin/Academics.aspx' style='display:inline'>");
            sb.Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='").Append(Attr(op)).Append("'/>");
            foreach (string[] f in fields)
                sb.Append("<input type='hidden' name='").Append(f[0]).Append("' value='").Append(f[1]).Append("'/>");
            sb.Append("<button class='imbtn'>").Append(label).Append("</button></form>");
            return sb.ToString();
        }

        private int? ParseIntForm() { return Parse(Request.Form["courseId"]); }
        private int? ParseIntQuery() { return Parse(Request.QueryString["course"]); }

        private static int? Parse(string v)
        {
            int n;
            return int.TryParse(v, out n) ? (int?)n : null;
        }
    }
}
