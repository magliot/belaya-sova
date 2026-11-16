const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.DANCE_BROWSER_MODULE || 'playwright');
const out = path.resolve('dance-checks');
fs.mkdirSync(out, { recursive: true });
const debug = `
window.__danceTest={
  ready:()=>HEROMODELS.bear.glb&&HEROMODELS.owl.glb,
  prepare(){save.meet.feathers=3;save.meet.gifts=20;save.best=5000;save.fis=[1,2,3,4,5,6,7,8,9,10];},
  pose(kind,id,t){hero=kind;G=newGame('menu');G.t=t;G.dance=id?{id,t0:0,until:99}:null;menuYaw=0;menuVel=0;refreshMenu();render(0);return this.measure(kind);},
  measure(kind){const h=HEROMODELS[kind],points={};h.root.updateMatrixWorld(true);
    for(const name of ['Head','LeftArm','LeftHand','RightArm','RightHand','LeftFoot','RightFoot']){
      const v=h.bones[name].getWorldPosition(new THREE.Vector3());h.holder.worldToLocal(v);points[name]=v.toArray();}
    const equipmentHidden=!(h.board&&h.board.visible)&&!(h.skis&&h.skis.some(s=>s.visible))&&!(h.poles&&h.poles.some(p=>p.g.visible));
    return {points,equipmentHidden};},
  advance(dt){update(dt);render(dt);return {mode:G.mode,hasDance:!!(G.dance&&G.t<G.dance.until)};},
  current(){return {hero,mode:G.mode,dance:G.dance&&G.dance.id};}
};
`;
let html=fs.readFileSync('docs/index.html','utf8');
html=html.replace('function frame(now){','function frame(now){if(window.__freeze)return;');
const end=html.lastIndexOf('</script>');
html=html.slice(0,end)+debug+html.slice(end);
fs.writeFileSync('docs/test.html',html);
(async()=>{
  const browser=await chromium.launch({headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  try {
    const page=await browser.newPage({viewport:{width:375,height:812},deviceScaleFactor:1});
    const errors=[];page.on('pageerror',e=>errors.push(String(e)));
    await page.addInitScript(()=>window.__freeze=true);
    await page.goto('http://127.0.0.1:8766/test.html',{waitUntil:'domcontentloaded'});
    await page.waitForFunction(()=>window.__danceTest&&__danceTest.ready(),{timeout:60000});
    await page.evaluate(()=>__danceTest.prepare());
    const summary=[];
    for(const hero of ['bear','owl']) {
      for(const [dance,t] of [[null,0],['sway',0],['floss',Math.PI/14],['floss',3*Math.PI/14],['hop',0],['hop',Math.PI/8.4],['dab',0],['champ',0]]){
        const result=await page.evaluate(([h,d,t])=>__danceTest.pose(h,d,t),[hero,dance,t]);
        assert(result.equipmentHidden,`${hero}: equipment visible in menu`);
        assert(Object.values(result.points).flat().every(Number.isFinite),`${hero}: invalid pose`);
        summary.push({hero,dance,t,...result});
        await page.screenshot({path:path.join(out,`${hero}-${dance||'idle'}-${t.toFixed(3)}.png`)});
      }
      await page.evaluate(h=>__danceTest.pose(h,null,0),hero);
      await page.getByRole('button',{name:'💃 Танцы',exact:true}).click();
      await page.locator('[data-d="hop"]').click();
      const state=await page.evaluate(()=>__danceTest.current());
      assert.equal(state.dance,'hop');assert.equal(state.hero,hero);
      const ended=await page.evaluate(()=>__danceTest.advance(8.1));
      assert.equal(ended.hasDance,false);assert.equal(ended.mode,'menu');
    }
    assert.deepEqual(errors,[],'JavaScript errors');
    fs.writeFileSync(path.join(out,'summary.json'),JSON.stringify({viewport:{width:375,height:812},errors,summary},null,2));
    console.log('PASS: both GLB heroes, all five dances, phone viewport, menu equipment and dance completion.');
  } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
