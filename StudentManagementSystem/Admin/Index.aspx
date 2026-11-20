<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Index.aspx.cs" Inherits="StudentManagementSystem.Admin.Index" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Dashboard - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server">
    <script src="/Content/js/dashboard.js"></script>
</asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page"><%= FlashHtml %>

    <div class="page-head">
        <div>
            <h1>Dashboard</h1>
            <p class="page-sub">Good <%= Greeting %>, <%= E(CurrentUser.FullName.Split(' ')[0]) %>. Enrollment, attendance, examinations and fee collection for the selected period, scoped to your administrator role.</p>
        </div>
        <div class="page-head-actions">
            <button class="btn btn-outline-sms btn-sm" type="button" id="dashCustomise" aria-expanded="false" aria-controls="dashWidgetPanel"><i class="bi bi-grid-3x3-gap"></i> Widgets</button>
            <a class="btn btn-sms btn-sm" href="<%= ExportUrl %>"><i class="bi bi-file-earmark-pdf"></i> Export dashboard</a>
        </div>
    </div>

    <div class="dash-widget-panel" id="dashWidgetPanel" hidden>
        <div class="sap-title">Visible dashboard widgets</div>
        <label><input type="checkbox" data-widget-toggle="trend" checked/> Enrollment trend</label>
        <label><input type="checkbox" data-widget-toggle="finance" checked/> Fee collection</label>
        <label><input type="checkbox" data-widget-toggle="risk" checked/> At-risk attendance</label>
        <label><input type="checkbox" data-widget-toggle="exams" checked/> Upcoming examinations</label>
        <label><input type="checkbox" data-widget-toggle="audit" checked/> Recent activity</label>
        <label><input type="checkbox" data-widget-toggle="courses" checked/> Course distribution</label>
        <label><input type="checkbox" data-widget-toggle="announce" checked/> Announcements</label>
    </div>

    <form method="get" action="/Admin/Index.aspx" class="filter-strip">
        <div><label for="fFrom">From</label><input id="fFrom" type="date" name="from" value="<%= Attr(FromDate) %>" class="form-control form-control-sm"/></div>
        <div><label for="fTo">To</label><input id="fTo" type="date" name="to" value="<%= Attr(ToDate) %>" class="form-control form-control-sm"/></div>
        <div><label for="fSession">Academic year</label><select id="fSession" name="sessionId" class="form-select form-select-sm"><%= SessionOptions %></select></div>
        <div><label for="fSem">Semester</label><select id="fSem" name="semester" class="form-select form-select-sm"><%= SemesterOptions %></select></div>
        <div class="filter-spacer"></div>
        <button class="btn btn-sms btn-sm" type="submit"><i class="bi bi-funnel"></i> Apply</button>
        <a class="btn btn-outline-sms btn-sm" href="/Admin/Index.aspx">Reset</a>
    </form>

    <%= TaskHtml %>

    <%= StatCards %>

    <div class="bento">
        <div class="c-7" data-widget="trend">
            <div class="tile h-100">
                <div class="tile__head"><div>Enrollment trend</div><span class="small text-muted"><%= EnrollmentWindowLabel %></span></div>
                <div class="tile__body"><%= TrendChart %></div>
            </div>
        </div>
        <div class="c-5" data-widget="finance">
            <div class="tile h-100">
                <div class="tile__head"><div>Fee collection</div><a class="imbtn" href="/Handlers/Export.ashx?scope=payments&amp;format=pdf">PDF</a></div>
                <div class="tile__body"><%= FinanceCard %></div>
            </div>
        </div>
    </div>

    <div class="bento">
        <div class="c-6" data-widget="risk">
            <div class="tile h-100">
                <div class="tile__head"><div>At-risk attendance</div><span class="badge badge-warn">below <%= Threshold %>%</span></div>
                <div class="tile__body sms-table-wrap table-wrap"><%= RiskTable %></div>
            </div>
        </div>
        <div class="c-6" data-widget="exams">
            <div class="tile h-100">
                <div class="tile__head"><div>Upcoming examinations</div><a class="imbtn" href="/Admin/Exams.aspx">Open exams</a></div>
                <div class="tile__body sms-table-wrap table-wrap"><%= ExamTable %></div>
            </div>
        </div>
    </div>

    <div class="bento">
        <div class="c-7" data-widget="audit">
            <div class="tile h-100">
                <div class="tile__head"><div>Recent activity</div><a class="imbtn" href="/Admin/Audit.aspx">Full audit log</a></div>
                <div class="tile__body sms-table-wrap table-wrap"><%= RecentAudit %></div>
            </div>
        </div>
        <div class="c-5" data-widget="courses">
            <div class="tile">
                <div class="tile__head"><div>Students per course</div></div>
                <div class="tile__body sms-table-wrap table-wrap"><%= CourseSplit %></div>
            </div>
        </div>
    </div>

    <div class="tile mt-3" data-widget="announce">
        <div class="tile__head"><div>Latest announcements</div><a class="imbtn" href="/Admin/Announcements.aspx">Manage</a></div>
        <div class="tile__body"><%= AnnouncementList %></div>
    </div>
    </div>
</asp:Content>

