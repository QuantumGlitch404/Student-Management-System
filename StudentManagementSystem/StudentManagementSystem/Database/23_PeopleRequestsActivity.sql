-- =====================================================================
-- 23_PeopleRequestsActivity.sql
-- People data, admin-completable profiles, email/profile requests and
-- per-user activity metadata. Idempotent: safe on existing databases.
-- =====================================================================
USE [StudentSMS];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

/* ---------- common person fields on Users ---------- */
IF COL_LENGTH('dbo.Users','DateOfBirth') IS NULL
    ALTER TABLE dbo.Users ADD DateOfBirth DATE NULL;
IF COL_LENGTH('dbo.Users','Gender') IS NULL
    ALTER TABLE dbo.Users ADD Gender NVARCHAR(24) NULL;
IF COL_LENGTH('dbo.Users','BloodGroup') IS NULL
    ALTER TABLE dbo.Users ADD BloodGroup NVARCHAR(10) NULL;
IF COL_LENGTH('dbo.Users','Nationality') IS NULL
    ALTER TABLE dbo.Users ADD Nationality NVARCHAR(60) NULL;
IF COL_LENGTH('dbo.Users','AddressLine1') IS NULL
    ALTER TABLE dbo.Users ADD AddressLine1 NVARCHAR(200) NULL;
IF COL_LENGTH('dbo.Users','AddressLine2') IS NULL
    ALTER TABLE dbo.Users ADD AddressLine2 NVARCHAR(200) NULL;
IF COL_LENGTH('dbo.Users','City') IS NULL
    ALTER TABLE dbo.Users ADD City NVARCHAR(80) NULL;
IF COL_LENGTH('dbo.Users','StateName') IS NULL
    ALTER TABLE dbo.Users ADD StateName NVARCHAR(80) NULL;
IF COL_LENGTH('dbo.Users','PostalCode') IS NULL
    ALTER TABLE dbo.Users ADD PostalCode NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.Users','Country') IS NULL
    ALTER TABLE dbo.Users ADD Country NVARCHAR(80) NULL;
IF COL_LENGTH('dbo.Users','EmergencyContactName') IS NULL
    ALTER TABLE dbo.Users ADD EmergencyContactName NVARCHAR(120) NULL;
IF COL_LENGTH('dbo.Users','EmergencyContactPhone') IS NULL
    ALTER TABLE dbo.Users ADD EmergencyContactPhone VARCHAR(20) NULL;
IF COL_LENGTH('dbo.Users','IsDeleted') IS NULL
    ALTER TABLE dbo.Users ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT (0);
IF COL_LENGTH('dbo.Users','DeletedUtc') IS NULL
    ALTER TABLE dbo.Users ADD DeletedUtc DATETIME2(0) NULL;
GO

/* ---------- Students: blank state allowed ---------- */
IF COL_LENGTH('dbo.Students','SectionCode') IS NULL
    ALTER TABLE dbo.Students ADD SectionCode VARCHAR(20) NULL;
IF COL_LENGTH('dbo.Students','Status') IS NULL
    ALTER TABLE dbo.Students ADD Status VARCHAR(30) NULL;
IF COL_LENGTH('dbo.Students','MentorUserId') IS NULL
    ALTER TABLE dbo.Students ADD MentorUserId INT NULL;
IF COL_LENGTH('dbo.Students','FeePlanOverride') IS NULL
    ALTER TABLE dbo.Students ADD FeePlanOverride NVARCHAR(120) NULL;
IF COL_LENGTH('dbo.Students','Concessions') IS NULL
    ALTER TABLE dbo.Students ADD Concessions NVARCHAR(300) NULL;
IF COL_LENGTH('dbo.Students','Skills') IS NULL
    ALTER TABLE dbo.Students ADD Skills NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.Students','Achievements') IS NULL
    ALTER TABLE dbo.Students ADD Achievements NVARCHAR(1000) NULL;
IF COL_LENGTH('dbo.Students','MedicalNotes') IS NULL
    ALTER TABLE dbo.Students ADD MedicalNotes NVARCHAR(1000) NULL;
GO

UPDATE dbo.Students SET Status = 'Profile not set up'
WHERE Status IS NULL OR Status = '';
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Students_Status')
    ALTER TABLE dbo.Students WITH NOCHECK ADD CONSTRAINT CK_Students_Status
        CHECK (Status IN ('Profile not set up','Active','Inactive','Alumni','Suspended'));
GO

/* EnrollNumber must allow NULL for "profile not set up" rows. Drop the
   inline UNIQUE constraint and replace it with a filtered unique index. */
IF EXISTS (SELECT 1 FROM sys.key_constraints kc
           JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
           JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
           WHERE kc.parent_object_id = OBJECT_ID('dbo.Students') AND c.name = 'EnrollNumber' AND kc.type = 'UQ')
