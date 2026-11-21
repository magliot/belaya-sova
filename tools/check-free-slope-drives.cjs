// Start from the normal menu. The only inputs during a ride are finger coordinates.
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const assert = require('node:assert/strict');
const {execFileSync} = require('node:child_process');
const {chromium} = require(process.env.DANCE_BROWSER_MODULE || 'playwright');

const url = new URL(process.argv[2] || 'http://127.0.0.1:8784/progression-test/');
const output = path.resolve(process.env.SLOPE_EVIDENCE || 'handoff/free-slope');
const warmup = process.argv.includes('--approach-only');
const width = 390, height = 780;

// Meter and lateral touch target. These are recorded maneuvers through the previous
// school groups, on the way to the part of the run under test.
const rides = {
  1911: {gate: -1.428, next: 1.171, inputs: [[0,0],[15,1],[19,3.25],[55,4.5],[60,5.5],[64,6.5],[69,-2.25],[111,-3.25],[131,4],[177,2.75],[185,1.75],[197,.75],[201,4.25],[222,3.25],[245,0],[268,-1],[273,-2.5],[278,-4]]},
  19: {gate: -1.652, next: 1.355, inputs: [[0,0],[21,3.25],[57,4.5],[64,6],[71,4.5],[108,3.5],[137,2],[143,-2.5],[238,-1.5],[245,0],[271,-2.25],[278,-3.5]]},
  783: {gate: 1.712, next: -1.404, inputs: [[0,0],[12,-3.75],[51,-5],[64,-6.75],[70,-4],[83,-2.75],[96,-3.75],[123,-2.5],[193,-3.75],[244,0],[268,-1.5],[274,-2.75],[280,-4.25]]}
};

function steerAt(ride, at, bypass) {
  if (at < 285) return ride.inputs.findLast(([m]) => at >= m)[1];
  const towardSkier = Math.sign(ride.gate);
  const pass = ride.gate - towardSkier * (bypass ? 5.7 : 1.55);
  // Approach both rides alike, then make the decision while the skier is in view.
  const points = [[285, ride.inputs.at(-1)[1]],[300,ride.gate],[315,ride.gate],
    [336,pass],[355,pass],[378,ride.next],[407,ride.next]];
  for (let i=1; i<points.length; i++) {
    const [finish,to] = points[i], [start,from] = points[i-1];
    if (at <= finish) {
      const t = Math.max(0,(at-start)/(finish-start));
      return from + (to-from)*t*t*(3-2*t);
    }
  }
  return ride.next;
}

// Preserve wall-clock timing of CDP frames, including slow frames on the test machine.
function saveMovie(frames, filename) {
  assert(frames.length > 12,'No usable browser recording');
  const folder = fs.mkdtempSync(path.join(os.tmpdir(),'owl-free-clip-'));
  const paths = [];
  try {
    const manifest = ['ffconcat version 1.0'];
    for (let i=0;i<frames.length;i++) {
      const basename = String(i).padStart(5,'0')+'.jpg';
      const p=path.join(folder,basename);paths.push(p);fs.writeFileSync(p, frames[i].bytes);
      manifest.push('file '+basename);
      if (i+1<frames.length) manifest.push('duration '+Math.max(.001,frames[i+1].t-frames[i].t).toFixed(4));
    }
    const list=path.join(folder,'frames.txt');paths.push(list);fs.writeFileSync(list,manifest.join('\n')+'\n');
    execFileSync('ffmpeg',['-y','-loglevel','error','-f','concat','-safe','1','-i',list,
      '-vf','fps=30,pad=ceil(iw/2)*2:ceil(ih/2)*2','-c:v','libx264','-crf','21',
      '-pix_fmt','yuv420p','-movflags','+faststart',filename],{windowsHide:true});
  } finally {
    for(const p of paths)fs.unlinkSync(p);
    fs.rmdirSync(folder);
  }
}

