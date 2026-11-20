<%@ Page Language="C#" MasterPageFile="~/Account/Account.master" AutoEventWireup="true" CodeBehind="Error.aspx.cs" Inherits="StudentManagementSystem.Account.Error" %>

<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Something went wrong - Student Management System</asp:Content>
<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="auth-error-mark" id="pMark" runat="server" aria-hidden="true">!</div>
    <h1 class="auth-title text-center" id="pTitle" runat="server">Something went wrong</h1>
    <p class="auth-sub text-center" id="pSub" runat="server">
        The page could not complete. The problem has been logged. Use the buttons below to continue.
    </p>
    <div class="d-grid gap-2">
        <a class="btn btn-primary btn-sms" id="pPrimary" runat="server" href="/Account/Login.aspx">Back to sign in</a>
        <a class="btn btn-outline-secondary" id="pSecondary" runat="server" href="/Default.aspx">Open the home page</a>
    </div>
    <p class="small text-muted text-center mt-3 mb-0" id="pDetail" runat="server"></p>
</asp:Content>
