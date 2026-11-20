// Reach a moving Unity race from the ordinary bot, browser and former pilot URLs.
const assert=require('node:assert/strict');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const root=new URL(process.argv[2]||'http://127.0.0.1:8784/');
async function main(){
  const browser=await chromium.launch({args:process.argv.includes('--software')?['--use-angle=swiftshader','--enable-unsafe-swiftshader']:[]});
  try{
    const page=await browser.newPage({viewport:{width:390,height:780},hasTouch:true});
    const resources=[],errors=[];
    page.on('request',r=>resources.push(new URL(r.url()).pathname));
    page.on('pageerror',e=>errors.push(String(e)));
    await page.route('https://script.google.com/**',r=>r.abort());
    for(const [link,entry,channel] of [['','unity','production'],['?preview=play','unity','production'],
      ['#tgWebAppStartParam=unitytest_20g','unity-test','test'],['unity-slope/?v=2026-11-20d','unity-test','test']]){
      await page.goto(new URL(link,root).href,{waitUntil:'commit'});
      await page.waitForURL(u=>u.pathname===root.pathname+entry+'/',{timeout:60000});
      await page.locator('#play').waitFor({timeout:180000});
      assert.equal(await page.evaluate(()=>window.mountainRelease.channel),channel);
      await page.locator('#play').tap();
      await page.waitForFunction(()=>parseInt(document.getElementById('metres').textContent,10)>=6,null,{timeout:60000});
      console.log('PASS',link||'ordinary start',entry,channel);
    }
    assert(!resources.some(p=>p.includes('/vendor/three/')));assert.deepEqual(errors,[]);
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
