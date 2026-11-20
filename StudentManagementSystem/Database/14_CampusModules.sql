-- =====================================================================
-- 14_CampusModules.sql
-- Timetable, rooms and the academic calendar. Idempotent.
-- =====================================================================
USE [StudentSMS];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.Rooms', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Rooms (
        RoomId       INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        RoomCode     VARCHAR(20)   NOT NULL,
        RoomName     NVARCHAR(80)  NOT NULL,
        Building     NVARCHAR(80)  NULL,
        Capacity     INT           NOT NULL CONSTRAINT DF_Rooms_Capacity DEFAULT (40),
        IsActive     BIT           NOT NULL CONSTRAINT DF_Rooms_Active DEFAULT (1),
        CreatedAtUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_Rooms_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_Rooms_Code UNIQUE (RoomCode)
    );
    PRINT N'Rooms created.';
END
GO

IF OBJECT_ID('dbo.TimetablePeriods', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TimetablePeriods (
        PeriodId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PeriodName   NVARCHAR(40)  NOT NULL,
        StartTime    TIME(0)       NOT NULL,
        EndTime      TIME(0)       NOT NULL,
        SortOrder    INT           NOT NULL CONSTRAINT DF_TTPeriod_Sort DEFAULT (0),
        IsActive     BIT           NOT NULL CONSTRAINT DF_TTPeriod_Active DEFAULT (1),
        CONSTRAINT CK_TTPeriod_Time CHECK (EndTime > StartTime)
    );
    PRINT N'TimetablePeriods created.';
END
GO

IF OBJECT_ID('dbo.TimetableEntries', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TimetableEntries (
        EntryId        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CourseId       INT           NOT NULL,
        SemesterNumber INT           NOT NULL,
        SectionCode    VARCHAR(20)   NOT NULL CONSTRAINT DF_TTEntry_Section DEFAULT ('A'),
        SubjectId      INT           NOT NULL,
        FacultyUserId  INT           NOT NULL,
        RoomId         INT           NULL,
        PeriodId       INT           NOT NULL,
        DayOfWeek      TINYINT       NOT NULL,
        SessionId      INT           NULL,
        EffectiveFrom  DATE          NOT NULL,
        EffectiveTo    DATE          NULL,
        Notes          NVARCHAR(300) NULL,
        IsActive       BIT           NOT NULL CONSTRAINT DF_TTEntry_Active DEFAULT (1),
        CreatedBy      INT           NULL,
        CreatedAtUtc   DATETIME2(0)  NOT NULL CONSTRAINT DF_TTEntry_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_TTEntry_Course FOREIGN KEY (CourseId) REFERENCES dbo.Courses (CourseId),
        CONSTRAINT FK_TTEntry_Subject FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects (SubjectId),
        CONSTRAINT FK_TTEntry_Faculty FOREIGN KEY (FacultyUserId) REFERENCES dbo.Users (UserId),
        CONSTRAINT FK_TTEntry_Room FOREIGN KEY (RoomId) REFERENCES dbo.Rooms (RoomId),
        CONSTRAINT FK_TTEntry_Period FOREIGN KEY (PeriodId) REFERENCES dbo.TimetablePeriods (PeriodId),
        CONSTRAINT FK_TTEntry_Session FOREIGN KEY (SessionId) REFERENCES dbo.AcademicSessions (SessionId),
        CONSTRAINT CK_TTEntry_Day CHECK (DayOfWeek BETWEEN 1 AND 7),
        CONSTRAINT CK_TTEntry_Sem CHECK (SemesterNumber BETWEEN 1 AND 12)
    );
    PRINT N'TimetableEntries created.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TTEntry_Slot' AND object_id = OBJECT_ID('dbo.TimetableEntries'))
    CREATE INDEX IX_TTEntry_Slot ON dbo.TimetableEntries (DayOfWeek, PeriodId, IsActive) INCLUDE (CourseId, SemesterNumber, SectionCode, FacultyUserId, RoomId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TTEntry_Faculty' AND object_id = OBJECT_ID('dbo.TimetableEntries'))
    CREATE INDEX IX_TTEntry_Faculty ON dbo.TimetableEntries (FacultyUserId, DayOfWeek, PeriodId);
GO

IF OBJECT_ID('dbo.TimetableChanges', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TimetableChanges (
        ChangeId               INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EntryId                INT           NOT NULL,
        ChangeDate             DATE          NOT NULL,
        ChangeKind             VARCHAR(16)   NOT NULL,
        SubstituteFacultyUserId INT          NULL,
        Reason                 NVARCHAR(300) NULL,
        CreatedBy              INT           NULL,
        CreatedAtUtc           DATETIME2(0)  NOT NULL CONSTRAINT DF_TTChange_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_TTChange_Entry FOREIGN KEY (EntryId) REFERENCES dbo.TimetableEntries (EntryId) ON DELETE CASCADE,
        CONSTRAINT FK_TTChange_Sub FOREIGN KEY (SubstituteFacultyUserId) REFERENCES dbo.Users (UserId),
        CONSTRAINT CK_TTChange_Kind CHECK (ChangeKind IN ('substitution', 'cancellation')),
        CONSTRAINT UQ_TTChange_EntryDate UNIQUE (EntryId, ChangeDate)
    );
    PRINT N'TimetableChanges created.';
END
GO

IF OBJECT_ID('dbo.CalendarEvents', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CalendarEvents (
        EventId      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Title        NVARCHAR(160) NOT NULL,
        Description  NVARCHAR(2000) NULL,
        Category     VARCHAR(20)   NOT NULL CONSTRAINT DF_CalEvent_Category DEFAULT ('event'),
        Audience     VARCHAR(20)   NOT NULL CONSTRAINT DF_CalEvent_Audience DEFAULT ('all'),
        CourseId     INT           NULL,
        SemesterNumber INT         NULL,
        StartsAtUtc  DATETIME2(0)  NOT NULL,
        EndsAtUtc    DATETIME2(0)  NOT NULL,
        IsAllDay     BIT           NOT NULL CONSTRAINT DF_CalEvent_AllDay DEFAULT (0),
        Location     NVARCHAR(160) NULL,
        IsActive     BIT           NOT NULL CONSTRAINT DF_CalEvent_Active DEFAULT (1),
        CreatedBy    INT           NULL,
        CreatedAtUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_CalEvent_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_CalEvent_Course FOREIGN KEY (CourseId) REFERENCES dbo.Courses (CourseId),
        CONSTRAINT FK_CalEvent_User FOREIGN KEY (CreatedBy) REFERENCES dbo.Users (UserId),
        CONSTRAINT CK_CalEvent_Category CHECK (Category IN ('holiday', 'exam', 'event', 'deadline', 'academic', 'other')),
        CONSTRAINT CK_CalEvent_Audience CHECK (Audience IN ('all', 'students', 'faculty', 'admin', 'course')),
        CONSTRAINT CK_CalEvent_Range CHECK (EndsAtUtc >= StartsAtUtc)
    );
    PRINT N'CalendarEvents created.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CalEvent_Window' AND object_id = OBJECT_ID('dbo.CalendarEvents'))
    CREATE INDEX IX_CalEvent_Window ON dbo.CalendarEvents (StartsAtUtc, EndsAtUtc, IsActive) INCLUDE (Title, Category, Audience, CourseId);
GO

-- Seed rooms and periods -----------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Rooms)
BEGIN
    INSERT dbo.Rooms (RoomCode, RoomName, Building, Capacity) VALUES
      (N'R101', N'Lecture Room 101', N'Main Block', 60),
      (N'R102', N'Lecture Room 102', N'Main Block', 60),
      (N'R201', N'Commerce Hall 201', N'Commerce Block', 80),
      (N'LAB1', N'Computer Lab 1', N'IT Block', 40);
    PRINT N'Rooms seeded.';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.TimetablePeriods)
BEGIN
    INSERT dbo.TimetablePeriods (PeriodName, StartTime, EndTime, SortOrder) VALUES
      (N'Period 1', '09:00', '09:50', 1),
      (N'Period 2', '10:00', '10:50', 2),
      (N'Period 3', '11:00', '11:50', 3),
      (N'Period 4', '12:00', '12:50', 4),
      (N'Period 5', '14:00', '14:50', 5),
      (N'Period 6', '15:00', '15:50', 6);
    PRINT N'Timetable periods seeded.';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.TimetableEntries)
