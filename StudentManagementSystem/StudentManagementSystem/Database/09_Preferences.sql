-- ==============================================================
-- 09_Preferences.sql - per-user appearance, accessibility and
-- notification preferences. Idempotent: safe on fresh installs and
-- on an existing installation (CREATE only when missing).
-- ==============================================================
USE [StudentSMS];
GO

IF OBJECT_ID('dbo.UserPreferences', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserPreferences (
        UserId              INT           NOT NULL PRIMARY KEY,
        Theme               VARCHAR(10)   NOT NULL CONSTRAINT DF_UP_Theme    DEFAULT ('light'),
        FontScale           TINYINT       NOT NULL CONSTRAINT DF_UP_Font     DEFAULT (100),
        Motion              VARCHAR(10)   NOT NULL CONSTRAINT DF_UP_Motion   DEFAULT ('full'),
        HighContrast        BIT           NOT NULL CONSTRAINT DF_UP_Contrast DEFAULT (0),
        SidebarCollapsed    BIT           NOT NULL CONSTRAINT DF_UP_Sidebar  DEFAULT (0),
        DateFormat          VARCHAR(20)   NOT NULL CONSTRAINT DF_UP_Date     DEFAULT ('yyyy-MM-dd'),
        TablePageSize       SMALLINT      NOT NULL CONSTRAINT DF_UP_PageSize DEFAULT (20),
        ReportFormat        VARCHAR(10)   NOT NULL CONSTRAINT DF_UP_Report   DEFAULT ('pdf'),
        AcademicYear        VARCHAR(20)   NULL,
        Semester            VARCHAR(20)   NULL,
        DefaultDashboard    VARCHAR(20)   NULL,
        DisabledNotifTypes  VARCHAR(120)  NOT NULL CONSTRAINT DF_UP_NotifOff DEFAULT (''),
        UpdatedAtUtc        DATETIME2(0)  NOT NULL CONSTRAINT DF_UP_Updated  DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_UserPreferences_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE CASCADE,
        CONSTRAINT CK_UP_Theme  CHECK (Theme IN ('light', 'dark', 'system')),
        CONSTRAINT CK_UP_Motion CHECK (Motion IN ('full', 'reduced')),
        CONSTRAINT CK_UP_Font   CHECK (FontScale BETWEEN 85 AND 140),
        CONSTRAINT CK_UP_Page   CHECK (TablePageSize BETWEEN 10 AND 100),
        CONSTRAINT CK_UP_Report CHECK (ReportFormat IN ('pdf', 'xlsx', 'csv'))
    );
    PRINT N'UserPreferences table created.';
END
ELSE
    PRINT N'UserPreferences already exists.';
GO

-- Optional link to the record a notification is about (attendance, complaint,
-- payment...). ActionUrl stays the navigation target; this is for reporting.
IF COL_LENGTH('dbo.Notifications', 'RelatedEntityId') IS NULL
BEGIN
    ALTER TABLE dbo.Notifications ADD RelatedEntityId INT NULL;
    PRINT N'Notifications.RelatedEntityId added.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserPreferences_Theme' AND object_id = OBJECT_ID('dbo.UserPreferences'))
    CREATE INDEX IX_UserPreferences_Theme ON dbo.UserPreferences (Theme);
GO

-- Paged inbox reads: newest-first per user. The existing (UserId, IsRead)
-- index covers the unread badge; this one covers the ORDER BY.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NRec_User_Read_Notif' AND object_id = OBJECT_ID('dbo.NotificationRecipients'))
    CREATE INDEX IX_NRec_User_Read_Notif
        ON dbo.NotificationRecipients (UserId, IsRead, NotificationId DESC);
GO

PRINT N'09_Preferences.sql complete.';
GO
