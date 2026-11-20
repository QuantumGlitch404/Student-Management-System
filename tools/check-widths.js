const { chromium } = require('playwright-core');
const widths=[2460,1920,1440,1280,1024,768,390];
async function login(ctx){const p=await ctx.newPage();await p.goto('http://localhost:8080/Account/Login.aspx',{waitUntil:'networkidle'});await p.locator('input[type=email]').fill('admin@sms.edu');await p.locator('input[type=password]').fill('Admin@123');await Promise.all([p.waitForNavigation({waitUntil:'networkidle'}),p.locator('input[type=submit]').click()]);return p;}
(async()=>{const b=await chromium.launch({channel:'chrome',headless:true});
for(const w of widths){const ctx=await b.newContext({viewport:{width:w,height:1000}});await ctx.addCookies([{name:'sms_zoom',value:'100',url:'http://localhost:8080'},{name:'sms_font',value:'100',url:'http://localhost:8080'},{name:'sms_theme',value:'light',url:'http://localhost:8080'}]);const p=await login(ctx);await p.goto('http://localhost:8080/Admin/Index.aspx',{waitUntil:'networkidle'});
const m=await p.evaluate(()=>{const r=e=>{const x=document.querySelector(e);if(!x)return null;const b=x.getBoundingClientRect();return {x:Math.round(b.x),w:Math.round(b.width),right:Math.round(b.right),vw:innerWidth,ov:b.right>innerWidth+1||b.left< -1}};return {vw:innerWidth,docW:document.documentElement.scrollWidth,side:r('.app-sidebar'),content:r('.app-content'),page:r('.page'),kpi:r('.grid-kpi'),top:r('.app-topbar')};});
console.log(w,JSON.stringify(m)); await ctx.close();}
await b.close();})();
