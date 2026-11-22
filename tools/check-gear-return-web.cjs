// Compare the actual snowboard rendered by WebGL against stock and purchased reference frames.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const evidence=process.env.SOVA_SHOP_SHOTS;
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
      if(!localStorage.getItem(key))localStorage.setItem(key,JSON.stringify({schema:1,uid:0,data:{hero:'owl',coins:8500,meet:{feathers:50}}}));
      let handler;
      Object.defineProperty(window,'onMountainState',{get:()=>handler,set:fn=>{handler=s=>{window.rider=s;fn(s);};}});
    });
    const phase=p=>page.waitForFunction(p=>window.rider?.phase===p,p,{timeout:80000});
    const tap=id=>page.locator('#'+id).tap();
    const hero=h=>page.locator('[data-hero="'+h+'"]').tap();
    const openShop=()=>page.locator('[data-panel="shop"]').tap();
    // The lightning on the planted tail stays below the moving wing at this viewport.
    async function deck(){
      await page.locator('#play').waitFor({timeout:100000});
      await page.waitForFunction(()=>rider.phase==='menu'&&rider.hero==='owl');
      await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));
      return (await page.screenshot({clip:{x:78,y:390,width:54,height:45}})).toString('base64');
    }
    await page.goto(url.href,{waitUntil:'commit'});
    const capture=async name=>{if(evidence){fs.mkdirSync(evidence,{recursive:true});await page.screenshot({path:path.join(evidence,name+'.png')});}};
    const stock=await deck();await capture('menu-owl');
    const settled=()=>page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));
    for(const [width,height,safe,content] of [[320,565,34,8],[360,640,24,0],[384,832,24,8],[390,844,34,0]]){
      await page.setViewportSize({width,height});
      await page.evaluate(([a,b])=>{
        const style=document.documentElement.style;
        style.setProperty('--tg-safe-area-inset-bottom',a+'px');
        style.setProperty('--tg-content-safe-area-inset-bottom',b+'px');
      },[safe,content]);
      await settled();
      for(const selector of ['#play','[data-panel="settings"]','[data-panel="missions"]','[data-panel="bonuses"]','[data-panel="rules"]']){
        assert(await page.locator(selector).evaluate(el=>{const b=el.getBoundingClientRect();return el.contains(document.elementFromPoint(b.x+b.width/2,b.y+b.height/2));}),selector);
      }
      const rules=page.getByRole('button',{name:/Правила FIS/});
      const box=await rules.boundingBox(), gap=Math.round(height-box.y-box.height);
      assert(box.height>=44&&gap>=safe+content+18,'FIS hit area or bottom clearance at '+width+'×'+height);
      console.log('FIS',width+'×'+height,'height',box.height,'bottom gap',gap);
      await rules.tap();await page.locator('#panel[data-view="rules"]').waitFor();await tap('closePanel');
      if(width===320||width===384){
        for(const name of ['bear','owl']){
          await hero(name);await page.waitForFunction(h=>rider.hero===h,name);await settled();
          await capture('home-'+name+'-'+width);
        }
      }
    }
    await page.evaluate(()=>{for(const prop of ['--tg-safe-area-inset-bottom','--tg-content-safe-area-inset-bottom'])document.documentElement.style.removeProperty(prop);});
    await page.setViewportSize({width:390,height:780});await settled();
    assert.equal(await page.locator('[data-mode="free"],[data-mode="sprint"],#reviewRide option[value="sprint"]').count(),0);
    await openShop();await page.locator('[data-buy="fireboard"]').tap();
    await page.locator('[data-buy="tune"]').tap();await tap('closePanel');
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
    await openShop();await page.locator('[data-equip="fireboard"]').tap();await tap('closePanel');await check('explicit stock',false);
    await openShop();await page.locator('[data-equip="fireboard"]').tap();await tap('closePanel');
    await hero('bear');await hero('owl');await check('switch hero');
    await page.locator('[data-panel="settings"]').tap();await page.getByRole('button',{name:'Осмотр трассы',exact:true}).tap();
    await phase('review');await tap('reviewPlay');await phase('play');await tap('pause');await tap('exitPaused');
    await phase('review');await tap('reviewExit');await check('practice ride');
    await page.reload({waitUntil:'commit'});await check('reload');
    assert.equal(await page.locator('#bank').innerText(),await page.evaluate(()=>String(JSON.parse(localStorage.getItem('bsova_unity_test_v1')).data.coins)));
    await openShop();assert.match(await page.locator('[data-buy="tune"]').innerText(),/1\/2/);
    const data=()=>page.evaluate(()=>JSON.parse(localStorage.getItem('bsova_unity_test_v1')).data);
    for(const [id,who] of [['pinkGoggles','owl'],['orangeHelmet','bear'],['carveSkis','bear']]){
      await page.locator('[data-product="'+id+'"]').tap();
      await page.waitForFunction(h=>rider.hero===h,who);
      const before=await data();assert(!before.ownedGear.includes(id));
      const box=await page.locator('#panel').boundingBox();assert(box.y>390,'Shop controls obscure the product');
      await capture('preview-'+id);
      if(id==='pinkGoggles'||id==='orangeHelmet'){
        const owl=id==='pinkGoggles';
        assert.match(await page.locator('.product h3').innerText(),owl?/Розовая каска Совы/:/Оранжевая каска/);
        async function finishCount(clip){
          const src=(await page.screenshot({clip})).toString('base64');
          return page.evaluate(async src=>{
            const img=new Image();img.src='data:image/png;base64,'+src;await img.decode();
            const ctx=new OffscreenCanvas(img.width,img.height).getContext('2d');ctx.drawImage(img,0,0);
            const px=ctx.getImageData(0,0,img.width,img.height).data;
            let pink=0,blue=0,orange=0,white=0;
            for(let i=0;i<px.length;i+=4){
              const [r,g,b]=[px[i],px[i+1],px[i+2]];
              if(r>g+22&&b>g+15&&r>b*.90)pink++;
              if(b>r+24&&b>g+8)blue++;
              if(r>g*1.45&&g>b*1.5&&r>65)orange++;
              if(Math.min(r,g,b)>110&&Math.max(r,g,b)-Math.min(r,g,b)<25)white++;
            }
            const n=px.length/4;return {pink:pink/n,blue:blue/n,orange:orange/n,white:white/n};
          },src);
        }
        if(owl){
          const lens=await finishCount({x:120,y:252,width:41,height:35});
          const crown=await finishCount({x:169,y:191,width:48,height:13});
          console.log('OWL FACE',lens,crown);
          assert(lens.blue>.60&&lens.pink<.02&&crown.pink>.55,'Owl visor must stay blue under the pink shell');
        }
        const touch=await page.context().newCDPSession(page);
        try{
          await touch.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:50,y:245}]});
          for(const [x,angle] of [[181,'side'],[312,'back'],[50,'front']]){
            await touch.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x,y:245}]});
            await settled();await capture('helmet-'+who+'-'+angle);
            if(angle==='back'){
              const rear=await finishCount(owl?{x:142,y:360,width:106,height:14}:{x:147,y:327,width:92,height:17});
              console.log('HELMET BACK',who,rear);
              assert(owl?rear.pink>.72:rear.orange>.82&&rear.white<.025,'Unpainted rear shell or brim: '+who);
            }
          }
        }finally{
          await touch.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});await touch.detach();
        }
      }
      await page.locator('[data-buy="'+id+'"]').tap();
      const after=await data();assert.equal(after.payments[id],'feathers');assert.equal(after.coins,before.coins);
    }
    await tap('closePanel');await page.waitForFunction(()=>rider.hero==='owl');
    await page.locator('[data-panel="bonuses"]').tap();await capture('bonuses');
    await page.locator('[data-buy="tubing"][data-currency="coins"]').tap();
    await page.locator('[data-buy="sled"][data-currency="feathers"]').tap();
    const finish=await data();assert.equal(finish.bonuses.length,2);
    assert.equal(await page.evaluate(()=>MountainSaves.featherBalance(JSON.parse(localStorage.getItem('bsova_unity_test_v1')).data)),0);
    for(const mode of ['tubing','sled']){
      await page.locator('#panelBody [data-mode="'+mode+'"]').tap();await phase('play');
      assert.equal(await page.evaluate(()=>rider.mode),mode);
      await tap('pause');await tap('exitPaused');await phase('menu');
      await page.locator('[data-panel="bonuses"]').tap();
    }
    await page.reload({waitUntil:'commit'});await phase('menu');
    for(const [who,headgear] of [['owl','pinkGoggles'],['bear','orangeHelmet']]){
      await hero(who);await page.waitForFunction(h=>rider.hero===h,who);
      assert.equal((await data()).headgear[who],headgear);
      await capture('equipped-home-'+who);
      await tap('play');await phase('play');await page.waitForFunction(()=>rider.metres>=30);
      await capture('equipped-ride-'+who);
      await tap('pause');await tap('exitPaused');await phase('over');await tap('exitResult');await phase('menu');
    }
    console.log('GEAR RETURN WEB PASS');
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
