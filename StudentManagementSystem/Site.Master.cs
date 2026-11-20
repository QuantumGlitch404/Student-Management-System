using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using NodaTime;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem
{
    /// <summary>
    /// Application shell: role-aware sidebar + topbar + notification badge anchor.
    /// The sidebar HTML is produced here so module pages stay markup-only.
    /// </summary>
    public partial class SiteMaster : System.Web.UI.MasterPage
    {
        protected string SidebarHtml;
        protected string PageHeadingText;
        protected string PushConfigJson;
        protected string PushButtonHtml;

        // Appearance / accessibility (UserPreferences, mirrored to a cookie so the
        // anonymous auth pages can honour the same choice).
        protected string ThemeAttr = "light";
        protected string ThemePref = "light";
        protected string FontAttr = "100";
        protected string MotionAttr = "full";
        protected string ContrastAttr = "normal";
        protected string SidebarClass = "";
        protected string ZoomAttr = "100";
        protected string ModuleAttr = "overview";
        protected string NotifCenterUrl = "/Notifications/Index.aspx";
        protected string CsrfToken = "";
        protected string BuildStamp = "BUILD DEV";
        protected string TickerConfigJson = "{enabled:false,speed:\"slow\",timezone:\"UTC\",clock:\"24\"}";
        protected bool ExamMode = false;
        protected string TimezoneNoticeHtml = "";
        protected bool ShowProfileGate = false;
        protected string ProfileGateHtml = "";

        // Topbar breadcrumb text per page. Without this the topbar showed the raw
        // request path ("Admin/Index.aspx"), which is not presentable to users.
        // Role-specific titles are resolved in FriendlyTitle() below.
        private static readonly Dictionary<string, string> PageTitles =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "/account/login.aspx", "Sign in" },
                { "/account/signup.aspx", "Create your account" },
                { "/account/verify.aspx", "Verify your account" },
                { "/account/forgot.aspx", "Forgot password" },
                { "/account/reset.aspx", "Reset password" },
                { "/account/error.aspx", "Something went wrong" },
                { "/account/settings.aspx", "Settings" },
                { "/account/activity.aspx", "My activity" },
                { "/notifications/index.aspx", "Notifications" },
                { "/chatbot/index.aspx", "AI assistant" },

                { "/admin/index.aspx", "Dashboard" },
                { "/admin/audit.aspx", "Activity log" },
                { "/admin/users.aspx", "Students and users" },
                { "/admin/academics.aspx", "Academics" },
                { "/admin/sessions.aspx", "Academic sessions" },
                { "/admin/attendance.aspx", "Attendance" },
                { "/admin/exams.aspx", "Exams and results" },
                { "/admin/onlinetests.aspx", "Online tests" },
                { "/timetable/index.aspx", "Timetable" },
                { "/calendar/index.aspx", "Academic calendar" },
                { "/admin/importexport.aspx", "Import and export" },
                { "/admin/fees.aspx", "Fee types" },
                { "/admin/payments.aspx", "Payments" },
                { "/admin/notifications.aspx", "Notifications" },
                { "/admin/announcements.aspx", "Announcements" },
                { "/admin/complaints.aspx", "Complaints" },
                { "/admin/settings.aspx", "Settings" },
                { "/admin/mailcenter.aspx", "Mail centre" },

                { "/faculty/index.aspx", "Faculty dashboard" },
                { "/faculty/attendance.aspx", "Take attendance" },
                { "/faculty/marks.aspx", "Marks and exams" },
                { "/faculty/complaints.aspx", "Complaint replies" },
                { "/faculty/profile.aspx", "My profile" },

                { "/student/index.aspx", "My dashboard" },
                { "/student/attendance.aspx", "My attendance" },
                { "/student/results.aspx", "My results" },
                { "/student/onlinetests.aspx", "Online tests" },
                { "/student/fees.aspx", "Fees and payments" },
                { "/student/notifications.aspx", "Notifications" },
                { "/student/complaint.aspx", "Raise a concern" },
                { "/student/profile.aspx", "My profile" },
                { "/student/paycheckout.aspx", "Checkout" }
            };

        protected void Page_Load(object sender, EventArgs e)
        {
            PageHeadingText = FriendlyTitle();
            SidebarHtml = BuildSidebar();
            LoadAppearance();
            BuildProfileGate();
            PushConfigJson = BuildPushConfig();
            PushButtonHtml = AppConfig.PushEnabled && !string.IsNullOrWhiteSpace(AppConfig.Get("Push:VapidPublicKey", ""))
                ? "<button type='button' class='sms-pushbtn' id='smsPushEnable' title='Get alerts on this device' aria-label='Enable device alerts'><svg class='ic'><use href='/Content/icons/sprite.svg#i-phone-vibrate'></use></svg></button>"
                : "";
        }

        /// <summary>
        /// Reads the signed-in user's appearance preferences. The database is
        /// authoritative; the cookie is refreshed from it so the login page shows
        /// the same theme after sign-out.
        /// </summary>
        private void LoadAppearance()
        {
            // The cookie is the freshest source of truth: it is written
            // synchronously by appearance.js on click. The database is only a
            // fallback for a first visit / a new device. This prevents a
            // postback from re-rendering an older theme over the new choice.
            UserPrefs cookie = PrefsRepo.FromCookies(Context);
            bool haveCookie = Context.Request.Cookies[PrefsRepo.CookieTheme] != null;
            UserPrefs prefs = null;
            try
            {
                prefs = PrefsRepo.Get(CurrentUser.UserId);
                if (haveCookie)
                {
                    bool differs = !string.Equals(prefs.Theme, cookie.Theme, StringComparison.OrdinalIgnoreCase)
                        || prefs.FontScale != cookie.FontScale
                        || !string.Equals(prefs.Motion, cookie.Motion, StringComparison.OrdinalIgnoreCase)
                        || prefs.Zoom != cookie.Zoom;
                    prefs.Theme = cookie.Theme;
                    prefs.FontScale = cookie.FontScale;
                    prefs.Motion = cookie.Motion;
                    prefs.Zoom = cookie.Zoom;
                    if (differs)
                    {
                        try
                        {
                            PrefsRepo.SaveAppearance(CurrentUser.UserId, prefs.Theme, prefs.FontScale, prefs.Motion);
                            PrefsRepo.SaveZoom(CurrentUser.UserId, prefs.Zoom);
                        }
                        catch (Exception) { /* cookie still wins for this render */ }
                    }
                }
            }
            catch (Exception) { prefs = haveCookie ? cookie : null; }
            if (prefs == null) prefs = haveCookie ? cookie : new UserPrefs { UserId = CurrentUser.UserId };

            ThemePref = prefs.Theme;
            // "system" is resolved by the pre-paint script; light is the safe default.
            ThemeAttr = prefs.Theme == "dark" ? "dark" : prefs.Theme == "eye" ? "eye" : "light";
            ZoomAttr = prefs.Zoom.ToString();
            ModuleAttr = ResolveModule();
            FontAttr = prefs.FontScale.ToString();
            MotionAttr = prefs.Motion;
            ContrastAttr = prefs.HighContrast ? "high" : "normal";
            SidebarClass = prefs.SidebarCollapsed ? "sms-sidebar-collapsed" : "";
            NotifCenterUrl = CurrentUser.RoleName == "Student" ? "/Student/Notifications.aspx" : "/Notifications/Index.aspx";
            BuildStamp = ReadBuildStamp();
            TickerConfigJson = BuildTickerConfig(prefs);
            AdoptDetectedTimezone(prefs);
            ExamMode = IsExamRunner();
            TimezoneNoticeHtml = BuildTimezoneNotice(prefs);

            CsrfToken = Session["AntiForgeryToken"] as string;
            if (string.IsNullOrEmpty(CsrfToken))
            {
                const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                var bytes = new byte[24];
                using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider()) rng.GetBytes(bytes);
                var sb = new StringBuilder(24);
                foreach (byte b in bytes) sb.Append(chars[b % chars.Length]);
                CsrfToken = sb.ToString();
                Session["AntiForgeryToken"] = CsrfToken;
            }

            try { PrefsRepo.WriteCookie(Context, prefs.Theme, prefs.FontScale, prefs.Motion, prefs.Zoom); }
            catch (Exception) { /* cookies disabled: page still renders with cookie/DB values */ }
        }

        private static string ReadBuildStamp()
        {
            try
            {
                string configured = AppConfig.Get("Build:Stamp", "");
                if (!string.IsNullOrWhiteSpace(configured)) return configured.Trim();
                string file = System.IO.Path.Combine(HttpRuntime.AppDomainAppPath, "BUILD-STAMP.txt");
                if (System.IO.File.Exists(file)) return System.IO.File.ReadAllText(file).Trim();
            }
            catch (Exception) { }
            return "BUILD DEV";
        }
        /// <summary>
        /// Client config for Web Push. Stays "enabled:false" until a VAPID key is
        /// configured, so the browser never shows a permission prompt that cannot
        /// lead to a delivered notification.
        /// </summary>
        private static string BuildTickerConfig(UserPrefs prefs)
        {
            string institution = Fmt.InstitutionZoneId;
            string userZone = prefs == null ? "" : (prefs.TimezoneId ?? "");
            string zone = string.IsNullOrEmpty(userZone) ? institution : userZone;
            string speed = prefs == null ? "slow" : prefs.TickerSpeed;
            string clock = prefs == null ? "24" : prefs.ClockFormat;
            bool enabled = prefs == null || prefs.TickerEnabled;
            return "{enabled:" + (enabled ? "true" : "false") +
                   ",speed:\"" + HttpUtility.JavaScriptStringEncode(speed) + "\"" +
                   ",timezone:\"" + HttpUtility.JavaScriptStringEncode(zone) + "\"" +
                   ",userTimezone:\"" + HttpUtility.JavaScriptStringEncode(userZone) + "\"" +
                   ",institutionTimezone:\"" + HttpUtility.JavaScriptStringEncode(institution) + "\"" +
                   ",clock:\"" + HttpUtility.JavaScriptStringEncode(clock) + "\"}";
        }

        /// <summary>On the first signed-in page, persist the device timezone detected by the head script.</summary>
        private void AdoptDetectedTimezone(UserPrefs prefs)
        {
            try
            {
                if (prefs == null || !string.IsNullOrEmpty(prefs.TimezoneId)) return;
                HttpCookie cookie = Context.Request.Cookies["sms_tz"];
                if (cookie == null) return;
                string zone = PrefsRepo.NormalizeTimezone(cookie.Value);
                if (string.IsNullOrEmpty(zone)) return;
                PrefsRepo.SaveTimezoneOnly(CurrentUser.UserId, zone);
                prefs.TimezoneId = zone;
                TickerConfigJson = BuildTickerConfig(prefs);
            }
            catch (Exception) { /* timezone adoption is best-effort */ }
        }

        private static bool IsExamRunner()
        {
            try
            {
                HttpContext ctx = HttpContext.Current;
                if (ctx == null) return false;
                string path = (ctx.Request.Path ?? "").ToLowerInvariant();
                if (!path.EndsWith("/student/onlinetests.aspx")) return false;
                return !string.IsNullOrEmpty(ctx.Request.QueryString["attempt"]);
            }
            catch (Exception) { return false; }
        }

        private string BuildTimezoneNotice(UserPrefs prefs)
        {
            try
            {
                if (prefs == null) return "";
                HttpCookie seen = Context.Request.Cookies["sms_tz_notice"];
                if (seen != null) return "";
                string zone = string.IsNullOrEmpty(prefs.TimezoneId) ? Fmt.InstitutionZoneId : prefs.TimezoneId;
                DateTimeZone tz = Fmt.Zone(zone);
                Offset offset = tz.GetUtcOffset(SystemClock.Instance.GetCurrentInstant());
                string sign = offset.Milliseconds >= 0 ? "+" : "-";
                int absMinutes = Math.Abs(offset.Milliseconds) / 60000;
                string abbr = tz.GetZoneInterval(SystemClock.Instance.GetCurrentInstant()).Name;
                string label = zone + " (" + abbr + ", UTC" + sign + (absMinutes / 60).ToString("00") + ":" + (absMinutes % 60).ToString("00") + ")";
                var c = new HttpCookie("sms_tz_notice", "1") { Expires = DateTime.UtcNow.AddDays(14), Path = "/" };
                Context.Response.Cookies.Set(c);
                return "<div class='tz-notice' id='smsTzNotice' role='status'>" +
                       "<span><strong>Your timezone is " + E(label) + ".</strong> Dates and times follow it everywhere.</span>" +
                       "<a class='btn btn-outline-sms btn-sm' href='/Account/Settings.aspx#timezone'>Change</a>" +
                       "<button type='button' class='tz-notice-close' onclick='this.parentNode.remove()' aria-label='Dismiss'>&times;</button></div>";
            }
            catch (Exception) { return ""; }
        }

        private static string BuildPushConfig()
        {
            string key = AppConfig.Get("Push:VapidPublicKey", "");
            bool enabled = AppConfig.PushEnabled && !string.IsNullOrWhiteSpace(key);
            return "{enabled:" + (enabled ? "true" : "false") +
                   ",key:\"" + System.Web.HttpUtility.JavaScriptStringEncode(key) + "\",auto:false}";
        }

        protected string E(object value)
        {
            return System.Web.HttpUtility.HtmlEncode(value == null ? "" : value.ToString());
        }

        protected string FirstLetter()
        {
            string name = CurrentUser.FullName;
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
        }

        protected string PageHeading()
        {
            if (!string.IsNullOrEmpty(PageHeadingText)) return PageHeadingText;
            return System.Web.HttpContext.Current == null ? "" : FriendlyTitle();
        }

        private static string FriendlyTitle()
        {
            if (System.Web.HttpContext.Current == null) return "";
            string path = System.Web.HttpContext.Current.Request.Path;
            string title;
            if (!string.IsNullOrEmpty(path) && PageTitles.TryGetValue(path, out title))
            {
                // Role-specific overrides for shared pages
                if (path.Equals("/timetable/index.aspx", StringComparison.OrdinalIgnoreCase))
                {
                    var role = CurrentUser.RoleName;
                    if (role == "Faculty" || role == "Student") return "My timetable";
                }
                else if (path.Equals("/calendar/index.aspx", StringComparison.OrdinalIgnoreCase))
                {
                    return "Academic calendar";
                }
                return title;
            }

            // Unknown page: show the file name as words rather than the raw path.
            string file = path == null ? "" : path.Substring(path.LastIndexOf('/') + 1);
            if (file.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)) file = file.Substring(0, file.Length - 5);
            if (string.Equals(file, "Index", StringComparison.OrdinalIgnoreCase)) return "Dashboard";
            return System.Text.RegularExpressions.Regex.Replace(file, "([a-z])([A-Z])", "$1 $2");
        }

        /// <summary>Accent personality for the current area (CSS reads data-module).</summary>
        private static string ResolveModule()
        {
            if (System.Web.HttpContext.Current == null) return "overview";
            string path = (System.Web.HttpContext.Current.Request.Path ?? "").ToLowerInvariant();
            if (path.Contains("/chatbot")) return "chatbot";
            if (path.Contains("/users") || path.Contains("/academics") || path.Contains("/sessions")) return "people";
            if (path.Contains("/attendance") || path.Contains("/exams") || path.Contains("/onlinetests") || path.Contains("/timetable") || path.Contains("/calendar") || path.Contains("/marks") || path.Contains("/importexport") || path.Contains("/results")) return "academics";
            if (path.Contains("/fees") || path.Contains("/payments") || path.Contains("/paycheckout")) return "finance";
            if (path.Contains("/announcement") || path.Contains("/complaint") || path.Contains("/notification") || path.Contains("/comms")) return "comms";
            if (path.Contains("/settings") || path.Contains("/audit")) return "system";
            return "overview";
        }

        protected string NotifTarget()
        {
            switch (CurrentUser.RoleName)
            {
                case "Admin": return "/Admin/Notifications.aspx";
                case "Faculty": return "/Faculty/Index.aspx";
                default: return "/Student/Notifications.aspx";
            }
        }

        private string Nav(string href, string icon, string label, string current, string tip = null, int badge = 0)
        {
            string path = current ?? "";
            bool active = path.Equals(href, StringComparison.OrdinalIgnoreCase) ||
                          (!href.EndsWith("/Index.aspx", StringComparison.OrdinalIgnoreCase) &&
                           path.StartsWith(href.Split('?')[0], StringComparison.OrdinalIgnoreCase));
            string title = string.IsNullOrEmpty(tip) ? label : tip;
            return "<a class='sms-link" + (active ? " active" : "") + "' href='" + href + "' title='" + E(title) + "' aria-current='" + (active ? "page" : "false") + "'>" +
                   "<svg class='ic' aria-hidden='true'><use href='/Content/icons/sprite.svg#i-" + icon + "'></use></svg><span>" + label + "</span>" +
                   (badge > 0 ? "<span class='sms-nav-badge'>" + (badge > 99 ? "99+" : badge.ToString()) + "</span>" : "") + "</a>";
        }

        /// <summary>
        /// Self-registered students/faculty keep working on common pages, but the
        /// information screens that depend on administrator-entered data show one calm,
        /// consistent empty state instead of fake defaults.
        /// </summary>
        private void BuildProfileGate()
        {
            try
            {
                if (!CurrentUser.IsAuthenticated) return;
                string role = CurrentUser.RoleName;
                if (role != "Student" && role != "Faculty") return;
                string path = (Request.Path ?? "").ToLowerInvariant();
                bool dataPage = role == "Student"
                    ? (path.StartsWith("/student/index") || path.Equals("/student/attendance.aspx")
                       || path.Equals("/student/results.aspx") || path.Equals("/student/fees.aspx")
                       || path.Equals("/student/onlinetests.aspx") || path.Equals("/timetable/index.aspx")
                       || path.Equals("/calendar/index.aspx"))
                    : (path.Equals("/faculty/index.aspx") || path.Equals("/faculty/attendance.aspx")
                       || path.Equals("/faculty/marks.aspx") || path.Equals("/timetable/index.aspx"));
                if (!dataPage) return;
                if (PeopleRepo.ProfileReady(CurrentUser.UserId, role)) return;

                ShowProfileGate = true;
                string profileUrl = role == "Student" ? "/Student/Profile.aspx" : "/Faculty/Profile.aspx";
                ProfileGateHtml =
                    "<div class='tile profile-gate-tile'><div class='tile__body text-center py-5'>" +
                    "<div class='tile__empty-icon mx-auto mb-3'><i class='bi bi-person-badge'></i></div>" +
                    "<h2 class='h4 mb-2'>Your profile has not been set up yet</h2>" +
                    "<p class='text-muted mb-4'>Please contact the administrator to add your course and details.</p>" +
                    "<form method='post' action='/Handlers/ProfileNotify.ashx' class='d-inline-block me-2'>" +
                    "<input type='hidden' name='__csrf' value='" + E(CsrfToken) + "'/>" +
                    "<input type='hidden' name='return' value='" + E(Request.RawUrl) + "'/>" +
                    "<button class='btn btn-sms'>Notify administrator</button></form> " +
                    "<a class='btn btn-outline-sms' href='" + profileUrl + "'>Open my profile</a>" +
                    "</div></div>";
            }
            catch (Exception ex) { Global.WriteLog("Site", "profile gate failed", ex); }
        }

        private string BuildSidebar()
        {
            var sb = new StringBuilder();
            string role = CurrentUser.RoleName;
            string self = (Page != null && System.Web.HttpContext.Current != null)
                ? System.Web.HttpContext.Current.Request.Path : "";

            switch (role)
            {
                case "Admin":
                    Group(sb, "Home");
                    sb.Append(Nav("/Admin/Index.aspx", "speedometer2", "Dashboard", self, "Overview, KPIs and what needs attention"));
                    sb.Append(Nav("/Notifications/Index.aspx", "inbox", "Tasks and notifications", self, "Your unread notifications and reminders"));
                    sb.Append(Nav("/Chatbot/Index.aspx", "stars", "AI assistant", self, "Ask the offline assistant about this page"));
                    Group(sb, "People");
                    int peopleBadge = 0;
                    try { peopleBadge = PeopleRepo.IncompleteStudents().Rows.Count + PeopleRepo.IncompleteFaculty().Rows.Count + PeopleRepo.OpenAdminRequestCount(); }
                    catch { }
                    sb.Append(Nav("/Admin/Users.aspx", "people", "Students and users", self, "Students, faculty and login accounts", peopleBadge));
                    sb.Append(Nav("/Admin/Academics.aspx", "journal-bookmark", "Courses and subjects", self, "Courses, subjects and departments"));
                    sb.Append(Nav("/Admin/Sessions.aspx", "calendar3", "Academic sessions", self, "Academic years and semesters"));
                    Group(sb, "Academics");
                    sb.Append(Nav("/Timetable/Index.aspx", "calendar2-week", "Timetable", self, "Weekly class timetable and substitutions"));
                    sb.Append(Nav("/Admin/Attendance.aspx", "clipboard-check", "Attendance", self, "Record and review attendance"));
                    sb.Append(Nav("/Admin/Exams.aspx", "journal-check", "Exams and results", self, "Exam schedules, marks and results"));
                    sb.Append(Nav("/Admin/OnlineTests.aspx", "ui-checks", "Online tests", self, "Question bank, tests, proctoring and results"));
                    sb.Append(Nav("/Calendar/Index.aspx", "calendar2-check", "Academic calendar", self, "Holidays, events and important dates"));
                    Group(sb, "Finance");
                    sb.Append(Nav("/Admin/Fees.aspx", "tags", "Fee types", self, "Fee heads and fee plans"));
                    sb.Append(Nav("/Admin/Payments.aspx", "cash-coin", "Payments", self, "Collections, receipts and dues"));
                    Group(sb, "Communication");
                    sb.Append(Nav("/Admin/Announcements.aspx", "megaphone", "Announcements", self, "Send notices to students and faculty"));
                    sb.Append(Nav("/Admin/Complaints.aspx", "flag", "Complaints", self, "Tickets raised by students"));
                    sb.Append(Nav("/Admin/Notifications.aspx", "bell", "Notification centre", self, "All notifications and delivery status"));
            sb.Append(Nav("/Admin/MailCenter.aspx", "envelope-open", "Mail centre", self, "Send, schedule and receive campus email"));
                    Group(sb, "Data");
                    sb.Append(Nav("/Admin/ImportExport.aspx", "box-arrow-in-down", "Import and export", self, "Bulk import data and export reports"));
                    sb.Append(Nav("/Admin/Audit.aspx", "clock-history", "Activity log", self, "Who changed what, and when"));
                    Group(sb, "System");
                    sb.Append(Nav("/Admin/Settings.aspx", "gear", "Settings", self, "Institution, academic, finance and system settings"));
                    break;

                case "Faculty":
                    Group(sb, "Home");
                    sb.Append(Nav("/Faculty/Index.aspx", "speedometer2", "Dashboard", self, "Your classes, tasks and reminders"));
                    sb.Append(Nav("/Notifications/Index.aspx", "bell", "Notifications", self, "Unread notices and reminders"));
                    sb.Append(Nav("/Chatbot/Index.aspx", "stars", "AI assistant", self, "Ask the offline assistant"));
                    Group(sb, "My classes");
                    sb.Append(Nav("/Timetable/Index.aspx", "calendar2-week", "My timetable", self, "Your weekly classes"));
                    sb.Append(Nav("/Faculty/Attendance.aspx", "clipboard-check", "Take attendance", self, "Mark attendance for a class"));
                    sb.Append(Nav("/Calendar/Index.aspx", "calendar2-check", "Academic calendar", self, "Holidays, events and important dates"));
                    Group(sb, "Teaching");
                    sb.Append(Nav("/Faculty/Marks.aspx", "pencil-square", "Marks and results", self, "Enter and review marks"));
                    Group(sb, "Communication");
                    sb.Append(Nav("/Faculty/Complaints.aspx", "flag", "Complaint replies", self, "Respond to student tickets"));
                    sb.Append(Nav("/Admin/MailCenter.aspx", "envelope-open", "Mail centre", self, "Email your classes"));
                    Group(sb, "Account");
                    sb.Append(Nav("/Faculty/Profile.aspx", "person-gear", "My profile", self, "Your contact and academic details"));
                    sb.Append(Nav("/Account/Activity.aspx", "clock-history", "My activity", self, "Your own account and record history"));
                    sb.Append(Nav("/Account/Settings.aspx", "sliders", "Settings", self, "Appearance, language, time and security"));
                    break;

                default: // Student
                    Group(sb, "Home");
                    sb.Append(Nav("/Student/Index.aspx", "speedometer2", "Dashboard", self, "Today's classes, attendance and tasks"));
                    sb.Append(Nav("/Student/Notifications.aspx", "bell", "Messages and notices", self, "Unread messages and reminders"));
                    sb.Append(Nav("/Chatbot/Index.aspx", "stars", "AI assistant", self, "Ask the offline assistant"));
                    Group(sb, "My learning");
                    sb.Append(Nav("/Timetable/Index.aspx", "calendar2-week", "Timetable", self, "Your weekly classes"));
                    sb.Append(Nav("/Student/Attendance.aspx", "clipboard-check", "My attendance", self, "Subject-wise attendance"));
                    sb.Append(Nav("/Student/Results.aspx", "journal-check", "My results", self, "Marks and exam results"));
                    sb.Append(Nav("/Student/OnlineTests.aspx", "ui-checks", "Online tests", self, "Available tests, practice and results"));
                    sb.Append(Nav("/Calendar/Index.aspx", "calendar2-check", "Academic calendar", self, "Holidays, events and important dates"));
                    Group(sb, "Services");
                    sb.Append(Nav("/Student/Fees.aspx", "cash-coin", "Fees and payments", self, "Dues, receipts and online payment"));
                    sb.Append(Nav("/Student/Complaint.aspx", "flag", "Raise a concern", self, "Raise and track a complaint"));
                    sb.Append(Nav("/Student/Profile.aspx", "person-gear", "My profile", self, "Your contact and academic details"));
                    sb.Append(Nav("/Account/Activity.aspx", "clock-history", "My activity", self, "Your own account and record history"));
                    Group(sb, "Account");
                    sb.Append(Nav("/Account/Settings.aspx", "sliders", "Settings", self, "Appearance, language, time and security"));
                    break;
            }
            return sb.ToString();
        }

        private static void Group(StringBuilder sb, string label)
        {
            sb.Append("<div class='sidebar-navgroup'>").Append(label).Append("</div>");
        }
    }
}






