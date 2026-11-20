<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Index.aspx.cs" Inherits="StudentManagementSystem.Faculty.Index" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Dashboard - Faculty</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <div class="kicker">Faculty workspace</div>
            <h1>Teaching overview</h1>
            <p class="page-sub">Your assignments, attendance sessions and student outcomes in one place.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=attendance&amp;format=pdf">Attendance PDF</a>
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=results&amp;format=pdf">Results PDF</a>
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=students&amp;format=csv">Students CSV</a>
        </div>
    </div>
    <%= TaskHtml %>

    <%= StatCards %>

    <div class="bento">
        <div class="c-7">
            <div class="tile"><div class="tile__head">My teaching assignments</div><div class="tile__body sms-table-wrap table-wrap"><%= AssignmentTable %></div></div>
        </div>
        <div class="c-5">
            <div class="tile"><div class="tile__head">Today's attendance sessions</div><div class="tile__body sms-table-wrap table-wrap"><%= TodaySessions %></div></div>
        </div>
    </div>

    <div class="tile mt-3"><div class="tile__head">Announcements</div><div class="tile__body sms-table-wrap table-wrap"><%= Announcements %></div></div>
    </div>
</asp:Content>



