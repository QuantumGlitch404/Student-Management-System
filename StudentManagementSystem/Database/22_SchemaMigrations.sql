SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID('dbo.SchemaMigrations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaMigrations
    (
        MigrationId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SchemaMigrations PRIMARY KEY,
        ScriptName   NVARCHAR(200) NOT NULL,
        AppliedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_SchemaMigrations_Applied DEFAULT SYSUTCDATETIME()
    );
    CREATE UNIQUE INDEX UX_SchemaMigrations_ScriptName ON dbo.SchemaMigrations(ScriptName);
END
GO

;WITH scripts AS
(
    SELECT ScriptName FROM (VALUES
        ('01_CreateDatabase.sql'), ('02_Tables.sql'), ('03_Indexes.sql'),
        ('04_Seed.sql'), ('05_SampleData.sql'), ('06_StoredProcedures.sql'),
        ('07_BackupRestore.sql'), ('08_SmokeTests.sql'), ('09_Preferences.sql'),
        ('10_Premium_AI.sql'), ('11_Module_Upgrade.sql'), ('12_CampusPlatform.sql'),
        ('13_OnlineTests.sql'), ('14_CampusModules.sql'), ('15_Proctoring.sql'),
        ('16_OnlineTestsSeed.sql'), ('17_MailCenter.sql'), ('18_RegistrationApproval.sql'),
        ('19_PaymentsUniqueFix.sql'), ('20_AppSecrets.sql'), ('21_BackfillStudentProfiles.sql'),
        ('22_SchemaMigrations.sql'), ('23_PeopleRequestsActivity.sql'),
        ('24_FeesTimetable.sql'), ('25_HelpSessionsMail.sql'), ('26_HelpSeed.sql'), ('27_RepairProfiles.sql'), ('28_PeopleExtras.sql'), ('29_FeeStructureStatus.sql'), ('30_HelpFaq.sql'))
    ) AS v(ScriptName)
)
INSERT INTO dbo.SchemaMigrations (ScriptName)
SELECT s.ScriptName
FROM scripts s
WHERE NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations m WHERE m.ScriptName = s.ScriptName);
GO



