SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

USE [StudentSMS];
GO

/* Approved self-registered students must be visible in the student register.
   Backfill a minimal profile for any approved Student account that has none.
   Course/session are defaults for the office to correct; enrollment is unique. */
;WITH Missing AS
(
    SELECT u.UserId,
           ROW_NUMBER() OVER (ORDER BY u.UserId) AS rn
    FROM dbo.Users u
    JOIN dbo.UserRoles ur ON ur.UserId = u.UserId
    JOIN dbo.Roles r ON r.RoleId = ur.RoleId
    WHERE r.RoleName = 'Student'
      AND ISNULL(u.ApprovalStatus, 'approved') = 'approved'
      AND NOT EXISTS (SELECT 1 FROM dbo.Students s WHERE s.UserId = u.UserId)
)
INSERT INTO dbo.Students (UserId, EnrollNumber, CourseId, SessionId, SemesterNumber, AdmissionDate)
SELECT m.UserId,
       CONCAT('REG-', YEAR(SYSUTCDATETIME()), '-', RIGHT('0000' + CAST(m.UserId AS varchar(10)), 4)),
       (SELECT TOP 1 CourseId FROM dbo.Courses ORDER BY CourseId),
       (SELECT TOP 1 SessionId FROM dbo.AcademicSessions ORDER BY IsActive DESC, SessionId DESC),
       1,
       CAST(SYSUTCDATETIME() AS date)
FROM Missing m
WHERE EXISTS (SELECT 1 FROM dbo.Courses)
  AND EXISTS (SELECT 1 FROM dbo.AcademicSessions)
  AND NOT EXISTS (
      SELECT 1 FROM dbo.Students s
      WHERE s.EnrollNumber = CONCAT('REG-', YEAR(SYSUTCDATETIME()), '-', RIGHT('0000' + CAST(m.UserId AS varchar(10)), 4))
  );

INSERT INTO dbo.Enrollments (StudentId, CourseId, SessionId)
SELECT s.StudentId, s.CourseId, s.SessionId
FROM dbo.Students s
JOIN dbo.Users u ON u.UserId = s.UserId
JOIN dbo.UserRoles ur ON ur.UserId = u.UserId
JOIN dbo.Roles r ON r.RoleId = ur.RoleId
WHERE r.RoleName = 'Student'
  AND s.EnrollNumber LIKE 'REG-%'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.Enrollments e
      WHERE e.StudentId = s.StudentId AND e.CourseId = s.CourseId AND e.SessionId = s.SessionId
  );
GO
