using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Account
{
    /// <summary>Role-scoped personal activity history for students and faculty.</summary>
    public partial class Activity : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Student", "Faculty" }; } }

        protected string Table, EntityOptions, TotalShown, PagerHtml;
        protected string Q = "", ActionFilter = "", EntityFilter = "", From = "", To = "";
        private int _page, _pageCount, _total;
        private const int PageSize = 50;
        private string _zone;

        protected void Page_Load(object sender, EventArgs e)
        {
            ReadFilters();
            _zone = PrefsRepo.Get(CurrentUser.UserId).TimezoneId ?? Fmt.InstitutionZoneId;
            Build();
        }

        private void ReadFilters()
        {
            Q = (Request.QueryString["q"] ?? "").Trim();
            ActionFilter = (Request.QueryString["action"] ?? "").Trim();
            EntityFilter = (Request.QueryString["entity"] ?? "").Trim();
            From = DateOnly(Request.QueryString["from"]);
            To = DateOnly(Request.QueryString["to"]);
            int.TryParse(Request.QueryString["page"], out _page);
            if (_page < 1) _page = 1;
        }

        private static string DateOnly(string value)
        {
            return Regex.IsMatch(value ?? "", @"^\d{4}-\d{2}-\d{2}$") ? value : "";
        }

        private const string Where =
            "WHERE a.UserId = @Me " +
            "AND (@Action = '' OR a.[Action] LIKE '%' + @Action + '%') " +
            "AND (@Term = '' OR a.[Action] LIKE '%' + @Term + '%' OR a.DetailJson LIKE '%' + @Term + '%') " +
            "AND (@Entity = '' OR a.EntityTable = @Entity) " +
            "AND (@From = '' OR CAST(a.CreatedAtUtc AS date) >= @From) " +
            "AND (@To = '' OR CAST(a.CreatedAtUtc AS date) <= @To) ";

        private void AddParams(System.Data.SqlClient.SqlParameterCollection p)
        {
            p.AddWithValue("@Me", CurrentUser.UserId);
            p.AddWithValue("@Action", ActionFilter);
            p.AddWithValue("@Term", Q);
            p.AddWithValue("@Entity", EntityFilter);
            p.AddWithValue("@From", From);
            p.AddWithValue("@To", To);
        }

        private void Build()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT a.AuditId, a.Action, a.EntityTable, a.EntityId, a.DetailJson, a.ClientIp, a.CreatedAtUtc " +
                "FROM dbo.AuditLogs a " + Where +
                "ORDER BY a.AuditId DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY",
                p =>
                {
                    AddParams(p);
                    p.AddWithValue("@Skip", (_page - 1) * PageSize);
                    p.AddWithValue("@Take", PageSize);
                });

            _total = SqlHelper.ExecuteScalarInt("SELECT COUNT(*) FROM dbo.AuditLogs a " + Where, AddParams);
            _pageCount = Math.Max(1, (int)Math.Ceiling(_total / (double)PageSize));
            if (_page > _pageCount) _page = _pageCount;
            TotalShown = _total + (_total == 1 ? " entry" : " entries");

            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm sms-datatable'><thead><tr><th>When</th><th>Activity</th><th>Area</th><th>Detail</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0)
                sb.Append("<tr><td colspan='4' class='msg-empty'>No activity matches these filters.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                string action = Convert.ToString(r["Action"]);
                string entity = Convert.ToString(r["EntityTable"]);
                sb.Append("<tr><td class='mono small' style='white-space:nowrap'>")
                  .Append(E(Fmt.Stamp(Convert.ToDateTime(r["CreatedAtUtc"]), _zone, "yyyy-MM-dd HH:mm"))).Append("</td>");
                sb.Append("<td><b>").Append(E(Humanize(action))).Append("</b></td>");
                sb.Append("<td class='small'>").Append(entity.Length == 0 ? "<span class='text-muted'>-</span>" : E(HumanizeEntity(entity)));
                if (r["EntityId"] != DBNull.Value) sb.Append(" <span class='mono text-muted'>#").Append(E(r["EntityId"])).Append("</span>");
                sb.Append("</td>");
                sb.Append("<td class='small'>").Append(E(CleanDetail(Convert.ToString(r["DetailJson"])))).Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            Table = sb.ToString();

            var entities = SqlHelper.FillDataTable(
                "SELECT DISTINCT EntityTable FROM dbo.AuditLogs WHERE UserId = @Me AND EntityTable IS NOT NULL ORDER BY EntityTable",
                p => p.AddWithValue("@Me", CurrentUser.UserId));
            var eo = new StringBuilder();
            foreach (DataRow r in entities.Rows)
            {
                string name = Convert.ToString(r[0]);
                eo.Append("<option value='").Append(Attr(name)).Append("'")
                  .Append(string.Equals(EntityFilter, name, StringComparison.OrdinalIgnoreCase) ? " selected" : "")
                  .Append(">").Append(E(HumanizeEntity(name))).Append("</option>");
            }
            EntityOptions = eo.ToString();
            PagerHtml = BuildPager();
        }

        private string BuildPager()
        {
            if (_pageCount <= 1)
                return "<div class='pager small text-muted'>Showing all " + _total + " entries.</div>";
            var sb = new StringBuilder("<div class='pager d-flex justify-content-between align-items-center mt-2 small'>");
            sb.Append("<span>Page " + _page + " of " + _pageCount + " - " + _total + " entries</span><span>");
            if (_page > 1) sb.Append("<a class='imbtn' href='").Append(PageUrl(_page - 1)).Append("'>Newer</a> ");
            if (_page < _pageCount) sb.Append("<a class='imbtn' href='").Append(PageUrl(_page + 1)).Append("'>Older</a>");
            sb.Append("</span></div>");
            return sb.ToString();
        }

        private string PageUrl(int page)
        {
            var parts = new List<string>();
            Action<string, string> add = (k, v) => { if (!string.IsNullOrEmpty(v)) parts.Add(k + "=" + HttpUtility.UrlEncode(v)); };
            add("q", Q); add("action", ActionFilter); add("entity", EntityFilter); add("from", From); add("to", To);
            parts.Add("page=" + page);
            return "/Account/Activity.aspx?" + string.Join("&", parts);
        }

        protected string ExportUrl(string format)
        {
            var parts = new List<string>();
            Action<string, string> add = (k, v) => { if (!string.IsNullOrEmpty(v)) parts.Add(k + "=" + HttpUtility.UrlEncode(v)); };
            add("action", ActionFilter); add("term", Q); add("entity", EntityFilter); add("from", From); add("to", To);
            return "/Handlers/Export.ashx?scope=myactivity&format=" + format + (parts.Count > 0 ? "&" + string.Join("&", parts) : "");
        }

        private static string Humanize(string action)
        {
            if (string.IsNullOrEmpty(action)) return "Activity";
            switch (action)
            {
                case "Login": return "Signed in";
                case "Logout": return "Signed out";
                case "LoginFailed": return "Failed sign-in";
                case "PasswordChanged": return "Password changed";
                case "PasswordReset": return "Password reset";
                case "ProfileUpdated": return "Profile updated";
                case "EmailChanged": return "Email address changed";
                case "TestAttemptStarted": return "Started an online test";
                case "TestSubmitted": return "Submitted an online test";
                case "StudentProfileCreated": return "Student profile created";
                case "RegistrationApproved": return "Registration approved";
                case "AttendanceMarked": return "Attendance marked";
                case "MarksSaved": return "Marks saved";
                case "ComplaintCreated": return "Complaint raised";
                case "ComplaintReplied": return "Complaint replied";
                case "DataExport": return "Data exported";
            }
            string spaced = Regex.Replace(action, "([a-z0-9])([A-Z])", "$1 $2");
            return spaced.Length == 0 ? action : char.ToUpperInvariant(spaced[0]) + spaced.Substring(1);
        }

        private static string HumanizeEntity(string entity)
        {
            if (string.IsNullOrEmpty(entity)) return "";
            string spaced = Regex.Replace(entity, "([a-z0-9])([A-Z])", "$1 $2");
            return spaced.Length == 0 ? entity : char.ToUpperInvariant(spaced[0]) + spaced.Substring(1);
        }

        private static string CleanDetail(string detail)
        {
            if (string.IsNullOrEmpty(detail)) return "";
            string text = detail.Replace("{", "").Replace("}", "").Replace("\"", "").Replace(":", ": ");
            text = text.Replace("  ", " ").Trim();
            return text.Length <= 180 ? text : text.Substring(0, 177) + "...";
        }
    }
}
