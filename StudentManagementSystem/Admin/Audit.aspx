<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Audit.aspx.cs" Inherits="StudentManagementSystem.Admin.Audit" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Audit log - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <div class="kicker">Governance</div>
            <h1>Audit trail</h1>
            <p class="page-sub">Every security-relevant action, who performed it, from where and when. Append-only.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-soft" href="<%= ExportUrl("pdf") %>">Export PDF</a>
            <a class="btn btn-soft" href="<%= ExportUrl("csv") %>">CSV</a>
            <a class="btn btn-soft" href="<%= ExportUrl("xlsx") %>">Excel</a>
            <a class="btn btn-soft" href="<%= ExportUrl("json") %>">JSON</a>
        </div>
    </div>

    <div class="tile"><div class="tile__head d-flex justify-content-between align-items-center">
        <span>Activity log <span class="mono small text-muted"><%= TotalShown %></span></span>
        <span class="small text-muted">Retention: permanent. Entries cannot be edited or deleted from the UI.</span>
    </div>
    <div class="tile__body">
        <form method="get" action="/Admin/Audit.aspx" class="filter-grid mb-3">
            <div><label class="form-label small">Search</label><input name="q" value="<%= Attr(Q) %>" class="form-control form-control-sm" placeholder="User, detail, IP..."/></div>
            <div><label class="form-label small">User</label><input name="user" value="<%= Attr(UserFilter) %>" class="form-control form-control-sm" placeholder="Name contains"/></div>
            <div><label class="form-label small">Role</label>
                <select name="role" class="form-select form-select-sm">
                    <option value="">All roles</option>
                    <option value="Admin"<%= Sel(RoleFilter, "Admin") %>>Admin</option>
                    <option value="Faculty"<%= Sel(RoleFilter, "Faculty") %>>Faculty</option>
                    <option value="Student"<%= Sel(RoleFilter, "Student") %>>Student</option>
                </select></div>
            <div><label class="form-label small">Action</label><input name="action" value="<%= Attr(ActionFilter) %>" class="form-control form-control-sm" placeholder="e.g. Login, Export"/></div>
            <div><label class="form-label small">Entity</label>
                <select name="entity" class="form-select form-select-sm">
                    <option value="">Any entity</option><%= EntityOptions %>
                </select></div>
            <div><label class="form-label small">Category</label>
                <select name="category" class="form-select form-select-sm">
                    <option value="">Any category</option>
                    <%= CategoryOptions %>
                </select></div>
            <div><label class="form-label small">IP address</label><input name="ip" value="<%= Attr(IpFilter) %>" class="form-control form-control-sm" placeholder="e.g. 127.0.0.1"/></div>
            <div><label class="form-label small">From</label><input type="date" name="from" value="<%= Attr(From) %>" class="form-control form-control-sm"/></div>
            <div><label class="form-label small">To</label><input type="date" name="to" value="<%= Attr(To) %>" class="form-control form-control-sm"/></div>
            <div class="filter-actions"><button class="btn btn-sms btn-sm">Apply filters</button><a class="btn btn-link btn-sm" href="/Admin/Audit.aspx">Reset</a></div>
        </form>
        <div class="sms-table-wrap table-wrap"><%= Table %></div>
    </div></div>
    </div>
</asp:Content>


