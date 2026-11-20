// Replay the steering sampled by ShopBalanceChecks through a phone viewport and a real Web player.
// Generate OwlSaleRides.json first with tools/build-unity-race.ps1 -Verify.
const assert=require('node:assert/strict');
const fs=require('node:fs'),os=require('node:os'),path=require('node:path');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const root=new URL(process.argv[2]||'http://127.0.0.1:8784/progression-test/');
const series=JSON.parse(fs.readFileSync(process.argv[3]||path.join(os.tmpdir(),'OwlSaleRides.json'),'utf8')).rides;
async function main(){
  assert(root.pathname.endsWith('/progression-test/'),'Use the isolated progression build');
  const browser=await chromium.launch({args:['--enable-unsafe-swiftshader']});
  try{
    const context=await browser.newContext({viewport:{width:390,height:780},hasTouch:true});
    const page=await context.newPage(),cdp=await context.newCDPSession(page);
    const out=path.resolve('course-checks','mountain-race','shop');fs.mkdirSync(out,{recursive:true});
    await page.route('**/*',route=>{
      const q=route.request();
      if(q.method()==='POST'&&new URL(q.url()).origin!==root.origin)return route.abort();
      return route.continue();
    });
    await page.addInitScript(()=>{
      let handle;
      Object.defineProperty(window,'onMountainState',{get(){return handle;},set(fn){handle=s=>{window.shopRace=s;fn(s);};}});
    });
    let wallet=0,firstAffordable=0,price;
    const touch=(type,x)=>cdp.send('Input.dispatchTouchEvent',{type,touchPoints:x===undefined?[]:[{x,y:500}]});
    const rows=[];
    for(const ride of series){
      const url=new URL(root);url.searchParams.set('seed',ride.seed);
      await page.goto(url.href,{waitUntil:'commit'});await page.locator('#play').waitFor({timeout:180000});
      assert.equal(await page.evaluate(()=>window.mountainRelease.channel),'progression');
      assert.equal(Number(await page.locator('#bank').innerText()),wallet);
      console.log('WEB SHOP START',ride.hero,'seed',ride.seed,'wallet',wallet);
      await page.locator('[data-hero="'+ride.hero+'"]').tap();
      price=await page.evaluate(()=>MountainSaves.price({gear:{owl:'standard'},hero:'owl',ownedGear:[]},'fireboard'));
      await page.locator('#play').tap();await page.waitForFunction(()=>shopRace.phase==='play');
      let index=0,held=false;
      const deadline=Date.now()+240000;
      for(;;){
        const s=await page.evaluate(()=>({at:shopRace.metres,phase:shopRace.phase}));
        if(s.phase==='over')break;
        assert(Date.now()<deadline,'ride did not end, seed '+ride.seed);
        let point;
        while(index<ride.input.length&&ride.input[index].at<=s.at)point=ride.input[index++];
        if(point||index===ride.input.length){
          if(held){await touch('touchEnd');held=false;}
          if(point?.steer){await touch('touchStart',195);held=true;await touch('touchMove',Math.max(12,Math.min(378,195+390*point.drag)));}
        }
        await page.waitForTimeout(32);
      }
      if(held)await touch('touchEnd');
      const stats=await page.evaluate(()=>({seed:shopRace.seed,metres:Math.floor(shopRace.metres),score:shopRace.score,fruit:shopRace.coins,
        exits:shopRace.message,parts:[shopRace.gates,shopRace.rings,shopRace.fis]}));
      await page.locator('#exitResult').tap();await page.locator('#play').waitFor();
      wallet+=stats.fruit;
      assert.equal(Number(await page.locator('#bank').innerText()),wallet,'wallet did not match fruit in the real rides');
      if(wallet>=price&&!firstAffordable)firstAffordable=rows.length+1;
      rows.push({...stats,wallet});
      console.log('WEB SHOP RIDE',JSON.stringify(rows.at(-1)));
      fs.writeFileSync(path.join(out,'actual-rides.json'),JSON.stringify({rows,firstAffordable,price},null,2));
    }
    assert(wallet>=price,'six actual rides did not buy a kit');
    await page.locator('[data-hero="owl"]').tap();await page.locator('[data-panel="shop"]').tap();
    await page.getByRole('button',{name:/взять напрокат/}).tap();
    assert.match(await page.locator('#panelBody').innerText(),/Доска «Салют» · выбрано/);
    await page.getByRole('button',{name:/Купить свой комплект/}).tap();
    assert.equal(Number(await page.locator('#bank').textContent()),wallet-price);
    await page.screenshot({path:path.join(out,'purchase.png')});
    await page.reload({waitUntil:'commit'});await page.locator('#play').waitFor({timeout:180000});
    assert.match(await page.locator('[data-hero="owl"]').innerText(),/Салют/);
    assert.doesNotMatch(await page.locator('[data-hero="owl"]').innerText(),/прокат/);
    // A separate ride in the purchased kit, using an independent finger gesture and a natural fall.
    await page.locator('#play').tap();
    await page.waitForFunction(()=>shopRace.phase==='play'&&shopRace.metres>=10);
    await touch('touchStart',195);await touch('touchMove',245);
    await page.waitForFunction(()=>shopRace.metres>=22||shopRace.phase==='over');
    await touch('touchEnd');
    await page.waitForFunction(()=>shopRace.phase==='over',null,{timeout:150000});
    const ownRide=await page.evaluate(()=>({metres:Math.floor(shopRace.metres),score:shopRace.score,fruit:shopRace.coins,fallen:shopRace.rankedResult}));
    assert(ownRide.fallen);await page.locator('#exitResult').tap();await page.locator('#play').waitFor();
    const balance=Number(await page.locator('#bank').innerText());
    assert.equal(balance,wallet-price+ownRide.fruit);
    const report={rows,firstAffordable,price,balanceAfterPurchase:wallet-price,ownRide,balance};
    fs.writeFileSync(path.join(out,'actual-rides.json'),JSON.stringify(report,null,2));
    assert(firstAffordable>=4&&firstAffordable<=6,'First personal kit became affordable on ride '+firstAffordable);
    console.log('WEB SHOP PASS',JSON.stringify(report));
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
