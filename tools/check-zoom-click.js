const { chromium } = require('playwright-core');
(async()=>{
 const b=await chromium.launch({channel:'chrome',headless:true}); const ctx=await b.newContext({viewport:{width:2460,height:1400}}); const p=await ctx.newPage();
 await p.goto('http://localhost:8080/Account/Login.aspx',{waitUntil:'networkidle'}); await p.locator('input[type=email]').fill('admin@sms.edu'); await p.locator('input[type=password]').fill('Admin@123'); await Promise.all([p.waitForNavigation({waitUntil:'networkidle'}),p.locator('input[type=submit]').click()]);
 console.log('before', await p.evaluate(()=>({z:document.documentElement.dataset.zoom,root:getComputedStyle(document.documentElement).fontSize,cookies:document.cookie})));
 const plus=p.locator('.sms-zoomctl .sms-iconbtn').last(); for(let i=0;i<5;i++){await plus.click(); await p.waitForTimeout(120);} 
 console.log('after', await p.evaluate(()=>({z:document.documentElement.dataset.zoom,root:getComputedStyle(document.documentElement).fontSize,cookies:document.cookie,docW:document.documentElement.scrollWidth,cw:document.documentElement.clientWidth})));
 await p.screenshot({path:'docs/dashboard-after-zoom150-2460.png',fullPage:true}); await b.close();
})();
