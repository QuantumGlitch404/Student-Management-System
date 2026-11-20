<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Index.aspx.cs" Inherits="StudentManagementSystem.Help.HelpCenter" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Help and guides</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
        <%= FlashHtml %>
        <div class="page-head">
            <div>
                <h1>Help and guides</h1>
                <p>Short answers, written around the task you are trying to finish.</p>
            </div>
        </div>
        <form method="get" action="/Help/Index.aspx" class="srch-bar mb-2">
          <input type="search" name="q" value="<%= Attr(Q) %>" class="form-control form-control-sm" placeholder="Search help: timetable, fees, password..." aria-label="Search help"/>
          <select name="lang" class="form-select form-select-sm" style="max-width:150px"><option value="en"<%= LangSel("en") %>>English</option><option value="hi"<%= LangSel("hi") %>>Hindi</option><option value="hinglish"<%= LangSel("hinglish") %>>Hinglish</option></select>
          <button class="btn btn-sms btn-sm">Search</button>
        </form>
        <form method="post" action="/Handlers/ProfileNotify.ashx" class="mb-3"><%= CsrfField() %><input type="hidden" name="return" value="/Help/Index.aspx"/>
          <button class="btn btn-outline-sms btn-sm">Ask the administrator</button>
          <span class="small text-muted ms-1">Write an article for a task that is missing.</span>
        </form>
        <%= SearchHtml %>
        <%= ArticleHtml %>
        <div class="bento">
            <div class="c-4"><div class="tile h-100"><div class="tile__head">Display and accessibility</div><div class="tile__body">
                <p>Use <strong>A- A A+</strong> in the top bar for text size and <strong>- 100% +</strong> for interface zoom. Both are saved to your account.</p>
                <p>Ctrl+Alt+Plus, Ctrl+Alt+Minus and Ctrl+Alt+0 also work. Motion can be reduced in <a href="/Account/Settings.aspx?tab=appearance">Settings &gt; Appearance</a>.</p>
            </div></div></div>
            <div class="c-4"><div class="tile h-100"><div class="tile__head">Find anything</div><div class="tile__body">
                <p>Press <strong>Ctrl+K</strong> or <strong>/</strong> to jump to any page. The sidebar search filters the visible links.</p>
                <p>The notification ticker shows unread notices; pause it if you need more reading time.</p>
            </div></div></div>
            <div class="c-4"><div class="tile h-100"><div class="tile__head">Your account</div><div class="tile__body">
                <p>Password, sign-in, Google linking and session options live in <a href="/Account/Settings.aspx?tab=account">Settings &gt; Account</a>.</p>
                <p>If a page says you do not have access, ask an administrator - roles are enforced on the server, not just hidden in the menu.</p>
            </div></div></div>
        </div>
        <%= FaqHtml %>
        <%= RoleGuideHtml %>
    </div>
</asp:Content>

