-- ==============================================================
-- 08_SmokeTests.sql - post-install verification
--
-- Run AFTER 01..06. Every check adds a row to #fail when it does not
-- hold; the script prints the failures and raises an error, so it can
-- be used from sqlcmd / CI and returns a non-zero exit code on failure.
--
--   sqlcmd -S "(LocalDB)\MSSQLLocalDB" -d StudentSMS -b -i 08_SmokeTests.sql
--
-- It is read-only: nothing is inserted into application tables.
-- ==============================================================
USE [StudentSMS];
GO
SET NOCOUNT ON;

CREATE TABLE #fail (CheckName NVARCHAR(160) NOT NULL, Detail NVARCHAR(400) NULL);
GO

/* ---------- 1. tables ---------- */
DECLARE @tables TABLE (name SYSNAME PRIMARY KEY);
INSERT INTO @tables (name) VALUES
 ('Roles'),('Users'),('UserRoles'),('ExternalLogins'),('OtpCodes'),('PasswordResetTokens'),
 ('AuditLogs'),('AppSettings'),('AcademicSessions'),('Semesters'),('Courses'),('Subjects'),
 ('CourseSubjects'),('Faculty'),('Students'),('Enrollments'),('FacultyAssignments'),
 ('AttendanceSessions'),('Attendance'),('Exams'),('Marks'),('GradeScale'),('Results'),
 ('Notifications'),('NotificationRecipients'),('NotificationOutbox'),('PushSubscriptions'),
 ('ComplaintCategories'),('Complaints'),('ComplaintThreads'),('Fees'),('Payments'),
 ('PaymentEvents'),('Announcements'),('ChatLogs'),('FrequentlyAsked'),('ImportJobs'),('ExportJobs');

INSERT INTO #fail (CheckName, Detail)
SELECT 'Missing table dbo.' + t.name, 'Run 02_Tables.sql'
FROM @tables t
WHERE OBJECT_ID(N'dbo.' + t.name, N'U') IS NULL;

/* ---------- 2. stored procedures ---------- */
DECLARE @procs TABLE (name SYSNAME PRIMARY KEY);
INSERT INTO @procs (name) VALUES
 ('usp_GetAdminDashboard'),('usp_GetAttendanceTrend'),('usp_GetCourseDistribution'),
 ('usp_GetPerformanceByCourse'),('usp_GetFacultyDashboard'),('usp_GetStudentDashboard'),
 ('usp_GetStudentAttendanceDetail'),('usp_GetExamResultSheet'),('usp_GetStudentResultSheet'),
 ('usp_CalculateExamResults');

INSERT INTO #fail (CheckName, Detail)
SELECT 'Missing procedure dbo.' + p.name, 'Run 06_StoredProcedures.sql'
FROM @procs p
WHERE OBJECT_ID(N'dbo.' + p.name, N'P') IS NULL;

/* ---------- 3. relationships and constraints ---------- */
IF (SELECT COUNT(*) FROM sys.foreign_keys) < 25
    INSERT INTO #fail VALUES ('Too few foreign keys', 'Expected >= 25, found ' + CAST((SELECT COUNT(*) FROM sys.foreign_keys) AS NVARCHAR(10)));

IF OBJECT_ID(N'dbo.UQ_AttnRow', N'UQ') IS NULL
    INSERT INTO #fail VALUES ('UQ_AttnRow missing', 'One attendance row per student per session is required');

IF OBJECT_ID(N'dbo.CK_Attn_Status', N'C') IS NULL
    INSERT INTO #fail VALUES ('CK_Attn_Status missing', 'Attendance status must be limited to P/A/L/E');

IF OBJECT_ID(N'dbo.CK_Pay_Status', N'C') IS NULL
    INSERT INTO #fail VALUES ('CK_Pay_Status missing', 'Payment status must be limited to created/pending/captured/failed/cancelled/refunded');

IF OBJECT_ID(N'dbo.UQ_PayEvents', N'UQ') IS NULL
    INSERT INTO #fail VALUES ('UQ_PayEvents missing', 'Webhook idempotency index missing');

/* ---------- 4. indexes used by the hot queries ---------- */
DECLARE @indexes TABLE (name SYSNAME PRIMARY KEY);
INSERT INTO @indexes (name) VALUES
 ('IX_Users_EmailVerified'),('IX_Students_Course'),('IX_Enroll_Student'),('IX_Enroll_Course'),
 ('IX_AttnSession_Date'),('IX_AttnSession_Course'),('IX_Marks_Enroll'),('IX_Results_Enroll'),
 ('IX_NRec_UserId_Read'),('IX_Outbox_State'),('IX_Audit_Action'),('IX_Compl_Status'),
 ('IX_Pay_Users'),('IX_Pay_Status'),('IX_Exams_Course'),('IX_Faq_Scope');

INSERT INTO #fail (CheckName, Detail)
SELECT 'Missing index ' + i.name, 'Run 03_Indexes.sql'
FROM @indexes i
WHERE NOT EXISTS (SELECT 1 FROM sys.indexes x WHERE x.name = i.name);

/* ---------- 5. seed data ---------- */
IF (SELECT COUNT(*) FROM dbo.Roles) < 3
    INSERT INTO #fail VALUES ('Roles not seeded', 'Expected Admin, Faculty, Student');

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = 'Admin')
    INSERT INTO #fail VALUES ('Role Admin missing', NULL);

