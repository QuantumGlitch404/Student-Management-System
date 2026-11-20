const { chromium } = require('playwright-core');
(async()=>{const b=await chromium.launch({channel:'chrome',headless:true});const ctx=await b.newContext({viewport:{width:1440,height:900}});const p=await ctx.newPage();
p.on('pageerror',e=>console.log('PAGEERROR',e.message)); p.on('console',m=>{if(m.type()==='error')console.log('CONSOLE',m.text())});
await p.goto('http://localhost:8080/Account/Login.aspx',{waitUntil:'networkidle'});await p.locator('input[type=email]').fill('admin@sms.edu');await p.locator('input[type=password]').fill('Admin@123');await Promise.all([p.waitForNavigation({waitUntil:'networkidle'}),p.locator('input[type=submit]').click()]);
const s=await p.evaluate(async()=>await (await fetch('/Handlers/AiStatus.ashx',{credentials:'same-origin'})).json()); console.log('STATUS',JSON.stringify(s));
await p.goto('http://localhost:8080/Chatbot/Index.aspx',{waitUntil:'networkidle'}); await p.waitForTimeout(3000); console.log('statusText', await p.locator('#aiStatusText').textContent());
const input=p.locator('textarea').first(); if(await input.count()){await input.fill('In one sentence, what is this system for?'); await p.locator('#aiSend, button[type=submit]').first().click(); await p.waitForTimeout(30000); const msgs=await p.locator('.ai-msg').allTextContents(); console.log('MSGS',msgs.slice(-2));}
console.log('status2', await p.locator('#aiStatusText').textContent());
await p.screenshot({path:'docs/ai-after-1440.png',fullPage:true});await b.close();})();
