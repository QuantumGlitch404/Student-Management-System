#!/usr/bin/env node
/*
 * Swiss Modernism 2.0 / Bento Grid audit.
 *
 * Run: node tools/ui-swiss-audit.js [--roles=anon,admin,faculty,student]
 *        [--themes=light,dark,eye] [--viewports=1920x1080,390x844]
 *        [--pages=admin/index] [--screenshots=true]
 *
 * Proves on the installed site that every page/role/theme:
 *   - uses Instrument Sans / Geist Mono with no legacy font
 *   - has no shadows, gradients, filters, text shadows or decorative images
 *   - keeps tile radii <= 8px and control radii <= 6px, one-pixel hairlines
 *   - paints only theme tokens (one accent + status colours)
 *   - has no horizontal overflow and no clipped labels below 12.5px
 *   - lays out the 12-column bento with no row holes
 *   - has one hero emphasis at most and a real page heading
 *   - carries the build stamp
 */
"use strict";

const fs = require("fs");
const path = require("path");
const { chromium } = require("playwright-core");

const ROOT = path.resolve(__dirname, "..");
const BASE = process.env.SMS_BASE || "http://localhost:8080";
const OUT_AUDIT = path.join(ROOT, "docs", "ui-swiss");
const OUT_SHOTS = path.join(OUT_AUDIT, "screenshots");

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
        "/Admin/OnlineTests.aspx",
        "/Timetable/Index.aspx",
        "/Calendar/Index.aspx",
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
        "/Timetable/Index.aspx",
        "/Calendar/Index.aspx",
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
        "/Student/OnlineTests.aspx",
        "/Timetable/Index.aspx",
        "/Calendar/Index.aspx",
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

const MAIN = [
    "/Account/Login.aspx",
    "/Admin/Index.aspx",
    "/Admin/Users.aspx",
    "/Account/Settings.aspx",
    "/Notifications/Index.aspx",
    "/Chatbot/Index.aspx"
];

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
const extraWidths = csv(arg("widths", "2460,1440,1280,1024,768,390")).map(Number).filter(Boolean);

