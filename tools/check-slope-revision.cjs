// Play through a bank exit from the menu. Use the same seed and camera to inspect later stages.
const fs=require('fs'),assert=require('assert/strict');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const out='stage-checks/revision',url='http://127.0.0.1:8766/?seed=1911';
const moduleProbe=`
window.slopeQA={
  ready:()=>!!HEROMODELS[hero].glb&&!modalLoad,
  bank(){
    G.p.lives=1;keys.ArrowRight=true;let outside=false,lost=0;
    for(let i=0;i<360;i++){
      const before=G.p.lives;update(1/60);outside||=X(G.p.x)>courseWidth(G.y/20)/2;
      if(G.p.lives<before){lost++;delete keys.ArrowRight;}
      if(lost&&G.p.fall<=0)break;
    }
    return {outside,lost,lives:G.p.lives,x:G.p.x,mode:G.mode};
  },
  inspect(d){
    G.mode='pause';reviewSeek(d);render(0);
    const objs=G.objs.filter(o=>o.m&&!o.dead),types=objs.map(o=>o.type);
    const overlap=[];
    for(let i=0;i<objs.length;i++)for(const b of objs.slice(i+1)){
      const a=objs[i];for(const [x,w,l] of a.footprint||[])for(const [u,v,h] of b.footprint||[]){
        if(Math.abs(a.y-b.y)<l+h&&Math.abs(x-u)<w+v)overlap.push([a.type,b.type]);
      }
    }
    const first=t=>objs.find(o=>o.type===t);
    const aid=first('aid'),gun=first('cannon'),arch=first('arch');
    return {types,overlap,aid:aid&&objs.some(o=>o.hurt&&o.sit&&Math.hypot(o.x-aid.x,o.y-aid.y)<48),
      gun:gun&&Math.sin(gun.m.children[0].rotation.y)*gun.m.userData.cannon>.99,
      archText:arch&&arch.m.children.some(c=>c.geometry?.type==='PlaneGeometry'),
      npc:objs.filter(o=>o.type==='npc').map(o=>o.sb?'board':'skier'),
      yetis:objs.filter(o=>o.type==='mogulYeti').map(o=>[o.route,o.leaps,!!o.prints]),
      twin:lamps.some(l=>l.userData.theme===6&&l.userData.second?.every(m=>m.visible)),
      headlights:first('cat')?.m.children.filter(c=>c.isSprite&&!c.userData.beacon&&c.material.blending===THREE.AdditiveBlending).length,
      evening:G.zone===4||stageDef(G.stage).env===4,hemi:hemi.intensity};
  },
  animate(n){G.mode='play';drag={};for(let i=0;i<n;i++){update(1/60);syncWorld(1/60);}render(0);return shells.some(s=>s.visible&&s.material.opacity>.5);}
};`;
(async()=>{
  fs.mkdirSync(out,{recursive:true});
  const browser=await chromium.launch({args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  try{
    const p=await browser.newPage({viewport:{width:375,height:812},hasTouch:true});
    const errors=[];p.on('pageerror',e=>errors.push(e.message));
    const html=fs.readFileSync('docs/index.html','utf8').replace('function frame(now){','function frame(now){return;');
    const cut=html.lastIndexOf('</script>');
    await p.route(url,r=>r.fulfill({body:html.slice(0,cut)+moduleProbe+html.slice(cut),contentType:'text/html; charset=utf-8'}));
    await p.route('**/vendor/telegram-web-app.js',r=>r.fulfill({body:''}));
    await p.goto(url);await p.waitForFunction(()=>window.slopeQA?.ready());
    await p.locator('#bPlay').click();await p.waitForFunction(()=>__g().mode==='play',null,{timeout:90000});
    const bank=await p.evaluate(()=>slopeQA.bank());
    assert(bank.outside&&bank.lost===1&&bank.lives===0&&bank.mode==='play'&&Math.abs(bank.x-200)<4,JSON.stringify(bank));
    const reports={},routes=new Set();let jumps=false;
    for(const d of [70,278,2440,2555,2640,5035,5150,5360,7510,10289,10484,12504,15024,15215,17525]){
      const s=await p.evaluate(d=>slopeQA.inspect(d),d);reports[d]=s;
      assert.deepEqual(s.overlap,[],d+': '+JSON.stringify(s.overlap));
      if(d===70||d===10289)assert(s.gun,'cannon '+d);
      if(d===278||d===10484)assert(s.aid,'rescue '+d);
      if(d===2440)assert(s.types.includes('entrySign')&&s.archText===false);
      if(d===5035||d===5150)assert(!s.types.includes('sideNet')&&!s.types.includes('stageProp'));
      if(d===7510)assert(s.types.includes('ramp')&&s.npc.includes('skier')&&!s.types.includes('stageProp'));
      if(d===12504)assert(s.types.filter(t=>t==='rock').length>=4);
      if(d===15024)assert(s.twin&&s.yetis.length);
      if(d===15215)assert.equal(s.headlights,2);
      if(d===17525){assert(s.evening&&s.hemi>.8);assert(await p.evaluate(()=>slopeQA.animate(22)));}
      for(const [route,leaps,prints] of s.yetis){routes.add(route);jumps||=leaps;assert(prints);}
      if([70,2555,5035,7510,12504,15024,15215,17525].includes(d))await p.screenshot({path:out+'/'+d+'.jpg'});
    }
    assert(routes.size>=2&&jumps);assert.deepEqual(errors,[]);
    fs.writeFileSync(out+'/reports.json',JSON.stringify({bank,reports},null,2));
    console.log('PASS: bank exit, one helmet and recenter; eight stages, footprints, rescue pairs, signs, cannons, lamps and headlights.');
  }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
