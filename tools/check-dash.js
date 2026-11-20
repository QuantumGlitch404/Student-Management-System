const { chromium } = require('playwright-core');
async function login(p){await p.goto('http://localhost:8080/Account/Login.aspx',{waitUntil:'networkidle'});await p.locator('input[type=email]').fill('admin@sms.edu');await p.locator('input[type=password]').fill('Admin@123');await Promise.all([p.waitForNavigation({waitUntil:'networkidle'}),p.locator('input[type=submit]').click()]);}
(async()=>{
 const b=await chromium.launch({channel:'chrome',headless:true});
 const ctx=await b.newContext({viewport:{width:2460,height:1400}});
 const p=await ctx.newPage();
 await login(p);
 const m=await p.evaluate(()=>{
   const R=e=>{const x=document.querySelector(e);if(!x)return null;const b=x.getBoundingClientRect(),s=getComputedStyle(x);return {x:Math.round(b.x),y:Math.round(b.y),w:Math.round(b.width),h:Math.round(b.height),display:s.display,overflowX:b.right>innerWidth+1,scrollW:x.scrollWidth,clientW:x.clientWidth};};
   return {vw:innerWidth,docW:document.documentElement.scrollWidth,shell:R('.app-shell'),sidebar:R('.app-sidebar'),main:R('.app-main'),topbar:R('.app-topbar'),content:R('.app-content'),kpiGrid:R('.grid-kpi'),kpi:R('.kpi'),title:document.title,stamp:document.querySelector('.build-stamp')?.textContent.trim()};
 });
 console.log(JSON.stringify(m,null,2));
 await p.screenshot({path:'docs/dashboard-after-2460.png',fullPage:true});
 await b.close();
})();
