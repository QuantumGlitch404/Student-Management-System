<%@ Page Language="C#" MasterPageFile="~/Account/Account.master" AutoEventWireup="true" CodeBehind="Verify.aspx.cs" Inherits="StudentManagementSystem.Account.Verify" %>

<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Verify code - Student Management System</asp:Content>
<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">

    <h1 class="auth-title">Verify your code</h1>
    <p class="auth-sub" id="pIntro" runat="server"></p>

    <asp:Literal ID="litAlert" runat="server" EnableViewState="false" />

    <div class="mb-3">
        <asp:Label ID="lblOtp" runat="server" AssociatedControlID="txtOtp" CssClass="form-label" Text="6-digit code" />
        <asp:TextBox ID="txtOtp" runat="server" CssClass="form-control otp-input" MaxLength="6" data-otp="1" />
        <asp:RequiredFieldValidator ID="rfvOtp" runat="server" ControlToValidate="txtOtp" ValidationGroup="Verify"
            ErrorMessage="Enter the code we sent you." CssClass="text-danger small" Display="Dynamic" />
        <asp:RegularExpressionValidator ID="revOtp" runat="server" ControlToValidate="txtOtp" ValidationGroup="Verify"
            ErrorMessage="The code is 6 digits." CssClass="text-danger small" Display="Dynamic"
            ValidationExpression="^[0-9]{6}$" />
    </div>

    <asp:Button ID="btnVerify" runat="server" CssClass="btn btn-primary btn-sms w-100" Text="Verify and continue"
        OnClick="btnVerify_Click" ValidationGroup="Verify" />

    <div class="d-flex justify-content-between mt-3 small">
        <asp:Button ID="btnResend" runat="server" CssClass="btn btn-link btn-sm p-0" Text="Resend code"
            OnClick="btnResend_Click" />
        <a href="/Account/Login.aspx">Back to sign in</a>
    </div>

    <div class="auth-note mt-3" id="pNote" runat="server"></div>
</asp:Content>
