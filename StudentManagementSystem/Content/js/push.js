/* Student Management System - browser push subscription helper.
 *
 * Only runs when the server rendered window.SMS_PUSH.enabled === true and a
 * VAPID public key is present. Otherwise it does nothing (honest no-op, not a
 * fake "notifications enabled" badge).
 */
(function () {
    "use strict";

    var cfg = window.SMS_PUSH;
    if (!cfg || !cfg.enabled || !cfg.key) return;
    if (!("serviceWorker" in navigator) || !("PushManager" in window)) return;
    if (Notification.permission === "denied") return;

    function urlBase64ToUint8Array(base64String) {
        var padding = "=".repeat((4 - (base64String.length % 4)) % 4);
        var base64 = (base64String + padding).replace(/-/g, "+").replace(/_/g, "/");
        var raw = window.atob(base64);
        var out = new Uint8Array(raw.length);
        for (var i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
        return out;
    }

    function save(sub) {
        var json = sub.toJSON ? sub.toJSON() : {
            endpoint: sub.endpoint,
            keys: {
                p256dh: btoa(String.fromCharCode.apply(null, new Uint8Array(sub.getKey("p256dh")))),
                auth: btoa(String.fromCharCode.apply(null, new Uint8Array(sub.getKey("auth"))))
            }
        };
        return fetch("/Handlers/PushSubscribe.ashx", {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ endpoint: json.endpoint, keys: json.keys })
        });
    }

    function enable() {
        if (Notification.permission === "default") {
            Notification.requestPermission().then(function (p) {
                if (p === "granted") enable();
            });
            return;
        }
        if (Notification.permission !== "granted") return;

        navigator.serviceWorker.register("/Content/js/push-sw.js")
            .then(function (reg) { return reg.pushManager.getSubscription().then(function (s) { return s || reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: urlBase64ToUint8Array(cfg.key) }); }); })
            .then(save)
            .catch(function () { /* permission or browser blocked - stay silent */ });
    }

    var btn = document.getElementById("smsPushEnable");
    if (btn) btn.addEventListener("click", enable);
    if (cfg.auto) enable();
})();
