/* ------------------------------------------------------------------
   Student Management System - interactive form validation
   Works with the ASP.NET validators already declared on each page:
     * nothing is shown before the user interacts (or submits)
     * a field is validated when it loses focus
     * a touched field re-validates while typing
     * on submit the first invalid field is focused and scrolled to
     * server-side validators remain the authority (works without JS)
   ASCII only.
   ------------------------------------------------------------------ */
(function () {
    "use strict";

    var touched = {};
    var submitted = false;

    function allValidators() {
        return Array.isArray(window.Page_Validators) ? window.Page_Validators : [];
    }

    function validatorsFor(controlId) {
        return allValidators().filter(function (v) {
            return v && v.controltovalidate === controlId;
        });
    }

    function controlOf(v) {
        return v && v.controltovalidate
            ? document.getElementById(v.controltovalidate)
            : null;
    }

    /* Validators rendered with Display="Static" reserve their line and are
       hidden with visibility, so showing a message never moves the layout
       (and never makes a button slip away mid-click). */
    function isStatic(v) {
        return /visibility/.test(v.getAttribute("style") || "");
    }

    function isShown(v) {
        var cs = window.getComputedStyle(v);
        return cs.display !== "none" && cs.visibility !== "hidden";
    }

    function paint(v) {
        var ctrl = controlOf(v);
        var visible = (v.isvalid === false);

        if (visible) {
            v.classList.add("sms-invalid-msg");
            v.setAttribute("role", "alert");
        } else {
            v.classList.remove("sms-invalid-msg");
            v.removeAttribute("role");
        }

        if (isStatic(v)) {
            v.style.visibility = visible ? "visible" : "hidden";
        } else {
            v.style.display = visible ? "block" : "none";
        }

        if (ctrl) {
            var shell = ctrl.closest(".input-shell");
            if (visible) {
                ctrl.classList.add("is-invalid");
                ctrl.setAttribute("aria-invalid", "true");
                if (shell) shell.classList.add("is-invalid");
            } else {
                ctrl.classList.remove("is-invalid");
                ctrl.removeAttribute("aria-invalid");
                if (shell) shell.classList.remove("is-invalid");
            }
        }
    }

    function runValidator(v) {
        if (typeof window.ValidatorValidate === "function") {
            window.ValidatorValidate(v);
        }
        paint(v);
    }

    function validateField(ctrl) {
        var list = validatorsFor(ctrl.id);
        if (!list.length) return true;
        list.forEach(runValidator);
        return list.every(function (v) { return v.isvalid !== false; });
    }

    /* On a fresh GET every message must be hidden. After a server-side
       postback the server may already have decided some fields are invalid -
       keep those visible instead of clearing them. */
    function resetInitialState() {
        var serverInvalid = allValidators().some(function (v) {
            return v.isvalid === false || (!isStatic(v) && v.style.display !== "none");
        });

        if (!serverInvalid) {
            Array.prototype.forEach.call(
                document.querySelectorAll(".is-invalid, .input-validation-error, [aria-invalid='true']"),
                function (ctrl) {
                    ctrl.classList.remove("is-invalid", "input-validation-error");
                    ctrl.removeAttribute("aria-invalid");
                }
            );
        }

        var anyVisible = allValidators().some(isShown);

        allValidators().forEach(function (v) {
            if (!anyVisible) {
                paint(v);
                return;
            }

            if (!isShown(v)) return;

            // server-rendered error: keep it, and mark the field
            v.classList.add("sms-invalid-msg");
            var ctrl = controlOf(v);
            if (ctrl) {
                ctrl.classList.add("is-invalid");
                ctrl.setAttribute("aria-invalid", "true");
            }
        });
    }

    function focusFirstInvalid() {
        var bad = allValidators().filter(function (v) {
            return v.isvalid === false;
        });
        if (!bad.length) return;

        var target = controlOf(bad[0]);
        if (!target) return;

        try {
            target.focus({ preventScroll: true });
            if (typeof target.scrollIntoView === "function") {
                target.scrollIntoView({ block: "center", behavior: "smooth" });
            }
        } catch (e) {
            target.focus();
        }
    }

    function bind() {
        resetInitialState();

        // Bind to the fields that actually have validators. Unobtrusive
        // validation is off, so the inputs carry no data-val attributes.
        var bound = {};
        allValidators().forEach(function (v) {
            if (v.controltovalidate) bound[v.controltovalidate] = true;
        });

        Object.keys(bound).forEach(function (id) {
            var ctrl = document.getElementById(id);
            if (!ctrl) return;

            ctrl.addEventListener("focusout", function () {
                touched[ctrl.id] = true;
                validateField(ctrl);
            });

            ctrl.addEventListener("input", function () {
                if (touched[ctrl.id] || submitted) validateField(ctrl);
            });

            ctrl.addEventListener("change", function () {
                if (touched[ctrl.id] || submitted) validateField(ctrl);
            });
        });

        Array.prototype.forEach.call(document.forms, function (form) {
            form.addEventListener("submit", function () {
                submitted = true;
                // ASP.NET's own submit handler runs first; surface the result
                // on the next tick so Page_IsValid is final.
                window.setTimeout(function () {
                    // ASP.NET validates straight to the spans; repaint them so
                    // the shared error styling and aria state are applied.
                    allValidators().forEach(paint);

                    if (window.Page_IsValid === false) {
                        focusFirstInvalid();
                        // the double-submit guard disables buttons early; give
                        // them back so the user can correct and resubmit
                        Array.prototype.forEach.call(
                            form.querySelectorAll('button[type="submit"], input[type="submit"]'),
                            function (btn) { btn.disabled = false; }
                        );
                    }
                }, 30);
            });
        });

        // Checkbox validators without a ControlToValidate still need a repaint
        // when the user toggles them.
        Array.prototype.forEach.call(
            document.querySelectorAll('input[type="checkbox"]'),
            function (box) {
                box.addEventListener("change", function () {
                    allValidators().forEach(function (v) {
                        if (!v.controltovalidate && touched[box.id]) runValidator(v);
                    });
                });
            }
        );
    }

    /* ---- client functions used by CustomValidator controls ---- */

    window.smsValidatePasswordLength = function (sender, args) {
        var min = (window.SMS_MIN_PASSWORD && parseInt(window.SMS_MIN_PASSWORD, 10)) || 8;
        args.IsValid = !args.Value || args.Value.length >= min;
    };

    window.smsValidateConsent = function (sender, args) {
        var id = sender.id.replace("cvConsent", "chkConsent");
        var box = document.getElementById(id);
        args.IsValid = !!(box && box.checked);
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", bind);
    } else {
        bind();
    }
})();
