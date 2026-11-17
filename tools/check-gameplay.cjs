const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const out=path.resolve(process.env.GAME_CHECK_OUT||'gameplay-checks');fs.mkdirSync(out,{recursive:true});
const source=fs.readFileSync('docs/index.html','utf8');
const steering=source.slice(source.indexOf('const RIV_AVOID='),source.indexOf('function rivalTick('));
const course=source.slice(source.indexOf('function addCourse('),source.indexOf('function addGate('));
function random(seed){let state=seed;return()=>{state=(state*1664525+1013904223)>>>0;return state/4294967296;};}
function rivalGrid(){
  const rows=[];
  for(const fps of [30,60])for(const speed of [230,350,500,650,800,950])for(const difficulty of [0,0.5,1]){
    let gates=0,missed=0,minMargin=Infinity,maxAccel=0;
    for(let seed=1;seed<=12;seed++){
      const math=Object.create(Math);math.random=random(seed);
      const G={objs:[],speed:speed/1.17*1.25,lastGateX:200,gateCol:0};
      const c={G,Math:math,clamp:(v,a,b)=>Math.max(a,Math.min(b,v)),lerp:(a,b,t)=>a+(b-a)*t,
        rnd:(a,b)=>a+math.random()*(b-a),addObj:o=>G.objs.push({...o,m:{}})};
      vm.createContext(c);vm.runInContext(steering+'\n'+course,c);
      let end=Math.max(400,speed*1.4);for(let i=0;i<8;i++)end+=c.addCourse(end,difficulty)+700;
      const o={type:'npc',rival:true,y:0,x:200,bx:200,vy:speed,amp:0,vx:0,m:{}};G.objs.push(o);
      const R={o},track=G.objs.filter(q=>q.type==='gate');let next=0;
      while(o.y<end){
        const prev=o.y,vx=o.vx;c.rivalSteer(R,1/fps);o.y+=speed/fps;o.x=o.bx;
        maxAccel=Math.max(maxAccel,Math.abs(o.vx-vx)*fps);
        const gate=track[next];if(gate&&prev<gate.y&&o.y>=gate.y){
          const margin=gate.gap/2-Math.abs(o.x-gate.x);minMargin=Math.min(minMargin,margin);gates++;if(margin<=0)missed++;next++;
        }
      }
    }
    rows.push({fps,speed,difficulty,gates,missed,minMargin:+minMargin.toFixed(3),maxAccel:+maxAccel.toFixed(3)});
  }
  fs.writeFileSync(path.join(out,'rival-grid.json'),JSON.stringify(rows,null,2));
  assert(rows.every(r=>r.missed===0),'Rival misses gates on a generated course');
  assert(rows.every(r=>r.maxAccel<=620.001),'Rival changes direction abruptly');
  console.log('Rival:',rows.reduce((n,r)=>n+r.gates,0),'gates; zero misses; 30/60 fps; speeds 230–950.');
  return rows;
}
const debug=`
window.__gameTest={
  ready:()=>HEROMODELS.bear.glb&&HEROMODELS.owl.glb&&['skier','sboy','sitter','ratrak','ramp'].every(k=>NPCM[k]),
  reset(kind,d,seed){clearWorld();hideToast();let state=seed;Math.random=()=>{state=(state*1664525+1013904223)>>>0;return state/4294967296;};
    hero=kind;save.muted=true;save.fis=[1,2,3,4,5,6,7,8,9,10];save.missions=[];
    for(const k in keys)delete keys[k];drag=null;G=newGame('play');G.y=G.prevY=d*20;G.p.travelHeading=courseHeading(-d);G.stage=G.lastSpawnStage=stageAt(d);G.nextY=Infinity;G.nextChaseD=Infinity;
    $('menu').classList.add('hidden');$('topbtns').classList.remove('hidden');return this.state();},
  state(){return {mode:G.mode,time:G.t,dist:G.y/20,gates:G.gates,failKind:G.failKind||null,chase:!!G.chase,gap:G.chase?(G.y-G.chase.y)/20:null,bonus:G.bonus};},
  advance(seconds,control,fps=60){drag=control==='free'?null:{};if(control==='slow')G.p.slow=seconds+1;if(control==='coffee')G.p.boost=seconds+1;
    for(let i=0;i<Math.ceil(seconds*fps)&&G.mode==='play';i++)update(1/fps);render(0);return this.state();},
  avalanche(kind,d,control,fps){this.reset(kind,d,34);G.nextChaseD=d;return this.advance(22,control,fps);},
  coast(kind,seed,fps,x){this.reset(kind,0,seed);G.p.x=G.tx=x;return this.advance(30,'free',fps);},
  gates(kind,d,seed,fps){this.reset(kind,d,seed);drag={};update(1/fps);
    const n0=G.objs.length;addCourse(G.y+G.speed*1.5,1);const target=G.objs.slice(n0).filter(o=>o.type==='gate');
    const seen=new Set();let rivalMissed=0;
    for(let i=0;i<20*fps&&G.mode==='play'&&(target.some(o=>!o.done)||seen.size<target.length);i++){
      const next=target.find(o=>!o.done);G.tx=next?next.x:200;const prev=G.rival?G.rival.o.y:null;update(1/fps);
      if(prev!==null&&G.rival){const r=G.rival.o;for(const gate of target)if(!seen.has(gate)&&prev<gate.y&&r.y>=gate.y){seen.add(gate);if(Math.abs(r.x-gate.x)>=gate.gap/2)rivalMissed++;}}
    }render(0);return {...this.state(),total:target.length,passed:target.filter(o=>o.ok).length,rivalPassed:seen.size-rivalMissed,rivalMissed};}
};
`;
async function browserChecks(){
  const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
  let html=source.replace('function frame(now){','function frame(now){if(window.__freeze)return;');
  const end=html.lastIndexOf('</script>');html=html.slice(0,end)+debug+html.slice(end);fs.writeFileSync('docs/test-gameplay.html',html);
  const browser=await chromium.launch({headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  try{
    const page=await browser.newPage({viewport:{width:375,height:812},deviceScaleFactor:1});const errors=[];
    page.on('pageerror',e=>errors.push(String(e)));await page.addInitScript(()=>window.__freeze=true);
    await page.goto('http://127.0.0.1:8766/test-gameplay.html',{waitUntil:'domcontentloaded'});
    await page.waitForFunction(()=>window.__gameTest&&__gameTest.ready(),{timeout:60000});
    const avalanches=[],coasting=[],gates=[];
    for(const kind of ['bear','owl'])for(const fps of [30,60]){
      for(const d of [900,13000])for(const control of ['cruise','coffee','slow']){
        let result=await page.evaluate(args=>__gameTest.avalanche(...args),[kind,d,control,fps]);
        if(control!=='slow'){
          if(d===900&&fps===60)await page.screenshot({path:path.join(out,kind+'-avalanche.png')});
          assert.equal(result.mode,'play','Automatic speed must escape avalanche without holding acceleration');assert.equal(result.chase,false);
        }else assert.equal(result.failKind,'avalanche','Slowed player must remain vulnerable to avalanche');
        avalanches.push({kind,fps,d,control,...result});
      }
      for(const seed of [1,7,23])for(const x of [20,200,380])coasting.push({kind,fps,seed,x,...await page.evaluate(args=>__gameTest.coast(...args),[kind,seed,fps,x])});
      for(const d of [0,13000,16000]){
        const result=await page.evaluate(args=>__gameTest.gates(...args),[kind,d,54,fps]);
        assert.equal(result.mode,'play');assert.equal(result.passed,result.total,'Player must be able to reach every gate');
        assert.equal(result.rivalMissed,0,'Rival misses a gate in the running game');assert.equal(result.rivalPassed,result.total);
        gates.push({kind,fps,d,...result});if(d===13000&&fps===60)await page.screenshot({path:path.join(out,kind+'-slalom.png')});
      }
    }
    assert(coasting.every(r=>r.mode==='crash'&&r.failKind==='edge'&&r.time<30),'An unattended run must hit a snowbank even without obstacles');assert.deepEqual(errors,[]);
    const report={viewport:{width:375,height:812},errors,avalanches,coasting,gates};
    fs.writeFileSync(path.join(out,'browser-summary.json'),JSON.stringify(report,null,2));
    console.log('Browser PASS:',avalanches.length,'avalanche cases,',coasting.length,'unattended runs,',gates.length,'player slaloms.');
  }finally{await browser.close();fs.rmSync('docs/test-gameplay.html',{force:true});}
}
(async()=>{rivalGrid();if(!process.argv.includes('--rival-only'))await browserChecks();})().catch(e=>{console.error(e);process.exitCode=1;});
