(function () {
    "use strict";

    var MAP = {
        "mortarboard-fill": "mortarboard-fill", "mortarboard": "mortarboard",
        "speedometer2": "speedometer2", "inbox": "inbox", "clock-history": "clock-history",
        "stars": "stars", "people": "people", "journal-bookmark": "journal-bookmark",
        "calendar3": "calendar3", "calendar2-check": "calendar2-check", "calendar2-week": "calendar2-week",
        "clipboard-check": "clipboard-check", "journal-check": "journal-check",
        "box-arrow-in-down": "box-arrow-in-down", "tags": "tags", "cash-coin": "cash-coin",
        "bell": "bell", "megaphone": "megaphone", "flag": "flag", "gear": "gear",
        "pencil-square": "pencil-square", "person-gear": "person-gear", "sliders": "sliders",
        "sliders2": "sliders2", "box-arrow-right": "box-arrow-right", "list": "list",
        "moon-stars": "moon-stars", "sun": "sun", "eye": "eye", "display": "display",
        "phone-vibrate": "phone-vibrate", "arrows-angle-expand": "arrows-angle-expand",
        "arrow-right": "arrow-right", "arrow-up": "arrow-up", "arrow-clockwise": "arrow-clockwise",
        "arrow-counterclockwise": "arrow-counterclockwise", "three-dots": "three-dots",
        "chat-square-text": "chat-square-text", "pin-angle": "pin-angle", "pin-angle-fill": "pin-angle-fill",
        "pencil": "pencil", "trash3": "trash3", "clipboard": "clipboard", "stop-fill": "stop-fill",
        "plus-lg": "plus-lg", "eraser": "eraser", "window": "window", "search": "search",
        "funnel": "funnel", "grid-3x3-gap": "grid-3x3-gap", "file-earmark-pdf": "file-earmark-pdf",
        "file-pdf": "file-pdf", "file-earmark-excel": "file-earmark-excel", "filetype-csv": "filetype-csv",
        "download": "download", "upload": "upload", "people-fill": "people", "person-check": "person-check",
        "person-dash": "person-dash", "person-badge": "person-badge", "shield-check": "shield-check",
        "shield-lock": "shield-lock", "check2-circle": "check2-circle", "check2-square": "check2-square",
        "exclamation-triangle": "exclamation-triangle", "slash-circle": "slash-circle",
        "x-octagon": "x-octagon", "archive": "archive", "award": "award", "book": "book",
        "bug": "bug", "cash-stack": "cash-stack", "clipboard-data": "clipboard-data",
        "diagram-3": "diagram-3", "envelope-open": "envelope-open", "flag-fill": "flag",
        "graph-up-arrow": "graph-up-arrow", "heart-fill": "heart-fill", "hourglass-split": "hourglass-split",
        "journal-text": "journal-text", "list-check": "list-check", "palette": "palette",
        "pause-circle": "pause-circle", "phone": "phone-vibrate", "receipt": "receipt",
        "stopwatch": "stopwatch", "sun-fill": "sun", "ticket-detailed": "ticket-detailed",
        "universal-access": "universal-access", "google": "google", "lock": "lock"
    };

    var EXTRA = {
        "arrow-right": "arrow-right", "check": "check", "x": "x", "chevron-down": "chevron-down",
        "chevron-left": "chevron-left", "chevron-right": "chevron-right", "heart": "heart"
    };

    function iconName(biClass) {
        var name = biClass.replace(/^bi-/, "");
        return MAP[name] || EXTRA[name] || name;
    }

    function svgFor(name, extraClass) {
        var svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
        svg.setAttribute("class", "ic" + (extraClass ? " " + extraClass : ""));
        svg.setAttribute("aria-hidden", "true");
        var use = document.createElementNS("http://www.w3.org/2000/svg", "use");
        use.setAttribute("href", "/Content/icons/sprite.svg#i-" + name);
        svg.appendChild(use);
        return svg;
    }

    function upgradeIcon(el) {
        if (!el || el.nodeType !== 1) return;
        var cls = el.getAttribute("class") || "";
        if (!/\bbi(?:-|\b)/.test(cls)) return;
        var match = cls.match(/\bbi-([a-z0-9-]+)/);
        if (!match) return;
        var extra = cls.split(/\s+/).filter(function (c) {
            return c && c !== "bi" && c.indexOf("bi-") !== 0;
        }).join(" ");
        var svg = svgFor(iconName(match[0]), extra);
        if (el.parentNode) el.parentNode.replaceChild(svg, el);
    }

    function upgradeIcons(root) {
        var scope = root || document;
        var icons = scope.querySelectorAll ? scope.querySelectorAll("i.bi, i[class*=' bi-']") : [];
        Array.prototype.forEach.call(icons, upgradeIcon);
    }

    function observeIcons() {
        if (!window.MutationObserver) return;
        var observer = new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                var added = records[i].addedNodes;
                for (var j = 0; j < added.length; j++) {
                    var node = added[j];
                    if (node.nodeType !== 1) continue;
                    if (node.tagName === "I") upgradeIcon(node);
                    else if (node.querySelectorAll) upgradeIcons(node);
                }
            }
        });
        observer.observe(document.body, { childList: true, subtree: true });
    }

    function bindMobileNav() {
        var burger = document.getElementById("smsBurger");
        var sidebar = document.getElementById("smsSidebar");
        var scrim = document.getElementById("smsScrim");
        if (!burger || !sidebar || !scrim) return;

        function setOpen(open) {
            sidebar.classList.toggle("sms-open", open);
            scrim.classList.toggle("is-on", open);
            burger.setAttribute("aria-expanded", open ? "true" : "false");
            burger.setAttribute("aria-label", open ? "Close navigation menu" : "Open navigation menu");
            document.body.classList.toggle("nav-open", open);
        }

        burger.addEventListener("click", function () {
            window.setTimeout(function () { setOpen(sidebar.classList.contains("sms-open")); }, 0);
        });
        scrim.addEventListener("click", function () { setOpen(false); });
        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape" && sidebar.classList.contains("sms-open")) {
                setOpen(false);
                burger.focus();
            }
        });
        sidebar.addEventListener("click", function (e) {
            var link = e.target.closest ? e.target.closest("a.sms-link") : null;
            if (link && window.matchMedia("(max-width: 900px)").matches) setOpen(false);
        });
        window.addEventListener("resize", function () {
            if (window.innerWidth > 900) setOpen(false);
        });
    }

    function syncCollapseButton() {
        var root = document.documentElement;
        var btn = document.getElementById("smsSidebarToggle");
        if (!btn) return;
        var collapsed = root.classList.contains("sms-sidebar-collapsed");
        var use = btn.querySelector("use");
        if (use) use.setAttribute("href", "/Content/icons/sprite.svg#i-panel-left-" + (collapsed ? "open" : "close"));
        btn.setAttribute("aria-label", collapsed ? "Expand navigation" : "Collapse navigation");
        btn.setAttribute("title", collapsed ? "Expand navigation" : "Collapse navigation");
        btn.setAttribute("aria-pressed", collapsed ? "true" : "false");
    }

    function bindCollapse() {
        var btn = document.getElementById("smsSidebarToggle");
        if (!btn) return;
        syncCollapseButton();
        btn.addEventListener("click", function () {
            window.requestAnimationFrame(syncCollapseButton);
        });
    }

    function polishEmptyStates() {
        Array.prototype.forEach.call(document.querySelectorAll(".empty-state > i:first-child"), function (el) {
            upgradeIcon(el);
        });
    }

    function markTables() {
        Array.prototype.forEach.call(document.querySelectorAll(".sms-table-wrap > table"), function (table) {
            table.setAttribute("role", "table");
        });
    }

    function init() {
        upgradeIcons(document);
        polishEmptyStates();
        bindCollapse();
        markTables();
        observeIcons();
        document.documentElement.classList.add("sms-ready");
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();
