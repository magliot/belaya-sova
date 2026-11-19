// Run against docs served on port 8766. --film saves excerpts from the same uninterrupted ride.
const fs=require('node:fs');
const path=require('node:path');
const assert=require('node:assert/strict');
const {execFileSync}=require('node:child_process');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const out=path.resolve('course-checks/ordinary');
const root='http://127.0.0.1:8766/';
const film=process.argv.includes('--film');
const html=fs.readFileSync('docs/index.html','utf8').replace('function frame(now){','function frame(now){if(window.fixedClock)return;');
const probe=`
const seenPeople=new Map(),seenSigns=new Map(),arches=new Map(),sampledTrees=new Set();
const spans=[[0,22],[52,83],[2424,2461],[2481,2533],[4928,4959],[7429,7459]];
let treeCount=0,maxTreeGap=-Infinity,lowestClearance=Infinity,deepestRoot=0,forestGates=0,blockedGates=0,frameCount=0;
const sheet=document.createElement('canvas');sheet.width=375;sheet.height=812;
function observe(){
  camera.updateMatrixWorld();scene.updateMatrixWorld();
  const d=G.y/20,v=new THREE.Vector3();
  for(const o of G.objs){
    if(o.dead||!o.m)continue;
    const ahead=o.y/20-d;
    if(o.type==='npc'&&d<112&&Math.abs(ahead)<68){
      const key=o.lessonStart+'-'+o.vy+'-'+!!o.kid+'-'+!!o.instr;
      if(!seenPeople.has(key))seenPeople.set(key,{kind:o.sb?'board':'ski',kid:!!o.kid,instructor:!!o.instr,stand:!!o.stand,speed:o.vy,y0:o.y/20,maxY:o.y/20,near:null,far:null,pass:null});
      const a=seenPeople.get(key);a.maxY=Math.max(a.maxY,o.y/20);
      const pos=o.m.getWorldPosition(v);pos.y+=.55;pos.project(camera);
      if(Math.abs(pos.x)<.91&&Math.abs(pos.y)<.90&&pos.z<1&&ahead>0&&ahead<25){a.far??=d;a.near=d;}
      if(ahead<0)a.pass??=d;
    }
    if(o.type==='arch'&&Math.abs(ahead)<100)arches.set(o.m.uuid+'-'+o.y,o.y/20);
    if(o.type==='entrySign'&&ahead>0&&ahead<70){
      const face=o.m.getObjectByName('entryFace'),bounds=[[-3.28,0],[3.28,0],[0,-.68],[0,.68]].map(([x,y])=>face.localToWorld(new THREE.Vector3(x,y,0)).project(camera));
      const px=Math.abs(bounds[2].y-bounds[3].y)*406;
      if(bounds.every(p=>p.z>0&&p.z<1&&Math.abs(p.x)<.98&&Math.abs(p.y)<.91)&&px>=16){
        let a=seenSigns.get(o.caption);
        if(!a){a={label:o.caption,at:o.y/20,first:d,last:d,firstTime:G.t,lastTime:G.t,maxPx:px};seenSigns.set(o.caption,a);}
        a.last=d;a.lastTime=G.t;a.maxPx=Math.max(px,a.maxPx);
      }
    }
    if(d>2500&&d<4900&&ahead>0&&ahead<24){
      if(o.type==='tree'&&!sampledTrees.has(o)){
        sampledTrees.add(o);treeCount++;lowestClearance=Math.min(lowestClearance,Math.abs(o.x-200)*U-o.r*o.s*U);
        // Measure the actual mesh bounds against the rendered snow, including its tessellation.
        const bounds=new THREE.Box3().setFromObject(o.m),origin=o.m.position.clone();origin.y+=5;
        const ray=new THREE.Raycaster(origin,new THREE.Vector3(0,-1,0));
        const ground=ray.intersectObject(piste,false)[0]||ray.intersectObject(powder,false)[0];
        if(ground){const contact=bounds.min.y-ground.point.y;maxTreeGap=Math.max(contact,maxTreeGap);deepestRoot=Math.min(deepestRoot,contact);}
      }
      if(o.type==='gate'&&!sampledTrees.has(o)){
        sampledTrees.add(o);forestGates++;
        const blocked=G.objs.some(t=>t.type==='tree'&&Math.abs(t.y-o.y)<140&&Math.abs(t.x-o.x)<o.gap/2+t.r*t.s);
        if(blocked)blockedGates++;
      }
    }
  }
}
window.ordinary={
  ready:()=>!modalLoad&&!!HEROMODELS[hero].glb,
  mode:()=>({hero,seed:G.seed,mode:G.mode,bonus:!!G.bonusRide,sprint:!!G.sprint,review:COURSE_REVIEW}),
  advance(stop,record){
    const photos=[];drag={};
    while(G.y/20<stop&&G.mode==='play'){
      const d=G.y/20;
      const next=G.objs.find(o=>!o.dead&&o.y>G.y-30&&o.y<G.y+420&&['tube','cat','snowman'].includes(o.type));
      G.tx=next?200-Math.sign(next.x-200)*48:200;
      update(1/60);frameCount++;
      const closeLook=spans.some(([a,b])=>d>=a&&d<b);
      const filming=record&&closeLook;
      if(filming&&frameCount%4===0){
        render(1/60);observe();
        const ctx=sheet.getContext('2d');ctx.drawImage(cv,0,0,375,812);ctx.drawImage(hud,0,0,375,812);
        photos.push({d,jpeg:sheet.toDataURL('image/jpeg',.90).split(',')[1]});
      }else if(closeLook||frameCount%12===0){
        syncWorld(closeLook?1/60:.2);
        if(frameCount%4===0)observe();
      }
    }
    return {d:G.y/20,mode:G.mode,fail:G.failKind,photos};
  },
  report:()=>({people:[...seenPeople.values()],signs:[...seenSigns.values()],arches:[...arches.values()],
    treeCount,lowestClearance,maxTreeGap,deepestRoot,forestGates,blockedGates})
};`;
(async()=>{
  fs.mkdirSync(out,{recursive:true});
  const browser=await chromium.launch({args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  const reports=[],errors=[],village=[];
  try{
    for(const [hero,seed,end] of [['bear',1911,7512],['owl',1911,2540],['bear',1927,2540],['owl',1927,2540]]){
      const page=await browser.newPage({viewport:{width:375,height:812},deviceScaleFactor:1,hasTouch:true});
      page.on('pageerror',e=>errors.push(e.message));
      page.on('request',r=>{if(/village-51c/i.test(r.url()))village.push(r.url());});
      await page.addInitScript(()=>window.fixedClock=true);
      await page.route('**/vendor/telegram-web-app.js',r=>r.fulfill({body:''}));
      const cut=html.lastIndexOf('</script>');
      await page.route(root+'?seed='+seed,r=>r.fulfill({contentType:'text/html; charset=utf-8',body:html.slice(0,cut)+probe+html.slice(cut)}));
      await page.goto(root+'?seed='+seed,{waitUntil:'domcontentloaded'});
      await page.waitForFunction(()=>window.ordinary?.ready());
      await page.locator('[data-h="'+hero+'"]').click();await page.waitForFunction(()=>ordinary.ready());
      await page.locator('#bPlay').click();await page.waitForFunction(()=>__g().mode==='play',null,{timeout:90000});
      assert.deepEqual(await page.evaluate(()=>ordinary.mode()),{hero,seed,mode:'play',bonus:false,sprint:false,review:false});
      let i=0;
      for(const stop of [115,2420,2540,4924,5010,7426,7512].filter(d=>d<=end)){
        const recording=film&&end===7512;
        const {photos,...state}=await page.evaluate(([d,record])=>ordinary.advance(d,record),[stop,recording]);
        assert(state.mode==='play'&&state.d>=stop,JSON.stringify({seed,hero,...state}));
        for(const {d,jpeg} of photos){
          fs.writeFileSync(path.join(out,'frame-'+String(i++).padStart(4,'0')+'.jpg'),Buffer.from(jpeg,'base64'));
          for(const mark of [5,61,73,85,2440,2490,2510,4941,7440]){
            if(Math.abs(d-mark)<.55)fs.writeFileSync(path.join(out,'at-'+mark+'.jpg'),Buffer.from(jpeg,'base64'));
          }
        }
      }
      const r={hero,seed,end,...await page.evaluate(()=>ordinary.report())};reports.push(r);
      fs.writeFileSync(path.join(out,'report.json'),JSON.stringify(reports,null,2));
      assert(r.people.some(p=>p.kind==='board'&&p.speed>0&&p.maxY-p.y0>1&&p.near!==null&&p.pass!==null));
      assert(r.people.some(p=>p.kind==='ski'&&p.kid&&p.maxY-p.y0>1&&p.near!==null&&p.pass!==null));
      assert(r.people.some(p=>p.stand&&p.instructor&&p.maxY===p.y0&&p.near!==null));
      const stages=end>7000?3:1;
      assert.deepEqual(r.arches,Array.from({length:stages},(_,n)=>(n+1)*2500));
      assert(r.signs.length===stages&&r.signs.every(s=>s.lastTime-s.firstTime>.60&&s.maxPx>21),JSON.stringify(r.signs));
      assert(r.treeCount>=3&&r.lowestClearance>3.7&&r.maxTreeGap<.15&&Number.isFinite(r.maxTreeGap),JSON.stringify(r));
      assert.equal(r.blockedGates,0);
      if(end>7000)assert(r.forestGates>0);
      console.log('PASS',hero,seed,end,'m, signs:',r.signs.map(s=>[s.label,s.first.toFixed(1),s.last.toFixed(1)]));
      if(i)execFileSync('ffmpeg',['-y','-hide_banner','-loglevel','error','-framerate','15','-i',path.join(out,'frame-%04d.jpg'),'-frames:v',String(i),'-vf','pad=ceil(iw/2)*2:ceil(ih/2)*2','-c:v','libx264','-pix_fmt','yuv420p','-crf','20','-movflags','+faststart','docs/review/ordinary-run-1911.mp4']);
      await page.close();
    }
    assert.deepEqual(errors,[]);assert.deepEqual(village,[]);
  }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
