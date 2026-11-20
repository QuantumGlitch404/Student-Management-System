/* ------------------------------------------------------------------
   Online test runner: countdown, serial autosave and proctoring events.
   ASCII only. Server remains the source of truth for timing and scoring.
   ------------------------------------------------------------------ */
(function () {
    "use strict";

    var form = document.getElementById("testForm");
    if (!form) return;

    var timer = document.getElementById("examTimer");
    var saveState = document.getElementById("examSaveState");
    var answeredEl = document.getElementById("examAnswered");
    var flagsEl = document.getElementById("examFlags");
    var attemptId = form.getAttribute("data-attempt") || "";
    var saveUrl = form.getAttribute("data-save-url") || "/Handlers/OnlineTestAction.ashx";
    var proctoring = form.getAttribute("data-proctoring") === "1";
    var csrfInput = form.querySelector("input[name='__csrf']");
    var csrf = csrfInput ? csrfInput.value : "";
    var pending = {};
    var flushing = false;
    var submitting = false;
    var submitted = false;
    var lastAway = 0;
    var lastEvent = {};

    form.removeAttribute("data-confirm");

    function post(data) {
        var params = new URLSearchParams();
        params.append("__csrf", csrf);
        Object.keys(data).forEach(function (key) { params.append(key, data[key]); });
        return fetch(saveUrl, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
                "X-Requested-With": "XMLHttpRequest"
            },
            body: params.toString()
        }).then(function (response) {
            return response.json().then(function (json) {
                if (!response.ok || !json.ok) throw new Error(json.error || "save failed");
                return json;
            });
        });
    }

    function setSaveState(text) {
        if (saveState) saveState.textContent = text;
    }

    function flushSave() {
        if (flushing) return;
        var keys = Object.keys(pending);
        if (!keys.length) return;
        flushing = true;
        var qid = keys[0];
        var option = pending[qid];
        delete pending[qid];
        setSaveState("Saving...");
        post({ op: "save", attemptId: attemptId, questionId: qid, option: option })
            .then(function () {
                flushing = false;
                if (Object.keys(pending).length) flushSave();
                else setSaveState("Saved just now");
            })
            .catch(function () {
                flushing = false;
                pending[qid] = option;
                setSaveState("Save failed - retrying...");
                window.setTimeout(flushSave, 4000);
            });
    }

    function queueSave(qid, option) {
        pending[qid] = option;
        flushSave();
    }

    function updateAnswered() {
        if (!answeredEl) return;
        var seen = {};
        form.querySelectorAll("input[type='radio']:checked").forEach(function (radio) {
            seen[radio.getAttribute("data-qid")] = true;
        });
        answeredEl.textContent = Object.keys(seen).length;
    }

    form.addEventListener("change", function (e) {
        var radio = e.target;
        if (!radio || radio.type !== "radio") return;
        updateAnswered();
        queueSave(radio.getAttribute("data-qid"), radio.value);
    });

    function showWarning(json) {
        var existing = document.getElementById("examWarning");
        if (existing) existing.parentNode.removeChild(existing);
        var limit = typeof json.warningLimit === "number" ? json.warningLimit : 1;
        var box = document.createElement("div");
        box.id = "examWarning";
        box.className = "exam-warning";
        box.setAttribute("role", "alertdialog");
        box.setAttribute("aria-live", "assertive");
        var head = document.createElement("strong");
        head.textContent = "Integrity warning " + json.violations + " of " + limit;
        var body = document.createElement("p");
        body.textContent = json.violations >= limit
            ? "This is your final warning. Leaving the test window again will submit your attempt automatically."
            : "Leaving the test window was recorded. Reaching the limit submits your attempt automatically.";
        var close = document.createElement("button");
        close.type = "button";
        close.className = "btn btn-ghost";
        close.textContent = "Continue test";
        close.addEventListener("click", function () { box.parentNode.removeChild(box); });
        box.appendChild(head); box.appendChild(body); box.appendChild(close);
        document.body.appendChild(box);
        close.focus();
        window.setTimeout(function () { if (box.parentNode) box.parentNode.removeChild(box); }, 8000);
    }

    function autoSubmitted(json) {
        submitted = true;
        submitting = true;
        var box = document.getElementById("examWarning");
        if (!box) {
            box = document.createElement("div");
            box.id = "examWarning";
            box.className = "exam-warning is-final";
            box.setAttribute("role", "alertdialog");
            document.body.appendChild(box);
        }
        box.innerHTML = "";
        var head = document.createElement("strong");
        head.textContent = "Test auto-submitted";
        var body = document.createElement("p");
        body.textContent = "The warning limit was reached (" + json.violations + " of " + json.warningLimit +
            "). Your answers have been saved and the attempt is closed. Loading your result...";
        box.appendChild(head); box.appendChild(body);
        window.setTimeout(function () { HTMLFormElement.prototype.submit.call(form); }, 2500);
    }

    function logEvent(type, detail) {
        if (!proctoring || submitted) return;
        var key = type === "visibility_hidden" || type === "blur" ? "away" : type;
        var now = Date.now();
        if (lastEvent[key] && now - lastEvent[key] < 4000) return;
        lastEvent[key] = now;
        post({ op: "event", attemptId: attemptId, type: type, detail: detail || "" })
            .then(function (json) {
                if (flagsEl && typeof json.violations === "number") {
                    flagsEl.textContent = json.violations + (json.violations === 1 ? " flag" : " flags");
                }
                if (json.autoSubmitted || json.action === "submitted") autoSubmitted(json);
                else if (json.action === "warning") showWarning(json);
            })
            .catch(function () { });
    }

    function markAway(type, detail) {
        var now = Date.now();
        if (now - lastAway < 3000) return;
        lastAway = now;
        logEvent(type, detail);
    }

    document.addEventListener("visibilitychange", function () {
        if (document.hidden) markAway("visibility_hidden", "Tab hidden");
        else logEvent("focus", "Tab visible");
    });
    window.addEventListener("blur", function () { markAway("blur", "Window lost focus"); });
    document.addEventListener("copy", function (e) { if (proctoring) { e.preventDefault(); logEvent("copy", "Copy blocked"); } });
    document.addEventListener("cut", function (e) { if (proctoring) { e.preventDefault(); logEvent("cut", "Cut blocked"); } });
    document.addEventListener("paste", function (e) { if (proctoring) { e.preventDefault(); logEvent("paste", "Paste blocked"); } });
    document.addEventListener("contextmenu", function (e) { if (proctoring) { e.preventDefault(); logEvent("contextmenu", "Context menu blocked"); } });
    document.addEventListener("keydown", function (e) {
        if (!proctoring) return;
        var key = (e.key || "").toLowerCase();
        var blocked = e.key === "F12" || (e.ctrlKey && e.shiftKey && (key === "i" || key === "j" || key === "c")) || (e.ctrlKey && key === "u");
        if (blocked) {
            e.preventDefault();
            logEvent("devtools_shortcut", "Blocked " + (e.key || "shortcut"));
        }
    });

    var wasFullscreen = false;
    document.addEventListener("fullscreenchange", function () {
        if (document.fullscreenElement) { wasFullscreen = true; return; }
        if (wasFullscreen) {
            wasFullscreen = false;
            logEvent("fullscreen_exit", "Fullscreen exited");
        }
    });

    window.setInterval(function () { if (!submitted) logEvent("heartbeat", "running"); }, 120000);

    function finishSubmit() {
        if (submitted || submitting) return;
        submitted = true;
        submitting = true;
        setSaveState("Submitting...");
        var button = form.querySelector("button[type='submit']");
        if (button) button.disabled = true;
        HTMLFormElement.prototype.submit.call(form);
    }

    form.addEventListener("submit", function (e) {
        if (submitting) { e.preventDefault(); return; }
        if (!window.confirm("Submit this test now? You cannot change answers after submitting.")) {
            e.preventDefault();
            return;
        }
        e.preventDefault();
        finishSubmit();
    });

    window.addEventListener("beforeunload", function (e) {
        if (submitted || submitting) return;
        e.preventDefault();
        e.returnValue = "";
    });

    if (timer) {
        var expires = Date.parse(timer.getAttribute("data-expires"));
        if (!isNaN(expires)) {
            var tick = function () {
                var left = expires - Date.now();
                if (left <= 0) {
                    timer.textContent = "00:00";
                    timer.classList.add("is-danger");
                    finishSubmit();
                    return;
                }
                var total = Math.floor(left / 1000);
                var minutes = Math.floor(total / 60);
                var seconds = total % 60;
                timer.textContent = (minutes < 10 ? "0" : "") + minutes + ":" + (seconds < 10 ? "0" : "") + seconds;
                timer.classList.toggle("is-warning", left <= 600000 && left > 300000);
                timer.classList.toggle("is-danger", left <= 300000);
                window.setTimeout(tick, 1000);
            };
            tick();
        }
    }

    updateAnswered();
})();
