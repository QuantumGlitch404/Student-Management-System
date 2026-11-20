using System;
using System.Data;
using System.Data.SqlClient;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Data
{
    /// <summary>
    /// Timetable, room, substitution and academic-calendar persistence.
    /// Schedule times are institution-local; all absolute event instants are UTC.
    /// </summary>
    public static class CampusRepo
    {
        public static readonly string[] EventCategories = { "holiday", "exam", "event", "deadline", "academic", "other" };
        public static readonly string[] EventAudiences = { "all", "students", "faculty", "admin", "course" };

        // ---------------- timetable reads ----------------

        public static DataTable Rooms()
        {
            return SqlHelper.FillDataTable(
                "SELECT RoomId, RoomCode, RoomName, Building, Capacity, IsActive FROM dbo.Rooms ORDER BY RoomCode");
        }

        public static DataTable Periods()
        {
            return SqlHelper.FillDataTable(
                "SELECT PeriodId, PeriodName, StartTime, EndTime, SortOrder, IsActive " +
                "FROM dbo.TimetablePeriods WHERE IsActive = 1 ORDER BY SortOrder");
        }

        public static DataTable Entries(int? courseId, int? semester, string section, int? facultyUserId, bool activeOnly = false)
        {
            return SqlHelper.FillDataTable(
                "SELECT e.EntryId, e.CourseId, e.SemesterNumber, e.SectionCode, e.SubjectId, e.FacultyUserId, " +
                "e.RoomId, e.PeriodId, e.DayOfWeek, e.SessionId, e.EffectiveFrom, e.EffectiveTo, e.Notes, e.IsActive, " +
                "e.CreatedAtUtc, c.CourseCode, c.CourseName, s.SubjectCode, s.SubjectName, " +
                "p.PeriodName, p.StartTime, p.EndTime, p.SortOrder, r.RoomCode, r.RoomName, u.FullName AS FacultyName " +
                "FROM dbo.TimetableEntries e " +
                "JOIN dbo.Courses c ON c.CourseId = e.CourseId " +
                "JOIN dbo.Subjects s ON s.SubjectId = e.SubjectId " +
                "JOIN dbo.TimetablePeriods p ON p.PeriodId = e.PeriodId " +
                "JOIN dbo.Users u ON u.UserId = e.FacultyUserId " +
                "LEFT JOIN dbo.Rooms r ON r.RoomId = e.RoomId " +
                "WHERE (@CourseId IS NULL OR e.CourseId = @CourseId) " +
                "  AND (@Semester IS NULL OR e.SemesterNumber = @Semester) " +
                "  AND (@Section = '' OR e.SectionCode = @Section) " +
                "  AND (@FacultyUserId IS NULL OR e.FacultyUserId = @FacultyUserId) " +
                "  AND (@ActiveOnly = 0 OR e.IsActive = 1) " +
                "ORDER BY e.DayOfWeek, p.SortOrder, c.CourseCode, e.SemesterNumber, e.SectionCode",
                p =>
                {
                    p.AddWithValue("@CourseId", (object)courseId ?? DBNull.Value);
                    p.AddWithValue("@Semester", (object)semester ?? DBNull.Value);
                    p.AddWithValue("@Section", (section ?? "").Trim());
                    p.AddWithValue("@FacultyUserId", (object)facultyUserId ?? DBNull.Value);
                    p.AddWithValue("@ActiveOnly", activeOnly ? 1 : 0);
                });
        }

        public static DataTable EntriesForStudent(int userId)
        {
            return SqlHelper.FillDataTable(
                "SELECT e.EntryId, e.CourseId, e.SemesterNumber, e.SectionCode, e.SubjectId, e.FacultyUserId, " +
                "e.RoomId, e.PeriodId, e.DayOfWeek, e.SessionId, e.EffectiveFrom, e.EffectiveTo, e.Notes, e.IsActive, " +
                "c.CourseCode, c.CourseName, s.SubjectCode, s.SubjectName, p.PeriodName, p.StartTime, p.EndTime, " +
                "p.SortOrder, r.RoomCode, r.RoomName, u.FullName AS FacultyName " +
                "FROM dbo.Students st " +
                "JOIN dbo.TimetableEntries e ON e.CourseId = st.CourseId AND e.SemesterNumber = st.SemesterNumber " +
                "JOIN dbo.Courses c ON c.CourseId = e.CourseId " +
                "JOIN dbo.Subjects s ON s.SubjectId = e.SubjectId " +
                "JOIN dbo.TimetablePeriods p ON p.PeriodId = e.PeriodId " +
                "JOIN dbo.Users u ON u.UserId = e.FacultyUserId " +
                "LEFT JOIN dbo.Rooms r ON r.RoomId = e.RoomId " +
                "WHERE st.UserId = @UserId AND e.IsActive = 1 " +
                "ORDER BY e.DayOfWeek, p.SortOrder, e.SectionCode",
                p => p.AddWithValue("@UserId", userId));
        }

        public static DataRow Entry(int entryId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT e.*, c.CourseCode, s.SubjectCode, p.PeriodName, p.StartTime, p.EndTime, u.FullName AS FacultyName " +
                "FROM dbo.TimetableEntries e JOIN dbo.Courses c ON c.CourseId=e.CourseId " +
                "JOIN dbo.Subjects s ON s.SubjectId=e.SubjectId " +
                "JOIN dbo.TimetablePeriods p ON p.PeriodId=e.PeriodId " +
                "JOIN dbo.Users u ON u.UserId=e.FacultyUserId WHERE e.EntryId=@Id",
                p => p.AddWithValue("@Id", entryId));
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        public static DataTable ChangesForUser(int userId, string role, DateTime fromDate, DateTime toDate)
        {
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return SqlHelper.FillDataTable(
                    "SELECT ch.ChangeId, ch.EntryId, ch.ChangeDate, ch.ChangeKind, ch.SubstituteFacultyUserId, ch.Reason, " +
                    "ch.CreatedAtUtc, c.CourseCode, s.SubjectCode, p.PeriodName, u.FullName AS SubstituteName " +
                    "FROM dbo.TimetableChanges ch JOIN dbo.TimetableEntries e ON e.EntryId=ch.EntryId " +
                    "JOIN dbo.Courses c ON c.CourseId=e.CourseId JOIN dbo.Subjects s ON s.SubjectId=e.SubjectId " +
                    "JOIN dbo.TimetablePeriods p ON p.PeriodId=e.PeriodId " +
                    "LEFT JOIN dbo.Users u ON u.UserId=ch.SubstituteFacultyUserId " +
                    "WHERE ch.ChangeDate BETWEEN @From AND @To ORDER BY ch.ChangeDate, p.SortOrder",
                    p => { p.AddWithValue("@From", fromDate.Date); p.AddWithValue("@To", toDate.Date); });
            }

            if (string.Equals(role, "Faculty", StringComparison.OrdinalIgnoreCase))
            {
                return SqlHelper.FillDataTable(
                    "SELECT ch.ChangeId, ch.EntryId, ch.ChangeDate, ch.ChangeKind, ch.SubstituteFacultyUserId, ch.Reason, " +
                    "ch.CreatedAtUtc, c.CourseCode, s.SubjectCode, p.PeriodName, u.FullName AS SubstituteName " +
                    "FROM dbo.TimetableChanges ch JOIN dbo.TimetableEntries e ON e.EntryId=ch.EntryId " +
                    "JOIN dbo.Courses c ON c.CourseId=e.CourseId JOIN dbo.Subjects s ON s.SubjectId=e.SubjectId " +
                    "JOIN dbo.TimetablePeriods p ON p.PeriodId=e.PeriodId " +
                    "LEFT JOIN dbo.Users u ON u.UserId=ch.SubstituteFacultyUserId " +
                    "WHERE ch.ChangeDate BETWEEN @From AND @To AND (e.FacultyUserId=@UserId OR ch.SubstituteFacultyUserId=@UserId) " +
                    "ORDER BY ch.ChangeDate, p.SortOrder",
                    p => { p.AddWithValue("@From", fromDate.Date); p.AddWithValue("@To", toDate.Date); p.AddWithValue("@UserId", userId); });
            }

            return SqlHelper.FillDataTable(
                "SELECT ch.ChangeId, ch.EntryId, ch.ChangeDate, ch.ChangeKind, ch.SubstituteFacultyUserId, ch.Reason, " +
                "ch.CreatedAtUtc, c.CourseCode, s.SubjectCode, p.PeriodName, u.FullName AS SubstituteName " +
                "FROM dbo.Students st JOIN dbo.TimetableEntries e ON e.CourseId=st.CourseId AND e.SemesterNumber=st.SemesterNumber " +
                "JOIN dbo.TimetableChanges ch ON ch.EntryId=e.EntryId " +
                "JOIN dbo.Courses c ON c.CourseId=e.CourseId JOIN dbo.Subjects s ON s.SubjectId=e.SubjectId " +
                "JOIN dbo.TimetablePeriods p ON p.PeriodId=e.PeriodId " +
                "LEFT JOIN dbo.Users u ON u.UserId=ch.SubstituteFacultyUserId " +
                "WHERE st.UserId=@UserId AND ch.ChangeDate BETWEEN @From AND @To " +
                "ORDER BY ch.ChangeDate, p.SortOrder",
                p => { p.AddWithValue("@From", fromDate.Date); p.AddWithValue("@To", toDate.Date); p.AddWithValue("@UserId", userId); });
        }

        // ---------------- timetable writes ----------------

        public static int CreateEntry(int courseId, int semester, string section, int subjectId, int facultyUserId,
            int? roomId, int periodId, int dayOfWeek, int? sessionId, DateTime effectiveFrom, DateTime? effectiveTo,
            string notes, int actorId)
        {
            ValidateEntry(courseId, semester, section, subjectId, facultyUserId, dayOfWeek, effectiveFrom, effectiveTo);
            string conflict = Conflict(0, courseId, semester, section, facultyUserId, roomId, periodId, dayOfWeek, effectiveFrom, effectiveTo);
            if (conflict != null) throw new Exception(conflict);

            int id = SqlHelper.ExecuteScalarInt(
                "INSERT dbo.TimetableEntries (CourseId, SemesterNumber, SectionCode, SubjectId, FacultyUserId, RoomId, " +
                "PeriodId, DayOfWeek, SessionId, EffectiveFrom, EffectiveTo, Notes, CreatedBy) " +
                "VALUES (@CourseId, @Semester, @Section, @SubjectId, @Faculty, @Room, @Period, @Day, @Session, " +
                "@From, @To, @Notes, @By); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                p => AddEntryParams(p, courseId, semester, section, subjectId, facultyUserId, roomId, periodId, dayOfWeek,
                    sessionId, effectiveFrom, effectiveTo, notes, actorId));
            AuditService.Log("TimetableEntryCreated", "TimetableEntries", id,
                subjectId + " day " + dayOfWeek + " period " + periodId);
            return id;
        }

        public static void UpdateEntry(int entryId, int courseId, int semester, string section, int subjectId,
            int facultyUserId, int? roomId, int periodId, int dayOfWeek, int? sessionId, DateTime effectiveFrom,
            DateTime? effectiveTo, string notes, int actorId)
        {
            ValidateEntry(courseId, semester, section, subjectId, facultyUserId, dayOfWeek, effectiveFrom, effectiveTo);
            string conflict = Conflict(entryId, courseId, semester, section, facultyUserId, roomId, periodId, dayOfWeek, effectiveFrom, effectiveTo);
            if (conflict != null) throw new Exception(conflict);

            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.TimetableEntries SET CourseId=@CourseId, SemesterNumber=@Semester, SectionCode=@Section, " +
                "SubjectId=@SubjectId, FacultyUserId=@Faculty, RoomId=@Room, PeriodId=@Period, DayOfWeek=@Day, " +
                "SessionId=@Session, EffectiveFrom=@From, EffectiveTo=@To, Notes=@Notes WHERE EntryId=@Id",
                p =>
                {
                    AddEntryParams(p, courseId, semester, section, subjectId, facultyUserId, roomId, periodId, dayOfWeek,
                        sessionId, effectiveFrom, effectiveTo, notes, actorId);
                    p.AddWithValue("@Id", entryId);
                });
            AuditService.Log("TimetableEntryUpdated", "TimetableEntries", entryId,
                subjectId + " day " + dayOfWeek + " period " + periodId);
        }

        public static void SetEntryActive(int entryId, bool active)
        {
            SqlHelper.ExecuteNonQuery("UPDATE dbo.TimetableEntries SET IsActive=@Active WHERE EntryId=@Id",
                p => { p.AddWithValue("@Active", active ? 1 : 0); p.AddWithValue("@Id", entryId); });
            AuditService.Log(active ? "TimetableEntryEnabled" : "TimetableEntryDisabled", "TimetableEntries", entryId);
        }

        public static void DeleteEntry(int entryId)
        {
            SqlHelper.ExecuteNonQuery("DELETE FROM dbo.TimetableEntries WHERE EntryId=@Id",
                p => p.AddWithValue("@Id", entryId));
            AuditService.Log("TimetableEntryDeleted", "TimetableEntries", entryId);
        }

        public static int AddChange(int entryId, DateTime changeDate, string kind, int? substituteUserId,
            string reason, int actorId)
        {
            kind = (kind ?? "").Trim().ToLowerInvariant();
            if (kind != "substitution" && kind != "cancellation") throw new Exception("Choose a valid schedule change.");
            DataRow entry = Entry(entryId);
            if (entry == null) throw new Exception("Timetable entry not found.");
            if (kind == "substitution")
            {
                if (!substituteUserId.HasValue || substituteUserId.Value <= 0)
                    throw new Exception("Choose the substitute faculty member.");
                int isFaculty = SqlHelper.ExecuteScalarInt(
                    "SELECT COUNT(*) FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.RoleId=ur.RoleId " +
                    "WHERE ur.UserId=@Id AND r.RoleName='Faculty'",
                    p => p.AddWithValue("@Id", substituteUserId.Value));
                if (isFaculty == 0) throw new Exception("The substitute must be an active faculty account.");
            }
            reason = (reason ?? "").Trim();
            if (reason.Length > 300) reason = reason.Substring(0, 300);

            int id = SqlHelper.ExecuteScalarInt(
                "IF EXISTS (SELECT 1 FROM dbo.TimetableChanges WHERE EntryId=@Entry AND ChangeDate=@Date) " +
                "  UPDATE dbo.TimetableChanges SET ChangeKind=@Kind, SubstituteFacultyUserId=@Sub, Reason=@Reason, " +
                "  CreatedBy=@By, CreatedAtUtc=SYSUTCDATETIME() WHERE EntryId=@Entry AND ChangeDate=@Date " +
                "ELSE " +
                "  INSERT dbo.TimetableChanges (EntryId, ChangeDate, ChangeKind, SubstituteFacultyUserId, Reason, CreatedBy) " +
                "  VALUES (@Entry, @Date, @Kind, @Sub, @Reason, @By); " +
                "SELECT ChangeId FROM dbo.TimetableChanges WHERE EntryId=@Entry AND ChangeDate=@Date;",
                p =>
                {
                    p.AddWithValue("@Entry", entryId);
                    p.AddWithValue("@Date", changeDate.Date);
                    p.AddWithValue("@Kind", kind);
                    p.AddWithValue("@Sub", (object)substituteUserId ?? DBNull.Value);
                    p.AddWithValue("@Reason", reason.Length == 0 ? (object)DBNull.Value : reason);
                    p.AddWithValue("@By", actorId);
                });

            int courseId = Convert.ToInt32(entry["CourseId"]);
            string subject = Convert.ToString(entry["SubjectCode"]);
            string message = kind == "cancellation"
                ? "The " + subject + " class on " + changeDate.ToString("yyyy-MM-dd") + " is cancelled." + (reason.Length == 0 ? "" : " " + reason)
                : "The " + subject + " class on " + changeDate.ToString("yyyy-MM-dd") + " has a substitute faculty member." + (reason.Length == 0 ? "" : " " + reason);
            try
            {
                CommsRepo.CreateNotification(kind == "cancellation" ? "Class cancelled" : "Class substitution",
                    message, "academic", "/Timetable/Index.aspx", actorId, "course:" + courseId,
                    id, "timetable-change:" + id, true);
            }
            catch (Exception ex) { Global.WriteLog("Timetable", "change notification failed", ex); }
            AuditService.Log(kind == "cancellation" ? "TimetableClassCancelled" : "TimetableClassSubstituted",
                "TimetableChanges", id, "entry " + entryId + " on " + changeDate.ToString("yyyy-MM-dd"));
            return id;
        }

        private static void AddEntryParams(SqlParameterCollection p, int courseId, int semester, string section,
            int subjectId, int facultyUserId, int? roomId, int periodId, int dayOfWeek, int? sessionId,
            DateTime effectiveFrom, DateTime? effectiveTo, string notes, int actorId)
        {
            p.AddWithValue("@CourseId", courseId);
            p.AddWithValue("@Semester", semester);
            p.AddWithValue("@Section", string.IsNullOrWhiteSpace(section) ? "A" : section.Trim().ToUpperInvariant());
            p.AddWithValue("@SubjectId", subjectId);
            p.AddWithValue("@Faculty", facultyUserId);
            p.AddWithValue("@Room", (object)roomId ?? DBNull.Value);
            p.AddWithValue("@Period", periodId);
            p.AddWithValue("@Day", dayOfWeek);
            p.AddWithValue("@Session", (object)sessionId ?? DBNull.Value);
            p.AddWithValue("@From", effectiveFrom.Date);
            p.AddWithValue("@To", effectiveTo.HasValue ? (object)effectiveTo.Value.Date : DBNull.Value);
            p.AddWithValue("@Notes", string.IsNullOrWhiteSpace(notes) ? (object)DBNull.Value : notes.Trim());
            p.AddWithValue("@By", actorId);
        }

        private static void ValidateEntry(int courseId, int semester, string section, int subjectId, int facultyUserId,
            int dayOfWeek, DateTime effectiveFrom, DateTime? effectiveTo)
        {
            if (courseId <= 0 || subjectId <= 0) throw new Exception("Choose a course and subject.");
            if (semester < 1 || semester > 12) throw new Exception("Semester must be 1-12.");
            if (string.IsNullOrWhiteSpace(section)) throw new Exception("Enter a section code.");
            if (section.Trim().Length > 20) throw new Exception("Section code is too long.");
            if (facultyUserId <= 0) throw new Exception("Choose the faculty member.");
            if (dayOfWeek < 1 || dayOfWeek > 7) throw new Exception("Choose a weekday.");
            if (effectiveTo.HasValue && effectiveTo.Value.Date < effectiveFrom.Date)
                throw new Exception("The end date must be on or after the start date.");
        }

        /// <summary>Detects faculty, room or section double-booking across overlapping effective dates.</summary>
        public static string Conflict(int entryId, int courseId, int semester, string section, int facultyUserId,
            int? roomId, int periodId, int dayOfWeek, DateTime effectiveFrom, DateTime? effectiveTo)
        {
            string sectionCode = string.IsNullOrWhiteSpace(section) ? "A" : section.Trim().ToUpperInvariant();
            DataTable clashes = SqlHelper.FillDataTable(
                "SELECT TOP 5 e.EntryId, c.CourseCode, s.SubjectCode, p.PeriodName, u.FullName AS FacultyName, r.RoomCode " +
                "FROM dbo.TimetableEntries e JOIN dbo.Courses c ON c.CourseId=e.CourseId " +
                "JOIN dbo.Subjects s ON s.SubjectId=e.SubjectId " +
                "JOIN dbo.TimetablePeriods p ON p.PeriodId=e.PeriodId " +
                "JOIN dbo.Users u ON u.UserId=e.FacultyUserId LEFT JOIN dbo.Rooms r ON r.RoomId=e.RoomId " +
                "WHERE e.IsActive=1 AND e.EntryId<>@Id AND e.DayOfWeek=@Day AND e.PeriodId=@Period " +
                "  AND (@From <= ISNULL(e.EffectiveTo, '9999-12-31') AND (@To IS NULL OR @To >= e.EffectiveFrom)) " +
                "  AND (e.FacultyUserId=@Faculty OR (@Room IS NOT NULL AND e.RoomId=@Room) " +
                "       OR (e.CourseId=@CourseId AND e.SemesterNumber=@Semester AND e.SectionCode=@Section))",
                p =>
                {
                    p.AddWithValue("@Id", entryId);
                    p.AddWithValue("@Day", dayOfWeek);
                    p.AddWithValue("@Period", periodId);
                    p.AddWithValue("@From", effectiveFrom.Date);
                    p.AddWithValue("@To", effectiveTo.HasValue ? (object)effectiveTo.Value.Date : DBNull.Value);
                    p.AddWithValue("@Faculty", facultyUserId);
                    p.AddWithValue("@Room", (object)roomId ?? DBNull.Value);
                    p.AddWithValue("@CourseId", courseId);
                    p.AddWithValue("@Semester", semester);
                    p.AddWithValue("@Section", sectionCode);
                });
            if (clashes.Rows.Count == 0) return null;
            DataRow r = clashes.Rows[0];
            return "Schedule conflict: " + Convert.ToString(r["CourseCode"]) + " " + Convert.ToString(r["SubjectCode"]) +
                   " already uses " + (r["RoomCode"] == DBNull.Value ? "this slot" : Convert.ToString(r["RoomCode"])) +
                   " with " + Convert.ToString(r["FacultyName"]) + " in " + Convert.ToString(r["PeriodName"]) + ".";
        }

        // ---------------- calendar reads ----------------

        public static DataTable CalendarForUser(int userId, string role, DateTime fromUtc, DateTime toUtc)
        {
            int courseId = 0;
            if (string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase))
            {
                courseId = SqlHelper.ExecuteScalarInt(
                    "SELECT CourseId FROM dbo.Students WHERE UserId=@UserId",
                    p => p.AddWithValue("@UserId", userId));
            }
            bool admin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
            bool student = string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase);
            bool faculty = string.Equals(role, "Faculty", StringComparison.OrdinalIgnoreCase);

            return SqlHelper.FillDataTable(
                "SELECT ev.EventId, ev.Title, ev.Description, ev.Category, ev.Audience, ev.CourseId, ev.SemesterNumber, " +
                "ev.StartsAtUtc, ev.EndsAtUtc, ev.IsAllDay, ev.Location, ev.IsActive, ev.CreatedAtUtc, c.CourseCode " +
                "FROM dbo.CalendarEvents ev LEFT JOIN dbo.Courses c ON c.CourseId=ev.CourseId " +
                "WHERE ev.IsActive=1 AND ev.EndsAtUtc >= @From AND ev.StartsAtUtc < @To " +
                "  AND (@Admin=1 OR ev.Audience='all' " +
                "       OR (@Student=1 AND (ev.Audience='students' OR (ev.Audience='course' AND ev.CourseId=@CourseId))) " +
                "       OR (@Faculty=1 AND ev.Audience='faculty')) " +
                "ORDER BY ev.StartsAtUtc, ev.EventId",
                p =>
                {
                    p.AddWithValue("@From", fromUtc);
                    p.AddWithValue("@To", toUtc);
                    p.AddWithValue("@Admin", admin ? 1 : 0);
                    p.AddWithValue("@Student", student ? 1 : 0);
                    p.AddWithValue("@Faculty", faculty ? 1 : 0);
                    p.AddWithValue("@CourseId", courseId);
                });
        }

        public static DataTable EventsAdmin(int limit = 200)
        {
            return SqlHelper.FillDataTable(
                "SELECT TOP (@Take) ev.EventId, ev.Title, ev.Description, ev.Category, ev.Audience, ev.CourseId, " +
                "ev.SemesterNumber, ev.StartsAtUtc, ev.EndsAtUtc, ev.IsAllDay, ev.Location, ev.IsActive, ev.CreatedAtUtc, " +
                "c.CourseCode, u.FullName AS CreatedByName " +
                "FROM dbo.CalendarEvents ev LEFT JOIN dbo.Courses c ON c.CourseId=ev.CourseId " +
                "LEFT JOIN dbo.Users u ON u.UserId=ev.CreatedBy ORDER BY ev.StartsAtUtc DESC, ev.EventId DESC",
                p => p.AddWithValue("@Take", limit < 1 ? 50 : (limit > 500 ? 500 : limit)));
        }

        public static DataRow Event(int eventId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT ev.*, c.CourseCode FROM dbo.CalendarEvents ev LEFT JOIN dbo.Courses c ON c.CourseId=ev.CourseId " +
                "WHERE ev.EventId=@Id", p => p.AddWithValue("@Id", eventId));
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        // ---------------- calendar writes ----------------

        public static int CreateEvent(string title, string description, string category, string audience,
            int? courseId, int? semester, DateTime startsUtc, DateTime endsUtc, bool allDay, string location, int actorId)
        {
            ValidateEvent(title, category, audience, courseId, startsUtc, endsUtc);
            int id = SqlHelper.ExecuteScalarInt(
                "INSERT dbo.CalendarEvents (Title, Description, Category, Audience, CourseId, SemesterNumber, " +
                "StartsAtUtc, EndsAtUtc, IsAllDay, Location, CreatedBy) VALUES (@Title, @Description, @Category, " +
                "@Audience, @CourseId, @Semester, @Starts, @Ends, @AllDay, @Location, @By); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                p => AddEventParams(p, title, description, category, audience, courseId, semester, startsUtc, endsUtc,
                    allDay, location, actorId));
            AuditService.Log("CalendarEventCreated", "CalendarEvents", id, title);
            NotifyEvent(id, title, description, audience, courseId, actorId);
            return id;
        }

        public static void UpdateEvent(int eventId, string title, string description, string category, string audience,
            int? courseId, int? semester, DateTime startsUtc, DateTime endsUtc, bool allDay, string location, int actorId)
        {
            ValidateEvent(title, category, audience, courseId, startsUtc, endsUtc);
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.CalendarEvents SET Title=@Title, Description=@Description, Category=@Category, " +
                "Audience=@Audience, CourseId=@CourseId, SemesterNumber=@Semester, StartsAtUtc=@Starts, " +
                "EndsAtUtc=@Ends, IsAllDay=@AllDay, Location=@Location WHERE EventId=@Id",
                p =>
                {
                    AddEventParams(p, title, description, category, audience, courseId, semester, startsUtc, endsUtc,
                        allDay, location, actorId);
                    p.AddWithValue("@Id", eventId);
                });
            AuditService.Log("CalendarEventUpdated", "CalendarEvents", eventId, title);
        }

        public static void SetEventActive(int eventId, bool active)
        {
            SqlHelper.ExecuteNonQuery("UPDATE dbo.CalendarEvents SET IsActive=@Active WHERE EventId=@Id",
                p => { p.AddWithValue("@Active", active ? 1 : 0); p.AddWithValue("@Id", eventId); });
            AuditService.Log(active ? "CalendarEventEnabled" : "CalendarEventDisabled", "CalendarEvents", eventId);
        }

        public static void DeleteEvent(int eventId)
        {
            SqlHelper.ExecuteNonQuery("DELETE FROM dbo.CalendarEvents WHERE EventId=@Id",
                p => p.AddWithValue("@Id", eventId));
            AuditService.Log("CalendarEventDeleted", "CalendarEvents", eventId);
        }

        private static void AddEventParams(SqlParameterCollection p, string title, string description, string category,
            string audience, int? courseId, int? semester, DateTime startsUtc, DateTime endsUtc, bool allDay,
            string location, int actorId)
        {
            p.AddWithValue("@Title", title.Trim());
            p.AddWithValue("@Description", string.IsNullOrWhiteSpace(description) ? (object)DBNull.Value : description.Trim());
            p.AddWithValue("@Category", NormalizeCategory(category));
            p.AddWithValue("@Audience", NormalizeAudience(audience));
            p.AddWithValue("@CourseId", (object)courseId ?? DBNull.Value);
            p.AddWithValue("@Semester", (object)semester ?? DBNull.Value);
            p.AddWithValue("@Starts", startsUtc);
            p.AddWithValue("@Ends", endsUtc);
            p.AddWithValue("@AllDay", allDay ? 1 : 0);
            p.AddWithValue("@Location", string.IsNullOrWhiteSpace(location) ? (object)DBNull.Value : location.Trim());
            p.AddWithValue("@By", actorId);
        }

        private static void ValidateEvent(string title, string category, string audience, int? courseId,
            DateTime startsUtc, DateTime endsUtc)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new Exception("Enter an event title.");
            if (title.Trim().Length > 160) throw new Exception("The title is too long.");
            if (Array.IndexOf(EventCategories, NormalizeCategory(category)) < 0) throw new Exception("Choose a valid category.");
            string aud = NormalizeAudience(audience);
            if (Array.IndexOf(EventAudiences, aud) < 0) throw new Exception("Choose a valid audience.");
            if (aud == "course" && (!courseId.HasValue || courseId.Value <= 0)) throw new Exception("Choose the course for this event.");
            if (endsUtc <= startsUtc) throw new Exception("The end time must be after the start time.");
        }

        public static string NormalizeCategory(string value)
        {
            string v = (value ?? "").Trim().ToLowerInvariant();
            return Array.IndexOf(EventCategories, v) >= 0 ? v : "other";
        }

        public static string NormalizeAudience(string value)
        {
            string v = (value ?? "").Trim().ToLowerInvariant();
            return Array.IndexOf(EventAudiences, v) >= 0 ? v : "all";
        }

        private static void NotifyEvent(int eventId, string title, string description, string audience,
            int? courseId, int actorId)
        {
            string target = NormalizeAudience(audience);
            if (target == "course") target = "course:" + (courseId ?? 0);
            try
            {
                CommsRepo.CreateNotification(title, string.IsNullOrWhiteSpace(description) ? title : description,
                    "academic", "/Calendar/Index.aspx?event=" + eventId, actorId, target, eventId,
                    "calendar-event:" + eventId, true);
            }
            catch (Exception ex) { Global.WriteLog("Calendar", "event notification failed for " + eventId, ex); }
        }
    }
}
