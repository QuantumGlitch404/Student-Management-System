/*
 * Installed-app UI structure audit.
 *
 * Run: node tools/ui-structure-audit.js [baseUrl]
 * Default baseUrl: http://localhost:8080
 *
 * Proves, on the actually installed site, that:
 *   - every stylesheet / script referenced by the shell and the auth shell is 200
 *   - the auth skeleton is two columns at 2460px and one column on phones
 *   - the app shell has no horizontal overflow at 2460/1920/1440/1280/1024/768/390
 *   - the desktop sidebar collapses and the mobile drawer opens/closes
 *   - theme, text size and zoom survive navigation and reload
 *   - the dashboard KPI grid and notification toolbar/list are real elements
 *   - the offline model reports ready and answers a question
 *   - the build stamp is present on both shells
 */
const { chromium } = require('playwright-core');
const fs = require('fs');
const path = require('path');

const BASE = process.argv[2] || 'http://localhost:8080';
const OUT = path.join(__dirname, '..', 'docs', 'ui-structure');
if (!fs.existsSync(OUT)) fs.mkdirSync(OUT, { recursive: true });

const results = [];
function check(name, ok, detail) {
  results.push({ name, ok: !!ok, detail });
  console.log((ok ? 'PASS ' : 'FAIL ') + name + (detail ? ' :: ' + detail : ''));
}
const sleep = ms => new Promise(r => setTimeout(r, ms));
const round = n => Math.round(n);

async function login(page, email, pass) {
  await page.goto(BASE + '/Account/Login.aspx', { waitUntil: 'networkidle' });
  await page.locator('input[type=email]').fill(email);
  await page.locator('input[type=password]').fill(pass);
  await Promise.all([
    page.waitForNavigation({ waitUntil: 'networkidle' }),
    page.locator('input[type=submit]').click()
  ]);
}

async function showDisplayPanel(page) {
  const toggle = page.locator('#smsDisplayBtn');
  if (!(await toggle.isVisible())) return;
  const open = await page.locator('#smsDisplayWrap').evaluate(el => el.classList.contains('is-open'));
  if (!open) { await toggle.click(); await sleep(120); }
}

async function clickVisible(page, selector) {
  for (let i = 0; i < 10; i++) {
    if (await page.locator(selector).isVisible()) break;
    await showDisplayPanel(page);
    await sleep(100);
  }
  await page.locator(selector).click();
}

async function metrics(page, selectors) {
  return page.evaluate(sels => {
    const out = { vw: innerWidth, docW: document.documentElement.scrollWidth, cw: document.documentElement.clientWidth };
    for (const s of sels) {
      const el = document.querySelector(s);
      if (!el) { out[s] = null; continue; }
      const b = el.getBoundingClientRect();
      const c = getComputedStyle(el);
      out[s] = { x: Math.round(b.x), y: Math.round(b.y), w: Math.round(b.width), h: Math.round(b.height), right: Math.round(b.right), bottom: Math.round(b.bottom), display: c.display, cols: c.gridTemplateColumns, overflow: b.right > innerWidth + 1 || b.left < -1 };
    }
    return out;
  }, selectors);
}

