-- ==============================================================
-- 10_Premium_AI.sql - premium redesign, zoom, AI assistant and
-- module expansion. Idempotent: safe on fresh installs and on an
-- existing database. No existing row is deleted.
-- ==============================================================
USE [StudentSMS];
GO

/* ------------------------- preferences ------------------------ */

IF COL_LENGTH('dbo.UserPreferences', 'Zoom') IS NULL
BEGIN
    ALTER TABLE dbo.UserPreferences ADD Zoom TINYINT NOT NULL
        CONSTRAINT DF_UP_Zoom DEFAULT (100);
    PRINT N'UserPreferences.Zoom added.';
END
GO
IF COL_LENGTH('dbo.UserPreferences', 'AiEnabled') IS NULL
    ALTER TABLE dbo.UserPreferences ADD AiEnabled BIT NOT NULL CONSTRAINT DF_UP_AiEnabled DEFAULT (1);
GO
IF COL_LENGTH('dbo.UserPreferences', 'AiContext') IS NULL
    ALTER TABLE dbo.UserPreferences ADD AiContext BIT NOT NULL CONSTRAINT DF_UP_AiContext DEFAULT (1);
GO
IF COL_LENGTH('dbo.UserPreferences', 'AiResponseLength') IS NULL
    ALTER TABLE dbo.UserPreferences ADD AiResponseLength VARCHAR(10) NOT NULL CONSTRAINT DF_UP_AiLength DEFAULT ('balanced');
GO
IF COL_LENGTH('dbo.UserPreferences', 'QuietHours') IS NULL
    ALTER TABLE dbo.UserPreferences ADD QuietHours VARCHAR(20) NULL;
GO
IF COL_LENGTH('dbo.UserPreferences', 'NotifSound') IS NULL
    ALTER TABLE dbo.UserPreferences ADD NotifSound BIT NOT NULL CONSTRAINT DF_UP_NotifSound DEFAULT (0);
GO

-- Theme: allow the third appearance. The old CHECK only knew light/dark/system.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_UP_Theme' AND parent_object_id = OBJECT_ID('dbo.UserPreferences'))
BEGIN
    ALTER TABLE dbo.UserPreferences DROP CONSTRAINT CK_UP_Theme;
    PRINT N'CK_UP_Theme replaced.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_UP_Theme_Eye' AND parent_object_id = OBJECT_ID('dbo.UserPreferences'))
    ALTER TABLE dbo.UserPreferences ADD CONSTRAINT CK_UP_Theme_Eye
        CHECK (Theme IN ('light', 'dark', 'eye', 'system'));
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_UP_Zoom' AND parent_object_id = OBJECT_ID('dbo.UserPreferences'))
    ALTER TABLE dbo.UserPreferences ADD CONSTRAINT CK_UP_Zoom CHECK (Zoom BETWEEN 80 AND 150);
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_UP_AiLength' AND parent_object_id = OBJECT_ID('dbo.UserPreferences'))
    ALTER TABLE dbo.UserPreferences ADD CONSTRAINT CK_UP_AiLength
        CHECK (AiResponseLength IN ('short', 'balanced', 'detailed'));
GO

/* ------------------------ notifications ----------------------- */

IF COL_LENGTH('dbo.Notifications', 'Archived') IS NULL
    ALTER TABLE dbo.Notifications ADD Archived BIT NOT NULL CONSTRAINT DF_Notif_Archived DEFAULT (0);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NRec_User_Archived' AND object_id = OBJECT_ID('dbo.NotificationRecipients'))
    CREATE INDEX IX_NRec_User_Archived ON dbo.NotificationRecipients (UserId, NotificationId DESC) INCLUDE (IsRead, ReadAtUtc);
GO

/* ------------------------ announcements ----------------------- */

IF COL_LENGTH('dbo.Announcements', 'Category') IS NULL
    ALTER TABLE dbo.Announcements ADD Category VARCHAR(30) NOT NULL CONSTRAINT DF_Ann_Category DEFAULT ('general');
GO
IF COL_LENGTH('dbo.Announcements', 'IsPinned') IS NULL
    ALTER TABLE dbo.Announcements ADD IsPinned BIT NOT NULL CONSTRAINT DF_Ann_Pinned DEFAULT (0);
GO
IF COL_LENGTH('dbo.Announcements', 'TargetCourseId') IS NULL
    ALTER TABLE dbo.Announcements ADD TargetCourseId INT NULL;
GO
IF COL_LENGTH('dbo.Announcements', 'TargetSemester') IS NULL
    ALTER TABLE dbo.Announcements ADD TargetSemester TINYINT NULL;
GO

IF OBJECT_ID('dbo.AnnouncementAcks', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AnnouncementAcks (
        AnnId     INT NOT NULL,
        UserId    INT NOT NULL,
        AckAtUtc  DATETIME2(0) NOT NULL CONSTRAINT DF_AnnAck_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_AnnouncementAcks PRIMARY KEY (AnnId, UserId),
        CONSTRAINT FK_AnnAck_Ann FOREIGN KEY (AnnId) REFERENCES dbo.Announcements (AnnId) ON DELETE CASCADE,
        CONSTRAINT FK_AnnAck_User FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId)
    );
    PRINT N'AnnouncementAcks created.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Announcements_Window' AND object_id = OBJECT_ID('dbo.Announcements'))
    CREATE INDEX IX_Announcements_Window ON dbo.Announcements (IsActive, PublishFrom, PublishTo) INCLUDE (Audience, IsPinned);
