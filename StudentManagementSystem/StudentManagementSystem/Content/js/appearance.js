/* ------------------------------------------------------------------
   Student Management System - appearance controls
   Theme: light | dark | eye | system
   Text size: 85..140 in 5% steps
   Interface zoom: 80..150 in 10% steps (independent of text size)
   Motion: full | reduced
   Values are applied to <html> immediately, mirrored in localStorage
   for a flash-free repeat visit, and persisted to UserPreferences for
   signed-in users. ASCII only.
   ------------------------------------------------------------------ */
(function () {
    "use strict";

    var root = document.documentElement;
    var FONT_MIN = 85, FONT_MAX = 140, FONT_STEP = 5;
    var ZOOM_MIN = 80, ZOOM_MAX = 150, ZOOM_STEP = 10;
    var STORE = "sms.appearance";

    function readStore() {
        try { return JSON.parse(localStorage.getItem(STORE) || "{}") || {}; } catch (e) { return {}; }
    }
    function writeStore(next) {
        try { localStorage.setItem(STORE, JSON.stringify(next)); } catch (e) { /* private mode */ }
    }

    function clampFont(n) {
        n = parseInt(n, 10);
        if (isNaN(n)) n = 100;
        return Math.min(FONT_MAX, Math.max(FONT_MIN, Math.round(n / FONT_STEP) * FONT_STEP));
    }
    function clampZoom(n) {
        n = parseInt(n, 10);
        if (isNaN(n)) n = 100;
        return Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, Math.round(n / ZOOM_STEP) * ZOOM_STEP));
    }

    var systemDark = false;
    try {
        var mq = window.matchMedia("(prefers-color-scheme: dark)");
        systemDark = mq.matches;
        if (mq.addEventListener) mq.addEventListener("change", function (e) {
            systemDark = e.matches;
            if (root.getAttribute("data-theme-pref") === "system") applyTheme("system", false);
        });
    } catch (e) { /* very old browser */ }

    function resolved(pref) {
        if (pref === "system") return systemDark ? "dark" : "light";
        if (pref === "eye") return "eye";
        return pref === "dark" ? "dark" : "light";
    }

    function themeLabel(pref) {
        return pref === "dark" ? "Switch to eye protection mode"
            : pref === "eye" ? "Switch to light mode"
            : "Switch to dark mode";
    }

    function applyTheme(pref, persist) {
        if (pref !== "dark" && pref !== "system" && pref !== "eye") pref = "light";
        root.setAttribute("data-theme", resolved(pref));
        root.setAttribute("data-theme-pref", pref);

        var btn = document.getElementById("smsThemeToggle");
        if (btn) {
            var label = themeLabel(pref);
            btn.setAttribute("title", label);
            btn.setAttribute("aria-label", label);
            btn.setAttribute("aria-pressed", pref === "dark" ? "true" : "false");
        }
        var meta = document.querySelector('meta[name="theme-color"]');
        if (meta) {
            var th = root.getAttribute("data-theme");
            meta.setAttribute("content", th === "dark" ? "#0E0E10" : th === "eye" ? "#EFE8D2" : "#F4F3EF");
        }
        var choices = document.querySelectorAll("[data-sms-theme]");
        for (var i = 0; i < choices.length; i++) {
            var on = choices[i].getAttribute("data-sms-theme") === pref;
            choices[i].classList.toggle("active", on);
            choices[i].setAttribute("aria-pressed", on ? "true" : "false");
        }
        if (persist) save({ theme: pref });
    }

    function applyFont(scale, persist) {
        scale = clampFont(scale);
        root.setAttribute("data-font", String(scale));
        root.style.setProperty("--text-scale", String(scale / 100));
        var downs = document.querySelectorAll("#smsFontDown, #smsFontDownDD");
        var ups = document.querySelectorAll("#smsFontUp, #smsFontUpDD");
        var resets = document.querySelectorAll("#smsFontReset, #smsFontResetDD");
        for (var d = 0; d < downs.length; d++) downs[d].disabled = scale <= FONT_MIN;
        for (var u = 0; u < ups.length; u++) ups[u].disabled = scale >= FONT_MAX;
        for (var r = 0; r < resets.length; r++) {
            resets[r].setAttribute("title", "Reset text size (currently " + scale + "%)");
            resets[r].setAttribute("aria-label", "Reset text size, currently " + scale + " percent");
            resets[r].textContent = "A";
        }
        var labels = document.querySelectorAll("[data-sms-font-label]");
        for (var i = 0; i < labels.length; i++) labels[i].textContent = scale + "%";
        if (persist) save({ font: scale });
    }

    function applyZoom(zoom, persist) {
        zoom = clampZoom(zoom);
        root.setAttribute("data-zoom", String(zoom));
        root.style.setProperty("--app-zoom", String(zoom / 100));
        var downs = document.querySelectorAll("#smsZoomDown, #smsZoomDownDD");
        var ups = document.querySelectorAll("#smsZoomUp, #smsZoomUpDD");
        var resets = document.querySelectorAll("#smsZoomReset, #smsZoomResetDD");
        for (var d = 0; d < downs.length; d++) downs[d].disabled = zoom <= ZOOM_MIN;
        for (var u = 0; u < ups.length; u++) ups[u].disabled = zoom >= ZOOM_MAX;
        for (var r = 0; r < resets.length; r++) {
            resets[r].textContent = zoom + "%";
            resets[r].setAttribute("title", "Reset interface zoom (currently " + zoom + "%)");
            resets[r].setAttribute("aria-label", "Reset interface zoom, currently " + zoom + " percent");
        }
        var labels = document.querySelectorAll("[data-sms-zoom-label]");
        for (var i = 0; i < labels.length; i++) labels[i].textContent = zoom + "%";
        if (persist) save({ zoom: zoom });
    }

    function applyMotion(mode, persist) {
        mode = mode === "reduced" ? "reduced" : "full";
        root.setAttribute("data-motion", mode);
        if (persist) save({ motion: mode });
    }

    /* The cookie is written synchronously on click, so the very next request
       already sees the new value even if the user navigates immediately. The
       server save is fire-and-forget with keepalive so a navigation cannot
       cancel it. localStorage mirrors the same choice for the pre-paint script. */
    var COOKIE_MAX_AGE = 31536000;
