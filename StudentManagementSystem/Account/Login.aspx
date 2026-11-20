<%@ Page Language="C#" MasterPageFile="~/Account/Account.master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="StudentManagementSystem.Account.Login" %>

<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Sign in - Student Management System</asp:Content>
<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">

    <h1 class="auth-title">Sign in</h1>
    <p class="auth-sub">Enter your registered email address and password to continue.</p>

    <asp:Literal ID="litAlert" runat="server" EnableViewState="false" />

    <div class="mb-3">
        <asp:Label ID="lblEmail" runat="server" AssociatedControlID="txtEmail" CssClass="form-label" Text="Email address" />
        <asp:TextBox ID="txtEmail" runat="server" CssClass="form-control" MaxLength="150"
            TextMode="Email" autocomplete="email" inputmode="email" spellcheck="false" />
        <asp:RequiredFieldValidator ID="rfvEmail" runat="server" ControlToValidate="txtEmail" ValidationGroup="Login"
            ErrorMessage="Email is required." CssClass="text-danger small sms-reserved" Display="Dynamic" />
        <asp:RegularExpressionValidator ID="revEmail" runat="server" ControlToValidate="txtEmail" ValidationGroup="Login"
            ErrorMessage="Enter a valid email address." CssClass="text-danger small sms-reserved" Display="Dynamic"
            ValidationExpression="^[^@\s]+@[^@\s]+\.[^@\s]+$" />
    </div>

    <div class="mb-2">
        <asp:Label ID="lblPassword" runat="server" AssociatedControlID="txtPassword" CssClass="form-label" Text="Password" />
        <div class="input-shell has-toggle">
            <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" MaxLength="100"
                TextMode="Password" autocomplete="current-password" />
            <button type="button" class="toggle-password" aria-label="Show password">Show</button>
        </div>
        <asp:RequiredFieldValidator ID="rfvPassword" runat="server" ControlToValidate="txtPassword" ValidationGroup="Login"
            ErrorMessage="Password is required." CssClass="text-danger small sms-reserved" Display="Dynamic" />
    </div>

    <div class="form-check mb-3">
        <asp:CheckBox ID="chkRemember" runat="server" CssClass="form-check-input" />
        <asp:Label ID="lblRemember" runat="server" AssociatedControlID="chkRemember" CssClass="form-check-label small" Text="Remember me on this computer" />
    </div>

    <asp:Button ID="btnSignIn" runat="server" CssClass="btn btn-primary btn-sms w-100" Text="Sign in"
        OnClick="btnSignIn_Click" ValidationGroup="Login" />

    <asp:PlaceHolder ID="phGoogle" runat="server" Visible="false">
        <div class="auth-or"><span>or</span></div>
        <a class="btn btn-outline-secondary btn-google w-100" href="/Handlers/GoogleOAuth.ashx?flow=start">
            <i class="bi bi-google"></i> Continue with Google
        </a>
    </asp:PlaceHolder>

    <div class="auth-alt d-flex justify-content-between">
        <a href="/Account/Forgot.aspx">Forgot password?</a>
        <a href="/Account/Signup.aspx">Create an account</a>
    </div>

</asp:Content>
