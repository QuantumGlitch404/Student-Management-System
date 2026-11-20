<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Attendance.aspx.cs" Inherits="StudentManagementSystem.Faculty.Attendance" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Attendance - Faculty</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>Take attendance</h1>
            <p>Create a session for one of your assignments, mark the roster and save. Saved sessions appear below for review.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=attendance&amp;format=pdf"><i class="bi bi-file-pdf"></i> My sessions PDF</a>
        </div>
    </div>

    <div class="tile mb-3"><div class="tile__head">New attendance session</div><div class="tile__body">
        <form method="post" action="/Faculty/Attendance.aspx" class="srch-bar">
            <%= CsrfField() %><input type="hidden" name="op" value="newsession"/>
            <select name="assignmentId" required class="form-select form-select-sm" style="max-width:340px">
                <%= AssignmentOptions %>
            </select>
            <input name="sessionDate" type="date" required value="<%= Attr(Today) %>" class="form-control form-control-sm" style="max-width:180px"/>
            <select name="period" class="form-select form-select-sm" style="max-width:130px">
                <% for (int i = 1; i <= 8; i++) { %><option value="<%= i %>">Period <%= i %></option><% } %>
            </select>
            <button class="btn btn-sms btn-sm">Create + open roster</button>
        </form>
    </div></div>

    <% if (OpenSessionId > 0 && RosterTable != null) { %>
    <div class="tile mb-3"><div class="tile__head d-flex justify-content-between">
        <span>Roster - <%= E(OpenInfo) %></span><a class="imbtn" href="/Faculty/Attendance.aspx">Close</a>
    </div><div class="tile__body">
        <form method="post" action="/Faculty/Attendance.aspx" data-confirm="Save attendance for this session?">
            <%= CsrfField() %><input type="hidden" name="op" value="save"/><input type="hidden" name="attnSessionId" value="<%= OpenSessionId %>"/>
            <div class="sms-table-wrap table-wrap"><%= RosterTable %></div>
            <button class="btn btn-sms btn-sm mt-2">Save attendance</button>
        </form>
    </div></div>
    <% } %>

    <div class="tile"><div class="tile__head">My sessions</div><div class="tile__body sms-table-wrap table-wrap"><%= SessionList %></div></div>
    </div>
</asp:Content>


