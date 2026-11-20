<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Complaint.aspx.cs" Inherits="StudentManagementSystem.Student.Complaint" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Support - Student</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>Help &amp; complaints</h1>
            <p>Raise a ticket with the right category and follow its status timeline. Staff replies appear on the ticket; you are notified when it changes.</p>
        </div>
    </div>
    <div class="bento">
        <div class="c-5"><div class="tile"><div class="tile__head">Raise a complaint</div><div class="tile__body">
            <form method="post" action="/Student/Complaint.aspx">
                <%= CsrfField() %><input type="hidden" name="op" value="new"/>
                <label class="form-label small">Category</label>
                <select name="categoryId" required class="form-select form-select-sm mb-2">
                    <option value="">-- pick a category --</option>
                    <%= Options("CategoryId", "CatName", Cats, "") %>
                </select>
                <label class="form-label small">Category subject</label>
                <input name="subject" required maxlength="140" class="form-control form-control-sm mb-2" placeholder="Short title"/>
                <label class="form-label small">Category description</label>
                <textarea name="description" required rows="4" maxlength="2000" class="form-control form-control-sm mb-2" placeholder="Describe the issue"></textarea>
                <button class="btn btn-sms btn-sm">Submit</button>
            </form>
        </div></div></div>
        <div class="c-7"><div class="tile"><div class="tile__head">My tickets</div><div class="tile__body sms-table-wrap table-wrap"><%= ListTable %></div></div>
            <% if (SelectedId > 0) { %><div class="tile mt-3"><div class="tile__head d-flex justify-content-between"><span><%= E(DetailHead) %></span>
                <a class="imbtn" href="/Student/Complaint.aspx">Close</a></div><div class="tile__body"><%= DetailBody %></div></div><% } %>
        </div>
    </div>
    </div>
</asp:Content>


