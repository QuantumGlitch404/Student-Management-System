<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Settings.aspx.cs" Inherits="StudentManagementSystem.Account.Settings" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Settings</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server">
    <script src="/Content/js/settings.js"></script>
</asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>Settings</h1>
            <p>Appearance, accessibility, notifications, the offline assistant, academic defaults and your account. Everything here is saved to your profile.</p>
        </div>
    </div>
    <div class="settings-shell settings-layout">
        <nav class="settings-nav" aria-label="Settings sections">
            <div class="list-group">
                <a class="list-group-item<%= TabClass("appearance") %>" href="/Account/Settings.aspx?tab=appearance"><i class="bi bi-palette"></i> Appearance</a>
                <a class="list-group-item<%= TabClass("accessibility") %>" href="/Account/Settings.aspx?tab=accessibility"><i class="bi bi-universal-access"></i> Accessibility</a>
                <a class="list-group-item<%= TabClass("notifications") %>" href="/Account/Settings.aspx?tab=notifications"><i class="bi bi-bell"></i> Notifications</a>
                <a class="list-group-item<%= TabClass("reminders") %>" href="/Account/Settings.aspx?tab=reminders"><i class="bi bi-alarm"></i> Reminders</a>
                <a class="list-group-item<%= TabClass("ai") %>" href="/Account/Settings.aspx?tab=ai"><i class="bi bi-stars"></i> AI assistant</a>
                <a class="list-group-item<%= TabClass("academic") %>" href="/Account/Settings.aspx?tab=academic"><i class="bi bi-mortarboard"></i> Academic &amp; regional</a>
                <a class="list-group-item<%= TabClass("account") %>" href="/Account/Settings.aspx?tab=account"><i class="bi bi-person-gear"></i> Account</a>
                <a class="list-group-item<%= TabClass("privacy") %>" href="/Account/Settings.aspx?tab=privacy"><i class="bi bi-shield-lock"></i> Privacy &amp; security</a>
                <a class="list-group-item<%= TabClass("data") %>" href="/Account/Settings.aspx?tab=data"><i class="bi bi-download"></i> Data &amp; export</a>
            </div>
        </nav>

        <div class="settings-body">
            <%= SectionHtml %>
        </div>
    </div>
    </div>
</asp:Content>


