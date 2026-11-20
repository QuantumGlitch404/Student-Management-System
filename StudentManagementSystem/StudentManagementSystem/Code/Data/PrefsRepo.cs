using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using NodaTime;
using StudentManagementSystem.Data;

namespace StudentManagementSystem.Data
{
    /// <summary>One user's stored UI/accessibility/notification preferences.</summary>
    public class UserPrefs
    {
        public int UserId;
        public string Theme = "light";      // light | dark | system
        public int FontScale = 100;         // 85..140 (step 5)
        public string Motion = "full";      // full | reduced
        public bool HighContrast;
        public bool SidebarCollapsed;
        public int Zoom = 100;              // 80..150 (step 10)
        public bool AiEnabled = true;
        public bool AiContext = true;
        public string AiResponseLength = "balanced";
        public string QuietHours;
        public bool NotifSound;
        public string DateFormat = "yyyy-MM-dd";
        public int TablePageSize = 20;
        public string ReportFormat = "pdf";
        public string AcademicYear;
        public string Semester;
        public string DefaultDashboard;
        public string DisabledNotifTypes = "";
        public string TimezoneId;
        public string ClockFormat = "24";
        public bool TickerEnabled = true;
        public string TickerSpeed = "slow";
        public int ReminderLeadMinutes = 15;
    }

    /// <summary>
    /// Per-user appearance and notification preferences.
    ///
    /// The theme/font/motion triple is also mirrored into a cookie so the
    /// anonymous login/registration pages (which have no identity yet) can honour
    /// the last choice and so the first paint after sign-in is correct before any
    /// database round trip. Cookies are only a cache: for signed-in users the
    /// database row is authoritative and overwrites the cookie on the next page.
    /// </summary>
    public static class PrefsRepo
    {
        public const string CookieTheme = "sms_theme";
        public const string CookieFont = "sms_font";
        public const string CookieMotion = "sms_motion";
        public const string CookieZoom = "sms_zoom";

        public static readonly string[] NotifTypes = { "general", "academic", "payment", "complaint", "system" };

