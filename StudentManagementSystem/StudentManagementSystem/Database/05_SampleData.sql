-- ==============================================================
-- 05_SampleData.sql - rich demo dataset (idempotent, set-based).
-- Generates: 3 courses, 6 subjects, 12 students, 2 faculty, enrollments,
-- attendance (~20 weekdays for 2 subjects), 1 published + 1 draft exam with
-- marks + published results, complaints + threads, notifications,
-- announcements. Safe to re-run.
-- Prerequesite: run 01,02,03,04 first.
-- ==============================================================
USE [StudentSMS];
GO
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- --------------------------------------------------------------------------
-- 1) Courses
-- --------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Courses WHERE CourseCode = N'BSC-CS')
    INSERT dbo.Courses (CourseCode, CourseName, Description, TotalSemesters, TotalCredits)
    VALUES (N'BSC-CS', N'B.Sc. Computer Science', N'Programming, data structures, databases and networking.', 6, 132);

IF NOT EXISTS (SELECT 1 FROM dbo.Courses WHERE CourseCode = N'BCOM')
    INSERT dbo.Courses (CourseCode, CourseName, Description, TotalSemesters, TotalCredits)
    VALUES (N'BCOM', N'B.Com. Accounting', N'Financial accounting, economics and taxation.', 6, 120);

IF NOT EXISTS (SELECT 1 FROM dbo.Courses WHERE CourseCode = N'BBA')
    INSERT dbo.Courses (CourseCode, CourseName, Description, TotalSemesters, TotalCredits)
    VALUES (N'BBA', N'BBA Management', N'Business fundamentals, marketing and HR.', 6, 120);
GO

-- --------------------------------------------------------------------------
-- 2) Subjects
-- --------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Subjects WHERE SubjectCode = N'CS101')
    INSERT dbo.Subjects (SubjectCode, SubjectName, WeeklyHours) VALUES (N'CS101', N'Programming Fundamentals (C)', 5);
IF NOT EXISTS (SELECT 1 FROM dbo.Subjects WHERE SubjectCode = N'CS102')
    INSERT dbo.Subjects (SubjectCode, SubjectName, WeeklyHours) VALUES (N'CS102', N'Data Structures', 4);
IF NOT EXISTS (SELECT 1 FROM dbo.Subjects WHERE SubjectCode = N'CS103')
    INSERT dbo.Subjects (SubjectCode, SubjectName, WeeklyHours) VALUES (N'CS103', N'Digital Logic Design', 4);
IF NOT EXISTS (SELECT 1 FROM dbo.Subjects WHERE SubjectCode = N'GE101')
    INSERT dbo.Subjects (SubjectCode, SubjectName, WeeklyHours) VALUES (N'GE101', N'Communication Skills', 3);
IF NOT EXISTS (SELECT 1 FROM dbo.Subjects WHERE SubjectCode = N'BC101')
    INSERT dbo.Subjects (SubjectCode, SubjectName, WeeklyHours) VALUES (N'BC101', N'Financial Accounting', 5);
IF NOT EXISTS (SELECT 1 FROM dbo.Subjects WHERE SubjectCode = N'BC102')
    INSERT dbo.Subjects (SubjectCode, SubjectName, WeeklyHours) VALUES (N'BC102', N'Business Economics', 4);
GO

-- --------------------------------------------------------------------------
-- 3) Course-Semester mapping
-- --------------------------------------------------------------------------
INSERT dbo.CourseSubjects (CourseId, SubjectId, SemesterNumber)
SELECT c.CourseId, s.SubjectId, x.Sem
FROM (VALUES
      (N'BSC-CS','CS101',1),(N'BSC-CS','CS102',1),(N'BSC-CS','CS103',1),(N'BSC-CS','GE101',1),
      (N'BCOM','BC101',1),(N'BCOM','BC102',1)) x (course, subj, Sem)
CROSS APPLY (SELECT CourseId FROM dbo.Courses c  WHERE c.CourseCode  = x.course) c
CROSS APPLY (SELECT SubjectId FROM dbo.Subjects s WHERE s.SubjectCode = x.subj) s
WHERE NOT EXISTS (SELECT 1 FROM dbo.CourseSubjects t
                  WHERE t.CourseId = c.CourseId AND t.SubjectId = s.SubjectId AND t.SemesterNumber = x.Sem);
GO