GO

/* -------------------------- complaints ------------------------ */

IF COL_LENGTH('dbo.Complaints', 'Priority') IS NULL
    ALTER TABLE dbo.Complaints ADD Priority VARCHAR(10) NOT NULL CONSTRAINT DF_Compl_Priority DEFAULT ('normal');
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Compl_Priority' AND parent_object_id = OBJECT_ID('dbo.Complaints'))
    ALTER TABLE dbo.Complaints ADD CONSTRAINT CK_Compl_Priority
        CHECK (Priority IN ('low', 'normal', 'high', 'urgent'));
GO
IF COL_LENGTH('dbo.Complaints', 'ResolutionNote') IS NULL
    ALTER TABLE dbo.Complaints ADD ResolutionNote NVARCHAR(500) NULL;
GO

IF OBJECT_ID('dbo.ComplaintEvents', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ComplaintEvents (
        EventId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ComplaintId INT NOT NULL,
        ActorId     INT NULL,
        EventType   VARCHAR(30) NOT NULL,      -- created|assigned|status|priority|reply|note|reopened
        OldValue    NVARCHAR(100) NULL,
        NewValue    NVARCHAR(100) NULL,
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_CompEv_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_CompEv_Complaint FOREIGN KEY (ComplaintId) REFERENCES dbo.Complaints (ComplaintId) ON DELETE CASCADE,
        CONSTRAINT FK_CompEv_Actor FOREIGN KEY (ActorId) REFERENCES dbo.Users (UserId)
    );
    PRINT N'ComplaintEvents created.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ComplaintEvents_Complaint' AND object_id = OBJECT_ID('dbo.ComplaintEvents'))
    CREATE INDEX IX_ComplaintEvents_Complaint ON dbo.ComplaintEvents (ComplaintId, EventId);
GO

/* --------------------------- sign-in history ------------------ */

IF OBJECT_ID('dbo.LoginHistory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LoginHistory (
        LoginId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId      INT NOT NULL,
        LoginAtUtc  DATETIME2(0) NOT NULL CONSTRAINT DF_LoginHist_At DEFAULT (SYSUTCDATETIME()),
        ClientIp    VARCHAR(45) NULL,
        UserAgent   NVARCHAR(300) NULL,
        WasSuccessful BIT NOT NULL CONSTRAINT DF_LoginHist_Ok DEFAULT (1),
        CONSTRAINT FK_LoginHist_User FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE CASCADE
    );
    PRINT N'LoginHistory created.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LoginHistory_User' AND object_id = OBJECT_ID('dbo.LoginHistory'))
    CREATE INDEX IX_LoginHistory_User ON dbo.LoginHistory (UserId, LoginAtUtc DESC);
GO

/* ------------------------- AI assistant ----------------------- */

IF OBJECT_ID('dbo.AiConversations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiConversations (
        ConversationId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId         INT NOT NULL,
        Title          NVARCHAR(120) NOT NULL CONSTRAINT DF_AiConv_Title DEFAULT (N'New conversation'),
        IsPinned       BIT NOT NULL CONSTRAINT DF_AiConv_Pin DEFAULT (0),
        CreatedAtUtc   DATETIME2(0) NOT NULL CONSTRAINT DF_AiConv_Created DEFAULT (SYSUTCDATETIME()),
        LastActivityUtc DATETIME2(0) NOT NULL CONSTRAINT DF_AiConv_Activity DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_AiConv_User FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE CASCADE
    );
    PRINT N'AiConversations created.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AiConversations_User' AND object_id = OBJECT_ID('dbo.AiConversations'))
    CREATE INDEX IX_AiConversations_User ON dbo.AiConversations (UserId, IsPinned DESC, LastActivityUtc DESC);
GO

IF OBJECT_ID('dbo.AiMessages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiMessages (
        MessageId      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ConversationId INT NOT NULL,
        Role           VARCHAR(10) NOT NULL,          -- user | assistant
        Content        NVARCHAR(MAX) NOT NULL,
        MetaJson       NVARCHAR(500) NULL,            -- model, latency, token counts, context used
        CreatedAtUtc   DATETIME2(0) NOT NULL CONSTRAINT DF_AiMsg_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_AiMsg_Conv FOREIGN KEY (ConversationId) REFERENCES dbo.AiConversations (ConversationId) ON DELETE CASCADE,
        CONSTRAINT CK_AiMsg_Role CHECK (Role IN ('user', 'assistant', 'system'))
    );
    PRINT N'AiMessages created.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AiMessages_Conv' AND object_id = OBJECT_ID('dbo.AiMessages'))
    CREATE INDEX IX_AiMessages_Conv ON dbo.AiMessages (ConversationId, MessageId);
GO

/* Audit table is append-only for the application role: no UPDATE/DELETE
   statements exist in the codebase. Keep an explicit model note. */
IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'Attendance:ShortagePercent')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue, Description)
    VALUES ('Attendance:ShortagePercent', '75', N'Attendance percentage below which a student is flagged at risk');
GO
IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'Ai:Enabled')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue, Description)
    VALUES ('Ai:Enabled', 'true', N'Offline assistant master switch');
GO

PRINT N'10_Premium_AI.sql complete.';
GO
