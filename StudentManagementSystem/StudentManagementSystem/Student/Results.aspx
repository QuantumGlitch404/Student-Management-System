<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Results.aspx.cs" Inherits="StudentManagementSystem.Student.Results" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">My results - Student</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>My results</h1>
            <p>Every published assessment with marks, percentage, grade and pass status. Draft results stay hidden until the examination office publishes them.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=marksheet&amp;format=pdf"><i class="bi bi-file-pdf"></i> Marksheet PDF</a>
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=myresults&amp;format=xlsx"><i class="bi bi-file-earmark-excel"></i> Excel</a>
        </div>
    </div>
    <div class="tile"><div class="tile__head">Published results</div><div class="tile__body sms-table-wrap table-wrap"><%= SheetTable %></div></div>
    </div>
</asp:Content>


