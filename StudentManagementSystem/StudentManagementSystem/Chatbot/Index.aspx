<%@ Page Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Index.aspx.cs" Inherits="StudentManagementSystem.Chatbot.Index" %>

<asp:Content ID="cHead" ContentPlaceHolderID="HeadContent" runat="server"></asp:Content>
<asp:Content ID="cTitle" ContentPlaceHolderID="TitleContent" runat="server">AI assistant</asp:Content>
<asp:Content ID="cScript" ContentPlaceHolderID="ScriptContent" runat="server">
    <script src="/Content/js/ai.js"></script>
</asp:Content>
<asp:Content ID="cMain" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page">
    <div class="page-head">
        <div>
            <h1>AI Assistant</h1>
            <p class="page-sub">Ask about this application and your own records. Answers run on the bundled local model, so your questions stay on this computer.</p>
        </div>
        <div class="page-head-actions">
            <span class="ai-context" title="The assistant receives the visible page and your role"><i class="bi bi-window"></i> Page context: <strong id="aiCtxState"><%= ContextEnabled ? "on" : "off" %></strong></span>
            <a class="btn btn-outline-sms btn-sm" href="/Account/Settings.aspx?tab=ai"><i class="bi bi-sliders"></i> Assistant settings</a>
        </div>
    </div>

    <%= DisabledHtml %>

    <div class="ai-shell chat-layout" id="aiShell" data-conv="<%= ActiveId %>" data-csrf="<%= Csrf() %>" data-role="<%= E(CurrentUser.RoleName) %>" data-page="<%= E(Request.Path) %>" data-context="<%= ContextEnabled ? "1" : "0" %>">
        <aside class="ai-side" aria-label="Conversations">
            <div class="ai-side-head">
                <button class="ai-new" id="aiNew" type="button"><i class="bi bi-plus-lg"></i> New chat</button>
                <div class="ai-search">
                    <i class="bi bi-search"></i>
                    <input id="aiSearch" type="search" placeholder="Search conversations" aria-label="Search conversations" autocomplete="off"/>
                </div>
            </div>
            <ul class="ai-convs" id="aiConvs"><%= ConversationListHtml %></ul>
            <div class="ai-side-foot">
                <span class="ai-dot <%= StatusDotClass %>" id="aiSideDot"></span>
                <span><%= E(StatusShort) %></span>
            </div>
        </aside>

        <section class="ai-main" aria-label="Conversation">
            <header class="ai-head">
                <div>
                    <div class="ai-head-title" id="aiTitle"><%= E(ActiveTitle) %></div>
                    <div class="ai-head-sub">
                        <span class="ai-dot <%= StatusDotClass %>" id="aiHeadDot"></span>
                        <span id="aiStatusText"><%= E(StatusShort) %></span>
                        <span aria-hidden="true">&middot;</span>
                        <span>Campus assistant</span>
                        <span aria-hidden="true">&middot;</span>
                        <span>private</span>
                    </div>
                </div>
                <div class="page-head-actions">
                    <button class="imbtn" id="aiClear" type="button" title="Remove all messages in this chat" aria-label="Clear conversation"><i class="bi bi-eraser"></i> Clear</button>
                    <button class="imbtn danger" id="aiDelete" type="button" title="Delete this conversation" aria-label="Delete conversation"><i class="bi bi-trash3"></i> Delete</button>
                </div>
            </header>

            <div class="ai-log" id="aiLog" role="log" aria-live="polite" aria-relevant="additions">
                <%= MessagesHtml %>
            </div>

            <div class="ai-compose">
                <div class="ai-inputwrap">
                    <textarea id="aiInput" rows="1" maxlength="2000" placeholder="Ask about attendance, results, fees, complaints or how to use the system..." aria-label="Message the assistant"></textarea>
                    <button class="ai-send" id="aiSend" type="button" aria-label="Send message" title="Send (Enter)"><i class="bi bi-arrow-up"></i></button>
                    <button class="ai-stop" id="aiStop" type="button" aria-label="Stop generating" title="Stop" hidden><i class="bi bi-stop-fill"></i></button>
                </div>
                <div class="ai-hint">
                    <span><kbd>Enter</kbd> send &middot; <kbd>Shift</kbd>+<kbd>Enter</kbd> new line &middot; answers come from a small local model and can be wrong</span>
                    <span id="aiMeta">&nbsp;</span>
                </div>
            </div>
        </section>
    </div>
    </div>
</asp:Content>


