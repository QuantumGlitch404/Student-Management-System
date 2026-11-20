<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Activity.aspx.cs" Inherits="StudentManagementSystem.Account.Activity" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">My activity</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
        <div class="page-head">
            <div>
                <h1>My activity</h1>
                <p>Your own account and record events, newest first. Times use your timezone.</p>
            </div>
            <div class="page-head-actions">
                <a class="btn btn-outline-sms btn-sm" href="<%= ExportUrl("csv") %>">Export CSV</a>
                <a class="btn btn-outline-sms btn-sm" href="<%= ExportUrl("pdf") %>">Export PDF</a>
            </div>
        </div>

        <form method="get" action="/Account/Activity.aspx" class="filter-strip">
            <div>
                <label class="form-label small" for="q">Search</label>
                <input class="form-control form-control-sm" id="q" name="q" value="<%= Attr(Q) %>" placeholder="Action or detail"/>
            </div>
            <div>
                <label class="form-label small" for="action">Action</label>
                <input class="form-control form-control-sm" id="action" name="action" value="<%= Attr(ActionFilter) %>" placeholder="e.g. Login"/>
            </div>
            <div>
                <label class="form-label small" for="entity">Area</label>
                <select class="form-select form-select-sm" id="entity" name="entity">
                    <option value="">All areas</option>
                    <%= EntityOptions %>
                </select>
            </div>
            <div>
                <label class="form-label small" for="from">From</label>
                <input class="form-control form-control-sm" id="from" name="from" type="date" value="<%= Attr(From) %>"/>
            </div>
            <div>
                <label class="form-label small" for="to">To</label>
                <input class="form-control form-control-sm" id="to" name="to" type="date" value="<%= Attr(To) %>"/>
            </div>
            <div class="d-flex gap-2">
                <button class="btn btn-sms btn-sm" type="submit">Apply</button>
                <a class="btn btn-outline-sms btn-sm" href="/Account/Activity.aspx">Reset</a>
            </div>
        </form>

        <div class="tile mt-3">
            <div class="tile__head d-flex justify-content-between">
                <span>Activity history</span>
                <span class="small text-muted"><%= TotalShown %></span>
            </div>
            <div class="tile__body sms-table-wrap table-wrap">
                <%= Table %>
            </div>
        </div>
        <%= PagerHtml %>
    </div>
</asp:Content>