BEGIN
    DECLARE @uq sysname;
    SELECT TOP 1 @uq = kc.name FROM sys.key_constraints kc
    JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE kc.parent_object_id = OBJECT_ID('dbo.Students') AND c.name = 'EnrollNumber' AND kc.type = 'UQ';
    IF @uq IS NOT NULL EXEC('ALTER TABLE dbo.Students DROP CONSTRAINT [' + @uq + ']');
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Students') AND name = 'EnrollNumber' AND is_nullable = 0)
    ALTER TABLE dbo.Students ALTER COLUMN EnrollNumber VARCHAR(24) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Students') AND name = 'CourseId' AND is_nullable = 0)
    ALTER TABLE dbo.Students ALTER COLUMN CourseId INT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Students') AND name = 'SessionId' AND is_nullable = 0)
    ALTER TABLE dbo.Students ALTER COLUMN SessionId INT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Students') AND name = 'SemesterNumber' AND is_nullable = 0)
    ALTER TABLE dbo.Students ALTER COLUMN SemesterNumber TINYINT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Students_EnrollNumber' AND object_id = OBJECT_ID('dbo.Students'))
    CREATE UNIQUE INDEX UX_Students_EnrollNumber ON dbo.Students (EnrollNumber) WHERE EnrollNumber IS NOT NULL;
GO

/* ---------- Faculty: blank state allowed + professional fields ---------- */
IF COL_LENGTH('dbo.Faculty','EmployeeId') IS NULL
    ALTER TABLE dbo.Faculty ADD EmployeeId VARCHAR(24) NULL;
IF COL_LENGTH('dbo.Faculty','Qualifications') IS NULL
    ALTER TABLE dbo.Faculty ADD Qualifications NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.Faculty','ExperienceYears') IS NULL
    ALTER TABLE dbo.Faculty ADD ExperienceYears INT NULL;
IF COL_LENGTH('dbo.Faculty','Specialisations') IS NULL
    ALTER TABLE dbo.Faculty ADD Specialisations NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.Faculty','OfficeHours') IS NULL
    ALTER TABLE dbo.Faculty ADD OfficeHours NVARCHAR(200) NULL;
IF COL_LENGTH('dbo.Faculty','Status') IS NULL
    ALTER TABLE dbo.Faculty ADD Status VARCHAR(30) NULL;
GO

UPDATE dbo.Faculty SET Status = CASE WHEN DeptCode IS NULL OR DeptCode = '' THEN 'Profile not set up' ELSE 'Active' END
WHERE Status IS NULL OR Status = '';
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Faculty') AND name = 'DeptCode' AND is_nullable = 0)
    ALTER TABLE dbo.Faculty ALTER COLUMN DeptCode NVARCHAR(60) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Faculty_EmployeeId' AND object_id = OBJECT_ID('dbo.Faculty'))
    CREATE UNIQUE INDEX UX_Faculty_EmployeeId ON dbo.Faculty (EmployeeId) WHERE EmployeeId IS NOT NULL;
GO

/* ---------- Audit metadata: category, target person, before/after ---------- */
IF COL_LENGTH('dbo.AuditLogs','Category') IS NULL
    ALTER TABLE dbo.AuditLogs ADD Category VARCHAR(30) NULL;
IF COL_LENGTH('dbo.AuditLogs','TargetUserId') IS NULL
    ALTER TABLE dbo.AuditLogs ADD TargetUserId INT NULL;
IF COL_LENGTH('dbo.AuditLogs','OldValue') IS NULL
    ALTER TABLE dbo.AuditLogs ADD OldValue NVARCHAR(400) NULL;
