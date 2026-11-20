/* ------------------------------------------------------------------
   Campus notification ticker
   Polls the unread feed (15s visible / 60s hidden), pauses on hover or
   focus, opens a preview dialog, and supports read + snooze actions.
   ASCII only. No innerHTML with message content.
   ------------------------------------------------------------------ */
(function () {
    "use strict";

    var cfg = window.SMS_TICKER || { enabled: true, speed: "slow", timezone: "UTC", clock: "24" };
    var root, track, live, dialog, labelEl, pauseEl, minEl;
    var items = [];
    var unreadCount = 0;
    var active = null;
    var paused = false;
    var lastTopId = 0;
    var freshIds = {};
    var hoverTimer = null;
    var pollTimer = null;
    var SPEEDS = { slow: 1.35, normal: 1, fast: 0.7 };

    function $(id) { return document.getElementById(id); }

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
        return d + " d ago";
    }

    function localTime(iso) {
        var d = new Date(iso);
        if (isNaN(d.getTime())) return "";
        try {
            return new Intl.DateTimeFormat(undefined, {
                timeZone: cfg.timezone || "UTC",
                dateStyle: "medium",
                timeStyle: "short"
            }).format(d);
        } catch (e) {
            return d.toLocaleString();
        }
    }

    function addSpan(parent, cls, text) {
        var el = document.createElement("span");
        el.className = cls;
        el.textContent = text;
        parent.appendChild(el);
        return el;
    }

    function buildItem(n) {
        var b = document.createElement("button");
        b.type = "button";
        b.className = "notif-ticker-item" + (freshIds[n.id] ? " is-new" : "");
        b.setAttribute("data-id", String(n.id));
        b.setAttribute("aria-label", n.title + ". Open notification preview.");
        if (n.type) addSpan(b, "ticker-cat", String(n.type).toUpperCase());
        addSpan(b, "ticker-title", n.title || "Notification");
        if (n.message) addSpan(b, "ticker-text", n.message);
        addSpan(b, "ticker-time", timeAgo(n.createdAt));
        if (freshIds[n.id]) addSpan(b, "ticker-new", "NEW");
        return b;
    }

    function render() {
        if (!root || !track) return;
        track.innerHTML = "";
        if (labelEl) labelEl.textContent = "Unread " + unreadCount;
        if (!cfg.enabled || !items.length) {
            root.hidden = true;
            return;
        }

        var first = document.createElement("div");
        first.className = "notif-ticker-half";
        items.forEach(function (n) { first.appendChild(buildItem(n)); });
        track.appendChild(first);

        var viewport = root.querySelector(".notif-ticker-viewport");
        var minWidth = Math.max(320, (viewport ? viewport.clientWidth : 600) * 1.25);
        var guard = 0;
        while (first.scrollWidth > 0 && first.scrollWidth < minWidth && guard < 5) {
            Array.prototype.slice.call(first.children).forEach(function (child) {
                first.appendChild(child.cloneNode(true));
            });
            guard++;
        }
        var tabbable = items.length;
        Array.prototype.forEach.call(first.querySelectorAll("button"), function (b, i) {
            if (i >= tabbable) b.tabIndex = -1;
        });

        var second = first.cloneNode(true);
        second.setAttribute("aria-hidden", "true");
        Array.prototype.forEach.call(second.querySelectorAll("button"), function (b) { b.tabIndex = -1; });
        track.appendChild(second);

        var half = first.scrollWidth || minWidth;
        var factor = SPEEDS[cfg.speed] || SPEEDS.slow;
        var seconds = Math.round(Math.max(20, half / 42) * factor);
        track.style.setProperty("--ticker-duration", seconds + "s");
        root.hidden = false;
    }

    function setItems(list) {
        items = Array.isArray(list) ? list : [];
        render();
    }

    function request(url, body) {
        return fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
            body: body.toString()
        }).then(function (r) { return r.ok ? r.json() : null; });
    }

    function action(op, id, extra) {
        var body = new URLSearchParams();
        body.set("op", op);
        body.set("id", String(id));
        body.set("__csrf", (window.SMS_NOTIF && window.SMS_NOTIF.csrf) || "");
        if (extra) Object.keys(extra).forEach(function (k) { body.set(k, String(extra[k])); });
        return request("/Handlers/NotifyAction.ashx", body);
    }

    function afterChanged() {
        window.dispatchEvent(new Event("sms:notifications-changed"));
        load();
    }

    function openDialog(n) {
        if (!dialog) return;
        active = n;
        $("smsTickerDialogTitle").textContent = n.title || "Notification";
        $("smsTickerDialogBody").textContent = n.message || "";
        $("smsTickerDialogMeta").textContent =
            (n.type ? n.type.toUpperCase() + " - " : "") + localTime(n.createdAt);
        var open = $("smsTickerDialogOpen");
        if (n.url) {
            open.href = "/Handlers/NotifyGo.ashx?id=" + encodeURIComponent(n.id);
            open.hidden = false;
        } else {
            open.removeAttribute("href");
            open.hidden = true;
        }
        track.classList.add("is-paused");
        if (typeof dialog.showModal === "function") dialog.showModal();
        else dialog.setAttribute("open", "open");
    }

    function closeDialog() {
        if (!dialog) return;
        if (dialog.open) dialog.close();
        else dialog.removeAttribute("open");
        track.classList.remove("is-paused");
        active = null;
    }

    function markRead(close) {
        if (!active) return;
        var n = active;
        action("markone", n.id).then(function (j) {
            if (!j || j.ok === false) throw new Error("failed");
            items = items.filter(function (x) { return x.id !== n.id; });
            render();
            if (close) closeDialog();
            afterChanged();
            if (window.SMS && window.SMS.toast) window.SMS.toast("Notification marked read");
        }).catch(function () {
            if (window.SMS && window.SMS.toast) window.SMS.toast("Could not update notification");
        });
    }

    function snooze(minutes) {
        if (!active) return;
        var n = active;
        action("snooze", n.id, { minutes: minutes }).then(function (j) {
            if (!j || j.ok === false) throw new Error("failed");
            items = items.filter(function (x) { return x.id !== n.id; });
            render();
            closeDialog();
            afterChanged();
            if (window.SMS && window.SMS.toast) window.SMS.toast("Snoozed");
        }).catch(function () {
            if (window.SMS && window.SMS.toast) window.SMS.toast("Could not snooze notification");
        });
    }

    function load() {
        if (!root || !cfg.enabled) return;
        fetch("/Handlers/TickerFeed.ashx", { credentials: "same-origin", cache: "no-store" })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (j) {
                if (!j || !j.ok) return;
                cfg.enabled = j.enabled !== false;
                if (j.speed) cfg.speed = j.speed;
                if (j.timezone) cfg.timezone = j.timezone;
                unreadCount = typeof j.unread === "number" ? j.unread : (j.items || []).length;
                var list = j.items || [];
                var top = 0;
                list.forEach(function (it) { if (it.id > top) top = it.id; });
                if (top > lastTopId) {
                    list.forEach(function (it) { if (it.id > lastTopId) freshIds[it.id] = true; });
                    if (lastTopId > 0 && live && list[0]) live.textContent = "New notification: " + list[0].title;
                    lastTopId = top;
                    window.setTimeout(function () { freshIds = {}; render(); }, 10000);
                } else if (lastTopId === 0) {
                    lastTopId = top;
                }
                setItems(list);
                if (unreadCount === 0) root.hidden = true;
            })
            .catch(function () { /* keep the last good feed */ });
    }

    function schedule() {
        if (pollTimer) clearTimeout(pollTimer);
        var wait = document.visibilityState === "visible" ? 15000 : 60000;
        pollTimer = setTimeout(function () {
            load();
            schedule();
        }, wait);
    }

    function init() {
        root = $("smsTicker");
        track = $("smsTickerTrack");
        live = $("smsTickerLive");
        dialog = $("smsTickerDialog");
        labelEl = $("smsTickerLabel");
        pauseEl = $("smsTickerPause");
        minEl = $("smsTickerMin");
        if (!root || !track) return;

        function itemFromEvent(e) {
            var btn = e.target.closest ? e.target.closest(".notif-ticker-item") : null;
            if (!btn) return null;
            var id = parseInt(btn.getAttribute("data-id"), 10);
            return items.filter(function (x) { return x.id === id; })[0] || null;
        }

        track.addEventListener("click", function (e) {
            var n = itemFromEvent(e);
            if (n) openDialog(n);
        });

        // Hover/focus opens the preview card after 150 ms and pauses the marquee.
        track.addEventListener("mouseover", function (e) {
            var n = itemFromEvent(e);
            if (!n) return;
            track.classList.add("is-paused");
            if (hoverTimer) clearTimeout(hoverTimer);
            hoverTimer = setTimeout(function () { openDialog(n); }, 150);
        });
        track.addEventListener("mouseout", function (e) {
            if (!e.relatedTarget || !track.contains(e.relatedTarget)) {
                if (hoverTimer) { clearTimeout(hoverTimer); hoverTimer = null; }
                if (!dialog || !dialog.open) track.classList.remove("is-paused");
            }
        });

        if (pauseEl) pauseEl.addEventListener("click", function () {
            paused = !paused;
            track.classList.toggle("is-paused", paused);
            pauseEl.textContent = paused ? "\u25B6" : "\u275A\u275A";
            pauseEl.setAttribute("aria-label", paused ? "Resume ticker" : "Pause ticker");
        });
        if (minEl) minEl.addEventListener("click", function () {
            root.classList.toggle("is-minimised");
            minEl.textContent = root.classList.contains("is-minimised") ? "+" : "\u2212";
            minEl.setAttribute("aria-label", root.classList.contains("is-minimised") ? "Expand ticker" : "Minimise ticker");
        });

        if (dialog) {
            $("smsTickerDialogClose").addEventListener("click", closeDialog);
            $("smsTickerDialogRead").addEventListener("click", function () { markRead(true); });
            Array.prototype.forEach.call(dialog.querySelectorAll("[data-ticker-snooze]"), function (b) {
                b.addEventListener("click", function () {
                    snooze(parseInt(b.getAttribute("data-ticker-snooze"), 10) || 60);
                });
            });
            $("smsTickerDialogOpen").addEventListener("click", function () {
                if (!active) return;
                action("markone", active.id).catch(function () { });
                afterChanged();
            });
        }

        document.addEventListener("visibilitychange", function () {
            if (document.visibilityState === "visible") load();
            schedule();
        });
        window.addEventListener("sms:notification-read", function () { load(); });

        load();
        schedule();
        if (live) live.textContent = "";
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();
