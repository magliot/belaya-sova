const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {chromium} = require(process.env.DANCE_BROWSER_MODULE || 'playwright');

const url = new URL(process.argv[2] || 'http://127.0.0.1:8798/unity/?seed=1911');
const evidence = process.env.FREERIDE_EVIDENCE_DIR || path.resolve(__dirname, '../course-checks/freeride-final');
const seed = Number(url.searchParams.get('seed') || 1911);
const fromTelegramLink = url.searchParams.get('startapp') === 'freeride_1911';
if (!fromTelegramLink) url.searchParams.set('seed', seed);

async function main() {
  const browser = await chromium.launch({
    executablePath: process.env.CHROME_EXECUTABLE || undefined,
    args: process.env.FREERIDE_SOFTWARE_GPU ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : []
  });
  try {
    const page = await browser.newPage({viewport:{width:390, height:780}, hasTouch:true, deviceScaleFactor:1});
    const errors = [], posts = [], samples = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => {
      if (/Shader error|(?:NullReference|IndexOutOfRange|InvalidOperation|MissingReference)Exception:/.test(message.text())) {
        errors.push(message.text().slice(0, 350));
      }
    });
    await page.route('**/*', route => {
      if (route.request().method() === 'POST') {
        let action;
        try { action = JSON.parse(route.request().postData()).action; } catch {}
        // The main menu also reads standings via POST. Only that read is mocked.
        if (action === 'leaderboard') {
          return route.fulfill({json:{ok:true, rows:[]}, headers:{'Access-Control-Allow-Origin':'*'}});
        }
        posts.push({action, url:route.request().url()});
        return route.abort();
      }
      return route.continue();
    });
    // Observe the public HUD event. Every driving action below goes through the real menu and controls.
    await page.addInitScript(() => {
      let listener;
      Object.defineProperty(window, 'onMountainState', {
        get: () => listener,
        set: fn => { listener = s => {
          window.probe=s;
          if(window.turnNames&&s.phase==='play'&&['Сальто назад','Оборот вправо','Вращение 360°'].includes(s.message)
            &&window.turnNames.at(-1)!==s.message)window.turnNames.push(s.message);
          fn(s);
        }; }
      });
    });
    fs.mkdirSync(evidence, {recursive:true});
    const touch = await page.context().newCDPSession(page);
    const phase = (value, hero) => page.waitForFunction(
      ([value, hero]) => window.probe?.phase === value && (!hero || probe.hero === hero),
      [value, hero], {timeout:45000}
    );
    const at = metres => page.waitForFunction(
      metres => window.probe?.phase === 'play' && probe.metres >= metres, metres, {timeout:60000}
    );
    const click = id => page.locator('#' + id).tap();
    const shot = filename => page.screenshot({path:path.join(evidence, filename + '.jpg'), type:'jpeg', quality:88});
    await page.goto(url.href, {waitUntil:'commit'});
    async function inspect() {
      await page.locator('[data-panel="settings"]').tap();
      await page.getByRole('button',{name:'Осмотр трассы',exact:true}).tap();
      await phase('review');
    }
    async function seek(d) {
      await page.locator('#reviewAt').fill(String(d));await page.locator('#reviewAt').press('Enter');
      await page.waitForFunction(d=>probe.phase==='review'&&probe.metres===d,d);
    }
    async function leavePractice() {
      await click('pause');await phase('pause');await click('exitPaused');await phase('review');
    }
    const setLane=x=>page.locator('#reviewLane').evaluate((el,value)=>{el.value=String(value);el.dispatchEvent(new Event('input',{bubbles:true}));},x);
    async function quickLink(name,metre){
      const dest=new URL(url);dest.pathname=dest.pathname.replace(/(?:unity(?:-test)?|progression-test)\/$/,'');
      dest.searchParams.set('startapp',name);dest.searchParams.delete('reviewStage');dest.searchParams.delete('reviewAt');
      await page.goto(dest.href,{waitUntil:'commit'});await phase('review');
      await page.waitForFunction(d=>probe.metres===d&&probe.seed===1911,metre);
      assert.equal(await page.evaluate(()=>mountainRelease.channel),'test');
    }
    async function touchTapWhileSteering(steer){
      const tap={x:292,y:530,id:2};
      await touch.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[steer,tap]});
      await touch.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[tap]});
    }
    if(fromTelegramLink){
      await phase('review');
      await page.waitForFunction(()=>probe.metres===19955&&probe.seed===1911);
      assert.equal(new URL(page.url()).searchParams.get('reviewStage'),'8');
      assert.equal(await page.evaluate(()=>mountainRelease.channel),'test');
      await click('reviewExit');
    }
    await phase('menu');
    assert.equal(await page.locator('[data-mode="freeride"]').count(),0);
    assert.equal(await page.locator('#freeRideJump').count(),0);
    await shot('menu');
    await page.setViewportSize({width:320,height:640});
    for(const name of ['collection','missions','shop','settings','rules']){
      await page.locator('[data-panel="'+name+'"]').tap();
      const el=page.locator('#panel');
      assert(await el.isVisible());
      assert.match(await el.evaluate(node=>getComputedStyle(node).borderImageSource),/hud-59b/);
      assert(await el.evaluate(node=>node.scrollWidth<=node.clientWidth+1));
      await click('closePanel');
    }
    await shot('menu-320');await page.setViewportSize({width:390,height:780});
    await click('play');await at(3);
    assert.equal(await page.evaluate(()=>probe.stage),0);
    assert.deepEqual(await page.locator('#hud button').evaluateAll(nodes=>nodes.map(node=>node.id)),['pause']);
    assert.match(await page.locator('#speedometer').evaluate(node=>getComputedStyle(node).borderImageSource),/hud-59b/);
    await click('pause');await phase('pause');await shot('winter-pause');await click('exitPaused');await phase('menu');
    await inspect();
    for(const hero of ['bear','owl']){
      await page.locator('#reviewHero').selectOption(hero);
      await phase('review',hero);
      await seek(19955);
      await page.evaluate(()=>{window.turnNames=[];});
      await click('reviewPlay');await phase('play',hero);
      await touch.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:195,y:515,id:1}]});
      await at(19967);
      if(hero==='bear')await shot('arch-19970m');
      await at(20001);
      await page.waitForFunction(()=>probe.stage===8);
      const x=195-4.83*390/(27*.79);
      const steer={x,y:515,id:1};
      await touch.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[steer]});
      await page.waitForFunction(()=>probe.freeRideTrickReady&&probe.air&&probe.metres>=20086,null,{timeout:12000});
      const tapMetres=await page.evaluate(()=>probe.metres);
      if(hero==='bear'){
        await touch.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
        await touch.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:285,y:490,id:1}]});
        await page.waitForFunction(()=>probe.air&&!probe.freeRideTrickReady,null,{timeout:2500});
        await touch.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
        await touch.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[steer]});
      }else{
        const tap={x:290,y:535,id:2};
        await touch.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[steer,tap]});
        await touch.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[tap]});
      }
      await page.waitForFunction(()=>probe.air&&!probe.freeRideTrickReady&&probe.metres>=20092,null,{timeout:3000});
      await shot(hero+'-jump');
      await at(20124);
      const stats=await page.evaluate(()=>probe);
      assert.equal(stats.helmets,1);assert.equal(stats.tricks,1);assert.equal(stats.courseMode,'normal');
      const moves=await page.evaluate(()=>turnNames);
      if(hero==='owl')assert.deepEqual(moves,['Сальто назад']);
      samples.push({hero,input:hero==='bear'?'airborne press':'second finger tap',tapMetres,metres:stats.metres,coins:stats.coins,tricks:stats.tricks,helmets:stats.helmets,moves});
      if(hero==='owl'){
        await touch.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{...steer,x:x+52}]});
        await page.waitForFunction(lane=>probe.riderLane>lane+1,stats.riderLane,{timeout:3000});
      }
      await page.waitForFunction(()=>probe.boulderNear>.30,null,{timeout:12000});
      if(hero==='bear')await shot('rolling-snow');
      await touch.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
      await leavePractice();
    }
    // A quick steering correction out and back ends where it began. It must still count as a drag.
    await seek(20048);
    await page.locator('#reviewLane').evaluate(el=>{el.value='-4.8';el.dispatchEvent(new Event('input',{bubbles:true}));});
    await click('reviewPlay');
    const centre={x:182,y:480,id:1};
    await touch.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[centre]});
    await at(20070);
    await touch.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
    await page.touchscreen.tap(182,480);
    await touch.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[centre]});
    await page.waitForFunction(()=>probe.air&&probe.freeRideTrickReady,null,{timeout:12000});
    await touch.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{...centre,x:centre.x+32}]});
    await touch.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[centre]});
    await touch.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
    await page.waitForFunction(()=>probe.freeRideInCrevasse,null,{timeout:8000});
    assert.equal(await page.evaluate(()=>probe.helmets),0);
    assert.equal(await page.evaluate(()=>probe.tricks),0);
    samples.push({hero:'owl',input:'ground tap, then steer only',helmets:0,tricks:0});
    await leavePractice();
    await page.locator('#reviewLane').evaluate(el=>{el.value='0';el.dispatchEvent(new Event('input',{bubbles:true}));});
    await seek(22485);await click('reviewPlay');await at(22503);
    assert.equal(await page.evaluate(()=>probe.stage),0);
    assert.match(await page.locator('#stage').innerText(),/Круг 2/);
    await leavePractice();await click('reviewExit');
    await quickLink('snowpark_1911',7500);
    for(const hero of ['bear','owl']){
      await page.locator('#reviewHero').selectOption(hero);await phase('review',hero);
      await page.evaluate(()=>{window.turnNames=[];});
      await setLane(-2.75);await seek(7650);await click('reviewPlay');
      const steer={x:190,y:510,id:1};
      await touch.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[steer]});
      await page.waitForFunction(()=>probe.air&&probe.metres>=7673);
      await touchTapWhileSteering(steer);
      await at(7688);await touchTapWhileSteering(steer);
      await at(7694);await shot('park-turn-'+hero);
      await at(7736);const s=await page.evaluate(()=>probe);
      assert.equal(s.tricks,2);assert.equal(s.rings,1);assert(s.feather);
      const moves=await page.evaluate(()=>turnNames);
      if(hero==='owl')assert.deepEqual(moves,['Сальто назад','Оборот вправо']);
      samples.push({hero,park:s.metres,tricks:s.tricks,feather:s.feather,moves});
      await touch.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});await leavePractice();
    }
    await quickLink('freeridewide_1911',20165);
    for(const [path,x] of [['shelf',-10.36],['jump',1.67]]){
      await setLane(0);await seek(20165);await click('reviewPlay');
      const origin={x:230,y:510,id:1},steer={...origin,x:230+x*390/(27*.79)};
      await touch.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[origin]});
      await touch.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[steer]});
      if(path==='shelf'){await at(20204);await shot('wide-approach');}
      else{await page.waitForFunction(()=>probe.freeRideTrickReady&&probe.metres>20228);await touchTapWhileSteering(steer);}
      await at(20234);await shot('wide-'+path);
      await at(20266);const s=await page.evaluate(()=>probe);
      assert.equal(s.helmets,1);assert.equal(s.tricks,path==='jump'?1:0);
      samples.push({path,metres:s.metres,tricks:s.tricks,helmets:s.helmets});
      await touch.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});await leavePractice();
    }
    assert.deepEqual(posts, []);
    assert.deepEqual(errors, []);
    console.log('FREE RIDE WEB PASS', JSON.stringify(samples));
  } finally {
    await browser.close();
  }
}
main().catch(error => {console.error(error); process.exitCode=1;});
