const { chromium } = require('playwright-core');
(async()=>{
 const b=await chromium.launch({channel:'chrome',headless:true});
 const ctx=await b.newContext({viewport:{width:2460,height:1400}});
 const p=await ctx.newPage();
 await p.goto('http://localhost:8080/Account/Login.aspx',{waitUntil:'networkidle'});
 const m=await p.evaluate(()=>{
   const r=e=>{const x=document.querySelector(e); if(!x) return null; const b=x.getBoundingClientRect(); const s=getComputedStyle(x); return {x:Math.round(b.x),y:Math.round(b.y),w:Math.round(b.width),h:Math.round(b.height),display:s.display,cols:s.gridTemplateColumns,overflowX:document.documentElement.scrollWidth>document.documentElement.clientWidth};};
   return {vw:innerWidth,scrollX:document.documentElement.scrollWidth,body:r('.auth'),brand:r('.auth__brand'),main:r('.auth__main'),card:r('.auth__card'),footer:r('.auth__footer'),appearance:r('.auth__appearance')};
 });
 console.log(JSON.stringify(m,null,2));
 await p.screenshot({path:'docs/login-after-2460.png',fullPage:true});
 await b.close();
})();
