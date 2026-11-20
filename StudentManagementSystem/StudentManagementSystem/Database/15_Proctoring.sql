-- =====================================================================
-- 15_Proctoring.sql
-- Proctoring warn/auto-submit rule, test taxonomy and demo reminders.
-- Idempotent. Never drops data.
-- =====================================================================
USE [StudentSMS];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- 1. Institution timezone: correct the legacy UTC default only.
UPDATE dbo.AppSettings
SET SettingValue = 'Asia/Kolkata'
WHERE SettingKey = 'InstitutionTimezone'
  AND (SettingValue IS NULL OR LTRIM(RTRIM(SettingValue)) = '' OR SettingValue = 'UTC');
GO

-- 2. One configurable warning before the automatic submit.
IF COL_LENGTH('dbo.OnlineTests', 'WarningLimit') IS NULL
BEGIN
    ALTER TABLE dbo.OnlineTests ADD WarningLimit INT NOT NULL CONSTRAINT DF_OT_Warning DEFAULT (1);
    PRINT N'OnlineTests.WarningLimit added.';
END
GO

-- 3. Auto-submitted attempts are a first-class status.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_OTA_Status' AND parent_object_id = OBJECT_ID('dbo.OnlineTestAttempts'))
    ALTER TABLE dbo.OnlineTestAttempts DROP CONSTRAINT CK_OTA_Status;
GO
IF COL_LENGTH('dbo.OnlineTestAttempts', 'Status') IS NOT NULL
    ALTER TABLE dbo.OnlineTestAttempts ALTER COLUMN Status VARCHAR(20) NOT NULL;
GO
ALTER TABLE dbo.OnlineTestAttempts ADD CONSTRAINT CK_OTA_Status
    CHECK (Status IN ('in_progress', 'submitted', 'expired', 'auto_submitted'));
GO

IF COL_LENGTH('dbo.OnlineTestAttempts', 'IntegrityNote') IS NULL
BEGIN
    ALTER TABLE dbo.OnlineTestAttempts ADD IntegrityNote NVARCHAR(200) NULL;
    PRINT N'OnlineTestAttempts.IntegrityNote added.';
END
GO

-- 4. Test taxonomy (categories / topics) and question classification.
IF OBJECT_ID('dbo.TestCategories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TestCategories (
        CategoryId   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CategoryName NVARCHAR(120) NOT NULL,
        Slug         VARCHAR(120)  NOT NULL,
        SortOrder    INT           NOT NULL CONSTRAINT DF_TC_Sort DEFAULT (0),
        IsActive     BIT           NOT NULL CONSTRAINT DF_TC_Active DEFAULT (1),
        CONSTRAINT UQ_TC_Slug UNIQUE (Slug)
    );
    PRINT N'TestCategories created.';
END
GO

