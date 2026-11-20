#!/usr/bin/env node
"use strict";

const fs = require("fs");
const path = require("path");
const { chromium } = require("playwright-core");

const ROOT = path.resolve(__dirname, "..");
const BASE = process.env.SMS_BASE || "http://localhost:8080";
const EDGE = process.env.SMS_EDGE || "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const OUT_AUDIT = path.join(ROOT, "docs", "ui-neu");
const OUT_SHOTS = path.join(ROOT, "docs", "ui-neu", "screenshots");

const CREDS = {
    admin: ["admin@sms.edu", "Admin@123"],
    faculty: ["faculty@sms.edu", "Faculty@123"],
    student: ["student@sms.edu", "Student@123"]
};

const PAGES = {
    anon: [
        "/Account/Login.aspx",
        "/Account/Signup.aspx",
        "/Account/Forgot.aspx",
        "/Account/Reset.aspx",
        "/Account/Verify.aspx",
        "/Account/Error.aspx?code=403",
        "/Account/Error.aspx?code=404",
        "/Account/Logout.aspx",
        "/StyleGuide.html"
    ],
    admin: [
        "/Admin/Index.aspx",
        "/Admin/Users.aspx",
        "/Admin/Academics.aspx",
        "/Admin/Sessions.aspx",
        "/Admin/Attendance.aspx",
        "/Admin/Exams.aspx",
        "/Admin/ImportExport.aspx",
        "/Admin/Fees.aspx",
        "/Admin/Payments.aspx",
        "/Admin/Notifications.aspx",
        "/Admin/Announcements.aspx",
        "/Admin/Complaints.aspx",
        "/Admin/Audit.aspx",
        "/Admin/Settings.aspx",
        "/Account/Settings.aspx",
        "/Notifications/Index.aspx",
        "/Chatbot/Index.aspx"
    ],
    faculty: [
        "/Faculty/Index.aspx",
        "/Faculty/Attendance.aspx",
        "/Faculty/Marks.aspx",
        "/Faculty/Complaints.aspx",
        "/Faculty/Profile.aspx",
        "/Account/Settings.aspx",
        "/Notifications/Index.aspx",
        "/Chatbot/Index.aspx"
    ],
    student: [
        "/Student/Index.aspx",
        "/Student/Attendance.aspx",
        "/Student/Results.aspx",
        "/Student/Fees.aspx",
        "/Student/PayCheckout.aspx",
        "/Student/Notifications.aspx",
        "/Student/Complaint.aspx",
        "/Student/Profile.aspx",
        "/Account/Settings.aspx",
        "/Notifications/Index.aspx",
        "/Chatbot/Index.aspx"
    ]
};

function arg(name, fallback) {
    const hit = process.argv.find((a) => a.startsWith("--" + name + "="));
    return hit ? hit.slice(name.length + 3) : fallback;
}

function csv(value) {
    return String(value || "").split(",").map((s) => s.trim()).filter(Boolean);
}

const wantedRoles = csv(arg("roles", "anon,admin,faculty,student"));
const themes = csv(arg("themes", "light,dark,eye"));
const viewports = csv(arg("viewports", "1920x1080,390x844")).map((v) => {
    const [w, h] = v.split("x").map(Number);
    return { width: w || 1920, height: h || 1080, label: v };
});
const pageFilters = csv(arg("pages", ""));
const wantShots = arg("screenshots", "false") === "true";

