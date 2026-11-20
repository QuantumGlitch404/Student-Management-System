-- =====================================================================
-- 17_MailCenter.sql
-- Free in-app mail centre: accounts, templates, bulk queue, recipients,
-- received-message cache, suppression and institution settings.
-- Idempotent. Never drops data.
-- =====================================================================
USE [StudentSMS];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.UserPreferences', 'Density') IS NULL
    ALTER TABLE dbo.UserPreferences ADD Density VARCHAR(12) NOT NULL
        CONSTRAINT DF_UserPrefs_Density DEFAULT ('comfortable');
GO

IF OBJECT_ID('dbo.InstitutionSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.InstitutionSettings (
        SettingKey   VARCHAR(80)   NOT NULL PRIMARY KEY,
        SettingValue NVARCHAR(MAX) NULL,
        UpdatedBy    INT           NULL,
        UpdatedAtUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_InstSettings_Updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_InstSettings_Users FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users (UserId)
    );
    PRINT N'InstitutionSettings created.';
END
GO

IF OBJECT_ID('dbo.MailAccounts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MailAccounts (
        AccountId        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Label            NVARCHAR(80)   NOT NULL,
        EmailAddress     NVARCHAR(150)  NOT NULL,
        DisplayName      NVARCHAR(100)  NULL,
        SmtpHost         NVARCHAR(150)  NULL,
        SmtpPort         INT            NOT NULL CONSTRAINT DF_MailAcc_SmtpPort DEFAULT (587),
        SmtpSsl          BIT            NOT NULL CONSTRAINT DF_MailAcc_SmtpSsl DEFAULT (1),
        SmtpUser         NVARCHAR(150)  NULL,
        SmtpPasswordEnc  NVARCHAR(500)  NULL,
        ImapHost         NVARCHAR(150)  NULL,
        ImapPort         INT            NOT NULL CONSTRAINT DF_MailAcc_ImapPort DEFAULT (993),
        ImapSsl          BIT            NOT NULL CONSTRAINT DF_MailAcc_ImapSsl DEFAULT (1),
        ImapUser         NVARCHAR(150)  NULL,
        ImapPasswordEnc  NVARCHAR(500)  NULL,
        DailyLimit       INT            NOT NULL CONSTRAINT DF_MailAcc_Daily DEFAULT (400),
        PerMinute        INT            NOT NULL CONSTRAINT DF_MailAcc_PerMin DEFAULT (20),
        IsDefault        BIT            NOT NULL CONSTRAINT DF_MailAcc_Default DEFAULT (0),
        IsActive         BIT            NOT NULL CONSTRAINT DF_MailAcc_Active DEFAULT (1),
        LastSyncUtc      DATETIME2(0)   NULL,
        LastError        NVARCHAR(400)  NULL,
        CreatedAtUtc     DATETIME2(0)   NOT NULL CONSTRAINT DF_MailAcc_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_MailAccounts_Email UNIQUE (EmailAddress)
    );
    PRINT N'MailAccounts created.';
END
GO

