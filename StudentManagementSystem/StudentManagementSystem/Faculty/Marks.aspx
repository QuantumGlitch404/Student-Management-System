<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Marks.aspx.cs" Inherits="StudentManagementSystem.Faculty.Marks" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Marks and exams - Faculty</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>Marks &amp; assessments</h1>
            <p>Schedule an assessment for your assignment, enter subject marks, and let the examination office publish the calculated grades.</p>
        </div>
        <div class="page-actions"><a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=results&amp;format=pdf"><i class="bi bi-file-pdf"></i> Results PDF</a></div>
    </div>

    <div class="tile mb-3"><div class="tile__head">My exams (by assignment)</div><div class="tile__body sms-table-wrap table-wrap"><%= ExamTable %></div></div>

    <div class="tile mb-3"><div class="tile__head">Create exam for an assignment</div><div class="tile__body">
        <form method="post" action="/Faculty/Marks.aspx" class="srch-bar">
            <%= CsrfField() %><input type="hidden" name="op" value="newexam"/>
            <select name="assignmentId" required class="form-select form-select-sm" style="max-width:340px"><%= AssignmentOptions %></select>
            <input name="name" required class="form-control form-control-sm" style="max-width:210px" placeholder="Exam name" maxlength="60"/>
            <input name="maxMarks" type="number" min="1" max="1000" value="100" class="form-control form-control-sm" style="max-width:120px" title="Max marks"/>
            <input name="passPct" type="number" min="0" max="100" value="33" class="form-control form-control-sm" style="max-width:120px" title="Pass percent"/>
            <input name="examDate" type="date" class="form-control form-control-sm" style="max-width:170px"/>
            <button class="btn btn-sms btn-sm">Create exam</button>
        </form>
    </div></div>

    <% if (OpenExamId > 0 && MarksTable != null) { %>
    <div class="tile mb-3"><div class="tile__head d-flex justify-content-between"><span>Enter marks - <%= E(OpenInfo) %></span>
        <a class="imbtn" href="/Faculty/Marks.aspx">Close</a></div>
    <div class="tile__body">
        <form method="post" action="/Faculty/Marks.aspx" data-confirm="Save the marks entered?">
            <%= CsrfField() %><input type="hidden" name="op" value="save"/><input type="hidden" name="examId" value="<%= OpenExamId %>"/>
            <div class="sms-table-wrap table-wrap"><%= MarksTable %></div>
            <button class="btn btn-sms btn-sm mt-2">Save marks</button>
        </form>
    </div></div>
    <% } %>

    <% if (OpenExamId > 0 && SheetTable != null) { %>
    <div class="tile"><div class="tile__head">Result sheet (after publish)</div><div class="tile__body sms-table-wrap table-wrap"><%= SheetTable %></div></div>
    <% } %>
    </div>
</asp:Content>


