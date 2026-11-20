const { chromium } = require('playwright-core');
(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const ctx = await browser.newContext({ viewport: { width: 2460, height: 1400 } });
  const page = await ctx.newPage();
  await page.goto('http://localhost:8080/Account/Login.aspx', { waitUntil: 'networkidle' });
  await page.locator('input[type=email]').fill('admin@sms.edu');
  await page.locator('input[type=password]').fill('Admin@123');
  await Promise.all([page.waitForNavigation({ waitUntil:'networkidle' }), page.locator('input[type=submit]').click()]);
  console.log('after login theme', await page.getAttribute('html','data-theme'), 'cookies', (await ctx.cookies()).filter(c=>c.name.startsWith('sms')).map(c=>c.name+'='+c.value));
  // click light
  await page.locator('[data-sms-theme="light"]').first().click();
  await page.waitForTimeout(800);
  console.log('after light theme', await page.getAttribute('html','data-theme'), 'cookies', (await ctx.cookies()).filter(c=>c.name.startsWith('sms')).map(c=>c.name+'='+c.value));
  // navigate
  await page.goto('http://localhost:8080/Admin/Users.aspx', { waitUntil:'networkidle' });
  console.log('after nav theme', await page.getAttribute('html','data-theme'), 'cookies', (await ctx.cookies()).filter(c=>c.name.startsWith('sms')).map(c=>c.name+'='+c.value));
  // reload
  await page.reload({waitUntil:'networkidle'});
  console.log('after reload theme', await page.getAttribute('html','data-theme'), 'cookies', (await ctx.cookies()).filter(c=>c.name.startsWith('sms')).map(c=>c.name+'='+c.value));
  await browser.close();
})();
