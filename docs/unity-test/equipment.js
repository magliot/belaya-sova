window.MountainEquipment=(()=>{
  const kits={
    fireboard:{hero:'owl',slot:'gear',tab:'Доска',name:'Доска «Салют»',stock:'Обычная доска',effect:'Быстрее разгоняется, чуть медленнее перекантовывается. За доской летят искры.',tune:'+2 м/с² к разгону'},
    carveSkis:{hero:'bear',slot:'gear',tab:'Лыжи',name:'Лыжи «Ледорез»',stock:'Обычные лыжи',effect:'Меньше бокового сноса в повороте. Скорость после поворота набирается медленнее.',tune:'быстрее гасится боковой снос'},
    orangeHelmet:{hero:'bear',slot:'headgear',tab:'Каска М',name:'Оранжевая каска «М»',stock:'Белая каска «М»',effect:'Мишаня встаёт на 0,15 с быстрее после удара, который погасила защитная каска.',tune:'ещё −0,15 с на восстановление'},
    pinkGoggles:{hero:'owl',slot:'headgear',tab:'Каска Совы',name:'Розовая каска Совы',stock:'Синяя каска Совы',effect:'Сова подбирает мандарины на снегу на 25 см шире.',tune:'ещё +20 см к радиусу сбора'}
  };
  const primary=hero=>hero==='owl'?'fireboard':'carveSkis';
  function config(d,hero=d.hero,inspect){
    const item=kits[inspect],equipment=item?.slot==='gear'?inspect:d.gear[hero];
    const headgear=item?.slot==='headgear'?inspect:d.headgear[hero];
    return {equipment,ownedEquipment:d.ownedGear.includes(equipment)||item?.slot==='gear',
      tuning:d.tuning[equipment]||0,headgear,headTuning:d.tuning[headgear]||0,inspect:item?.slot||''};
  }
  function node(tag,cls,content){
    const el=document.createElement(tag);if(cls)el.className=cls;if(content)el.textContent=content;return el;
  }
  function wallet(d){
    const el=node('div','wallet'),icon=node('i','feather');icon.setAttribute('aria-hidden','true');
    el.append(icon,'Перья '+MountainSaves.featherBalance(d)+'  ·  🍊 '+d.coins);return el;
  }
  function buyButton(profile,btn,id,currency,label,again,kit){
    const d=profile.data,cost=MountainSaves.price(d,id,currency,kit);
    const funds=currency==='feathers'?MountainSaves.featherBalance(d):d.coins;
    const button=btn(label+(cost===undefined?' · максимум':' · '+cost+(currency==='feathers'?' перьев':' 🍊')),
      ()=>{if(profile.buy(id,currency,kit))again();},cost===undefined||funds<cost);
    button.dataset.buy=id;button.dataset.currency=currency;return button;
  }
  function render(body,profile,btn,selected,select){
    const d=profile.data,item=kits[selected],refresh=()=>select(selected);
    body.append(wallet(d));const tabs=node('div','shelf-tabs');
    for(const [id,title] of [...Object.entries(kits).map(([id,k])=>[id,k.tab]),['supplies','Бусты']]){
      const tab=btn(title,()=>select(id));tab.dataset.product=id;tab.setAttribute('aria-pressed',id===selected);tabs.append(tab);
    }
    body.append(tabs);
    if(!item){
      for(const [key,label] of [['helmets','Каска на старт · запас '+d.helmets+'/3'],['boost','Кофе +0,8 с · '+d.boost+'/5'],['mag','Магнит +2 с · '+d.mag+'/5']])
        body.append(buyButton(profile,btn,key,'coins',label,refresh));
      return;
    }
    const product=node('section','product');product.append(node('h3','',item.name),node('p','',item.effect));
    const owned=d.ownedGear.includes(selected),equipped=d[item.slot][item.hero]===selected;
    const actions=node('div','store-actions');
    if(!owned)actions.append(buyButton(profile,btn,selected,'feathers','Купить',refresh));
    else{
      const equip=btn(equipped?'Снять':'Надеть',()=>{
        profile.change([item.slot],p=>{p[item.slot][item.hero]=equipped?'standard':selected;},item.hero);refresh();
      });equip.dataset.equip=selected;actions.append(equip);
      actions.append(buyButton(profile,btn,'tune','coins','Улучшить '+d.tuning[selected]+'/2',refresh,selected));
    }
    product.append(actions,node('p','', 'Улучшение: '+item.tune+'.'));body.append(product);
  }
  function bonuses(body,profile,btn,start,refresh){
    const d=profile.data;body.append(wallet(d));
    for(const [id,title,desc] of [['tubing','Плюшки','Скоростной слалом на плюшке. Собирай подарки и мандарины.'],['sled','Снегоход','75 секунд, чтобы сбить снеговиков и собрать подарки.']]){
      const card=node('section','bonus-card');card.append(node('h3','',title),node('p','',desc));
      const actions=node('div','store-actions');
      if(d.bonuses.includes(id)){const go=btn('Поехали!',()=>start(id));go.dataset.mode=id;actions.append(go);}
      else for(const via of ['feathers','coins'])actions.append(buyButton(profile,btn,id,via,'Открыть',refresh));
      card.append(actions);body.append(card);
    }
  }
  function label(d,hero){return kits[d.gear[hero]]?.name||kits[primary(hero)].stock;}
  return {config,render,bonuses,label,primary,heroFor:id=>kits[id]?.hero};
})();