        public static readonly string[] DateFormats = { "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "dd MMM yyyy" };
        public static readonly int[] PageSizes = { 10, 20, 50, 100 };
        public static readonly string[] ReportFormats = { "pdf", "xlsx", "csv" };
        public static readonly int[] ZoomLevels = { 80, 90, 100, 110, 120, 130, 140, 150 };
        public static readonly string[] AiLengths = { "short", "balanced", "detailed" };
        public static readonly string[] TickerSpeeds = { "slow", "normal", "fast" };
        public static readonly string[] ClockFormats = { "24", "12" };
        public static readonly int[] ReminderLeads = { 0, 5, 15, 30, 60 };

        // ---------------- read ----------------

        /// <summary>True when the user already has a stored preferences row.</summary>
        public static bool Exists(int userId)
        {
            try
            {
                return SqlHelper.ExecuteScalarInt(
                    "SELECT COUNT(*) FROM dbo.UserPreferences WHERE UserId = @UserId",
                    p => p.AddWithValue("@UserId", userId)) > 0;
            }
            catch (Exception) { return true; }   // no table: never try to adopt
        }

        public static UserPrefs Get(int userId)
        {
            DataTable dt;
            try
            {
                dt = SqlHelper.FillDataTable(
                    "SELECT UserId, Theme, FontScale, Motion, HighContrast, SidebarCollapsed, DateFormat, TablePageSize, " +
                    "ReportFormat, AcademicYear, Semester, DefaultDashboard, DisabledNotifTypes, " +
                    "Zoom, AiEnabled, AiContext, AiResponseLength, QuietHours, NotifSound, " +
                    "TimezoneId, ClockFormat, TickerEnabled, TickerSpeed, ReminderLeadMinutes " +
                    "FROM dbo.UserPreferences WHERE UserId = @UserId",
                    p => p.AddWithValue("@UserId", userId));
            }
            catch (Exception)
            {
                // A pre-1.2 database without the table must not break every page.
                return new UserPrefs { UserId = userId };
            }

            if (dt.Rows.Count == 0)
            {
                var created = new UserPrefs { UserId = userId };
                try { EnsureRow(userId); } catch (Exception) { /* best effort */ }
                return created;
            }

            DataRow r = dt.Rows[0];
            return new UserPrefs
            {
                UserId = userId,
                Theme = NormalizeTheme(r["Theme"] as string),
                FontScale = NormalizeScale(r["FontScale"] == DBNull.Value ? 100 : Convert.ToInt32(r["FontScale"])),
                Motion = NormalizeMotion(r["Motion"] as string),
                HighContrast = Convert.ToBoolean(r["HighContrast"]),
                SidebarCollapsed = Convert.ToBoolean(r["SidebarCollapsed"]),
                DateFormat = NormalizeDateFormat(r["DateFormat"] as string),
                TablePageSize = NormalizePageSize(r["TablePageSize"] == DBNull.Value ? 20 : Convert.ToInt32(r["TablePageSize"])),
                ReportFormat = NormalizeReportFormat(r["ReportFormat"] as string),
                AcademicYear = r["AcademicYear"] as string,
                Semester = r["Semester"] as string,
                DefaultDashboard = r["DefaultDashboard"] as string,
                DisabledNotifTypes = (r["DisabledNotifTypes"] as string) ?? "",
                Zoom = NormalizeZoom(r["Zoom"] == DBNull.Value ? 100 : Convert.ToInt32(r["Zoom"])),
                AiEnabled = Convert.ToBoolean(r["AiEnabled"]),
                AiContext = Convert.ToBoolean(r["AiContext"]),
                AiResponseLength = NormalizeAiLength(r["AiResponseLength"] as string),
                QuietHours = r["QuietHours"] as string,
                NotifSound = Convert.ToBoolean(r["NotifSound"]),
                TimezoneId = NormalizeTimezone(r["TimezoneId"] as string),
                ClockFormat = NormalizeClockFormat(r["ClockFormat"] as string),
                TickerEnabled = Convert.ToBoolean(r["TickerEnabled"]),
                TickerSpeed = NormalizeTickerSpeed(r["TickerSpeed"] as string),
                ReminderLeadMinutes = NormalizeLead(r["ReminderLeadMinutes"] == DBNull.Value ? 15 : Convert.ToInt32(r["ReminderLeadMinutes"]))
            };
        }

        /// <summary>The anonymous fallback: cookie values, validated against the same allow-lists.</summary>
        public static UserPrefs FromCookies(HttpContext ctx)
        {
            var p = new UserPrefs();
            if (ctx == null) return p;
            var theme = ctx.Request.Cookies[CookieTheme];
            var font = ctx.Request.Cookies[CookieFont];
            var motion = ctx.Request.Cookies[CookieMotion];
            var zoom = ctx.Request.Cookies[CookieZoom];
            if (theme != null) p.Theme = NormalizeTheme(theme.Value);
            if (zoom != null) { int z; if (int.TryParse(zoom.Value, out z)) p.Zoom = NormalizeZoom(z); }
            if (font != null) { int f; if (int.TryParse(font.Value, out f)) p.FontScale = NormalizeScale(f); }
            if (motion != null) p.Motion = NormalizeMotion(motion.Value);
            return p;
        }

        /// <summary>Writes the cookie mirror used by the anonymous auth pages.</summary>
        public static void WriteCookie(HttpContext ctx, string theme, int fontScale, string motion, int zoom = 0)
        {
            if (ctx == null) return;
            var expire = DateTime.UtcNow.AddDays(365);
            SetCookie(ctx, CookieTheme, NormalizeTheme(theme), expire);
            SetCookie(ctx, CookieFont, NormalizeScale(fontScale).ToString(), expire);
            SetCookie(ctx, CookieMotion, NormalizeMotion(motion), expire);
            if (zoom > 0) SetCookie(ctx, CookieZoom, NormalizeZoom(zoom).ToString(), expire);
        }

        private static void SetCookie(HttpContext ctx, string name, string value, DateTime expire)
        {
            var c = new HttpCookie(name, value) { Expires = expire, HttpOnly = false, Path = "/" };
            ctx.Response.Cookies.Set(c);
        }

        // ---------------- write ----------------

        public static void SaveAppearance(int userId, string theme, int fontScale, string motion)
        {
            EnsureRow(userId);
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.UserPreferences SET Theme = @Theme, FontScale = @Font, Motion = @Motion, " +
                "UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @UserId",
                p =>
                {
                    p.AddWithValue("@Theme", NormalizeTheme(theme));
                    p.AddWithValue("@Font", NormalizeScale(fontScale));
                    p.AddWithValue("@Motion", NormalizeMotion(motion));
                    p.AddWithValue("@UserId", userId);
                });
        }

