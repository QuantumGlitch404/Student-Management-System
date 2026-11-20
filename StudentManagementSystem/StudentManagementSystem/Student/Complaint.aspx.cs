using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Student
{
    /// <summary>Raise + track support tickets; internal notes are never shown to students.</summary>
    public partial class Complaint : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Student" }; } }

        protected string FlashHtml, ListTable, DetailHead, DetailBody;
        protected DataTable Cats;
        protected int SelectedId;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            Cats = CommsRepo.ComplaintCategories();
            int.TryParse(Request.QueryString["id"], out SelectedId);
            BuildList();
            if (SelectedId > 0) BuildDetail();
        }

        private void HandlePost()
        {
            string error = null;
            int newId = 0;
            try
            {
                switch (Request.Form["op"])
                {
                    case "new":
                        {
                            int categoryId; int.TryParse(Request.Form["categoryId"], out categoryId);
                            if (categoryId <= 0) throw new Exception("Pick a category.");
                            string subject = (Request.Form["subject"] ?? "").Trim();
                            string description = (Request.Form["description"] ?? "").Trim();
                            if (subject.Length < 5) throw new Exception("Give the ticket a short subject.");
                            if (description.Length < 10) throw new Exception("Describe the issue with a bit more detail.");
                            int id = CommsRepo.CreateComplaint(CurrentUser.UserId, categoryId, subject,
                                description.Substring(0, Math.Min(2000, description.Length)));
                            newId = id;
                            Flash("success", "Ticket raised. Use the number to follow up.");
                            break;
                        }
                    case "reply":
                        {
                            int complaintId = int.Parse(Request.Form["complaintId"]);
                            DataRow c = CommsRepo.OneComplaint(complaintId);
                            if (c == null || Convert.ToInt32(c["RaisedBy"]) != CurrentUser.UserId)
                                throw new Exception("Ticket not found.");
                            string message = (Request.Form["message"] ?? "").Trim();
                            if (message.Length == 0) throw new Exception("Message is empty.");
                            CommsRepo.AddThread(complaintId, CurrentUser.UserId, message, false);
                            Flash("success", "Reply posted.");
                            break;
                        }
                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }
            string target = newId > 0 ? "/Student/Complaint.aspx?id=" + newId : "/Student/Complaint.aspx";
            if (SelectedId > 0 && newId == 0) target += "?id=" + SelectedId;
            Redirect(target + (error != null ? (target.Contains("?") ? "&" : "?") + "err=" + HttpUtility.UrlEncode(error) : ""));
        }

        private void BuildList()
        {
            DataTable dt = CommsRepo.Complaints(CurrentUser.UserId, null, "", "", 50);
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Ticket</th><th>Subject</th><th>Status</th><th></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='4' class='msg-empty'>No ticket yet.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                sb.Append("<tr><td><b>" + E(r["ComplaintNo"]) + "</b></td><td>" + E(r["Subject"]) + "</td>");
                sb.Append("<td><span class='badge " + CommsRepo.StatusClass(r["Status"] as string) + "'>" + E(r["Status"]) + "</span></td>");
                sb.Append("<td><a class='imbtn' href='/Student/Complaint.aspx?id=" + E(r["ComplaintId"]) + "'>Open</a></td></tr>");
            }
            sb.Append("</tbody></table>");
            ListTable = sb.ToString();
        }

        private void BuildDetail()
        {
            DataRow c = CommsRepo.OneComplaint(SelectedId);
            if (c == null || Convert.ToInt32(c["RaisedBy"]) != CurrentUser.UserId)
            { DetailHead = "Ticket unavailable"; DetailBody = "<p class='msg-empty'>Not found or not yours.</p>"; return; }

            DetailHead = c["ComplaintNo"] + " - " + c["Subject"];
            var sb = new StringBuilder();
            sb.Append("<div class='small text-muted mb-2'>" + E(c["CatName"]) + " - raised " + ((DateTime)c["CreatedAtUtc"]).ToString("yyyy-MM-dd HH:mm") +
                " - handled by <b>" + E(c["AssignedName"] ?? "the office (not assigned yet)") + "</b></div>");
            sb.Append("<p class='border-bottom pb-2'>" + E(c["Description"]) + "</p>");
            foreach (DataRow t in CommsRepo.ComplaintThread(SelectedId).Rows)
            {
                if (Convert.ToBoolean(t["IsInternal"])) continue;
                sb.Append("<div class='border rounded p-2 mb-2' style='background:#eef2fb'>");
                sb.Append("<div class='small fw-semibold'>" + E(t["AuthorName"]) + (t["AuthorRole"] == DBNull.Value || t["AuthorRole"] == null ? "" : " (" + t["AuthorRole"] + ")") + "</div>");
                sb.Append("<div>" + E(t["Message"]) + "</div>");
                sb.Append("<div class='small text-muted'>" + ((DateTime)t["CreatedAtUtc"]).ToString("yyyy-MM-dd HH:mm") + " UTC</div></div>");
            }
            sb.Append("<form method='post' action='/Student/Complaint.aspx' class='form-grid'>");
            sb.Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='reply'/>");
            sb.Append("<input type='hidden' name='complaintId' value='" + SelectedId + "'/>");
            sb.Append("<div class='c-12'><textarea name='message' rows='2' required maxlength='1000' class='form-control form-control-sm' placeholder='Add a message'></textarea></div>");
            sb.Append("<div class='c-12'><button class='btn btn-sms btn-sm'>Send</button></div></form>");
            DetailBody = sb.ToString();
        }
    }
}

