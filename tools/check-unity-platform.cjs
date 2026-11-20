const assert=require('node:assert/strict');
require('../unity/OwlSlope/Assets/WebGLTemplates/MountainRace/save.js');
const {create,leaderboard}=globalThis.MountainSaves;
const storage=new Map();
const driver={getItem:k=>storage.get(k)??null,setItem:(k,v)=>storage.set(k,v)};
const write=(k,v)=>driver.setItem(k,JSON.stringify(v));
function telegram(id,values){
  const writes=[];
  return {initData:'fixture',initDataUnsafe:{user:{id}},isVersionAtLeast:v=>v==='6.9',writes,CloudStorage:{
    getItems:(keys,cb)=>queueMicrotask(()=>cb(null,Object.fromEntries(keys.map(k=>[k,values[k]])))),
    setItem:(k,v,cb)=>{writes.push(k);values[k]=v;queueMicrotask(()=>cb(null,true));}
  }};
}
async function main(){
  write('bsova_hero','owl');write('bsova_best',7);
  write('bsova_tg127_hero','bear');write('bsova_tg127_best',900);write('bsova_tg127_coins',280);
  write('bsova_tg127_meet',{goldenOwl:true});write('bsova_tg127_coll',[{w:0,i:0,ch:'E',dist:2400,date:'18.11.2026'}]);
  write('bsova_course_review_tg127_best',99999);
  const oldCloud={uid:127,hero:'owl',best:1200,coins:501,vol:{music:4,sfx:5,vox:6},meet:{prizeFeather:true},sens:7};
  const values={save:JSON.stringify(oldCloud)},legacySnapshot=new Map(storage);
  const app=telegram(127,values),p=create({storage:driver,app});
  await p.load();
  assert.equal(p.data.hero,'owl');assert.equal(p.data.best,1200);assert.equal(p.data.sens,7);assert.equal(p.data.coll.length,1);
  assert(p.data.meet.goldenOwl&&p.data.meet.prizeFeather);
  const price=MountainSaves.price(p.data,'mag');assert(p.buy('mag'));assert.equal(p.data.coins,501-price);
  p.change(['hero'],d=>d.hero='bear');
  const run={mode:'normal',phase:'over',rankedResult:true,stage:0,hero:'bear',score:1400,metres:1343,elapsed:72,coins:19};
  const bank=p.data.coins;
  p.finish('run-1',run);const total=p.data.coins;assert(total>=bank+19);
  assert.equal(p.finish('run-1',run),false);assert.equal(p.data.coins,total);
  assert.equal(p.finish('run-2',{...run,mode:'review'}),false);
  p.unlock({fis:[9],lettersTotal:2});
  await new Promise(r=>setTimeout(r,2));
  assert.equal(values.save,JSON.stringify(oldCloud));assert(app.writes.length&&app.writes.every(k=>k==='unity_test_v1'));
  for(const [k,v] of legacySnapshot)assert.equal(storage.get(k),v);
  const after=create({storage:driver,app});await after.load();
  assert.equal(after.data.hero,'bear');assert.equal(after.data.coins,total);assert.equal(after.data.best,1400);assert.equal(after.data.coll.length,2);
  const foreign=create({storage:driver,app:telegram(998,values)});await foreign.load();
  assert.equal(foreign.data.best,0);assert.equal(foreign.data.hero,'bear');assert(!foreign.data.meet.prizeFeather);
  const guest=create({storage:driver});await guest.load();assert.equal(guest.data.best,7);assert.equal(guest.data.hero,'owl');
  const lower=create({storage:driver,app:telegram(127,{save:JSON.stringify({...oldCloud,best:5000,uid:998})}),channel:'production'});await lower.load();
  assert.equal(lower.data.best,900);assert.equal(lower.data.hero,'bear');assert(!lower.data.meet.prizeFeather);
  const older=create({storage:driver,app:telegram(127,{save:JSON.stringify({...oldCloud,best:300})}),channel:'production'});await older.load();
  assert.equal(older.data.best,900);assert.equal(older.data.hero,'bear');assert(older.data.meet.prizeFeather);
  let delayedRead;const lateApp=telegram(620,{});lateApp.CloudStorage.getItems=(_keys,cb)=>delayedRead=cb;
  const late=create({storage:driver,app:lateApp,cloudWait:1});await late.load();
  late.change(['hero'],d=>d.hero='bear');late.finish('local-before-cloud',{...run,metres:0,score:10,coins:3,gifts:1});late.unlock({fis:[8],lettersTotal:1});
  delayedRead(null,{save:JSON.stringify({uid:620,hero:'owl',best:7000,coins:901,meet:{gifts:8},fis:[9],coll:[{w:0,i:0,ch:'E',date:'yesterday',dist:300}]})});
  assert.equal(late.data.hero,'bear');assert.equal(late.data.best,7000);assert.equal(late.data.coins,904);assert.equal(late.data.meet.gifts,9);
  assert(late.data.fis.includes(8)&&late.data.fis.includes(9));assert.equal(late.data.coll.length,1);
  const calls=[];
  global.fetch=async(url,options)=>{calls.push([url,JSON.parse(options.body)]);return {ok:true,json:async()=>({ok:true,rows:[],rank:1,weekRank:1})};};
  const test=leaderboard({channel:'test',leaderboardEndpoint:'https://example.invalid/should-never-be-called'},app,p);
  assert.equal(await test.submit(run),null);assert.equal((await test.top('all')).rows[0].score,1400);assert.equal(calls.length,0);
  const prod=leaderboard({channel:'production',leaderboardEndpoint:'https://example.invalid/acceptance-fixture'},app,lower);
  await prod.submit(run,'device.run0001');await prod.top('week');
  assert.deepEqual(calls[0][1],{action:'submit',initData:'fixture',scoring:2,runId:'device.run0001',score:1400,dist:1343,dur:72,hero:'bear',stage:1,coins:19,gates:0,rings:0,preciseGates:0,preciseRings:0,rules:0});
  assert.deepEqual(calls[1][1],{action:'leaderboard',initData:'fixture',period:'week'});
  for(const update of [{phase:'play'},{phase:'pause'},{rankedResult:false},{mode:'review'},{mode:'free'},{mode:'sprint'}])
    assert.equal(await prod.submit({...run,...update},'device.ignored'),null);
  assert.equal(calls.length,2);
  const candidate=create({storage:driver,app,channel:'progression'});await candidate.load();
  assert.equal(candidate.data.best,0);assert.equal(candidate.data.coins,0);
  candidate.unlock({fis:[9],fisContexts:[9001,9001,9072]});assert.deepEqual(candidate.data.fisContexts,[9001,9072]);
  candidate.finish('device.quit0001',{...run,rankedResult:false});assert.equal(candidate.data.best,0);assert.equal(candidate.rows.length,0);
  candidate.finish('device.new0002',run);assert.equal(candidate.data.best,1400);assert.equal(candidate.rows.length,1);
  assert(!candidate.finish('device.quit0001',run));assert.equal(candidate.data.coins,38);
  const previewConfig={channel:'progression',previewBoard:'https://example.invalid/isolated'};
  const board=leaderboard(previewConfig,app,candidate);
  const online=global.fetch;global.fetch=async()=>{throw Error('offline');};
  await assert.rejects(board.submit(run,'device.rank0002'));assert.equal(candidate.pendingScores.length,1);
  global.fetch=online;
  const reopened=create({storage:driver,app,channel:'progression'});await reopened.load();
  await leaderboard(previewConfig,app,reopened).flush();assert.equal(reopened.pendingScores.length,0);
  const [endpoint,payload]=calls.at(-1);assert.equal(endpoint,previewConfig.previewBoard);
  assert.equal(payload.runId,'device.rank0002');assert.match(payload.tester,/^[a-f0-9]{32}$/);assert(!('initData' in payload));
  console.log('PASS legacy cloud, wallet once, FIS mastery, ranked fall eligibility, isolated test ranks and offline retry');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
