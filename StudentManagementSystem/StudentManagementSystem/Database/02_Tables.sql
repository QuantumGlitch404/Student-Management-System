-- ==============================================================
-- 02_Tables.sql - Student Information and Academic Management System
-- SQL Server (T-SQL). Run after 01_CreateDatabase.sql.
-- Timestamps are UTC (SYSUTCDATETIME()) for consistency.
-- ==============================================================
USE [StudentSMS];
GO
SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
GO

/* ---------- IDENTITY & SECURITY ---------- */

CREATE TABLE dbo.Roles (
    RoleId      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    RoleName    NVARCHAR(20)  NOT NULL UNIQUE,
    Description NVARCHAR(150) NULL
);
GO

CREATE TABLE dbo.Users (
    UserId           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    FullName         NVARCHAR(150)  NOT NULL,
    Email            NVARCHAR(150)  NOT NULL UNIQUE,
    Phone            VARCHAR(15)    NULL,
    PasswordHash     VARCHAR(180)   NOT NULL,             -- "iterations.saltB64.hashB64" (PBKDF2)
    EmailVerified    BIT            NOT NULL CONSTRAINT DF_Users_EmailVerified DEFAULT (0),
    PhoneVerified    BIT            NOT NULL CONSTRAINT DF_Users_PhoneVerified DEFAULT (0),
    ProfileImage     NVARCHAR(300)  NULL,
    IsActive         BIT            NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    FailedLoginCount INT            NOT NULL CONSTRAINT DF_Users_FailedLogin DEFAULT (0),
    LockoutEndUtc    DATETIME2(0)   NULL,
    TwoFactorEnabled BIT            NOT NULL CONSTRAINT DF_Users_2FA DEFAULT (0),
    ConsentAccepted  BIT            NOT NULL CONSTRAINT DF_Users_Consent DEFAULT (0),
    LastLoginAtUtc   DATETIME2(0)   NULL,
    CreatedAtUtc     DATETIME2(0)   NOT NULL CONSTRAINT DF_Users_Created DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc     DATETIME2(0)   NOT NULL CONSTRAINT DF_Users_Updated DEFAULT (SYSUTCDATETIME())
);
GO
-- Unique when present (SQL Server UNIQUE disallows multiple NULLs; filtered index does not)
CREATE UNIQUE INDEX UQ_Users_Phone ON dbo.Users (Phone) WHERE Phone IS NOT NULL;
GO

CREATE TABLE dbo.UserRoles (
    UserId INT NOT NULL,
    RoleId INT NOT NULL,
    CONSTRAINT PK_UserRoles PRIMARY KEY CLUSTERED (UserId, RoleId),
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId)
);
GO

CREATE TABLE dbo.ExternalLogins (
    LoginId        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId         INT               NOT NULL,
    Provider       NVARCHAR(30)      NOT NULL,           -- 'Google'
    ProviderUserId NVARCHAR(128)     NOT NULL,
    Email          NVARCHAR(150)     NULL,
    DisplayName    NVARCHAR(150)     NULL,
    PictureUrl     NVARCHAR(400)     NULL,
    CreatedAtUtc   DATETIME2(0)      NOT NULL CONSTRAINT DF_ExternalLogins_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_ExternalLogins_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT UQ_ExternalLogins UNIQUE (Provider, ProviderUserId)
);
GO

CREATE TABLE dbo.OtpCodes (
    OtpId        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId       INT           NOT NULL,
    Purpose      VARCHAR(20)   NOT NULL,                 -- signup | login | reset | phone
    CodeHash     VARCHAR(100)  NOT NULL,                 -- HMAC-SHA256(code, serverKey)
    ExpiresAtUtc DATETIME2(0)  NOT NULL,
    MaxAttempts  TINYINT       NOT NULL CONSTRAINT DF_Otp_MaxAttempts DEFAULT (3),
    AttemptsUsed TINYINT       NOT NULL CONSTRAINT DF_Otp_AttemptsUsed DEFAULT (0),
    IsUsed       BIT           NOT NULL CONSTRAINT DF_Otp_IsUsed DEFAULT (0),
    SentChannel  VARCHAR(10)   NOT NULL CONSTRAINT DF_Otp_Channel DEFAULT ('email'), -- email | sms
    CreatedAtUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_Otp_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_OtpCodes_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_OtpCodes_Purpose CHECK (Purpose IN ('signup','login','reset','phone'))
);
GO

