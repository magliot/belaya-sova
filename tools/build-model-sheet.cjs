const fs=require('node:fs');
const path=require('node:path');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');

const groups=['Модели','Объекты из кода'];
const cards=[];
function item(group,id,title,note,source,params={}){
  cards.push({group,id,title,note,source,params});
}
const imported=[
  ['pole','Лыжная палка','Часть модели Мишани: рукоятка находится в закрытой перчатке в меню и на спуске.'],
  ['bear','Мишаня','Лыжник. Модель меню и запечённая анимация спуска.'],
  ['owl','Белая Сова','Сноубордистка. Модель меню и запечённая анимация спуска.'],
  ['skier','Лыжник','Участник движения и инструктор лыжной группы.'],
  ['kid','Ребёнок на лыжах','Едет следом за инструктором.'],
  ['sitter','Сидящий лыжник','Встречается у края и в эпизоде с аптечкой.'],
  ['sboy','Юный сноубордист 09A','Участник группы на досках.'],
  ['sman','Взрослый сноубордист','Попутчик на трассе.'],
  ['sinstr','Инструктор на доске','Ведёт группу. Цвета курток меняются кодом.'],
  ['cat12A','Маленький ратрак 12A','На учебном склоне чередуется с исходным ратраком.'],
  ['sboyC','Юный сноубордист 09C','Малиновая куртка и шлем.'],
  ['ratrak','Ратрак','Едет попутно или навстречу.'],
  ['snowmobile','Лесной патруль','Снегоход с водителем, занимает одну полосу.'],
  ['sled','Снегоход игрока','Отдельный кузов. За рулём выбранный герой.'],
  ['chair','Кресло подъёмника','Движется по тросу.'],
  ['tower','Опора подъёмника','К опорам привязана линия троса.'],
  ['tree','Заснеженная ель','Первый вид леса.'],
  ['pine','Высокая сосна','Формирует внешний край лесной тропы.'],
  ['fir','Молодая ель','Более низкое дерево на проходах.'],
  ['ramp','Обычный трамплин','Импортированная модель на обычных трассах.'],
  ['rock_wide','Широкий валун','Препятствие с низким силуэтом.'],
  ['rock_tall','Высокий валун','Второй вариант камня.'],
  ['snowman','Снеговик','Бросает снежки. В бонусе его можно сбить.'],
  ['dog','Горная собака','Перебегает трассу.'],
  ['yeti','Йети','На буграх пересекает следы перед игроком. На вершине и ночью машет у края.'],
  ['snow_cannon','Снежная пушка','Препятствие с выбросом снега.'],
  ['mandarin','Мандаринка','Валюта. В бонусе участвует в серии.'],
  ['cocoa','Кофе','Ускоряет героя.'],
  ['helmet','Каска','Дополнительная жизнь.'],
  ['gift','Подарок','Особенно ценен в заезде на снегоходе.'],
  ['magnet','Магнит','Притягивает мандаринки.'],
  ['aid','Аптечка патруля','Белый кейс из Tripo Studio. Подбор у упавшего открывает правило FIS №9.'],

  ['feather','Золотое перо','Гранёная модель 36B на высокой линии сноупарка. Два завершённых трюка и контакт открывают бонусные заезды.']
];
for(const [id,title,note] of imported)item(0,id,title,note,id);
for(const [id,title] of [['tube60B','Плюшка · янтарная'],['tube60C','Плюшка · хвойная'],['tube61A','Плюшка · синяя'],['tube61C','Плюшка · малиновая']]){
  item(0,id,title,'Ездок на учебном склоне. Движется с обочины к середине, оставляя проезд с другой стороны.',id);
}
for(const [id,title] of [['snow26A','Снеговик в шапке'],['snow26B','Снеговик в цилиндре'],['snow26C','Снеговик в наушниках']]){
  item(0,id,title,'Стоит на учебном склоне. На этой трассе встречаются все три варианта.',id);
}
const notes={
 tube60B:'Борода и тёплая куртка выделяют лицо.',tube60C:'Тёмная кромка отделяет зелёную плюшку от снега.',
 tube61A:'Синий костюм и хвост видны в профиль.',tube61C:'Малиновый костюм выделяется на фоне хвои.',
 snow26A:'Красная шапка и синий шарф читаются издали.',snow26B:'Высокий тёмный цилиндр обозначает силуэт.',
 snow26C:'Синие наушники и жёлтый шарф выделяют голову и шею.',
 pole:'Вариант 05A с красным темляком и тёмным карбоновым древком. Ладонь удерживает рукоятку в меню и на спуске.',
 bear:'Проверять крупным планом: оранжевый колпачок над пальцами, древко ниже ладони. В стойке обе руки вынесены вперёд.',owl:'Тёмный шлем и холодная тень отделяют белую голову от снега. Следующим проходом усилить разницу силуэтов двух кантов.',
 skier:'Выделить плечи одним пятном и сделать палки темнее снега.',kid:'Сохранить низкий рост, дать шлему единый яркий цвет для чтения издалека.',sitter:'Подчеркнуть тёмной полосой нижний край куртки: сидячая фигура низко над снегом.',sboy:'Развести руки и корпус в профиль; окрасить нос доски.',sman:'Укрупнить контрастные перчатки: из-за спины райдер похож на лыжника.',sinstr:'Постоянная яркая спинка отличит ведущего от группы при смене вариантов куртки.',
 cat12A:'Короткая красная кабина и широкий отвал видны из обеих полос.',sboyC:'Малиновая окраска отделяет куртку от тёмной доски.',
 ratrak:'Оставить широкий отвал; подчеркнуть полосой его опасные углы.',snowmobile:'Отделить тёмную гусеницу от костюма водителя; яркую фару сохранить.',sled:'Отличить нос снегохода игрока от патрульного по окраске капота.',chair:'Утолстить силуэт сиденья, оставить открытым просвет под подвесом.',tower:'Оставить тёмный контур опоры, добавить цвет у основания на высоте игрока.',tree:'Разделить белые ярусы более тёмными промежутками.',pine:'Высокий силуэт работает по краю дороги; не увеличивать ширину ветвей.',fir:'Сохранить просветы между ветвями; на солнце затемнить нижнюю хвою.',ramp:'Обозначить нос и линию отрыва одним контрастным цветом.',
 rock_wide:'Низкий край почти теряется: затемнить боковую грань под снежной шапкой.',rock_tall:'Сильный вертикальный контур; усилить переход от шапки к камню.',snowman:'Шарф и шляпа помогают распознаванию; сделать жест броска крупнее.',dog:'Дать движение хвосту: направление перебежки должно читаться сбоку.',yeti:'Затемнить кончики лап; расширить первый шаг перед пересечением следов.',snow_cannon:'Разнести по тону ствол и основание; оставить тёмное отверстие сопла.',
 mandarin:'Сохранить цельное оранжевое пятно, укрупнить зелёный лист.',cocoa:'Яркая кружка читается по цвету; расширить просвет ручки.',helmet:'Добавить контрастную полосу вдоль шлема, чтобы отличать его от валюты.',gift:'Сделать бант шире: мелкие ленты теряются на дистанции.',magnet:'Подчеркнуть светлые наконечники и открытый промежуток между ними.',aid:'Белый кейс на снегу нуждается в более широкой цветной полосе по торцу.',feather:'Три тона золота отделяют стержень от гранёных бородок; тёплый контур виден над снегом.'};
