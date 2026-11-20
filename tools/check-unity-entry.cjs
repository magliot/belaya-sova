// Router regression checks. No network, Unity or Telegram account required.
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const router=fs.readFileSync(path.join(__dirname,'../docs/motion-entry.js'),'utf8');
const domain='https://magliot.github.io';
const root=domain+'/belaya-sova/';
const full=root+'unity-test/';
const pilotUrl=root+'unity-slope/';
const init=start=>new URLSearchParams({user:'{"id":123,"first_name":"QA & Co"}',...(start?{start_param:start}:{}),auth_date:'42',hash:'example+data'}).toString();
const hash=(data,other={})=>new URLSearchParams({tgWebAppData:data,tgWebAppVersion:'8.0',tgWebAppPlatform:'android',...other}).toString();
function fixture(url,{pilot=false,stored='',loading=false}={}){
  let current=new URL(url);
  const redirects=[],handlers={},native={},docHandlers={},links=[],ready=[];
  const location={get search(){return current.search;},get hash(){return current.hash;},get href(){return current.href;},
    get origin(){return current.origin;},replace(target){redirects.push(new URL(target,current));}};
  // Simulate a normal SDK restoring initData from an earlier Mini App launch.
  const app={initData:stored,initDataUnsafe:{start_param:new URLSearchParams(stored).get('start_param')},
    ready(){ready.push(true);},onEvent(name,fn){native[name]=fn;}};
  const document={currentScript:{hasAttribute:()=>pilot},readyState:loading?'loading':'interactive',
    addEventListener(name,fn){docHandlers[name]=fn;},querySelectorAll:()=>links};
  const window={Telegram:{WebApp:app},addEventListener(name,fn){handlers[name]=fn;}};
  vm.runInNewContext(router,{window,document,location,URLSearchParams,URL});
  return {redirects,window,links,ready,setURL(next){current=new URL(next,current);},
    hashchange(next){current=new URL(next,current);handlers.hashchange();},
    activated(){native.activated();},show(persisted=false){handlers.pageshow({persisted});},
    pop(){handlers.popstate();},loaded(){document.readyState='interactive';docHandlers.DOMContentLoaded();}};
}
function target(result,expected){
  assert.equal(result.redirects.length,1);const url=result.redirects[0];
  assert.equal(url.origin+url.pathname,expected);return url;
}
for(const token of ['unitytest','unitytest_20g']){
  target(fixture(root+'?tgWebAppStartParam='+token),full);
  target(fixture(root+'#tgWebAppStartParam='+token),full);
  const data=init(token),context=hash(data);
  for(const fragment of ['#','#/play?','#?']){
    const url=target(fixture(root+'?preview=play&seed=1911'+fragment+context),full);
    assert.equal(url.searchParams.get('seed'),'1911');assert.equal(url.searchParams.get('v'),'2026-11-20g');
    assert.equal(new URLSearchParams(url.hash.slice(1)).get('tgWebAppData'),data);
  }
  target(fixture(root+'?tgWebAppStartParam=unity#'+context),full);
  const get=target(fixture(root+'?'+hash(data,{tgWebAppStartParam:'unity',preview:'course'})),full);
  assert(!get.searchParams.has('tgWebAppData'));assert.equal(new URLSearchParams(get.hash.slice(1)).get('tgWebAppData'),data);
  target(fixture(pilotUrl+'?tgWebAppStartParam='+token,{pilot:true}),full);
  target(fixture(pilotUrl+'#'+context,{pilot:true}),full);
}
console.log('PASS both test tokens in GET, fragment, nested current data, path fragment, pilot and preview; preserve credentials and seed');
for(const origin of [root,root+'?preview=play',root+'?v=old#'+hash(init())]){
  const page=fixture(origin,{stored:init('unitytest')});page.activated();page.show();page.pop();
  assert.equal(page.redirects.length,0);assert.equal(page.ready.length,1);
}
for(const origin of [pilotUrl,pilotUrl+'?tgWebAppStartParam=unity']){
  const page=fixture(origin,{pilot:true,stored:init('unitytest')});assert.equal(page.redirects.length,0);
}
for(const build of ['2026-11-20d','2026-11-20e']){
  target(fixture(pilotUrl+'?v='+build,{pilot:true}),full);
  for(const select of ['&tgWebAppStartParam=unity','&startapp=unity','&preview=play','#'+hash(init('unity'))]){
    assert.equal(fixture(pilotUrl+'?v='+build+select,{pilot:true}).redirects.length,0);
  }
}
target(fixture(root+'?tgWebAppStartParam=unity'),pilotUrl);
target(fixture(root+'?preview=motion'),root+'animation-test.html');
assert.equal(fixture(root+'?tgWebAppStartParam=course').window.courseLaunch,true);
assert.equal(fixture(root+'?tgWebAppStartParam=unknown').redirects.length,0);
console.log('PASS ordinary menu and pilot after a test, stale SDK, preview and existing motion/course links');
for(const [url,pilot] of [[root,false],[pilotUrl,true]]){
  const page=fixture(url,{pilot,loading:true});const context=hash(init(''));
  page.hashchange('#/play?'+context);
  const link={href:new URL(pilot?'../unity-test/?v=2026-11-20g':'./unity-test/?v=2026-11-20g',url).href};
  page.links.push(link);page.loaded();const actual=new URL(link.href);
  assert.equal(actual.origin+actual.pathname,full);assert.equal(actual.searchParams.get('v'),'2026-11-20g');
  assert.equal(new URLSearchParams(actual.hash.slice(1)).get('tgWebAppData'),init(''));
  page.hashchange('#tgWebAppStartParam=unitytest_20g');page.activated();page.pop();page.show();
  target(page,full);assert(page.window.sovaEntryNavigating);
  const retained=new URL(page.links[0].href);assert.equal(new URLSearchParams(retained.hash.slice(1)).get('tgWebAppStartParam'),'unitytest_20g');
  const restored=fixture(url,{pilot,stored:init('unity')});
  restored.setURL('#'+hash(init('unitytest_20g')));restored.show(true);target(restored,full);
}
console.log('PASS manual links from both screens after DOM is ready, same-document reentry, deduplicated navigation');