CREATE TABLE dbo.PasswordResetTokens (
    TokenId      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT (NEWID()),
    UserId       INT          NOT NULL,
    TokenHash    CHAR(64)     NOT NULL UNIQUE,             -- SHA256 hex of the token
    ExpiresAtUtc DATETIME2(0) NOT NULL,
    UsedAtUtc    DATETIME2(0) NULL,
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Reset_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Reset_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_Reset_Expiry CHECK (ExpiresAtUtc > DATEADD(MINUTE, 0, SYSUTCDATETIME()) OR 1 = 1)
);
GO

CREATE TABLE dbo.AuditLogs (
    AuditId      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId       INT           NULL,
    Action       VARCHAR(60)   NOT NULL,
    EntityTable  VARCHAR(60)   NULL,
    EntityId     INT           NULL,
    DetailJson   NVARCHAR(1000) NULL,
    ClientIp     VARCHAR(45)   NULL,
    CreatedAtUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_Audit_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_AuditLog_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE SET NULL
);
GO

CREATE TABLE dbo.AppSettings (
    SettingKey   VARCHAR(50)   NOT NULL PRIMARY KEY,
    SettingValue NVARCHAR(300) NOT NULL,
    Description  NVARCHAR(300) NULL,
    ModifiedBy   INT           NULL,
    ModifiedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Settings_Modified DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Settings_Users FOREIGN KEY (ModifiedBy) REFERENCES dbo.Users (UserId)
);
GO

/* ---------- ACADEMIC STRUCTURE ---------- */

CREATE TABLE dbo.AcademicSessions (
    SessionId   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    SessionName NVARCHAR(20) NOT NULL UNIQUE,              -- e.g. '2025-2026'
    StartDate   DATE        NOT NULL,
    EndDate     DATE        NOT NULL,
    IsActive    BIT         NOT NULL CONSTRAINT DF_Session_Active DEFAULT (0),
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Session_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Session_Range CHECK (EndDate > StartDate)
);
GO

CREATE TABLE dbo.Semesters (
    SemesterId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    SessionId      INT   NOT NULL,
    SemesterNumber TINYINT NOT NULL,
    SemesterName   NVARCHAR(30) NOT NULL,
    StartDate      DATE NULL,
    EndDate        DATE NULL,
    CONSTRAINT FK_Semesters_Session FOREIGN KEY (SessionId) REFERENCES dbo.AcademicSessions (SessionId),
    CONSTRAINT CK_Semesters_Number CHECK (SemesterNumber BETWEEN 1 AND 12),
    CONSTRAINT UQ_Semesters_Session_Number UNIQUE (SessionId, SemesterNumber)
);
GO

CREATE TABLE dbo.Courses (
    CourseId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CourseCode   VARCHAR(20)   NOT NULL UNIQUE,
    CourseName   NVARCHAR(120) NOT NULL,
    Description  NVARCHAR(500) NULL,
    TotalSemesters TINYINT     NOT NULL CONSTRAINT DF_Course_Duration DEFAULT (6),
    TotalCredits SMALLINT      NOT NULL CONSTRAINT DF_Course_Credits DEFAULT (120),
    IsActive     BIT           NOT NULL CONSTRAINT DF_Course_Active DEFAULT (1),
    CreatedAtUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_Course_Created DEFAULT (SYSUTCDATETIME())
);
GO

CREATE TABLE dbo.Subjects (
    SubjectId    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    SubjectCode  VARCHAR(20)   NOT NULL UNIQUE,
    SubjectName  NVARCHAR(120) NOT NULL,
    DefaultMaxMarks DECIMAL(5,0) NOT NULL CONSTRAINT DF_Subject_MaxMarks DEFAULT (100),
    DefaultPassPercent DECIMAL(5,2) NOT NULL CONSTRAINT DF_Subject_PassPct DEFAULT (33),
    WeeklyHours  TINYINT       NULL,
    IsActive     BIT           NOT NULL CONSTRAINT DF_Subject_Active DEFAULT (1),
    CreatedAtUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_Subject_Created DEFAULT (SYSUTCDATETIME())
);
GO

