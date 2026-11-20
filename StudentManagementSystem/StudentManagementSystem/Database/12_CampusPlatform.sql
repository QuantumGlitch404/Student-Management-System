-- =====================================================================
-- 12_CampusPlatform.sql
-- Ticker, reminders and IANA timezone foundation. Idempotent.
-- =====================================================================
USE [StudentSMS];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.UserPreferences', 'TimezoneId') IS NULL
BEGIN
    ALTER TABLE dbo.UserPreferences ADD TimezoneId VARCHAR(64) NULL;
    PRINT N'UserPreferences.TimezoneId added.';
END
GO

IF COL_LENGTH('dbo.UserPreferences', 'ClockFormat') IS NULL
BEGIN
    ALTER TABLE dbo.UserPreferences ADD ClockFormat VARCHAR(2) NOT NULL CONSTRAINT DF_UP_ClockFormat DEFAULT ('24');
    PRINT N'UserPreferences.ClockFormat added.';
END
GO

IF COL_LENGTH('dbo.UserPreferences', 'TickerEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.UserPreferences ADD TickerEnabled BIT NOT NULL CONSTRAINT DF_UP_Ticker DEFAULT (1);
    PRINT N'UserPreferences.TickerEnabled added.';
END
GO

IF COL_LENGTH('dbo.UserPreferences', 'TickerSpeed') IS NULL
BEGIN
    ALTER TABLE dbo.UserPreferences ADD TickerSpeed VARCHAR(8) NOT NULL CONSTRAINT DF_UP_TickerSpeed DEFAULT ('slow');
    PRINT N'UserPreferences.TickerSpeed added.';
END
GO

IF COL_LENGTH('dbo.UserPreferences', 'ReminderLeadMinutes') IS NULL
BEGIN
    ALTER TABLE dbo.UserPreferences ADD ReminderLeadMinutes INT NOT NULL CONSTRAINT DF_UP_ReminderLead DEFAULT (15);
    PRINT N'UserPreferences.ReminderLeadMinutes added.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_UP_ClockFormat' AND parent_object_id = OBJECT_ID('dbo.UserPreferences'))
    ALTER TABLE dbo.UserPreferences ADD CONSTRAINT CK_UP_ClockFormat CHECK (ClockFormat IN ('12', '24'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_UP_TickerSpeed' AND parent_object_id = OBJECT_ID('dbo.UserPreferences'))
    ALTER TABLE dbo.UserPreferences ADD CONSTRAINT CK_UP_TickerSpeed CHECK (TickerSpeed IN ('slow', 'normal', 'fast'));
GO

IF COL_LENGTH('dbo.NotificationRecipients', 'SnoozedUntilUtc') IS NULL
BEGIN
    ALTER TABLE dbo.NotificationRecipients ADD SnoozedUntilUtc DATETIME2(0) NULL;
    PRINT N'NotificationRecipients.SnoozedUntilUtc added.';
END
GO

IF COL_LENGTH('dbo.Notifications', 'IsTicker') IS NULL
BEGIN
    ALTER TABLE dbo.Notifications ADD IsTicker BIT NOT NULL CONSTRAINT DF_Notif_IsTicker DEFAULT (1);
    PRINT N'Notifications.IsTicker added.';
END
GO

IF COL_LENGTH('dbo.Notifications', 'SourceKey') IS NULL
BEGIN
    ALTER TABLE dbo.Notifications ADD SourceKey VARCHAR(100) NULL;
    PRINT N'Notifications.SourceKey added.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Notif_SourceKey' AND object_id = OBJECT_ID('dbo.Notifications'))
    CREATE UNIQUE INDEX UX_Notif_SourceKey ON dbo.Notifications (SourceKey) WHERE SourceKey IS NOT NULL;
GO

IF OBJECT_ID('dbo.TickerDismissals', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TickerDismissals (
        UserId          INT           NOT NULL,
        NotificationId  INT           NOT NULL,
        DismissedAtUtc  DATETIME2(0)  NOT NULL CONSTRAINT DF_TickDismiss_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_TickerDismissals PRIMARY KEY (UserId, NotificationId),
        CONSTRAINT FK_TickDismiss_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE CASCADE,
        CONSTRAINT FK_TickDismiss_Notif FOREIGN KEY (NotificationId) REFERENCES dbo.Notifications (NotificationId) ON DELETE CASCADE
    );
    PRINT N'TickerDismissals created.';
END
GO

IF OBJECT_ID('dbo.Reminders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Reminders (
        ReminderId      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId          INT           NOT NULL,
        Title           NVARCHAR(160) NOT NULL,
        Body            NVARCHAR(1000) NULL,
        ActionUrl       VARCHAR(300)  NULL,
        DueAtUtc        DATETIME2(0)  NOT NULL,
        Status          VARCHAR(12)   NOT NULL CONSTRAINT DF_Reminder_Status DEFAULT ('pending'),
        RepeatRule      VARCHAR(20)   NOT NULL CONSTRAINT DF_Reminder_Repeat DEFAULT ('none'),
        UniqueKey       VARCHAR(100)  NOT NULL,
        CreatedAtUtc    DATETIME2(0)  NOT NULL CONSTRAINT DF_Reminder_Created DEFAULT (SYSUTCDATETIME()),
        SentAtUtc       DATETIME2(0)  NULL,
        SnoozedUntilUtc DATETIME2(0)  NULL,
        CONSTRAINT FK_Reminders_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE CASCADE,
        CONSTRAINT CK_Reminder_Status CHECK (Status IN ('pending', 'sent', 'cancelled')),
        CONSTRAINT CK_Reminder_Repeat CHECK (RepeatRule IN ('none', 'daily', 'weekly', 'monthly')),
        CONSTRAINT UQ_Reminder_User_Key UNIQUE (UserId, UniqueKey)
    );
    PRINT N'Reminders created.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reminders_Due' AND object_id = OBJECT_ID('dbo.Reminders'))
    CREATE INDEX IX_Reminders_Due ON dbo.Reminders (Status, DueAtUtc) INCLUDE (UserId, Title, Body, ActionUrl, SnoozedUntilUtc);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NRec_User_Ticker' AND object_id = OBJECT_ID('dbo.NotificationRecipients'))
    CREATE INDEX IX_NRec_User_Ticker ON dbo.NotificationRecipients (UserId, IsRead, IsArchived, SnoozedUntilUtc, NotificationId DESC);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.AppSettings WHERE SettingKey = 'InstitutionTimezone')
    INSERT dbo.AppSettings (SettingKey, SettingValue, Description)
    VALUES ('InstitutionTimezone', 'Asia/Kolkata', N'IANA timezone used for institution-wide timestamps, exports and reminders.');
GO

PRINT N'12_CampusPlatform.sql complete.';
GO

