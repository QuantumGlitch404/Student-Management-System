<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Attendance.aspx.cs" Inherits="StudentManagementSystem.Student.Attendance" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">My attendance - Student</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>My attendance</h1>
            <p>Present ratio by subject, plus every marked session. Leave and authorised absence count as attended; shortage is shown against the college threshold.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=myattendance&amp;format=pdf"><i class="bi bi-file-pdf"></i> PDF</a>
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=myattendance&amp;format=csv"><i class="bi bi-filetype-csv"></i> CSV</a>
        </div>
    </div>
    <div class="tile mb-3"><div class="tile__head">Attendance by subject</div><div class="tile__body sms-table-wrap table-wrap"><%= SubjectTable %></div></div>
    <div class="tile"><div class="tile__head">Marked sessions</div><div class="tile__body sms-table-wrap table-wrap"><%= DetailTable %></div></div>
    </div>
</asp:Content>


