using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Faculty
{
    /// <summary>Complaints assigned to me + replies. Faculty only sees what they own.</summary>
    public partial class Complaints : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Faculty" }; } }

        protected string FlashHtml, ListTable, DetailHead, DetailBody;
        protected int SelectedId;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            int.TryParse(Request.QueryString["id"], out SelectedId);
            BuildList();
            if (SelectedId > 0) BuildDetail();
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
                            DataRow c = CommsRepo.OneComplaint(complaintId);
                            if (c == null || (c["AssignedTo"] == DBNull.Value || c["AssignedTo"] == null ? 0 : Convert.ToInt32(c["AssignedTo"])) != CurrentUser.UserId)
                                throw new Exception("That complaint is not assigned to you.");
                            string message = (Request.Form["message"] ?? "").Trim();
                            if (message.Length == 0) throw new Exception("Message is empty.");
                            CommsRepo.AddThread(complaintId, CurrentUser.UserId, message, false);
                            Flash("success", "Reply posted.");
                            break;
                        }
                    case "status":
                        {
                            int complaintId = int.Parse(Request.Form["complaintId"]);
                            string status = (Request.Form["status"] ?? "").Trim();
                            if (status != "in_progress" && status != "resolved") throw new Exception("Pick in_progress or resolved.");
                            CommsRepo.UpdateComplaint(complaintId, status, CurrentUser.UserId);
                            Flash("success", "Ticket set to " + status + ".");
                            break;
                        }
                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }
            string keepId = SelectedId > 0 ? "?id=" + SelectedId : "";
            Redirect("/Faculty/Complaints.aspx" + keepId
                + (error != null ? (keepId.Length > 0 ? "&" : "?") + "err=" + HttpUtility.UrlEncode(error) : ""));
        }

        private void BuildList()
        {
            DataTable dt = CommsRepo.Complaints(null, CurrentUser.UserId, "", "", 50);
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Ticket</th><th>Subject</th><th>Status</th><th></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='4' class='msg-empty'>Nothing assigned to you.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                sb.Append("<tr><td><b>" + E(r["ComplaintNo"]) + "</b></td>");
                sb.Append("<td>" + E(r["Subject"]) + "</td>");
                sb.Append("<td><span class='badge " + CommsRepo.StatusClass(r["Status"] as string) + "'>" + E(r["Status"]) + "</span></td>");
                sb.Append("<td><a class='imbtn' href='/Faculty/Complaints.aspx?id=" + E(r["ComplaintId"]) + "'>Open</a></td></tr>");
            }
            sb.Append("</tbody></table>");
            ListTable = sb.ToString();
        }

        private void BuildDetail()
        {
            DataRow c = CommsRepo.OneComplaint(SelectedId);
            if (c == null || (c["AssignedTo"] == DBNull.Value || c["AssignedTo"] == null ? 0 : Convert.ToInt32(c["AssignedTo"])) != CurrentUser.UserId)
            { DetailHead = "Ticket unavailable"; DetailBody = "<p class='msg-empty'>Not assigned to you.</p>"; return; }

            DetailHead = c["ComplaintNo"] + " - " + c["Subject"];

            var sb = new StringBuilder();
            sb.Append("<div class='small text-muted mb-2'>From <b>" + E(c["RaisedByName"]) + "</b> in <b>" + E(c["CatName"]) + "</b> at " + ((DateTime)c["CreatedAtUtc"]).ToString("yyyy-MM-dd HH:mm") + " UTC</div>");
            sb.Append("<p class='border-bottom pb-2'>" + E(c["Description"]) + "</p>");
            foreach (DataRow t in CommsRepo.ComplaintThread(SelectedId).Rows)
            {
                if (Convert.ToBoolean(t["IsInternal"])) continue; // faculty never sees internal admin notes
                sb.Append("<div class='border rounded p-2 mb-2' style='background:#eef2fb'>");
                sb.Append("<div class='small fw-semibold'>" + E(t["AuthorName"]) + "</div>");
                sb.Append("<div>" + E(t["Message"]) + "</div>");
                sb.Append("<div class='small text-muted'>" + ((DateTime)t["CreatedAtUtc"]).ToString("yyyy-MM-dd HH:mm") + " UTC</div></div>");
            }

            sb.Append("<form method='post' action='/Faculty/Complaints.aspx' class='form-grid'>");
            sb.Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='reply'/>");
            sb.Append("<input type='hidden' name='complaintId' value='" + SelectedId + "'/>");
            sb.Append("<div class='c-12'><textarea name='message' rows='2' required class='form-control form-control-sm' maxlength='1000' placeholder='Your reply to the student'></textarea></div>");
            sb.Append("<div class='c-12 d-flex gap-2'><button class='btn btn-sms btn-sm'>Post reply</button></div></form>");

            sb.Append("<form method='post' action='/Faculty/Complaints.aspx' class='srch-bar mt-2'>");
            sb.Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='status'/>");
            sb.Append("<input type='hidden' name='complaintId' value='" + SelectedId + "'/>");
            sb.Append("<select name='status' class='form-select form-select-sm' style='max-width:160px'>");
            string cur = (c["Status"] as string ?? "");
            foreach (string s in new[] { "in_progress", "resolved" })
                sb.Append("<option value='" + s + "'" + (s == cur ? " selected" : "") + ">" + s + "</option>");
            sb.Append("</select><button class='btn btn-sms btn-sm'>Update status</button></form>");
            DetailBody = sb.ToString();
        }
    }
}

