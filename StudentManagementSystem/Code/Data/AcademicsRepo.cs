using System;
using System.Data;
using System.Data.SqlClient;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Data
{
    /// <summary>Courses, subjects, course-subject mapping, sessions, enrollments.</summary>
    public static class AcademicsRepo
    {
        // ---------------- courses ----------------
        public static DataTable Courses()
        {
            return SqlHelper.FillDataTable(
                "SELECT c.CourseId, c.CourseCode, c.CourseName, c.TotalSemesters, c.TotalCredits, c.IsActive, " +
                "(SELECT COUNT(*) FROM dbo.Students s WHERE s.CourseId = c.CourseId) AS StudentCount " +
                "FROM dbo.Courses c ORDER BY c.CourseCode");
        }

        public static void CreateCourse(string code, string name, string desc, int semesters, int credits)
        {
            SqlHelper.ExecuteNonQuery(
                "INSERT INTO dbo.Courses (CourseCode, CourseName, Description, TotalSemesters, TotalCredits) " +
                "VALUES (@Code, @Name, @Desc, @Semesters, @Credits)",
                p =>
                {
                    p.AddWithValue("@Code", code);
                    p.AddWithValue("@Name", name);
                    p.AddWithValue("@Desc", (object)desc ?? DBNull.Value);
                    p.AddWithValue("@Semesters", semesters);
                    p.AddWithValue("@Credits", credits);
                });
            AuditService.Log("CourseCreated", "Courses", null, code);
        }

        public static void UpdateCourse(int courseId, string name, string desc, int semesters, int credits, bool active)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Courses SET CourseName = @Name, Description = @Desc, TotalSemesters = @Semesters, " +
                "TotalCredits = @Credits, IsActive = @Active WHERE CourseId = @CourseId",
                p =>
                {
                    p.AddWithValue("@Name", name);
                    p.AddWithValue("@Desc", (object)desc ?? DBNull.Value);
                    p.AddWithValue("@Semesters", semesters);
                    p.AddWithValue("@Credits", credits);
                    p.AddWithValue("@Active", active ? 1 : 0);
                    p.AddWithValue("@CourseId", courseId);
                });
            AuditService.Log("CourseUpdated", "Courses", courseId);
        }

        // ---------------- subjects ----------------
        public static DataTable Subjects()
        {
            return SqlHelper.FillDataTable(
                "SELECT s.SubjectId, s.SubjectCode, s.SubjectName, s.DefaultMaxMarks, s.DefaultPassPercent, s.WeeklyHours, s.IsActive, " +
                "(SELECT COUNT(*) FROM dbo.CourseSubjects cs WHERE cs.SubjectId = s.SubjectId) AS CourseCount " +
                "FROM dbo.Subjects s ORDER BY s.SubjectCode");
        }

        public static void CreateSubject(string code, string name, decimal maxMarks, decimal passPct, int weeklyHours)
        {
            SqlHelper.ExecuteNonQuery(
                "INSERT INTO dbo.Subjects (SubjectCode, SubjectName, DefaultMaxMarks, DefaultPassPercent, WeeklyHours) " +
                "VALUES (@Code, @Name, @MaxMarks, @PassPct, @WeeklyHours)",
                p =>
                {
                    p.AddWithValue("@Code", code);
                    p.AddWithValue("@Name", name);
                    p.AddWithValue("@MaxMarks", maxMarks);
                    p.AddWithValue("@PassPct", passPct);
                    p.AddWithValue("@WeeklyHours", weeklyHours);
                });
            AuditService.Log("SubjectCreated", "Subjects", null, code);
        }

        public static void UpdateSubject(int subjectId, string name, decimal maxMarks, decimal passPct, int weeklyHours, bool active)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Subjects SET SubjectName = @Name, DefaultMaxMarks = @MaxMarks, DefaultPassPercent = @PassPct, " +
                "WeeklyHours = @WeeklyHours, IsActive = @Active WHERE SubjectId = @SubjectId",
                p =>
                {
                    p.AddWithValue("@Name", name);
                    p.AddWithValue("@MaxMarks", maxMarks);
                    p.AddWithValue("@PassPct", passPct);
                    p.AddWithValue("@WeeklyHours", weeklyHours);
                    p.AddWithValue("@Active", active ? 1 : 0);
                    p.AddWithValue("@SubjectId", subjectId);
                });
            AuditService.Log("SubjectUpdated", "Subjects", subjectId);
        }

        // ---------------- course - subject mapping ----------------
        public static DataTable CourseSubjects(int courseId)
        {
            return SqlHelper.FillDataTable(
                "SELECT cs.CourseId, cs.SubjectId, cs.SemesterNumber, s.SubjectCode, s.SubjectName, s.WeeklyHours " +
                "FROM dbo.CourseSubjects cs JOIN dbo.Subjects s ON s.SubjectId = cs.SubjectId " +
                "WHERE cs.CourseId = @CourseId ORDER BY cs.SemesterNumber, s.SubjectCode",
                p => p.AddWithValue("@CourseId", courseId));
        }

        public static bool MapSubject(int courseId, int subjectId, int semester)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.CourseSubjects (CourseId, SubjectId, SemesterNumber) VALUES (@CourseId, @SubjectId, @Semester)",
                    p =>
                    {
                        p.AddWithValue("@CourseId", courseId);
                        p.AddWithValue("@SubjectId", subjectId);
                        p.AddWithValue("@Semester", semester);
                    });
                AuditService.Log("SubjectMapped", "CourseSubjects", subjectId, "course " + courseId + " sem " + semester);
                return true;
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                return false;
            }
        }

        public static void UnmapSubject(int courseId, int subjectId, int semester)
        {
            SqlHelper.ExecuteNonQuery(
                "DELETE FROM dbo.CourseSubjects WHERE CourseId = @CourseId AND SubjectId = @SubjectId AND SemesterNumber = @Semester",
                p =>
                {
                    p.AddWithValue("@CourseId", courseId);
                    p.AddWithValue("@SubjectId", subjectId);
                    p.AddWithValue("@Semester", semester);
                });
        }

        // ---------------- academic sessions / semesters ----------------
        public static DataTable Sessions()
        {
            return SqlHelper.FillDataTable(
                "SELECT SessionId, SessionName, StartDate, EndDate, IsActive FROM dbo.AcademicSessions ORDER BY StartDate DESC");
        }

        public static int ActiveSessionId()
        {
            return SqlHelper.ExecuteScalarInt("SELECT TOP 1 SessionId FROM dbo.AcademicSessions WHERE IsActive = 1");
        }

        public static void CreateSession(string name, string startDate, string endDate)
        {
            SqlHelper.ExecuteNonQuery(
                "INSERT INTO dbo.AcademicSessions (SessionName, StartDate, EndDate) " +
                "VALUES (@Name, CAST(@Start AS DATE), CAST(@End AS DATE))",
                p => { p.AddWithValue("@Name", name); p.AddWithValue("@Start", startDate); p.AddWithValue("@End", endDate); });
            AuditService.Log("SessionCreated", "AcademicSessions", null, name);
        }

        public static void SetActiveSession(int sessionId)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.AcademicSessions SET IsActive = CASE WHEN SessionId = @Id THEN 1 ELSE 0 END",
                p => p.AddWithValue("@Id", sessionId));
            AuditService.Log("SessionActivated", "AcademicSessions", sessionId);
        }

        public static DataTable Semesters(int sessionId)
        {
            return SqlHelper.FillDataTable(
                "SELECT SemesterId, SemesterNumber, SemesterName, StartDate, EndDate FROM dbo.Semesters WHERE SessionId = @SessionId ORDER BY SemesterNumber",
                p => p.AddWithValue("@SessionId", sessionId));
        }

        public static void UpsertSemester(int sessionId, int semNumber, string name, string start, string end)
        {
            bool exists = SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.Semesters WHERE SessionId = @SessionId AND SemesterNumber = @Num",
                p => { p.AddWithValue("@SessionId", sessionId); p.AddWithValue("@Num", semNumber); }) > 0;
            if (exists)
            {
                SqlHelper.ExecuteNonQuery(
                    "UPDATE dbo.Semesters SET SemesterName = @Name, StartDate = CASE WHEN @Start = '' THEN NULL ELSE CAST(@Start AS DATE) END, " +
                    "EndDate = CASE WHEN @End = '' THEN NULL ELSE CAST(@End AS DATE) END " +
                    "WHERE SessionId = @SessionId AND SemesterNumber = @Num",
                    p =>
                    {
                        p.AddWithValue("@Name", name); p.AddWithValue("@Start", start); p.AddWithValue("@End", end);
                        p.AddWithValue("@SessionId", sessionId); p.AddWithValue("@Num", semNumber);
                    });
            }
            else
            {
                SqlHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.Semesters (SessionId, SemesterNumber, SemesterName, StartDate, EndDate) " +
                    "VALUES (@SessionId, @Num, @Name, CASE WHEN @Start = '' THEN NULL ELSE CAST(@Start AS DATE) END, " +
                    "CASE WHEN @End = '' THEN NULL ELSE CAST(@End AS DATE) END)",
                    p =>
                    {
                        p.AddWithValue("@SessionId", sessionId); p.AddWithValue("@Num", semNumber); p.AddWithValue("@Name", name);
                        p.AddWithValue("@Start", start); p.AddWithValue("@End", end);
                    });
            }
        }

        // ---------------- enrollments ----------------
        public static DataTable EnrollmentList(string term, int? courseId)
        {
            return SqlHelper.FillDataTable(
                "SELECT TOP 300 e.EnrollId, e.StudentId, e.CourseId, e.SessionId, e.IsActive, e.EnrolledAtUtc, " +
                "u.FullName, u.Email, st.EnrollNumber, st.SemesterNumber, c.CourseCode, c.CourseName, cs.SessionName " +
                "FROM dbo.Enrollments e " +
                "JOIN dbo.Students st ON st.StudentId = e.StudentId " +
                "JOIN dbo.Users u ON u.UserId = st.UserId " +
                "JOIN dbo.Courses c ON c.CourseId = e.CourseId " +
                "JOIN dbo.AcademicSessions cs ON cs.SessionId = e.SessionId " +
                "WHERE (@Term IS NULL OR u.FullName LIKE '%' + @Term + '%' OR st.EnrollNumber LIKE '%' + @Term + '%') " +
                "  AND (@CourseId IS NULL OR e.CourseId = @CourseId) " +
                "ORDER BY e.EnrolledAtUtc DESC",
                p =>
                {
                    p.AddWithValue("@Term", (object)(string.IsNullOrWhiteSpace(term) ? null : term) ?? DBNull.Value);
                    p.AddWithValue("@CourseId", (object)courseId ?? DBNull.Value);
                });
        }

        public static bool EnrollStudent(int studentId, int courseId, int sessionId)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.Enrollments (StudentId, CourseId, SessionId) VALUES (@StudentId, @CourseId, @SessionId)",
                    p =>
                    {
                        p.AddWithValue("@StudentId", studentId);
                        p.AddWithValue("@CourseId", courseId);
                        p.AddWithValue("@SessionId", sessionId);
                    });
                AuditService.Log("Enrolled", "Enrollments", studentId, "course " + courseId);
                return true;
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                return false;
            }
        }

        public static void SetEnrollmentActive(int enrollId, bool active)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.Enrollments SET IsActive = @Active WHERE EnrollId = @EnrollId",
                p => { p.AddWithValue("@Active", active ? 1 : 0); p.AddWithValue("@EnrollId", enrollId); });
            AuditService.Log("EnrollmentStatus", "Enrollments", enrollId, active ? "active" : "inactive");
        }

        // ---------------- faculty assignments ----------------
        public static void AssignFaculty(int facultyId, int courseId, int subjectId, int sessionId, int semester)
        {
            try
            {
                SqlHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.FacultyAssignments (FacultyId, CourseId, SubjectId, SessionId, SemesterNumber) " +
                    "VALUES (@FacultyId, @CourseId, @SubjectId, @SessionId, @Semester)",
                    p =>
                    {
                        p.AddWithValue("@FacultyId", facultyId);
                        p.AddWithValue("@CourseId", courseId);
                        p.AddWithValue("@SubjectId", subjectId);
                        p.AddWithValue("@SessionId", sessionId);
                        p.AddWithValue("@Semester", semester);
                    });
                AuditService.Log("FacultyAssigned", "FacultyAssignments", facultyId, "course " + courseId + " subject " + subjectId);
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627) { /* already assigned */ }
        }

        public static void UnassignFaculty(int assignmentId)
        {
            SqlHelper.ExecuteNonQuery("DELETE FROM dbo.FacultyAssignments WHERE AssignmentId = @Id",
                p => p.AddWithValue("@Id", assignmentId));
        }

        public static DataTable FacultyAssignments(int facultyUserId)
        {
            return SqlHelper.FillDataTable(
                "SELECT fa.AssignmentId, fa.FacultyId, fa.CourseId, fa.SubjectId, fa.SessionId, fa.SemesterNumber, " +
                "u.FullName AS FacultyName, f.UserId AS FacultyUserId, c.CourseCode, c.CourseName, s.SubjectCode, s.SubjectName, cs.SessionName " +
                "FROM dbo.FacultyAssignments fa " +
                "JOIN dbo.Faculty f ON f.FacultyId = fa.FacultyId " +
                "JOIN dbo.Users u ON u.UserId = f.UserId " +
                "JOIN dbo.Courses c ON c.CourseId = fa.CourseId " +
                "JOIN dbo.Subjects s ON s.SubjectId = fa.SubjectId " +
                "JOIN dbo.AcademicSessions cs ON cs.SessionId = fa.SessionId " +
                "WHERE f.UserId = @FacultyUserId " +
                "ORDER BY cs.SessionName, s.SubjectCode",
                p => p.AddWithValue("@FacultyUserId", facultyUserId));
        }
    }
}
