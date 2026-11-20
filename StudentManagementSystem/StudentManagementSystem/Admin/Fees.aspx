<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Fees.aspx.cs" Inherits="StudentManagementSystem.Admin.Fees" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Fee heads - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>Fee structure</h1>
            <p>Course-wise fee heads that drive student dues and payment receipts. Deactivating a head keeps historical payments intact.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-sms btn-sm" href="/Admin/FeeStructure.aspx"><i class="bi bi-diagram-3"></i> Fee structures</a>
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=fees&amp;format=pdf<%= ExportQuery %>"><i class="bi bi-file-pdf"></i> PDF</a>
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=fees&amp;format=xlsx<%= ExportQuery %>"><i class="bi bi-file-earmark-excel"></i> Excel</a>
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=fees&amp;format=csv<%= ExportQuery %>"><i class="bi bi-filetype-csv"></i> CSV</a>
        </div>
    </div>

    <%= SummaryCards %>

    <form method="get" action="/Admin/Fees.aspx" class="filter-grid tile mb-3">
        <div class="tile__body">
            <div class="bento">
                <div class="c-5"><label class="form-label small">Course</label><select name="courseId" class="form-select form-select-sm"><%= FilterCourseOptions %></select></div>
                <div class="c-3"><label class="form-label small">Status</label><select name="status" class="form-select form-select-sm">
                    <option value="">All</option>
                    <option value="active"<%= Status == "active" ? " selected" : "" %>>Active only</option>
                    <option value="inactive"<%= Status == "inactive" ? " selected" : "" %>>Inactive only</option>
                </select></div>
                <div class="c-4 d-flex gap-2"><button class="btn btn-sms btn-sm">Apply</button><a class="btn btn-outline-sms btn-sm" href="/Admin/Fees.aspx">Reset</a></div>
            </div>
        </div>
    </form>

    <div class="tile mb-3">
        <div class="tile__head d-flex justify-content-between"><span>Fee heads per course</span><span class="small text-muted">Amounts in <%= E(Currency) %></span></div>
        <div class="tile__body sms-table-wrap table-wrap"><%= FeeTable %></div>
    </div>

    <div class="tile">
        <div class="tile__head">Create fee head</div>
        <div class="tile__body">
            <form method="post" action="/Admin/Fees.aspx" class="form-grid">
                <%= CsrfField() %><input type="hidden" name="op" value="newfee"/>
                <div class="c-4"><label class="form-label small">Course</label><select name="courseId" class="form-select form-select-sm"><%= CourseOptions %></select></div>
                <div class="c-2"><label class="form-label small">Code</label><input name="code" required class="form-control form-control-sm" placeholder="TUITION" maxlength="20"/></div>
                <div class="c-3"><label class="form-label small">Name</label><input name="name" required class="form-control form-control-sm" placeholder="Tuition fee" maxlength="100"/></div>
                <div class="c-2"><label class="form-label small">Amount</label><input name="amount" type="number" step="0.01" min="0.01" required class="form-control form-control-sm"/></div>
                <div class="c-1 d-flex align-items-end"><button class="btn btn-sms btn-sm w-100">Save</button></div>
            </form>
            <p class="settings-note mt-2 mb-0">To collect a payment, use <a href="/Admin/Payments.aspx">Payments</a>. Fees changed here apply to new dues, not to captured transactions.</p>
        </div>
    </div>
    </div>
</asp:Content>



