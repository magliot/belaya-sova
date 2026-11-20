// Play the same training fork twice with actual browser touch events.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {chromium} = require(process.env.DANCE_BROWSER_MODULE || 'playwright');
const url = new URL(process.argv[2] || 'http://127.0.0.1:8784/progression-test/');
assert(url.pathname.endsWith('/progression-test/'),'Use the separate progression build');
url.searchParams.set('seed','1911');
const out = path.resolve('course-checks','progression-lines');
fs.mkdirSync(out,{recursive:true});

async function check() {
  const browser = await chromium.launch({
    ...(process.env.CHROME_CHANNEL && {channel:process.env.CHROME_CHANNEL}),
    args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']
  });
  try {
    const page = await browser.newPage({viewport:{width:390,height:780},hasTouch:true});
    const failures=[];
    page.on('pageerror',e=>failures.push(e.message));
    await page.addInitScript(()=>{
      let dispatch;
      Object.defineProperty(window,'onMountainState',{
        get:()=>dispatch,
        set(fn){dispatch=state=>{window.trackFrame=state;fn(state);};}
      });
    });
    await page.goto(url.href,{waitUntil:'commit'});
    await page.locator('#play').waitFor({timeout:210000});
    await page.locator('[data-panel="settings"]').tap();
    await page.locator('#panel').getByRole('button',{name:'Осмотр трассы',exact:true}).tap();
    await page.waitForFunction(()=>window.trackFrame?.phase==='review');
    async function seek(metres) {
      await page.locator('#reviewAt').fill(String(metres));
      await page.locator('#reviewAt').press('Enter');
      await page.waitForFunction(m=>trackFrame.metres===m,metres);
    }
    await seek(300);
    await page.locator('#reviewFold').tap();
    await page.screenshot({path:path.join(out,'training-fork.png')});
    await page.locator('#reviewFold').tap();
    const touch=await page.context().newCDPSession(page);
    const rows=[];
    for(const [line,sign] of [['wide',1],['precision',-1]]) {
      await seek(312);
      await page.locator('#reviewPlay').tap();
      await page.waitForFunction(()=>trackFrame.phase==='play');
      const x0=195,y=530;
      await touch.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:x0,y}]});
      const smooth=t=>{t=Math.max(0,Math.min(1,t));return t*t*(3-2*t);};
      const deadline=Date.now()+65000;
      let state;
      do {
        state=await page.evaluate(()=>window.trackFrame);
        if(state.phase!=='play'||state.metres>=371)break;
        const t=(state.metres+4-312.5)/62.5;
        const lane=sign*3.55*smooth(t/.34)*smooth((1-t)/.28);
        await touch.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x:x0+390*lane/(27*.79),y}]});
        await page.waitForTimeout(30);
      } while(Date.now()<deadline);
      await touch.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
      assert.equal(state.phase,'play',line+' ended at '+state.metres+' m: '+state.message);
      assert(state.metres>=371,line+' stalled at '+state.metres);
      assert.equal(state.seed,1911);
      assert.equal(state.helmets,1,line+' lost helmet');
      assert.equal(state.gates,1,line+' counted wrong number of gates');
      assert.equal(state.preciseGates,line==='precision'?1:0);
      rows.push({line,metres:state.metres,fruit:state.coins,gatePoints:state.scoreGates});
      await page.locator('#pause').tap();
      await page.locator('#exitPaused').tap();
      await page.waitForFunction(()=>trackFrame.phase==='review');
    }
    assert(rows[1].fruit>rows[0].fruit&&rows[1].gatePoints>rows[0].gatePoints);
    assert.deepEqual(failures,[]);
    fs.writeFileSync(path.join(out,'training-fork.json'),JSON.stringify(rows,null,2));
    console.log('WEB TRAINING LINES PASS',JSON.stringify(rows));
  } finally { await browser.close(); }
}
check().catch(error=>{console.error(error);process.exitCode=1;});
