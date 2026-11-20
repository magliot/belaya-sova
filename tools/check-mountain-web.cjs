// Run the full compiled Unity game through the ordinary UI, including the Telegram dispatcher.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const {chromium} = require(process.env.DANCE_BROWSER_MODULE || 'playwright');
const root = new URL(process.argv[2] || 'http://127.0.0.1:8784/');
const hardware=process.argv.includes('--hardware');
const out = path.resolve('course-checks','mountain-race',root.hostname+(hardware?'-gpu':''));
fs.mkdirSync(out,{recursive:true});
async function main(){
  const browser=await chromium.launch({args:hardware?[]:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  try{
    const page=await browser.newPage({viewport:{width:390,height:780},hasTouch:true});
    const errors=[],artifacts=new Set(),writes=[];
    page.on('pageerror',e=>{errors.push(String(e));console.error('page',e.message);});
    page.on('console',m=>{if(m.type()==='error'||/Exception:|MOUNTAIN RACE READY/.test(m.text()))console.log(m.text().slice(0,420));});
    page.on('response',r=>{if(r.ok()&&r.url().includes('/unity-test/Build/'))artifacts.add(new URL(r.url()).pathname);});
    await page.route('https://script.google.com/**',route=>{
      if(page.url().includes('/unity-test/')&&route.request().method()==='POST')writes.push(route.request().url());
      return route.abort();
    });
    await page.addInitScript(()=>{
      if(!location.pathname.includes('/unity-test/'))return;
      let callback;window.raceEvents=[];
      Object.defineProperty(window,'onMountainState',{get(){return callback;},set(fn){
        callback=s=>{window.lastRace=s;window.raceEvents.push([s.phase,s.mode,s.hero,s.metres,s.cue,s.message]);
          if(window.raceEvents.length>2300)window.raceEvents.shift();fn(s);};
      }});
    });
    const launch=new URL(root);
    launch.searchParams.set('seed','1911');
    launch.hash=new URLSearchParams({tgWebAppStartParam:'unitytest',tgWebAppPlatform:'android',tgWebAppVersion:'8.0'}).toString();
    await page.goto(launch.href,{waitUntil:'commit'});
    await page.waitForURL(u=>u.pathname.endsWith('/unity-test/'),{timeout:60000});
    await page.locator('#play').waitFor({timeout:210000});
    const waitState=(phase,hero)=>page.waitForFunction(({phase,hero})=>window.lastRace?.phase===phase&&(!hero||lastRace.hero===hero),{phase,hero},{timeout:90000});
    for(const hero of ['bear','owl']){
      await page.locator('[data-hero="'+hero+'"]').tap();
      await waitState('menu',hero);
      await page.waitForTimeout(220);
      await page.screenshot({path:path.join(out,'menu-'+hero+'.png')});
      await page.locator('#play').tap();
      await waitState('play',hero);
      await page.waitForFunction(()=>lastRace.metres>=22,null,{timeout:60000});
      await page.screenshot({path:path.join(out,'ordinary-'+hero+'.png')});
      await page.locator('#pause').tap();await waitState('pause',hero);
      const paused=await page.evaluate(()=>lastRace.metres);
      await page.waitForTimeout(400);
      assert.equal(await page.evaluate(()=>lastRace.metres),paused);
      await page.locator('#resume').tap();await waitState('play',hero);
      await page.locator('#pause').tap();await waitState('pause',hero);
      await page.locator('#restartPaused').tap();await waitState('play',hero);
      assert(await page.evaluate(()=>lastRace.metres<10));
      if(hero==='bear'){
        await page.locator('#pause').tap();await waitState('pause',hero);
        await page.locator('#exitPaused').tap();await waitState('menu',hero);
      }
    }
    // Take a real line along the edge to exercise a collision and the result screen.
    const cdp=await page.context().newCDPSession(page);
    await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:28,y:555}]});
    for(const x of [48,65,80,78,63,50,70,95,115,135,175,225,280,325,365]){
      await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x,y:555}]});
      await page.waitForTimeout(50);
    }
    await page.waitForTimeout(950);await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
    await waitState('over','owl');
    await page.screenshot({path:path.join(out,'result.png')});
    await page.locator('#again').tap();await waitState('play','owl');
    const metrics=await page.evaluate(()=>{
      const gl=document.querySelector('#slope').getContext('webgl2'),info=gl?.getExtension('WEBGL_debug_renderer_info');
      return {...MountainMetrics.report(),renderer:info?gl.getParameter(info.UNMASKED_RENDERER_WEBGL):null};
    });
    assert(metrics.measuredFrames>0&&metrics.dragSamples>0);
    fs.writeFileSync(path.join(out,'desktop-chrome.json'),JSON.stringify({...metrics,environment:'Windows Chrome, touch emulation'},null,2));
    await page.locator('#pause').tap();await waitState('pause','owl');await page.locator('#exitPaused').tap();await waitState('menu','owl');
    await page.locator('[data-panel="more"]').tap();
    await page.locator('#panel').getByRole('button',{name:'Настройки',exact:true}).tap();
    await page.locator('#panel').getByRole('button',{name:'Осмотр трассы',exact:true}).tap();
    await waitState('review','owl');
    for(const [name,metre] of [['forest-arch',2472],['park',7630],['summit',10104],['night',15105],['holiday',17525]]){
      await page.locator('#seek').evaluate((el,value)=>{el.value=value;el.dispatchEvent(new Event('input',{bubbles:true}));},metre);
      await page.waitForFunction(n=>Math.abs(lastRace.metres-n)<2,metre,{timeout:30000});
      await page.locator('#reviewFold').tap();
      await page.waitForTimeout(150);await page.screenshot({path:path.join(out,name+'.png')});
      await page.locator('#reviewFold').tap();
    }
    assert(artifacts.size>=4,'Incomplete Unity Web load');
    await page.goto(root.href,{waitUntil:'domcontentloaded'});
    await page.waitForFunction(()=>window.__g?.()?.mode==='menu',null,{timeout:120000});
    await page.locator('#bPlay').tap();await page.waitForFunction(()=>window.__g?.()?.mode==='play'&&__g().y>15,null,{timeout:120000});
    assert.equal(new URL(page.url()).pathname,root.pathname);
    assert.deepEqual(errors,[]);assert.deepEqual(writes,[]);
    console.log('PASS actual Web player: both heroes, normal run, touch, collision, pause/resume/replay, stage views, isolated score, default Three.js route. Assets:',artifacts.size);
    console.log('Desktop emulation',JSON.stringify(metrics));
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
