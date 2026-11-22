// Exercise save import with a save actually earned by the archived Three.js game.
// With a URL argument, run the second half against that real Unity Web player.
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),assert=require('node:assert/strict');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const docs=path.resolve('docs'),template=path.resolve('unity/OwlSlope/Assets/WebGLTemplates/MountainRace');
const mimes={'.html':'text/html; charset=utf-8','.js':'application/javascript; charset=utf-8','.css':'text/css','.woff2':'font/woff2','.wasm':'application/wasm','.mp3':'audio/mpeg','.webp':'image/webp','.jpg':'image/jpeg'};
const goldenSource=fs.readFileSync('tools/check-golden-run.cjs','utf8').match(/const probe=`([\s\S]*?)`;\r?\nasync function run/)[1];
const mockLoader=`window.createUnityInstance=async(canvas,config,progress)=>{
  progress(1);let s={phase:'menu',mode:'normal',hero:'bear',metres:0,stage:0,score:0,coins:0,helmets:0,seed:0};
  const emit=()=>window.onMountainState({...s});emit();
  return {SendMessage(_,name,arg){
    if(name==='ChooseHero')s.hero=arg;
    else if(name==='StartRace'){const c=JSON.parse(arg);Object.assign(s,c,{phase:c.mode==='review'?'review':'play',score:0,metres:0,stage:0});}
    else if(name==='Seek'&&s.phase==='review'){s.metres=arg;s.stage=Math.floor(s.metres/2500);}
    else if(name==='ReviewStep'&&s.phase==='review')s.metres+=.25;
    else if(name==='BeginSteer'&&s.phase==='play')s.metres=35;
    else if(name==='EndRide'&&s.phase==='pause'){s.phase='over';s.score=400;s.gates=1;s.tricks=0;}
    else if(name==='PauseRide')s.phase='pause';
    else if(name==='ResumeRide')s.phase='play';
    else if(name==='Menu')s.phase='menu';
    else return;emit();
  }};
};`;
function server(){
  return http.createServer((req,res)=>{
    const url=new URL(req.url,'http://localhost'),legacy=url.pathname.startsWith('/legacy/');
    const root=legacy?docs:template,name=decodeURIComponent(url.pathname.slice(legacy?8:1))||'index.html';
    if(name==='native-stub.js'){res.writeHead(200,{'Content-Type':mimes['.js']});return res.end(mockLoader);}
    const filename=path.resolve(root,name);if(!filename.startsWith(root+path.sep)){res.writeHead(404);return res.end();}
    fs.readFile(filename,(e,data)=>{
      if(e){res.writeHead(404);return res.end();}
      if(legacy&&['index.html','motion-entry.js','version.txt'].includes(name))
        data=Buffer.from(require('./archived-three.cjs')(name));
      if(legacy&&name==='index.html'){
        const html=data.toString().replace('function frame(now){','function frame(now){if(window.stopFrames)return;');
        const n=html.lastIndexOf('</script>');data=html.slice(0,n)+goldenSource+html.slice(n);
      }
      if(!legacy&&name==='index.html')data=data.toString().replace('Build/{{{ LOADER_FILENAME }}}','native-stub.js');
      res.writeHead(200,{'Content-Type':mimes[path.extname(filename)]||'application/octet-stream'});res.end(data);
    });
  });
}
async function main(){
  const s=server();await new Promise(resolve=>s.listen(0,'127.0.0.1',resolve));const base='http://127.0.0.1:'+s.address().port+'/';
  const browser=await chromium.launch({args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  const p=await browser.newPage({viewport:{width:375,height:667},hasTouch:true});
  let phase='legacy';const posts=[],errors=[];
  try{
    p.on('pageerror',e=>{if(phase==='unity')errors.push(e.message.slice(0,300));});
    await p.route('https://script.google.com/**',route=>{
      posts.push(phase);return route.fulfill({json:{ok:true,rank:1,weekRank:1,rows:[]}});
    });
    await p.route(/\/race\.js(?:\?|$)/,async route=>{
      const response=await route.fetch(),script=await response.text();
      const before='      await data;';
      assert(script.includes(before));
      // Observe the real JS-to-WASM bridge. Forward all arguments unchanged to Unity.
      const observe="const send=player.SendMessage.bind(player);player.SendMessage=(...a)=>{if(a[1]==='MenuSpin'||a[1]==='EndMenuSpin')window.spinCalls.push([a[1],a[2]??null]);return send(...a);};";
      await route.fulfill({response,body:script.replace(before,observe+'\n'+before)});
    });
    await p.addInitScript(()=>{
      window.hostEvents=[];
      window.spinCalls=[];
      window.TelegramWebviewProxy={postEvent(name,raw){
        const payload=JSON.parse(raw||'{}');window.hostEvents.push(name);
        if(name!=='web_app_invoke_custom_method')return;
        let result=true;
        if(payload.method==='saveStorageValue')localStorage.setItem('fixture-cloud-'+payload.params.key,payload.params.value);
        if(payload.method==='getStorageValues')result=Object.fromEntries(payload.params.keys.map(k=>[k,localStorage.getItem('fixture-cloud-'+k)||'']));
        setTimeout(()=>window.Telegram.WebView.receiveEvent('custom_method_invoked',{req_id:payload.req_id,result}),1);
      }};
    });
    const hash='#'+new URLSearchParams({tgWebAppData:new URLSearchParams({user:JSON.stringify({id:127,first_name:'QA'}),auth_date:'1770000000',hash:'fixture'}).toString(),tgWebAppPlatform:'android',tgWebAppVersion:'8.0'}).toString();
    await p.goto(base+'legacy/'+hash,{waitUntil:'domcontentloaded'});
    await p.waitForFunction(()=>window.goldenCheck?.heroReady());
    await p.locator('[data-h="owl"]').first().click();
    await p.locator('#bPlay').click();await p.waitForFunction(()=>goldenCheck.ready(),{},{timeout:90000});
    const caught=await p.evaluate(async()=>{window.stopFrames=true;const caught=await goldenCheck.meet(2,60,0,false,'owl');goldenCheck.end();return caught;});
    assert(caught.caught&&caught.unlock);await p.waitForFunction(()=>localStorage.getItem('fixture-cloud-save')&&JSON.parse(localStorage.getItem('fixture-cloud-save')).meet.prizeFeather);
    const legacy=await p.evaluate(()=>Object.fromEntries(Object.entries(localStorage).filter(([k])=>k.startsWith('bsova_tg127_')||k==='fixture-cloud-save')));
    assert.equal(JSON.parse(legacy.bsova_tg127_hero),'owl');assert(JSON.parse(legacy.bsova_tg127_best)>100);
    console.log('Archived game earned owl, feather and record.');
    // Preserve the real host's CloudStorage records across HTTP origins, like the Telegram backend.
    if(process.argv[2])await p.addInitScript(snapshot=>{for(const [k,v] of Object.entries(snapshot))if(k.startsWith('fixture-cloud-'))localStorage.setItem(k,v);},legacy);
    phase='unity';await p.goto((process.argv[2]||base).replace(/#.*/,'')+hash,{waitUntil:'domcontentloaded'});
    await p.locator('#play').waitFor({timeout:150000});
    console.log('New game reached the menu.');
    assert.equal(await p.locator('[data-hero="owl"]').getAttribute('aria-pressed'),'true');
    assert.equal(await p.locator('#best').innerText(),String(JSON.parse(legacy.bsova_tg127_best)));
    await p.locator('[data-panel="bonuses"]').click();
    for(const mode of ['tubing','sled'])assert(await p.locator('#panelBody [data-mode="'+mode+'"]').isEnabled());
    await p.locator('#closePanel').click();
    if(!process.argv[2]){
      const sample={phase:'play',hero:'owl',mode:'normal',metres:2520,stage:1,elapsed:10.3,speed:57,score:123456,helmets:1,coins:12,multiplier:3,gates:4};
      const hud=fields=>p.evaluate(s=>onMountainState(s),{...sample,...fields});
      await hud({score:42,scoreDistance:42,riderScreenX:.47,riderScreenY:.61});
      await hud({score:43,scoreDistance:43,riderScreenX:.47,riderScreenY:.61});
      assert.equal(await p.locator('#rewardFloats b').count(),0);
      await hud({score:46,scoreDistance:43,riderScreenX:.47,riderScreenY:.61});
      assert.equal(await p.locator('#rewardFloats b').last().innerText(),'+3');
      await hud({score:56,scoreDistance:45,riderScreenX:.52,riderScreenY:.61});
      assert.equal(await p.locator('#rewardFloats b').last().innerText(),'+8');
      await hud({boost:true,boostSeconds:3.4});
      assert.equal(await p.locator('#stage').innerText(),'2 · Лесная тропа');
      assert.equal(await p.locator('#metres').innerText(),'2520 м');assert.equal(await p.locator('#pace').innerText(),'57 км/ч');
      assert(await p.locator('#multiplier').isHidden());assert.equal(await p.locator('#score').innerText(),'123456');
      assert.equal(await p.locator('#coins').innerText(),'12');assert.match(await p.locator('#helmets').getAttribute('aria-label'),/1/);
      assert.match(await p.locator('#rideStatus').innerText(),/Ускорение · 3.4 с/);
      for(const width of [320,384]){
        await p.setViewportSize({width,height:740});
        await hud({stage:7,metres:17540,boost:true,boostSeconds:3.4});
        const [pause,stage,status,board]=await Promise.all(['#pause','#stage','#rideStatus','.scoreboard'].map(s=>p.locator(s).boundingBox()));
        assert(stage.y>=pause.y+pause.height&&stage.x>=board.x+board.width-1,'Stage should sit below Pause beside the scoreboard');
        assert(status.y>=stage.y+stage.height&&status.x>=board.x+board.width-1,'Boost must stay in the right column');
        assert(stage.x+stage.width<=width-7&&status.x+status.width<=width-7,'Long names must wrap inside the safe area');
      }
      await p.setViewportSize({width:375,height:667});
      await hud({magnetSeconds:2});assert.match(await p.locator('#rideStatus').innerText(),/Магнит · 2.0 с/);
      await hud({nextLetter:'С',letterSide:-1});assert.equal(await p.locator('#rideStatus').innerText(),'Буква «С» ←');
      await hud({speed:90});assert.equal(await p.locator('#pace').innerText(),'90 км/ч');
      assert.equal(await p.locator('#speedometer').evaluate(el=>el.style.getPropertyValue('--needle')),'0deg');
      await hud({boulderChasing:true,boulderLine:4});assert.equal(await p.locator('#rideStatus').innerText(),'Ком сзади · справа');await hud({});
      assert.equal(await p.locator('#avalancheMeter').count(),0);assert(await p.locator('#rideStatus').isHidden());
      await hud({mode:'tubing',metres:490,bonusLocation:1});assert.equal(await p.locator('#stage').innerText(),'Плюшки · 65 с');
      assert.equal(await p.locator('#multiplier').innerText(),'×3');
      assert.equal(await p.locator('#rideStatus').innerText(),'Еловый лес · ворота 4');
      await hud({mode:'review',phase:'review',courseMode:'sled',snowmen:3,gifts:5});
      assert.equal(await p.locator('#rideStatus').innerText(),'Снеговики 3 · подарки 5');assert(await p.locator('#pause').isHidden());
      await hud({phase:'pause'});assert(await p.locator('#hud').isVisible());assert(await p.locator('#paused').isVisible());
      await hud({phase:'menu'});
    }
    await p.evaluate(()=>Telegram.WebView.receiveEvent('viewport_changed',{height:565,is_state_stable:true,is_expanded:true}));
    assert.equal(Math.round((await p.locator('#app').boundingBox()).height),565);
    for(const v of ['#play','[data-hero="bear"]','[data-panel="bonuses"]'])assert(await p.locator(v).evaluate(el=>{const b=el.getBoundingClientRect();return el.contains(document.elementFromPoint(b.x+b.width/2,b.y+b.height/2));}));
    await p.locator('#slope').tap({position:{x:90,y:170}});assert.deepEqual(await p.evaluate(()=>spinCalls),[]);
    await p.mouse.move(80,170);await p.mouse.down();await p.mouse.move(104,170);await p.mouse.move(119,170);await p.mouse.up();
    assert.deepEqual(await p.evaluate(()=>spinCalls),[['MenuSpin',24],['MenuSpin',15],['EndMenuSpin',null]]);
    const host=(name,data={})=>p.evaluate(([n,d])=>Telegram.WebView.receiveEvent(n,d),[name,data]);
    const nav=name=>p.locator('#panelBody').getByRole('button',{name,exact:true}).click();
    const menu=panel=>p.locator('[data-panel="'+panel+'"]').click();
    const back=()=>host('back_button_pressed');
    await menu('top');
    await p.locator('#panelBody').getByText(/На этом устройстве|Личные заезды/).waitFor();await back();
    await menu('missions');assert.equal(await p.locator('#panelBody .stat').count(),4);await back();
    await menu('shop');assert.match(await p.locator('#panelBody').innerText(),/Доска «Салют»/);
    assert(await p.locator('[data-buy="fireboard"]').isDisabled());
    await p.locator('[data-product="supplies"]').click();
    assert.match(await p.locator('#panelBody').innerText(),/Магнит/);
    assert.match(await p.locator('#panelBody').innerText(),/Каска на старт/);await back();
    await menu('collection');assert.equal(await p.locator('.letter-row').count(),3);await back();
    await menu('rules');
    if(await p.evaluate(()=>!!window.MountainEquipment)){
      assert.equal(await p.locator('#panelBody>details,#panelBody>.stat').count(),10);
      assert.match(await p.locator('#panelBody').innerText(),/9\. Помоги пострадавшему/);
      const opened=p.locator('#panelBody summary');
      if(await opened.count()){await opened.first().click();assert.match(await p.locator('details[open]').innerText(),/В игре/);}
    }else{
      await p.locator('#panelBody summary').nth(8).click();assert.match(await p.locator('details[open]').innerText(),/аптечка/);
    }
    await back();
    assert.equal(await p.locator('#dances,[data-mode="sprint"],[data-mode="free"]').count(),0);
    assert.equal(await p.locator('#mast [data-panel="settings"]').count(),1);
    await menu('settings');await p.getByRole('button',{name:'Выключить звук',exact:true}).click();
    await p.getByRole('button',{name:'Включить звук',exact:true}).click();await back();
    await menu('settings');await nav('Замеры');await p.locator('dialog[open]').waitFor();await back();await p.locator('dialog').waitFor({state:'detached'});
    await nav('Лист моделей');await p.locator('.catalog-grid').waitFor();
    assert.equal(await p.locator('.catalog-grid button').count(),65);
    await nav('Из кода');const subset=await p.locator('.catalog-grid button').count();assert(subset>0&&subset<65);
    await p.locator('.catalog-grid button').first().click();assert((await p.locator('#panelBody>img').count())===1);await back();await back();
    await nav('Осмотр трассы');await p.locator('#review').waitFor();
    console.log('Opened the Unity course inspector after navigating the catalog.');
    await p.locator('#reviewAt').fill('2507');await p.locator('#reviewAt').press('Enter');
    await p.waitForFunction(()=>Number(document.getElementById('reviewMetres').value)===2507);
    await p.locator('#reviewLater').click();await p.waitForFunction(()=>Number(document.getElementById('reviewMetres').value)===2607);
    await p.locator('#reviewEarlier').click();
    await p.locator('#reviewStep').click();await p.locator('#reviewRate').selectOption('3');await p.locator('#reviewRate').selectOption('0');
    await p.locator('#reviewLane').evaluate(e=>{e.value='3';e.dispatchEvent(new Event('input',{bubbles:true}));});await p.locator('#reviewEffect').selectOption('coffee');
    await p.locator('#reviewHero').selectOption('bear');await p.locator('#reviewRide').selectOption('tubing');
    await p.waitForFunction(()=>Number(document.getElementById('reviewMetres').value)===2196);assert(await p.locator('#reviewStage').isDisabled());
    await p.locator('#reviewFold').click();assert(await p.locator('#reviewBody').isHidden());
    await p.locator('#reviewExit').click();await p.locator('#play').waitFor();
    console.log('Driving from ordinary menu.');
    await p.locator('#play').click();await p.locator('#pause').waitFor();
    await host('back_button_pressed');await p.locator('#resume').waitFor();
    assert.equal(await p.locator('#pauseVolumes input[type="range"]').count(),3);
    await p.locator('#resume').click();
    await host('visibility_changed',{is_visible:false});await p.locator('#resume').waitFor();
    await host('visibility_changed',{is_visible:true});assert(await p.locator('#resume').isVisible());
    await p.locator('#resume').click();await p.locator('#pause').click();await p.locator('#restartPaused').click();await p.locator('#pause').waitFor();
    await p.locator('#pause').click();await host('back_button_pressed');await p.locator('#play').waitFor();
    await p.locator('[data-hero="bear"]').click();
    await p.locator('#play').click();await p.locator('#pause').waitFor();await p.locator('#slope').tap({position:{x:174,y:310}});
    await p.waitForFunction(()=>parseInt(document.getElementById('metres').innerText,10)>=30);
    await p.locator('#pause').click();await p.locator('#exitPaused').click();
    assert.match(await p.locator('#resultStats').innerText(),/серия.*трюки[\s\S]*рекорд/);
    await p.locator('#resultTasks summary').click();assert.equal(await p.locator('#resultTasks .stat').count(),3);
    await p.locator('#share').click();
    await p.waitForFunction(()=>document.getElementById('shareImage').naturalWidth===1080);
    assert.equal(await p.locator('#shareImage').evaluate(e=>e.naturalHeight),1350);
    assert(!(await p.evaluate(()=>hostEvents)).includes('web_app_open_tg_link'));await back();await p.locator('#exitResult').click();
    await p.locator('#play').waitFor();
    const old=await p.evaluate(()=>Object.fromEntries(Object.entries(localStorage).filter(([k])=>(k.startsWith('bsova_tg127_')&&!k.includes('unity_'))||k==='fixture-cloud-save')));
    assert.equal(old['fixture-cloud-save'],legacy['fixture-cloud-save']);
    if(!process.argv[2])assert.deepEqual(old,legacy);
    assert(!posts.includes('unity'));assert((await p.evaluate(()=>hostEvents)).includes('web_app_ready'));
    assert.deepEqual(errors,[]);
    console.log('PASS earned legacy owl + feather, best and both bonuses survive; missions/shop/FIS/letters/settings/model catalog/review and 1080x1350 share preview; Telegram SDK Back, resize 565 px, minimize, return, explicit resume, restart, isolated writes; '+(process.argv[2]?'Unity Web':'native callbacks simulated'));
  }catch(error){
    const state=await p.evaluate(()=>({page:location.pathname,visible:[...document.querySelectorAll('section')].filter(el=>!el.hidden).map(el=>el.id),
      label:document.getElementById('loadText')?.innerText,metres:document.getElementById('metres')?.innerText})).catch(()=>null);
    console.error('Failed stage',phase,state,errors);throw error;
  }finally{await browser.close();await new Promise(r=>s.close(r));}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
