-- ==============================================================
-- 99_TearDown.sql - removes the whole database (DEV ONLY)
-- ==============================================================
USE [master];
GO
IF DB_ID(N'StudentSMS') IS NOT NULL
BEGIN
    ALTER DATABASE [StudentSMS] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [StudentSMS];
END
GO
PRINT N'StudentSMS database dropped (dev teardown).';
