<%@ Page Language="C#" MasterPageFile="~/Account/Account.master" AutoEventWireup="true" CodeBehind="Logout.aspx.cs" Inherits="StudentManagementSystem.Account.Logout" %>

<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Signed out - Student Management System</asp:Content>
<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <h1 class="auth-title">You are signed out</h1>
    <p class="auth-sub">Your session was cleared. It is safe to leave this page open.</p>
    <a class="btn btn-primary btn-sms w-100" href="/Account/Login.aspx">Sign in again</a>
</asp:Content>
