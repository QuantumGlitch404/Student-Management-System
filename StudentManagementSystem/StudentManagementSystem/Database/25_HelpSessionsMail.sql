-- =====================================================================
-- 25_HelpSessionsMail.sql
-- Help centre content tables, academic session history and mail extras.
-- Idempotent.
-- =====================================================================
USE [StudentSMS];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

/* =================== HELP CENTRE =================== */
IF OBJECT_ID('dbo.HelpCategories','U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpCategories (
        CategoryId   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        RoleName     VARCHAR(20)    NOT NULL,
        Slug         VARCHAR(80)    NOT NULL,
        TitleEn      NVARCHAR(120)  NOT NULL,
        TitleHi      NVARCHAR(160)  NULL,
        TitleHinglish NVARCHAR(160) NULL,
        SortOrder    INT            NOT NULL CONSTRAINT DF_HelpCat_Sort DEFAULT (0),
        IsActive     BIT            NOT NULL CONSTRAINT DF_HelpCat_Active DEFAULT (1),
        CONSTRAINT UQ_HelpCategories UNIQUE (RoleName, Slug)
    );
    PRINT N'HelpCategories created.';
END
GO

IF OBJECT_ID('dbo.HelpArticles','U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpArticles (
        ArticleId    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CategoryId   INT            NOT NULL,
        RoleName     VARCHAR(20)    NOT NULL,
        Slug         VARCHAR(160)   NOT NULL,
        TitleEn      NVARCHAR(200)  NOT NULL,
        SummaryEn    NVARCHAR(600)  NOT NULL,
        StepsJson    NVARCHAR(MAX)  NULL,
        TipsEn       NVARCHAR(1000) NULL,
        RelatedJson  NVARCHAR(1000) NULL,
        KeywordsEn   NVARCHAR(400)  NULL,
        IsPublished  BIT            NOT NULL CONSTRAINT DF_HelpArt_Published DEFAULT (1),
        SortOrder    INT            NOT NULL CONSTRAINT DF_HelpArt_Sort DEFAULT (0),
        UpdatedUtc   DATETIME2(0)   NOT NULL CONSTRAINT DF_HelpArt_Updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_HelpArticles_Category FOREIGN KEY (CategoryId) REFERENCES dbo.HelpCategories (CategoryId) ON DELETE CASCADE,
        CONSTRAINT UQ_HelpArticles UNIQUE (RoleName, Slug)
    );
    CREATE INDEX IX_HelpArticles_Role ON dbo.HelpArticles (RoleName, IsPublished);
    PRINT N'HelpArticles created.';
END
GO

IF OBJECT_ID('dbo.HelpTranslations','U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpTranslations (
        TranslationId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ArticleId     INT            NOT NULL,
        LanguageCode  VARCHAR(10)    NOT NULL,      -- hi | hinglish
        Title         NVARCHAR(200)  NOT NULL,
        Summary       NVARCHAR(600)  NOT NULL,
        StepsJson     NVARCHAR(MAX)  NULL,
        Tips          NVARCHAR(1000) NULL,
        Reviewed      BIT            NOT NULL CONSTRAINT DF_HelpTrans_Reviewed DEFAULT (0),
        CONSTRAINT FK_HelpTrans_Article FOREIGN KEY (ArticleId) REFERENCES dbo.HelpArticles (ArticleId) ON DELETE CASCADE,
        CONSTRAINT UQ_HelpTranslations UNIQUE (ArticleId, LanguageCode)
    );
    PRINT N'HelpTranslations created.';
END
GO

IF OBJECT_ID('dbo.HelpFeedback','U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpFeedback (
        FeedbackId   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ArticleId    INT            NOT NULL,
        UserId       INT            NULL,
        Helpful      BIT            NOT NULL,
        Comment      NVARCHAR(500)  NULL,
        CreatedUtc   DATETIME2(0)   NOT NULL CONSTRAINT DF_HelpFeedback_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_HelpFeedback_Article FOREIGN KEY (ArticleId) REFERENCES dbo.HelpArticles (ArticleId) ON DELETE CASCADE
    );
    PRINT N'HelpFeedback created.';
END
GO

IF COL_LENGTH('dbo.HelpArticles','SearchText') IS NULL
    ALTER TABLE dbo.HelpArticles ADD SearchText NVARCHAR(MAX) NULL;
GO

/* =================== ACADEMIC SESSION HISTORY =================== */
IF OBJECT_ID('dbo.AcademicSessionHistory','U') IS NULL
BEGIN
    CREATE TABLE dbo.AcademicSessionHistory (
        HistoryId   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SessionId   INT            NOT NULL,
        Action      VARCHAR(40)    NOT NULL,
        OldJson     NVARCHAR(MAX)  NULL,
        NewJson     NVARCHAR(MAX)  NULL,
        Reason      NVARCHAR(300)  NULL,
        ChangedBy   INT            NULL,
        ChangedUtc  DATETIME2(0)   NOT NULL CONSTRAINT DF_AcadSessionHist_Changed DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_AcadSessionHist_Session FOREIGN KEY (SessionId) REFERENCES dbo.AcademicSessions (SessionId) ON DELETE CASCADE
    );
    CREATE INDEX IX_AcadSessionHist_Session ON dbo.AcademicSessionHistory (SessionId, ChangedUtc DESC);
    PRINT N'AcademicSessionHistory created.';
END
GO

