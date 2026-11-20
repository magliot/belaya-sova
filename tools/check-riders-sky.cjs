// Capture the game camera at phone size. --film also writes the review reel.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {execFileSync}=require('node:child_process');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const root='http://127.0.0.1:8766/',out='course-checks/riders-sky',film=process.argv.includes('--film');
const probe=`
const sheet=document.createElement('canvas');sheet.width=375;sheet.height=812;
const ctx=sheet.getContext('2d',{willReadFrequently:true});
function composite(){ctx.drawImage(cv,0,0,375,812);ctx.drawImage(hud,0,0,375,812);}
function skyPixels(){renderer.render(scene,camera);composite();return ctx.getImageData(0,0,375,205).data;}
function changed(a,b){let count=0;for(let i=0;i<a.length;i+=4)if(Math.abs(a[i]-b[i])+Math.abs(a[i+1]-b[i+1])+Math.abs(a[i+2]-b[i+2])>32)count++;return count;}
window.rideTest={
  start(d,x){G.mode='pause';reviewX=x;reviewSeek(d);G.mode='play';drag={};},
  tick(ticks,photo){
    const before=new Map();
    for(let i=0;i<ticks;i++){
      if(i===ticks-1)for(const o of G.objs)if(o.type==='npc'&&o.m)before.set(o,o.m.position.clone());
      G.tx=reviewX;update(1/60);syncWorld(1/60);
    }
    render(0);composite();
    const riders=G.objs.filter(o=>o.type==='npc'&&o.vy>0&&o.rideAge>0&&o.m).map(o=>({
      board:!!o.sb,shapeCount:o.m.userData.carveShapes?.length,flex:o.m.userData.carveShapes?.[0]?.morphTargetInfluences[0],
      heading:o.bearing,alignment:Math.abs(o.bearing+Math.atan2(o.carveV,o.pace)),
      // Compare the rendered body axis with the last world-space displacement on the curved piste.
      courseError:(()=>{if(!before.has(o)||o.air)return null;
        const delta=o.m.position.clone().sub(before.get(o));delta.y=0;if(delta.lengthSq()<1e-9)return null;
        const ski=new THREE.Vector3(0,0,-1).applyQuaternion(o.m.quaternion);ski.y=0;return ski.angleTo(delta);})()
    }));
    return {riders,d:G.y/20,photo:photo&&sheet.toDataURL('image/jpeg',.92).split(',')[1]};
  },
  aurora(){
    // Hold all poses, shadows, HUD and the camera still; only advance the curtain.
    const at=t=>landscape.update(G.y/20,false,3,t);
    at(0);const a=skyPixels();at(0);const still=changed(a,skyPixels());
    at(2.4);const wave=changed(a,skyPixels());at(G.t);return {still,wave};
  },
  sparks(){
    const active=shells.filter(s=>s.visible);
    const a=skyPixels();for(const s of active)s.visible=false;
    const exposed=changed(a,skyPixels());for(const s of active)s.visible=true;
    return {count:active.length,exposed};
  }
};`;
(async()=>{
  fs.mkdirSync(out,{recursive:true});let frame=0,maxSparks=0;const riders=[],errors=[];
  const browser=await chromium.launch({args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  try{
    const page=await browser.newPage({viewport:{width:375,height:812},deviceScaleFactor:1,hasTouch:true});
    page.on('pageerror',e=>errors.push(e.message));
    page.on('console',m=>{if(/Shader Error|INVALID_OPERATION/.test(m.text()))errors.push(m.text());});
    const html=fs.readFileSync('docs/index.html','utf8').replace('function frame(now){','function frame(now){if(window.holdFrame)return;');
    const cut=html.lastIndexOf('</script>');
    await page.route('**/riders-test.html*',r=>r.fulfill({contentType:'text/html; charset=utf-8',body:html.slice(0,cut)+probe+html.slice(cut)}));
    await page.route('**/vendor/telegram-web-app.js',r=>r.fulfill({body:''}));
    await page.goto(root+'riders-test.html?preview=course&seed=1911');
    await page.waitForFunction(()=>window.__g?.()?.mode==='pause'&&window.rideTest,null,{timeout:90000});
    await page.evaluate(()=>window.holdFrame=true);
    for(const [d,x,frames,ticks] of [[1,253,36,3],[54,125,34,3],[32,148,31,3],[2472,200,25,3],[15105,200,52,4],[17525,170,60,4]]){
      await page.evaluate(([d,x])=>rideTest.start(d,x),[d,x]);
      if(d===15105){const sky=await page.evaluate(()=>rideTest.aurora());console.log('aurora',sky);assert(sky.wave>350&&sky.still===0);}
      for(let i=0;i<frames;i++){
        const v=await page.evaluate(([ticks,film])=>rideTest.tick(ticks,film),[ticks,film]);riders.push(...v.riders);
        if(d===17525&&i%6===0){const sparks=await page.evaluate(()=>rideTest.sparks());assert(sparks.count<=3);maxSparks=Math.max(maxSparks,sparks.exposed);}
        if(film){fs.writeFileSync(path.join(out,'frame-'+String(frame++).padStart(4,'0')+'.jpg'),Buffer.from(v.photo,'base64'));}
      }
    }
    for(const board of [false,true]){
      const group=riders.filter(v=>v.board===board);
      assert(group.length&&group.every(v=>v.shapeCount>0&&v.alignment<1e-5));
      assert(Math.max(...group.map(v=>v.flex))-Math.min(...group.map(v=>v.flex))>.10);
      const worst=Math.max(...group.map(v=>v.courseError||0));console.log(board?'board':'skis','max ground heading difference',worst*180/Math.PI);assert(worst<.38);
      assert(group.some(v=>v.heading>.08)&&group.some(v=>v.heading<-.08));
    }
    assert(maxSparks>300,'visible firework pixels '+maxSparks);assert.deepEqual(errors,[]);
    if(film)execFileSync('ffmpeg',['-y','-hide_banner','-loglevel','error','-framerate','15','-i',out+'/frame-%04d.jpg','-frames:v',String(frame),'-vf','pad=ceil(iw/2)*2:ceil(ih/2)*2','-c:v','libx264','-pix_fmt','yuv420p','-crf','20','-movflags','+faststart','docs/review/riders-and-sky.mp4']);
    console.log('PASS both rider types steer and flex; visible firework pixels',maxSparks,'frames',frame);
  }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
