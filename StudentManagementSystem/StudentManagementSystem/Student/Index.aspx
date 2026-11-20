<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Index.aspx.cs" Inherits="StudentManagementSystem.Student.Index" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Dashboard - Student</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <div class="kicker">Student workspace</div>
            <h1><%= Greeting %></h1>
            <p class="page-sub">Attendance, fees, results and campus announcements, up to date.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=marksheet&amp;format=pdf">Marksheet PDF</a>
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=myattendance&amp;format=pdf">Attendance PDF</a>
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=mypayments&amp;format=csv">Payments CSV</a>
        </div>
    </div>
    <%= TaskHtml %>

    <%= StatCards %>

    <div class="tile mt-3"><div class="tile__head">Upcoming examinations</div><div class="tile__body sms-table-wrap table-wrap"><%= Upcoming %></div></div>

    <div class="bento">
        <div class="c-6">
            <div class="tile"><div class="tile__head">Notifications</div><div class="tile__body sms-table-wrap table-wrap"><%= NoticeList %></div></div>
            <div class="tile mt-3"><div class="tile__head">My grades</div><div class="tile__body sms-table-wrap table-wrap"><%= Grades %></div></div>
        </div>
        <div class="c-6">
            <div class="tile"><div class="tile__head">Fees due</div><div class="tile__body sms-table-wrap table-wrap"><%= FeesDue %></div></div>
            <div class="tile mt-3"><div class="tile__head">Announcements</div><div class="tile__body sms-table-wrap table-wrap"><%= Announcements %></div></div>
        </div>
    </div>
    </div>
</asp:Content>



