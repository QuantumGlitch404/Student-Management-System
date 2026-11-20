using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using StudentManagementSystem.Data;
using StudentManagementSystem.Integrations;
using StudentManagementSystem.Lib;
using StudentManagementSystem.Security;

namespace StudentManagementSystem.Data
{
    /// <summary>One editable person: user account plus student/faculty profile.</summary>
    public class PersonInput
    {
        public int UserId;
        public string Role;
        public string FullName, Email, Phone, DateOfBirth, Gender, BloodGroup, Nationality;
        public string AddressLine1, AddressLine2, City, StateName, PostalCode, Country;
        public string EmergencyContactName, EmergencyContactPhone, ProfileImage;
        public bool IsActive;
        public string StudentEnrollNumber, SectionCode, StudentStatus, FeePlanOverride, Concessions;
        public string GuardianName, Address, Skills, Achievements, MedicalNotes;
        public int? CourseId, SessionId, SemesterNumber, MentorUserId;
        public string AdmissionDate;
        public string EmployeeId, DeptCode, Designation, JoinedOn, Qualifications, Specialisations, OfficeHours, FacultyStatus;
        public int? ExperienceYears;
    }

    /// <summary>People data, profile completion, requests and per-person activity.</summary>
    public static class PeopleRepo
    {
        public static string RoleOf(int userId)
        {
            object v = SqlHelper.ExecuteScalar(
                "SELECT TOP 1 r.RoleName FROM dbo.Users u JOIN dbo.UserRoles ur ON ur.UserId=u.UserId " +
                "JOIN dbo.Roles r ON r.RoleId=ur.RoleId WHERE u.UserId=@Id ORDER BY CASE r.RoleName WHEN 'Admin' THEN 1 WHEN 'Faculty' THEN 2 ELSE 3 END",
                p => p.AddWithValue("@Id", userId));
            return v == null || v == DBNull.Value ? "Student" : Convert.ToString(v);
        }

        public static DataRow Person(int userId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT u.*, ISNULL(u.ApprovalStatus,'approved') AS ApprovalStatus, r.RoleName, " +
                "s.StudentId, s.EnrollNumber, s.CourseId, s.SessionId, s.SemesterNumber, s.SectionCode, s.Status AS StudentStatus, " +
                "s.MentorUserId, s.FeePlanOverride, s.Concessions, s.Skills, s.Achievements, s.MedicalNotes, " +
                "s.AdmissionDate, s.GuardianName, s.Address AS StudentAddress, s.DateOfBirth AS StudentDob, " +
                "f.FacultyId, f.EmployeeId, f.DeptCode, f.Designation, f.JoinedOn, f.Qualifications, f.ExperienceYears, " +
                "f.Specialisations, f.OfficeHours, f.Status AS FacultyStatus, " +
                "c.CourseCode, c.CourseName, cs.SessionName " +
                "FROM dbo.Users u " +
                "LEFT JOIN dbo.UserRoles ur ON ur.UserId=u.UserId " +
                "LEFT JOIN dbo.Roles r ON r.RoleId=ur.RoleId " +
                "LEFT JOIN dbo.Students s ON s.UserId=u.UserId " +
                "LEFT JOIN dbo.Faculty f ON f.UserId=u.UserId " +
                "LEFT JOIN dbo.Courses c ON c.CourseId=s.CourseId " +
                "LEFT JOIN dbo.AcademicSessions cs ON cs.SessionId=s.SessionId " +
                "WHERE u.UserId=@Id", p => p.AddWithValue("@Id", userId));
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        public static bool StudentProfileComplete(int userId)
        {
            return SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.Students WHERE UserId=@Id AND CourseId IS NOT NULL " +
                "AND SemesterNumber IS NOT NULL AND EnrollNumber IS NOT NULL AND ISNULL(Status,'') <> 'Profile not set up'",
                p => p.AddWithValue("@Id", userId)) > 0;
        }

