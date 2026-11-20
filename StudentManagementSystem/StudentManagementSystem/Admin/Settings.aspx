<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Settings.aspx.cs" Inherits="StudentManagementSystem.Admin.Settings" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">Settings - Admin</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server"></asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <%= FlashHtml %>
    <div class="page-head">
        <div>
            <h1>System settings</h1>
            <p>Installation-level configuration: integrations, AppSettings values, health, AI model status and the FAQ library.</p>
        </div>
    </div>

    <div class="bento">
        <div class="c-12">
            <div class="alert alert-info small mb-0">Looking for theme, text size or notification categories? Those are personal preferences on <a href="/Account/Settings.aspx">your own settings page</a>. This page configures the installation.</div>
        </div>
        <div class="c-4">
            <div class="tile mb-3"><div class="tile__head">Integration status (Web.config appSettings)</div><div class="tile__body sms-table-wrap table-wrap"><%= ConfigTable %></div></div>
            <div class="tile"><div class="tile__head">Test message delivery</div><div class="tile__body">
                <form method="post" action="/Admin/Settings.aspx" class="form-grid">
                    <%= CsrfField() %><input type="hidden" name="op" value="testemail"/>
                    <div class="c-8"><input name="email" type="email" required value="<%= E(AdminEmail) %>" class="form-control form-control-sm" placeholder="destination email"/></div>
                    <div class="c-4"><button class="btn btn-sms btn-sm w-100">Send test</button></div>
                    <div class="c-12 mt-1"><span class="small text-muted">SMS test needs <code>Sms:ApiKey</code> configured first (see docs).</span></div>
                </form>
            </div></div>
        </div>

        <div class="c-8">
            <div class="tile mb-3"><div class="tile__head">Integrations, registration and payments</div><div class="tile__body">
                <%= IntegrationsHtml %>
            </div></div>

            <div class="tile mb-3"><div class="tile__head">Application settings (AppSettings table)</div><div class="tile__body">
                <form method="post" action="/Admin/Settings.aspx">
                    <%= CsrfField() %><input type="hidden" name="op" value="savesettings"/>
                    <div class="sms-table-wrap table-wrap"><%= AppSettingRows %></div>
                    <button class="btn btn-sms btn-sm mt-2">Save settings</button>
                </form>
            </div></div>

            <div class="tile mb-3"><div class="tile__head">Application and system health</div><div class="tile__body sms-table-wrap table-wrap"><%= SystemTable %></div></div>

            <div class="tile mb-3"><div class="tile__head">Chatbot FAQ library (offline answers first, LLM second)</div><div class="tile__body">
                <div class="sms-table-wrap table-wrap"><%= FaqTable %></div>
                <form method="post" action="/Admin/Settings.aspx" class="srch-bar mt-2">
                    <%= CsrfField() %><input type="hidden" name="op" value="upsertfaq"/>
                    <input type="hidden" name="faqId" value="0"/>
                    <input name="question" required class="form-control form-control-sm" style="max-width:220px" placeholder="Question" maxlength="150"/>
                    <input name="answer" required class="form-control form-control-sm" style="max-width:320px" placeholder="Answer" maxlength="800"/>
                    <input name="keywords" class="form-control form-control-sm" style="max-width:180px" placeholder="fee|payment" maxlength="150"/>
                    <select name="roleScope" class="form-select form-select-sm" style="max-width:120px">
                        <option value="all">All roles</option>
                        <option value="student">Student</option>
                        <option value="faculty">Faculty</option>
                    </select>
                    <button class="btn btn-sms btn-sm">Add FAQ</button>
                </form>
            </div></div>
        </div>
    </div>
    </div>
</asp:Content>




