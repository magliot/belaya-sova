// The Web player uses the same three audio channels and recorded voices as the original.
(() => {
  const save={muted:false,vol:{music:6,sfx:8,vox:8}};
  let state={phase:'menu',hero:'bear'},active=false,lastEvent=-1;
  let song=null,songKey='',musicDuck=null,voice=null,voiceTicket=0,lastVoice=-Infinity;
  let graphUnavailable=false,musicUnavailable=false;
  const samples=new Map();
  const rare=new Map();
let engG=null,engP=null,rumG=null,rumFilter=null;const engO=[];
let AC=null,master=null,windG=null,carveG=null,windF=null,noiseBuf=null,sfxBus=null,voxBus=null,musBus=null;
// три канала громкости: музыка, звуки игры, голоса героев (0…10 из настроек)
const VOLK={music:0.55,sfx:1,vox:1};
function applyVol(){if(!AC)return;const t=AC.currentTime;for(const [k,b] of [['music',musBus],['sfx',sfxBus],['vox',voxBus]])b.gain.setTargetAtTime(Math.pow(save.vol[k]/10,1.6)*VOLK[k],t,0.05);}
function changeContextState(action){
  try{AC?.[action]?.()?.catch(()=>{});}catch{}
}
function initAudio(){
  if(graphUnavailable)return;
  if(!AC){
    try{
      AC=new (window.AudioContext||window.webkitAudioContext)();
      createGraph();
    }catch(error){
      graphUnavailable=true;
      changeContextState('close');
      AC=master=windG=carveG=windF=noiseBuf=sfxBus=voxBus=musBus=engG=engP=rumG=rumFilter=null;
      engO.length=0;
      console.warn('Web Audio unavailable:',error);
      return;
    }
  }
  if(AC.state==='suspended')changeContextState('resume');
}
function createGraph(){
  master=AC.createGain();master.gain.value=save.muted?0:0.65;master.connect(AC.destination);
  sfxBus=AC.createGain();voxBus=AC.createGain();musBus=AC.createGain();for(const b of[sfxBus,voxBus,musBus])b.connect(master);applyVol();
  const len=AC.sampleRate*2;noiseBuf=AC.createBuffer(1,len,AC.sampleRate);const d=noiseBuf.getChannelData(0);
  let b0=0,b1=0,b2=0,b3=0,b4=0,b5=0,b6=0;
  for(let i=0;i<len;i++){const w=Math.random()*2-1;b0=.99886*b0+w*.0555179;b1=.99332*b1+w*.0750759;b2=.969*b2+w*.153852;b3=.8665*b3+w*.3104856;b4=.55*b4+w*.5329522;b5=-.7616*b5-w*.016898;d[i]=(b0+b1+b2+b3+b4+b5+b6+w*.5362)*.11;b6=w*.115926;}
  const n1=AC.createBufferSource();n1.buffer=noiseBuf;n1.loop=true;windF=AC.createBiquadFilter();windF.type='bandpass';windF.frequency.value=420;windF.Q.value=0.5;windG=AC.createGain();windG.gain.value=0;n1.connect(windF).connect(windG).connect(sfxBus);n1.start();
  // мотор ратрака: два «дизельных» пилообразных тона + тарахтение, громкость и панорама — по расстоянию до ближайшего ратрака
  {const lp=AC.createBiquadFilter();lp.type='lowpass';lp.frequency.value=660;const chug=AC.createGain();chug.gain.value=0.6;
    const lfo=AC.createOscillator();lfo.frequency.value=11;const lg=AC.createGain();lg.gain.value=0.4;lfo.connect(lg).connect(chug.gain);lfo.start();
    engG=AC.createGain();engG.gain.value=0;engP=AC.createStereoPanner?AC.createStereoPanner():null;
    for(const [f,v] of[[46,0.5],[69,0.35],[184,.12]]){const o=AC.createOscillator();o.type='sawtooth';o.frequency.value=f;const g=AC.createGain();g.gain.value=v;o.connect(g).connect(lp);o.start();engO.push(o);}
    const n3=AC.createBufferSource();n3.buffer=noiseBuf;n3.loop=true;const bp=AC.createBiquadFilter();bp.type='bandpass';bp.frequency.value=160;bp.Q.value=1.2;const ng=AC.createGain();ng.gain.value=1.4;n3.connect(bp).connect(ng).connect(lp);n3.start();
    lp.connect(chug).connect(engG);if(engP)engG.connect(engP).connect(sfxBus);else engG.connect(sfxBus);}
  // гул лавины
  {const n4=AC.createBufferSource();n4.buffer=noiseBuf;n4.loop=true;rumFilter=AC.createBiquadFilter();rumFilter.type='lowpass';rumFilter.frequency.value=140;rumG=AC.createGain();rumG.gain.value=0;n4.connect(rumFilter).connect(rumG).connect(sfxBus);n4.start(0,1.1);}
  const n2=AC.createBufferSource();n2.buffer=noiseBuf;n2.loop=true;const hp=AC.createBiquadFilter();hp.type='highpass';hp.frequency.value=700;const lp=AC.createBiquadFilter();lp.type='lowpass';lp.frequency.value=3200;carveG=AC.createGain();carveG.gain.value=0;n2.connect(hp).connect(lp).connect(carveG).connect(sfxBus);n2.start(0,0.7);
}
function tone(f,d,v,type,f2,delay){if(!AC||save.muted)return;const t0=AC.currentTime+(delay||0);const o=AC.createOscillator(),g=AC.createGain();o.type=type||'sine';o.frequency.setValueAtTime(f,t0);if(f2)o.frequency.exponentialRampToValueAtTime(f2,t0+d);g.gain.setValueAtTime(0.0001,t0);g.gain.exponentialRampToValueAtTime(v,t0+0.015);g.gain.exponentialRampToValueAtTime(0.0001,t0+d);o.connect(g).connect(sfxBus);o.start(t0);o.stop(t0+d+0.05);}
function noise(d,v,ft,f,f2,q,delay){if(!AC||save.muted)return;const t0=AC.currentTime+(delay||0);const s=AC.createBufferSource();s.buffer=noiseBuf;const fl=AC.createBiquadFilter();fl.type=ft;fl.frequency.setValueAtTime(f,t0);if(f2)fl.frequency.exponentialRampToValueAtTime(f2,t0+d);fl.Q.value=q||0.7;const g=AC.createGain();g.gain.setValueAtTime(0.0001,t0);g.gain.exponentialRampToValueAtTime(v,t0+0.02);g.gain.exponentialRampToValueAtTime(0.0001,t0+d);s.connect(fl).connect(g).connect(sfxBus);s.start(t0,Math.random()*1.4);s.stop(t0+d+0.05);}
const PENTA=[0,2,4,7,9];
// звуки из наборов Kenney (CC0): audio/sfx/<имя>_<вариант>.mp3; если не загрузились — синтез ниже
const SFX={
  coin(){noise(0.08,0.18,'bandpass',1400,700,2.5);tone(520,0.1,0.05,'sine',820);},
  // ворота: тихий мягкий «динь», нота растёт по ходу серии; последние ворота серии — аккорд. Промах — без звука
  gate(c,last){const n=PENTA[(c-1)%5]+12*(Math.floor((c-1)/5)%2);const f=523*Math.pow(2,n/12);tone(f,0.25,0.028);if(last){tone(f*1.25,0.35,0.02,'sine',null,0.08);tone(f*1.5,0.45,0.018,'sine',null,0.16);}},
  miss(){},
  horn(){if(!AC||save.muted)return;for(const d of[0,0.42])for(const f of[311,392]){const t0=AC.currentTime+d;const o=AC.createOscillator(),g=AC.createGain(),lp=AC.createBiquadFilter();o.type='square';o.frequency.value=f;lp.type='lowpass';lp.frequency.value=1400;
    g.gain.setValueAtTime(0.0001,t0);g.gain.exponentialRampToValueAtTime(0.045,t0+0.02);g.gain.setValueAtTime(0.045,t0+0.3);g.gain.exponentialRampToValueAtTime(0.0001,t0+0.36);o.connect(lp).connect(g).connect(sfxBus);o.start(t0);o.stop(t0+0.4);}},
  jump(){noise(0.4,0.16,'bandpass',350,1600,1.1);},
  land(){if(playS('land',0.9,0.28))return;noise(0.16,0.22,'lowpass',600,140);},
  trick(){noise(0.3,0.12,'bandpass',800,2400,1.6);},
  trickDone(){tone(784,0.14,0.05);tone(1175,0.22,0.05,'sine',null,0.07);},
  crash(){playS('land',0.7,0.3,0.15);noise(0.7,0.45,'lowpass',1100,110);tone(150,0.45,0.12,'sine',50);},
  smash(){playS('land',0.8,0.22,0.02);noise(0.35,0.3,'lowpass',1800,260);},
  power(){[523,659,784,1046].forEach((f,i)=>tone(f,0.2,0.05,'sine',null,i*0.06));},
  feather(){[784,988,1175,1568,1976].forEach((f,i)=>tone(f,0.35,0.045,'sine',null,i*0.07));},
  polite(){},
  warn(){tone(466,0.16,0.06,'triangle');tone(466,0.16,0.06,'triangle',null,0.2);},
  shield(){noise(0.3,0.2,'highpass',1500,4000);tone(600,0.3,0.06,'sine',300);},
  chime(){tone(1046,0.5,0.04);tone(1568,0.6,0.03,'sine',null,0.12);},
  snowball(){noise(.13,.13,'lowpass',1150,420);tone(205,.07,.035,'sine',112);},
  wobble(){noise(0.25,0.18,'lowpass',900,300);}
};
  function loadSample(key){
    if(samples.has(key))return samples.get(key);
    const pending=fetch('audio/'+key+'.mp3').then(response=>{
      if(!response.ok)throw Error(response.status);
      return response.arrayBuffer();
    }).then(bytes=>AC.decodeAudioData(bytes)).catch(()=>{samples.delete(key);return null;});
    samples.set(key,pending);return pending;
  }
  function stopVoice(){
    voiceTicket++;
    if(voice){voice.onended=null;voice.stop();voice.disconnect();voice=null;}
    if(musicDuck)musicDuck.gain.setTargetAtTime(1,AC.currentTime,.15);
  }
  function settings(profile){
    save.muted=Boolean(profile.muted);
    for(const key of ['music','sfx','vox']){
      const value=Number(profile.vol?.[key]);
      if(Number.isFinite(value))save.vol[key]=Math.max(0,Math.min(10,value));
    }
    if(AC){master.gain.setTargetAtTime(save.muted?0:.65,AC.currentTime,.05);applyVol();}
    if(save.muted||!save.vol.vox)stopVoice();
    syncMusic();
  }
  function syncMusic(){
    if(musicUnavailable)return;
    const key=state.phase==='menu'?'menu':state.phase==='play'?'run':'';
    if(!active||!AC||document.hidden||!key||save.muted||!save.vol.music){song?.pause();return;}
    if(!song){
      try{
        const track=new Audio(),duck=AC.createGain();
        track.loop=true;
        AC.createMediaElementSource(track).connect(duck).connect(musBus);
        song=track;musicDuck=duck;
      }catch(error){musicUnavailable=true;console.warn('Music source unavailable:',error);return;}
    }
    if(songKey!==key){songKey=key;song.src='audio/music_'+key+'.mp3';}
    if(song.paused)song.play()?.catch(()=>{});
  }
  async function speak(key,priority=false,phase='play'){
    const now=performance.now();
    if(!active||save.muted||!save.vol.vox||document.hidden)return;
    const cooldown={polite:40000,miss:40000,warn:16000,jump:14000}[key]||0;
    if(!priority&&(voice||now-lastVoice<3000||now-(rare.get(key)??-Infinity)<cooldown))return;
    stopVoice();const ticket=voiceTicket;
    lastVoice=now;rare.set(key,now);
    const buffer=await loadSample(state.hero+'_'+key);
    if(!buffer||ticket!==voiceTicket||state.phase!==phase||document.hidden)return;
    const source=AC.createBufferSource();source.buffer=buffer;source.connect(voxBus);voice=source;
    musicDuck?.gain.setTargetAtTime(.38,AC.currentTime,.045);
    source.onended=()=>{
      source.disconnect();if(voice!==source)return;
      voice=null;musicDuck?.gain.setTargetAtTime(1,AC.currentTime,.18);
    };
    source.start();
  }
  let landingBuffer=null;
  function playS(_,rate,volume,delay=0){
    if(!landingBuffer||save.muted||!save.vol.sfx)return false;
    const source=AC.createBufferSource(),gain=AC.createGain();
    source.buffer=landingBuffer;source.playbackRate.value=rate;gain.gain.value=volume;
    source.connect(gain).connect(sfxBus);source.onended=()=>{source.disconnect();gain.disconnect();};
    source.start(AC.currentTime+delay);return true;
  }
  function update(next){
    const previous=state;state=next;
    if(previous.phase!==next.phase){
      if(next.phase!=='play'||previous.phase!=='pause')stopVoice();
      syncMusic();
    }
    if(!AC)return;
    const riding=next.phase==='play'&&!document.hidden&&!save.muted;
    const t=AC.currentTime;
    const storm=(next.blizzard||0)*Math.abs(.75*Math.sin(next.elapsed*.55)+.25*Math.sin(next.elapsed*1.7));
    windF.frequency.setTargetAtTime(420+storm*340,t,.3);
    windG.gain.setTargetAtTime(riding?.015+.025*Math.min(1,(next.speed||0)/80)+.09*storm:0,t,.15);
    carveG.gain.setTargetAtTime(riding&&!next.air?.05*Math.abs(next.edge||0):0,t,.08);
    const proximity=next.mode==='sled'?.65:(next.engineNear||0);
    const motor=next.mode==='sled'?.2*proximity*proximity:.30*proximity*(.55+.45*proximity);
    engG.gain.setTargetAtTime(riding?motor:0,t,.12);
    engP?.pan.setTargetAtTime(next.enginePan||0,t,.1);
    const snow=next.boulderNear||0;
    rumFilter.frequency.setTargetAtTime(140+460*snow,t,.16);
    const roll=.30*snow**1.6*(.84+.16*Math.sin((next.elapsed||0)*13));
    rumG.gain.setTargetAtTime(riding?Math.max(.16*(next.avalanche||0),roll):0,t,.12);
    if(!riding){
      if(next.phase==='over'&&previous.phase!=='over'&&next.cue==='fallEnd')speak('crash',true,'over');
      lastEvent=next.eventId;return;
    }
    if(next.horns>(previous.horns||0))SFX.horn();
    // Several contacts can share one HUD snapshot. The pickup counter survives a later cue in that frame.
    if(next.feather&&next.feathers>(previous.feathers||0)){
      lastEvent=next.eventId;SFX.feather();speak('feather',true);return;
    }
    if(!next.cue||next.eventId===lastEvent)return;
    lastEvent=next.eventId;
    const key=next.cue;
    if(key==='feather')return;
    if(key==='gate')SFX.gate(Math.max(1,next.combo||1),false);
    else if(Object.prototype.hasOwnProperty.call(SFX,key))SFX[key]();
    if(key==='crash'&&save.vol.vox>0)loadSample(next.hero+'_crash');
    if(['start','jump','helmet','fis','record','polite','warn','miss','stage'].includes(key))speak(key,key==='start');
  }
  function activate(){
    active=true;initAudio();syncMusic();
    if(AC&&!samples.has('land'))loadSample('land').then(buffer=>{landingBuffer=buffer;});
  }
  document.addEventListener('pointerdown',activate,{passive:true});
  document.addEventListener('keydown',activate);
  document.addEventListener('visibilitychange',()=>{
    if(document.hidden){song?.pause();stopVoice();changeContextState('suspend');}
    else if(active){initAudio();syncMusic();}
  });
  window.MountainAudio={settings,update};
})();
