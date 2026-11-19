const fs=require('node:fs'),assert=require('node:assert/strict');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const out='course-checks/art-update';fs.mkdirSync(out,{recursive:true});
const base='http://127.0.0.1:8766';
const hook=`
window.visualQA={seek(d,x){reviewX=x;reviewSeek(d);render(0);},list:()=>G.objs.filter(o=>o.m).map(o=>[o.type,o.variant||o.theme||'',o.x,Math.round(o.y/20)]),
  driveTraining(){
    drag={};const start=G.y/20;
    for(let i=0;i<700&&G.mode==='play'&&G.y/20<85;i++){
      G.tx=200;update(1/60);syncWorld(1/60);
    }
    render(0);drag=null;return {start,d:G.y/20,mode:G.mode,seed:G.seed};
  },
  async flight(kind,taps){
    await loadTrackAssets(kind);hero=kind;G.mode='pause';reviewX=145;reviewSeek(7650);G.mode='play';drag={};
    this.lane=G.objs.find(o=>o.type==='feather').x;G.p.x=G.tx=reviewX=this.lane;
    this.taps=taps;this.count=0;this.air=false;this.land=null;this.phases=new Set();
  },
  step(){const p=G.p;
    for(let k=0;k<4;k++){G.tx=this.lane;update(1/60);
      if(p.parkFlight?.prizeLine&&!p.trick&&this.count<this.taps){
        for(const type of ['pointerdown','pointerup'])cv.dispatchEvent(new PointerEvent(type,{pointerId:1,clientX:170,clientY:540}));
        this.count++;drag={};
      }
      if(p.rampH>.1)this.phases.add('approach');if(p.air)this.phases.add('flight');
      if(p.trick&&p.style!=null)this.phases.add('style'+p.style);
      if(this.air&&!p.air){this.land=G.t;this.phases.add('land');}
      this.air=!!p.air;
      if(k<3)syncWorld(1/60);
    }
    render(1/60);
    const sheet=this.sheet??=document.createElement('canvas');sheet.width=375;sheet.height=812;
    const ctx=sheet.getContext('2d');ctx.drawImage(cv,0,0,375,812);ctx.drawImage(hud,0,0,375,812);
    const focus=HEROMODELS[hero].root.getWorldPosition(new THREE.Vector3());focus.y+=.65;focus.project(camera);
    const fx=(focus.x+1)*cv.width/2,fy=(1-focus.y)*cv.height/2,scale=cv.width/375;
    const side=126*scale;ctx.fillStyle='#dce8f3';ctx.fillRect(81,562,213,179);
    ctx.drawImage(cv,fx-side/2,fy-side*.42,side,side*.84,83,564,209,175);
    ctx.fillStyle='rgba(10,29,55,.88)';ctx.fillRect(0,754,375,58);
    ctx.fillStyle='white';ctx.font='bold 20px system-ui';ctx.fillText((hero==='owl'?'Сова':'Мишаня')+' · '+['Обычный прыжок','Один трюк','Два трюка'][this.taps],14,780);
    ctx.font='16px system-ui';ctx.fillText((hero==='owl'?['','Indy','Indy → Melon']:['','Shifty','Shifty → Spread eagle'])[this.taps]||'Высокая линия',14,802);
    return {jpeg:sheet.toDataURL('image/jpeg',.90).split(',')[1],done:this.land!=null&&G.t-this.land>.72,phases:[...this.phases],tricks:G.tricks,d:G.y/20};
  }};
`;
(async()=>{
 const browser=await chromium.launch({args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try {
   const page=await browser.newPage({viewport:{width:375,height:812},deviceScaleFactor:1});
   const errors=[];page.on('pageerror',e=>{errors.push(e.message);console.error(e.message);});page.on('console',c=>{if(/Shader Error/.test(c.text()))errors.push(c.text());});
   const html=fs.readFileSync('docs/index.html','utf8').replace('function frame(now){','function frame(now){if(window.freezeArt)return;');
   const at=html.lastIndexOf('</script>');await page.route('**/art-test.html*',r=>r.fulfill({body:html.slice(0,at)+hook+html.slice(at),contentType:'text/html; charset=utf-8'}));
   await page.route('**/vendor/telegram-web-app.js',r=>r.fulfill({body:''}));
   await page.addInitScript(()=>localStorage.setItem('bsova_meet',JSON.stringify({prizeFeather:true})));
   await page.goto(base+'/art-test.html?seed=1911');
   await page.waitForFunction(()=>!document.querySelector('#menu').classList.contains('booting')&&!document.querySelector('#bTube').disabled&&!document.querySelector('#assetWait:modal'));
   await page.waitForFunction(()=>document.querySelector('#menubg').style.backgroundImage&&document.querySelector('#assetWait').hidden);
   for(const size of [[320,568],[375,812]]){await page.setViewportSize({width:size[0],height:size[1]});await page.waitForTimeout(480);
     for(const sel of ['[data-h="bear"]','[data-h="owl"]','#bPlay','#bTube','#bSled']){const b=await page.locator(sel).boundingBox();assert(b&&b.y>=0&&b.y+b.height<=size[1],sel+JSON.stringify(b));}}
   await page.screenshot({path:out+'/menu.png'});await page.locator('#bPlay').click();
   await page.waitForFunction(()=>window.__g?.()?.mode==='play'&&__g().y>0,null,{timeout:60000}).catch(async e=>{throw Error(e.message+' '+await page.locator('#assetWait').innerText()+' '+errors.join(' | '));});await page.evaluate(()=>window.freezeArt=true);
   if(process.argv.includes('--film')){
     const {execFileSync}=require('node:child_process');
     const ff=(args)=>execFileSync('ffmpeg',['-hide_banner','-loglevel','error','-y',...args]);
     const verification=[];
     for(const taps of [0,1,2]){
       const movies=[];
       for(const rider of ['bear','owl']){
         const dir=out+'/'+rider+taps;fs.mkdirSync(dir,{recursive:true});await page.evaluate(args=>visualQA.flight(...args),[rider,taps]);
         let done=false;
         for(let i=0;i<230;i++){
           const {jpeg,...result}=await page.evaluate(()=>visualQA.step());
           fs.writeFileSync(dir+'/'+String(i).padStart(3,'0')+'.jpg',Buffer.from(jpeg,'base64'));
           if(result.done){assert.equal(result.tricks,taps,JSON.stringify(result));assert(result.phases.includes('approach')&&result.phases.includes('flight')&&result.phases.includes('land'));verification.push({rider,taps,...result});movies.push({rider,frames:i+1,dir});done=true;break;}
         }
         assert(done,'Landing missing for '+rider+taps);
       }
       const [a,b]=movies;
       ff(['-framerate','15','-i',a.dir+'/%03d.jpg','-framerate','15','-i',b.dir+'/%03d.jpg','-filter_complex','[0:v]tpad=stop_mode=clone:stop_duration=2[a];[1:v]tpad=stop_mode=clone:stop_duration=2[b];[a][b]hstack=inputs=2,format=yuv420p','-frames:v',String(Math.max(a.frames,b.frames)),'-c:v','libx264','-crf','19',out+'/jump'+taps+'.mp4']);
     }
     fs.writeFileSync(out+'/scenes.txt',[0,1,2].map(i=>"file 'jump"+i+".mp4'").join('\n'));
     ff(['-f','concat','-safe','0','-i',out+'/scenes.txt','-c','copy','-movflags','+faststart','docs/review/jumps-1911.mp4']);
     assert.deepEqual(errors,[]);fs.writeFileSync(out+'/jumps.json',JSON.stringify(verification,null,2));console.log(verification);return;
   }
   const ride=await page.evaluate(()=>visualQA.driveTraining());
   assert(ride.start<20&&ride.mode==='play'&&ride.seed===1911&&ride.d>=85&&ride.d<86,JSON.stringify(ride));
   await page.screenshot({path:out+'/training-live.png'});
   const samples={};
   for(const [name,d,x] of [['training',85,200],['training2',105,210],['valley',525,200],['forest',2571,200],['moguls',5040,245],['park',7650,145],['aurora',15500,200]]){
     await page.evaluate(([d,x])=>visualQA.seek(d,x),[d,x]);await page.screenshot({path:out+'/'+name+'.png'});samples[name]=await page.evaluate(()=>visualQA.list());
   }
   for(const [name,d] of [['A',100],['B',490],['C',985]]){
     await page.goto(base+'/art-test.html?preview=course&ride=tubing&seed=1911&at='+d);
     await page.waitForFunction(d=>window.__g?.()?.mode==='pause'&&__g().y===d*20,d);
     await page.locator('[data-r="fold"]').click();await page.screenshot({path:out+'/tube'+name+'.png'});
   }
   assert.deepEqual(errors,[]);fs.writeFileSync(out+'/objects.json',JSON.stringify(samples,null,2));
   console.log('PASS normal menu/start, 320x568 and 375x812 controls, seven course views, three bonus locations.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
