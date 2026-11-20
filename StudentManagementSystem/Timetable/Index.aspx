<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Index.aspx.cs" Inherits="StudentManagementSystem.Campus.TimetablePage" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server">
    <link href="/Content/css/pages/timetable.css" rel="stylesheet" />
</asp:Content>

<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Timetable</asp:Content>

<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server">
    <script src="/Content/js/patterns.js" defer></script>
</asp:Content>

<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page" data-pattern="split">
        <%= FlashHtml %>

        <div class="page-head">
            <div>
                <h1>Timetable</h1>
                <p><%= Intro %></p>
            </div>
            <div class="page-actions">
                <a class="btn btn-outline-sms btn-sm" href="/Calendar/Index.aspx"><i class="bi bi-calendar3"></i> Academic calendar</a>
                <button type="button" class="btn btn-outline-sms btn-sm focus-mode-toggle" id="focusMode" aria-label="Toggle focus mode">
                    <i class="bi bi-arrows-fullscreen"></i> Focus
                </button>
            </div>
        </div>

        <!-- P3 Context Bar: Course, Semester, Section selector -->
        <div class="context-bar" data-pattern="contextbar" role="toolbar" aria-label="Timetable scope">
            <asp:PlaceHolder ID="phContextBar" runat="server" />
        </div>

        <!-- P1 Split View: Left = Course list, Right = Weekly Grid -->
        <div class="split-view" id="splitView" runat="server" data-pattern="split" role="main" aria-label="Timetable workspace">
            <!-- Left Pane: Course/Section cards with search -->
            <div class="split-view__left" id="leftPane" runat="server">
                <div class="split-view__left-header">
                    <label class="form-label small" for="courseSearch">Courses & sections</label>
                    <input type="search" id="courseSearch" class="form-control form-control-sm" placeholder="Search course, section, faculty..." aria-label="Search courses" />
                </div>
                <div class="split-view__left-body" id="leftBody" runat="server">
                    <asp:PlaceHolder ID="phCourseList" runat="server" />
                </div>
                <div class="split-view__left-footer">
                    <asp:PlaceHolder ID="phLeftFooter" runat="server" />
                </div>
            </div>

            <!-- Right Pane: Weekly Grid with sticky header -->
            <div class="split-view__right" id="rightPane" runat="server">
                <div class="split-view__right-header" id="rightHeader" runat="server">
                    <div class="d-flex align-items-center gap-2 flex-wrap">
                        <div id="selectedCourseInfo" class="small text-muted">Select a course to view its timetable</div>
                        <div class="ms-auto d-flex gap-1">
                            <button type="button" class="btn btn-sm btn-outline-sms" id="btnFocusToggle" aria-label="Toggle focus mode" onclick="document.body.classList.toggle('is-focus-mode'); this.classList.toggle('is-active'); localStorage.setItem('focusMode', document.body.classList.contains('is-focus-mode'))">
                                <i class="bi bi-arrows-fullscreen"></i> Focus
                            </button>
                            <asp:PlaceHolder ID="phRightHeaderActions" runat="server" />
                        </div>
                    </div>
                </div>
                <div class="split-view__right-body" id="rightBody" runat="server">
                    <asp:PlaceHolder ID="phWeekGrid" runat="server" />
                    <asp:PlaceHolder ID="phAdminTools" runat="server" />
                </div>
            </div>
        </div>
    </div>
</asp:Content>