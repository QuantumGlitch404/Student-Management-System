<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Profile.aspx.cs" Inherits="StudentManagementSystem.Student.Profile" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">My profile - Student</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <div class="kicker">Account</div>
            <h1>My profile</h1>
            <p class="page-sub">Your academic record, contact details, password and two-factor sign-in.</p>
        </div>
    </div>
    <div class="bento"><%= Body %></div>
    </div>
</asp:Content>


