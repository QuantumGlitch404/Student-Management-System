using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Admin
{
    /// <summary>Academic sessions + their semesters.</summary>
    public partial class Sessions : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, SessionTable, SessionOptions, SemesterTable, SessionHistory;
        protected int SemesterSessionId;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            Build();
            BuildHistory();
        }

        private void HandlePost()
        {
            string error = null;
            try
            {
                switch (Request.Form["op"])
                {
                    case "newsession":
                        AcademicsRepo.CreateSession((Request.Form["name"] ?? "").Trim(),
                            Request.Form["start"], Request.Form["end"]);
                        Flash("success", "Session created.");
                        break;
                    case "activate":
                        AcademicsRepo.SetActiveSession(int.Parse(Request.Form["sessionId"]));
                        Flash("success", "Session set active. All new enrollments reference it.");
                        break;
                    case "upsertsemester":
                        int sid = int.Parse(Request.Form["sessionId"]);
                        AcademicsRepo.UpsertSemester(sid, int.Parse(Request.Form["num"]),
                            (Request.Form["name"] ?? "").Trim(),
                            Request.Form["start"] ?? "", Request.Form["end"] ?? "");
                        Flash("success", "Semester saved.");
                        break;
                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }

            string back = "/Admin/Sessions.aspx";
            if (!string.IsNullOrEmpty(Request.Form["sessionId"]))
                back += "?session=" + Request.Form["sessionId"];
            Redirect(back + (error != null ? (back.Contains("?") ? "&" : "?") + "err=" + HttpUtility.UrlEncode(error) : ""));
        }

        private void BuildHistory()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT h.HistoryId, h.Action, h.Reason, h.ChangedUtc, s.SessionName, u.FullName FROM dbo.AcademicSessionHistory h " +
                "LEFT JOIN dbo.AcademicSessions s ON s.SessionId=h.SessionId LEFT JOIN dbo.Users u ON u.UserId=h.ChangedBy ORDER BY h.HistoryId DESC");
            var sb = new StringBuilder("<table class='table table-sm'><thead><tr><th>When</th><th>Session</th><th>Action</th><th>Reason</th><th>By</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='5' class='msg-empty'>No history yet.</td></tr>");
            foreach (DataRow r in dt.Rows)
                sb.Append("<tr><td class='small'>").Append(((DateTime)r["ChangedUtc"]).ToString("dd MMM yyyy HH:mm")).Append("</td><td>").Append(E(r["SessionName"])).Append("</td><td>").Append(E(r["Action"])).Append("</td><td class='small'>").Append(E(r["Reason"])).Append("</td><td>").Append(E(r["FullName"])).Append("</td></tr>");
            sb.Append("</tbody></table>");
            SessionHistory = sb.ToString();
        }

        private void Build()
        {
            DataTable sessions = AcademicsRepo.Sessions();
            SemesterSessionId = ParseQ("session") ?? AcademicsRepo.ActiveSessionId();

            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Name</th><th>Dates</th><th>Status</th><th></th></tr></thead><tbody>");
            bool any = false;
            foreach (DataRow r in sessions.Rows)
            {
                any = true;
                bool isActive = Convert.ToBoolean(r["IsActive"]);
                sb.Append("<tr>");
                sb.Append("<td><b>" + E(r["SessionName"]) + "</b></td>");
                sb.Append("<td class='small'>" + FormatDate(r["StartDate"]) + " to " + FormatDate(r["EndDate"]) + "</td>");
                sb.Append("<td><span class='badge " + (isActive ? "badge-ok" : "badge-off") + "'>" + (isActive ? "Active" : "Inactive") + "</span></td>");
                sb.Append("<td>" + (isActive ? "" :
                    ImbForm("activate", new[] { new[] { "sessionId", E(r["SessionId"]) } }, "Set active")) + "</td></tr>");
            }
            if (!any) sb.Append("<tr><td colspan='4' class='msg-empty'>No academic session yet.</td></tr>");
            sb.Append("</tbody></table>");
            SessionTable = sb.ToString();

            SessionOptions = Options("SessionId", "SessionName", sessions, SemesterSessionId.ToString());

            DataTable sems = AcademicsRepo.Semesters(SemesterSessionId);
            var sbb = new StringBuilder();
            sbb.Append("<table class='table table-sm'><thead><tr><th>#</th><th>Name</th><th>Dates</th></tr></thead><tbody>");
            bool anyS = false;
            foreach (DataRow s in sems.Rows)
            {
                anyS = true;
                sbb.Append("<tr><td><b>Sem " + E(s["SemesterNumber"]) + "</b></td><td>" + E(s["SemesterName"]) + "</td>");
                sbb.Append("<td class='small'>" + FormatDate(s["StartDate"]) + " to " + FormatDate(s["EndDate"]) + "</td></tr>");
            }
            if (!anyS) sbb.Append("<tr><td colspan='3' class='msg-empty'>No semester rows for this session.</td></tr>");
            sbb.Append("</tbody></table>");
            SemesterTable = sbb.ToString();
        }

        private int? ParseQ(string key)
        {
            int n;
            return int.TryParse(Request.QueryString[key], out n) ? (int?)n : null;
        }

        private static string FormatDate(object d)
        {
            if (d == null || d == DBNull.Value) return "-";
            DateTime dt;
            return DateTime.TryParse(d.ToString(), out dt) ? dt.ToString("yyyy-MM-dd") : d.ToString();
        }

        private string ImbForm(string op, string[][] fields, string label)
        {
            var sb = new StringBuilder();
            sb.Append("<form method='post' action='/Admin/Sessions.aspx' style='display:inline'>");
            sb.Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='").Append(Attr(op)).Append("'/>");
            foreach (string[] f in fields)
                sb.Append("<input type='hidden' name='").Append(f[0]).Append("' value='").Append(f[1]).Append("'/>");
            sb.Append("<button class='imbtn'>").Append(label).Append("</button></form>");
            return sb.ToString();
        }
    }
}