CREATE TABLE dbo.CourseSubjects (
    CourseId       INT   NOT NULL,
    SubjectId      INT   NOT NULL,
    SemesterNumber TINYINT NOT NULL,
    CONSTRAINT PK_CourseSubjects PRIMARY KEY CLUSTERED (CourseId, SubjectId, SemesterNumber),
    CONSTRAINT FK_CourseSubjects_Course FOREIGN KEY (CourseId) REFERENCES dbo.Courses (CourseId) ON DELETE CASCADE,
    CONSTRAINT FK_CourseSubjects_Subject FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects (SubjectId) ON DELETE CASCADE,
    CONSTRAINT FK_CourseSubjects_SemNo CHECK (SemesterNumber BETWEEN 1 AND 12)
);
GO

/* ---------- PEOPLE ---------- */

CREATE TABLE dbo.Faculty (
    FacultyId   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId      INT          NOT NULL UNIQUE,
    DeptCode    NVARCHAR(60) NOT NULL,
    Designation NVARCHAR(60) NULL,
    JoinedOn    DATE         NULL,
    CONSTRAINT FK_Faculty_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId)
);
GO

CREATE TABLE dbo.Students (
    StudentId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId        INT          NOT NULL UNIQUE,
    EnrollNumber  VARCHAR(24)  NOT NULL UNIQUE,           -- registration number
    CourseId      INT          NOT NULL,
    SessionId     INT          NOT NULL,
    SemesterNumber TINYINT     NOT NULL CONSTRAINT DF_Student_Sem DEFAULT (1),
    AdmissionDate DATE         NULL,
    DateOfBirth   DATE         NULL,
    GuardianName  NVARCHAR(120) NULL,
    Address       NVARCHAR(300) NULL,
    CONSTRAINT FK_Students_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_Students_Courses FOREIGN KEY (CourseId) REFERENCES dbo.Courses (CourseId),
    CONSTRAINT FK_Students_Session FOREIGN KEY (SessionId) REFERENCES dbo.AcademicSessions (SessionId)
);
GO

CREATE TABLE dbo.Enrollments (
    EnrollId   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    StudentId  INT NOT NULL,
    CourseId   INT NOT NULL,
    SessionId  INT NOT NULL,
    IsActive   BIT NOT NULL CONSTRAINT DF_Enroll_Active DEFAULT (1),
    EnrolledAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Enroll_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Enroll_Students FOREIGN KEY (StudentId) REFERENCES dbo.Students (StudentId),
    CONSTRAINT FK_Enroll_Courses FOREIGN KEY (CourseId) REFERENCES dbo.Courses (CourseId),
    CONSTRAINT FK_Enroll_Session FOREIGN KEY (SessionId) REFERENCES dbo.AcademicSessions (SessionId),
    CONSTRAINT UQ_Enrollments UNIQUE (StudentId, CourseId, SessionId)
);
GO

CREATE TABLE dbo.FacultyAssignments (
    AssignmentId   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    FacultyId      INT   NOT NULL,
    CourseId       INT   NOT NULL,
    SubjectId      INT   NOT NULL,
    SessionId      INT   NOT NULL,
    SemesterNumber TINYINT NOT NULL,
    CONSTRAINT FK_FA_Faculty  FOREIGN KEY (FacultyId) REFERENCES dbo.Faculty (FacultyId),
    CONSTRAINT FK_FA_Course   FOREIGN KEY (CourseId)  REFERENCES dbo.Courses (CourseId),
    CONSTRAINT FK_FA_Subject  FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects (SubjectId),
    CONSTRAINT FK_FA_Session  FOREIGN KEY (SessionId) REFERENCES dbo.AcademicSessions (SessionId),
    CONSTRAINT UQ_FA_Assignment UNIQUE (FacultyId, CourseId, SubjectId, SessionId, SemesterNumber)
);
GO

/* ---------- ATTENDANCE ---------- */