-- --------------------------------------------------------------------------
-- 4) Second faculty
-- --------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'faculty2@sms.edu')
    INSERT dbo.Users (FullName, Email, PasswordHash, EmailVerified, IsActive, ConsentAccepted)
    VALUES (N'Prof. Sneha Kulkarni', N'faculty2@sms.edu',
            N'100000.lszMPs+rO+6JsWQhS/zNIQ==.K5TtGuD8LG5AIhVAPyBWhdiyJ2xSbABWN6QSRL8ARik=', 1, 1, 1);

INSERT dbo.UserRoles (UserId, RoleId)
SELECT u.UserId, r.RoleId FROM dbo.Users u CROSS JOIN dbo.Roles r
WHERE u.Email = N'faculty2@sms.edu' AND r.RoleName = N'Faculty'
  AND NOT EXISTS (SELECT 1 FROM dbo.UserRoles x WHERE x.UserId = u.UserId AND x.RoleId = r.RoleId);

INSERT dbo.Faculty (UserId, DeptCode, Designation, JoinedOn)
SELECT u.UserId, N'MGMT', N'Assistant Professor', '2021-08-16'
FROM dbo.Users u WHERE u.Email = N'faculty2@sms.edu'
  AND NOT EXISTS (SELECT 1 FROM dbo.Faculty f WHERE f.UserId = u.UserId);
GO

-- --------------------------------------------------------------------------
-- 5) Students (12): users + roles + Student rows with deterministic course
-- --------------------------------------------------------------------------
DECLARE @stuPwd VARCHAR(180) =
    N'100000.Z5KPm1y6P0UeXbborkdBCQ==.fKN3KsiDXbRQljMeKTST1bDnEtrUCyfT15bmdZGH2+s='; -- Student#123

IF OBJECT_ID('tempdb..#stu') IS NOT NULL DROP TABLE #stu;
CREATE TABLE #stu (
    Seq   INT, Name NVARCHAR(120), Email NVARCHAR(150), Phone VARCHAR(15),
    Dob   DATE, Guard NVARCHAR(120), Addr NVARCHAR(300)
);
INSERT #stu VALUES
 (1,'Rahul Sharma',     N'student@sms.edu',   '9876500001', '2004-03-12', 'Mr. Ramesh Sharma', N'12 Shivaji Nagar, Pune'),
 (2,'Priya Patel',      N'student2@sms.edu',  '9876500002', '2004-07-25', 'Mr. Kiran Patel',   N'45 Munirka Road, Delhi'),
 (3,'Aravind Kumar',    N'student3@sms.edu',  '9876500003', '2003-11-02', 'Mr. Suresh Kumar',  N'TP 14, Bhubaneswar'),
 (4,'Meera Iyer',       N'student4@sms.edu',  '9876500004', '2004-01-18', 'Mr. Ravi Iyer',     N'48 Jayanagar, Bengaluru'),
 (5,'Vikram Singh',     N'student5@sms.edu',  '9876500005', '2003-09-30', 'Mr. Dev Singh',     N'15 Vasant Vihar, Jaipur'),
 (6,'Sneha Reddy',      N'student6@sms.edu',  '9876500006', '2004-05-14', 'Mr. Anil Reddy',    N'9 Kondapur, Hyderabad'),
 (7,'Aditya Verma',     N'student7@sms.edu',  '9876500007', '2004-02-08', 'Mr. Manoj Verma',   N'102 CG Block, Kanpur'),
 (8,'Kavya Nair',       N'student8@sms.edu',  '9876500008', '2003-12-21', 'Mr. Rajesh Nair',   N'60 Marine Drive, Kochi'),
 (9,'Rohit Joshi',      N'student9@sms.edu',  '9876500009', '2004-08-03', 'Mr. Satish Joshi',  N'175 Chandni Chowk, Delhi'),
 (10,'Ishita Bose',     N'student10@sms.edu', '9876500010', '2004-06-11', 'Mr. Debasish Bose', N'27 Salt Lake, Kolkata'),
 (11,'Nikhil Chandra',  N'student11@sms.edu', '9876500011', '2003-10-27', 'Mr. Badri Chandra', N'31 Anna Salai, Chennai'),
 (12,'Ananya Ghosh',    N'student12@sms.edu', '9876500012', '2004-04-19', 'Mr. Tarun Ghosh',   N'6 Lake Gardens, Kolkata');

