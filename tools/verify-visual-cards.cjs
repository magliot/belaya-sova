const fs=require('node:fs'),assert=require('node:assert/strict');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const rows=fs.readFileSync('tools/visual-stops.tsv','utf8').trim().split(/\r?\n/).map(s=>s.split('|'));
const dir='docs/review/cards';fs.mkdirSync(dir,{recursive:true});
const probe=`
window.cardShot=(id,index,d,x)=>{
  reviewX=x;reviewSeek(d);const ticks=(n)=>{G.mode='play';for(let i=0;i<n;i++){G.tx=x;update(1/60);syncWorld(1/60);}G.mode='pause';};
  if(id===55)ticks(40);
  if(id===57){for(let i=0;i<80&&!G.objs.some(o=>o.type==='ball');i++)ticks(1);}
  if(id===58){let was=false;for(let i=0;i<600;i++){ticks(1);if(was&&!G.p.air)break;was=!!G.p.air;}}
  animObjs(0);render(0);
  const codes={6:'npc',7:'npc',8:'npc',9:'npc',10:'npc',11:'npc',12:'cat',13:'snowmobile',22:'ramp',23:'rock',24:'rock',25:'cannon',26:'snowman',27:'dog',28:'mogulYeti',30:'coin',31:'thermos',32:'helmet',33:'gift',34:'magnet',35:'aid',36:'feather',37:'letter',38:'gate',39:'gate',40:'airRing',41:'fence',42:'entrySign',43:'entrySign',44:'entrySign',45:'arch',46:'finish',47:'rail',48:'ramp',49:'mogul',57:'ball',60:'tube',61:'tube'};
  const key=codes[id];
  const signature=o=>{
    if(id===6)return !o.kid&&!o.sit&&!o.sb;
    if(id===7)return o.kid&&!o.sb;if(id===8)return o.sit;
    if(id===9)return o.sb&&o.kid&&o.vi===index;
    if(id===10)return o.sb&&!o.kid&&!o.instr;if(id===11)return o.sb&&o.instr;
    if(id===12)return index?o.variant==='cat12A':!o.variant;
    if(id===23||id===24)return o.rockWide===(id===23);
    if(id===26)return o.variant==='snow26'+'ABC'[index];
    if(id===38||id===39)return o.col===id-38;
    if(id===40)return o.ringStyle===index;if(id===41)return o.fenceStyle===index;
    if(id>=42&&id<=44)return o.theme===['forest','moguls','park'][id-42];
    if(id===48)return o.park;if(id===49)return o.profile===index;
    if(id===60||id===61)return o.variant===(id===60?['tube60B','tube60C']:['tube61A','tube61C'])[index];
    return true;
  };
  let observed='';
  if(key){
    const found=G.objs.filter(o=>o.type===key&&o.m&&o.m.visible&&!o.dead&&signature(o)&&o.y>=G.y-30).sort((a,b)=>Math.abs(a.y-G.y)-Math.abs(b.y-G.y))[0];
    if(found)observed=key+' · '+Math.round(found.y/20)+' м';
  }else if(id===14){if(playerSled.visible&&playerSled.children.length)observed='player-sled';}
  else if(id===15||id===16||id===17){if(lift.visible&&towers.some(t=>t.userData.glb)&&chairs.some(c=>c.userData.glb))observed=['chair','tower16C','cable'][id-15];}
  else if(id===18){if(lamps.some(l=>l.getObjectByName('lamp18'+'ABC'[index])))observed='lamp18'+'ABC'[index];}
  else if(id>=19&&id<=21){const k='tree'+id+'ABC'[index];if(G.objs.some(o=>o.type==='tree'&&o.m?.getObjectByName(k)&&Math.abs(o.y-G.y)<500)&&kit[k]&&forestIM[(id-19)*3+index].visible&&forestIM[(id-19)*3+index].count>0)observed=k;}
  else if(id===50||id===56){if(MOG.patches.some(p=>id===50?p.mesh:p.tracks))observed=id===50?'рельеф':'отпечатки';}
  else if([52,54,62].includes(id)){const order={52:-12,54:-4,62:-9+index}[id];const layers=mountains.children.filter(o=>o.renderOrder===order&&o.visible&&o.material.map?.image&&o.material.opacity>.05);if(id===52||id===54){
      const variant='abc'[index]+'.webp';
      const seen=layers.find(o=>(o.material.map.image.currentSrc||o.material.map.image.src).endsWith(variant)&&Math.abs(o.getWorldPosition(new THREE.Vector3()).project(camera).x)<1);
      if(seen)observed=seen.material.map.image.src.split('/').pop();
    }else if(layers.length)observed='слои · '+layers.length;}
  else if(id===55){if(trackI)observed='двойной след';}
  else if(id===58){if(landingSpray.visible&&landingSpray.material.map)observed='снежная крошка';}
  else if(id===53){if(index===1?G.objs.some(o=>o.type==='sideNet'):index===0?G.objs.some(o=>o.type==='boundaryPole'):piste.geometry.attributes.color.array.some(n=>n<.5))observed='полотно';}
  else if(id===59){if(hud.width)observed='HUD';}
  else if(HEROMODELS[hero].motion&&HEROMODELS[hero].root.visible)observed=hero;
  const c=document.createElement('canvas');c.width=240;c.height=520;const ctx=c.getContext('2d');ctx.drawImage(cv,0,0,240,520);ctx.drawImage(hud,0,0,240,520);
  return {observed,image:c.toDataURL('image/webp',.81).split(',')[1]};
};`;
(async()=>{
 const browser=await chromium.launch({args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});const failures=[],errors=[];
 try{
  const page=await browser.newPage({viewport:{width:375,height:812}});page.on('pageerror',e=>errors.push(e.message));
  const html=fs.readFileSync('docs/index.html','utf8').replace('function frame(now){','function frame(now){if(window.cardFreeze)return;');
  const cut=html.lastIndexOf('</script>');await page.route('**/cards-test.html*',r=>r.fulfill({body:html.slice(0,cut)+probe+html.slice(cut),contentType:'text/html; charset=utf-8'}));
  await page.route('**/vendor/telegram-web-app.js',r=>r.fulfill({body:''}));
  const results=[];const places=[];
  for(const [n,asset,points] of rows){if(points==='—')continue;points.split(' ').forEach((pt,index)=>{const [where,mode='normal']=pt.split('@'),[d,x=200]=where.split(':').map(Number);places.push({n:+n,index,d,x,mode});});}
  const filter=process.argv.slice(2).map(Number);
  const selected=places.filter(p=>!filter.length||filter.includes(p.n));
  for(const mode of new Set(selected.map(p=>p.mode))){
    const q=mode==='normal'?'':mode==='owl'?'&hero=owl':'&ride='+mode;
    await page.goto('http://127.0.0.1:8766/cards-test.html?preview=course&seed=1911'+q);
    await page.waitForFunction(()=>window.__g?.()?.mode==='pause'&&!!window.cardShot,null,{timeout:90000});
    await page.evaluate(()=>window.cardFreeze=true);
    for(const p of selected.filter(p=>p.mode===mode)){
      const {observed,image}=await page.evaluate(p=>cardShot(p.n,p.index,p.d,p.x),p);
      if(!observed)failures.push(p);
      fs.writeFileSync(dir+'/'+String(p.n).padStart(2,'0')+'-'+p.index+'.webp',Buffer.from(image,'base64'));results.push({...p,observed});
    }
  }
  assert.equal(rows.length,62);assert(!/goldenOwl\s*\(o\)|type:\s*['"]goldenOwl/.test(html));
  fs.mkdirSync('course-checks/art-update',{recursive:true});const log='course-checks/art-update/cards.json';
  const old=filter.length&&fs.existsSync(log)?JSON.parse(fs.readFileSync(log,'utf8')).filter(p=>!filter.includes(p.n)&&places.some(place=>place.n===p.n&&place.index===p.index)):[];
  fs.writeFileSync(log,JSON.stringify([...old,...results].sort((a,b)=>a.n-b.n||a.index-b.index),null,2));
  assert.deepEqual(errors,[]);assert.deepEqual(failures,[]);console.log('PASS '+selected.length+' in-game views, 62 indexed decisions, obsolete owl absent.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
