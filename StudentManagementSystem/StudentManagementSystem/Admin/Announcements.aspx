<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Announcements.aspx.cs" Inherits="StudentManagementSystem.Admin.Announcements" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Announcements - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <div class="kicker">Communications</div>
            <h1>Announcements</h1>
            <p class="page-sub">Publish, schedule and target notices. Pinned items surface first; readers can acknowledge them.</p>
        </div>
        <div class="page-actions">
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=announcements&amp;format=pdf">Export PDF</a>
            <a class="btn btn-soft" href="/Handlers/Export.ashx?scope=announcements&amp;format=csv">CSV</a>
            <% if (Editing) { %><a class="btn btn-soft" href="/Admin/Announcements.aspx">New announcement</a><% } %>
        </div>
    </div>

    <div class="tile mb-3"><div class="tile__head"><%= Editing ? "Edit announcement" : "Create announcement" %></div><div class="tile__body">
        <form method="post" action="/Admin/Announcements.aspx" class="form-grid">
            <%= CsrfField() %>
            <input type="hidden" name="op" value="<%= Editing ? "update" : "new" %>"/>
            <input type="hidden" name="annId" value="<%= AnnId %>"/>
            <div class="c-8"><label class="form-label small">Title</label><input name="title" required value="<%= Attr(FTitle) %>" class="form-control form-control-sm" maxlength="150"/></div>
            <div class="c-4"><label class="form-label small">Category</label>
                <select name="category" class="form-select form-select-sm"><%= CategoryOptions %></select></div>
            <div class="c-12"><label class="form-label small">Body</label><textarea name="body" required class="form-control form-control-sm" rows="3" maxlength="2000"><%= FBody %></textarea></div>
            <div class="c-3"><label class="form-label small">Audience</label>
                <select name="audience" class="form-select form-select-sm"><%= AudienceOptions %></select></div>
            <div class="c-3"><label class="form-label small">Course (optional)</label>
                <select name="targetCourseId" class="form-select form-select-sm"><option value="">All courses</option><%= CourseOptions %></select></div>
            <div class="c-2"><label class="form-label small">Semester (optional)</label>
                <select name="targetSemester" class="form-select form-select-sm"><option value="">All</option><%= SemesterOptions %></select></div>
            <div class="c-2"><label class="form-label small">Publish from</label><input name="publishFrom" type="date" value="<%= Attr(FPublishedFrom) %>" class="form-control form-control-sm"/></div>
            <div class="c-2"><label class="form-label small">Show until</label><input name="publishTo" type="date" value="<%= Attr(FPublishedTo) %>" class="form-control form-control-sm"/></div>
            <div class="c-12 d-flex align-items-center gap-3 mt-2">
                <label class="form-check-label small"><input type="checkbox" name="pinned" value="1"<%= FPinned ? " checked" : "" %>/> Pin to top</label>
                <button class="btn btn-sms btn-sm"><%= Editing ? "Save changes" : "Publish announcement" %></button>
                <span class="small text-muted">Publishing also sends an in-app notification to the selected audience.</span>
            </div>
        </form>
    </div></div>

    <div class="tile"><div class="tile__head d-flex justify-content-between"><span>All announcements</span><span class="small text-muted"><%= TotalLabel %></span></div>
        <div class="tile__body sms-table-wrap table-wrap"><%= AnnTable %></div></div>
    </div>
</asp:Content>



