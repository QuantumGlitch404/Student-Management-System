<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="OnlineTests.aspx.cs" Inherits="StudentManagementSystem.Admin.OnlineTests" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Online tests - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>Online tests</h1>
            <p>Build a subject question bank, assemble timed tests, publish them to a course, and review attempts with proctoring events.</p>
        </div>
    </div>

    <%= SummaryCards %>

    <div class="bento">
        <div class="c-12">
            <div class="tile">
                <div class="tile__head">Create an online test</div>
                <div class="tile__body">
                    <form method="post" action="/Admin/OnlineTests.aspx" class="form-grid">
                        <%= CsrfField() %><input type="hidden" name="op" value="newtest"/>
                        <div class="c-4"><label class="form-label small">Title</label><input name="title" required maxlength="160" class="form-control form-control-sm" placeholder="Unit 2 quiz"/></div>
                        <div class="c-3"><label class="form-label small">Course</label><select name="courseId" class="form-select form-select-sm"><%= CourseOptions %></select></div>
                        <div class="c-3"><label class="form-label small">Subject</label><select name="subjectId" class="form-select form-select-sm"><%= SubjectOptions %></select></div>
                        <div class="c-2"><label class="form-label small">Duration (min)</label><input name="duration" type="number" min="1" max="480" value="30" class="form-control form-control-sm"/></div>
                        <div class="c-2"><label class="form-label small">Attempts</label><input name="attempts" type="number" min="1" max="10" value="1" class="form-control form-control-sm"/></div>
                        <div class="c-2"><label class="form-label small">Pass %</label><input name="passPct" type="number" min="0" max="100" step="0.01" value="40" class="form-control form-control-sm"/></div>
                        <div class="c-2"><label class="form-label small">Warn limit</label><input name="warningLimit" type="number" min="0" max="10" value="1" class="form-control form-control-sm" title="Warnings before an automatic submit"/></div>
                        <div class="c-3"><label class="form-label small">Opens (local)</label><input name="opensAt" type="datetime-local" class="form-control form-control-sm"/></div>
                        <div class="c-3"><label class="form-label small">Closes (local)</label><input name="closesAt" type="datetime-local" class="form-control form-control-sm"/></div>
                        <div class="c-12"><label class="form-label small">Instructions</label><textarea name="instructions" rows="2" maxlength="2000" class="form-control form-control-sm" placeholder="One attempt. Keep this tab open."></textarea></div>
                        <div class="c-3 d-flex align-items-end gap-3">
                            <label class="form-check small"><input class="form-check-input" type="checkbox" name="shuffle" value="1" checked/> Shuffle questions</label>
                            <label class="form-check small"><input class="form-check-input" type="checkbox" name="proctor" value="1" checked/> Proctoring</label>
                        </div>
                        <div class="c-9 d-flex align-items-end"><button class="btn btn-sms btn-sm"><i class="bi bi-plus-lg"></i> Create test</button></div>
                    </form>
                </div>
            </div>
        </div>

        <div class="c-12">
            <div class="tile">
                <div class="tile__head d-flex justify-content-between"><span>Test catalogue</span><span class="small text-muted">Publish only after questions are attached.</span></div>
                <div class="tile__body sms-table-wrap table-wrap"><%= TestTable %></div>
            </div>
        </div>

        <%= TestDetailHtml %>

        <div class="c-12">
            <div class="tile">
                <div class="tile__head d-flex justify-content-between align-items-center"><span>Question bank</span>
                    <form method="get" action="/Admin/OnlineTests.aspx" class="d-flex gap-2 align-items-center flex-wrap">
                        <select name="subjectId" class="form-select form-select-sm" style="max-width:200px"><%= QuestionFilterOptions %></select>
                        <select name="categoryId" class="form-select form-select-sm" style="max-width:220px"><%= CategoryFilterOptions %></select>
                        <button class="btn btn-outline-sms btn-sm">Filter</button>
                    </form>
                </div>
                <div class="tile__body">
                    <div class="sms-table-wrap table-wrap"><%= QuestionTable %></div>
                    <hr class="my-3"/>
                    <form method="post" action="/Admin/OnlineTests.aspx" class="form-grid">
                        <%= CsrfField() %><input type="hidden" name="op" value="newquestion"/>
                        <div class="c-3"><label class="form-label small">Subject</label><select name="questionSubject" class="form-select form-select-sm"><%= QuestionSubjectOptions %></select></div>
                        <div class="c-2"><label class="form-label small">Difficulty</label><select name="difficulty" class="form-select form-select-sm"><option value="easy">Easy</option><option value="medium" selected>Medium</option><option value="hard">Hard</option></select></div>
                        <div class="c-7"><label class="form-label small">Question</label><input name="prompt" required maxlength="1000" class="form-control form-control-sm" placeholder="Which data structure uses FIFO?"/></div>
                        <div class="c-3"><label class="form-label small">Option A</label><input name="optionA" required maxlength="400" class="form-control form-control-sm"/></div>
                        <div class="c-3"><label class="form-label small">Option B</label><input name="optionB" required maxlength="400" class="form-control form-control-sm"/></div>
                        <div class="c-3"><label class="form-label small">Option C (optional)</label><input name="optionC" maxlength="400" class="form-control form-control-sm"/></div>
                        <div class="c-3"><label class="form-label small">Option D (optional)</label><input name="optionD" maxlength="400" class="form-control form-control-sm"/></div>
                        <div class="c-2"><label class="form-label small">Correct</label><select name="correct" class="form-select form-select-sm"><option>A</option><option>B</option><option>C</option><option>D</option></select></div>
                        <div class="c-8"><label class="form-label small">Explanation (optional)</label><input name="explanation" maxlength="1000" class="form-control form-control-sm"/></div>
                        <div class="c-2 d-flex align-items-end"><button class="btn btn-sms btn-sm w-100">Add question</button></div>
                    </form>
                    <details class="mt-3"><summary class="small"><b>Bulk import questions</b></summary>
                        <form method="post" action="/Admin/OnlineTests.aspx" class="mt-2">
                            <%= CsrfField() %><input type="hidden" name="op" value="bulkquestions"/>
                            <p class="small text-muted mb-1">One question per line: <code>SubjectCode|Prompt|A|B|C|D|Correct|Difficulty</code>. Correct is A-D; difficulty is easy, medium or hard.</p>
                            <textarea name="bulk" rows="4" class="form-control form-control-sm" placeholder="CS101|Which structure is FIFO?|Stack|Queue|Tree|Graph|B|easy"></textarea>
                            <button class="btn btn-outline-sms btn-sm mt-2">Import questions</button>
                        </form>
                    </details>
                </div>
            </div>
        </div>
    </div>
    </div>
</asp:Content>
