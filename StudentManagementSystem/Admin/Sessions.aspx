<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Sessions.aspx.cs" Inherits="StudentManagementSystem.Admin.Sessions" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Sessions - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>Academic sessions</h1>
            <p>Define each academic year and its semesters. Sessions scope enrollment, faculty allocation and reports.</p>
        </div>
    </div>

    <div class="bento">
        <div class="c-7">
            <div class="tile"><div class="tile__head">Academic sessions</div><div class="tile__body sms-table-wrap table-wrap"><%= SessionTable %></div></div>
        </div>
        <div class="c-5">
            <div class="tile mb-3"><div class="tile__head">New session</div><div class="tile__body">
                <form method="post" action="/Admin/Sessions.aspx" class="form-grid">
                    <%= CsrfField() %><input type="hidden" name="op" value="newsession"/>
                    <div class="c-6"><label class="form-label small">Name (e.g. 2026-2027)</label><input name="name" required class="form-control form-control-sm" maxlength="20"/></div>
                    <div class="c-6"></div>
                    <div class="c-6"><label class="form-label small">Start date</label><input name="start" type="date" required class="form-control form-control-sm"/></div>
                    <div class="c-6"><label class="form-label small">End date</label><input name="end" type="date" required class="form-control form-control-sm"/></div>
                    <div class="c-12 mt-2"><button class="btn btn-sms btn-sm">Save session</button></div>
                </form>
            </div></div>

            <div class="tile"><div class="tile__head">Semesters of the selected session</div><div class="tile__body">
                <form method="get" action="/Admin/Sessions.aspx" class="srch-bar mb-2">
                    <select name="session" class="form-select form-select-sm" style="max-width:240px"><%= SessionOptions %></select>
                    <button class="btn btn-sms btn-sm">Show semesters</button>
                </form>
                <div class="sms-table-wrap table-wrap"><%= SemesterTable ?? "" %></div>
                <% if (SemesterSessionId > 0) { %>
                <form method="post" action="/Admin/Sessions.aspx" class="srch-bar mt-2">
                    <%= CsrfField() %><input type="hidden" name="op" value="upsertsemester"/>
                    <input type="hidden" name="sessionId" value="<%= SemesterSessionId %>"/>
                    <input name="num" type="number" min="1" max="12" required class="form-control form-control-sm" style="max-width:120px" placeholder="Sem #"/>
                    <input name="name" class="form-control form-control-sm" style="max-width:180px" placeholder="Name (opt. Sem 1)" maxlength="30"/>
                    <input name="start" type="date" class="form-control form-control-sm" style="max-width:170px"/>
                    <input name="end" type="date" class="form-control form-control-sm" style="max-width:170px"/>
                    <button class="btn btn-sms btn-sm">Save semester</button>
                </form>
                <% } %>
            </div></div>
        </div>
    </div>
    </div>
</asp:Content>




