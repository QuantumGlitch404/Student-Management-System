/* Student Management System - auth pages client script (ASCII only). */
(function () {
    "use strict";

    function $(sel, root) { return (root || document).querySelector(sel); }
    function $all(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }

    /* show/hide password fields (detects bare inputs and the buttons rendered by auth shell) */
    function bindPasswordToggles() {
        $all('.toggle-password').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var shell = btn.closest('.input-shell') || btn.parentElement;
                var input = shell ? shell.querySelector('input[type="password"], input[type="text"]') : null;
                if (!input) return;
                var show = input.type === 'password';
                input.type = show ? 'text' : 'password';
                btn.textContent = show ? 'Hide' : 'Show';
                btn.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
            });
        });
    }

    /* very light password strength meter: length + variety */
    function scorePassword(pwd) {
        if (!pwd) return 0;
        var score = 0;
        if (pwd.length >= 8) score++;
        if (pwd.length >= 12) score++;
        if (/[a-z]/.test(pwd) && /[A-Z]/.test(pwd)) score++;
        if (/[0-9]/.test(pwd)) score++;
        if (/[^a-zA-Z0-9]/.test(pwd)) score++;
        return Math.min(score, 4); // 0..4
    }

    function bindPasswordMeters() {
        $all('[data-pwd-meter]').forEach(function (input) {
            var meter = document.getElementById(input.getAttribute('data-pwd-meter'));
            if (!meter) return;
            input.addEventListener('input', function () {
                var s = scorePassword(input.value);
                meter.className = 'pwd-meter' + (s >= 4 ? ' is-strong' : s >= 2 ? ' is-fair' : s >= 1 ? ' is-weak' : '');
            });
        });
    }

    /* OTP input: digits only, upper bound 6, strip whitespace */
    function bindOtpInputs() {
        $all('input[data-otp]').forEach(function (input) {
            input.setAttribute('inputmode', 'numeric');
            input.setAttribute('maxlength', '6');
            input.setAttribute('autocomplete', 'one-time-code');
            input.addEventListener('input', function () {
                input.value = input.value.replace(/[^0-9]/g, '').slice(0, 6);
            });
        });
    }

    /* prevent double submits */
    function bindNoDoubleSubmit() {
        $all('form').forEach(function (form) {
            form.addEventListener('submit', function () {
                $all('button[type="submit"], input[type="submit"]', form).forEach(function (b) {
                    window.setTimeout(function () { b.disabled = true; }, 10);
                });
            });
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        bindPasswordToggles();
        bindPasswordMeters();
        bindOtpInputs();
        bindNoDoubleSubmit();
    });
})();
