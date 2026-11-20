(function(){
 function ready(f){document.readyState!=='loading'?f():document.addEventListener('DOMContentLoaded',f)}
 ready(function(){
  var nav=document.querySelector('.profile-nav');if(!nav)return;
  var body=nav.closest('.profile-shell')||document;
  var links=Array.prototype.slice.call(nav.querySelectorAll('a[href^="#"]'));
  if(!links.length)return;
  var panes=[];
  links.forEach(function(a){
   var id=a.getAttribute('href').slice(1);var target=document.getElementById(id);if(!target)return;
   var pane=target.closest('form.tile')||target.closest('.tile');if(!pane)return;
   pane.classList.add('profile-pane');panes.push(pane);
   var b=document.createElement('button');b.type='button';b.className=a.className;b.setAttribute('role','tab');b.setAttribute('data-tab',id);b.textContent=a.textContent;
   a.parentNode.replaceChild(b,a);
  });
  var buttons=Array.prototype.slice.call(nav.querySelectorAll('button[data-tab]'));
  function show(id,push){
   var found=false;
   buttons.forEach(function(b){var on=b.getAttribute('data-tab')===id;b.classList.toggle('active',on);b.setAttribute('aria-selected',on?'true':'false');if(on)found=true});
   panes.forEach(function(p){p.classList.toggle('is-active',!found||p===paneOf(id))});
   var pane=paneOf(id);if(pane){var content=pane.closest('.profile-content')||pane.parentNode;try{content.scrollTop=0}catch(e){}}
   if(push&&history.replaceState)history.replaceState(null,'','?tab='+encodeURIComponent(id));
  }
  function paneOf(id){var t=document.getElementById(id);if(!t)return null;return t.closest('form.tile')||t.closest('.tile')}
  buttons.forEach(function(b){b.addEventListener('click',function(){show(b.getAttribute('data-tab'),true)});b.addEventListener('keydown',function(e){if(e.key!=='ArrowDown'&&e.key!=='ArrowUp'&&e.key!=='ArrowRight'&&e.key!=='ArrowLeft')return;e.preventDefault();var i=buttons.indexOf(b);var n=(e.key==='ArrowDown'||e.key==='ArrowRight')?(i+1)%buttons.length:(i-1+buttons.length)%buttons.length;buttons[n].focus();show(buttons[n].getAttribute('data-tab'),true)})});
  var q=new URLSearchParams(location.search).get('tab')||(location.hash||'').replace('#','')||(buttons[0]&&buttons[0].getAttribute('data-tab'));
  if(q&&buttons.some(function(b){return b.getAttribute('data-tab')===q}))show(q,false);else show(buttons[0].getAttribute('data-tab'),false);
  window.addEventListener('popstate',function(){var t=new URLSearchParams(location.search).get('tab');if(t)show(t,false)});
 });
})();
