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
  ready:()=>trackReady&&G.mode==='play'&&!!collectibleModels.aid&&!!collectibleModels.goldenOwl&&!!HEROMODELS[hero].motion,
  meet(catchBird,fps){
    const o={type:'goldenOwl',anchor:2200,y:2200,x:200,side:1};
    clearWorld();G=newGame('play');G.y=G.prevY=1700;G.nextY=G.nextChaseD=Infinity;drag={};
    addObj(o);let range=[400,0],was=false;
    for(let i=0;i<fps*9;i++){
      G.tx=catchBird?o.x:90;update(1/fps);range=[Math.min(range[0],o.x),Math.max(range[1],o.x)];
      if(o.dead){was=true;break;}
    }
    return {caught:was,unlock:!!save.meet.goldenOwl,range};
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
  const miss=await page.evaluate(()=>goldenCheck.meet(false,30));assert(!miss.caught&&!miss.unlock);
  const caught=await page.evaluate(()=>goldenCheck.meet(true,60));assert(caught.caught&&caught.unlock&&caught.range[1]-caught.range[0]>20);
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
  assert.deepEqual(errors,[]);fs.writeFileSync(out+'/report.json',JSON.stringify({arms,heavy,aid,miss,caught,result,owl,slow,submissions,errors},null,2));
  console.log('PASS menu requests, wave, rescue, missed/caught owl, persistent unlock, both sled riders, pause, scoring and finish.');
 }finally{await browser.close();}
}
run().catch(e=>{console.error(e);process.exitCode=1;});
