using System;
using System.Data;
using System.Text;
using System.Web;

namespace StudentManagementSystem.Lib
{
    /// <summary>
    /// Shared rendering for the notification centre (student inbox page and the
    /// all-role page). Kept in one place so filters, pagination, search, archive
    /// state and the clickable/read behaviour cannot drift between roles.
    /// </summary>
    public static class InboxView
    {
        public static readonly string[] Types = { "general", "academic", "payment", "complaint", "system" };

        public static string Icon(string type)
        {
            switch ((type ?? "").ToLowerInvariant())
            {
                case "academic": return "bi-journal-check";
                case "payment": return "bi-cash-coin";
                case "complaint": return "bi-flag";
                case "system": return "bi-shield-lock";
                default: return "bi-bell";
            }
        }

        public static string FilterBar(string baseUrl, string filter, string type, string q, int unread, int read, int all, int archived)
        {
            var sb = new StringBuilder();
            sb.Append("<div class='stack'>");
            sb.Append("<div class='cluster' role='group' aria-label='Filter notifications'>");
            Tab(sb, baseUrl, filter, type, q, "all", "All", all);
            Tab(sb, baseUrl, filter, type, q, "unread", "Unread", unread);
            Tab(sb, baseUrl, filter, type, q, "read", "Read", read);
            Tab(sb, baseUrl, filter, type, q, "archived", "Archived", archived);
            sb.Append("</div>");

            sb.Append("<form method='get' action='").Append(HttpUtility.HtmlAttributeEncode(baseUrl)).Append("' class='toolbar' role='search'>");
            sb.Append("<label class='visually-hidden' for='ncQ'>Search notifications</label>");
            sb.Append("<input id='ncQ' name='q' value='").Append(HttpUtility.HtmlAttributeEncode(q ?? "")).Append("' class='form-control form-control-sm toolbar__grow' placeholder='Search title or message' maxlength='80'/>");
            sb.Append("<label class='visually-hidden' for='ncType'>Category</label>");
            sb.Append("<select id='ncType' name='type' class='form-select form-select-sm toolbar__fixed'>");
            sb.Append("<option value=''>All categories</option>");
            foreach (string t in Types)
                sb.Append("<option value='").Append(t).Append("'").Append(string.Equals(t, type, StringComparison.OrdinalIgnoreCase) ? " selected='selected'" : "").Append(">").Append(Cap(t)).Append("</option>");
            sb.Append("</select>");
            sb.Append("<input type='hidden' name='filter' value='").Append(HttpUtility.HtmlAttributeEncode(filter)).Append("'/>");
            sb.Append("<button class='btn btn-sm btn-outline-sms toolbar__fixed' type='submit'><i class='bi bi-search'></i> Search</button>");
            if (!string.IsNullOrEmpty(q) || !string.IsNullOrEmpty(type))
                sb.Append("<a class='btn btn-sm btn-link toolbar__fixed' href='").Append(HttpUtility.HtmlAttributeEncode(baseUrl)).Append("?filter=").Append(HttpUtility.HtmlAttributeEncode(filter)).Append("'>Reset</a>");
            sb.Append("</form></div>");
            return sb.ToString();
        }
        private static void Tab(StringBuilder sb, string baseUrl, string current, string type, string q, string value, string label, int count)
        {
            bool active = string.Equals(current, value, StringComparison.OrdinalIgnoreCase);
            string url = baseUrl + "?filter=" + HttpUtility.UrlEncode(value)
                + (string.IsNullOrEmpty(type) ? "" : "&type=" + HttpUtility.UrlEncode(type))
                + (string.IsNullOrEmpty(q) ? "" : "&q=" + HttpUtility.UrlEncode(q));
            sb.Append("<a class='btn btn-sm ").Append(active ? "btn-sms" : "btn-outline-sms").Append("' href='")
              .Append(HttpUtility.HtmlAttributeEncode(url)).Append("'>")
              .Append(label).Append(" <span class='badge badge-muted ms-1'>").Append(count).Append("</span></a>");
        }

