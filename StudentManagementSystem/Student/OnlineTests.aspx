<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="OnlineTests.aspx.cs" Inherits="StudentManagementSystem.Student.OnlineTests" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Online tests</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server">
    <script src="/Content/js/online-test.js"></script>
</asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <%= PageBody %>
    </div>
</asp:Content>
