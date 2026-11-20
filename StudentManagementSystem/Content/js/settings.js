/* ------------------------------------------------------------------
   Settings page helpers: warn before leaving with unsaved changes,
   reveal the sticky save bar, preview theme choices and enable the
   loading state on the save button that was pressed.
   ------------------------------------------------------------------ */
(function () {
    "use strict";

    var dirty = false;
    var forms = Array.prototype.slice.call(document.querySelectorAll(".settings-body form"));

    forms.forEach(function (form) {
        var bar = form.querySelector("[data-settings-savebar]");

        function markDirty() {
            dirty = true;
            if (bar) bar.hidden = false;
        }

        form.addEventListener("input", markDirty);
        form.addEventListener("change", function (e) {
            markDirty();
            var el = e.target;
            if (!el || !window.SMS) return;
            if (el.hasAttribute("data-settings-theme") && window.SMS.theme) window.SMS.theme(el.value, false);
            if (el.name === "fontScale" && window.SMS.font) window.SMS.font(parseInt(el.value, 10), false);
            if (el.name === "zoom" && window.SMS.zoom) window.SMS.zoom(parseInt(el.value, 10), false);
            if (el.name === "motion" && window.SMS.motion) window.SMS.motion(el.value, false);
            if (el.name === "highContrast") rootToggle("data-contrast", el.checked ? "high" : "normal");
            if (el.name === "sidebarCollapsed") {
                document.documentElement.classList.toggle("sms-sidebar-collapsed", el.checked);
            }
        });
        form.addEventListener("click", function (e) {
            var step = e.target && e.target.closest ? e.target.closest("[data-settings-step]") : null;
            if (!step) return;
            e.preventDefault();
            var key = step.getAttribute("data-settings-step");
            var dir = parseInt(step.getAttribute("data-dir"), 10) || 1;
            if (key === "font" && window.SMS && window.SMS.font) {
                var input = form.querySelector("[name='fontScale']");
                var next = Math.max(85, Math.min(140, (parseInt(input && input.value, 10) || 100) + dir * 5));
                if (input) input.value = next;
                window.SMS.font(next, false);
            } else if (key === "zoom" && window.SMS && window.SMS.zoom) {
                var zi = form.querySelector("[name='zoom']");
                var zn = Math.max(80, Math.min(150, (parseInt(zi && zi.value, 10) || 100) + dir * 10));
                if (zi) zi.value = zn;
                window.SMS.zoom(zn, false);
            }
            markDirty();
        });

        form.addEventListener("submit", function () {
            dirty = false;
            if (bar) bar.hidden = true;
            var btn = form.querySelector('button[type="submit"].btn-sms');
            if (btn) {
                btn.setAttribute("data-loading", "true");
                btn.disabled = true;
                // Re-enable if the post is blocked by validation.
                setTimeout(function () { btn.removeAttribute("data-loading"); btn.disabled = false; }, 8000);
            }
        });

        var discard = form.querySelector("[data-settings-discard]");
        if (discard) {
            discard.addEventListener("click", function () {
                form.reset();
                if (bar) bar.hidden = true;
                dirty = false;
                var selected = form.querySelector("input[data-settings-theme]:checked");
                if (selected && window.SMS && window.SMS.theme) {
                    window.SMS.theme(selected.value, false);
                }
            });
        }
    });

    window.addEventListener("beforeunload", function (e) {
        if (!dirty) return;
        e.preventDefault();
        e.returnValue = "";
        return "";
    });

    // In-page section links should not trigger the prompt.
    document.querySelectorAll(".settings-nav a").forEach(function (a) {
        a.addEventListener("click", function () { dirty = false; });
    });

    function rootToggle(attr, value) {
        document.documentElement.setAttribute(attr, value);
    }
})();
