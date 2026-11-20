using System;
using System.Data;
using System.Data.SqlClient;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Data
{
    /// <summary>Question bank, online test definitions, timed attempts, scoring and proctoring events.</summary>
    public sealed class ProctorResult
    {
        public int Violations { get; set; }
        public int WarningLimit { get; set; }
        public bool AutoSubmitted { get; set; }
        public string Action { get; set; }
    }

    public static class OnlineTestsRepo
    {
        public static DataTable AdminTests()
        {
            return SqlHelper.FillDataTable(
                "SELECT t.TestId, t.CourseId, t.SubjectId, t.Title, t.Instructions, t.DurationMinutes, " +
                "t.OpensAtUtc, t.ClosesAtUtc, t.MaxAttempts, t.PassPercent, t.ShuffleQuestions, " +
                "t.ProctoringEnabled, t.WarningLimit, t.IsPublished, t.CreatedAtUtc, c.CourseCode, c.CourseName, " +
                "s.SubjectCode, s.SubjectName, " +
                "(SELECT COUNT(*) FROM dbo.OnlineTestQuestions q WHERE q.TestId = t.TestId) AS QuestionCount, " +
                "(SELECT ISNULL(SUM(q.Points), 0) FROM dbo.OnlineTestQuestions q WHERE q.TestId = t.TestId) AS MaxScore, " +
                "(SELECT COUNT(*) FROM dbo.OnlineTestAttempts a WHERE a.TestId = t.TestId) AS AttemptCount, " +
                "(SELECT COUNT(*) FROM dbo.OnlineTestAttempts a WHERE a.TestId = t.TestId AND a.Status = 'in_progress') AS InProgress " +
                "FROM dbo.OnlineTests t " +
                "JOIN dbo.Courses c ON c.CourseId = t.CourseId " +
                "JOIN dbo.Subjects s ON s.SubjectId = t.SubjectId " +
                "ORDER BY t.TestId DESC");
        }

        public static DataTable Categories()
        {
            return SqlHelper.FillDataTable(
                "SELECT CategoryId, CategoryName, Slug, SortOrder FROM dbo.TestCategories WHERE IsActive = 1 ORDER BY SortOrder, CategoryName");
        }

        public static DataTable Questions(int? subjectId, int? categoryId = null)
        {
            return SqlHelper.FillDataTable(
                "SELECT TOP 200 q.QuestionId, q.SubjectId, q.Prompt, q.OptionA, q.OptionB, q.OptionC, q.OptionD, " +
                "q.CorrectOption, q.Explanation, q.Difficulty, q.IsActive, q.CreatedAtUtc, s.SubjectCode, s.SubjectName, " +
                "ISNULL(tc.CategoryName, '-') AS CategoryName, " +
                "(SELECT COUNT(*) FROM dbo.OnlineTestQuestions tq WHERE tq.QuestionId = q.QuestionId) AS TestCount " +
                "FROM dbo.QuestionBank q JOIN dbo.Subjects s ON s.SubjectId = q.SubjectId " +
                "LEFT JOIN dbo.TestCategories tc ON tc.CategoryId = q.CategoryId " +
                "WHERE (@SubjectId IS NULL OR q.SubjectId = @SubjectId) AND (@CategoryId IS NULL OR q.CategoryId = @CategoryId) " +
                "ORDER BY q.QuestionId DESC",
                p =>
                {
                    p.AddWithValue("@SubjectId", (object)subjectId ?? DBNull.Value);
                    p.AddWithValue("@CategoryId", (object)categoryId ?? DBNull.Value);
                });
        }

        public static int CreateQuestion(int subjectId, string prompt, string optionA, string optionB,
            string optionC, string optionD, string correct, string explanation, string difficulty, int userId)
        {
            correct = (correct ?? "").Trim().ToUpperInvariant();
            if (correct != "A" && correct != "B" && correct != "C" && correct != "D")
                throw new Exception("Choose the correct option A-D.");
            string diff = (difficulty ?? "medium").Trim().ToLowerInvariant();
            if (diff != "easy" && diff != "medium" && diff != "hard") diff = "medium";
            return SqlHelper.ExecuteScalarInt(
                "INSERT INTO dbo.QuestionBank (SubjectId, Prompt, OptionA, OptionB, OptionC, OptionD, CorrectOption, " +
                "Explanation, Difficulty, CreatedBy) VALUES (@Subject, @Prompt, @A, @B, @C, @D, @Correct, @Explanation, @Difficulty, @By); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                p =>
                {
                    p.AddWithValue("@Subject", subjectId);
                    p.AddWithValue("@Prompt", prompt);
                    p.AddWithValue("@A", optionA);
                    p.AddWithValue("@B", optionB);
                    p.AddWithValue("@C", (object)NullIfEmpty(optionC) ?? DBNull.Value);
                    p.AddWithValue("@D", (object)NullIfEmpty(optionD) ?? DBNull.Value);
                    p.AddWithValue("@Correct", correct);
                    p.AddWithValue("@Explanation", (object)NullIfEmpty(explanation) ?? DBNull.Value);
                    p.AddWithValue("@Difficulty", diff);
                    p.AddWithValue("@By", userId);
                });
        }

        public static void ToggleQuestion(int questionId)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.QuestionBank SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END WHERE QuestionId = @Id",
                p => p.AddWithValue("@Id", questionId));
            AuditService.Log("QuestionToggled", "QuestionBank", questionId);
        }

        public static int CreateTest(int courseId, int subjectId, string title, string instructions,
            int durationMinutes, DateTime? opensUtc, DateTime? closesUtc, int maxAttempts, decimal passPercent,
            bool shuffle, bool proctoring, int userId, int warningLimit = 1)
        {
            if (opensUtc.HasValue && closesUtc.HasValue && closesUtc.Value <= opensUtc.Value)
                throw new Exception("Closing time must be after opening time.");
            if (durationMinutes < 1 || durationMinutes > 480) throw new Exception("Duration must be 1-480 minutes.");
            if (maxAttempts < 1 || maxAttempts > 10) throw new Exception("Attempts must be 1-10.");
            if (passPercent < 0 || passPercent > 100) throw new Exception("Pass percentage must be 0-100.");
            if (warningLimit < 0 || warningLimit > 10) throw new Exception("Warning limit must be 0-10.");
            return SqlHelper.ExecuteScalarInt(
                "INSERT INTO dbo.OnlineTests (CourseId, SubjectId, Title, Instructions, DurationMinutes, OpensAtUtc, " +
                "ClosesAtUtc, MaxAttempts, PassPercent, ShuffleQuestions, ProctoringEnabled, WarningLimit, CreatedBy) " +
                "VALUES (@Course, @Subject, @Title, @Instructions, @Duration, @Opens, @Closes, @Attempts, @Pass, @Shuffle, @Proctor, @Warn, @By); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                p =>
                {
                    p.AddWithValue("@Course", courseId);
                    p.AddWithValue("@Subject", subjectId);
                    p.AddWithValue("@Title", title);
                    p.AddWithValue("@Instructions", (object)NullIfEmpty(instructions) ?? DBNull.Value);
                    p.AddWithValue("@Duration", durationMinutes);
                    p.AddWithValue("@Opens", (object)opensUtc ?? DBNull.Value);
                    p.AddWithValue("@Closes", (object)closesUtc ?? DBNull.Value);
                    p.AddWithValue("@Attempts", maxAttempts);
                    p.AddWithValue("@Pass", passPercent);
                    p.AddWithValue("@Shuffle", shuffle ? 1 : 0);
                    p.AddWithValue("@Proctor", proctoring ? 1 : 0);
                    p.AddWithValue("@Warn", warningLimit);
                    p.AddWithValue("@By", userId);
                });
        }

        public static void UpdateTest(int testId, string title, string instructions, int durationMinutes,
            DateTime? opensUtc, DateTime? closesUtc, int maxAttempts, decimal passPercent, bool shuffle,
            bool proctoring, int warningLimit)
        {
            if (opensUtc.HasValue && closesUtc.HasValue && closesUtc.Value <= opensUtc.Value)
                throw new Exception("Closing time must be after opening time.");
            if (durationMinutes < 1 || durationMinutes > 480) throw new Exception("Duration must be 1-480 minutes.");
            if (maxAttempts < 1 || maxAttempts > 10) throw new Exception("Attempts must be 1-10.");
            if (passPercent < 0 || passPercent > 100) throw new Exception("Pass percentage must be 0-100.");
            if (warningLimit < 0 || warningLimit > 10) throw new Exception("Warning limit must be 0-10.");
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.OnlineTests SET Title=@Title, Instructions=@Instructions, DurationMinutes=@Duration, " +
                "OpensAtUtc=@Opens, ClosesAtUtc=@Closes, MaxAttempts=@Attempts, PassPercent=@Pass, " +
                "ShuffleQuestions=@Shuffle, ProctoringEnabled=@Proctor, WarningLimit=@Warn WHERE TestId=@Id",
                p =>
                {
                    p.AddWithValue("@Id", testId);
                    p.AddWithValue("@Title", title);
                    p.AddWithValue("@Instructions", (object)NullIfEmpty(instructions) ?? DBNull.Value);
                    p.AddWithValue("@Duration", durationMinutes);
                    p.AddWithValue("@Opens", (object)opensUtc ?? DBNull.Value);
                    p.AddWithValue("@Closes", (object)closesUtc ?? DBNull.Value);
                    p.AddWithValue("@Attempts", maxAttempts);
                    p.AddWithValue("@Pass", passPercent);
                    p.AddWithValue("@Shuffle", shuffle ? 1 : 0);
                    p.AddWithValue("@Proctor", proctoring ? 1 : 0);
                    p.AddWithValue("@Warn", warningLimit);
                });
        }

        public static string SetPublished(int testId, bool publish)
        {
            if (publish)
            {
                int count = SqlHelper.ExecuteScalarInt(
                    "SELECT COUNT(*) FROM dbo.OnlineTestQuestions WHERE TestId = @Id",
                    p => p.AddWithValue("@Id", testId));
                if (count == 0) return "Add at least one question before publishing.";
            }
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.OnlineTests SET IsPublished = @P WHERE TestId = @Id",
                p => { p.AddWithValue("@P", publish ? 1 : 0); p.AddWithValue("@Id", testId); });
            AuditService.Log(publish ? "TestPublished" : "TestUnpublished", "OnlineTests", testId);
            return null;
        }

        public static void DeleteTest(int testId)
        {
            SqlHelper.ExecuteNonQuery("DELETE FROM dbo.OnlineTests WHERE TestId = @Id",
                p => p.AddWithValue("@Id", testId));
            AuditService.Log("TestDeleted", "OnlineTests", testId);
        }

        public static void AddQuestion(int testId, int questionId, decimal points)
        {
            int subject = SqlHelper.ExecuteScalarInt(
                "SELECT SubjectId FROM dbo.OnlineTests WHERE TestId = @Id", p => p.AddWithValue("@Id", testId));
            int qSubject = SqlHelper.ExecuteScalarInt(
                "SELECT SubjectId FROM dbo.QuestionBank WHERE QuestionId = @Id", p => p.AddWithValue("@Id", questionId));
            if (subject == 0 || qSubject == 0) throw new Exception("Test or question not found.");
            if (subject != qSubject) throw new Exception("Question belongs to a different subject.");
            SqlHelper.ExecuteNonQuery(
                "IF NOT EXISTS (SELECT 1 FROM dbo.OnlineTestQuestions WHERE TestId = @Test AND QuestionId = @Question) " +
                "INSERT INTO dbo.OnlineTestQuestions (TestId, QuestionId, Points, SortOrder) " +
                "VALUES (@Test, @Question, @Points, ISNULL((SELECT MAX(SortOrder) + 1 FROM dbo.OnlineTestQuestions WHERE TestId = @Test), 0))",
                p =>
                {
                    p.AddWithValue("@Test", testId);
                    p.AddWithValue("@Question", questionId);
                    p.AddWithValue("@Points", points < 0.01m ? 1m : points);
                });
            AuditService.Log("TestQuestionAdded", "OnlineTests", testId, "question " + questionId);
        }

        public static void RemoveQuestion(int testId, int questionId)
        {
            SqlHelper.ExecuteNonQuery(
                "DELETE FROM dbo.OnlineTestQuestions WHERE TestId = @Test AND QuestionId = @Question",
                p => { p.AddWithValue("@Test", testId); p.AddWithValue("@Question", questionId); });
            AuditService.Log("TestQuestionRemoved", "OnlineTests", testId, "question " + questionId);
        }

        public static DataTable TestQuestions(int testId)
        {
            return SqlHelper.FillDataTable(
                "SELECT tq.QuestionId, tq.Points, tq.SortOrder, q.Prompt, q.CorrectOption, q.Difficulty, q.IsActive " +
                "FROM dbo.OnlineTestQuestions tq JOIN dbo.QuestionBank q ON q.QuestionId = tq.QuestionId " +
                "WHERE tq.TestId = @Id ORDER BY tq.SortOrder, tq.QuestionId",
                p => p.AddWithValue("@Id", testId));
        }

        public static DataTable TestAttempts(int testId)
        {
            return SqlHelper.FillDataTable(
                "SELECT a.AttemptId, a.StudentId, a.StartedAtUtc, a.ExpiresAtUtc, a.SubmittedAtUtc, a.Score, a.MaxScore, " +
                "a.Status, a.Violations, a.LastSavedAtUtc, s.EnrollNumber, u.FullName, u.Email " +
                "FROM dbo.OnlineTestAttempts a JOIN dbo.Students s ON s.StudentId = a.StudentId " +
                "JOIN dbo.Users u ON u.UserId = s.UserId WHERE a.TestId = @Id ORDER BY a.AttemptId DESC",
                p => p.AddWithValue("@Id", testId));
        }

        public static DataTable AttemptEvents(int attemptId)
        {
            return SqlHelper.FillDataTable(
                "SELECT EventId, EventType, Detail, OccurredAtUtc FROM dbo.OnlineTestEvents WHERE AttemptId = @Id ORDER BY EventId",
                p => p.AddWithValue("@Id", attemptId));
        }

        public static DataTable TestEvents(int testId)
        {
            return SqlHelper.FillDataTable(
                "SELECT TOP 50 e.EventId, e.AttemptId, e.EventType, e.Detail, e.OccurredAtUtc, s.EnrollNumber, u.FullName " +
                "FROM dbo.OnlineTestEvents e JOIN dbo.OnlineTestAttempts a ON a.AttemptId = e.AttemptId " +
                "JOIN dbo.Students s ON s.StudentId = a.StudentId JOIN dbo.Users u ON u.UserId = s.UserId " +
                "WHERE a.TestId = @Id ORDER BY e.EventId DESC",
                p => p.AddWithValue("@Id", testId));
        }

        public static DataTable TestsForStudent(int studentId)
        {
            return SqlHelper.FillDataTable(
                "SELECT t.TestId, t.Title, t.Instructions, t.DurationMinutes, t.OpensAtUtc, t.ClosesAtUtc, t.MaxAttempts, " +
                "t.PassPercent, t.ShuffleQuestions, t.ProctoringEnabled, c.CourseCode, s.SubjectCode, s.SubjectName, " +
                "(SELECT COUNT(*) FROM dbo.OnlineTestQuestions q WHERE q.TestId = t.TestId) AS QuestionCount, " +
                "(SELECT ISNULL(SUM(q.Points), 0) FROM dbo.OnlineTestQuestions q WHERE q.TestId = t.TestId) AS MaxScore, " +
                "(SELECT COUNT(*) FROM dbo.OnlineTestAttempts a WHERE a.TestId = t.TestId AND a.StudentId = @Sid) AS Attempts, " +
                "(SELECT TOP 1 a.AttemptId FROM dbo.OnlineTestAttempts a WHERE a.TestId = t.TestId AND a.StudentId = @Sid AND a.Status = 'in_progress' ORDER BY a.AttemptId DESC) AS ActiveAttemptId, " +
                "(SELECT MAX(a.Score) FROM dbo.OnlineTestAttempts a WHERE a.TestId = t.TestId AND a.StudentId = @Sid AND a.Status <> 'in_progress') AS BestScore, " +
                "(SELECT TOP 1 a.AttemptId FROM dbo.OnlineTestAttempts a WHERE a.TestId = t.TestId AND a.StudentId = @Sid AND a.Status <> 'in_progress' ORDER BY a.AttemptId DESC) AS LastAttemptId " +
                "FROM dbo.OnlineTests t " +
                "JOIN dbo.Courses c ON c.CourseId = t.CourseId " +
                "JOIN dbo.Subjects s ON s.SubjectId = t.SubjectId " +
                "JOIN dbo.Students st ON st.StudentId = @Sid " +
                "WHERE t.IsPublished = 1 AND t.CourseId = st.CourseId " +
                "ORDER BY CASE WHEN t.ClosesAtUtc IS NULL THEN 1 ELSE 0 END, t.ClosesAtUtc, t.TestId DESC",
                p => p.AddWithValue("@Sid", studentId));
        }

        public static int StartAttempt(int testId, int studentId)
        {
            using (var conn = ConnectionManager.CreateOpenConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    int course, subject, duration, maxAttempts, questionCount;
                    DateTime? opens, closes;
                    string title;
                    using (var cmd = new SqlCommand(
                        "SELECT CourseId, SubjectId, DurationMinutes, MaxAttempts, OpensAtUtc, ClosesAtUtc, Title, IsPublished " +
                        "FROM dbo.OnlineTests WITH (UPDLOCK, HOLDLOCK) WHERE TestId = @Id", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Id", testId);
                        using (var r = cmd.ExecuteReader())
                        {
                            if (!r.Read()) throw new Exception("Test not found.");
                            if (!Convert.ToBoolean(r["IsPublished"])) throw new Exception("This test is not published.");
                            course = Convert.ToInt32(r["CourseId"]);
                            subject = Convert.ToInt32(r["SubjectId"]);
                            duration = Convert.ToInt32(r["DurationMinutes"]);
                            maxAttempts = Convert.ToInt32(r["MaxAttempts"]);
                            opens = r["OpensAtUtc"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["OpensAtUtc"]);
                            closes = r["ClosesAtUtc"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ClosesAtUtc"]);
                            title = Convert.ToString(r["Title"]);
                        }
                    }

                    int studentCourse = SqlHelper.ExecuteScalarInt(
                        "SELECT CourseId FROM dbo.Students WHERE StudentId = @Sid", p => p.AddWithValue("@Sid", studentId));
                    if (studentCourse != course) throw new Exception("This test is not available for your course.");
                    DateTime now = DateTime.UtcNow;
                    if (opens.HasValue && now < opens.Value) throw new Exception("This test has not opened yet.");
                    if (closes.HasValue && now >= closes.Value) throw new Exception("This test has closed.");

                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM dbo.OnlineTestAttempts WHERE TestId = @Test AND StudentId = @Sid", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Test", testId);
                        cmd.Parameters.AddWithValue("@Sid", studentId);
                        if (Convert.ToInt32(cmd.ExecuteScalar()) >= maxAttempts)
                            throw new Exception("You have used all " + maxAttempts + " attempt(s).");
                    }
                    using (var cmd = new SqlCommand(
                        "SELECT TOP 1 AttemptId FROM dbo.OnlineTestAttempts WHERE TestId = @Test AND StudentId = @Sid " +
                        "AND Status = 'in_progress' ORDER BY AttemptId DESC", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Test", testId);
                        cmd.Parameters.AddWithValue("@Sid", studentId);
                        object active = cmd.ExecuteScalar();
                        if (active != null && active != DBNull.Value) { tx.Commit(); return Convert.ToInt32(active); }
                    }
                    decimal maxScore;
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*), ISNULL(SUM(Points), 0) FROM dbo.OnlineTestQuestions WHERE TestId = @Test", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Test", testId);
                        using (var r = cmd.ExecuteReader())
                        {
                            r.Read();
                            questionCount = Convert.ToInt32(r.GetValue(0));
                            maxScore = Convert.ToDecimal(r.GetValue(1));
                        }
                    }
                    if (questionCount == 0) throw new Exception("This test has no questions.");
                    DateTime expires = now.AddMinutes(duration);
                    if (closes.HasValue && closes.Value < expires) expires = closes.Value;

                    int attemptId;
                    using (var cmd = new SqlCommand(
                        "INSERT INTO dbo.OnlineTestAttempts (TestId, StudentId, ExpiresAtUtc, MaxScore) " +
                        "OUTPUT INSERTED.AttemptId VALUES (@Test, @Sid, @Expires, @Max)", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Test", testId);
                        cmd.Parameters.AddWithValue("@Sid", studentId);
                        cmd.Parameters.AddWithValue("@Expires", expires);
                        cmd.Parameters.AddWithValue("@Max", maxScore);
                        attemptId = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    tx.Commit();
                    AuditService.Log("TestAttemptStarted", "OnlineTests", testId, "attempt " + attemptId + " for student " + studentId);
                    return attemptId;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public static DataRow Attempt(int attemptId, int studentId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT a.AttemptId, a.TestId, a.StudentId, a.StartedAtUtc, a.ExpiresAtUtc, a.SubmittedAtUtc, " +
                "a.Score, a.MaxScore, a.Status, a.Violations, a.LastSavedAtUtc, a.IntegrityNote, t.Title, t.Instructions, " +
                "t.DurationMinutes, t.PassPercent, t.ShuffleQuestions, t.ProctoringEnabled, t.WarningLimit, t.ClosesAtUtc, " +
                "c.CourseCode, s.SubjectCode, s.SubjectName " +
                "FROM dbo.OnlineTestAttempts a JOIN dbo.OnlineTests t ON t.TestId = a.TestId " +
                "JOIN dbo.Courses c ON c.CourseId = t.CourseId JOIN dbo.Subjects s ON s.SubjectId = t.SubjectId " +
                "WHERE a.AttemptId = @Id AND a.StudentId = @Sid",
                p => { p.AddWithValue("@Id", attemptId); p.AddWithValue("@Sid", studentId); });
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        public static DataTable RunnerQuestions(int attemptId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT tq.QuestionId, tq.Points, q.Prompt, q.OptionA, q.OptionB, q.OptionC, q.OptionD, " +
                "a.SelectedOption, t.ShuffleQuestions " +
                "FROM dbo.OnlineTestAttempts att JOIN dbo.OnlineTests t ON t.TestId = att.TestId " +
                "JOIN dbo.OnlineTestQuestions tq ON tq.TestId = t.TestId " +
                "JOIN dbo.QuestionBank q ON q.QuestionId = tq.QuestionId " +
                "LEFT JOIN dbo.OnlineTestAnswers a ON a.AttemptId = att.AttemptId AND a.QuestionId = q.QuestionId " +
                "WHERE att.AttemptId = @Id ORDER BY CASE WHEN t.ShuffleQuestions = 1 THEN CHECKSUM(CONCAT(q.QuestionId, '|', att.AttemptId)) ELSE tq.SortOrder END, tq.QuestionId",
                p => p.AddWithValue("@Id", attemptId));
            return dt;
        }

        public static DataTable ResultQuestions(int attemptId)
        {
            return SqlHelper.FillDataTable(
                "SELECT q.QuestionId, q.Prompt, q.OptionA, q.OptionB, q.OptionC, q.OptionD, q.CorrectOption, q.Explanation, " +
                "tq.Points, a.SelectedOption, a.IsCorrect, a.PointsAwarded FROM dbo.OnlineTestAttempts att " +
                "JOIN dbo.OnlineTestQuestions tq ON tq.TestId = att.TestId " +
                "JOIN dbo.QuestionBank q ON q.QuestionId = tq.QuestionId " +
                "LEFT JOIN dbo.OnlineTestAnswers a ON a.AttemptId = att.AttemptId AND a.QuestionId = q.QuestionId " +
                "WHERE att.AttemptId = @Id ORDER BY tq.SortOrder, q.QuestionId",
                p => p.AddWithValue("@Id", attemptId));
        }

        public static bool SaveAnswer(int attemptId, int studentId, int questionId, string option)
        {
            option = (option ?? "").Trim().ToUpperInvariant();
            if (option.Length == 0) option = null;
            if (option != null && option != "A" && option != "B" && option != "C" && option != "D") return false;
            DataRow attempt = Attempt(attemptId, studentId);
            if (attempt == null) return false;
            if (!string.Equals(Convert.ToString(attempt["Status"]), "in_progress", StringComparison.OrdinalIgnoreCase)) return false;
            if (Convert.ToDateTime(attempt["ExpiresAtUtc"]) <= DateTime.UtcNow) return false;
            int allowed = SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.OnlineTestQuestions WHERE TestId = @Test AND QuestionId = @Q",
                p => { p.AddWithValue("@Test", Convert.ToInt32(attempt["TestId"])); p.AddWithValue("@Q", questionId); });
            if (allowed == 0) return false;
            SqlHelper.ExecuteNonQuery(
                "MERGE INTO dbo.OnlineTestAnswers AS t USING (SELECT @Attempt AS AttemptId, @Question AS QuestionId) s " +
                "ON t.AttemptId = s.AttemptId AND t.QuestionId = s.QuestionId " +
                "WHEN MATCHED THEN UPDATE SET SelectedOption = @Option, AnsweredAtUtc = SYSUTCDATETIME() " +
                "WHEN NOT MATCHED THEN INSERT (AttemptId, QuestionId, SelectedOption) VALUES (@Attempt, @Question, @Option); " +
                "UPDATE dbo.OnlineTestAttempts SET LastSavedAtUtc = SYSUTCDATETIME() WHERE AttemptId = @Attempt;",
                p =>
                {
                    p.AddWithValue("@Attempt", attemptId);
                    p.AddWithValue("@Question", questionId);
                    p.AddWithValue("@Option", (object)option ?? DBNull.Value);
                });
            return true;
        }

        public static DataRow SubmitAttempt(int attemptId, int studentId)
        {
            return SubmitAttempt(attemptId, studentId, null, null);
        }

        public static DataRow SubmitAttempt(int attemptId, int studentId, string forcedStatus, string note)
        {
            using (var conn = ConnectionManager.CreateOpenConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    DateTime? expires;
                    string status;
                    using (var cmd = new SqlCommand(
                        "SELECT ExpiresAtUtc, Status FROM dbo.OnlineTestAttempts WITH (UPDLOCK, HOLDLOCK) " +
                        "WHERE AttemptId = @Id AND StudentId = @Sid", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Id", attemptId);
                        cmd.Parameters.AddWithValue("@Sid", studentId);
                        using (var r = cmd.ExecuteReader())
                        {
                            if (!r.Read()) throw new Exception("Attempt not found.");
                            expires = Convert.ToDateTime(r["ExpiresAtUtc"]);
                            status = Convert.ToString(r["Status"]);
                        }
                    }
                    if (status != "in_progress")
                    {
                        tx.Commit();
                        return Attempt(attemptId, studentId);
                    }

                    using (var cmd = new SqlCommand(
                        "UPDATE a SET IsCorrect = CASE WHEN a.SelectedOption = q.CorrectOption THEN 1 ELSE 0 END, " +
                        "PointsAwarded = CASE WHEN a.SelectedOption = q.CorrectOption THEN tq.Points ELSE 0 END " +
                        "FROM dbo.OnlineTestAnswers a JOIN dbo.OnlineTestQuestions tq ON tq.TestId = " +
                        "(SELECT TestId FROM dbo.OnlineTestAttempts WHERE AttemptId = @Id) AND tq.QuestionId = a.QuestionId " +
                        "JOIN dbo.QuestionBank q ON q.QuestionId = a.QuestionId WHERE a.AttemptId = @Id", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Id", attemptId);
                        cmd.ExecuteNonQuery();
                    }

                    decimal score;
                    using (var cmd = new SqlCommand(
                        "SELECT ISNULL(SUM(PointsAwarded), 0) FROM dbo.OnlineTestAnswers WHERE AttemptId = @Id", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Id", attemptId);
                        score = Convert.ToDecimal(cmd.ExecuteScalar());
                    }

                    string finalStatus = !string.IsNullOrEmpty(forcedStatus)
                        ? forcedStatus
                        : (DateTime.UtcNow > expires.Value ? "expired" : "submitted");
                    using (var cmd = new SqlCommand(
                        "UPDATE dbo.OnlineTestAttempts SET Score = @Score, SubmittedAtUtc = SYSUTCDATETIME(), " +
                        "Status = @Status, IntegrityNote = COALESCE(@Note, IntegrityNote) WHERE AttemptId = @Id", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Score", score);
                        cmd.Parameters.AddWithValue("@Status", finalStatus);
                        cmd.Parameters.AddWithValue("@Note", (object)NullIfEmpty(note) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Id", attemptId);
                        cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                    AuditService.Log("TestAttemptSubmitted", "OnlineTests", attemptId, finalStatus + ", score " + score);
                    return Attempt(attemptId, studentId);
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public static ProctorResult ProctorEvent(int attemptId, int studentId, string eventType, string detail)
        {
            var result = new ProctorResult { Action = "none" };
            DataRow attempt = Attempt(attemptId, studentId);
            if (attempt == null) return result;
            if (!string.Equals(Convert.ToString(attempt["Status"]), "in_progress", StringComparison.OrdinalIgnoreCase))
            {
                result.Action = "closed";
                return result;
            }

            int warningLimit = attempt.Table.Columns.Contains("WarningLimit") && attempt["WarningLimit"] != DBNull.Value
                ? Convert.ToInt32(attempt["WarningLimit"]) : 1;
            eventType = (eventType ?? "unknown").Trim().ToLowerInvariant();
            if (eventType.Length == 0) eventType = "unknown";
            if (eventType.Length > 40) eventType = eventType.Substring(0, 40);
            detail = (detail ?? "").Trim();
            if (detail.Length > 300) detail = detail.Substring(0, 300);
            bool counts = eventType != "focus" && eventType != "heartbeat" && eventType != "resume";

            SqlHelper.ExecuteNonQuery(
                "INSERT INTO dbo.OnlineTestEvents (AttemptId, EventType, Detail) VALUES (@Id, @Type, @Detail); " +
                "IF @Counts = 1 UPDATE dbo.OnlineTestAttempts SET Violations = Violations + 1 WHERE AttemptId = @Id;",
                p =>
                {
                    p.AddWithValue("@Id", attemptId);
                    p.AddWithValue("@Type", eventType);
                    p.AddWithValue("@Detail", (object)NullIfEmpty(detail) ?? DBNull.Value);
                    p.AddWithValue("@Counts", counts ? 1 : 0);
                });

            result.Violations = SqlHelper.ExecuteScalarInt(
                "SELECT Violations FROM dbo.OnlineTestAttempts WHERE AttemptId = @Id",
                p => p.AddWithValue("@Id", attemptId));
            result.WarningLimit = warningLimit;

            if (!counts) return result;

            if (result.Violations > warningLimit)
            {
                SubmitAttempt(attemptId, studentId, "auto_submitted",
                    "Auto-submitted after " + result.Violations + " integrity events (limit " + warningLimit + ").");
                result.AutoSubmitted = true;
                result.Action = "submitted";
            }
            else
            {
                result.Action = "warning";
            }
            return result;
        }

        public static DataRow Student(int userId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT StudentId, CourseId, SessionId, SemesterNumber, EnrollNumber FROM dbo.Students WHERE UserId = @Id",
                p => p.AddWithValue("@Id", userId));
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        private static string NullIfEmpty(string value)
        {
            string v = (value ?? "").Trim();
            return v.Length == 0 ? null : v;
        }
    }
}

