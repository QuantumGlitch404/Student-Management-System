-- =====================================================================
-- 24_FeesTimetable.sql
-- Fee structures per course (versioned) and timetable templates,
-- exceptions, versions and status. Idempotent.
-- =====================================================================
USE [StudentSMS];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

/* =================== FEES PER COURSE =================== */
IF OBJECT_ID('dbo.FeeStructures','U') IS NULL
BEGIN
    CREATE TABLE dbo.FeeStructures (
        FeeStructureId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CourseId       INT            NOT NULL,
        SemesterNumber TINYINT        NULL,
        SessionId      INT            NULL,
        Name           NVARCHAR(120)  NOT NULL,
        Version        INT            NOT NULL CONSTRAINT DF_FeeStructure_Version DEFAULT (1),
        Status         VARCHAR(20)    NOT NULL CONSTRAINT DF_FeeStructure_Status DEFAULT ('active'),
        IsDeleted      BIT            NOT NULL CONSTRAINT DF_FeeStructure_Deleted DEFAULT (0),
        CreatedBy      INT            NULL,
        CreatedAtUtc   DATETIME2(0)   NOT NULL CONSTRAINT DF_FeeStructure_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc   DATETIME2(0)   NOT NULL CONSTRAINT DF_FeeStructure_Updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_FeeStructures_Course FOREIGN KEY (CourseId) REFERENCES dbo.Courses (CourseId),
        CONSTRAINT FK_FeeStructures_Session FOREIGN KEY (SessionId) REFERENCES dbo.AcademicSessions (SessionId),
        CONSTRAINT CK_FeeStructures_Status CHECK (Status IN ('active','archived'))
    );
    CREATE INDEX IX_FeeStructures_Course ON dbo.FeeStructures (CourseId, SemesterNumber, SessionId);
    PRINT N'FeeStructures created.';
END
GO

IF OBJECT_ID('dbo.FeeStructureItems','U') IS NULL
BEGIN
    CREATE TABLE dbo.FeeStructureItems (
        FeeItemId      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        FeeStructureId INT            NOT NULL,
        FeeCode        VARCHAR(24)    NOT NULL,
        FeeName        NVARCHAR(120)  NOT NULL,
        Amount         DECIMAL(10,2)  NOT NULL,
        DueDate        DATE           NULL,
        Instalments    TINYINT        NOT NULL CONSTRAINT DF_FeeItem_Instalments DEFAULT (1),
        LateFeeRule    NVARCHAR(200)  NULL,
        SortOrder      INT            NOT NULL CONSTRAINT DF_FeeItem_Sort DEFAULT (0),
        CONSTRAINT FK_FeeItems_Structure FOREIGN KEY (FeeStructureId) REFERENCES dbo.FeeStructures (FeeStructureId) ON DELETE CASCADE,
        CONSTRAINT CK_FeeItems_Amount CHECK (Amount >= 0)
    );
    PRINT N'FeeStructureItems created.';
END
GO

IF OBJECT_ID('dbo.FeeStructureVersions','U') IS NULL
BEGIN
    CREATE TABLE dbo.FeeStructureVersions (
        VersionId      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        FeeStructureId INT            NOT NULL,
        Version        INT            NOT NULL,
        SnapshotJson   NVARCHAR(MAX)  NOT NULL,
        ChangeSummary  NVARCHAR(400)  NULL,
        ApplyScope     VARCHAR(30)    NULL,
        CreatedBy      INT            NULL,
        CreatedAtUtc   DATETIME2(0)   NOT NULL CONSTRAINT DF_FeeVersion_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_FeeVersions_Structure FOREIGN KEY (FeeStructureId) REFERENCES dbo.FeeStructures (FeeStructureId) ON DELETE CASCADE
    );
    PRINT N'FeeStructureVersions created.';
END
GO

/* Map the legacy per-course Fees rows into one structure per course. */
IF NOT EXISTS (SELECT 1 FROM dbo.FeeStructures)
BEGIN
    INSERT INTO dbo.FeeStructures (CourseId, Name, Version, Status, CreatedAtUtc, UpdatedAtUtc)
    SELECT c.CourseId, c.CourseName + N' fee plan', 1, 'active', SYSUTCDATETIME(), SYSUTCDATETIME()
    FROM dbo.Courses c
    WHERE EXISTS (SELECT 1 FROM dbo.Fees f WHERE f.CourseId = c.CourseId);

    INSERT INTO dbo.FeeStructureItems (FeeStructureId, FeeCode, FeeName, Amount, SortOrder)
    SELECT fs.FeeStructureId, f.FeeCode, f.FeeName, f.Amount, f.FeeId
    FROM dbo.Fees f
    JOIN dbo.FeeStructures fs ON fs.CourseId = f.CourseId AND fs.SemesterNumber IS NULL;
    PRINT N'Legacy fee heads mapped to course structures.';
