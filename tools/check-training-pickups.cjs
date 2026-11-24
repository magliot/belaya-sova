// Drive from the normal menu in the isolated profile, using only touch input.
const assert = require('node:assert/strict');
const {chromium} = require(process.env.DANCE_BROWSER_MODULE || 'playwright');
const url = new URL(process.argv[2] || 'http://127.0.0.1:8798/unity-test/');
assert(url.pathname.endsWith('/unity-test/'));
url.searchParams.set('seed', '1911');
const width=390, height=780;
const line=[[0,0],[15,1],[19,3.25],[55,4.5],[60,5.5],[64,6.5],[69,-2.25],
  [111,-3.25],[131,4],[177,2.75],[185,1.75],[197,.75],[201,4.25],[222,3.25],[245,0]];

async function renderSize(page,mode){
  const size=await page.locator('#slope').evaluate(canvas=>{
    const gl=canvas.getContext('webgl2');
    return {css:[canvas.clientWidth,canvas.clientHeight],buffer:[gl.drawingBufferWidth,gl.drawingBufferHeight],dpr:devicePixelRatio};
  });
  const ratio=Math.min(size.dpr,1.5)*(mode==='light' ? .88 : 1);
  assert.deepEqual(size.buffer,size.css.map(n=>Math.round(n*ratio)),'actual WebGL buffer for '+mode);
  return size.buffer.join('×');
}

async function ride(browser,hero,stock){
  const ctx=await browser.newContext({viewport:{width,height},deviceScaleFactor:hero==='bear'?2:1,hasTouch:true});
  try{
    const page=await ctx.newPage();
    await page.addInitScript(({stock,hero})=>{
      localStorage.setItem('bsova_unity_test_v1',JSON.stringify({schema:1,uid:0,updated:1,data:{hero,helmets:stock}}));
      let listener;window.pickups=[];
      window.helmetPaints=0;
      document.addEventListener('DOMContentLoaded',()=>{
        new MutationObserver(records=>window.helmetPaints+=records.length)
          .observe(document.querySelector('#helmets'),{attributes:true,attributeFilter:['aria-label']});
      });
      Object.defineProperty(window,'onMountainState',{get:()=>listener,set(next){
        listener=s=>{
          next(s);
          if(s.phase==='play'||s.phase==='over')window.pickups.push({
            d:s.metres,lane:s.riderLane,h:s.helmets,coins:s.coins,p:s.phase,msg:s.message,
            icons:[...document.querySelectorAll('#helmets img')].filter(img=>img.style.opacity==='1').length});
        };
      }});
    },{stock,hero});
    await page.goto(url.href,{waitUntil:'commit'});
    await page.locator('#play').waitFor({timeout:180000});
    const mode=hero==='bear'?'light':'standard';
    const select=page.getByRole('combobox',{name:'Графика',exact:true});
    await page.locator('[data-panel="settings"]').tap();
    assert.equal(await select.inputValue(),'standard');
    await renderSize(page,'standard');
    if(mode==='light'){
      await select.selectOption('light');
      await renderSize(page,'light');
      await page.reload({waitUntil:'commit'});
      await page.locator('#play').waitFor({timeout:180000});
      await page.locator('[data-panel="settings"]').tap();
      assert.equal(await select.inputValue(),'light','device must remember the choice after reloading Unity');
    }
    await page.locator('#closePanel').tap();
    const before=await renderSize(page,mode);
    await page.locator('[data-hero="'+hero+'"]').tap();
    await page.locator('#play').tap();
    await page.waitForFunction(()=>pickups[0]?.p==='play');
    const cdp=await ctx.newCDPSession(page);
    const touch=(type,x)=>cdp.send('Input.dispatchTouchEvent',{type,touchPoints:x===undefined?[]:[{x,y:545}]});
    await touch('touchStart',width/2);
    let previousX,edge=false;
    const deadline=Date.now()+85000;
    for(;;){
      const s=await page.evaluate(()=>pickups.at(-1));
      if(s.p!=='play'||s.d>338||edge&&s.h<stock+1&&s.msg?.includes('спасла'))break;
      assert(Date.now()<deadline,'Timed out at '+s.d);
      if(s.d>=292&&!edge){
        await touch('touchEnd');await touch('touchStart',30);await touch('touchMove',width-10);edge=true;
      }else if(!edge){
        const target=line.findLast(([at])=>s.d>=at)[1],x=Math.round(width/2+width*target/(27*.79));
        if(x!==previousX){await touch('touchMove',x);previousX=x;}
      }
      await page.waitForTimeout(28);
    }
    await touch('touchEnd');
    await page.locator('#pause').tap();
    await page.locator('#paused').waitFor();
    assert.equal(await page.locator('#helmets img').count(),3,'three visible HUD slots');
    assert.equal(await select.inputValue(),mode);
    const distance=await page.locator('#metres').innerText();
    const switched=mode==='light'?'standard':'light';
    await select.selectOption(switched);
    const afterSwitch=await renderSize(page,switched);
    assert.equal(await page.locator('#metres').innerText(),distance,'changing graphics must keep the run');
    console.log(hero,distance,mode,before,'→',switched,afterSwitch);
    const samples=await page.evaluate(()=>pickups);
    const first=samples[0],approach=samples.find(s=>s.d>=280),after=samples.find(s=>s.d>=286);
    console.log(hero,'stock',stock,'approach',approach,'after',after,'end',samples.at(-1));
    assert(first.d<.1&&first.h===stock&&first.icons===stock,'ordinary equipped start');
    assert(approach&&after&&Math.abs(after.lane+.15)<1,'route must cross the actual training helmet');
    assert.equal(approach.h,stock,'prior collision or pickup');
    assert.equal(after.h,stock+1,'helmet at 284 m must add one protection');
    assert.equal(after.icons,after.h,'HUD helmet icons');
    assert(samples.some(s=>s.d>292&&s.p==='play'&&s.h===stock&&s.msg.includes('спасла')),'a hit must spend one protection');
    assert(after.coins>0,'collected fruit on the approach');
    const paints=await page.evaluate(()=>helmetPaints);
    assert(paints<=4,'helmet HUD rewrote an unchanged count');
    await page.locator('#resume').tap();
    await page.waitForFunction(d=>pickups.at(-1)?.d>d+1,samples.at(-1).d);
    console.log('PASS',hero,'helmet HUD updates',paints,'over',samples.length,'race snapshots');
  }finally{await ctx.close();}
}
(async()=>{
  const args=process.argv.includes('--software')?['--use-angle=swiftshader','--enable-unsafe-swiftshader']:[];
  const browser=await chromium.launch({args});
  try{await ride(browser,'owl',1);await ride(browser,'bear',0);}
  finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