IF (SELECT COUNT(*) FROM dbo.AppSettings) < 5
    INSERT INTO #fail VALUES ('AppSettings not seeded', 'Attendance/payment settings are read from this table');

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'MinAttendancePercent')
    INSERT INTO #fail VALUES ('MinAttendancePercent setting missing', 'usp_GetStudentDashboard reads it');

IF NOT EXISTS (SELECT 1 FROM dbo.GradeScale)
    INSERT INTO #fail VALUES ('GradeScale empty', 'Result sheets need grading rows');

IF NOT EXISTS (SELECT 1 FROM dbo.ComplaintCategories)
    INSERT INTO #fail VALUES ('ComplaintCategories empty', 'Students cannot raise a categorised ticket');

IF NOT EXISTS (SELECT 1 FROM dbo.FrequentlyAsked)
    INSERT INTO #fail VALUES ('FrequentlyAsked empty', 'The chatbot FAQ fallback would have no answers');

/* ---------- 6. demo accounts and password storage ---------- */
IF (SELECT COUNT(*) FROM dbo.Users) < 3
    INSERT INTO #fail VALUES ('Demo users missing', 'Run 04_Seed.sql');

INSERT INTO #fail (CheckName, Detail)
SELECT 'Password not hashed for ' + Email, 'Expected PBKDF2 "iterations.salt.hash"'
FROM dbo.Users
WHERE PasswordHash NOT LIKE '%.%.%' OR LEN(PasswordHash) < 50 OR PasswordHash LIKE '%@123%';

IF NOT EXISTS (
    SELECT 1 FROM dbo.Users u
    JOIN dbo.UserRoles ur ON ur.UserId = u.UserId
    JOIN dbo.Roles r ON r.RoleId = ur.RoleId
    WHERE r.RoleName = 'Admin')
    INSERT INTO #fail VALUES ('No administrator account', 'The admin area would be unreachable');

/* ---------- 7. data integrity in existing rows ---------- */
IF EXISTS (SELECT 1 FROM dbo.Attendance GROUP BY AttnSessionId, EnrollId HAVING COUNT(*) > 1)
    INSERT INTO #fail VALUES ('Duplicate attendance rows', 'Same student marked twice in one session');

IF EXISTS (SELECT 1 FROM dbo.Attendance WHERE AttnStatus NOT IN ('P','A','L','E'))
    INSERT INTO #fail VALUES ('Invalid attendance status stored', 'Expected P/A/L/E');

IF EXISTS (SELECT 1 FROM dbo.Marks m JOIN dbo.Exams e ON e.ExamId = m.ExamId
           WHERE m.MarksObtained < 0 OR m.MarksObtained > e.MaxMarks)
    INSERT INTO #fail VALUES ('Marks outside 0..MaxMarks', 'A mark exceeds its exam maximum');

IF EXISTS (SELECT 1 FROM dbo.Students s JOIN dbo.Users u ON u.UserId = s.UserId WHERE s.CourseId IS NULL)
    INSERT INTO #fail VALUES ('Student without course', 'Every student must be assigned to a course');

IF EXISTS (SELECT 1 FROM dbo.Payments WHERE PayStatus = 'captured' AND GatewayPaymentId IS NULL)
    INSERT INTO #fail VALUES ('Captured payment without gateway id', 'Server-side verification was bypassed');

/* ---------- 8. report procedures must actually execute ---------- */
BEGIN TRY
    EXEC dbo.usp_GetAdminDashboard;
END TRY
BEGIN CATCH
    INSERT INTO #fail VALUES ('usp_GetAdminDashboard failed', ERROR_MESSAGE());
END CATCH;

BEGIN TRY
    EXEC dbo.usp_GetAttendanceTrend @NoDays = 14;
END TRY
BEGIN CATCH
    INSERT INTO #fail VALUES ('usp_GetAttendanceTrend failed', ERROR_MESSAGE());
END CATCH;

BEGIN TRY
    EXEC dbo.usp_GetCourseDistribution;
END TRY
BEGIN CATCH
    INSERT INTO #fail VALUES ('usp_GetCourseDistribution failed', ERROR_MESSAGE());
END CATCH;

DECLARE @studentUser INT = (SELECT TOP (1) UserId FROM dbo.Students ORDER BY StudentId);
IF @studentUser IS NOT NULL
BEGIN
    BEGIN TRY
        EXEC dbo.usp_GetStudentDashboard @StudentUserId = @studentUser;
    END TRY
    BEGIN CATCH
        INSERT INTO #fail VALUES ('usp_GetStudentDashboard failed', ERROR_MESSAGE());
    END CATCH;
END

DECLARE @facultyUser INT = (SELECT TOP (1) UserId FROM dbo.Faculty ORDER BY FacultyId);
IF @facultyUser IS NOT NULL
BEGIN
    BEGIN TRY
        EXEC dbo.usp_GetFacultyDashboard @FacultyUserId = @facultyUser;
    END TRY
    BEGIN CATCH
        INSERT INTO #fail VALUES ('usp_GetFacultyDashboard failed', ERROR_MESSAGE());
    END CATCH;
END

/* ---------- 9. report ---------- */
IF EXISTS (SELECT 1 FROM #fail)
BEGIN
    SELECT CheckName AS FailedCheck, Detail FROM #fail ORDER BY CheckName;
    DECLARE @msg NVARCHAR(200) = N'Smoke tests FAILED: ' + CAST((SELECT COUNT(*) FROM #fail) AS NVARCHAR(10)) + N' check(s)';
    THROW 51000, @msg, 1;
END

PRINT N'All smoke tests passed: schema, constraints, indexes, seed data, hashes and report procedures.';
DROP TABLE #fail;
GO