-- User accounts for everybody who does not exist yet
INSERT dbo.Users (FullName, Email, Phone, PasswordHash, EmailVerified, IsActive, ConsentAccepted)
SELECT s.Name, s.Email, s.Phone, @stuPwd, 1, 1, 1
FROM #stu s
LEFT JOIN dbo.Users u ON u.Email = s.Email
WHERE u.UserId IS NULL;

INSERT dbo.UserRoles (UserId, RoleId)
SELECT u.UserId, r.RoleId
FROM dbo.Users u CROSS JOIN dbo.Roles r
JOIN #stu s ON s.Email = u.Email
WHERE r.RoleName = N'Student'
  AND NOT EXISTS (SELECT 1 FROM dbo.UserRoles x WHERE x.UserId = u.UserId AND x.RoleId = r.RoleId);

-- Student profile rows (first 10 students -> B.Sc., 11-12 -> B.Com.)
INSERT dbo.Students (UserId, EnrollNumber, CourseId, SessionId, SemesterNumber, AdmissionDate, DateOfBirth, GuardianName, Address)
SELECT u.UserId,
       'REG-2025-' + RIGHT('000' + CAST(s.Seq AS VARCHAR(4)), 4),
       CASE WHEN s.Seq <= 10
            THEN (SELECT CourseId FROM dbo.Courses WHERE CourseCode = N'BSC-CS')
            ELSE (SELECT CourseId FROM dbo.Courses WHERE CourseCode = N'BCOM') END,
       (SELECT TOP 1 SessionId FROM dbo.AcademicSessions ORDER BY IsActive DESC, SessionId),
       1, '2025-07-15', s.Dob, s.Guard, s.Addr
FROM #stu s
JOIN dbo.Users u ON u.Email = s.Email
WHERE NOT EXISTS (SELECT 1 FROM dbo.Students st WHERE st.UserId = u.UserId);
GO

-- --------------------------------------------------------------------------
-- 6) Enrollments for all students in the active session
-- --------------------------------------------------------------------------
INSERT dbo.Enrollments (StudentId, CourseId, SessionId)
SELECT st.StudentId, st.CourseId, st.SessionId
FROM dbo.Students st
WHERE st.SessionId IN (SELECT TOP 1 SessionId FROM dbo.AcademicSessions ORDER BY IsActive DESC, SessionId)
  AND NOT EXISTS (SELECT 1 FROM dbo.Enrollments e
                  WHERE e.StudentId = st.StudentId AND e.CourseId = st.CourseId AND e.SessionId = st.SessionId);
GO

-- --------------------------------------------------------------------------
-- 7) Faculty subject assignments (active session)
-- --------------------------------------------------------------------------
INSERT dbo.FacultyAssignments (FacultyId, CourseId, SubjectId, SessionId, SemesterNumber)
SELECT f.FacultyId, c.CourseId, s.SubjectId, sess.SessionId, x.Sem
FROM (VALUES
        (N'faculty@sms.edu',  N'BSC-CS', N'CS101', 1),
        (N'faculty@sms.edu',  N'BSC-CS', N'CS102', 1),
        (N'faculty@sms.edu',  N'BSC-CS', N'GE101', 1),
        (N'faculty2@sms.edu', N'BCOM',   N'BC101', 1)
     ) x (facEmail, course, subj, Sem)
CROSS APPLY (SELECT TOP 1 SessionId FROM dbo.AcademicSessions ORDER BY IsActive DESC, SessionId) sess
JOIN dbo.Faculty f                 ON f.FacultyId IN (SELECT FacultyId FROM dbo.Faculty f2 JOIN dbo.Users u2 ON u2.UserId = f2.UserId WHERE u2.Email = x.facEmail)
JOIN dbo.Users fu                  ON fu.UserId = f.UserId AND fu.Email = x.facEmail
JOIN dbo.Courses c                 ON c.CourseCode = x.course
JOIN dbo.Subjects s                ON s.SubjectCode = x.subj
WHERE NOT EXISTS (SELECT 1 FROM dbo.FacultyAssignments t
                  WHERE t.FacultyId = f.FacultyId AND t.CourseId = c.CourseId
                    AND t.SubjectId = s.SubjectId AND t.SessionId = sess.SessionId
                    AND t.SemesterNumber = x.Sem);
GO