IF COL_LENGTH('dbo.AuditLogs','NewValue') IS NULL
    ALTER TABLE dbo.AuditLogs ADD NewValue NVARCHAR(400) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_Target' AND object_id = OBJECT_ID('dbo.AuditLogs'))
    CREATE INDEX IX_AuditLogs_Target ON dbo.AuditLogs (TargetUserId, CreatedAtUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_User' AND object_id = OBJECT_ID('dbo.AuditLogs'))
    CREATE INDEX IX_AuditLogs_User ON dbo.AuditLogs (UserId, CreatedAtUtc DESC);
GO

/* ---------- Email change / profile update requests ---------- */
IF OBJECT_ID('dbo.ProfileRequests','U') IS NULL
BEGIN
    CREATE TABLE dbo.ProfileRequests (
        RequestId       INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId          INT            NOT NULL,
        Kind            VARCHAR(20)    NOT NULL,          -- 'email' | 'profile'
        FieldName       VARCHAR(60)    NULL,
        OldValue        NVARCHAR(300)  NULL,
        NewValue        NVARCHAR(300)  NULL,
        Stage           VARCHAR(20)    NOT NULL CONSTRAINT DF_ProfileReq_Stage DEFAULT ('verify'),
        Status          VARCHAR(20)    NOT NULL CONSTRAINT DF_ProfileReq_Status DEFAULT ('pending'),
        VerifyCodeHash  VARCHAR(128)   NULL,
        VerifyExpiresUtc DATETIME2(0)  NULL,
        VerifiedUtc     DATETIME2(0)   NULL,
        Reason          NVARCHAR(300)  NULL,
        RequestedUtc    DATETIME2(0)   NOT NULL CONSTRAINT DF_ProfileReq_Requested DEFAULT (SYSUTCDATETIME()),
        DecidedBy       INT            NULL,
        DecidedUtc      DATETIME2(0)   NULL,
        ExpiresUtc      DATETIME2(0)   NULL,
        CONSTRAINT FK_ProfileRequests_User FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE CASCADE,
        CONSTRAINT CK_ProfileRequests_Kind CHECK (Kind IN ('email','profile')),
        CONSTRAINT CK_ProfileRequests_Stage CHECK (Stage IN ('verify','pending','done')),
        CONSTRAINT CK_ProfileRequests_Status CHECK (Status IN ('pending','approved','rejected','cancelled','expired'))
    );
    CREATE INDEX IX_ProfileRequests_Status ON dbo.ProfileRequests (Status, Kind, RequestedUtc DESC);
    CREATE INDEX IX_ProfileRequests_User ON dbo.ProfileRequests (UserId, Status);
    PRINT N'ProfileRequests created.';
END
GO

/* one open email-change request per user */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ProfileRequests_OpenEmail' AND object_id = OBJECT_ID('dbo.ProfileRequests'))
    CREATE UNIQUE INDEX UX_ProfileRequests_OpenEmail ON dbo.ProfileRequests (UserId)
        WHERE Kind = 'email' AND Status = 'pending';
GO

/* ---------- Requests raised to the administrator ---------- */
IF OBJECT_ID('dbo.AdminRequests','U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminRequests (
        AdminRequestId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId         INT            NOT NULL,
        Kind           VARCHAR(30)    NOT NULL,      -- profile-setup | general | help
        Message        NVARCHAR(1000) NULL,
        Status         VARCHAR(20)    NOT NULL CONSTRAINT DF_AdminReq_Status DEFAULT ('open'),
        CreatedUtc     DATETIME2(0)   NOT NULL CONSTRAINT DF_AdminReq_Created DEFAULT (SYSUTCDATETIME()),
        ResolvedBy     INT            NULL,
        ResolvedUtc    DATETIME2(0)   NULL,
        CONSTRAINT FK_AdminRequests_User FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE CASCADE,
        CONSTRAINT CK_AdminRequests_Status CHECK (Status IN ('open','resolved','dismissed'))
    );
    CREATE INDEX IX_AdminRequests_Status ON dbo.AdminRequests (Status, CreatedUtc DESC);
    PRINT N'AdminRequests created.';
END
GO

/* ---------- Data integrity repair ---------- */
/* 1) Role holders without a profile row get a blank profile. */
INSERT INTO dbo.Students (UserId, EnrollNumber, CourseId, SessionId, SemesterNumber, Status)
SELECT u.UserId, NULL, NULL, NULL, NULL, 'Profile not set up'
FROM dbo.Users u
JOIN dbo.UserRoles ur ON ur.UserId = u.UserId
JOIN dbo.Roles r ON r.RoleId = ur.RoleId
WHERE r.RoleName = 'Student'
  AND NOT EXISTS (SELECT 1 FROM dbo.Students s WHERE s.UserId = u.UserId);
PRINT N'Blank student profiles created: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

INSERT INTO dbo.Faculty (UserId, DeptCode, Designation, Status)
SELECT u.UserId, NULL, NULL, 'Profile not set up'
FROM dbo.Users u
JOIN dbo.UserRoles ur ON ur.UserId = u.UserId
JOIN dbo.Roles r ON r.RoleId = ur.RoleId
WHERE r.RoleName = 'Faculty'
  AND NOT EXISTS (SELECT 1 FROM dbo.Faculty f WHERE f.UserId = u.UserId);
PRINT N'Blank faculty profiles created: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

/* 2) Self-registered rows still carrying the old auto-default course,
      with no administrator edit audit row, are reset to the blank state. */
DECLARE @reset INT = 0;
;WITH suspect AS
(
    SELECT s.StudentId
    FROM dbo.Students s
    WHERE s.EnrollNumber LIKE 'REG-%'
      AND s.CourseId IS NOT NULL
      AND s.CourseId = (SELECT MIN(CourseId) FROM dbo.Courses)
      AND EXISTS (SELECT 1 FROM dbo.AuditLogs a2 WHERE a2.Action = 'Signup' AND a2.EntityId = s.UserId)
      AND NOT EXISTS (SELECT 1 FROM dbo.AuditLogs a
                      WHERE a.EntityTable = 'Students' AND a.EntityId = s.StudentId
                        AND a.Action IN ('StudentUpdated','StudentProfileCompleted'))
)
UPDATE s
SET CourseId = NULL, SessionId = NULL, SemesterNumber = NULL,
    EnrollNumber = NULL, Status = 'Profile not set up'
FROM dbo.Students s JOIN suspect x ON x.StudentId = s.StudentId;
SET @reset = @@ROWCOUNT;
PRINT N'Self-registration default profiles reset to blank: ' + CAST(@reset AS VARCHAR(10));
GO

PRINT N'23_PeopleRequestsActivity.sql complete.';
GO
