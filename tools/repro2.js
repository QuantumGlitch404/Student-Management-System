const { chromium } = require('playwright-core');
(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const ctx = await browser.newContext({ viewport: { width: 2460, height: 1400 }, deviceScaleFactor: 1 });
  const page = await ctx.newPage();
  await page.goto('http://localhost:8080/Account/Login.aspx', { waitUntil: 'networkidle' });
  console.log(await page.evaluate(()=>Array.from(document.querySelectorAll('input,button,select')).map(e=>({tag:e.tagName,type:e.type,name:e.name,id:e.id,text:(e.textContent||'').trim().slice(0,40)})).slice(0,30)));
  const email = page.locator('input[type=email], input[name*="Email"], #txtEmail, #Email').first();
  const pass = page.locator('input[type=password]').first();
  await email.fill('admin@sms.edu');
  await pass.fill('Admin@123');
  await Promise.all([page.waitForLoadState('networkidle'), page.locator('input[type=submit], button[type=submit]').first().click()]);
  console.log('url', page.url());
  console.log('theme', await page.getAttribute('html','data-theme'));
  const info = await page.evaluate(() => ({
    kpiCount: document.querySelectorAll('.kpi,.stat-card').length,
    statText: Array.from(document.querySelectorAll('.stat-card,.kpi')).slice(0,4).map(e=>e.innerText.replace(/\s+/g,' ').slice(0,80)),
    sheets: Array.from(document.styleSheets).map(s=>s.href).filter(Boolean).map(h=>h.split('/').slice(-2).join('/')),
    bodyW: document.body.scrollWidth, winW: innerWidth,
    mainW: document.querySelector('.app-shell')?.getBoundingClientRect().width
  }));
  console.log(JSON.stringify(info,null,2));
  await page.screenshot({ path: 'docs/dashboard-before-2460.png', fullPage: true });
  await browser.close();
})();
