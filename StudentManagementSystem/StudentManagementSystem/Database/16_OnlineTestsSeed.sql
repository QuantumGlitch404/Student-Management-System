-- =====================================================================
-- 16_OnlineTestsSeed.sql
-- 60 practice tests, 20 questions each (1,200 questions) and demo attempts.
-- Idempotent: keyed on category slug / prompt prefix.
-- =====================================================================
USE [StudentSMS];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

DECLARE @admin INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Email = N'admin@sms.edu');
IF @admin IS NULL SET @admin = (SELECT TOP 1 UserId FROM dbo.Users ORDER BY UserId);

-- 1. Twenty reusable, factual questions. Each category gets its own copy so the
--    bank reaches 1,200 searchable questions across 60 categories.
DECLARE @qt TABLE (n INT, Prompt NVARCHAR(1000), OA NVARCHAR(400), OB NVARCHAR(400), OC NVARCHAR(400), OD NVARCHAR(400), Ans CHAR(1), Expl NVARCHAR(1000));
INSERT @qt (n, Prompt, OA, OB, OC, OD, Ans, Expl) VALUES
 (1,  N'When solving a {cat} problem systematically, which step comes first?', N'Understand the problem and requirements', N'Skip verification', N'Guess the answer', N'Copy a friend''s work', 'A', N'Understanding the problem before acting is the foundation of every method.'),
 (2,  N'Which habit most improves accuracy while practising {cat}?', N'Reviewing your work before moving on', N'Rushing to finish first', N'Ignoring the instructions', N'Answering at random', 'A', N'Reviewing catches avoidable mistakes.'),
 (3,  N'What is 15% of 200?', N'30', N'20', N'25', N'35', 'A', N'200 x 0.15 = 30.'),
 (4,  N'A train covers 120 km in 2 hours. What is its average speed?', N'40 km/h', N'60 km/h', N'80 km/h', N'120 km/h', 'B', N'120 / 2 = 60 km/h.'),
 (5,  N'Which number completes the series 2, 6, 12, 20, ?', N'24', N'26', N'30', N'32', 'C', N'The gaps are 4, 6, 8, 10, so 20 + 10 = 30.'),
 (6,  N'The value of 7 x 8 - 6 is:', N'50', N'42', N'56', N'48', 'A', N'56 - 6 = 50.'),
 (7,  N'Which word is the odd one out: apple, banana, carrot, mango?', N'Apple', N'Banana', N'Carrot', N'Mango', 'C', N'Carrot is a vegetable; the others are fruits.'),
 (8,  N'Choose the correctly spelled word.', N'Recieve', N'Receive', N'Receve', N'Recive', 'B', N'Receive follows the i-before-e-except-after-c rule.'),
 (9,  N'A shop offers 20% off on a 500 item. What is the final price?', N'450', N'400', N'380', N'420', 'B', N'500 - 100 = 400.'),
 (10, N'If today is Monday, what day will it be after 10 days?', N'Tuesday', N'Wednesday', N'Friday', N'Thursday', 'D', N'10 mod 7 = 3, and Monday + 3 days is Thursday.'),
 (11, N'Which is the largest: 0.5, 0.05, 0.55, 0.505?', N'0.5', N'0.05', N'0.55', N'0.505', 'C', N'0.55 has the greatest value.'),
 (12, N'What comes next in the series A, C, E, G, ?', N'I', N'H', N'J', N'K', 'A', N'Every second letter: A, C, E, G, I.'),
 (13, N'Which unit measures digital storage?', N'Byte', N'Litre', N'Newton', N'Watt', 'A', N'Byte is the standard unit of digital information.'),
 (14, N'The opposite of ''scarce'' is:', N'Rare', N'Limited', N'Short', N'Abundant', 'D', N'Scarce means in short supply; abundant means plentiful.'),
 (15, N'If x + 5 = 12, then x equals:', N'5', N'7', N'8', N'17', 'B', N'x = 12 - 5 = 7.'),
 (16, N'A triangle with all three sides equal is called:', N'Isosceles', N'Scalene', N'Equilateral', N'Right angled', 'C', N'Equilateral triangles have three equal sides.'),
 (17, N'Which activity best supports revision in {cat}?', N'Working through practice questions', N'Reading only the title', N'Skipping difficult topics', N'Studying only the night before', 'A', N'Active practice beats passive reading.'),
 (18, N'Which is a valid time-management habit during an online exam?', N'Attempting every question twice', N'Allocating time per section and tracking it', N'Leaving all answers until the last minute', N'Ignoring the timer', 'B', N'Section-wise time budgets keep you on schedule.'),
 (19, N'What should you do before submitting an online test?', N'Review questions you left unanswered', N'Close the browser immediately', N'Share answers with a friend', N'Delete your answers', 'A', N'A final review prevents accidental blanks.'),
 (20, N'Which behaviour protects exam integrity in {cat}?', N'Discussing answers during the test', N'Using unauthorised notes', N'Keeping your work your own', N'Copying from another screen', 'C', N'Independent work is the basis of fair assessment.');