END
GO

/* =================== PAYMENT TEST MODE =================== */
IF COL_LENGTH('dbo.Payments','IsTestMode') IS NULL
    ALTER TABLE dbo.Payments ADD IsTestMode BIT NOT NULL CONSTRAINT DF_Payments_IsTestMode DEFAULT (0);
GO

/* =================== TIMETABLE TEMPLATES =================== */
IF OBJECT_ID('dbo.TimetableTemplates','U') IS NULL
BEGIN
    CREATE TABLE dbo.TimetableTemplates (
        TemplateId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CourseId       INT            NOT NULL,
        SemesterNumber INT            NOT NULL,
        SectionCode    VARCHAR(20)    NOT NULL,
        SessionId      INT            NULL,
        Status         VARCHAR(20)    NOT NULL CONSTRAINT DF_TTTemplate_Status DEFAULT ('draft'),
        PublishedUtc   DATETIME2(0)   NULL,
        DeletedBy      INT            NULL,
        DeletedUtc     DATETIME2(0)   NULL,
        DeleteReason   NVARCHAR(300)  NULL,
        CreatedBy      INT            NULL,
        CreatedAtUtc   DATETIME2(0)   NOT NULL CONSTRAINT DF_TTTemplate_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc   DATETIME2(0)   NOT NULL CONSTRAINT DF_TTTemplate_Updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_TTTemplates_Course FOREIGN KEY (CourseId) REFERENCES dbo.Courses (CourseId),
        CONSTRAINT FK_TTTemplates_Session FOREIGN KEY (SessionId) REFERENCES dbo.AcademicSessions (SessionId),
        CONSTRAINT CK_TTTemplates_Status CHECK (Status IN ('draft','published','deleted'))
    );
    CREATE UNIQUE INDEX UX_TTTemplates_Scope ON dbo.TimetableTemplates (CourseId, SemesterNumber, SectionCode, SessionId);
    PRINT N'TimetableTemplates created.';
END
GO

IF COL_LENGTH('dbo.TimetableEntries','TemplateId') IS NULL
    ALTER TABLE dbo.TimetableEntries ADD TemplateId INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TTEntry_Template')
    ALTER TABLE dbo.TimetableEntries ADD CONSTRAINT FK_TTEntry_Template
        FOREIGN KEY (TemplateId) REFERENCES dbo.TimetableTemplates (TemplateId);
GO

/* Backfill a template for every existing timetable scope, then link entries. */
INSERT INTO dbo.TimetableTemplates (CourseId, SemesterNumber, SectionCode, SessionId, Status, PublishedUtc, CreatedAtUtc, UpdatedAtUtc)
SELECT g.CourseId, g.SemesterNumber, g.SectionCode, g.SessionId, 'published',
       SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME()
FROM (
    SELECT DISTINCT CourseId, SemesterNumber, SectionCode, SessionId
    FROM dbo.TimetableEntries
) g
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.TimetableTemplates t
    WHERE t.CourseId = g.CourseId AND t.SemesterNumber = g.SemesterNumber
      AND t.SectionCode = g.SectionCode
      AND (t.SessionId = g.SessionId OR (t.SessionId IS NULL AND g.SessionId IS NULL))
);
GO

UPDATE e SET e.TemplateId = t.TemplateId
FROM dbo.TimetableEntries e
JOIN dbo.TimetableTemplates t
  ON t.CourseId = e.CourseId AND t.SemesterNumber = e.SemesterNumber
 AND t.SectionCode = e.SectionCode
 AND (t.SessionId = e.SessionId OR (t.SessionId IS NULL AND e.SessionId IS NULL))
WHERE e.TemplateId IS NULL;
GO

