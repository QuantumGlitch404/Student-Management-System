<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Notifications.aspx.cs" Inherits="StudentManagementSystem.Student.Notifications" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">My notifications - Student</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page"><%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>My notifications</h1>
            <p>Results, attendance alerts, fee reminders and replies from the college, all in one searchable history.</p>
        </div>
        <div class="page-actions"><a class="btn btn-outline-sms btn-sm" href="/Account/Settings.aspx#notifications"><i class="bi bi-sliders"></i> Preferences</a></div>
    </div>
<section class="panel"><div class="panel__body"><%= InboxHtml %></div></section>
    </div>
</asp:Content>

