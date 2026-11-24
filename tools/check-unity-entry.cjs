const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
const root=new URL('https://example.test/belaya-sova/');
const script=fs.readFileSync(require('node:path').resolve(__dirname,'../docs/motion-entry.js'),'utf8');
function route(input,pilot=false){
  const current=new URL(input,root);let target=null;
  const location={search:current.search,hash:current.hash,href:current.href,replace:href=>{target=new URL(href);}};
  vm.runInNewContext(script,{location,document:{currentScript:{hasAttribute:()=>pilot}},URLSearchParams,URL});
  return target;
}
for(const path of ['', '?preview=play','?preview=course','#tgWebAppPlatform=android','?startapp=motion']){
  assert.equal(route(path).pathname,root.pathname+'unity/');
}
for(const token of ['unitytest','unitytest_20g','unitytest_20h','unity']){
  for(const url of ['?startapp='+token,'#tgWebAppStartParam='+token]){
    assert.equal(route(url).pathname,root.pathname+'unity-test/');
  }
}
for(const path of ['?startapp=unitytest_1911','#tgWebAppStartParam=unitytest_1911']){
  const ordinary=route(path);
  assert.equal(ordinary.pathname,root.pathname+'unity-test/');
  assert.equal(ordinary.searchParams.get('seed'),'1911');
  assert.equal(ordinary.searchParams.get('reviewStage'),null);
}
const data='auth_date=42&user=%7B%22id%22%3A123%7D&start_param=unitytest_20g&hash=fixture';
const previewData=data.replace('unitytest_20g','unitytest_progress21');
for(const [token,stage,metre] of [['snowpark_1911','3',null],['festival_1911','7',null],['freeridewide_1911','8','165'],['boulder_1911','8','119']]){
  const jump=route('#'+new URLSearchParams({tgWebAppData:data.replace('unitytest_20g',token)}));
  assert.equal(jump.pathname,root.pathname+'unity-test/');
  assert.equal(jump.searchParams.get('reviewStage'),stage);assert.equal(jump.searchParams.get('reviewOffset'),metre);
}
for(const input of ['?startapp=freeride_1911','#tgWebAppStartParam=freeride_1911',
  '#'+new URLSearchParams({tgWebAppData:data.replace('unitytest_20g','freeride_1911')})]){
  const trial=route(input);
  assert.equal(trial.pathname,root.pathname+'unity-test/');
  assert.equal(trial.searchParams.get('reviewStage'),'8');
  assert.equal(trial.searchParams.get('seed'),'1911');
}
for(const input of ['?startapp=unitytest_progress21','#tgWebAppStartParam=unitytest_progress21',
  '#'+new URLSearchParams({tgWebAppData:previewData})]){
  assert.equal(route(input).pathname,root.pathname+'progression-test/');
}
for(const prefix of ['#','#?','#/play?','&']){
  const carried=new URLSearchParams({tgWebAppData:data,tgWebAppPlatform:'android'});
  const target=route('?seed=1911&preview=play'+prefix+carried);
  assert.equal(target.pathname,root.pathname+'unity-test/');
  assert.equal(target.searchParams.get('seed'),'1911');
  assert(!target.searchParams.has('tgWebAppData'));
  assert.equal(new URLSearchParams(target.hash.slice(1)).get('tgWebAppData'),data);
}
assert.equal(route('unity-slope/?v=2026-11-20d',true).pathname,root.pathname+'unity-test/');
console.log('PASS normal bot and browser open Unity, old test URLs stay isolated and Telegram data survives.');