        public static bool FacultyProfileComplete(int userId)
        {
            return SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.Faculty WHERE UserId=@Id AND DeptCode IS NOT NULL AND DeptCode <> ''",
                p => p.AddWithValue("@Id", userId)) > 0;
        }

        /// <summary>True when the signed-in role's information screens have real data to show.</summary>
        public static bool ProfileReady(int userId, string role)
        {
            if (role == "Student") return StudentProfileComplete(userId);
            if (role == "Faculty") return FacultyProfileComplete(userId);
            return true;
        }

        public static int BlankStudentProfile(int userId)
        {
            object existing = SqlHelper.ExecuteScalar(
                "SELECT StudentId FROM dbo.Students WHERE UserId=@Id", p => p.AddWithValue("@Id", userId));
            if (existing != null && existing != DBNull.Value) return Convert.ToInt32(existing);
            return SqlHelper.ExecuteScalarInt(
                "INSERT INTO dbo.Students (UserId, EnrollNumber, CourseId, SessionId, SemesterNumber, Status) " +
                "VALUES (@Id, NULL, NULL, NULL, NULL, 'Profile not set up'); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                p => p.AddWithValue("@Id", userId));
        }

        public static int BlankFacultyProfile(int userId)
        {
            object existing = SqlHelper.ExecuteScalar(
                "SELECT FacultyId FROM dbo.Faculty WHERE UserId=@Id", p => p.AddWithValue("@Id", userId));
            if (existing != null && existing != DBNull.Value) return Convert.ToInt32(existing);
            return SqlHelper.ExecuteScalarInt(
                "INSERT INTO dbo.Faculty (UserId, DeptCode, Designation, Status) VALUES (@Id, NULL, NULL, 'Profile not set up'); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);", p => p.AddWithValue("@Id", userId));
        }

        /// <summary>Notifies every administrator once per person per day.</summary>
        public static void RaiseAdminRequest(int userId, string kind, string message)
        {
            int recent = SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.AdminRequests WHERE UserId=@Id AND Kind=@Kind " +
                "AND CreatedUtc > DATEADD(HOUR,-24,SYSUTCDATETIME())",
                p => { p.AddWithValue("@Id", userId); p.AddWithValue("@Kind", kind); });
            if (recent > 0) return;

            SqlHelper.ExecuteNonQuery(
                "INSERT INTO dbo.AdminRequests (UserId, Kind, Message) VALUES (@Id, @Kind, @Message)",
                p =>
                {
                    p.AddWithValue("@Id", userId);
                    p.AddWithValue("@Kind", kind);
                    p.AddWithValue("@Message", (object)message ?? DBNull.Value);
                });
            AuditService.Log("AdminRequestRaised", "AdminRequests", userId, kind, "Requests", userId, null, kind);
            NotifyRole("Admin", "Profile request from " + CurrentUser.FullName,
                message ?? "A user asked for help with their profile.", "system", "/Admin/Users.aspx?tab=requests");
        }

        public static int OpenAdminRequestCount()
        {
            return SqlHelper.ExecuteScalarInt("SELECT COUNT(*) FROM dbo.AdminRequests WHERE Status='open'");
        }

        public static DataTable IncompleteStudents()
        {
            return SqlHelper.FillDataTable(
                "SELECT u.UserId, u.FullName, u.Email, u.CreatedAtUtc, s.EnrollNumber " +
                "FROM dbo.Users u JOIN dbo.UserRoles ur ON ur.UserId=u.UserId JOIN dbo.Roles r ON r.RoleId=ur.RoleId " +
                "LEFT JOIN dbo.Students s ON s.UserId=u.UserId " +
                "WHERE r.RoleName='Student' AND (s.StudentId IS NULL OR s.CourseId IS NULL OR ISNULL(s.Status,'')='Profile not set up') " +
                "ORDER BY u.CreatedAtUtc DESC");
        }

        public static DataTable IncompleteFaculty()
        {
            return SqlHelper.FillDataTable(
                "SELECT u.UserId, u.FullName, u.Email, u.CreatedAtUtc, f.EmployeeId " +
                "FROM dbo.Users u JOIN dbo.UserRoles ur ON ur.UserId=u.UserId JOIN dbo.Roles r ON r.RoleId=ur.RoleId " +
                "LEFT JOIN dbo.Faculty f ON f.UserId=u.UserId " +
                "WHERE r.RoleName='Faculty' AND (f.FacultyId IS NULL OR f.DeptCode IS NULL OR f.DeptCode='') " +
                "ORDER BY u.CreatedAtUtc DESC");
        }

        public static void NotifyRole(string role, string title, string message, string ntype, string url)
        {
            try
            {
                string audience = role == "Admin" ? "admin" : role == "Faculty" ? "faculty" : "students";
                CommsRepo.CreateNotification(title, message, ntype, url,
                    CurrentUser.UserId > 0 ? CurrentUser.UserId : 0, audience);
            }
            catch (Exception ex) { Global.WriteLog("People", "notify role failed", ex); }
        }

        public static void NotifyUser(int userId, string title, string message, string ntype, string url)
        {
            try { CommsRepo.CreateNotification(title, message, ntype, url, CurrentUser.UserId > 0 ? CurrentUser.UserId : 0, "user:" + userId); }
            catch (Exception ex) { Global.WriteLog("People", "notify user failed", ex); }
        }

        // ---------------- admin save with field-level audit ----------------
        public static void Save(PersonInput input, bool isAdmin)
        {
            DataRow before = Person(input.UserId);
            if (before == null) throw new Exception("Person not found.");
            string role = Convert.ToString(before["RoleName"]);
            bool student = role == "Student";
            bool faculty = role == "Faculty";

            using (var conn = ConnectionManager.CreateOpenConnection())
            {
                SqlTransaction tx = conn.BeginTransaction();
                try
                {
                    Action<string, string, string, object> audit = (field, oldValue, newValue, ignored) =>
                    {
                        if (string.Equals(oldValue ?? "", newValue ?? "", StringComparison.OrdinalIgnoreCase)) return;
                        AuditService.Log("ProfileFieldUpdated", "Users", input.UserId,
                            field, "Profile", input.UserId, oldValue, newValue);
                    };

                    audit("FullName", Str(before["FullName"]), input.FullName, null);
                    audit("Phone", Str(before["Phone"]), input.Phone, null);
                    audit("DateOfBirth", DateStr(before["DateOfBirth"]), input.DateOfBirth, null);
                    audit("Gender", Str(before["Gender"]), input.Gender, null);
                    audit("BloodGroup", Str(before["BloodGroup"]), input.BloodGroup, null);
                    audit("Nationality", Str(before["Nationality"]), input.Nationality, null);
                    audit("AddressLine1", Str(before["AddressLine1"]), input.AddressLine1, null);
                    audit("City", Str(before["City"]), input.City, null);
                    audit("State", Str(before["StateName"]), input.StateName, null);
                    audit("PostalCode", Str(before["PostalCode"]), input.PostalCode, null);
                    audit("Country", Str(before["Country"]), input.Country, null);
                    audit("EmergencyContactName", Str(before["EmergencyContactName"]), input.EmergencyContactName, null);
                    audit("EmergencyContactPhone", Str(before["EmergencyContactPhone"]), input.EmergencyContactPhone, null);
                    audit("IsActive", Convert.ToBoolean(before["IsActive"]) ? "Active" : "Inactive", input.IsActive ? "Active" : "Inactive", null);

                    bool emailChanged = !string.Equals(Str(before["Email"]), input.Email, StringComparison.OrdinalIgnoreCase);
                    if (emailChanged)
                    {
                        if (UsersRepo.EmailExists(input.Email)) throw new Exception("That email address belongs to another account.");
                        audit("Email", Str(before["Email"]), input.Email, null);
                    }

                    using (var cmd = new SqlCommand(
                        "UPDATE dbo.Users SET FullName=@FullName, Email=@Email, Phone=@Phone, " +
                        "DateOfBirth=@Dob, Gender=@Gender, BloodGroup=@Blood, Nationality=@Nationality, " +
                        "AddressLine1=@A1, AddressLine2=@A2, City=@City, StateName=@State, PostalCode=@Postal, Country=@Country, " +
                        "EmergencyContactName=@Ecn, EmergencyContactPhone=@Ecp, ProfileImage=@Photo, IsActive=@Active, " +
                        "EmailVerified=CASE WHEN @EmailChanged=1 THEN 1 ELSE EmailVerified END, UpdatedAtUtc=SYSUTCDATETIME() " +
                        "WHERE UserId=@Id", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@FullName", input.FullName ?? "");
                        cmd.Parameters.AddWithValue("@Email", (input.Email ?? "").Trim().ToLowerInvariant());
                        cmd.Parameters.AddWithValue("@Phone", (object)input.Phone ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Dob", NullableDate(input.DateOfBirth));
                        cmd.Parameters.AddWithValue("@Gender", (object)input.Gender ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Blood", (object)input.BloodGroup ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Nationality", (object)input.Nationality ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@A1", (object)input.AddressLine1 ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@A2", (object)input.AddressLine2 ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@City", (object)input.City ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@State", (object)input.StateName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Postal", (object)input.PostalCode ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Country", (object)input.Country ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Ecn", (object)input.EmergencyContactName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Ecp", (object)input.EmergencyContactPhone ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Photo", (object)input.ProfileImage ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Active", input.IsActive ? 1 : 0);
                        cmd.Parameters.AddWithValue("@EmailChanged", emailChanged ? 1 : 0);
                        cmd.Parameters.AddWithValue("@Id", input.UserId);
                        cmd.ExecuteNonQuery();
                    }

                    if (student)
                    {
                        BlankStudentProfile(input.UserId);
                        audit("Enrollment number", Str(before["EnrollNumber"]), input.StudentEnrollNumber, null);
                        audit("Course", Str(before["CourseCode"]), input.CourseId.HasValue ? CourseLabel(input.CourseId.Value) : "", null);
                        audit("Semester", Str(before["SemesterNumber"]), input.SemesterNumber?.ToString(), null);
                        audit("Section", Str(before["SectionCode"]), input.SectionCode, null);
                        audit("Status", Str(before["StudentStatus"]), input.StudentStatus, null);
                        audit("Guardian", Str(before["GuardianName"]), input.GuardianName, null);

                        using (var cmd = new SqlCommand(
                            "UPDATE dbo.Students SET EnrollNumber=@Enroll, CourseId=@Course, SessionId=@Session, " +
                            "SemesterNumber=@Sem, SectionCode=@Section, Status=@Status, MentorUserId=@Mentor, " +
                            "FeePlanOverride=@Fee, Concessions=@Conc, Skills=@Skills, Achievements=@Ach, MedicalNotes=@Medical, " +
                            "AdmissionDate=@Admission, GuardianName=@Guardian, Address=@Address, DateOfBirth=@Dob " +
                            "WHERE UserId=@Id", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@Enroll", NullIfEmpty(input.StudentEnrollNumber));
                            cmd.Parameters.AddWithValue("@Course", (object)input.CourseId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Session", (object)input.SessionId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Sem", (object)input.SemesterNumber ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Section", (object)NullIfEmpty(input.SectionCode) ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(input.StudentStatus)
                                ? (input.CourseId.HasValue ? "Active" : "Profile not set up") : input.StudentStatus);
                            cmd.Parameters.AddWithValue("@Mentor", (object)input.MentorUserId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Fee", (object)input.FeePlanOverride ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Conc", (object)input.Concessions ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Skills", (object)input.Skills ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Ach", (object)input.Achievements ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Medical", (object)input.MedicalNotes ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Admission", NullableDate(input.AdmissionDate));
                            cmd.Parameters.AddWithValue("@Guardian", (object)input.GuardianName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Address", (object)input.Address ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Dob", NullableDate(input.DateOfBirth));
                            cmd.Parameters.AddWithValue("@Id", input.UserId);
                            cmd.ExecuteNonQuery();
                        }

                        if (input.CourseId.HasValue && input.SessionId.HasValue)
                        {
                            object sid = SqlHelper.ExecuteScalar("SELECT StudentId FROM dbo.Students WHERE UserId=@Id",
                                p => p.AddWithValue("@Id", input.UserId));
                            if (sid != null && sid != DBNull.Value)
                                SqlHelper.ExecuteNonQuery(
                                    "IF NOT EXISTS (SELECT 1 FROM dbo.Enrollments WHERE StudentId=@S AND CourseId=@C AND SessionId=@Ss) " +
                                    "INSERT INTO dbo.Enrollments (StudentId, CourseId, SessionId) VALUES (@S,@C,@Ss)",
                                    p =>
                                    {
                                        p.AddWithValue("@S", Convert.ToInt32(sid));
                                        p.AddWithValue("@C", input.CourseId.Value);
                                        p.AddWithValue("@Ss", input.SessionId.Value);
                                    });
                        }
                        if (input.CourseId.HasValue && input.SessionId.HasValue && input.SemesterNumber.HasValue)
                            AuditService.Log("StudentProfileCompleted", "Students", input.UserId,
                                "course/semester/section saved", "Profile", input.UserId, "", "complete");
                    }
                    else if (faculty)
                    {
                        BlankFacultyProfile(input.UserId);
                        audit("Department", Str(before["DeptCode"]), input.DeptCode, null);
                        audit("Designation", Str(before["Designation"]), input.Designation, null);
                        audit("Employee ID", Str(before["EmployeeId"]), input.EmployeeId, null);
                        audit("Joining date", DateStr(before["JoinedOn"]), input.JoinedOn, null);
                        audit("Qualifications", Str(before["Qualifications"]), input.Qualifications, null);
                        audit("Experience", Str(before["ExperienceYears"]), input.ExperienceYears?.ToString(), null);
                        audit("Specialisations", Str(before["Specialisations"]), input.Specialisations, null);
                        audit("Office hours", Str(before["OfficeHours"]), input.OfficeHours, null);

                        using (var cmd = new SqlCommand(
                            "UPDATE dbo.Faculty SET EmployeeId=@Emp, DeptCode=@Dept, Designation=@Desig, JoinedOn=@Joined, " +
                            "Qualifications=@Qual, ExperienceYears=@Exp, Specialisations=@Spec, OfficeHours=@Hours, Status=@Status " +
                            "WHERE UserId=@Id", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@Emp", (object)NullIfEmpty(input.EmployeeId) ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Dept", (object)NullIfEmpty(input.DeptCode) ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Desig", (object)input.Designation ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Joined", NullableDate(input.JoinedOn));
                            cmd.Parameters.AddWithValue("@Qual", (object)input.Qualifications ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Exp", (object)input.ExperienceYears ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Spec", (object)input.Specialisations ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Hours", (object)input.OfficeHours ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(input.FacultyStatus)
                                ? (string.IsNullOrWhiteSpace(input.DeptCode) ? "Profile not set up" : "Active") : input.FacultyStatus);
                            cmd.Parameters.AddWithValue("@Id", input.UserId);
                            cmd.ExecuteNonQuery();
                        }
                        if (!string.IsNullOrWhiteSpace(input.DeptCode))
                            AuditService.Log("FacultyProfileCompleted", "Faculty", input.UserId,
                                "department saved", "Profile", input.UserId, "", input.DeptCode);
                    }

                    tx.Commit();
                    NotifyUser(input.UserId, "Administrator updated your profile",
                        "Your profile details were updated by an administrator.", "system", "/Account/Settings.aspx");
                }
                catch { tx.Rollback(); throw; }
            }
        }

        // ---------------- email change requests ----------------
        public static string StartEmailChange(int userId, string newEmail)
        {
            newEmail = (newEmail ?? "").Trim().ToLowerInvariant();
            if (newEmail.Length < 5 || !newEmail.Contains("@") || !newEmail.Contains(".")) return "Enter a valid email address.";
            if (UsersRepo.EmailExists(newEmail)) return "That email address is already in use.";
            int pending = SqlHelper.ExecuteScalarInt(
                "SELECT COUNT(*) FROM dbo.ProfileRequests WHERE UserId=@Id AND Kind='email' AND Status='pending'",
                p => p.AddWithValue("@Id", userId));
            if (pending > 0) return "You already have an email-change request awaiting review.";

            string code = PasswordHasher.GenerateNumericOtp(AppConfig.GetInt("Otp:Length", 6));
            SqlHelper.ExecuteNonQuery(
                "DELETE FROM dbo.ProfileRequests WHERE UserId=@Id AND Kind='email' AND Status<>'pending'; " +
                "INSERT INTO dbo.ProfileRequests (UserId, Kind, FieldName, OldValue, NewValue, Stage, Status, VerifyCodeHash, VerifyExpiresUtc, ExpiresUtc) " +
                "VALUES (@Id,'email','Email',(SELECT Email FROM dbo.Users WHERE UserId=@Id),@New,'verify','pending',@Hash,DATEADD(MINUTE,10,SYSUTCDATETIME()),DATEADD(DAY,7,SYSUTCDATETIME()));",
                p =>
                {
                    p.AddWithValue("@Id", userId);
                    p.AddWithValue("@New", newEmail);
                    p.AddWithValue("@Hash", PasswordHasher.Hmac(AppConfig.Get("App:OtpSecret", "sms-otp-key-v1-static"), code));
                });

            var mail = new SmtpMailService();
            string body = mail.BuildOtpTemplate(CurrentUser.FullName, "email change", code, 10);
            if (!mail.Send(newEmail, "Confirm your new email address", body))
                return "We could not send the code to the new address. Check the address and try again.";

            AuditService.Log("EmailChangeRequested", "ProfileRequests", userId, newEmail, "Requests", userId, null, newEmail);
            return null;
        }

        public static string VerifyEmailChange(int userId, string code)
        {
            DataRow req = NewestVerifyRequest(userId);
            if (req == null) return "Start the email change again.";
            if (req["VerifyExpiresUtc"] != DBNull.Value && Convert.ToDateTime(req["VerifyExpiresUtc"]) < DateTime.UtcNow)
                return "The code expired. Start the email change again.";
            string expected = AppConfig.Get("App:OtpSecret", "sms-otp-key-v1-static");
            string hash = PasswordHasher.Hmac(expected, (code ?? "").Trim());
            if (!string.Equals(hash, Convert.ToString(req["VerifyCodeHash"]), StringComparison.Ordinal))
                return "That code is not correct.";

            int requestId = Convert.ToInt32(req["RequestId"]);
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.ProfileRequests SET Stage='pending', VerifiedUtc=SYSUTCDATETIME(), VerifyCodeHash=NULL, " +
                "ExpiresUtc=DATEADD(DAY,7,SYSUTCDATETIME()) WHERE RequestId=@Id",
                p => p.AddWithValue("@Id", requestId));
            AuditService.Log("EmailChangeVerified", "ProfileRequests", requestId,
                Convert.ToString(req["NewValue"]), "Requests", userId, null, Convert.ToString(req["NewValue"]));
            NotifyRole("Admin", "Email change request",
                CurrentUser.FullName + " asked to change their email to " + Convert.ToString(req["NewValue"]) + ".",
                "system", "/Admin/Users.aspx?tab=requests");
            return null;
        }

        private static DataRow NewestVerifyRequest(int userId)
        {
            DataTable dt = SqlHelper.FillDataTable(
                "SELECT TOP 1 * FROM dbo.ProfileRequests WHERE UserId=@Id AND Kind='email' AND Status='pending' ORDER BY RequestId DESC",
                p => p.AddWithValue("@Id", userId));
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        public static DataTable PendingRequests()
        {
            return SqlHelper.FillDataTable(
                "SELECT pr.*, u.FullName, u.Email AS CurrentEmail FROM dbo.ProfileRequests pr " +
                "JOIN dbo.Users u ON u.UserId=pr.UserId " +
                "WHERE pr.Status='pending' AND pr.Stage='pending' AND (pr.ExpiresUtc IS NULL OR pr.ExpiresUtc > SYSUTCDATETIME()) " +
                "ORDER BY pr.RequestedUtc DESC");
        }

        public static DataTable MyRequests(int userId)
        {
            return SqlHelper.FillDataTable(
                "SELECT * FROM dbo.ProfileRequests WHERE UserId=@Id ORDER BY RequestId DESC",
                p => p.AddWithValue("@Id", userId));
        }

        public static void DecideRequest(int requestId, bool approve, string reason)
        {
            DataRow req = SqlHelper.FillDataTable("SELECT * FROM dbo.ProfileRequests WHERE RequestId=@Id",
                p => p.AddWithValue("@Id", requestId)).Rows.Cast<DataRow>().FirstOrDefault();
            if (req == null) throw new Exception("Request not found.");
            if (Convert.ToString(req["Status"]) != "pending") throw new Exception("This request has already been decided.");

            int userId = Convert.ToInt32(req["UserId"]);
            string newEmail = Convert.ToString(req["NewValue"]);
            string oldEmail = Convert.ToString(req["OldValue"]);

            if (approve)
            {
                if (UsersRepo.EmailExists(newEmail) && !string.Equals(newEmail, Convert.ToString(req["CurrentEmail"]), StringComparison.OrdinalIgnoreCase))
                    throw new Exception("The new address is now used by another account.");
                SqlHelper.ExecuteNonQuery(
                    "UPDATE dbo.Users SET Email=@New, EmailVerified=1, UpdatedAtUtc=SYSUTCDATETIME() WHERE UserId=@Id",
                    p => { p.AddWithValue("@New", newEmail); p.AddWithValue("@Id", userId); });
                AuditService.Log("EmailChangeApproved", "Users", userId, oldEmail + " -> " + newEmail,
                    "Requests", userId, oldEmail, newEmail);
            }
            else
            {
                AuditService.Log("EmailChangeRejected", "Users", userId, reason,
                    "Requests", userId, oldEmail, newEmail);
            }

            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.ProfileRequests SET Status=@Status, Reason=@Reason, DecidedBy=@Admin, DecidedUtc=SYSUTCDATETIME(), Stage='done' WHERE RequestId=@Id",
                p =>
                {
                    p.AddWithValue("@Status", approve ? "approved" : "rejected");
                    p.AddWithValue("@Reason", (object)reason ?? DBNull.Value);
                    p.AddWithValue("@Admin", CurrentUser.UserId);
                    p.AddWithValue("@Id", requestId);
                });

            NotifyUser(userId, approve ? "Email address changed" : "Email change request rejected",
                approve ? "Your sign-in email is now " + newEmail + "." : "Your email was not changed. " + (reason ?? ""),
                "system", "/Account/Settings.aspx?tab=account");

            try
            {
                var mail = new SmtpMailService();
                string subject = approve ? "Your email address was changed" : "Your email change request was rejected";
                string body = "<p>Hello " + System.Web.HttpUtility.HtmlEncode(CurrentUser.FullName) + ",</p><p>" +
                    (approve ? "Your sign-in email is now <b>" + System.Web.HttpUtility.HtmlEncode(newEmail) + "</b>."
                             : "Your request to change your email to " + System.Web.HttpUtility.HtmlEncode(newEmail) + " was rejected. " + System.Web.HttpUtility.HtmlEncode(reason ?? "")) +
                    "</p>";
                mail.Send(approve ? newEmail : oldEmail, subject, body);
            }
            catch (Exception ex) { Global.WriteLog("People", "request mail failed", ex); }
        }

        public static void CancelRequest(int requestId, int userId)
        {
            SqlHelper.ExecuteNonQuery(
                "UPDATE dbo.ProfileRequests SET Status='cancelled', Stage='done', DecidedUtc=SYSUTCDATETIME() " +
                "WHERE RequestId=@Id AND UserId=@User AND Status='pending'",
                p => { p.AddWithValue("@Id", requestId); p.AddWithValue("@User", userId); });
            AuditService.Log("EmailChangeCancelled", "ProfileRequests", requestId, "cancelled by user", "Requests", userId, null, null);
        }

        // ---------------- activity ----------------
        public static DataTable Activity(int? userId, int? targetUserId, string category, DateTime? from, DateTime? to, int limit)
        {
            return SqlHelper.FillDataTable(
                "SELECT TOP (@Limit) a.AuditId, a.UserId, a.TargetUserId, a.Action, a.Category, a.EntityTable, a.EntityId, " +
                "a.OldValue, a.NewValue, a.DetailJson, a.CreatedAtUtc, " +
                "actor.FullName AS ActorName, target.FullName AS TargetName " +
                "FROM dbo.AuditLogs a " +
                "LEFT JOIN dbo.Users actor ON actor.UserId=a.UserId " +
                "LEFT JOIN dbo.Users target ON target.UserId=a.TargetUserId " +
                "WHERE (@User IS NULL OR a.UserId=@User) " +
                "AND (@Target IS NULL OR a.TargetUserId=@Target) " +
                "AND (@Category IS NULL OR a.Category=@Category) " +
                "AND (@From IS NULL OR a.CreatedAtUtc >= @From) " +
                "AND (@To IS NULL OR a.CreatedAtUtc < @To) " +
                "ORDER BY a.AuditId DESC",
                p =>
                {
                    p.AddWithValue("@Limit", limit <= 0 ? 200 : limit);
                    p.AddWithValue("@User", (object)userId ?? DBNull.Value);
                    p.AddWithValue("@Target", (object)targetUserId ?? DBNull.Value);
                    p.AddWithValue("@Category", (object)(string.IsNullOrWhiteSpace(category) ? null : category) ?? DBNull.Value);
                    p.AddWithValue("@From", (object)from ?? DBNull.Value);
                    p.AddWithValue("@To", (object)to ?? DBNull.Value);
                });
        }

        public static void ResetPasswordAndMail(int userId)
        {
            string raw = ResetTokenRepo.CreateAndReturnRaw(userId);
            DataRow p = Person(userId);
            string email = p == null ? null : Convert.ToString(p["Email"]);
            if (!string.IsNullOrEmpty(email))
            {
                string url = AppConfig.BaseUrl.TrimEnd('/') + "/Account/Reset.aspx?token=" + System.Web.HttpUtility.UrlEncode(raw);
                try { new SmtpMailService().Send(email, "Reset your password", "<p>Use this link to reset your password:</p><p><a href='" + url + "'>" + url + "</a></p>"); }
                catch (Exception ex) { Global.WriteLog("People", "reset mail failed", ex); }
            }
            AuditService.Log("PasswordResetByAdmin", "Users", userId, "email sent", "Account", userId, null, null);
        }

        // ---------------- helpers ----------------
        private static string Str(object v) { return v == null || v == DBNull.Value ? null : Convert.ToString(v); }
        private static string DateStr(object v) { return v == null || v == DBNull.Value ? null : Convert.ToDateTime(v).ToString("yyyy-MM-dd"); }
        private static object NullableDate(string s)
        {
            DateTime d;
            return DateTime.TryParse(s, out d) ? (object)d.Date : DBNull.Value;
        }
        private static string NullIfEmpty(string s) { return string.IsNullOrWhiteSpace(s) ? null : s.Trim(); }
        private static string CourseLabel(int courseId)
        {
            object v = SqlHelper.ExecuteScalar("SELECT CourseCode FROM dbo.Courses WHERE CourseId=@Id",
                p => p.AddWithValue("@Id", courseId));
            return v == null || v == DBNull.Value ? "" : Convert.ToString(v);
        }
    }
}
