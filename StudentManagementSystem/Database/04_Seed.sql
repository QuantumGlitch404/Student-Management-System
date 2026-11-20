-- ==============================================================
-- 04_Seed.sql - required reference data. Idempotent (IF NOT EXISTS guards).
-- Dev/demo passwords (CHANGE THESE for any real deployment):
--   admin@sms.edu    / Admin@123
--   faculty@sms.edu  / Faculty@123
--   student@sms.edu  / Student@123
-- Hash format = "iterations.saltBase64.hashBase64" (PBKDF2-SHA256, 100k iterations).
-- ==============================================================
USE [StudentSMS];
GO
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM dbo.Roles)
BEGIN
    INSERT dbo.Roles (RoleName, Description) VALUES
      (N'Admin',   N'System administrator - full control'),
      (N'Faculty', N'Teaching staff'),
      (N'Student', N'Enrolled student');
END
GO

-- Core accounts ---------------------------------------------------------------
DECLARE @adminId INT, @facultyId INT, @studentId INT;

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'admin@sms.edu')
BEGIN
    INSERT dbo.Users (FullName, Email, PasswordHash, EmailVerified, IsActive, ConsentAccepted)
    VALUES (N'Administrator', N'admin@sms.edu',
            N'100000.acVWLIV25be0XtMouAFl2w==.gxFRARPZfwmjSVSPVX5HMPWl60lk9TSh1h/YNtUilSQ=', 1, 1, 1);
END
SELECT @adminId = UserId FROM dbo.Users WHERE Email = N'admin@sms.edu';

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'faculty@sms.edu')
BEGIN
    INSERT dbo.Users (FullName, Email, PasswordHash, EmailVerified, IsActive, ConsentAccepted)
    VALUES (N'Dr. Anil Mehta', N'faculty@sms.edu',
            N'100000.lszMPs+rO+6JsWQhS/zNIQ==.K5TtGuD8LG5AIhVAPyBWhdiyJ2xSbABWN6QSRL8ARik=', 1, 1, 1);
END
SELECT @facultyId = UserId FROM dbo.Users WHERE Email = N'faculty@sms.edu';

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'student@sms.edu')
BEGIN
    INSERT dbo.Users (FullName, Email, PasswordHash, EmailVerified, IsActive, ConsentAccepted)
    VALUES (N'Rahul Sharma', N'student@sms.edu',
            N'100000.rMEshCCtir+0Dik0nwQxzQ==.LdkA97tiIdo+ZMQOC85dnrgF+kOLr7UucD/4kGxzRsA=', 1, 1, 1);
END
SELECT @studentId = UserId FROM dbo.Users WHERE Email = N'student@sms.edu';

-- Idempotent: the installer re-runs this script when upgrading an existing database.
INSERT dbo.UserRoles (UserId, RoleId)
SELECT v.UserId, v.RoleId
FROM (VALUES (@adminId, 1), (@facultyId, 2), (@studentId, 3)) AS v(UserId, RoleId)
WHERE NOT EXISTS (SELECT 1 FROM dbo.UserRoles ur WHERE ur.UserId = v.UserId AND ur.RoleId = v.RoleId);
GO

-- Demo faculty profile --------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Faculty)
BEGIN
    INSERT dbo.Faculty (UserId, DeptCode, Designation, JoinedOn)
    SELECT UserId, N'CSE', N'Associate Professor', '2019-07-01'
    FROM dbo.Users WHERE Email = N'faculty@sms.edu';
END
GO

-- Password policy / low-water-mark settings -----------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'MinPasswordLength')
BEGIN
    INSERT dbo.AppSettings (SettingKey, SettingValue, Description, ModifiedBy)
    VALUES
      (N'MinPasswordLength',   N'8',    N'Minimum password length',                     NULL),
      (N'MinAttendancePercent',N'75',   N'Attendance below this % generates a warning',  NULL),
      (N'OtpExpiryMinutes',    N'10',   N'OTP validity window in minutes',               NULL),
      (N'OtpMaxAttempts',      N'3',    N'Maximum failed OTP attempts per code',         NULL),
      (N'LockoutAfterFailures',N'5',    N'Failed logins before temporary lockout',       NULL),
      (N'LockoutMinutes',      N'15',   N'Account lockout duration in minutes',          NULL);
END
GO

