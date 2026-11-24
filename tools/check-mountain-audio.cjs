// Drive Unity's HUD snapshots through the actual Web Audio graph in headless Chromium.
const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');
const template=path.resolve('unity/OwlSlope/Assets/WebGLTemplates/MountainRace');

async function main(){
  const browser=await chromium.launch();
  try{
    const page=await browser.newPage();
    const errors=[];page.on('pageerror',e=>errors.push(e.message));
    const requested=[];
    await page.route('http://audio.test/**',route=>{
      const resource=new URL(route.request().url()).pathname;
      if(resource==='/')return route.fulfill({contentType:'text/html',body:'<button>Start</button><script src="/mountain-audio.js"></script>'});
      if(resource==='/mountain-audio.js')return route.fulfill({path:path.join(template,'mountain-audio.js'),contentType:'text/javascript'});
      if(/^\/audio\/[a-z_]+\.mp3$/.test(resource)){
        requested.push(resource);return route.fulfill({path:path.join(template,resource),contentType:'audio/mpeg'});
      }
      return route.abort();
    });
    await page.addInitScript(()=>{
      window.clips=[];window.songs=[];window.hornNotes=[];window.motorLevels=[];
      Object.hasOwn=undefined;
      const oscStart=OscillatorNode.prototype.start;
      OscillatorNode.prototype.start=function(...args){
        if(this.type==='square'&&[311,392].includes(this.frequency.value))hornNotes.push(this.frequency.value);
        return oscStart.apply(this,args);
      };
      const pannerGains=new WeakSet(),connect=AudioNode.prototype.connect,target=AudioParam.prototype.setTargetAtTime;
      AudioNode.prototype.connect=function(node,...args){
        if(this instanceof GainNode&&node instanceof StereoPannerNode)pannerGains.add(this.gain);
        return connect.call(this,node,...args);
      };
      AudioParam.prototype.setTargetAtTime=function(value,...args){
        if(pannerGains.has(this))motorLevels.push(value);
        return target.call(this,value,...args);
      };
      const tags=new WeakMap(),bytes=Response.prototype.arrayBuffer,decode=BaseAudioContext.prototype.decodeAudioData;
      Response.prototype.arrayBuffer=async function(){
        const data=await bytes.call(this);tags.set(data,this.url.split('/').pop());return data;
      };
      BaseAudioContext.prototype.decodeAudioData=async function(data){
        const buffer=await decode.call(this,data);tags.set(buffer,tags.get(data));return buffer;
      };
      const start=AudioBufferSourceNode.prototype.start,stop=AudioBufferSourceNode.prototype.stop;
      AudioBufferSourceNode.prototype.start=function(...args){
        if(tags.has(this.buffer))clips.push(['play',tags.get(this.buffer),this.buffer.duration]);
        return start.apply(this,args);
      };
      AudioBufferSourceNode.prototype.stop=function(...args){
        if(tags.has(this.buffer))clips.push(['stop',tags.get(this.buffer)]);
        return stop.apply(this,args);
      };
      const media=AudioContext.prototype.createMediaElementSource;
      AudioContext.prototype.createMediaElementSource=function(song){songs.push(song);return media.call(this,song);};
      const fault=location.search.slice(1);
      window.faultCalls=0;
      const unavailable=()=>{faultCalls++;throw new Error('Unavailable '+fault);};
      if(fault==='context')window.AudioContext=class{constructor(){unavailable();}};
      if(fault==='graph')AudioContext.prototype.createBiquadFilter=unavailable;
      if(fault==='music')AudioContext.prototype.createMediaElementSource=unavailable;
    });
    await page.goto('http://audio.test/');
    await page.getByRole('button').click();
    await page.waitForFunction(()=>songs.length===1&&songs[0].currentTime>0&&songs[0].src.endsWith('/music_menu.mp3'));
    const state={phase:'play',mode:'normal',hero:'bear',elapsed:0,speed:60,feather:false,feathers:0,eventId:1,cue:'start'};
    const send=async changes=>{Object.assign(state,changes);await page.evaluate(s=>MountainAudio.update(s),state);};
    const waitClip=(name,action='play')=>page.waitForFunction(([name,action])=>clips.some(c=>c[0]===action&&c[1]===name),[name,action]);
    for(const cue of [
      {cue:'start'}, {eventId:2,cue:'chime',goldenOwl:true}, {eventId:3,cue:'chime',encounters:1},
      {eventId:4,cue:'chime',letters:1}, {eventId:5,cue:'feather'}
    ])await send(cue);
    await waitClip('bear_start.mp3');
    await send({engineNear:.1});await send({engineNear:.55,horns:1});await send({engineNear:.9});await send({});
    const approach=await page.evaluate(()=>({notes:hornNotes,levels:motorLevels.slice(-4)}));
    assert.deepEqual(approach.notes,[311,392,311,392],'one approaching groomer should honk once');
    const [far,middle,near]=approach.levels;
    assert(far>0&&middle>far&&near>middle&&near>.2,'engine must grow towards the player');
    await page.waitForFunction(()=>songs.length===1&&songs[0].currentTime>0&&!songs[0].paused&&songs[0].src.endsWith('/music_run.mp3'));
    assert(!requested.includes('/audio/bear_feather.mp3'),'A non-feather reward triggered speech');
    // A second contact overwrote "feather" within the same Unity HUD tick.
    await send({eventId:7,cue:'gate',feather:true,feathers:1});await send({});
    await waitClip('bear_feather.mp3');await waitClip('bear_start.mp3','stop');
    await send({phase:'pause'});await waitClip('bear_feather.mp3','stop');
    assert(await page.evaluate(()=>songs[0].paused),'Music continued under pause');
    await send({phase:'play'});await send({phase:'over',eventId:8,cue:'fallEnd'});await send({});
    await waitClip('bear_crash.mp3');
    assert.equal(await page.evaluate(()=>clips.filter(c=>c[0]==='play'&&c[1]==='bear_crash.mp3').length),1);
    assert.equal(await page.evaluate(()=>clips.filter(c=>c[0]==='play'&&c[1]==='bear_feather.mp3').length),1);
    await send({phase:'menu',hero:'owl',feather:false,feathers:0,cue:''});
    await send({phase:'play',eventId:9,cue:'start'});await waitClip('owl_start.mp3');
    await page.evaluate(()=>MountainAudio.settings({muted:true,vol:{music:6,vox:8,sfx:8}}));
    await waitClip('owl_start.mp3','stop');
    await send({eventId:10,cue:'feather',feather:true,feathers:1});
    await page.evaluate(()=>MountainAudio.settings({muted:false,vol:{music:6,vox:8,sfx:8}}));
    await send({});assert(!requested.includes('/audio/owl_feather.mp3'),'Muted reward replayed');
    await send({eventId:11,cue:'feather',feathers:2});await waitClip('owl_feather.mp3');
    await send({eventId:12,cue:'land'});await waitClip('land.mp3');
    await page.evaluate(()=>{
      Object.defineProperty(document,'hidden',{configurable:true,get:()=>true});
      document.dispatchEvent(new Event('visibilitychange'));
    });
    await waitClip('owl_feather.mp3','stop');
    assert(await page.evaluate(()=>songs[0].paused),'Background music continued after hiding');
    await send({eventId:13,cue:'start'});
    await page.evaluate(()=>{delete document.hidden;document.dispatchEvent(new Event('visibilitychange'));});
    await send({});
    await page.waitForFunction(()=>!songs[0].paused);
    assert.equal(await page.evaluate(()=>clips.filter(c=>c[0]==='play'&&c[1]==='owl_start.mp3').length),1);
    await send({eventId:14,cue:'crash'});
    await send({phase:'over',eventId:15,cue:'fallEnd'});await send({});
    await waitClip('owl_crash.mp3');
    await send({phase:'menu',cue:''});await waitClip('owl_crash.mp3','stop');
    await send({phase:'play',eventId:16,cue:'start'});
    await send({phase:'over',eventId:17,cue:'exit'});
    assert.equal(await page.evaluate(()=>clips.filter(c=>c[0]==='play'&&c[1]==='owl_crash.mp3').length),1);
    assert.deepEqual(errors,[]);
    assert.equal(await page.evaluate(()=>songs.length),1);
    for(const [,name,duration] of await page.evaluate(()=>clips.filter(c=>c[0]==='play')))assert(duration>0,name);
    for(const fault of ['context','graph','music']){
      await page.goto('http://audio.test/?'+fault);
      await page.getByRole('button').click();
      await page.getByRole('button').click();
      await page.evaluate(()=>{
        const settings={muted:false,vol:{music:6,vox:8,sfx:8}};
        const state={hero:'bear',elapsed:0,edge:0,speed:0,phase:'play',eventId:1,cue:'start'};
        MountainAudio.settings(settings);
        MountainAudio.update(state);
        MountainAudio.update({...state,phase:'pause',cue:''});
        MountainAudio.update({...state,eventId:2});
        MountainAudio.settings({...settings,muted:true});
        MountainAudio.settings(settings);
      });
      assert.equal(await page.evaluate(()=>faultCalls),1,fault+' retried on every tap');
      if(fault==='music'){
        await page.evaluate(()=>MountainAudio.update({hero:'bear',elapsed:1,speed:0,phase:'play',eventId:3,cue:'start'}));
        await waitClip('bear_start.mp3');
      }
      assert.deepEqual(errors,[],fault+' broke audio settings or state delivery');
    }
    console.log('Real MP3 decoding and playback passed for both heroes, both soundtracks and landing. Reward, pause, mute, resume and result events passed.');
    console.log('Startup passed without Object.hasOwn and with denied audio context, graph or music source.');
  }finally{await browser.close();}
}
// Only an actual feather contact may emit this gameplay cue.
const sources=['MountainRaceContacts.cs','MountainRacePhysics.cs'].map(file=>fs.readFileSync(path.resolve('unity/OwlSlope/Assets',file),'utf8')).join('\n');
assert.equal((sources.match(/Notice\([^;]+,"feather"\)/g)||[]).length,1);
assert.match(sources,/void CollectFeather\(RaceItem item\)\s*{\s*if\(item\.consumed\)return;\s*item\.Consume\(\);Status\.feather=true;Status\.feathers\+\+;[^;]+;\s*Notice\([^;]+,"feather"\)/);
assert.match(sources,/case "feather":[\s\S]*?TouchAir\(o,[\s\S]*?if\(flightTricks>=2\)CollectFeather\(o\)/);
main().catch(error=>{console.error(error);process.exitCode=1;});
