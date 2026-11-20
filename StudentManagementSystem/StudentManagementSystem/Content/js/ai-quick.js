/* ------------------------------------------------------------------
   Student Management System - contextual assistant panel
   Available on every signed-in page. It captures a compact, authorised
   view of the current page (headings, filters, first table rows) so the
   local model can answer "explain this page" style questions. The full
   conversation is still stored by the server and can be opened later.
   ------------------------------------------------------------------ */
(function () {
    "use strict";

    var path = location.pathname || "";

    function text(el) { return (el && el.textContent ? el.textContent : "").replace(/\s+/g, " ").trim(); }

    function captureContext() {
        var bits = ["Path: " + path, "Title: " + document.title];
        var head = document.querySelector(".page-head h1, .hero-panel h1, h1");
        if (head) bits.push("Heading: " + text(head));
        var sub = document.querySelector(".page-sub, .hero-sub");
        if (sub) bits.push("Description: " + text(sub));
        var sections = [];
        var cards = document.querySelectorAll(".card-header");
        for (var i = 0; i < cards.length && sections.length < 8; i++) {
            var t = text(cards[i]);
            if (t) sections.push(t);
        }
        if (sections.length) bits.push("Sections: " + sections.join("; "));
        var filters = [];
        var fields = document.querySelectorAll(".filter-strip select, .filter-strip input, .srch-bar select, .srch-bar input");
        for (var f = 0; f < fields.length && filters.length < 8; f++) {
            if (fields[f].name && fields[f].value) filters.push(fields[f].name + "=" + fields[f].value);
        }
        if (filters.length) bits.push("Filters: " + filters.join(", "));
        var table = document.querySelector(".app-content table, .sms-content table");
        if (table) {
            var rows = table.querySelectorAll("tr"), lines = [];
            for (var r = 0; r < rows.length && lines.length < 5; r++) {
                var cells = rows[r].querySelectorAll("th,td"), vals = [];
                for (var c = 0; c < cells.length && vals.length < 7; c++) {
                    var v = text(cells[c]);
                    if (v) vals.push(v);
                }
                if (vals.length) lines.push(vals.join(" | "));
            }
            if (lines.length) bits.push("Visible rows:\n" + lines.join("\n"));
        }
        return bits.join("\n").substring(0, 2000);
    }

    // Remember the last real page so the full assistant can use it too.
    try {
        if (!/\/chatbot\/index\.aspx/i.test(path)) {
            sessionStorage.setItem("sms.lastPageContext", captureContext());
        }
    } catch (e) { /* storage disabled */ }

    var panel = document.getElementById("smsChat");
    var fab = document.getElementById("smsChatFab");
    if (!panel || !fab) return;
    if (/\/chatbot\/index\.aspx/i.test(path)) { fab.style.display = "none"; panel.style.display = "none"; return; }

    var input = document.getElementById("smsChatInput");
    var sendBtn = document.getElementById("smsChatSend");
    var box = document.getElementById("smsChatMsgs");
    var csrf = panel.getAttribute("data-csrf") || "";
    var busy = false;
    var statusError = "";

    function convId() {
        try { return parseInt(sessionStorage.getItem("sms.aiConv") || "0", 10) || 0; } catch (e) { return 0; }
    }
    function setConv(id) {
        try { sessionStorage.setItem("sms.aiConv", String(id)); } catch (e) { /* ignore */ }
    }
    function esc(s) {
        return String(s == null ? "" : s).replace(/&/g, "&amp;").replace(/</g, "&lt;")
            .replace(/>/g, "&gt;").replace(/"/g, "&quot;");
    }
    function fmt(s) {
        var safe = esc(s);
        safe = safe.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>");
        var blocks = safe.split(/\n{2,}/);
        for (var i = 0; i < blocks.length; i++) {
            if (/^\s*[-*]\s/m.test(blocks[i])) {
                blocks[i] = "<ul>" + blocks[i].split(/\n/).map(function (l) {
                    return "<li>" + l.replace(/^\s*[-*]\s*/, "") + "</li>";
                }).join("") + "</ul>";
            } else {
                blocks[i] = "<p>" + blocks[i].replace(/\n/g, "<br/>") + "</p>";
            }
        }
        return blocks.join("");
    }
    function scroll() { box.scrollTop = box.scrollHeight; }
    function add(role, content, isHtml) {
        var d = document.createElement("div");
        d.className = "smc-msg " + (role === "user" ? "me" : "bot");
        d.innerHTML = "<span class='who'>" + (role === "user" ? "You" : "Assistant") + "</span><span>" +
            (isHtml ? content : esc(content)) + "</span>";
        box.appendChild(d);
        scroll();
        return d;
    }
    function toggle(on) {
        if (typeof on === "boolean") panel.classList.toggle("d-none", !on);
        else panel.classList.toggle("d-none");
        if (!panel.classList.contains("d-none") && input) input.focus();
    }
    function setStatus(kind, label) {
        var dot = document.getElementById("smsChatDot");
        var t = document.getElementById("smsChatStatus");
        if (dot) dot.className = "ai-dot " + (kind || "");
        if (t) t.textContent = label || "local";
    }

    function send(q) {
        q = (q || "").trim();
        if (!q || busy) return;
        var welcome = box.querySelector(".smc-welcome");
        if (welcome && welcome.parentNode) welcome.parentNode.removeChild(welcome);
        var sugg = box.querySelector(".smc-suggests");
        if (sugg && sugg.parentNode) sugg.parentNode.removeChild(sugg);

        busy = true;
        if (input) { input.value = ""; input.disabled = true; }
        if (sendBtn) sendBtn.disabled = true;
        add("user", q);
        var typingEl = add("bot", "<span class='ai-typing'><span></span><span></span><span></span></span>", true);
        setStatus("busy", "thinking");

        var body = new URLSearchParams();
        body.set("conversationId", String(convId()));
        body.set("message", q);
        body.set("page", path);
        body.set("pageTitle", document.title);
        body.set("context", captureContext());

        fetch("/Handlers/AiChat.ashx", {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8", "X-CSRF": csrf },
            body: body.toString()
        }).then(function (r) { return r.json(); }).then(function (data) {
            if (typingEl.parentNode) typingEl.parentNode.removeChild(typingEl);
            if (!data.ok) {
                add("bot", "**Could not answer:** " + (data.error || "unknown error"), true);
                setStatus("off", "unavailable");
            } else {
                if (data.conversationId) setConv(data.conversationId);
                var msg = add("bot", fmt(data.answer), true);
                if (data.conversationId) {
                    var link = document.createElement("a");
                    link.className = "smc-open-full";
                    link.href = "/Chatbot/Index.aspx?id=" + data.conversationId;
                    link.innerHTML = "Open in full assistant <i class='bi bi-arrow-right'></i>";
                    msg.appendChild(link);
                }
                setStatus("ok", "local");
            }
            scroll();
        }).catch(function () {
            if (typingEl.parentNode) typingEl.parentNode.removeChild(typingEl);
            add("bot", "Could not reach the assistant service. " + (statusError || "The local model may still be starting; wait a moment and try again, or open the full assistant page for the exact status."));
            setStatus("off", "error");
        }).then(function () {
            busy = false;
            if (input) { input.disabled = false; input.focus(); }
            if (sendBtn) sendBtn.disabled = false;
        });
    }

    fab.addEventListener("click", function () { toggle(); });
    var close = document.getElementById("smsChatClose");
    if (close) close.addEventListener("click", function () { toggle(false); });
    if (sendBtn) sendBtn.addEventListener("click", function () { send(input.value); });
    if (input) input.addEventListener("keydown", function (e) {
        if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(input.value); }
    });
    box.addEventListener("click", function (e) {
        var b = e.target.closest("[data-suggest]");
        if (b) send(b.getAttribute("data-suggest"));
    });
    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape" && !panel.classList.contains("d-none")) toggle(false);
    });

    function refreshStatus() {
        fetch("/Handlers/AiStatus.ashx", { credentials: "same-origin" }).then(function (r) { return r.json(); }).then(function (d) {
            if (!d.ok) { statusError = "The assistant endpoint rejected the request."; setStatus("off", "error"); return; }
            if (d.state === "ready") { statusError = ""; setStatus("ok", "ready"); return; }
            if (d.error) { statusError = d.error; setStatus("off", "error"); return; }
            statusError = "";
            setStatus("busy", d.modelPresent && d.runtimePresent ? "starting model" : "unavailable");
        }).catch(function () {
            statusError = "The local assistant service is not responding.";
            setStatus("off", "error");
        });
    }
    refreshStatus();
    setInterval(refreshStatus, 20000);
})();

