<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Fees.aspx.cs" Inherits="StudentManagementSystem.Student.Fees" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Fees and payments - Student</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>Fees &amp; payments</h1>
            <p>Your fee book, outstanding balance and every transaction. Receipts and statements can be downloaded as genuine PDFs.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-outline-sms btn-sm" href="/Handlers/Export.ashx?scope=mypayments&amp;format=pdf"><i class="bi bi-file-pdf"></i> Statement</a>
        </div>
    </div>
    <% if (DemoMode) { %><div class="alert alert-info small">DEMO mode is on: online payment buttons record a demonstration receipt and no money is charged. An administrator can switch to Razorpay in Settings &gt; Integrations.</div><% } %>
    <div class="tile mb-3"><div class="tile__head">My fee book (<%= E(CourseLabel) %>)</div><div class="tile__body sms-table-wrap table-wrap"><%= FeeBookTable %></div></div>
    <div class="tile"><div class="tile__head">Payment history</div><div class="tile__body sms-table-wrap table-wrap"><%= HistoryTable %></div></div>

    <div class="tile mt-3"><div class="tile__head">Download my records</div><div class="tile__body">
        <p class="small text-muted mb-2">Your own data only, in the format you need. Every export is audit-logged.</p>
        <div class="d-flex flex-wrap gap-2">
            <span class="small me-2">Payments:</span>
            <a class="imbtn" href="/Handlers/Export.ashx?scope=mypayments&amp;format=csv">CSV</a>
            <a class="imbtn" href="/Handlers/Export.ashx?scope=mypayments&amp;format=xlsx">Excel</a>
            <a class="imbtn" href="/Handlers/Export.ashx?scope=mypayments&amp;format=pdf">PDF</a>
            <a class="imbtn" href="/Handlers/Export.ashx?scope=mypayments&amp;format=json">JSON</a>
        </div>
        <div class="d-flex flex-wrap gap-2 mt-2">
            <span class="small me-2">Results:</span>
            <a class="imbtn" href="/Handlers/Export.ashx?scope=myresults&amp;format=csv">CSV</a>
            <a class="imbtn" href="/Handlers/Export.ashx?scope=myresults&amp;format=xlsx">Excel</a>
            <a class="imbtn" href="/Handlers/Export.ashx?scope=myresults&amp;format=pdf">PDF</a>
            <a class="imbtn" href="/Handlers/Export.ashx?scope=myresults&amp;format=json">JSON</a>
        </div>
        <div class="d-flex flex-wrap gap-2 mt-2">
            <span class="small me-2">Attendance:</span>
            <a class="imbtn" href="/Handlers/Export.ashx?scope=myattendance&amp;format=csv">CSV</a>
            <a class="imbtn" href="/Handlers/Export.ashx?scope=myattendance&amp;format=xlsx">Excel</a>
            <a class="imbtn" href="/Handlers/Export.ashx?scope=myattendance&amp;format=pdf">PDF</a>
            <a class="imbtn" href="/Handlers/Export.ashx?scope=myattendance&amp;format=json">JSON</a>
        </div>
    </div></div>
    </div>
</asp:Content>