IF OBJECT_ID('dbo.AcademicSessionSnapshots','U') IS NULL
BEGIN
    CREATE TABLE dbo.AcademicSessionSnapshots (
        SnapshotId  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SessionId   INT            NOT NULL,
        SnapshotJson NVARCHAR(MAX) NOT NULL,
        CreatedUtc  DATETIME2(0)   NOT NULL CONSTRAINT DF_AcadSnap_Created DEFAULT (SYSUTCDATETIME()),
        CreatedBy   INT            NULL,
        CONSTRAINT FK_AcadSnap_Session FOREIGN KEY (SessionId) REFERENCES dbo.AcademicSessions (SessionId) ON DELETE CASCADE
    );
    PRINT N'AcademicSessionSnapshots created.';
END
GO

/* =================== MAIL EXTRAS =================== */
IF COL_LENGTH('dbo.MailMessages','Snippet') IS NULL
    ALTER TABLE dbo.MailMessages ADD Snippet NVARCHAR(300) NULL;
IF COL_LENGTH('dbo.MailMessages','HasAttachments') IS NULL
    ALTER TABLE dbo.MailMessages ADD HasAttachments BIT NOT NULL CONSTRAINT DF_MailMsg_Attach DEFAULT (0);
IF COL_LENGTH('dbo.MailMessages','MessageIdHeader') IS NULL
    ALTER TABLE dbo.MailMessages ADD MessageIdHeader NVARCHAR(300) NULL;
IF COL_LENGTH('dbo.MailMessages','AttachmentJson') IS NULL
    ALTER TABLE dbo.MailMessages ADD AttachmentJson NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID('dbo.MailLabels','U') IS NULL
BEGIN
    CREATE TABLE dbo.MailLabels (
        LabelId    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        LabelName  NVARCHAR(60)  NOT NULL,
        Colour     VARCHAR(20)   NOT NULL CONSTRAINT DF_MailLabel_Colour DEFAULT ('#2563EB'),
        CreatedBy  INT           NULL,
        CreatedUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_MailLabel_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_MailLabels_Name UNIQUE (LabelName)
    );
    PRINT N'MailLabels created.';
END
GO

IF OBJECT_ID('dbo.MailMessageLabels','U') IS NULL
BEGIN
    CREATE TABLE dbo.MailMessageLabels (
        MessageId INT NOT NULL,
        LabelId   INT NOT NULL,
        CONSTRAINT PK_MailMessageLabels PRIMARY KEY (MessageId, LabelId),
        CONSTRAINT FK_MailMsgLabels_Message FOREIGN KEY (MessageId) REFERENCES dbo.MailMessages (MessageId) ON DELETE CASCADE,
        CONSTRAINT FK_MailMsgLabels_Label FOREIGN KEY (LabelId) REFERENCES dbo.MailLabels (LabelId) ON DELETE CASCADE
    );
    PRINT N'MailMessageLabels created.';
END
GO

IF OBJECT_ID('dbo.MailRules','U') IS NULL
BEGIN
    CREATE TABLE dbo.MailRules (
        RuleId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        RuleName   NVARCHAR(100) NOT NULL,
        MatchField VARCHAR(20)   NOT NULL CONSTRAINT DF_MailRule_Field DEFAULT ('from'),
        MatchText  NVARCHAR(200) NOT NULL,
        Action     VARCHAR(20)   NOT NULL CONSTRAINT DF_MailRule_Action DEFAULT ('label'),
        ActionValue NVARCHAR(120) NULL,
        IsActive   BIT           NOT NULL CONSTRAINT DF_MailRule_Active DEFAULT (1),
        CreatedBy  INT           NULL,
        CreatedUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_MailRule_Created DEFAULT (SYSUTCDATETIME())
    );
    PRINT N'MailRules created.';
END
GO

IF OBJECT_ID('dbo.MailAutoReply','U') IS NULL
BEGIN
    CREATE TABLE dbo.MailAutoReply (
        AutoReplyId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        IsEnabled  BIT           NOT NULL CONSTRAINT DF_MailAutoReply_Enabled DEFAULT (0),
        StartsUtc  DATETIME2(0)  NULL,
        EndsUtc    DATETIME2(0)  NULL,
        Subject    NVARCHAR(200) NULL,
        BodyText   NVARCHAR(MAX) NULL,
        UpdatedUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_MailAutoReply_Updated DEFAULT (SYSUTCDATETIME())
    );
    IF NOT EXISTS (SELECT 1 FROM dbo.MailAutoReply)
        INSERT INTO dbo.MailAutoReply (IsEnabled) VALUES (0);
    PRINT N'MailAutoReply created.';
END
GO

IF OBJECT_ID('dbo.MailContacts','U') IS NULL
BEGIN
    CREATE TABLE dbo.MailContacts (
        ContactId  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        DisplayName NVARCHAR(120) NULL,
        Email       NVARCHAR(150) NOT NULL,
        GroupName   NVARCHAR(80)  NULL,
        IsExternal  BIT           NOT NULL CONSTRAINT DF_MailContacts_External DEFAULT (1),
        CreatedUtc  DATETIME2(0)  NOT NULL CONSTRAINT DF_MailContacts_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_MailContacts_Email UNIQUE (Email)
    );
    PRINT N'MailContacts created.';
END
GO

IF OBJECT_ID('dbo.MailSignatures','U') IS NULL
BEGIN
    CREATE TABLE dbo.MailSignatures (
        SignatureId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId      INT            NOT NULL,
        BodyHtml    NVARCHAR(MAX)  NULL,
        IsDefault   BIT            NOT NULL CONSTRAINT DF_MailSig_Default DEFAULT (1),
        UpdatedUtc  DATETIME2(0)   NOT NULL CONSTRAINT DF_MailSig_Updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_MailSignatures_User FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE CASCADE
    );
    PRINT N'MailSignatures created.';
END
GO

PRINT N'25_HelpSessionsMail.sql complete.';
GO
