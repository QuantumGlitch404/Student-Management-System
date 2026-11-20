-- =====================================================================
-- 28_PeopleExtras.sql
-- Personal documents, verified-document workflow and force sign-out stamp.
-- Idempotent.
-- =====================================================================
USE [StudentSMS];
GO
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.Users', 'AuthStamp') IS NULL
    ALTER TABLE dbo.Users ADD AuthStamp UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_Users_AuthStamp DEFAULT NEWID();
GO

IF OBJECT_ID('dbo.UserDocuments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserDocuments (
        DocId          INT IDENTITY(1,1) PRIMARY KEY,
        UserId         INT NOT NULL,
        Kind           NVARCHAR(60) NOT NULL,
        FileName       NVARCHAR(260) NOT NULL,
        ContentType    NVARCHAR(120) NULL,
        FileSize       INT NULL,
        StoredName     NVARCHAR(120) NOT NULL,
        Note           NVARCHAR(400) NULL,
        UploadedUtc    DATETIME2 NOT NULL CONSTRAINT DF_UserDocuments_Uploaded DEFAULT SYSUTCDATETIME(),
        UploadedBy     INT NULL,
        VerifiedUtc    DATETIME2 NULL,
        VerifiedBy     INT NULL,
        IsDeleted      BIT NOT NULL CONSTRAINT DF_UserDocuments_Deleted DEFAULT 0,
        CONSTRAINT FK_UserDocuments_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
    );
    CREATE INDEX IX_UserDocuments_User ON dbo.UserDocuments(UserId, IsDeleted);
END
GO

PRINT N'28_PeopleExtras.sql complete.';
GO
