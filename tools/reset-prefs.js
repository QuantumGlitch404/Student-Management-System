const { chromium } = require('playwright-core');
(async()=>{const b=await chromium.launch({channel:'chrome',headless:true});const ctx=await b.newContext({viewport:{width:1920,height:1080}});const p=await ctx.newPage();await p.goto('http://localhost:8080/Account/Login.aspx',{waitUntil:'networkidle'});await p.locator('input[type=email]').fill('admin@sms.edu');await p.locator('input[type=password]').fill('Admin@123');await Promise.all([p.waitForNavigation({waitUntil:'networkidle'}),p.locator('input[type=submit]').click()]);
await p.evaluate(()=>{document.cookie='sms_theme=light;path=/;max-age=31536000';document.cookie='sms_font=100;path=/;max-age=31536000';document.cookie='sms_zoom=100;path=/;max-age=31536000';document.cookie='sms_sidebar=0;path=/;max-age=31536000';});
await p.reload({waitUntil:'networkidle'}); await p.waitForTimeout(500); await p.reload({waitUntil:'networkidle'});
console.log(await p.evaluate(()=>({theme:document.documentElement.dataset.theme,zoom:document.documentElement.dataset.zoom,font:document.documentElement.dataset.font,side:getComputedStyle(document.documentElement).getPropertyValue('--sidebar-w').trim(),cookies:document.cookie})));
await b.close();})();
