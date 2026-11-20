/* ------------------------------------------------------------------
   Student Management System - notification panel
   The bell opens a panel with the newest notifications. Clicking one
   marks it read on the server and opens its destination through
   /Handlers/NotifyGo.ashx, which re-checks ownership and that the
   destination is allowed for the signed-in role. The unread badge is
   refreshed after every action and polled at a low rate while the tab
   is visible.
   ASCII only.
   ------------------------------------------------------------------ */
(function () {
    "use strict";

    var bell, panel, listEl, countEl, badge, markAllBtn, state = { loaded: false, open: false, busy: false };

    function $(id) { return document.getElementById(id); }
    function esc(s) {
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }

    var ICONS = {
        academic: "bi-journal-check",
        payment: "bi-cash-coin",
        complaint: "bi-flag",
        system: "bi-shield-lock",
        general: "bi-bell"
    };

    function timeAgo(iso) {
        var t = Date.parse(iso);
        if (isNaN(t)) return "";
        var diff = Math.max(0, Date.now() - t);
        var min = Math.floor(diff / 60000);
        if (min < 1) return "just now";
        if (min < 60) return min + " min ago";
        var h = Math.floor(min / 60);
        if (h < 24) return h + " h ago";
        var d = Math.floor(h / 24);
        if (d < 7) return d + " d ago";
        var dt = new Date(t);
        try {
            return new Intl.DateTimeFormat(undefined, {
                timeZone: (window.SMS_TICKER && window.SMS_TICKER.timezone) || "UTC",
                year: "numeric", month: "2-digit", day: "2-digit"
            }).format(dt);
        } catch (e) {
            return dt.toISOString().slice(0, 10);
        }
    }

    function setBadge(n) {
        if (!badge) return;
        n = parseInt(n, 10) || 0;
        if (n > 0) {
            badge.textContent = n > 99 ? "99+" : String(n);
            badge.classList.remove("d-none");
            bell.setAttribute("aria-label", "Notifications, " + n + " unread");
        } else {
            badge.classList.add("d-none");
            bell.setAttribute("aria-label", "Notifications, none unread");
        }
        if (countEl) {
            countEl.textContent = n > 0 ? n + " unread" : "All read";
            countEl.className = n > 0 ? "snp-count" : "snp-count is-clear";
        }
    }

    function render(items) {
        if (!listEl) return;
        if (!items || !items.length) {
            listEl.innerHTML = '<li class="snp-empty"><i class="bi bi-inbox d-block mb-1" style="font-size:1.4rem"></i>You are all caught up.</li>';
            return;
        }
        var html = "";
        for (var i = 0; i < items.length; i++) {
            var n = items[i];
            var cls = n.unread ? "snp-item unread" : "snp-item";
            html += '<li class="' + cls + '"><a href="/Handlers/NotifyGo.ashx?id=' + encodeURIComponent(n.id) + '" data-id="' + esc(n.id) + '">' +
                '<span class="snp-ic"><i class="bi ' + (ICONS[n.type] || ICONS.general) + '"></i></span>' +
                '<span class="snp-body">' +
                '<span class="snp-itemtitle">' + esc(n.title) + '</span>' +
                '<span class="snp-itemmsg">' + esc(n.message) + '</span>' +
                '<span class="snp-meta">' +
                (n.unread ? '<span class="snp-dot" aria-label="Unread"></span>' : "") +
                '<span>' + esc(timeAgo(n.createdAt)) + '</span>' +
                '<span class="badge badge-info">' + esc(n.type) + '</span>' +
                '</span></span></a></li>';
        }
        listEl.innerHTML = html;
        Array.prototype.forEach.call(listEl.querySelectorAll("a[data-id]"), function (a) {
            a.addEventListener("click", function () { setBadge(Math.max(0, currentUnread - 1)); });
        });
    }

    var currentUnread = 0;

    function load(force) {
        if (state.busy) return;
        if (state.loaded && !force) return;
        state.busy = true;
        listEl.innerHTML =
            '<li class="snp-item"><span class="snp-ic sms-skeleton"></span><span class="snp-body">' +
            '<span class="sms-skeleton d-block mb-1" style="height:11px;width:70%"></span>' +
            '<span class="sms-skeleton d-block" style="height:9px;width:90%"></span></span></li>' +
            '<li class="snp-item"><span class="snp-ic sms-skeleton"></span><span class="snp-body">' +
            '<span class="sms-skeleton d-block mb-1" style="height:11px;width:55%"></span>' +
            '<span class="sms-skeleton d-block" style="height:9px;width:80%"></span></span></li>';
        fetch("/Handlers/NotifyList.ashx", { credentials: "same-origin", cache: "no-store" })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (j) {
                if (!j) throw new Error("bad response");
                currentUnread = parseInt(j.unread, 10) || 0;
                setBadge(currentUnread);
                render(j.items || []);
                state.loaded = true;
            })
            .catch(function () {
                listEl.innerHTML = '<li class="snp-empty">Could not load notifications.</li>';
            })
            .finally(function () { state.busy = false; });
    }

    function refreshBadge() {
        fetch("/Handlers/NotifyBadge.ashx", { credentials: "same-origin", cache: "no-store" })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (j) {
                if (!j) return;
                currentUnread = parseInt(j.unread, 10) || 0;
                setBadge(currentUnread);
                if (state.open) load(true);
            })
            .catch(function () { });
    }

    function openPanel(open) {
        if (!panel) return;
        state.open = open;
        panel.hidden = !open;
        bell.setAttribute("aria-expanded", open ? "true" : "false");
        if (open) {
            load(false);
            var first = panel.querySelector("button, a");
            if (first) first.focus({ preventScroll: true });
        }
    }

    function markAll() {
        markAllBtn.disabled = true;
        var body = new URLSearchParams();
        body.set("op", "markall");
        body.set("__csrf", (window.SMS_NOTIF && window.SMS_NOTIF.csrf) || "");
        fetch("/Handlers/NotifyAction.ashx", {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
            body: body.toString()
        }).then(function (r) { return r.json(); })
          .then(function (j) {
              setBadge(0);
              currentUnread = 0;
              Array.prototype.forEach.call(listEl.querySelectorAll(".snp-item"), function (li) { li.classList.remove("unread"); });
              var dots = listEl.querySelectorAll(".snp-dot");
              Array.prototype.forEach.call(dots, function (d) { d.parentNode.removeChild(d); });
              if (window.SMS && window.SMS.toast) window.SMS.toast("All notifications marked as read");
          })
          .catch(function () { if (window.SMS && window.SMS.toast) window.SMS.toast("Could not update notifications"); })
          .finally(function () { markAllBtn.disabled = false; });
    }

    function init() {
        bell = $("smsBell"); panel = $("smsNotifPanel");
        listEl = $("smsNotifList"); countEl = $("smsNotifCount"); badge = $("smsBadge"); markAllBtn = $("smsMarkAll");
        if (!bell || !panel) return;

        bell.addEventListener("click", function (e) {
            e.preventDefault();
            openPanel(panel.hidden);
        });
        if (markAllBtn) markAllBtn.addEventListener("click", markAll);

        var closeBtn = $("smsNotifClose");
        if (closeBtn) closeBtn.addEventListener("click", function () { openPanel(false); });

        document.addEventListener("click", function (e) {
            if (panel.hidden) return;
            if (panel.contains(e.target) || bell.contains(e.target)) return;
            openPanel(false);
        });
        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape" && !panel.hidden) { openPanel(false); bell.focus(); }
        });

        refreshBadge();
        /* Poll only while the tab is visible; a hidden tab stops entirely. */
        setInterval(function () {
            if (document.visibilityState === "visible") refreshBadge();
        }, 45000);
        document.addEventListener("visibilitychange", function () {
            if (document.visibilityState === "visible") refreshBadge();
        });

        /* Keep the "marked read" state in sync when the user returns with Back. */
        window.addEventListener("pageshow", function (e) {
            if (e.persisted) { state.loaded = false; refreshBadge(); }
        });
        window.addEventListener("sms:notifications-changed", function () {
            state.loaded = false;
            refreshBadge();
        });
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();
