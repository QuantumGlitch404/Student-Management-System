using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Data
{
    /// <summary>Exams, marks entry (bulk editable), result publishing.</summary>
    public static class MarksRepo
    {
        public static DataTable Exams(int? courseId, int? subjectId)
        {
            return SqlHelper.FillDataTable(
                "SELECT TOP 200 e.ExamId, e.ExamName, e.CourseId, e.SubjectId, e.MaxMarks, e.PassPercent, e.ExamDate, " +
                "e.IsPublished, e.PublishedAtUtc, c.CourseCode, s.SubjectCode, s.SubjectName, u.FullName AS CreatorName, " +
                "(SELECT COUNT(*) FROM dbo.Marks m WHERE m.ExamId = e.ExamId) AS MarksCount " +
                "FROM dbo.Exams e " +
                "JOIN dbo.Courses c ON c.CourseId = e.CourseId " +
                "JOIN dbo.Subjects s ON s.SubjectId = e.SubjectId " +
                "JOIN dbo.Users u ON u.UserId = e.CreatedBy " +
                "WHERE (@CourseId IS NULL OR e.CourseId = @CourseId) " +
                "  AND (@SubjectId IS NULL OR e.SubjectId = @SubjectId) " +
                "ORDER BY e.CreatedAtUtc DESC",
                p =>
                {
                    p.AddWithValue("@CourseId", (object)courseId ?? DBNull.Value);
                    p.AddWithValue("@SubjectId", (object)subjectId ?? DBNull.Value);
                });
        }

        public static bool ExamsOfMySubject(int examId, int subjectId, int courseId)
        {
            return SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.Exams WHERE ExamId = @ExamId AND SubjectId = @SubjectId AND CourseId = @CourseId",
                p =>
                {
                    p.AddWithValue("@ExamId", examId);
                    p.AddWithValue("@SubjectId", subjectId);
                    p.AddWithValue("@CourseId", courseId);
                }) > 0;
        }

        public static int CreateExam(string name, int courseId, int subjectId, int createdBy,
            decimal maxMarks, decimal passPercent, string examDate, int weightType)
        {
            int examId = -1;
            using (var conn = ConnectionManager.CreateOpenConnection())
            {
                SqlTransaction tx = conn.BeginTransaction();
                try
                {
                    using (var cmd = new SqlCommand(
                        "INSERT INTO dbo.Exams (ExamName, CourseId, SubjectId, CreatedBy, MaxMarks, PassPercent, WeightType, ExamDate) " +
                        "VALUES (@Name, @CourseId, @SubjectId, @CreatedBy, @MaxMarks, @PassPercent, @WeightType, " +
                        "CASE WHEN @ExamDate = '' THEN NULL ELSE CAST(@ExamDate AS DATE) END); " +
                        "SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@CourseId", courseId);
                        cmd.Parameters.AddWithValue("@SubjectId", subjectId);
                        cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                        cmd.Parameters.AddWithValue("@MaxMarks", maxMarks);
                        cmd.Parameters.AddWithValue("@PassPercent", passPercent);
                        cmd.Parameters.AddWithValue("@WeightType", weightType);
                        cmd.Parameters.AddWithValue("@ExamDate", examDate ?? "");
                        examId = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    tx.Commit();
                    AuditService.Log("ExamCreated", "Exams", examId, name);
                }
                catch { tx.Rollback(); throw; }
            }
            return examId;
        }

        public static void DeleteExam(int examId)
        {
            SqlHelper.ExecuteNonQuery("DELETE FROM dbo.Exams WHERE ExamId = @Id", p => p.AddWithValue("@Id", examId));
            AuditService.Log("ExamDeleted", "Exams", examId);
        }

        public static DataRow OneExam(int examId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT e.*, c.CourseCode, s.SubjectCode, s.SubjectName FROM dbo.Exams e " +
                "JOIN dbo.Courses c ON c.CourseId = e.CourseId " +
                "JOIN dbo.Subjects s ON s.SubjectId = e.SubjectId WHERE e.ExamId = @Id",
                p => p.AddWithValue("@Id", examId));
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        /// <summary>Calculates and persists results for one exam via the stored procedure.</summary>
        public static void CalculateResults(int examId)
        {
            SqlHelper.ExecuteNonQuery("dbo.usp_CalculateExamResults", CommandType.StoredProcedure,
                p => p.AddWithValue("@ExamId", examId));
            AuditService.Log("ResultsCalculated", "Results", examId);
        }

        // ---------------- roster with marks ----------------
        public static DataTable MarksRoster(int examId)
        {
            return SqlHelper.FillDataTable(
                "SELECT e.EnrollId, st.StudentId, st.EnrollNumber, u.FullName, m.MarksObtained " +
                "FROM dbo.Exams ex " +
                "JOIN dbo.Enrollments e ON e.CourseId = ex.CourseId AND e.SessionId = (SELECT TOP 1 s2.SessionId FROM dbo.AcademicSessions s2 WHERE s2.IsActive = 1) " +
                "JOIN dbo.Students st ON st.StudentId = e.StudentId AND st.SemesterNumber IN ( " +
                "     SELECT cs.SemesterNumber FROM dbo.CourseSubjects cs WHERE cs.CourseId = ex.CourseId AND cs.SubjectId = ex.SubjectId) " +
                "JOIN dbo.Users u ON u.UserId = st.UserId " +
                "LEFT JOIN dbo.Marks m ON m.ExamId = ex.ExamId AND m.EnrollId = e.EnrollId " +
                "WHERE ex.ExamId = @ExamId AND e.IsActive = 1 " +
                "ORDER BY st.EnrollNumber",
                p => p.AddWithValue("@ExamId", examId));
        }

        public static int SaveMarks(int examId, int enteredBy, Dictionary<int, decimal> marks)
        {
            DataTable exam = SqlHelper.FillDataTable("SELECT MaxMarks FROM dbo.Exams WHERE ExamId = @Id",
                p => p.AddWithValue("@Id", examId));
            if (exam.Rows.Count == 0) throw new Exception("Exam not found.");
            decimal max = Convert.ToDecimal(exam.Rows[0]["MaxMarks"]);
            int saved = 0;

            using (var conn = ConnectionManager.CreateOpenConnection())
            {
                SqlTransaction tx = conn.BeginTransaction();
                try
                {
                    using (var del = new SqlCommand("DELETE FROM dbo.Marks WHERE ExamId = @ExamId AND EnrollId = @EnrollId", conn, tx))
                    using (var ins = new SqlCommand(
                        "INSERT INTO dbo.Marks (ExamId, EnrollId, MarksObtained, EnteredBy) " +
                        "VALUES (@ExamId, @EnrollId, @Marks, @EnteredBy)", conn, tx))
                    {
                        var pExam = del.Parameters.Add("@ExamId", SqlDbType.Int); pExam.Value = examId;
                        var pEnrollDel = del.Parameters.Add("@EnrollId", SqlDbType.Int);

                        var pInsExam = ins.Parameters.Add("@ExamId", SqlDbType.Int); pInsExam.Value = examId;
                        var pInsEnroll = ins.Parameters.Add("@EnrollId", SqlDbType.Int);
                        var pInsMarks = ins.Parameters.Add("@Marks", SqlDbType.Decimal); pInsMarks.Precision = 5; pInsMarks.Scale = 2;
                        var pInsBy = ins.Parameters.Add("@EnteredBy", SqlDbType.Int); pInsBy.Value = enteredBy;

                        foreach (var kv in marks)
                        {
                            decimal v = kv.Value;
                            if (v < 0 || v > max) throw new Exception("Marks out of range 0-" + max + " for enroll " + kv.Key + ".");
                            pEnrollDel.Value = kv.Key;
                            pInsEnroll.Value = kv.Key;
                            pInsMarks.Value = v;
                            saved += del.ExecuteNonQuery() + ins.ExecuteNonQuery();
                        }
                    }
                    tx.Commit();
                    AuditService.Log("MarksSaved", "Marks", examId, saved + " rows by user " + enteredBy);
                    return saved;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public static string GradeFor(decimal percent)
        {
            return SqlHelper.ExecuteScalar(
                "SELECT TOP 1 Grade FROM dbo.GradeScale WHERE MinPercent <= @Pct ORDER BY MinPercent DESC",
                p => p.AddWithValue("@Pct", percent)) as string ?? "F";
        }

        public static DataTable ResultSheet(int examId)
        {
            return SqlHelper.FillDataTable(
                "SELECT r.EnrollId, st.EnrollNumber, u.FullName, r.MarksObtained, r.MaxMarks, r.PercentScore, r.Grade, r.IsPass, ex.ExamName " +
                "FROM dbo.Results r " +
                "JOIN dbo.Enrollments e ON e.EnrollId = r.EnrollId " +
                "JOIN dbo.Students st ON st.StudentId = e.StudentId " +
                "JOIN dbo.Users u ON u.UserId = st.UserId " +
                "JOIN dbo.Exams ex ON ex.ExamId = r.ExamId " +
                "WHERE r.ExamId = @ExamId ORDER BY st.EnrollNumber",
                p => p.AddWithValue("@ExamId", examId));
        }
    }
}