DECLARE @nums TABLE (n INT PRIMARY KEY);
INSERT @nums (n) VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13),(14),(15),(16),(17),(18),(19),(20);

;WITH src AS (
    SELECT c.CategoryId, c.CategoryName, c.Slug, q.*,
           ROW_NUMBER() OVER (PARTITION BY c.CategoryId ORDER BY q.n) AS rn
    FROM dbo.TestCategories c CROSS JOIN @qt q
)
INSERT dbo.QuestionBank (SubjectId, CategoryId, TopicId, Prompt, OptionA, OptionB, OptionC, OptionD, CorrectOption, Explanation, Difficulty, IsActive, CreatedBy)
SELECT
    CASE
        WHEN src.Slug IN ('c-programming','data-structures','algorithms','oop','java','python','git-version-control','digital-logic','computer-architecture') THEN (SELECT TOP 1 SubjectId FROM dbo.Subjects WHERE SubjectCode = 'CS101')
        WHEN src.Slug IN ('databases-sql','operating-systems','computer-networks','web-development','software-engineering','cyber-security','cloud-computing','mobile-development') THEN (SELECT TOP 1 SubjectId FROM dbo.Subjects WHERE SubjectCode = 'CS102')
        WHEN src.Slug IN ('artificial-intelligence','machine-learning','data-science','research-methods') THEN (SELECT TOP 1 SubjectId FROM dbo.Subjects WHERE SubjectCode = 'CS103')
        WHEN src.Slug IN ('financial-accounting','cost-accounting','taxation','corporate-finance','financial-literacy','banking-awareness') THEN (SELECT TOP 1 SubjectId FROM dbo.Subjects WHERE SubjectCode = 'BC101')
        WHEN src.Slug IN ('business-economics','marketing-management','human-resource-management','business-law','entrepreneurship','business-communication') THEN (SELECT TOP 1 SubjectId FROM dbo.Subjects WHERE SubjectCode = 'BC102')
        ELSE (SELECT TOP 1 SubjectId FROM dbo.Subjects WHERE SubjectCode = 'GE101')
    END,
    src.CategoryId,
    (SELECT TOP 1 TopicId FROM dbo.TestTopics t WHERE t.CategoryId = src.CategoryId ORDER BY t.SortOrder),
    N'[' + src.CategoryName + N'] ' + src.Prompt,
    src.OA, src.OB, src.OC, src.OD, src.Ans, src.Expl,
    CASE WHEN src.rn % 3 = 0 THEN 'easy' WHEN src.rn % 3 = 1 THEN 'medium' ELSE 'hard' END,
    1, @admin
FROM src
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.QuestionBank x
    WHERE x.CategoryId = src.CategoryId AND x.Prompt = N'[' + src.CategoryName + N'] ' + src.Prompt);
