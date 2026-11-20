// Exercise the two in-app links as well as a Telegram launch on the existing "return to play" URL.
const assert=require('node:assert/strict');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const root=new URL(process.argv[2]||'http://127.0.0.1:8784/');
async function main(){
  const browser=await chromium.launch();
  try{
    const page=await browser.newPage({hasTouch:true,viewport:{width:390,height:780}});
    const errors=[];
    page.on('pageerror',e=>errors.push(e.message));
    const controls=page.getByRole('link',{name:'Полная игра · Unity',exact:true});
    async function startFull(label){
      await page.waitForURL(url=>url.pathname.endsWith('/unity-test/'),{timeout:60000});
      await page.locator('#play').waitFor({timeout:150000});
      await page.locator('[data-hero="owl"]').tap();await page.locator('#play').tap();
      await page.waitForFunction(()=>parseInt(document.getElementById('metres').textContent)>=6,null,{timeout:60000});
      console.log('PASS',label);
    }
    await page.goto(root.href,{waitUntil:'domcontentloaded'});
    await controls.tap();await startFull('Three.js menu to full Unity');
    await page.goto(new URL('unity-slope/?v=2026-11-20g',root).href,{waitUntil:'domcontentloaded'});
    await controls.tap();await startFull('old slalom loading screen to full Unity');
    const link=new URL('?preview=play&tgWebAppStartParam=unity',root);
    link.hash='/play?'+new URLSearchParams({tgWebAppData:new URLSearchParams({start_param:'unitytest_20g'}).toString(),
      tgWebAppVersion:'8.0',tgWebAppPlatform:'android'});
    await page.goto(link.href,{waitUntil:'commit'});await startFull('fresh test payload on a legacy return address');
    await page.goto(new URL('unity-slope/?v=2026-11-20d',root).href,{waitUntil:'commit'});
    await startFull('old cached menu href to full Unity');
    await page.goto(root.href,{waitUntil:'domcontentloaded'});await page.locator('#bPlay').tap();
    await page.waitForFunction(()=>window.__g?.()?.mode==='play'&&__g().y>80,null,{timeout:90000});
    assert.equal(new URL(page.url()).pathname,root.pathname);assert.deepEqual(errors,[]);
    console.log('PASS ordinary Telegram reopening starts Three.js');
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e.message);process.exitCode=1;});