IF OBJECT_ID('dbo.TimetableExceptions','U') IS NULL
BEGIN
    CREATE TABLE dbo.TimetableExceptions (
        ExceptionId   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TemplateId    INT            NOT NULL,
        EntryId       INT            NULL,
        ExceptionDate DATE           NOT NULL,
        Kind          VARCHAR(20)    NOT NULL,      -- cancelled | substitute | room | extra
        SubjectId     INT            NULL,
        FacultyUserId INT            NULL,
        RoomId        INT            NULL,
        PeriodId      INT            NULL,
        DayOfWeek     TINYINT        NULL,
        Note          NVARCHAR(300)  NULL,
        CreatedBy     INT            NULL,
        CreatedAtUtc  DATETIME2(0)   NOT NULL CONSTRAINT DF_TTException_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_TTExceptions_Template FOREIGN KEY (TemplateId) REFERENCES dbo.TimetableTemplates (TemplateId) ON DELETE CASCADE,
        CONSTRAINT FK_TTExceptions_Entry FOREIGN KEY (EntryId) REFERENCES dbo.TimetableEntries (EntryId),
        CONSTRAINT FK_TTExceptions_Subject FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects (SubjectId),
        CONSTRAINT FK_TTExceptions_Faculty FOREIGN KEY (FacultyUserId) REFERENCES dbo.Users (UserId),
        CONSTRAINT FK_TTExceptions_Room FOREIGN KEY (RoomId) REFERENCES dbo.Rooms (RoomId),
        CONSTRAINT FK_TTExceptions_Period FOREIGN KEY (PeriodId) REFERENCES dbo.TimetablePeriods (PeriodId),
        CONSTRAINT CK_TTExceptions_Kind CHECK (Kind IN ('cancelled','substitute','room','extra'))
    );
    CREATE INDEX IX_TTExceptions_Date ON dbo.TimetableExceptions (ExceptionDate, TemplateId);
    PRINT N'TimetableExceptions created.';
END
GO

IF OBJECT_ID('dbo.TimetableVersions','U') IS NULL
BEGIN
    CREATE TABLE dbo.TimetableVersions (
        VersionId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TemplateId    INT            NOT NULL,
        Action        VARCHAR(20)    NOT NULL,
        SnapshotJson  NVARCHAR(MAX)  NOT NULL,
        Reason        NVARCHAR(300)  NULL,
        SlotCount     INT            NOT NULL CONSTRAINT DF_TTVersion_Slots DEFAULT (0),
        CreatedBy     INT            NULL,
        CreatedAtUtc  DATETIME2(0)   NOT NULL CONSTRAINT DF_TTVersion_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_TTVersions_Template FOREIGN KEY (TemplateId) REFERENCES dbo.TimetableTemplates (TemplateId) ON DELETE CASCADE,
        CONSTRAINT CK_TTVersions_Action CHECK (Action IN ('Created','Edited','Deleted','Restored','Published'))
    );
    CREATE INDEX IX_TTVersions_Template ON dbo.TimetableVersions (TemplateId, CreatedAtUtc DESC);
    PRINT N'TimetableVersions created.';
END
GO

/* Existing scopes get an initial snapshot so History is never empty. */
INSERT INTO dbo.TimetableVersions (TemplateId, Action, SnapshotJson, Reason, SlotCount, CreatedAtUtc)
SELECT t.TemplateId, 'Published',
       (SELECT e.EntryId, e.SubjectId, e.FacultyUserId, e.RoomId, e.PeriodId, e.DayOfWeek,
               e.EffectiveFrom, e.EffectiveTo, e.Notes, e.IsActive
        FROM dbo.TimetableEntries e WHERE e.TemplateId = t.TemplateId
        FOR JSON PATH),
       N'Imported from the existing timetable', 
       (SELECT COUNT(*) FROM dbo.TimetableEntries e WHERE e.TemplateId = t.TemplateId),
       SYSUTCDATETIME()
FROM dbo.TimetableTemplates t
WHERE NOT EXISTS (SELECT 1 FROM dbo.TimetableVersions v WHERE v.TemplateId = t.TemplateId);
GO

/* Faculty editing policy: all slots of assigned courses by default. */
IF NOT EXISTS (SELECT 1 FROM dbo.InstitutionSettings WHERE SettingKey = 'Timetable.FacultyEditing')
    INSERT INTO dbo.InstitutionSettings (SettingKey, SettingValue) VALUES ('Timetable.FacultyEditing', N'course');
GO

PRINT N'24_FeesTimetable.sql complete.';
GO
