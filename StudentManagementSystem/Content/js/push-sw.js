/* Student Management System - Web Push service worker.
 *
 * Runs in the background (even when no tab is open) and displays notifications
 * delivered by the server. Registration is done by /Content/js/push.js.
 *
 * NOTE: this worker only receives pushes when the server actually sends them,
 * which requires Push:Enabled = true and valid VAPID keys in Web.config.
 */
"use strict";

self.addEventListener("install", function (event) {
    self.skipWaiting();
});

self.addEventListener("activate", function (event) {
    event.waitUntil(self.clients.claim());
});

self.addEventListener("push", function (event) {
    var payload = { title: "Student Management System", body: "You have a new update.", url: "/" };
    if (event.data) {
        try { payload = Object.assign(payload, event.data.json()); }
        catch (e) { payload.body = event.data.text(); }
    }
    event.waitUntil(self.registration.showNotification(payload.title, {
        body: payload.body,
        icon: "/Content/images/icon-192.png",
        badge: "/Content/images/icon-192.png",
        tag: payload.tag || "sms-notification",
        data: { url: payload.url || "/" }
    }));
});

self.addEventListener("notificationclick", function (event) {
    event.notification.close();
    var target = (event.notification.data && event.notification.data.url) || "/";
    event.waitUntil(
        self.clients.matchAll({ type: "window", includeUncontrolled: true }).then(function (list) {
            for (var i = 0; i < list.length; i++) {
                if (list[i].url.indexOf(target) >= 0 && "focus" in list[i]) return list[i].focus();
            }
            return self.clients.openWindow(target);
        })
    );
});
