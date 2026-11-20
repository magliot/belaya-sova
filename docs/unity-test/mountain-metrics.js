(() => {
  const frames=[],inputs=[];
  let last=0,pointer=0,down=false,phase='',menu=0,startPress=0,firstRide=0,play=0,seed=0;
  const now=()=>performance.now();
  // Only active, visible play counts. Deltas above one second remain visible in p95/max.
  function record(a,v){if(a.length===3600)a.splice(0,300);a.push(v);}
  function frame(){
    const t=now();
    if(document.hidden||phase!=='play'){last=0;pointer=0;return;}
    if(!firstRide)firstRide=t;
    if(last)record(frames,t-last);
    if(pointer){record(inputs,t-pointer);pointer=0;}
    last=t;
  }
  function update(state){
    if(!menu&&state.phase==='menu')menu=now();
    if(phase!==state.phase){last=0;pointer=0;down=false;}
    if(phase!=='play'&&state.phase==='play'&&!play)play=now();
    phase=state.phase;seed=state.seed||seed;
  }
  document.addEventListener('click',e=>{if(!firstRide&&e.target.closest('[data-mode]'))startPress=now();},true);
  document.addEventListener('pointerdown',e=>{down=phase==='play'&&e.target.tagName==='CANVAS';},true);
  document.addEventListener('pointermove',e=>{if(down&&!pointer&&phase==='play'&&e.target.tagName==='CANVAS')pointer=now();},true);
  for(const name of ['pointerup','pointercancel','blur'])window.addEventListener(name,()=>down=false);
  document.addEventListener('visibilitychange',()=>{last=pointer=0;down=false;});
  const round=x=>Number(x.toFixed(1));
  function percentile(values,q){
    if(!values.length)return null;
    const sorted=[...values].sort((a,b)=>a-b);
    return round(sorted[Math.min(sorted.length-1,Math.ceil(sorted.length*q)-1)]);
  }
  function report(){
    const total=frames.reduce((a,b)=>a+b,0);
    return {platform:window.Telegram?.WebApp?.platform||'browser',browser:navigator.userAgent,
      viewport:[innerWidth,innerHeight],devicePixelRatio,seed,menuReadyMs:menu?Math.round(menu):null,
      navigationToRideMs:firstRide?Math.round(firstRide):null,
      startButtonToFrameMs:firstRide&&startPress?Math.round(firstRide-startPress):null,
      measuredFrames:frames.length,meanFPS:total?round(1000*frames.length/total):null,
      frameP95Ms:percentile(frames,.95),slowestFrameMs:percentile(frames,1),
      dragSamples:inputs.length,dragToFrameP50Ms:percentile(inputs,.5),dragToFrameP95Ms:percentile(inputs,.95)};
  }
  function show(){
    const r=report(),dialog=document.createElement('dialog'),title=document.createElement('h2'),body=document.createElement('pre');
    title.textContent='Замеры заезда';body.style.cssText='white-space:pre-wrap;overflow-wrap:anywhere;font:14px/1.5 system-ui';
    const v=(n,s='')=>n==null?'нет замера':n+s;
    body.textContent=[r.platform+' · '+r.viewport.join(' × '),'Меню готово за '+v(r.menuReadyMs,' мс'),
      'От открытия страницы до первого кадра заезда: '+v(r.navigationToRideMs,' мс'),
      'От кнопки до первого кадра: '+v(r.startButtonToFrameMs,' мс'),'Средний FPS: '+v(r.meanFPS),
      '95% кадров до '+v(r.frameP95Ms,' мс')+' ('+r.measuredFrames+' кадров)',
      'От движения пальца до конца кадра Unity: '+v(r.dragToFrameP50Ms,' мс')+' (медиана), '+v(r.dragToFrameP95Ms,' мс')+' (95%)',
      'Движений измерено: '+r.dragSamples,'Seed: '+seed].join('\n');
    const copy=document.createElement('button');copy.textContent='Скопировать результаты';
    copy.onclick=async()=>{try{await navigator.clipboard.writeText(body.textContent+'\n'+r.browser);copy.textContent='Скопировано';}
      catch{const selection=getSelection(),range=document.createRange();range.selectNodeContents(body);selection.removeAllRanges();selection.addRange(range);}};
    const close=document.createElement('button');close.textContent='Закрыть';close.onclick=()=>dialog.close();
    dialog.style.cssText='max-width:min(92vw,420px);max-height:85vh;overflow:auto;border:0;border-radius:16px;padding:20px;color:#17364a;background:#f8fbfe';
    dialog.append(title,body,copy,close);dialog.onclose=()=>dialog.remove();document.body.append(dialog);dialog.showModal();
  }
  window.MountainMetrics={frame,update,report,show};
})();
