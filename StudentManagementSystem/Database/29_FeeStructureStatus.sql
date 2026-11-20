SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_FeeStructures_Status')
    ALTER TABLE dbo.FeeStructures DROP CONSTRAINT CK_FeeStructures_Status;
GO

ALTER TABLE dbo.FeeStructures WITH CHECK ADD CONSTRAINT CK_FeeStructures_Status
    CHECK (Status IN ('draft', 'published', 'active', 'archived'));
GO

UPDATE dbo.FeeStructures SET Status = 'published' WHERE Status = 'active';
UPDATE dbo.FeeStructures SET Status = 'draft' WHERE Status NOT IN ('draft', 'published', 'archived');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE ScriptName = '29_FeeStructureStatus.sql')
    INSERT INTO dbo.SchemaMigrations (ScriptName) VALUES ('29_FeeStructureStatus.sql');
GO
