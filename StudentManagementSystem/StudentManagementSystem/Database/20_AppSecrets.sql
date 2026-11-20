/* 20_AppSecrets.sql - encrypted integration credentials (idempotent).
   Values are DPAPI (LocalMachine) protected by the app or tools\provision-secrets.ps1.
   Web.config remains the fallback for a fresh unprovisioned install. */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID('dbo.AppSecrets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppSecrets
    (
        SecretKey    NVARCHAR(100) NOT NULL CONSTRAINT PK_AppSecrets PRIMARY KEY,
        SecretValue  NVARCHAR(MAX) NULL,
        IsEncrypted  BIT NOT NULL CONSTRAINT DF_AppSecrets_IsEncrypted DEFAULT (1),
        Description  NVARCHAR(200) NULL,
        UpdatedBy    INT NULL,
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AppSecrets_UpdatedAtUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO
