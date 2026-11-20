using System;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Admin
{
    /// <summary>User management: students/faculty creation, verify, activate, reset, role change.</summary>
    public partial class Users : StudentManagementSystem.Lib.SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, UsersTable, PendingTable, RequestsTable, CourseOptions, SessionOptions, SemesterOptions, QTerm;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            QTerm = Request.QueryString["q"];
            FlashHtml = PopFlash();
            BuildLists();
            BuildTable();
        }

        private static string GenerateTempPassword()
        {
            return "SMS@" + PasswordHasher.GenerateUrlToken(4).Substring(0, 6) + "#1";
        }

        // ---------------- actions ----------------
        private void HandlePost()
        {
            string op = Request.Form["op"];
            string error = null;
            try
            {
                int userId;
                switch (op)
                {
                    case "verify":
                        userId = RequiredInt("userId");
                        UsersRepo.VerifyEmail(userId);
                        AuditService.Log("EmailVerifiedByAdmin", "Users", userId);
                        Flash("success", "User #" + userId + " email marked verified.");
                        break;

                    case "approve":
                        userId = RequiredInt("userId");
                        UsersRepo.SetApproval(userId, "approved");
                        AuditService.Log("RegistrationApproved", "Users", userId);
                        Flash("success", "User #" + userId + " approved and activated.");
                        break;

                    case "reject":
                        userId = RequiredInt("userId");
                        UsersRepo.SetApproval(userId, "rejected");
                        AuditService.Log("RegistrationRejected", "Users", userId);
                        Flash("info", "User #" + userId + " rejected.");
                        break;

                    case "toggle":
                        userId = RequiredInt("userId");
                        if (userId == CurrentUser.UserId)
                        { error = "You cannot deactivate your own account."; break; }
                        bool makeActive = Request.Form["value"] == "1";
                        SqlHelper.ExecuteNonQuery(
                            "UPDATE dbo.Users SET IsActive = @Active WHERE UserId = @Id",
                            p => { p.AddWithValue("@Active", makeActive ? 1 : 0); p.AddWithValue("@Id", userId); });
                        AuditService.Log(makeActive ? "UserActivated" : "UserDeactivated", "Users", userId);
                        Flash("info", "User #" + userId + (makeActive ? " activated." : " deactivated."));
                        break;

                    case "resetpwd":
                        userId = RequiredInt("userId");
                        string temp = GenerateTempPassword();
                        UsersRepo.UpdatePassword(userId, PasswordHasher.Hash(temp));
                        AuditService.Log("PasswordResetByAdmin", "Users", userId);
                        Flash("success", "Temporary password: " + temp + " - share privately; they should change it on first sign-in.");
                        break;

                    case "changerole":
                        userId = RequiredInt("userId");
                        string newRole = Request.Form["value"];
                        if (userId == CurrentUser.UserId)
                        { error = "You cannot change your own role."; break; }
                        ChangeRoleInDb(userId, newRole);
                        Flash("success", "User #" + userId + " role set to " + newRole + ".");
                        break;

                    case "bulk":
                        RunBulk();
                        break;

                    case "decidereq":
                        int reqId;
                        if (!int.TryParse(Request.Form["requestId"], out reqId)) throw new Exception("Missing request.");
                        PeopleRepo.DecideRequest(reqId, Request.Form["decision"] == "approve", null);
                        Flash("success", "Request " + (Request.Form["decision"] == "approve" ? "approved" : "rejected") + ".");
                        break;

                    case "addstudent":
                        AddStudent();
                        break;

                    case "addfaculty":
                        AddFaculty();
                        break;

                    default:
                        throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex)
            {
                error = SqlHelper.FriendlyError(ex).Message;
            }
            Redirect("/Admin/Users.aspx" + (error != null
                ? "?err=" + HttpUtility.UrlEncode(error) + RetainsQuery()
                : RetainsQuery()));
        }

        private void RunBulk()
        {
            string action = (Request.Form["bulkAction"] ?? "").Trim();
            string role = (Request.Form["role"] ?? "Student").Trim();
            if (role != "Student" && role != "Faculty" && role != "Admin") throw new Exception("Unknown role.");
            string[] raw = Request.Form.GetValues("ids");
            if (raw == null || raw.Length == 0) throw new Exception("Select at least one account first.");

            int count = 0, skipped = 0;
            foreach (string item in raw)
            {
                int userId;
                if (!int.TryParse(item, out userId)) continue;
                if (userId == CurrentUser.UserId) { skipped++; continue; }

                switch (action)
                {
                    case "role":
                        ChangeRoleInDb(userId, role);
                        break;
                    case "activate":
                    case "deactivate":
                        SqlHelper.ExecuteNonQuery("UPDATE dbo.Users SET IsActive=@A WHERE UserId=@Id",
                            p => { p.AddWithValue("@A", action == "activate" ? 1 : 0); p.AddWithValue("@Id", userId); });
                        AuditService.Log(action == "activate" ? "UserActivated" : "UserDeactivated", "Users", userId, "bulk");
                        break;
                    case "verify":
                        UsersRepo.VerifyEmail(userId);
                        AuditService.Log("EmailVerifiedByAdmin", "Users", userId, "bulk");
                        break;
                    case "approve":
                        UsersRepo.SetApproval(userId, "approved");
                        AuditService.Log("RegistrationApproved", "Users", userId, "bulk");
                        break;
                    case "reject":
                        UsersRepo.SetApproval(userId, "rejected");
                        AuditService.Log("RegistrationRejected", "Users", userId, "bulk");
                        break;
                    default:
                        throw new Exception("Unknown bulk action.");
                }
                count++;
            }
            Flash("success", count + " account(s) updated" + (skipped > 0 ? "; your own account was skipped." : "."));
        }

        private void AddStudent()
        {
            string fullName = (Request.Form["fullName"] ?? "").Trim();
            string email = (Request.Form["email"] ?? "").Trim().ToLowerInvariant();
            string phone = (Request.Form["phone"] ?? "").Trim();
            string enroll = (Request.Form["enrollNumber"] ?? "").Trim();
            int courseId, sessionId, semester;
            courseId = RequiredInt("courseId");
            sessionId = RequiredInt("sessionId");
            semester = RequiredInt("semester");

            if (fullName.Length == 0 || email.Length == 0 || enroll.Length == 0)
                throw new Exception("Name, email and enrollment number are required fields.");

            string temp = GenerateTempPassword();
            int userId = StudentsRepo.CreateStudent(
                fullName, email, phone.Length > 0 ? phone : null,
                PasswordHasher.Hash(temp),
                enroll, courseId, sessionId, semester,
                Request.Form["dob"] ?? "",
                Request.Form["guardian"] ?? null,
                Request.Form["address"] ?? null,
                "admin:" + CurrentUser.UserId);

            // also open the enrollment row for the current session
            object studentIdObj = SqlHelper.ExecuteScalar(
                "SELECT StudentId FROM dbo.Students WHERE UserId = @UserId",
                p => p.AddWithValue("@UserId", userId));
            if (studentIdObj != null && studentIdObj != DBNull.Value)
                AcademicsRepo.EnrollStudent(Convert.ToInt32(studentIdObj), courseId, sessionId);

            Flash("success", "Student created (" + email + ") with temporary password: " + temp);
        }

        private void AddFaculty()
        {
            string fullName = (Request.Form["fullName"] ?? "").Trim();
            string email = (Request.Form["email"] ?? "").Trim().ToLowerInvariant();
            string phone = (Request.Form["phone"] ?? "").Trim();
            string dept = (Request.Form["dept"] ?? "").Trim();
            string designation = (Request.Form["designation"] ?? "").Trim();

            if (fullName.Length == 0 || email.Length == 0 || dept.Length == 0)
                throw new Exception("Name, email and department are required fields.");

            string temp = GenerateTempPassword();
            int userId = StudentsRepo.CreateFaculty(
                fullName, email, phone.Length > 0 ? phone : null,
                PasswordHasher.Hash(temp), dept, designation.Length > 0 ? designation : null);

            Flash("success", "Faculty created (" + email + ") with temporary password: " + temp);
        }

        private void ChangeRoleInDb(int userId, string role)
        {
            using (var conn = ConnectionManager.CreateOpenConnection())
            {
                SqlTransaction tx = conn.BeginTransaction();
                try
                {
                    using (var del = new SqlCommand("DELETE FROM dbo.UserRoles WHERE UserId = @UserId", conn, tx))
                    {
                        del.Parameters.AddWithValue("@UserId", userId);
                        del.ExecuteNonQuery();
                    }
                    using (var ins = new SqlCommand(
                        "INSERT INTO dbo.UserRoles (UserId, RoleId) SELECT @UserId, RoleId FROM dbo.Roles WHERE RoleName = @RoleName", conn, tx))
                    {
                        ins.Parameters.AddWithValue("@UserId", userId);
                        ins.Parameters.AddWithValue("@RoleName", role);
                        ins.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
            AuditService.Log("RoleChanged", "Users", userId, role);
        }

        // ---------------- lists ----------------
        private void BuildLists()
        {
            DataTable courses = AcademicsRepo.Courses();
            DataTable sessions = AcademicsRepo.Sessions();
            CourseOptions = Options("CourseId", "CourseName", courses, "");
            SessionOptions = Options("SessionId", "SessionName", sessions, AcademicsRepo.ActiveSessionId().ToString());

            var sb = new StringBuilder();
            for (int i = 1; i <= 8; i++) sb.Append("<option value='" + i + "'>Semester " + i + "</option>");
            SemesterOptions = sb.ToString();
        }

        private void BuildTable()
        {
            bool all = Request.QueryString["all"] == "1";
            string role = Request.QueryString["role"];
            if (role == "Faculty" || role == "Admin") BuildStaffTable(role);
            else BuildStudentTable(all);
            BuildPendingTable();
            BuildRequestsTable();
        }

        /// <summary>
        /// Self-registered accounts have no profile/enrollment row yet, so they are
        /// not part of the student list. Surface them here so an administrator can
        /// verify the email and let them sign in.
        /// </summary>
        private void BuildRequestsTable()
        {
            DataTable dt = PeopleRepo.PendingRequests();
            if (dt.Rows.Count == 0) { RequestsTable = ""; return; }
            var sb = new StringBuilder();
            sb.Append("<div class='tile mb-3'><div class='tile__head d-flex justify-content-between'><span>Profile change requests</span><span class='small text-muted'>").Append(dt.Rows.Count).Append(" waiting</span></div>");
            sb.Append("<div class='tile__body sms-table-wrap table-wrap'><table class='table table-sm'><thead><tr><th>Person</th><th>Request</th><th>Change</th><th>When</th><th style='width:200px'>Decision</th></tr></thead><tbody>");
            foreach (DataRow r in dt.Rows)
            {
                sb.Append("<tr><td><a href='/Admin/Person.aspx?id=").Append(E(r["UserId"])).Append("'>").Append(E(r["FullName"])).Append("</a></td>");
                sb.Append("<td>").Append(E(r["Kind"])).Append("</td><td class='small'>").Append(E(r["OldValue"])).Append(" &rarr; ").Append(E(r["NewValue"])).Append("</td>");
                sb.Append("<td class='small'>").Append(((DateTime)r["RequestedUtc"]).ToString("dd MMM yyyy HH:mm")).Append("</td><td>");
                sb.Append("<form method='post' action='/Admin/Users.aspx' class='d-inline'>").Append(CsrfField())
                  .Append("<input type='hidden' name='op' value='decidereq'/><input type='hidden' name='requestId' value='").Append(r["RequestId"]).Append("'/>")
                  .Append("<input type='hidden' name='decision' value='approve'/><button class='imbtn'>Approve</button></form> ");
                sb.Append("<form method='post' action='/Admin/Users.aspx' class='d-inline'>").Append(CsrfField())
                  .Append("<input type='hidden' name='op' value='decidereq'/><input type='hidden' name='requestId' value='").Append(r["RequestId"]).Append("'/>")
                  .Append("<input type='hidden' name='decision' value='reject'/><button class='imbtn' data-confirm='Reject this request?'>Reject</button></form>");
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table></div></div>");
            RequestsTable = sb.ToString();
        }

        private void BuildPendingTable()
        {
            DataTable dt;
            try
            {
                dt = SqlHelper.FillDataTable(
                    "SELECT TOP 50 u.UserId, u.FullName, u.Email, u.Phone, u.IsActive, u.EmailVerified, " +
                    "ISNULL(u.ApprovalStatus,'approved') AS ApprovalStatus, r.RoleName " +
                    "FROM dbo.Users u " +
                    "JOIN dbo.UserRoles ur ON ur.UserId = u.UserId " +
                    "JOIN dbo.Roles r ON r.RoleId = ur.RoleId " +
                    "WHERE (u.EmailVerified = 0 OR ISNULL(u.ApprovalStatus,'approved') = 'pending') " +
                    "AND (@Q IS NULL OR u.Email LIKE @Q OR u.FullName LIKE @Q) " +
                    "ORDER BY u.UserId DESC",
                    p => p.AddWithValue("@Q", string.IsNullOrWhiteSpace(QTerm)
                        ? (object)DBNull.Value
                        : "%" + QTerm.Trim() + "%"));
            }
            catch
            {
                PendingTable = "";
                return;
            }

            if (dt.Rows.Count == 0) { PendingTable = ""; return; }

            var sb = new StringBuilder();
            sb.Append("<div class='tile mb-3'><div class='tile__head d-flex justify-content-between'>");
            sb.Append("<span>Registrations waiting for review</span>");
            sb.Append("<span class='small text-muted'>Approve only after checking the student record</span></div>");
            sb.Append("<div class='tile__body sms-table-wrap table-wrap'><table class='table table-sm users-table users-pending'><thead><tr>");
            sb.Append("<th>Name</th><th>Email</th><th>Phone</th><th>Role</th><th>Status</th><th style='width:290px'>Actions</th></tr></thead><tbody>");
            foreach (DataRow r in dt.Rows)
            {
                bool isActive = Convert.ToBoolean(r["IsActive"]);
                bool verified = Convert.ToBoolean(r["EmailVerified"]);
                string approval = Convert.ToString(r["ApprovalStatus"]);
                sb.Append("<tr><td>" + E(r["FullName"]) + "</td><td>" + E(r["Email"]) + "</td>");
                sb.Append("<td>" + E(r["Phone"]) + "</td><td>" + E(r["RoleName"]) + "</td>");
                sb.Append("<td>" + (approval == "pending" ? "<span class='badge badge-warn'>Approval pending</span>" : verified ? "<span class='badge badge-ok'>Email verified</span>" : "<span class='badge badge-off'>Email unverified</span>") + "</td><td>");
                sb.Append(ActButton("approve", r["UserId"], "", "Approve"));
                sb.Append(ActButton("reject", r["UserId"], "", "Reject", true));
                if (!verified) sb.Append(ActButton("verify", r["UserId"], "", "Verify email"));
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table></div></div>");
            PendingTable = sb.ToString();
        }

        private void BuildStudentTable(bool all)
        {
            DataTable dt = StudentsRepo.Search(QTerm, null, all ? 1 : 0, Request.QueryString["profile"] == "incomplete");

            var sb = new StringBuilder();
            sb.Append("<div class='tile'><div class='tile__head d-flex justify-content-between'><span>Student accounts</span>");
            sb.Append("<span class='small text-muted'>" + dt.Rows.Count + " shown (max 200)</span></div>");
            sb.Append("<div class='tile__body sms-table-wrap table-wrap'>");
            sb.Append("<table class='table table-sm users-table users-student'><thead><tr>" +
                "<th><input type='checkbox' data-check-all='bulkUsers' title='Select all'/></th><th>Enrollment</th><th>Name</th><th>Email</th><th>Course</th><th>Status</th><th>Last login</th><th>Created</th><th>Actions</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0)
                sb.Append("<tr><td colspan='9' class='msg-empty'>No student matches this filter.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                bool isActive = Convert.ToBoolean(r["IsActive"]);
                bool verified = Convert.ToBoolean(r["EmailVerified"]);
                sb.Append("<tr><td><input type='checkbox' name='ids' value='" + E(r["UserId"]) + "' form='bulkUsers'/></td>");
                sb.Append("<td><b>" + E(r["EnrollNumber"]) + "</b></td>");
                sb.Append("<td>" + E(r["FullName"]) + "</td><td>" + E(r["Email"]) + "</td>");
                sb.Append("<td>" + E(r["CourseCode"]) + "<div class='small text-muted'>Sem " + E(r["SemesterNumber"]) + ", " + E(r["SessionName"]) + "</div></td>");
                sb.Append("<td>" + StatusBadge(isActive, verified) + "</td>");
                sb.Append("<td class='mono small'>" + (r["LastLoginAtUtc"] == DBNull.Value ? "-" : ((DateTime)r["LastLoginAtUtc"]).ToString("yyyy-MM-dd HH:mm")) + "</td>");
                sb.Append("<td class='mono small'>" + ((DateTime)r["CreatedAtUtc"]).ToString("yyyy-MM-dd") + "</td><td>");
                sb.Append("<a class='imbtn' href='/Admin/Person.aspx?id=" + E(r["UserId"]) + "'>Open</a> ");
                sb.Append(ActButton("toggle", r["UserId"], isActive ? "0" : "1", isActive ? "Deactivate" : "Activate", true));
                sb.Append(ActButton("verify", r["UserId"], "", verified ? null : "Verify email"));
                sb.Append(ActButton("resetpwd", r["UserId"], "", "Reset password", true));
                sb.Append(ActButton("changerole", r["UserId"], "Faculty", "Make faculty", true));
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table></div></div>");
            UsersTable = sb.ToString();
        }

        private void BuildStaffTable(string role)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP 300 u.UserId, u.FullName, u.Email, u.Phone, u.IsActive, u.EmailVerified, u.LastLoginAtUtc, u.CreatedAtUtc, f.DeptCode, f.Designation " +
                "FROM dbo.Users u " +
                "JOIN dbo.UserRoles ur ON ur.UserId = u.UserId " +
                "JOIN dbo.Roles r ON r.RoleId = ur.RoleId " +
                "LEFT JOIN dbo.Faculty f ON f.UserId = u.UserId " +
                "WHERE r.RoleName = @Role ORDER BY u.FullName",
                p => p.AddWithValue("@Role", role));

            var sb = new StringBuilder();
            sb.Append("<div class='tile'><div class='tile__head'>" + E(role) + " accounts</div>");
            sb.Append("<div class='tile__body sms-table-wrap table-wrap'>");
            sb.Append("<table class='table table-sm users-table users-staff'><thead><tr>" +
                "<th><input type='checkbox' data-check-all='bulkUsers' title='Select all'/></th><th>Name</th><th>Email</th><th>Phone</th><th>Dept</th><th>Status</th><th>Last login</th><th>Actions</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0)
                sb.Append("<tr><td colspan='8' class='msg-empty'>No " + E(role.ToLowerInvariant()) + " account found.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                bool isActive = Convert.ToBoolean(r["IsActive"]);
                bool verified = Convert.ToBoolean(r["EmailVerified"]);
                sb.Append("<tr><td><input type='checkbox' name='ids' value='" + E(r["UserId"]) + "' form='bulkUsers'/></td>");
                sb.Append("<td>" + E(r["FullName"]) + "</td><td>" + E(r["Email"]) + "</td><td>" + E(r["Phone"]) + "</td>");
                sb.Append("<td>" + E(r["DeptCode"]) + (r["Designation"] == DBNull.Value || r["Designation"] == null ? "" : " - " + E(r["Designation"])) + "</td>");
                sb.Append("<td>" + StatusBadge(isActive, verified) + "</td>");
                sb.Append("<td class='mono small'>" + (r["LastLoginAtUtc"] == DBNull.Value ? "-" : ((DateTime)r["LastLoginAtUtc"]).ToString("yyyy-MM-dd HH:mm")) + "</td>");
                sb.Append("<td class='mono small'>" + ((DateTime)r["CreatedAtUtc"]).ToString("yyyy-MM-dd") + "</td><td>");
                sb.Append("<a class='imbtn' href='/Admin/Person.aspx?id=" + E(r["UserId"]) + "'>Open</a> ");
                sb.Append(ActButton("toggle", r["UserId"], isActive ? "0" : "1", isActive ? "Deactivate" : "Activate", true));
                sb.Append(ActButton("verify", r["UserId"], "", verified ? null : "Verify email"));
                sb.Append(ActButton("resetpwd", r["UserId"], "", "Reset password", true));
                sb.Append(role == "Faculty" ? ActButton("changerole", r["UserId"], "Student", "Make student", true) : "");
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table></div></div>");
            UsersTable = sb.ToString();
        }

        // ---------------- render helpers ----------------
        private string ActButton(string op, object userId, string value, string label, bool confirm = false)
        {
            if (label == null) return "";
            var sb = new StringBuilder();
            sb.Append("<form method='post' action='/Admin/Users.aspx' class='action-form'")
              .Append(confirm ? " data-confirm='OK to perform this action?'" : "").Append(">");
            sb.Append(CsrfField());
            sb.Append("<input type='hidden' name='op' value='").Append(Attr(op)).Append("'/>");
            sb.Append("<input type='hidden' name='userId' value='").Append(Attr(userId)).Append("'/>");
            sb.Append("<input type='hidden' name='value' value='").Append(Attr(value)).Append("'/>");
            sb.Append("<button class='imbtn'>").Append(label).Append("</button></form> ");
            return sb.ToString();
        }

        private string RetainsQuery()
        {
            var pairs = new StringBuilder();
            foreach (string key in Request.QueryString.AllKeys)
            {
                if (string.IsNullOrEmpty(key) || key == "err") continue;
                pairs.Append('&').Append(key).Append('=').Append(HttpUtility.UrlEncode(Request.QueryString[key]));
            }
            return pairs.ToString();
        }

        private int RequiredInt(string field)
        {
            int n;
            if (!int.TryParse(Request.Form[field], out n))
                throw new Exception("Missing or invalid value for " + field + ".");
            return n;
        }

        protected static string StatusBadge(bool isActive, bool verified)
        {
            return "<span class='badge " + (isActive ? "badge-ok" : "badge-off") + "'>" + (isActive ? "Active" : "Inactive") + "</span> " +
                   "<span class='badge " + (verified ? "badge-ok" : "badge-warn") + "'>" + (verified ? "Verified" : "Unverified") + "</span>";
        }

        protected string RoleSelected(string role)
        {
            return Request.QueryString["role"] == role ? " selected" : "";
        }

        protected string ProfileSelected(string value)
        {
            return Request.QueryString["profile"] == value ? " selected" : "";
        }

        protected string IncludeInactiveChecked()
        {
            return Request.QueryString["all"] == "1" ? " checked" : "";
        }
    }
}


