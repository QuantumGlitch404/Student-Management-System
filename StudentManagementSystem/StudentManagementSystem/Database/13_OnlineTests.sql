-- =====================================================================
-- 13_OnlineTests.sql
-- Online tests, question bank, attempts, answers and proctoring events.
-- Idempotent.
-- =====================================================================
USE [StudentSMS];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.QuestionBank', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.QuestionBank (
        QuestionId      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SubjectId       INT           NOT NULL,
        Prompt          NVARCHAR(1000) NOT NULL,
        OptionA         NVARCHAR(400) NOT NULL,
        OptionB         NVARCHAR(400) NOT NULL,
        OptionC         NVARCHAR(400) NULL,
        OptionD         NVARCHAR(400) NULL,
        CorrectOption   CHAR(1)       NOT NULL,
        Explanation     NVARCHAR(1000) NULL,
        Difficulty      VARCHAR(10)   NOT NULL CONSTRAINT DF_QB_Difficulty DEFAULT ('medium'),
        IsActive        BIT           NOT NULL CONSTRAINT DF_QB_Active DEFAULT (1),
        CreatedBy       INT           NULL,
        CreatedAtUtc    DATETIME2(0)  NOT NULL CONSTRAINT DF_QB_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_QB_Subject FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects (SubjectId),
        CONSTRAINT FK_QB_User FOREIGN KEY (CreatedBy) REFERENCES dbo.Users (UserId),
        CONSTRAINT CK_QB_Correct CHECK (CorrectOption IN ('A', 'B', 'C', 'D')),
        CONSTRAINT CK_QB_Difficulty CHECK (Difficulty IN ('easy', 'medium', 'hard'))
    );
    PRINT N'QuestionBank created.';
END
GO

