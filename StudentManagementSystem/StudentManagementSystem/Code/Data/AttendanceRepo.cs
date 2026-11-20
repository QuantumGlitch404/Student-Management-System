using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Data
{
    /// <summary>Attendance sessions + per-student rows (bulk save in one transaction).</summary>
    public static class AttendanceRepo
    {
        public static DataTable Sessions(int? facultyUserId, int limit)
        {
            return SqlHelper.FillDataTable(
                "SELECT TOP (@Limit) a.AttnSessionId, a.FacultyUserId, a.CourseId, a.SubjectId, a.SessionDate, a.PeriodNo, " +
                "a.SubjectName, c.CourseCode, s.UserId AS FacultyUserRef, su.FullName AS FacultyName, " +
                "(SELECT COUNT(*) FROM dbo.Attendance x WHERE x.AttnSessionId = a.AttnSessionId) AS MarkedCount, " +
                "(SELECT COUNT(*) FROM dbo.Attendance x WHERE x.AttnSessionId = a.AttnSessionId AND x.AttnStatus = 'P') AS PresentCount " +
                "FROM dbo.AttendanceSessions a " +
                "JOIN dbo.Courses c ON c.CourseId = a.CourseId " +
                "JOIN dbo.Users su ON su.UserId = a.FacultyUserId " +
                "LEFT JOIN dbo.Faculty s ON s.UserId = a.FacultyUserId " +
                "WHERE (@FacultyUserId IS NULL OR a.FacultyUserId = @FacultyUserId) " +
                "ORDER BY a.SessionDate DESC, a.PeriodNo",
                p =>
                {
                    p.AddWithValue("@Limit", limit);
                    p.AddWithValue("@FacultyUserId", (object)facultyUserId ?? DBNull.Value);
                });
        }

        public static DataRow OneSession(int attnSessionId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT a.AttnSessionId, a.FacultyUserId, a.CourseId, a.SubjectId, a.SessionDate, a.PeriodNo, a.SubjectName, " +
                "c.CourseCode, c.CourseName, s.SubjectCode, su.FullName AS FacultyName " +
                "FROM dbo.AttendanceSessions a " +
                "JOIN dbo.Courses c ON c.CourseId = a.CourseId " +
                "JOIN dbo.Subjects s ON s.SubjectId = a.SubjectId " +
                "JOIN dbo.Users su ON su.UserId = a.FacultyUserId " +
                "WHERE a.AttnSessionId = @Id",
                p => p.AddWithValue("@Id", attnSessionId));
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        /// <summary>Creates a session row; returns id, or -1 when the duplicate key exists.</summary>
        public static int CreateSession(int facultyUserId, int courseId, int subjectId, string date, int period, string subjectName)
        {
            try
            {
                return SqlHelper.ExecuteScalarInt(
                    "INSERT INTO dbo.AttendanceSessions (FacultyUserId, CourseId, SubjectId, SessionDate, PeriodNo, SubjectName) " +
                    "VALUES (@FacultyUserId, @CourseId, @SubjectId, CAST(@Date AS DATE), @Period, @SubjectName); " +
                    "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    p =>
                    {
                        p.AddWithValue("@FacultyUserId", facultyUserId);
                        p.AddWithValue("@CourseId", courseId);
                        p.AddWithValue("@SubjectId", subjectId);
                        p.AddWithValue("@Date", date);
                        p.AddWithValue("@Period", period);
                        p.AddWithValue("@SubjectName", (object)subjectName ?? DBNull.Value);
                    });
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                return -1;
            }
        }

        // ---------------- roster ----------------
        public static DataTable Roster(int courseId, int sessionId, int semester)
        {
            return SqlHelper.FillDataTable(
                "SELECT e.EnrollId, st.StudentId, u.UserId, u.FullName, st.EnrollNumber, " +
                "CAST(e.IsActive AS INT) AS EnrollActive " +
                "FROM dbo.Enrollments e " +
                "JOIN dbo.Students st ON st.StudentId = e.StudentId AND st.SemesterNumber = @Semester " +
                "JOIN dbo.Users u ON u.UserId = st.UserId " +
                "WHERE e.CourseId = @CourseId AND e.SessionId = @SessionId AND e.IsActive = 1 " +
                "ORDER BY st.EnrollNumber",
                p =>
                {
                    p.AddWithValue("@CourseId", courseId);
                    p.AddWithValue("@SessionId", sessionId);
                    p.AddWithValue("@Semester", semester);
                });
        }

        /// <summary>Roster of an already created session, joined with existing attendance rows.</summary>
        public static DataTable RosterForSession(int attnSessionId, int courseId, int sessionId)
        {
            return SqlHelper.FillDataTable(
                "SELECT e.EnrollId, st.StudentId, u.FullName, st.EnrollNumber, at.AttnStatus, at.Remarks " +
                "FROM dbo.Enrollments e " +
                "JOIN dbo.Students st ON st.StudentId = e.StudentId " +
                "JOIN dbo.Users u ON u.UserId = st.UserId " +
                "LEFT JOIN dbo.Attendance at ON at.EnrollId = e.EnrollId AND at.AttnSessionId = @AttnSessionId " +
                "WHERE e.CourseId = @CourseId AND e.SessionId = @SessionId " +
                "ORDER BY st.EnrollNumber",
                p =>
                {
                    p.AddWithValue("@AttnSessionId", attnSessionId);
                    p.AddWithValue("@CourseId", courseId);
                    p.AddWithValue("@SessionId", sessionId);
                });
        }

        /// <summary>Saves one full session (delete + insert rows) inside a SqlTransaction.</summary>
        public static int SaveSession(int attnSessionId, Dictionary<int, string> statuses)
        {
            using (var conn = ConnectionManager.CreateOpenConnection())
            {
                SqlTransaction tx = conn.BeginTransaction();
                int saved = 0;
                try
                {
                    using (var del = new SqlCommand("DELETE FROM dbo.Attendance WHERE AttnSessionId = @Id", conn, tx))
                    {
                        del.Parameters.AddWithValue("@Id", attnSessionId);
                        del.ExecuteNonQuery();
                    }
                    foreach (var kv in statuses)
                    {
                        using (var ins = new SqlCommand(
                            "INSERT INTO dbo.Attendance (AttnSessionId, EnrollId, AttnStatus) VALUES (@Session, @Enroll, @Status)", conn, tx))
                        {
                            ins.Parameters.AddWithValue("@Session", attnSessionId);
                            ins.Parameters.AddWithValue("@Enroll", kv.Key);
                            ins.Parameters.AddWithValue("@Status", kv.Value);
                            saved += ins.ExecuteNonQuery();
                        }
                    }
                    tx.Commit();
                    AuditService.Log("AttendanceSaved", "Attendance", attnSessionId, saved + " rows");
                    return saved;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public static void DeleteSession(int attnSessionId)
        {
            SqlHelper.ExecuteNonQuery("DELETE FROM dbo.AttendanceSessions WHERE AttnSessionId = @Id",
                p => p.AddWithValue("@Id", attnSessionId));
            AuditService.Log("AttendanceSessionDeleted", "AttendanceSessions", attnSessionId);
        }
    }
}
