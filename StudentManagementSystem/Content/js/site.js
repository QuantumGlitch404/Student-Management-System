/* Student Management System - main client script (ASCII only). */
(function () {
    "use strict";

    function $(sel) { return document.querySelector(sel); }
    function $all(sel) { return Array.prototype.slice.call(document.querySelectorAll(sel)); }

    function ready(fn) {
        if (document.readyState !== "loading") fn();
        else document.addEventListener("DOMContentLoaded", fn);
    }

    ready(function () {
        var burger = $("#smsBurger");
        var sidebar = $("#smsSidebar");
        var scrim = $("#smsScrim");

        function setSidebar(open) {
            if (!sidebar) return;
            sidebar.classList.toggle("sms-open", open);
            if (scrim) scrim.classList.toggle("is-on", open);
            if (burger) {
                burger.setAttribute("aria-expanded", open ? "true" : "false");
                burger.setAttribute("aria-label", open ? "Close navigation menu" : "Open navigation menu");
            }
            document.body.classList.toggle("nav-open", open);
        }

        if (burger && sidebar)
            burger.addEventListener("click", function () {
                setSidebar(!sidebar.classList.contains("sms-open"));
            });

        if (scrim)
            scrim.addEventListener("click", function () { setSidebar(false); });

        function setDisplay(open) {
            var wrap = $("#smsDisplayWrap");
            var btn = $("#smsDisplayBtn");
            if (!wrap) return;
            wrap.classList.toggle("is-open", open);
            if (btn) btn.setAttribute("aria-expanded", open ? "true" : "false");
        }

        var displayBtn = $("#smsDisplayBtn");
        var displayWrap = $("#smsDisplayWrap");
        if (displayBtn && displayWrap)
            displayBtn.addEventListener("click", function (e) {
                e.stopPropagation();
                setDisplay(!displayWrap.classList.contains("is-open"));
            });

        document.addEventListener("click", function (e) {
            if (displayWrap && !displayWrap.contains(e.target)) setDisplay(false);
        });

        document.addEventListener("keydown", function (e) {
            if (e.key !== "Escape") return;
            setSidebar(false);
            setDisplay(false);
        });

        window.addEventListener("resize", function () {
            if (window.innerWidth > 900) setSidebar(false);
        });

        /* Navigation filter: the topbar search on wide screens, the sidebar
           search on small screens. Filters the real role-aware sidebar links. */
        function filterNav(raw) {
            if (!sidebar) return;
            var q = (raw || "").trim().toLowerCase();
            var links = $all(".sms-link");
            var any = false;
            links.forEach(function (link) {
                var text = (link.textContent || "").toLowerCase();
                var match = !q || text.indexOf(q) !== -1;
                link.hidden = !match;
                if (match) any = true;
            });

            $all(".sidebar-navgroup").forEach(function (group) {
                var next = group.nextElementSibling;
                var groupHas = false;
                while (next && !next.classList.contains("sidebar-navgroup")) {
                    if (next.classList.contains("sms-link") && !next.hidden) groupHas = true;
                    next = next.nextElementSibling;
                }
                group.hidden = !!q && !groupHas;
            });

            var empty = $("#smsNavEmpty");
            if (empty) empty.hidden = !q || any;
            var nav = $(".sidebar-nav");
            if (nav) nav.hidden = !!q && !any;
        }

        $all("#smsGlobalSearch, #smsNavSearch").forEach(function (input) {
            input.addEventListener("input", function () { filterNav(input.value); });
            input.addEventListener("keydown", function (e) {
                if (e.key === "Escape") {
                    input.value = "";
                    filterNav("");
                }
            });
        });

        $all("#smsSearchForm").forEach(function (form) {
            form.addEventListener("submit", function (e) { e.preventDefault(); });
        });

        /* The bell is owned by notifications.js (panel + unread badge + polling). */

        /* auto-dismiss flash messages */
        $all(".alert-dismissible").forEach(function (el) {
            window.setTimeout(function () {
                if (el.classList.contains("show")) el.classList.remove("show");
                el.style.transition = "opacity 110ms linear";
                el.style.opacity = "0";
                window.setTimeout(function () { el.remove(); }, 120);
            }, 6000);
        });

        /* confirmation on destructive actions */
    
    /* scroll active sidebar link into view on load */
    var activeLink = $(".sms-link.active, .sms-link[aria-current='page']");
    var nav = $(".sidebar-nav");
    if (activeLink && nav) {
        activeLink.scrollIntoView({ block: "nearest", inline: "nearest" });
    }
    
    /* Bulk selection: a checkbox with data-check-all="formId" toggles every
       checkbox in that form, including ones linked via the form attribute. */
    document.addEventListener("change", function (e) {
        var master = e.target.closest("[data-check-all]");
        if (!master) return;
        var form = document.getElementById(master.getAttribute("data-check-all"));
        if (!form) return;
        var boxes = form.querySelectorAll("input[name='ids']");
        for (var i = 0; i < boxes.length; i++) boxes[i].checked = master.checked;
    });

    $all("form[data-confirm]").forEach(function (form) {
            form.addEventListener("submit", function (e) {
                if (!window.confirm(form.getAttribute("data-confirm"))) e.preventDefault();
            });
        });
    });

    /* The assistant is a real local model; its panel lives in ai-quick.js. */
})();
