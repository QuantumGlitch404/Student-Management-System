<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Payments.aspx.cs" Inherits="StudentManagementSystem.Admin.Payments" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Payments - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <div class="kicker">Finance</div>
            <h1>Payments</h1>
            <p class="page-sub">Every transaction with its gateway reference, receipt and settlement status. Refunds are recorded, never invented.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-soft" href="<%= ExportUrl("pdf") %>">Export PDF</a>
            <a class="btn btn-soft" href="<%= ExportUrl("xlsx") %>">Excel</a>
            <a class="btn btn-soft" href="<%= ExportUrl("csv") %>">CSV</a>
            <a class="btn btn-soft" href="<%= ExportUrl("json") %>">JSON</a>
        </div>
    </div>

    <% if (RazorpayOn) { %>
      <div class="alert <%= RazorpayTest ? "alert-warning" : "alert-success" %> small"><b><%= RazorpayTest ? "TEST MODE" : "LIVE MODE" %></b> - <%= RazorpayTest ? "payments are simulated by Razorpay; no real money moves. Switch keys in Settings before going live." : "real payments are being accepted." %></div>
    <% } else { %>
      <div class="alert alert-secondary small"><b>OFFLINE MODE</b> - no online gateway is configured. Record cash/bank/UPI collections from the ledger below.</div>
    <% } %>
    <%= SummaryCards %>

    <div class="tile"><div class="tile__head d-flex justify-content-between">
        <span>Transaction ledger</span>
        <span class="small text-muted">Razorpay is <%= RazorpayOn ? "enabled (test mode)" : "not configured - offline payments can be captured here" %></span>
    </div>
    <div class="tile__body">
        <form method="get" action="/Admin/Payments.aspx" class="filter-grid mb-3">
            <div><label class="form-label small">Status</label>
                <select name="status" class="form-select form-select-sm">
                    <option value="">All statuses</option>
                    <option value="created"<%= Sel("created") %>>Created</option>
                    <option value="pending"<%= Sel("pending") %>>Pending</option>
                    <option value="captured"<%= Sel("captured") %>>Captured</option>
                    <option value="failed"<%= Sel("failed") %>>Failed</option>
                    <option value="cancelled"<%= Sel("cancelled") %>>Cancelled</option>
                </select></div>
            <div><label class="form-label small">From</label><input type="date" name="from" value="<%= Attr(From) %>" class="form-control form-control-sm"/></div>
            <div><label class="form-label small">To</label><input type="date" name="to" value="<%= Attr(To) %>" class="form-control form-control-sm"/></div>
            <div><label class="form-label small">Search</label><input name="q" value="<%= Attr(QTerm) %>" class="form-control form-control-sm" placeholder="Name, email, order, receipt"/></div>
            <div class="filter-actions"><button class="btn btn-sms btn-sm">Filter</button><a class="btn btn-link btn-sm" href="/Admin/Payments.aspx">Reset</a></div>
        </form>
        <div class="sms-table-wrap table-wrap"><%= PaymentTable %></div>
    </div></div>
    </div>
</asp:Content>



