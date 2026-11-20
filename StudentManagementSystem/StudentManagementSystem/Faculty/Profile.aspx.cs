using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Faculty
{
    public partial class Profile : SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Faculty" }; } }
        protected string Body, FlashHtml;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }
            FlashHtml = PopFlash();
            Build();
        }

        private void HandlePost()
        {
            string error = null;
            try
            {
                switch (Request.Form["op"])
                {
                    case "profile": SaveSelf(); Flash("success", "Your details were saved."); break;
                    case "password": ChangePassword(); break;
                    case "twofa": ToggleTwoFa(); break;
                    case "emailstart": StartEmailChange(); break;
                    case "emailverify": VerifyEmailChange(); break;
                    case "emailcancel": CancelEmailChange(); break;
                    case "notify": PeopleRepo.RaiseAdminRequest(CurrentUser.UserId, "profile", Request.Form["message"]); Flash("success", "An administrator has been notified."); break;
                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }
            Redirect("/Faculty/Profile.aspx" + (error != null ? "?err=" + HttpUtility.UrlEncode(error) : ""));
        }

        private void ChangePassword()
        {
            string current = Request.Form["current"] ?? "";
            string next = Request.Form["next"] ?? "";
            string confirm = Request.Form["confirm"] ?? "";
            DataRow user = UserRow();
            if (user == null) throw new Exception("Session stale. Sign in again.");
            if (!PasswordHasher.Verify(current, user["PasswordHash"] as string ?? "")) throw new Exception("Current password is wrong.");
            if (next.Length < AppConfig.GetInt("MinPasswordLength", 8)) throw new Exception("New password is too short (" + AppConfig.GetInt("MinPasswordLength", 8) + " characters minimum).");
            if (next != confirm) throw new Exception("The two new passwords do not match.");
            UsersRepo.UpdatePassword(CurrentUser.UserId, PasswordHasher.Hash(next));
            AuditService.Log("PasswordChanged", "Users", CurrentUser.UserId, "self", "Security", CurrentUser.UserId, null, null);
            Flash("success", "Password changed.");
        }

        private void ToggleTwoFa()
        {
            bool enable = Request.Form["value"] == "1";
            if (enable && !AppConfig.EmailEnabled) throw new Exception("Two-factor sign-in needs email configured on the server; ask an admin.");
            SqlHelper.ExecuteNonQuery("UPDATE dbo.Users SET TwoFactorEnabled = @V, UpdatedAtUtc = SYSUTCDATETIME() WHERE UserId = @Id", p => { p.AddWithValue("@V", enable ? 1 : 0); p.AddWithValue("@Id", CurrentUser.UserId); });
            AuditService.Log("TwoFactorToggled", "Users", CurrentUser.UserId, enable ? "on" : "off", "Security", CurrentUser.UserId, null, enable ? "On" : "Off");
            Flash("success", "Two-factor sign-in " + (enable ? "enabled." : "disabled."));
        }

        private void StartEmailChange() { string msg = PeopleRepo.StartEmailChange(CurrentUser.UserId, Request.Form["newEmail"]); if (msg != null) throw new Exception(msg); Flash("success", "A 6-digit code was sent to the new address. Enter it below to verify."); }
        private void VerifyEmailChange() { string msg = PeopleRepo.VerifyEmailChange(CurrentUser.UserId, Request.Form["code"]); if (msg != null) throw new Exception(msg); Flash("success", "Email verified. An administrator will review the change."); }
        private void CancelEmailChange() { PeopleRepo.CancelRequest(CurrentUser.UserId, int.Parse(Request.Form["requestId"])); Flash("info", "Email change cancelled."); }

        private void SaveSelf()
        {
            DataRow before = PeopleRepo.Person(CurrentUser.UserId);
            if (before == null) throw new Exception("Profile not found.");
            string[] fields = { "Phone", "AddressLine1", "AddressLine2", "City", "StateName", "PostalCode", "Country", "EmergencyContactName", "EmergencyContactPhone", "ProfileImage" };
            string[] form = { "phone", "addr1", "addr2", "city", "state", "postal", "country", "ecName", "ecPhone", "photo" };
            using (var conn = ConnectionManager.CreateOpenConnection())
            {
                var tx = conn.BeginTransaction();
                try
                {
                    var cmd = new System.Data.SqlClient.SqlCommand(
                        "UPDATE dbo.Users SET Phone=@Phone, AddressLine1=@A1, AddressLine2=@A2, City=@City, StateName=@State, PostalCode=@Postal, " +
                        "Country=@Country, EmergencyContactName=@Ecn, EmergencyContactPhone=@Ecp, ProfileImage=@Photo, UpdatedAtUtc=SYSUTCDATETIME() WHERE UserId=@Id", conn, tx);
                    for (int i = 0; i < fields.Length; i++)
                    {
                        string value = (Request.Form[form[i]] ?? "").Trim();
                        string oldValue = before[fields[i]] == DBNull.Value ? "" : Convert.ToString(before[fields[i]]);
                        if (!string.Equals(oldValue, value, StringComparison.OrdinalIgnoreCase))
                            AuditService.Log("ProfileFieldUpdated", "Users", CurrentUser.UserId, fields[i], "Profile", CurrentUser.UserId, oldValue, value);
                        cmd.Parameters.AddWithValue("@" + Param(fields[i]), value.Length == 0 ? (object)DBNull.Value : value);
                    }
                    cmd.Parameters.AddWithValue("@Id", CurrentUser.UserId);
                    cmd.ExecuteNonQuery();

                    string quals = (Request.Form["qualifications"] ?? "").Trim();
                    string specs = (Request.Form["specialisations"] ?? "").Trim();
                    string hours = (Request.Form["officeHours"] ?? "").Trim();
                    var cmd2 = new System.Data.SqlClient.SqlCommand(
                        "UPDATE dbo.Faculty SET Qualifications=@Q, Specialisations=@S, OfficeHours=@H WHERE UserId=@Id", conn, tx);
                    cmd2.Parameters.AddWithValue("@Q", quals.Length == 0 ? (object)DBNull.Value : quals);
                    cmd2.Parameters.AddWithValue("@S", specs.Length == 0 ? (object)DBNull.Value : specs);
                    cmd2.Parameters.AddWithValue("@H", hours.Length == 0 ? (object)DBNull.Value : hours);
                    cmd2.Parameters.AddWithValue("@Id", CurrentUser.UserId);
                    cmd2.ExecuteNonQuery();
                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
        }

        private static string Param(string column)
        {
            switch (column) { case "AddressLine1": return "A1"; case "AddressLine2": return "A2"; case "StateName": return "State"; case "PostalCode": return "Postal"; case "EmergencyContactName": return "Ecn"; case "EmergencyContactPhone": return "Ecp"; case "ProfileImage": return "Photo"; default: return column; }
        }

        private void Build()
        {
            DataRow p = PeopleRepo.Person(CurrentUser.UserId);
            if (p == null) { Body = "<p class='msg-empty'>Profile unavailable.</p>"; return; }
            bool twoFa = Convert.ToBoolean(p["TwoFactorEnabled"]);
            bool ready = PeopleRepo.FacultyProfileComplete(CurrentUser.UserId);

            var sb = new StringBuilder();
            sb.Append("<div class='tile profile-head-tile mb-3'><div class='tile__body'>")
              .Append("<div class='profile-avatar'>")
              .Append(p["ProfileImage"] != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(p["ProfileImage"]))
                  ? "<img src='" + Attr(p["ProfileImage"]) + "' alt=''/>" : E(FirstLetter(Convert.ToString(p["FullName"]))))
              .Append("</div><div><h1>").Append(E(p["FullName"])).Append("</h1>")
              .Append("<div class='profile-id'>").Append(E(p["EmployeeId"])).Append(" &middot; ").Append(E(p["DeptCode"])).Append(" &middot; ").Append(E(p["Designation"])).Append("</div>")
              .Append("<div class='profile-meta'><span class='badge ").Append(ready ? "badge-ok" : "badge-warn").Append("'>").Append(ready ? "Profile set up" : "Profile not set up").Append("</span>")
              .Append("<span class='badge badge-info'>").Append(E(p["Email"])).Append("</span></div></div>")
              .Append("<div class='profile-head-actions'><a class='btn btn-outline-sms btn-sm' href='/Handlers/Export.ashx?scope=profile&format=pdf'>Download profile PDF</a><a class='btn btn-outline-sms btn-sm' href='/Account/Settings.aspx?tab=account'>Change password</a></div>")
              .Append("</div></div>");

            sb.Append("<div class='profile-shell'><aside><div class='tile profile-nav'><div class='tile__body'>")
              .Append("<a href='#personal' class='active'>Personal information</a><a href='#contact'>Contact and address</a><a href='#professional'>Professional record</a><a href='#teaching'>Teaching</a><a href='#documents'>Documents</a><a href='#security'>Sign-in and security</a><a href='#requests'>Requests</a><a href='#activity'>My activity</a></div></div>")
              .Append("<div class='tile mt-3'><div class='tile__body'><div class='profile-completeness'><div class='d-flex justify-content-between small mb-1'><span>Profile completeness</span><b>").Append(Completeness(p)).Append("%</b></div><div class='bar'><span style='width:").Append(Completeness(p)).Append("%'></span></div></div>")
              .Append("<div class='small text-muted mt-2'>Department, designation and employee ID are set by the office.</div>")
              .Append("<form method='post' action='/Faculty/Profile.aspx' class='mt-2'>").Append(CsrfField())
              .Append("<input type='hidden' name='op' value='notify'/><input name='message' class='form-control form-control-sm mb-1' placeholder='What should the office fix?' maxlength='300'/>")
              .Append("<button class='btn btn-outline-sms btn-sm w-100'>Notify administrator</button></form></div></div>")
              .Append("</aside><div>");

            sb.Append("<form method='post' action='/Faculty/Profile.aspx' class='tile mb-3 profile-form'>").Append(CsrfField()).Append("<input type='hidden' name='op' value='profile'/>")
              .Append("<div class='tile__head'>Personal information</div><div class='tile__body'><div id='personal' class='form-grid'>");
            sb.Append(Row(p, "Full name", "FullName", true)).Append(Row(p, "Date of birth", "DateOfBirth", true))
              .Append(Row(p, "Gender", "Gender", true)).Append(Row(p, "Blood group", "BloodGroup", true))
              .Append(Row(p, "Nationality", "Nationality", true));
            sb.Append("</div><h3 id='contact' class='h6 mt-4 mb-2'>Contact and address</h3><div class='form-grid'>");
            sb.Append(Input(p, "Phone", "phone", "tel")).Append(Input(p, "Address line 1", "addr1", "text")).Append(Input(p, "Address line 2", "addr2", "text"))
              .Append(Input(p, "City", "city", "text")).Append(Input(p, "State", "state", "text")).Append(Input(p, "Postal code", "postal", "text"))
              .Append(Input(p, "Country", "country", "text")).Append(Input(p, "Emergency contact name", "ecName", "text")).Append(Input(p, "Emergency contact phone", "ecPhone", "tel"));
            sb.Append("</div><h3 id='professional' class='h6 mt-4 mb-2'>Professional record</h3><div class='form-grid'>");
            sb.Append(Read(p, "Department", p["DeptCode"])).Append(Read(p, "Designation", p["Designation"]))
              .Append(Read(p, "Employee ID", p["EmployeeId"])).Append(Read(p, "Joined on", p["JoinedOn"]))
              .Append(Read(p, "Experience (years)", p["ExperienceYears"]));
            sb.Append("<div class='c-6'><label class='form-label'>Qualifications</label><textarea class='form-control form-control-sm' name='qualifications' rows='2'>").Append(E(p["Qualifications"])).Append("</textarea></div>");
            sb.Append("<div class='c-6'><label class='form-label'>Specialisations</label><textarea class='form-control form-control-sm' name='specialisations' rows='2'>").Append(E(p["Specialisations"])).Append("</textarea></div>");
            sb.Append("<div class='c-12'><label class='form-label'>Office hours / consultation</label><input class='form-control form-control-sm' name='officeHours' maxlength='200' value='").Append(Attr(p["OfficeHours"])).Append("'/></div>");
            sb.Append("</div><div class='profile-savebar'><span class='hint'>Name, department and employee ID are locked.</span><button class='btn btn-sms btn-sm'>Save my details</button></div>");
            sb.Append("</div></form>");

            sb.Append("<div class='tile mb-3' id='teaching'><div class='tile__head'>Teaching</div><div class='tile__body'><p class='small mb-2'>Your assigned courses, subjects and weekly workload are managed by the timetable and academics sections.</p>")
              .Append("<a class='btn btn-outline-sms btn-sm' href='/Timetable/Index.aspx'>Open my timetable</a> <a class='btn btn-outline-sms btn-sm' href='/Faculty/Attendance.aspx'>Take attendance</a></div></div>");

            sb.Append("<div class='tile mb-3' id='documents'><div class='tile__head'>Documents</div><div class='tile__body'><div class='profile-empty'>Your profile has not been set up yet. Please contact the administrator</div><form method='post' action='/Faculty/Profile.aspx' class='mt-2'>").Append(CsrfField()).Append("<input type='hidden' name='op' value='notify'/><input name='message' class='form-control form-control-sm mb-1' placeholder='What should the office fix?' maxlength='300'/><button class='btn btn-outline-sms btn-sm w-100'>Notify administrator</button></form></div></div>");

            sb.Append(BuildEmailRequest(p));

            sb.Append("<div class='tile mb-3' id='security'><div class='tile__head d-flex justify-content-between'>Sign-in and security<span class='badge ").Append(twoFa ? "badge-ok" : "badge-off").Append("'>2FA ").Append(twoFa ? "ON" : "OFF").Append("</span></div><div class='tile__body'><div class='form-grid'>");
            sb.Append("<div class='c-6'><form method='post' action='/Faculty/Profile.aspx'>").Append(CsrfField()).Append("<input type='hidden' name='op' value='password'/>")
              .Append("<label class='form-label'>Current password</label><input name='current' type='password' required class='form-control form-control-sm mb-2'/>")
              .Append("<label class='form-label'>New password</label><input name='next' type='password' required class='form-control form-control-sm mb-2'/>")
              .Append("<label class='form-label'>Confirm new</label><input name='confirm' type='password' required class='form-control form-control-sm mb-2'/>")
              .Append("<button class='btn btn-sms btn-sm'>Update password</button></form></div>");
            sb.Append("<div class='c-6'><p class='small'>When two-factor is on, each sign-in also asks for a 6-digit code sent to your email.</p>")
              .Append("<form method='post' action='/Faculty/Profile.aspx'>").Append(CsrfField())
              .Append("<input type='hidden' name='op' value='twofa'/><input type='hidden' name='value' value='").Append(twoFa ? "0" : "1").Append("'/>")
              .Append("<button class='btn btn-outline-sms btn-sm'>").Append(twoFa ? "Turn off" : "Turn on").Append("</button></form></div>");
            sb.Append("</div></div></div>");

            sb.Append("<div class='tile' id='activity'><div class='tile__head'>My activity</div><div class='tile__body sms-table-wrap table-wrap'>")
              .Append(ActivityTable()).Append("</div></div>");
            sb.Append("</div></div>");
            Body = sb.ToString();
        }

        private string BuildEmailRequest(DataRow p)
        {
            var sb = new StringBuilder("<div class='tile mb-3' id='requests'><div class='tile__head'>Change email</div><div class='tile__body'>");
            DataRow pending = null;
            DataTable reqs = PeopleRepo.MyRequests(CurrentUser.UserId);
            foreach (DataRow r in reqs.Rows)
                if (Convert.ToString(r["Kind"]) == "email" && Convert.ToString(r["Status"]) == "pending") { pending = r; break; }
            if (pending == null)
            {
                sb.Append("<p class='small'>Current address: <b>").Append(E(p["Email"])).Append("</b>. Changing it requires a code sent to the new address and an administrator's approval.</p>")
                  .Append("<form method='post' action='/Faculty/Profile.aspx' class='srch-bar'>").Append(CsrfField())
                  .Append("<input type='hidden' name='op' value='emailstart'/><input type='email' name='newEmail' required class='form-control form-control-sm' placeholder='new@address.com'/>")
                  .Append("<button class='btn btn-outline-sms btn-sm'>Send verification code</button></form>");
            }
            else
            {
                string stage = Convert.ToString(pending["Stage"]);
                if (stage == "verify")
                    sb.Append("<p class='small'>Enter the 6-digit code sent to <b>").Append(E(pending["NewValue"])).Append("</b>.</p>")
                      .Append("<form method='post' action='/Faculty/Profile.aspx' class='srch-bar'>").Append(CsrfField())
                      .Append("<input type='hidden' name='op' value='emailverify'/><input name='code' required class='form-control form-control-sm' style='max-width:160px' inputmode='numeric'/>")
                      .Append("<button class='btn btn-sms btn-sm'>Verify email</button></form>");
                else
                    sb.Append("<p class='small'>Waiting for administrator approval to change ").Append(E(pending["OldValue"])).Append(" to <b>").Append(E(pending["NewValue"])).Append("</b>.</p>");
                sb.Append("<form method='post' action='/Faculty/Profile.aspx' class='mt-2'>").Append(CsrfField())
                  .Append("<input type='hidden' name='op' value='emailcancel'/><input type='hidden' name='requestId' value='").Append(pending["RequestId"]).Append("'/>")
                  .Append("<button class='btn btn-outline-bad btn-sm'>Cancel request</button></form>");
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private string ActivityTable()
        {
            DataTable dt = PeopleRepo.Activity(null, CurrentUser.UserId, null, null, null, 30);
            var sb = new StringBuilder("<table class='table table-sm'><thead><tr><th>When</th><th>Changed</th><th>Old</th><th>New</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='4' class='msg-empty'>No changes yet.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                sb.Append("<tr><td class='small'>").Append(Fmt.Stamp((DateTime)r["CreatedAtUtc"], Fmt.CurrentZoneId(), "dd MMM yyyy HH:mm")).Append("</td><td>")
                  .Append(E(r["DetailJson"])).Append("</td><td class='old small'>").Append(E(r["OldValue"])).Append("</td><td class='new small'>").Append(E(r["NewValue"])).Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private string Row(DataRow p, string label, string column, bool locked)
        {
            string value = E(p[column]);
            string lockedHtml = locked ? "<span class='locked-badge' title='Set by administrator'><i class='bi bi-lock'></i> Locked</span>" : "";
            return "<div class='c-6'><label class='form-label'>" + E(label) + "</label><div class='profile-readonly'>" + value + " " + lockedHtml + "</div></div>";
        }

        private string Input(DataRow p, string label, string name, string type)
        {
            return "<div class='c-4'><label class='form-label'>" + E(label) + "</label><input type='" + type + "' class='form-control form-control-sm' name='" + name + "' value='" + Attr(p[Map(name)]) + "'/></div>";
        }

        private string Read(DataRow p, string label, object value)
        {
            return "<div class='c-4'><div class='form-label'>" + E(label) + "</div><div class='profile-readonly'>" + E(value) + "</div></div>";
        }

        private static string Map(string name)
        {
            switch (name) { case "addr1": return "AddressLine1"; case "addr2": return "AddressLine2"; case "state": return "StateName"; case "postal": return "PostalCode"; case "ecName": return "EmergencyContactName"; case "ecPhone": return "EmergencyContactPhone"; default: return char.ToUpperInvariant(name[0]) + name.Substring(1); }
        }

        private static string FirstLetter(string value) { return string.IsNullOrEmpty(value) ? "?" : value.Substring(0, 1).ToUpperInvariant(); }

        private static DataRow UserRow()
        {
            DataTable dt = SqlHelper.FillDataTable("SELECT u.FullName, u.Email, u.Phone, u.TwoFactorEnabled, u.PasswordHash FROM dbo.Users u WHERE u.UserId = @Id", p => p.AddWithValue("@Id", CurrentUser.UserId));
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        private int Completeness(DataRow p)
        {
            string[] keys = { "FullName", "DateOfBirth", "Gender", "BloodGroup", "Nationality", "Phone", "AddressLine1", "City", "StateName", "PostalCode", "Country", "EmergencyContactName", "EmergencyContactPhone", "ProfileImage", "DeptCode", "Designation", "EmployeeId", "JoinedOn", "ExperienceYears", "Qualifications", "Specialisations", "OfficeHours" };
            int filled = 0;
            foreach (string k in keys) if (p.Table.Columns.Contains(k) && p[k] != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(p[k]))) filled++;
            return (int)Math.Round(filled * 100.0 / keys.Length);
        }
    }
}