using System;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Data
{
    /// <summary>Students, faculty people management (admin module).</summary>
    public static class StudentsRepo
    {
        public static DataTable Search(string term, int? courseId, int roleFilterAll)
        {
            return Search(term, courseId, roleFilterAll, false);
        }

        public static DataTable Search(string term, int? courseId, int roleFilterAll, bool incompleteOnly)
        {
            return SqlHelper.FillDataTable(
                "SELECT TOP 200 s.StudentId, s.UserId, s.EnrollNumber, s.CourseId, s.SessionId, s.SemesterNumber, " +
                "s.DateOfBirth, s.GuardianName, s.Address, s.SectionCode, s.Status AS StudentStatus, u.FullName, u.Email, u.Phone, u.IsActive, u.EmailVerified, " +
                "c.CourseCode, c.CourseName, cs.SessionName, u.LastLoginAtUtc, u.CreatedAtUtc " +
                "FROM dbo.Students s " +
                "JOIN dbo.Users u ON u.UserId = s.UserId " +
                "LEFT JOIN dbo.Courses c ON c.CourseId = s.CourseId " +
                "LEFT JOIN dbo.AcademicSessions cs ON cs.SessionId = s.SessionId " +
                "WHERE (@Term IS NULL OR u.FullName LIKE '%' + @Term + '%' OR u.Email LIKE '%' + @Term + '%' " +
                "       OR s.EnrollNumber LIKE '%' + @Term + '%') " +
                "  AND (@CourseId IS NULL OR s.CourseId = @CourseId) " +
                "  AND (@NoCourse = 0 OR s.CourseId IS NULL OR ISNULL(s.Status,'') = 'Profile not set up') " +
                "  AND (@ShowAll = 1 OR u.IsActive = 1) " +
                "ORDER BY u.FullName",
                p =>
                {
                    p.AddWithValue("@Term", (object)(string.IsNullOrWhiteSpace(term) ? null : term) ?? DBNull.Value);
                    p.AddWithValue("@CourseId", (object)courseId ?? DBNull.Value);
                    p.AddWithValue("@NoCourse", incompleteOnly ? 1 : 0);
                    p.AddWithValue("@ShowAll", roleFilterAll);
                });
        }

        /// <summary>Students whose course details have not been set by an administrator.</summary>
        public static DataTable SearchIncomplete(bool all)
        {
            return SqlHelper.FillDataTable(
                "SELECT s.StudentId, s.UserId, s.EnrollNumber, s.CourseId, s.SessionId, s.SemesterNumber, " +
                "s.DateOfBirth, s.GuardianName, s.Address, s.SectionCode, s.Status AS StudentStatus, u.FullName, u.Email, u.Phone, u.IsActive, u.EmailVerified, " +
                "c.CourseCode, c.CourseName, cs.SessionName, u.LastLoginAtUtc, u.CreatedAtUtc " +
                "FROM dbo.Students s JOIN dbo.Users u ON u.UserId = s.UserId " +
                "LEFT JOIN dbo.Courses c ON c.CourseId = s.CourseId " +
                "LEFT JOIN dbo.AcademicSessions cs ON cs.SessionId = s.SessionId " +
                "WHERE (s.CourseId IS NULL OR ISNULL(s.Status,'') = 'Profile not set up') " +
                "AND (@ShowAll = 1 OR u.IsActive = 1) ORDER BY u.CreatedAtUtc DESC",
                p => p.AddWithValue("@ShowAll", all ? 1 : 0));
        }

        public static DataRow One(int studentId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT s.*, u.FullName, u.Email, u.Phone FROM dbo.Students s " +
                "JOIN dbo.Users u ON u.UserId = s.UserId WHERE s.StudentId = @Id",
                p => p.AddWithValue("@Id", studentId));
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public static DataTable FacultyList()
        {
            return SqlHelper.FillDataTable(
                "SELECT f.FacultyId, f.UserId, f.DeptCode, f.Designation, f.JoinedOn, u.FullName, u.Email, u.Phone, u.IsActive " +
                "FROM dbo.Faculty f JOIN dbo.Users u ON u.UserId = f.UserId ORDER BY u.FullName");
        }

        /// <summary>Creates the user + role + student profile atomically in one SqlTransaction.</summary>
        public static int CreateStudent(string fullName, string email, string phone, string passwordHash,
            string enrollNumber, int courseId, int sessionId, int semester,
            string dob, string guardian, string address, string createdBy)
        {
            using (var conn = ConnectionManager.CreateOpenConnection())
            {
                SqlTransaction tx = conn.BeginTransaction();
                try
                {
                    int userId = UsersRepo.CreateUser(fullName, email, phone, passwordHash, "Student", false);
                    using (var cmd = new SqlCommand(
                        "INSERT INTO dbo.Students (UserId, EnrollNumber, CourseId, SessionId, SemesterNumber, " +
                        "AdmissionDate, DateOfBirth, GuardianName, Address, Status) " +
                        "VALUES (@UserId, @EnrollNumber, @CourseId, @SessionId, @Semester, CAST(SYSUTCDATETIME() AS date), " +
                        "CASE WHEN @Dob = '' THEN NULL ELSE CAST(@Dob AS DATE) END, @Guardian, @Address, 'Active')", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@EnrollNumber", enrollNumber);
                        cmd.Parameters.AddWithValue("@CourseId", courseId);
                        cmd.Parameters.AddWithValue("@SessionId", sessionId);
                        cmd.Parameters.AddWithValue("@Semester", semester);
                        cmd.Parameters.AddWithValue("@Dob", dob ?? "");
                        cmd.Parameters.AddWithValue("@Guardian", (object)guardian ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Address", (object)address ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                    AuditService.Log("StudentCreated", "Students", userId, createdBy + ": " + enrollNumber);
                    return userId;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public static int CreateFaculty(string fullName, string email, string phone, string passwordHash,
            string dept, string designation)
        {
            int userId = UsersRepo.CreateUser(fullName, email, phone, passwordHash, "Faculty", false);
            SqlHelper.ExecuteNonQuery(
                "INSERT INTO dbo.Faculty (UserId, DeptCode, Designation, JoinedOn) " +
                "VALUES (@UserId, @Dept, @Designation, CAST(SYSUTCDATETIME() AS DATE))",
                p =>
                {
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@Dept", dept);
                    p.AddWithValue("@Designation", (object)designation ?? DBNull.Value);
                });
            AuditService.Log("FacultyCreated", "Faculty", userId, email);
            return userId;
        }

        public static void UpdateStudent(int studentId, string fullName, string phone,
            string guardian, string address, string dob, string enrollNumber, int semester)
        {
            using (var conn = ConnectionManager.CreateOpenConnection())
            {
                SqlTransaction tx = conn.BeginTransaction();
                try
                {
                    DataRow row = One(studentId);
                    if (row == null) throw new Exception("Student not found.");
                    int userId = Convert.ToInt32(row["UserId"]);
                    using (var cu = new SqlCommand(
                        "UPDATE dbo.Users SET FullName = @FullName, Phone = @Phone, UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @UserId", conn, tx))
                    {
                        cu.Parameters.AddWithValue("@FullName", fullName);
                        cu.Parameters.AddWithValue("@Phone", (object)phone ?? DBNull.Value);
                        cu.Parameters.AddWithValue("@UserId", userId);
                        cu.ExecuteNonQuery();
                    }
                    using (var cs = new SqlCommand(
                        "UPDATE dbo.Students SET EnrollNumber = @EnrollNumber, SemesterNumber = @Semester, " +
                        "GuardianName = @Guardian, Address = @Address, " +
                        "DateOfBirth = CASE WHEN @Dob = '' THEN NULL ELSE CAST(@Dob AS DATE) END " +
                        "WHERE StudentId = @StudentId", conn, tx))
                    {
                        cs.Parameters.AddWithValue("@EnrollNumber", enrollNumber);
                        cs.Parameters.AddWithValue("@Semester", semester);
                        cs.Parameters.AddWithValue("@Guardian", (object)guardian ?? DBNull.Value);
                        cs.Parameters.AddWithValue("@Address", (object)address ?? DBNull.Value);
                        cs.Parameters.AddWithValue("@Dob", dob ?? "");
                        cs.Parameters.AddWithValue("@StudentId", studentId);
                        cs.ExecuteNonQuery();
                    }
                    tx.Commit();
                    AuditService.Log("StudentUpdated", "Students", studentId);
                }
                catch { tx.Rollback(); throw; }
            }
        }

        /// <summary>
        /// Self-registered users are approved before they have a real profile.
        /// Create a blank profile marked "Profile not set up"; the administrator
        /// assigns course, semester, section and enrollment later. No fake defaults.
        /// Idempotent: an existing profile is left untouched.
        /// </summary>
        public static int EnsureProfileForUser(int userId)
        {
            object existing = SqlHelper.ExecuteScalar(
                "SELECT StudentId FROM dbo.Students WHERE UserId = @UserId",
                p => p.AddWithValue("@UserId", userId));
            if (existing != null && existing != DBNull.Value) return Convert.ToInt32(existing);

            int isStudent = SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.RoleId = ur.RoleId " +
                "WHERE ur.UserId = @UserId AND r.RoleName = 'Student'",
                p => p.AddWithValue("@UserId", userId));
            if (isStudent == 0) return 0;

            int studentId = SqlHelper.ExecuteScalarInt(
                "INSERT INTO dbo.Students (UserId, EnrollNumber, CourseId, SessionId, SemesterNumber, Status) " +
                "VALUES (@UserId, NULL, NULL, NULL, NULL, 'Profile not set up'); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                p => p.AddWithValue("@UserId", userId));
            AuditService.Log("StudentProfileCreated", "Students", studentId,
                "blank profile on approval for user " + userId + "; waiting for administrator", "Profile", userId, null, "Profile not set up");
            return studentId;
        }
    }
}