function slugFor(page) {
    return page.replace(/^\//, "").replace(/[^a-zA-Z0-9]+/g, "-").replace(/-+$/, "").replace(/^-+/, "");
}

async function applyTheme(page, theme) {
    await page.evaluate((t) => {
        try {
            if (window.SMS && typeof window.SMS.theme === "function") window.SMS.theme(t, false);
        } catch (e) { /* manual fallback below */ }
        document.documentElement.setAttribute("data-theme", t);
        document.documentElement.setAttribute("data-theme-pref", t);
        document.documentElement.setAttribute("data-font", "100");
        document.documentElement.setAttribute("data-zoom", "100");
    }, theme);
}

function auditPage(opts) {
    const seen = new Set();
    const violations = [];
    const allowedFonts = ["red hat display", "figtree", "dm mono", "bootstrap-icons", "segoe ui", "arial", "system-ui", "sans-serif", "consolas", "monospace"];

    function selectorFor(el) {
        if (!el || el.nodeType !== 1) return "?";
        if (el === document.documentElement) return "html";
        if (el === document.body) return "body";
        const bits = [];
        let cur = el;
        let depth = 0;
        while (cur && cur.nodeType === 1 && cur !== document.body && depth < 5) {
            let bit = cur.tagName.toLowerCase();
            if (cur.id) {
                bit += "#" + cur.id;
                bits.unshift(bit);
                break;
            }
            const cls = (cur.getAttribute("class") || "").trim().split(/\s+/).filter(Boolean).slice(0, 2);
            if (cls.length) bit += "." + cls.join(".");
            bits.unshift(bit);
            cur = cur.parentElement;
            depth++;
        }
        return bits.join(" > ");
    }

    const skipAncestors = "[hidden],[aria-hidden=true],.dropdown-menu:not(.show),.modal:not(.show),.offcanvas:not(.show),.collapse:not(.show),.sms-toast-wrap,.visually-hidden,.sr-only";

    function visible(el) {
        if (!el.getClientRects || !el.getClientRects().length) return false;
        const s = getComputedStyle(el);
        if (s.display === "none" || s.visibility !== "visible") return false;
        if (parseFloat(s.opacity) < 0.02) return false;
        if (el.closest(skipAncestors)) return false;
        return true;
    }

    function parseColor(value) {
        if (!value) return null;
        let m = String(value).match(/^rgba?\(([^)]+)\)$/i);
        if (m) {
            const p = m[1].split(/[,\s/]+/).filter(Boolean).map(Number);
            if (p.length >= 3) {
                const a = p.length >= 4 ? (isNaN(p[3]) ? 1 : p[3]) : 1;
                return { r: p[0], g: p[1], b: p[2], a: a };
            }
        }
        m = String(value).match(/^#([0-9a-f]{3,8})$/i);
        if (m) {
            let h = m[1];
            if (h.length === 3) h = h.split("").map((c) => c + c).join("");
            const r = parseInt(h.slice(0, 2), 16);
            const g = parseInt(h.slice(2, 4), 16);
            const b = parseInt(h.slice(4, 6), 16);
            const a = h.length === 8 ? parseInt(h.slice(6, 8), 16) / 255 : 1;
            return { r: r, g: g, b: b, a: a };
        }
        if (/^transparent$/i.test(value)) return { r: 0, g: 0, b: 0, a: 0 };
        return null;
    }

    function blend(top, bottom) {
        const a = top.a + bottom.a * (1 - top.a);
        if (a <= 0) return { r: 0, g: 0, b: 0, a: 0 };
        return {
            r: Math.round((top.r * top.a + bottom.r * bottom.a * (1 - top.a)) / a),
            g: Math.round((top.g * top.a + bottom.g * bottom.a * (1 - top.a)) / a),
            b: Math.round((top.b * top.a + bottom.b * bottom.a * (1 - top.a)) / a),
            a: a
        };
    }

    function bgFor(el) {
        const layers = [];
        let cur = el;
        while (cur && cur.nodeType === 1) {
            const s = getComputedStyle(cur);
            if (s.backgroundImage && s.backgroundImage !== "none" && /gradient\(/i.test(s.backgroundImage)) return null;
            const c = parseColor(s.backgroundColor);
            if (c && c.a > 0) {
                layers.push(c);
                if (c.a >= 1) break;
            }
            cur = cur.parentElement;
        }
        if (!layers.length) return { r: 255, g: 255, b: 255, a: 1 };
        let out = layers[layers.length - 1];
        for (let i = layers.length - 2; i >= 0; i--) out = blend(layers[i], out);
        return out.a >= 1 ? out : blend(out, { r: 255, g: 255, b: 255, a: 1 });
    }

    function luminance(c) {
        const f = (v) => {
            v /= 255;
            return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
        };
        return 0.2126 * f(c.r) + 0.7152 * f(c.g) + 0.0722 * f(c.b);
    }

    function contrast(a, b) {
        const la = luminance(a);
        const lb = luminance(b);
        return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05);
    }

    function push(type, el, message, detail) {
        const key = type + "|" + selectorFor(el) + "|" + message;
        if (seen.has(key)) return;
        seen.add(key);
        if (violations.length < 250) {
            violations.push({
                type: type,
                selector: selectorFor(el),
                message: message,
                text: (el && el.textContent ? el.textContent : "").replace(/\s+/g, " ").trim().slice(0, 80),
                detail: detail || ""
            });
        }
    }

    const rootStyle = getComputedStyle(document.documentElement);
    const tokenNames = ["base", "hl", "sh", "ink", "ink-2", "ink-3", "accent", "accent-ink", "on-accent", "success", "warning", "danger", "info"];
    const tokenSet = new Set();
    const tokenRaw = {};
    for (const name of tokenNames) {
        const raw = rootStyle.getPropertyValue("--" + name).trim();
        tokenRaw[name] = raw;
        const c = parseColor(raw);
        if (c) tokenSet.add(c.r + "," + c.g + "," + c.b);
    }
    const base = parseColor(tokenRaw.base);
    tokenSet.add("255,255,255");
    tokenSet.add("0,0,0");

    function isScrim(el) {
        return !!(el.closest && el.closest(".modal-backdrop,.sidebar-scrim,.offcanvas-backdrop"));
    }

    const all = Array.prototype.slice.call(document.querySelectorAll("body *"));
    const stats = { elements: all.length, text: 0, shadows: 0, radii: 0, surfaces: 0 };
    const textTagsSkip = ["script", "style", "noscript", "option", "select", "textarea", "title"];

    for (const el of all) {
        if (!visible(el)) continue;
        const s = getComputedStyle(el);
        const tag = el.tagName.toLowerCase();
        if (textTagsSkip.indexOf(tag) >= 0) continue;
        if (el.disabled || el.closest(".disabled,[disabled]")) continue;

        const text = Array.prototype.filter.call(el.childNodes, (n) => n.nodeType === 3 && /\S/.test(n.nodeValue)).map((n) => n.nodeValue).join(" ").replace(/\s+/g, " ").trim();
        if (text) {
            stats.text++;
            const fg0 = parseColor(s.color);
            const bg = bgFor(el);
            if (fg0 && fg0.a > 0 && bg) {
                const fg = fg0.a < 1 ? blend(fg0, bg) : fg0;
                const size = parseFloat(s.fontSize) || 16;
                const weight = parseInt(s.fontWeight, 10) || 400;
                const large = size >= 24 || (size >= 18.66 && weight >= 700);
                const need = large ? 3 : 4.5;
                const ratio = contrast(fg, bg);
                if (ratio + 0.01 < need) {
                    push("contrast", el, "ratio " + ratio.toFixed(2) + " below " + need, "fg " + s.color + " bg rgb(" + bg.r + "," + bg.g + "," + bg.b + ") size " + size + "px");
                }
            }
            const fs = parseFloat(s.fontSize) || 16;
            if (fs < 12.45) push("small-text", el, "font-size " + fs.toFixed(1) + "px", "min 12.5px at 100% text and zoom");
            if (s.textShadow && s.textShadow !== "none") push("text-shadow", el, s.textShadow);
            const clip = (s.overflowX === "hidden" || s.overflowX === "clip") && s.textOverflow !== "ellipsis";
            if (clip && ["input", "select", "textarea"].indexOf(tag) < 0 && el.scrollWidth - el.clientWidth > 2) {
                push("text-clip", el, "text clips by " + (el.scrollWidth - el.clientWidth) + "px", "overflow-x " + s.overflowX);
            }
        }

        if (!isScrim(el)) {
            const fg = parseColor(s.color);
            if (fg && fg.a > 0.001 && fg.a >= 0.999 && !tokenSet.has(fg.r + "," + fg.g + "," + fg.b)) {
                push("off-palette-color", el, s.color, "not a theme colour");
            }
            const bgc = parseColor(s.backgroundColor);
            if (bgc && bgc.a > 0.001 && bgc.a >= 0.999 && !tokenSet.has(bgc.r + "," + bgc.g + "," + bgc.b)) {
                push("off-palette-background", el, s.backgroundColor, "not a theme surface");
            }
        }

        const bgImage = s.backgroundImage || "";
        if (/gradient\(/i.test(bgImage)) push("gradient", el, "gradient background", bgImage.slice(0, 160));
        if (/url\(/i.test(bgImage) && bgImage !== "none" && !el.closest(".btn-close,.form-select,.form-check-input,.auth-theme-preview,.theme-preview")) {
            push("background-image", el, "unexpected background image", bgImage.slice(0, 160));
        }
        if ((s.backdropFilter && s.backdropFilter !== "none") || (s.filter && s.filter !== "none")) {
            push("filter", el, "filter/backdrop-filter", (s.backdropFilter || "") + " " + (s.filter || ""));
        }
        if (s.transform && s.transform !== "none") {
            const m = s.transform.match(/^matrix\(([^)]+)\)$/);
            if (m) {
                const p = m[1].split(",").map(Number);
                if (Math.abs(p[1] || 0) > 0.001 || Math.abs(p[2] || 0) > 0.001) push("transform-rotate", el, "rotate/skew", s.transform);
            }
        }

        const families = (s.fontFamily || "").split(",").map((f) => f.trim().replace(/^["']|["']$/g, "").toLowerCase());
        const first = families[0];
        if (first && allowedFonts.indexOf(first) < 0) push("font-family", el, "first family " + first, s.fontFamily);
    }

    function radiusFloors() {
        const groups = [
            { sel: ".btn:not(.btn-link),.imbtn,.form-control,.form-select,.sms-iconbtn,.sms-themeseg", min: 8 },
            { sel: ".card,.panel,.kpi,.stat-card,.modal-content,.auth__card,.auth-card,.settings-section,.theme-card,.alert,.badge,.sms-toast", min: 8 }
        ];
        for (const group of groups) {
            for (const el of Array.prototype.slice.call(document.querySelectorAll(group.sel)).filter(visible)) {
                const s = getComputedStyle(el);
                const radii = [s.borderTopLeftRadius, s.borderTopRightRadius, s.borderBottomLeftRadius, s.borderBottomRightRadius].map((r) => r.indexOf("%") >= 0 ? 999 : (parseFloat(r) || 0));
                stats.radii++;
                if (Math.min.apply(null, radii) < group.min) push("radius", el, "corner radius " + Math.min.apply(null, radii).toFixed(1) + "px", "min " + group.min + "px");
            }
        }
    }
    radiusFloors();

    function hardBorders() {
        const sels = ".btn:not(.btn-link),.imbtn,.card,.panel,.kpi,.stat-card,.modal-content,.auth__card,.auth-card,.settings-section,.theme-card,.alert,.badge,.sms-themeseg";
        for (const el of Array.prototype.slice.call(document.querySelectorAll(sels)).filter(visible)) {
            const s = getComputedStyle(el);
            const widths = [s.borderTopWidth, s.borderRightWidth, s.borderBottomWidth, s.borderLeftWidth].map((w) => parseFloat(w) || 0);
            if (Math.max.apply(null, widths) > 1.5) push("hard-border", el, "border " + widths.join("/"), "max 1px");
        }
    }
    hardBorders();

    function shadowChecks() {
        const raised = ".card,.panel,.kpi,.stat-card,.modal-content,.auth__card,.auth-card,.settings-section,.theme-card,.sms-themeseg";
        for (const el of Array.prototype.slice.call(document.querySelectorAll(raised)).filter(visible)) {
            const s = getComputedStyle(el);
            if (!s.boxShadow || s.boxShadow === "none") {
                push("missing-shadow", el, "raised surface has no shadow", "");
                continue;
            }
            stats.shadows++;
            const parts = s.boxShadow.split(/,(?![^(]*\))/);
            if (parts.length < 2) push("shadow-pair", el, "single shadow", s.boxShadow);
        }
    }
    shadowChecks();

    function surfaceChecks() {
        const sels = "body,.app-main,.app-content,.app-sidebar,.topbar,.panel,.card,.kpi,.stat-card,.auth__card,.auth-card,.modal-content,.settings-section";
        for (const el of Array.prototype.slice.call(document.querySelectorAll(sels)).filter(visible)) {
            const bg = bgFor(el);
            if (!bg) continue;
            stats.surfaces++;
            if (base && (bg.r !== base.r || bg.g !== base.g || bg.b !== base.b)) {
                push("surface", el, "surface rgb(" + bg.r + "," + bg.g + "," + bg.b + ")", "base rgb(" + base.r + "," + base.g + "," + base.b + ")");
            }
        }
    }
    surfaceChecks();

    function buttonChecks() {
        for (const el of Array.prototype.slice.call(document.querySelectorAll(".btn,.imbtn,.sms-iconbtn,.sms-themeseg-btn,.choice-pill,.ai-suggest")).filter(visible)) {
            const s = getComputedStyle(el);
            if ((s.textTransform || "").toLowerCase() === "uppercase") push("uppercase-button", el, "uppercase button label", el.textContent.trim().slice(0, 40));
            if (el.scrollWidth - el.clientWidth > 2 && parseFloat(s.fontSize) > 0) {
                push("button-clip", el, "label clips by " + (el.scrollWidth - el.clientWidth) + "px", el.textContent.trim().slice(0, 40));
            }
            const fs = parseFloat(s.fontSize) || 16;
            if (fs < 12.45) push("small-text", el, "button font-size " + fs.toFixed(1) + "px", "");
        }
    }
    buttonChecks();

    function fontContract() {
        function expect(sel, family, label) {
            for (const el of Array.prototype.slice.call(document.querySelectorAll(sel)).filter(visible).slice(0, 40)) {
                const f = (getComputedStyle(el).fontFamily || "").toLowerCase();
                if (f.indexOf(family) < 0) push("font-contract", el, label + " should use " + family, f);
            }
        }
        expect("body", "figtree", "Body");
        expect("h1,h2,h3,.kpi__value,.brand-name,.auth-title", "red hat display", "Display");
        expect(".mono,.num,.badge,.kicker,.table thead th,.sidebar-role,.auth-brand-sub", "dm mono", "Mono");
    }
    fontContract();

    const html = document.documentElement;
    const body = document.body;
    const overflow = Math.max(html.scrollWidth, body.scrollWidth) - Math.max(html.clientWidth, body.clientWidth);
    if (overflow > 1) {
        push("overflow", body, "document overflows by " + overflow + "px", "scroll " + html.scrollWidth + " client " + html.clientWidth);
        let count = 0;
        for (const el of all) {
            if (!visible(el)) continue;
            let cur = el.parentElement;
            let inScroller = false;
            while (cur && cur !== body) {
                const ox = getComputedStyle(cur).overflowX;
                if (ox === "auto" || ox === "scroll" || ox === "hidden") { inScroller = true; break; }
                cur = cur.parentElement;
            }
            if (inScroller) continue;
            const r = el.getBoundingClientRect();
            const pos = getComputedStyle(el).position;
            if (pos === "fixed" || pos === "absolute") continue;
            if (r.width > 0 && (r.right > window.innerWidth + 1 || r.left < -1)) {
                push("overflow-element", el, "extends " + Math.round(r.left) + ".." + Math.round(r.right), "viewport " + window.innerWidth);
                if (++count >= 12) break;
            }
        }
    }

    const content = document.querySelector(".app-content");
    if (content && content.scrollWidth - content.clientWidth > 1) {
        push("content-overflow", content, "content overflows by " + (content.scrollWidth - content.clientWidth) + "px", "");
    }

    if (window.innerWidth <= 600) {
        const tools = document.querySelector(".topbar-tools");
        if (tools) {
            const tr = tools.getBoundingClientRect();
            if (tr.right > window.innerWidth + 1 || tr.left < -1) push("topbar-tools", tools, "tools outside viewport " + Math.round(tr.left) + ".." + Math.round(tr.right), "");
            for (const child of Array.prototype.slice.call(tools.children).filter(visible)) {
                const r = child.getBoundingClientRect();
                if (r.right > window.innerWidth + 1 || r.left < -1) push("topbar-tools-child", child, "outside viewport " + Math.round(r.left) + ".." + Math.round(r.right), "");
            }
        }
        const auth = document.querySelector(".auth-appearance,.auth__appearance");
        if (auth) {
            const ar = auth.getBoundingClientRect();
            if (ar.right > window.innerWidth + 1 || ar.left < -1) push("auth-appearance", auth, "outside viewport " + Math.round(ar.left) + ".." + Math.round(ar.right), "");
            if (getComputedStyle(auth).position === "absolute") push("auth-appearance", auth, "still absolutely positioned on mobile", "");
            const font = auth.querySelector(".sms-fontctl");
            const zoom = auth.querySelector(".sms-zoomctl");
            if (font && getComputedStyle(font).display === "none") push("auth-controls", font, "text-size control hidden on mobile", "");
            if (zoom && getComputedStyle(zoom).display === "none") push("auth-controls", zoom, "zoom control hidden on mobile", "");
        }
        const head = document.querySelector(".ai-head");
        if (head) {
            const title = head.querySelector(".ai-head-title");
            const actions = Array.prototype.slice.call(head.querySelectorAll(".imbtn,.btn,.sms-iconbtn")).filter(visible);
            for (const a of actions) {
                const r = a.getBoundingClientRect();
                if (r.right > window.innerWidth + 1 || r.left < -1) push("chat-head-action", a, "action outside viewport " + Math.round(r.left) + ".." + Math.round(r.right), "");
                if (title && r.width > 0) {
                    const tr = title.getBoundingClientRect();
                    if (r.left < tr.right - 1 && r.right > tr.left + 1) push("chat-head-overlap", a, "action overlaps title", "");
                }
            }
        }
    }

    return {
        title: document.title,
        url: location.href,
        theme: opts.theme,
        viewport: { width: window.innerWidth, height: window.innerHeight },
        scrollWidth: Math.max(html.scrollWidth, body.scrollWidth),
        stats: stats,
        violations: violations,
        counts: violations.reduce((acc, v) => { acc[v.type] = (acc[v.type] || 0) + 1; return acc; }, {})
    };
}

async function ensureDir(dir) {
    fs.mkdirSync(dir, { recursive: true });
}

async function login(page, role) {
    const creds = CREDS[role];
    if (!creds) return;
    await page.goto(BASE + "/Account/Login.aspx", { waitUntil: "networkidle" });
    await page.fill('input[name$="txtEmail"]', creds[0]);
    await page.fill('input[name$="txtPassword"]', creds[1]);
    await Promise.all([
        page.waitForNavigation({ waitUntil: "networkidle" }),
        page.click('input[name$="btnSignIn"]')
    ]);
}

(async () => {
    await ensureDir(OUT_AUDIT);
    if (wantShots) await ensureDir(OUT_SHOTS);
    const browser = await chromium.launch({ executablePath: EDGE, headless: true });
    const summary = [];
    const failures = [];

    for (const role of wantedRoles) {
        if (!PAGES[role]) continue;
        const pages = PAGES[role].filter((p) => !pageFilters.length || pageFilters.some((f) => p.toLowerCase().indexOf(f.toLowerCase()) >= 0));
        const context = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
        await context.addInitScript(() => {
            try {
                const stored = JSON.parse(localStorage.getItem("sms.appearance") || "{}");
                stored.theme = "light";
                stored.font = 100;
                stored.zoom = 100;
                stored.motion = "full";
                localStorage.setItem("sms.appearance", JSON.stringify(stored));
            } catch (e) { }
            try {
                document.documentElement.setAttribute("data-theme", "light");
                document.documentElement.setAttribute("data-font", "100");
                document.documentElement.setAttribute("data-zoom", "100");
            } catch (e) { }
        });
        await context.addCookies([
            { name: "sms_theme", value: "light", domain: "localhost", path: "/" },
            { name: "sms_font", value: "100", domain: "localhost", path: "/" },
            { name: "sms_zoom", value: "100", domain: "localhost", path: "/" }
        ]);
        const page = await context.newPage();
        const consoleErrors = [];
        page.on("pageerror", (e) => consoleErrors.push(String(e)));
        page.on("requestfailed", (r) => consoleErrors.push("REQFAIL " + r.url() + " :: " + ((r.failure() || {}).errorText || "")));

        if (role !== "anon") await login(page, role);

        for (const rel of pages) {
            const url = BASE + rel;
            const slug = slugFor(rel);
            const roleDir = path.join(OUT_AUDIT, role);
            await ensureDir(roleDir);
            const aggregate = { role: role, page: rel, viewports: {}, themes: {}, consoleErrors: [] };
            for (const theme of themes) {
                aggregate.themes[theme] = { viewports: {} };
                for (const vp of viewports) {
                    await page.setViewportSize({ width: vp.width, height: vp.height });
                    let result = null;
                    let error = null;
                    let status = null;
                    try {
                        const response = await page.goto(url, { waitUntil: "networkidle", timeout: 45000 });
                        status = response ? response.status() : null;
                        await applyTheme(page, theme);
                        try { await page.evaluate(() => document.fonts.ready); } catch (e) { }
                        await page.waitForTimeout(350);
                        await page.evaluate(() => window.scrollTo(0, 0));
                        result = await page.evaluate(auditPage, { theme: theme });
                        result.status = status;
                        result.requested = rel;
                        result.finalUrl = page.url();
                        if (wantShots) {
                            const shotDir = path.join(OUT_SHOTS, role);
                            await ensureDir(shotDir);
                            await page.screenshot({ path: path.join(shotDir, slug + "-" + theme + "-" + vp.width + ".png"), fullPage: false });
                        }
                    } catch (e) {
                        error = String(e && e.message ? e.message : e);
                    }

                    const key = vp.label;
                    if (error) {
                        aggregate.themes[theme].viewports[key] = { error: error };
                        failures.push(role + " " + rel + " " + theme + " " + vp.label + ": " + error);
                    } else {
                        aggregate.themes[theme].viewports[key] = {
                            status: status,
                            finalUrl: result.finalUrl,
                            title: result.title,
                            scrollWidth: result.scrollWidth,
                            stats: result.stats,
                            counts: result.counts,
                            violations: result.violations
                        };
                    }
                    const vcount = error ? -1 : result.violations.length;
                    summary.push({ role: role, page: rel, theme: theme, viewport: vp.label, violations: vcount, status: status, error: error });
                    console.log((error ? "ERR  " : vcount ? "FAIL " : "PASS ") + role + " " + rel + " " + theme + " " + vp.label + (error ? " :: " + error : " :: " + vcount));
                }
            }
            const uniqErrors = consoleErrors.length ? Array.from(new Set(consoleErrors)).slice(0, 20) : [];
            for (const theme of themes) {
                const themeFile = {
                    role: role,
                    page: rel,
                    theme: theme,
                    generated: new Date().toISOString(),
                    viewports: aggregate.themes[theme].viewports,
                    consoleErrors: uniqErrors
                };
                fs.writeFileSync(path.join(roleDir, slug + "-" + theme + ".json"), JSON.stringify(themeFile, null, 2));
            }
        }
        await context.close();
    }

    const report = [];
    report.push("# Neumorphism UI Audit Report");
    report.push("");
    report.push("Generated " + new Date().toISOString());
    report.push("");
    const total = summary.reduce((n, s) => n + (s.violations > 0 ? s.violations : 0), 0);
    const failing = summary.filter((s) => s.violations > 0 || s.error).length;
    report.push("- Checks run: " + summary.length);
    report.push("- Failing checks: " + failing);
    report.push("- Total violations: " + total);
    report.push("- Pass criterion: zero violations across every role, page, theme and viewport.");
    report.push("");
    report.push("| Role | Page | Theme | Viewport | Status | Violations |");
    report.push("|---|---|---|---|---|---|");
    for (const s of summary) {
        report.push("| " + s.role + " | " + s.page + " | " + s.theme + " | " + s.viewport + " | " + (s.error ? "error" : s.status) + " | " + (s.error ? s.error : s.violations) + " |");
    }
    report.push("");
    report.push("## Violation types");
    report.push("");
    const types = {};
    for (const role of wantedRoles) {
        if (!PAGES[role]) continue;
        for (const file of fs.readdirSync(path.join(OUT_AUDIT, role)).filter((f) => f.endsWith(".json"))) {
            try {
                const data = JSON.parse(fs.readFileSync(path.join(OUT_AUDIT, role, file), "utf8"));
                for (const vp of Object.keys(data.viewports || {})) {
                    const entry = data.viewports[vp];
                    for (const k of Object.keys(entry.counts || {})) types[k] = (types[k] || 0) + entry.counts[k];
                }
            } catch (e) { }
        }
    }
    for (const k of Object.keys(types).sort((a, b) => types[b] - types[a])) report.push("- " + k + ": " + types[k]);
    report.push("");
    fs.writeFileSync(path.join(OUT_AUDIT, "REPORT.md"), report.join("\n"));
    await browser.close();
    console.log("REPORT " + path.join(OUT_AUDIT, "REPORT.md"));
    if (failures.length) console.log("FAILURES:\n" + failures.join("\n"));
    if (failing) process.exitCode = 2;
})().catch((e) => { console.error(e); process.exit(1); });
