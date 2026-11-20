<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" ValidateRequest="false" CodeBehind="MailCenter.aspx.cs" Inherits="StudentManagementSystem.Admin.MailCenter" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Mail centre</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
        <%= FlashHtml %>
        <div class="page-head">
            <div>
                <h1>Mail centre</h1>
                <p>Send, schedule and track campus email from your own mailbox. No paid mail service required.</p>
            </div>
            <div class="page-actions"><a class="btn btn-outline-sms btn-sm" href="?tab=compose">New message</a></div>
        </div>
        <nav class="tabs" role="tablist" aria-label="Mail centre sections"><%= TabsHtml %></nav>
        <%= BodyHtml %>
    </div>
</asp:Content>