const authored=[
 ['gate-red','Красные ворота','Мягкие стойки 38C со снежинками на флагах. Ширина прохода зависит от уровня.','gate',{gap:120,col:0}],
 ['gate-blue','Синие ворота','Мягкие стойки 39C на синей линии.','gate',{gap:120,col:1}],
 ['airRing','Кольцо 40A','Синий обод с золотыми муфтами.','airRing',{radius:1.2,ringStyle:0}],
 ['ring40B','Кольцо 40B','Радужная подсветка средней линии.','airRing',{radius:1.2,ringStyle:1}],
 ['ring40C','Кольцо 40C','Ледяной обод высокой линии.','airRing',{radius:1.2,ringStyle:2}],
 ['forest-sign','Указатель леса','Зелёная табличка на деревянной консоли перед въездом в лес.','entrySign',{theme:'forest',caption:'ЛЕС',x:57}],
 ['ridge-sign','Указатель бугров','Охристая табличка перед полем бугров.','entrySign',{theme:'moguls',caption:'БУГРЫ',x:57}],
 ['park-sign','Указатель парка','Синяя табличка перед въездом в сноупарк.','entrySign',{theme:'park',caption:'СНОУПАРК',x:57}],
 ['rail','Бокс','Можно моделировать боковины. Верх совпадает с игровой поверхностью.','rail',{hh:.7,w:42,len:210}],
 ['park-ramp','Трамплин сноупарка','Три высоты. Для замены подойдут накладки на борта, профиль отрыва задаёт физика.','ramp',{park:true,hh:1.6,w:95,len:180}],
 ['mogul','Отдельный бугор','Сугроб, который Мишаня может снести.','mogul'],
 ['rollers','Поле гребней','Непрерывный рельеф совпадает с поверхностью столкновения.','rollers'],
 ['fence','Защитная сетка','Три сетки 41A/B/C с разными стойками. Длина повторяет игровой барьер.','fence',{x1:50,x2:190}],
 ['arch','Арка этапа','Деревянная арка 45A со снежной крышей. Название зависит от этапа.','arch',{stage:2}],
 ['finish','Финиш','Клетчатый финиш 46A короткого спуска и бонусов.','finish'],
 ['lamp','Фонарь','Три фонаря 18A/B/C из игрового набора окружения.','lamp'],
 ['mountain','Горы и долина','Дальний план. Важны крупные силуэты и низкая стоимость отрисовки.','mountain'],
 ['snow','Полотно трассы','Изгибы, уклон и борта повторяют физическую трассу.','snow'],
 ['aurora','Северное сияние','Полупрозрачная рисованная текстура на плоскости.','aurora'],
 ['track','Следы лыж','Повторяющиеся отпечатки на снегу.','track'],
 ['yeti-tracks','Следы йети','Предупреждают, с какой стороны выйдет йети.','yeti-tracks'],
 ['cable','Трос подъёмника','Соединяет соседние опоры точно по высоте.','cable'],
 ['ski','Лыжа','Геометрия меню и запасной позы. В запечённом движении используется снаряжение Blender.','ski'],
 ['board','Сноуборд','Доска для меню и запасной позы.','board'],
 ['ball','Снежок','Снаряд снеговика.','ball'],
 ['letter','Коллекционная буква','Динамическая буква из собираемого слова.','letter',{ch:'С'}],
 ['hud','Счёт, каски и мандарины','Плоские значки и табло интерфейса для чтения на телефоне.','hud'],
 ['particles','Снежные частицы','Снегопад, брызги и салюты используют частицы.','particles'],
];
for(const [id,title,note,source,params] of authored)item(1,id,title,note,source,params);
function studio(){
  const effects=new Map();
  const args=c=>({type:c.source,x:140,y:0,vx:90,vy:90,w:80,stage:1,colHex:0x3679aa,...c.params});
  function make(c){
    const k=c.source,o=args(c);o.h=100;o.w=c.params.w||80;
    if(k==='bear'||k==='owl'){
      const h=HEROMODELS[k];poseGlb(h,{air:0,ang:0},'menu',0);
      return h.holder;
    }
    const asset={tower:'tower16C',tree:'tree19A',pine:'tree20A',fir:'tree21A',magnet:'magnet34B',snow_cannon:'cannon25A'}[k];
    if(asset)return kit[asset].clone();
    if(k==='snowmobile')return patrolModel.clone();
    if(k==='sman')return NPCV.sman[1].clone();
    if(k==='rock_wide'||k==='rock_tall')return FACT.rock({y:0,rockWide:k==='rock_wide'});
    if(k==='sboyC')return NPCV.sboy[1].clone();
    if(c.group===0&&NPCM[k])return NPCM[k].clone();
    if(c.group===0&&PROPS[k])return PROPS[k].clone();
    if(c.group===0&&WINTER[k])return winterClone(k);
    if(trainingCast[k])return trainingCast[k].clone();
    if(collectibleModels[k])return collectibleModels[k].clone();
    if(k==='sled')return playerSled.children[0].clone();
    if(k==='lamp')return lamps[0].clone();
    if(k==='cable'){const m=towers[0].userData.cab[0].clone();m.scale.set(.034,3,.034);m.rotation.z=1.35;return m;}
    if(k==='mountain')return mountains.children.find(q=>q.renderOrder===-12).clone();
    if(k==='snow'){fitPlane(piste,-96);return piste.clone();}
    if(k==='aurora'){const m=mountains.children.find(q=>q.renderOrder===-4).clone();m.material=m.material.clone();m.material.opacity=1;return m;}
    if(k==='rollers'||k==='yeti-tracks'){
      rollerSection(100000);
      const p=MOG.patches.at(-1);return (k==='rollers'?p.mesh:p.tracks).clone();
    }
    if(k==='track'){
      const g=new THREE.Group();for(const x of [-.2,.2]){const m=new THREE.Mesh(new THREE.PlaneGeometry(.1,2),trackMesh.material);m.rotation.x=-Math.PI/2;m.position.x=x;g.add(m);}return g;
    }
    if(k==='ski')return buildSki();if(k==='board')return buildBoard();
    if(k==='pole')return HEROMODELS.bear.heldPoles[0].clone();
    const m=FACT[k](o);if(o.park||k==='rail'){o.m=m;fitParkSurface(o);}return m;
  }
  const sc=new THREE.Scene();sc.background=new THREE.Color(0xdbe8ee);
  sc.add(new THREE.HemisphereLight(0xffffff,0xb2bdca,1.9));
  const sun=new THREE.DirectionalLight(0xfff4df,2.2);sun.position.set(-3,5,4);sc.add(sun);
  const studioRenderer=new THREE.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});studioRenderer.setSize(320,230);
  const cam=new THREE.PerspectiveCamera(33,320/230,.001,10000);
  return {
    async setup(cards){
      await Promise.all([loadTrackAssets('owl'),loadTrackAssets('bear'),loadPlayerSled()]);
    },
    ready(){return HEROMODELS.owl.glb&&HEROMODELS.bear.glb&&Object.keys(NPCM).length===13&&Object.keys(WINTER).length===8&&Object.keys(PROPS).length===3&&Object.keys(collectibleModels).length===2&&patrolModel&&playerSled.children.length===1;},
    slope(c){hero=['bear','pole'].includes(c.id)?'bear':'owl';reviewSeek(200);for(const o of G.objs)if(o.m)o.m.visible=false;G.p.x=200;render(1/30);
      const anchor=new THREE.Group();if(!['bear','owl','pole'].includes(c.id)){let m=c.id==='ramp'?FACT.ramp({w:75,hh:.8}):make(c);anchor.add(m);scene.add(anchor);courseGround(anchor.position,2,-208);if(['aid','mandarin','cocoa','helmet','gift','magnet','feather','chair'].includes(c.id))anchor.position.y+=c.id==='chair'?2:.9;}
      renderer.render(scene,camera);const img=renderer.domElement.toDataURL('image/webp',.86);scene.remove(anchor);return img;},
    shot(c){
      if(c.source==='hud'){
        const el=document.createElement('canvas');el.width=375;el.height=145;const ctx=el.getContext('2d');ctx.fillStyle='#dbe8ee';ctx.fillRect(0,0,375,145);
        drawRaceHUD(ctx,{width:375,height:812,safeTop:0,time:1,score:2160,distance:380,coins:24,speed:52,lives:2,multiplier:2,stage:'Лесная тропа'});return el.toDataURL('image/webp',.9);
      }
      if(c.source==='particles'){
        const g=new THREE.Group();for(let i=0;i<50;i++){const q=new THREE.Mesh(partMesh.geometry,new THREE.MeshBasicMaterial({color:i%3?0xffffff:0xffc454}));q.position.set(Math.sin(i*3)*i/50,Math.sin(i*7)*i/55,Math.cos(i)*.4);q.scale.setScalar(.045);g.add(q);}effects.set(c.id,g);
      }
      const m=effects.get(c.id)||make(c);
      const parent=m.parent,old=m.position.clone();sc.add(m);m.position.set(0,0,0);m.visible=true;m.updateMatrixWorld(true);
      const box=new THREE.Box3().setFromObject(m),center=box.getCenter(new THREE.Vector3()),size=box.getSize(new THREE.Vector3());
      const radius=Math.max(size.x/1.25,size.y,size.z*.85,.1)*2.5;
      const dir=new THREE.Vector3(.50,.32,1).normalize();
      if(['owl','bear','goldenOwl','feather','aid'].includes(c.source))dir.set(.26,.16,c.source==='bear'||c.source==='owl'?-1:1).normalize();
      if(['yeti','dog'].includes(c.source))dir.set(.54,.23,-1).normalize();
      if(['snow','track','yeti-tracks','rollers'].includes(c.source))dir.set(.32,.96,.75).normalize();
      cam.position.copy(center).addScaledVector(dir,radius);cam.lookAt(center);cam.near=radius/1000;cam.far=radius*10;cam.updateProjectionMatrix();
      studioRenderer.render(sc,cam);const img=studioRenderer.domElement.toDataURL('image/webp',.85);
      sc.remove(m);if(parent)parent.add(m);m.position.copy(old);return img;
    }
  };
}
async function main(){
 const browser=await chromium.launch({args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try{
  const page=await browser.newPage({viewport:{width:375,height:667},deviceScaleFactor:1});const html=fs.readFileSync('docs/index.html','utf8').replace(/requestAnimationFrame\(frame\);\s*<\/script>/, 'window.sheet=('+studio.toString()+')();\n</script>');
  await page.route('**/sheet-preview.html',r=>r.fulfill({body:html,contentType:'text/html; charset=utf-8'}));
  await page.route('**/vendor/telegram-web-app.js',r=>r.fulfill({body:''}));
  page.on('pageerror',console.error);await page.goto('http://127.0.0.1:8766/sheet-preview.html');
  await page.evaluate(cs=>sheet.setup(cs),cards);await page.waitForFunction(()=>sheet.ready(),null,{timeout:90000});
  fs.mkdirSync('docs/ui/models/slope',{recursive:true});
  for(const c of cards.filter(c=>process.argv.length===2||process.argv.slice(2).includes(c.id))){
    if(!c.group){const slope=await page.evaluate(c=>sheet.slope(c),c);fs.writeFileSync(path.join('docs/ui/models/slope',c.id+'.webp'),Buffer.from(slope.split(',')[1],'base64'));}
    const img=await page.evaluate(c=>sheet.shot(c),c).catch(e=>{throw Error(c.id+': '+e.message)});
    fs.writeFileSync(path.join('docs/ui/models',c.id+'.webp'),Buffer.from(img.split(',')[1],'base64'));
  }
  const tmpl=fs.readFileSync('tools/model-sheet.template.html','utf8');
  const card=c=>`<figure data-group="${c.group}"><img src="ui/models/${c.id}.webp" loading="lazy" alt=""><figcaption><small>${groups[c.group]}</small><h2>${c.title}</h2><p>${c.note}</p>${c.group===0?`<p>${notes[c.id]}</p><a href="ui/models/slope/${c.id}.webp">На трассе · 375×667</a>${['owl','bear'].includes(c.id)?` · <a href="review/${c.id==='bear'?'bear-grip-menu.webp':'owl-menu-after.jpg'}">Меню</a>`:''}`:''}</figcaption></figure>`;
  fs.writeFileSync('docs/models.html',tmpl.replace('<!--CARDS-->',cards.map(card).join('\n')));
  console.log(cards.length+' previews');
 }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