-- Grade scale (configurable; no invented university rules, editable in DB) -----
IF NOT EXISTS (SELECT 1 FROM dbo.GradeScale)
BEGIN
    INSERT dbo.GradeScale (MinPercent, Grade, Remark) VALUES
      (85.00, N'A+', N'Outstanding'),
      (70.00, N'A',  N'Excellent'),
      (60.00, N'B',  N'Very good'),
      (50.00, N'C',  N'Good'),
      (33.00, N'D',  N'Pass'),
      (0.00,  N'F',  N'Fail - reappear');
END
GO

-- Academic session ------------------------------------------------------------
DECLARE @sessionId INT;
IF NOT EXISTS (SELECT 1 FROM dbo.AcademicSessions)
BEGIN
    INSERT dbo.AcademicSessions (SessionName, StartDate, EndDate, IsActive)
    VALUES (N'2025-2026', '2025-07-01', '2026-06-30', 1),
           (N'2024-2025', '2024-07-01', '2025-06-30', 0);
END
SELECT @sessionId = SessionId FROM dbo.AcademicSessions WHERE IsActive = 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Semesters WHERE SessionId = @sessionId AND SemesterNumber = 1)
BEGIN
    INSERT dbo.Semesters (SessionId, SemesterNumber, SemesterName, StartDate, EndDate)
    VALUES (@sessionId, 1, N'Semester 1', '2025-07-01', '2025-12-31'),
           (@sessionId, 2, N'Semester 2', '2026-01-01', '2026-06-30');
END
GO

-- Complaint categories --------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.ComplaintCategories)
BEGIN
    INSERT dbo.ComplaintCategories (CatName) VALUES
      (N'Academic'), (N'Fee/Payment'), (N'Website/App Issue'),
      (N'Hostel'), (N'Library'), (N'Certificate/Document'), (N'Other');
END
GO

-- Fee schedule (per course; safe to re-run) -----------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Fees)
BEGIN
    INSERT dbo.Fees (CourseId, FeeCode, FeeName, Amount)
    SELECT c.CourseId, f.FeeCode, f.FeeName, f.Amount
    FROM dbo.Courses c
    CROSS JOIN (VALUES
        (N'FEE-EXAM',   N'Examination Fee',        350.00),
        (N'FEE-LAB',    N'Laboratory Fee',         500.00),
        (N'FEE-LIB',    N'Library Fee',            200.00)
    ) f (FeeCode, FeeName, Amount);
END
GO

-- Knowledge-base FAQs (chatbot fallback) --------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.FrequentlyAsked)
BEGIN
    INSERT dbo.FrequentlyAsked (Question, Answer, Keywords, RoleScope, SortOrder) VALUES
      (N'How do I check my attendance?',
       N'Open the sidebar Attendance page. It shows subject-wise attendance, overall percentage and a warning if you are below the minimum percentage configured by your college.',
       N'attendance,percent,absent', N'student', 1),
      (N'When are results published?',
       N'Faculty enter marks first; the administrator publishes the exam. Once published, results appear under Results in the sidebar and you get a notification.',
       N'result,marks,publish', N'student', 2),
      (N'How do I pay a fee?',
       N'Go to Payments, choose an active fee, and Pay. After payment, download the receipt from the same page. Test-mode payments use Razorpay test cards.',
       N'payment,fee,receipt,pay', N'student', 3),
      (N'How do I raise a complaint?',
       N'Use Complaints > New Complaint, pick a category and describe the issue. You get a ticket number and can track replies and status there.',
       N'complaint,issue,ticket', N'student', 4),
      (N'How do I mark attendance for my subject?',
       N'Faculty: Attendance > Take Attendance, pick your assigned course-subject and date, mark each student, then Save. Duplicate saves for the same session are blocked.',
       N'take,attendance,faculty', N'faculty', 5),
      (N'What are the working application links?',
       N'Dashboard, Students, Courses, Subjects, Attendance, Marks, Results, Reports, Notifications, Complaints, Payments, Chat, Profile, Settings, Logout.',
       N'link,navigate,menu', N'all', 6),
      (N'I forgot my password',
       N'Use the Forgot Password link on the login page. You will receive a reset link by email (or OTP if SMS is configured); links are single-use and expire in 1 hour.',
       N'password,reset,forgot', N'all', 7),
      (N'How do I export my data?',
       N'The Export button on Profile and admin list pages downloads CSV, Excel, JSON or PDF of the permitted records only for your role.',
       N'export,download,report', N'all', 8);
END
GO

PRINT N'Seed data inserted.';
