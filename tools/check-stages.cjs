const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const out=path.resolve('stage-checks');fs.mkdirSync(out,{recursive:true});
let html=fs.readFileSync('docs/index.html','utf8').replace('function frame(now){','function frame(now){if(window.__freeze)return;');
const debug=`
window.__stageTest={
 ready:()=>HEROMODELS.bear.motion&&HEROMODELS.owl.motion&&NPCM.tree&&NPCM.ramp&&WINTER.snowman,
 isSprint:()=>G.sprint,
 sprint(fps){
  drag={};let coffee=false,jump=false,windup=false,ball=false,motion=false,finish=false;
  for(let i=0;i<110*fps&&G.mode==='play';i++){
   const next=G.objs.filter(o=>!o.dead&&o.y>=G.y&&o.y-G.y<470&&['thermos','ramp','gate'].includes(o.type)).sort((a,b)=>a.y-b.y)[0];
   G.tx=next?.x??200;update(1/fps);
   if(!motion){render(0);motion=HEROMODELS[hero].motion.active;}
   if(G.p.air&&G.p.trick<=0&&!G.p.spin)tryTrick(0);
   coffee||=G.p.boost>0;jump||=G.p.air>0;windup||=G.objs.some(o=>o.windup>0);
   ball||=G.objs.some(o=>o.type==='ball');finish||=G.objs.some(o=>o.type==='finish');
  }
  return {hero,fps,sprint:G.sprint,mode:G.mode,d:G.y/20,time:G.t,coffee,jump,windup,ball,motion,finish,tricks:G.tricks};
 },
 landmark(){
  reviewSeek(10035);const o=G.objs.find(o=>o.type==='snowman'&&o.y===10055*20);if(!o)return null;
  const v=o.m.position.clone();v.y+=1;v.project(camera);
  return {x:v.x,y:v.y,z:v.z};
 },
 capture(kind,d){G.mode='pause';reviewSeek(d);hero=kind;G.p.lives=2;G.coins=24;G.bonus=1265;render(0);
  const counts={};for(const o of G.objs)counts[o.type]=(counts[o.type]||0)+1;
  let peak=0;for(const P of MOG.patches)for(let y=P.y0;y<P.y1;y+=30)for(let x=20;x<400;x+=20)peak=Math.max(peak,bumpH(P,x,y));
  return {d,kind,counts,patches:MOG.patches.length,rolling:MOG.patches.some(P=>P.rolling),peak,width:courseWidth(d),wall:courseCrossHeight(12,-d),fogNear:scene.fog.near,fogFar:scene.fog.far,
   triangles:renderer.info.render.triangles,calls:renderer.info.render.calls,finite:HEROMODELS[kind].root.position.toArray().every(Number.isFinite)};
 },
 ride(kind,d,fps){clearWorld();hideToast();hero=kind;save.missions=[];save.fis=[1,2,3,4,5,6,7,8,9,10];G=newGame('play');G.y=G.prevY=d*20;G.stage=G.lastSpawnStage=stageAt(d);G.nextY=G.y+300;G.nextChaseD=Infinity;drag={};let peak=0,maxAbove=0;
  for(let i=0;i<20*fps&&G.mode==='play';i++){const gate=G.objs.find(o=>o.type==='gate'&&!o.done&&o.y>G.y);G.tx=gate?gate.x:200;update(1/fps);peak=Math.max(peak,G.p.mh);maxAbove=Math.max(maxAbove,G.p.mh-mogAt(G.p.x,G.y));}
  render(0);return {kind,d,fps,mode:G.mode,fail:G.failKind||null,peak,maxAbove,finite:Number.isFinite(G.p.mh)&&Number.isFinite(G.p.alt)};
 },
 rail(kind,fps){clearWorld();hero=kind;save.missions=[];G=newGame('play');G.y=G.prevY=7600*20;G.stage=G.lastSpawnStage=3;G.nextY=G.nextChaseD=Infinity;drag={};
  addObj({type:'rail',x:200,y:G.y+200,w:46,len:200,hh:.65});let height=0;
  for(let i=0;i<3*fps&&G.mode==='play';i++){G.tx=200;update(1/fps);height=Math.max(height,G.p.rampH);}render(0);return {kind,fps,mode:G.mode,height,grinds:G.grinds||0};},
 helmetAlpha(n){G.p.lives=n;render(0);const a=hx.getImageData(W-110,10+SAFE_TOP+48,100,42).data;let sum=0;for(let i=3;i<a.length;i+=4)sum+=a[i];return sum;}
};
`;
const at=html.lastIndexOf('</script>');html=html.slice(0,at)+debug+html.slice(at);
(async()=>{
 const browser=await chromium.launch({headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try{
  const page=await browser.newPage({viewport:{width:375,height:812},deviceScaleFactor:1});const errors=[];
  page.on('pageerror',e=>errors.push(e.message));await page.addInitScript(()=>window.__freeze=true);
  await page.route('https://telegram.org/js/telegram-web-app.js',r=>r.fulfill({body:''}));
  await page.route('**/test-stages.html',r=>r.fulfill({contentType:'text/html; charset=utf-8',body:html}));
  await page.goto('http://127.0.0.1:8766/test-stages.html',{waitUntil:'domcontentloaded'});
  await page.waitForFunction(()=>window.__stageTest?.ready(),null,{timeout:60000});
  const sprints=[];
  for(const [kind,fps] of [['bear',30],['owl',60]]){
   await page.locator('[data-h="'+kind+'"]').click();
   await page.locator('#bSprint').click();
   const run=await page.evaluate(fps=>__stageTest.sprint(fps),fps);
   assert(run.sprint&&run.mode==='over'&&run.d===1200&&run.time>=60&&run.time<=90,JSON.stringify(run));
   assert(run.coffee&&run.jump&&run.windup&&run.ball&&run.motion&&run.finish&&run.tricks>0,JSON.stringify(run));
   assert.match(await page.locator('#oFail').innerText(),/Финиш/);
   await page.screenshot({path:path.join(out,kind+'-sprint-finish.png')});
   await page.locator('#bAgain').click();
   assert(await page.evaluate(()=>__stageTest.isSprint()));
   await page.locator('#bPause').click();await page.locator('#bQuit').click();
   sprints.push(run);
  }
  await page.locator('#bPlay').click();assert.equal(await page.evaluate(()=>__stageTest.isSprint()),false);
  const landmark=await page.evaluate(()=>__stageTest.landmark());
  assert(landmark&&Math.abs(landmark.x)<.95&&Math.abs(landmark.y)<.95&&landmark.z<1,JSON.stringify(landmark));
  await page.screenshot({path:path.join(out,'summit-landmark.png')});
  const scenes=[],rides=[],rails=[];
  for(const kind of ['bear','owl'])for(const [name,d] of [['training',100],['forest',2600],['moguls',5100],['park',7580],['summit',10100]]){
   const s=await page.evaluate(args=>__stageTest.capture(...args),[kind,d]);assert(s.finite);
   if(name==='forest'){assert(s.width<14);assert(s.counts.tree>=10);assert(!s.counts.rock&&!s.counts.rail);}
   if(name==='moguls'){assert(s.rolling&&s.peak>1&&s.peak<2.8);assert(!s.counts.rock&&!s.counts.ramp);}
   if(name==='park'){assert(s.counts.rail>=1&&s.counts.ramp>=1&&s.counts.stageProp>=6);assert(s.wall>3.5);}
   if(name==='summit')assert(s.fogNear>=20&&s.fogFar>=95);
   await page.screenshot({path:path.join(out,kind+'-'+name+'.png')});scenes.push({name,...s});
  }
  const empty=await page.evaluate(()=>__stageTest.helmetAlpha(0)),two=await page.evaluate(()=>__stageTest.helmetAlpha(2));assert(two>empty*1.5,'Active helmet icons must be visibly stronger than empty slots');
  for(const kind of ['bear','owl'])for(const fps of [30,60]){
   for(const d of [2600,5100,7600]){const r=await page.evaluate(args=>__stageTest.ride(...args),[kind,d,fps]);assert.equal(r.mode,'play',JSON.stringify(r));assert(r.finite&&r.maxAbove<=1.001);rides.push(r);}
   const r=await page.evaluate(args=>__stageTest.rail(...args),[kind,fps]);assert.equal(r.mode,'play');assert(r.height>.6&&r.grinds===1);rails.push(r);
  }
  assert.deepEqual(errors,[]);fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({viewport:[375,812],scenes,rides,rails,sprints,landmark,helmetAlpha:{empty,two},errors},null,2));
  console.log('PASS: distinct forest/mogul/park geometry and layouts, summit visibility, large HUD with drawn helmets, 12 twenty-second runs, four rideable-box cases, no JS errors.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