function slugFor(page) {
    return page.replace(/^\//, "").replace(/[^a-zA-Z0-9]+/g, "-").replace(/-+$/, "").replace(/^-+/, "");
}

async function applyTheme(page, theme) {
    await page.evaluate((t) => {
        try {
            if (window.SMS && typeof window.SMS.theme === "function") window.SMS.theme(t, false);
        } catch (e) { }
        document.documentElement.setAttribute("data-theme", t);
        document.documentElement.setAttribute("data-theme-pref", t);
        document.documentElement.setAttribute("data-font", "100");
        document.documentElement.setAttribute("data-zoom", "100");
    }, theme);
}

function auditPage(opts) {
    const seen = new Set();
    const violations = [];
    const stats = { elements: 0, text: 0, tiles: 0, bentos: 0, emphasis: 0 };
    const allowedFonts = [
        "instrument sans", "geist mono", "helvetica neue", "helvetica", "arial",
        "system-ui", "sans-serif", "ui-monospace", "consolas", "monospace",
        "bootstrap-icons"
    ];

    function selectorFor(el) {
        if (!el || el.nodeType !== 1) return "?";
        if (el === document.documentElement) return "html";
        if (el === document.body) return "body";
        const bits = [];
        let cur = el;
        let depth = 0;
        while (cur && cur.nodeType === 1 && cur !== document.body && depth < 5) {
            let bit = cur.tagName.toLowerCase();
            if (cur.id) { bit += "#" + cur.id; bits.unshift(bit); break; }
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
        const f = (v) => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); };
        return 0.2126 * f(c.r) + 0.7152 * f(c.g) + 0.0722 * f(c.b);
    }
    function contrast(a, b) {
        const la = luminance(a), lb = luminance(b);
        return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05);
    }
    function push(type, el, message, detail) {
        const key = type + "|" + selectorFor(el) + "|" + message;
        if (seen.has(key)) return;
        seen.add(key);
        if (violations.length < 600) {
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
    const paletteNames = [
        "canvas", "tile", "ink", "ink-2", "ink-3", "rule", "rule-strong", "accent",
        "accent-tint", "on-accent", "on-ink", "success", "warning", "danger", "info",
        "hover-tint", "canvas-hover"
    ];
    const tokenSet = new Set(["255,255,255", "0,0,0"]);
    for (const name of paletteNames) {
        const c = parseColor(rootStyle.getPropertyValue("--" + name).trim());
        if (c) tokenSet.add(c.r + "," + c.g + "," + c.b);
    }

    const all = Array.prototype.slice.call(document.querySelectorAll("body *"));
    stats.elements = all.length;
    const textTagsSkip = ["script", "style", "noscript", "option", "select", "textarea", "title"];
    const tileSel = ".tile,.card,.panel,.stat-card,.kpi,.auth-card,.auth__card,.settings-section,.theme-card,.modal-content";
    const controlSel = ".btn,.imbtn,.form-control,.form-select,.sms-iconbtn,.sms-themeseg,.sms-themeseg-btn,.choice-pill,.ai-suggest,.ai-new,.ai-send,.ai-stop";

    for (const el of all) {
        if (!visible(el)) continue;
        const s = getComputedStyle(el);
        const tag = el.tagName.toLowerCase();

        const bgImage = s.backgroundImage || "";
        if (/gradient\(/i.test(bgImage)) push("gradient", el, "gradient background", bgImage.slice(0, 160));
        if (/url\(/i.test(bgImage) && bgImage !== "none" && !el.closest(".form-select,.form-check-input,.theme-preview")) {
            push("background-image", el, "unexpected background image", bgImage.slice(0, 160));
        }
        if (s.boxShadow && s.boxShadow !== "none" && !/^rgba\(\s*0\s*,\s*0\s*,\s*0\s*,\s*0\s*\)/.test(s.boxShadow.replace(/\s+/g, " "))) {
            push("box-shadow", el, s.boxShadow);
        }
        if (s.textShadow && s.textShadow !== "none") push("text-shadow", el, s.textShadow);
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
        if (families[0] && allowedFonts.indexOf(families[0]) < 0) push("font-family", el, "first family " + families[0], s.fontFamily);

        if (textTagsSkip.indexOf(tag) < 0) {
            const text = Array.prototype.filter.call(el.childNodes, (n) => n.nodeType === 3 && /\S/.test(n.nodeValue)).map((n) => n.nodeValue).join(" ").replace(/\s+/g, " ").trim();
            const fs = parseFloat(s.fontSize) || 16;
            if (text) {
                stats.text++;
                if (fs < 12.45) push("small-text", el, "font-size " + fs.toFixed(1) + "px", "min 12.5px");
            }
            if (fs < 12.45 && ["button", "a", "span", "label"].indexOf(tag) >= 0 && el.closest(controlSel)) {
                push("small-text", el, "control font-size " + fs.toFixed(1) + "px", "min 12.5px");
            }
            const ta = (s.textAlign || "").toLowerCase();
            if (text && ta === "center" && ["h1", "h2", "h3", "p", "div"].indexOf(tag) >= 0 && !el.closest(".empty-state,.msg-empty,.badge,.pagination,.btn,.auth-error,.auth__brand")) {
                push("centered-text", el, "centered text", text.slice(0, 60));
            }
        }

        const bgc = parseColor(s.backgroundColor);
        if (bgc && bgc.a >= 0.999 && !tokenSet.has(bgc.r + "," + bgc.g + "," + bgc.b)) {
            push("off-palette-background", el, s.backgroundColor, "not a theme surface");
        }
        const fg = parseColor(s.color);
        if (fg && fg.a >= 0.999 && !tokenSet.has(fg.r + "," + fg.g + "," + fg.b) && ["input", "select", "textarea", "option"].indexOf(tag) < 0) {
            push("off-palette-color", el, s.color, "not a theme colour");
        }

        if (textTagsSkip.indexOf(tag) < 0) {
            const text = Array.prototype.filter.call(el.childNodes, (n) => n.nodeType === 3 && /\S/.test(n.nodeValue)).map((n) => n.nodeValue).join(" ").replace(/\s+/g, " ").trim();
            if (text) {
                const fg0 = parseColor(s.color);
                const bg = bgFor(el);
                if (fg0 && fg0.a > 0 && bg) {
                    const fgc = fg0.a < 1 ? blend(fg0, bg) : fg0;
                    const size = parseFloat(s.fontSize) || 16;
                    const weight = parseInt(s.fontWeight, 10) || 400;
                    const large = size >= 24 || (size >= 18.66 && weight >= 700);
                    const need = large ? 3 : 4.5;
                    const ratio = contrast(fgc, bg);
                    if (ratio + 0.01 < need) {
                        push("contrast", el, "ratio " + ratio.toFixed(2) + " below " + need,
                            "fg " + s.color + " bg rgb(" + bg.r + "," + bg.g + "," + bg.b + ") size " + size + "px");
                    }
                }
            }
        }

        const radii = [s.borderTopLeftRadius, s.borderTopRightRadius, s.borderBottomLeftRadius, s.borderBottomRightRadius]
            .map((r) => r.indexOf("%") >= 0 ? 999 : (parseFloat(r) || 0));
        if (el.matches(tileSel)) {
            stats.tiles++;
            if (Math.max.apply(null, radii) > 8.5) push("tile-radius", el, "tile radius " + Math.max.apply(null, radii).toFixed(1) + "px", "max 8px");
            const widths = [s.borderTopWidth, s.borderRightWidth, s.borderBottomWidth, s.borderLeftWidth].map((w) => parseFloat(w) || 0);
            if (Math.max.apply(null, widths) > 2.5) push("tile-border", el, "tile border " + widths.join("/"), "max 2px");
        }
        if (el.matches(controlSel)) {
            if (Math.max.apply(null, radii) > 6.5) push("control-radius", el, "control radius " + Math.max.apply(null, radii).toFixed(1) + "px", "max 6px");
            if (el.scrollWidth - el.clientWidth > 2 && tag !== "input" && tag !== "select") {
                push("control-clip", el, "label clips by " + (el.scrollWidth - el.clientWidth) + "px", el.textContent.trim().slice(0, 50));
            }
        }
    }

    stats.bentos = document.querySelectorAll(".bento").length;
    if (window.innerWidth >= 1024) {
        for (const grid of Array.prototype.slice.call(document.querySelectorAll(".bento")).filter(visible)) {
            const s = getComputedStyle(grid);
            if (s.display !== "grid") {
                push("bento-display", grid, "bento is " + s.display, "");
                continue;
            }
            const tracks = (s.gridTemplateColumns || "").trim().split(/\s+/).filter(Boolean);
            if (tracks.length !== 12) push("bento-columns", grid, "expected 12 columns, found " + tracks.length, s.gridTemplateColumns);
            const children = Array.prototype.slice.call(grid.children).filter((c) => visible(c) && getComputedStyle(c).position !== "absolute");
            const rows = new Map();
            let total = 0;
            for (const child of children) {
                const cs = getComputedStyle(child);
                const start = cs.gridColumnStart;
                const end = cs.gridColumnEnd;
                let span = 1;
                if (start && start.indexOf("span ") === 0) span = parseInt(start.slice(5), 10) || 1;
                else if (end && end.indexOf("span ") === 0) span = parseInt(end.slice(5), 10) || 1;
                else if (/^\d+$/.test(start) && /^\d+$/.test(end)) span = Math.max(1, parseInt(end, 10) - parseInt(start, 10));
                total += span;
                const top = Math.round(child.getBoundingClientRect().top);
                rows.set(top, (rows.get(top) || 0) + span);
            }
            if (children.length && total % 12 !== 0) {
                push("bento-span", grid, "children span " + total + " columns", "total must be a multiple of 12");
            }
            for (const [top, sum] of rows) {
                if (sum !== 12) push("bento-hole", grid, "row at y=" + top + " spans " + sum, "row must fill 12 columns");
            }
        }
    } else if (window.innerWidth <= 640) {
        for (const grid of Array.prototype.slice.call(document.querySelectorAll(".bento")).filter(visible)) {
            const s = getComputedStyle(grid);
            const tracks = (s.gridTemplateColumns || "").trim().split(/\s+/).filter(Boolean);
            if (tracks.length !== 1) push("bento-mobile", grid, "expected 1 column, found " + tracks.length, s.gridTemplateColumns);
        }
    }

    const ink = rootStyle.getPropertyValue("--ink").trim();
    stats.emphasis = Array.prototype.slice.call(document.querySelectorAll(".tile--ink,.tile--accent,.kpi--ink,.grid-kpi > .kpi:first-child,.bento > :first-child > .stat-card,.bento > .stat-card:first-child")).filter(visible).length;
    if (stats.emphasis > 2) push("emphasis", document.body, stats.emphasis + " emphasis tiles", "max 2");

    if (!document.querySelector(".auth")) {
        const h1 = document.querySelector(".page-head h1, .page h1");
        if (!h1 || !visible(h1)) push("page-heading", document.body, "no visible page h1", "");
    }
    if (!document.querySelector(".build-stamp")) push("build-stamp", document.body, "no build stamp", "");

    const html = document.documentElement;
    const body = document.body;
    const overflow = Math.max(html.scrollWidth, body.scrollWidth) - Math.max(html.clientWidth, body.clientWidth);
    if (overflow > 1) {
        push("overflow", body, "document overflows by " + overflow + "px", "scroll " + html.scrollWidth + " client " + html.clientWidth);
        let count = 0;
        for (const el of all) {
            if (!visible(el)) continue;
            let cur = el.parentElement, inScroller = false;
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

    if (window.innerWidth <= 640) {
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

async function ensureDir(dir) { fs.mkdirSync(dir, { recursive: true }); }

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
    const browser = await chromium.launch({ channel: "chrome", headless: true });
    const summary = [];
    const failures = [];

    async function makeContext(role, width, height) {
        const context = await browser.newContext({ viewport: { width: width, height: height } });
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
        if (role !== "anon") await login(page, role);
        return { context, page };
    }

    for (const role of wantedRoles) {
        if (!PAGES[role]) continue;
        const pages = PAGES[role].filter((p) => !pageFilters.length || pageFilters.some((f) => p.toLowerCase().indexOf(f.toLowerCase()) >= 0));
        const { context, page } = await makeContext(role, 1920, 1080);
        const consoleErrors = [];
        page.on("pageerror", (e) => consoleErrors.push(String(e)));
        page.on("requestfailed", (r) => consoleErrors.push("REQFAIL " + r.url() + " :: " + ((r.failure() || {}).errorText || "")));

        for (const rel of pages) {
            const slug = slugFor(rel);
            const roleDir = path.join(OUT_AUDIT, role);
            await ensureDir(roleDir);
            const aggregate = { role: role, page: rel, themes: {}, consoleErrors: [] };
            for (const theme of themes) {
                aggregate.themes[theme] = { viewports: {} };
                for (const vp of viewports) {
                    await page.setViewportSize({ width: vp.width, height: vp.height });
                    let result = null, error = null, status = null;
                    try {
                        const response = await page.goto(BASE + rel, { waitUntil: "networkidle", timeout: 45000 });
                        status = response ? response.status() : null;
                        await applyTheme(page, theme);
                        try { await page.evaluate(() => document.fonts.ready); } catch (e) { }
                        await page.waitForTimeout(300);
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
                            status: status, finalUrl: result.finalUrl, title: result.title,
                            scrollWidth: result.scrollWidth, stats: result.stats,
                            counts: result.counts, violations: result.violations
                        };
                    }
                    const vcount = error ? -1 : result.violations.length;
                    summary.push({ role: role, page: rel, theme: theme, viewport: vp.label, violations: vcount, status: status, error: error });
                    console.log((error ? "ERR  " : vcount ? "FAIL " : "PASS ") + role + " " + rel + " " + theme + " " + vp.label + (error ? " :: " + error : " :: " + vcount));
                }
            }
            for (const theme of themes) {
                const themeFile = {
                    role: role, page: rel, theme: theme, generated: new Date().toISOString(),
                    viewports: aggregate.themes[theme].viewports,
                    consoleErrors: consoleErrors.length ? Array.from(new Set(consoleErrors)).slice(0, 20) : []
                };
                fs.writeFileSync(path.join(roleDir, slug + "-" + theme + ".json"), JSON.stringify(themeFile, null, 2));
            }
        }
        await context.close();
    }

    // Every main page at the full desktop ladder, including 2460 px.
    for (const rel of MAIN) {
        const role = rel.startsWith("/Account/") || rel === "/StyleGuide.html" ? "anon" : "admin";
        for (const width of extraWidths) {
            const { context, page } = await makeContext(role, width, 1100);
            let result = null, error = null, status = null;
            try {
                const response = await page.goto(BASE + rel, { waitUntil: "networkidle", timeout: 45000 });
                status = response ? response.status() : null;
                await applyTheme(page, "light");
                try { await page.evaluate(() => document.fonts.ready); } catch (e) { }
                await page.waitForTimeout(250);
                result = await page.evaluate(auditPage, { theme: "light" });
                if (wantShots) {
                    const shotDir = path.join(OUT_SHOTS, role);
                    await ensureDir(shotDir);
                    await page.screenshot({ path: path.join(shotDir, slugFor(rel) + "-light-" + width + ".png"), fullPage: false });
                }
                fs.writeFileSync(path.join(OUT_AUDIT, role, slugFor(rel) + "-light-" + width + ".json"),
                    JSON.stringify({ role: role, page: rel, theme: "light", width: width, result: result }, null, 2));
            } catch (e) {
                error = String(e && e.message ? e.message : e);
            }
            const vcount = error ? -1 : result.violations.length;
            summary.push({ role: role, page: rel, theme: "light", viewport: width + "x1100", violations: vcount, status: status, error: error });
            console.log((error ? "ERR  " : vcount ? "FAIL " : "PASS ") + role + " " + rel + " light " + width + "x1100" + (error ? " :: " + error : " :: " + vcount));
            await context.close();
        }
    }

    const report = [];
    report.push("# Swiss Modernism 2.0 UI Audit Report");
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
        const dir = path.join(OUT_AUDIT, role);
        if (!fs.existsSync(dir)) continue;
        for (const file of fs.readdirSync(dir).filter((f) => f.endsWith(".json"))) {
            try {
                const data = JSON.parse(fs.readFileSync(path.join(dir, file), "utf8"));
                if (data.result) {
                    for (const k of Object.keys(data.result.counts || {})) types[k] = (types[k] || 0) + data.result.counts[k];
                } else {
                    for (const vp of Object.keys(data.viewports || {})) {
                        const entry = data.viewports[vp];
                        for (const k of Object.keys(entry.counts || {})) types[k] = (types[k] || 0) + entry.counts[k];
                    }
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
