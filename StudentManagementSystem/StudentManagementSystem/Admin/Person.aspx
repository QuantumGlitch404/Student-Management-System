<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Person.aspx.cs" Inherits="StudentManagementSystem.Admin.Person" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Person - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <div class="kicker">People</div>
            <h1><%= E(PersonName) %></h1>
            <p class="page-sub">Every field is editable by an administrator. Each change is written to the audit trail with the old and new value.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-soft" href="/Admin/Users.aspx">Back to people</a>
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=person&amp;userId=<%= UserId %>&amp;format=pdf">Profile PDF</a>
        </div>
    </div>

    <div class="tile mb-3"><div class="tile__body">
      <div class="profile-head">
        <div class="profile-avatar"><%= AvatarHtml %></div>
        <div>
          <div class="h5 mb-1"><%= E(PersonName) %> <span class="badge badge-info"><%= E(RoleName) %></span></div>
          <div class="profile-id"><%= E(IdentityLine) %></div>
          <div class="profile-meta">
            <span class="badge <%= ProfileReady ? "badge-ok" : "badge-warn" %>"><%= ProfileReady ? "Profile set up" : "Profile not set up" %></span>
            <span class="badge <%= IsActive ? "badge-ok" : "badge-off" %>"><%= IsActive ? "Active" : "Disabled" %></span>
            <span class="small text-muted">Member since <%= E(MemberSince) %></span>
            <span class="small text-muted">Last sign-in <%= E(LastLogin) %></span>
          </div>
        </div>
      </div>
    </div></div>

    <div class="profile-shell">
      <aside>
        <div class="tile profile-nav"><div class="tile__body">
          <a href="#personal" class="active">Personal information</a>
          <a href="#contact">Contact and address</a>
          <a href="#role-data"><%= RoleName == "Student" ? "Academic" : RoleName == "Faculty" ? "Professional" : "Administration" %></a>
          <a href="#account">Account and security</a>
          <a href="#activity">Activity</a>
          <a href="#requests">Requests</a>
        </div></div>
      </aside>

      <div>
        <form method="post" action="/Admin/Person.aspx?id=<%= UserId %>" class="tile mb-3">
          <%= CsrfField() %><input type="hidden" name="op" value="save"/>
          <div class="tile__head">Personal information</div>
          <div class="tile__body">
            <div id="personal" class="bento">
              <div class="c-6"><label class="form-label">Full name</label><input class="form-control form-control-sm" name="fullName" required maxlength="150" value="<%= Attr(Val("FullName")) %>"/></div>
              <div class="c-6"><label class="form-label">Email</label><input class="form-control form-control-sm" type="email" name="email" required maxlength="150" value="<%= Attr(Val("Email")) %>"/>
                <div class="small text-muted">Changing this directly updates the sign-in address after the confirmation on save.</div></div>
              <div class="c-4"><label class="form-label">Phone</label><input class="form-control form-control-sm" name="phone" maxlength="15" value="<%= Attr(Val("Phone")) %>"/></div>
              <div class="c-4"><label class="form-label">Date of birth</label><input class="form-control form-control-sm" type="date" name="dob" value="<%= Attr(Val("DateOfBirth")) %>"/></div>
              <div class="c-4"><label class="form-label">Gender</label><input class="form-control form-control-sm" name="gender" maxlength="24" value="<%= Attr(Val("Gender")) %>"/></div>
              <div class="c-4"><label class="form-label">Blood group</label><input class="form-control form-control-sm" name="blood" maxlength="10" value="<%= Attr(Val("BloodGroup")) %>"/></div>
              <div class="c-4"><label class="form-label">Nationality</label><input class="form-control form-control-sm" name="nationality" maxlength="60" value="<%= Attr(Val("Nationality")) %>"/></div>
              <div class="c-4"><label class="form-label">Photo URL</label><input class="form-control form-control-sm" name="photo" maxlength="300" value="<%= Attr(Val("ProfileImage")) %>"/></div>
            </div>

            <h3 id="contact" class="h6 mt-4 mb-2">Contact and address</h3>
            <div class="bento">
              <div class="c-8"><label class="form-label">Address line 1</label><input class="form-control form-control-sm" name="addr1" maxlength="200" value="<%= Attr(Val("AddressLine1")) %>"/></div>
              <div class="c-4"><label class="form-label">Address line 2</label><input class="form-control form-control-sm" name="addr2" maxlength="200" value="<%= Attr(Val("AddressLine2")) %>"/></div>
              <div class="c-3"><label class="form-label">City</label><input class="form-control form-control-sm" name="city" maxlength="80" value="<%= Attr(Val("City")) %>"/></div>
              <div class="c-3"><label class="form-label">State</label><input class="form-control form-control-sm" name="state" maxlength="80" value="<%= Attr(Val("StateName")) %>"/></div>
              <div class="c-3"><label class="form-label">Postal code</label><input class="form-control form-control-sm" name="postal" maxlength="20" value="<%= Attr(Val("PostalCode")) %>"/></div>
              <div class="c-3"><label class="form-label">Country</label><input class="form-control form-control-sm" name="country" maxlength="80" value="<%= Attr(Val("Country")) %>"/></div>
              <div class="c-6"><label class="form-label">Emergency contact name</label><input class="form-control form-control-sm" name="ecName" maxlength="120" value="<%= Attr(Val("EmergencyContactName")) %>"/></div>
              <div class="c-6"><label class="form-label">Emergency contact phone</label><input class="form-control form-control-sm" name="ecPhone" maxlength="20" value="<%= Attr(Val("EmergencyContactPhone")) %>"/></div>
            </div>

            <h3 id="role-data" class="h6 mt-4 mb-2"><%= RoleName == "Student" ? "Academic" : RoleName == "Faculty" ? "Professional" : "Administration" %></h3>
            <%= RoleFieldsHtml %>

            <h3 id="account" class="h6 mt-4 mb-2">Account and security</h3>            <div class="bento">
              <div class="c-4"><label class="form-label d-flex align-items-center gap-2"><input type="checkbox" name="isActive" value="1" <%= IsActive ? "checked" : "" %>/> Account is active</label></div>
              <div class="c-4"><div class="form-label">Email verified</div><div class="profile-readonly"><%= Verified ? "Yes" : "No" %></div></div>
              <div class="c-4"><div class="form-label">Two-factor</div><div class="profile-readonly"><%= TwoFactor ? "Enabled" : "Not enabled" %></div></div>
            </div>

            <div class="profile-savebar">
              <span class="hint">Saving writes an audit row for every changed field.</span>
              <a class="btn btn-outline-sms btn-sm" href="/Admin/Person.aspx?id=<%= UserId %>">Cancel</a>
              <button class="btn btn-sms btn-sm" data-confirm="Save the changes to this person?">Save changes</button>
            </div>
          </div>
        </form>

        <div class="tile mt-3" id="documents"><div class="tile__head">Documents</div><div class="tile__body">
          <div class="sms-table-wrap table-wrap mb-2"><%= DocumentsHtml %></div>
          <form method="post" action="/Admin/Person.aspx?id=<%= UserId %>" enctype="multipart/form-data" class="srch-bar">
            <%= CsrfField() %><input type="hidden" name="op" value="uploaddoc"/>
            <select name="docKind" class="form-select form-select-sm" style="max-width:170px"><option>ID proof</option><option>Photo</option><option>Marksheet</option><option>Certificate</option><option>Other</option></select>
            <input type="file" name="docFile" required class="form-control form-control-sm" style="max-width:260px"/>
            <input name="docNote" class="form-control form-control-sm" maxlength="300" placeholder="Note (optional)"/>
            <button class="btn btn-sms btn-sm">Upload document</button>
          </form>
          <div class="small text-muted mt-1">Documents are stored privately and are only visible to administrators and the owner. Verify after checking the original.</div>
        </div></div>

        <div class="bento">
          <div class="c-6">
            <div class="tile"><div class="tile__head">Account actions</div><div class="tile__body">
              <form method="post" action="/Admin/Person.aspx?id=<%= UserId %>" class="mb-2"><%= CsrfField() %><input type="hidden" name="op" value="resetpwd"/>
                <button class="btn btn-outline-sms btn-sm" data-confirm="Email a password reset link to this person?">Reset password by email</button></form>
              <form method="post" action="/Admin/Person.aspx?id=<%= UserId %>" class="mb-2"><%= CsrfField() %><input type="hidden" name="op" value="unlock"/>
                <button class="btn btn-outline-sms btn-sm">Unlock account</button></form>
              <form method="post" action="/Admin/Person.aspx?id=<%= UserId %>" class="mb-2"><%= CsrfField() %><input type="hidden" name="op" value="forcesignout"/>
                <button class="btn btn-outline-bad btn-sm" data-confirm="Sign this person out of every device?">Force sign-out</button></form>
              <form method="post" action="/Admin/Person.aspx?id=<%= UserId %>" class="mb-2"><%= CsrfField() %><input type="hidden" name="op" value="toggle"/>
                <button class="btn btn-outline-sms btn-sm" data-confirm="Change the active state of this account?"><%= IsActive ? "Disable account" : "Enable account" %></button></form>
              <form method="post" action="/Admin/Person.aspx?id=<%= UserId %>" class="mb-2"><%= CsrfField() %><input type="hidden" name="op" value="role"/>
                <div class="d-flex gap-2 align-items-end"><select class="form-select form-select-sm" name="role"><option>Student</option><option>Faculty</option><option>Admin</option></select>
                <button class="btn btn-outline-bad btn-sm" data-confirm="Change this person's role? Their data stays, but their workspace changes.">Change role</button></div></form>
              <%= DeleteFormHtml %>
            </div></div>
          </div>
          <div class="c-6">
            <div class="tile"><div class="tile__head">Completeness</div><div class="tile__body">
              <div class="profile-completeness"><div class="d-flex justify-content-between small mb-1"><span><%= E(RoleName) %> profile</span><b><%= Completeness %>%</b></div>
                <div class="bar"><span style="width:<%= Completeness %>%"></span></div></div>
              <div class="small text-muted mt-2"><%= CompleteHint %></div>
            </div></div>
          </div>
        </div>

        <div class="tile mt-3" id="activity"><div class="tile__head d-flex justify-content-between"><span>Activity for this person</span>
          <a class="small" href="/Admin/Audit.aspx?target=<%= UserId %>">Open full log</a></div>
          <div class="tile__body sms-table-wrap table-wrap"><%= ActivityHtml %></div></div>

        <div class="tile mt-3" id="requests"><div class="tile__head">Requests</div>
          <div class="tile__body sms-table-wrap table-wrap"><%= RequestsHtml %></div></div>
      </div>
    </div>
    </div>
</asp:Content>
