// Export the summit landmarks used by Unity and the original holiday layouts kept as art references.
const fs=require('node:fs'),path=require('node:path');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const root=path.resolve('unity/OwlSlope/Assets/Resources/Race/Layouts'),base=process.argv[2]||'http://127.0.0.1:8766/';
const townArchive=path.resolve('art/course-review/legacy-town-layouts');
function generate(seed,stage){
 const originalRandom=Math.random;let state=seed+stage;
 Math.random=()=>((state=Math.imul(1664525,state)+1013904223>>>0)/4294967296);
 try{
  window.extractRows={objects:[],patches:[]};G=newGame('play');G.seed=seed;G.stage=stage;G.lastSpawnStage=stage-1;G.nextY=stage*50000;G.y=G.nextY;G.lastLaneY=G.y;
  useCourse(seed);
  while(G.nextY<(stage+1)*50000){
   G.y=G.nextY;G.speed=Math.min(230+G.y/20*.085,560)*(.8+.7*gradeD(G.y/20))*1.1;
   G.objs=G.objs.filter(o=>o.y>G.nextY-300);
   const prev=G.nextY;spawnSeg();if(!(G.nextY>prev))throw Error('stalled '+prev);
  }
  return JSON.parse(JSON.stringify(window.extractRows,(key,v)=>key==='m'||key==='lead'||key==='footprint'||key==='prints'?undefined:key==='air'?!!v:v));
 }finally{delete window.extractRows;delete window.laneExport;Math.random=originalRandom;}
}
(async()=>{
 fs.mkdirSync(root,{recursive:true});
 fs.mkdirSync(townArchive,{recursive:true});
 const b=await chromium.launch({args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try{const p=await b.newPage();await p.route('https://script.google.com/**',r=>r.abort());
  let src=require('./archived-three.cjs')('index.html');
  await p.route('**/motion-entry.js*',r=>r.fulfill({contentType:'text/javascript',body:''}));
  await p.route('**/version.txt*',r=>r.fulfill({body:require('./archived-three.cjs')('version.txt')}));
  for(const [from,to] of [
   ['G.objs.push(o);o.m=acquire(o);placeObj(o);return true;', `G.objs.push(o);if(window.extractRows){
       if(window.laneExport)Object.assign(o,laneExport);
       o.m=true;extractRows.objects.push(o);return true;
     }o.m=acquire(o);placeObj(o);return true;`],
   ["case 'lane':{const px=G.p?G.p.x:200;const x=laneDue?clamp(px+rnd(-10,10),30,370):clamp(Math.random()<0.6?px+rnd(-25,25):rnd(70,330),50,350);",
    `case 'lane':{const px=G.p?G.p.x:200,aim=laneDue||Math.random()<.6;
      const x=laneDue?clamp(px+rnd(-10,10),30,370):clamp(aim?px+rnd(-25,25):rnd(70,330),50,350);
      if(window.extractRows&&aim)window.laneExport={laneAt:y,laneBase:x,laneMargin:laneDue?30:50};`],
   ['len+=40;break;}', 'delete window.laneExport;len+=40;break;}'],
   ['const W=400*U,L=(P.y1-P.y0)*U','if(window.extractRows){extractRows.patches.push(P);return P;}const W=400*U,L=(P.y1-P.y0)*U'],
   ['function tryTrick(idx)',`window.exportLegacyRows=${generate};function tryTrick(idx)`]
  ]){if(!src.includes(from))throw Error('Source marker missing '+from);src=src.replace(from,to);}
  await p.route(base,r=>r.fulfill({contentType:'text/html',body:src}));
  await p.goto(base,{waitUntil:'domcontentloaded'});await p.waitForFunction(()=>window.exportLegacyRows&&window.__g?.()?.mode==='menu');
  for(const stage of [4,7])for(const seed of [19,1911,1927,4903]){
   const result=await p.evaluate(([s,t])=>exportLegacyRows(s,t),[seed,stage]);
   fs.writeFileSync(path.join(stage===7?townArchive:root,'stage'+stage+'-'+seed+'.json'),JSON.stringify(result));
   console.log(stage,seed,result.objects.length,result.patches.length);
  }
 }finally{await b.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