-- --------------------------------------------------------------------------
-- 8) Attendance sessions + rows for the last ~20 weekdays (2 subjects/day)
-- --------------------------------------------------------------------------
DECLARE @start DATE = DATEADD(WEEK, -4, CAST(GETDATE() AS DATE));
DECLARE @d DATE = @start, @sidCourse INT, @csSubj INT, @eeSubj INT, @fac INT, @sess INT;
SELECT @csSubj = SubjectId FROM dbo.Subjects WHERE SubjectCode = N'CS101';
SELECT @eeSubj = SubjectId FROM dbo.Subjects WHERE SubjectCode = N'CS102';
SELECT @sess   = SessionId   FROM dbo.AcademicSessions WHERE IsActive = 1;
SELECT @sidCourse = CourseId FROM dbo.Courses WHERE CourseCode = N'BSC-CS';
SELECT @fac = UserId FROM dbo.Users WHERE Email = N'faculty@sms.edu';

WHILE @d <= CAST(GETDATE() AS DATE)
BEGIN
    IF DATENAME(WEEKDAY, @d) NOT IN ('Saturday','Sunday') AND @d >= @start
    BEGIN
        -- CS101 period 1
        IF NOT EXISTS (SELECT 1 FROM dbo.AttendanceSessions
                       WHERE FacultyUserId = @fac AND SessionDate = @d AND PeriodNo = 1 AND SubjectId = @csSubj)
        BEGIN
            INSERT dbo.AttendanceSessions (FacultyUserId, CourseId, SubjectId, SessionDate, PeriodNo, SubjectName)
            VALUES (@fac, @sidCourse, @csSubj, @d, 1, N'Programming Fundamentals (C)');

            INSERT dbo.Attendance (AttnSessionId, EnrollId, AttnStatus)
            SELECT src.ASess, e.EnrollId,
                   CASE WHEN ABS(CHECKSUM(NEWID())) % 100 < 15 THEN 'A' ELSE 'P' END
            FROM (SELECT a.AttnSessionId AS ASess
                  FROM dbo.AttendanceSessions a
                  WHERE a.FacultyUserId = @fac AND a.SessionDate = @d
                    AND a.PeriodNo = 1 AND a.SubjectId = @csSubj) src
            JOIN dbo.Enrollments e ON e.CourseId = @sidCourse AND e.SessionId = @sess AND e.IsActive = 1;
        END

        -- CS102 period 2
        IF NOT EXISTS (SELECT 1 FROM dbo.AttendanceSessions
                       WHERE FacultyUserId = @fac AND SessionDate = @d AND PeriodNo = 2 AND SubjectId = @eeSubj)
        BEGIN
            INSERT dbo.AttendanceSessions (FacultyUserId, CourseId, SubjectId, SessionDate, PeriodNo, SubjectName)
            VALUES (@fac, @sidCourse, @eeSubj, @d, 2, N'Data Structures');

            INSERT dbo.Attendance (AttnSessionId, EnrollId, AttnStatus)
            SELECT src.ASess, e.EnrollId,
                   CASE WHEN ABS(CHECKSUM(NEWID())) % 100 < 18 THEN 'A' ELSE 'P' END
            FROM (SELECT a.AttnSessionId AS ASess
                  FROM dbo.AttendanceSessions a
                  WHERE a.FacultyUserId = @fac AND a.SessionDate = @d
                    AND a.PeriodNo = 2 AND a.SubjectId = @eeSubj) src
            JOIN dbo.Enrollments e ON e.CourseId = @sidCourse AND e.SessionId = @sess AND e.IsActive = 1;
        END
    END
    SET @d = DATEADD(DAY, 1, @d);
END
GO

-- --------------------------------------------------------------------------
-- 9) Exams + marks + published results
-- --------------------------------------------------------------------------
DECLARE @e1 INT, @e2 INT, @csSubj INT, @csCourse INT, @admin INT;
SELECT @csSubj   = SubjectId FROM dbo.Subjects WHERE SubjectCode = N'CS101';
SELECT @csCourse = CourseId  FROM dbo.Courses  WHERE CourseCode  = N'BSC-CS';
SELECT @admin    = UserId    FROM dbo.Users    WHERE Email      = N'admin@sms.edu';

IF NOT EXISTS (SELECT 1 FROM dbo.Exams WHERE ExamName = N'Internal Test I' AND SubjectId = @csSubj)
BEGIN
    INSERT dbo.Exams (ExamName, CourseId, SubjectId, CreatedBy, MaxMarks, PassPercent, WeightType, ExamDate, IsPublished, PublishedAtUtc)
    VALUES (N'Internal Test I', @csCourse, @csSubj, @admin, 100, 33, 1, DATEADD(DAY, -21, GETDATE()), 1, SYSUTCDATETIME());