BEGIN
    DECLARE @sessionId INT = (SELECT TOP 1 SessionId FROM dbo.AcademicSessions WHERE IsActive = 1 ORDER BY SessionId);
    DECLARE @adminId INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Email = N'admin@sms.edu');
    DECLARE @fac1 INT = (SELECT TOP 1 u.UserId FROM dbo.Users u JOIN dbo.UserRoles ur ON ur.UserId = u.UserId WHERE ur.RoleId = 2 AND u.Email = N'faculty@sms.edu');
    DECLARE @fac2 INT = (SELECT TOP 1 u.UserId FROM dbo.Users u JOIN dbo.UserRoles ur ON ur.UserId = u.UserId WHERE ur.RoleId = 2 AND u.Email = N'faculty2@sms.edu');

    INSERT dbo.TimetableEntries
        (CourseId, SemesterNumber, SectionCode, SubjectId, FacultyUserId, RoomId, PeriodId, DayOfWeek, SessionId, EffectiveFrom, EffectiveTo, CreatedBy)
    SELECT c.CourseId, 1, N'A', s.SubjectId,
           COALESCE(fa.UserId, CASE WHEN x.CourseCode = N'BCOM' THEN @fac2 ELSE @fac1 END),
           r.RoomId, p.PeriodId, x.DayOfWeek, @sessionId, '2025-07-01', '2026-06-30', @adminId
    FROM (VALUES
        (N'BSC-CS', N'CS101', N'R101', 1, 1),
        (N'BSC-CS', N'CS102', N'R102', 1, 2),
        (N'BSC-CS', N'CS103', N'R101', 2, 1),
        (N'BSC-CS', N'GE101', N'R102', 2, 2),
        (N'BSC-CS', N'CS101', N'LAB1', 3, 1),
        (N'BSC-CS', N'CS102', N'R102', 4, 1),
        (N'BSC-CS', N'CS103', N'R101', 5, 1),
        (N'BSC-CS', N'GE101', N'R102', 5, 2),
        (N'BCOM',   N'BC101', N'R201', 1, 3),
        (N'BCOM',   N'BC102', N'R201', 2, 3),
        (N'BCOM',   N'BC101', N'R201', 3, 3),
        (N'BCOM',   N'BC102', N'R201', 4, 3)
    ) x (CourseCode, SubjectCode, RoomCode, DayOfWeek, PeriodSort)
    JOIN dbo.Courses c ON c.CourseCode = x.CourseCode
    JOIN dbo.Subjects s ON s.SubjectCode = x.SubjectCode
    JOIN dbo.Rooms r ON r.RoomCode = x.RoomCode
    JOIN dbo.TimetablePeriods p ON p.SortOrder = x.PeriodSort
    OUTER APPLY (
        SELECT TOP 1 f.UserId
        FROM dbo.FacultyAssignments a
        JOIN dbo.Faculty f ON f.FacultyId = a.FacultyId
        WHERE a.CourseId = c.CourseId AND a.SubjectId = s.SubjectId
        ORDER BY a.AssignmentId
    ) fa;
    PRINT N'Timetable entries seeded.';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.CalendarEvents)
