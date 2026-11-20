-- ==============================================================
-- 18_RegistrationApproval.sql
-- Adds an explicit approval state to self-registered accounts so the
-- "pending" Registration.Policy can require an administrator decision
-- even after the email address has been verified.
-- Idempotent: safe to run on existing databases.
-- ==============================================================
USE [StudentSMS];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.Users', 'ApprovalStatus') IS NULL
BEGIN
    ALTER TABLE dbo.Users
        ADD ApprovalStatus VARCHAR(20) NOT NULL
            CONSTRAINT DF_Users_ApprovalStatus DEFAULT ('approved');
END
GO

UPDATE dbo.Users SET ApprovalStatus = 'approved'
WHERE ApprovalStatus IS NULL OR ApprovalStatus = '';
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Users_ApprovalStatus')
BEGIN
    ALTER TABLE dbo.Users WITH CHECK
        ADD CONSTRAINT CK_Users_ApprovalStatus
            CHECK (ApprovalStatus IN ('approved', 'pending', 'rejected'));
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_ApprovalStatus' AND object_id = OBJECT_ID('dbo.Users'))
BEGIN
    CREATE INDEX IX_Users_ApprovalStatus ON dbo.Users (ApprovalStatus, EmailVerified, IsActive);
END
GO

PRINT N'Registration approval state is ready.';