        /// <summary>Rows for one page. Opening a row goes through NotifyGo, which re-checks ownership and role.</summary>
        public static string Rows(DataTable dt, string dateFormat, string emptyText, string zoneId = null)
        {
            if (dt == null || dt.Rows.Count == 0)
                return "<ul class='list'><li class='list__item'><span class='list__icon'><i class='bi bi-inbox'></i></span><div class='list__body'><p class='list__msg'>" +
                       HttpUtility.HtmlEncode(emptyText ?? "No notifications.") +
                       "</p></div><div class='list__actions'><a class='btn btn-sm btn-outline-sms' href='/Account/Settings.aspx#notifications'>Notification settings</a></div></li></ul>";

            var sb = new StringBuilder("<ul class='list'>");
            foreach (DataRow r in dt.Rows)
            {
                bool unread = !Convert.ToBoolean(r["IsRead"]);
                bool archived = r.Table.Columns.Contains("IsArchived") && r["IsArchived"] != DBNull.Value && Convert.ToBoolean(r["IsArchived"]);
                int id = Convert.ToInt32(r["NotificationId"]);
                string title = r["Title"] == DBNull.Value ? "" : r["Title"].ToString();
                string message = r["Message"] == DBNull.Value ? "" : r["Message"].ToString();
                string type = r["NType"] == DBNull.Value ? "general" : r["NType"].ToString();
                string when = FormatDate(r["CreatedAtUtc"], dateFormat, zoneId);

                sb.Append("<li class='list__item").Append(unread ? " is-unread" : "").Append(archived ? " archived" : "").Append("'>");
                sb.Append("<span class='list__icon' aria-hidden='true'><i class='bi ").Append(Icon(type)).Append("'></i></span>");
                sb.Append("<div class='list__body'>");
                sb.Append("<a class='list__title' href='/Handlers/NotifyGo.ashx?id=").Append(id).Append("'>")
                  .Append(unread ? "<span class='visually-hidden'>Unread: </span>" : "")
                  .Append(HttpUtility.HtmlEncode(title)).Append("</a>");
                sb.Append("<p class='list__msg'>").Append(HttpUtility.HtmlEncode(message)).Append("</p>");
                sb.Append("<div class='cluster'>");
                if (unread) sb.Append("<span class='badge badge-warn'>unread</span>");
                if (archived) sb.Append("<span class='badge badge-off'>archived</span>");
                sb.Append("<span class='badge badge-info'>").Append(HttpUtility.HtmlEncode(type)).Append("</span>");
                sb.Append("<span class='mono'>").Append(HttpUtility.HtmlEncode(when)).Append("</span>");
                sb.Append("</div></div>");
                sb.Append("<div class='list__actions'>");
                if (unread)
                    sb.Append("<form method='post'>")
                      .Append(CsrfHidden())
                      .Append("<input type='hidden' name='op' value='read'/>")
                      .Append("<input type='hidden' name='notificationId' value='").Append(id).Append("'/>")
                      .Append("<button class='imbtn' type='submit'>Mark read</button></form>");
                sb.Append("<form method='post'>")
                  .Append(CsrfHidden())
                  .Append("<input type='hidden' name='op' value='").Append(archived ? "unarchive" : "archive").Append("'/>")
                  .Append("<input type='hidden' name='notificationId' value='").Append(id).Append("'/>")
                  .Append("<button class='imbtn' type='submit' title='").Append(archived ? "Move back to inbox" : "Archive notification").Append("'>")
                  .Append("<i class='bi ").Append(archived ? "bi-arrow-counterclockwise" : "bi-archive").Append("'></i> ")
                  .Append(archived ? "Restore" : "Archive").Append("</button></form>");
                sb.Append("</div></li>");
            }
            sb.Append("</ul>");
            return sb.ToString();
        }
        /// <summary>CSRF field is supplied by the page; this placeholder is replaced by the caller.</summary>
        public static string CsrfPlaceholder = "<!--CSRF-->";

        private static string CsrfHidden()
        {
            return CsrfPlaceholder;
        }

        public static string WithCsrf(string html, string csrfField)
        {
            return (html ?? "").Replace(CsrfPlaceholder, csrfField ?? "");
        }

        public static string Pager(string baseUrl, string filter, string type, string q, int page, int pageSize, int total)
        {
            int pages = Math.Max(1, (int)Math.Ceiling(total / (double)Math.Max(1, pageSize)));
            if (pages <= 1) return "";
            var sb = new StringBuilder("<nav aria-label='Notification pages'><ul class='pagination pagination-sm justify-content-center mt-3'>");
            for (int i = 1; i <= pages; i++)
            {
                if (pages > 9 && i > 2 && i < pages - 1 && Math.Abs(i - page) > 1)
                {
                    if (i == 3) sb.Append("<li class='page-item disabled'><span class='page-link'>...</span></li>");
                    continue;
                }
                sb.Append("<li class='page-item").Append(i == page ? " active" : "").Append("'>");
                sb.Append("<a class='page-link' href='").Append(HttpUtility.HtmlAttributeEncode(baseUrl))
                  .Append("?filter=").Append(HttpUtility.UrlEncode(filter))
                  .Append("&type=").Append(HttpUtility.UrlEncode(type ?? ""))
                  .Append("&q=").Append(HttpUtility.UrlEncode(q ?? ""))
                  .Append("&page=").Append(i).Append("'>").Append(i).Append("</a></li>");
            }
            sb.Append("</ul></nav>");
            return sb.ToString();
        }

        public static string Header(int unread, int total, string q)
        {
            return "<div class='cluster notification-summary'>" +
                   "<span class='settings-note'>" + total + " notification" + (total == 1 ? "" : "s") +
                   " &middot; " + unread + " unread" + (string.IsNullOrEmpty(q) ? "" : " &middot; matching \"" + HttpUtility.HtmlEncode(q) + "\"") + "</span>" +
                   "<form method='post' style='display:inline'>" + CsrfHidden() +
                   "<input type='hidden' name='op' value='readall'/>" +
                   "<button class='btn btn-sm btn-outline-sms' type='submit'>Mark all as read</button>" +
                   "</form></div>";
        }
        public static string FormatDate(object value, string format, string zoneId = null)
        {
            if (value == null || value == DBNull.Value) return "";
            DateTime dt = Convert.ToDateTime(value);
            string f = string.IsNullOrEmpty(format) ? "yyyy-MM-dd" : format;
            try
            {
                return string.IsNullOrWhiteSpace(zoneId)
                    ? dt.ToString(f + " HH:mm")
                    : Fmt.Stamp(dt, zoneId, f + " HH:mm");
            }
            catch (FormatException)
            {
                return string.IsNullOrWhiteSpace(zoneId)
                    ? dt.ToString("yyyy-MM-dd HH:mm")
                    : Fmt.Stamp(dt, zoneId, "yyyy-MM-dd HH:mm");
            }
        }

        private static string Cap(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}


