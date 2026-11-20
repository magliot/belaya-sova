// Capture the real Unity Web camera through the game's inspection controls.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {performance}=require('node:perf_hooks');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE || 'playwright');
const url=new URL(process.argv[2] || 'http://127.0.0.1:8784/unity-test/');
url.searchParams.set('seed','1911');
const out=path.resolve('course-checks','mountain-race','weather-review');
const shots=[
  {name:'cannon',metres:68,lane:-4,rate:'.25',seconds:2.6},
  {name:'blizzard',metres:10104,lane:0,rate:'0',seconds:1.6},
  {name:'plough',metres:15216,lane:-2,rate:'.25',seconds:2.8},
  {name:'fireworks',metres:17525,lane:0,rate:'0',seconds:5.2}
];
(async()=>{
  fs.mkdirSync(out,{recursive:true});
  const browser=await chromium.launch();
  try{
    const page=await browser.newPage({viewport:{width:390,height:780},hasTouch:true});
    const errors=[];page.on('pageerror',e=>errors.push(e.message));
    page.on('console',m=>{if(/shader error|NullReferenceException|ArgumentException|Can't add component/i.test(m.text()))errors.push(m.text());});
    await page.route('https://script.google.com/**',r=>r.abort());
    await page.goto(url.href,{waitUntil:'domcontentloaded'});
    await page.locator('#play').waitFor({timeout:180000});await page.locator('[data-hero="owl"]').tap();
    await page.locator('[data-panel="settings"]').tap();
    await page.locator('#panel').getByRole('button',{name:'Осмотр трассы',exact:true}).tap();
    const set=(id,n)=>page.locator(id).evaluate((el,v)=>{el.value=v;el.dispatchEvent(new Event('input',{bubbles:true}));},n);
    let serial=0;
    for(const shot of shots){
      await page.locator('#reviewRate').selectOption('0');
      await set('#seek',shot.metres);await set('#reviewLane',shot.lane);
      await page.waitForFunction(n=>Number(document.getElementById('reviewMetres').textContent)===n,shot.metres);
      await page.waitForTimeout(280);await page.locator('#reviewRate').selectOption(shot.rate);await page.locator('#reviewFold').tap();
      const clock=performance.now(),fps=12,frames=Math.ceil(shot.seconds*fps);
      for(let i=0;i<frames;i++){
        await page.waitForTimeout(Math.max(0,Math.round(clock+1000*i/fps-performance.now())));
        await page.screenshot({path:path.join(out,'frame'+String(serial++).padStart(4,'0')+'.jpg'),type:'jpeg',quality:93});
        const poster=shot.name==='fireworks'?Math.round(1.25*fps):Math.floor(frames/2);
        if(i===poster)await page.screenshot({path:path.join(out,shot.name+'.png')});
      }
      console.log(shot.name,shot.metres,'to',await page.locator('#reviewMetres').textContent(),'m,',frames,'frames');
      await page.locator('#reviewFold').tap();
    }
    assert.deepEqual(errors,[]);console.log(out,serial,'frames at 12 fps');
  }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
