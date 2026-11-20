using System;
using System.Data;
using System.Text;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Admin
{
    /// <summary>Admin-owned complete person profile with field-level audit.</summary>
    public partial class Person : SmsPage
    {
        protected override string[] AllowedRoles { get { return new[] { "Admin" }; } }

        protected string FlashHtml, PersonName, RoleName, AvatarHtml, IdentityLine, MemberSince, LastLogin;
        protected bool ProfileReady, IsActive, Verified, TwoFactor;
        protected string RoleFieldsHtml, DeleteFormHtml, Completeness, CompleteHint, ActivityHtml, RequestsHtml, DocumentsHtml;
        protected int UserId { get { int id; int.TryParse(Request.QueryString["id"], out id); return id; } }

        private DataRow _row;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (UserId <= 0) { Redirect("/Admin/Users.aspx"); return; }
            if (Request.HttpMethod == "POST") { HandlePost(); if (Response.IsRequestBeingRedirected) return; }

            _row = PeopleRepo.Person(UserId);
            if (_row == null) { Redirect("/Admin/Users.aspx?err=Person+not+found"); return; }

            FlashHtml = PopFlash();
            PersonName = Convert.ToString(_row["FullName"]);
            RoleName = Convert.ToString(_row["RoleName"]);
            IsActive = Convert.ToBoolean(_row["IsActive"]);
            Verified = Convert.ToBoolean(_row["EmailVerified"]);
            TwoFactor = Convert.ToBoolean(_row["TwoFactorEnabled"]);
            ProfileReady = RoleName == "Student" ? PeopleRepo.StudentProfileComplete(UserId)
                        : RoleName == "Faculty" ? PeopleRepo.FacultyProfileComplete(UserId) : true;
            MemberSince = _row["CreatedAtUtc"] == DBNull.Value ? "" : Fmt.UserDate((DateTime)_row["CreatedAtUtc"], CurrentUser.UserId);
            LastLogin = _row["LastLoginAtUtc"] == DBNull.Value ? "never" : Fmt.Stamp((DateTime)_row["LastLoginAtUtc"], Fmt.CurrentZoneId(), "dd MMM yyyy, HH:mm");
            string initial = string.IsNullOrEmpty(PersonName) ? "?" : HttpUtility.HtmlEncode(PersonName.Substring(0, 1).ToUpperInvariant());
            AvatarHtml = _row["ProfileImage"] != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(_row["ProfileImage"]))
                ? "<img src='" + Attr(_row["ProfileImage"]) + "' alt=''/>" : initial;
            IdentityLine = RoleName == "Student"
                ? "Enrollment " + Val("EnrollNumber") + " - " + Val("CourseCode")
                : RoleName == "Faculty" ? "Employee " + Val("EmployeeId") + " - " + Val("DeptCode")
                : "Administrator";
            BuildRoleFields();
            BuildCompleteness();
            BuildActivity();
            BuildRequests();
            BuildDocuments();
            BuildDeleteForm();
        }

        private void HandlePost()
        {
            string error = null;
            try
            {
                switch ((Request.Form["op"] ?? "").Trim())
                {
                    case "save": SavePerson(); break;
                    case "resetpwd": PeopleRepo.ResetPasswordAndMail(UserId); Flash("success", "A password reset link has been emailed to this person."); break;
                    case "unlock":
                        SqlHelper.ExecuteNonQuery("UPDATE dbo.Users SET LockoutEndUtc=NULL, FailedLoginCount=0 WHERE UserId=@Id", p => p.AddWithValue("@Id", UserId));
                        AuditService.Log("AccountUnlocked", "Users", UserId, null, "Account", UserId, null, null);
                        Flash("success", "Account unlocked."); break;
                    case "toggle":
                        bool active = !Convert.ToBoolean(Convert.ToInt32(SqlHelper.ExecuteScalar("SELECT IsActive FROM dbo.Users WHERE UserId=@Id", p => p.AddWithValue("@Id", UserId))));
                        SqlHelper.ExecuteNonQuery("UPDATE dbo.Users SET IsActive=@A WHERE UserId=@Id", p => { p.AddWithValue("@A", active ? 1 : 0); p.AddWithValue("@Id", UserId); });
                        AuditService.Log(active ? "UserActivated" : "UserDeactivated", "Users", UserId, null, "Account", UserId, null, active ? "Active" : "Disabled");
                        Flash("success", active ? "Account enabled." : "Account disabled."); break;
                    case "role": ChangeRole(); break;
                    case "forcesignout":
                        SqlHelper.ExecuteNonQuery("UPDATE dbo.Users SET AuthStamp=NEWID() WHERE UserId=@Id", p => p.AddWithValue("@Id", UserId));
                        AuditService.Log("ForceSignOut", "Users", UserId, null, "Security", UserId, null, "Signed out on next request");
                        Flash("success", "This person has been signed out of all devices (within 20 seconds)."); break;
                    case "uploaddoc": UploadDocument(); break;
                    case "verifydoc":
                        SqlHelper.ExecuteNonQuery("UPDATE dbo.UserDocuments SET VerifiedUtc=SYSUTCDATETIME(), VerifiedBy=@By WHERE DocId=@Id AND UserId=@U",
                            p => { p.AddWithValue("@By", CurrentUser.UserId); p.AddWithValue("@Id", Request.Form["docId"]); p.AddWithValue("@U", UserId); });
                        AuditService.Log("DocumentVerified", "UserDocuments", Convert.ToInt32(Request.Form["docId"]), null, "Documents", UserId, null, "Verified");
                        Flash("success", "Document marked verified."); break;
                    case "deletedoc":
                        SqlHelper.ExecuteNonQuery("UPDATE dbo.UserDocuments SET IsDeleted=1 WHERE DocId=@Id AND UserId=@U",
                            p => { p.AddWithValue("@Id", Request.Form["docId"]); p.AddWithValue("@U", UserId); });
                        AuditService.Log("DocumentDeleted", "UserDocuments", Convert.ToInt32(Request.Form["docId"]), null, "Documents", UserId, null, "Deleted");
                        Flash("info", "Document removed."); break;
                    case "decide":
                        int requestId; if (!int.TryParse(Request.Form["requestId"], out requestId)) throw new Exception("Missing request.");
                        PeopleRepo.DecideRequest(requestId, Request.Form["decision"] == "approve", Request.Form["reason"]);
                        Flash("success", "Request " + (Request.Form["decision"] == "approve" ? "approved" : "rejected") + "."); break;
                    case "delete":
                        SqlHelper.ExecuteNonQuery("UPDATE dbo.Users SET IsDeleted=1, IsActive=0, DeletedUtc=SYSUTCDATETIME() WHERE UserId=@Id", p => p.AddWithValue("@Id", UserId));
                        AuditService.Log("UserSoftDeleted", "Users", UserId, Request.Form["reason"], "Account", UserId, null, "Deleted");
                        Flash("info", "Account soft-deleted. It can be restored from this page."); break;
                    case "restore":
                        SqlHelper.ExecuteNonQuery("UPDATE dbo.Users SET IsDeleted=0, IsActive=1, DeletedUtc=NULL WHERE UserId=@Id", p => p.AddWithValue("@Id", UserId));
                        AuditService.Log("UserRestored", "Users", UserId, null, "Account", UserId, "Deleted", "Active");
                        Flash("success", "Account restored."); break;
                    default: throw new Exception("Unknown operation.");
                }
            }
            catch (Exception ex) { error = SqlHelper.FriendlyError(ex).Message; }
            Redirect("/Admin/Person.aspx?id=" + UserId + (error != null ? "&err=" + HttpUtility.UrlEncode(error) : ""));
        }

        private void SavePerson()
        {
            PersonInput input = new PersonInput
            {
                UserId = UserId,
                FullName = (Request.Form["fullName"] ?? "").Trim(),
                Email = (Request.Form["email"] ?? "").Trim().ToLowerInvariant(),
                Phone = Nz("phone"), DateOfBirth = Nz("dob"), Gender = Nz("gender"), BloodGroup = Nz("blood"),
                Nationality = Nz("nationality"), AddressLine1 = Nz("addr1"), AddressLine2 = Nz("addr2"),
                City = Nz("city"), StateName = Nz("state"), PostalCode = Nz("postal"), Country = Nz("country"),
                EmergencyContactName = Nz("ecName"), EmergencyContactPhone = Nz("ecPhone"), ProfileImage = Nz("photo"),
                IsActive = Request.Form["isActive"] == "1",
                StudentEnrollNumber = Nz("enroll"), SectionCode = Nz("section"), StudentStatus = Nz("status"),
                GuardianName = Nz("guardian"), Address = Nz("address"), Skills = Nz("skills"),
                Achievements = Nz("achievements"), MedicalNotes = Nz("medical"), AdmissionDate = Nz("admission"),
                FeePlanOverride = Nz("feeOverride"), Concessions = Nz("concessions"),
                CourseId = IntOrNull("courseId"), SessionId = IntOrNull("sessionId"),
                SemesterNumber = IntOrNull("semester"), MentorUserId = IntOrNull("mentorId"),
                EmployeeId = Nz("employeeId"), DeptCode = Nz("dept"), Designation = Nz("designation"),
                JoinedOn = Nz("joined"), Qualifications = Nz("qualifications"), Specialisations = Nz("specialisations"),
                OfficeHours = Nz("officeHours"), FacultyStatus = Nz("facultyStatus"),
                ExperienceYears = IntOrNull("experience")
            };
            if (string.IsNullOrWhiteSpace(input.FullName)) throw new Exception("Full name is required.");
            if (string.IsNullOrWhiteSpace(input.Email)) throw new Exception("Email is required.");
            PeopleRepo.Save(input, true);
            Flash("success", "Profile saved. Every changed field was written to the audit trail.");
        }

        private void ChangeRole()
        {
            string role = (Request.Form["role"] ?? "").Trim();
            if (role != "Student" && role != "Faculty" && role != "Admin") throw new Exception("Unknown role.");
            if (UserId == CurrentUser.UserId) throw new Exception("You cannot change your own role.");
            using (var conn = ConnectionManager.CreateOpenConnection())
            {
                var tx = conn.BeginTransaction();
                try
                {
                    using (var del = new System.Data.SqlClient.SqlCommand("DELETE FROM dbo.UserRoles WHERE UserId=@Id", conn, tx))
                    { del.Parameters.AddWithValue("@Id", UserId); del.ExecuteNonQuery(); }
                    using (var ins = new System.Data.SqlClient.SqlCommand(
                        "INSERT INTO dbo.UserRoles (UserId, RoleId) SELECT @Id, RoleId FROM dbo.Roles WHERE RoleName=@Role", conn, tx))
                    { ins.Parameters.AddWithValue("@Id", UserId); ins.Parameters.AddWithValue("@Role", role); ins.ExecuteNonQuery(); }
                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
            if (role == "Student") PeopleRepo.BlankStudentProfile(UserId);
            if (role == "Faculty") PeopleRepo.BlankFacultyProfile(UserId);
            AuditService.Log("RoleChanged", "Users", UserId, role, "Account", UserId, null, role);
            Flash("success", "Role changed to " + role + ".");
        }

        // ---------------- render ----------------
        public string Val(string column)
        {
            if (_row == null || !_row.Table.Columns.Contains(column) || _row[column] == DBNull.Value) return "";
            if (_row[column] is DateTime) return ((DateTime)_row[column]).ToString("yyyy-MM-dd");
            return Convert.ToString(_row[column]);
        }

        private void BuildRoleFields()
        {
            var sb = new StringBuilder();
            if (RoleName == "Student")
            {
                sb.Append("<div class='bento'>");
                sb.Append("<div class='c-4'><label class='form-label'>Course</label><select class='form-select form-select-sm' name='courseId'><option value=''>Not set</option>")
                  .Append(Options("CourseId", "Label", SqlHelper.FillDataTable("SELECT CourseId, CourseCode + N' - ' + CourseName AS Label FROM dbo.Courses WHERE IsActive=1 ORDER BY CourseCode"), Val("CourseId"))).Append("</select></div>");
                sb.Append("<div class='c-4'><label class='form-label'>Academic session</label><select class='form-select form-select-sm' name='sessionId'><option value=''>Not set</option>")
                  .Append(Options("SessionId", "SessionName", AcademicsRepo.Sessions(), Val("SessionId"))).Append("</select></div>");
                sb.Append("<div class='c-2'><label class='form-label'>Semester</label><select class='form-select form-select-sm' name='semester'><option value=''>-</option>")
                  .Append(NumberOptions(1, 12, Val("SemesterNumber"))).Append("</select></div>");
                sb.Append("<div class='c-2'><label class='form-label'>Section</label><input class='form-control form-control-sm' name='section' maxlength='20' value='").Append(Attr(Val("SectionCode"))).Append("'/></div>");
                sb.Append("<div class='c-4'><label class='form-label'>Enrollment number</label><input class='form-control form-control-sm' name='enroll' maxlength='24' value='").Append(Attr(Val("EnrollNumber"))).Append("'/></div>");
                sb.Append("<div class='c-4'><label class='form-label'>Status</label><select class='form-select form-select-sm' name='status'>")
                  .Append(Pick("Profile not set up,Active,Inactive,Alumni,Suspended", Val("StudentStatus"), "Profile not set up")).Append("</select></div>");
                sb.Append("<div class='c-4'><label class='form-label'>Mentor</label><select class='form-select form-select-sm' name='mentorId'><option value=''>Not set</option>")
                  .Append(Options("UserId", "FullName", SqlHelper.FillDataTable("SELECT u.UserId, u.FullName FROM dbo.Users u JOIN dbo.UserRoles ur ON ur.UserId=u.UserId JOIN dbo.Roles r ON r.RoleId=ur.RoleId WHERE r.RoleName='Faculty' ORDER BY u.FullName"), Val("MentorUserId"))).Append("</select></div>");
                sb.Append("<div class='c-4'><label class='form-label'>Admission date</label><input type='date' class='form-control form-control-sm' name='admission' value='").Append(Attr(Val("AdmissionDate"))).Append("'/></div>");
                sb.Append("<div class='c-4'><label class='form-label'>Guardian name</label><input class='form-control form-control-sm' name='guardian' maxlength='120' value='").Append(Attr(Val("GuardianName"))).Append("'/></div>");
                sb.Append("<div class='c-4'><label class='form-label'>Fee plan override</label><input class='form-control form-control-sm' name='feeOverride' maxlength='120' value='").Append(Attr(Val("FeePlanOverride"))).Append("'/></div>");
                sb.Append("<div class='c-4'><label class='form-label'>Concessions</label><input class='form-control form-control-sm' name='concessions' maxlength='300' value='").Append(Attr(Val("Concessions"))).Append("'/></div>");
                sb.Append("<div class='c-4'><label class='form-label'>Guardian address</label><input class='form-control form-control-sm' name='address' maxlength='300' value='").Append(Attr(Val("StudentAddress"))).Append("'/></div>");
                sb.Append("<div class='c-6'><label class='form-label'>Skills and achievements</label><textarea class='form-control form-control-sm' name='skills' rows='2'>").Append(E(Val("Skills"))).Append("</textarea></div>");
                sb.Append("<div class='c-6'><label class='form-label'>Achievements</label><textarea class='form-control form-control-sm' name='achievements' rows='2'>").Append(E(Val("Achievements"))).Append("</textarea></div>");
                sb.Append("<div class='c-12'><label class='form-label'>Medical notes (restricted)</label><textarea class='form-control form-control-sm' name='medical' rows='2'>").Append(E(Val("MedicalNotes"))).Append("</textarea></div>");
                sb.Append("</div>");
            }
            else if (RoleName == "Faculty")
            {
                sb.Append("<div class='bento'>");
                sb.Append("<div class='c-4'><label class='form-label'>Department</label><input class='form-control form-control-sm' name='dept' maxlength='60' value='").Append(Attr(Val("DeptCode"))).Append("'/></div>");
                sb.Append("<div class='c-4'><label class='form-label'>Designation</label><input class='form-control form-control-sm' name='designation' maxlength='60' value='").Append(Attr(Val("Designation"))).Append("'/></div>");
                sb.Append("<div class='c-4'><label class='form-label'>Employee ID</label><input class='form-control form-control-sm' name='employeeId' maxlength='24' value='").Append(Attr(Val("EmployeeId"))).Append("'/></div>");
                sb.Append("<div class='c-4'><label class='form-label'>Joining date</label><input type='date' class='form-control form-control-sm' name='joined' value='").Append(Attr(Val("JoinedOn"))).Append("'/></div>");
                sb.Append("<div class='c-4'><label class='form-label'>Experience (years)</label><input type='number' min='0' class='form-control form-control-sm' name='experience' value='").Append(Attr(Val("ExperienceYears"))).Append("'/></div>");
                sb.Append("<div class='c-4'><label class='form-label'>Status</label><select class='form-select form-select-sm' name='facultyStatus'>")
                  .Append(Pick("Profile not set up,Active,Inactive,Suspended", Val("FacultyStatus"), "Profile not set up")).Append("</select></div>");
                sb.Append("<div class='c-6'><label class='form-label'>Qualifications</label><textarea class='form-control form-control-sm' name='qualifications' rows='2'>").Append(E(Val("Qualifications"))).Append("</textarea></div>");
                sb.Append("<div class='c-6'><label class='form-label'>Specialisations</label><textarea class='form-control form-control-sm' name='specialisations' rows='2'>").Append(E(Val("Specialisations"))).Append("</textarea></div>");
                sb.Append("<div class='c-12'><label class='form-label'>Office hours</label><input class='form-control form-control-sm' name='officeHours' maxlength='200' value='").Append(Attr(Val("OfficeHours"))).Append("'/></div>");
                sb.Append("</div>");
                sb.Append("<div class='profile-empty mt-2'>Assigned courses and weekly workload are edited in <a href='/Admin/Academics.aspx'>Academics</a> and the <a href='/Timetable/Index.aspx'>Timetable</a>.</div>");
            }
            else
            {
                sb.Append("<div class='profile-empty'>Administrator scope follows the Admin role. Use Settings to review institution-wide permissions.</div>");
            }
            RoleFieldsHtml = sb.ToString();
        }

        private void BuildCompleteness()
        {
            string[] fields = RoleName == "Student"
                ? new[] { "FullName", "Email", "Phone", "DateOfBirth", "Gender", "AddressLine1", "City", "CourseCode", "EnrollNumber", "SectionCode" }
                : new[] { "FullName", "Email", "Phone", "DateOfBirth", "AddressLine1", "City", "DeptCode", "Designation", "EmployeeId", "JoinedOn" };
            int filled = 0;
            foreach (string f in fields) if (!string.IsNullOrWhiteSpace(Val(f))) filled++;
            int pct = (int)Math.Round(filled * 100.0 / fields.Length);
            Completeness = pct.ToString();
            CompleteHint = pct >= 100 ? "All key fields are filled."
                : "Add the highlighted details so this person sees real data on their information pages.";
        }

        private void BuildActivity()
        {
            DataTable dt = PeopleRepo.Activity(null, UserId, null, null, null, 40);
            var sb = new StringBuilder("<table class='table table-sm'><thead><tr><th>When</th><th>Category</th><th>Action</th><th>Change</th><th>By</th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='5' class='msg-empty'>No recorded activity yet.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                sb.Append("<tr><td class='small'>").Append(Fmt.Stamp((DateTime)r["CreatedAtUtc"], Fmt.CurrentZoneId(), "dd MMM yyyy HH:mm")).Append("</td>");
                sb.Append("<td><span class='badge badge-info'>").Append(E(r["Category"])).Append("</span></td>");
                sb.Append("<td class='small'>").Append(E(r["Action"])).Append("</td><td class='small'>");
                if (r["OldValue"] != DBNull.Value || r["NewValue"] != DBNull.Value)
                    sb.Append("<span class='old'>").Append(E(r["OldValue"])).Append("</span> &rarr; <span class='new'>").Append(E(r["NewValue"])).Append("</span>");
                else sb.Append(E(r["DetailJson"]));
                sb.Append("</td><td class='small'>").Append(E(r["ActorName"])).Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            ActivityHtml = sb.ToString();
        }

        private void BuildRequests()
        {
            DataTable dt = PeopleRepo.MyRequests(UserId);
            var sb = new StringBuilder("<table class='table table-sm'><thead><tr><th>Request</th><th>From</th><th>To</th><th>Status</th><th>When</th><th></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>No requests.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                string status = Convert.ToString(r["Status"]);
                sb.Append("<tr><td>").Append(E(r["Kind"])).Append("</td><td>").Append(E(r["OldValue"])).Append("</td><td>").Append(E(r["NewValue"]))
                  .Append("</td><td>").Append(E(status)).Append("</td><td class='small'>").Append(Fmt.Stamp((DateTime)r["RequestedUtc"], Fmt.CurrentZoneId(), "dd MMM yyyy HH:mm")).Append("</td><td>");
                if (status == "pending")
                {
                    sb.Append("<form method='post' action='/Admin/Person.aspx?id=").Append(UserId).Append("' class='d-inline'>")
                      .Append(CsrfField()).Append("<input type='hidden' name='op' value='decide'/><input type='hidden' name='requestId' value='").Append(r["RequestId"]).Append("'/>")
                      .Append("<input type='hidden' name='decision' value='approve'/><button class='imbtn'>Approve</button></form> ");
                    sb.Append("<form method='post' action='/Admin/Person.aspx?id=").Append(UserId).Append("' class='d-inline'>")
                      .Append(CsrfField()).Append("<input type='hidden' name='op' value='decide'/><input type='hidden' name='requestId' value='").Append(r["RequestId"]).Append("'/>")
                      .Append("<input type='hidden' name='decision' value='reject'/><input type='hidden' name='reason' value='admin'/><button class='imbtn' data-confirm='Reject this request?'>Reject</button></form>");
                }
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
            RequestsHtml = sb.ToString();
        }

        private void BuildDeleteForm()
        {
            bool deleted = Convert.ToBoolean(Convert.ToInt32(SqlHelper.ExecuteScalar("SELECT ISNULL(IsDeleted,0) FROM dbo.Users WHERE UserId=@Id", p => p.AddWithValue("@Id", UserId))));
            DeleteFormHtml = deleted
                ? "<form method='post' action='/Admin/Person.aspx?id=" + UserId + "'><input type='hidden' name='__csrf' value='" + Attr(Csrf()) + "'/><input type='hidden' name='op' value='restore'/><button class='btn btn-outline-sms btn-sm'>Restore deleted account</button></form>"
                : "<form method='post' action='/Admin/Person.aspx?id=" + UserId + "'><input type='hidden' name='__csrf' value='" + Attr(Csrf()) + "'/><input type='hidden' name='op' value='delete'/><input type='hidden' name='reason' value='admin'/><button class='btn btn-outline-bad btn-sm' data-confirm='Soft-delete this account? It can be restored.'>Delete account (soft)</button></form>";
        }

        private void BuildDocuments()
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT d.*, up.FullName AS UploaderName, vp.FullName AS VerifierName FROM dbo.UserDocuments d " +
                "LEFT JOIN dbo.Users up ON up.UserId=d.UploadedBy LEFT JOIN dbo.Users vp ON vp.UserId=d.VerifiedBy " +
                "WHERE d.UserId=@Id AND d.IsDeleted=0 ORDER BY d.UploadedUtc DESC",
                p => p.AddWithValue("@Id", UserId));
            var sb = new StringBuilder("<table class='table table-sm'><thead><tr><th>Kind</th><th>File</th><th>Note</th><th>Uploaded</th><th>Status</th><th></th></tr></thead><tbody>");
            if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='6' class='msg-empty'>No documents uploaded.</td></tr>");
            foreach (DataRow r in dt.Rows)
            {
                bool verified = r["VerifiedUtc"] != DBNull.Value;
                sb.Append("<tr><td>").Append(E(r["Kind"])).Append("</td><td><a href='/Handlers/Document.ashx?doc=").Append(r["DocId"]).Append("'>")
                  .Append(E(r["FileName"])).Append("</a><div class='small text-muted'>").Append(Math.Max(1, Convert.ToInt64(r["FileSize"] ?? 0) / 1024)).Append(" KB</div></td>");
                sb.Append("<td class='small'>").Append(E(r["Note"])).Append("</td><td class='small'>").Append(Fmt.Stamp((DateTime)r["UploadedUtc"], Fmt.CurrentZoneId(), "dd MMM yyyy HH:mm")).Append("</td>");
                sb.Append("<td>").Append(verified ? "<span class='badge badge-ok'>Verified</span>" : "<span class='badge badge-warn'>Unverified</span>").Append("</td><td>");
                if (!verified) sb.Append("<form method='post' action='/Admin/Person.aspx?id=").Append(UserId).Append("' class='d-inline'>").Append(CsrfField())
                    .Append("<input type='hidden' name='op' value='verifydoc'/><input type='hidden' name='docId' value='").Append(r["DocId"]).Append("'/><button class='imbtn'>Verify</button></form> ");
                sb.Append("<form method='post' action='/Admin/Person.aspx?id=").Append(UserId).Append("' class='d-inline'>").Append(CsrfField())
                  .Append("<input type='hidden' name='op' value='deletedoc'/><input type='hidden' name='docId' value='").Append(r["DocId"]).Append("'/><button class='imbtn' data-confirm='Remove this document?'>Remove</button></form></td></tr>");
            }
            sb.Append("</tbody></table>");
            DocumentsHtml = sb.ToString();
        }

        private void UploadDocument()
        {
            HttpPostedFile file = Request.Files["docFile"];
            if (file == null || file.ContentLength <= 0) throw new Exception("Choose a file to upload.");
            if (file.ContentLength > 10 * 1024 * 1024) throw new Exception("The file is larger than 10 MB.");
            string ext = System.IO.Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(ext) || ext.Length > 10) ext = ".bin";
            string folder = Server.MapPath("~/App_Data/documents/" + UserId);
            System.IO.Directory.CreateDirectory(folder);
            string stored = Guid.NewGuid().ToString("N") + ext.ToLowerInvariant();
            file.SaveAs(System.IO.Path.Combine(folder, stored));
            SqlHelper.ExecuteNonQuery(
                "INSERT INTO dbo.UserDocuments (UserId, Kind, FileName, ContentType, FileSize, StoredName, Note, UploadedBy) " +
                "VALUES (@U,@Kind,@Name,@Type,@Size,@Stored,@Note,@By)",
                p =>
                {
                    p.AddWithValue("@U", UserId);
                    p.AddWithValue("@Kind", (Request.Form["docKind"] ?? "Other").Trim());
                    p.AddWithValue("@Name", System.IO.Path.GetFileName(file.FileName));
                    p.AddWithValue("@Type", file.ContentType);
                    p.AddWithValue("@Size", file.ContentLength);
                    p.AddWithValue("@Stored", stored);
                    p.AddWithValue("@Note", (Request.Form["docNote"] ?? "").Trim());
                    p.AddWithValue("@By", CurrentUser.UserId);
                });
            AuditService.Log("DocumentUploaded", "UserDocuments", UserId, file.FileName, "Documents", UserId, null, file.FileName);
            Flash("success", "Document uploaded.");
        }

        private string Nz(string key) { string v = (Request.Form[key] ?? "").Trim(); return v.Length == 0 ? null : v; }
        private int? IntOrNull(string key) { int n; return int.TryParse(Request.Form[key], out n) && n > 0 ? n : (int?)null; }
        private string NumberOptions(int from, int to, string selected)
        {
            var sb = new StringBuilder();
            for (int i = from; i <= to; i++) sb.Append("<option value='").Append(i).Append("'").Append(i.ToString() == selected ? " selected" : "").Append(">").Append(i).Append("</option>");
            return sb.ToString();
        }
        private string Pick(string csv, string selected, string placeholder)
        {
            var sb = new StringBuilder();
            if (placeholder != null) sb.Append("<option value=''>").Append(placeholder).Append("</option>");
            foreach (string item in csv.Split(',')) sb.Append("<option").Append(item == selected ? " selected" : "").Append(">").Append(item).Append("</option>");
            return sb.ToString();
        }
    }
}

