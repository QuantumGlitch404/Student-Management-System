<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Academics.aspx.cs" Inherits="StudentManagementSystem.Admin.Academics" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Academics - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>Academic structure</h1>
            <p>Courses, subjects, semester mapping, faculty allocation and student enrollment. These links feed attendance, exams and fees.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=courses&amp;format=pdf"><i class="bi bi-file-pdf"></i> Courses PDF</a>
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=enrollments&amp;format=xlsx"><i class="bi bi-file-earmark-excel"></i> Enrollments</a>
        </div>
    </div>

    <%= SummaryCards %>

    <div class="bento">
        <div class="c-6">
            <div class="tile mb-3"><div class="tile__head">Courses</div><div class="tile__body sms-table-wrap table-wrap"><%= CourseTable %></div></div>
            <div class="tile"><div class="tile__head">Create course</div><div class="tile__body">
                <form method="post" action="/Admin/Academics.aspx" class="form-grid">
                    <%= CsrfField() %><input type="hidden" name="op" value="newcourse"/>
                    <div class="c-4"><label class="form-label small">Code</label><input name="code" required class="form-control form-control-sm" maxlength="20"/></div>
                    <div class="c-8"><label class="form-label small">Name</label><input name="name" required class="form-control form-control-sm" maxlength="120"/></div>
                    <div class="c-12"><label class="form-label small">Description</label><input name="desc" class="form-control form-control-sm" maxlength="500"/></div>
                    <div class="c-6"><label class="form-label small">Semesters</label><input name="semesters" type="number" min="1" max="12" value="6" class="form-control form-control-sm"/></div>
                    <div class="c-6"><label class="form-label small">Credits</label><input name="credits" type="number" min="1" max="500" value="120" class="form-control form-control-sm"/></div>
                    <div class="c-12 mt-2"><button class="btn btn-sms btn-sm">Save course</button></div>
                </form>
            </div></div>
        </div>

        <div class="c-6">
            <div class="tile mb-3"><div class="tile__head">Subjects</div><div class="tile__body sms-table-wrap table-wrap"><%= SubjectTable %></div></div>
            <div class="tile"><div class="tile__head">Create subject</div><div class="tile__body">
                <form method="post" action="/Admin/Academics.aspx" class="form-grid">
                    <%= CsrfField() %><input type="hidden" name="op" value="newsubject"/>
                    <div class="c-4"><label class="form-label small">Code</label><input name="code" required class="form-control form-control-sm" maxlength="20"/></div>
                    <div class="c-8"><label class="form-label small">Name</label><input name="name" required class="form-control form-control-sm" maxlength="120"/></div>
                    <div class="c-4"><label class="form-label small">Max marks</label><input name="maxMarks" type="number" min="1" max="1000" value="100" class="form-control form-control-sm"/></div>
                    <div class="c-4"><label class="form-label small">Pass %</label><input name="passPct" type="number" min="0" max="100" value="33" class="form-control form-control-sm"/></div>
                    <div class="c-4"><label class="form-label small">Hours/week</label><input name="hours" type="number" min="0" max="40" class="form-control form-control-sm"/></div>
                    <div class="c-12 mt-2"><button class="btn btn-sms btn-sm">Save subject</button></div>
                </form>
            </div></div>
        </div>
    </div>

    <div class="tile mt-3"><div class="tile__head d-flex justify-content-between"><span>Subjects in course</span><span class="small text-muted">Course-subject map drives attendance, marks and fees</span></div>
        <div class="tile__body">
            <form method="get" action="/Admin/Academics.aspx" class="srch-bar mb-2">
                <select name="course" class="form-select form-select-sm" style="max-width:280px"><%= CourseOptions(true) %></select>
                <button class="btn btn-sms btn-sm">Load mapped subjects</button>
            </form>

            <div class="sms-table-wrap table-wrap"><%= MapTable ?? "" %></div>

            <form method="post" action="/Admin/Academics.aspx" class="srch-bar mt-3">
                <%= CsrfField() %><input type="hidden" name="op" value="mapsubject"/>
                <input type="hidden" name="courseId" value="<%= Attr(CourseIdNow) %>"/>
                <select name="subjectId" class="form-select form-select-sm" style="max-width:260px"><%= SubjectOptions %></select>
                <select name="semester" class="form-select form-select-sm" style="max-width:150px"><%= SemesterOptions %></select>
                <button class="btn btn-sms btn-sm">Map subject to semester</button>
            </form>
        </div>
    </div>

    <div class="tile mt-3"><div class="tile__head">Faculty assignment</div><div class="tile__body">
        <form method="post" action="/Admin/Academics.aspx" class="form-grid">
            <%= CsrfField() %><input type="hidden" name="op" value="assignfaculty"/>
            <div class="c-3"><label class="form-label small">Faculty</label><select name="facultyId" class="form-select form-select-sm"><%= FacultyOptions %></select></div>
            <div class="c-3"><label class="form-label small">Course</label><select name="courseId" class="form-select form-select-sm"><%= CourseOptions(false) %></select></div>
            <div class="c-2"><label class="form-label small">Subject</label><select name="subjectId" class="form-select form-select-sm"><%= SubjectOptions %></select></div>
            <div class="c-2"><label class="form-label small">Session</label><select name="sessionId" class="form-select form-select-sm"><%= SessionOptions %></select></div>
            <div class="c-1"><label class="form-label small">Sem</label><select name="semester" class="form-select form-select-sm"><%= SemesterOptions %></select></div>
            <div class="c-1 d-flex align-items-end"><button class="btn btn-sms btn-sm w-100">Assign</button></div>
        </form>
        <p class="settings-note mt-2 mb-0">Assigning an existing combination updates its semester; duplicates are ignored by the repository.</p>
    </div></div>

    <div class="tile mt-3"><div class="tile__head">Enrollments</div><div class="tile__body">
        <div class="sms-table-wrap table-wrap"><%= EnrollmentTable %></div>
        <form method="post" action="/Admin/Academics.aspx" class="srch-bar mt-3">
            <%= CsrfField() %><input type="hidden" name="op" value="enroll"/>
            <input name="term" class="form-control form-control-sm" style="max-width:240px" placeholder="Student (name or enrolment no. or email)"/>
            <select name="courseId" class="form-select form-select-sm" style="max-width:280px"><%= CourseOptions(false) %></select>
            <select name="sessionId" class="form-select form-select-sm" style="max-width:200px"><%= SessionOptions %></select>
            <button class="btn btn-sms btn-sm">Enroll student in course</button>
        </form>
    </div></div>
    </div>
</asp:Content>



