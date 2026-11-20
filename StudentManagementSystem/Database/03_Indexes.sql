-- ==============================================================
-- 03_Indexes.sql - performance indexes
-- ==============================================================
USE [StudentSMS];
GO

CREATE INDEX IX_Users_EmailVerified ON dbo.Users (EmailVerified) INCLUDE (FullName, IsActive);
CREATE INDEX IX_UserRoles_RoleId    ON dbo.UserRoles (RoleId);
CREATE INDEX IX_Students_Course     ON dbo.Students (CourseId, SessionId);
CREATE INDEX IX_Enroll_Student      ON dbo.Enrollments (StudentId);
CREATE INDEX IX_Enroll_Course       ON dbo.Enrollments (CourseId, SessionId) INCLUDE (EnrollId);
CREATE INDEX IX_FA_Faculty          ON dbo.FacultyAssignments (FacultyId, SessionId);
CREATE INDEX IX_AttnSession_Date    ON dbo.AttendanceSessions (SessionDate);
CREATE INDEX IX_AttnSession_Course  ON dbo.AttendanceSessions (CourseId, SubjectId);
CREATE INDEX IX_Marks_Enroll        ON dbo.Marks (EnrollId);
CREATE INDEX IX_Results_Enroll      ON dbo.Results (EnrollId);
CREATE INDEX IX_NRec_UserId_Read    ON dbo.NotificationRecipients (UserId, IsRead);
CREATE INDEX IX_NRec_Notif          ON dbo.NotificationRecipients (NotificationId);
CREATE INDEX IX_Outbox_State        ON dbo.NotificationOutbox (Status, NextAttemptAtUtc);
CREATE INDEX IX_Audit_Action        ON dbo.AuditLogs (Action, CreatedAtUtc);
CREATE INDEX IX_Audit_User          ON dbo.AuditLogs (UserId, CreatedAtUtc);
CREATE INDEX IX_Compl_Status        ON dbo.Complaints (Status, CreatedAtUtc);
CREATE INDEX IX_Compl_By            ON dbo.Complaints (RaisedBy);
CREATE INDEX IX_Pay_Users           ON dbo.Payments (UserId, CreatedAtUtc);
CREATE INDEX IX_Pay_Status          ON dbo.Payments (PayStatus, CapturedAtUtc);
CREATE INDEX IX_PayEvents_Pay       ON dbo.PaymentEvents (PaymentId);
CREATE INDEX IX_Exams_Course        ON dbo.Exams (CourseId, SubjectId, IsPublished);
CREATE INDEX IX_Chat_Users          ON dbo.ChatLogs (UserId, CreatedAtUtc);
CREATE INDEX IX_Faq_Scope           ON dbo.FrequentlyAsked (RoleScope, IsActive, SortOrder);
CREATE INDEX IX_Otp_User_Purpose    ON dbo.OtpCodes (UserId, Purpose, IsUsed);
CREATE INDEX IX_Thrd_Compl          ON dbo.ComplaintThreads (ComplaintId, CreatedAtUtc);
GO
PRINT N'Indexes created.';
