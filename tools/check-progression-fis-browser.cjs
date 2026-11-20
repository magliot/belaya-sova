// Exercise the real WebAssembly HUD by touch. The inspection segment must keep the journal unchanged.
const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const game=new URL(process.argv[2]||'http://127.0.0.1:8784/progression-test/?seed=1911');
assert.ok(game.pathname.endsWith('/progression-test/'));
const output=path.resolve('course-checks/mountain-race/fis');
async function main(){
  fs.mkdirSync(output,{recursive:true});
  const browser=await chromium.launch();
  try{
    const page=await browser.newPage({viewport:{width:320,height:640},hasTouch:true});
    const errors=[];page.on('pageerror',e=>errors.push(e.message));
    await page.route('**/*',route=>route.request().method()==='POST'&&new URL(route.request().url()).origin!==game.origin?route.abort():route.continue());
    await page.addInitScript(()=>{
      let sink;
      Object.defineProperty(window,'onMountainState',{get(){return sink;},set(f){sink=s=>{window.fisRide=s;f(s);};}});
    });
    await page.goto(game.href,{waitUntil:'commit'});
    const at=(selector)=>page.locator(selector);
    const shot=file=>page.screenshot({path:path.join(output,file+'.png')});
    await at('#play').waitFor({timeout:180000});
    assert.equal(await page.evaluate(()=>mountainRelease.channel),'progression');
    const rules=at('[data-panel="rules"]');
    await rules.tap();assert.equal(await at('#panelBody').getByText('Ещё не открыто',{exact:true}).count(),10);
    await shot('journal-before');await at('#closePanel').tap();
    const seen={};
    async function act(action){
      await page.waitForFunction(key=>window.fisRide?.fisAction===key,action,{timeout:60000});
      const button=at('#fisAct');
      const bounds=await button.boundingBox();
      assert.ok(bounds&&bounds.x>=0&&bounds.y>=0&&bounds.x+bounds.width<=320.01&&bounds.y+bounds.height<=640.01&&bounds.height>=40,
        action+' action is clipped or too small');
      seen[action]={label:await button.innerText(),at:await page.evaluate(()=>Math.round(fisRide.metres)),width:Math.round(bounds.width),height:Math.round(bounds.height)};
      if(action==='patrol'||action==='join')await shot('hud-'+action);
      await button.tap();
    }
    await at('#play').tap();await act('brake');
    await page.waitForFunction(()=>fisRide.phase==='play'&&fisRide.metres>=43,null,{timeout:60000});
    await at('#pause').tap();await at('#exitPaused').tap();await at('#exitResult').tap();
    await rules.tap();
    const cards=at('#panelBody details');
    assert.ok(await cards.count()>0);
    await cards.first().locator('summary').tap();assert.ok(await cards.first().locator('p').isVisible());
    await shot('journal-opened');
    const journal=await at('#panelBody').innerText();
    const current=await at('#ruleCount').innerText();
    await at('#closePanel').tap();await at('[data-panel="settings"]').tap();
    await at('#panelBody').getByRole('button',{name:'Осмотр трассы'}).tap();
    await at('#reviewAt').fill('255');await at('#reviewAt').press('Tab');
    await at('#reviewLane').focus();await at('#reviewLane').press('Home');
    await at('#reviewPlay').tap();
    for(const action of ['stop','patrol','witness','walk','look','join'])await act(action);
    assert.ok(await page.evaluate(()=>[5,6,7,9,10].every(n=>fisRide.fis.includes(n))));
    await at('#pause').tap();await at('#exitPaused').tap();await at('#reviewExit').tap();
    await rules.tap();assert.equal(await at('#ruleCount').innerText(),current);
    await at('#panelBody details').first().locator('summary').tap();
    assert.equal(await at('#panelBody').innerText(),journal);
    assert.deepEqual(errors,[]);
    fs.writeFileSync(path.join(output,'hud.json'),JSON.stringify({viewport:[320,640],seed:1911,current,seen},null,2));
    console.log('FIS mobile Web PASS',current,JSON.stringify(seen),output);
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
