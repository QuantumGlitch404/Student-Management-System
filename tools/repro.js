const { chromium } = require('playwright-core');
(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const page = await browser.newPage({ viewport: { width: 2460, height: 1400 }, deviceScaleFactor: 1 });
  await page.goto('http://localhost:8080/Account/Login.aspx', { waitUntil: 'networkidle' });
  console.log('title', await page.title());
  console.log('theme', await page.getAttribute('html','data-theme'));
  const info = await page.evaluate(() => {
    const el = document.querySelector('.auth-card');
    const shell = document.querySelector('.auth-shell');
    return { card: el && el.getBoundingClientRect().toJSON(), shell: shell && getComputedStyle(shell).display, sheetCount: document.styleSheets.length, bodyW: document.body.scrollWidth, winW: innerWidth };
  });
  console.log(JSON.stringify(info,null,2));
  await page.screenshot({ path: 'docs/login-before-2460.png', fullPage: true });
  await browser.close();
})();
