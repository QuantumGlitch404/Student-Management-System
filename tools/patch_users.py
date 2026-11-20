# -*- coding: utf-8 -*-
import io

def read(p):
    return io.open(p, encoding="utf-8-sig").read()

def write(p, s):
    io.open(p, "w", encoding="utf-8-sig").write(s)

p = "StudentManagementSystem/Code/Data/StudentsRepo.cs"
s = read(p)
s = s.replace('"c.CourseCode, c.CourseName, cs.SessionName " +',
              '"c.CourseCode, c.CourseName, cs.SessionName, u.LastLoginAtUtc, u.CreatedAtUtc " +')
write(p, s)

p = "StudentManagementSystem/Admin/Users.aspx"
s = read(p)
s = s.replace("""    <%= FlashHtml %>

    <div class="card mb-3"><div class="card-body">""", """    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <div class="kicker">People</div>
            <h1>User accounts</h1>
            <p class="page-sub">Search, verify, activate and export every account. Passwords are never shown or stored in plain text.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=students&amp;format=pdf">Students PDF</a>
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=students&amp;format=xlsx">Students Excel</a>
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=students&amp;format=csv">Students CSV</a>
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=faculty&amp;format=pdf">Faculty PDF</a>
        </div>
    </div>

    <div class="card mb-3"><div class="card-body">""")

# bulk form after tables
s = s.replace("""    <%= PendingTable %>
    <%= UsersTable %>
""", """    <%= PendingTable %>
    <%= UsersTable %>

    <div class="card mb-3"><div class="card-header">Bulk account actions</div><div class="card-body">
        <form id="bulkUsers" method="post" action="/Admin/Users.aspx" class="srch-bar">
            <%= CsrfField() %><input type="hidden" name="op" value="bulk"/>
            <span class="small text-muted">Select accounts with the checkboxes in the table, then:</span>
            <select name="bulkAction" class="form-select form-select-sm" style="max-width:190px">
                <option value="role">Assign role</option>
                <option value="activate">Activate</option>
                <option value="deactivate">Deactivate</option>
                <option value="verify">Verify email</option>
            </select>
            <select name="role" class="form-select form-select-sm" style="max-width:150px">
                <option value="Student">Student</option>
                <option value="Faculty">Faculty</option>
                <option value="Admin">Admin</option>
            </select>
            <button class="btn btn-sms btn-sm" data-confirm="Apply this bulk change to every selected account?">Apply to selected</button>
        </form>
        <div class="small text-muted mt-1">Your own account is always skipped. Changes are written to the audit trail.</div>
    </div></div>
""")
write(p, s)

p = "StudentManagementSystem/Admin/Users.aspx.cs"
s = read(p)

# Handle bulk op
s = s.replace("""                    case "addstudent":
                        AddStudent();
                        break;""", """                    case "bulk":
                        RunBulk();
                        break;

                    case "addstudent":
                        AddStudent();
                        break;""")

s = s.replace("""        private void AddStudent()""", """        private void RunBulk()
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
                    default:
                        throw new Exception("Unknown bulk action.");
                }
                count++;
            }
            Flash("success", count + " account(s) updated" + (skipped > 0 ? "; your own account was skipped." : "."));
        }

        private void AddStudent()""")

# checkbox column in student table
s = s.replace("""                "<th>Enrollment</th><th>Name</th><th>Email</th><th>Course</th><th>Status</th><th style='width:280px'>Actions</th></tr></thead><tbody>");""",
              """                "<th><input type='checkbox' data-check-all='bulkUsers' title='Select all'/></th><th>Enrollment</th><th>Name</th><th>Email</th><th>Course</th><th>Status</th><th>Last login</th><th>Created</th><th style='width:280px'>Actions</th></tr></thead><tbody>");""")
s = s.replace("""                sb.Append("<tr><td colspan='6' class='msg-empty'>No student matches this filter.</td></tr>");""",
              """                sb.Append("<tr><td colspan='9' class='msg-empty'>No student matches this filter.</td></tr>");""")
s = s.replace("""                sb.Append("<tr><td><b>" + E(r["EnrollNumber"]) + "</b></td>");""",
              """                sb.Append("<tr><td><input type='checkbox' name='ids' value='" + E(r["UserId"]) + "' form='bulkUsers'/></td>");
                sb.Append("<td><b>" + E(r["EnrollNumber"]) + "</b></td>");""")
s = s.replace("""                sb.Append("<td>" + StatusBadge(isActive, verified) + "</td><td>");
                sb.Append(ActButton("toggle", r["UserId"], isActive ? "0" : "1", isActive ? "Deactivate" : "Activate", true));""",
              """                sb.Append("<td>" + StatusBadge(isActive, verified) + "</td>");
                sb.Append("<td class='mono small'>" + (r["LastLoginAtUtc"] == DBNull.Value ? "-" : ((DateTime)r["LastLoginAtUtc"]).ToString("yyyy-MM-dd HH:mm")) + "</td>");
                sb.Append("<td class='mono small'>" + ((DateTime)r["CreatedAtUtc"]).ToString("yyyy-MM-dd") + "</td><td>");
                sb.Append(ActButton("toggle", r["UserId"], isActive ? "0" : "1", isActive ? "Deactivate" : "Activate", true));""")

# staff table: selection + last login
s = s.replace('''                "SELECT TOP 300 u.UserId, u.FullName, u.Email, u.Phone, u.IsActive, u.EmailVerified, f.DeptCode, f.Designation " +''',
              '''                "SELECT TOP 300 u.UserId, u.FullName, u.Email, u.Phone, u.IsActive, u.EmailVerified, u.LastLoginAtUtc, u.CreatedAtUtc, f.DeptCode, f.Designation " +''')
s = s.replace('''                "<th>Name</th><th>Email</th><th>Phone</th><th>Dept</th><th>Status</th><th style='width:280px'>Actions</th></tr></thead><tbody>");''',
              '''                "<th><input type='checkbox' data-check-all='bulkUsers' title='Select all'/></th><th>Name</th><th>Email</th><th>Phone</th><th>Dept</th><th>Status</th><th>Last login</th><th style='width:280px'>Actions</th></tr></thead><tbody>");''')
s = s.replace('''                sb.Append("<tr><td colspan='6' class='msg-empty'>No " + E(role.ToLowerInvariant()) + " account found.</td></tr>");''',
              '''                sb.Append("<tr><td colspan='8' class='msg-empty'>No " + E(role.ToLowerInvariant()) + " account found.</td></tr>");''')
s = s.replace('''                sb.Append("<tr><td>" + E(r["FullName"]) + "</td><td>" + E(r["Email"]) + "</td><td>" + E(r["Phone"]) + "</td>");''',
              '''                sb.Append("<tr><td><input type='checkbox' name='ids' value='" + E(r["UserId"]) + "' form='bulkUsers'/></td>");
                sb.Append("<td>" + E(r["FullName"]) + "</td><td>" + E(r["Email"]) + "</td><td>" + E(r["Phone"]) + "</td>");''')
s = s.replace('''                sb.Append("<td>" + StatusBadge(isActive, verified) + "</td><td>");''',
              '''                sb.Append("<td>" + StatusBadge(isActive, verified) + "</td>");
                sb.Append("<td class='mono small'>" + (r["LastLoginAtUtc"] == DBNull.Value ? "-" : ((DateTime)r["LastLoginAtUtc"]).ToString("yyyy-MM-dd HH:mm")) + "</td><td>");''')

write(p, s)
print("users patched")
