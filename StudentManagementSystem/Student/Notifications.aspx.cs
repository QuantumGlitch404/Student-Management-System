using System;
using System.Data;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Student
{
    /// <summary>
    /// The student inbox keeps its own URL (bookmarks, sidebar) but renders the
    /// shared notification-centre view: paged, filtered, clickable rows that are
    /// marked read through NotifyGo. Only the caller's recipient rows are read.
    /// </summary>
    public partial class Notifications : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Student" }; } }

        protected string InboxHtml, FlashHtml;

        private const string BaseUrl = "/Student/Notifications.aspx";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            Build();
        }

        private void HandlePost()
        {
            try
            {
                switch (Request.Form["op"])
                {
                    case "read":
                        CommsRepo.MarkRead(int.Parse(Request.Form["notificationId"]), CurrentUser.UserId);
                        Flash("success", "Marked as read.");
                        break;
                    case "readall":
                        CommsRepo.MarkAllRead(CurrentUser.UserId);
                        Flash("success", "All notifications marked as read.");
                        break;
                    case "archive":
                        CommsRepo.Archive(int.Parse(Request.Form["notificationId"]), CurrentUser.UserId, true);
                        Flash("success", "Notification archived.");
                        break;
                    case "unarchive":
                        CommsRepo.Archive(int.Parse(Request.Form["notificationId"]), CurrentUser.UserId, false);
                        Flash("success", "Notification restored to the inbox.");
                        break;
                    default: throw new Exception("Unknown operation.");
                }
                Redirect(BaseUrl + "?filter=" + HttpUtility.UrlEncode(Filter())
                    + "&type=" + HttpUtility.UrlEncode(TypeFilter())
                    + "&q=" + HttpUtility.UrlEncode(SearchQ()));
            }
            catch (Exception ex)
            {
                Redirect(BaseUrl + "?err=" + HttpUtility.UrlEncode(SqlHelper.FriendlyError(ex).Message));
            }
        }

        private string Filter()
        {
            string f = (Request.QueryString["filter"] ?? "all").Trim().ToLowerInvariant();
            return f == "unread" || f == "read" || f == "archived" ? f : "all";
        }

        private string SearchQ()
        {
            string q = (Request.QueryString["q"] ?? "").Trim();
            return q.Length > 80 ? q.Substring(0, 80) : q;
        }

        private string TypeFilter()
        {
            string t = (Request.QueryString["type"] ?? "").Trim().ToLowerInvariant();
            return Array.IndexOf(InboxView.Types, t) >= 0 ? t : "";
        }

        private int PageNo()
        {
            int p;
            return int.TryParse(Request.QueryString["page"], out p) && p > 0 ? p : 1;
        }

        private void Build()
        {
            int userId = CurrentUser.UserId;
            UserPrefs prefs;
            try { prefs = PrefsRepo.Get(userId); }
            catch (Exception) { prefs = new UserPrefs { UserId = userId }; }

            string filter = Filter();
            string type = TypeFilter();
            string q = SearchQ();
            int page = PageNo();
            int pageSize = prefs.TablePageSize;

            int unread = CommsRepo.InboxCountVisible(userId, prefs, "unread", type, q);
            int read = CommsRepo.InboxCountVisible(userId, prefs, "read", type, q);
            int all = CommsRepo.InboxCountVisible(userId, prefs, "all", type, q);
            int archived = CommsRepo.InboxCountVisible(userId, prefs, "archived", type, q);

            DataTable dt = CommsRepo.InboxVisible(userId, prefs, filter, type, q, page, pageSize);
            string csrf = CsrfField();
            int total = filter == "unread" ? unread : filter == "read" ? read : filter == "archived" ? archived : all;

            InboxHtml = InboxView.FilterBar(BaseUrl, filter, type, q, unread, read, all, archived)
                + InboxView.WithCsrf(InboxView.Header(unread, total, q), csrf)
                + InboxView.WithCsrf(InboxView.Rows(dt, prefs.DateFormat,
                    filter == "unread" ? "Nothing unread. You are all caught up."
                    : filter == "read" ? "Nothing read yet."
                    : filter == "archived" ? "Nothing archived yet."
                    : q.Length > 0 ? "No notification matches that search."
                    : "No notifications for this account yet.",
                    prefs.TimezoneId ?? Fmt.InstitutionZoneId), csrf)
                + InboxView.Pager(BaseUrl, filter, type, q, page, pageSize, total);
        }
    }
}
