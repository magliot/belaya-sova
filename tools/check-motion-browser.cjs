const fs=require('node:fs'),assert=require('node:assert/strict');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const out='motion-checks';fs.mkdirSync(out,{recursive:true});
(async()=>{
 const browser=await chromium.launch({headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try{
  const page=await browser.newPage({viewport:{width:375,height:812},deviceScaleFactor:1});
  const errors=[],posts=[];page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(r.method()==='POST')posts.push(r.url())});
  await page.route('https://telegram.org/js/telegram-web-app.js',route=>route.fulfill({contentType:'text/javascript',body:`
   window.__cloudCalls=[];window.Telegram={WebApp:{initData:'synthetic-browser-test',initDataUnsafe:{start_param:'motion',user:{id:991,first_name:'Test'}},platform:'ios',
   ready(){},expand(){},disableVerticalSwipes(){},setHeaderColor(){},setBackgroundColor(){},setBottomBarColor(){},isVersionAtLeast(){return false},onEvent(){},enableClosingConfirmation(){},disableClosingConfirmation(){},
   BackButton:{onClick(){},show(){},hide(){}},HapticFeedback:{impactOccurred(){}},
   CloudStorage:{getItem(k,cb){__cloudCalls.push(['get',k]);cb(null,null)},setItem(k,v){__cloudCalls.push(['set',k])}}}};
  `}));
  await page.goto('http://127.0.0.1:8766/?tgWebAppStartParam=motion#tgWebAppData=test');
  await page.waitForURL(/animation-test\.html/);assert(page.url().includes('#tgWebAppData=test'));
  await page.waitForFunction(()=>window.__motionTest?.ready(),{timeout:60000});
  const states=[];
  for(const kind of ['bear','owl'])for(const phase of ['ride','left','right','air','land']){
   await page.evaluate(({kind,phase})=>__motionTest.testPose(kind,phase),{kind,phase});
   const state=await page.evaluate(kind=>{
    const h=__motionTest.heroes[kind],g=__motionTest.game;
    return {model:h.motion.model.visible,old:h.motion.original.visible,finite:h.root.position.toArray().every(Number.isFinite),mode:g.mode,phase:document.querySelector('#motion-test-status').textContent};
   },kind);assert(state.model&&!state.old&&state.finite);states.push({kind,phase,...state});
   await page.screenshot({path:`${out}/${kind}-${phase}.png`});
  }
  await page.getByRole('button',{name:'Играть',exact:true}).click();
  await page.waitForFunction(()=>__motionTest.game.t>1);
  assert.equal(await page.evaluate(()=>__motionTest.game.mode),'play');
  await page.getByRole('button',{name:'Мишаня',exact:true}).click();
  assert.match(await page.locator('#motion-test-status').innerText(),/Мишаня/);
  await page.getByRole('button',{name:'Сова',exact:true}).click();
  assert.match(await page.locator('#motion-test-status').innerText(),/Сова/);
  await page.waitForFunction(()=>__cloudCalls.some(c=>c[0]==='set'),{timeout:5000});
  const cloud=await page.evaluate(()=>__cloudCalls);
  assert(cloud.every(c=>c[1]==='motion_test_save'));assert.deepEqual(posts,[]);assert.deepEqual(errors,[]);
  fs.writeFileSync(`${out}/report.json`,JSON.stringify({viewport:[375,812],states,cloud,posts,errors},null,2));
  console.log('PASS: Telegram launch, 10 poses, both hero controls, test run, isolated cloud saves and no leaderboard submissions.');
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
