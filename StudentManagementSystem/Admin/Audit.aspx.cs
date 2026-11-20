using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Admin
{
    /// <summary>Read-only audit log with multi-field filters, paging and export.</summary>
    public partial class Audit : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, Table, EntityOptions, CategoryOptions, TotalShown;
        protected string Q, UserFilter, RoleFilter, ActionFilter, EntityFilter, IpFilter, From, To, CategoryFilter;
        protected int TargetFilter;
        private int _page, _pageCount, _total;
        private const int PageSize = 100;

        protected void Page_Load(object sender, EventArgs e)
        {
            FlashHtml = PopFlash();
            ReadFilters();
            Build();
        }

        private void ReadFilters()
        {
            Q = (Request.QueryString["q"] ?? "").Trim();
            UserFilter = (Request.QueryString["user"] ?? "").Trim();
            RoleFilter = NormalizeRole(Request.QueryString["role"]);
            ActionFilter = (Request.QueryString["action"] ?? "").Trim();
            EntityFilter = (Request.QueryString["entity"] ?? "").Trim();
            IpFilter = (Request.QueryString["ip"] ?? "").Trim();
            From = DateOnly(Request.QueryString["from"]);
            To = DateOnly(Request.QueryString["to"]);
            CategoryFilter = (Request.QueryString["category"] ?? "").Trim();
            int.TryParse(Request.QueryString["target"], out TargetFilter);
            int.TryParse(Request.QueryString["page"], out _page);
            if (_page < 1) _page = 1;
        }

        private static string NormalizeRole(string role)
        {
            return role == "Admin" || role == "Faculty" || role == "Student" ? role : "";
        }

        private static string DateOnly(string value)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(value ?? "", @"^\d{4}-\d{2}-\d{2}$") ? value : "";
        }

        private const string Where =
            "WHERE (@Action = '' OR a.[Action] LIKE '%' + @Action + '%') " +
            "AND (@Term = '' OR u.FullName LIKE '%' + @Term + '%' OR a.DetailJson LIKE '%' + @Term + '%' OR a.ClientIp LIKE '%' + @Term + '%') " +
            "AND (@UserName = '' OR u.FullName LIKE '%' + @UserName + '%') " +
            "AND (@Role = '' OR r.RoleName = @Role) " +
            "AND (@Entity = '' OR a.EntityTable = @Entity) " +
            "AND (@Category = '' OR a.Category = @Category) " +
            "AND (@Target = 0 OR a.TargetUserId = @Target OR a.UserId = @Target) " +
            "AND (@Ip = '' OR a.ClientIp LIKE '%' + @Ip + '%') " +
            "AND (@From = '' OR CAST(a.CreatedAtUtc AS date) >= @From) " +
            "AND (@To = '' OR CAST(a.CreatedAtUtc AS date) <= @To) ";

        private const string FromJoin =
            "FROM dbo.AuditLogs a " +
            "LEFT JOIN dbo.Users u ON u.UserId = a.UserId " +
            "OUTER APPLY (SELECT TOP 1 rr.RoleName FROM dbo.UserRoles ur JOIN dbo.Roles rr ON rr.RoleId = ur.RoleId WHERE ur.UserId = a.UserId) r ";

        private void AddParams(System.Data.SqlClient.SqlParameterCollection p)
        {
            p.AddWithValue("@Action", ActionFilter);
            p.AddWithValue("@Term", Q);
            p.AddWithValue("@UserName", UserFilter);
            p.AddWithValue("@Role", RoleFilter);
            p.AddWithValue("@Entity", EntityFilter);
            p.AddWithValue("@Ip", IpFilter);
            p.AddWithValue("@From", From);
            p.AddWithValue("@To", To);
            p.AddWithValue("@Category", CategoryFilter);
            p.AddWithValue("@Target", TargetFilter);
        }

        private void Build()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT a.AuditId, a.Action, a.Category, a.TargetUserId, a.EntityTable, a.EntityId, a.DetailJson, a.ClientIp, a.CreatedAtUtc, " +
                "u.FullName, r.RoleName " + FromJoin + Where +
                "ORDER BY a.AuditId DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY",
                p =>
                {
                    AddParams(p);
                    p.AddWithValue("@Skip", (_page - 1) * PageSize);
                    p.AddWithValue("@Take", PageSize);
                });

            _total = SqlHelper.ExecuteScalarInt("SELECT COUNT(*) " + FromJoin + Where, AddParams);
            _pageCount = Math.Max(1, (int)Math.Ceiling(_total / (double)PageSize));
            if (_page > _pageCount) _page = _pageCount;
            TotalShown = _total + (_total == 1 ? " entry" : " entries");

            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm sms-datatable'><thead><tr><th>#</th><th>When (UTC)</th><th>User</th><th>Action</th><th>Entity</th><th>Detail</th><th>IP</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='7' class='msg-empty'>No audit entries match these filters.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                string action = r["Action"] as string ?? "";
                sb.Append("<tr><td class='mono text-muted'>" + E(r["AuditId"]) + "</td>");
                sb.Append("<td class='mono small' style='white-space:nowrap'>" + ((DateTime)r["CreatedAtUtc"]).ToString("yyyy-MM-dd HH:mm:ss") + "</td>");
                sb.Append("<td>" + E(r["FullName"]) + (r["RoleName"] == DBNull.Value || r["RoleName"] == null ? "" :
                    " <span class='badge badge-info'>" + E(r["RoleName"]) + "</span>") + "</td>");
                sb.Append("<td><span class='badge " + ActionClass(action) + "'>" + E(action) + "</span></td>");
                sb.Append("<td class='small'>" + (r["EntityTable"] == DBNull.Value || r["EntityTable"] == null ? "<span class='text-muted'>-</span>" :
                    E(r["EntityTable"]) + (r["EntityId"] == DBNull.Value ? "" : " <span class='mono'>#" + E(r["EntityId"]) + "</span>")) + "</td>");
                sb.Append("<td class='small mono audit-detail'>" + E(Trim(r["DetailJson"] as string, 240)) + "</td>");
                sb.Append("<td class='mono small'>" + E(r["ClientIp"]) + "</td></tr>");
            }
            sb.Append("</tbody></table>");
            Table = sb.ToString() + Pager();

            var entities = SqlHelper.FillDataTable("SELECT DISTINCT EntityTable FROM dbo.AuditLogs WHERE EntityTable IS NOT NULL ORDER BY EntityTable");
            var eo = new StringBuilder();
            foreach (DataRow r in entities.Rows)
            {
                string name = Convert.ToString(r[0]);
                eo.Append("<option value='" + Attr(name) + "'" + Sel(EntityFilter, name) + ">" + E(name) + "</option>");
            }
            EntityOptions = eo.ToString();

            var cats = SqlHelper.FillDataTable("SELECT DISTINCT Category FROM dbo.AuditLogs WHERE Category IS NOT NULL AND Category<>'' ORDER BY Category");
            var co = new StringBuilder();
            foreach (DataRow r in cats.Rows)
            {
                string name = Convert.ToString(r[0]);
                co.Append("<option value='" + Attr(name) + "'" + Sel(CategoryFilter, name) + ">" + E(name) + "</option>");
            }
            CategoryOptions = co.ToString();
        }

        private string Pager()
        {
            if (_pageCount <= 1) return "<div class='pager small text-muted'>Showing all " + _total + " entries.</div>";
            var sb = new StringBuilder("<div class='pager d-flex justify-content-between align-items-center mt-2 small'>");
            sb.Append("<span>Page " + _page + " of " + _pageCount + " - " + _total + " entries</span><span>");
            if (_page > 1) sb.Append("<a class='imbtn' href='" + PageUrl(_page - 1) + "'>Newer</a> ");
            if (_page < _pageCount) sb.Append("<a class='imbtn' href='" + PageUrl(_page + 1) + "'>Older</a>");
            sb.Append("</span></div>");
            return sb.ToString();
        }

        private string QueryString(bool includePage)
        {
            var parts = new List<string>();
            Action<string, string> add = (k, v) => { if (!string.IsNullOrEmpty(v)) parts.Add(k + "=" + HttpUtility.UrlEncode(v)); };
            add("q", Q); add("user", UserFilter); add("role", RoleFilter); add("action", ActionFilter);
            add("entity", EntityFilter); add("ip", IpFilter); add("from", From); add("to", To); add("category", CategoryFilter); add("target", TargetFilter > 0 ? TargetFilter.ToString() : "");
            if (includePage && _page > 1) add("page", _page.ToString());
            return string.Join("&", parts);
        }

        private string PageUrl(int page)
        {
            var parts = new List<string>();
            Action<string, string> add = (k, v) => { if (!string.IsNullOrEmpty(v)) parts.Add(k + "=" + HttpUtility.UrlEncode(v)); };
            add("q", Q); add("user", UserFilter); add("role", RoleFilter); add("action", ActionFilter);
            add("entity", EntityFilter); add("ip", IpFilter); add("from", From); add("to", To); add("category", CategoryFilter); add("target", TargetFilter > 0 ? TargetFilter.ToString() : "");
            parts.Add("page=" + page);
            return "/Admin/Audit.aspx?" + string.Join("&", parts);
        }

        protected string ExportUrl(string format)
        {
            string qs = QueryString(false);
            return "/Handlers/Export.ashx?scope=audit&format=" + format + (qs.Length > 0 ? "&" + qs : "");
        }

        protected string Sel(string current, string value)
        {
            return string.Equals(current, value, StringComparison.OrdinalIgnoreCase) ? " selected" : "";
        }

        private static string ActionClass(string action)
        {
            string a = (action ?? "").ToLowerInvariant();
            if (a.Contains("fail") || a.Contains("delete") || a.Contains("deactivat")) return "badge-bad";
            if (a.Contains("login") || a.Contains("export")) return "badge-info";
            if (a.Contains("create") || a.Contains("publish") || a.Contains("verify")) return "badge-ok";
            return "badge-off";
        }

        private static string Trim(string value, int len)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= len ? value : value.Substring(0, len - 1) + "\u2026";
        }
    }
}

