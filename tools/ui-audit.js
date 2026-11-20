#!/usr/bin/env node
"use strict";

const fs = require("fs");
const path = require("path");
const { chromium } = require("playwright-core");

const ROOT = path.resolve(__dirname, "..");
const BASE = process.env.SMS_BASE || "http://localhost:8080";
const EDGE = process.env.SMS_EDGE || "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const OUT_AUDIT = path.join(ROOT, "docs", "ui-audit");
const OUT_SHOTS = path.join(ROOT, "docs", "ui-screenshots");

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
const viewports = csv(arg("viewports", "1440x900,768x1024,390x844")).map((v) => {
  const [w, h] = v.split("x").map(Number);
  return { width: w || 1440, height: h || 900, label: v };
});
const pageFilters = csv(arg("pages", ""));
const wantShots = arg("screenshots", "true") !== "false";

function slugFor(page) {
  return page.replace(/^\//, "").replace(/[^a-zA-Z0-9]+/g, "-").replace(/-+$/, "").replace(/^-+/, "");
}

async function applyTheme(page, theme) {
  await page.evaluate((t) => {
    try {
      if (window.SMS && typeof window.SMS.theme === "function") {
        window.SMS.theme(t, false);
        return;
      }
    } catch (e) { /* fall through */ }
    document.documentElement.setAttribute("data-theme", t);
    document.documentElement.setAttribute("data-theme-pref", t);
  }, theme);
}

function auditPage(opts) {
  const seen = new Set();
  const violations = [];
  const allowedFonts = ["archivo", "schibsted grotesk", "space mono", "bootstrap-icons", "font awesome 6 free", "font awesome 6 brands", "font awesome 5 free", "fontawesome"];
  const radiusAllowed = [
    "input[type=radio]",
    ".form-check-input[type=radio]",
    ".sidebar-avatar",
    ".sm-avatar",
    ".topbar-avatar",
    ".ai-avatar"
  ];
  const skipAncestors = "[hidden],[aria-hidden=true],.dropdown-menu:not(.show),.modal:not(.show),.offcanvas:not(.show),.collapse:not(.show),.sms-toast-wrap,.visually-hidden,.sr-only,.visuallyhidden";

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
      const parent = cur.parentElement;
      if (parent) {
        const same = Array.prototype.filter.call(parent.children, (c) => c.tagName === cur.tagName);
        if (same.length > 1) bit += ":nth-of-type(" + (same.indexOf(cur) + 1) + ")";
      }
      bits.unshift(bit);
      cur = parent;
      depth++;
    }
    return bits.join(" > ");
  }

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
    let m = value.match(/^rgba?\(([^)]+)\)$/i);
    if (m) {
      const p = m[1].split(/[,\s/]+/).filter(Boolean).map(Number);
      if (p.length >= 3) {
        const a = p.length >= 4 ? (isNaN(p[3]) ? 1 : p[3]) : 1;
        return { r: p[0], g: p[1], b: p[2], a: a };
      }
    }
    m = value.match(/^#([0-9a-f]{3,8})$/i);
    if (m) {
      let h = m[1];
      if (h.length === 3) h = h.split("").map((c) => c + c).join("");
      const r = parseInt(h.slice(0, 2), 16);
      const g = parseInt(h.slice(2, 4), 16);
      const b = parseInt(h.slice(4, 6), 16);
      const a = h.length === 8 ? parseInt(h.slice(6, 8), 16) / 255 : 1;
      return { r, g, b, a };
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
      if (s.backgroundImage && s.backgroundImage !== "none") return null;
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

  const rootStyle = getComputedStyle(document.documentElement);
  const tokenNames = [
    "paper", "card", "ink", "ink-2", "ink-3", "yellow", "yellow-soft", "blue", "tomato", "green", "orange",
    "danger-ink", "success-ink", "warning-ink", "shadow-color",
    "on-yellow", "on-blue", "on-tomato", "on-green", "on-orange"
  ];
  const tokenSet = new Set();
  for (const name of tokenNames) {
    const raw = rootStyle.getPropertyValue("--" + name).trim();
    const c = parseColor(raw);
    if (c) tokenSet.add(c.r + "," + c.g + "," + c.b);
  }
  tokenSet.add("255,255,255");
  tokenSet.add("17,17,17");
  tokenSet.add("217,207,175");
  tokenSet.add("58,54,43");
  tokenSet.add("205,190,146");

  function isScrim(el) {
    return !!(el.closest && el.closest(".modal-backdrop,.sidebar-scrim,.offcanvas-backdrop"));
  }

  function checkColor(el, value, prop) {
    const c = parseColor(value);
    if (!c || c.a === 0) return;
    if (prop === "color" && el.tagName === "INPUT") {
      const type = (el.getAttribute("type") || "text").toLowerCase();
      if (["checkbox", "radio", "file", "range", "color", "hidden"].indexOf(type) >= 0) return;
    }
    if (!isScrim(el)) {
      if (c.a < 0.999) {
        push("alpha-color", el, prop + " alpha " + c.a.toFixed(2), value);
      } else if (!tokenSet.has(c.r + "," + c.g + "," + c.b)) {
        push("off-token", el, prop + " " + value, "not a theme token");
      }
    }
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

  function inScroller(el) {
    let cur = el.parentElement;
    while (cur && cur !== document.body && cur !== document.documentElement) {
      const ox = getComputedStyle(cur).overflowX;
      if (ox === "auto" || ox === "scroll" || ox === "hidden") return true;
      cur = cur.parentElement;
    }
    return false;
  }

  const all = Array.prototype.slice.call(document.querySelectorAll("body *"));
  const textCount = { checked: 0, skipped: 0 };
  const stats = { elements: all.length, borderless: 0, softShadows: 0, gradients: 0, gradientsList: [], smallText: 0, longTransitions: 0 };

  for (const el of all) {
    if (!visible(el)) continue;
    const s = getComputedStyle(el);
    const tag = el.tagName.toLowerCase();
    if (["script", "style", "noscript", "option", "select", "textarea", "title"].indexOf(tag) >= 0) continue;
    if (el.disabled || el.closest(".disabled,[disabled]")) continue;

    const text = Array.prototype.filter.call(el.childNodes, (n) => n.nodeType === 3 && /\S/.test(n.nodeValue)).map((n) => n.nodeValue).join(" ").replace(/\s+/g, " ").trim();
    if (text) {
      textCount.checked++;
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
      if (fs < 12.4) {
        stats.smallText++;
        push("small-text", el, "font-size " + fs.toFixed(1) + "px", "min 12.48px");
      }
      const shadow = s.textShadow;
      if (shadow && shadow !== "none") push("text-shadow", el, shadow);
      const ox = s.overflowX;
      if ((ox === "hidden" || ox === "clip") && s.textOverflow !== "ellipsis" && ["input", "select", "textarea"].indexOf(tag) < 0 && el.scrollWidth - el.clientWidth > 2) {
        push("text-clip", el, "text clipped by " + (el.scrollWidth - el.clientWidth) + "px", "overflow-x " + ox);
      }
    }

    const radius = [s.borderTopLeftRadius, s.borderTopRightRadius, s.borderBottomLeftRadius, s.borderBottomRightRadius].map((r) => parseFloat(r) || 0);
    if (radius.some((r) => r > 0.5)) {
      let allowed = radiusAllowed.some((sel) => {
        try { return el.matches(sel); } catch (e) { return false; }
      });
      if (!allowed && (el.getAttribute("class") || "").toLowerCase().indexOf("avatar") >= 0) allowed = true;
      if (!allowed && tag === "img") allowed = true;
      if (!allowed) push("radius", el, "border-radius " + s.borderTopLeftRadius, "");
    }

    if (s.boxShadow && s.boxShadow !== "none") {
      const parts = s.boxShadow.split(/,(?![^(]*\))/);
      for (const part of parts) {
        const px = (part.match(/-?\d*\.?\d+px/g) || []).map((n) => parseFloat(n));
        if (/inset/i.test(part)) {
          stats.softShadows++;
          push("shadow", el, "inset shadow", part.trim());
          continue;
        }
        const blur = px.length >= 3 ? px[2] : 0;
        const spread = px.length >= 4 ? px[3] : 0;
        if (blur !== 0 || spread !== 0) {
          stats.softShadows++;
          push("shadow", el, "blur " + blur + " spread " + spread, part.trim());
        }
        const c = part.match(/rgba?\([^)]*\)|#[0-9a-f]{3,8}/i);
        const color = c ? parseColor(c[0]) : null;
        if (color && color.a < 1) {
          stats.softShadows++;
          push("shadow-alpha", el, "translucent shadow", part.trim());
        }
      }
    }

    const bgImage = s.backgroundImage || "";
    if (/gradient\(/i.test(bgImage)) {
      stats.gradients++;
      stats.gradientsList.push(selectorFor(el));
      push("gradient", el, "gradient background", bgImage.slice(0, 160));
    }
    if (/url\(/i.test(bgImage) && bgImage !== "none") {
      push("background-url", el, "background image", bgImage.slice(0, 160));
    }
    if ((s.backdropFilter && s.backdropFilter !== "none") || (s.filter && s.filter !== "none")) {
      push("filter", el, "filter/backdrop-filter", (s.backdropFilter || "") + " " + (s.filter || ""));
    }
    if (parseFloat(s.opacity) < 0.999 && !isScrim(el)) {
      push("opacity", el, "opacity " + s.opacity, "");
    }
    const bgc = parseColor(s.backgroundColor);
    if (bgc && bgc.a > 0.001 && bgc.a < 0.999 && !isScrim(el)) {
      push("translucent-bg", el, "background alpha " + bgc.a, s.backgroundColor);
    }
    checkColor(el, s.color, "color");
    checkColor(el, s.backgroundColor, "background");
    if (s.borderTopStyle !== "none" && s.borderTopStyle !== "hidden" && parseFloat(s.borderTopWidth) > 0) checkColor(el, s.borderTopColor, "border-top-color");
    if (s.borderRightStyle !== "none" && s.borderRightStyle !== "hidden" && parseFloat(s.borderRightWidth) > 0) checkColor(el, s.borderRightColor, "border-right-color");
    if (s.borderBottomStyle !== "none" && s.borderBottomStyle !== "hidden" && parseFloat(s.borderBottomWidth) > 0) checkColor(el, s.borderBottomColor, "border-bottom-color");
    if (s.borderLeftStyle !== "none" && s.borderLeftStyle !== "hidden" && parseFloat(s.borderLeftWidth) > 0) checkColor(el, s.borderLeftColor, "border-left-color");
    if (s.outlineStyle && s.outlineStyle !== "none") checkColor(el, s.outlineColor, "outline-color");
    if (s.transform && s.transform !== "none") {
      const m = s.transform.match(/^matrix\(([^)]+)\)$/);
      if (m) {
        const p = m[1].split(",").map(Number);
        if (Math.abs(p[1] || 0) > 0.001 || Math.abs(p[2] || 0) > 0.001) {
          push("transform-rotate", el, "rotate/skew transform", s.transform);
        }
      }
    }

    const families = (s.fontFamily || "").split(",").map((f) => f.trim().replace(/^["']|["']$/g, "").toLowerCase());
    const first = families[0];
    if (first && allowedFonts.indexOf(first) < 0 && first !== "inherit") {
      push("font-family", el, "first family " + first, s.fontFamily);
    }

    const durations = (s.transitionDuration || "0s").split(",");
    for (const d of durations) {
      const v = d.trim();
      let sec = 0;
      if (/ms$/.test(v)) sec = parseFloat(v) / 1000;
      else sec = parseFloat(v) || 0;
      if (sec > 0.111) {
        stats.longTransitions++;
        push("transition", el, "duration " + v, s.transitionProperty);
      }
    }
  }

  const html = document.documentElement;
  const body = document.body;
  const overflow = Math.max(html.scrollWidth, body.scrollWidth) - Math.max(html.clientWidth, body.clientWidth);
  if (overflow > 1) {
    push("overflow", body, "document overflows by " + overflow + "px", "scroll " + html.scrollWidth + " client " + html.clientWidth);
    let count = 0;
    for (const el of all) {
      if (!visible(el)) continue;
      if (inScroller(el)) continue;
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
    push("content-overflow", content, "content overflows by " + (content.scrollWidth - content.clientWidth) + "px", "scroll " + content.scrollWidth + " client " + content.clientWidth);
  }

  const buttons = Array.prototype.slice.call(document.querySelectorAll(".btn")).filter(visible).slice(0, 60);
  for (const b of buttons) {
    if (b.classList.contains("btn-link")) continue;
    const s = getComputedStyle(b);
    if (s.borderStyle === "none" || s.borderStyle === "hidden" || parseFloat(s.borderTopWidth) < 2) {
      stats.borderless++;
      push("border", b, "button border " + s.borderTopWidth + " " + s.borderStyle, "min 2px");
    }
  }
  const fields = Array.prototype.slice.call(document.querySelectorAll(".form-control,.form-select")).filter(visible).slice(0, 80);
  for (const f of fields) {
    if (f.getAttribute("type") === "hidden") continue;
    const s = getComputedStyle(f);
    if (s.borderStyle === "none" || s.borderStyle === "hidden" || parseFloat(s.borderTopWidth) < 2) {
      stats.borderless++;
      push("border", f, "field border " + s.borderTopWidth + " " + s.borderStyle, "min 2px");
    }
  }
  const boxSelectors = ".card,.modal-content,.dropdown-menu,.popover,.list-group-item,.badge,.sm-badge,.choice-pill,.stat-card,.filter-strip,.alert,.toast,.sms-toast,.sms-themeseg,.sms-notif-panel,.sms-chat-panel,.ai-bubble,.ai-inputwrap,.ai-conv-menu-pop,.settings-savebar,.theme-card,.theme-preview,.snp-body,.snp-item,.tooltip-inner,.progress,.btn-close,.imbtn,.settings-section";
  const boxes = Array.prototype.slice.call(document.querySelectorAll(boxSelectors)).filter(visible).slice(0, 160);
  for (const b of boxes) {
    const s = getComputedStyle(b);
    const sides = [s.borderTopWidth, s.borderRightWidth, s.borderBottomWidth, s.borderLeftWidth].map((w) => parseFloat(w) || 0);
    if (Math.min.apply(null, sides) < 2 || ["none", "hidden"].indexOf(s.borderTopStyle) >= 0) {
      stats.borderless++;
      push("border", b, "container border " + sides.join("/"), "min 2px on all sides");
    }
  }

  return {
    title: document.title,
    url: location.href,
    viewport: { width: window.innerWidth, height: window.innerHeight },
    scrollWidth: Math.max(html.scrollWidth, body.scrollWidth),
    stats: stats,
    textChecked: textCount.checked,
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
  await applyTheme(page, "light");
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
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    await context.addInitScript(() => {
      try {
        const stored = JSON.parse(localStorage.getItem("sms.appearance") || "{}");
        stored.theme = stored.theme || "light";
        localStorage.setItem("sms.appearance", JSON.stringify(stored));
      } catch (e) { }
    });
    const page = await context.newPage();
    const consoleErrors = [];
    page.on("pageerror", (e) => consoleErrors.push(String(e)));
    page.on("requestfailed", (r) => consoleErrors.push("REQFAIL " + r.url() + " :: " + ((r.failure() || {}).errorText || "")));

    if (role !== "anon") await login(page, role);

    for (const theme of themes) {
      for (const vp of viewports) {
        await page.setViewportSize({ width: vp.width, height: vp.height });
        for (const rel of pages) {
          const url = BASE + rel;
          const slug = slugFor(rel);
          const themeDir = path.join(OUT_AUDIT, role);
          await ensureDir(themeDir);
          const auditFile = path.join(themeDir, slug + "-" + theme + ".json");
          let result = null;
          let error = null;
          let status = null;
          try {
            const response = await page.goto(url, { waitUntil: "networkidle", timeout: 45000 });
            status = response ? response.status() : null;
            await applyTheme(page, theme);
            try { await page.evaluate(() => document.fonts.ready); } catch (e) { }
            await page.waitForTimeout(450);
            await page.evaluate(() => window.scrollTo(0, 0));
            result = await page.evaluate(auditPage, { theme });
            result.status = status;
            result.requested = rel;
            result.finalUrl = page.url();
            if (wantShots) {
              const vwSuffix = vp.width === 1440 && vp.height === 900 ? "" : "-" + vp.width;
              const shotDir = path.join(OUT_SHOTS, role);
              await ensureDir(shotDir);
              await page.screenshot({ path: path.join(shotDir, slug + "-" + theme + vwSuffix + ".png"), fullPage: false });
            }
          } catch (e) {
            error = String(e && e.message ? e.message : e);
          }

          let aggregate = { role, page: rel, theme, viewports: {}, consoleErrors: [] };
          if (fs.existsSync(auditFile)) {
            try { aggregate = JSON.parse(fs.readFileSync(auditFile, "utf8")); } catch (e) { }
          }
          aggregate.role = role;
          aggregate.page = rel;
          aggregate.theme = theme;
          aggregate.viewports = aggregate.viewports || {};
          aggregate.consoleErrors = aggregate.consoleErrors || [];
          aggregate.viewports[vp.label] = error ? { error: error } : {
            status: status,
            finalUrl: result.finalUrl,
            title: result.title,
            scrollWidth: result.scrollWidth,
            textChecked: result.textChecked,
            stats: result.stats,
            counts: result.counts,
            violations: result.violations
          };
          if (error) failures.push(role + " " + rel + " " + theme + " " + vp.label + ": " + error);
          if (consoleErrors.length) aggregate.consoleErrors = Array.from(new Set(consoleErrors)).slice(0, 20);
          fs.writeFileSync(auditFile, JSON.stringify(aggregate, null, 2));
          const vcount = error ? -1 : result.violations.length;
          summary.push({ role, page: rel, theme, viewport: vp.label, violations: vcount, status, finalUrl: result ? result.finalUrl : null, error });
          console.log((error ? "ERR " : vcount ? "FAIL " : "PASS ") + role + " " + rel + " " + theme + " " + vp.label + (error ? " :: " + error : " :: " + vcount));
        }
      }
    }
    await context.close();
  }

  const report = [];
  report.push("# UI Audit Report");
  report.push("");
  report.push("Generated " + new Date().toISOString());
  report.push("");
  const total = summary.reduce((n, s) => n + (s.violations > 0 ? s.violations : 0), 0);
  const failing = summary.filter((s) => s.violations > 0 || s.error).length;
  report.push("- Checks run: " + summary.length);
  report.push("- Failing checks: " + failing);
  report.push("- Total violations: " + total);
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
  for (const s of summary) {
    if (s.violations <= 0) continue;
    const file = path.join(OUT_AUDIT, s.role, slugFor(s.page) + "-" + s.theme + ".json");
    try {
      const data = JSON.parse(fs.readFileSync(file, "utf8"));
      const vp = data.viewports[s.viewport];
      if (!vp || !vp.counts) continue;
      for (const k of Object.keys(vp.counts)) types[k] = (types[k] || 0) + vp.counts[k];
    } catch (e) { }
  }
  for (const k of Object.keys(types).sort((a, b) => types[b] - types[a])) {
    report.push("- " + k + ": " + types[k]);
  }
  report.push("");
  fs.writeFileSync(path.join(OUT_AUDIT, "REPORT.md"), report.join("\n"));
  await browser.close();
  console.log("REPORT " + path.join(OUT_AUDIT, "REPORT.md"));
  if (failures.length) console.log("FAILURES:\n" + failures.join("\n"));
})().catch((e) => { console.error(e); process.exit(1); });