BEGIN
    DECLARE @adminUser INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Email = N'admin@sms.edu');
    DECLARE @today DATE = CAST(SYSUTCDATETIME() AS DATE);
    INSERT dbo.CalendarEvents
        (Title, Description, Category, Audience, StartsAtUtc, EndsAtUtc, IsAllDay, Location, CreatedBy)
    VALUES
      (N'Mid-semester examinations', N'Check the exam timetable for room and seat details.', N'exam', N'students', DATEADD(DAY, 7, CAST(@today AS DATETIME2(0))), DATEADD(DAY, 12, CAST(@today AS DATETIME2(0))), 1, N'Exam halls', @adminUser),
      (N'Fee payment deadline', N'Clear outstanding fees before the late-fee window opens.', N'deadline', N'all', DATEADD(DAY, 21, CAST(@today AS DATETIME2(0))), DATEADD(SECOND, 86399, DATEADD(DAY, 21, CAST(@today AS DATETIME2(0)))), 1, NULL, @adminUser),
      (N'Annual cultural festival', N'Three days of student clubs, performances and exhibitions.', N'event', N'all', DATEADD(DAY, 30, CAST(@today AS DATETIME2(0))), DATEADD(SECOND, 86399, DATEADD(DAY, 32, CAST(@today AS DATETIME2(0)))), 1, N'Main campus', @adminUser),
      (N'Faculty development workshop', N'Outcome-based education and assessment design.', N'academic', N'faculty', DATEADD(HOUR, 9, DATEADD(DAY, 3, CAST(@today AS DATETIME2(0)))), DATEADD(HOUR, 11, DATEADD(DAY, 3, CAST(@today AS DATETIME2(0)))), 0, N'Seminar Hall', @adminUser);
    PRINT N'Calendar events seeded.';
END
GO

PRINT N'14_CampusModules.sql complete.';
GO
