window.MountainEquipment=(()=>{
  const types={
    owl:{key:'fireboard',name:'Доска «Салют»',stock:'Обычная доска',detail:'Быстрее набирает ход на уклоне и после кофе. Медленнее меняет направление.',finish:'Малиновая дека и цветные искры.'},
    bear:{key:'carveSkis',name:'Лыжи «Ледорез»',stock:'Обычные лыжи',detail:'Держат дугу с меньшим сносом. Медленнее разгоняются после поворота.',finish:'Бирюзовые лыжи и ледяные искры на канте.'}
  };
  function config(d){
    const equipment=d.gear[d.hero];
    return {equipment,ownedEquipment:d.ownedGear.includes(equipment),tuning:d.tuning[equipment]||0};
  }
  function render(body,profile,btn,stat,refresh){
    const d=profile.data,kit=types[d.hero];
    const own=d.ownedGear.includes(kit.key),equipped=d.gear[d.hero]===kit.key;
    function pick(key){profile.change(['gear'],p=>{p.gear[p.hero]=key;});refresh();}
    function buy(key,label){
      const cost=MountainSaves.price(d,key);
      body.append(btn(cost===undefined?label+' · максимум':label+' · '+cost+' 🍊',()=>{profile.buy(key);refresh();},cost===undefined||d.coins<cost));
    }
    function note(value){const text=document.createElement('p');text.textContent=value;body.append(text);}
    body.append(stat('Кошелёк',d.coins+' 🍊'));
    body.append(btn(kit.stock+(equipped?'':' · выбрано'),()=>pick('standard'),!equipped));
    body.append(btn(kit.name+(equipped?' · выбрано':own?' · надеть':' · взять напрокат бесплатно'),()=>pick(kit.key),equipped));
    note(kit.detail+' В зачёте прокат и личный комплект едут одинаково.');
    if(!own){note(kit.finish);buy(kit.key,'Купить свой комплект');}
    else if(equipped){note('Настройка действует в свободном катании.');buy('tune','Настройка · '+d.tuning[kit.key]+'/2');}
    note('Каску можно взять на свободный или короткий спуск. За заезд можно использовать две каски, по одной за раз.');
    buy('helmets','Каска на старт · в запасе '+d.helmets);
    note('В свободном катании улучшения продлевают действие подбора на трассе.');
    buy('boost','Кофе · +0,8 с · уровень '+d.boost+'/5');buy('mag','Магнит · +2 с · уровень '+d.mag+'/5');
  }
  function label(d,hero){
    const kit=types[hero];
    return d.gear[hero]===kit.key?kit.name+(d.ownedGear.includes(kit.key)?'':' · прокат'):kit.stock;
  }
  return {config,render,label};
})();
