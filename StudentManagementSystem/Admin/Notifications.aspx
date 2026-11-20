<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Notifications.aspx.cs" Inherits="StudentManagementSystem.Admin.Notifications" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Notifications - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>Send notifications</h1>
            <p>Broadcast academic, payment or system messages. Recipients see them in the notification centre and can archive what they no longer need.</p>
        </div>
        <div class="page-actions"><a class="btn btn-outline-sms btn-sm" href="/Notifications/Index.aspx"><i class="bi bi-inbox"></i> My notification centre</a></div>
    </div>

    <div class="bento">
        <div class="c-4">
            <div class="tile"><div class="tile__head">Compose</div><div class="tile__body">
                <form method="post" action="/Admin/Notifications.aspx" class="form-grid">
                    <%= CsrfField() %><input type="hidden" name="op" value="send"/>
                    <div class="c-5"><label class="form-label small">Type</label>
                        <select name="ntype" class="form-select form-select-sm">
                            <option value="general">General</option>
                            <option value="academic">Academic</option>
                            <option value="payment">Payment</option>
                            <option value="complaint">Complaint</option>
                            <option value="system">System</option>
                        </select></div>
                    <div class="c-7"><label class="form-label small">Audience</label>
                        <select name="audience" class="form-select form-select-sm">
                            <option value="all">Everyone</option>
                            <option value="students">All students</option>
                            <option value="faculty">All faculty</option>
                            <option value="course:">Course students</option>
                        </select></div>
                    <div class="c-12"><label class="form-label small">Course (when audience is a course)</label>
                        <select name="courseId" class="form-select form-select-sm"><%= CourseOptions %></select></div>
                    <div class="c-12"><label class="form-label small">Title</label><input name="title" required class="form-control form-control-sm" maxlength="150"/></div>
                    <div class="c-12"><label class="form-label small">Message</label><textarea name="message" required class="form-control form-control-sm" rows="3" maxlength="600"></textarea></div>
                    <div class="c-12"><label class="form-label small">Optional action link</label><input name="actionUrl" class="form-control form-control-sm" maxlength="200" placeholder="/Student/Results.aspx"/></div>
                    <div class="c-12 mt-2">
                        <label class="form-check-label small"><input type="checkbox" name="alsoEmail" value="1" <%= EmailEnabled ? "" : "disabled" %>/> also deliver by email (config: <code>Email:Enabled</code>)</label>
                    </div>
                    <div class="c-12 mt-2"><button class="btn btn-sms btn-sm">Send notification</button></div>
                </form>
            </div></div>
        </div>

        <div class="c-8">
            <div class="tile"><div class="tile__head">Recent notifications</div><div class="tile__body sms-table-wrap table-wrap"><%= RecentTable %></div></div>
        </div>
    </div>
    </div>
</asp:Content>