GO

PRINT N'Question bank seeded from 20 templates x categories.';
GO

-- 2. One 20-question practice test per category (60 tests).
DECLARE @admin2 INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Email = N'admin@sms.edu');
IF @admin2 IS NULL SET @admin2 = (SELECT TOP 1 UserId FROM dbo.Users ORDER BY UserId);

INSERT dbo.OnlineTests (CourseId, SubjectId, Title, Instructions, DurationMinutes, MaxAttempts, PassPercent, ShuffleQuestions, ProctoringEnabled, IsPublished, WarningLimit, CreatedBy)
SELECT
    CASE WHEN c.SortOrder BETWEEN 1 AND 34 THEN (SELECT TOP 1 CourseId FROM dbo.Courses WHERE CourseCode = 'BSC-CS')
         WHEN c.SortOrder BETWEEN 35 AND 44 THEN (SELECT TOP 1 CourseId FROM dbo.Courses WHERE CourseCode = 'BCOM')
         ELSE (SELECT TOP 1 CourseId FROM dbo.Courses WHERE CourseCode = 'BBA') END,
    CASE
        WHEN c.Slug IN ('c-programming','data-structures','algorithms','oop','java','python','git-version-control','digital-logic','computer-architecture') THEN (SELECT TOP 1 SubjectId FROM dbo.Subjects WHERE SubjectCode = 'CS101')
        WHEN c.Slug IN ('databases-sql','operating-systems','computer-networks','web-development','software-engineering','cyber-security','cloud-computing','mobile-development') THEN (SELECT TOP 1 SubjectId FROM dbo.Subjects WHERE SubjectCode = 'CS102')
        WHEN c.Slug IN ('artificial-intelligence','machine-learning','data-science','research-methods') THEN (SELECT TOP 1 SubjectId FROM dbo.Subjects WHERE SubjectCode = 'CS103')
        WHEN c.Slug IN ('financial-accounting','cost-accounting','taxation','corporate-finance','financial-literacy','banking-awareness') THEN (SELECT TOP 1 SubjectId FROM dbo.Subjects WHERE SubjectCode = 'BC101')
        WHEN c.Slug IN ('business-economics','marketing-management','human-resource-management','business-law','entrepreneurship','business-communication') THEN (SELECT TOP 1 SubjectId FROM dbo.Subjects WHERE SubjectCode = 'BC102')
        ELSE (SELECT TOP 1 SubjectId FROM dbo.Subjects WHERE SubjectCode = 'GE101') END,
    c.CategoryName + N' - Practice Test',
    N'Answer all 20 questions. One warning is issued before an automatic submit if you leave the test window. Read every question carefully.',
    30, 3, 40, 1, 1, 1, 1, @admin2
FROM dbo.TestCategories c
WHERE NOT EXISTS (SELECT 1 FROM dbo.OnlineTests t WHERE t.Title = c.CategoryName + N' - Practice Test');
GO

-- 3. Link each category's questions to that category's test.
INSERT dbo.OnlineTestQuestions (TestId, QuestionId, Points, SortOrder)
SELECT t.TestId, q.QuestionId, 1,
       ROW_NUMBER() OVER (PARTITION BY t.TestId ORDER BY q.QuestionId)
FROM dbo.OnlineTests t
JOIN dbo.TestCategories c ON t.Title = c.CategoryName + N' - Practice Test'
JOIN dbo.QuestionBank q ON q.CategoryId = c.CategoryId
WHERE NOT EXISTS (SELECT 1 FROM dbo.OnlineTestQuestions x WHERE x.TestId = t.TestId AND x.QuestionId = q.QuestionId);
GO

PRINT N'60 practice tests linked to their questions.';
GO

