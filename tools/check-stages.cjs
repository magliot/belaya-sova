const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const out=path.resolve('stage-checks');fs.mkdirSync(out,{recursive:true});
let html=fs.readFileSync('docs/index.html','utf8').replace('function frame(now){','function frame(now){if(window.__freeze)return;');
const debug=`
window.__stageTest={
 ready:()=>HEROMODELS.bear.motion&&HEROMODELS.owl.motion&&NPCM.tree&&NPCM.ramp&&WINTER.snowman,
 loadPatrol,patrolReady:()=>!!patrolModel&&WINTER.yeti&&NPCM.fir&&NPCM.pine,
 trailShot(type){
  const start=type==='snowmobile'?2500:5000;
  hero='bear';G.mode='pause';reviewX=200;reviewSeek(start);
  const first=G.objs.find(o=>o.type===type);
  if(!first)throw Error('Missing first '+type);
  const d=first.marker/20-(type==='snowmobile'?9:12);
  reviewX=200+first.side*23;reviewSeek(d);
  const actor=G.objs.find(o=>o.type===type&&o.marker===first.marker);
  const pos=actor.m.position.clone();pos.y+=1;pos.project(camera);
  const forest=new Set(G.objs.filter(o=>o.type==='tree').map(o=>o.pine?'pine':o.fir?'fir':'tree'));
  const result={type,d,visible:Math.abs(pos.x)<.94&&Math.abs(pos.y)<.94&&pos.z<1,
   species:[...forest],trackPrints:MOG.patches.some(p=>p.tracks),model:type==='snowmobile'?!!actor.m.getObjectByName('Forest_patrol')||!!actor.m.getObjectByName('Forest patrol'):actor.m.userData.worldAsset==='yeti'};
  reviewX=200;return result;
 },
 encounter(type,kind,fps,collide){
  hero=kind;G.mode='pause';reviewX=200;reviewSeek(type==='snowmobile'?2500:5000);
  const first=G.objs.find(o=>o.type===type);
  reviewSeek(first.marker/20-12);
  const actor=G.objs.find(o=>o.type===type&&o.marker===first.marker);
  const x=collide?200+actor.side*(type==='snowmobile'?67:60):200-actor.side*34;
  const met=save.meet.yeti,initial=[actor.x,actor.y];G.p.x=G.tx=x;G.mode='play';G.nextChaseD=Infinity;
  G.p.lives=0;drag={};
  for(let i=0;i<fps*5&&G.mode==='play'&&G.y<first.marker+130;i++){G.tx=x;update(1/fps);}
  return {type,kind,fps,collide,fail:G.failKind||null,mode:G.mode,met:save.meet.yeti-met,dx:actor.x-initial[0],dy:actor.y-initial[1]};
 },
 ghost(kind){
  hero=kind;G.mode='pause';reviewSeek(7520);G.mode='play';update(1/60);
  const o=G.rival.o;o.x=G.p.x;o.y=G.y;render(0);
  const other=rivalKind(),r=HEROMODELS[other],state=r.ghost;
  const ratio=()=>{const [old,ghost]=state.materials.entries().next().value;return ghost.opacity/old.opacity;};
  const close=ratio();o.y=G.y+180;render(0);const far=ratio();
  const casts=state.meshes.some(e=>e.mesh.castShadow);
  hero=other;render(0);const restored=state.meshes.every(e=>e.mesh.material===e.base&&e.mesh.castShadow===e.shadow);
  return {kind,close,far,casts,restored};
 },
 skiSlope(d,ang){
  hero='bear';G.mode='pause';reviewSeek(d);G.p.ang=ang;const h=HEROMODELS.bear;
  h.motion.active=false;h.motion.lastT=null;h.motion.state={};
  for(let i=0;i<25;i++){G.t+=1/60;syncWorld(1/60);}
  scene.updateMatrixWorld(true);
  const ray=new THREE.Raycaster(),down=new THREE.Vector3(0,-1,0),mins=[];
  for(const name of ['Left_Ski','Right_Ski']){
   let lowest=Infinity;h.motion.model.getObjectByName(name).traverse(m=>{
    if(!m.isMesh)return;const positions=m.geometry.attributes.position;
    for(let i=0;i<positions.count;i+=2){
     const v=m.localToWorld(new THREE.Vector3().fromBufferAttribute(positions,i)),above=v.clone();above.y+=4;
     ray.set(above,down);const hit=ray.intersectObject(piste,false)[0];if(hit)lowest=Math.min(lowest,v.y-hit.point.y);
    }
   });mins.push(lowest);
  }
  return {d,ang,mins};
 },
 isSprint:()=>G.sprint,
 sprint(fps){
  drag={};let coffee=false,jump=false,windup=false,ball=false,motion=false,finish=false;
  for(let i=0;i<110*fps&&G.mode==='play';i++){
   const next=G.objs.filter(o=>!o.dead&&o.y>=G.y&&o.y-G.y<470&&(o.airHeight!=null||['thermos','ramp','gate'].includes(o.type))).sort((a,b)=>a.y-b.y)[0];
   G.tx=next?.x??200;update(1/fps);
   if(!motion){render(0);motion=HEROMODELS[hero].motion.active;}
   if(G.p.air&&G.p.trick<=0&&!G.p.spin)tryTrick(0);
   coffee||=G.p.boost>0;jump||=G.p.air>0;windup||=G.objs.some(o=>o.windup>0);
   ball||=G.objs.some(o=>o.type==='ball');finish||=G.objs.some(o=>o.type==='finish');
  }
  return {hero,fps,sprint:G.sprint,mode:G.mode,d:G.y/20,time:G.t,coffee,jump,windup,ball,motion,finish,rings:G.rings,tricks:G.tricks};
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
 park(kind,fps,tier,boosted=false,miss='',stop=''){
  hero=kind;G.mode='pause';const start=7650*20;
  reviewSeek(start/20);
  G.objs.forEach(release);G.objs=[];G.mode='play';G.nextY=G.nextChaseD=Infinity;
  drag={};save.missions=[];G.p.lives=0;G.p.boost=boosted?5:0;
  parkSection(start,tier);
  const ramp=G.objs.find(o=>o.park),ring=G.objs.find(o=>o.type==='airRing');
  const lip=ramp.y+ramp.len/2,targets=G.objs.filter(o=>o.airHeight!=null&&o!==ring);
  const x=ramp.x+(miss==='side'?40:0);G.p.x=G.tx=x;
  if(miss==='ground'){release(ramp);G.objs=G.objs.filter(o=>o!==ramp);G.p.magnet=7;}
  if(miss==='low')ramp.lift=.05;
  let top=0,error=0,samples=0,landed=false,tookOff=false;
  const down=new THREE.Vector3(0,-1,0),ray=new THREE.Raycaster(),ground=new THREE.Vector3();
  for(let i=0;i<10*fps&&G.mode==='play'&&G.y<lip+ramp.span*1.1+70;i++){
   G.tx=x;update(1/fps);
   if(G.p.rampH>0&&ramp.m){
    courseGround(ground,X(G.p.x),Z(G.y));const origin=ground.clone();origin.y+=10;
    ramp.m.updateMatrixWorld(true);ray.set(origin,down);const hit=ray.intersectObject(ramp.m,false)[0];
    if(!hit)throw Error('No visible ramp under riding hero');
    error=Math.max(error,Math.abs(hit.point.y-ground.y-G.p.rampH));samples++;
   }
   if(G.p.parkFlight){top=Math.max(top,flightHeight(G.p,G.y));tookOff=true;}
   else if(tookOff)landed=true;
   if(stop){syncWorld(1/fps);
    if((stop==='approach'&&G.y>ramp.y-ramp.len/2-50)||(stop==='ride'&&G.p.rampH>ramp.hh*.55)||(stop==='flight'&&G.y>lip+ramp.span*.26)){render(0);break;}
   }
  }
  return {kind,fps,tier,boosted,miss,top,error,samples,tookOff,landed,rings:G.rings,coins:G.coins,
   taken:targets.filter(o=>o.dead).map(o=>o.type),lives:G.p.lives,boost:G.p.boost,passed:!!ring.passed};
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
  await page.evaluate(()=>__stageTest.loadPatrol());
  await page.waitForFunction(()=>__stageTest.patrolReady(),null,{timeout:60000});
  const sprints=[];
  for(const [kind,fps] of [['bear',30],['owl',60]]){
   await page.locator('[data-h="'+kind+'"]').click();
   await page.locator('#bSprint').click();
   const run=await page.evaluate(fps=>__stageTest.sprint(fps),fps);
   assert(run.sprint&&run.mode==='over'&&run.d===1200&&run.time>=60&&run.time<=90,JSON.stringify(run));
   assert(run.coffee&&run.jump&&run.windup&&run.ball&&run.motion&&run.finish&&run.rings===3&&run.tricks>0,JSON.stringify(run));
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
   if(name==='park'){assert(s.counts.airRing>=1&&s.counts.ramp>=1&&s.counts.stageProp>=2);assert(s.wall>3.5);}
   if(name==='summit')assert(s.fogNear>=20&&s.fogFar>=95);
   await page.screenshot({path:path.join(out,kind+'-'+name+'.png')});scenes.push({name,...s});
  }
  const empty=await page.evaluate(()=>__stageTest.helmetAlpha(0)),two=await page.evaluate(()=>__stageTest.helmetAlpha(2));assert(two>empty*1.5,'Active helmet icons must be visibly stronger than empty slots');
  for(const kind of ['bear','owl'])for(const fps of [30,60]){
   for(const d of [2600,5100,7600]){const r=await page.evaluate(args=>__stageTest.ride(...args),[kind,d,fps]);assert.equal(r.mode,'play',JSON.stringify(r));assert(r.finite&&r.maxAbove<=1.001);rides.push(r);}
   const r=await page.evaluate(args=>__stageTest.rail(...args),[kind,fps]);assert.equal(r.mode,'play');assert(r.height>.6&&r.grinds===1);rails.push(r);
  }
  const flights=[];
  for(const kind of ['bear','owl'])for(const fps of [30,60])for(const tier of [0,1,2]){
   for(const boosted of [false,true]){
    const result=await page.evaluate(args=>__stageTest.park(...args),[kind,fps,tier,boosted]);
    assert(result.landed&&result.top>[2,3,4][tier]&&result.samples>1&&result.error<.035,JSON.stringify(result));
    assert(result.rings===1&&result.passed&&result.taken.length===4,JSON.stringify(result));
    assert.equal(result.coins,tier===0?4:3);
    if(tier===1)assert(result.taken.includes('thermos')&&result.boost>0);
    if(tier===2)assert(result.taken.includes('helmet')&&result.lives===1);
    flights.push(result);
   }
  }
  for(const miss of ['ground','side','low']){
   const result=await page.evaluate(args=>__stageTest.park(...args),['bear',30,2,false,miss]);
   assert(result.rings===0&&result.coins===0&&result.lives===0&&!result.passed,JSON.stringify(result));
   assert.equal(result.tookOff,miss!=='ground');flights.push(result);
  }
  for(const [kind,phase] of [['bear','approach'],['owl','ride'],['bear','flight'],['owl','flight']]){
   await page.evaluate(args=>__stageTest.park(...args),[kind,60,2,false,'',phase]);
   await page.screenshot({path:path.join(out,kind+'-park-'+phase+'.png')});
  }
  const encounters=[],trailShots=[],ghosts=[],skiContact=[];
  for(const type of ['snowmobile','mogulYeti']){
   const shot=await page.evaluate(type=>__stageTest.trailShot(type),type);
   assert(shot.visible&&shot.model,JSON.stringify(shot));
   if(type==='snowmobile')assert.equal(shot.species.length,3);else assert(shot.trackPrints);
   trailShots.push(shot);await page.screenshot({path:path.join(out,type+'.png')});
   for(const kind of ['owl','bear'])for(const fps of [30,60])for(const collide of [false,true]){
    const run=await page.evaluate(args=>__stageTest.encounter(...args),[type,kind,fps,collide]);
    assert(Math.abs(run.dx)+Math.abs(run.dy)>10,JSON.stringify(run));
    assert.equal(run.fail,collide?type:null,JSON.stringify(run));
    assert.equal(run.met,type==='mogulYeti'&&!collide?1:0);encounters.push(run);
   }
  }
  for(const kind of ['bear','owl']){
   const state=await page.evaluate(k=>__stageTest.ghost(k),kind);
   assert(state.close<.10&&state.far<.5&&state.far>state.close&&!state.casts&&state.restored,JSON.stringify(state));ghosts.push(state);
  }
  for(const d of [60,100,160,2510])for(const angle of [0,-.8,.8]){
   const sample=await page.evaluate(args=>__stageTest.skiSlope(...args),[d,angle]);
   assert(sample.mins.every(v=>Math.abs(v)<.018),JSON.stringify(sample));skiContact.push(sample);
  }
  assert.deepEqual(errors,[]);fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({viewport:[375,812],scenes,rides,rails,sprints,flights,landmark,encounters,trailShots,ghosts,skiContact,helmetAlpha:{empty,two},errors},null,2));
  console.log('PASS: distinct forest/mogul/park geometry and layouts, summit visibility, large HUD with drawn helmets, 12 twenty-second runs, four rideable-box cases, 24 air lines, 3 misses, 16 trail encounter cases, both ghost roles and 12 ski contact poses; no JS errors.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
