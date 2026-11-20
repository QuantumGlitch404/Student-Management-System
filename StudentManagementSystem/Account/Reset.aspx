<%@ Page Language="C#" MasterPageFile="~/Account/Account.master" AutoEventWireup="true" CodeBehind="Reset.aspx.cs" Inherits="StudentManagementSystem.Account.Reset" %>

<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Set new password - Student Management System</asp:Content>
<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">

    <h1 class="auth-title">Set a new password</h1>
    <p class="auth-sub">Choose a strong password. At least 8 characters with letters and numbers.</p>

    <asp:Literal ID="litAlert" runat="server" EnableViewState="false" />

    <asp:PlaceHolder ID="phInvalid" runat="server" Visible="false">
        <div class="alert alert-warning small">This reset link is invalid, used, or expired. Request a new one.</div>
        <a class="btn btn-outline-primary w-100" href="/Account/Forgot.aspx">Request a new link</a>
    </asp:PlaceHolder>

    <asp:PlaceHolder ID="phForm" runat="server">
        <div class="mb-2">
            <asp:Label ID="lblPassword" runat="server" AssociatedControlID="txtPassword" CssClass="form-label" Text="New password" />
            <div class="input-shell has-toggle">
                <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" MaxLength="100" data-pwd-meter="pwdMeter2" />
                <button type="button" class="toggle-password" aria-label="Show password">Show</button>
            </div>
            <div id="pwdMeter2" class="pwd-meter"><span></span><span></span><span></span><span></span></div>
        </div>

        <div class="mb-3">
            <asp:Label ID="lblConfirm" runat="server" AssociatedControlID="txtConfirm" CssClass="form-label" Text="Confirm new password" />
            <div class="input-shell has-toggle">
                <asp:TextBox ID="txtConfirm" runat="server" CssClass="form-control" MaxLength="100" />
                <button type="button" class="toggle-password" aria-label="Show password">Show</button>
            </div>
        </div>

        <asp:Button ID="btnReset" runat="server" CssClass="btn btn-primary btn-sms w-100" Text="Save new password"
            OnClick="btnReset_Click" ValidationGroup="Reset" />
    </asp:PlaceHolder>

    <div class="auth-alt">
        <a href="/Account/Login.aspx">Back to sign in</a>
    </div>
</asp:Content>
