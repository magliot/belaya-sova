// Reach a moving Unity race from the ordinary bot, browser and former pilot URLs.
const assert=require('node:assert/strict');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const root=new URL(process.argv[2]||'http://127.0.0.1:8784/');
async function freshTelegramStart(browser,platform){
  const context=await browser.newContext({viewport:{width:360,height:740},hasTouch:true});
  try{
    const page=await context.newPage();
    const errors=[];
    page.on('pageerror',error=>errors.push(error.message));
    await context.route('https://script.google.com/**',route=>route.abort());
    if(platform==='ios'){
      await page.route(/\/Build\/[0-9a-f]{32}\.loader\.js(?:\?|$)/,async route=>{
        const response=await route.fetch();
        const original=await response.text();
        await route.fulfill({response,body:original+`
          const loadForRetryCheck=createUnityInstance;
          createUnityInstance=async function(...args){
            const game=await loadForRetryCheck(...args);
            const send=game.SendMessage.bind(game);
            game.SendMessage=(object,method,...rest)=>{
              if(method==='StartRace'&&!sessionStorage.retryChecked){
                sessionStorage.retryChecked='1';
                throw new Error('First StartRace bridge call failed in this test');
              }
              return send(object,method,...rest);
            };
            return game;
          };`});
      });
    }
    await page.addInitScript(platform=>{
      if(platform==='android')delete Object.hasOwn;
      // Isolate both Telegram profiles. The iOS account owns two spare helmets.
      window.TelegramWebviewProxy={postEvent(name,raw){
        if(name!=='web_app_invoke_custom_method')return;
        const data=JSON.parse(raw);
        const result=data.method==='getStorageValues'?
          (platform==='ios'?{unity_release_v1:JSON.stringify({schema:1,uid:9082,updated:1,data:{helmets:2}})}:{}):true;
        setTimeout(()=>Telegram.WebView.receiveEvent('custom_method_invoked',{req_id:data.req_id,result}),0);
      }};
    },platform);
    const user={id:platform==='ios'?9082:9081,first_name:'Тест запуска'};
    const init=new URLSearchParams({user:JSON.stringify(user),auth_date:'1770000000',hash:'fixture'});
    const url=new URL(root);
    url.hash=new URLSearchParams({tgWebAppData:init.toString(),tgWebAppPlatform:platform,tgWebAppVersion:'8.0'});
    await page.goto(url.href,{waitUntil:'commit'});
    await page.waitForURL(u=>u.pathname===root.pathname+'unity/',{timeout:60000});
    await page.locator('#play').waitFor({timeout:180000});
    assert.equal(await page.locator('#profileName').innerText(),user.first_name);
    assert.equal(await page.locator('#best').innerText(),'0');
    const localHelmets=()=>page.evaluate(()=>JSON.parse(localStorage.getItem('bsova_tg9082_unity_release_v1')).data.helmets);
    if(platform==='ios'){
      await page.locator('#play').tap();
      await page.locator('#retryLoad').waitFor({timeout:25000});
      assert((await page.locator('#loadText').innerText()).trim());
      assert.equal(await localHelmets(),2,'a rejected start consumed a helmet');
      await page.locator('#retryLoad').tap();
      await page.locator('#play').waitFor({timeout:180000});
      // A failing optional subsystem must not hold the menu over a running WebAssembly game.
      await page.evaluate(()=>{
        const previous=MountainAudio.update;
        window.soundFailureObserved=false;
        MountainAudio.update=state=>{
          if(state.phase==='play'){
            window.soundFailureObserved=true;
            throw new Error('Audio graph unavailable in this test');
          }
          previous(state);
        };
      });
    }
    await page.locator('#play').tap();
    await page.waitForFunction(()=>!document.querySelector('#hud').hidden&&
      parseInt(document.querySelector('#metres').textContent,10)>=6,null,{timeout:60000});
    assert(await page.locator('#menu').isHidden());
    if(platform==='ios'){
      assert(await page.evaluate(()=>window.soundFailureObserved));
      const rentedRanked=await page.evaluate(()=>!!window.MountainEquipment);
      assert.equal(await localHelmets(),rentedRanked?2:1,'wrong start entitlement for the published ruleset');
    }
    assert.deepEqual(errors,[]);
    console.log('PASS fresh Telegram',platform,'ordinary start, moving HUD');
  }finally{await context.close();}
}
async function main(){
  const browser=await chromium.launch({args:process.argv.includes('--software')?['--use-angle=swiftshader','--enable-unsafe-swiftshader']:[]});
  try{
    const page=await browser.newPage({viewport:{width:390,height:780},hasTouch:true});
    const resources=[],errors=[];
    page.on('request',r=>resources.push(new URL(r.url()).pathname));
    page.on('pageerror',e=>errors.push(String(e)));
    await page.route('https://script.google.com/**',r=>r.abort());
    for(const [link,entry,channel] of [['','unity','production'],['?preview=play','unity','production'],
      ['#tgWebAppStartParam=unitytest_20g','unity-test','test'],['unity-slope/?v=2026-11-20d','unity-test','test']]){
      await page.goto(new URL(link,root).href,{waitUntil:'commit'});
      await page.waitForURL(u=>u.pathname===root.pathname+entry+'/',{timeout:60000});
      await page.locator('#play').waitFor({timeout:180000});
      assert.equal(await page.evaluate(()=>window.mountainRelease.channel),channel);
      await page.locator('#play').tap();
      await page.waitForFunction(()=>parseInt(document.getElementById('metres').textContent,10)>=6,null,{timeout:60000});
      console.log('PASS',link||'ordinary start',entry,channel);
    }
    assert(!resources.some(p=>p.includes('/vendor/three/')));assert.deepEqual(errors,[]);
    await page.close();
    for(const platform of ['android','ios'])await freshTelegramStart(browser,platform);
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
