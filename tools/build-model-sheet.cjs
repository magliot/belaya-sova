const fs=require('node:fs');
const path=require('node:path');
const {chromium}=require(process.env.DANCE_BROWSER_MODULE||'playwright');

const groups=['Модели','Объекты из кода','Запасные формы'];
const cards=[];
function item(group,id,title,note,source,params={}){
  cards.push({group,id,title,note,source,params});
}
const imported=[
  ['bear','Мишаня','Лыжник. Модель меню и запечённая анимация спуска.'],
  ['owl','Белая Сова','Сноубордистка. Модель меню и запечённая анимация спуска.'],
  ['skier','Лыжник','Участник движения и инструктор лыжной группы.'],
  ['kid','Ребёнок на лыжах','Едет следом за инструктором.'],
  ['sitter','Сидящий лыжник','Встречается у края и в эпизоде с аптечкой.'],
  ['sboy','Юный сноубордист','Участник группы на досках.'],
  ['sman','Взрослый сноубордист','Попутчик на трассе.'],
  ['sinstr','Инструктор на доске','Ведёт группу. Цвета курток меняются кодом.'],
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

  ['goldenOwl','Летящая золотая сова','Модель из Tripo Studio, парит над трассой. Поимка открывает снегоход.']
];
for(const [id,title,note] of imported)item(0,id,title,note,id);
const authored=[
 ['gate-red','Красные ворота','Стойки и флаги можно заменить модульной моделью. Ореол и ширина прохода меняются в игре.','gate',{gap:120,col:0}],
 ['gate-blue','Синие ворота','Вторая расцветка слаломной пары.','gate',{gap:120,col:1}],
 ['airRing','Воздушное кольцо','Кандидат для резного обода с фасками. Свечение и диаметр зависят от прыжка.','airRing',{radius:1.2}],
 ['forest-sign','Указатель леса','Кандидат для деревянной рамы со снежной шапкой. Надпись и стрелка меняются.','stageProp',{theme:'forest'}],
 ['ridge-sign','Указатель бугров','Общая модель рамы пригодится для разных этапов.','stageProp',{theme:'moguls'}],
 ['park-sign','Указатель парка','Высокий вариант указателя.','stageProp',{theme:'park'}],
 ['rail','Бокс','Можно моделировать боковины. Верх совпадает с игровой поверхностью.','rail',{hh:.7,w:42,len:210}],
 ['park-ramp','Трамплин сноупарка','Три высоты. Для замены подойдут накладки на борта, профиль отрыва задаёт физика.','ramp',{park:true,hh:1.6,w:95,len:180}],
 ['mogul','Отдельный бугор','Сугроб, который Мишаня может снести.','mogul'],
 ['rollers','Поле гребней','Непрерывный рельеф совпадает с поверхностью столкновения.','rollers'],
 ['fence','Защитная сетка','Кандидат для стоек и креплений. Длина барьера переменная.','fence',{x1:50,x2:190}],
 ['arch','Арка этапа','Кандидат для фирменной деревянной арки. Название зависит от этапа.','arch',{stage:2}],
 ['finish','Финиш','Хороший кандидат для праздничной финишной конструкции.','finish'],
 ['lamp','Фонарь','Повторяемый объект. Один недорогой GLB может заменить всю серию.','lamp'],
 ['village','Дальний посёлок','Кандидат для набора из нескольких сахалинских домиков.','village'],
 ['mountain','Горы и долина','Дальний план. Важны крупные силуэты и низкая стоимость отрисовки.','mountain'],
 ['snow','Полотно трассы','Изгибы, уклон и борта повторяют физическую трассу.','snow'],
 ['aurora','Северное сияние','Полупрозрачная рисованная текстура на плоскости.','aurora'],
 ['track','Следы лыж','Повторяющиеся отпечатки на снегу.','track'],
 ['yeti-tracks','Следы йети','Предупреждают, с какой стороны выйдет йети.','yeti-tracks'],
 ['cable','Трос подъёмника','Соединяет соседние опоры точно по высоте.','cable'],
 ['ski','Лыжа','Геометрия меню и запасной позы. В запечённом движении используется снаряжение Blender.','ski'],
 ['board','Сноуборд','Доска для меню и запасной позы.','board'],
 ['pole','Лыжная палка','Длина и направление зависят от хвата.','pole'],
 ['ball','Снежок','Снаряд снеговика.','ball'],
 ['feather','Золотое перо','Коллекционная находка. Кандидат для скульптурной модели с рельефом опахала.','feather'],
 ['letter','Коллекционная буква','Динамическая буква из собираемого слова.','letter',{ch:'С'}],
 ['hud','Счёт, каски и мандарины','Плоские значки и табло интерфейса для чтения на телефоне.','hud'],
 ['particles','Снежные частицы','Снегопад, брызги и салюты используют частицы.','particles'],
];
for(const [id,title,note,source,params] of authored)item(1,id,title,note,source,params);
for(const [key,title,params] of [
 ['bear','Мишаня'],['owl','Сова'],['npc','Лыжник',{colHex:0x1859ab}],['npc','Сидящий лыжник',{sit:true,colHex:0xa64e31}],
 ['npc','Ребёнок',{kid:true,colHex:0x445599}],['cat','Ратрак',{vy:10}],['tree','Ель'],['tree','Сосна',{pine:true}],
 ['ramp','Трамплин',{w:100,hh:.8}],['rock','Валун'],['dog','Собака'],['snowman','Снеговик'],['yeti','Йети'],
 ['snowmobile','Патруль'],['cannon','Пушка'],['aid','Аптечка'],['gift','Подарок'],
 ['coin','Мандаринка'],['helmet','Каска'],['thermos','Кофе'],['magnet','Магнит'],['chair','Кресло'],['tower','Опора'],
 ])item(2,'reserve-'+key+'-'+cards.length,title,'Форма из кода до загрузки основной модели.',key,params);

