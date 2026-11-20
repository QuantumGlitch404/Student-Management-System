/*
 * Class coverage: every structural primitive the UI contract relies on must
 * exist (a) as a real CSS rule in the installed stylesheets and (b) in the
 * rendered installed HTML at least once.
 *
 * Run: node tools/class-coverage.js [baseUrl]
 */
const { chromium } = require('playwright-core');
const fs = require('fs');
const path = require('path');

const BASE = process.argv[2] || 'http://localhost:8080';
const OUT = path.join(__dirname, '..', 'docs', 'ui-structure');
if (!fs.existsSync(OUT)) fs.mkdirSync(OUT, { recursive: true });

const required = [
  'app-shell', 'app-sidebar', 'app-content', 'app-footer',
  'page', 'page-head', 'toolbar',
  'grid-kpi', 'kpi', 'kpi__head', 'kpi__body', 'kpi__value', 'kpi__note',
  'panel', 'panel__head', 'panel__body', 'table-wrap',
  'list', 'list__item', 'list__body', 'list__title',
  'settings-layout', 'chat-layout',
  'auth', 'auth__brand', 'auth__main', 'auth__card', 'auth__appearance', 'auth__footer'
];

// Primitives that exist as CSS contracts even when not every page uses them.
const cssOnly = [
  'grid-2', 'grid-main-side', 'form-grid', 'field', 'cluster', 'stack',
  'page-actions', 'sms-sidebar-collapsed'
];

const pages = [
  '/Account/Login.aspx',
  '/Admin/Index.aspx',
  '/Admin/Users.aspx',
  '/Notifications/Index.aspx',
  '/Chatbot/Index.aspx',
  '/Account/Settings.aspx'
];

(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const ctx = await browser.newContext({ viewport: { width: 2460, height: 1400 } });
  const page = await ctx.newPage();

  const cssTexts = [];
  const addCss = async url => {
    const r = await page.request.get(BASE + url);
    if (r.ok()) cssTexts.push(await r.text());
  };

  await page.goto(BASE + '/Account/Login.aspx', { waitUntil: 'networkidle' });
  await addCss('/Content/css/tokens.css');
  await addCss('/Content/css/base.css');
  await addCss('/Content/css/layout.css');
  await addCss('/Content/css/components.css');
  await addCss('/Content/css/pages/auth.css');
  await addCss('/Content/css/pages/dashboard.css');
  await addCss('/Content/css/pages/misc.css');
  await addCss('/Content/css/pages/settings.css');
  await addCss('/Content/css/pages/chat.css');
  const css = cssTexts.join('\n');

  // Collect classes from the installed pages (auth first, then signed in).
  const htmlClasses = new Set();
  const collect = async () => {
    const list = await page.evaluate(() => {
      const set = new Set();
      document.querySelectorAll('*').forEach(el => el.classList.forEach(c => set.add(c)));
      return [...set];
    });
    list.forEach(c => htmlClasses.add(c));
  };
  await collect();

  await page.locator('input[type=email]').fill('admin@sms.edu');
  await page.locator('input[type=password]').fill('Admin@123');
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle' }), page.locator('input[type=submit]').click()]);

  for (const u of pages.slice(1)) {
    await page.goto(BASE + u, { waitUntil: 'networkidle' });
    await collect();
  }
  await browser.close();

const rows = required.map(cls => {
    const inCss = new RegExp('\\.' + cls.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '(?![\\w-])').test(css);
    const inHtml = htmlClasses.has(cls);
    return { cls, inCss, inHtml, ok: inCss && inHtml };
  }).concat(cssOnly.map(cls => {
    const inCss = new RegExp('\\.' + cls.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '(?![\\w-])').test(css);
    return { cls: cls + ' (CSS)', inCss, inHtml: true, ok: inCss };
  }));

  const failed = rows.filter(r => !r.ok);
  const md = ['# Class coverage', '', 'Base URL: ' + BASE, '', '| Class | CSS rule | Rendered | Result |', '| --- | --- | --- | --- |']
    .concat(rows.map(r => '| `.' + r.cls + '` | ' + (r.inCss ? 'yes' : 'NO') + ' | ' + (r.inHtml ? 'yes' : 'NO') + ' | ' + (r.ok ? 'PASS' : 'FAIL') + ' |'))
    .join('\n') + '\n';
  fs.writeFileSync(path.join(OUT, 'CLASS-COVERAGE.md'), md);
  console.log((rows.length - failed.length) + '/' + rows.length + ' required classes covered.');
  if (failed.length) {
    console.log('Missing: ' + failed.map(f => f.cls + (f.inCss ? '' : ' (css)') + (f.inHtml ? '' : ' (html)')).join(', '));
    process.exit(1);
  }
})().catch(err => { console.error(err); process.exit(2); });
