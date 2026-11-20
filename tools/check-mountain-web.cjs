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
    page.on('console',m=>{
      const line=m.text().slice(0,420);
      if(/Can't add component|(?:NullReference|MissingReference|InvalidOperation|IndexOutOfRange)Exception:|Shader error/.test(line))errors.push(line);
      if(m.type()==='error'||/Exception:|MOUNTAIN RACE READY/.test(line))console.log(line);
    });
    page.on('response',r=>{if(r.ok()&&r.url().includes('/unity-test/Build/'))artifacts.add(new URL(r.url()).pathname);});
    await page.route('https://script.google.com/**',route=>{
      if(page.url().includes('/unity-test/')&&route.request().method()==='POST')writes.push(route.request().url());
      return route.abort();
    });
    await page.addInitScript(()=>{
      if(!/\/unity(?:-test)?\/$/.test(location.pathname))return;
      let callback;window.raceEvents=[];
      Object.defineProperty(window,'onMountainState',{get(){return callback;},set(fn){
        callback=s=>{window.lastRace=s;window.raceEvents.push([s.phase,s.mode,s.hero,s.metres,s.cue,s.message]);
          if(window.raceEvents.length>2300)window.raceEvents.shift();fn(s);};
      }});
    });
    const launch=new URL(root);
    launch.searchParams.set('seed','1911');
    launch.hash=new URLSearchParams({tgWebAppStartParam:'unitytest',tgWebAppPlatform:'android',tgWebAppVersion:'8.0'}).toString();
    async function openMenu(){
      await page.goto(launch.href,{waitUntil:'commit'});
      await page.waitForURL(u=>u.pathname.endsWith('/unity-test/'),{timeout:60000});
      await page.locator('#play').waitFor({timeout:210000});
    }
    const waitState=(phase,hero)=>page.waitForFunction(({phase,hero})=>window.lastRace?.phase===phase&&(!hero||lastRace.hero===hero),{phase,hero},{timeout:90000});
    for(const hero of ['bear','owl']){
      await openMenu();
      await page.locator('[data-hero="'+hero+'"]').tap();
      await waitState('menu',hero);
      await page.waitForTimeout(220);
      await page.screenshot({path:path.join(out,'menu-'+hero+'.png')});
      await page.locator('#play').tap();
      await waitState('play',hero);
      await page.waitForFunction(()=>lastRace.metres>=1,null,{timeout:60000});
      await page.locator('#pause').tap();await waitState('pause',hero);
      const paused=await page.evaluate(()=>lastRace.metres);
      await page.waitForTimeout(400);
      assert.equal(await page.evaluate(()=>lastRace.metres),paused);
      await page.locator('#resume').tap();await waitState('play',hero);
      await page.locator('#pause').tap();await waitState('pause',hero);
      await page.locator('#restartPaused').tap();await waitState('play',hero);
      assert(await page.evaluate(()=>lastRace.metres<10));
      // Capture a live descent after the control checks. Software rendering can advance
      // the rider all the way to an obstacle while Playwright captures the canvas.
      if(hero==='owl'){
        await page.waitForFunction(()=>lastRace.phase==='play'&&lastRace.coins>=1,null,{timeout:60000});
        await page.screenshot({path:path.join(out,'mandarin.png')});
      }
      await page.waitForFunction(()=>lastRace.phase==='play'&&lastRace.metres>=22,null,{timeout:60000});
      await page.screenshot({path:path.join(out,'ordinary-'+hero+'.png')});
    }
    // Take a real line along the edge to exercise a collision and the result screen.
    await openMenu();
    await waitState('menu','owl');
    await page.locator('#play').tap();await waitState('play','owl');
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
    await page.locator('[data-panel="settings"]').tap();
    await page.locator('#panel').getByRole('button',{name:'Осмотр трассы',exact:true}).tap();
    await waitState('review','owl');
    const weatherFrames={};
    await page.locator('#reviewEffect').selectOption('coffee');
    await page.waitForFunction(()=>lastRace.boostSeconds===5 && document.querySelector('#rideStatus').textContent.includes('5.0 с'));
    await page.locator('#reviewEffect').selectOption('avalanche');
    await page.waitForFunction(()=>lastRace.avalanche>0 && !document.querySelector('#avalancheMeter').hidden);
    await page.locator('#reviewEffect').selectOption('normal');
    await page.locator('#reviewRide').selectOption('sled');
    await page.waitForFunction(()=>lastRace.courseMode==='sled' && document.querySelector('#rideStatus').textContent==='Снеговики 0 · подарки 0');
    await page.locator('#reviewRide').selectOption('tubing');
    await page.waitForFunction(()=>lastRace.courseMode==='tubing' && document.querySelector('#rideStatus').textContent==='Открытый склон · ворота 0');
    await page.locator('#reviewRide').selectOption('normal');
    await page.waitForFunction(()=>lastRace.courseMode==='normal');
    for(const [name,metre] of [['tube',29],['cannon',78],['coffee',105],['magnet',653],['forest-arch',2472],['forest-canopy',2510],
      ['training-mound',205],['uphill-groomer',443],['tube-moguls',5150],['ramp',7655],['park',7630],['summit',10104],
      ['summit-approach',9910],['black-gate',12507],['night-approach',14910],['night',15050],['night-slalom',15082],
      ['groomer',15232],['holiday',17525],['holiday-slalom',17618],['forest-fork',22620],['park-lap-two',27643],['night-fork',35120]]){
      await page.locator('#seek').evaluate((el,value)=>{el.value=value;el.dispatchEvent(new Event('input',{bubbles:true}));},metre);
      await page.waitForFunction(n=>Math.abs(lastRace.metres-n)<2,metre,{timeout:30000});
      if(metre>=20000)assert.equal(await page.evaluate(()=>lastRace.stage),Math.floor(metre/2500)%8);
      await page.locator('#reviewFold').tap();
      await page.waitForTimeout(850);const frame=await page.screenshot({path:path.join(out,name+'.png')});
      if(['summit','park','night','night-slalom'].includes(name))weatherFrames[name]=frame.toString('base64');
      if(name==='summit'){
        await page.waitForTimeout(350);
        weatherFrames.gust=(await page.screenshot({path:path.join(out,'summit-gust.png')})).toString('base64');
      }
      await page.locator('#reviewFold').tap();
    }
    // Use the phone controls to ride, restart and return to the selected point on lap two.
    const profileBefore=await page.evaluate(()=>JSON.stringify({...localStorage}));
    await page.locator('#reviewPlay').tap();
    await page.waitForFunction(()=>lastRace.phase==='play'&&lastRace.stage===6&&lastRace.metres>35124);
    await page.locator('#pause').tap();await waitState('pause','owl');
    await page.locator('#restartPaused').tap();
    await page.waitForFunction(()=>lastRace.phase==='play'&&lastRace.metres>=35120&&lastRace.metres<35160);
    await page.locator('#pause').tap();await waitState('pause','owl');
    await page.locator('#exitPaused').tap();await waitState('review','owl');
    assert.equal(await page.evaluate(()=>lastRace.metres),35120);
    assert.equal(await page.evaluate(()=>JSON.stringify({...localStorage})),profileBefore,'inspection practice modified the saved profile');
    // The summit encounter uses the original stationary Yeti mesh on the left edge.
    // Compare a brush against its arm and a safe pass with the same camera, seed and approach.
    for(const [lane,helmets] of [[-7.75,0],[-5,1]]){
      await page.locator('#reviewAt').fill('11782');await page.locator('#reviewAt').press('Enter');
      await page.locator('#reviewLane').evaluate((e,x)=>{e.value=x;e.dispatchEvent(new Event('input',{bubbles:true}));},lane);
      await page.waitForFunction(()=>lastRace.phase==='review'&&lastRace.metres===11782);
      await page.locator('#reviewPlay').tap();
      await page.waitForFunction(()=>lastRace.phase==='play'&&lastRace.metres>11790,null,{timeout:20000});
      assert.equal(await page.evaluate(()=>lastRace.helmets),helmets,'summit Yeti at lane '+lane);
      await page.locator('#pause').tap();await waitState('pause','owl');
      await page.locator('#exitPaused').tap();await waitState('review','owl');
    }
    const weather=await page.evaluate(async images=>{
      const decoded={};let width,height;
      for(const [name,source] of Object.entries(images)){
        const img=new Image();img.src='data:image/png;base64,'+source;await img.decode();
        width=img.width;height=img.height;
        const canvas=document.createElement('canvas');canvas.width=width;canvas.height=height;
        const ctx=canvas.getContext('2d');ctx.drawImage(img,0,0);decoded[name]=ctx.getImageData(0,0,width,height).data;
      }
      let changed=0,count=0,day=0,night=0,road=0;
      for(let y=Math.floor(.29*height);y<.75*height;y++)for(let x=Math.floor(.08*width);x<.92*width;x++){
        const i=4*(y*width+x);count++;
        if([0,1,2].some(c=>Math.abs(decoded.summit[i+c]-decoded.gust[i+c])>24))changed++;
        if(y>.56*height&&x>.22*width&&x<.78*width){
          const luma=img=>.213*img[i]+.715*img[i+1]+.072*img[i+2];
          day+=luma(decoded.park);night+=luma(decoded.night);road++;
        }
      }
      // At 15,082 m both blue gate sleeves should be visible over 36 m ahead, above the snow.
      const gate=decoded['night-slalom'],sleeves=[];
      for(let x=Math.floor(.2*width);x<.78*width;x++){
        let lit=0;
        for(let y=Math.floor(.38*height);y<.50*height;y++){
          const i=4*(y*width+x),r=gate[i],g=gate[i+1],b=gate[i+2];
          if(g>75&&b>95&&g>1.7*r&&b>1.8*r)lit++;
        }
        if(lit>=4)sleeves.push(x);
      }
      const gateSpan=sleeves.length?(sleeves[sleeves.length-1]-sleeves[0])/width:0;
      return {summitMovingPixels:changed,area:count,movingFraction:changed/count,parkLuma:day/road,nightLuma:night/road,gateSpan};
    },weatherFrames);
    console.log('Stationary chase camera: storm motion and road luminance',weather);
    assert(weather.movingFraction>.003,'No visible blowing snow while the summit camera is held still');
    assert(weather.nightLuma<.9*weather.parkLuma,'The night run is as bright as the daytime piste');
    assert(weather.gateSpan>.07,'Both night gate posts should be luminous from the chase camera');
    fs.writeFileSync(path.join(out,'weather.json'),JSON.stringify(weather,null,2));
    assert(artifacts.size>=4,'Incomplete Unity Web load');
    await page.goto(root.href,{waitUntil:'commit'});
    await page.waitForURL(u=>u.pathname===root.pathname+'unity/',{timeout:60000});
    await page.locator('#play').waitFor({timeout:210000});
    assert.equal(await page.evaluate(()=>mountainRelease.channel),'production');
    await page.locator('#play').tap();await page.waitForFunction(()=>lastRace.phase==='play'&&lastRace.metres>15,null,{timeout:60000});
    assert.equal(await page.evaluate(()=>typeof window.__g),'undefined');
    assert.deepEqual(errors,[]);assert.deepEqual(writes,[]);
    console.log('PASS compiled Unity: both heroes, touch, collision, pause/resume/replay, stage views, isolated score, default bot route. Assets:',artifacts.size);
    console.log('Desktop emulation',JSON.stringify(metrics));
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