END
IF NOT EXISTS (SELECT 1 FROM dbo.Exams WHERE ExamName = N'Internal Test II' AND SubjectId = @csSubj)
BEGIN
    INSERT dbo.Exams (ExamName, CourseId, SubjectId, CreatedBy, MaxMarks, PassPercent, WeightType, ExamDate, IsPublished)
    VALUES (N'Internal Test II', @csCourse, @csSubj, @admin, 100, 33, 1, DATEADD(DAY, -3, GETDATE()), 0);
END
SELECT @e1 = ExamId FROM dbo.Exams WHERE ExamName = N'Internal Test I'  AND SubjectId = @csSubj;
SELECT @e2 = ExamId FROM dbo.Exams WHERE ExamName = N'Internal Test II' AND SubjectId = @csSubj;

-- Marks for exam 1 (deterministic spread 42..58 - demotoast)
IF NOT EXISTS (SELECT 1 FROM dbo.Marks WHERE ExamId = @e1)
    INSERT dbo.Marks (ExamId, EnrollId, MarksObtained, EnteredBy)
    SELECT @e1, e.EnrollId, CAST(42.0 + ((e.EnrollId * 7) % 17) AS DECIMAL(5,2)), @admin
    FROM dbo.Enrollments e JOIN dbo.Students st ON st.StudentId = e.StudentId
    WHERE st.CourseId = @csCourse AND e.SessionId = (SELECT TOP 1 SessionId FROM dbo.AcademicSessions ORDER BY IsActive DESC, SessionId);

-- Marks for exam 2 (not published)
IF NOT EXISTS (SELECT 1 FROM dbo.Marks WHERE ExamId = @e2)
    INSERT dbo.Marks (ExamId, EnrollId, MarksObtained, EnteredBy)
    SELECT @e2, e.EnrollId, CAST(38.0 + ((e.EnrollId * 5) % 21) AS DECIMAL(5,2)), @admin
    FROM dbo.Enrollments e JOIN dbo.Students st ON st.StudentId = e.StudentId
    WHERE st.CourseId = @csCourse AND e.SessionId = (SELECT TOP 1 SessionId FROM dbo.AcademicSessions ORDER BY IsActive DESC, SessionId);
GO

-- --------------------------------------------------------------------------
-- 10) Fees for every course (exam/lab/library) - unique per course+code
-- --------------------------------------------------------------------------
INSERT dbo.Fees (CourseId, FeeCode, FeeName, Amount)
SELECT c.CourseId, f.FeeCode, f.FeeName, f.Amount
FROM dbo.Courses c
CROSS JOIN (VALUES
    (N'FEE-EXAM', N'Examination Fee', 350.00),
    (N'FEE-LAB',  N'Laboratory Fee',  500.00),
    (N'FEE-LIB',  N'Library Fee',     200.00)
) f (FeeCode, FeeName, Amount)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Fees x WHERE x.CourseId = c.CourseId AND x.FeeCode = f.FeeCode);
GO

-- --------------------------------------------------------------------------
-- 11) Complaints + threads
-- --------------------------------------------------------------------------
DECLARE @demo INT, @adm INT;
SELECT @demo = UserId FROM dbo.Users WHERE Email = N'student@sms.edu';
SELECT @adm  = UserId FROM dbo.Users WHERE Email = N'admin@sms.edu';

INSERT dbo.Complaints (ComplaintNo, RaisedBy, CategoryId, Subject, Description, Status, AssignedTo)
SELECT N'CMP-2026-0001', @demo, c.CategoryId,
       N'College website not opening in mobile', N'When I open the attendance page on my phone the layout breaks.', N'open', NULL
FROM dbo.ComplaintCategories c
WHERE @demo IS NOT NULL AND c.CatName = N'Website/App Issue'
  AND NOT EXISTS (SELECT 1 FROM dbo.Complaints WHERE ComplaintNo = N'CMP-2026-0001');

INSERT dbo.Complaints (ComplaintNo, RaisedBy, CategoryId, Subject, Description, Status, AssignedTo)
SELECT N'CMP-2026-0002', @demo, c.CategoryId,
       N'Fee receipt shows wrong student name', N'Receipt for Lab Fee contains a spelling mistake in my name.', N'in_progress', @adm