function writeCookie(name, value) {
        try {
            document.cookie = name + "=" + encodeURIComponent(value) +
                ";path=/;max-age=" + COOKIE_MAX_AGE + ";samesite=lax";
        } catch (e) { /* cookies disabled */ }
    }

    function readCookie(name) {
        try {
            var m = document.cookie.match(new RegExp("(?:^|; )" + name + "=([^;]*)"));
            return m ? decodeURIComponent(m[1]) : null;
        } catch (e) { return null; }
    }

    function save(patch) {
        var stored = readStore();
        for (var k in patch) if (Object.prototype.hasOwnProperty.call(patch, k)) stored[k] = patch[k];
        writeStore(stored);

        /* One canonical JSON blob for the pre-paint script on the next request. */
        var canonical = {
            theme: stored.theme || root.getAttribute("data-theme-pref") || "light",
            zoom: clampZoom(stored.zoom || root.getAttribute("data-zoom") || 100),
            textScale: clampFont(stored.font || root.getAttribute("data-font") || 100),
            motion: stored.motion || root.getAttribute("data-motion") || "full",
            density: stored.density || root.getAttribute("data-density") || "comfortable",
            sidebarCollapsed: root.classList.contains("sms-sidebar-collapsed") ? 1 : 0,
            highContrast: root.getAttribute("data-contrast") === "high" ? 1 : 0
        };
        try { localStorage.setItem("sms_prefs", JSON.stringify(canonical)); } catch (e2) { /* private mode */ }
        writeCookie("sms_prefs", JSON.stringify(canonical));

        if (patch.theme !== undefined) writeCookie("sms_theme", patch.theme);
        if (patch.font !== undefined) writeCookie("sms_font", patch.font);
        if (patch.motion !== undefined) writeCookie("sms_motion", patch.motion);
        if (patch.zoom !== undefined) writeCookie("sms_zoom", patch.zoom);

        var body = new URLSearchParams();
        if (patch.theme !== undefined) body.set("theme", patch.theme);
        if (patch.font !== undefined) body.set("font", patch.font);
        if (patch.motion !== undefined) body.set("motion", patch.motion);
        if (patch.zoom !== undefined) body.set("zoom", patch.zoom);
        if (!body.toString()) return;
        fetch("/Handlers/Preferences.ashx", {
            method: "POST",
            credentials: "same-origin",
            keepalive: true,
            headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
            body: body.toString()
        }).catch(function () { /* offline: cookie + localStorage still hold the choice */ });
    }

    /* A partial postback re-renders markup, not <html>. Re-assert the cookie
       anyway for UpdatePanel/async scenarios and any script that touched it. */
    function reassertFromCookie() {
        try {
            var m = document.cookie.match(/(?:^|; )sms_theme=([^;]*)/);
            var t = m ? decodeURIComponent(m[1]) : "";
            if (t === "light" || t === "dark" || t === "eye") {
                root.setAttribute("data-theme", t);
                root.setAttribute("data-theme-pref", t);
                var choices = document.querySelectorAll("[data-sms-theme]");
                for (var i = 0; i < choices.length; i++) {
                    var on = choices[i].getAttribute("data-sms-theme") === t;
                    choices[i].classList.toggle("active", on);
                    choices[i].setAttribute("aria-pressed", on ? "true" : "false");
                }
            }
            var zm = document.cookie.match(/(?:^|; )sms_zoom=([^;]*)/);
            if (zm) applyZoom(parseInt(decodeURIComponent(zm[1]), 10), false);
            var fm = document.cookie.match(/(?:^|; )sms_font=([^;]*)/);
            if (fm) applyFont(parseInt(decodeURIComponent(fm[1]), 10), false);
        } catch (e) { /* ignore */ }
    }
    window.SMS_REASSERT_THEME = reassertFromCookie;

    function currentFont() { return clampFont(root.getAttribute("data-font") || 100); }
    function currentZoom() { return clampZoom(root.getAttribute("data-zoom") || 100); }

    /* --------------------------- panel --------------------------- */

    var panel, accessBtn;
    function openPanel(open) {
        if (!panel) return;
        panel.hidden = !open;
        if (accessBtn) accessBtn.setAttribute("aria-expanded", open ? "true" : "false");
    }

    /* ------------------------------- wire ------------------------------- */

    function bind() {
        panel = document.getElementById("smsAccessPanel");
        accessBtn = document.getElementById("smsAccessBtn");
        if (accessBtn && panel) {
            accessBtn.addEventListener("click", function (e) {
                e.stopPropagation();
                openPanel(panel.hidden);
            });
            document.addEventListener("click", function (e) {
                if (!panel.hidden && !panel.contains(e.target) && e.target !== accessBtn) openPanel(false);
            });
            document.addEventListener("keydown", function (e) {
                if (e.key === "Escape" && !panel.hidden) { openPanel(false); accessBtn.focus(); }
            });
        }

        var themeBtn = document.getElementById("smsThemeToggle");
        if (themeBtn) themeBtn.addEventListener("click", function () {
            var current = root.getAttribute("data-theme-pref") || "light";
            var next = current === "light" ? "dark" : current === "dark" ? "eye" : "light";
            applyTheme(next, true);
        });

        function onAll(selector, fn) {
            var list = document.querySelectorAll(selector);
            for (var i = 0; i < list.length; i++) list[i].addEventListener("click", fn);
        }
        onAll("#smsFontDown, #smsFontDownDD", function () { applyFont(currentFont() - FONT_STEP, true); });
        onAll("#smsFontUp, #smsFontUpDD", function () { applyFont(currentFont() + FONT_STEP, true); });
        onAll("#smsFontReset, #smsFontResetDD", function () { applyFont(100, true); });
        onAll("#smsZoomDown, #smsZoomDownDD", function () { applyZoom(currentZoom() - ZOOM_STEP, true); });
        onAll("#smsZoomUp, #smsZoomUpDD", function () { applyZoom(currentZoom() + ZOOM_STEP, true); });
        onAll("#smsZoomReset, #smsZoomResetDD", function () { applyZoom(100, true); });

        var themeChoices = document.querySelectorAll("[data-sms-theme]");
        for (var t = 0; t < themeChoices.length; t++) {
            themeChoices[t].addEventListener("click", function () {
                applyTheme(this.getAttribute("data-sms-theme"), true);
            });
        }
        var fontSteps = document.querySelectorAll("[data-sms-font]");
        for (var f = 0; f < fontSteps.length; f++) {
            fontSteps[f].addEventListener("click", function () {
                var d = parseInt(this.getAttribute("data-sms-font"), 10) || 0;
                applyFont(d === 0 ? 100 : currentFont() + d, true);
            });
        }
        var zoomSteps = document.querySelectorAll("[data-sms-zoom]");
        for (var z = 0; z < zoomSteps.length; z++) {
            zoomSteps[z].addEventListener("click", function () {
                var d = parseInt(this.getAttribute("data-sms-zoom"), 10) || 0;
                applyZoom(d === 0 ? 100 : currentZoom() + d, true);
            });
        }

        var motionToggle = document.getElementById("smsMotionToggle");
        if (motionToggle) {
            motionToggle.addEventListener("change", function () {
                applyMotion(motionToggle.checked ? "reduced" : "full", true);
            });
        }

        var navToggle = document.getElementById("smsSidebarToggle");
        if (navToggle && !navToggle.hasAttribute("data-static")) {
            navToggle.addEventListener("click", function () { collapseSidebar(!root.classList.contains("sms-sidebar-collapsed")); });
        }

        /* Sidebar collapse is a client preference; restore it before paint of the next page. */
        var local = readStore();
        var sbCookie = readCookie("sms_sidebar");
        var sbOn = sbCookie !== null ? sbCookie === "1" : !!local.sidebar;
        root.classList.toggle("sms-sidebar-collapsed", sbOn);

        /* Server-rendered values win on first paint. */
        if (!root.getAttribute("data-theme-pref") && local.theme) applyTheme(local.theme, false);
        else applyTheme(root.getAttribute("data-theme-pref") || root.getAttribute("data-theme") || "light", false);
        if (!root.getAttribute("data-font") && local.font) applyFont(local.font, false);
        else applyFont(currentFont(), false);
        if (!root.getAttribute("data-zoom") && local.zoom) applyZoom(local.zoom, false);
        else applyZoom(currentZoom(), false);
        applyMotion(root.getAttribute("data-motion") || local.motion || "full", false);

        if (motionToggle) motionToggle.checked = root.getAttribute("data-motion") === "reduced";

        /* Ctrl+Alt +/- and Ctrl+Alt+0 work on every page, including the test runner. */
        document.addEventListener("keydown", function (e) {
            if (!e.ctrlKey || !e.altKey) return;
            if (e.key === "+" || e.key === "=") { e.preventDefault(); applyZoom(currentZoom() + ZOOM_STEP, true); }
            else if (e.key === "-" || e.key === "_") { e.preventDefault(); applyZoom(currentZoom() - ZOOM_STEP, true); }
            else if (e.key === "0") { e.preventDefault(); applyZoom(100, true); }
        });

        /* Keep the current page visible in a long, independently scrolling sidebar. */
        var active = document.querySelector(".sidebar-nav .sms-link.active, .sidebar-nav .sms-link[aria-current='page']");
        if (active && active.scrollIntoView) {
            try { active.scrollIntoView({ block: "nearest" }); } catch (e2) { active.scrollIntoView(false); }
        }
    }

    function collapseSidebar(on) {
        root.classList.toggle("sms-sidebar-collapsed", on);
        document.cookie = "sms_sidebar=" + (on ? "1" : "0") + ";path=/;max-age=31536000;samesite=lax";
        var btn = document.getElementById("smsSidebarToggle");
        if (btn) btn.setAttribute("aria-pressed", on ? "true" : "false");
        writeStore(Object.assign(readStore(), { sidebar: on ? 1 : 0 }));
    }

    /* Small helper reused by notifications for feedback. */
    function toast(message) {
        var wrap = document.querySelector(".sms-toast-wrap");
        if (!wrap) {
            wrap = document.createElement("div");
            wrap.className = "sms-toast-wrap";
            wrap.setAttribute("role", "status");
            wrap.setAttribute("aria-live", "polite");
            document.body.appendChild(wrap);
        }
        var bad = /could not|error|failed|invalid|cannot/i.test(message);
        var warn = /stop|nothing/i.test(message);
        var status = bad ? "bad" : (warn ? "warn" : "");
        var el = document.createElement("div");
        el.className = "sms-toast" + (status ? " " + status : "");
        var head = document.createElement("div");
        head.className = "sms-toast-head";
        var title = document.createElement("strong");
        title.textContent = bad ? "Error" : (warn ? "Notice" : "Done");
        var close = document.createElement("button");
        close.type = "button";
        close.className = "sms-toast-close";
        close.setAttribute("aria-label", "Dismiss notification");
        close.textContent = "\u00d7";
        close.addEventListener("click", function () { if (el.parentNode) el.parentNode.removeChild(el); });
        head.appendChild(title);
        head.appendChild(close);
        var body = document.createElement("div");
        body.className = "sms-toast-msg";
        body.textContent = message;
        var bar = document.createElement("div");
        bar.className = "sms-toast-bar";
        el.appendChild(head);
        el.appendChild(body);
        el.appendChild(bar);
        wrap.appendChild(el);
        setTimeout(function () {
            el.classList.add("leaving");
            setTimeout(function () { if (el.parentNode) el.parentNode.removeChild(el); }, 120);
        }, 5000);
    }

    window.SMS = window.SMS || {};
    window.SMS.theme = applyTheme;
    window.SMS.font = applyFont;
    window.SMS.zoom = applyZoom;
    window.SMS.motion = applyMotion;
    window.SMS.toast = toast;

    function wirePartialPostbacks() {
        try {
            if (window.Sys && Sys.WebForms && Sys.WebForms.PageRequestManager) {
                Sys.WebForms.PageRequestManager.getInstance().add_endRequest(reassertFromCookie);
            }
        } catch (e) { /* no UpdatePanel on this page */ }
        if (document.addEventListener) {
            document.addEventListener("endRequest", reassertFromCookie, true);
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", function () { bind(); wirePartialPostbacks(); });
    } else {
        bind();
        wirePartialPostbacks();
    }
})();


