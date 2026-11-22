// Compare the actual snowboard rendered by WebGL against stock and purchased reference frames.
const assert=require('node:assert/strict');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const url=new URL(process.argv[2]||'http://127.0.0.1:8798/unity-test/?seed=1911');
assert(url.pathname.endsWith('/unity-test/'),'Use the isolated test profile');

async function main(){
  const browser=await chromium.launch();
  try{
    const page=await browser.newPage({viewport:{width:390,height:780},hasTouch:true});
    await page.route('**/*',r=>r.request().method()==='POST'?r.abort():r.continue());
    await page.addInitScript(()=>{
      const key='bsova_unity_test_v1';
      if(!localStorage.getItem(key))localStorage.setItem(key,JSON.stringify({schema:1,uid:0,data:{hero:'owl',coins:495}}));
      let handler;
      Object.defineProperty(window,'onMountainState',{get:()=>handler,set:fn=>{handler=s=>{window.rider=s;fn(s);};}});
    });
    const phase=p=>page.waitForFunction(p=>window.rider?.phase===p,p,{timeout:80000});
    const tap=id=>page.locator('#'+id).tap();
    const hero=h=>page.locator('[data-hero="'+h+'"]').tap();
    const openShop=()=>page.locator('[data-panel="shop"]').tap();
    // The planted deck is fixed on the left of the menu camera. The bindings and background
    // are identical in the two references, so only pixels of the changed finish are measured.
    async function deck(){
      await page.locator('#play').waitFor({timeout:100000});
      await page.waitForFunction(()=>rider.phase==='menu'&&rider.hero==='owl');
      await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));
      return (await page.screenshot({clip:{x:12,y:195,width:118,height:236}})).toString('base64');
    }
    await page.goto(url.href,{waitUntil:'commit'});
    const stock=await deck();
    await openShop();await page.getByRole('button',{name:/Купить свой комплект/}).tap();
    await page.getByRole('button',{name:/Настройка · 0\/2/}).tap();await tap('closePanel');
    const owned=await deck();
    async function check(label,wantOwned=true){
      const frame=await deck();
      const result=await page.evaluate(async images=>{
        const read=async src=>{
          const img=new Image();img.src='data:image/png;base64,'+src;await img.decode();
          const c=new OffscreenCanvas(img.width,img.height).getContext('2d');c.drawImage(img,0,0);
          return c.getImageData(0,0,img.width,img.height).data;
        };
        const [a,b,c]=await Promise.all(images.map(read));
        let total=0,owned=0;
        for(let i=0;i<a.length;i+=4){
          const dif=(p,q)=>Math.abs(p[i]-q[i])+Math.abs(p[i+1]-q[i+1])+Math.abs(p[i+2]-q[i+2]);
          if(dif(a,b)<80)continue;
          total++;if(dif(c,b)<dif(c,a))owned++;
        }
        return {total,match:owned/total};
      },[stock,owned,frame]);
      const labelText=await page.locator('[data-hero="owl"] small').innerText();
      console.log('DECK',label,JSON.stringify(result),labelText);
      assert.match(labelText,wantOwned?/Салют/:/Обычная/);
      assert(result.total>120,'Purchase must visibly change the actual deck');
      assert(wantOwned?result.match>.88:result.match<.12,'Wrong 3D deck after '+label);
    }
    await check('purchase');
    await tap('play');await phase('play');await page.waitForFunction(()=>rider.metres>=35);
    await tap('pause');await tap('exitPaused');await phase('over');await tap('exitResult');await check('finished ride');
    await openShop();await page.getByRole('button',{name:'Обычная доска'}).tap();await tap('closePanel');await check('explicit stock',false);
    await openShop();await page.getByRole('button',{name:/Салют.*надеть/}).tap();await tap('closePanel');
    await hero('bear');await hero('owl');await check('switch hero');
    await page.locator('[data-panel="settings"]').tap();await page.getByRole('button',{name:'Осмотр трассы',exact:true}).tap();
    await phase('review');await tap('reviewPlay');await phase('play');await tap('pause');await tap('exitPaused');
    await phase('review');await tap('reviewExit');await check('practice ride');
    await page.reload({waitUntil:'commit'});await check('reload');
    assert.equal(await page.locator('#bank').innerText(),await page.evaluate(()=>String(JSON.parse(localStorage.getItem('bsova_unity_test_v1')).data.coins)));
    await openShop();await page.getByRole('button',{name:/Настройка · 1\/2/}).waitFor();
    console.log('GEAR RETURN WEB PASS');
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