IF OBJECT_ID('dbo.OnlineTests', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OnlineTests (
        TestId              INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CourseId            INT           NOT NULL,
        SubjectId           INT           NOT NULL,
        Title               NVARCHAR(160) NOT NULL,
        Instructions        NVARCHAR(2000) NULL,
        DurationMinutes     INT           NOT NULL CONSTRAINT DF_OT_Duration DEFAULT (30),
        OpensAtUtc          DATETIME2(0)  NULL,
        ClosesAtUtc         DATETIME2(0)  NULL,
        MaxAttempts         TINYINT       NOT NULL CONSTRAINT DF_OT_Attempts DEFAULT (1),
        PassPercent         DECIMAL(5,2)  NOT NULL CONSTRAINT DF_OT_Pass DEFAULT (40),
        ShuffleQuestions    BIT           NOT NULL CONSTRAINT DF_OT_Shuffle DEFAULT (1),
        ProctoringEnabled   BIT           NOT NULL CONSTRAINT DF_OT_Proctor DEFAULT (1),
        IsPublished         BIT           NOT NULL CONSTRAINT DF_OT_Published DEFAULT (0),
        CreatedBy           INT           NULL,
        CreatedAtUtc        DATETIME2(0)  NOT NULL CONSTRAINT DF_OT_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_OT_Course FOREIGN KEY (CourseId) REFERENCES dbo.Courses (CourseId),
        CONSTRAINT FK_OT_Subject FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects (SubjectId),
        CONSTRAINT FK_OT_User FOREIGN KEY (CreatedBy) REFERENCES dbo.Users (UserId),
        CONSTRAINT CK_OT_Duration CHECK (DurationMinutes BETWEEN 1 AND 480),
        CONSTRAINT CK_OT_Attempts CHECK (MaxAttempts BETWEEN 1 AND 10),
        CONSTRAINT CK_OT_Pass CHECK (PassPercent BETWEEN 0 AND 100)
    );
    PRINT N'OnlineTests created.';
END
GO

IF OBJECT_ID('dbo.OnlineTestQuestions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OnlineTestQuestions (
        TestId          INT           NOT NULL,
        QuestionId      INT           NOT NULL,
        Points          DECIMAL(6,2)  NOT NULL CONSTRAINT DF_OTQ_Points DEFAULT (1),
        SortOrder       INT           NOT NULL CONSTRAINT DF_OTQ_Sort DEFAULT (0),
        CONSTRAINT PK_OnlineTestQuestions PRIMARY KEY (TestId, QuestionId),
        CONSTRAINT FK_OTQ_Test FOREIGN KEY (TestId) REFERENCES dbo.OnlineTests (TestId) ON DELETE CASCADE,
        CONSTRAINT FK_OTQ_Question FOREIGN KEY (QuestionId) REFERENCES dbo.QuestionBank (QuestionId) ON DELETE CASCADE,
        CONSTRAINT CK_OTQ_Points CHECK (Points > 0)
    );
    PRINT N'OnlineTestQuestions created.';
END
GO

IF OBJECT_ID('dbo.OnlineTestAttempts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OnlineTestAttempts (
        AttemptId       INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TestId          INT           NOT NULL,
        StudentId       INT           NOT NULL,
        StartedAtUtc    DATETIME2(0)  NOT NULL CONSTRAINT DF_OTA_Started DEFAULT (SYSUTCDATETIME()),
        ExpiresAtUtc    DATETIME2(0)  NOT NULL,
        SubmittedAtUtc  DATETIME2(0)  NULL,
        Score           DECIMAL(6,2)  NULL,
        MaxScore        DECIMAL(6,2)  NOT NULL CONSTRAINT DF_OTA_Max DEFAULT (0),
        Status          VARCHAR(12)   NOT NULL CONSTRAINT DF_OTA_Status DEFAULT ('in_progress'),
        Violations      INT           NOT NULL CONSTRAINT DF_OTA_Violations DEFAULT (0),
        LastSavedAtUtc  DATETIME2(0)  NULL,
        CONSTRAINT FK_OTA_Test FOREIGN KEY (TestId) REFERENCES dbo.OnlineTests (TestId) ON DELETE CASCADE,
        CONSTRAINT FK_OTA_Student FOREIGN KEY (StudentId) REFERENCES dbo.Students (StudentId) ON DELETE CASCADE,
        CONSTRAINT CK_OTA_Status CHECK (Status IN ('in_progress', 'submitted', 'expired'))
    );
    PRINT N'OnlineTestAttempts created.';
END
GO

IF OBJECT_ID('dbo.OnlineTestAnswers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OnlineTestAnswers (
        AttemptId       INT           NOT NULL,
        QuestionId      INT           NOT NULL,
        SelectedOption  CHAR(1)       NULL,
        IsCorrect       BIT           NOT NULL CONSTRAINT DF_OTAns_Correct DEFAULT (0),
        PointsAwarded   DECIMAL(6,2)  NOT NULL CONSTRAINT DF_OTAns_Points DEFAULT (0),
        AnsweredAtUtc   DATETIME2(0)  NOT NULL CONSTRAINT DF_OTAns_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_OnlineTestAnswers PRIMARY KEY (AttemptId, QuestionId),
        CONSTRAINT FK_OTAns_Attempt FOREIGN KEY (AttemptId) REFERENCES dbo.OnlineTestAttempts (AttemptId) ON DELETE CASCADE,
        CONSTRAINT FK_OTAns_Question FOREIGN KEY (QuestionId) REFERENCES dbo.QuestionBank (QuestionId)
    );
    PRINT N'OnlineTestAnswers created.';
END
GO

IF OBJECT_ID('dbo.OnlineTestEvents', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OnlineTestEvents (
        EventId         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AttemptId       INT           NOT NULL,
        EventType       VARCHAR(40)   NOT NULL,
        Detail          NVARCHAR(300) NULL,
        OccurredAtUtc   DATETIME2(0)  NOT NULL CONSTRAINT DF_OTE_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_OTE_Attempt FOREIGN KEY (AttemptId) REFERENCES dbo.OnlineTestAttempts (AttemptId) ON DELETE CASCADE
    );
    PRINT N'OnlineTestEvents created.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QB_Subject' AND object_id = OBJECT_ID('dbo.QuestionBank'))
    CREATE INDEX IX_QB_Subject ON dbo.QuestionBank (SubjectId, IsActive);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OT_Course' AND object_id = OBJECT_ID('dbo.OnlineTests'))
    CREATE INDEX IX_OT_Course ON dbo.OnlineTests (CourseId, IsPublished, ClosesAtUtc);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OTA_Test_Student' AND object_id = OBJECT_ID('dbo.OnlineTestAttempts'))
    CREATE INDEX IX_OTA_Test_Student ON dbo.OnlineTestAttempts (TestId, StudentId, Status);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OTE_Attempt' AND object_id = OBJECT_ID('dbo.OnlineTestEvents'))
    CREATE INDEX IX_OTE_Attempt ON dbo.OnlineTestEvents (AttemptId, OccurredAtUtc);
GO

PRINT N'13_OnlineTests.sql complete.';
GO
