-- ==============================================================
-- 06_StoredProcedures.sql - reporting + result-calculation procedures
-- ==============================================================
USE [StudentSMS];
GO
SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
GO

/* ------------------------------------------------------------------
 * Admin dashboard KPIs (single wide row)
 * ------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_GetAdminDashboard
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today DATE = CAST(SYSUTCDATETIME() AS DATE);

    SELECT
      (SELECT COUNT(*) FROM dbo.Students s JOIN dbo.Users u ON u.UserId = s.UserId WHERE u.IsActive = 1)                       AS TotalStudents,
      (SELECT COUNT(*) FROM dbo.Faculty f JOIN dbo.Users u ON u.UserId = f.UserId WHERE u.IsActive = 1)                        AS TotalFaculty,
      (SELECT COUNT(*) FROM dbo.Courses WHERE IsActive = 1)                                                                    AS TotalCourses,
      (SELECT COUNT(*) FROM dbo.Subjects WHERE IsActive = 1)                                                                   AS TotalSubjects,
      (SELECT COUNT(*) FROM dbo.Complaints WHERE Status IN ('open','in_progress'))                                             AS OpenComplaints,
      (SELECT COUNT(*) FROM dbo.Exams WHERE IsPublished = 0)                                                                   AS DraftExams,
      (SELECT COUNT(*) FROM dbo.Payments WHERE PayStatus = 'captured' AND CAST(CapturedAtUtc AS DATE) = @today)                 AS PaymentsTodayCount,
      (SELECT ISNULL(SUM(AmountInPaise),0)/100.0 FROM dbo.Payments WHERE PayStatus = 'captured' AND CAST(CapturedAtUtc AS DATE) = @today) AS PaymentsTodayAmount,
      (SELECT COUNT(DISTINCT AttnSessionId) FROM dbo.AttendanceSessions WHERE SessionDate = @today)                             AS AttendanceSessionsToday,
      (SELECT COUNT(*) FROM dbo.Users WHERE IsActive = 1 AND CAST(CreatedAtUtc AS DATE) >= DATEADD(DAY, -7, @today))            AS NewUsersThisWeek;
END
GO

/* ------------------------------------------------------------------
 * Attendance trend (per-day, for charts) - no divide-by-zero
 * ------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_GetAttendanceTrend
    @NoDays INT = 14
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
      asn.SessionDate,
      COUNT(a.AttnId)                                            AS ClassesHeld,
      SUM(CASE WHEN a.AttnStatus IN ('P','L','E') THEN 1 ELSE 0 END) AS PresentCount,
      CAST(ROUND(100.0 * SUM(CASE WHEN a.AttnStatus IN ('P','L','E') THEN 1 ELSE 0 END) / NULLIF(COUNT(a.AttnId),0), 1) AS DECIMAL(5,2)) AS PercentPresent
    FROM dbo.AttendanceSessions asn
    JOIN dbo.Attendance a ON a.AttnSessionId = asn.AttnSessionId
    WHERE asn.SessionDate >= DATEADD(DAY, -(@NoDays), CAST(SYSUTCDATETIME() AS DATE))
    GROUP BY asn.SessionDate
    ORDER BY asn.SessionDate DESC;
END
GO

/* ------------------------------------------------------------------
 * Course-wise enrollment distribution
 * ------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_GetCourseDistribution
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.CourseCode, c.CourseName,
           COUNT(DISTINCT e.StudentId) AS EnrolledStudents
    FROM dbo.Courses c
    LEFT JOIN dbo.Enrollments e ON e.CourseId = c.CourseId AND e.IsActive = 1
    WHERE c.IsActive = 1
    GROUP BY c.CourseId, c.CourseCode, c.CourseName
    ORDER BY c.CourseCode;
END
GO

/* ------------------------------------------------------------------
 * Course-wise average score across published exams
 * ------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_GetPerformanceByCourse
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.CourseCode, c.CourseName,
           CAST(ROUND(AVG(100.0 * m.MarksObtained / ex.MaxMarks), 1) AS DECIMAL(5,1)) AS AvgPercent
    FROM dbo.Marks m
    JOIN dbo.Exams ex ON ex.ExamId = m.ExamId AND ex.IsPublished = 1
    JOIN dbo.Courses c ON c.CourseId = ex.CourseId
    GROUP BY c.CourseId, c.CourseCode, c.CourseName
    ORDER BY c.CourseCode;
END
GO

/* ------------------------------------------------------------------
 * Faculty dashboard
 * ------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_GetFacultyDashboard
    @FacultyUserId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @sess INT = (SELECT TOP 1 SessionId FROM dbo.AcademicSessions ORDER BY IsActive DESC, SessionId);
    DECLARE @today DATE = CAST(SYSUTCDATETIME() AS DATE);

    -- 1) Assigned subjects with enrolled count
    SELECT fa.CourseId, fa.SubjectId, fa.SemesterNumber,
           c.CourseCode, s.SubjectName, s.SubjectCode,
           COUNT(DISTINCT e.StudentId) AS EnrolledCount
    FROM dbo.Faculty f
    JOIN dbo.FacultyAssignments fa ON fa.FacultyId = f.FacultyId
         AND fa.SessionId = @sess
    JOIN dbo.Courses c  ON c.CourseId  = fa.CourseId
    JOIN dbo.Subjects s ON s.SubjectId = fa.SubjectId
    LEFT JOIN dbo.Enrollments e ON e.CourseId = fa.CourseId
         AND e.SessionId = fa.SessionId AND e.IsActive = 1
    WHERE f.UserId = @FacultyUserId
    GROUP BY fa.CourseId, fa.SubjectId, fa.SemesterNumber, c.CourseCode, s.SubjectName, s.SubjectCode
    ORDER BY c.CourseCode, s.SubjectCode;

    -- 2) Sessions taken today
    SELECT asn.AttnSessionId, s.SubjectName, asn.SessionDate, asn.PeriodNo,
           SUM(CASE WHEN a.AttnStatus = 'P' THEN 1 ELSE 0 END) AS PresentCount,
           COUNT(a.AttnId) AS MarkedCount
    FROM dbo.AttendanceSessions asn
    JOIN dbo.Subjects s ON s.SubjectId = asn.SubjectId
    LEFT JOIN dbo.Attendance a ON a.AttnSessionId = asn.AttnSessionId
    WHERE asn.FacultyUserId = @FacultyUserId AND asn.SessionDate = @today
    GROUP BY asn.AttnSessionId, s.SubjectName, asn.SessionDate, asn.PeriodNo;

    -- 3) Draft (unpublished) exams for my subjects
    SELECT ex.ExamId, ex.ExamName, s.SubjectName, ex.ExamDate,
           (SELECT COUNT(*) FROM dbo.Marks m WHERE m.ExamId = ex.ExamId) AS MarksEntered
    FROM dbo.Faculty f
    JOIN dbo.FacultyAssignments fa ON fa.FacultyId = f.FacultyId AND fa.SessionId = @sess
    JOIN dbo.Exams ex ON ex.CourseId = fa.CourseId AND ex.SubjectId = fa.SubjectId
    JOIN dbo.Subjects s ON s.SubjectId = ex.SubjectId
    WHERE f.UserId = @FacultyUserId AND ex.IsPublished = 0
    GROUP BY ex.ExamId, ex.ExamName, s.SubjectName, ex.ExamDate
    ORDER BY ex.ExamDate DESC;

    -- 4) Recent notifications
    SELECT TOP 5 n.NotificationId, n.Title, n.Message, r.IsRead, n.CreatedAtUtc, n.ActionUrl
    FROM dbo.NotificationRecipients r
    JOIN dbo.Notifications n ON n.NotificationId = r.NotificationId
    WHERE r.UserId = @FacultyUserId
    ORDER BY n.CreatedAtUtc DESC;
END
GO

/* ------------------------------------------------------------------
 * Student dashboard
 * ------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_GetStudentDashboard
    @StudentUserId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @studentId INT = (SELECT StudentId FROM dbo.Students WHERE UserId = @StudentUserId);
    DECLARE @sess INT  = (SELECT TOP 1 SessionId FROM dbo.AcademicSessions ORDER BY IsActive DESC, SessionId);
    DECLARE @minPct DECIMAL(5,2) = CAST((SELECT SettingValue FROM dbo.AppSettings WHERE SettingKey = 'MinAttendancePercent') AS DECIMAL(5,2));

    -- 1) Overall attendance
    SELECT
      COUNT(a.AttnId)                                               AS ClassesHeld,
      SUM(CASE WHEN a.AttnStatus IN ('P','L','E') THEN 1 ELSE 0 END) AS ClassesAttended,
      CAST(ROUND(100.0 * SUM(CASE WHEN a.AttnStatus IN ('P','L','E') THEN 1 ELSE 0 END) / NULLIF(COUNT(a.AttnId),0), 1) AS DECIMAL(5,2)) AS AttendancePercent,
      @minPct                                                        AS MinimumRequired
    FROM dbo.Enrollments e
    JOIN dbo.Attendance a ON a.EnrollId = e.EnrollId
    WHERE e.StudentId = @studentId AND e.SessionId = @sess;

    -- 2) Subject-wise attendance
    SELECT s.SubjectName, s.SubjectCode,
           COUNT(a.AttnId)                                               AS Held,
           SUM(CASE WHEN a.AttnStatus IN ('P','L','E') THEN 1 ELSE 0 END) AS Attended,
           CAST(ROUND(100.0 * SUM(CASE WHEN a.AttnStatus IN ('P','L','E') THEN 1 ELSE 0 END) / NULLIF(COUNT(a.AttnId),0), 1) AS DECIMAL(5,2)) AS PercentPresent
    FROM dbo.Enrollments e
    JOIN dbo.Attendance                          a ON a.EnrollId     = e.EnrollId
    JOIN dbo.AttendanceSessions                  asn ON asn.AttnSessionId = a.AttnSessionId
    JOIN dbo.Subjects                            s ON s.SubjectId   = asn.SubjectId
    WHERE e.StudentId = @studentId AND e.SessionId = @sess
    GROUP BY s.SubjectId, s.SubjectName, s.SubjectCode
    ORDER BY s.SubjectCode;

    -- 3) Latest results (top 5)
    SELECT TOP 5 ex.ExamName, s.SubjectName, r.MarksObtained, r.MaxMarks,
                 r.PercentScore, r.Grade, r.IsPass, r.PublishedAtUtc
    FROM dbo.Results r
    JOIN dbo.Exams ex    ON ex.ExamId   = r.ExamId
    JOIN dbo.Subjects s  ON s.SubjectId = ex.SubjectId
    JOIN dbo.Enrollments e ON e.EnrollId = r.EnrollId
    WHERE e.StudentId = @studentId
    ORDER BY r.PublishedAtUtc DESC;

    -- 4) Open complaints
    SELECT COUNT(*) AS OpenComplaints
    FROM dbo.Complaints
    WHERE RaisedBy = @StudentUserId AND Status IN ('open','in_progress');

    -- 5) Fees (not yet paid in this session)
    SELECT f.FeeId, f.FeeName, f.Amount,
           (SELECT COUNT(*) FROM dbo.Payments p
            WHERE p.FeeId = f.FeeId AND p.UserId = @StudentUserId AND p.PayStatus = 'captured') AS PaidCount
    FROM dbo.Fees f
    JOIN dbo.Courses c ON c.CourseId = f.CourseId
    JOIN dbo.Students st ON st.CourseId = c.CourseId
    WHERE st.StudentId = @studentId AND f.IsActive = 1;

    -- 6) Recent notifications (top 5)
    SELECT TOP 5 n.NotificationId, n.Title, n.Message, r.IsRead, n.CreatedAtUtc, n.ActionUrl
    FROM dbo.NotificationRecipients r
    JOIN dbo.Notifications n ON n.NotificationId = r.NotificationId
    WHERE r.UserId = @StudentUserId
    ORDER BY n.CreatedAtUtc DESC;
END
GO

/* ------------------------------------------------------------------
 * Date-wise attendance detail for one student (optionally one subject)
 * ------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_GetStudentAttendanceDetail
    @StudentUserId INT,
    @SubjectId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT asn.SessionDate, s.SubjectName, s.SubjectCode, a.AttnStatus, a.Remarks, asn.PeriodNo
    FROM dbo.Enrollments e
    JOIN dbo.Attendance         a   ON a.EnrollId = e.EnrollId
    JOIN dbo.AttendanceSessions asn ON asn.AttnSessionId = a.AttnSessionId
    JOIN dbo.Subjects           s   ON s.SubjectId = asn.SubjectId
    JOIN dbo.Students           st  ON st.StudentId = e.StudentId
    WHERE st.UserId = @StudentUserId
      AND (@SubjectId IS NULL OR s.SubjectId = @SubjectId)
    ORDER BY asn.SessionDate DESC, s.SubjectCode;
END
GO

/* ------------------------------------------------------------------
 * Full result sheet for an exam (admin results page)
 * ------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_GetExamResultSheet
    @ExamId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT st.EnrollNumber, u.FullName, m.MarksObtained,
           ex.MaxMarks,
           CAST(ROUND(100.0 * m.MarksObtained / ex.MaxMarks, 2) AS DECIMAL(5,2)) AS PercentScore,
           r.Grade, r.IsPass
    FROM dbo.Exams ex
    LEFT JOIN dbo.Marks    m  ON m.ExamId = ex.ExamId
    LEFT JOIN dbo.Enrollments e ON e.EnrollId = m.EnrollId
    LEFT JOIN dbo.Students    st ON st.StudentId = e.StudentId
    LEFT JOIN dbo.Users       u  ON u.UserId = st.UserId
    LEFT JOIN dbo.Results     r  ON r.ExamId = ex.ExamId AND r.EnrollId = e.EnrollId
    WHERE ex.ExamId = @ExamId
    ORDER BY ISNULL(m.MarksObtained, -1) DESC;
END
GO

/* ------------------------------------------------------------------
 * Result sheet for one student (their published results only)
 * ------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_GetStudentResultSheet
    @StudentUserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ex.ExamName, s.SubjectName, s.SubjectCode, ex.ExamDate,
           r.MarksObtained, r.MaxMarks, r.PercentScore, r.Grade, r.IsPass, r.PublishedAtUtc
    FROM dbo.Results r
    JOIN dbo.Exams ex    ON ex.ExamId   = r.ExamId
    JOIN dbo.Subjects s  ON s.SubjectId = ex.SubjectId
    JOIN dbo.Enrollments e ON e.EnrollId = r.EnrollId
    JOIN dbo.Students st ON st.StudentId = e.StudentId
    WHERE st.UserId = @StudentUserId
    ORDER BY r.PublishedAtUtc DESC;
END
GO

/* ------------------------------------------------------------------
 * Calculate + publish results for an exam (idempotent upsert)
 * ------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_CalculateExamResults
    @ExamId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;

    -- Guard: every enrolled (active) student of that course must have marks
    IF EXISTS (
      SELECT 1 FROM dbo.Enrollments e
      WHERE e.CourseId  = (SELECT CourseId FROM dbo.Exams WHERE ExamId = @ExamId)
        AND e.SessionId IN (SELECT TOP 1 SessionId FROM dbo.AcademicSessions ORDER BY IsActive DESC, SessionId)
        AND e.IsActive = 1
        AND NOT EXISTS (SELECT 1 FROM dbo.Marks m WHERE m.ExamId = @ExamId AND m.EnrollId = e.EnrollId))
    BEGIN
        ROLLBACK TRAN;
        RAISERROR ('Marks for some enrolled students are missing.', 16, 1);
        RETURN;
    END

    UPDATE ex SET IsPublished = 1, PublishedAtUtc = SYSUTCDATETIME()
    FROM dbo.Exams ex WHERE ex.ExamId = @ExamId AND ex.IsPublished = 0;

    MERGE dbo.Results AS tgt
    USING (
        SELECT m.EnrollId, m.ExamId, m.MarksObtained, ex.MaxMarks, ex.PassPercent,
               CAST(ROUND(100.0 * m.MarksObtained / ex.MaxMarks, 2) AS DECIMAL(5,2)) AS Pct
        FROM dbo.Marks m
        JOIN dbo.Exams ex ON ex.ExamId = m.ExamId
        WHERE m.ExamId = @ExamId
    ) AS src ON tgt.EnrollId = src.EnrollId AND tgt.ExamId = src.ExamId
    WHEN MATCHED THEN
        UPDATE SET MarksObtained = src.MarksObtained, MaxMarks = src.MaxMarks,
                   PercentScore = src.Pct,
                   Grade = (SELECT TOP 1 g.Grade FROM dbo.GradeScale g WHERE g.MinPercent <= src.Pct ORDER BY g.MinPercent DESC),
                   IsPass = CASE WHEN src.Pct >= src.PassPercent THEN 1 ELSE 0 END,
                   PublishedAtUtc = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (ExamId, EnrollId, MarksObtained, MaxMarks, PercentScore, Grade, IsPass, PublishedAtUtc)
        VALUES (src.ExamId, src.EnrollId, src.MarksObtained, src.MaxMarks, src.Pct,
                (SELECT TOP 1 g.Grade FROM dbo.GradeScale g WHERE g.MinPercent <= src.Pct ORDER BY g.MinPercent DESC),
                CASE WHEN src.Pct >= src.PassPercent THEN 1 ELSE 0 END, SYSUTCDATETIME());

    COMMIT TRAN;
END
GO
PRINT N'Stored procedures created.';
