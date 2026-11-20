const { chromium } = require('playwright-core');
async function login(p){await p.goto('http://localhost:8080/Account/Login.aspx',{waitUntil:'networkidle'});await p.locator('input[type=email]').fill('admin@sms.edu');await p.locator('input[type=password]').fill('Admin@123');await Promise.all([p.waitForNavigation({waitUntil:'networkidle'}),p.locator('input[type=submit]').click()]);}
(async()=>{
 const b=await chromium.launch({channel:'chrome',headless:true});
 for (const z of [80,100,125,150]) {
   const ctx=await b.newContext({viewport:{width:2460,height:1400}});
   await ctx.addCookies([{name:'sms_zoom',value:String(z),url:'http://localhost:8080'}]);
   const p=await ctx.newPage(); await login(p);
   const m=await p.evaluate(()=>({zoom:document.documentElement.dataset.zoom,root:getComputedStyle(document.documentElement).fontSize,side:document.querySelector('.app-sidebar').getBoundingClientRect().width,docW:document.documentElement.scrollWidth,cw:document.documentElement.clientWidth,topOverflow:document.querySelector('.app-topbar')?.scrollWidth>document.querySelector('.app-topbar')?.clientWidth}));
   console.log(JSON.stringify(m));
   await ctx.close();
 }
 await b.close();
})();
