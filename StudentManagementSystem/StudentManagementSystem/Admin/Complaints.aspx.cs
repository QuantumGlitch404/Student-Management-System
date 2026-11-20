using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Admin
{
    /// <summary>Complaint ticket queue with priority, assignment, timeline and resolution workflow.</summary>
    public partial class Complaints : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, ComplaintTable, DetailHead, DetailBody, ResponseForm, WorkflowForm;
        protected string SummaryCards, StatusOptions, PriorityOptions, CategoryOptions, Q, FilterQuery;
        protected int SelectedId;

        private static readonly string[] Statuses = { "open", "assigned", "in_progress", "waiting", "resolved", "closed", "reopened" };
        private static readonly string[] Priorities = { "low", "normal", "high", "urgent" };

        protected void Page_Load(object sender, EventArgs e)
        {
            // Read the ticket id before handling a POST: action redirects keep the
            // user on the same ticket, and SelectedId is used inside HandlePost.
            int.TryParse(Request.QueryString["id"], out SelectedId);
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            Q = (Request.QueryString["q"] ?? "").Trim();
            BuildOptions();
            BuildSummary();
            BuildTable();
            FilterQuery = BuildFilterQuery();
            if (SelectedId > 0) BuildDetail();
        }

        private string BuildFilterQuery()
        {
            var sb = new StringBuilder();
            Action<string, string> add = (k, v) => { if (!string.IsNullOrEmpty(v)) sb.Append("&" + k + "=" + HttpUtility.UrlEncode(v)); };
            add("status", Request.QueryString["status"]);
            add("priority", Request.QueryString["priority"]);
            add("category", Request.QueryString["category"]);
            add("q", Q);
            return sb.ToString();
        }

        private void HandlePost()
        {
            string error = null;
            try
            {
                switch (Request.Form["op"])
                {
                    case "reply":
                        {
                            int complaintId = int.Parse(Request.Form["complaintId"]);
                            string message = (Request.Form["message"] ?? "").Trim();
                            bool internalNote = Request.Form["internal"] == "1";
                            if (message.Length == 0) throw new Exception("Message is empty.");
                            CommsRepo.AddThread(complaintId, CurrentUser.UserId, message, internalNote);
                            CommsRepo.UpdateComplaint(complaintId, Request.Form["setStatus"] ?? "waiting", null, null, CurrentUser.UserId);
                            Flash("success", internalNote ? "Internal note saved." : "Reply posted and ticket set to waiting for response.");
                            break;
                        }
                    case "status":
                        {
                            int complaintId = int.Parse(Request.Form["complaintId"]);
                            string status = (Request.Form["status"] ?? "open").Trim();
                            CommsRepo.UpdateComplaint(complaintId, status, null,
                                (Request.Form["resolutionNote"] ?? "").Trim(), CurrentUser.UserId);
                            Flash("success", "Ticket updated to " + status.Replace('_', ' ') + ".");
                            break;
                        }
                    case "assign":
                        {
                            int complaintId = int.Parse(Request.Form["complaintId"]);
                            int staffId = int.Parse(Request.Form["staffId"]);
                            CommsRepo.AssignComplaint(complaintId, staffId, CurrentUser.UserId);
                            Flash("success", "Ticket assigned.");
                            break;
                        }
                    case "priority":
                        {
                            int complaintId = int.Parse(Request.Form["complaintId"]);
                            CommsRepo.SetComplaintPriority(complaintId, (Request.Form["priority"] ?? "normal").Trim(), CurrentUser.UserId);
                            Flash("info", "Priority updated.");
                            break;
                        }
                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }
            string keepId = SelectedId > 0 ? "?id=" + SelectedId : "";
            Redirect("/Admin/Complaints.aspx" + keepId
                + (error != null ? (keepId.Length > 0 ? "&" : "?") + "err=" + HttpUtility.UrlEncode(error) : ""));
        }

        private void BuildOptions()
        {
            string curStatus = Request.QueryString["status"] ?? "";
            var so = new StringBuilder("<option value=''>All statuses</option>");
            foreach (string s in Statuses)
                so.Append("<option value='" + s + "'" + (curStatus == s ? " selected" : "") + ">" + Label(s) + "</option>");
            StatusOptions = so.ToString();

            string curPriority = Request.QueryString["priority"] ?? "";
            var po = new StringBuilder("<option value=''>All priorities</option>");
            foreach (string p in Priorities)
                po.Append("<option value='" + p + "'" + (curPriority == p ? " selected" : "") + ">" + Label(p) + "</option>");
            PriorityOptions = po.ToString();

            string curCategory = Request.QueryString["category"] ?? "";
            var co = new StringBuilder();
            foreach (DataRow r in CommsRepo.ComplaintCategories().Rows)
            {
                string name = Convert.ToString(r["CatName"]);
                co.Append("<option value='" + Attr(name) + "'" + (curCategory == name ? " selected" : "") + ">" + E(name) + "</option>");
            }
            CategoryOptions = co.ToString();
        }

        private void BuildSummary()
        {
            if (CommsRepo.ComplaintSummary().Rows.Count == 0) { SummaryCards = ""; return; }
            DataRow r = CommsRepo.ComplaintSummary().Rows[0];
            var sb = new StringBuilder("<div class='bento'>");
            Kpi(sb, "Total tickets", Convert.ToInt32(r["Total"]).ToString("N0"), "bi-ticket-detailed");
            Kpi(sb, "Open", Convert.ToInt32(r["OpenCount"]).ToString("N0"), "bi-envelope-open");
            Kpi(sb, "High / urgent", Convert.ToInt32(r["Escalated"]).ToString("N0"), "bi-exclamation-triangle");
            Kpi(sb, "Resolved / closed", Convert.ToInt32(r["ClosedCount"]).ToString("N0"), "bi-check2-circle");
            Kpi(sb, "Avg resolution", Convert.ToDecimal(r["AvgHours"]).ToString("0.#") + " h", "bi-stopwatch");
            sb.Append("</div>");
            SummaryCards = sb.ToString();
        }

        private static void Kpi(StringBuilder sb, string label, string value, string icon)
        {
            sb.Append("<div class='c-3'><div class='stat-card'><div class='stat-head'>").Append(label).Append("</div><div class='stat-body'><div class='num'>").Append(value).Append("</div></div></div></div>");
        }

        private void BuildTable()
        {
            DataTable dt = CommsRepo.Complaints(null, null,
                Request.QueryString["status"] ?? "", Request.QueryString["priority"] ?? "",
                Request.QueryString["category"] ?? "", Q, 300);
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Ticket</th><th>Subject</th><th>Student</th><th>Category</th><th>Priority</th><th>Status</th><th>Assigned</th><th>Age</th><th></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='9' class='msg-empty'>No tickets match these filters.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                DateTime created = (DateTime)r["CreatedAtUtc"];
                string age = ((int)(DateTime.UtcNow - created).TotalDays) + "d";
                sb.Append("<tr><td class='mono'><b>" + E(r["ComplaintNo"]) + "</b></td>");
                sb.Append("<td>" + Trunc(r["Subject"] as string, 60) + "</td>");
                sb.Append("<td>" + E(r["RaisedByName"]) + "</td><td>" + E(r["CatName"]) + "</td>");
                sb.Append("<td><span class='badge " + PriorityClass(Convert.ToString(r["Priority"])) + "'>" + E(r["Priority"]) + "</span></td>");
                sb.Append("<td><span class='badge " + StatusClass(Convert.ToString(r["Status"])) + "'>" + Label(Convert.ToString(r["Status"])) + "</span></td>");
                sb.Append("<td>" + (r["AssignedName"] == DBNull.Value ? "<span class='text-muted'>Unassigned</span>" : E(r["AssignedName"])) + "</td>");
                sb.Append("<td class='mono small'>" + age + "</td>");
                sb.Append("<td><a class='imbtn' href='/Admin/Complaints.aspx?id=" + E(r["ComplaintId"]) + "'>Open</a></td></tr>");
            }
            sb.Append("</tbody></table>");
            ComplaintTable = sb.ToString();
        }

        private void BuildDetail()
        {
            DataRow c = CommsRepo.OneComplaint(SelectedId);
            if (c == null) { DetailHead = "Ticket not found"; DetailBody = "<p class='msg-empty'>No such complaint.</p>"; ResponseForm = WorkflowForm = ""; return; }
            DetailHead = c["ComplaintNo"] + " - " + c["Subject"] + " (" + Label(Convert.ToString(c["Status"])) + ")";

            var sb = new StringBuilder();
            sb.Append("<div class='d-flex flex-wrap gap-2 mb-2'>");
            sb.Append("<span class='badge " + PriorityClass(Convert.ToString(c["Priority"])) + "'>" + E(c["Priority"]) + " priority</span>");
            sb.Append("<span class='badge " + StatusClass(Convert.ToString(c["Status"])) + "'>" + Label(Convert.ToString(c["Status"])) + "</span>");
            sb.Append("<span class='badge badge-info'>" + E(c["CatName"]) + "</span>");
            sb.Append("<span class='badge badge-off mono'>" + E(c["ComplaintNo"]) + "</span></div>");
            sb.Append("<div class='small text-muted mb-2'>Raised by <b>" + E(c["RaisedByName"]) + "</b> (" + E(c["RaisedByEmail"]) + ")"
                + " on " + ((DateTime)c["CreatedAtUtc"]).ToString("yyyy-MM-dd HH:mm") + " UTC"
                + (c["AssignedName"] == DBNull.Value ? " - currently unassigned" : " - assigned to " + E(c["AssignedName"])) + "</div>");
            sb.Append("<p class='border-bottom pb-2'>" + E(c["Description"]) + "</p>");
            if (c["ResolutionNote"] != DBNull.Value && Convert.ToString(c["ResolutionNote"]).Length > 0)
                sb.Append("<div class='alert alert-success py-2 small'><b>Resolution:</b> " + E(c["ResolutionNote"]) + "</div>");

            DataTable th = CommsRepo.ComplaintThread(SelectedId);
            sb.Append("<h6 class='mt-3'>Conversation</h6><div class='mb-2'>");
            bool anyT = false;
            foreach (DataRow t in th.Rows)
            {
                anyT = true;
                bool internalNote = Convert.ToBoolean(t["IsInternal"]);
                sb.Append("<div class='complaint-bubble" + (internalNote ? " internal" : "") + "'>");
                sb.Append("<div class='small fw-semibold'>" + E(t["AuthorName"]) + (t["AuthorRole"] == DBNull.Value ? "" : " (" + E(t["AuthorRole"]) + ")")
                    + (internalNote ? " - internal note" : "") + "</div>");
                sb.Append("<div>" + E(t["Message"]) + "</div>");
                sb.Append("<div class='small text-muted mono'>" + ((DateTime)t["CreatedAtUtc"]).ToString("yyyy-MM-dd HH:mm") + " UTC</div></div>");
            }
            if (!anyT) sb.Append("<div class='small text-muted'>No replies yet.</div>");
            sb.Append("</div>");

            DataTable ev = CommsRepo.ComplaintEvents(SelectedId);
            sb.Append("<h6 class='mt-3'>Status timeline</h6><ol class='timeline small'>");
            if (ev.Rows.Count == 0) sb.Append("<li class='text-muted'>No recorded transitions.</li>");
            foreach (DataRow e in ev.Rows)
            {
                string change = e["OldValue"] == DBNull.Value || Convert.ToString(e["OldValue"]).Length == 0
                    ? Convert.ToString(e["NewValue"]) : Convert.ToString(e["OldValue"]) + " &rarr; " + Convert.ToString(e["NewValue"]);
                sb.Append("<li><span class='mono text-muted'>" + ((DateTime)e["CreatedAtUtc"]).ToString("yyyy-MM-dd HH:mm") + "</span> ");
                sb.Append("<b>" + Label(Convert.ToString(e["EventType"])) + "</b> by " + (e["ActorName"] == DBNull.Value ? "system" : E(e["ActorName"])));
                sb.Append(" <span class='text-muted'>" + change + "</span></li>");
            }
            sb.Append("</ol>");
            DetailBody = sb.ToString();

            var res = new StringBuilder();
            res.Append("<form method='post' action='/Admin/Complaints.aspx' class='form-grid'>" + CsrfField());
            res.Append("<input type='hidden' name='op' value='reply'/><input type='hidden' name='complaintId' value='" + SelectedId + "'/>");
            res.Append("<div class='c-12'><textarea name='message' rows='3' required class='form-control form-control-sm' maxlength='1000' placeholder='Write a reply the student will see'></textarea></div>");
            res.Append("<div class='c-6 d-flex align-items-center gap-2'><label class='form-check-label small'><input type='checkbox' name='internal' value='1'/> internal note</label></div>");
            res.Append("<div class='c-6 d-flex justify-content-end gap-2'>");
            res.Append("<select name='setStatus' class='form-select form-select-sm' style='max-width:160px'>");
            res.Append("<option value='waiting'>Waiting for response</option><option value='in_progress'>In progress</option><option value='resolved'>Resolved</option></select>");
            res.Append("<button class='btn btn-sms btn-sm'>Post reply</button></div></form>");
            ResponseForm = res.ToString();

            var wf = new StringBuilder();
            wf.Append("<form method='post' action='/Admin/Complaints.aspx' class='form-grid'>" + CsrfField());
            wf.Append("<input type='hidden' name='op' value='assign'/><input type='hidden' name='complaintId' value='" + SelectedId + "'/>");
            wf.Append("<div class='c-8'><label class='form-label small'>Assign to</label><select name='staffId' class='form-select form-select-sm'>");
            foreach (DataRow s in CommsRepo.StaffForAssignment().Rows)
            {
                bool selected = c["AssignedTo"] != DBNull.Value && Convert.ToInt32(c["AssignedTo"]) == Convert.ToInt32(s["UserId"]);
                wf.Append("<option value='" + s["UserId"] + "'" + (selected ? " selected" : "") + ">" + E(s["FullName"]) + " (" + E(s["RoleName"]) + ")</option>");
            }
            wf.Append("</select></div><div class='c-4 d-flex align-items-end'><button class='btn btn-sms btn-sm'>Assign</button></div></form>");

            wf.Append("<form method='post' action='/Admin/Complaints.aspx' class='form-grid'>" + CsrfField());
            wf.Append("<input type='hidden' name='op' value='priority'/><input type='hidden' name='complaintId' value='" + SelectedId + "'/>");
            wf.Append("<div class='c-8'><label class='form-label small'>Priority</label><select name='priority' class='form-select form-select-sm'>");
            foreach (string p in Priorities)
                wf.Append("<option value='" + p + "'" + (p == Convert.ToString(c["Priority"]) ? " selected" : "") + ">" + Label(p) + "</option>");
            wf.Append("</select></div><div class='c-4 d-flex align-items-end'><button class='btn btn-sms btn-sm'>Set</button></div></form>");

            wf.Append("<form method='post' action='/Admin/Complaints.aspx' class='form-grid'>" + CsrfField());
            wf.Append("<input type='hidden' name='op' value='status'/><input type='hidden' name='complaintId' value='" + SelectedId + "'/>");
            wf.Append("<div class='c-12'><label class='form-label small'>Status</label><select name='status' class='form-select form-select-sm'>");
            foreach (string s in Statuses)
                wf.Append("<option value='" + s + "'" + (s == Convert.ToString(c["Status"]) ? " selected" : "") + ">" + Label(s) + "</option>");
            wf.Append("</select></div>");
            wf.Append("<div class='c-12'><label class='form-label small'>Resolution note (visible to the student)</label>");
            wf.Append("<textarea name='resolutionNote' rows='2' class='form-control form-control-sm' maxlength='500'>" + E(c["ResolutionNote"]) + "</textarea></div>");
            wf.Append("<div class='c-12'><button class='btn btn-sms btn-sm'>Update ticket</button></div></form>");
            WorkflowForm = wf.ToString();
        }

        protected static string StatusClass(string status)
        {
            switch (status)
            {
                case "open": case "reopened": return "badge-warn";
                case "assigned": return "badge-info";
                case "in_progress": return "badge-info";
                case "waiting": return "badge-off";
                case "resolved": return "badge-ok";
                case "closed": return "badge-off";
                default: return "badge-off";
            }
        }

        protected static string PriorityClass(string priority)
        {
            switch (priority)
            {
                case "urgent": return "badge-bad";
                case "high": return "badge-warn";
                case "low": return "badge-off";
                default: return "badge-info";
            }
        }

        protected static string Label(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            string s = value.Replace('_', ' ');
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        protected static string Trunc(string value, int len)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= len ? HttpUtility.HtmlEncode(value)
                : HttpUtility.HtmlEncode(value.Substring(0, len - 3)) + "...";
        }
    }
}