IF OBJECT_ID('dbo.TestTopics', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TestTopics (
        TopicId    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CategoryId INT           NOT NULL,
        TopicName  NVARCHAR(160) NOT NULL,
        SortOrder  INT           NOT NULL CONSTRAINT DF_TT_Sort DEFAULT (0),
        CONSTRAINT FK_TT_Category FOREIGN KEY (CategoryId) REFERENCES dbo.TestCategories (CategoryId) ON DELETE CASCADE
    );
    PRINT N'TestTopics created.';
END
GO

IF COL_LENGTH('dbo.QuestionBank', 'CategoryId') IS NULL
BEGIN
    ALTER TABLE dbo.QuestionBank ADD CategoryId INT NULL;
    PRINT N'QuestionBank.CategoryId added.';
END
GO
IF COL_LENGTH('dbo.QuestionBank', 'TopicId') IS NULL
BEGIN
    ALTER TABLE dbo.QuestionBank ADD TopicId INT NULL;
    PRINT N'QuestionBank.TopicId added.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_QB_Category')
    ALTER TABLE dbo.QuestionBank ADD CONSTRAINT FK_QB_Category FOREIGN KEY (CategoryId) REFERENCES dbo.TestCategories (CategoryId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_QB_Topic')
    ALTER TABLE dbo.QuestionBank ADD CONSTRAINT FK_QB_Topic FOREIGN KEY (TopicId) REFERENCES dbo.TestTopics (TopicId);
GO

-- 5. 60 categories covering the seeded programmes and common exam streams.
IF (SELECT COUNT(*) FROM dbo.TestCategories) = 0
BEGIN
    INSERT dbo.TestCategories (CategoryName, Slug, SortOrder) VALUES
    (N'C Programming', 'c-programming', 1),
    (N'Data Structures', 'data-structures', 2),
    (N'Algorithms', 'algorithms', 3),
    (N'Object Oriented Programming', 'oop', 4),
    (N'Java', 'java', 5),
    (N'Python', 'python', 6),
    (N'Web Development', 'web-development', 7),
    (N'Databases and SQL', 'databases-sql', 8),
    (N'Operating Systems', 'operating-systems', 9),
    (N'Computer Networks', 'computer-networks', 10),
    (N'Digital Logic', 'digital-logic', 11),
    (N'Computer Architecture', 'computer-architecture', 12),
    (N'Software Engineering', 'software-engineering', 13),
    (N'Cyber Security', 'cyber-security', 14),
    (N'Artificial Intelligence', 'artificial-intelligence', 15),
    (N'Machine Learning', 'machine-learning', 16),
    (N'Cloud Computing', 'cloud-computing', 17),
    (N'Data Science', 'data-science', 18),
    (N'Mobile Development', 'mobile-development', 19),
    (N'Git and Version Control', 'git-version-control', 20),
    (N'Arithmetic', 'arithmetic', 21),
    (N'Algebra', 'algebra', 22),
    (N'Geometry', 'geometry', 23),
    (N'Trigonometry', 'trigonometry', 24),
    (N'Calculus', 'calculus', 25),
    (N'Statistics', 'statistics', 26),
    (N'Probability', 'probability', 27),
    (N'Quantitative Aptitude', 'quantitative-aptitude', 28),
    (N'Logical Reasoning', 'logical-reasoning', 29),
    (N'Verbal Ability', 'verbal-ability', 30),
    (N'Physics', 'physics', 31),
    (N'Chemistry', 'chemistry', 32),
    (N'Biology', 'biology', 33),
    (N'Environmental Science', 'environmental-science', 34),
    (N'Financial Accounting', 'financial-accounting', 35),
    (N'Cost Accounting', 'cost-accounting', 36),
    (N'Business Economics', 'business-economics', 37),
    (N'Banking Awareness', 'banking-awareness', 38),
    (N'Taxation', 'taxation', 39),
    (N'Marketing Management', 'marketing-management', 40),
    (N'Human Resource Management', 'human-resource-management', 41),
    (N'Business Law', 'business-law', 42),
    (N'Corporate Finance', 'corporate-finance', 43),
    (N'Entrepreneurship', 'entrepreneurship', 44),
    (N'History', 'history', 45),
    (N'Geography', 'geography', 46),
    (N'Civics and Polity', 'civics-polity', 47),
    (N'English Language', 'english-language', 48),
    (N'Hindi Language', 'hindi-language', 49),
    (N'Communication Skills', 'communication-skills', 50),
    (N'General Knowledge', 'general-knowledge', 51),
    (N'Current Affairs', 'current-affairs', 52),
    (N'Competitive Exam Aptitude', 'competitive-exam-aptitude', 53),
    (N'Interview Preparation', 'interview-preparation', 54),
    (N'Digital Literacy', 'digital-literacy', 55),
    (N'Office Productivity', 'office-productivity', 56),
    (N'Soft Skills', 'soft-skills', 57),
    (N'Financial Literacy', 'financial-literacy', 58),
    (N'Research Methods', 'research-methods', 59),
    (N'Business Communication', 'business-communication', 60);
    PRINT N'Seeded 60 test categories.';
END
GO

-- 6. Five topics per category = 300 topics.
IF (SELECT COUNT(*) FROM dbo.TestTopics) = 0
BEGIN
    INSERT dbo.TestTopics (CategoryId, TopicName, SortOrder)
    SELECT c.CategoryId, c.CategoryName + CASE t.n
        WHEN 1 THEN N' - fundamentals'
        WHEN 2 THEN N' - core concepts'
        WHEN 3 THEN N' - problem solving'
        WHEN 4 THEN N' - applications'
        ELSE N' - revision' END, t.n
    FROM dbo.TestCategories c
    CROSS JOIN (SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5) t(n);
    PRINT N'Seeded 300 test topics.';
END
GO

-- 7. Demo reminders/notifications so the ticker visibly works on a fresh install.
IF COL_LENGTH('dbo.Notifications', 'SourceKey') IS NULL
BEGIN
    ALTER TABLE dbo.Notifications ADD SourceKey VARCHAR(100) NULL;
    PRINT N'Notifications.SourceKey added.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notif_SourceKey' AND object_id = OBJECT_ID('dbo.Notifications'))
    CREATE INDEX IX_Notif_SourceKey ON dbo.Notifications (SourceKey);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Notifications WHERE SourceKey LIKE 'demo:%')
