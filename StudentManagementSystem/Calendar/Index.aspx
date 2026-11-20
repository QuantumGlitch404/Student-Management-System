<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Index.aspx.cs" Inherits="StudentManagementSystem.Campus.CalendarPage" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Academic calendar</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>

    <div class="page-head">
        <div>
            <h1>Academic calendar</h1>
            <p><%= Intro %></p>
        </div>
        <div class="page-actions">
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/CalendarIcs.ashx"><i class="bi bi-download"></i> Download ICS</a>
            <a class="btn btn-outline-sms btn-sm" href="/Timetable/Index.aspx"><i class="bi bi-table"></i> Timetable</a>
        </div>
    </div>

    <div class="bento">
        <div class="c-8">
            <div class="tile">
                <div class="tile__head">Upcoming events</div>
                <div class="tile__body"><%= EventList %></div>
            </div>
        </div>
        <div class="c-4">
            <div class="tile">
                <div class="tile__head">Calendar at a glance</div>
                <div class="tile__body"><%= StatsHtml %></div>
            </div>
            <div class="tile mt-3">
                <div class="tile__head">How this calendar works</div>
                <div class="tile__body small text-muted">
                    <p class="mb-2">Dates are shown in your saved timezone. Event instants are stored in UTC, so exports and reminders stay consistent across devices.</p>
                    <p class="mb-0">The ICS download includes every event your role is allowed to see. Add it to Outlook, Apple Calendar or Google Calendar to keep it in sync.</p>
                </div>
            </div>
        </div>
    </div>

    <%= AdminTools %>
    </div>
</asp:Content>
