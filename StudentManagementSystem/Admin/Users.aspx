<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Users.aspx.cs" Inherits="StudentManagementSystem.Admin.Users" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Users - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
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

    <div class="tile mb-3"><div class="tile__body">
        <form method="get" action="/Admin/Users.aspx" class="srch-bar">
            <input type="text" name="q" value="<%= Attr(QTerm) %>" placeholder="Name, email or enrollment no." class="form-control form-control-sm"/>
            <select name="role" class="form-select form-select-sm" style="max-width:150px">
                <option value="">All roles</option>
                <option value="Student"<%= RoleSelected("Student") %>>Students</option>
                <option value="Faculty"<%= RoleSelected("Faculty") %>>Faculty</option>
                <option value="Admin"<%= RoleSelected("Admin") %>>Admins</option>
            </select>
            <select name="profile" class="form-select form-select-sm" style="max-width:210px">
                <option value="">All profiles</option>
                <option value="incomplete"<%= ProfileSelected("incomplete") %>>Profiles to complete</option>
            </select>
            <label class="small text-muted"><input type="checkbox" name="all" value="1"<%= IncludeInactiveChecked() %>/> include inactive</label>
            <button class="btn btn-sms btn-sm">Search</button>
            <span class="ms-auto small"><a href="/Admin/Users.aspx">All</a> &middot; <a href="/Admin/Users.aspx?profile=incomplete">Profiles to complete</a></span>
        </form>
    </div></div>

    <%= PendingTable %>
    <%= UsersTable %>

    <div class="tile mb-3"><div class="tile__head">Bulk account actions</div><div class="tile__body">
        <form id="bulkUsers" method="post" action="/Admin/Users.aspx" class="srch-bar">
            <%= CsrfField() %><input type="hidden" name="op" value="bulk"/>
            <span class="small text-muted">Select accounts with the checkboxes in the table, then:</span>
            <select name="bulkAction" class="form-select form-select-sm" style="max-width:190px">
                <option value="role">Assign role</option>
                <option value="activate">Activate</option>
                <option value="deactivate">Deactivate</option>
                <option value="verify">Verify email</option>
                <option value="approve">Approve registration</option>
                <option value="reject">Reject registration</option>
            </select>
            <select name="role" class="form-select form-select-sm" style="max-width:150px">
                <option value="Student">Student</option>
                <option value="Faculty">Faculty</option>
                <option value="Admin">Admin</option>
            </select>
            <button class="btn btn-sms btn-sm">Apply to selected</button>
        </form>
        <div class="small text-muted mt-1">Your own account is always skipped. Changes are written to the audit trail.</div>
    </div></div>

    <div class="bento">
        <div class="c-6">
            <div class="tile"><div class="tile__head">Add student (creates user + profile)</div><div class="tile__body">
                <form method="post" action="/Admin/Users.aspx" class="form-grid">
                    <%= CsrfField() %><input type="hidden" name="op" value="addstudent"/>
                    <div class="c-6"><label class="form-label small">Full name</label><input name="fullName" required class="form-control form-control-sm" maxlength="120"/></div>
                    <div class="c-6"><label class="form-label small">Email</label><input name="email" type="email" required class="form-control form-control-sm" maxlength="150"/></div>
                    <div class="c-6"><label class="form-label small">Phone (optional)</label><input name="phone" class="form-control form-control-sm" maxlength="15" pattern="[0-9]{10,15}"/></div>
                    <div class="c-6"><label class="form-label small">Enrollment number</label><input name="enrollNumber" required class="form-control form-control-sm" maxlength="24"/></div>
                    <div class="c-6"><label class="form-label small">Course</label><select name="courseId" class="form-select form-select-sm"><%= CourseOptions %></select></div>
                    <div class="c-3"><label class="form-label small">Session</label><select name="sessionId" class="form-select form-select-sm"><%= SessionOptions %></select></div>
                    <div class="c-3"><label class="form-label small">Semester</label><select name="semester" class="form-select form-select-sm"><%= SemesterOptions %></select></div>
                    <div class="c-4"><label class="form-label small">Date of birth</label><input name="dob" type="date" class="form-control form-control-sm"/></div>
                    <div class="c-8"><label class="form-label small">Guardian name</label><input name="guardian" class="form-control form-control-sm" maxlength="120"/></div>
                    <div class="c-12"><label class="form-label small">Address</label><input name="address" class="form-control form-control-sm" maxlength="300"/></div>
                    <div class="c-12 mt-2"><button class="btn btn-sms btn-sm">Create student</button>
                        <div class="small text-muted">A private temporary password is generated and shown after saving; the account starts <b>unverified</b>.</div></div>
                </form>
            </div></div>
        </div>
        <div class="c-6">
            <div class="tile"><div class="tile__head">Add faculty</div><div class="tile__body">
                <form method="post" action="/Admin/Users.aspx" class="form-grid">
                    <%= CsrfField() %><input type="hidden" name="op" value="addfaculty"/>
                    <div class="c-6"><label class="form-label small">Full name</label><input name="fullName" required class="form-control form-control-sm" maxlength="120"/></div>
                    <div class="c-6"><label class="form-label small">Email</label><input name="email" type="email" required class="form-control form-control-sm" maxlength="150"/></div>
                    <div class="c-6"><label class="form-label small">Phone (optional)</label><input name="phone" class="form-control form-control-sm" maxlength="15" pattern="[0-9]{10,15}"/></div>
                    <div class="c-6"><label class="form-label small">Department</label><input name="dept" required class="form-control form-control-sm" maxlength="60"/></div>
                    <div class="c-12"><label class="form-label small">Designation</label><input name="designation" class="form-control form-control-sm" maxlength="60"/></div>
                    <div class="c-12 mt-2"><button class="btn btn-sms btn-sm">Create faculty</button>
                        <div class="small text-muted">A private temporary password is generated and shown after saving.</div></div>
                </form>
            </div></div>
        </div>
    </div>
    </div>
</asp:Content>



