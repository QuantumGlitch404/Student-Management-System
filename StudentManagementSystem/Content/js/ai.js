/* ------------------------------------------------------------------
   Student Management System - offline assistant (full page)
   Talks only to same-origin handlers, which in turn talk to the local
   llama.cpp server on 127.0.0.1. No cloud endpoint is involved.
   ------------------------------------------------------------------ */
(function () {
    "use strict";

    var shell = document.getElementById("aiShell");
    if (!shell) return;

    var log = document.getElementById("aiLog");
    var input = document.getElementById("aiInput");
    var sendBtn = document.getElementById("aiSend");
    var stopBtn = document.getElementById("aiStop");
    var titleEl = document.getElementById("aiTitle");
    var metaEl = document.getElementById("aiMeta");
    var convs = document.getElementById("aiConvs");
    var csrf = shell.getAttribute("data-csrf") || "";
    var state = {
        conv: parseInt(shell.getAttribute("data-conv"), 10) || 0,
        generating: false,
        abort: null,
        lastUser: "",
        context: shell.getAttribute("data-context") === "1"
    };

    /* ----------------------------- helpers ----------------------------- */

    function esc(s) {
        return String(s == null ? "" : s)
            .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;").replace(/'/g, "&#39;");
    }

    function inline(text) {
        return text
            .replace(/`([^`\n]+)`/g, "<code>$1</code>")
            .replace(/\*\*([^*\n]+)\*\*/g, "<strong>$1</strong>")
            .replace(/(^|[^*])\*([^*\n]+)\*/g, "$1<em>$2</em>")
            .replace(/\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g, function (m, label, url) {
                return "<a href=\"" + url + "\" target=\"_blank\" rel=\"noopener noreferrer\">" + label + "</a>";
            });
    }

    /* A deliberately small Markdown subset: headings, lists, tables,
       fenced code, inline code, bold, italic and safe links. The input is
       escaped first, so model output can never inject HTML or script. */
    function markdown(raw) {
        var lines = esc(raw).split(/\r?\n/);
        var out = [], i = 0, inCode = false, code = [], listType = null, table = [];

        function closeList() { if (listType) { out.push("</" + listType + ">"); listType = null; } }
        function flushTable() {
            if (!table.length) return;
            var html = "<table>";
            for (var r = 0; r < table.length; r++) {
                if (r === 1 && /^\s*\|?[\s:|-]+\|?\s*$/.test(table[r])) continue;
                var cells = table[r].replace(/^\s*\|/, "").replace(/\|\s*$/, "").split("|");
                var tag = r === 0 ? "th" : "td";
                html += "<tr>";
                for (var c = 0; c < cells.length; c++) html += "<" + tag + ">" + inline(cells[c].trim()) + "</" + tag + ">";
                html += "</tr>";
            }
            out.push(html + "</table>");
            table = [];
        }

        while (i < lines.length) {
            var line = lines[i];
            if (/^```/.test(line)) {
                if (inCode) { out.push("<pre><code>" + code.join("\n") + "</code></pre>"); code = []; inCode = false; }
                else { closeList(); flushTable(); inCode = true; }
                i++; continue;
            }
            if (inCode) { code.push(line); i++; continue; }

            if (/^\s*\|.*\|\s*$/.test(line)) { closeList(); table.push(line); i++; continue; }
            if (table.length) flushTable();

            if (/^\s*$/.test(line)) { closeList(); i++; continue; }
            var h = line.match(/^(#{1,4})\s+(.*)$/);
            if (h) { closeList(); out.push("<h4>" + inline(h[2]) + "</h4>"); i++; continue; }
            if (/^\s*([-*_])\1{2,}\s*$/.test(line)) { closeList(); out.push("<hr/>"); i++; continue; }
            if (/^\s*[-*+]\s+/.test(line)) {
                if (listType !== "ul") { closeList(); out.push("<ul>"); listType = "ul"; }
                out.push("<li>" + inline(line.replace(/^\s*[-*+]\s+/, "")) + "</li>"); i++; continue;
            }
            if (/^\s*\d+[.)]\s+/.test(line)) {
                if (listType !== "ol") { closeList(); out.push("<ol>"); listType = "ol"; }
                out.push("<li>" + inline(line.replace(/^\s*\d+[.)]\s+/, "")) + "</li>"); i++; continue;
            }
            if (/^\s*>\s?/.test(line)) {
                closeList();
                out.push("<blockquote><p>" + inline(line.replace(/^\s*>\s?/, "")) + "</p></blockquote>"); i++; continue;
            }
            closeList();
            var para = [line];
            i++;
            while (i < lines.length && !/^\s*$/.test(lines[i]) && !/^```/.test(lines[i]) &&
                   !/^\s*[-*+]\s+/.test(lines[i]) && !/^\s*\d+[.)]\s+/.test(lines[i]) &&
                   !/^#{1,4}\s/.test(lines[i]) && !/^\s*\|.*\|\s*$/.test(lines[i])) {
                para.push(lines[i]); i++;
            }
            out.push("<p>" + inline(para.join(" ")) + "</p>");
        }
        if (inCode) out.push("<pre><code>" + code.join("\n") + "</code></pre>");
        closeList();
        flushTable();
        return out.join("\n");
    }

    function renderBubbles(root) {
        var bubbles = (root || document).querySelectorAll(".ai-bubble[data-raw]");
        for (var i = 0; i < bubbles.length; i++) {
            bubbles[i].innerHTML = markdown(bubbles[i].getAttribute("data-raw") || "");
            bubbles[i].style.whiteSpace = "normal";
        }
    }

    function scrollBottom() {
        if (log) log.scrollTop = log.scrollHeight;
    }

    function toast(msg) {
        if (window.SMS && window.SMS.toast) window.SMS.toast(msg);
    }

    function post(url, params) {
        var body = new URLSearchParams();
        for (var k in params) if (Object.prototype.hasOwnProperty.call(params, k)) body.set(k, params[k]);
        return fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8", "X-CSRF": csrf },
            body: body.toString()
        }).then(function (r) { return r.json(); });
    }

    /* --------------------------- page context -------------------------- */

    function visibleContext() {
        if (!state.context) return "Page context is disabled in settings.";
        var current = location.pathname;
        if (/\/chatbot\/index\.aspx/i.test(current)) {
            try {
                var saved = sessionStorage.getItem("sms.lastPageContext");
                return saved ? "The user opened the assistant from another page. Last page they were viewing:\n" + saved : "";
            } catch (e) { return ""; }
        }
        var bits = ["Path: " + current, "Title: " + document.title];
        var head = document.querySelector(".page-head h1, .hero-panel h1, h1");
        if (head) bits.push("Heading: " + head.textContent.trim());
        var subs = document.querySelectorAll(".page-sub, .hero-sub, .card-header span:first-child");
        var picks = [];
        for (var i = 0; i < subs.length && picks.length < 10; i++) {
            var t = (subs[i].textContent || "").trim().replace(/\s+/g, " ");
            if (t) picks.push(t);
        }
        if (picks.length) bits.push("Sections: " + picks.join("; "));
        var filters = [];
        var fields = document.querySelectorAll(".filter-strip select, .filter-strip input, .srch-bar select, .srch-bar input");
        for (var f = 0; f < fields.length && filters.length < 8; f++) {
            var name = fields[f].getAttribute("name");
            if (!name || !fields[f].value) continue;
            filters.push(name + "=" + fields[f].value);
        }
        if (filters.length) bits.push("Selected filters: " + filters.join(", "));
        var table = document.querySelector(".app-content table, .sms-content table");
        if (table) {
            var rows = table.querySelectorAll("tr");
            var lines = [];
            for (var r = 0; r < rows.length && lines.length < 7; r++) {
                var cells = rows[r].querySelectorAll("th,td");
                var vals = [];
                for (var c = 0; c < cells.length && vals.length < 8; c++) {
                    var v = (cells[c].textContent || "").trim().replace(/\s+/g, " ");
                    if (v) vals.push(v);
                }
                if (vals.length) lines.push(vals.join(" | "));
            }
            if (lines.length) bits.push("Visible table (first rows):\n" + lines.join("\n"));
        }
        return bits.join("\n").substring(0, 2200);
    }

    /* ---------------------------- DOM append --------------------------- */

    function appendMessage(role, text, meta) {
        var me = role === "user";
        var wrap = document.createElement("div");
        wrap.className = "ai-msg " + (me ? "me" : "bot");
        var who = me ? "You" : "SMS Assistant";
        wrap.innerHTML =
            "<div class='ai-avatar'>" + (me ? "You" : "AI") + "</div>" +
            "<div class='ai-bubble-wrap'><div class='ai-who'>" + who + " &middot; just now</div>" +
            "<div class='ai-bubble' style='white-space:pre-wrap'></div>" +
            "<div class='ai-actions'><button type='button' class='ai-act' data-copy='1'><i class='bi bi-clipboard'></i> Copy</button>" +
            (me ? "" : "<button type='button' class='ai-act' data-regen='1'><i class='bi bi-arrow-clockwise'></i> Regenerate</button>") +
            "</div>" + (meta ? "<div class='ai-meta'>" + esc(meta) + "</div>" : "") + "</div>";
        var bubble = wrap.querySelector(".ai-bubble");
        bubble.setAttribute("data-raw", text);
        bubble.textContent = text;
        log.appendChild(wrap);
        renderBubbles(wrap);
        scrollBottom();
        return wrap;
    }

    function typing() {
        var wrap = document.createElement("div");
        wrap.className = "ai-msg bot";
        wrap.id = "aiTyping";
        wrap.innerHTML = "<div class='ai-avatar'>AI</div><div class='ai-bubble-wrap'><div class='ai-who'>SMS Assistant</div>" +
                         "<div class='ai-bubble'><span class='ai-typing'><span></span><span></span><span></span></span></div></div>";
        log.appendChild(wrap);
        scrollBottom();
    }

    function removeTyping() {
        var t = document.getElementById("aiTyping");
        if (t && t.parentNode) t.parentNode.removeChild(t);
    }

    function welcomeNode() {
        return log.querySelector(".ai-welcome");
    }

    function setBusy(on) {
        state.generating = on;
        if (sendBtn) sendBtn.hidden = on;
        if (stopBtn) stopBtn.hidden = !on;
        if (input) input.disabled = on;
        sendBtn && sendBtn.setAttribute("disabled", on ? "disabled" : "");
    }

    function setStatus(stateName, text) {
        var dots = [document.getElementById("aiHeadDot"), document.getElementById("aiSideDot")];
        for (var i = 0; i < dots.length; i++) if (dots[i]) dots[i].className = "ai-dot " + (stateName || "");
        var t = document.getElementById("aiStatusText");
        if (t && text) t.textContent = text;
    }

    /* ------------------------------ sending ---------------------------- */

    function ensureConversation() {
        if (state.conv > 0) return Promise.resolve(state.conv);
        return post("/Handlers/AiConversations.ashx", { op: "new" }).then(function (r) {
            if (!r.ok) throw new Error(r.error || "Could not create a conversation.");
            state.conv = r.id;
            shell.setAttribute("data-conv", String(r.id));
            try { history.replaceState(null, "", "/Chatbot/Index.aspx?id=" + r.id); } catch (e) { /* file URLs */ }
            return r.id;
        });
    }

    function send(text, reuse) {
        text = (text || "").trim();
        if (!text && !reuse) return;
        if (state.generating) return;

        var welcome = welcomeNode();
        if (welcome && welcome.parentNode) welcome.parentNode.removeChild(welcome);

        setBusy(true);
        setStatus("busy", "Thinking locally...");
        metaEl && (metaEl.textContent = "local model is working");

        if (!reuse) {
            var last = log.querySelectorAll(".ai-msg.me");
            for (var i = 0; i < last.length; i++) {
                var btns = last[i].querySelectorAll("[data-regen]");
                for (var b = 0; b < btns.length; b++) btns[b].parentNode.removeChild(btns[b]);
            }
            appendMessage("user", text, "");
            state.lastUser = text;
        }
        typing();

        var controller = null;
        try { controller = new AbortController(); state.abort = controller; } catch (e) { /* older browser */ }
        var started = Date.now();

        ensureConversation().then(function (id) {
            var body = new URLSearchParams();
            body.set("conversationId", String(id));
            body.set("message", reuse ? state.lastUser : text);
            if (reuse) body.set("reuse", "1");
            body.set("page", location.pathname);
            body.set("pageTitle", document.title);
            body.set("context", visibleContext());

            return fetch("/Handlers/AiChat.ashx", {
                method: "POST",
                credentials: "same-origin",
                signal: controller ? controller.signal : undefined,
                headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8", "X-CSRF": csrf },
                body: body.toString()
            });
        }).then(function (r) { return r.json(); }).then(function (data) {
            removeTyping();
            if (!data.ok) {
                appendMessage("assistant", "**The assistant could not answer.**\n\n" + (data.error || "Unknown error.") +
                    "\n\nYou can still use the page you are on, or try again.", "");
                setStatus("off", "Model unavailable");
            } else {
                if (data.conversationId && data.conversationId !== state.conv) state.conv = data.conversationId;
                appendMessage("assistant", data.answer, (data.elapsedMs ? (data.elapsedMs / 1000).toFixed(1) + "s \u00b7 " +
                    (data.completionTokens || 0) + " tokens \u00b7 local" : ""));
                if (data.title) titleEl && (titleEl.textContent = data.title);
                setStatus("ok", "Local model ready");
                metaEl && (metaEl.textContent = "answered in " + ((Date.now() - started) / 1000).toFixed(1) + "s \u00b7 offline");
            }
            loadConversations();
        }).catch(function (err) {
            removeTyping();
            if (err && err.name === "AbortError") {
                appendMessage("assistant", "_Generation stopped._ The local model may finish the current answer in the background.", "");
                toast("Stopped");
            } else {
                appendMessage("assistant", "**Connection problem.** " + (err && err.message ? err.message : "The request failed.") +
                    "\n\nCheck the assistant status on this page. If it says starting, wait for the local model; if it names a missing file, reinstall the AI component.", "");
            }
            setStatus("off", "Model unavailable");
        }).then(function () {
            state.abort = null;
            setBusy(false);
            if (input) { input.value = ""; autoGrow(); input.focus({ preventScroll: true }); }
            scrollBottom();
        });
    }

    /* --------------------------- conversations ------------------------- */

    function relative(iso) {
        var d = new Date(iso), diff = (Date.now() - d.getTime()) / 60000;
        if (diff < 1) return "just now";
        if (diff < 60) return Math.floor(diff) + "m ago";
        if (diff < 1440) return Math.floor(diff / 60) + "h ago";
        if (diff < 10080) return Math.floor(diff / 1440) + "d ago";
        return d.toLocaleDateString();
    }

    function loadConversations(q) {
        var url = "/Handlers/AiConversations.ashx" + (q ? "?q=" + encodeURIComponent(q) : "");
        fetch(url, { credentials: "same-origin" }).then(function (r) { return r.json(); }).then(function (data) {
            if (!data.ok) return;
            if (!data.items.length) {
                convs.innerHTML = "<li class='ai-empty-convs'>" + (q ? "No conversation matches that search." : "No conversations yet. Start with a question.") + "</li>";
                return;
            }
            var html = "";
            for (var i = 0; i < data.items.length; i++) {
                var c = data.items[i];
                var active = c.id === state.conv;
                html += "<li class='ai-conv" + (active ? " active" : "") + "' data-id='" + c.id + "' data-title='" + esc(c.title) + "' data-pinned='" + (c.pinned ? "1" : "0") + "'>" +
                    "<span class='ai-conv-ico'><i class='bi " + (c.pinned ? "bi-pin-angle-fill" : "bi-chat-square-text") + "'></i></span>" +
                    "<span class='ai-conv-body'><span class='ai-conv-title'>" + esc(c.title) + "</span>" +
                    "<span class='ai-conv-time'>" + relative(c.last) + " &middot; " + c.messages + " msg</span></span>" +
                    "<button class='ai-conv-menu' type='button' aria-label='Conversation actions'><i class='bi bi-three-dots'></i></button>" +
                    "<div class='ai-conv-menu-pop' role='menu'>" +
                    "<button type='button' data-act='rename'><i class='bi bi-pencil'></i> Rename</button>" +
                    "<button type='button' data-act='pin'><i class='bi bi-pin-angle'></i> " + (c.pinned ? "Unpin" : "Pin") + "</button>" +
                    "<button type='button' data-act='delete' class='danger'><i class='bi bi-trash3'></i> Delete</button>" +
                    "</div></li>";
            }
            convs.innerHTML = html;
        }).catch(function () { /* leave existing list */ });
    }

    function actOnConversation(id, act) {
        if (act === "rename") {
            var item = convs.querySelector("[data-id='" + id + "']");
            var current = item ? item.getAttribute("data-title") : "";
            var name = window.prompt("Rename conversation", current);
            if (name === null) return;
            name = name.trim();
            if (!name) { toast("Title cannot be empty"); return; }
            post("/Handlers/AiConversations.ashx", { op: "rename", id: id, title: name }).then(function (r) {
                if (!r.ok) { toast(r.error || "Could not rename"); return; }
                loadConversations();
                if (id === state.conv && titleEl) titleEl.textContent = r.title;
                toast("Renamed");
            });
        } else if (act === "pin") {
            var node = convs.querySelector("[data-id='" + id + "']");
            var pinned = node && node.getAttribute("data-pinned") === "1" ? "0" : "1";
            post("/Handlers/AiConversations.ashx", { op: "pin", id: id, pinned: pinned }).then(function (r) {
                if (r.ok) { loadConversations(); toast(pinned === "1" ? "Pinned" : "Unpinned"); }
            });
        } else if (act === "delete") {
            if (!window.confirm("Delete this conversation and all of its messages?")) return;
            post("/Handlers/AiConversations.ashx", { op: "delete", id: id }).then(function (r) {
                if (!r.ok) { toast(r.error || "Could not delete"); return; }
                if (id === state.conv) location.href = "/Chatbot/Index.aspx";
                else { loadConversations(); toast("Conversation deleted"); }
            });
        }
    }

    /* ------------------------------ events ----------------------------- */

    function autoGrow() {
        if (!input) return;
        input.style.height = "auto";
        input.style.height = Math.min(input.scrollHeight, 150) + "px";
    }

    if (input) {
        input.addEventListener("input", autoGrow);
        input.addEventListener("keydown", function (e) {
            if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(input.value, false); }
        });
    }
    if (sendBtn) sendBtn.addEventListener("click", function () { send(input.value, false); });
    if (stopBtn) stopBtn.addEventListener("click", function () {
        if (state.abort) state.abort.abort();
    });

    document.getElementById("aiNew").addEventListener("click", function () {
        if (state.generating && state.abort) state.abort.abort();
        post("/Handlers/AiConversations.ashx", { op: "new" }).then(function (r) {
            if (r.ok) location.href = "/Chatbot/Index.aspx?id=" + r.id;
        });
    });

    var search = document.getElementById("aiSearch");
    var searchTimer = null;
    if (search) search.addEventListener("input", function () {
        clearTimeout(searchTimer);
        var q = search.value.trim();
        searchTimer = setTimeout(function () { loadConversations(q); }, 260);
    });

    convs.addEventListener("click", function (e) {
        var menuBtn = e.target.closest(".ai-conv-menu");
        var item = e.target.closest(".ai-conv");
        if (!item) return;
        var id = parseInt(item.getAttribute("data-id"), 10);
        if (menuBtn) {
            e.stopPropagation();
            var pop = item.querySelector(".ai-conv-menu-pop");
            var open = pop.classList.contains("open");
            var all = convs.querySelectorAll(".ai-conv-menu-pop.open");
            for (var i = 0; i < all.length; i++) all[i].classList.remove("open");
            if (!open) pop.classList.add("open");
            return;
        }
        var act = e.target.closest("[data-act]");
        if (act) { e.stopPropagation(); actOnConversation(id, act.getAttribute("data-act")); return; }
        if (!state.generating) location.href = "/Chatbot/Index.aspx?id=" + id;
    });

    document.addEventListener("click", function (e) {
        if (!e.target.closest(".ai-conv-menu") && !e.target.closest(".ai-conv-menu-pop")) {
            var all = convs.querySelectorAll(".ai-conv-menu-pop.open");
            for (var i = 0; i < all.length; i++) all[i].classList.remove("open");
        }
    });

    log.addEventListener("click", function (e) {
        var copy = e.target.closest("[data-copy]");
        if (copy) {
            var bubble = copy.closest(".ai-bubble-wrap").querySelector(".ai-bubble");
            var text = bubble.getAttribute("data-raw") || bubble.textContent;
            if (navigator.clipboard) navigator.clipboard.writeText(text).then(function () { toast("Copied"); });
            else {
                var ta = document.createElement("textarea");
                ta.value = text; document.body.appendChild(ta); ta.select();
                try { document.execCommand("copy"); toast("Copied"); } catch (err) { /* ignore */ }
                document.body.removeChild(ta);
            }
            return;
        }
        var regen = e.target.closest("[data-regen]");
        if (regen) {
            var msg = regen.closest(".ai-msg");
            if (msg && msg.parentNode) msg.parentNode.removeChild(msg);
            send(state.lastUser, true);
            return;
        }
        var suggest = e.target.closest("[data-suggest]");
        if (suggest) send(suggest.getAttribute("data-suggest"), false);
    });

    var clearBtn = document.getElementById("aiClear");
    if (clearBtn) clearBtn.addEventListener("click", function () {
        if (!state.conv) { toast("Nothing to clear yet"); return; }
        if (!window.confirm("Remove all messages from this conversation? The conversation stays in the sidebar.")) return;
        post("/Handlers/AiConversations.ashx", { op: "clear", id: state.conv }).then(function (r) {
            if (r.ok) location.reload();
        });
    });

    var deleteBtn = document.getElementById("aiDelete");
    if (deleteBtn) deleteBtn.addEventListener("click", function () {
        if (!state.conv) { toast("Nothing to delete yet"); return; }
        if (!window.confirm("Delete this conversation permanently?")) return;
        post("/Handlers/AiConversations.ashx", { op: "delete", id: state.conv }).then(function (r) {
            if (r.ok) location.href = "/Chatbot/Index.aspx";
        });
    });

    fetch("/Handlers/AiStatus.ashx", { credentials: "same-origin" }).then(function (r) { return r.json(); }).then(function (d) {
        if (!d.ok) return;
        if (d.state === "ready") setStatus("ok", "Local model ready");
        else if (d.error) setStatus("off", d.error);
        else setStatus("", "Model starts on first question");
    }).catch(function () { /* page still works */ });

    renderBubbles(document);
    scrollBottom();
    if (input && window.innerWidth > 900) {
        setTimeout(function () { input.focus({ preventScroll: true }); }, 120);
    }
})();

