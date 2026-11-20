import fs from 'node:fs';
import {execFileSync} from 'node:child_process';
fs.mkdirSync('docs/models/studies',{recursive:true});
// Code-only updates reuse the checked-in exports without rerunning texture conversion.
if(!process.argv.includes('--html-only'))for(const [kind,p] of Object.entries({bear:'art/animation-pilot/mishanya-ski-study.glb',owl:'art/owl-animation-pilot/sova-board-study.glb'}))execFileSync(process.execPath,['tools/shrink.mjs',p,'docs/models/studies/'+kind+'.glb','1','512'],{stdio:'inherit'});
let html=fs.readFileSync('docs/index.html','utf8');
html=html.replace(/<script>\s*\/\/ Telegram[\s\S]*?<\/script>/,'');
html=html.replace('<title>Белая Сова · Спуск</title>','<title>Белая Сова · Проба анимации</title>');
// Keep native Telegram sizing and controls, but isolate all test progress.
html=html.replace(/<script (?:defer )?src="motion-entry\.js[^"]*"><\/script>/,'');
html=html.replaceAll("CloudStorage.setItem('save'","CloudStorage.setItem('motion_test_save'");
html=html.replaceAll("CloudStorage.getItem('save'","CloudStorage.getItem('motion_test_save'");
html=html.replace("const SKEY='bsova_'","const SKEY='bsova_motion_test_'");
html=html.replace(/const API_URL=[^;]+;/,"const API_URL='';");
html=html.replace("function frame(now){","function frame(now){if(window.__motionPause){last=now;requestAnimationFrame(frame);return;}");
const testUI=`
loadTrackAssets('bear').catch(()=>{});loadTrackAssets('owl').catch(()=>{});
const testPanel=document.createElement('div');testPanel.id='motion-test-controls';
testPanel.style.cssText='position:fixed;bottom:calc(var(--sb, 0px) + 8px);left:max(8px, calc((100vw - 544px)/2));right:max(8px, calc((100vw - 544px)/2));z-index:9999;background:#ffffffeb;padding:8px;border-radius:12px;color:#243f4f;font:11px system-ui';
testPanel.innerHTML='<div id="motion-test-status" style="margin-bottom:6px">Загрузка новых движений…</div><div style="display:flex;gap:4px;flex-wrap:wrap"><button id="test-bear">Мишаня</button><button id="test-owl">Сова</button><button id="test-ride">Стойка</button><button id="test-left">Влево</button><button id="test-right">Вправо</button><button id="test-air">Прыжок</button><button id="test-land">Посадка</button><button id="test-play">Играть</button></div>';
document.body.appendChild(testPanel);
const controlStyle=document.createElement('style');controlStyle.textContent='#motion-test-controls button{min-height:38px;padding:8px 10px;border:1px solid #d9b98a;border-radius:9px;background:#fff8ea;color:#243f4f;font:700 12px system-ui}';document.head.appendChild(controlStyle);
function testPose(kind,phase){
 if(!HEROMODELS[kind].motion)return;clearWorld();window.__motionPause=true;hero=kind;G=newGame('play');G.y=1200;G.t=10;G.p.x=200;G.tx=200;G.p.v=400;G.shake=0;G.rival=null;G.p.rampH=0;G.p.mh=0;
 const p=G.p;if(phase==='left')p.ang=-.8;if(phase==='right')p.ang=.8;
 if(phase==='air'){p.air=1;p.airT=.5;p.airMax=1;p.alt=.8;p.launchH=0;}
 const hm=HEROMODELS[kind];hm.motion.active=false;hm.motion.lastT=null;hm.motion.state=phase==='land'?{landed:9.85}:{};
 for(const ui of document.querySelectorAll('.ui'))ui.classList.add('hidden');
 for(let i=0;i<40;i++)render(1/60);
 document.getElementById('motion-test-status').textContent=(kind==='bear'?'Мишаня':'Сова')+' · '+({ride:'стойка',left:'поворот влево',right:'поворот вправо',air:'полёт',land:'приземление'}[phase])+' · тестовая версия';
}
document.getElementById('test-bear').onclick=()=>testPose('bear','ride');document.getElementById('test-owl').onclick=()=>testPose('owl','ride');
for(const phase of ['ride','left','right','air','land'])document.getElementById('test-'+phase).onclick=()=>testPose(hero,phase);
document.getElementById('test-play').onclick=()=>{window.__motionPause=false;startGame();document.getElementById('motion-test-status').textContent='Тестовый заезд · '+(hero==='bear'?'Мишаня':'Сова');};
const readyTimer=setInterval(()=>{
 if(Object.values(HEROMODELS).some(h=>h.motionStatus==='error')){clearInterval(readyTimer);document.getElementById('motion-test-status').textContent='Не удалось загрузить движения. Обновите страницу.';return;}
 if(HEROMODELS.bear.motion&&HEROMODELS.owl.motion){clearInterval(readyTimer);testPose('bear','ride')}
},300);
window.__motionTest={testPose,ready:()=>!!(HEROMODELS.bear.motion&&HEROMODELS.owl.motion),get heroes(){return HEROMODELS},get game(){return G},render,update};
`;
const end=html.lastIndexOf('</script>');html=html.slice(0,end)+testUI+html.slice(end);
fs.writeFileSync('docs/animation-test.html',html);
// Portable browser review: public original assets, embedded new study GLBs.
// Separate in-memory storage avoids saves on a data-document's opaque origin.
let portable=html.replace(/<script (?:defer )?src="vendor\/telegram-web-app\.js"><\/script>/,'').replace('<head>','<head><base href="https://magliot.github.io/belaya-sova/">');
portable=portable.replaceAll('localStorage','previewStorage').replaceAll('sessionStorage','previewStorage');
const module=fs.readFileSync('docs/hero-motion.js','utf8');portable=portable.replace(/'\.\/hero-motion\.js(?:\?[^']*)?'/,JSON.stringify('data:text/javascript;base64,'+Buffer.from(module).toString('base64')));
const courseModule=fs.readFileSync('docs/course.js','utf8');portable=portable.replace(/'\.\/course\.js(?:\?[^']*)?'/,JSON.stringify('data:text/javascript;base64,'+Buffer.from(courseModule).toString('base64')));
const trainingModule=fs.readFileSync('docs/training.js','utf8').replace("'./course.js'",JSON.stringify('data:text/javascript;base64,'+Buffer.from(courseModule).toString('base64')));
portable=portable.replace("'./training.js'",JSON.stringify('data:text/javascript;base64,'+Buffer.from(trainingModule).toString('base64')));
for(const name of ['rider-motion','painted-slope','fireworks']){
  const source=fs.readFileSync('docs/'+name+'.js','utf8').replace("'./course.js'",JSON.stringify('data:text/javascript;base64,'+Buffer.from(courseModule).toString('base64')));
  portable=portable.replace("'./"+name+".js'",JSON.stringify('data:text/javascript;base64,'+Buffer.from(source).toString('base64')));
}
portable=portable.replace("'models/training-cast.glb'",JSON.stringify('data:model/gltf-binary;base64,'+fs.readFileSync('docs/models/training-cast.glb').toString('base64')));
portable=portable.replace("'models/'+file+'.glb'","(file==='prize-feather'?"+JSON.stringify('data:model/gltf-binary;base64,'+fs.readFileSync('docs/models/prize-feather.glb').toString('base64'))+":'models/'+file+'.glb')");
const hudModule=fs.readFileSync('docs/race-hud.js','utf8');portable=portable.replace(/'\.\/race-hud\.js(?:\?[^']*)?'/,JSON.stringify('data:text/javascript;base64,'+Buffer.from(hudModule).toString('base64')));
const trailModule=fs.readFileSync('docs/trail-visuals.js','utf8');portable=portable.replace(/'\.\/trail-visuals\.js(?:\?[^']*)?'/,JSON.stringify('data:text/javascript;base64,'+Buffer.from(trailModule).toString('base64')));
const b64=Object.fromEntries(['bear','owl'].map(k=>[k,fs.readFileSync('docs/models/studies/'+k+'.glb').toString('base64')]));
portable=portable.replace('<head>','<head><script>const previewStorage={getItem(){return null},setItem(){},removeItem(){}};window.__MOTION_STUDIES='+JSON.stringify(b64)+';</script>');
fs.mkdirSync('art/animation-integration',{recursive:true});
const portableFrame=`<!doctype html><html lang="ru"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Белая Сова · Проверка движений</title><style>body{margin:0;background:#203440;display:grid;place-items:center;min-height:100dvh}iframe{width:375px;height:812px;max-width:100vw;max-height:100dvh;border:0;background:#dcefff}</style></head><body><iframe title="Игра · 375×812"></iframe><script>document.querySelector('iframe').srcdoc=new TextDecoder().decode(Uint8Array.from(atob(${JSON.stringify(Buffer.from(portable).toString('base64'))}),c=>c.charCodeAt(0)));</script></body></html>`;
fs.writeFileSync('art/animation-integration/phone-preview.html',portableFrame);
console.log('Built isolated game page and portable review; docs/index.html unchanged.');

fs.writeFileSync('docs/animation-phone.html',`<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Белая Сова · Проверка на телефоне</title><style>body{margin:0;background:#203440;display:grid;place-items:center;min-height:100vh}iframe{width:375px;height:812px;max-width:100vw;max-height:100dvh;border:0;background:#dcefff}</style><iframe title="Проверка анимаций 375×812" src="animation-test.html"></iframe></html>`);
