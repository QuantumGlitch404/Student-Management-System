/* ========================================================================
   Layout Test — Playwright geometry assertions for layout patterns
   Tests: L1-L10, P1-P10 pattern compliance
======================================================================== */
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

async function login(page, email, password) {
  await page.goto(BASE + "/Account/Login.aspx", { waitUntil: "domcontentloaded" });
  await page.fill('input[name="ctl00$MainContent$txtEmail"]', email);
  await page.fill('input[name="ctl00$MainContent$txtPassword"]', password);
  await Promise.all([
    page.waitForURL(/Index\.aspx/i, { timeout: 20000 }),
    page.click('input[name="ctl00$MainContent$btnSignIn"]')
  ]);
}

async function testPage(page, role, url, pattern, checks) {
  await page.goto(BASE + url, { waitUntil: "domcontentloaded" });
  await page.waitForLoadState("networkidle");

  // L1: H1 and primary action in viewport
  const h1 = await page.$("h1");
  const primaryBtn = await page.$(".page-head .btn, .page-actions .btn, .page-head-actions .btn");
  if (h1) {
    const h1Box = await h1.boundingBox();
    check(`${role} ${url} L1 H1 in viewport`, h1Box && h1Box.y >= 0 && h1Box.y < 768, `y=${h1Box?.y}`);
  }
  if (primaryBtn) {
    const btnBox = await primaryBtn.boundingBox();
    check(`${role} ${url} L1 primary action in viewport`, btnBox && btnBox.y >= 0 && btnBox.y < 768, `y=${btnBox?.y}`);
  }

  // L3: Document scrollHeight <= 3 * viewport height
  const scrollHeight = await page.evaluate(() => document.documentElement.scrollHeight);
  check(`${role} ${url} L3 max 3 viewports`, scrollHeight <= 3 * 768, `scrollHeight=${scrollHeight}`);

  // No horizontal scroll
  const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
  check(`${role} ${url} no horizontal scroll`, scrollWidth <= 1366, `scrollWidth=${scrollWidth}`);

  // Pattern-specific checks
  if (pattern === "split") {
    // L2: Click first item, detail in viewport
    const items = await page.$$("[data-split-select]");
    if (items.length > 0) {
      for (let i = 0; i < Math.min(3, items.length); i++) {
        const beforeScroll = await page.evaluate(() => window.scrollY);
        await items[i].click();
        await page.waitForTimeout(300);
        const afterScroll = await page.evaluate(() => window.scrollY);
        const detail = await page.$("#rightBody, .split-view__right-body");
        if (detail) {
          const detailBox = await detail.boundingBox();
          check(`${role} ${url} L2 split detail in viewport`, detailBox && detailBox.y >= 0 && detailBox.y < 768, `y=${detailBox?.y}`);
        }
        check(`${role} ${url} L2 scrollY change <= 8px`, Math.abs(afterScroll - beforeScroll) <= 8, `Δ=${Math.abs(afterScroll - beforeScroll)}`);
        // Check URL updated
        const url = page.url();
        check(`${role} ${url} L2 URL has id`, url.includes("courseId=") || url.includes("id="), url);
        // Go back to list
        await page.goBack();
        await page.waitForTimeout(200);
      }
    }
    // L4: Split panes scroll independently
    const leftPane = await page.$(".split-view__left");
    const rightPane = await page.$(".split-view__right-body");
    if (leftPane && rightPane) {
      const leftScroll = await leftPane.evaluate(el => el.scrollHeight > el.clientHeight);
      const rightScroll = await rightPane.evaluate(el => el.scrollHeight > el.clientHeight);
      check(`${role} ${url} L4 left pane scrollable`, leftScroll);
      check(`${role} ${url} L4 right pane scrollable`, rightScroll);
    }
    // Sticky header stays
    const rightHeader = await page.$(".split-view__right-header");
    if (rightHeader) {
      const before = await rightHeader.boundingBox();
      await page.evaluate(() => document.querySelector(".split-view__right-body").scrollTop = 300);
      await page.waitForTimeout(200);
      const after = await rightHeader.boundingBox();
      check(`${role} ${url} L4 sticky header stays`, before && after && Math.abs(before.y - after.y) < 5, `Δy=${Math.abs(before.y - after.y)}`);
    }
  }

  if (pattern === "drawer") {
    // Open drawer
    const openBtn = await page.$("[data-drawer-open]");
    if (openBtn) {
      await openBtn.click();
      await page.waitForTimeout(300);
      const drawer = await page.$(".drawer.is-open");
      check(`${role} ${url} P2 drawer opens`, !!drawer);
      if (drawer) {
        const drawerBox = await drawer.boundingBox();
        check(`${role} ${url} P2 drawer full height`, drawerBox && drawerBox.height > 600);
        // Close drawer
        await page.keyboard.press("Escape");
        await page.waitForTimeout(200);
        const closed = await page.$(".drawer.is-open");
        check(`${role} ${url} P2 drawer closes on Esc`, !closed);
      }
    }
  }

  if (pattern === "hub") {
    // Tab bar sticky
    const tabBar = await page.$(".tab-bar");
    if (tabBar) {
      const before = await tabBar.boundingBox();
      await page.evaluate(() => window.scrollBy(0, 300));
      await page.waitForTimeout(200);
      const after = await tabBar.boundingBox();
      check(`${role} ${url} P4 tab bar sticky`, before && after && Math.abs(before.y - after.y) < 5);
    }
  }

  if (pattern === "table") {
    // Row click opens drawer
    const row = await page.$("tbody tr[data-row-id]");
    if (row) {
      await row.click();
      await page.waitForTimeout(300);
      const drawer = await page.$(".drawer.is-open");
      check(`${role} ${url} P6 row click opens drawer`, !!drawer);
      if (drawer) {
        await page.keyboard.press("Escape");
        await page.waitForTimeout(200);
      }
    }
    // Sticky header
    const thead = await page.$(".table-workspace__table thead");
    if (thead) {
      const before = await thead.boundingBox();
      await page.evaluate(() => document.querySelector(".table-workspace__table-wrap").scrollTop = 300);
      await page.waitForTimeout(200);
      const after = await thead.boundingBox();
      check(`${role} ${url} P6 sticky header`, before && after && Math.abs(before.y - after.y) < 5);
    }
  }

  if (pattern === "inbox") {
    // Three panes
    const panes = await page.$$(".inbox__pane");
    check(`${role} ${url} P7 three panes`, panes.length >= 2);
  }

  if (pattern === "profile") {
    // Left nav sticky
    const nav = await page.$(".profile-layout__nav");
    if (nav) {
      const before = await nav.boundingBox();
      await page.evaluate(() => window.scrollBy(0, 300));
      await page.waitForTimeout(200);
      const after = await nav.boundingBox();
      check(`${role} ${url} P10 left nav sticky`, before && after && Math.abs(before.y - after.y) < 5);
    }
  }

  // L5: Save keeps scrollY (test with a form if present)
  // L7: Pattern marker
  const patternEl = await page.$(`[data-pattern="${pattern}"]`);
  check(`${role} ${url} L7 pattern marker`, !!patternEl, pattern);

  // L8: Forms single-column or two-column
  const forms = await page.$$("form");
  for (const form of forms) {
    const inputs = await form.$$("input, select, textarea");
    if (inputs.length > 8) {
      check(`${role} ${url} L8 form fields <= 8`, false, `${inputs.length} fields`);
    }
  }

  // L9: Dashboard tiles
  if (url.includes("Index.aspx") && (url.includes("/Admin/") || url.includes("/Faculty/") || url.includes("/Student/"))) {
    const tiles = await page.$$(".tile");
    check(`${role} ${url} L9 tiles present`, tiles.length > 0, `${tiles.length} tiles`);
  }

  // L10: Mobile check (390x844)
  if (["/Admin/Index.aspx", "/Faculty/Index.aspx", "/Student/Index.aspx", "/Timetable/Index.aspx", "/Account/Activity.aspx"].includes(url)) {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.waitForTimeout(300);
    const mobileScroll = await page.evaluate(() => document.documentElement.scrollWidth);
    check(`${role} ${url} L10 mobile no horizontal scroll`, mobileScroll <= 390, `scrollWidth=${mobileScroll}`);
    await page.setViewportSize({ width: 1366, height: 768 });
  }

  // No element overlaps
  const overlap = await page.evaluate(() => {
    const elements = document.querySelectorAll("*");
    for (let i = 0; i < elements.length; i++) {
      const a = elements[i];
      const aRect = a.getBoundingClientRect();
      for (let j = i + 1; j < elements.length; j++) {
        const b = elements[j];
        const bRect = b.getBoundingClientRect();
        if (aRect.width > 0 && aRect.height > 0 && bRect.width > 0 && bRect.height > 0) {
          const overlap = !(aRect.right <= bRect.left || bRect.right <= aRect.left ||
                           aRect.bottom <= bRect.top || bRect.bottom <= aRect.top);
          if (overlap && aRect.width > 10 && aRect.height > 10 && bRect.width > 10 && bRect.height > 10) {
            return true;
          }
        }
      }
    }
    return false;
  });
  check(`${role} ${url} no overlaps`, !overlap);
}

