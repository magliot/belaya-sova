// Real WebGL renders of the curved course. Inspect the PNGs before release.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.DANCE_BROWSER_MODULE || 'playwright');
const out = path.resolve(process.env.COURSE_CHECK_OUT || 'course-checks');
fs.mkdirSync(out, { recursive: true });
const source = fs.readFileSync('docs/index.html', 'utf8');
const debug = `
window.__courseReview = {
 ready:()=>HEROMODELS.bear.glb&&HEROMODELS.owl.glb&&['skier','ramp','tree'].every(k=>NPCM[k]),
 snapshot(kind,d,phase='ride'){
  clearWorld(); hideToast(); hero=kind; G=newGame('play');
  G.y=G.prevY=d*20; G.stage=G.lastSpawnStage=stageAt(d); G.nextY=G.y+300;
  G.nextChaseD=Infinity; G.p.v=400; G.speed=400; G.t=10;
  G.camY=courseHeight(d-6.4)+2.3;
  for(const t of forest)t.userData.z-=d;
  for(const l of lamps)l.userData.z-=d;
  for(const t of towers)t.userData.z-=d;
  for(const c of chairs)c.userData.z=-d-20-(chairs.indexOf(c)%12)*20;
  while(G.nextY<G.y+AHEAD)spawnSeg();
  // Consistent landmarks expose terrain alignment and visibility in every capture.
  addObj({type:'gate',x:200,y:G.y+220,gap:140,col:0});
  addObj({type:'ramp',x:270,y:G.y+560,w:44,hh:.75});
  if(phase==='air'){G.p.air=1;G.p.airT=.35;G.p.airMax=1;G.p.alt=.8;}
  if(phase==='left')G.p.ang=-.65;
  if(phase==='right')G.p.ang=.65;
  document.querySelectorAll('.ui').forEach(el=>el.classList.add('hidden'));
  $('topbtns').classList.remove('hidden');
  for(let i=0;i<80;i++)updEnv(1/10,stageDef(stageAt(d)).env);
  animObjs(0);render(1/60);
  return this.measure();
 },
 measure(){
  const h=HEROMODELS[hero];camera.updateMatrixWorld(true);
  const foot=h.root.position.clone().project(camera);
  return {hero,mode:G.mode,distance:G.y/20,heading:courseHeading(Z(G.y)),
   heroPosition:h.root.position.toArray(),heroScreen:foot.toArray(),camera:camera.position.toArray(),
   drawCalls:renderer.info.render.calls,triangles:renderer.info.render.triangles,
   contextLost:renderer.getContext().isContextLost()};
 },
 step(n){for(let i=0;i<n;i++){update(1/60);render(1/60);}return this.measure();},
 get state(){return {mode:G.mode,x:G.p.x,t:G.t,air:G.p.air,speed:G.speed};},
 get audio(){return {...save.vol};}
};
`;
let html = source.replace('function frame(now){', 'function frame(now){if(window.__freeze)return;');
const end = html.lastIndexOf('</script>');
html = html.slice(0, end) + debug + html.slice(end);
(async () => {
 const browser = await chromium.launch({headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try {
  const page = await browser.newPage({viewport:{width:375,height:812},deviceScaleFactor:1,hasTouch:true});
  const errors=[], failed=[], snapshots=[];
  page.on('pageerror', e=>errors.push(e.message));
  page.on('response', r=>{if(r.status()>=400 && r.url().startsWith('http://127.0.0.1:8766/'))failed.push([r.status(),r.url()]);});
  await page.route('https://telegram.org/js/telegram-web-app.js',r=>r.fulfill({contentType:'text/javascript',body:''}));
  await page.route('**/test-course.html',r=>r.fulfill({contentType:'text/html; charset=utf-8',body:html}));
  await page.addInitScript(()=>window.__freeze=true);
  await page.goto('http://127.0.0.1:8766/test-course.html',{waitUntil:'domcontentloaded'});
  await page.waitForFunction(()=>window.__courseReview?.ready(),null,{timeout:60000});
  for(const kind of ['bear','owl']) {
   for(const [d,phase] of [[0,'ride'],[45,'left'],[110,'right'],[180,'air'],[315,'ride'],[450,'ride'],[2510,'ride'],[5020,'ride'],[10020,'ride'],[15030,'ride'],[17530,'ride']]) {
    const state=await page.evaluate(args=>__courseReview.snapshot(...args),[kind,d,phase]);
    assert(state.heroPosition.concat(state.camera,state.heroScreen).every(Number.isFinite));
    assert(Math.abs(state.heroScreen[0])<1&&Math.abs(state.heroScreen[1])<1&&state.heroScreen[2]<1);
    assert(!state.contextLost);assert(state.drawCalls>0&&state.triangles>0);
    const file=kind+'-'+d+'-'+phase+'.png';
    await page.screenshot({path:path.join(out,file)});snapshots.push({file,...state});
   }
  }
  await page.setViewportSize({width:1280,height:800});
  for(const kind of ['bear','owl']) {
   await page.evaluate(kind=>__courseReview.snapshot(kind,110),kind);
   await page.screenshot({path:path.join(out,kind+'-desktop.png')});
  }
  await page.setViewportSize({width:375,height:812});
  // Exercise real UI handlers, keyboard steering, pause and restart.
  await page.reload({waitUntil:'domcontentloaded'});
  await page.waitForFunction(()=>window.__courseReview?.ready());
  await page.evaluate(()=>__courseReview.step(5));
  await page.screenshot({path:path.join(out,'menu.png')});
  await page.locator('#bSet').click();
  for(const [kind,value] of [['music',3],['sfx',4],['vox',5]]) {
   const slider=page.locator('#settings [data-v="'+kind+'"]');
   await slider.fill(String(value));await slider.dispatchEvent('input');
  }
  await page.locator('#settings .pclose').click();
  await page.reload({waitUntil:'domcontentloaded'});
  await page.waitForFunction(()=>window.__courseReview?.ready());
  assert.deepEqual(await page.evaluate(()=>__courseReview.audio),{music:3,sfx:4,vox:5});
  await page.evaluate(()=>__courseReview.step(1));
  await page.locator('#bPlay').click();
  assert.equal(await page.evaluate(()=>__courseReview.state.mode),'play');
  await page.keyboard.down('ArrowRight');
  await page.evaluate(()=>__courseReview.step(30));await page.keyboard.up('ArrowRight');
  assert((await page.evaluate(()=>__courseReview.state.x))>220);
  const pointer={pointerId:1,pointerType:'touch',clientX:200,clientY:500};
  const beforeTouch=await page.evaluate(()=>__courseReview.state.x);
  await page.locator('#gl').dispatchEvent('pointerdown',pointer);
  await page.locator('#gl').dispatchEvent('pointermove',{...pointer,clientX:100,clientY:560});
  await page.evaluate(()=>__courseReview.step(18));
  assert((await page.evaluate(()=>__courseReview.state.x))<beforeTouch);
  await page.locator('#gl').dispatchEvent('pointerup',{...pointer,clientX:100,clientY:560});
  await page.locator('#bPause').click();
  assert.equal(await page.evaluate(()=>__courseReview.state.mode),'pause');
  await page.screenshot({path:path.join(out,'pause.png')});
  const controls=await page.locator('#pause button').allTextContents();
  assert(controls.length>=2);
  await page.locator('#bResume').click();
  assert.equal(await page.evaluate(()=>__courseReview.state.mode),'play');
  await page.locator('#bPause').click();await page.locator('#bQuit').click();
  await page.evaluate(()=>__courseReview.step(1));
  await page.locator('#bPlay').click();
  assert.equal(await page.evaluate(()=>__courseReview.state.mode),'play');
  assert.deepEqual(errors,[]);assert.deepEqual(failed,[]);
  fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({viewport:[375,812],desktop:[1280,800],snapshots,errors,failed,visualAcceptance:'Inspect PNG files; phone/Telegram hardware performance requires a device.'},null,2));
  console.log('PASS: 22 phone course renders, both desktop heroes, start/restart/steering/pause, touch steering, saved volume controls, no JS errors or local HTTP failures. Inspect course-checks PNGs.');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
