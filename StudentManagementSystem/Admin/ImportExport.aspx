<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="ImportExport.aspx.cs" Inherits="StudentManagementSystem.Admin.ImportExport" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Import and export - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <%= Result %>

    <div class="page-head">
        <div>
            <h1>Import &amp; export</h1>
            <p>Validate a CSV or JSON file before touching the database, then import students, faculty, courses, subjects or enrollments. Every export is permission-checked, audit-logged and available as PDF, Excel, CSV or JSON.</p>
        </div>
        <div class="page-actions d-flex flex-wrap gap-2"><%= TemplateLinks %></div>
    </div>

    <div class="bento">
        <div class="c-6">
            <div class="tile"><div class="tile__head">Import data from CSV or JSON</div><div class="tile__body">
                <p class="small text-muted mb-2">Each type has a template above. CSV or a JSON array of objects is accepted. New people get a generated temporary password (one per row, emailed when configured). Imports are audit-logged.</p>
                <form method="post" action="/Admin/ImportExport.aspx" enctype="multipart/form-data">
                    <%= CsrfField() %><input type="hidden" name="op" value="import"/>
                    <select name="kind" class="form-select form-select-sm mb-2" aria-label="Import type"><%= ImportKindOptions %></select>
                    <input type="file" name="file" accept=".csv,text/csv,.json,application/json" required class="form-control form-control-sm"/>
                    <label class="form-check small mt-2"><input class="form-check-input" type="checkbox" name="preview" value="1"/> Validate only (dry run, nothing is written)</label>
                    <button class="btn btn-sms btn-sm mt-2"><i class="bi bi-upload"></i> Process file</button>
                </form>
            </div></div>

            <div class="tile mt-3"><div class="tile__head">Export data</div><div class="tile__body">
                <p class="small text-muted">Each export is audit-logged and recorded in ExportJobs.
                PDFs are paginated with repeated headers, filters and page numbers; use CSV or XLSX when you need every column.</p>
                <%= ExportLinks %></div></div>
        </div>

        <div class="c-6">
            <div class="tile"><div class="tile__head">Recent import jobs</div><div class="tile__body sms-table-wrap table-wrap"><%= JobsTable %></div></div>
        </div>
    </div>
    </div>
</asp:Content>



