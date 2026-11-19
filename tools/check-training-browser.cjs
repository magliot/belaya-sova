const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const out=path.resolve(process.env.COURSE_CHECK_OUT||'course-checks','training');
fs.mkdirSync(out,{recursive:true});
const local='http://127.0.0.1:8766';
const source=fs.readFileSync('docs/index.html','utf8').replace('function frame(now){','function frame(now){if(window.freezeRide)return;');
const at=source.lastIndexOf('</script>');
const probe=`
window.trainingCheck={
  drive(d,fps,side){
    drag={};G.p.lives=0;
    while(G.y/20<d&&G.mode==='play'){
      const tube=G.objs.find(o=>o.type==='tube'&&o.m);
      G.tx=side<0?(tube?.x??115):146;
      update(1/fps);
      syncWorld(1/fps);
    }
    animObjs(0);render(0);
    const o=G.objs.find(o=>o.type==='tube'&&o.m);
    const expected=o&&courseGround(new THREE.Vector3(),X(o.x),Z(o.y));
    return {hero,seed:G.seed,mode:G.mode,d:G.y/20,fail:G.failKind,variant:o?.variant,
      flex:o?.m?.userData.flex.length,lean:o?.lean,error:o?o.m.position.distanceTo(expected):null};
  },
  seek(d,x){reviewX=x;reviewSeek(d);$('topbtns').classList.remove('hidden');render(0);},
  finishTraining(fps){
    const costumes=new Set();drag={};
    while(G.mode==='play'&&G.y/20<2501){
      const next=G.objs.find(o=>o.y>G.y-30&&o.y<G.y+750&&['tube','cat','snowman'].includes(o.type));
      G.tx=next?200-Math.sign(next.x-200)*57:200;
      for(const o of G.objs)if(o.variant)costumes.add(o.variant);
      update(1/fps);
    }
    return {hero,d:G.y/20,mode:G.mode,stage:G.stage,fail:G.failKind,costumes:[...costumes]};
  },
  get ready(){return !modalLoad&&HEROMODELS[hero].glb;}
};`;
(async()=>{
 const browser=await chromium.launch({headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try{
  const page=await browser.newPage({viewport:{width:375,height:812},deviceScaleFactor:1,hasTouch:true});
  const errors=[],runs=[],descents=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.addInitScript(()=>window.freezeRide=true);
  await page.route('**/vendor/telegram-web-app.js',r=>r.fulfill({body:''}));
  await page.route('**/training-test.html*',r=>r.fulfill({contentType:'text/html; charset=utf-8',body:source.slice(0,at)+probe+source.slice(at)}));
  for(const hero of ['bear','owl'])for(const fps of [30,60])for(const collide of [false,true]){
    await page.goto(local+'/training-test.html?seed=1911',{waitUntil:'domcontentloaded'});
    await page.waitForFunction(()=>window.trainingCheck?.ready);
    await page.locator('[data-h="'+hero+'"]').click();
    await page.waitForFunction(()=>trainingCheck.ready);
    await page.locator('#bPlay').click();
    await page.waitForFunction(()=>__g().mode==='play',null,{timeout:60000});
    const drive=(d)=>page.evaluate(args=>trainingCheck.drive(...args),[d,fps,collide?-1:1]);
    const near=await drive(36);
    assert.equal(near.seed,1911);assert.equal(near.hero,hero);assert.equal(near.variant,'tube60C');
    assert(near.error<.002&&near.flex>0&&Math.abs(near.lean)>.01,JSON.stringify(near));
    if(fps===30&&!collide)await page.screenshot({path:path.join(out,hero+'-tube.png')});
    const run=await drive(62);runs.push({hero,fps,collide,...run});
    assert.equal(run.mode,collide?'crash':'play',JSON.stringify(run));
    assert.equal(run.fail,collide?'tube':undefined);
    if(fps===30&&!collide){
      const descent=await page.evaluate(fps=>trainingCheck.finishTraining(fps),fps);
      assert(descent.mode==='play'&&descent.stage===1,JSON.stringify(descent));
      assert.equal(descent.costumes.filter(k=>k.startsWith('snow')).length,3);
      descents.push(descent);
      for(const [name,d,x] of [['snowman',145,125],['ratrak',469,150],['forest',2571,200]]){
        await page.evaluate(args=>trainingCheck.seek(...args),[d,x]);
        await page.screenshot({path:path.join(out,hero+'-'+name+'.png')});
      }
    }
  }
  await page.goto(local+'/?preview=course&seed=1911&at=148',{waitUntil:'domcontentloaded'});
  await page.waitForFunction(()=>window.__g?.()?.mode==='pause'&&__g().y===2960);
  assert.equal(await page.evaluate(()=>__g().seed),1911);
  let attempts=0;
  await page.route('**/models/training-cast.glb',r=>++attempts===1?r.fulfill({status:503,body:''}):r.continue());
  await page.goto(local+'/',{waitUntil:'domcontentloaded'});
  await page.locator('#bPlay').click();
  await page.locator('#assetRetry').waitFor({state:'visible'});
  assert.equal(await page.evaluate(()=>__g().mode),'menu');
  await page.locator('#assetRetry').click();
  await page.waitForFunction(()=>__g().mode==='play'&&__g().y>0);
  const first=await page.evaluate(()=>__g().seed);
  await page.locator('#bPause').click();
  assert((await page.locator('#courseNumber').textContent()).includes(String(first)));
  await page.goto(local+'/',{waitUntil:'domcontentloaded'});await page.locator('#bPlay').click();
  await page.waitForFunction(()=>__g().mode==='play'&&__g().y>0);
  assert.notEqual(await page.evaluate(()=>__g().seed),first);
  assert.deepEqual(errors,[]);
  fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({viewport:[375,812],runs,descents,errors},null,2));
  console.log('PASS real play entry, 8 hit/bypass runs at 30/60 Hz, correct model positions, deep link, eight phone screenshots.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
