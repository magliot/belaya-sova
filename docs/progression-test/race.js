(function(){
  'use strict';
  const $=id=>document.getElementById(id), canvas=$('slope'), release=window.mountainRelease;
  const app=window.Telegram?.WebApp?.initData?window.Telegram.WebApp:null;
  const names={owl:'Сова',bear:'Мишаня'};
  const stages=['Учебный склон','Лесная тропа','Бугры','Сноупарк','Вершина','Чёрная трасса','Ночная трасса','Новогодний городок'];
  const modes={normal:'Спуск',free:'Свободное катание',sprint:'Короткий спуск',sled:'Снегоход',tubing:'Плюшки',review:'Осмотр трассы'};
  const fis=['Уважай других','Контролируй скорость','Выбирай траекторию','Обгоняй с запасом','Смотри по сторонам','Не стой где попало','Пешком — по краю','Соблюдай знаки','Помоги пострадавшему','Назови себя'];
  const ruleText=['Катайся так, чтобы никому не навредить и никого не напугать.','Подбирай скорость по своим силам, рельефу, снегу, погоде и людям вокруг.','Если ты сверху, выбирай линию безопасно для тех, кто ниже.','Обгоняй с любой стороны и оставляй место на манёвр.','Перед стартом и выездом на трассу оглянись вверх и вниз.','Избегай остановок в узких, плохо видимых местах. Упал — освободи трассу.','Поднимаешься или спускаешься пешком — иди у края трассы.','Следи за указателями и разметкой, держись в пределах сеток.','Заметил пострадавшего — остановись и позови патруль.','Участник или свидетель происшествия сообщает свои данные.'];
  const ruleHow=['оставь запас при проезде мимо ученика','сбавь ход перед группой или на участке с плохой видимостью','выбери сторону заранее и сохрани просвет до человека','обгони движущегося лыжника или сноубордиста с запасом','после остановки осмотрись и дождись свободного выезда','остановись у края рядом с пострадавшим','пройди вдоль безопасной обочины','объедь ограждение со стороны стрелки','остановись рядом с пострадавшим и вызови патруль','назовись свидетелем прибывающему патрулю'];
  const dances=[['sway','Ёлочка',()=>true,''],['floss','Снежный флосс',d=>d.meet.feathers>=3,'3 пера'],
    ['hop','Весёлые прыжки',d=>d.meet.gifts>=20,'20 подарков'],['dab','Победный дэб',d=>d.best>=5000,'5000 очков'],['champ','Танец чемпиона',d=>d.fis.length>=10,'10 правил FIS']];
  const panels={settings:'Настройки',missions:'Задания',shop:'Прокат и магазин',collection:'Коллекция',rules:'Правила FIS',top:'Рейтинг',dances:'Танцы',catalog:'Лист моделей'};
  let deviceStorage;try{deviceStorage=window.localStorage;}catch{}
  const profile=MountainSaves.create({storage:deviceStorage,app,channel:release.channel,onChange:changed});
  const ranking=MountainSaves.leaderboard(release,app,profile);
  const fixedSeed=new URLSearchParams(location.search).get('seed');
  let seedOverride=fixedSeed!==null&&/^\d{1,10}$/.test(fixedSeed)&&+fixedSeed<=2147483647?+fixedSeed:null;
  let player,ready=false,snapshot=null,panel='',run=null,finger=null,noticeTimer,rankPeriod='all',topRequest=0,requestPause=false;
  const actions={brake:'Сбавить ход',stop:'Остановиться у края',patrol:'Вызвать патруль',witness:'Назваться свидетелем',walk:'Пешком по краю',look:'Осмотреть склон',join:'Выехать на трассу'};
  let catalogData,modelIndex=-1,group='all',menuFinger=null;
  let pendingStart=null,soundFailed=false;
  function sound(method,value){
    if(soundFailed)return;
    try{window.MountainAudio?.[method](value);}
    catch(error){soundFailed=true;console.warn('Mountain audio unavailable',error);}
  }
  function command(name,arg){
    if(!player)return;
    if(arg===undefined)player.SendMessage('Mountain race',name);
    else player.SendMessage('Mountain race',name,arg);
  }
  function setText(id,text){const el=$(id),v=String(text);if(el.textContent!==v)el.textContent=v;}
  const inRace=()=>snapshot?.phase==='play'||snapshot?.phase==='review';
  function changed(){
    sound('settings',profile.data);
    if(!ready)return;
    updateMenu();
    if(snapshot?.phase==='menu'&&snapshot.hero!==profile.data.hero)command('ChooseHero',profile.data.hero);
    if(snapshot?.phase==='menu')command('PreviewGear',JSON.stringify(MountainEquipment.config(profile.data)));
  }
  function updateMenu(){
    const d=profile.data;setText('best',d.best);setText('bank',d.coins);
    setText('ruleCount',d.fis.length+'/10');
    const playerName=app?.initDataUnsafe?.user?.first_name;
    $('profileName').hidden=!playerName;if(playerName)setText('profileName',playerName);
    $('mute').setAttribute('aria-pressed',String(d.muted));
    $('mute').setAttribute('aria-label',d.muted?'Включить звук':'Выключить звук');
    $('mute').firstElementChild.textContent=d.muted?'🔇':'🔊';
    for(const button of document.querySelectorAll('[data-hero]')){
      button.setAttribute('aria-pressed',button.dataset.hero===d.hero);
      button.querySelector('small').textContent=MountainEquipment.label(d,button.dataset.hero);
    }
    for(const button of $('menu').querySelectorAll('[data-mode]')){
      const mode=button.dataset.mode;if(mode!=='tubing'&&mode!=='sled')continue;
      button.disabled=mode==='tubing'?!d.meet.prizeFeather:!(d.meet.prizeFeather||d.meet.goldenOwl);
      button.querySelector('small').textContent=button.disabled?(mode==='sled'?'Сова или перо':'Золотое перо'):'Бонус · 75 с';
    }
  }
  function backUI(){
    if(!app?.isVersionAtLeast?.('6.1'))return;
    if(panel||snapshot&&snapshot.phase!=='menu')app.BackButton.show();else app.BackButton.hide();
  }
  function drawHud(s){
    const mode=s.courseMode||(s.mode==='review'?$('reviewRide').value:s.mode);
    const metres=Math.floor(s.metres),bonus=mode==='tubing'||mode==='sled';
    const warning=s.chaseSeconds>0||s.avalanche>0;
    setText('metres',metres+' м');setText('pace',Math.round(s.speed||0)+' км/ч');
    $('pace').classList.toggle('boosting',!!s.boost);
    $('multiplier').hidden=!bonus||!(s.multiplier>1);if(bonus&&s.multiplier>1)setText('multiplier','×'+s.multiplier);
    const action=actions[s.fisAction]||'';
    $('fisControls').hidden=s.phase!=='play'||(!action&&!s.fisCanLeave);
    $('fisAct').hidden=!action;if(action)setText('fisAct',action);
    $('fisLeave').hidden=!s.fisCanLeave;
    const stage=bonus?modes[mode]+' · '+Math.max(0,Math.ceil(75-s.elapsed))+' с':
      mode==='sprint'?'Финиш · '+Math.max(0,1200-metres)+' м':(metres>=20000?'Круг '+(1+Math.floor(metres/20000))+' · ':'')+(1+s.stage%8)+' · '+stages[s.stage%8];
    setText('stage',stage);
    let status='';
    if(mode==='sled')status='Снеговики '+(s.snowmen||0)+' · подарки '+(s.gifts||0);
    else if(warning)status=s.chaseSeconds>0?'Лавина · '+Math.ceil(s.chaseSeconds)+' с · '+Math.round(s.chaseGap)+' м':'Лавина';
    else if(s.boostSeconds>0)status='☕ Ускорение · '+s.boostSeconds.toFixed(1)+' с';
    else if(s.magnetSeconds>0)status='🧲 Магнит · '+s.magnetSeconds.toFixed(1)+' с';
    else if(s.nextLetter)status='Буква «'+s.nextLetter+'» '+(s.letterSide<0?'←':'→');
    else if(mode==='tubing')status=['Открытый склон','Еловый лес','Закат над бухтой'][s.bonusLocation||0]+' · ворота '+(s.gates||0);
    setText('rideStatus',status);$('rideStatus').hidden=!status;$('rideStatus').classList.toggle('warning',warning);
    $('avalancheMeter').hidden=!warning;
    if(warning)$('avalancheMeter').firstElementChild.style.transform='scaleX('+Math.max(0,Math.min(1,s.avalanche||0))+')';
    setText('score',s.score);$('score').style.setProperty('--digits',String(s.score).length);
    setText('coins',s.coins);$('coins').classList.toggle('many',s.coins>=1000);
    $('helmets').setAttribute('aria-label','Каски '+s.helmets);
    for(const [i,img] of [...$('helmets').children].entries())img.style.opacity=i<s.helmets?1:.23;
  }
  function draw(s,prev){
    const home=s.phase==='menu',review=s.mode==='review'&&s.phase==='review';
    if(!home)panel='';
    $('mast').hidden=!home||!!panel;$('menu').hidden=!home||!!panel;
    $('panel').hidden=!home||!panel;$('hud').hidden=s.phase!=='play'&&s.phase!=='pause'&&!review;
    $('pause').hidden=review||s.phase==='pause';$('paused').hidden=s.phase!=='pause';$('review').hidden=!review;
    const finished=s.phase==='over'||s.phase==='finish';
    if(!finished)$('shareSheet').hidden=true;
    $('result').hidden=!finished||!$('shareSheet').hidden;
    if(home){updateMenu();if(prev?.phase!=='menu')refreshMenuRank();}
    if(!$('hud').hidden){
      drawHud(s);
      if(review){
        if(document.activeElement!==$('seek'))$('seek').value=Math.floor(s.metres);
        if(document.activeElement!==$('reviewAt'))$('reviewAt').value=Math.floor(s.metres);
        setText('reviewMetres',Math.floor(s.metres));$('reviewStage').value=String(s.stage%8);
      }
    }
    if(s.phase==='pause'){
      if(prev?.phase!=='pause')$('pauseVolumes').replaceChildren(...volumeSliders());
      setText('pausedDist',modes[s.mode]+' · '+Math.floor(s.metres)+' м · трасса № '+s.seed);
      setText('exitPaused',run?.practice?'К осмотру':s.metres>=30?'Завершить заезд':'В меню');$('pauseSense').value=profile.data.sens;setText('pauseSenseValue',profile.data.sens);
    }
    if(s.phase==='play'&&s.message&&s.message!==prev?.message){
      clearTimeout(noticeTimer);
      const onTimer=(s.boostSeconds>0&&s.message.startsWith('Кофе'))||
        (s.magnetSeconds>0&&s.message.startsWith('Магнит'))||
        (s.chaseSeconds>0&&s.message.startsWith('Лавина!'));
      $('notice').hidden=onTimer;
      if(!onTimer){setText('notice',s.message);noticeTimer=setTimeout(()=>{$('notice').hidden=true;},1250);}
    }else if(s.phase!=='play')$('notice').hidden=true;
    if(s.phase!==prev?.phase){
      backUI();
      if(app?.isVersionAtLeast?.('6.2')){if(s.phase==='play')app.enableClosingConfirmation();else app.disableClosingConfirmation();}
      if(s.phase==='play'&&!performance.getEntriesByName('mountain-first-ride').length)performance.mark('mountain-first-ride');
    }
  }
  function onState(s){
    if(!s||typeof s.phase!=='string')return;
    const prev=snapshot;snapshot=s;
    if(pendingStart&&s.mode===pendingStart.mode&&s.seed===pendingStart.seed&&s.phase===(s.mode==='review'?'review':'play')){
      const {useHelmet,timeout}=pendingStart;pendingStart=null;clearTimeout(timeout);$('loading').hidden=true;
      if(run?.mode==='normal'&&!profile.data.fisIntroSeen)profile.change(['fisIntroSeen'],d=>{d.fisIntroSeen=true;});
      if(useHelmet)profile.change(['helmets'],d=>{d.helmets=Math.max(0,d.helmets-1);});
    }
    if(ready)draw(s,prev);
    window.MountainMetrics?.update(s);sound('update',s);
    if(s.phase==='play'&&(requestPause||document.hidden||app?.isActive===false)){requestPause=false;command('PauseRide');return;}
    if(run?.practice&&(s.phase==='finish'||s.phase==='over')){
      queueMicrotask(()=>{if(run?.practice)returnToInspection();});return;
    }
    if(run&&run.mode!=='review'&&['play','finish','over'].includes(s.phase))profile.unlock(s);
    if(run&&run.mode!=='review'&&(s.phase==='finish'||s.phase==='over')){
      const oldBest=profile.data.best,missions=profile.data.missions.slice();
      if(profile.finish(run.id,s)){
        setText('resultTitle',s.message||(s.phase==='finish'?'Финиш':'Заезд завершён'));setText('resultScore',s.score+' очков');
        $('breakdown').hidden=s.mode!=='normal';
        if(s.mode==='normal')$('breakdown').replaceChildren(...[
          ['Дистанция',s.scoreDistance],['Мандарины · '+s.coins,s.scoreFruit],['Ворота · '+s.gates,s.scoreGates],
          ['Кольца · '+s.rings,s.scoreRings],['Правила FIS · '+(s.fis?.length||0),s.scoreFis]
        ].map(([label,value])=>stat(label,'+'+(value||0))));
        const detail=s.mode==='sled'?'Снеговики '+(s.snowmen||0)+' · подарки '+(s.gifts||0)+' · бонус за сбор +'+Math.max(0,s.score-Math.floor(s.metres)):
          s.mode==='tubing'?'Ворота '+s.gates+' · подарки '+(s.gifts||0):
          'Ворота '+s.gates+' · серия '+(s.maxCombo||0)+' · трюки '+s.tricks+' · кольца '+(s.rings||0);
        const route=s.mode==='normal'?'Этап '+(s.stage+1)+' · '+stages[s.stage%8]:modes[s.mode];
        setText('resultStats',route+' · '+Math.floor(s.metres)+' м\n'+detail+'\n🍊 +'+s.coins+(s.feathers?' · перья '+s.feathers:'')+' · рекорд '+profile.data.best);
        const rule=s.fis?.at(-1);
        $('resultRule').hidden=!rule;
        if(rule)setText('resultRule','Правило FIS №'+rule+' · '+ruleText[rule-1]);
        $('resultTasks').open=false;
        $('resultTasks').lastElementChild.replaceChildren(...missions.map(m=>{
          const done=!profile.data.missions.includes(m);
          return stat(MountainSaves.missions.find(t=>t[0]===m.id)[1]+' · '+m.n,done?'Выполнено':'—');
        }));
        const seen=profile.data.meet.danceSeen||['sway'],fresh=dances.filter(([id,,open])=>open(profile.data)&&!seen.includes(id));
        const extra=[];
        if(s.rankedResult&&s.score>oldBest)extra.push('Новый рекорд!');
        for(const [,title] of fresh)extra.push('Открыт танец «'+title+'»');
        if(fresh.length)profile.change(['meet'],d=>d.meet.danceSeen=[...seen,...fresh.map(([id])=>id)]);
        setText('resultExtra',extra.join(' · '));$('resultExtra').hidden=!extra.length;
        $('resultBonuses').hidden=!s.feather;
        setText('rankStatus',!s.rankedResult?'Вне зачёта · мандарины в кошельке':release.previewBoard||app&&release.channel==='production'?'Отправляем результат…':'Результат сохранён на устройстве');
        const current=run.id;ranking.submit(s,current).then(r=>{
          if(r&&current===run?.id)setText('rankStatus',(release.previewBoard?'Тестовый рейтинг · ':'')+'Место #'+r.rank+' · за неделю #'+r.weekRank);
        }).catch(()=>{if(current===run?.id)setText('rankStatus','Сервер рейтинга недоступен. Рекорд сохранён на устройстве.');});
      }
    }
  }
  window.onMountainState=onState;
  function start(mode,retry=false){
    if(!ready||pendingStart)return;
    const d=profile.data;
    if(mode==='tubing'&&!d.meet.prizeFeather||mode==='sled'&&!d.meet.prizeFeather&&!d.meet.goldenOwl)return;
    try{
      const seed=retry&&run?run.seed:seedOverride??(crypto.getRandomValues(new Uint32Array(1))[0]&0x7fffffff);
      const config={mode,seed,sensitivity:d.sens,helmets:d.helmets,boost:d.boost,mag:d.mag,collectedLetters:d.coll.length,...MountainEquipment.config(d)};
      if(mode==='review'){
        $('reviewRate').value='0';$('reviewLane').value='0';$('reviewEffect').value='normal';
        $('reviewHero').value=d.hero;$('reviewRide').value='normal';setReviewBounds();
      }
      config.showFisHints=mode==='normal'&&!d.fisIntroSeen;
      run={id:crypto.randomUUID?.()||String(Date.now())+Math.random(),seed,mode};requestPause=false;panel='';
      if(snapshot.hero!==d.hero)command('ChooseHero',d.hero);
      performance.mark('mountain-start-input');
      pendingStart={mode,seed,useHelmet:(mode==='free'||mode==='sprint')&&config.helmets>0,
        timeout:setTimeout(()=>failed(Error('No start acknowledgement'),'Игра не ответила на запуск заезда. Попробуй перезагрузить игру.'),20000)};
      $('menu').hidden=true;$('mast').hidden=true;$('paused').hidden=true;$('result').hidden=true;
      $('loadBar').hidden=true;$('loading').hidden=false;setText('loadText','Выходим на склон…');
      command('StartRace',JSON.stringify(config));canvas.focus();
    }catch(error){failed(error,'Не удалось начать заезд. Попробуй перезагрузить игру.');}
  }
  function leave(){
    bankInterruptedRide();
    const restoreHero=snapshot?.hero!==profile.data.hero;
    finger=null;requestPause=false;run=null;panel='';command('ReleaseSteer');command('Menu');
    if(restoreHero)command('ChooseHero',profile.data.hero);
  }
  function pause(){
    if(finger){finger=null;command('ReleaseSteer');}
    if(snapshot?.phase==='play')command('PauseRide');
    else if(snapshot?.phase==='review'){$('reviewRate').value='0';command('ReviewRate',0);}
  }
  function bankInterruptedRide(){
    if(run&&run.mode!=='review'&&!run.practice&&snapshot?.phase==='pause'){
      profile.finish(run.id,{...snapshot,phase:'over',rankedResult:false});
    }
  }
  function suspended(){if(run&&snapshot?.phase!=='pause')requestPause=true;pause();}
  function restoreViewport(){
    const height=app?.viewportHeight||window.visualViewport?.height||window.innerHeight;
    if(height>0)document.documentElement.style.setProperty('--mountain-height',Math.round(height)+'px');
  }
  function back(){
    const dialog=document.querySelector('dialog[open]');if(dialog){dialog.close();return;}
    if(!$('shareSheet').hidden){$('shareSheet').hidden=true;$('result').hidden=false;return;}
    if(panel==='catalog'&&modelIndex>=0){modelIndex=-1;switchPanel('catalog');return;}
    if(panel){switchPanel(panel==='catalog'?'settings':'');return;}
    if(snapshot?.phase==='play'){pause();return;}
    if(snapshot?.phase&&snapshot.phase!=='menu')leave();
  }
  const releaseFinger=e=>{
    if(menuFinger?.id===e.pointerId){
      if(menuFinger.moved)command('EndMenuSpin');
      menuFinger=null;return;
    }
    if(finger?.id!==e.pointerId)return;
    const f=finger;finger=null;
    if(e.type==='pointerup'&&snapshot?.air&&!f.tricked&&performance.now()-f.at<300&&Math.hypot(e.clientX-f.x,e.clientY-f.y)<20)command('AirTrick','tap');
    else if(e.type==='pointerup'&&snapshot?.air&&f.pending&&!f.tricked)command('AirTrick',f.pending);
    command('ReleaseSteer');
  };
  canvas.addEventListener('pointerdown',e=>{
    if(ready&&snapshot?.phase==='menu'&&!panel&&!menuFinger&&e.button===0){
      menuFinger={id:e.pointerId,x:e.clientX,y:e.clientY,moved:false};canvas.setPointerCapture(e.pointerId);return;
    }
    if(snapshot?.phase!=='play'||finger||e.button!==0)return;
    const at=performance.now();finger={id:e.pointerId,x:e.clientX,y:e.clientY,rx:e.clientX,ry:e.clientY,at,since:at,pending:null,tricked:false};
    canvas.setPointerCapture(e.pointerId);command('BeginSteer');canvas.focus();
  });
  canvas.addEventListener('pointermove',e=>{
    if(menuFinger?.id===e.pointerId){
      if(snapshot?.phase!=='menu'||panel){releaseFinger(e);return;}
      const dx=e.clientX-menuFinger.x;
      if(!menuFinger.moved&&Math.abs(dx)<Math.max(3,Math.abs(e.clientY-menuFinger.y)))return;
      if(dx){menuFinger.moved=true;menuFinger.x=e.clientX;command('MenuSpin',dx);}
      return;
    }
    if(!finger||finger.id!==e.pointerId)return;const f=finger,t=performance.now();
    command('DragSteer',(e.clientX-f.x)/canvas.clientWidth);
    if(!snapshot?.air){f.rx=e.clientX;f.ry=e.clientY;f.since=t;f.pending=null;return;}
    if(t-f.since>260&&!f.pending){f.rx=e.clientX;f.ry=e.clientY;f.since=t;return;}
    const dx=e.clientX-f.rx,dy=e.clientY-f.ry,len=Math.hypot(dx,dy);
    const dir=Math.abs(dx)>Math.abs(dy)?dx<0?'left':'right':dy<0?'up':'down';
    if(len>110){command('AirTrick','big-'+dir);f.tricked=true;f.rx=e.clientX;f.ry=e.clientY;f.since=t;f.pending=null;}
    else if(len>42&&!f.pending){
      f.pending=dir;
      setTimeout(()=>{if(finger===f&&f.pending&&snapshot?.air){command('AirTrick',f.pending);f.tricked=true;f.pending=null;f.rx=e.clientX;f.ry=e.clientY;f.since=performance.now();}},150);
    }
  });
  for(const e of ['pointerup','pointercancel','lostpointercapture'])canvas.addEventListener(e,releaseFinger);
  function btn(label,action,disabled=false){
    const b=document.createElement('button');b.type='button';b.textContent=label;b.disabled=disabled;b.onclick=action;return b;
  }
  function stat(title,value){
    const row=document.createElement('div');row.className='stat';const t=document.createElement('span'),v=document.createElement('b');
    t.textContent=title;v.textContent=value;row.append(t,v);return row;
  }
  function slider(title,value,lo,hi,change){
    const label=document.createElement('label'),input=document.createElement('input'),out=document.createElement('output');
    out.textContent=value;label.append(title+' ',out);input.type='range';input.min=lo;input.max=hi;input.value=value;
    input.addEventListener('input',()=>{out.textContent=input.value;change(+input.value);});label.append(input);return label;
  }
  function volumeSliders(){
    return [['music','Музыка'],['sfx','Эффекты'],['vox','Голоса']].map(([key,title])=>
      slider(title,profile.data.vol[key],0,10,value=>profile.change(['vol'],d=>d.vol[key]=value)));
  }
  async function refreshMenuRank(){
    try{
      await ranking.flush();
      const list=await ranking.top('all'),leader=list.rows[0];
      setText('menuRank',list.me?'#'+list.me.rank+' · '+list.me.score+' очк.':leader?'#1 · '+leader.score+' очк.':'Пока нет заездов');
    }catch{setText('menuRank','Нет связи');}
  }
  async function showTop(body){
    const request=++topRequest;const buttons=document.createElement('div');buttons.className='row';
    for(const [period,title] of [['week','Неделя'],['all','Всё время']])buttons.append(btn(title,()=>{rankPeriod=period;switchPanel('top');},period===rankPeriod));
    body.append(buttons);const hint=document.createElement('p');body.append(hint);
    hint.textContent='Загрузка…';
    try{
      const result=await ranking.top(rankPeriod);if(request!==topRequest||panel!=='top')return;
      hint.textContent=(result.local?'На этом устройстве':release.previewBoard?'Тестовый общий зачёт':'Общий зачёт')+(result.me?' · твоё место #'+result.me.rank:'');
      let place=0,last=-1;
      result.rows.forEach((r,i)=>{if(r.score!==last)place=i+1;last=r.score;body.append(stat((r.rank||place)+'. '+r.name+' · '+(names[r.hero]||'')+' · '+r.dist+' м',r.score+' очк.'));});
      if(!result.rows.length)body.append('Заездов пока нет.');
    }catch{hint.textContent='Рейтинг сейчас недоступен.';}
  }
  async function showCatalog(body){
    try{
      if(!catalogData){
        body.textContent='Загружаем изображения…';
        const result=await fetch('catalog.json');if(!result.ok)throw Error('catalog');
        catalogData=await result.json();
      }
      if(panel!=='catalog')return;
      body.replaceChildren();
      const picture=(key,caption)=>{
        const img=document.createElement('img');img.loading='lazy';img.alt=caption;img.src=catalogData.images[key];return img;
      };
      if(modelIndex>=0){
        const item=catalogData.items[modelIndex];setText('panelTitle',item.name);
        body.append(picture(item.thumb,item.name));for(const line of item.details){const p=document.createElement('p');p.textContent=line;body.append(p);}
        for(const [key,label] of item.views){const figure=document.createElement('figure'),text=document.createElement('figcaption');text.textContent=label;figure.append(picture(key,label),text);body.append(figure);}
        return;
      }
      const description=document.createElement('p');description.textContent='Референсы моделей и объектов из кода. Снимки сделаны в версии Three.js.';
      const filters=document.createElement('div');filters.className='row';
      for(const [value,title] of [['all','Все'],['0','Модели'],['1','Из кода']])filters.append(btn(title,()=>{group=value;switchPanel('catalog');},group===value));
      const grid=document.createElement('div');grid.className='catalog-grid';body.append(description,filters,grid);
      for(const [i,item] of catalogData.items.entries()){
        if(group!=='all'&&Number(group)!==item.group)continue;
        const tile=btn(item.name,()=>{modelIndex=i;switchPanel('catalog');});tile.prepend(picture(item.thumb,''));grid.append(tile);
      }
    }catch{if(panel==='catalog')body.replaceChildren('Не удалось загрузить лист моделей.',btn('Повторить',()=>switchPanel('catalog')));}
  }
  function switchPanel(next){
    if(panel==='dances'&&next!=='dances')command('StopDance');
    panel=next;const body=$('panelBody');body.replaceChildren();if(snapshot)draw(snapshot,snapshot);backUI();
    if(!next)return;
    setText('panelTitle',panels[next]);const d=profile.data;
    if(next==='top'){showTop(body);}
    else if(next==='shop')MountainEquipment.render(body,profile,btn,stat,()=>switchPanel('shop'));
    else if(next==='settings'){
      body.append(slider('Чувствительность',d.sens,1,10,v=>profile.change(['sens'],p=>p.sens=v)));
      body.append(btn(d.muted?'Включить звук':'Выключить звук',()=>{profile.change(['muted'],p=>p.muted=!p.muted);switchPanel('settings');}));
      body.append(...volumeSliders());
      const label=document.createElement('label'),input=document.createElement('input');
      label.textContent='Номер трассы ';input.type='text';input.inputMode='numeric';input.maxLength=10;
      input.value=seedOverride??'';input.placeholder='Случайный';input.onchange=()=>{seedOverride=/^\d{1,10}$/.test(input.value)&&Number(input.value)<=2147483647?Number(input.value):null;input.value=seedOverride??'';};label.append(input);body.append(label);
      body.append(btn('Осмотр трассы',()=>start('review')),btn('Лист моделей',()=>{modelIndex=-1;switchPanel('catalog');}),btn('Замеры',()=>window.MountainMetrics?.show()));
      const guide=document.createElement('details'),heading=document.createElement('summary'),controls=document.createElement('p'),scoring=document.createElement('p');
      heading.textContent='Как играть и получать очки';
      controls.textContent='Веди пальцем влево и вправо, чтобы рулить. Герой разгоняется сам. Кофе прибавляет 65% скорости. Трамплин подбрасывает героя. В воздухе коснись экрана или проведи пальцем, чтобы сделать трюк до приземления.';
      scoring.textContent='В общий рейтинг идёт лучший спуск со старта до падения. Метр — 1 очко, мандарин — 3, чистые ворота — 8 или 18, кольцо — 14 или 28. Первое применение каждого правила FIS за спуск приносит 25 очков. Два трюка и перо на высокой линии открывают оба бонуса.';
      guide.append(heading,controls,scoring);body.append(guide);
      if(profile.cloudError)body.append(profile.cloudError);
    }else if(next==='catalog'){showCatalog(body);}
    else if(next==='missions'){
      for(const m of d.missions||[]){const spec=MountainSaves.missions.find(t=>t[0]===m.id);body.append(stat(spec[1],m.n));}
      body.append(stat('Выполнено',d.mdone));
    }else if(next==='rules'){
      fis.forEach((title,i)=>{
        const details=document.createElement('details'),summary=document.createElement('summary'),text=document.createElement('p');
        const known=d.fis.includes(i+1),mastered=d.fisContexts.filter(v=>Math.floor(v/1000)===i+1).length>=2;
        summary.textContent=(i+1)+'. '+title+' · '+(mastered?'Освоено':known?'Открыто':'Ещё не открыто');
        if(!known){body.append(stat((i+1)+'. '+title,'Ещё не открыто'));return;}
        text.textContent=ruleText[i]+' В игре — '+ruleHow[i]+'.';details.append(summary,text);body.append(details);
      });
    }else if(next==='collection'){
      for(const [i,word] of MountainSaves.words.entries()){
        const got=d.coll.filter(c=>c.w===i),open=i===0||d.coll.filter(c=>c.w===i-1).length>=MountainSaves.words[i-1].length;
        const row=document.createElement('div');row.className='letter-row';
        [...word].forEach((letter,n)=>{const mark=document.createElement('abbr'),entry=got.find(v=>v.i===n);
          mark.textContent=open||entry?letter:'?';if(entry){mark.className='found';mark.title=entry.date+' · '+entry.dist+' м';}row.append(mark);});
        body.append(row,stat(got.length===word.length?'Слово собрано':open?'Буквы на склоне':'После предыдущего слова',got.length+' / '+word.length));
        if(got.length){const last=got[got.length-1];body.append('Последняя буква '+last.ch+' · '+last.date+' · '+last.dist+' м');}
      }
      for(const [t,v] of [['Золотое перо · сноупарк',d.meet.prizeFeather?'Поймано':'Два трюка на высокой линии'],['Встречи с йети',d.meet.yeti],['Перья',d.meet.feathers],['Золотая сова',d.meet.goldenOwl?'✓':'—'],['Лучший этап',d.meet.bestStage?d.meet.bestStage+' · '+stages[(d.meet.bestStage-1)%8]:'—'],['Рекорд очков',d.best]])body.append(stat(t,v||0));
    }else if(next==='dances'){
      for(const [id,name,open,lock] of dances){
        const row=document.createElement('div');row.className='row';row.append(btn(open(d)?name:name+' · '+lock,()=>{switchPanel('');command('Dance',id);},!open(d)));
        if(open(d))row.append(btn('Поделиться',async()=>{
          const text=names[d.hero]+' танцует «'+name+'» в игре «Белая Сова».';
          try{if(app)app.openTelegramLink('https://t.me/share/url?url='+encodeURIComponent(shareUrl)+'&text='+encodeURIComponent(text));
            else if(navigator.share)await navigator.share({text,url:shareUrl});
            else await navigator.clipboard.writeText(text+' '+shareUrl);
          }catch(e){if(e.name!=='AbortError')body.append(text+' '+shareUrl);}
        }));body.append(row);
      }
    }
  }
  let shareText='',shareBlob=null,cardId=0;
  const shareUrl=release.shareUrl||(app?'https://t.me/belaya_sovabot/play'+(release.channel==='test'?'?startapp=unitytest':''):location.origin+location.pathname);
  function showShare(){
    if(!run||!snapshot)return;
    const s=snapshot,d=profile.data,c=document.createElement('canvas');c.width=1080;c.height=1350;
    const ctx=c.getContext('2d'),sky=ctx.createLinearGradient(0,0,0,1350);
    sky.addColorStop(0,'#183f67');sky.addColorStop(1,'#81bad3');ctx.fillStyle=sky;ctx.fillRect(0,0,1080,1350);
    const mountains=(points,color)=>{ctx.fillStyle=color;ctx.beginPath();ctx.moveTo(0,1350);for(const [x,y] of points)ctx.lineTo(x,y);ctx.lineTo(1080,1350);ctx.fill();};
    mountains([[0,820],[210,650],[430,785],[720,546],[1080,805]],'#86aac3');
    mountains([[0,980],[215,817],[438,945],[740,727],[1080,890]],'#c4dce4');
    mountains([[0,1030],[335,900],[650,976],[910,852],[1080,920]],'#f4f6ef');
    ctx.textAlign='center';
    const line=(message,y,size,color='#fff')=>{ctx.fillStyle=color;ctx.font='800 '+size+'px Rubik, sans-serif';ctx.fillText(message,540,y,986);};
    line('БЕЛАЯ СОВА',145,96);line(names[s.hero]+' · '+modes[s.mode],226,43,'#d4ecf3');
    if(app?.initDataUnsafe?.user?.first_name)line(app.initDataUnsafe.user.first_name,285,40,'#ffdc85');
    line(String(s.score),465,175,'#ffdc85');line(s.score>=d.best?'очков · лучший результат':'очков',527,45);
    line(Math.floor(s.metres)+' м · '+stages[s.stage%8],615,48);
    const finds=[];if(s.feathers)finds.push('Перьев '+s.feathers);if(s.encounters)finds.push('Встречи с йети '+s.encounters);if(s.letters)finds.push('Букв '+s.letters);
    if(finds.length)line(finds.join(' · '),991,40,'#234c61');
    const words=MountainSaves.words;
    let wi=words.findIndex((w,i)=>d.coll.filter(c=>c.w===i).length<w.length);if(wi<0)wi=words.length-1;
    const word=words[wi],tile=Math.min(110,Math.floor(920/word.length)-8);
    [...word].forEach((ch,i)=>{
      const x=540+(i-(word.length-1)/2)*(tile+8),found=d.coll.some(entry=>entry.w===wi&&entry.i===i);
      ctx.fillStyle=found?'#f3bc52':'#d9e3e5';ctx.fillRect(x-tile/2,1062,tile,112);ctx.fillStyle=found?'#234c61':'#738a98';ctx.font='800 72px Rubik, sans-serif';ctx.fillText(ch,x,1142);
    });
    line('EMCO · БЕЗОПАСНЫЙ ОТДЫХ',1285,38,'#234c61');
    shareText=names[s.hero]+' в игре «Белая Сова» · '+s.score+' очков, '+Math.floor(s.metres)+' м · '+stages[s.stage%8]+(finds.length?' · '+finds.join(', '):'');
    const id=++cardId;shareBlob=null;$('shareImage').src=c.toDataURL('image/png');c.toBlob(blob=>{if(id===cardId)shareBlob=blob;},'image/png');
    setText('shareHint','Удержи картинку, чтобы сохранить её.');$('result').hidden=true;$('shareSheet').hidden=false;
  }
  async function share(){
    try{
      if(app){app.openTelegramLink('https://t.me/share/url?url='+encodeURIComponent(shareUrl)+'&text='+encodeURIComponent(shareText));return;}
      const file=shareBlob&&new File([shareBlob],'belaya-sova.png',{type:'image/png'});
      if(file&&navigator.canShare?.({files:[file]}))await navigator.share({text:shareText,files:[file]});
      else if(navigator.share)await navigator.share({text:shareText,url:shareUrl});
      else setText('shareHint','Удержи картинку или скопируй результат кнопкой ниже.');
    }catch(e){if(e.name!=='AbortError')setText('shareHint','Удержи картинку или скопируй результат кнопкой ниже.');}
  }
  for(const b of document.querySelectorAll('[data-hero]'))b.onclick=()=>{
    profile.change(['hero'],p=>p.hero=b.dataset.hero);
  };
  for(const b of document.querySelectorAll('[data-mode]'))b.onclick=()=>start(b.dataset.mode);
  $('closePanel').onclick=back;
  for(const b of document.querySelectorAll('[data-panel]'))b.onclick=()=>switchPanel(b.dataset.panel);
  $('mute').onclick=()=>profile.change(['muted'],p=>p.muted=!p.muted);
  $('pause').onclick=pause;$('resume').onclick=()=>{requestPause=false;command('ResumeRide');canvas.focus();};
  $('fisAct').onclick=()=>{if(snapshot?.fisAction)command('FisAction',snapshot.fisAction);};
  $('fisLeave').onclick=()=>command('FisAction','leave');
  $('pauseSense').oninput=e=>{const v=Number(e.target.value);setText('pauseSenseValue',v);profile.change(['sens'],p=>p.sens=v);command('SetSensitivity',v);};
  function rideFromInspection(){
    if(snapshot?.phase!=='review'||!run)return;
    run.practice={metres:snapshot.metres,lane:+$('reviewLane').value,course:snapshot.courseMode};
    command('RideFromReview');canvas.focus();
  }
  function returnToInspection(restart=false){
    const from=run.practice;delete run.practice;
    const d=profile.data;requestPause=false;finger=null;
    command('StartRace',JSON.stringify({mode:'review',reviewRide:from.course,seed:run.seed,sensitivity:d.sens,boost:d.boost,mag:d.mag,collectedLetters:d.coll.length}));
    $('reviewEffect').value='normal';$('reviewRate').value='0';
    command('ReviewLane',from.lane);command('Seek',from.metres);
    if(restart){run.practice=from;command('RideFromReview');canvas.focus();}
  }
  $('reviewPlay').onclick=rideFromInspection;
  $('again').onclick=$('restartPaused').onclick=()=>{
    if(run?.practice)returnToInspection(true);else if(run){bankInterruptedRide();start(run.mode,true);}
  };
  $('exitResult').onclick=$('reviewExit').onclick=leave;
  $('exitPaused').onclick=()=>run?.practice?returnToInspection():snapshot.metres>=30?command('EndRide'):leave();
  $('share').onclick=showShare;$('closeShare').onclick=back;$('sendShare').onclick=share;
  $('copyShare').onclick=async()=>{try{await navigator.clipboard.writeText(shareText+' '+shareUrl);setText('shareHint','Результат скопирован.');}catch{setText('shareHint',shareText+' '+shareUrl);}};
  $('seek').oninput=e=>command('Seek',+e.target.value);
  for(const [i,title] of stages.entries())$('reviewStage').add(new Option(title,String(i)));
  $('reviewStage').onchange=e=>command('Seek',Math.floor(snapshot.metres/20000)*20000 + Number(e.target.value)*2500);
  $('reviewRate').onchange=e=>command('ReviewRate',+e.target.value);
  function setReviewBounds(){
    const ride=$('reviewRide').value,end=ride==='normal'?79999:ride==='sprint'?1199:ride==='sled'?2471:2196;
    $('seek').max=$('reviewAt').max=end;$('reviewStage').disabled=ride!=='normal';return end;
  }
  $('reviewHero').onchange=e=>command('ChooseHero',e.target.value);
  $('reviewRide').onchange=()=>{
    const d=profile.data,at=Math.min(snapshot.metres,setReviewBounds());
    command('StartRace',JSON.stringify({mode:'review',reviewRide:$('reviewRide').value,seed:run.seed,sensitivity:d.sens,collectedLetters:d.coll.length}));
    command('ReviewLane',+$('reviewLane').value);command('Seek',at);command('ReviewEffect',$('reviewEffect').value);command('ReviewRate',+$('reviewRate').value);
  };
  $('reviewLane').oninput=e=>command('ReviewLane',+e.target.value);
  $('reviewStep').onclick=()=>command('ReviewStep');
  $('reviewAt').onchange=e=>{const d=Number(e.target.value);if(Number.isFinite(d))command('Seek',Math.max(0,Math.min(+$('seek').max,d)));};
  $('reviewEarlier').onclick=()=>command('Seek',Math.max(0,Math.floor(snapshot.metres)-100));
  $('reviewLater').onclick=()=>command('Seek',Math.min(+$('seek').max,Math.floor(snapshot.metres)+100));
  $('reviewEffect').onchange=e=>command('ReviewEffect',e.target.value);
  $('reviewFold').onclick=()=>{const hidden=!$('reviewBody').hidden;$('reviewBody').hidden=hidden;setText('reviewFold',hidden?'Развернуть':'Свернуть');$('reviewFold').setAttribute('aria-expanded',!hidden);};
  $('retryLoad').onclick=()=>location.reload();
  document.addEventListener('keydown',e=>{if(e.key==='Escape'){e.preventDefault();back();}});
  document.addEventListener('visibilitychange',()=>{if(document.hidden)suspended();else restoreViewport();});
  window.addEventListener('pagehide',suspended);window.addEventListener('blur',suspended);
  window.addEventListener('resize',restoreViewport);window.visualViewport?.addEventListener('resize',restoreViewport);
  if(app){
    for(const event of ['viewportChanged','safeAreaChanged','contentSafeAreaChanged','fullscreenChanged'])app.onEvent(event,restoreViewport);
    app.onEvent('deactivated',suspended);app.onEvent('activated',restoreViewport);
    if(app.isVersionAtLeast('6.1'))app.BackButton.onClick(back);
    app.ready();app.expand();if(app.isVersionAtLeast('7.7'))app.disableVerticalSwipes();
    if(app.isVersionAtLeast('8.0')&&/android|ios/i.test(app.platform))app.requestFullscreen();
  }
  restoreViewport();
  function failed(error,message){
    if(pendingStart){clearTimeout(pendingStart.timeout);pendingStart=null;}
    ready=false;
    console.error('Unity startup:',String(error));$('loading').hidden=false;$('menu').hidden=true;
    setText('loadText',message||'Не удалось загрузить 3D-игру. Проверь соединение и повтори загрузку.');
    $('loadBar').hidden=true;$('retryLoad').hidden=false;
  }
  window.MountainShell={async boot(urls){
    try{
      performance.mark('mountain-load-start');
      const data=profile.load();
      await new Promise((ok,fail)=>{const script=document.createElement('script');script.src=urls.loader;script.onload=ok;script.onerror=()=>fail(Error('loader'));document.body.append(script);});
      player=await createUnityInstance(canvas,{dataUrl:urls.data,frameworkUrl:urls.framework,codeUrl:urls.code,
        streamingAssetsUrl:'StreamingAssets',companyName:'Белая Сова',productName:'MountainRace',productVersion:'1',
        devicePixelRatio:Math.min(devicePixelRatio||1,1.5)},v=>{$('loadBar').value=v;setText('loadText',Math.round(v*100)+'%');});
      await data;
      const limit=performance.now()+20000;
      while(!snapshot&&performance.now()<limit)await new Promise(r=>setTimeout(r,100));
      if(!snapshot)throw Error('Mountain race scene did not initialize');
      ready=true;$('loading').hidden=true;draw(snapshot,null);changed();performance.mark('mountain-menu-ready');
    }catch(e){failed(e);}
  }};
})();
