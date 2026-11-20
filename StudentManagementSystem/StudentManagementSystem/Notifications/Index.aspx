<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Index.aspx.cs" Inherits="StudentManagementSystem.Notifications.Index" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Notifications</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page"><%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>Notification centre</h1>
            <p>Search your history, filter by category, and archive what you no longer need. Opening a notification marks it read and takes you to the related record.</p>
        </div>
        <div class="page-actions"><a class="btn btn-outline-sms btn-sm" href="/Account/Settings.aspx#notifications"><i class="bi bi-sliders"></i> Preferences</a></div>
    </div>
<section class="panel">
        <div class="panel__head"><strong>Notifications</strong></div>
        <div class="panel__body">
            <%= FilterBar %>
            <%= CountsHeader %>
            <%= RowsHtml %>
            <%= PagerHtml %>
        </div>
    </section>
    </div>
</asp:Content>