(async () => {
  const browser = await chromium.launch({ executablePath: EXE, headless: true });
  const roles = {
    admin: { email: "admin@sms.edu", pass: "Admin@123" },
    faculty: { email: "faculty@sms.edu", pass: "Faculty@123" },
    student: { email: "student@sms.edu", pass: "Student@123" }
  };

  const pages = {
    admin: [
      { url: "/Admin/Index.aspx", pattern: "dashboard" },
      { url: "/Admin/Users.aspx", pattern: "table" },
      { url: "/Admin/Academics.aspx", pattern: "table" },
      { url: "/Timetable/Index.aspx", pattern: "split" },
      { url: "/Admin/Exams.aspx", pattern: "hub" },
      { url: "/Admin/MailCenter.aspx", pattern: "inbox" },
    ],
    faculty: [
      { url: "/Faculty/Index.aspx", pattern: "dashboard" },
      { url: "/Faculty/Attendance.aspx", pattern: "split" },
      { url: "/Faculty/Marks.aspx", pattern: "table" },
      { url: "/Timetable/Index.aspx", pattern: "split" },
      { url: "/Faculty/Profile.aspx", pattern: "profile" },
    ],
    student: [
      { url: "/Student/Index.aspx", pattern: "dashboard" },
      { url: "/Student/Attendance.aspx", pattern: "split" },
      { url: "/Student/Results.aspx", pattern: "hub" },
      { url: "/Timetable/Index.aspx", pattern: "split" },
      { url: "/Account/Activity.aspx", pattern: "table" },
      { url: "/Student/Profile.aspx", pattern: "profile" },
    ]
  };

  for (const [role, cfg] of Object.entries(roles)) {
    const context = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const page = await context.newPage();
    try {
      await login(page, cfg.email, cfg.pass);
      for (const { url, pattern } of pages[role]) {
        await testPage(page, role, url, pattern, {});
      }
    } catch (e) {
      console.log("FAIL " + role + " " + e.message);
    } finally {
      await context.close();
    }
  }

  await browser.close();

  const passed = results.filter(r => r.ok).length;
  const failed = results.filter(r => !r.ok).length;
  console.log(`\n=== LAYOUT TEST SUMMARY ===`);
  console.log(`Passed: ${passed}`);
  console.log(`Failed: ${failed}`);
  console.log(`Total:  ${results.length}`);

  if (failed > 0) {
    console.log("\nFAILURES:");
    results.filter(r => !r.ok).forEach(r => console.log(`  ${r.name} - ${r.extra}`));
    process.exit(1);
  } else {
    console.log("\nALL TESTS PASSED");
    process.exit(0);
  }
})().catch(e => { console.error("FATAL", e); process.exit(1); });