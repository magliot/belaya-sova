/* Exercise the real player with mobile-sized HTTP transfers and a broken
   connection. Run after tools/package-unity-shell.cjs, using local Chrome. */
const assert=require('node:assert/strict');
const fs=require('node:fs'),http=require('node:http'),path=require('node:path');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');

const docs=path.resolve(__dirname,'../docs'),block=1024*1024;
const mime={'.js':'text/javascript','.css':'text/css','.html':'text/html;charset=utf-8',
  '.json':'application/json','.jpg':'image/jpeg','.png':'image/png','.webp':'image/webp',
  '.woff2':'font/woff2','.svg':'image/svg+xml'};
let fault='offline',held=false,rangeSupport=true,missingShell=false,encoded=false;
const requests=[];
const server=http.createServer((req,res)=>{
  let url=new URL(req.url,'http://localhost').pathname;
  if(url.endsWith('/'))url+='index.html';
  const file=path.resolve(docs,'.'+url);
  if(!file.startsWith(docs+path.sep)||!fs.existsSync(file)||!fs.statSync(file).isFile()){
    res.writeHead(404).end();return;
  }
  if(missingShell&&url.endsWith('/save.js')){res.writeHead(503).end();return;}
  const size=fs.statSync(file).size,headers={'Content-Type':mime[path.extname(file)]||'application/octet-stream','Cache-Control':'no-store'};
  const heavy=/\.(data|wasm)\.unityweb$/.exec(file);
  if(heavy&&encoded)headers['Content-Encoding']='br';
  if(req.method==='HEAD'){res.writeHead(200,{...headers,'Content-Length':size}).end();return;}
  let start=0,end=size-1,partial=false;
  if(heavy){
    const range=/^bytes=(\d+)-(\d+)$/.exec(req.headers.range||'');
    if(rangeSupport&&range){start=Number(range[1]);end=Number(range[2]);partial=true;}
    requests.push({kind:heavy[1],start,end});
    if(heavy[1]==='data'&&start===block&&fault==='offline'){res.writeHead(503).end();return;}
    if(partial)headers['Content-Range']='bytes '+start+'-'+end+'/'+size;
  }
  res.writeHead(partial?206:200,{...headers,'Content-Length':end-start+1});
  if(heavy?.[1]==='data'&&start===block&&fault==='stall'&&!held){
    held=true;fs.createReadStream(file,{start,end:start+16383}).pipe(res,{end:false});return;
  }
  const stream=fs.createReadStream(file,{start,end});stream.pipe(res);
  res.on('close',()=>stream.destroy());
});

async function run(){
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  const origin='http://127.0.0.1:'+server.address().port;
  const browser=await chromium.launch();
  try{
    const ctx=await browser.newContext({viewport:{width:390,height:780},hasTouch:true});
    const page=await ctx.newPage();
    const errors=[];page.on('pageerror',e=>errors.push(e.message));
    const loaded=()=>page.locator('#play').waitFor({timeout:90000});
    const moving=async()=>{
      await loaded();await page.locator('#play').tap();
      await page.waitForFunction(()=>!document.querySelector('#hud').hidden&&parseInt(document.querySelector('#metres').textContent)>3,null,{timeout:30000});
    };
    await page.goto(origin+'/?startapp=unitytest_1911');
    await page.waitForFunction(()=>!document.querySelector('#retryLoad').hidden&&document.querySelector('#loadBar').hidden);
    assert.equal(requests.filter(r=>r.kind==='data'&&r.start===block).length,3);
    assert.equal(requests.filter(r=>r.kind==='data'&&r.start===0).length,1);
    fault='stall';
    await page.locator('#retryLoad').tap();
    await page.waitForFunction(()=>!document.querySelector('#loadBar').hidden&&!document.querySelector('#retryLoad').hidden,null,{timeout:30000});
    await moving();
    assert(held,'the request must stall after delivering real bytes');
    assert.equal(requests.filter(r=>r.kind==='data'&&r.start===block).length,5,'stalled range was retried exactly once');
    assert.equal(requests.filter(r=>r.kind==='data'&&r.start===0).length,1,'completed first range was fetched again');
    assert.equal(new URL(page.url()).searchParams.get('seed'),'1911');
    const coldCount=requests.length;
    await page.reload();await moving();
    assert.equal(requests.length,coldCount,'warm start downloaded a heavy file');
    assert.deepEqual(errors,[]);
    console.log('PASS hard error → retry, stalled body → automatic resume, cached prefix retained, warm start → zero data/Wasm requests, moving HUD.');
    await ctx.close();

    rangeSupport=false;fault='none';requests.length=0;
    const fallback=await browser.newContext({viewport:{width:360,height:740},hasTouch:true});
    await fallback.addInitScript(()=>Object.defineProperty(window,'caches',{get(){throw Error('private storage');}}));
    const tab=await fallback.newPage();
    missingShell=true;
    await tab.goto(origin+'/?startapp=unitytest_1911');
    await tab.locator('#retryLoad').waitFor({timeout:20000});
    missingShell=false;await tab.locator('#retryLoad').tap();
    await tab.locator('#play').waitFor({timeout:90000});await tab.locator('#play').tap();
    await tab.waitForFunction(()=>parseInt(document.querySelector('#metres').textContent)>3);
    assert.deepEqual(requests.map(r=>r.kind).sort(),['data','wasm']);
    console.log('PASS missing shell script → retry, restricted storage and server without ranges → one GET per player file, moving HUD.');
    await fallback.close();

    rangeSupport=true;encoded=true;requests.length=0;
    const standard=await browser.newContext({viewport:{width:390,height:780},hasTouch:true});
    const normal=await standard.newPage();
    await normal.goto(origin+'/unity-test/?seed=1911');
    await normal.locator('#play').waitFor({timeout:60000});await normal.locator('#play').tap();
    await normal.waitForFunction(()=>parseInt(document.querySelector('#metres').textContent)>3);
    for(const kind of ['data','wasm']){
      const seen=requests.filter(request=>request.kind===kind);
      assert.equal(seen.length,2,kind+' expected one range probe and one complete encoded stream');
      assert(seen[1].end>seen[0].end);
    }
    const count=requests.length;
    await normal.reload();await normal.locator('#play').waitFor({timeout:60000});
    assert.equal(requests.length,count,'decoded complete files should be reused');
    await standard.close();console.log('PASS HTTP Content-Encoding with ranges and decoded warm cache.');
  }finally{await browser.close();server.closeAllConnections();await new Promise(resolve=>server.close(resolve));}
}
run().catch(e=>{console.error(e);process.exitCode=1;});
