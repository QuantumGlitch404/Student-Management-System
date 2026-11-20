/* ------------------------------------------------------------------
   Delegated display-preference controller.
   Any page can opt in with:
     <button data-pref-action="zoom-in">
     <select data-setting="motion">
   The controller saves a JSON blob for the pre-paint script, writes the
   cookie mirror, and POSTs to /Handlers/Settings.ashx with keepalive.
   ------------------------------------------------------------------ */
(function () {
    "use strict";

    var root = document.documentElement;
    var MIN_FONT = 85, MAX_FONT = 140, MIN_ZOOM = 80, MAX_ZOOM = 150;
    var J = "sms_prefs";

    function read() {
        var p = {};
        try { p = JSON.parse(localStorage.getItem(J) || "{}") || {}; } catch (e) { p = {}; }
        try {
            var m = document.cookie.match(/(?:^|; )sms_prefs=([^;]*)/);
            if (m) {
                var c = JSON.parse(decodeURIComponent(m[1]) || "{}") || {};
                for (var k in c) if (Object.prototype.hasOwnProperty.call(c, k)) p[k] = c[k];
            }
        } catch (e2) { /* ignore */ }
        return p;
    }

    function clamp(n, min, max, step) {
        n = parseInt(n, 10);
        if (isNaN(n)) n = min <= 100 && max >= 100 ? 100 : min;
        n = Math.max(min, Math.min(max, n));
        return step ? Math.round(n / step) * step : n;
    }

    function apply(p) {
        if (p.theme && /^(light|dark|eye|system)$/.test(p.theme)) {
            var t = p.theme === "system"
                ? (window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light")
                : p.theme;
            root.setAttribute("data-theme", t);
            root.setAttribute("data-theme-pref", p.theme);
        }
        if (p.zoom !== undefined) {
            var z = clamp(p.zoom, MIN_ZOOM, MAX_ZOOM, 10);
            root.setAttribute("data-zoom", String(z));
            root.style.setProperty("--app-zoom", String(z / 100));
        }
        if (p.textScale !== undefined) {
            var f = clamp(p.textScale, MIN_FONT, MAX_FONT, 5);
            root.setAttribute("data-font", String(f));
            root.style.setProperty("--text-scale", String(f / 100));
        }
        if (p.motion === "reduced" || p.motion === "full") root.setAttribute("data-motion", p.motion);
        if (p.density === "compact" || p.density === "comfortable") root.setAttribute("data-density", p.density);
        root.setAttribute("data-contrast", p.highContrast ? "high" : "normal");
        root.classList.toggle("sms-sidebar-collapsed", !!p.sidebarCollapsed);
    }

    function save(patch) {
        var p = read();
        for (var k in patch) if (Object.prototype.hasOwnProperty.call(patch, k)) p[k] = patch[k];
        p.theme = p.theme || root.getAttribute("data-theme-pref") || "light";
        p.zoom = clamp(p.zoom === undefined ? root.getAttribute("data-zoom") : p.zoom, MIN_ZOOM, MAX_ZOOM, 10);
        p.textScale = clamp(p.textScale === undefined ? root.getAttribute("data-font") : p.textScale, MIN_FONT, MAX_FONT, 5);
        p.motion = p.motion || root.getAttribute("data-motion") || "full";
        p.sidebarCollapsed = root.classList.contains("sms-sidebar-collapsed") ? 1 : 0;
        p.highContrast = root.getAttribute("data-contrast") === "high" ? 1 : 0;
        try { localStorage.setItem(J, JSON.stringify(p)); } catch (e) { /* private mode */ }
        try {
            document.cookie = J + "=" + encodeURIComponent(JSON.stringify(p)) +
                ";path=/;max-age=31536000;samesite=lax";
            document.cookie = "sms_theme=" + encodeURIComponent(p.theme) + ";path=/;max-age=31536000;samesite=lax";
            document.cookie = "sms_zoom=" + p.zoom + ";path=/;max-age=31536000;samesite=lax";
            document.cookie = "sms_font=" + p.textScale + ";path=/;max-age=31536000;samesite=lax";
            document.cookie = "sms_motion=" + encodeURIComponent(p.motion) + ";path=/;max-age=31536000;samesite=lax";
            document.cookie = "sms_sidebar=" + (p.sidebarCollapsed ? "1" : "0") + ";path=/;max-age=31536000;samesite=lax";
        } catch (e2) { /* ignore */ }

        var body = new URLSearchParams();
        for (var key in patch) if (Object.prototype.hasOwnProperty.call(patch, key)) body.set(key, patch[key]);
        if (!body.toString()) return;
        fetch("/Handlers/Settings.ashx", {
            method: "POST",
            credentials: "same-origin",
            keepalive: true,
            headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
            body: body.toString()
        }).catch(function () { /* cookie + localStorage still hold the choice */ });
    }

    function current() {
        return {
            theme: root.getAttribute("data-theme-pref") || "light",
            zoom: clamp(root.getAttribute("data-zoom") || 100, MIN_ZOOM, MAX_ZOOM, 10),
            textScale: clamp(root.getAttribute("data-font") || 100, MIN_FONT, MAX_FONT, 5),
            motion: root.getAttribute("data-motion") || "full"
        };
    }

    function act(name, el) {
        var c = current();
        switch (name) {
            case "zoom-in": save({ zoom: c.zoom + 10 }); break;
            case "zoom-out": save({ zoom: c.zoom - 10 }); break;
            case "zoom-reset": save({ zoom: 100 }); break;
            case "font-in": save({ textScale: c.textScale + 5 }); break;
            case "font-out": save({ textScale: c.textScale - 5 }); break;
            case "font-reset": save({ textScale: 100 }); break;
            case "theme-cycle":
                save({ theme: c.theme === "light" ? "dark" : c.theme === "dark" ? "eye" : "light" });
                break;
            case "motion-toggle": save({ motion: c.motion === "reduced" ? "full" : "reduced" }); break;
            case "density-toggle": save({ density: root.getAttribute("data-density") === "compact" ? "comfortable" : "compact" }); break;
            case "sidebar-toggle": save({ sidebarCollapsed: root.classList.contains("sms-sidebar-collapsed") ? 0 : 1 }); break;
            default:
                if (el && el.getAttribute("data-pref-theme")) save({ theme: el.getAttribute("data-pref-theme") });
        }
        apply(read());
        syncControls();
    }

    function syncControls() {
        var p = read(), c = current();
        var zl = document.querySelectorAll("[data-sms-zoom-label]");
        for (var i = 0; i < zl.length; i++) zl[i].textContent = c.zoom + "%";
        var fl = document.querySelectorAll("[data-sms-font-label]");
        for (var j = 0; j < fl.length; j++) fl[j].textContent = c.textScale + "%";
        var z = document.querySelectorAll("[data-setting='zoom']");
        for (var k = 0; k < z.length; k++) z[k].value = c.zoom;
        var f = document.querySelectorAll("[data-setting='textScale'], [name='fontScale']");
        for (var m = 0; m < f.length; m++) f[m].value = c.textScale;
        var mo = document.querySelectorAll("[data-setting='motion'], [name='motion']");
        for (var n = 0; n < mo.length; n++) {
            if (mo[n].type === "checkbox") mo[n].checked = c.motion === "reduced";
            else if (mo[n].tagName === "SELECT") mo[n].value = c.motion;
        }
        var mt = document.querySelectorAll("[data-pref-action='motion-toggle']");
        for (var q = 0; q < mt.length; q++) {
            mt[q].textContent = c.motion === "reduced" ? "Motion: reduced" : "Motion: full";
            mt[q].setAttribute("aria-pressed", c.motion === "reduced" ? "true" : "false");
        }
        var compact = root.getAttribute("data-density") === "compact";
        var dt = document.querySelectorAll("[data-pref-action='density-toggle']");
        for (var r = 0; r < dt.length; r++) {
            dt[r].textContent = compact ? "Density: compact" : "Density: comfortable";
            dt[r].setAttribute("aria-pressed", compact ? "true" : "false");
        }
    }

    document.addEventListener("click", function (e) {
        var el = e.target && e.target.closest ? e.target.closest("[data-pref-action]") : null;
        if (!el) return;
        e.preventDefault();
        act(el.getAttribute("data-pref-action"), el);
    });

    document.addEventListener("change", function (e) {
        var el = e.target;
        if (!el || !el.getAttribute) return;
        var key = el.getAttribute("data-setting");
        if (!key) return;
        var patch = {};
        if (el.type === "checkbox") patch[key] = el.checked ? 1 : 0;
        else patch[key] = el.value;
        save(patch);
        apply(read());
        syncControls();
    });

    apply(read());
    syncControls();
    window.SMS = window.SMS || {};
    window.SMS.prefs = { get: read, apply: apply, save: save };
})();
