<%@ Page Language="C#" MasterPageFile="~/Account/Account.master" AutoEventWireup="true" CodeBehind="Signup.aspx.cs" Inherits="StudentManagementSystem.Account.Signup" %>

<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Join - Student Management System</asp:Content>
<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">

    <h1 class="auth-title">Create account</h1>
    <p class="auth-sub">Create your account with any email address. A verification code is sent by email when configured.</p>

    <asp:Literal ID="litAlert" runat="server" EnableViewState="false" />

    <div class="auth-field-row">
        <div class="mb-3">
            <asp:Label ID="lblFullName" runat="server" AssociatedControlID="txtFullName" CssClass="form-label" Text="Full name" />
            <asp:TextBox ID="txtFullName" runat="server" CssClass="form-control" MaxLength="120"
                autocomplete="name" />
            <div class="form-text">As printed on your ID.</div>
            <asp:RequiredFieldValidator ID="rfvFullName" runat="server" ControlToValidate="txtFullName" ValidationGroup="Signup"
                ErrorMessage="Full name is required." CssClass="text-danger small sms-reserved" Display="Dynamic" />
        </div>

        <div class="mb-3">
            <asp:Label ID="lblPhone" runat="server" AssociatedControlID="txtPhone" CssClass="form-label" Text="Mobile (optional)" />
            <asp:TextBox ID="txtPhone" runat="server" CssClass="form-control" MaxLength="15"
                TextMode="Phone" autocomplete="tel" inputmode="tel" />
            <div class="form-text">Optional, 10-15 digits.</div>
            <asp:RegularExpressionValidator ID="revPhone" runat="server" ControlToValidate="txtPhone" ValidationGroup="Signup"
                ErrorMessage="Use 10 to 15 digits." CssClass="text-danger small sms-reserved" Display="Dynamic"
                ValidationExpression="^[0-9]{10,15}$" />
        </div>
    </div>

    <div class="mb-3">
        <asp:Label ID="lblEmail" runat="server" AssociatedControlID="txtEmail" CssClass="form-label" Text="Email address" />
        <asp:TextBox ID="txtEmail" runat="server" CssClass="form-control" MaxLength="150"
            TextMode="Email" autocomplete="email" inputmode="email" spellcheck="false" />
        <asp:RequiredFieldValidator ID="rfvEmail" runat="server" ControlToValidate="txtEmail" ValidationGroup="Signup"
            ErrorMessage="Email is required." CssClass="text-danger small sms-reserved" Display="Dynamic" />
        <asp:RegularExpressionValidator ID="revEmail" runat="server" ControlToValidate="txtEmail" ValidationGroup="Signup"
            ErrorMessage="Enter a valid email address." CssClass="text-danger small sms-reserved" Display="Dynamic"
            ValidationExpression="^[^@\s]+@[^@\s]+\.[^@\s]+$" />
    </div>

    <div class="auth-field-row">
        <div class="mb-2">
            <asp:Label ID="lblPassword" runat="server" AssociatedControlID="txtPassword" CssClass="form-label" Text="Password" />
            <div class="input-shell has-toggle">
                <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" MaxLength="100"
                    TextMode="Password" autocomplete="new-password" data-pwd-meter="pwdMeter1" />
                <button type="button" class="toggle-password" aria-label="Show password">Show</button>
            </div>
            <asp:RequiredFieldValidator ID="rfvPassword" runat="server" ControlToValidate="txtPassword" ValidationGroup="Signup"
                ErrorMessage="Password is required." CssClass="text-danger small sms-reserved" Display="Dynamic" />
            <asp:CustomValidator ID="cvPasswordLength" runat="server" ControlToValidate="txtPassword" ValidationGroup="Signup"
                ErrorMessage="Password must be at least 8 characters." CssClass="text-danger small sms-reserved" Display="Dynamic"
                ClientValidationFunction="smsValidatePasswordLength"
                OnServerValidate="cvPasswordLength_ServerValidate" />
        </div>

        <div class="mb-2">
            <asp:Label ID="lblConfirm" runat="server" AssociatedControlID="txtConfirm" CssClass="form-label" Text="Confirm password" />
            <div class="input-shell has-toggle">
                <asp:TextBox ID="txtConfirm" runat="server" CssClass="form-control" MaxLength="100"
                    TextMode="Password" autocomplete="new-password" />
                <button type="button" class="toggle-password" aria-label="Show password">Show</button>
            </div>
            <asp:RequiredFieldValidator ID="rfvConfirm" runat="server" ControlToValidate="txtConfirm" ValidationGroup="Signup"
                ErrorMessage="Confirm your password." CssClass="text-danger small sms-reserved" Display="Dynamic" />
            <asp:CompareValidator ID="cpvConfirm" runat="server" ControlToCompare="txtPassword" ControlToValidate="txtConfirm" ValidationGroup="Signup"
                ErrorMessage="Passwords do not match." CssClass="text-danger small sms-reserved" Display="Dynamic" />
        </div>
    </div>
    <div class="auth-password-help">
        <div id="pwdMeter1" class="pwd-meter"><span></span><span></span><span></span><span></span></div>
        <div class="auth-hint mb-2" id="divMinLength" runat="server"></div>
    </div>

    <div class="form-check auth-consent mb-3">
        <asp:CheckBox ID="chkConsent" runat="server" CssClass="form-check-input" />
        <asp:Label ID="lblConsent" runat="server" AssociatedControlID="chkConsent" CssClass="form-check-label small"
            Text="I consent to the stored data being used for academic administration." />
        <asp:CustomValidator ID="cvConsent" runat="server" ValidationGroup="Signup"
            ErrorMessage="Consent is required." CssClass="text-danger small sms-reserved" Display="Dynamic"
            ClientValidationFunction="smsValidateConsent"
            OnServerValidate="cvConsent_ServerValidate" />
    </div>

    <asp:Button ID="btnCreate" runat="server" CssClass="btn btn-primary btn-sms w-100" Text="Create account"
        OnClick="btnCreate_Click" ValidationGroup="Signup" />

    <div class="auth-alt auth-alt-center">
        <a href="/Account/Login.aspx">Already registered? Sign in</a>
    </div>
</asp:Content>

<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server">
    <script>window.SMS_MIN_PASSWORD = <%= MinPasswordLength %>;</script>
</asp:Content>
