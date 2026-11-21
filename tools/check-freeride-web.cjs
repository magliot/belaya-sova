const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {chromium} = require(process.env.DANCE_BROWSER_MODULE || 'playwright');

const url = new URL(process.argv[2] || 'http://127.0.0.1:8798/unity/?seed=1911');
const evidence = process.env.FREERIDE_EVIDENCE_DIR || path.resolve(__dirname, '../course-checks/freeride-skier');
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
        set: fn => { listener = s => { window.probe = s; fn(s); }; }
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
    const save = () => page.evaluate(() => JSON.stringify({...localStorage}));
    const shot = filename => page.screenshot({path:path.join(evidence, filename + '.jpg'), type:'jpeg', quality:88});
    await page.goto(url.href, {waitUntil:'commit'});
    await page.locator('[data-mode="freeride"]').waitFor({timeout:210000});
    if (fromTelegramLink) {
      const landing = new URL(page.url());
      assert.equal(landing.pathname, new URL('unity-test/', url).pathname);
      assert.equal(landing.searchParams.get('freerideSeed'), '1911');
      assert.equal(landing.searchParams.has('seed'), false, 'The trial should pin only Free Ride');
      const profile = await page.evaluate(() => window.mountainRelease);
      assert.equal(profile.channel, 'test');
      assert.equal(profile.leaderboardEndpoint, '');
    }
    const rides=[
      {hero:'owl',line:'swipe'}, {hero:'owl',guest:true,line:'button'},
      {hero:'owl',guest:true,line:'fall'}, {hero:'bear',line:'bypass'}
    ];
    for (const {hero,guest=false,line} of rides) {
      await page.locator('[data-hero="' + hero + '"]').tap();
      await phase('menu', hero);
      const profile = await save();
      await page.locator('[data-mode="freeride"]').tap();
      if(line==='button')await shot('pick-skier');
      await page.getByRole('button',{name:guest?'Лыжник · сальто назад':hero==='owl'?'Ехать за Сову':'Ехать за Мишаню',exact:true}).tap();
      await phase('play', hero);
      await at(4);
      const start = await page.evaluate(() => probe);
      assert.equal(start.courseMode, 'freeride');
      assert.equal(start.seed, seed);
      assert.equal(start.rankedResult, false);
      assert.equal(start.freeRideSkier, guest);
      await click('pause');
      await phase('pause', hero);
      const pausedAt = await page.evaluate(() => probe.metres);
      await page.waitForTimeout(350);
      assert.equal(await page.evaluate(() => probe.metres), pausedAt);
      await click('resume');
      await at(pausedAt + 3);
      await click('pause');
      await phase('pause', hero);
      await click('restartPaused');
      await phase('play', hero);
      const retry = await page.evaluate(() => probe);
      assert.equal(retry.seed, seed);
      assert.equal(retry.freeRideSkier, guest);
      assert(retry.metres < pausedAt + 2, 'Retry must return to the start');

      const fixedFirstGap = seed === 1911;
      let steeringTouch;
      if (fixedFirstGap) {
        await at(28);
        // On seed 1911 the first cornice is left of centre. Take it with the owl and pass on the right with the bear.
        const lane = await page.evaluate(() => probe.riderLane);
        const destination = line==='bypass' ? 2.9 : -4.83;
        await touch.send('Input.dispatchTouchEvent', {type:'touchStart', touchPoints:[{x:195,y:515,id:1}]});
        steeringTouch={x:Math.max(16, Math.min(374, 195 + (destination - lane)*390/(27*.79))),y:515,id:1};
        await touch.send('Input.dispatchTouchEvent', {type:'touchMove', touchPoints:[steeringTouch]});
      }
      await at(58);
      const frame = await page.evaluate(() => probe);
      if(line==='button')await shot('skier-descent');
      assert.equal(frame.courseMode, 'freeride');
      assert(frame.riderScreenX > .05 && frame.riderScreenX < .95, 'Hero outside phone view');
      if (fixedFirstGap) {
        if (line==='button'||line==='swipe') {
          await page.waitForFunction(()=>probe.freeRideJumpReady&&probe.metres>=69,null,{timeout:8000});
          if(line==='button'){
            const box=await page.locator('#freeRideJump').boundingBox();
            await touch.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[steeringTouch,{x:box.x+box.width/2,y:box.y+box.height/2,id:2}]});
          }else{
            await touch.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{...steeringTouch,y:447}]});
          }
          await page.waitForFunction(() => probe.air && probe.metres >= 86 && probe.metres < 110, null, {timeout:12000});
          if(guest)await shot('skier-flip');
        }else if(line==='fall'){
          await page.waitForFunction(()=>probe.freeRideInCrevasse,null,{timeout:8000});
          await page.waitForTimeout(410);
          await shot('cave-fall');
        }
        await at(113);
        const landing = await page.evaluate(() => probe);
        assert(!landing.air&&!landing.freeRideInCrevasse,'Expected return to snow');
        assert.equal(landing.helmets,line==='fall'?0:1);
        if(line==='button'||line==='swipe')assert(landing.tricks>=1,'Expected a completed trick');
        await touch.send('Input.dispatchTouchEvent', {type:'touchEnd', touchPoints:[]});
      }
      await click('pause');
      await phase('pause', hero);
      await click('exitPaused');
      await phase('over', hero);
      const result = await page.evaluate(() => probe);
      assert.equal(result.rankedResult, false);
      assert.match(await page.locator('#rankStatus').innerText(), /вне рейтинга/i);
      samples.push({hero,guest,line,seed,coins:result.coins,helmets:result.helmets,tricks:result.tricks,stoppedAt:result.metres});
      await click('exitResult');
      await phase('menu', hero);
      assert.equal(await save(), profile, 'Free Ride changed the saved profile for ' + hero);
    }
    // Inspect both lines from the same game camera, at the exact metre before the first cornice.
    await page.locator('[data-panel="settings"]').tap();
    const inspectionSeed = page.getByLabel('Номер трассы');
    await inspectionSeed.fill(String(seed));
    await inspectionSeed.press('Tab');
    await page.getByRole('button', {name:'Осмотр трассы', exact:true}).tap();
    await phase('review', 'bear');
    await page.locator('#reviewRide').selectOption('freeride');
    await page.locator('#reviewAt').fill('64');
    await page.locator('#reviewAt').press('Enter');
    await page.waitForFunction(n => probe.courseMode === 'freeride' && probe.seed === n && probe.metres === 64, seed);
    await click('reviewFold');
    await shot('approach-64m');
    await click('reviewExit');
    await phase('menu');
    await click('play');
    await at(7);
    assert.equal(await page.evaluate(() => probe.courseMode), 'normal');
    assert.equal(await page.evaluate(() => probe.freeRideSkier), false);
    assert(samples.some(sample => sample.coins > 0), 'No mandarin collected to check wallet isolation');
    assert.deepEqual(posts, []);
    assert.deepEqual(errors, []);
    console.log('FREE RIDE WEB PASS', JSON.stringify(samples));
  } finally {
    await browser.close();
  }
}
main().catch(error => {console.error(error); process.exitCode=1;});
