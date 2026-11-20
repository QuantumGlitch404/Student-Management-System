/* =====================================================================
   11_Module_Upgrade.sql
   Idempotent module expansion: per-user notification archive, expanded
   complaint workflow statuses. Safe to run repeatedly and on upgrades.
   ===================================================================== */
USE [StudentSMS];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.NotificationRecipients', 'IsArchived') IS NULL
BEGIN
    ALTER TABLE dbo.NotificationRecipients ADD IsArchived BIT NOT NULL CONSTRAINT DF_NRec_Archived DEFAULT (0);
    PRINT N'NotificationRecipients.IsArchived added.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NRec_User_Archive' AND object_id = OBJECT_ID('dbo.NotificationRecipients'))
    CREATE INDEX IX_NRec_User_Archive ON dbo.NotificationRecipients (UserId, IsArchived, IsRead) INCLUDE (NotificationId, ReadAtUtc);
GO

/* Expand the complaint status constraint to the full workflow. */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Compl_Status' AND parent_object_id = OBJECT_ID('dbo.Complaints'))
BEGIN
    ALTER TABLE dbo.Complaints DROP CONSTRAINT CK_Compl_Status;
    PRINT N'Old complaint status constraint dropped.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Compl_Status' AND parent_object_id = OBJECT_ID('dbo.Complaints'))
    ALTER TABLE dbo.Complaints ADD CONSTRAINT CK_Compl_Status
        CHECK (Status IN ('open', 'assigned', 'in_progress', 'waiting', 'resolved', 'closed', 'reopened'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Compl_Priority' AND object_id = OBJECT_ID('dbo.Complaints'))
    CREATE INDEX IX_Compl_Priority ON dbo.Complaints (Status, Priority, CreatedAtUtc DESC);
GO

PRINT N'11_Module_Upgrade.sql complete.';
GO