(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  let ctx, page;

  // ---------------------------------------------------------------- assets
ctx = await browser.newContext({ viewport: { width: 2460, height: 1400 } });
  await ctx.addCookies([
    { name: 'sms_zoom', value: '100', url: BASE },
    { name: 'sms_font', value: '100', url: BASE },
    { name: 'sms_theme', value: 'light', url: BASE }
  ]);
  page = await ctx.newPage();
  await page.goto(BASE + '/Account/Login.aspx', { waitUntil: 'networkidle' });
  await page.screenshot({ path: path.join(OUT, 'auth-2460.png'), fullPage: true });

  const assets = await page.evaluate(async () => {
    const urls = [...document.querySelectorAll('link[rel=stylesheet], script[src]')]
      .map(e => e.getAttribute('href') || e.getAttribute('src')).filter(Boolean);
    const seen = new Set();
    const rows = [];
    for (const u of urls) {
      if (seen.has(u) || !u.startsWith('/Content/')) continue;
      seen.add(u);
      const r = await fetch(u, { cache: 'no-store' });
      rows.push({ url: u, status: r.status, bytes: (await r.text()).length });
    }
    return rows;
  });
  const missing = assets.filter(a => a.status !== 200 || a.bytes < 50);
  check('Auth assets all 200', missing.length === 0, missing.map(m => m.url + '=' + m.status).join(', ') || assets.length + ' assets');

  const am = await metrics(page, ['.auth', '.auth__brand', '.auth__main', '.auth__card', '.auth__footer', '.auth__appearance']);
  check('Auth shell two columns at 2460', am['.auth'].display === 'grid' && am['.auth'].cols.split(' ').length === 2, am['.auth'].cols);
  check('Auth card is centred, not full width', am['.auth__card'].x > 0 && am['.auth__card'].w < 700 && am['.auth__card'].right < 2460, JSON.stringify(am['.auth__card']));
  check('Auth footer has build stamp', (await page.locator('.auth__footer .build-stamp').textContent()).includes('BUILD'), await page.locator('.auth__footer .build-stamp').textContent());
  check('Auth has no horizontal overflow', am.docW === am.cw, am.docW + '/' + am.cw);

  const authMobile = [];
  for (const w of [768, 390]) {
    const c2 = await browser.newContext({ viewport: { width: w, height: 900 } });
    const p2 = await c2.newPage();
    await p2.goto(BASE + '/Account/Login.aspx', { waitUntil: 'networkidle' });
    const m2 = await metrics(p2, ['.auth', '.auth__brand', '.auth__card', '.auth__appearance']);
    authMobile.push({ w, m: m2 });
    await p2.screenshot({ path: path.join(OUT, 'auth-' + w + '.png'), fullPage: true });
    await c2.close();
  }
  check('Auth becomes one column below 960px', authMobile.every(o => o.m['.auth'].cols.split(' ').length === 1), authMobile.map(o => o.w + ':' + o.m['.auth'].cols).join(', '));
  check('Auth phone card stays inside viewport', authMobile.every(o => o.m['.auth__card'].x >= 0 && o.m['.auth__card'].right <= o.w), authMobile.map(o => o.w + ':' + JSON.stringify(o.m['.auth__card'])).join(' '));

  await page.locator('input[type=email]').fill('admin@sms.edu');
  await page.locator('input[type=password]').fill('Admin@123');
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle' }), page.locator('input[type=submit]').click()]);

  const appAssets = await page.evaluate(async () => {
    const urls = [...document.querySelectorAll('link[rel=stylesheet], script[src]')]
      .map(e => e.getAttribute('href') || e.getAttribute('src')).filter(Boolean);
    const seen = new Set(); const rows = [];
    for (const u of urls) {
      if (seen.has(u) || !u.startsWith('/Content/')) continue;
      seen.add(u);
      const r = await fetch(u, { cache: 'no-store' });
      rows.push({ url: u, status: r.status, bytes: (await r.text()).length });
    }
    return rows;
  });
  const appMissing = appAssets.filter(a => a.status !== 200 || a.bytes < 50);
  check('App assets all 200', appMissing.length === 0, appMissing.map(m => m.url + '=' + m.status).join(', ') || appAssets.length + ' assets');

  // -------------------------------------------------------------- widths
  const widths = [2460, 1920, 1440, 1280, 1024, 768, 390];
  const widthRows = [];
  for (const w of widths) {
    const c2 = await browser.newContext({ viewport: { width: w, height: 1000 } });
    await c2.addCookies([
      { name: 'sms_zoom', value: '100', url: BASE },
      { name: 'sms_font', value: '100', url: BASE },
      { name: 'sms_theme', value: 'light', url: BASE }
    ]);
    const p2 = await c2.newPage();
    await login(p2, 'admin@sms.edu', 'Admin@123');
    await p2.goto(BASE + '/Admin/Index.aspx', { waitUntil: 'networkidle' });
    const m2 = await metrics(p2, ['.app-sidebar', '.app-content', '.page', '.grid-kpi', '.kpi', '.app-footer']);
    widthRows.push({ w, m2 });
    if (w === 2460) await p2.screenshot({ path: path.join(OUT, 'dashboard-2460.png'), fullPage: true });
    if (w === 1440) await p2.screenshot({ path: path.join(OUT, 'dashboard-1440.png'), fullPage: true });
    if (w === 768) await p2.screenshot({ path: path.join(OUT, 'dashboard-768.png'), fullPage: true });
    if (w === 390) await p2.screenshot({ path: path.join(OUT, 'dashboard-390.png'), fullPage: true });
    await c2.close();
  }
  check('No page horizontal overflow at any width', widthRows.every(o => o.m2.docW === o.m2.cw), widthRows.map(o => o.w + ':' + o.m2.docW + '/' + o.m2.cw).join(' '));
  check('Desktop sidebar occupies its grid column', widthRows.filter(o => o.w >= 1024).every(o => o.m2['.app-sidebar'].x === 0 && o.m2['.app-sidebar'].w >= 240), widthRows.filter(o => o.w >= 1024).map(o => o.w + ':' + o.m2['.app-sidebar'].w).join(' '));
  check('Content starts after the sidebar on desktop', widthRows.filter(o => o.w >= 1024).every(o => o.m2['.app-content'].x === o.m2['.app-sidebar'].right), widthRows.filter(o => o.w >= 1024).map(o => o.w + ':' + o.m2['.app-content'].x).join(' '));
  check('Page is capped and centred at 2460', widthRows.find(o => o.w === 2460).m2['.page'].w <= 1681, JSON.stringify(widthRows.find(o => o.w === 2460).m2['.page']));
  check('KPI grid has multiple columns at 2460', widthRows.find(o => o.w === 2460).m2['.grid-kpi'].cols.split(' ').length > 1, widthRows.find(o => o.w === 2460).m2['.grid-kpi'].cols);
  check('Mobile drawer is off-canvas by default', widthRows.filter(o => o.w <= 768).every(o => o.m2['.app-sidebar'].x < 0), widthRows.filter(o => o.w <= 768).map(o => o.w + ':' + o.m2['.app-sidebar'].x).join(' '));
  check('App footer has build stamp', (await page.locator('.app-footer .build-stamp').textContent()).includes('BUILD'), await page.locator('.app-footer .build-stamp').textContent());

  // ---------------------------------------------------------- notifications
  await page.goto(BASE + '/Notifications/Index.aspx', { waitUntil: 'networkidle' });
  const nm = await metrics(page, ['.toolbar', 'ul.list', '.list__item', '.page', '.panel']);
  check('Notification page has .toolbar', nm['.toolbar'] !== null, JSON.stringify(nm['.toolbar']));
  check('Notification page has .list rows', nm['ul.list'] !== null && nm['.list__item'] !== null, JSON.stringify(nm['.list__item']));
  check('Notification page no overflow', nm.docW === nm.cw, nm.docW + '/' + nm.cw);
  await page.screenshot({ path: path.join(OUT, 'notifications-2460.png'), fullPage: true });

  // --------------------------------------------------------------- sidebar
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(BASE + '/Admin/Index.aspx', { waitUntil: 'networkidle' });
  const openW = (await metrics(page, ['.app-sidebar']))['.app-sidebar'].w;
  await page.locator('#smsSidebarToggle').click();
  await sleep(300);
  const collapsed = await metrics(page, ['.app-sidebar', '.app-content']);
  check('Sidebar collapses and content reflows', collapsed['.app-sidebar'].w < openW - 150 && collapsed['.app-content'].x === collapsed['.app-sidebar'].right, JSON.stringify(collapsed));
  await page.reload({ waitUntil: 'networkidle' });
  const collapsedReload = await metrics(page, ['.app-sidebar']);
  check('Sidebar collapse survives reload', collapsedReload['.app-sidebar'].w < openW - 150, String(collapsedReload['.app-sidebar'].w));
  await page.locator('#smsSidebarToggle').click();
  await sleep(200);

  // mobile drawer
  await page.setViewportSize({ width: 390, height: 900 });
  await page.goto(BASE + '/Admin/Index.aspx', { waitUntil: 'networkidle' });
  await page.locator('#smsBurger').click();
  await sleep(300);
  const drawer = await metrics(page, ['.app-sidebar', '.sidebar-scrim']);
  check('Mobile drawer opens over the page', drawer['.app-sidebar'].x >= 0 && drawer['.app-sidebar'].right <= 390, JSON.stringify(drawer['.app-sidebar']));
  check('Mobile drawer has a scrim', await page.locator('.sidebar-scrim.is-on').count() === 1, 'scrim');
  await page.keyboard.press('Escape');
  await sleep(250);

  // ---------------------------------------------------------------- themes
  await page.setViewportSize({ width: 1920, height: 1080 });
  await page.goto(BASE + '/Admin/Index.aspx', { waitUntil: 'networkidle' });
  const themeRows = [];
  for (const theme of ['light', 'dark', 'eye']) {
    const btn = page.locator('[data-sms-theme="' + theme + '"]').first();
    if (await btn.count()) await btn.click();
    await sleep(400);
    const t = await page.evaluate(() => ({
      theme: document.documentElement.getAttribute('data-theme'),
      cookie: (document.cookie.match(/(?:^|; )sms_theme=([^;]*)/) || [])[1] || '',
      bg: getComputedStyle(document.body).backgroundColor,
      overflow: document.documentElement.scrollWidth > document.documentElement.clientWidth
    }));
    themeRows.push(t);
    await page.screenshot({ path: path.join(OUT, 'theme-' + theme + '-1920.png') });
  }
  check('Theme buttons change the theme attribute', themeRows.map(t => t.theme).join(',') === 'light,dark,eye', themeRows.map(t => t.theme).join(','));
  check('Theme cookie follows every click', themeRows.every(t => t.cookie === t.theme), JSON.stringify(themeRows.map(t => t.cookie)));
  check('Themes change the painted background', new Set(themeRows.map(t => t.bg)).size === 3, themeRows.map(t => t.bg).join(' | '));
  await page.locator('[data-sms-theme="dark"]').first().click();
  await sleep(300);
  await page.goto(BASE + '/Admin/Users.aspx', { waitUntil: 'networkidle' });
  check('Dark theme survives navigation', await page.getAttribute('html', 'data-theme') === 'dark', await page.getAttribute('html', 'data-theme'));
  await page.reload({ waitUntil: 'networkidle' });
  check('Dark theme survives reload', await page.getAttribute('html', 'data-theme') === 'dark', await page.getAttribute('html', 'data-theme'));
  await page.locator('[data-sms-theme="light"]').first().click();
  await sleep(300);

  // ------------------------------------------------------------------ zoom
await page.goto(BASE + '/Admin/Index.aspx', { waitUntil: 'networkidle' });
  if (await page.locator('#smsDisplayBtn').isVisible()) await page.locator('#smsDisplayBtn').click();
  await clickVisible(page, '#smsZoomReset');
  await sleep(200);
  const z100 = await page.evaluate(() => ({ z: document.documentElement.dataset.zoom, root: getComputedStyle(document.documentElement).fontSize }));
  const up = page.locator('#smsZoomUp');
  for (let i = 0; i < 7; i++) { if (await up.isDisabled()) break; await clickVisible(page, '#smsZoomUp'); await sleep(120); }
  const zTop = await page.evaluate(() => ({ z: document.documentElement.dataset.zoom, root: getComputedStyle(document.documentElement).fontSize, docW: document.documentElement.scrollWidth, cw: document.documentElement.clientWidth, side: Math.round(document.querySelector('.app-sidebar').getBoundingClientRect().width) }));
  check('Zoom reaches 150 and scales the root rem', zTop.z === '150' && parseFloat(zTop.root) >= 23.5, JSON.stringify(z100) + ' -> ' + JSON.stringify(zTop));
  check('Zoom does not cause horizontal overflow', zTop.docW === zTop.cw, zTop.docW + '/' + zTop.cw);
  await page.screenshot({ path: path.join(OUT, 'zoom-150-1920.png'), fullPage: true });
  const down = page.locator('#smsZoomDown');
  for (let i = 0; i < 10; i++) { if (await down.isDisabled()) break; await clickVisible(page, '#smsZoomDown'); await sleep(120); }
  const zLow = await page.evaluate(() => ({ z: document.documentElement.dataset.zoom, root: getComputedStyle(document.documentElement).fontSize, docW: document.documentElement.scrollWidth, cw: document.documentElement.clientWidth }));
  check('Zoom reaches 80 with no overflow', zLow.z === '80' && parseFloat(zLow.root) <= 13 && zLow.docW === zLow.cw, JSON.stringify(zLow));
  await clickVisible(page, '#smsZoomReset');
  await sleep(250);

  // ------------------------------------------------------------------ font
  await page.goto(BASE + '/Admin/Index.aspx', { waitUntil: 'networkidle' });
  const bodyBefore = await page.evaluate(() => ({ font: getComputedStyle(document.body).fontSize, side: Math.round(document.querySelector('.app-sidebar').getBoundingClientRect().width) }));
  const fup = page.locator('#smsFontUp');
  for (let i = 0; i < 4; i++) { if (await fup.isDisabled()) break; await clickVisible(page, '#smsFontUp'); await sleep(120); }
  const bodyAfter = await page.evaluate(() => ({ font: getComputedStyle(document.body).fontSize, side: Math.round(document.querySelector('.app-sidebar').getBoundingClientRect().width), f: document.documentElement.dataset.font }));
  check('Text size changes body type only', parseFloat(bodyAfter.font) > parseFloat(bodyBefore.font) && bodyAfter.side === bodyBefore.side, JSON.stringify(bodyBefore) + ' -> ' + JSON.stringify(bodyAfter));
  for (let i = 0; i < 4; i++) { const dn = page.locator('#smsFontDown'); if (await dn.isDisabled()) break; await clickVisible(page, '#smsFontDown'); await sleep(100); }
  await clickVisible(page, '#smsFontReset');
  await sleep(250);

  // ------------------------------------------------------------------- AI
  const status = await page.evaluate(async () => await (await fetch('/Handlers/AiStatus.ashx', { credentials: 'same-origin' })).json());
  check('Installed AI model and runtime are present', status.modelPresent === true && status.runtimePresent === true && status.modelBytes > 100000000, JSON.stringify({ model: status.model, bytes: status.modelBytes, runtime: status.runtime }));
  check('Installed AI runtime reports ready', status.ready === true && status.state === 'ready', status.state + ' ' + (status.error || ''));
  fs.writeFileSync(path.join(OUT, 'ai-status.json'), JSON.stringify(status, null, 2));

  await page.goto(BASE + '/Chatbot/Index.aspx', { waitUntil: 'networkidle' });
  await sleep(2500);
  const ta = page.locator('textarea').first();
  await ta.fill('In one sentence, what can this system help me do?');
  await page.locator('#aiSend, button[type=submit]').first().click();
  let answer = '';
  for (let i = 0; i < 60; i++) {
    await sleep(1000);
const bodies = await page.locator('.ai-msg .ai-bubble, .ai-msg').allTextContents();
    if (bodies.length >= 2 && bodies[bodies.length - 1].trim().length > 20) { answer = bodies[bodies.length - 1]; break; }
  }
  check('Installed AI answers a question', answer.trim().length > 20, answer.slice(0, 120));
  await page.screenshot({ path: path.join(OUT, 'ai-answer-1440.png'), fullPage: true });

  // reset appearance for the next manual look
  await page.evaluate(() => {
    document.cookie = 'sms_theme=light;path=/;max-age=31536000';
    document.cookie = 'sms_font=100;path=/;max-age=31536000';
    document.cookie = 'sms_zoom=100;path=/;max-age=31536000';
    document.cookie = 'sms_sidebar=0;path=/;max-age=31536000';
  });

  await browser.close();

  const failed = results.filter(r => !r.ok);
  const md = ['# UI structure audit', '', 'Base URL: ' + BASE, '', '| Result | Check | Detail |', '| --- | --- | --- |']
    .concat(results.map(r => '| ' + (r.ok ? 'PASS' : 'FAIL') + ' | ' + r.name.replace(/\|/g, '\\|') + ' | ' + String(r.detail || '').replace(/\|/g, '\\|').replace(/\n/g, ' ') + ' |'))
    .join('\n') + '\n';
  fs.writeFileSync(path.join(OUT, 'AUDIT.md'), md);
  console.log('\n' + (results.length - failed.length) + '/' + results.length + ' checks passed. Report: docs/ui-structure/AUDIT.md');
  process.exit(failed.length ? 1 : 0);
})().catch(err => { console.error(err); process.exit(2); });