FROM dbo.ComplaintCategories c
WHERE @demo IS NOT NULL AND c.CatName = N'Fee/Payment'
  AND NOT EXISTS (SELECT 1 FROM dbo.Complaints WHERE ComplaintNo = N'CMP-2026-0002');

INSERT dbo.Complaints (ComplaintNo, RaisedBy, CategoryId, Subject, Description, Status, AssignedTo, ResolvedAtUtc)
SELECT N'CMP-2026-0003', @demo, c.CategoryId,
       N'Library card lost', N'Lost my library card, please issue a duplicate.', N'resolved', @adm, DATEADD(DAY, -5, GETDATE())
FROM dbo.ComplaintCategories c
WHERE @demo IS NOT NULL AND c.CatName = N'Library'
  AND NOT EXISTS (SELECT 1 FROM dbo.Complaints WHERE ComplaintNo = N'CMP-2026-0003');
GO

IF (SELECT COUNT(*) FROM dbo.ComplaintThreads) = 0
BEGIN
    INSERT dbo.ComplaintThreads (ComplaintId, AuthorId, Message, IsInternal)
    SELECT cp.ComplaintId, cp.RaisedBy, N'Please help, my name is printed wrong.', 0
    FROM dbo.Complaints cp WHERE cp.ComplaintNo = N'CMP-2026-0002';

    INSERT dbo.ComplaintThreads (ComplaintId, AuthorId, Message, IsInternal)
    SELECT cp.ComplaintId, cp.AssignedTo, N'Checking the record, we will correct it and reissue.', 1
    FROM dbo.Complaints cp WHERE cp.ComplaintNo = N'CMP-2026-0002' AND cp.AssignedTo IS NOT NULL;
END
GO

-- --------------------------------------------------------------------------
-- 12) Announcements
-- --------------------------------------------------------------------------
IF (SELECT COUNT(*) FROM dbo.Announcements) = 0
BEGIN
    DECLARE @adminU INT; SELECT @adminU = UserId FROM dbo.Users WHERE Email = N'admin@sms.edu';
    INSERT dbo.Announcements (AnnTitle, AnnBody, Audience, PublishFrom, PublishTo, CreatedBy) VALUES
      (N'Semester 1 results for Internal Test I are now live',
       N'B.Sc. CS semester 1 students can see marks for the first internal test under Results. Contact the department for re-evaluation requests within 7 days.',
       N'students', DATEADD(DAY, -20, CAST(GETDATE() AS DATE)), DATEADD(DAY, 40, CAST(GETDATE() AS DATE)), @adminU),
      (N'Faculty marks-entry window for Internal Test II',
       N'Faculty assigned to B.Sc. CS semester 1 subjects: please complete the Internal Test II marks entry before the deadline.',
       N'faculty', DATEADD(DAY, -3, CAST(GETDATE() AS DATE)), DATEADD(DAY, 14, CAST(GETDATE() AS DATE)), @adminU);
END
GO

-- --------------------------------------------------------------------------
-- 13) Sample notifications (welcome + results published)
-- --------------------------------------------------------------------------
IF (SELECT COUNT(*) FROM dbo.Notifications) = 0
BEGIN
    DECLARE @adm2 INT; SELECT @adm2 = UserId FROM dbo.Users WHERE Email = N'admin@sms.edu';

    INSERT dbo.Notifications (Title, Message, NType, ActionUrl, CreatedBy)
    VALUES (N'Welcome to the Student Management System',
            N'Check your dashboard, keep your profile updated and reach out via Complaints for any help.',
            N'general', N'/', @adm2);

    INSERT dbo.Notifications (Title, Message, NType, ActionUrl, CreatedBy)
    VALUES (N'Internal Test I results published',
            N'B.Sc. CS semester 1 students: internal test 1 results for CS101 are now available.',
            N'academic', N'/Student/Results.aspx', @adm2);

    -- send welcome to all active users, results only to students
    INSERT dbo.NotificationRecipients (NotificationId, UserId)
    SELECT n.NotificationId, u.UserId
    FROM dbo.Notifications n CROSS JOIN dbo.Users u
    WHERE (n.Title LIKE N'Welcome%' OR (u.Email LIKE N'student%@sms.edu' AND n.Title LIKE N'Internal Test I%'))
      AND NOT EXISTS (SELECT 1 FROM dbo.NotificationRecipients r
                      WHERE r.NotificationId = n.NotificationId AND r.UserId = u.UserId);
END
GO

PRINT N'Sample data generation complete.';