-- 4. Demo attempts for the seeded student so results screens are populated.
DECLARE @stu INT = (SELECT TOP 1 s.StudentId
                    FROM dbo.Students s JOIN dbo.Users u ON u.UserId = s.UserId
                    WHERE u.Email = N'student@sms.edu');

IF @stu IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.OnlineTestAttempts WHERE StudentId = @stu)
BEGIN
    DECLARE @picks TABLE (TestId INT PRIMARY KEY, RowNum INT);
    INSERT @picks (TestId, RowNum)
    SELECT TestId, ROW_NUMBER() OVER (ORDER BY TestId)
    FROM dbo.OnlineTests
    WHERE CourseId = (SELECT TOP 1 CourseId FROM dbo.Courses WHERE CourseCode = 'BSC-CS')
      AND IsPublished = 1;

    DECLARE @t INT, @rn INT, @attempt INT, @score INT, @max INT;
    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT TestId, RowNum FROM @picks WHERE RowNum <= 6;
    OPEN cur;
    FETCH NEXT FROM cur INTO @t, @rn;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @max = (SELECT COUNT(*) * 1 FROM dbo.OnlineTestQuestions WHERE TestId = @t);
        SET @score = 0;

        INSERT dbo.OnlineTestAttempts (TestId, StudentId, StartedAtUtc, ExpiresAtUtc, SubmittedAtUtc, Score, MaxScore, Status, Violations, LastSavedAtUtc, IntegrityNote)
        VALUES (@t, @stu,
                DATEADD(DAY, -@rn, DATEADD(MINUTE, -40, SYSUTCDATETIME())),
                DATEADD(DAY, -@rn, DATEADD(MINUTE, 10, SYSUTCDATETIME())),
                DATEADD(DAY, -@rn, DATEADD(MINUTE, -12, SYSUTCDATETIME())),
                NULL, @max,
                CASE WHEN @rn = 6 THEN 'auto_submitted' ELSE 'submitted' END,
                CASE WHEN @rn = 6 THEN 2 ELSE 0 END,
                DATEADD(DAY, -@rn, DATEADD(MINUTE, -12, SYSUTCDATETIME())),
                CASE WHEN @rn = 6 THEN N'Auto-submitted after the warning limit was reached (demo).' ELSE NULL END);
        SET @attempt = SCOPE_IDENTITY();

        INSERT dbo.OnlineTestAnswers (AttemptId, QuestionId, SelectedOption, IsCorrect, PointsAwarded, AnsweredAtUtc)
        SELECT @attempt, q.QuestionId,
               CASE WHEN (q.QuestionId + @rn) % 10 < 7 THEN q.CorrectOption ELSE 'A' END,
               CASE WHEN (q.QuestionId + @rn) % 10 < 7 THEN 1 ELSE 0 END,
               CASE WHEN (q.QuestionId + @rn) % 10 < 7 THEN 1 ELSE 0 END,
               DATEADD(DAY, -@rn, DATEADD(MINUTE, -20, SYSUTCDATETIME()))
        FROM dbo.OnlineTestQuestions tq
        JOIN dbo.QuestionBank q ON q.QuestionId = tq.QuestionId
        WHERE tq.TestId = @t;

        SELECT @score = SUM(PointsAwarded) FROM dbo.OnlineTestAnswers WHERE AttemptId = @attempt;
        UPDATE dbo.OnlineTestAttempts SET Score = ISNULL(@score, 0) WHERE AttemptId = @attempt;

        IF @rn = 6
            INSERT dbo.OnlineTestEvents (AttemptId, EventType, Detail, OccurredAtUtc)
            VALUES (@attempt, 'auto_submit', N'Warning limit reached; attempt auto-submitted.', DATEADD(DAY, -6, SYSUTCDATETIME()));

        FETCH NEXT FROM cur INTO @t, @rn;
    END
    CLOSE cur; DEALLOCATE cur;
    PRINT N'Seeded 6 demo attempts.';
END
GO

PRINT N'16_OnlineTestsSeed.sql complete.';
GO



