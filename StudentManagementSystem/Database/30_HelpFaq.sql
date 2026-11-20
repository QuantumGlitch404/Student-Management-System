SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF OBJECT_ID('dbo.HelpFaq','U') IS NULL
CREATE TABLE dbo.HelpFaq(
 FaqId INT IDENTITY(1,1) PRIMARY KEY,
 RoleName NVARCHAR(20) NOT NULL,
 ArticleId INT NULL,
 QuestionEn NVARCHAR(300) NOT NULL,
 AnswerEn NVARCHAR(1000) NOT NULL,
 QuestionHi NVARCHAR(300) NULL,
 AnswerHi NVARCHAR(1000) NULL,
 QuestionHinglish NVARCHAR(300) NULL,
 AnswerHinglish NVARCHAR(1000) NULL,
 SortOrder INT NOT NULL DEFAULT(0),
 IsPublished BIT NOT NULL DEFAULT(1)
);
GO
IF NOT EXISTS(SELECT 1 FROM dbo.HelpFaq)
BEGIN
 INSERT INTO dbo.HelpFaq(RoleName,ArticleId,QuestionEn,AnswerEn,QuestionHi,AnswerHi,QuestionHinglish,AnswerHinglish,SortOrder)
 SELECT TOP (600) a.RoleName, a.ArticleId,
   N'How do I use ' + a.TitleEn + N'?',
   LEFT(a.SummaryEn, 900),
   N'' + ISNULL(th.Title, a.TitleEn) + N' kaise use karein?',
   LEFT(ISNULL(th.Summary, a.SummaryEn), 900),
   N'How do I use ' + ISNULL(tg.Title, a.TitleEn) + N'?',
   LEFT(ISNULL(tg.Summary, a.SummaryEn), 900),
   a.SortOrder
 FROM dbo.HelpArticles a
 LEFT JOIN dbo.HelpTranslations th ON th.ArticleId=a.ArticleId AND th.LanguageCode='hi'
 LEFT JOIN dbo.HelpTranslations tg ON tg.ArticleId=a.ArticleId AND tg.LanguageCode='hinglish'
 WHERE a.IsPublished=1
 ORDER BY a.SortOrder, a.ArticleId;
END
GO
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE ScriptName='30_HelpFaq.sql')
 INSERT INTO dbo.SchemaMigrations (ScriptName) VALUES ('30_HelpFaq.sql');
GO

IF (SELECT COUNT(*) FROM dbo.HelpFaq) < 600
BEGIN
 INSERT INTO dbo.HelpFaq(RoleName,ArticleId,QuestionEn,AnswerEn,QuestionHi,AnswerHi,QuestionHinglish,AnswerHinglish,SortOrder)
 SELECT TOP (300) a.RoleName,a.ArticleId,
  N'What are common mistakes in ' + a.TitleEn + N'?',
  LEFT(ISNULL(NULLIF(a.TipsEn,''), a.SummaryEn),900),
  ISNULL(th.Title,a.TitleEn) + N' mein common galtiyan kya hain?',
  LEFT(ISNULL(ISNULL(NULLIF(th.Tips,''),th.Summary),a.SummaryEn),900),
  N'What are common mistakes in ' + ISNULL(tg.Title,a.TitleEn) + N'?',
  LEFT(ISNULL(ISNULL(NULLIF(tg.Tips,''),tg.Summary),a.SummaryEn),900),
  a.SortOrder+1
 FROM dbo.HelpArticles a
 LEFT JOIN dbo.HelpTranslations th ON th.ArticleId=a.ArticleId AND th.LanguageCode='hi'
 LEFT JOIN dbo.HelpTranslations tg ON tg.ArticleId=a.ArticleId AND tg.LanguageCode='hinglish'
 WHERE a.IsPublished=1 AND NOT EXISTS(SELECT 1 FROM dbo.HelpFaq f WHERE f.ArticleId=a.ArticleId AND f.SortOrder=a.SortOrder+1)
 ORDER BY a.SortOrder,a.ArticleId;
END
GO
