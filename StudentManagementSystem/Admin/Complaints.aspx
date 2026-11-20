<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Complaints.aspx.cs" Inherits="StudentManagementSystem.Admin.Complaints" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Complaints - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <div class="kicker">Service desk</div>
            <h1>Complaint tickets</h1>
            <p class="page-sub">Prioritise, assign, reply and resolve. Every status change is written to the ticket timeline.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=complaints&amp;format=pdf<%= FilterQuery %>">Export PDF</a>
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=complaints&amp;format=xlsx<%= FilterQuery %>">Excel</a>
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=complaints&amp;format=csv<%= FilterQuery %>">CSV</a>
        </div>
    </div>

    <%= SummaryCards %>

    <% if (SelectedId > 0) { %>
    <div class="tile mb-3"><div class="tile__head d-flex justify-content-between"><span><%= E(DetailHead) %></span>
        <a class="imbtn" href="/Admin/Complaints.aspx">Back to list</a></div>
        <div class="tile__body"><%= DetailBody %></div></div>
    <div class="bento">
        <div class="c-7"><div class="tile"><div class="tile__head">Reply to student</div><div class="tile__body"><%= ResponseForm %></div></div></div>
        <div class="c-5"><div class="tile"><div class="tile__head">Workflow</div><div class="tile__body"><%= WorkflowForm %></div></div></div>
    </div>
    <% } %>

    <div class="tile mb-3"><div class="tile__head">Ticket queue</div>
        <div class="tile__body">
            <form method="get" action="/Admin/Complaints.aspx" class="filter-grid mb-3">
                <div><label class="form-label small">Search</label><input name="q" value="<%= Attr(Q) %>" class="form-control form-control-sm" placeholder="Ticket, subject, text"/></div>
                <div><label class="form-label small">Status</label><select name="status" class="form-select form-select-sm"><%= StatusOptions %></select></div>
                <div><label class="form-label small">Priority</label><select name="priority" class="form-select form-select-sm"><%= PriorityOptions %></select></div>
                <div><label class="form-label small">Category</label><select name="category" class="form-select form-select-sm"><option value="">All categories</option><%= CategoryOptions %></select></div>
                <div class="filter-actions"><button class="btn btn-sms btn-sm">Filter</button><a class="btn btn-link btn-sm" href="/Admin/Complaints.aspx">Reset</a></div>
            </form>
            <div class="sms-table-wrap table-wrap"><%= ComplaintTable %></div>
        </div></div>
    </div>
</asp:Content>


