// Regression checks for free coasting, coffee, close racing, compact HUD and isolated course review.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const out=path.resolve('driving-checks');fs.mkdirSync(out,{recursive:true});
let html=fs.readFileSync('docs/index.html','utf8').replace('function frame(now){','function frame(now){if(window.__freeze)return;');
const debug=`
window.__drivingTest={
 ready:()=>HEROMODELS.bear.glb&&HEROMODELS.owl.glb,
 reset(kind='bear',d=0){clearWorld();hideToast();hero=kind;save.muted=true;save.missions=[];save.fis=[1,2,3,4,5,6,7,8,9,10];
  for(const k in keys)delete keys[k];drag=null;G=newGame('play');G.y=G.prevY=d*20;G.p.travelHeading=courseHeading(-d);G.stage=G.lastSpawnStage=stageAt(d);G.nextY=G.nextChaseD=Infinity;
  G.camY=courseHeight(d-6.4)+2.3;document.querySelectorAll('.ui').forEach(el=>el.classList.add('hidden'));$('topbtns').classList.remove('hidden');},
 step(n,fps=60){for(let i=0;i<n&&G.mode==='play';i++)update(1/fps);animObjs(0);render(0);return this.state();},
 state(){return {mode:G.mode,d:G.y/20,speed:G.speed,base:G.base,boost:G.p.boost,x:G.p.x,fail:G.failKind};},
 coffee(){this.reset();drag={};addObj({type:'thermos',x:200,y:8,r:12});return this.step(2);},
 gate(){this.reset();drag={};const o={type:'gate',x:200,y:80,gap:140,col:0};addObj(o);const before=o.m.userData.halo.material.opacity;
  this.step(30);return {before,after:o.m.userData.halo.material.opacity,passed:o.ok,gates:G.gates};},
 race(kind,fps){this.reset(kind);drag={};const samples=[];let coffeeAt=null;
  for(let i=0;i<100*fps;i++){if(i===30*fps){G.p.boost=4.5;coffeeAt=G.t;}update(1/fps);
   if(i%fps===0&&G.rival)samples.push({t:G.t,gap:(G.rival.o.y-G.y)/20,boost:G.p.boost});}
  render(0);return {samples,coffeeAt,mode:G.mode};},
 hud(){this.reset();drag={};this.step(1);G.chase={y:G.y-300,t:18,k:.9};G.rSay={txt:'Длинная реплика соперника, которая раньше закрывала склон',until:G.t+100};
  addObj({type:'gate',x:200,y:G.y+180,gap:140,col:0});updEnv(2,0);animObjs(0);banner('Испытание выполнено · +300','','gold');render(0);}
};
`;
const end=html.lastIndexOf('</script>');html=html.slice(0,end)+debug+html.slice(end);
(async()=>{
 const browser=await chromium.launch({headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try{
  const page=await browser.newPage({viewport:{width:375,height:812},hasTouch:true});const errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.route('https://telegram.org/js/telegram-web-app.js',r=>r.fulfill({body:''}));
  await page.route('**/test-driving.html',r=>r.fulfill({contentType:'text/html; charset=utf-8',body:html}));
  await page.addInitScript(()=>window.__freeze=true);
  await page.goto('http://127.0.0.1:8766/test-driving.html',{waitUntil:'domcontentloaded'});
  await page.waitForFunction(()=>window.__drivingTest?.ready(),null,{timeout:60000});
  const speeds=[];
  for(const kind of ['bear','owl'])for(const input of ['released','held','dragged-down','keyboard']){
   await page.evaluate(kind=>__drivingTest.reset(kind),kind);
   const pointer={pointerId:1,pointerType:'touch',clientX:190,clientY:450};
   if(input==='held'||input==='dragged-down')await page.locator('#gl').dispatchEvent('pointerdown',pointer);
   if(input==='dragged-down')await page.locator('#gl').dispatchEvent('pointermove',{...pointer,clientY:550});
   if(input==='keyboard')await page.keyboard.down('w');
   const s=await page.evaluate(()=>__drivingTest.step(60));speeds.push({kind,input,...s});
   await page.locator('#gl').dispatchEvent('pointerup',pointer);await page.keyboard.up('w');
  }
  for(const kind of ['bear','owl']){const rows=speeds.filter(r=>r.kind===kind);assert(rows.every(r=>r.mode==='play'));assert(rows.every(r=>Math.abs(r.speed-rows[0].speed)<1e-8),'Touch must never change acceleration');}
  const coffee=await page.evaluate(()=>__drivingTest.coffee());assert(Math.abs(coffee.speed/coffee.base-1.65)<1e-6);assert(coffee.boost>4.4);
  const gate=await page.evaluate(()=>__drivingTest.gate());assert(gate.passed&&gate.gates===1);assert(gate.after<gate.before/2);
  const races=[];
  for(const kind of ['bear','owl'])for(const fps of [30,60]){
   const r=await page.evaluate(args=>__drivingTest.race(...args),[kind,fps]);assert.equal(r.mode,'play');
   const ordinary=r.samples.filter(s=>s.t<30||s.t>45);
   const close=ordinary.filter(s=>Math.abs(s.gap)<18).length/ordinary.length;
   assert(close>.9,'Rival must stay nearby for most of the run');
   assert(r.samples.some(s=>s.gap>1)&&r.samples.some(s=>s.gap<-1),'Both heroes must have opportunities to lead');
   assert(r.samples.filter(s=>s.t>45).every(s=>Math.abs(s.gap)<18),'Rival must recover after coffee without cancelling the boost');
   races.push({kind,fps,closeFraction:close,maxGap:Math.max(...r.samples.map(s=>Math.abs(s.gap)))});
  }
  await page.evaluate(()=>__drivingTest.hud());
  const toast=await page.locator('#toast .tt').boundingBox();assert(toast.height<30&&toast.y>700,'Notifications must stay out of the central view');
  await page.screenshot({path:path.join(out,'compact-hud-gate.png')});
  assert.deepEqual(errors,[]);
  // A separate context simulates Telegram and protects sentinel production saves.
  const review=await browser.newPage({viewport:{width:375,height:812}}),posts=[],reviewErrors=[];
  review.on('pageerror',e=>reviewErrors.push(e.message));review.on('request',r=>{if(r.method()==='POST')posts.push(r.url());});
  await review.addInitScript(()=>{localStorage.setItem('bsova_tg991_best','7312');localStorage.setItem('bsova_tg991_coins','410');});
  await review.route('https://telegram.org/js/telegram-web-app.js',r=>r.fulfill({body:
   "window.__cloud=[];window.Telegram={WebApp:{initData:'test',initDataUnsafe:{user:{id:991},start_param:'course'},platform:'web',ready(){},expand(){},onEvent(){},enableClosingConfirmation(){},disableClosingConfirmation(){},isVersionAtLeast(){return false},BackButton:{onClick(){},show(){},hide(){}},CloudStorage:{getItem(k,cb){__cloud.push(['get',k]);cb(null,null)},setItem(k){__cloud.push(['set',k])}}}};"}));
  await review.goto('http://127.0.0.1:8766/?preview=course',{waitUntil:'domcontentloaded'});
  await review.locator('#course-review').waitFor();
  const stages=[];
  for(let i=0;i<8;i++){
   await review.locator('[data-r="stage"]').selectOption(String(i));
   const s=await review.evaluate(()=>({d:__g().y/20,stage:__g().stage,mode:__g().mode}));
   assert.equal(s.stage,i);assert.equal(s.d,i*2500+10);assert.equal(s.mode,'pause');stages.push(s);
  }
  await review.locator('[data-r="rate"]').selectOption('6');await review.locator('[data-r="play"]').click();
  await review.waitForFunction(()=>__g().y/20>17540);await review.locator('[data-r="play"]').click();
  const pausedAt=await review.evaluate(()=>__g().y);await review.waitForTimeout(250);assert.equal(await review.evaluate(()=>__g().y),pausedAt);
  await review.locator('[data-r="step"]').click();assert((await review.evaluate(()=>__g().y))>pausedAt);
  await review.locator('[data-r="hero"]').selectOption('owl');
  await review.locator('[data-r="effect"]').selectOption('avalanche');await review.screenshot({path:path.join(out,'course-review.png')});
  await review.locator('[data-r="effect"]').selectOption('coffee');assert.equal(await review.evaluate(()=>__g().p.boost),6);
  await review.locator('[data-r="seek"]').fill('1234');await review.locator('[data-r="seek"]').dispatchEvent('change');
  assert.equal(await review.evaluate(()=>__g().y/20),1234);
  const isolated=await review.evaluate(()=>({best:localStorage.getItem('bsova_tg991_best'),coins:localStorage.getItem('bsova_tg991_coins'),cloud:__cloud}));
  assert.equal(isolated.best,'7312');assert.equal(isolated.coins,'410');assert.deepEqual(isolated.cloud,[]);assert.deepEqual(posts,[]);assert.deepEqual(reviewErrors,[]);
  await review.locator('[data-r="exit"]').click();await review.waitForURL(/preview=play/);assert.equal(await review.locator('#course-review').count(),0);
  fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({speeds,coffee,gate,races,stages,isolated,posts,errors,reviewErrors},null,2));
  console.log('PASS: automatic speed for both heroes and all gestures; stronger collected coffee; close rival and catch-up; glowing gate; compact HUD; 8 review stages, pause/step/seek, isolated Telegram saves and no score posts.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
