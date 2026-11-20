/* Topbar clock: user's IANA timezone with abbreviation, 12/24-hour toggle, popover. */
(function () {
    "use strict";

    var btn, timeEl, timer, popover, popoverTimer;
    var cfg = window.SMS_TICKER || { timezone: "UTC", clock: "24" };

    // Resolution order: user saved timezone -> sms_tz cookie -> institution -> UTC
    function getEffectiveTimezone() {
        if (cfg && cfg.userTimezone) return cfg.userTimezone;
        try {
            var m = document.cookie.match(/(?:^|; )sms_tz=([^;]*)/);
            if (m && m[1]) return decodeURIComponent(m[1]);
        } catch (e) { }
        if (cfg && cfg.institutionTimezone) return cfg.institutionTimezone;
        if (cfg && cfg.timezone) return cfg.timezone;
        return "UTC";
    }

    var clock = (cfg && cfg.clock === "12") ? "12" : "24";
    var tz = getEffectiveTimezone();

    function format(d, options) {
        var opts = Object.assign({
            timeZone: tz,
            hour: "2-digit",
            minute: "2-digit",
            hour12: clock === "12"
        }, options);
        try {
            return new Intl.DateTimeFormat(undefined, opts).format(d);
        } catch (e) {
            return d.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit", hour12: clock === "12" });
        }
    }

    function getZoneAbbr(d) {
        try {
            return new Intl.DateTimeFormat(undefined, {
                timeZone: tz,
                timeZoneName: "short"
            }).formatToParts(d).find(function (p) { return p.type === "timeZoneName"; }).value;
        } catch (e) {
            return tz;
        }
    }

    function paint() {
        if (!timeEl) return;
        var now = new Date();
        var timeStr = format(now);
        var zoneAbbr = getZoneAbbr(now);
        timeEl.textContent = timeStr + " " + zoneAbbr;
        timeEl.setAttribute("datetime", now.toISOString());
        if (btn) {
            btn.setAttribute("aria-label", "Local time " + timeStr + " " + zoneAbbr + ". Click for details.");
            btn.title = "Local time in " + tz + ". Click for details.";
        }
    }

    function saveClockFormat() {
        if (!window.fetch) return;
        var body = new URLSearchParams();
        body.set("clock", clock);
        body.set("__csrf", (window.SMS_NOTIF && window.SMS_NOTIF.csrf) || "");
        fetch("/Handlers/Preferences.ashx", {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
            body: body.toString()
        }).catch(function () { });
    }

    function buildPopoverContent() {
        var now = new Date();
        var parts = new Intl.DateTimeFormat(undefined, {
            timeZone: tz,
            weekday: "long", year: "numeric", month: "long", day: "numeric",
            hour: "2-digit", minute: "2-digit", second: "2-digit",
            hour12: clock === "12", timeZoneName: "long"
        }).formatToParts(now);

        var dateParts = {};
        parts.forEach(function (p) { dateParts[p.type] = p.value; });

        var localStr = (dateParts.weekday || "") + ", " + (dateParts.month || "") + " " + (dateParts.day || "") + ", " + (dateParts.year || "") + " " +
            (dateParts.hour || "") + ":" + (dateParts.minute || "") + ":" + (dateParts.second || "") + " " + (dateParts.timeZoneName || tz);

        var utcStr = now.toISOString().replace("T", " ").replace("Z", " UTC");

        // Institution time
        var instTz = (cfg && cfg.institutionTimezone) || (cfg && cfg.timezone) || "UTC";
        var instParts = new Intl.DateTimeFormat(undefined, {
            timeZone: instTz,
            hour: "2-digit", minute: "2-digit", hour12: clock === "12", timeZoneName: "short"
        }).formatToParts(now);
        var instTime = instParts.map(function (p) { return p.value; }).join("");

        // World clocks
        var world = [
            { label: "UTC", zone: "UTC" },
            { label: "New York", zone: "America/New_York" },
            { label: "London", zone: "Europe/London" },
            { label: "Dubai", zone: "Asia/Dubai" },
            { label: "Singapore", zone: "Asia/Singapore" }
        ].map(function (w) {
            try {
                var f = new Intl.DateTimeFormat(undefined, { timeZone: w.zone, hour: "2-digit", minute: "2-digit", hour12: clock === "12", timeZoneName: "short" });
                return w.label + ": " + f.format(now);
            } catch (e) { return w.label + ": —"; }
        }).join("  |  ");

        return {
            local: localStr,
            utc: utcStr,
            institution: "Institution (" + instTz + "): " + instTime,
            world: world,
            dst: getDstInfo(tz, now)
        };
    }

    function getDstInfo(zone, date) {
        try {
            var jan = new Date(date.getFullYear(), 0, 1);
            var jul = new Date(date.getFullYear(), 6, 1);
            var janOffset = -new Intl.DateTimeFormat(undefined, { timeZone: zone, timeZoneName: "shortOffset" }).format(jan);
            var julOffset = -new Intl.DateTimeFormat(undefined, { timeZone: zone, timeZoneName: "shortOffset" }).format(jul);
            var isDst = Math.max(janOffset, julOffset) !== Math.min(janOffset, julOffset);
            if (!isDst) return "No DST in " + zone;
            var stdOffset = Math.max(janOffset, julOffset);
            var dstOffset = Math.min(janOffset, julOffset);
            return zone + " observes DST (standard UTC" + formatOffset(stdOffset) + ", DST UTC" + formatOffset(dstOffset) + ")";
        } catch (e) { return ""; }
    }

    function formatOffset(minutes) {
        var sign = minutes >= 0 ? "+" : "-";
        var abs = Math.abs(minutes);
        var h = Math.floor(abs / 60);
        var m = abs % 60;
        return sign + String(h).padStart(2, "0") + ":" + String(m).padStart(2, "0");
    }

    function showPopover() {
        if (popover) return;
        var content = buildPopoverContent();
        popover = document.createElement("div");
        popover.className = "clock-popover";
        popover.innerHTML =
            '<div class="cp-section"><strong>Your time</strong><div class="cp-time">' + content.local + '</div></div>' +
            '<div class="cp-section"><strong>UTC</strong><div>' + content.utc + '</div></div>' +
            '<div class="cp-section"><strong>' + content.institution + '</strong></div>' +
            '<div class="cp-section"><strong>World clocks</strong><div class="cp-world">' + content.world + '</div></div>' +
            (content.dst ? '<div class="cp-section cp-dst">' + content.dst + '</div>' : '') +
            '<div class="cp-actions"><button type="button" class="btn btn-sm btn-primary" id="cpChangeTz">Change timezone</button>' +
            '<button type="button" class="btn btn-sm btn-outline" id="cpOpenSettings">All settings</button></div>';
        document.body.appendChild(popover);

        var rect = btn.getBoundingClientRect();
        popover.style.left = rect.left + "px";
        popover.style.top = (rect.bottom + 4) + "px";

        document.getElementById("cpChangeTz").addEventListener("click", function () {
            hidePopover();
            window.location.href = "/Account/Settings.aspx#timezone";
        });
        document.getElementById("cpOpenSettings").addEventListener("click", function () {
            hidePopover();
            window.location.href = "/Account/Settings.aspx";
        });

        document.addEventListener("click", onDocClick);
        document.addEventListener("keydown", onKeyDown);
    }

    function hidePopover() {
        if (popover) {
            popover.remove();
            popover = null;
            document.removeEventListener("click", onDocClick);
            document.removeEventListener("keydown", onKeyDown);
        }
    }

    function onDocClick(e) {
        if (popover && !popover.contains(e.target) && e.target !== btn) hidePopover();
    }

    function onKeyDown(e) {
        if (e.key === "Escape") hidePopover();
    }

    function tick() {
        if (document.visibilityState === "visible") paint();
    }

    function init() {
        btn = document.getElementById("smsClockBtn");
        timeEl = document.getElementById("smsClockTime");
        if (!btn || !timeEl) return;

        // Update every second for smooth clock
        paint();
        if (timer) clearInterval(timer);
        timer = setInterval(tick, 1000);
        document.addEventListener("visibilitychange", tick);

        btn.addEventListener("click", function (e) {
            e.stopPropagation();
            if (popover) hidePopover(); else showPopover();
        });

        // Sync timezone from cookie if it changed
        window.addEventListener("storage", function (e) {
            if (e.key === "sms_tz") {
                tz = getEffectiveTimezone();
                paint();
            }
        });
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();