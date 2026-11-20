// Start the static docs server first. Pass its base URL or the deployed Pages URL.
const assert=require('node:assert/strict');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const base=new URL(process.argv[2]||'http://127.0.0.1:8766/');
async function main(){
  const browser=await chromium.launch({args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
  try{
    const page=await browser.newPage({viewport:{width:375,height:667},deviceScaleFactor:1,hasTouch:true});
    const errors=[],assets=new Set();
    page.on('pageerror',e=>errors.push(e.message));
    page.on('response',r=>{if(r.ok()&&r.url().includes('/unity-slope/Build/'))assets.add(r.url().split('/').pop());});
    const ready=page.waitForEvent('console',{predicate:m=>m.text().includes('OWL SLOPE READY'),timeout:150000});
    await Promise.all([ready,(async()=>{
      const entry=new URL(base);entry.searchParams.set('tgWebAppStartParam','unity');
      await page.goto(entry.href,{waitUntil:'commit'});
      await page.waitForURL(u=>u.pathname.endsWith('/unity-slope/'),{timeout:45000});
    })()]);
    const canvas=page.locator('#slope');await canvas.waitFor();
    await page.waitForFunction(()=>document.querySelector('#loading')?.hidden);
    await canvas.focus();await page.waitForTimeout(250);
    const first=await canvas.screenshot({path:'course-checks/unity-web/start.png'});const bounds=await canvas.boundingBox();
    await page.touchscreen.tap(bounds.x+bounds.width*.72,bounds.y+bounds.height*.24);
    await page.keyboard.down('ArrowRight');
    try{await page.keyboard.press('Space');await page.waitForTimeout(350);}finally{await page.keyboard.up('ArrowRight');}
    assert(!first.equals(await canvas.screenshot()),'The Unity canvas did not change after steering and jumping');
    assert([...assets].some(n=>n.includes('.wasm'))&&[...assets].some(n=>n.includes('.data')),'Missing Unity player resources');
    assert.deepEqual(errors,[]);console.log('PASS Unity startup, deep link, WASM/data and moving phone-sized canvas:',[...assets].join(', '));
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
