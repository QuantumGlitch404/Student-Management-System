<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Complaints.aspx.cs" Inherits="StudentManagementSystem.Faculty.Complaints" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Complaint replies - Faculty</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>Complaints assigned to me</h1>
            <p>Read the ticket history, reply to the student and move the ticket through the workflow. Internal notes are never shown to students.</p>
        </div>
    </div>

    <div class="tile mb-3"><div class="tile__head">Complaints I am handling</div>
        <div class="tile__body sms-table-wrap table-wrap"><%= ListTable %></div></div>

    <% if (SelectedId > 0) { %>
    <div class="tile"><div class="tile__head d-flex justify-content-between"><span><%= E(DetailHead) %></span>
        <a class="imbtn" href="/Faculty/Complaints.aspx">Close</a></div>
        <div class="tile__body"><%= DetailBody %></div></div>
    <% } %>
    </div>
</asp:Content>


