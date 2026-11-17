const {chromium}=require('playwright');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const out=path.resolve('art/animation-pilot');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.PILOT_CHROMIUM||(fs.existsSync('/usr/bin/chromium')?'/usr/bin/chromium':undefined),args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try{
  const page=await browser.newPage({viewport:{width:375,height:812},deviceScaleFactor:1});const errors=[];
  page.on('pageerror',e=>errors.push(String(e)));
  await page.goto('http://127.0.0.1:8770/art/animation-pilot/viewer.html');
  await page.waitForFunction(()=>window.pilot?.ready,null,{timeout:60000});await page.evaluate(()=>pilot.freeze());
  const report=await page.evaluate(()=>{
   const frames=[];const rigs=[];pilot.model.traverse(o=>{if(o.isSkinnedMesh)rigs.push(o.skeleton.bones.length)});
   for(let i=0;i<=180;i++){pilot.pose(i/30);frames.push({t:i/30,feet:pilot.measure()});}
   return {frames,rigs,clips:pilot.clips.map(c=>({name:c.name,duration:c.duration,tracks:c.tracks.length})),finite:pilot.clips.every(c=>c.tracks.every(t=>Array.from(t.values).every(Number.isFinite)))};
  });
  assert(report.clips.length>0);assert(report.finite);assert.deepEqual(errors,[]);
  const base=Object.values(report.frames[0].feet);assert.equal(base.length,2);
  let drift=0;for(const f of report.frames)Object.values(f.feet).forEach((p,i)=>{drift=Math.max(drift,Math.abs(p[0]-base[i][0]),Math.abs(p[2]-base[i][2]));});assert(drift<1e-4,`Feet drift ${drift}`);
  for(const view of ['three','front','side','back'])for(const t of [0,1.5,3.05,3.7,4.23]){
   await page.evaluate(({view,t})=>{pilot.view(view);pilot.pose(t)},{view,t});
   await page.screenshot({path:path.join(out,`phone-${view}-${t}.png`)});
  }
  fs.writeFileSync(path.join(out,'browser-check.json'),JSON.stringify({...report,errors,maxHorizontalFootDrift:drift,viewport:[375,812]},null,2));
  await page.setViewportSize({width:720,height:900});await page.evaluate(()=>pilot.view('three'));
  fs.mkdirSync(path.join(out,'frames'),{recursive:true});
  for(let f=0;f<180;f++){
   await page.evaluate(t=>pilot.pose(t),f/30);
   await page.screenshot({path:path.join(out,'frames',String(f).padStart(4,'0')+'.png')});
  }
  console.log('PASS',JSON.stringify({clips:report.clips,rigs:report.rigs,drift,errors}));
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
