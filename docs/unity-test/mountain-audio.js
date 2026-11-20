// The Web player uses the same three audio channels and recorded voices as the original.
(() => {
  const save={muted:false,vol:{music:6,sfx:8,vox:8}};
  let state={phase:'menu',hero:'bear'},active=false,lastEvent=-1,lastStage=-1;
  let song=null,songKey='',voicePlayer=null,lastVoice=-Infinity;
  const rare=new Map();
let engG=null,engP=null,rumG=null;const engO=[];
let AC=null,master=null,windG=null,carveG=null,windF=null,noiseBuf=null,sfxBus=null,voxBus=null,musBus=null;
// три канала громкости: музыка, звуки игры, голоса героев (0…10 из настроек)
const VOLK={music:0.55,sfx:1,vox:1};
function applyVol(){if(!AC)return;const t=AC.currentTime;for(const [k,b] of [['music',musBus],['sfx',sfxBus],['vox',voxBus]])b.gain.setTargetAtTime(Math.pow(save.vol[k]/10,1.6)*VOLK[k],t,0.05);}
function initAudio(){
  if(AC){if(AC.state==='suspended')AC.resume().catch(()=>{});return;}
  try{AC=new (window.AudioContext||window.webkitAudioContext)();}catch(e){AC=null;return;}
  master=AC.createGain();master.gain.value=save.muted?0:0.65;master.connect(AC.destination);
  sfxBus=AC.createGain();voxBus=AC.createGain();musBus=AC.createGain();for(const b of[sfxBus,voxBus,musBus])b.connect(master);applyVol();
  const len=AC.sampleRate*2;noiseBuf=AC.createBuffer(1,len,AC.sampleRate);const d=noiseBuf.getChannelData(0);
  let b0=0,b1=0,b2=0,b3=0,b4=0,b5=0,b6=0;
  for(let i=0;i<len;i++){const w=Math.random()*2-1;b0=.99886*b0+w*.0555179;b1=.99332*b1+w*.0750759;b2=.969*b2+w*.153852;b3=.8665*b3+w*.3104856;b4=.55*b4+w*.5329522;b5=-.7616*b5-w*.016898;d[i]=(b0+b1+b2+b3+b4+b5+b6+w*.5362)*.11;b6=w*.115926;}
  const n1=AC.createBufferSource();n1.buffer=noiseBuf;n1.loop=true;windF=AC.createBiquadFilter();windF.type='bandpass';windF.frequency.value=420;windF.Q.value=0.5;windG=AC.createGain();windG.gain.value=0;n1.connect(windF).connect(windG).connect(sfxBus);n1.start();
  // мотор ратрака: два «дизельных» пилообразных тона + тарахтение, громкость и панорама — по расстоянию до ближайшего ратрака
  {const lp=AC.createBiquadFilter();lp.type='lowpass';lp.frequency.value=380;const chug=AC.createGain();chug.gain.value=0.6;
    const lfo=AC.createOscillator();lfo.frequency.value=11;const lg=AC.createGain();lg.gain.value=0.4;lfo.connect(lg).connect(chug.gain);lfo.start();
    engG=AC.createGain();engG.gain.value=0;engP=AC.createStereoPanner?AC.createStereoPanner():null;
    for(const [f,v] of[[46,0.5],[69,0.35]]){const o=AC.createOscillator();o.type='sawtooth';o.frequency.value=f;const g=AC.createGain();g.gain.value=v;o.connect(g).connect(lp);o.start();engO.push(o);}
    const n3=AC.createBufferSource();n3.buffer=noiseBuf;n3.loop=true;const bp=AC.createBiquadFilter();bp.type='bandpass';bp.frequency.value=160;bp.Q.value=1.2;const ng=AC.createGain();ng.gain.value=1.4;n3.connect(bp).connect(ng).connect(lp);n3.start();
    lp.connect(chug).connect(engG);if(engP)engG.connect(engP).connect(sfxBus);else engG.connect(sfxBus);}
  // гул лавины
  {const n4=AC.createBufferSource();n4.buffer=noiseBuf;n4.loop=true;const lp=AC.createBiquadFilter();lp.type='lowpass';lp.frequency.value=140;rumG=AC.createGain();rumG.gain.value=0;n4.connect(lp).connect(rumG).connect(sfxBus);n4.start(0,1.1);}
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
  wobble(){noise(0.25,0.18,'lowpass',900,300);}
};
// мягкий «поп» на всех кнопках меню и окон: пузырёк вверх + лёгкий звон (выбор героя звучит по-своему)

  function level(channel){return save.muted?0:Math.pow(save.vol[channel]/10,1.6)*VOLK[channel];}
  function settings(profile){
    save.muted=Boolean(profile.muted);
    for(const key of ['music','sfx','vox']){
      const value=Number(profile.vol?.[key]);
      if(Number.isFinite(value))save.vol[key]=Math.max(0,Math.min(10,value));
    }
    if(AC){master.gain.setTargetAtTime(save.muted?0:.65,AC.currentTime,.05);applyVol();}
    if(voicePlayer)voicePlayer.volume=level('vox');syncMusic();
  }
  function syncMusic(){
    const key=state.phase==='menu'?'menu':state.phase==='play'?'run':'';
    if(!active||document.hidden||!key||!level('music')){song?.pause();return;}
    if(songKey!==key){
      song?.pause();songKey=key;song=new Audio('audio/music_'+key+'.mp3');song.loop=true;
    }
    song.volume=level('music')*.65;
    if(song.paused)song.play().catch(()=>{});
  }
  function speak(key){
    const now=performance.now();
    if(!active||save.muted||document.hidden||now-lastVoice<2500)return;
    if((key==='polite'||key==='miss')&&now-(rare.get(key)??-Infinity)<40000)return;
    lastVoice=now;rare.set(key,now);voicePlayer?.pause();
    voicePlayer=new Audio('audio/'+state.hero+'_'+key+'.mp3');
    voicePlayer.volume=level('vox');voicePlayer.play().catch(()=>{});
  }
  function playS(_,rate,volume){
    if(!active||!level('sfx'))return false;
    const sound=new Audio('audio/land.mp3');sound.playbackRate=rate||1;
    sound.volume=Math.min(1,(volume??.4)*level('sfx'));sound.play().catch(()=>{});return true;
  }
  function update(next){
    const oldPhase=state.phase;state=next;
    if(oldPhase!==next.phase){syncMusic();if(next.phase==='menu'||next.phase==='play'&&oldPhase!=='pause')lastEvent=-1;}
    if(!AC)return;
    const riding=next.phase==='play'&&!document.hidden&&!save.muted;
    const t=AC.currentTime;
    windG.gain.setTargetAtTime(riding?.015+.025*Math.min(1,(next.speed||0)/80):0,t,.15);
    carveG.gain.setTargetAtTime(riding&&!next.air?.05*Math.abs(next.edge||0):0,t,.08);
    const proximity=next.mode==='sled'?.65:(next.engineNear||0);
    engG.gain.setTargetAtTime(riding?.2*proximity*proximity:0,t,.10);
    engP?.pan.setTargetAtTime(next.enginePan||0,t,.1);
    rumG.gain.setTargetAtTime(riding?.16*(next.avalanche||0):0,t,.12);
    if(next.phase==='play'&&lastStage!==-1&&lastStage!==next.stage)speak('stage');
    lastStage=next.phase==='play'?next.stage:-1;
    if(!next.cue||next.eventId===lastEvent||document.hidden)return;
    lastEvent=next.eventId;
    const key=next.cue;
    if(key==='gate')SFX.gate(Math.max(1,next.combo||1),false);
    else if(Object.hasOwn(SFX,key))SFX[key]();
    if(['start','jump','crash','helmet','fis','record','polite','warn','miss','feather'].includes(key))speak(key);
  }
  function activate(){active=true;initAudio();syncMusic();}
  document.addEventListener('pointerdown',activate,{passive:true});
  document.addEventListener('keydown',activate);
  document.addEventListener('visibilitychange',()=>{
    if(document.hidden){song?.pause();voicePlayer?.pause();AC?.suspend().catch(()=>{});}
    else{syncMusic();}
  });
  window.MountainAudio={settings,update};
})();
