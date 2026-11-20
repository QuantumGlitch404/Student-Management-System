/* Dashboard widget visibility, stored per browser. */
(function () {
    "use strict";
    var btn = document.getElementById("dashCustomise");
    var panel = document.getElementById("dashWidgetPanel");
    if (!btn || !panel) return;
    var KEY = "sms.dash.hidden." + (document.body.getAttribute("data-module") || "overview");

    function read() {
        try { return JSON.parse(localStorage.getItem(KEY) || "[]") || []; } catch (e) { return []; }
    }
    function write(list) {
        try { localStorage.setItem(KEY, JSON.stringify(list)); } catch (e) { /* private mode */ }
    }

    function apply() {
        var hidden = read();
        var widgets = document.querySelectorAll("[data-widget]");
        for (var i = 0; i < widgets.length; i++) {
            var key = widgets[i].getAttribute("data-widget");
            widgets[i].style.display = hidden.indexOf(key) >= 0 ? "none" : "";
        }
        var boxes = panel.querySelectorAll("[data-widget-toggle]");
        for (var b = 0; b < boxes.length; b++) {
            boxes[b].checked = hidden.indexOf(boxes[b].getAttribute("data-widget-toggle")) < 0;
        }
    }

    btn.addEventListener("click", function (e) {
        e.stopPropagation();
        var open = panel.hidden;
        panel.hidden = !open;
        btn.setAttribute("aria-expanded", open ? "true" : "false");
    });
    panel.addEventListener("click", function (e) { e.stopPropagation(); });
    document.addEventListener("click", function () {
        if (!panel.hidden) { panel.hidden = true; btn.setAttribute("aria-expanded", "false"); }
    });
    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape" && !panel.hidden) { panel.hidden = true; btn.setAttribute("aria-expanded", "false"); btn.focus(); }
    });
    panel.addEventListener("change", function (e) {
        var box = e.target.closest("[data-widget-toggle]");
        if (!box) return;
        var key = box.getAttribute("data-widget-toggle");
        var hidden = read();
        var at = hidden.indexOf(key);
        if (!box.checked && at < 0) hidden.push(key);
        if (box.checked && at >= 0) hidden.splice(at, 1);
        write(hidden);
        apply();
    });

    apply();
})();