IF OBJECT_ID('dbo.MailTemplates', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MailTemplates (
        TemplateId   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name         NVARCHAR(80)   NOT NULL,
        Subject      NVARCHAR(200)  NOT NULL,
        BodyHtml     NVARCHAR(MAX)  NOT NULL,
        IsActive     BIT            NOT NULL CONSTRAINT DF_MailTpl_Active DEFAULT (1),
        CreatedAtUtc DATETIME2(0)   NOT NULL CONSTRAINT DF_MailTpl_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_MailTemplates_Name UNIQUE (Name)
    );
    PRINT N'MailTemplates created.';
END
GO

IF OBJECT_ID('dbo.MailQueue', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MailQueue (
        QueueId        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AccountId      INT            NULL,
        Subject        NVARCHAR(200)  NOT NULL,
        BodyHtml       NVARCHAR(MAX)  NOT NULL,
        CreatedBy      INT            NULL,
        Status         VARCHAR(12)    NOT NULL CONSTRAINT DF_MailQueue_Status DEFAULT ('queued'),
        ScheduledUtc   DATETIME2(0)   NOT NULL CONSTRAINT DF_MailQueue_Sched DEFAULT (SYSUTCDATETIME()),
        IsBulk         BIT            NOT NULL CONSTRAINT DF_MailQueue_Bulk DEFAULT (0),
        TotalRecipients INT           NOT NULL CONSTRAINT DF_MailQueue_Total DEFAULT (0),
        SentCount      INT            NOT NULL CONSTRAINT DF_MailQueue_Sent DEFAULT (0),
        FailedCount    INT            NOT NULL CONSTRAINT DF_MailQueue_Failed DEFAULT (0),
        Attempts       INT            NOT NULL CONSTRAINT DF_MailQueue_Attempts DEFAULT (0),
        LastError      NVARCHAR(400)  NULL,
        CreatedAtUtc   DATETIME2(0)   NOT NULL CONSTRAINT DF_MailQueue_Created DEFAULT (SYSUTCDATETIME()),
        CompletedAtUtc DATETIME2(0)   NULL,
        CONSTRAINT FK_MailQueue_Account FOREIGN KEY (AccountId) REFERENCES dbo.MailAccounts (AccountId),
        CONSTRAINT FK_MailQueue_User FOREIGN KEY (CreatedBy) REFERENCES dbo.Users (UserId),
        CONSTRAINT CK_MailQueue_Status CHECK (Status IN ('queued','sending','sent','failed','paused','cancelled'))
    );
    CREATE INDEX IX_MailQueue_Due ON dbo.MailQueue (Status, ScheduledUtc);
    PRINT N'MailQueue created.';
END
GO

IF OBJECT_ID('dbo.MailRecipients', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MailRecipients (
        RecipientId  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        QueueId      INT            NOT NULL,
        Email        NVARCHAR(150)  NOT NULL,
        DisplayName  NVARCHAR(120)  NULL,
        Status       VARCHAR(12)    NOT NULL CONSTRAINT DF_MailRcpt_Status DEFAULT ('queued'),
        Error        NVARCHAR(300)  NULL,
        UnsubscribeToken VARCHAR(48) NULL,
        SentAtUtc    DATETIME2(0)   NULL,
        CONSTRAINT FK_MailRecipients_Queue FOREIGN KEY (QueueId) REFERENCES dbo.MailQueue (QueueId),
        CONSTRAINT CK_MailRcpt_Status CHECK (Status IN ('queued','sent','failed','bounced','suppressed'))
    );
    CREATE INDEX IX_MailRecipients_Queue ON dbo.MailRecipients (QueueId, Status);
    PRINT N'MailRecipients created.';
END
GO

IF OBJECT_ID('dbo.MailMessages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MailMessages (
        MessageId    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AccountId    INT            NULL,
        Direction    VARCHAR(3)     NOT NULL CONSTRAINT DF_MailMsg_Dir DEFAULT ('in'),
        FromAddress  NVARCHAR(200)  NULL,
        ToAddress    NVARCHAR(400)  NULL,
        Subject      NVARCHAR(300)  NULL,
        BodyHtml     NVARCHAR(MAX)  NULL,
        BodyText     NVARCHAR(MAX)  NULL,
        Folder       NVARCHAR(40)   NOT NULL CONSTRAINT DF_MailMsg_Folder DEFAULT ('INBOX'),
        IsRead       BIT            NOT NULL CONSTRAINT DF_MailMsg_Read DEFAULT (0),
        IsStarred    BIT            NOT NULL CONSTRAINT DF_MailMsg_Star DEFAULT (0),
        ThreadKey    NVARCHAR(200)  NULL,
        ExternalUid  NVARCHAR(80)   NULL,
        ReceivedUtc  DATETIME2(0)   NOT NULL CONSTRAINT DF_MailMsg_Received DEFAULT (SYSUTCDATETIME()),
        CreatedAtUtc DATETIME2(0)   NOT NULL CONSTRAINT DF_MailMsg_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_MailMessages_Account FOREIGN KEY (AccountId) REFERENCES dbo.MailAccounts (AccountId)
    );
    CREATE INDEX IX_MailMessages_Inbox ON dbo.MailMessages (Direction, Folder, ReceivedUtc DESC);
    PRINT N'MailMessages created.';
END
GO

IF OBJECT_ID('dbo.MailSuppressions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MailSuppressions (
        Email        NVARCHAR(150) NOT NULL PRIMARY KEY,
        Reason       NVARCHAR(200) NULL,
        CreatedAtUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_MailSupp_Created DEFAULT (SYSUTCDATETIME())
    );
    PRINT N'MailSuppressions created.';
END
GO

-- Institution-level defaults used by the mail centre and payment settings UI.
IF NOT EXISTS (SELECT 1 FROM dbo.InstitutionSettings WHERE SettingKey = 'Mail.FromName')
    INSERT INTO dbo.InstitutionSettings (SettingKey, SettingValue) VALUES ('Mail.FromName', N'Campus Office');
IF NOT EXISTS (SELECT 1 FROM dbo.InstitutionSettings WHERE SettingKey = 'Mail.DailyLimit')
    INSERT INTO dbo.InstitutionSettings (SettingKey, SettingValue) VALUES ('Mail.DailyLimit', N'400');
IF NOT EXISTS (SELECT 1 FROM dbo.InstitutionSettings WHERE SettingKey = 'Mail.PerMinute')
    INSERT INTO dbo.InstitutionSettings (SettingKey, SettingValue) VALUES ('Mail.PerMinute', N'20');
IF NOT EXISTS (SELECT 1 FROM dbo.InstitutionSettings WHERE SettingKey = 'Mail.AutoSyncMinutes')
    INSERT INTO dbo.InstitutionSettings (SettingKey, SettingValue) VALUES ('Mail.AutoSyncMinutes', N'2');
IF NOT EXISTS (SELECT 1 FROM dbo.InstitutionSettings WHERE SettingKey = 'Payments.Provider')
    INSERT INTO dbo.InstitutionSettings (SettingKey, SettingValue) VALUES ('Payments.Provider', N'demo');
IF NOT EXISTS (SELECT 1 FROM dbo.InstitutionSettings WHERE SettingKey = 'Payments.DemoEnabled')
    INSERT INTO dbo.InstitutionSettings (SettingKey, SettingValue) VALUES ('Payments.DemoEnabled', N'1');
IF NOT EXISTS (SELECT 1 FROM dbo.InstitutionSettings WHERE SettingKey = 'Registration.Policy')
    INSERT INTO dbo.InstitutionSettings (SettingKey, SettingValue) VALUES ('Registration.Policy', N'pending');
GO

-- A ready-to-edit welcome template makes the composer useful immediately.
IF NOT EXISTS (SELECT 1 FROM dbo.MailTemplates WHERE Name = N'Welcome')
    INSERT INTO dbo.MailTemplates (Name, Subject, BodyHtml)
    VALUES (N'Welcome', N'Welcome to {{institution}}',
        N'<p>Hello {{name}},</p><p>Your campus account is ready. Sign in at {{baseurl}} to see your timetable, attendance and fees.</p><p>Regards,<br/>{{institution}}</p>');
IF NOT EXISTS (SELECT 1 FROM dbo.MailTemplates WHERE Name = N'Fee reminder')
    INSERT INTO dbo.MailTemplates (Name, Subject, BodyHtml)
    VALUES (N'Fee reminder', N'Fee payment reminder',
        N'<p>Hello {{name}},</p><p>Our records show an outstanding fee balance. Pay online from the Fees page or visit the office.</p><p>{{institution}}</p>');
GO

PRINT N'17_MailCenter.sql complete.';
GO
