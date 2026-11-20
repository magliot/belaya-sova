// Two independent players enter from the Telegram test route, ride and read each other's place.
const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const root=new URL(process.argv[2]||'http://127.0.0.1:8784/');
async function main(){
  const browser=await chromium.launch();
  const reports=[],evidence=path.resolve('course-checks/mountain-race/progression');
  fs.mkdirSync(evidence,{recursive:true});
  try{
    for(const [hero,seed] of [['owl',1911],['bear',783]]){
      const context=await browser.newContext({viewport:{width:384,height:832},hasTouch:true});
      try{
        await context.addInitScript(()=>{
          let handle;
          Object.defineProperty(window,'onMountainState',{get:()=>handle,set:fn=>{handle=s=>{window.latestRide=s;fn(s);};}});
        });
        const page=await context.newPage(),errors=[];
        page.on('pageerror',e=>errors.push(e.message));
        const url=new URL(root);url.searchParams.set('seed',seed);url.hash='tgWebAppStartParam=unitytest_progress21';
        await page.goto(url.href,{waitUntil:'commit'});
        await page.locator('#play').waitFor({timeout:180000});
        assert(page.url().includes('/progression-test/'));
        assert.equal(await page.evaluate(()=>mountainRelease.channel),'progression');
        await page.locator('[data-hero="'+hero+'"]').tap();await page.locator('#play').tap();
        await page.waitForFunction(()=>window.latestRide?.metres>=3,null,{timeout:60000});
        console.log('WEB START',hero,seed);
        await page.locator('#exitResult').waitFor({timeout:120000});
        const s=await page.evaluate(()=>window.latestRide);
        console.log('WEB FALL',hero,s.score,Math.floor(s.metres));
        assert(s.rankedResult&&s.mode==='normal'&&s.hero===hero);
        const sum=[s.scoreDistance,s.scoreFruit,s.scoreGates,s.scoreRings,s.scoreFis].reduce((a,b)=>a+b,0);
        assert.equal(s.score,sum);assert.equal(await page.locator('#breakdown .stat').count(),5);
        await page.waitForFunction(()=>document.querySelector('#rankStatus').textContent.includes('Место #'),null,{timeout:35000});
        const receipt=await page.locator('#rankStatus').innerText();
        await page.screenshot({path:path.join(evidence,hero+'-result.png')});
        await page.locator('#exitResult').tap();await page.locator('#play').waitFor();
        assert.equal(Number(await page.locator('#bank').innerText()),s.coins);
        assert.equal(Number(await page.locator('#best').innerText()),s.score);
        await page.locator('[data-panel="top"]').first().tap();
        await page.waitForFunction(score=>Array.from(document.querySelectorAll('#panelBody .stat b')).some(el=>el.textContent===score+' очк.'),s.score);
        for(const r of reports)assert((await page.locator('#panelBody').innerText()).includes(r.score+' очк.'));
        assert.deepEqual(errors,[]);
        reports.push({hero,seed,dist:s.scoreDistance,coins:s.coins,fruit:s.scoreFruit,gates:s.scoreGates,rings:s.scoreRings,fis:s.scoreFis,score:s.score,receipt});
        console.log('WEB RANK',JSON.stringify(reports.at(-1)));
        if(reports.length===2)await page.screenshot({path:path.join(evidence,'standings.png')});
      }finally{await context.close();}
    }
    fs.writeFileSync(path.join(evidence,'receipt.json'),JSON.stringify(reports,null,2));
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
