// Film two connected excerpts of a real seeded Web ride. This test starts the normal
// mode with a held touch and observes published Unity HUD events without moving actors.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {spawnSync}=require('node:child_process');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const url=new URL(process.argv[2]||'http://127.0.0.1:8784/unity-test/');
assert(url.pathname.endsWith('/unity-test/'),'Capture requires the isolated test profile');
url.searchParams.set('seed','1911');
const work=path.resolve('course-checks/mountain-race/film-pickups');
const output=path.resolve(process.argv[3]||'handoff/unity-20i/pickups.mp4');
fs.mkdirSync(work,{recursive:true});fs.mkdirSync(path.dirname(output),{recursive:true});

async function main(){
  const browser=await chromium.launch();
  const frames=[];
  try{
    const page=await browser.newPage({viewport:{width:420,height:800},hasTouch:true});
    await page.route('https://script.google.com/**',route=>route.abort());
    await page.addInitScript(()=>{
      window.states=[];let callback;
      Object.defineProperty(window,'onMountainState',{get(){return callback;},set(next){
        callback=s=>{window.states.push({time:Date.now()/1000,phase:s.phase,metres:s.metres,coins:s.coins,
          cue:s.cue,helmets:s.helmets,score:s.score});next(s);};
      }});
    });
    await page.goto(url.href,{waitUntil:'commit'});
    await page.locator('#play').waitFor({timeout:150000});
    await page.locator('[data-hero="owl"]').tap();
    const cdp=await page.context().newCDPSession(page);
    cdp.on('Page.screencastFrame',event=>{
      const t=event.metadata.timestamp;
      frames.push({t,bytes:Buffer.from(event.data,'base64')});
      cdp.send('Page.screencastFrameAck',{sessionId:event.sessionId}).catch(()=>{});
    });
    await cdp.send('Page.startScreencast',{format:'jpeg',quality:94,maxWidth:420,maxHeight:800,everyNthFrame:1});
    await page.locator('#play').tap();
    await page.waitForFunction(()=>states.at(-1)?.phase==='play');
    await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:210,y:560}]});
    // Use the open line just inside the snowman, so both the throwing hand and
    // the descending snowball stay inside the normal narrow chase camera.
    await page.waitForFunction(()=>states.at(-1).metres>=111||states.at(-1).phase==='over',null,{timeout:60000});
    for(const x of [200,190,180,170,160]){
      await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x,y:560}]});
      await page.waitForTimeout(40);
    }
    await page.waitForFunction(()=>states.at(-1).metres>=170||states.at(-1).phase==='over',null,{timeout:60000});
    await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
    await cdp.send('Page.stopScreencast');
    const states=(await page.evaluate(()=>states)).filter(s=>s.phase==='play');
    const first=states.find(s=>s.coins>=1),hit=states.find(s=>s.cue==='snowball');
    fs.writeFileSync(path.join(work,'events.json'),JSON.stringify(states,null,2));
    assert(first,'No fruit picked up through gameplay');assert(hit,'No physical snowball contact from the first training snowman');
    assert(hit.helmets===states[0].helmets,'Snowball removed protection');
    const intervals=[[states[0].time,first.time+1.22],[hit.time-2.0,hit.time+.64]];
    let count=0;
    for(const [start,end] of intervals){
      const take=frames.filter(f=>f.t>=start&&f.t<=end);
      assert(take.length>18,'Incomplete Web video frames');
      for(let t=start;t<=end;t+=1/30){
        const chosen=take.reduce((best,f)=>Math.abs(f.t-t)<Math.abs(best.t-t)?f:best);
        fs.writeFileSync(path.join(work,String(count++).padStart(4,'0')+'.jpg'),chosen.bytes);
      }
    }
    for(const [label,event] of [['mandarin',first],['snowball',hit]]){
      for(const [suffix,delay] of [['before',-.10],['after',.14],['after-late',.27]]){
        const at=event.time+delay,closest=frames.reduce((best,f)=>Math.abs(f.t-at)<Math.abs(best.t-at)?f:best);
        fs.writeFileSync(path.join(work,label+'-'+suffix+'.jpg'),closest.bytes);
      }
    }
    const baked=spawnSync('ffmpeg',['-hide_banner','-loglevel','error','-y','-framerate','30','-start_number','0',
      '-i',path.join(work,'%04d.jpg'),'-frames:v',String(count),'-c:v','libx264','-pix_fmt','yuv420p',
      '-crf','18','-movflags','+faststart',output],{encoding:'utf8'});
    assert.equal(baked.status,0,baked.error?.message||baked.stderr);
    console.log('Actual Web ride: fruit',first.metres,'m; snowball',hit.metres,'m; protected helmets',hit.helmets,
      '; captured',count,'frames at 30 FPS. Gross capture',frames.length,'compositor frames.');
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
