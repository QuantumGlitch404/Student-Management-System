using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NodaTime;
using StudentManagementSystem.Data;

namespace StudentManagementSystem.Lib
{
    /// <summary>
    /// IANA timezone formatting. Instants are stored as UTC; every display,
    /// export, reminder and test timestamp is converted from UTC exactly once.
    /// </summary>
    public static class Fmt
    {
        private static readonly object Sync = new object();
        private static string _institutionZone;
        private static DateTime _institutionLoadedUtc = DateTime.MinValue;

        public static string InstitutionZoneId
        {
            get
            {
                lock (Sync)
                {
                    if (_institutionZone != null && (DateTime.UtcNow - _institutionLoadedUtc).TotalMinutes < 5)
                        return _institutionZone;
                    string value = null;
                    try
                    {
                        object scalar = SqlHelper.ExecuteScalar(
                            "SELECT SettingValue FROM dbo.AppSettings WHERE SettingKey = 'InstitutionTimezone'");
                        value = scalar == null || scalar == DBNull.Value ? null : Convert.ToString(scalar);
                    }
                    catch (Exception) { }
                    _institutionZone = PrefsRepo.NormalizeTimezone(value) ?? "Asia/Kolkata";
                    _institutionLoadedUtc = DateTime.UtcNow;
                    return _institutionZone;
                }
            }
        }

        public static string UserZoneId(int userId)
        {
            try
            {
                UserPrefs prefs = PrefsRepo.Get(userId);
                if (!string.IsNullOrEmpty(prefs.TimezoneId)) return prefs.TimezoneId;
            }
            catch (Exception) { }

            // Resolution order: saved user zone -> sms_tz cookie -> institution -> UTC.
            try
            {
                HttpContext ctx = HttpContext.Current;
                if (ctx != null)
                {
                    HttpCookie cookie = ctx.Request.Cookies["sms_tz"];
                    string detected = cookie == null ? null : PrefsRepo.NormalizeTimezone(cookie.Value);
                    if (!string.IsNullOrEmpty(detected)) return detected;
                }
            }
            catch (Exception) { }
            return InstitutionZoneId;
        }

        /// <summary>Effective zone for the current request (cookie aware), used by display helpers.</summary>
        public static string CurrentZoneId()
        {
            if (CurrentUser.IsAuthenticated) return UserZoneId(CurrentUser.UserId);
            try
            {
                HttpContext ctx = HttpContext.Current;
                if (ctx != null)
                {
                    HttpCookie cookie = ctx.Request.Cookies["sms_tz"];
                    string detected = cookie == null ? null : PrefsRepo.NormalizeTimezone(cookie.Value);
                    if (!string.IsNullOrEmpty(detected)) return detected;
                }
            }
            catch (Exception) { }
            return InstitutionZoneId;
        }

        /// <summary>All IANA zone ids, sorted for the settings picker.</summary>
        public static IEnumerable<string> TimeZoneIds()
        {
            return DateTimeZoneProviders.Tzdb.Ids.OrderBy(id => id, StringComparer.OrdinalIgnoreCase);
        }

        public static DateTimeZone Zone(string zoneId)
        {
            string id = PrefsRepo.NormalizeTimezone(zoneId) ?? InstitutionZoneId;
            DateTimeZone zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(id);
            return zone ?? DateTimeZone.Utc;
        }

        public static Instant Instant(DateTime utc)
        {
            DateTime value = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return NodaTime.Instant.FromDateTimeUtc(value);
        }

        public static ZonedDateTime InZone(DateTime utc, string zoneId)
        {
            return Instant(utc).InZone(Zone(zoneId));
        }

        public static DateTime ToLocal(DateTime utc, string zoneId)
        {
            return InZone(utc, zoneId).LocalDateTime.ToDateTimeUnspecified();
        }

        public static DateTime ToUtc(LocalDateTime local, string zoneId)
        {
            // InZoneLeniently shifts a spring-forward gap forward by the gap size
            // and chooses the earlier instant for an autumn overlap.
            return local.InZoneLeniently(Zone(zoneId)).ToInstant().ToDateTimeUtc();
        }

        public static DateTime ToUtc(DateTime local, string zoneId)
        {
            return ToUtc(LocalDateTime.FromDateTime(local), zoneId);
        }

        public static string Date(DateTime utc, string zoneId, string format = "yyyy-MM-dd")
        {
            return ToLocal(utc, zoneId).ToString(format);
        }

        public static string Stamp(DateTime utc, string zoneId, string format = "yyyy-MM-dd HH:mm")
        {
            return ToLocal(utc, zoneId).ToString(format);
        }

        public static string Time(DateTime utc, string zoneId, bool clock24)
        {
            return ToLocal(utc, zoneId).ToString(clock24 ? "HH:mm" : "h:mm tt");
        }

        public static string Range(DateTime startUtc, DateTime endUtc, string zoneId, string format = "yyyy-MM-dd HH:mm")
        {
            return Stamp(startUtc, zoneId, format) + " - " + Stamp(endUtc, zoneId, format);
        }

        public static string Relative(DateTime utc)
        {
            TimeSpan age = DateTime.UtcNow - (utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc));
            if (age.TotalSeconds < 60) return "just now";
            if (age.TotalMinutes < 60) return (int)age.TotalMinutes + " min ago";
            if (age.TotalHours < 24) return (int)age.TotalHours + " h ago";
            if (age.TotalDays < 7) return (int)age.TotalDays + " d ago";
            return Date(utc, InstitutionZoneId);
        }

        /// <summary>Year/date pattern honouring the signed-in user's stored date format.</summary>
        public static string UserDate(DateTime utc, int userId)
        {
            UserPrefs prefs = PrefsRepo.Get(userId);
            return Date(utc, prefs.TimezoneId ?? InstitutionZoneId, prefs.DateFormat);
        }

        public static string IsoUtc(DateTime utc)
        {
            DateTime value = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return value.ToString("o");
        }
    }
}