BEGIN
    DECLARE @adminId INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Email = N'admin@sms.edu');
    DECLARE @facId   INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Email = N'faculty@sms.edu');
    DECLARE @stuId   INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Email = N'student@sms.edu');
    DECLARE @n INT;

    DECLARE @demo TABLE (SourceKey VARCHAR(100), Title NVARCHAR(160), Message NVARCHAR(1000), NType NVARCHAR(30), ActionUrl VARCHAR(300), TargetUser INT);
    INSERT @demo VALUES
      ('demo:admin:1', N'Fee collection summary is ready', N'Open Payments to review today''s collections and outstanding dues.', N'payment', '/Admin/Payments.aspx', @adminId),
      ('demo:admin:2', N'Three students have overdue fees', N'Follow up from the Finance area.', N'payment', '/Admin/Fees.aspx', @adminId),
      ('demo:admin:3', N'Weekly backup reminder', N'Run a database backup from Settings > Backups.', N'system', '/Admin/Settings.aspx', @adminId),
      ('demo:faculty:1', N'Attendance for CS101 is due', N'Mark attendance for your next class.', N'academic', '/Faculty/Attendance.aspx', @facId),
      ('demo:faculty:2', N'Internal Test II marks close soon', N'Complete marks entry before the deadline.', N'academic', '/Faculty/Marks.aspx', @facId),
      ('demo:faculty:3', N'New complaint assigned', N'Review the student ticket in Complaint replies.', N'complaint', '/Faculty/Complaints.aspx', @facId),
      ('demo:student:1', N'Assignment due in two days', N'Check My learning for the upcoming deadline.', N'academic', '/Student/Index.aspx', @stuId),
      ('demo:student:2', N'Fee payment due soon', N'Pay online from Fees and payments.', N'payment', '/Student/Fees.aspx', @stuId),
      ('demo:student:3', N'Your next class starts at 10:00', N'See the full week in Timetable.', N'academic', '/Timetable/Index.aspx', @stuId);

    INSERT dbo.Notifications (Title, Message, NType, ActionUrl, CreatedBy, SourceKey)
    SELECT d.Title, d.Message, d.NType, d.ActionUrl, @adminId, d.SourceKey
    FROM @demo d
    WHERE EXISTS (SELECT 1 FROM dbo.Users u WHERE u.UserId = d.TargetUser);

    INSERT dbo.NotificationRecipients (NotificationId, UserId)
    SELECT n.NotificationId, d.TargetUser
    FROM dbo.Notifications n JOIN @demo d ON d.SourceKey = n.SourceKey
    WHERE NOT EXISTS (SELECT 1 FROM dbo.NotificationRecipients r
                      WHERE r.NotificationId = n.NotificationId AND r.UserId = d.TargetUser);

    -- Upcoming personal reminders (due in 1-3 days) for the same three roles.
    INSERT dbo.Reminders (UserId, Title, Body, ActionUrl, DueAtUtc, Status, RepeatRule, UniqueKey)
    SELECT d.TargetUser, d.Title, d.Message, d.ActionUrl,
           DATEADD(DAY, 1 + (ABS(CHECKSUM(d.SourceKey)) % 3), SYSUTCDATETIME()),
           'pending', 'none', 'demo:' + d.SourceKey
    FROM @demo d
    WHERE EXISTS (SELECT 1 FROM dbo.Users u WHERE u.UserId = d.TargetUser)
      AND NOT EXISTS (SELECT 1 FROM dbo.Reminders r WHERE r.UniqueKey = 'demo:' + d.SourceKey);
    PRINT N'Seeded demo notifications and reminders.';
END
GO

PRINT N'15_Proctoring.sql complete.';
GO

