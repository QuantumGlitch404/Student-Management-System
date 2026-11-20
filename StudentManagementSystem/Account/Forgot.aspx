<%@ Page Language="C#" MasterPageFile="~/Account/Account.master" AutoEventWireup="true" CodeBehind="Forgot.aspx.cs" Inherits="StudentManagementSystem.Account.Forgot" %>

<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Forgot password - Student Management System</asp:Content>
<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">

    <h1 class="auth-title">Reset your password</h1>
    <p class="auth-sub">Enter the email address on your account and we will send a one-time reset link.</p>

    <asp:Literal ID="litAlert" runat="server" EnableViewState="false" />

    <asp:PlaceHolder ID="phForm" runat="server">
        <div class="mb-3">
            <asp:Label ID="lblEmail" runat="server" AssociatedControlID="txtEmail" CssClass="form-label" Text="Email address" />
            <asp:TextBox ID="txtEmail" runat="server" CssClass="form-control" MaxLength="150"
                TextMode="Email" autocomplete="email" inputmode="email" spellcheck="false" />
            <asp:RequiredFieldValidator ID="rfvEmail" runat="server" ControlToValidate="txtEmail" ValidationGroup="Forgot"
                ErrorMessage="Email is required." CssClass="text-danger small sms-reserved" Display="Dynamic" />
            <asp:RegularExpressionValidator ID="revEmail" runat="server" ControlToValidate="txtEmail" ValidationGroup="Forgot"
                ErrorMessage="Enter a valid email address." CssClass="text-danger small sms-reserved" Display="Dynamic"
                ValidationExpression="^[^@\s]+@[^@\s]+\.[^@\s]+$" />
        </div>
        <asp:Button ID="btnSend" runat="server" CssClass="btn btn-primary btn-sms w-100" Text="Email me the reset link"
            OnClick="btnSend_Click" ValidationGroup="Forgot" />
    </asp:PlaceHolder>

    <div class="auth-alt d-flex justify-content-between">
        <a href="/Account/Login.aspx">Back to sign in</a>
        <a href="/Account/Signup.aspx">Create an account</a>
    </div>
</asp:Content>
