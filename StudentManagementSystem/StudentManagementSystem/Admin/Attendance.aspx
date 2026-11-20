<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Attendance.aspx.cs" Inherits="StudentManagementSystem.Admin.Attendance" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Attendance - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>Attendance overview</h1>
            <p>Every captured session, the present ratio and the students below the <%= Threshold %>% shortage line. Drill into a session for the roll call.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=attendance&amp;format=pdf<%= ExportQuery %>"><i class="bi bi-file-pdf"></i> PDF</a>
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=attendance&amp;format=csv<%= ExportQuery %>"><i class="bi bi-filetype-csv"></i> CSV</a>
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=attendance&amp;format=xlsx<%= ExportQuery %>"><i class="bi bi-file-earmark-excel"></i> Excel</a>
        </div>
    </div>

    <form method="get" action="/Admin/Attendance.aspx" class="filter-grid tile mb-3">
        <div class="tile__body">
            <div class="bento">
                <div class="c-3"><label class="form-label small">Course</label><select name="courseId" class="form-select form-select-sm"><%= CourseOptions %></select></div>
                <div class="c-3"><label class="form-label small">Faculty</label><select name="facultyId" class="form-select form-select-sm"><%= FacultyOptions %></select></div>
                <div class="c-2"><label class="form-label small">From</label><input type="date" name="from" value="<%= E(FromDate) %>" class="form-control form-control-sm"/></div>
                <div class="c-2"><label class="form-label small">To</label><input type="date" name="to" value="<%= E(ToDate) %>" class="form-control form-control-sm"/></div>
                <div class="c-2 d-flex gap-2"><button class="btn btn-sms btn-sm w-100">Apply</button><a class="btn btn-outline-sms btn-sm" href="/Admin/Attendance.aspx">Reset</a></div>
            </div>
        </div>
    </form>

    <%= SummaryCards %>

    <div class="bento">
        <div class="c-8">
            <div class="tile">
                <div class="tile__head d-flex justify-content-between align-items-center"><span>Sessions</span><span class="small text-muted mono"><%= SessionList == null ? 0 : SessionCount %> shown</span></div>
                <div class="tile__body sms-table-wrap table-wrap"><%= SessionList %></div>
            </div>
        </div>
        <div class="c-4">
            <div class="tile">
                <div class="tile__head d-flex justify-content-between"><span>Attendance shortage</span><span class="badge badge-warn">below <%= Threshold %>%</span></div>
                <div class="tile__body"><%= ShortageList %></div>
            </div>
        </div>
    </div>

    <% if (SelectedId > 0) { %>
    <div class="tile mt-3">
        <div class="tile__head d-flex justify-content-between align-items-center"><span>Session detail &middot; <%= E(SelectedInfo) %></span><a class="btn btn-outline-sms btn-sm" href="/Admin/Attendance.aspx<%= FilterQuery %>">Back to overview</a></div>
        <div class="tile__body sms-table-wrap table-wrap"><%= DetailList %></div>
    </div>
    <% } %>
    </div>
</asp:Content>


