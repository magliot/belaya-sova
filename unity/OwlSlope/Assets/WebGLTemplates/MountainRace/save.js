/* Save schema shared with the Three.js release. Legacy local and cloud records are read-only. */
(function(root) {
  'use strict';
  const defaults={best:0,coins:0,hero:'owl',muted:false,mag:0,boost:0,helmets:0,sens:4,coll:[],fis:[],
    meet:{yeti:0,feathers:0,bestStage:0},mdone:0,missions:null,vol:{music:6,sfx:8,vox:8}};
  const words=['EMCO','СОВА','ШАХТЕРСК'];
  defaults.fisContexts=[];defaults.fisIntroSeen=false;defaults.testerKey='';
  function mergeContexts(a,b){
    const result=[];
    for(const n of [...a,...b]){
      const rule=Math.floor(n/1000);
      if(Number.isInteger(n)&&rule>=1&&rule<=10&&n%1000>0&&!result.includes(n)&&result.filter(v=>Math.floor(v/1000)===rule).length<2)result.push(n);
    }
    return result;
  }
  const ranked=s=>s.mode==='normal'&&s.phase==='over'&&s.rankedResult===true;
  const missions=[
    ['gates','Ворота','gates',[12,25,40,60]],['combo','Ворота подряд','maxCombo',[6,12,20,30]],
    ['coins','Мандарины','coins',[25,50,90,140]],['dist','Метры','metres',[400,900,1600,2500]],
    ['tricks','Трюки','tricks',[2,5,9,14]],['polite','Вежливые обгоны','polite',[2,5,8,12]],
    ['score','Очки','score',[1500,4000,8000,15000]],['stage','Этап','stage',[2,3,4,6]],
    ['feather','Золотые перья','feathers',[1,1,2,2]]];
  const upgrades=[280,385,510,660,850];
  const helmetPrice=50,featherPrice=10;
  const items={fireboard:['owl','gear'],carveSkis:['bear','gear'],orangeHelmet:['bear','headgear'],pinkGoggles:['owl','headgear']};
  const bonusPrices={tubing:3000,sled:5000};
  defaults.gear={owl:'standard',bear:'standard'};
  defaults.headgear={owl:'standard',bear:'standard'};
  defaults.ownedGear=[];defaults.tuning={fireboard:0,carveSkis:0,orangeHelmet:0,pinkGoggles:0};
  defaults.bonuses=[];defaults.payments={};
  const clone=v=>JSON.parse(JSON.stringify(v));
  const count=v=>Number.isFinite(v)?Math.max(0,Math.floor(v)):0;
  function weekStart(ms){
    const shift=3*3600000,d=new Date(ms+shift);
    return Date.UTC(d.getUTCFullYear(),d.getUTCMonth(),d.getUTCDate()-(d.getUTCDay()+6)%7)-shift;
  }
  const obj=v=>v&&typeof v==='object'&&!Array.isArray(v);
  function parse(raw){try{return JSON.parse(raw);}catch{return null;}}
  function clean(raw={}){
    const d=clone(defaults);
    if(!obj(raw))return d;
    for(const k of ['best','coins','mag','boost','helmets','mdone'])d[k]=count(raw[k]);
    d.hero=raw.hero==='bear'?'bear':'owl';d.muted=raw.muted===true;
    d.sens=Number.isFinite(raw.sens)?Math.min(10,Math.max(1,Math.round(raw.sens))):4;
    d.mag=Math.min(5,d.mag);d.boost=Math.min(5,d.boost);
    for(const [key,[hero,slot]] of Object.entries(items)){
      if(Array.isArray(raw.ownedGear)&&raw.ownedGear.includes(key))d.ownedGear.push(key);
      if(d.ownedGear.includes(key)){
        if(raw[slot]?.[hero]===key||raw[slot]?.[hero]==null)d[slot][hero]=key;
        d.tuning[key]=Math.min(2,count(raw.tuning?.[key]));
      }
    }
    if(obj(raw.vol))for(const k of Object.keys(d.vol))if(Number.isFinite(raw.vol[k]))d.vol[k]=Math.min(10,count(raw.vol[k]));
    if(obj(raw.meet))d.meet={...d.meet,...raw.meet};
    d.meet.feathers=count(d.meet.feathers);
    d.bonuses=Array.isArray(raw.bonuses)?Object.keys(bonusPrices).filter(k=>raw.bonuses.includes(k)):
      Object.keys(bonusPrices).filter(k=>d.meet.prizeFeather||k==='sled'&&d.meet.goldenOwl);
    for(const id of [...d.ownedGear,...d.bonuses]){
      const currency=raw.payments?.[id];
      if(currency==='feathers'||currency==='coins'&&id in bonusPrices)d.payments[id]=currency;
    }
    if(Array.isArray(raw.fis))d.fis=[...new Set(raw.fis.filter(n=>Number.isInteger(n)&&n>=1&&n<=10))];
    if(Array.isArray(raw.fisContexts))d.fisContexts=mergeContexts([],raw.fisContexts);
    d.fisIntroSeen=raw.fisIntroSeen===true;
    if(/^[a-f0-9]{32}$/.test(raw.testerKey||''))d.testerKey=raw.testerKey;
    if(Array.isArray(raw.coll))d.coll=raw.coll.filter(v=>obj(v)&&Number.isInteger(v.w)&&words[v.w]&&Number.isInteger(v.i)&&words[v.w][v.i]);
    if(Array.isArray(raw.missions))d.missions=raw.missions.filter(m=>obj(m)&&missions.some(([id])=>id===m.id)&&m.n>0&&Number.isFinite(m.reward)).slice(0,3);
    return d;
  }
  // Legacy precedence matches cloud restore in docs/index.html, including an older record's unlocks.
  function legacyMerge(local,cloud,uid){
    if(!uid||!cloud||cloud.uid!==uid)return clean(local);
    const preferred=count(cloud.best)>=count(local.best)?{...local,...cloud}:local;
    const out=clean(preferred);
    for(const k of ['goldenOwl','prizeFeather'])if(local.meet?.[k]||cloud.meet?.[k])out.meet[k]=true;
    out.bonuses=[...new Set([...out.bonuses,...clean(local).bonuses,...clean(cloud).bonuses])];
    return out;
  }
  function completeMissions(d){
    d.missions=d.missions||[];
    while(d.missions.length<3){
      const available=missions.filter(([id])=>!d.missions.some(m=>m.id===id));
      const chosen=available[Math.floor(Math.random()*available.length)],level=Math.min(3,Math.floor(d.mdone/4));
      d.missions.push({id:chosen[0],n:chosen[3][level],reward:40+30*level+10*Math.floor(Math.random()*3)});
    }
  }
  function owner(app){const id=app?.initDataUnsafe?.user?.id;return app?.initData&&Number.isSafeInteger(id)&&id>0?id:0;}
  function create({storage,app,channel='test',now=Date.now,onChange=()=>{},cloudWait=3000}){
    const uid=owner(app),scope=uid?'tg'+uid+'_':'',legacy='bsova_'+scope;
    const preview=channel==='progression';
    const cloudKey='unity_'+(preview?'progression':channel==='production'?'release':'test')+'_v1';
    const key='bsova_'+scope+cloudKey, cloud=uid&&app?.isVersionAtLeast?.('6.9')?app.CloudStorage:null;
    const get=k=>{try{return parse(storage?.getItem(k));}catch{return null;}};
    const put=(k,v)=>{try{storage?.setItem(k,JSON.stringify(v));}catch{}};
    const legacyLocal={};
    if(!preview)for(const k of Object.keys(defaults)){const v=get(legacy+k);if(v!==null)legacyLocal[k]=v;}
    const valid=v=>obj(v)&&v.schema===1&&v.uid===uid&&obj(v.data);
    const cached=get(key),storedScores=get(key+'_scores');
    let pendingScores=Array.isArray(storedScores)?storedScores:[];
    let record=valid(cached)?{...cached,data:clean(cached.data)}:
      {schema:1,uid,updated:0,lastRun:'',rows:[],data:clean(legacyLocal)};
    const initial=clone(record.data);
    if(!Array.isArray(record.rows))record.rows=[];
    let writing=false,queued=false,readyForWrite=!cloud,changed=false,loaded,lastCloudError='';
    const edited=new Set(),gearChoices={gear:new Set(),headgear:new Set()};
    function sendCloud(){
      if(!cloud||writing||!readyForWrite||!queued)return;
      const envelope={...record,rows:undefined,recent:undefined};
      const encoded=JSON.stringify(envelope);
      if(new TextEncoder().encode(encoded).length>4096){lastCloudError='Большой профиль сохранён на устройстве.';return;}
      writing=true;queued=false;
      try{cloud.setItem(cloudKey,encoded,err=>{
        writing=false;lastCloudError=err?'Облачное сохранение недоступно.':'';
        if(queued)sendCloud();
      });}catch{writing=false;lastCloudError='Облачное сохранение недоступно.';}
    }
    function notify(){onChange(record.data);}
    function commit(keys,gearHero=record.data.hero){
      changed=true;for(const k of keys)edited.add(k);
      for(const slot of Object.keys(gearChoices))if(keys.includes(slot))gearChoices[slot].add(gearHero);
      record.updated=Math.max(now(),record.updated+1);put(key,record);queued=true;sendCloud();notify();
    }
    function mergeCloud(values){
      const old=parse(values?.save),remote=parse(values?.[cloudKey]),localSnapshot=valid(cached)?cached:null;
      let previous=localSnapshot||null;
      if(valid(remote)&&(!previous||remote.updated>previous.updated))previous=remote;
      const migrated=legacyMerge(legacyLocal,preview?null:old,uid), merged=previous?clean(previous.data):migrated;
      if(localSnapshot&&valid(remote)){
        const other=clean((previous===remote?localSnapshot:remote).data);
        for(const [kit,[hero,slot]] of Object.entries(items)){
          if(!other.ownedGear.includes(kit))continue;
          if(!merged.ownedGear.includes(kit)){
            merged.ownedGear.push(kit);merged[slot][hero]=other[slot][hero];
            if(other.payments[kit])merged.payments[kit]=other.payments[kit];
          }
          merged.tuning[kit]=Math.max(merged.tuning[kit],other.tuning[kit]);
        }
        for(const bonus of other.bonuses)if(!merged.bonuses.includes(bonus)){
          merged.bonuses.push(bonus);if(other.payments[bonus])merged.payments[bonus]=other.payments[bonus];
        }
        merged.meet.feathers=Math.max(merged.meet.feathers,other.meet.feathers);
      }
      merged.bonuses=[...new Set([...merged.bonuses,...migrated.bonuses])];
      merged.best=Math.max(merged.best,migrated.best);
      for(const flag of ['prizeFeather','goldenOwl'])if(migrated.meet[flag])merged.meet[flag]=true;
      if(changed){
        // A slow Telegram callback must preserve edits already made during this session.
        const known=new Set([...merged.ownedGear,...merged.bonuses]);
        const paidElsewhere=record.data.bonuses.reduce((refund,id)=>refund+
          (!initial.bonuses.includes(id)&&known.has(id)&&record.data.payments[id]==='coins'?bonusPrices[id]:0),0);
        for(const k of edited)if(k in defaults){
          if(k==='best')merged.best=Math.max(record.data.best,merged.best);
          else if(k==='meet'){
            for(const name of ['prizeFeather','goldenOwl'])if(record.data.meet[name])merged.meet[name]=true;
            merged.meet.bestStage=Math.max(count(merged.meet.bestStage),count(record.data.meet.bestStage));
            for(const name of ['gifts','aidKits','feathers','yeti'])merged.meet[name]=Math.max(0,count(merged.meet[name])+count(record.data.meet[name])-count(initial.meet[name]));
          }else if(k==='ownedGear')merged.ownedGear=[...new Set([...merged.ownedGear,...record.data.ownedGear])];
          else if(k==='bonuses')merged.bonuses=[...new Set([...merged.bonuses,...record.data.bonuses])];
          else if(k==='payments'){for(const [id,via] of Object.entries(record.data.payments))if(!known.has(id))merged.payments[id]=via;}
          else if(k in gearChoices){for(const hero of gearChoices[k])merged[k][hero]=record.data[k][hero];}
          else if(k==='tuning'){for(const kit of Object.keys(initial.tuning))merged.tuning[kit]=Math.max(merged.tuning[kit],record.data.tuning[kit]);}
          else if(k==='fis')merged.fis=[...new Set([...merged.fis,...record.data.fis])];
          else if(k==='fisContexts')merged.fisContexts=mergeContexts(merged.fisContexts,record.data.fisContexts);
          else if(k==='testerKey')merged.testerKey=merged.testerKey||record.data.testerKey;
          else if(k==='coll'){
            for(const c of record.data.coll)if(!merged.coll.some(entry=>entry.w===c.w&&entry.i===c.i))merged.coll.push(c);
          }else if(['coins','helmets','mdone','boost','mag'].includes(k)){
            merged[k]=Math.max(0,merged[k]+record.data[k]-initial[k]+(k==='coins'?paidElsewhere:0));
            if(k==='boost'||k==='mag')merged[k]=Math.min(5,merged[k]);
          }
          else merged[k]=record.data[k];
        }
      }else if(previous){record.updated=previous.updated||0;record.lastRun=previous.lastRun||'';}
      record.data=merged;completeMissions(merged);readyForWrite=true;
      if(previous||changed)put(key,record);
      if(queued)sendCloud();notify();
    }
    return {
      get data(){return record.data;},get rows(){return record.rows;},get cloudError(){return lastCloudError;},key,uid,
      load(){
        if(loaded)return loaded;
        completeMissions(record.data);
        if(!cloud){loaded=Promise.resolve(record.data);return loaded;}
        loaded=new Promise(resolve=>{
          // Late responses can still import the old phone without replacing menu choices made meanwhile.
          const timer=setTimeout(()=>{lastCloudError='Облако пока недоступно. Используется профиль устройства.';resolve(record.data);},cloudWait);
          try{cloud.getItems(preview?[cloudKey]:['save',cloudKey],(err,values)=>{
            clearTimeout(timer);
            if(!err){lastCloudError='';mergeCloud(values);}
            else lastCloudError='Облако пока недоступно. Используется профиль устройства.';
            resolve(record.data);
          });}catch{clearTimeout(timer);lastCloudError='Облако пока недоступно.';resolve(record.data);}
        });return loaded;
      },
      change(keys,edit,hero){edit(record.data);commit(keys,hero);},
      get pendingScores(){return pendingScores;},
      savePending(scores){pendingScores=scores;put(key+'_scores',scores);},
      testerKey(){
        if(!record.data.testerKey){
          record.data.testerKey=Array.from(crypto.getRandomValues(new Uint8Array(16)),n=>n.toString(16).padStart(2,'0')).join('');
          commit(['testerKey']);
        }
        return record.data.testerKey;
      },
      buy(kind,currency,upgradeItem){
        const s=record.data,cost=price(s,kind,currency,upgradeItem);
        const feathers=kind in items||kind in bonusPrices&&currency==='feathers';
        if(cost==null||(feathers?featherBalance(s):s.coins)<cost)return false;
        if(!feathers)s.coins-=cost;
        if(kind in items||kind in bonusPrices){
          s.payments[kind]=feathers?'feathers':'coins';
          const fields=['payments'];if(!feathers)fields.push('coins');
          if(kind in items){
            const [hero,slot]=items[kind];s.ownedGear.push(kind);s[slot][hero]=kind;
            commit([...fields,'ownedGear',slot],hero);
          }else{s.bonuses.push(kind);commit([...fields,'bonuses']);}
        }else if(kind==='tune'){
          s.tuning[upgradeItem||s.gear[s.hero]]++;commit(['coins','tuning']);
        }else{s[kind]++;commit(['coins',kind]);}
        return true;
      },
      unlock(state){
        const d=record.data,keys=[];
        if(state.feather&&!d.meet.prizeFeather){d.meet.prizeFeather=true;keys.push('meet');}
        if(Array.isArray(state.fis)){
          const added=state.fis.filter(n=>Number.isInteger(n)&&n>=1&&n<=10&&!d.fis.includes(n));
          if(added.length){d.fis.push(...added);keys.push('fis');}
        }
        if(Array.isArray(state.fisContexts)){
          const next=mergeContexts(d.fisContexts,state.fisContexts);
          if(next.length!==d.fisContexts.length){d.fisContexts=next;keys.push('fisContexts');}
        }
        let needed=count(state.lettersTotal);
        while(needed>d.coll.length){
          let w=0;while(w<words.length&&d.coll.filter(c=>c.w===w).length>=words[w].length)w++;
          if(w===words.length)break;
          const i=d.coll.filter(c=>c.w===w).length;d.coll.push({w,i,ch:words[w][i],date:new Date(now()).toLocaleDateString('ru-RU'),dist:count(state.metres)});
          keys.push('coll');
        }
        if(keys.length)commit(keys);
      },
      finish(id,state){
        if(!id||record.lastRun===id||record.recent?.includes(id)||state.mode==='review'||!['over','finish'].includes(state.phase))return false;
        const d=record.data,stats={...state,stage:count(state.stage)+1};
        completeMissions(d);
        d.missions=d.missions.filter(m=>{
          const spec=missions.find(t=>t[0]===m.id);
          if(count(stats[spec[2]])<m.n)return true;
          d.mdone++;return false;
        });
        completeMissions(d);d.coins+=count(state.coins);
        if(ranked(state))d.best=Math.max(d.best,count(state.score));
        d.meet.bestStage=Math.max(count(d.meet.bestStage),stats.stage);d.meet.gifts=count(d.meet.gifts)+count(state.gifts);
        d.meet.aidKits=count(d.meet.aidKits)+count(state.aidKits);
        d.meet.feathers=count(d.meet.feathers)+count(state.feathers);d.meet.yeti=count(d.meet.yeti)+count(state.encounters);
        record.lastRun=id;
        record.recent=[...(record.recent||[]),id].slice(-32);
        if(ranked(state))record.rows.push({score:count(state.score),dist:count(state.metres),hero:state.hero,date:now()});
        record.rows.sort((a,b)=>b.score-a.score||a.date-b.date);
        const week=record.rows.filter(r=>r.date>=weekStart(now())).slice(0,25);
        record.rows=[...new Set([...record.rows.slice(0,25),...week])].sort((a,b)=>b.score-a.score||a.date-b.date);
        commit(['missions','mdone','coins','best','meet']);return true;
      }
    };
  }
  function leaderboard({channel,leaderboardEndpoint,previewBoard},app,profile){
    const preview=channel==='progression'&&!!previewBoard;
    const endpoint=preview?previewBoard:channel==='production'?leaderboardEndpoint:null;
    const canSubmit=!!endpoint&&(preview||!!app?.initData);
    let sending;
    async function call(action,payload){
      const identity=preview?{tester:profile.testerKey()}:{initData:app?.initData||''};
      const response=await fetch(endpoint,{method:'POST',headers:{'Content-Type':'text/plain;charset=utf-8'},
        body:JSON.stringify({action,...identity,...payload})});
      const result=await response.json();if(!response.ok||!result.ok)throw Error(result.error||'Рейтинг недоступен');return result;
    }
    async function sendPending(){
      const results=new Map();
      while(profile.pendingScores.length){
        const score=profile.pendingScores[0],receipt=await call('submit',score);
        results.set(score.runId,receipt);
        profile.savePending(profile.pendingScores.filter(row=>row.runId!==score.runId));
      }
      return results;
    }
    function flush(){
      if(!canSubmit)return Promise.resolve(new Map());
      if(!sending)sending=sendPending().finally(()=>{sending=null;});
      return sending;
    }
    return {
      async top(period){
        if(endpoint)return call('leaderboard',{period});
        const age=period==='week'?weekStart(Date.now()):0;
        return {rows:profile.rows.filter(r=>r.date>=age).slice(0,25).map(r=>({...r,name:'Мой заезд'})),local:true};
      },
      flush,
      async submit(s,id){
        if(!canSubmit||!id||!ranked(s))return null;
        const queue=profile.pendingScores;
        if(!queue.some(row=>row.runId===id))profile.savePending([...queue,{runId:id,scoring:2,score:count(s.score),
          dist:count(s.metres),dur:+Number(s.elapsed).toFixed(1),hero:s.hero,stage:count(s.stage)+1,
          // Keep the receipt compatible with already queued results from the fork-based course.
          coins:count(s.coins),gates:count(s.gates),rings:count(s.rings),preciseGates:0,
          preciseRings:0,rules:s.fis?.length||0}]);
        return (await flush()).get(id)||null;
      }
    };
  }
  function featherBalance(d){
    return Math.max(0,count(d.meet?.feathers)-Object.values(d.payments||{}).filter(v=>v==='feathers').length*featherPrice);
  }
  function price(d,kind,currency,upgradeItem){
    if(kind==='mag'||kind==='boost')return upgrades[d[kind]];
    if(kind==='helmets'&&d.helmets<3)return helmetPrice;
    if(kind in items&&!d.ownedGear.includes(kind)&&(!currency||currency==='feathers'))return featherPrice;
    if(kind in bonusPrices&&!d.bonuses.includes(kind))return currency==='feathers'?featherPrice:currency==='coins'?bonusPrices[kind]:undefined;
    const kit=upgradeItem||d.gear[d.hero];
    if(kind==='tune'&&d.ownedGear.includes(kit)&&d.tuning[kit]<2)return upgrades[d.tuning[kit]];
  }
  root.MountainSaves={create,leaderboard,legacyMerge,words,missions,upgrades,price,featherBalance,featherPrice,bonusPrices};
})(typeof window==='undefined'?globalThis:window);
