// Replay the steering sampled by ShopBalanceChecks through a phone viewport and a real Web player.
// Generate OwlSaleRides.json first with tools/build-unity-race.ps1 -Verify.
// The default keeps one finger on the route. The optional fourth argument "short" tests repeated release/regrip.
const assert=require('node:assert/strict');
const fs=require('node:fs'),os=require('node:os'),path=require('node:path');
const continuous=process.argv[4]!=='short';
async function inventoryLimits(){
  // An isolated map for boundary cases. The browser below starts with its own empty wallet.
  require('../unity/OwlSlope/Assets/WebGLTemplates/MountainRace/save.js');
  const records=new Map(),storage={getItem:k=>records.get(k),setItem:(k,v)=>records.set(k,v)};
  const open=()=>MountainSaves.create({channel:'progression',storage});
  const account=open();await account.load();
  assert.equal(account.buy('carveSkis'),false);
  account.change(['coins','meet'],d=>{d.coins=20500;d.meet.feathers=50;});
  const value=(p,via)=>via==='coins'?p.data.coins:MountainSaves.featherBalance(p.data);
  function paid(id,via='coins',kit){
    const before=value(account,via),cost=MountainSaves.price(account.data,id,via,kit);
    assert(account.buy(id,via,kit),id);assert.equal(value(account,via),before-cost,id);
  }
  function capped(id,via='coins',kit){
    const before=value(account,via);assert.equal(account.buy(id,via,kit),false);assert.equal(value(account,via),before);
  }
  for(let i=0;i<3;i++)paid('helmets');capped('helmets');
  for(const [hero,slot,key] of [['owl','gear','fireboard'],['bear','gear','carveSkis'],['bear','headgear','orangeHelmet'],['owl','headgear','pinkGoggles']]){
    assert.equal(MountainSaves.price(account.data,key),10);
    capped(key,'coins');paid(key,'feathers');capped(key,'feathers');
    assert.equal(account.data[slot][hero],key);
    paid('tune','coins',key);paid('tune','coins',key);capped('tune','coins',key);
  }
  for(const item of ['boost','mag']){for(let i=0;i<5;i++)paid(item);capped(item);}
  assert.equal(MountainSaves.price(account.data,'sled','coins'),5000);
  assert.equal(MountainSaves.price(account.data,'tubing','coins'),3000);
  paid('tubing','coins');capped('tubing','feathers');paid('sled','feathers');capped('sled','coins');
  const restored=open();await restored.load();
  assert.equal(value(restored,'feathers'),0);assert.equal(restored.data.meet.feathers,50);
  assert.deepEqual(restored.data.bonuses.sort(),['sled','tubing']);
  assert.deepEqual(restored.data.tuning,{fireboard:2,carveSkis:2,orangeHelmet:2,pinkGoggles:2});
  assert.equal(restored.data.helmets,3);assert.equal(restored.data.coins,account.data.coins);
  const earned=MountainSaves.create({channel:'progression'});await earned.load();
  const prize={mode:'normal',phase:'over',hero:'owl',stage:3,metres:7731,feather:true,feathers:1,coins:0};
  for(let n=1;n<=10;n++){
    earned.unlock(prize);assert(earned.finish('park-'+n,prize));assert(!earned.finish('park-'+n,prize));
    assert.equal(MountainSaves.featherBalance(earned.data),n);
    assert.deepEqual(earned.data.bonuses,[]);
    assert.equal(earned.buy('pinkGoggles','feathers'),n===10);
  }
  assert.equal(earned.data.headgear.owl,'pinkGoggles');assert.equal(MountainSaves.featherBalance(earned.data),0);
  for(const [flag,expected] of [['prizeFeather',['tubing','sled']],['goldenOwl',['sled']]]){
    const result=MountainSaves.legacyMerge({hero:'bear',meet:{[flag]:true,feathers:1},gear:{bear:'carveSkis'}},null,0);
    assert.deepEqual(result.bonuses,expected);assert.equal(result.gear.bear,'standard');
  }
  for(const elsewhere of [false,true]){
    const key='unity_test_v1', wrap=(data,updated)=>JSON.stringify({schema:1,uid:91,updated,data});
    const initial={hero:'owl',coins:6000,meet:{feathers:20},ownedGear:['fireboard'],gear:{owl:'standard'}};
    storage.setItem('bsova_tg91_'+key,wrap(initial,1));
    let incoming;
    const app={initData:'fixture',initDataUnsafe:{user:{id:91}},isVersionAtLeast:()=>true,
      CloudStorage:{getItems:(_keys,done)=>{incoming=done;},setItem:(_key,_v,done)=>done(null)}};
    const local=MountainSaves.create({channel:'test',storage,app});const loading=local.load();
    assert(local.buy('tubing','coins'));assert(local.buy('pinkGoggles','feathers'));
    const remote=elsewhere?{...initial,coins:3000,bonuses:['tubing'],ownedGear:['pinkGoggles','fireboard'],
      payments:{tubing:'coins',pinkGoggles:'feathers'}}:initial;
    incoming(null,{[key]:wrap(remote,2)});await loading;
    assert.equal(local.data.coins,3000);assert.equal(value(local,'feathers'),10);
    assert.equal(local.data.headgear.owl,'pinkGoggles');assert.equal(local.data.gear.owl,'standard');
    assert.deepEqual(local.data.bonuses,['tubing']);
  }
}
async function checkEarnedKits(rides){
  assert.equal(rides.length,6);
  const profile=MountainSaves.create({channel:'progression'});await profile.load();
  let fruit=0,feathers=0;
  for(const ride of rides){
    const state={...ride,mode:'normal',phase:'over',rankedResult:true};
    assert(profile.finish('earned-'+ride.seed,state));assert.equal(profile.finish('earned-'+ride.seed,state),false);
    fruit+=ride.coins;feathers+=ride.feathers||0;
    assert.equal(profile.data.coins,fruit);assert.equal(MountainSaves.featherBalance(profile.data),feathers);
  }
  assert(profile.buy('helmets','coins'));assert.equal(profile.data.coins,fruit-50);
  console.log('Six finished rides',rides.map(r=>r.coins),'earned fruit',fruit,'feathers',feathers,'after start helmet',profile.data.coins);
}
async function main(){
  await inventoryLimits();
  if(process.argv.includes('--inventory')){console.log('PASS inventory prices, caps, repeat purchase and reload');return;}
  if(process.argv[2]==='--receipts'){
    const file=process.argv[3]||path.join(os.tmpdir(),'OwlSaleRides.json');
    await checkEarnedKits(JSON.parse(fs.readFileSync(file,'utf8')).rides);return;
  }
  const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
  const root=new URL(process.argv[2]||'http://127.0.0.1:8784/progression-test/');
  assert(root.pathname.endsWith('/progression-test/'),'Use the isolated progression build');
  const series=JSON.parse(fs.readFileSync(process.argv[3]||path.join(os.tmpdir(),'OwlSaleRides.json'),'utf8')).rides;
  const browser=await chromium.launch({args:['--enable-unsafe-swiftshader']});
  try{
    const context=await browser.newContext({viewport:{width:390,height:780},hasTouch:true});
    const page=await context.newPage(),cdp=await context.newCDPSession(page);
    const out=path.resolve('course-checks','mountain-race',continuous?'shop-held':'shop');fs.mkdirSync(out,{recursive:true});
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
      price=await page.evaluate(()=>MountainSaves.price({helmets:0},'helmets'));
      await page.locator('#play').tap();await page.waitForFunction(()=>shopRace.phase==='play');
      if(!continuous)assert(await page.evaluate(()=>Number.isFinite(shopRace.riderLane)),'Update the Web build for short-drag replay');
      const build=await page.evaluate(()=>performance.getEntriesByType('resource').find(r=>r.name.includes('.wasm.'))?.name.split('/').pop());
      let index=0,held=false;
      if(continuous){
        assert(Number.isFinite(ride.input[0].lane),'Regenerate the Editor touch traces');
        await touch('touchStart',195);held=true;
      }
      const deadline=Date.now()+240000;
      for(;;){
        const s=await page.evaluate(()=>({at:shopRace.metres,lane:shopRace.riderLane,phase:shopRace.phase}));
        if(s.phase==='over')break;
        assert(Date.now()<deadline,'ride did not end, seed '+ride.seed);
        let point;
        while(index<ride.input.length&&ride.input[index].at<=s.at)point=ride.input[index++];
        if(continuous&&point){
          // Keep the finger on the glass; follow the lateral target sighted in the recorded run.
          const drag=point.lane/(27*.79)+(point.steer?point.drag:0);
          await touch('touchMove',Math.max(12,Math.min(378,195+390*drag)));
        }else if(!continuous&&(point||index===ride.input.length)){
          if(held){await touch('touchEnd');held=false;}
          if(point?.steer){
            const drag=point.drag+(Number.isFinite(s.lane)&&Number.isFinite(point.lane)?(point.lane-s.lane)/(27*.79):0);
            await touch('touchStart',195);held=true;await touch('touchMove',Math.max(12,Math.min(378,195+390*drag)));
          }
        }
        await page.waitForTimeout(32);
      }
      if(held)await touch('touchEnd');
      const stats=await page.evaluate(()=>({seed:shopRace.seed,hero:shopRace.hero,metres:Math.floor(shopRace.metres),score:shopRace.score,fruit:shopRace.coins,
        exits:shopRace.message,gates:shopRace.gates,rings:shopRace.rings,fis:shopRace.fis,
        points:{distance:shopRace.scoreDistance,fruit:shopRace.scoreFruit,gates:shopRace.scoreGates,rings:shopRace.scoreRings,fis:shopRace.scoreFis}}));
      assert.equal(stats.hero,ride.hero);assert.equal(Object.values(stats.points).reduce((a,b)=>a+b,0),stats.score);
      await page.locator('#exitResult').tap();await page.locator('#play').waitFor();
      wallet+=stats.fruit;
      assert.equal(Number(await page.locator('#bank').innerText()),wallet,'wallet did not match fruit in the real rides');
      if(wallet>=price&&!firstAffordable)firstAffordable=rows.length+1;
      rows.push({...stats,wallet,build});
      console.log('WEB SHOP RIDE',JSON.stringify(rows.at(-1)));
      fs.writeFileSync(path.join(out,'actual-rides.json'),JSON.stringify({rows,firstAffordable,price},null,2));
    }
    assert(wallet>=price,'six rides should afford a start helmet');
    await page.locator('[data-hero="owl"]').tap();await page.locator('[data-panel="shop"]').tap();
    await page.locator('[data-product="supplies"]').tap();
    await page.locator('[data-buy="helmets"]').tap();
    assert.equal(Number(await page.locator('#bank').textContent()),wallet-price);
    await page.screenshot({path:path.join(out,'purchase.png')});
    await page.reload({waitUntil:'commit'});await page.locator('#play').waitFor({timeout:180000});
    // A separate ride in the purchased kit, using an independent finger gesture and a natural fall.
    await page.locator('#play').tap();
    await page.waitForFunction(()=>window.shopRace?.phase==='play'&&shopRace.metres>=10);
    assert.equal(await page.evaluate(()=>shopRace.helmets),1);
    await touch('touchStart',195);await touch('touchMove',245);
    await page.waitForFunction(()=>shopRace.metres>=22||shopRace.phase==='over');
    await touch('touchEnd');
    await page.waitForFunction(()=>shopRace.phase==='over',null,{timeout:150000});
    const ownRide=await page.evaluate(()=>({metres:Math.floor(shopRace.metres),score:shopRace.score,fruit:shopRace.coins,fallen:shopRace.rankedResult}));
    assert(ownRide.fallen);await page.locator('#exitResult').tap();await page.locator('#play').waitFor();
    const balance=Number(await page.locator('#bank').innerText());
    assert.equal(balance,wallet-price+ownRide.fruit);
    const report={rows,steering:continuous?'held':'short drags',firstAffordable,price,balanceAfterPurchase:wallet-price,ownRide,balance};
    fs.writeFileSync(path.join(out,'actual-rides.json'),JSON.stringify(report,null,2));
    console.log('WEB SHOP PASS',JSON.stringify(report));
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
