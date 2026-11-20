/* Global command palette: pages from the sidebar plus common actions (Ctrl+K or "/"). */
(function () {
    "use strict";

    var dialog, input, results, items = [], filtered = [], activeIndex = 0;

    function $(id) { return document.getElementById(id); }

    function collect() {
        var seen = {};
        var list = [];
        Array.prototype.forEach.call(document.querySelectorAll(".sidebar-nav a.sms-link"), function (a) {
            var href = a.getAttribute("href");
            if (!href || seen[href]) return;
            seen[href] = true;
            var label = (a.textContent || "").trim();
            var tip = a.getAttribute("title") || label;
            list.push({ title: label, hint: tip, href: href, kind: "Page" });
        });
        var actions = [
            { title: "Create online test", hint: "Build a test and publish it", href: "/Admin/OnlineTests.aspx", kind: "Action" },
            { title: "Add student", hint: "Create a student account", href: "/Admin/Users.aspx", kind: "Action" },
            { title: "Take attendance", hint: "Mark attendance for a class", href: "/Admin/Attendance.aspx", kind: "Action" },
            { title: "Import students", hint: "Bulk import from a file", href: "/Admin/ImportExport.aspx", kind: "Action" },
            { title: "Send announcement", hint: "Notify students or faculty", href: "/Admin/Announcements.aspx", kind: "Action" },
            { title: "Collect fee", hint: "Record a payment", href: "/Admin/Payments.aspx", kind: "Action" },
            { title: "Change timezone", hint: "Language and time settings", href: "/Account/Settings.aspx#timezone", kind: "Setting" },
            { title: "Notifications and reminders", hint: "Unread items and tasks", href: "/Notifications/Index.aspx", kind: "Setting" },
            { title: "Appearance and dark mode", hint: "Theme, text size and zoom", href: "/Account/Settings.aspx#appearance", kind: "Setting" },
            { title: "My profile", hint: "Contact and academic details", href: "/Account/Settings.aspx#profile", kind: "Setting" }
        ];
        return list.concat(actions);
    }

    function render() {
        if (!results) return;
        results.innerHTML = "";
        if (!filtered.length) {
            var li = document.createElement("li");
            li.className = "cmd-empty";
            li.textContent = "No matching page or action.";
            results.appendChild(li);
            return;
        }
        filtered.slice(0, 30).forEach(function (item, index) {
            var li = document.createElement("li");
            li.className = "cmd-item" + (index === activeIndex ? " is-active" : "");
            li.setAttribute("role", "option");
            var a = document.createElement("a");
            a.href = item.href;
            var kind = document.createElement("span");
            kind.className = "cmd-kind";
            kind.textContent = item.kind;
            var title = document.createElement("span");
            title.className = "cmd-title";
            title.textContent = item.title;
            var hint = document.createElement("span");
            hint.className = "cmd-hint";
            hint.textContent = item.hint || "";
            a.appendChild(kind); a.appendChild(title); a.appendChild(hint);
            li.appendChild(a);
            li.addEventListener("mouseenter", function () { activeIndex = index; paintActive(); });
            results.appendChild(li);
        });
    }

    function paintActive() {
        Array.prototype.forEach.call(results.children, function (li, i) {
            if (li.classList) li.classList.toggle("is-active", i === activeIndex);
        });
    }

    function filter(value) {
        var q = (value || "").trim().toLowerCase();
        filtered = !q ? items.slice() : items.filter(function (item) {
            return (item.title + " " + item.hint + " " + item.kind).toLowerCase().indexOf(q) !== -1;
        });
        activeIndex = 0;
        render();
    }

    function open() {
        if (!dialog) return;
        filter("");
        if (typeof dialog.showModal === "function") { if (!dialog.open) dialog.showModal(); }
        else dialog.setAttribute("open", "open");
        window.setTimeout(function () { if (input) input.focus(); }, 20);
    }

    function close() {
        if (!dialog) return;
        if (dialog.open) dialog.close();
        else dialog.removeAttribute("open");
    }

    function init() {
        dialog = $("smsCmd");
        input = $("smsCmdInput");
        results = $("smsCmdResults");
        if (!dialog) return;
        items = collect();

        input.addEventListener("input", function () { filter(input.value); });
        input.addEventListener("keydown", function (e) {
            if (e.key === "ArrowDown") { e.preventDefault(); activeIndex = Math.min(activeIndex + 1, Math.min(filtered.length, 30) - 1); paintActive(); }
            else if (e.key === "ArrowUp") { e.preventDefault(); activeIndex = Math.max(activeIndex - 1, 0); paintActive(); }
            else if (e.key === "Enter") {
                var item = filtered[activeIndex];
                if (item) { e.preventDefault(); window.location.href = item.href; }
            }
        });

        dialog.addEventListener("click", function (e) { if (e.target === dialog) close(); });

        document.addEventListener("keydown", function (e) {
            var tag = (e.target && e.target.tagName) || "";
            var typing = tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT" || (e.target && e.target.isContentEditable);
            if ((e.ctrlKey || e.metaKey) && (e.key === "k" || e.key === "K")) { e.preventDefault(); open(); return; }
            if (!typing && e.key === "/") { e.preventDefault(); open(); return; }
            if (!typing && e.key === "?") { e.preventDefault(); if (window.SMS && window.SMS.toast) window.SMS.toast("Shortcuts: Ctrl+K or / to search, Esc to close"); }
        });

        var top = $("smsGlobalSearch");
        if (top) top.addEventListener("focus", function (e) { e.preventDefault(); top.blur(); open(); });
        var nav = $("smsNavSearch");
        if (nav) nav.addEventListener("focus", function (e) { e.preventDefault(); nav.blur(); open(); });

        var shortcut = document.createElement("button");
        shortcut.type = "button";
        shortcut.className = "cmd-launch";
        shortcut.setAttribute("aria-label", "Find anything");
        shortcut.title = "Find anything (Ctrl+K)";
        shortcut.innerHTML = "<svg class='ic' aria-hidden='true'><use href='/Content/icons/sprite.svg#i-search'></use></svg><span>Find</span><kbd>Ctrl K</kbd>";
        shortcut.addEventListener("click", open);
        var tools = document.querySelector(".topbar-tools");
        if (tools) tools.insertBefore(shortcut, tools.firstChild);
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();