async function checkRide(browser, seed, hero, bypass, film) {
  const route=rides[seed], name=hero+'-'+seed+'-'+(bypass?'bypass':'gate');
  const context=await browser.newContext({viewport:{width,height},hasTouch:true});
  try {
    const page=await context.newPage();
    await page.addInitScript(()=>{
      const samples=[];let forward;
      window.rideSamples=samples;
      Object.defineProperty(window,'onMountainState',{get:()=>forward,set(fn){
        forward=s=>{
          window.rideFrame=s;
          if(s.phase==='play'||s.phase==='over')samples.push({
            d:s.metres,lane:s.riderLane,g:s.gates,h:s.helmets,f:s.coins,p:s.scoreGates,fisPoints:s.scoreFis,
            cue:s.cue,evt:s.eventId,phase:s.phase,seconds:s.elapsed});
          fn(s);
        };
      }});
    });
    const link=new URL(url);link.searchParams.set('seed',String(seed));
    await page.goto(link.href,{waitUntil:'commit'});
    await page.locator('#play').waitFor({timeout:180000});
    assert.equal(await page.evaluate(()=>mountainRelease.channel),'progression');
    await page.locator('[data-hero="'+hero+'"]').tap();
    const cdp=await context.newCDPSession(page), frames=[];
    cdp.on('Page.screencastFrame',event=>{
      frames.push({t:event.metadata.timestamp,bytes:Buffer.from(event.data,'base64')});
      cdp.send('Page.screencastFrameAck',{sessionId:event.sessionId}).catch(()=>{});
    });
    const touch = (type,x)=>cdp.send('Input.dispatchTouchEvent',{type,touchPoints:x==null?[]:[{x,y:550}]});
    await page.locator('#play').tap();
    await page.waitForFunction(()=>window.rideFrame?.phase==='play');
    await touch('touchStart',width/2);
    let recording=false, lastX, s;
    const deadline=Date.now()+140000;
    for(;;) {
      s=await page.evaluate(()=>({phase:rideFrame.phase,d:rideFrame.metres,lane:rideFrame.riderLane,
        msg:rideFrame.message,gates:rideFrame.gates,hero:rideFrame.hero,mode:rideFrame.mode}));
      if (s.phase!=='play'||s.d>=(warmup?288:410))break;
      assert(Date.now()<deadline,name+' stalled at '+s.d);
      if(film&&!recording&&s.d>=290){
        await cdp.send('Page.startScreencast',{format:'jpeg',quality:91,maxWidth:width,maxHeight:height,everyNthFrame:1});
        recording=true;
      }
      const x=Math.round(width/2+width*steerAt(route,s.d,bypass)/(27*.79));
      if(x!==lastX){await touch('touchMove',x);lastX=x;}
      await page.waitForTimeout(20);
    }
    if(recording)await cdp.send('Page.stopScreencast');
    await touch('touchEnd');
    console.log(name,s.phase,Math.round(s.d),s.msg);
    assert.equal(s.phase,'play',name+' naturally fell at '+s.d+' m, lane '+s.lane+' · '+s.msg);
    assert.equal(s.hero,hero);assert.equal(s.mode,'normal');
    const samples = await page.evaluate(()=>window.rideSamples);
    assert(samples[0].d<.1,'Race started away from the common start');
    if(warmup)return;
    const at=m=>samples.find(row=>row.d>=m);
    const before=at(305),after=at(355),next=at(405);
    assert(before&&after&&next);
    const delta=after.g-before.g, back=next.g-after.g;
    const gateOffset=Math.abs(at(347.5).lane-route.gate);
    assert(bypass?gateOffset>3.1+.65:gateOffset<3.1-.45,name+' did not clear the post');
    assert.equal(delta,bypass?0:1,name+' gate count at 347.5 m');
    assert.equal(back,1,name+' missed the following gate at 400 m');
    assert.equal(after.p-before.p,8*delta);assert.equal(next.p-after.p,8*back);
    assert(samples.filter(row=>row.d>=305&&row.d<=405).every(row=>row.h>=before.h),'Helmet lost');
    const result={seed,hero,line:bypass?'bypass':'gate',startedAt:samples[0].d,
      gate347:delta,gate400:back,gatePoints347:after.p-before.p,gatePoints400:next.p-after.p,
      at347:at(347.5),at375:at(375),at400:at(400),phase:s.phase,
      wasm:await page.evaluate(()=>performance.getEntriesByType('resource').find(r=>r.name.includes('.wasm.'))?.name.split('/').pop())};
    assert(result.wasm && result.wasm.endsWith('.wasm.unityweb'),'Missing compiled player identity');
    if(film){
      const file=(bypass?'bypass':'gate')+'-1911.mp4';
      saveMovie(frames,path.join(output,file));result.clip=file;
    }
    console.log('PASS',JSON.stringify(result));
    return result;
  } finally {await context.close();}
}

async function main(){
  if(!warmup)fs.mkdirSync(output,{recursive:true});
  const browser=await chromium.launch({args:['--enable-unsafe-swiftshader']});
  try {
    const plan=[[1911,'bear',false,true],[1911,'bear',true,true],[1911,'owl',true,false],
      [19,'bear',true,false],[19,'owl',false,false],[783,'bear',false,false],[783,'owl',true,false]];
    const cases=(process.env.SLOPE_CASES||'').split(',').filter(Boolean);
    const selected=plan.filter((_,i)=>!cases.length||cases.includes(String(i+1)));
    // Give each movie the whole renderer. Independent unrecorded controls can run in pairs.
    const rows=[];
    for(const args of selected.filter(args=>args[3]))rows.push(await checkRide(browser,...args));
    const rest=selected.filter(args=>!args[3]);
    for(let i=0;i<rest.length;i+=2)
      rows.push(...await Promise.all(rest.slice(i,i+2).map(args=>checkRide(browser,...args))));
    if(!warmup&&rows.length){
      assert(rows.every(r=>r.wasm===rows[0].wasm),'Player changed during the check');
      const report=cases.length?'rides-'+cases.join('-')+'.json':'rides.json';
      fs.writeFileSync(path.join(output,report),JSON.stringify(rows,null,2)+'\n');
    }
  } finally {await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