CREATE TABLE dbo.AttendanceSessions (
    AttnSessionId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    FacultyUserId INT NOT NULL,
    CourseId      INT NOT NULL,
    SubjectId     INT NOT NULL,
    SessionDate   DATE NOT NULL,
    PeriodNo      TINYINT NOT NULL CONSTRAINT DF_AttnSession_Period DEFAULT (1),
    SubjectName   NVARCHAR(120) NULL,                      -- denormalised snapshot
    CONSTRAINT FK_AttnSession_Faculty FOREIGN KEY (FacultyUserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_AttnSession_Course  FOREIGN KEY (CourseId)  REFERENCES dbo.Courses (CourseId),
    CONSTRAINT FK_AttnSession_Subject FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects (SubjectId),
    CONSTRAINT UQ_AttnSession UNIQUE (FacultyUserId, SessionDate, PeriodNo, SubjectId, CourseId)
);
GO

CREATE TABLE dbo.Attendance (
    AttnId        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    AttnSessionId INT NOT NULL,
    EnrollId      INT NOT NULL,
    AttnStatus    CHAR(1)   NOT NULL,                      -- P | A | L(exempt) | E(authorised)
    Remarks       NVARCHAR(100) NULL,
    CreatedAtUtc  DATETIME2(0) NOT NULL CONSTRAINT DF_Attn_Created DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc  DATETIME2(0) NOT NULL CONSTRAINT DF_Attn_Updated DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Attn_Session FOREIGN KEY (AttnSessionId) REFERENCES dbo.AttendanceSessions (AttnSessionId) ON DELETE CASCADE,
    CONSTRAINT FK_Attn_Enroll  FOREIGN KEY (EnrollId)      REFERENCES dbo.Enrollments (EnrollId),
    CONSTRAINT UQ_AttnRow UNIQUE (AttnSessionId, EnrollId),
    CONSTRAINT CK_Attn_Status CHECK (AttnStatus IN ('P','A','L','E'))
);
GO

/* ---------- MARKS / EXAMS / RESULTS ---------- */

CREATE TABLE dbo.Exams (
    ExamId      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ExamName    NVARCHAR(60) NOT NULL,
    CourseId    INT NOT NULL,
    SubjectId   INT NOT NULL,
    CreatedBy   INT NOT NULL,
    MaxMarks    DECIMAL(5,0) NOT NULL CONSTRAINT DF_Exam_MaxMarks DEFAULT (100),
    PassPercent DECIMAL(5,2) NOT NULL CONSTRAINT DF_Exam_PassPct DEFAULT (33),
    WeightType  TINYINT NOT NULL CONSTRAINT DF_Exam_WeightType DEFAULT (1),  -- 1 = regular test, 2 = final exam, 3 = practical
    ExamDate    DATE NULL,
    IsPublished BIT  NOT NULL CONSTRAINT DF_Exam_Published DEFAULT (0),
    PublishedAtUtc DATETIME2(0) NULL,
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Exam_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Exams_Course  FOREIGN KEY (CourseId)  REFERENCES dbo.Courses (CourseId),
    CONSTRAINT FK_Exams_Subject FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects (SubjectId),
    CONSTRAINT FK_Exams_Creator FOREIGN KEY (CreatedBy) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_Exams_Max CHECK (MaxMarks > 0),
    CONSTRAINT CK_Exams_Pass CHECK (PassPercent BETWEEN 0 AND 100)
);
GO

CREATE TABLE dbo.Marks (
    MarkId        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ExamId        INT NOT NULL,
    EnrollId      INT NOT NULL,
    MarksObtained DECIMAL(5,2) NOT NULL,
    EnteredBy     INT NOT NULL,
    CreatedAtUtc  DATETIME2(0) NOT NULL CONSTRAINT DF_Marks_Created DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc  DATETIME2(0) NOT NULL CONSTRAINT DF_Marks_Updated DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Marks_Exam   FOREIGN KEY (ExamId)   REFERENCES dbo.Exams (ExamId) ON DELETE CASCADE,
    CONSTRAINT FK_Marks_Enroll FOREIGN KEY (EnrollId) REFERENCES dbo.Enrollments (EnrollId),
    CONSTRAINT FK_Marks_By     FOREIGN KEY (EnteredBy) REFERENCES dbo.Users (UserId),
    CONSTRAINT UQ_Marks UNIQUE (ExamId, EnrollId)
);
GO

CREATE TABLE dbo.GradeScale (
    GradeId    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    MinPercent DECIMAL(5,2) NOT NULL,
    Grade      VARCHAR(4)   NOT NULL,
    Remark     NVARCHAR(60) NULL,
    CONSTRAINT UQ_GradeScale_Grade UNIQUE (Grade),
    CONSTRAINT CK_GradeScale_Pct CHECK (MinPercent BETWEEN 0 AND 100)
);
GO

CREATE TABLE dbo.Results (
    ResultId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ExamId       INT NOT NULL,
    EnrollId     INT NOT NULL,
    MarksObtained DECIMAL(5,2) NOT NULL,
    MaxMarks     DECIMAL(5,0) NOT NULL,
    PercentScore DECIMAL(5,2) NOT NULL,
    Grade        VARCHAR(4)   NOT NULL,
    IsPass       BIT          NOT NULL,
    PublishedAtUtc DATETIME2(0) NULL,
    CONSTRAINT FK_Results_Exam   FOREIGN KEY (ExamId)   REFERENCES dbo.Exams (ExamId) ON DELETE CASCADE,
    CONSTRAINT FK_Results_Enroll FOREIGN KEY (EnrollId) REFERENCES dbo.Enrollments (EnrollId),
    CONSTRAINT UQ_Results UNIQUE (ExamId, EnrollId)
);
GO

/* ---------- NOTIFICATIONS ---------- */

CREATE TABLE dbo.Notifications (
    NotificationId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Title        NVARCHAR(150) NOT NULL,
    Message      NVARCHAR(600) NOT NULL,
    NType        VARCHAR(25)   NOT NULL CONSTRAINT DF_Notif_Type DEFAULT ('general'), -- general|academic|payment|complaint|system
    ActionUrl    NVARCHAR(200) NULL,
    CreatedBy    INT           NULL,
    CreatedAtUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_Notif_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Notif_By FOREIGN KEY (CreatedBy) REFERENCES dbo.Users (UserId)
);
GO

CREATE TABLE dbo.NotificationRecipients (
    NotificationId INT NOT NULL,
    UserId       INT NOT NULL,
    IsRead       BIT NOT NULL CONSTRAINT DF_NRec_IsRead DEFAULT (0),
    ReadAtUtc    DATETIME2(0) NULL,
    CONSTRAINT PK_NRecipients PRIMARY KEY CLUSTERED (NotificationId, UserId),
    CONSTRAINT FK_NRec_Notif FOREIGN KEY (NotificationId) REFERENCES dbo.Notifications (NotificationId) ON DELETE CASCADE,
    CONSTRAINT FK_NRec_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT UQ_NRecipients UNIQUE (NotificationId, UserId)
);
GO

CREATE TABLE dbo.NotificationOutbox (
    OutboxId    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    NotificationId INT NOT NULL,
    Channel     VARCHAR(10)  NOT NULL CONSTRAINT DF_Outbox_Channel DEFAULT ('email'), -- email|push|sms
    Status      VARCHAR(10)  NOT NULL CONSTRAINT DF_Outbox_Status DEFAULT ('queued'), -- queued|sent|failed
    Attempts    TINYINT      NOT NULL CONSTRAINT DF_Outbox_Attempts DEFAULT (0),
    LastError   NVARCHAR(500) NULL,
    SentAtUtc   DATETIME2(0) NULL,
    NextAttemptAtUtc DATETIME2(0) NULL,
    CONSTRAINT FK_Outbox_Notif FOREIGN KEY (NotificationId) REFERENCES dbo.Notifications (NotificationId)
);
GO

CREATE TABLE dbo.PushSubscriptions (
    SubId      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId     INT NOT NULL,
    Endpoint   NVARCHAR(500) NOT NULL,
    P256dh     NVARCHAR(300) NOT NULL,
    AuthSecret NVARCHAR(200) NOT NULL,
    UserAgent  NVARCHAR(200) NULL,
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Push_Created DEFAULT (SYSUTCDATETIME()),
    LastOkAtUtc  DATETIME2(0) NULL,
    IsExpired   BIT NOT NULL CONSTRAINT DF_Push_Expired DEFAULT (0),
    CONSTRAINT FK_Push_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT UQ_Push_Endpoint UNIQUE (Endpoint)
);
GO

/* ---------- COMPLAINTS ---------- */

CREATE TABLE dbo.ComplaintCategories (
    CategoryId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CatName    NVARCHAR(60) NOT NULL UNIQUE,
    IsActive   BIT NOT NULL CONSTRAINT DF_CCat_Active DEFAULT (1)
);
GO

CREATE TABLE dbo.Complaints (
    ComplaintId  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ComplaintNo  VARCHAR(14)  NOT NULL UNIQUE,           -- CMP-2026-0001
    RaisedBy     INT NOT NULL,
    CategoryId   INT NOT NULL,
    Subject      NVARCHAR(150) NOT NULL,
    Description  NVARCHAR(2000) NOT NULL,
    Status       VARCHAR(12) NOT NULL CONSTRAINT DF_Compl_Status DEFAULT ('open'),
    AssignedTo   INT NULL,
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Compl_Created DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Compl_Updated DEFAULT (SYSUTCDATETIME()),
    ResolvedAtUtc DATETIME2(0) NULL,
    CONSTRAINT FK_Compl_By    FOREIGN KEY (RaisedBy) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_Compl_Cat   FOREIGN KEY (CategoryId) REFERENCES dbo.ComplaintCategories (CategoryId),
    CONSTRAINT FK_Compl_Asgn  FOREIGN KEY (AssignedTo) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_Compl_Status CHECK (Status IN ('open','in_progress','resolved','closed'))
);
GO

CREATE TABLE dbo.ComplaintThreads (
    ThreadId    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ComplaintId INT NOT NULL,
    AuthorId    INT NOT NULL,
    Message     NVARCHAR(1000) NOT NULL,
    IsInternal  BIT NOT NULL CONSTRAINT DF_Thrd_Internal DEFAULT (0),
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Thrd_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Thrd_Compl FOREIGN KEY (ComplaintId) REFERENCES dbo.Complaints (ComplaintId) ON DELETE CASCADE,
    CONSTRAINT FK_Thrd_Author FOREIGN KEY (AuthorId) REFERENCES dbo.Users (UserId)
);
GO

/* ---------- PAYMENTS ---------- */

CREATE TABLE dbo.Fees (
    FeeId   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CourseId INT NOT NULL,
    FeeCode  VARCHAR(20) NOT NULL,
    FeeName  NVARCHAR(100) NOT NULL,
    Amount   DECIMAL(10,2) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Fee_Active DEFAULT (1),
    CONSTRAINT FK_Fees_Course FOREIGN KEY (CourseId) REFERENCES dbo.Courses (CourseId),
    CONSTRAINT UQ_Fees_Course_Code UNIQUE (CourseId, FeeCode),
    CONSTRAINT CK_Fees_Amount CHECK (Amount > 0)
);
GO

CREATE TABLE dbo.Payments (
    PaymentId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId        INT NOT NULL,
    FeeId         INT NULL,
    Purpose       NVARCHAR(120) NOT NULL,
    Amount        DECIMAL(10,2) NOT NULL,
    Currency      CHAR(3)   NOT NULL CONSTRAINT DF_Pay_Currency DEFAULT ('INR'),
    PayStatus     VARCHAR(12) NOT NULL CONSTRAINT DF_Pay_Status DEFAULT ('created'),
    AmountInPaise BIGINT    NOT NULL CONSTRAINT DF_Pay_Paise DEFAULT (0),
    GatewayOrderId    VARCHAR(64) NOT NULL,
    GatewayPaymentId  VARCHAR(64) NULL,
    GatewaySignature  VARCHAR(256) NULL,
    PayMethod     VARCHAR(20) NULL,
    ReceiptNo     VARCHAR(20) NULL,
    ReceiptIssuedAt DATETIME2(0) NULL,
    IdempotencyKey VARCHAR(48) NULL,
    CreatedAtUtc  DATETIME2(0) NOT NULL CONSTRAINT DF_Pay_Created DEFAULT (SYSUTCDATETIME()),
    CapturedAtUtc DATETIME2(0) NULL,
    UpdatedAtUtc  DATETIME2(0) NOT NULL CONSTRAINT DF_Pay_Updated DEFAULT (SYSUTCDATETIME()),
    Remarks       NVARCHAR(300) NULL,
    CONSTRAINT FK_Pay_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_Pay_Fees FOREIGN KEY (FeeId) REFERENCES dbo.Fees (FeeId),
    CONSTRAINT CK_Pay_Amount CHECK (Amount > 0),
    CONSTRAINT CK_Pay_Status CHECK (PayStatus IN ('created','pending','captured','failed','cancelled','refunded'))
);
GO

CREATE UNIQUE INDEX UX_Payments_GatewayOrderId ON dbo.Payments (GatewayOrderId);
CREATE UNIQUE INDEX UX_Payments_GatewayPaymentId ON dbo.Payments (GatewayPaymentId) WHERE GatewayPaymentId IS NOT NULL;
CREATE UNIQUE INDEX UX_Payments_ReceiptNo ON dbo.Payments (ReceiptNo) WHERE ReceiptNo IS NOT NULL;
CREATE UNIQUE INDEX UX_Payments_IdempotencyKey ON dbo.Payments (IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
GO

CREATE TABLE dbo.PaymentEvents (
    EventId       INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PaymentId     INT NOT NULL,
    EventType     VARCHAR(50) NOT NULL,
    PayloadHash   VARCHAR(64) NOT NULL,
    SignatureValid BIT NOT NULL,
    ProcessedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_PayEv_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_PayEvents_Pay FOREIGN KEY (PaymentId) REFERENCES dbo.Payments (PaymentId),
    CONSTRAINT UQ_PayEvents UNIQUE (EventType, PayloadHash)  -- webhook idempotency
);
GO

/* ---------- ANNOUNCEMENTS ---------- */

CREATE TABLE dbo.Announcements (
    AnnId      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    AnnTitle   NVARCHAR(150) NOT NULL,
    AnnBody    NVARCHAR(2000) NOT NULL,
    Audience   VARCHAR(10) NOT NULL CONSTRAINT DF_Ann_Audience DEFAULT ('all'), -- all|students|faculty
    PublishFrom DATE NOT NULL CONSTRAINT DF_Ann_From DEFAULT (GETDATE()),
    PublishTo   DATE NULL,
    IsActive   BIT NOT NULL CONSTRAINT DF_Ann_Active DEFAULT (1),
    CreatedBy  INT NOT NULL,
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Ann_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Ann_By FOREIGN KEY (CreatedBy) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_Ann_Audience CHECK (Audience IN ('all','students','faculty')),
    CONSTRAINT CK_Ann_Range CHECK (PublishTo IS NULL OR PublishTo >= PublishFrom)
);
GO

/* ---------- CHAT / JOBS ---------- */

CREATE TABLE dbo.ChatLogs (
    ChatId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId     INT NULL,
    UserRole   VARCHAR(10) NULL,
    Question   NVARCHAR(500) NOT NULL,
    Answer     NVARCHAR(1500) NULL,
    Source     VARCHAR(12) NOT NULL CONSTRAINT DF_Chat_Source DEFAULT ('faq'), -- faq|llm|db
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Chat_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Chat_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId)
);
GO

CREATE TABLE dbo.FrequentlyAsked (
    FaqId    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Question NVARCHAR(150) NOT NULL,
    Answer   NVARCHAR(800) NOT NULL,
    Keywords NVARCHAR(150) NULL,
    RoleScope VARCHAR(12) NOT NULL CONSTRAINT DF_Faq_Scope DEFAULT ('all'),  -- all|student|faculty|admin
    SortOrder INT NOT NULL CONSTRAINT DF_Faq_Sort DEFAULT (0),
    IsActive BIT NOT NULL CONSTRAINT DF_Faq_Active DEFAULT (1)
);
GO

CREATE TABLE dbo.ImportJobs (
    JobId      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UploadedBy INT NOT NULL,
    FileName   NVARCHAR(200) NOT NULL,
    EntityTarget VARCHAR(40) NOT NULL,                    -- students | faculty | courses ...
    RowsTotal  INT NOT NULL CONSTRAINT DF_IJobs_Total DEFAULT (0),
    RowsOk     INT NOT NULL CONSTRAINT DF_IJobs_Ok DEFAULT (0),
    RowsError  INT NOT NULL CONSTRAINT DF_IJobs_Error DEFAULT (0),
    ErrorsSummary NVARCHAR(2000) NULL,
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_IJobs_Created DEFAULT (SYSUTCDATETIME()),
    CompletedAtUtc DATETIME2(0) NULL,
    CONSTRAINT FK_IJobs_By FOREIGN KEY (UploadedBy) REFERENCES dbo.Users (UserId)
);
GO

CREATE TABLE dbo.ExportJobs (
    ExportId    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    RequestedBy INT NOT NULL,
    Scope       VARCHAR(40) NOT NULL,
    Format      VARCHAR(10) NOT NULL,                     -- CSV | XLSX | JSON | PDF
    RowsReturned INT NOT NULL CONSTRAINT DF_EJobs_Rows DEFAULT (0),
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_EJobs_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_EJobs_By FOREIGN KEY (RequestedBy) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_EJobs_Format CHECK (Format IN ('CSV','XLSX','JSON','PDF'))
);
GO
PRINT N'All tables created.';