        public static void SaveZoom(int userId, int zoom)
        {
            EnsureRow(userId);
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.UserPreferences SET Zoom = @Zoom, UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @UserId",
                p => { p.AddWithValue("@Zoom", NormalizeZoom(zoom)); p.AddWithValue("@UserId", userId); });
        }

        public static void SaveAi(int userId, bool enabled, bool context, string responseLength)
        {
            EnsureRow(userId);
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.UserPreferences SET AiEnabled = @Enabled, AiContext = @Context, AiResponseLength = @Length, " +
                "UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @UserId",
                p =>
                {
                    p.AddWithValue("@Enabled", enabled ? 1 : 0);
                    p.AddWithValue("@Context", context ? 1 : 0);
                    p.AddWithValue("@Length", NormalizeAiLength(responseLength));
                    p.AddWithValue("@UserId", userId);
                });
        }

        public static void SaveNotificationExtras(int userId, string quietHours, bool sound)
        {
            EnsureRow(userId);
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.UserPreferences SET QuietHours = @Quiet, NotifSound = @Sound, UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @UserId",
                p =>
                {
                    p.AddWithValue("@Quiet", string.IsNullOrWhiteSpace(quietHours) ? (object)DBNull.Value : quietHours.Trim());
                    p.AddWithValue("@Sound", sound ? 1 : 0);
                    p.AddWithValue("@UserId", userId);
                });
        }

        public static void SaveTicker(int userId, bool enabled, string speed)
        {
            EnsureRow(userId);
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.UserPreferences SET TickerEnabled = @Enabled, TickerSpeed = @Speed, " +
                "UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @UserId",
                p =>
                {
                    p.AddWithValue("@Enabled", enabled ? 1 : 0);
                    p.AddWithValue("@Speed", NormalizeTickerSpeed(speed));
                    p.AddWithValue("@UserId", userId);
                });
        }

        /// <summary>Stores the detected device timezone on first sign-in without touching other prefs.</summary>
        public static void SaveTimezoneOnly(int userId, string timezoneId)
        {
            string zone = NormalizeTimezone(timezoneId);
            if (zone == null) return;
            EnsureRow(userId);
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.UserPreferences SET TimezoneId = @Tz, UpdatedAtUtc = SYSUTCDATETIME() " +
                "WHERE UserId = @UserId AND (TimezoneId IS NULL OR LTRIM(RTRIM(TimezoneId)) = '')",
                p => { p.AddWithValue("@Tz", zone); p.AddWithValue("@UserId", userId); });
        }

        public static void SaveRegional(int userId, string timezoneId, string clockFormat, int reminderLeadMinutes)
        {
            EnsureRow(userId);
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.UserPreferences SET TimezoneId = @Tz, ClockFormat = @Clock, " +
                "ReminderLeadMinutes = @Lead, UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @UserId",
                p =>
                {
                    p.AddWithValue("@Tz", (object)NormalizeTimezone(timezoneId) ?? DBNull.Value);
                    p.AddWithValue("@Clock", NormalizeClockFormat(clockFormat));
                    p.AddWithValue("@Lead", NormalizeLead(reminderLeadMinutes));
                    p.AddWithValue("@UserId", userId);
                });
        }

        public static void SaveAccessibility(int userId, bool highContrast, bool sidebarCollapsed)
        {
            EnsureRow(userId);
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.UserPreferences SET HighContrast = @Contrast, SidebarCollapsed = @Sidebar, " +
                "UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @UserId",
                p =>
                {
                    p.AddWithValue("@Contrast", highContrast ? 1 : 0);
                    p.AddWithValue("@Sidebar", sidebarCollapsed ? 1 : 0);
                    p.AddWithValue("@UserId", userId);
                });
        }

        public static void SaveAcademic(int userId, string dateFormat, int pageSize, string reportFormat,
            string academicYear, string semester, string dashboard)
        {
            EnsureRow(userId);
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.UserPreferences SET DateFormat = @Date, TablePageSize = @Page, ReportFormat = @Report, " +
                "AcademicYear = @Year, Semester = @Semester, DefaultDashboard = @Dash, UpdatedAtUtc = SYSUTCDATETIME() " +
                "WHERE UserId = @UserId",
                p =>
                {
                    p.AddWithValue("@Date", NormalizeDateFormat(dateFormat));
                    p.AddWithValue("@Page", NormalizePageSize(pageSize));
                    p.AddWithValue("@Report", NormalizeReportFormat(reportFormat));
                    p.AddWithValue("@Year", (object)NullIfEmpty(academicYear) ?? DBNull.Value);
                    p.AddWithValue("@Semester", (object)NullIfEmpty(semester) ?? DBNull.Value);
                    p.AddWithValue("@Dash", (object)NullIfEmpty(dashboard) ?? DBNull.Value);
                    p.AddWithValue("@UserId", userId);
                });
        }

        public static void SaveNotifTypes(int userId, IEnumerable<string> disabledTypes)
        {
            EnsureRow(userId);
            var clean = (disabledTypes ?? Enumerable.Empty<string>())
                .Select(t => (t ?? "").Trim().ToLowerInvariant())
                .Where(t => NotifTypes.Contains(t))
                .Distinct()
                .ToList();
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.UserPreferences SET DisabledNotifTypes = @Types, UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @UserId",
                p => { p.AddWithValue("@Types", string.Join(",", clean)); p.AddWithValue("@UserId", userId); });
        }

        public static bool IsNotifEnabled(UserPrefs prefs, string type)
        {
            if (prefs == null || string.IsNullOrEmpty(prefs.DisabledNotifTypes)) return true;
            string t = (type ?? "general").Trim().ToLowerInvariant();
            foreach (string raw in prefs.DisabledNotifTypes.Split(','))
                if (string.Equals(raw.Trim(), t, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        /// <summary>Types the user has switched off, for the settings checkboxes.</summary>
        public static HashSet<string> DisabledSet(UserPrefs prefs)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (prefs == null) return set;
            foreach (string raw in (prefs.DisabledNotifTypes ?? "").Split(','))
                if (raw.Trim().Length > 0) set.Add(raw.Trim());
            return set;
        }

        private static void EnsureRow(int userId)
        {
            SqlHelper.ExecuteNonQuery(
                "IF NOT EXISTS (SELECT 1 FROM dbo.UserPreferences WHERE UserId = @UserId) " +
                "INSERT INTO dbo.UserPreferences (UserId) VALUES (@UserId)",
                p => p.AddWithValue("@UserId", userId));
        }

        // ---------------- validation (shared with the AJAX handler) ----------------

        public static string NormalizeTheme(string value)
        {
            string v = (value ?? "").Trim().ToLowerInvariant();
            return v == "dark" || v == "system" || v == "eye" ? v : "light";
        }

        public static int NormalizeZoom(int value)
        {
            if (value < 80) return 80;
            if (value > 150) return 150;
            return (int)(Math.Round(value / 10.0) * 10);
        }

        public static string NormalizeAiLength(string value)
        {
            string v = (value ?? "").Trim().ToLowerInvariant();
            return AiLengths.Contains(v) ? v : "balanced";
        }

        public static string NormalizeMotion(string value)
        {
            return string.Equals((value ?? "").Trim(), "reduced", StringComparison.OrdinalIgnoreCase) ? "reduced" : "full";
        }

        public static int NormalizeScale(int value)
        {
            if (value < 85) return 85;
            if (value > 140) return 140;
            // snap to the 5% ladder
            return (int)(Math.Round(value / 5.0) * 5);
        }

        public static string NormalizeDateFormat(string value)
        {
            return DateFormats.Contains(value) ? value : "yyyy-MM-dd";
        }

        public static string NormalizeClockFormat(string value)
        {
            return string.Equals((value ?? "").Trim(), "12", StringComparison.Ordinal) ? "12" : "24";
        }

        public static string NormalizeTickerSpeed(string value)
        {
            string v = (value ?? "").Trim().ToLowerInvariant();
            return TickerSpeeds.Contains(v) ? v : "slow";
        }

        public static int NormalizeLead(int value)
        {
            return ReminderLeads.Contains(value) ? value : 15;
        }

        /// <summary>Returns a valid IANA zone id, or null for "use the institution default".</summary>
        public static string NormalizeTimezone(string value)
        {
            string v = (value ?? "").Trim();
            if (v.Length == 0) return null;
            try
            {
                return DateTimeZoneProviders.Tzdb.GetZoneOrNull(v) == null ? null : v;
            }
            catch (Exception) { return null; }
        }

        public static int NormalizePageSize(int value)
        {
            return PageSizes.Contains(value) ? value : 20;
        }

        public static string NormalizeReportFormat(string value)
        {
            string v = (value ?? "").Trim().ToLowerInvariant();
            return ReportFormats.Contains(v) ? v : "pdf";
        }

        private static string NullIfEmpty(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }
    }
}
