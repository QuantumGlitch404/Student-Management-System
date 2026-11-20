using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Faculty
{
    /// <summary>Take attendance: create session, edit the roster (P/A/L/E), bulk save in one txn.</summary>
    public partial class Attendance : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Faculty" }; } }

        protected string FlashHtml, AssignmentOptions, RosterTable, SessionList, OpenInfo, Today;
        protected int OpenSessionId;

        protected void Page_Load(object sender, EventArgs e)
        {
            Today = Fmt.ToLocal(DateTime.UtcNow, Fmt.CurrentZoneId()).ToString("yyyy-MM-dd");
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            BuildAssignmentOptions();
            int.TryParse(Request.QueryString["session"], out OpenSessionId);
            if (OpenSessionId > 0) BuildOpenRoster();
            BuildSessionList();
        }

        private void HandlePost()
        {
            string error = null;
            try
            {
                switch (Request.Form["op"])
                {
                    case "newsession":
                        {
                            DataRow assignment = AssignmentById(int.Parse(Request.Form["assignmentId"]));
                            if (assignment == null) throw new Exception("Pick one of your assignments.");

                            int newSessionId = AttendanceRepo.CreateSession(CurrentUser.UserId,
                                Convert.ToInt32(assignment["CourseId"]), Convert.ToInt32(assignment["SubjectId"]),
                                Request.Form["sessionDate"], int.Parse(Request.Form["period"]), assignment["SubjectName"] as string);
                            if (newSessionId < 0)
                            {
                                object found = SqlHelper.ExecuteScalar(
                                    "SELECT AttnSessionId FROM dbo.AttendanceSessions " +
                                    "WHERE FacultyUserId = @F AND CourseId = @C AND SubjectId = @S " +
                                    "AND SessionDate = CAST(@D AS DATE) AND PeriodNo = @P",
                                    p =>
                                    {
                                        p.AddWithValue("@F", CurrentUser.UserId);
                                        p.AddWithValue("@C", Convert.ToInt32(assignment["CourseId"]));
                                        p.AddWithValue("@S", Convert.ToInt32(assignment["SubjectId"]));
                                        p.AddWithValue("@D", Request.Form["sessionDate"]);
                                        p.AddWithValue("@P", int.Parse(Request.Form["period"]));
                                    });
                                newSessionId = found == null || found == DBNull.Value ? 0 : Convert.ToInt32(found);
                                Flash("info", "That session already exists - opened it again.");
                            }
                            else Flash("success", "Session created. Mark attendance below.");
                            Redirect("/Faculty/Attendance.aspx?session=" + newSessionId);
                            return;
                        }
                    case "save":
                        OpenSessionId = int.Parse(Request.Form["attnSessionId"]);
                        DataRow session = AttendanceRepo.OneSession(OpenSessionId);
                        if (session == null || Convert.ToInt32(session["FacultyUserId"]) != CurrentUser.UserId)
                            throw new Exception("That session is not yours.");

                        var statuses = new System.Collections.Generic.Dictionary<int, string>();
                        foreach (string key in Request.Form.AllKeys)
                        {
                            if (key == null) continue;
                            if (!key.StartsWith("st_")) continue;
                            int enrollId;
                            if (!int.TryParse(key.Substring(3), out enrollId)) continue;
                            string value = Request.Form[key];
                            if (value == "P" || value == "A" || value == "L" || value == "E") statuses[enrollId] = value;
                        }
                        if (statuses.Count == 0) throw new Exception("Nothing selected.");
                        AttendanceRepo.SaveSession(OpenSessionId, statuses);
                        Flash("success", "Attendance saved for " + statuses.Count + " student(s).");
                        break;
                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }
            if (error != null) Flash("danger", error);
            Redirect(OpenSessionId > 0 ? "/Faculty/Attendance.aspx?session=" + OpenSessionId : "/Faculty/Attendance.aspx");
        }

        private DataRow AssignmentById(int assignmentId)
        {
            foreach (DataRow r in AcademicsRepo.FacultyAssignments(CurrentUser.UserId).Rows)
                if (Convert.ToInt32(r["AssignmentId"]) == assignmentId) return r;
            return null;
        }

        private void BuildAssignmentOptions()
        {
            DataTable dt = AcademicsRepo.FacultyAssignments(CurrentUser.UserId);
            var sb = new StringBuilder();
            foreach (DataRow r in dt.Rows)
            {
                sb.Append("<option value='" + E(r["AssignmentId"]) + "'>")
                  .Append(E(r["SubjectCode"]) + " " + E(r["SubjectName"]) + " - " + E(r["CourseCode"]) + " Sem " + E(r["SemesterNumber"]))
                  .Append("</option>");
            }
            if (dt.Rows.Count == 0) sb.Append("<option value=''>No assignment yet</option>");
            AssignmentOptions = sb.ToString();
        }

        private void BuildOpenRoster()
        {
            DataRow session = AttendanceRepo.OneSession(OpenSessionId);
            if (session == null) { RosterTable = "<p class='msg-empty'>Session not found.</p>"; return; }
            if (Convert.ToInt32(session["FacultyUserId"]) != CurrentUser.UserId)
            { RosterTable = "<p class='msg-empty'>You can only edit your own sessions.</p>"; OpenInfo = "denied"; return; }

            OpenInfo = ((DateTime)session["SessionDate"]).ToString("yyyy-MM-dd") + " P" + session["PeriodNo"] +
                " " + session["SubjectCode"] + " (" + session["CourseCode"] + ")";

            DataTable roster = AttendanceRepo.RosterForSession(OpenSessionId,
                Convert.ToInt32(session["CourseId"]),
                (Request.QueryString["sessionId"] != null ? int.Parse(Request.QueryString["sessionId"]) : ActiveSession()));
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th style='width:120px'>Enrollment</th><th>Student</th>");
            foreach (string c in new[] { "P", "A", "L", "E" })
                sb.Append("<th class='text-center' style='width:52px'>" + c + "</th>");
            sb.Append("</tr></thead><tbody>");
            int i = 0;
            foreach (DataRow r in roster.Rows)
            {
                i++;
                string current = (r["AttnStatus"] == DBNull.Value || r["AttnStatus"] == null ? "" : (r["AttnStatus"] as string ?? "").Trim());
                if (current.Length == 0) current = i % 2 == 1 ? "P" : "P"; // default Present; teacher can change
                sb.Append("<tr><td><b>" + E(r["EnrollNumber"]) + "</b></td><td>" + E(r["FullName"]) + "</td>");
                foreach (string c in new[] { "P", "A", "L", "E" })
                {
                    string id = "st_" + r["EnrollId"];
                    sb.Append("<td class='text-center'><input type='radio' name='" + id + "' value='" + c + "'")
                      .Append(string.Equals(current, c) ? " checked" : "").Append(" required/></td>");
                }
                sb.Append("</tr>");
            }
            if (roster.Rows.Count == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>No active enrollment for that course/semester yet.</td></tr>");
            sb.Append("</tbody></table>");
            RosterTable = sb.ToString();
        }

        private static int ActiveSession()
        {
            return AcademicsRepo.ActiveSessionId();
        }

        private void BuildSessionList()
        {
            DataTable dt = AttendanceRepo.Sessions(CurrentUser.UserId, 50);
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm'><thead><tr><th>Date</th><th>Period</th><th>Subject</th><th>Present</th><th></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='5' class='msg-empty'>No session yet.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                sb.Append("<tr><td>" + ((DateTime)r["SessionDate"]).ToString("yyyy-MM-dd") + "</td>");
                sb.Append("<td>P" + E(r["PeriodNo"]) + "</td><td>" + E(r["SubjectName"]) + "</td>");
                sb.Append("<td>" + E(r["PresentCount"]) + "/" + E(r["MarkedCount"]) + "</td>");
                sb.Append("<td><a class='imbtn' href='/Faculty/Attendance.aspx?session=" + E(r["AttnSessionId"]) + "'>Open</a></td></tr>");
            }
            sb.Append("</tbody></table>");
            SessionList = sb.ToString();
        }
    }
}