function studio(){
  const fallback=new Map();
  const args=c=>({type:c.source,x:140,y:0,vx:90,vy:90,w:80,stage:1,colHex:0x3679aa,...c.params});
  function make(c){
    const k=c.source,o=args(c);o.h=100;o.w=c.params.w||80;
    if(c.group===2){
      if(k==='bear')return buildBear().root;
      if(k==='owl')return buildOwl().root;
      if(k==='tower'||k==='chair'){
        const model=(k==='tower'?towers:chairs)[0].clone();
        if(k==='tower')model.children.slice(-2).forEach(q=>model.remove(q));
        return model;
      }
      return FACT[k](o);
    }
    if(k==='bear'||k==='owl'){
      const h=HEROMODELS[k];poseGlb(h,{air:0,ang:0},'menu',0);
      return h.holder;
    }
    if(c.group===0&&NPCM[k])return NPCM[k].clone();
    if(c.group===0&&PROPS[k])return PROPS[k].clone();
    if(c.group===0&&WINTER[k])return winterClone(k);
    if(collectibleModels[k])return collectibleModels[k].clone();
    if(k==='sled')return playerSled.children[0].clone();
    if(k==='lamp')return lamps[0].clone();
    if(k==='cable'){const m=towers[0].userData.cab[0].clone();m.scale.set(.034,3,.034);m.rotation.z=1.35;return m;}
    if(k==='village'||k==='mountain'){
      const g=mountains.clone();
      if(k==='village'){for(const q of [...g.children])if(q.position.z>-399||q.position.z<-485||q.position.x<0||q.position.x>40)g.remove(q);}
      return g;
    }
    if(k==='snow'){fitPlane(piste,-96);return piste.clone();}
    if(k==='aurora'){const m=aurora.clone();m.material=m.material.clone();m.material.opacity=1;return m;}
    if(k==='rollers'||k==='yeti-tracks'){
      rollerSection(100000);
      const p=MOG.patches.at(-1);return (k==='rollers'?p.mesh:p.tracks).clone();
    }
    if(k==='track'){
      const g=new THREE.Group();for(const x of [-.2,.2]){const m=new THREE.Mesh(new THREE.PlaneGeometry(.1,2),trackMesh.material);m.rotation.x=-Math.PI/2;m.position.x=x;g.add(m);}return g;
    }
    if(k==='ski')return buildSki();if(k==='board')return buildBoard();
    if(k==='pole'){const m=buildPole();sizePole(m,1.05);return m;}
    const m=FACT[k](o);if(o.park||k==='rail'){o.m=m;fitParkSurface(o);}return m;
  }
  const sc=new THREE.Scene();sc.background=new THREE.Color(0xdbe8ee);
  sc.add(new THREE.HemisphereLight(0xffffff,0xb2bdca,1.9));
  const sun=new THREE.DirectionalLight(0xfff4df,2.2);sun.position.set(-3,5,4);sc.add(sun);
  const renderer=new THREE.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});renderer.setSize(320,230);
  const cam=new THREE.PerspectiveCamera(33,320/230,.001,10000);
  return {
    async setup(cards){
      for(const c of cards.filter(c=>c.group===2))fallback.set(c.id,make(c));
      loadHero('bear');loadHero('owl');loadTrackAssets();loadPatrol();loadPlayerSled();
    },
    ready(){return HEROMODELS.owl.glb&&HEROMODELS.bear.glb&&Object.keys(NPCM).length===13&&Object.keys(WINTER).length===8&&Object.keys(PROPS).length===3&&Object.keys(collectibleModels).length===2&&patrolModel&&playerSled.children.length===1;},
    shot(c){
      if(c.source==='hud'){
        const el=document.createElement('canvas');el.width=375;el.height=145;const ctx=el.getContext('2d');ctx.fillStyle='#dbe8ee';ctx.fillRect(0,0,375,145);
        drawRaceHUD(ctx,{width:375,height:812,safeTop:0,time:1,score:2160,distance:380,coins:24,speed:52,lives:2,multiplier:2,stage:'Лесная тропа'});return el.toDataURL('image/webp',.9);
      }
      if(c.source==='particles'){
        const g=new THREE.Group();for(let i=0;i<50;i++){const q=new THREE.Mesh(partMesh.geometry,new THREE.MeshBasicMaterial({color:i%3?0xffffff:0xffc454}));q.position.set(Math.sin(i*3)*i/50,Math.sin(i*7)*i/55,Math.cos(i)*.4);q.scale.setScalar(.045);g.add(q);}fallback.set(c.id,g);
      }
      const m=fallback.get(c.id)||make(c);
      const parent=m.parent,old=m.position.clone();sc.add(m);m.position.set(0,0,0);m.visible=true;m.updateMatrixWorld(true);
      const box=new THREE.Box3().setFromObject(m),center=box.getCenter(new THREE.Vector3()),size=box.getSize(new THREE.Vector3());
      const radius=Math.max(size.x/1.25,size.y,size.z*.85,.1)*2.5;
      const dir=new THREE.Vector3(.50,.32,1).normalize();
      if(['owl','bear','goldenOwl','feather','aid'].includes(c.source))dir.set(.26,.16,c.source==='bear'||c.source==='owl'?-1:1).normalize();
      if(['snow','track','yeti-tracks','rollers'].includes(c.source))dir.set(.32,.96,.75).normalize();
      cam.position.copy(center).addScaledVector(dir,radius);cam.lookAt(center);cam.near=radius/1000;cam.far=radius*10;cam.updateProjectionMatrix();
      renderer.render(sc,cam);const img=renderer.domElement.toDataURL('image/webp',.85);
      sc.remove(m);if(parent)parent.add(m);m.position.copy(old);return img;
    }
  };
}
async function main(){
 const browser=await chromium.launch({args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 try{
  const page=await browser.newPage();const html=fs.readFileSync('docs/index.html','utf8').replace(/requestAnimationFrame\(frame\);\s*<\/script>/, 'window.sheet=('+studio.toString()+')();\n</script>');
  await page.route('**/sheet-preview.html',r=>r.fulfill({body:html,contentType:'text/html; charset=utf-8'}));
  await page.route('**/vendor/telegram-web-app.js',r=>r.fulfill({body:''}));
  page.on('pageerror',console.error);await page.goto('http://127.0.0.1:8766/sheet-preview.html');
  await page.evaluate(cs=>sheet.setup(cs),cards);await page.waitForFunction(()=>sheet.ready(),null,{timeout:90000});
  fs.mkdirSync('docs/ui/models',{recursive:true});
  for(const c of cards){
    const img=await page.evaluate(c=>sheet.shot(c),c).catch(e=>{throw Error(c.id+': '+e.message)});
    fs.writeFileSync(path.join('docs/ui/models',c.id+'.webp'),Buffer.from(img.split(',')[1],'base64'));
  }
  const tmpl=fs.readFileSync('tools/model-sheet.template.html','utf8');
  const card=c=>`<figure data-group="${c.group}"><img src="ui/models/${c.id}.webp" loading="lazy" alt=""><figcaption><small>${groups[c.group]}</small><h2>${c.title}</h2><p>${c.note}</p></figcaption></figure>`;
  fs.writeFileSync('docs/models.html',tmpl.replace('<!--CARDS-->',cards.map(card).join('\n')));
  console.log(cards.length+' previews');
 }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
