/* Headless functional test for the display controls and core pages.
   No screenshots, no visual diffing: DOM and computed-style assertions only. */
"use strict";
const { chromium } = require("playwright-core");

const BASE = process.env.SMS_BASE || "http://localhost:8080";
const EXE = process.env.SMS_CHROME ||
  "C:\\Users\\sampa\\AppData\\Local\\ms-playwright\\chromium-1228\\chrome-win64\\chrome.exe";

const results = [];
function check(name, ok, extra) {
  results.push({ name, ok, extra: extra || "" });
  console.log((ok ? "PASS " : "FAIL ") + name + (extra ? " - " + extra : ""));
}

(async () => {
  const browser = await chromium.launch({ executablePath: EXE, headless: true });
  const context = await browser.newContext({ viewport: { width: 1366, height: 600 } });
  const page = await context.newPage();
  const errors = [];
  page.on("pageerror", e => errors.push(String(e)));
  try {
    await page.goto(BASE + "/Account/Login.aspx", { waitUntil: "domcontentloaded" });
    await page.fill('input[name="ctl00$MainContent$txtEmail"]', "admin@sms.edu");
    await page.fill('input[name="ctl00$MainContent$txtPassword"]', "Admin@123");
    await page.click('input[name="ctl00$MainContent$btnSignIn"]');
    await page.waitForURL(/\/Admin\/Index\.aspx/i, { timeout: 20000 });
    check("Admin sign-in", true, page.url());

    await page.click("#smsZoomReset");
    await page.click("#smsFontReset");
    await page.waitForTimeout(150);
    const base = await page.evaluate(() => ({
      font: parseFloat(getComputedStyle(document.documentElement).fontSize),
      zoom: document.documentElement.getAttribute("data-zoom"),
      text: document.documentElement.getAttribute("data-font")
    }));

    await page.click("#smsZoomUp");
    await page.waitForTimeout(120);
    const zoomed = await page.evaluate(() => ({
      font: parseFloat(getComputedStyle(document.documentElement).fontSize),
      zoom: document.documentElement.getAttribute("data-zoom"),
      varZoom: getComputedStyle(document.documentElement).getPropertyValue("--app-zoom").trim()
    }));
    check("Zoom + changes attribute", zoomed.zoom === "110", "zoom=" + zoomed.zoom);
    check("Zoom + changes computed size", zoomed.font > base.font, base.font + " -> " + zoomed.font);
    check("Zoom + sets CSS variable", zoomed.varZoom === "1.1", zoomed.varZoom);

    await page.click("#smsFontUp");
    await page.waitForTimeout(120);
    const scaled = await page.evaluate(() => ({
      font: parseFloat(getComputedStyle(document.documentElement).fontSize),
      text: document.documentElement.getAttribute("data-font"),
      varText: getComputedStyle(document.documentElement).getPropertyValue("--text-scale").trim()
    }));
    check("Text size + changes attribute", scaled.text === "105", "data-font=" + scaled.text);
    check("Text size + changes computed size", scaled.font > zoomed.font, zoomed.font + " -> " + scaled.font);
    check("Text size + sets CSS variable", scaled.varText === "1.05", scaled.varText);

    await page.click("#smsZoomReset");
    await page.click("#smsFontReset");
    await page.waitForTimeout(120);

    const sidebar = await page.evaluate(() => {
      const nav = document.querySelector(".sidebar-nav");
      const shell = document.querySelector(".app-shell");
      const link = document.querySelector(".sidebar-nav a");
      const btn = document.querySelector("button");
      return {
        exists: !!nav,
        overflow: nav ? getComputedStyle(nav).overflowY : "",
        client: nav ? nav.clientHeight : 0,
        scroll: nav ? nav.scrollHeight : 0,
        shellH: shell ? getComputedStyle(shell).height : "",
        linkCursor: link ? getComputedStyle(link).cursor : "",
        buttonCursor: btn ? getComputedStyle(btn).cursor : ""
      };
    });
    check("Sidebar nav exists", sidebar.exists);
    check("Sidebar nav is independently scrollable", sidebar.overflow === "auto", sidebar.overflow);
    check("Sidebar content overflows viewport", sidebar.scroll > sidebar.client, sidebar.scroll + ">" + sidebar.client);
    await page.evaluate(() => { document.querySelector(".sidebar-nav").style.scrollBehavior = "auto"; document.querySelector(".sidebar-nav").scrollTop = 240; });
    await page.waitForTimeout(200);
    const scrolled = await page.evaluate(() => document.querySelector(".sidebar-nav").scrollTop);
    check("Sidebar actually scrolls", scrolled > 0, "scrollTop=" + scrolled);
    check("Link has pointer cursor", sidebar.linkCursor === "pointer", sidebar.linkCursor);
    check("Button has pointer cursor", sidebar.buttonCursor === "pointer", sidebar.buttonCursor);

    await page.goto(BASE + "/Account/Settings.aspx?tab=appearance", { waitUntil: "domcontentloaded" });
    await page.click('label[for="ch_motion_reduced"]');
    await page.waitForTimeout(120);
    const motion = await page.evaluate(() => document.documentElement.getAttribute("data-motion"));
    check("Motion setting applies immediately", motion === "reduced", motion);
    await page.click('label[for="ch_motion_full"]');
    await page.waitForTimeout(120);
    const motion2 = await page.evaluate(() => document.documentElement.getAttribute("data-motion"));
    check("Motion setting restores full", motion2 === "full", motion2);

    await page.selectOption('select[name="zoom"]', "120");
    await page.waitForTimeout(120);
    const z2 = await page.evaluate(() => document.documentElement.getAttribute("data-zoom"));
    check("Settings zoom applies immediately", z2 === "120", z2);
    await page.selectOption('select[name="zoom"]', "100");
    await page.selectOption('select[name="fontScale"]', "110");
    await page.waitForTimeout(120);
    const f2 = await page.evaluate(() => document.documentElement.getAttribute("data-font"));
    check("Settings text size applies immediately", f2 === "110", f2);
    await page.selectOption('select[name="fontScale"]', "100");

    await page.goto(BASE + "/Admin/MailCenter.aspx", { waitUntil: "domcontentloaded" });
    check("Mail centre loads", await page.locator("h1", { hasText: "Mail centre" }).count() === 1, page.url());
    check("Mail composer present", await page.locator("#mcSubject").count() === 1);

    await page.goto(BASE + "/Help/Index.aspx", { waitUntil: "domcontentloaded" });
    check("Help centre loads", await page.locator("h1", { hasText: "Help and guides" }).count() === 1);

    const health = await page.request.get(BASE + "/Handlers/Health.ashx");
    check("Health endpoint returns 200", health.status() === 200, String(health.status()));
    const healthJson = await health.json();
    check("Health reports database reachable", healthJson.ok === true && healthJson.database.reachable === true);

    check("No uncaught page errors", errors.length === 0, errors.join(" | "));
  } catch (e) {
    check("Test run completed", false, String(e && e.stack || e));
  } finally {
    await browser.close();
    const failed = results.filter(r => !r.ok).length;
    console.log("\n" + (results.length - failed) + "/" + results.length + " checks passed.");
    process.exit(failed === 0 ? 0 : 1);
  }
})();

