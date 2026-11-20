<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="PayCheckout.aspx.cs" Inherits="StudentManagementSystem.Student.PayCheckout" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Confirm payment - Student</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="tile" style="max-width:480px"><div class="tile__head">Online payment</div><div class="tile__body">
        <p class="mb-1 h5"><%= E(Line1) %></p>
        <p class="small text-muted">Amount <b><%= E(AmtStr) %></b> - order <span class="text-break"><%= E(OrderId) %></span></p>
        <!-- The checkout handler posts back to Fees.aspx, which validates this token. -->
        <input type="hidden" name="__csrf" value="<%= Csrf() %>" />
        <button id="rzpBtn" class="btn btn-sms">Pay now</button>
        <a class="btn btn-outline-sms btn-sm" href="/Student/Fees.aspx">Cancel</a>
    </div></div>
    <%= ScriptBlock %>
    </div>
</asp:Content>

