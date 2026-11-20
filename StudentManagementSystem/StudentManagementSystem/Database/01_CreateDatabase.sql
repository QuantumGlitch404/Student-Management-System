-- ==============================================================
-- 01_CreateDatabase.sql
-- Creates the StudentSMS database (SQL Server 2019+, incl. LocalDB).
-- Run with: sqlcmd -S "(localdb)\MSSQLLocalDB" -E -i 01_CreateDatabase.sql
-- ==============================================================
IF DB_ID(N'StudentSMS') IS NULL
BEGIN
    CREATE DATABASE [StudentSMS];
END
GO
ALTER DATABASE [StudentSMS] SET AUTO_SHRINK OFF;
ALTER DATABASE [StudentSMS] SET RECOVERY SIMPLE;  -- dev-friendly; see 07_BackupRestore.sql for production guidance
GO
USE [StudentSMS];
GO
PRINT N'Database StudentSMS is ready.';
