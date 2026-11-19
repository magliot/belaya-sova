const fs=require('node:fs'),assert=require('node:assert/strict');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const out='golden-checks';fs.mkdirSync(out,{recursive:true});
const probe=`
window.goldenCheck={
  menu(t){
    hero='bear';G.t=t;G.waveT=1.6-t;render(0);
    const h=HEROMODELS.bear;
    const point=n=>h.holder.worldToLocal(h.bones[n].getWorldPosition(new THREE.Vector3()));
    const held=['Left','Right'].every(side=>{
      const hand=h.bones[side+'Hand'],pole=hand.getObjectByName(side+'_Pole');
      return pole?.visible&&pole.getObjectByName(side+'_Grip')&&hand.getObjectByName(side+'_FingerPad')&&hand.getObjectByName(side+'_ThumbPad');
    });
    return {held,hand:point('RightHand').toArray(),shoulder:point('RightArm').toArray(),center:point('Spine2').toArray()};
  },
  heroReady:()=>!!HEROMODELS[hero].glb,

  sledReady:()=>G.mode==='play'&&G.sled&&playerSled.children.length===1&&!!playerSled.children[0].isGroup,
  ready:()=>trackReady&&G.mode==='play'&&!!collectibleModels.aid&&!!collectibleModels.feather&&!!HEROMODELS[hero].motion,
  async meet(count,fps,side=0,boost=false,kind='bear',photo=false){
    await loadTrackAssets(kind);hero=kind;
    G.mode='pause';reviewX=145;reviewSeek(7650);G.mode='play';drag={};
    const o=G.objs.find(a=>a.type==='feather'),lane=o.x+side;
    G.p.x=G.tx=lane;if(boost)G.p.boost=6;let taps=0;
    for(let i=0;i<fps*12&&G.y<o.y+100&&G.mode==='play';i++){
      G.tx=lane;update(1/fps);if(photo)syncWorld(1/fps);
      if(G.p.parkFlight?.prizeLine&&!G.p.trick&&taps<count){
        for(const type of ['pointerdown','pointerup'])cv.dispatchEvent(new PointerEvent(type,{pointerId:1,clientX:170,clientY:540}));
        taps++;drag={};
      }
      if(photo&&G.y>o.y-145){render(0);break;}
    }
    return {hero,caught:!!o.dead,unlock:!!save.meet.prizeFeather,tricks:G.tricks,taps,d:o.y/20,boost};
  },
  end(){gameOver();},
  drive(seconds,fps){
    drag={};
    for(let i=0;i<Math.ceil(seconds*fps)&&G.mode==='play';i++){
      const target=G.objs.filter(o=>!o.dead&&o.y>G.y&&['snowman','gift','coin'].includes(o.type)).sort((a,b)=>a.y-b.y)[0];
      G.tx=target?target.x:200;update(1/fps);if(seconds<=8)syncWorld(1/fps);
    }
    animObjs(0);render(0);return {t:G.t,mode:G.mode,score:score(),dist:Math.floor(G.y/20),coins:G.coins,...G.sled,vehicle:playerSled.visible};
  },
  slowFrames(){drag={};for(let i=0;i<380&&G.mode==='play';i++){G.tx=200;advanceSledFrame(.2);}
   return {dist:Math.floor(G.y/20),elapsed:G.sled.elapsed,mode:G.mode};},
  tubeRun(){
    const phases=new Set();drag={};let shown=false;
    for(let i=0;i<4600&&G.mode==='play';i++){
      G.tx=G.objs.find(o=>o.y>G.y&&!o.dead&&['gate','coin'].includes(o.type))?.x??200;
      update(1/60);phases.add(tubeLocation(G.y/20));
      if(i%60===0){syncWorld(1/60);shown||=G.tubing.model.visible&&!HEROMODELS[hero].root.visible;}
    }
    return {phases:[...phases],shown,mode:G.mode,coins:G.coins,gates:G.gates,time:G.tubing.elapsed};
  },
  pauseSeconds(){const before=G.t;for(let i=0;i<300;i++)update(1/30);return G.t-before;},
  aid(){const o={type:'aid',x:200,y:G.y+2};G.p.x=G.tx=200;drag={};addObj(o);update(1/60);return {model:!!collectibleModels.aid,collected:o.dead,rule:save.fis.includes(9)};}
};`;
async function run(){
 const browser=await chromium.launch({args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try{
  const page=await browser.newPage({viewport:{width:375,height:812},deviceScaleFactor:1});
  const errors=[],requests=[],submissions=[];page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>requests.push(new URL(r.url()).pathname));
  let html=fs.readFileSync('docs/index.html','utf8');
  html=html.replace('function frame(now){','function frame(now){if(window.stopFrames)return;');
  const i=html.lastIndexOf('</script>');html=html.slice(0,i)+probe+html.slice(i);
  await page.route('**/vendor/telegram-web-app.js',r=>r.fulfill({body:`
    window.Telegram={WebApp:{initData:'test',initDataUnsafe:{user:{id:91}},platform:'web',
    ready(){},expand(){},onEvent(){},BackButton:{onClick(){},show(){},hide(){}},
    enableClosingConfirmation(){},disableClosingConfirmation(){},isVersionAtLeast(){return false}}};`}));
  await page.route('https://script.google.com/**',r=>{
    const data=JSON.parse(r.request().postData());if(data.action==='submit')submissions.push({score:data.score,dist:data.dist,dur:data.dur,hero:data.hero});
    return r.fulfill({json:{ok:true,rank:4,weekRank:3}});
  });
  await page.route('**/test-gold.html',r=>r.fulfill({body:html,contentType:'text/html; charset=utf-8'}));
  await page.goto('http://127.0.0.1:8766/test-gold.html');await page.waitForFunction(()=>goldenCheck.heroReady());
  await page.evaluate(()=>window.stopFrames=true);
  const heavy=requests.filter(r=>/models\/(?:owl-menu|studies|npc|trees|props|winter|sounds)/.test(r));
  assert.deepEqual(heavy,[]);assert(!(await page.locator('#bSled').isVisible()));
  const arms=[];for(const t of [.02,.2,.55,.9,1.35,1.58])arms.push(await page.evaluate(t=>goldenCheck.menu(t),t));
  await page.evaluate(()=>goldenCheck.menu(.6));await page.screenshot({path:out+'/wave.png'});
  assert(arms.every(({hand,center,held})=>held&&Math.abs(hand[0]-center[0])>.18),'Both poles must stay in the raised and resting gloves, clear of the jacket');
  await page.locator('#bPlay').click();await page.waitForFunction(()=>goldenCheck.ready());
  const aid=await page.evaluate(()=>goldenCheck.aid());assert(aid.model&&aid.collected&&aid.rule);
  const miss=[];
  for(const [count,side] of [[0,0],[1,0],[2,35]]){const r=await page.evaluate(args=>goldenCheck.meet(...args),[count,30,side]);assert(!r.caught&&!r.unlock,JSON.stringify(r));miss.push(r);}
  await page.evaluate(()=>goldenCheck.meet(2,60,0,false,'owl',true));await page.screenshot({path:out+'/feather.png'});
  const caught=[];
  for(const kind of ['owl','bear'])for(const fps of [30,60])for(const boosted of [false,true]){
    const r=await page.evaluate(args=>goldenCheck.meet(...args),[2,fps,0,boosted,kind]);
    assert(r.caught&&r.unlock&&r.tricks===2,JSON.stringify(r));caught.push(r);
  }
  await page.evaluate(()=>goldenCheck.end());await page.locator('#bSledResult').click();await page.waitForFunction(()=>goldenCheck.sledReady());
  await page.evaluate(()=>goldenCheck.drive(8,60));await page.screenshot({path:out+'/sled.png'});
  await page.locator('#bPause').click();assert(Math.abs(await page.evaluate(()=>goldenCheck.pauseSeconds()))<.00001);
  await page.locator('#bResume').click();
  const result=await page.evaluate(()=>goldenCheck.drive(80,60));assert.equal(result.mode,'over');assert(Math.abs(result.t-75)<.04);
  assert(result.coins>0&&result.gifts>0&&result.hits>0&&result.score>3000);
  assert(result.dist<=result.t*45+20&&result.score-result.dist<=result.dist*45+3000);
  await page.screenshot({path:out+'/result.png'});await page.locator('#bAgain').click();
  assert(await page.evaluate(()=>!!__g().sled));await page.reload();await page.locator('#bSled').waitFor();
  await page.locator('.seg [data-h="owl"]').click();await page.locator('#bSled').click();await page.waitForFunction(()=>goldenCheck.sledReady());await page.evaluate(()=>window.stopFrames=true);
  const owl=await page.evaluate(()=>goldenCheck.drive(90,30));assert.equal(owl.mode,'over');assert(owl.hits>0&&owl.gifts>0);
  await page.waitForTimeout(100);
  for(const [hero,run] of [['bear',result],['owl',owl]])assert(submissions.some(s=>s.hero===hero&&s.score===run.score&&s.dist===run.dist&&s.dur===75));
  const early=await browser.newPage({viewport:{width:375,height:812}});
  let releaseSDK;const gate=new Promise(r=>releaseSDK=r);
  await early.route('**/vendor/telegram-web-app.js',async route=>{await gate;await route.fulfill({body:''});});
  await early.goto('http://127.0.0.1:8766/',{waitUntil:'commit'});
  await early.locator('.booting [data-h="owl"]').click();await early.locator('#bSprint').click();releaseSDK();
  await early.waitForFunction(()=>window.__g?.()?.sprint&&__g().mode==='play');
  assert.equal(await early.evaluate(()=>JSON.parse(localStorage.getItem('bsova_hero'))),'owl');
  await early.close();
  await page.locator('#bAgain').click();await page.evaluate(()=>window.stopFrames=true);
  const slow=await page.evaluate(()=>goldenCheck.slowFrames());assert.equal(slow.mode,'over');assert.equal(slow.elapsed,75);
  assert(Math.abs(slow.dist-result.dist)<=1,'A 5 FPS client must cover the same bonus distance');
  await page.locator('#bMenu').click();await page.locator('#bTube').click();
  await page.waitForFunction(()=>!!__g().tubing);
  const tubing=await page.evaluate(()=>goldenCheck.tubeRun());
  assert(tubing.shown&&tubing.coins>0&&tubing.gates>0&&tubing.mode==='over'&&tubing.time===75,JSON.stringify(tubing));
  assert.deepEqual(tubing.phases,[0,1,2]);await page.locator('#bAgain').click();
  assert(await page.evaluate(()=>!!__g().tubing));
  const legacy=await browser.newPage();
  await legacy.addInitScript(()=>localStorage.setItem('bsova_meet',JSON.stringify({goldenOwl:true})));
  await legacy.goto('http://127.0.0.1:8766/');await legacy.locator('#bSled').waitFor();
  assert(!(await legacy.locator('#bTube').isVisible()));await legacy.close();
  for(const [ride,d] of [['tubing',100],['tubing',490],['tubing',870],['sled',420]]){
    await page.goto('http://127.0.0.1:8766/?preview=course&ride='+ride+'&seed=1911&at='+d);
    await page.waitForFunction(d=>window.__g?.()?.mode==='pause'&&__g().y===d*20,d);
    await page.locator('[data-r="fold"]').click();await page.screenshot({path:out+'/'+ride+'-'+d+'.png'});
  }
  assert.deepEqual(errors,[]);fs.writeFileSync(out+'/report.json',JSON.stringify({arms,heavy,aid,miss,caught,result,owl,slow,submissions,errors},null,2));
  console.log('PASS menu, feather with two tricks, three misses, persistent sled unlock, both riders, pause, scoring and finish.');
 }finally{await browser.close();}
}
run().catch(e=>{console.error(e);process.exitCode=1;});
