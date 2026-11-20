(function(){
  'use strict';
  const $=id=>document.getElementById(id), canvas=$('slope'), release=window.mountainRelease;
  const app=window.Telegram?.WebApp?.initData?window.Telegram.WebApp:null;
  const names={owl:'Сова',bear:'Мишаня'};
  const stages=['Учебный склон','Лесная тропа','Бугры','Сноупарк','Вершина','Чёрная трасса','Ночная трасса','Новогодний городок'];
  const modes={normal:'Спуск',sprint:'Короткий спуск',sled:'Снегоход',tubing:'Плюшки',review:'Осмотр трассы'};
  const fis=['Уважай других','Контролируй скорость','Выбирай траекторию','Обгоняй с запасом','Смотри по сторонам','Не стой где попало','Пешком — по краю','Соблюдай знаки','Помоги пострадавшему','Назови себя'];
  const ruleText=['Катайся так, чтобы никому не навредить и никого не напугать.','Подбирай скорость по своим силам, рельефу, снегу, погоде и людям вокруг.','Если ты сверху, выбирай линию безопасно для тех, кто ниже.','Обгоняй с любой стороны и оставляй место на манёвр.','Перед стартом и выездом на трассу оглянись вверх и вниз.','Избегай остановок в узких, плохо видимых местах. Упал — освободи трассу.','Поднимаешься или спускаешься пешком — иди у края трассы.','Следи за указателями и разметкой, держись в пределах сеток.','Заметил пострадавшего — остановись и позови патруль.','Участник или свидетель происшествия сообщает свои данные.'];
  const ruleHow=['1500 м без столкновений','объезд группы детей с запасом','пять вежливых обгонов','вежливый обгон лыжника','разъезд с ратраком','объезд сидящего','объезд пешехода у края','проезд сужения без касания','аптечка у пострадавшего','поделиться результатом'];
  const dances=[['sway','Ёлочка',()=>true,''],['floss','Снежный флосс',d=>d.meet.feathers>=3,'3 пера'],
    ['hop','Весёлые прыжки',d=>d.meet.gifts>=20,'20 подарков'],['dab','Победный дэб',d=>d.best>=5000,'5000 очков'],['champ','Танец чемпиона',d=>d.fis.length>=10,'10 правил FIS']];
  const panels={more:'Меню',settings:'Настройки',missions:'Задания',shop:'Улучшения',collection:'Коллекция',rules:'Правила FIS',top:'Рейтинг',dances:'Танцы',catalog:'Лист моделей'};
  let deviceStorage;try{deviceStorage=window.localStorage;}catch{}
  const profile=MountainSaves.create({storage:deviceStorage,app,channel:release.channel,onChange:changed});
  const ranking=MountainSaves.leaderboard(release,app,profile);
  const fixedSeed=new URLSearchParams(location.search).get('seed');
  let seedOverride=fixedSeed!==null&&/^\d{1,10}$/.test(fixedSeed)&&+fixedSeed<=2147483647?+fixedSeed:null;
  let player,ready=false,snapshot=null,panel='',run=null,finger=null,noticeTimer,rankPeriod='week',topRequest=0,requestPause=false;
  let catalogData,modelIndex=-1,group='all';
  function command(name,arg){
    if(!player)return;
    if(arg===undefined)player.SendMessage('Mountain race',name);
    else player.SendMessage('Mountain race',name,arg);
  }
  function setText(id,text){const el=$(id),v=String(text);if(el.textContent!==v)el.textContent=v;}
  const inRace=()=>snapshot?.phase==='play'||snapshot?.phase==='review';
  function changed(){
    window.MountainAudio?.settings(profile.data);
    if(!ready)return;
    updateMenu();
    if(snapshot?.phase==='menu'&&snapshot.hero!==profile.data.hero)command('ChooseHero',profile.data.hero);
  }
  function updateMenu(){
    const d=profile.data;setText('best',d.best);setText('bank',d.coins);
    for(const button of document.querySelectorAll('[data-hero]'))button.setAttribute('aria-pressed',button.dataset.hero===d.hero);
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
  function draw(s,prev){
    const home=s.phase==='menu',review=s.mode==='review'&&s.phase==='review';
    if(!home)panel='';
    $('mast').hidden=!home||!!panel;$('menu').hidden=!home||!!panel;
    $('panel').hidden=!home||!panel;$('hud').hidden=s.phase!=='play'&&!review;
    $('pause').hidden=review;$('paused').hidden=s.phase!=='pause';$('review').hidden=!review;
    const finished=s.phase==='over'||s.phase==='finish';
    if(!finished)$('shareSheet').hidden=true;
    $('result').hidden=!finished||!$('shareSheet').hidden;
    if(home)updateMenu();
    if(!$('hud').hidden){
      setText('metres',Math.floor(s.metres)+' м');setText('stage',stages[s.stage%8]||'Спуск');
      setText('pace',Math.round(s.speed||0)+' км/ч'+(s.multiplier>1?' · ×'+s.multiplier:''));
      setText('score',s.score);setText('coins',s.coins);
      $('helmets').setAttribute('aria-label','Каски '+s.helmets);
      for(const [i,img] of [...$('helmets').children].entries())img.style.opacity=i<s.helmets?1:.22;
      if(review){
        if(document.activeElement!==$('seek'))$('seek').value=Math.floor(s.metres);
        if(document.activeElement!==$('reviewAt'))$('reviewAt').value=Math.floor(s.metres);
        setText('reviewMetres',Math.floor(s.metres));$('reviewStage').value=String(s.stage%8);
      }
    }
    if(s.phase==='pause'){setText('pausedDist',modes[s.mode]+' · '+Math.floor(s.metres)+' м · трасса № '+s.seed);setText('exitPaused',s.metres>=30?'Завершить заезд':'В меню');$('pauseSense').value=profile.data.sens;setText('pauseSenseValue',profile.data.sens);}
    if(s.phase==='play'&&s.message&&(s.message!==prev?.message||s.eventId!==prev?.eventId)){
      setText('notice',s.message);$('notice').hidden=false;clearTimeout(noticeTimer);
      noticeTimer=setTimeout(()=>{$('notice').hidden=true;},1500);
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
    window.MountainMetrics?.update(s);window.MountainAudio?.update(s);
    if(ready)draw(s,prev);
    if(s.phase==='play'&&(requestPause||document.hidden||app?.isActive===false)){requestPause=false;command('PauseRide');return;}
    if(run&&s.mode!=='review'&&['play','finish','over'].includes(s.phase))profile.unlock(s);
    if(run&&s.mode!=='review'&&(s.phase==='finish'||s.phase==='over')){
      const bank=profile.data.coins,oldBest=profile.data.best;
      if(profile.finish(run.id,s)){
        setText('resultTitle',s.message||(s.phase==='finish'?'Финиш':'Заезд завершён'));setText('resultScore',s.score+' очков');
        const detail=s.mode==='sled'?'Снеговики '+(s.snowmen||0)+' · подарки '+(s.gifts||0):s.mode==='tubing'?'Ворота '+s.gates+' · подарки '+(s.gifts||0):'Ворота '+s.gates+' · трюки '+s.tricks+' · кольца '+(s.rings||0);
        setText('resultStats',Math.floor(s.metres)+' м · '+detail+' · 🍊 '+s.coins);
        const seen=profile.data.meet.danceSeen||['sway'],fresh=dances.filter(([id,,open])=>open(profile.data)&&!seen.includes(id));
        const extra=[],reward=profile.data.coins-bank-s.coins;
        if(s.score>oldBest)extra.push('Новый рекорд!');if(reward>0)extra.push('За задания +'+reward+' 🍊');
        for(const [,title] of fresh)extra.push('Открыт танец «'+title+'»');
        if(fresh.length)profile.change(['meet'],d=>d.meet.danceSeen=[...seen,...fresh.map(([id])=>id)]);
        setText('resultExtra',extra.join(' · '));$('resultExtra').hidden=!extra.length;
        $('resultBonuses').hidden=!s.feather;
        setText('rankStatus',release.channel==='test'?'Результат в личном тестовом рейтинге.':app?'Отправляем результат…':'Рекорд сохранён на устройстве.');
        const current=run.id;ranking.submit(s).then(r=>{
          if(r&&current===run?.id)setText('rankStatus','Место за неделю #'+r.weekRank+' · за всё время #'+r.rank);
        }).catch(()=>{if(current===run?.id)setText('rankStatus','Сервер рейтинга недоступен. Рекорд сохранён на устройстве.');});
      }
    }
  }
  window.onMountainState=onState;
  function start(mode,retry=false){
    if(!ready)return;
    const d=profile.data;
    if(mode==='tubing'&&!d.meet.prizeFeather||mode==='sled'&&!d.meet.prizeFeather&&!d.meet.goldenOwl)return;
    const seed=retry&&run?run.seed:seedOverride??(crypto.getRandomValues(new Uint32Array(1))[0]&0x7fffffff);
    const config={mode,seed,sensitivity:d.sens,helmets:d.helmets,boost:d.boost,mag:d.mag,collectedLetters:d.coll.length};
    if(mode==='review'){
      $('reviewRate').value='0';$('reviewLane').value='0';$('reviewEffect').value='normal';
      $('reviewHero').value=d.hero;$('reviewRide').value='normal';setReviewBounds();
    }
    run={id:crypto.randomUUID?.()||String(Date.now())+Math.random(),seed,mode};requestPause=false;panel='';
    command('ChooseHero',d.hero);performance.mark('mountain-start-input');command('StartRace',JSON.stringify(config));
    if((mode==='normal'||mode==='sprint')&&config.helmets>0)profile.change(['helmets'],p=>{p.helmets--;});
    canvas.focus();
  }
  function leave(){
    const restoreHero=snapshot?.hero!==profile.data.hero;
    finger=null;requestPause=false;run=null;panel='';command('ReleaseSteer');command('Menu');
    if(restoreHero)command('ChooseHero',profile.data.hero);
  }
  function pause(){
    if(finger){finger=null;command('ReleaseSteer');}
    if(snapshot?.phase==='play')command('PauseRide');
    else if(snapshot?.phase==='review'){$('reviewRate').value='0';command('ReviewRate',0);}
  }
  function suspended(){if(run&&snapshot?.phase!=='pause')requestPause=true;pause();if(run)ranking.checkpoint(snapshot);}
  function restoreViewport(){
    const height=app?.viewportHeight||window.visualViewport?.height||window.innerHeight;
    if(height>0)document.documentElement.style.setProperty('--mountain-height',Math.round(height)+'px');
  }
  function back(){
    const dialog=document.querySelector('dialog[open]');if(dialog){dialog.close();return;}
    if(!$('shareSheet').hidden){$('shareSheet').hidden=true;$('result').hidden=false;return;}
    if(panel==='catalog'&&modelIndex>=0){modelIndex=-1;switchPanel('catalog');return;}
    if(panel){switchPanel(panel==='more'?'':panel==='catalog'?'settings':'more');return;}
    if(snapshot?.phase==='play'){pause();return;}
    if(snapshot?.phase&&snapshot.phase!=='menu')leave();
  }
  const releaseFinger=e=>{
    if(finger?.id!==e.pointerId)return;
    const f=finger;finger=null;
    if(e.type==='pointerup'&&snapshot?.air&&!f.tricked&&performance.now()-f.at<300&&Math.hypot(e.clientX-f.x,e.clientY-f.y)<20)command('AirTrick','tap');
    else if(e.type==='pointerup'&&snapshot?.air&&f.pending&&!f.tricked)command('AirTrick',f.pending);
    command('ReleaseSteer');
  };
  canvas.addEventListener('pointerdown',e=>{
    if(snapshot?.phase!=='play'||finger||e.button!==0)return;
    const at=performance.now();finger={id:e.pointerId,x:e.clientX,y:e.clientY,rx:e.clientX,ry:e.clientY,at,since:at,pending:null,tricked:false};
    canvas.setPointerCapture(e.pointerId);command('BeginSteer');canvas.focus();
  });
  canvas.addEventListener('pointermove',e=>{
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
  async function showTop(body){
    const request=++topRequest;const buttons=document.createElement('div');buttons.className='row';
    for(const [period,title] of [['week','Неделя'],['all','Всё время']])buttons.append(btn(title,()=>{rankPeriod=period;switchPanel('top');},period===rankPeriod));
    body.append(buttons);const hint=document.createElement('p');body.append(hint);
    hint.textContent='Загрузка…';
    try{
      const result=await ranking.top(rankPeriod);if(request!==topRequest||panel!=='top')return;
      hint.textContent=result.local?'Личные заезды на этом устройстве.':result.me?.rank?'Твоё место #'+result.me.rank:'';
      result.rows.forEach((r,i)=>body.append(stat((i+1)+'. '+r.name+' · '+(names[r.hero]||'')+' · '+r.dist+' м',r.score)));
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
    if(next==='more'){
      for(const k of ['top','missions','shop','collection','rules','dances','settings'])body.append(btn(panels[k],()=>switchPanel(k)));
    }else if(next==='top'){showTop(body);}
    else if(next==='shop'){
      body.append(stat('Мандарины',d.coins));
      for(const [k,name] of [['boost','Кофе'],['mag','Магнит'],['helmets','Каска на старт']]){
        const price=k==='helmets'?250:MountainSaves.upgrades[d[k]];
        const label=price===undefined?name+' · максимум':name+' · '+(k==='helmets'?'в запасе '+d[k]:'уровень '+(d[k]+1))+' · '+price+' 🍊';
        body.append(btn(label,()=>{profile.buy(k);switchPanel('shop');},price===undefined||d.coins<price));
      }
    }else if(next==='settings'){
      body.append(slider('Чувствительность',d.sens,1,10,v=>profile.change(['sens'],p=>p.sens=v)));
      body.append(btn(d.muted?'Включить звук':'Выключить звук',()=>{profile.change(['muted'],p=>p.muted=!p.muted);switchPanel('settings');}));
      for(const [k,title] of [['music','Музыка'],['sfx','Эффекты'],['vox','Голоса']])body.append(slider(title,d.vol[k],0,10,v=>profile.change(['vol'],p=>p.vol[k]=v)));
      const label=document.createElement('label'),input=document.createElement('input');
      label.textContent='Номер трассы ';input.type='text';input.inputMode='numeric';input.maxLength=10;
      input.value=seedOverride??'';input.placeholder='Случайный';input.onchange=()=>{seedOverride=/^\d{1,10}$/.test(input.value)&&Number(input.value)<=2147483647?Number(input.value):null;input.value=seedOverride??'';};label.append(input);body.append(label);
      body.append(btn('Осмотр трассы',()=>start('review')),btn('Лист моделей',()=>{modelIndex=-1;switchPanel('catalog');}),btn('Замеры',()=>window.MountainMetrics?.show()));
      if(profile.cloudError)body.append(profile.cloudError);
    }else if(next==='catalog'){showCatalog(body);}
    else if(next==='missions'){
      for(const m of d.missions||[]){const spec=MountainSaves.missions.find(t=>t[0]===m.id);body.append(stat(spec[1]+' · '+m.n,'+'+m.reward+' 🍊'));}
      body.append(stat('Выполнено',d.mdone));
    }else if(next==='rules'){
      fis.forEach((title,i)=>{
        const details=document.createElement('details'),summary=document.createElement('summary'),text=document.createElement('p');
        summary.textContent=(d.fis.includes(i+1)?'✓ ':'')+(i+1)+'. '+title;
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
      for(const [t,v] of [['Встречи с йети',d.meet.yeti],['Перья',d.meet.feathers],['Золотая сова',d.meet.goldenOwl?'✓':'—'],['Лучший этап',d.meet.bestStage]])body.append(stat(t,v||0));
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
    setText('shareHint','Удержи картинку, чтобы сохранить её.');$('result').hidden=true;$('shareSheet').hidden=false;profile.unlock({fis:[10]});
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
  $('closePanel').onclick=back;document.querySelector('[data-panel]').onclick=()=>switchPanel('more');
  $('pause').onclick=pause;$('resume').onclick=()=>{requestPause=false;command('ResumeRide');canvas.focus();};
  $('pauseSense').oninput=e=>{const v=Number(e.target.value);setText('pauseSenseValue',v);profile.change(['sens'],p=>p.sens=v);command('SetSensitivity',v);};
  $('again').onclick=$('restartPaused').onclick=()=>{if(run)start(run.mode,true);};
  $('exitResult').onclick=$('reviewExit').onclick=leave;$('exitPaused').onclick=()=>snapshot.metres>=30?command('EndRide'):leave();
  $('share').onclick=showShare;$('closeShare').onclick=back;$('sendShare').onclick=share;
  $('copyShare').onclick=async()=>{try{await navigator.clipboard.writeText(shareText+' '+shareUrl);setText('shareHint','Результат скопирован.');}catch{setText('shareHint',shareText+' '+shareUrl);}};
  $('seek').oninput=e=>command('Seek',+e.target.value);
  for(const [i,title] of stages.entries())$('reviewStage').add(new Option(title,String(i)));
  $('reviewStage').onchange=e=>command('Seek',+e.target.value*2500);
  $('reviewRate').onchange=e=>command('ReviewRate',+e.target.value);
  function setReviewBounds(){
    const ride=$('reviewRide').value,end=ride==='normal'?19999:ride==='sprint'?1199:1161;
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
  function failed(error){
    console.error('Unity startup:',String(error));$('loading').hidden=false;$('menu').hidden=true;
    setText('loadText','Не удалось загрузить 3D-игру. Проверь соединение и повтори загрузку.');
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
