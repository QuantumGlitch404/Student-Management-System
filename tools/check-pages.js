const { chromium } = require('playwright-core');
const pages=['/Admin/Index.aspx','/Admin/Users.aspx','/Admin/Notifications.aspx','/Notifications/Index.aspx','/Student/Notifications.aspx','/Chatbot/Index.aspx','/Account/Settings.aspx','/Admin/Settings.aspx'];
(async()=>{
 const b=await chromium.launch({channel:'chrome',headless:true});
 const ctx=await b.newContext({viewport:{width:2460,height:1400}});
 const p=await ctx.newPage();
 const errs=[]; p.on('pageerror',e=>errs.push('PAGEERROR '+e.message)); p.on('console',m=>{if(m.type()==='error')errs.push('CONSOLE '+m.text())});
 await p.goto('http://localhost:8080/Account/Login.aspx',{waitUntil:'networkidle'});
 await p.locator('input[type=email]').fill('admin@sms.edu'); await p.locator('input[type=password]').fill('Admin@123');
 await Promise.all([p.waitForNavigation({waitUntil:'networkidle'}),p.locator('input[type=submit]').click()]);
 for(const u of pages){
   errs.length=0;
   const r=await p.goto('http://localhost:8080'+u,{waitUntil:'networkidle'});
   const m=await p.evaluate(()=>({w:document.documentElement.scrollWidth, cw:document.documentElement.clientWidth, title:document.title, h1:document.querySelector('h1')?.textContent.trim(), toolbar:!!document.querySelector('.toolbar'), list:!!document.querySelector('.list'), kpi:!!document.querySelector('.kpi'), page:!!document.querySelector('.page')}));
   console.log(u, r.status(), JSON.stringify(m), errs.length?errs.join(' | '):'OK');
 }
 await b.close();
})();
