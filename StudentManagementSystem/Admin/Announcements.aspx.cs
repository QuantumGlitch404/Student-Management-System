using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Admin
{
    /// <summary>Announcement board: scheduling, targeting, pinning, acknowledgements.</summary>
    public partial class Announcements : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, AnnTable, CategoryOptions, AudienceOptions, CourseOptions, SemesterOptions;
        protected string FTitle = "", FBody = "", FPublishedFrom = "", FPublishedTo = "", TotalLabel;
        protected bool Editing, FPinned;
        protected int AnnId;

        private static readonly string[] Categories = { "general", "academic", "exam", "fee", "event", "security" };

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            AnnId = ParseInt(Request.QueryString["id"]);
            if (AnnId > 0) LoadForEdit();
            BuildOptions();
            BuildTable();
        }

        private static int ParseInt(string value)
        {
            int n;
            return int.TryParse(value, out n) ? n : 0;
        }

        private void LoadForEdit()
        {
            DataRow r = CommsRepo.OneAnnouncement(AnnId);
            if (r == null) { AnnId = 0; return; }
            Editing = true;
            FTitle = Convert.ToString(r["AnnTitle"]);
            FBody = Convert.ToString(r["AnnBody"]);
            FPinned = r["IsPinned"] != DBNull.Value && Convert.ToBoolean(r["IsPinned"]);
            FPublishedFrom = r["PublishFrom"] == DBNull.Value ? "" : ((DateTime)r["PublishFrom"]).ToString("yyyy-MM-dd");
            FPublishedTo = r["PublishTo"] == DBNull.Value ? "" : ((DateTime)r["PublishTo"]).ToString("yyyy-MM-dd");
        }

        private void HandlePost()
        {
            string error = null;
            try
            {
                switch (Request.Form["op"])
                {
                    case "new":
                    case "update":
                        {
                            string title = (Request.Form["title"] ?? "").Trim();
                            string body = (Request.Form["body"] ?? "").Trim();
                            string audience = Allow(Request.Form["audience"], new[] { "all", "students", "faculty" }, "all");
                            string category = Allow(Request.Form["category"], Categories, "general");
                            string from = DateOrEmpty(Request.Form["publishFrom"]);
                            string to = DateOrEmpty(Request.Form["publishTo"]);
                            int? courseId = ParseInt(Request.Form["targetCourseId"]) > 0 ? ParseInt(Request.Form["targetCourseId"]) : (int?)null;
                            int? semester = ParseInt(Request.Form["targetSemester"]) > 0 ? ParseInt(Request.Form["targetSemester"]) : (int?)null;
                            bool pinned = Request.Form["pinned"] == "1";
                            if (title.Length == 0 || body.Length == 0) throw new Exception("Title and body are required.");

                            if (Request.Form["op"] == "update")
                            {
                                int id = ParseInt(Request.Form["annId"]);
                                CommsRepo.UpdateAnnouncement(id, title, body, audience, category, from, to, courseId, semester, pinned);
                                Flash("success", "Announcement updated.");
                            }
                            else
                            {
                                CommsRepo.CreateAnnouncement(title, body, audience, category, CurrentUser.UserId, from, to, courseId, semester, pinned);
                                Flash("success", "Announcement published and notifications sent.");
                            }
                            break;
                        }
                    case "toggle":
                        CommsRepo.ToggleAnnouncement(ParseInt(Request.Form["annId"]));
                        Flash("info", "Announcement visibility toggled.");
                        break;
                    case "pin":
                        CommsRepo.ToggleAnnouncementPin(ParseInt(Request.Form["annId"]));
                        Flash("info", "Pin state updated.");
                        break;
                    case "delete":
                        CommsRepo.DeleteAnnouncement(ParseInt(Request.Form["annId"]));
                        Flash("info", "Announcement deleted.");
                        break;
                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }
            Redirect("/Admin/Announcements.aspx" + (error != null ? "?err=" + HttpUtility.UrlEncode(error) : ""));
        }

        private static string DateOrEmpty(string value)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(value ?? "", @"^\d{4}-\d{2}-\d{2}$") ? value : "";
        }

        private static string Allow(string value, string[] allowed, string fallback)
        {
            foreach (string a in allowed) if (string.Equals(a, value, StringComparison.OrdinalIgnoreCase)) return a;
            return fallback;
        }

        private void BuildOptions()
        {
            var cats = new StringBuilder();
            string selectedCategory = "general";
            if (Editing)
            {
                DataRow r = CommsRepo.OneAnnouncement(AnnId);
                if (r != null) selectedCategory = Convert.ToString(r["Category"]);
            }
            foreach (string c in Categories)
                cats.Append("<option value='" + c + "'" + (c == selectedCategory ? " selected" : "") + ">" + Capitalize(c) + "</option>");
            CategoryOptions = cats.ToString();

            string aud = "all";
            if (Editing)
            {
                DataRow r = CommsRepo.OneAnnouncement(AnnId);
                if (r != null) aud = Convert.ToString(r["Audience"]);
            }
            var ao = new StringBuilder();
            foreach (string a in new[] { "all", "students", "faculty" })
                ao.Append("<option value='" + a + "'" + (a == aud ? " selected" : "") + ">" +
                    (a == "all" ? "Everyone" : a == "students" ? "Students" : "Faculty") + "</option>");
            AudienceOptions = ao.ToString();

            DataTable courses = AcademicsRepo.Courses();
            var co = new StringBuilder();
            int? targetCourse = null, targetSemester = null;
            if (Editing)
            {
                DataRow r = CommsRepo.OneAnnouncement(AnnId);
                if (r != null)
                {
                    if (r["TargetCourseId"] != DBNull.Value) targetCourse = Convert.ToInt32(r["TargetCourseId"]);
                    if (r["TargetSemester"] != DBNull.Value) targetSemester = Convert.ToInt32(r["TargetSemester"]);
                    // Audience is fetched above; the selected values drive the dropdowns.
                }
            }
            foreach (DataRow c in courses.Rows)
            {
                int id = Convert.ToInt32(c["CourseId"]);
                co.Append("<option value='" + id + "'" + (targetCourse == id ? " selected" : "") + ">" +
                    E(c["CourseCode"]) + " - " + E(c["CourseName"]) + "</option>");
            }
            CourseOptions = co.ToString();

            var so = new StringBuilder();
            for (int i = 1; i <= 8; i++)
                so.Append("<option value='" + i + "'" + (targetSemester == i ? " selected" : "") + ">Semester " + i + "</option>");
            SemesterOptions = so.ToString();
        }

        private void BuildTable()
        {
            DataTable dt = CommsRepo.AnnouncementsAll();
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Title</th><th>Category</th><th>Audience / target</th><th>Window</th><th>Status</th><th>Acks</th><th>Actions</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='7' class='msg-empty'>No announcement yet. Create the first one above.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                bool isActive = Convert.ToBoolean(r["IsActive"]);
                bool pinned = r["IsPinned"] != DBNull.Value && Convert.ToBoolean(r["IsPinned"]);
                int ackCount = r["AckCount"] == DBNull.Value ? 0 : Convert.ToInt32(r["AckCount"]);
                sb.Append("<tr><td>" + (pinned ? "<i class='bi bi-pin-angle-fill' title='Pinned'></i> " : "") +
                    "<b>" + E(r["AnnTitle"]) + "</b><div class='small text-muted'>" + Snippet(r["AnnBody"] as string) + "</div></td>");
                sb.Append("<td><span class='badge badge-info'>" + E(r["Category"]) + "</span></td>");
                sb.Append("<td class='small'>" + AudienceLabel(Convert.ToString(r["Audience"])));
                if (r["TargetCourse"] != DBNull.Value) sb.Append("<div>Course: " + E(r["TargetCourse"]) + "</div>");
                if (r["TargetSemester"] != DBNull.Value) sb.Append("<div>Semester: " + E(r["TargetSemester"]) + "</div>");
                if (r["TargetCourseId"] == DBNull.Value && r["TargetSemester"] == DBNull.Value) sb.Append("<div class='text-muted'>All courses</div>");
                sb.Append("</td>");
                sb.Append("<td class='mono small'>" + FormatDate(r["PublishFrom"]) + (r["PublishTo"] == DBNull.Value ? " &rarr; no end" : " &rarr; " + FormatDate(r["PublishTo"])) + "</td>");
                sb.Append("<td><span class='badge " + (isActive ? "badge-ok" : "badge-off") + "'>" + (isActive ? "Published" : "Hidden") + "</span></td>");
                sb.Append("<td class='mono'>" + ackCount + "</td>");
                sb.Append("<td>");
                sb.Append(ImbForm("pin", new[] { new[] { "annId", E(r["AnnId"]) } }, pinned ? "Unpin" : "Pin"));
                sb.Append("<a class='imbtn' href='/Admin/Announcements.aspx?id=" + E(r["AnnId"]) + "#edit'>Edit</a> ");
                sb.Append(ImbForm("toggle", new[] { new[] { "annId", E(r["AnnId"]) } }, isActive ? "Hide" : "Publish"));
                sb.Append(ImbForm("delete", new[] { new[] { "annId", E(r["AnnId"]) } }, "Delete", true));
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            AnnTable = sb.ToString();
            TotalLabel = dt.Rows.Count + " announcements";
        }

        private static string AudienceLabel(string audience)
        {
            return audience == "students" ? "Students" : audience == "faculty" ? "Faculty" : "Everyone";
        }

        private static string Capitalize(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private static string Snippet(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length > 120 ? text.Substring(0, 117) + "..." : text;
        }

        private static string FormatDate(object d)
        {
            if (d == null || d == DBNull.Value) return "-";
            DateTime dt;
            return DateTime.TryParse(d.ToString(), out dt) ? dt.ToString("yyyy-MM-dd") : d.ToString();
        }

        private string ImbForm(string op, string[][] fields, string label, bool confirm = false)
        {
            var sb = new StringBuilder();
            sb.Append("<form method='post' action='/Admin/Announcements.aspx' style='display:inline'")
              .Append(confirm ? " data-confirm='Delete this announcement? This cannot be undone.'" : "").Append(">");
            sb.Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='").Append(Attr(op)).Append("'/>");
            foreach (string[] f in fields)
                sb.Append("<input type='hidden' name='").Append(f[0]).Append("' value='").Append(f[1]).Append("'/>");
            sb.Append("<button class='imbtn").Append(op == "delete" ? " danger" : "").Append("'>").Append(label).Append("</button></form> ");
            return sb.ToString();
        }
    }
}
