// Play the first festival gate and both bonus courses through the mobile Web UI.
const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const root=new URL(process.argv[2]||'http://127.0.0.1:8798/');
root.searchParams.set('startapp','festival_1911');
const evidence=process.env.WINTER_EVIDENCE_DIR||path.resolve('course-checks/winter-22h');

async function main(){
  fs.mkdirSync(evidence,{recursive:true});
  const browser=await chromium.launch({executablePath:process.env.CHROME_EXECUTABLE||undefined});
  try{
    const page=await browser.newPage({viewport:{width:390,height:780},hasTouch:true});
    const errors=[],results=[];
    page.on('pageerror',e=>errors.push(e.message));
    page.on('console',m=>{if(/(?:NullReference|IndexOutOfRange|InvalidOperation|MissingReference)Exception:|Shader error/.test(m.text()))errors.push(m.text());});
    await page.addInitScript(()=>{
      let draw;Object.defineProperty(window,'onMountainState',{configurable:true,
        get:()=>draw,set:fn=>{draw=s=>{window.probe=s;fn(s);};}});
    });
    await page.goto(root.href,{waitUntil:'commit'});
    const wait=(fn,arg)=>page.waitForFunction(fn,arg,{timeout:90000});
    const tap=id=>page.locator('#'+id).tap();
    const shot=name=>page.screenshot({path:path.join(evidence,name+'.jpg'),type:'jpeg',quality:88});
    const setLane=x=>page.locator('#reviewLane').evaluate((e,x)=>{e.value=x;e.dispatchEvent(new Event('input',{bubbles:true}));},x);
    async function seek(d){
      await page.locator('#reviewAt').fill(String(d));await page.locator('#reviewAt').press('Enter');
      await wait(d=>probe.phase==='review'&&probe.metres===d,d);
    }
    async function back(){await tap('pause');await tap('exitPaused');await wait(()=>probe.phase==='review');}
    await wait(()=>window.probe?.phase==='review'&&probe.metres===28000);
    assert.equal(await page.evaluate(()=>window.mountainRelease.channel),'test');
    const touch=await page.context().newCDPSession(page);
    const finger={x:195,y:510,id:1};
    const down=()=>touch.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[finger]});
    const up=()=>touch.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
    for(const [name,x,helmets,gates] of [['clean',3.5,1,1],['left-post',0,0,0],['right-post',7,0,0],['outside',-3,1,0]]){
      await setLane(x);await seek(28010);await tap('reviewPlay');await down();
      if(name==='clean')await shot('festival-approach');
      await wait(()=>probe.phase==='play'&&probe.metres>=28034);
      const s=await page.evaluate(()=>probe);
      assert.equal(s.helmets,helmets,name);assert.equal(s.gates,gates,name);assert.equal(s.scoreGates,gates*8,name);
      results.push({name,helmets:s.helmets,gatePoints:s.scoreGates});
      await up();await back();
    }
    for(const mode of ['tubing','sled']){
      await page.locator('#reviewRide').selectOption(mode);await setLane(0);await seek(0);
      await tap('reviewPlay');await down();
      await wait(()=>probe.metres>=35);await shot(mode);
      for(const x of [136,249,186,220]){
        await touch.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{...finger,x}]});
        const next=await page.evaluate(()=>probe.metres+24);await wait(d=>probe.metres>=d,next);
      }
      const s=await page.evaluate(()=>probe);assert.equal(s.phase,'play');assert.equal(s.courseMode,mode);
      assert(s.coins>0&&s.speed>=80,mode+' fruit and speed');
      results.push({mode,metres:Math.floor(s.metres),speedKmh:Math.round(s.speed),fruit:s.coins,snowmen:s.snowmen});
      await up();await back();
    }
    assert.deepEqual(errors,[]);console.log('WINTER COURSE WEB PASS '+JSON.stringify(results));
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
