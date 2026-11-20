-- =====================================================================
-- 27_RepairProfiles.sql
-- Restores administrator/seed-created student profiles that an earlier
-- over-broad heuristic reset, while keeping self-registered blank profiles.
-- Idempotent.
-- =====================================================================
USE [StudentSMS];
GO
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

UPDATE s
SET s.CourseId = e.CourseId,
    s.SessionId = e.SessionId,
    s.SemesterNumber = ISNULL(s.SemesterNumber, 1),
    s.Status = 'Active',
    s.EnrollNumber = 'REG-2025-' + RIGHT('000' + CAST(s.StudentId AS VARCHAR(4)), 4)
FROM dbo.Students s
JOIN dbo.Users u ON u.UserId = s.UserId
JOIN (
    SELECT StudentId, MIN(EnrollId) AS EnrollId
    FROM dbo.Enrollments GROUP BY StudentId
) pick ON pick.StudentId = s.StudentId
JOIN dbo.Enrollments e ON e.EnrollId = pick.EnrollId
WHERE s.CourseId IS NULL
  AND s.EnrollNumber IS NULL
  AND NOT EXISTS (
      SELECT 1 FROM dbo.AuditLogs a
      WHERE a.Action = 'Signup' AND a.EntityId = s.UserId
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.Students other
      WHERE other.EnrollNumber = 'REG-2025-' + RIGHT('000' + CAST(s.StudentId AS VARCHAR(4)), 4)
        AND other.StudentId <> s.StudentId
  );
PRINT N'Seed/administrator student profiles restored: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
GO

PRINT N'27_RepairProfiles.sql complete.';
GO
