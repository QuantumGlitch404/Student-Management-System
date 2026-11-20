<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Exams.aspx.cs" Inherits="StudentManagementSystem.Admin.Exams" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Exams and results - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>Examinations &amp; results</h1>
            <p>Schedule assessments, watch marks arrive, publish calculated results, and inspect grade distribution before release.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=results&amp;format=pdf<%= ExportQuery %>"><i class="bi bi-file-pdf"></i> Results PDF</a>
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=results&amp;format=csv<%= ExportQuery %>"><i class="bi bi-filetype-csv"></i> Results CSV</a>
            <a class="btn btn-outline-sms btn-sm" href="/Faculty/Marks.aspx"><i class="bi bi-pencil-square"></i> Marks entry</a>
        </div>
    </div>

    <%= SummaryCards %>

    <form method="get" action="/Admin/Exams.aspx" class="filter-grid tile mb-3">
        <div class="tile__body">
            <div class="bento">
                <div class="c-4"><label class="form-label small">Course</label><select name="courseId" class="form-select form-select-sm"><%= CourseFilterOptions %></select></div>
                <div class="c-3"><label class="form-label small">Publication</label><select name="status" class="form-select form-select-sm">
                    <option value="">All</option>
                    <option value="published"<%= Status == "published" ? " selected" : "" %>>Published</option>
                    <option value="draft"<%= Status == "draft" ? " selected" : "" %>>Draft</option>
                </select></div>
                <div class="c-3"><label class="form-label small">Search</label><input name="q" value="<%= E(Q) %>" class="form-control form-control-sm" maxlength="60" placeholder="Exam or subject"/></div>
                <div class="c-2 d-flex gap-2"><button class="btn btn-sms btn-sm w-100">Apply</button><a class="btn btn-outline-sms btn-sm" href="/Admin/Exams.aspx">Reset</a></div>
            </div>
        </div>
    </form>

    <div class="tile mb-3">
        <div class="tile__head d-flex justify-content-between"><span>Exam catalogue</span><span class="small text-muted">Publishing calculates grades with the stored procedure and releases them to students.</span></div>
        <div class="tile__body sms-table-wrap table-wrap"><%= ExamTable %></div>
    </div>

    <div class="tile">
        <div class="tile__head">Create exam</div>
        <div class="tile__body">
            <form method="post" action="/Admin/Exams.aspx" class="form-grid">
                <%= CsrfField() %><input type="hidden" name="op" value="newexam"/>
                <div class="c-3"><label class="form-label small">Exam name</label><input name="name" required class="form-control form-control-sm" placeholder="Mid Term" maxlength="60"/></div>
                <div class="c-3"><label class="form-label small">Course</label><select name="courseId" class="form-select form-select-sm"><%= CourseOptions %></select></div>
                <div class="c-3"><label class="form-label small">Subject</label><select name="subjectId" class="form-select form-select-sm"><%= SubjectOptions %></select></div>
                <div class="c-2"><label class="form-label small">Type</label><select name="weightType" class="form-select form-select-sm">
                    <option value="1">Regular test</option><option value="2">Final exam</option><option value="3">Practical</option>
                </select></div>
                <div class="c-1"><label class="form-label small">Max</label><input name="maxMarks" type="number" min="1" max="1000" value="100" class="form-control form-control-sm"/></div>
                <div class="c-2"><label class="form-label small">Pass %</label><input name="passPct" type="number" min="0" max="100" value="33" class="form-control form-control-sm"/></div>
                <div class="c-3"><label class="form-label small">Date</label><input name="examDate" type="date" class="form-control form-control-sm"/></div>
                <div class="c-7 d-flex align-items-end"><button class="btn btn-sms btn-sm"><i class="bi bi-plus-lg"></i> Create exam</button></div>
            </form>
        </div>
    </div>

    <% if (SheetExamId > 0) { %>
    <div class="tile mt-3">
        <div class="tile__head d-flex justify-content-between align-items-center">
            <span>Result sheet &middot; exam <%= SheetExamId %></span>
            <span class="d-flex gap-2">
                <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=results&amp;format=pdf"><i class="bi bi-file-pdf"></i> PDF</a>
                <a class="btn btn-outline-sms btn-sm" href="/Admin/Exams.aspx">Close</a>
            </span>
        </div>
        <div class="tile__body sms-table-wrap table-wrap"><%= ResultSheet %></div>
    </div>
    <% } %>
    </div>
</asp:Content>



