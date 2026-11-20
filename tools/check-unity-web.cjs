// Start the static docs server first. Pass its base URL or the deployed Pages URL.
const assert=require('node:assert/strict');
const fs=require('node:fs');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const base=new URL(process.argv[2]||'http://127.0.0.1:8766/');
async function main(){
  const browser=await chromium.launch({args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  try{
    const page=await browser.newPage({viewport:{width:375,height:667},deviceScaleFactor:1,hasTouch:true});
    const errors=[],assets=new Set();
    page.on('pageerror',e=>errors.push(e.message));
    page.on('response',r=>{if(r.ok()&&r.url().includes('/unity-slope/Build/'))assets.add(r.url().split('/').pop());});
    const entry=new URL(base);
    entry.hash=new URLSearchParams({tgWebAppData:'start_param=unity',tgWebAppPlatform:'android',tgWebAppVersion:'8.0'}).toString();
    await page.route('https://script.google.com/**',r=>r.abort());
    const ready=page.waitForEvent('console',{predicate:m=>m.text().includes('OWL SLOPE READY'),timeout:150000});
    await Promise.all([ready,(async()=>{
      await page.goto(entry.href,{waitUntil:'commit'});
      await page.waitForURL(u=>u.pathname.endsWith('/unity-slope/'),{timeout:45000});
    })()]);
    const canvas=page.locator('#slope');await canvas.waitFor();
    await page.waitForFunction(()=>document.querySelector('#loading')?.hidden);
    fs.mkdirSync('course-checks/unity-web',{recursive:true});
    const go=page.getByRole('button',{name:'Поехали',exact:true});
    await go.waitFor();
    assert.match(await page.locator('#objective').innerText(),/мандарин в прыжке/);
    const bounds=await canvas.boundingBox();
    await page.screenshot({path:'course-checks/unity-web/start.png'});
    await canvas.screenshot({path:'course-checks/unity-web/control-after.png',style:'article,nav,#hud{visibility:hidden}'});
    await go.tap();
    await page.getByText('Ворота слева',{exact:true}).waitFor();
    await page.getByRole('button',{name:'Пауза'}).tap();
    await page.getByRole('button',{name:'Продолжить'}).tap();
    await page.touchscreen.tap(bounds.x+bounds.width*.72,bounds.y+bounds.height*.24);
    await page.keyboard.down('ArrowRight');
    try{await page.keyboard.press('Space');await page.waitForTimeout(350);}finally{await page.keyboard.up('ArrowRight');}
    await page.screenshot({path:'course-checks/unity-web/ride.png'});
    const replay=page.getByRole('button',{name:'Ещё заезд'});
    await replay.waitFor({timeout:120000});
    assert.match(await page.locator('#objective').innerText(),/^Ворота: [0-3] из 3\. Мандарин пропущен\.$/);
    await page.screenshot({path:'course-checks/unity-web/finish.png'});
    await replay.tap();
    await page.getByText('Ворота слева',{exact:true}).waitFor();
    const touch=await page.context().newCDPSession(page);
    async function at(remaining){
      await page.waitForFunction(limit=>{
        const metres=document.querySelector('#distance').textContent.match(/· (\d+) м/);
        return metres&&Number(metres[1])<=limit;
      },remaining,{polling:'raf',timeout:30000});
    }
    const startX=.71, y=bounds.y+bounds.height*.70;
    async function finger(type,offset=0){
      // Default sensitivity 4. One continuous drag can choose all three lines.
      const x=bounds.x+bounds.width*(startX+offset/(27*.79));
      await touch.send('Input.dispatchTouchEvent',{type,touchPoints:type==='touchEnd'?[]:[{x,y}]});
    }
    await finger('touchStart');await finger('touchMove',-3.4);
    await at(70);await finger('touchMove',3.4);
    await at(46);await finger('touchMove',0);
    await at(38);await finger('touchEnd');
    await page.touchscreen.tap(bounds.x+bounds.width*.62,y);
    await page.getByText('Грэб!',{exact:true}).waitFor({timeout:4000});
    await page.screenshot({path:'course-checks/unity-web/grab.png'});
    await at(30);await finger('touchStart');await finger('touchMove',-2.8);
    await at(13);await finger('touchEnd');
    await replay.waitFor({timeout:30000});
    assert.equal(await page.locator('#objective').innerText(),'Ворота: 3 из 3. Мандарин пойман!');
    await page.screenshot({path:'course-checks/unity-web/finish.png'});
    assert([...assets].some(n=>n.includes('.wasm'))&&[...assets].some(n=>n.includes('.data')),'Missing Unity player resources');
    await page.locator('#menu-link').tap();
    await page.waitForFunction(()=>window.__g?.()?.mode==='menu',null,{timeout:60000});
    // Ordinary reopen in the same tab, without the explicit menu override. The real SDK still has the previous Unity initData.
    await page.goto(base.href,{waitUntil:'domcontentloaded'});
    await page.waitForFunction(()=>window.__g?.()?.mode==='menu',null,{timeout:60000});
    assert.equal(await page.evaluate(()=>Telegram.WebApp.initDataUnsafe.start_param),'unity');
    assert(await page.getByRole('link',{name:'Склон Unity'}).isVisible());
    await page.locator('#bPlay').tap();
    await page.waitForFunction(()=>window.__g?.()?.mode==='play'&&window.__g().y>10,null,{timeout:90000});
    assert.equal(new URL(page.url()).pathname,base.pathname);
    assert.deepEqual(errors,[]);
    const cold=await browser.newPage({viewport:{width:375,height:667},hasTouch:true});
    await cold.addInitScript(()=>{
      if(sessionStorage.getItem('graphics-retried'))return;
      sessionStorage.setItem('graphics-retried','1');
      const get=HTMLCanvasElement.prototype.getContext;
      HTMLCanvasElement.prototype.getContext=function(type,...args){return /webgl/i.test(type)?null:get.call(this,type,...args);};
    });
    await cold.route('https://script.google.com/**',r=>r.abort());
    await cold.goto(base.href,{waitUntil:'domcontentloaded'});
    await cold.locator('#assetRetry').waitFor();
    assert.match(await cold.locator('#assetMessage').innerText(),/включить 3D/);
    await cold.locator('#assetRetry').tap();
    await cold.waitForFunction(()=>window.__g?.()?.mode==='menu',null,{timeout:60000});
    console.log('PASS relative drag, ramp takeoff, air tap, 3/3 and fruit, pause, finish, replay, ordinary play after cached Unity, and WebGL retry');
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
