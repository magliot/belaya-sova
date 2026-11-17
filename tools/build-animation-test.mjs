import fs from 'node:fs';
import {execFileSync} from 'node:child_process';
fs.mkdirSync('docs/models/studies',{recursive:true});
for(const [kind,p] of Object.entries({bear:'art/animation-pilot/mishanya-ski-study.glb',owl:'art/owl-animation-pilot/sova-board-study.glb'}))execFileSync(process.execPath,['tools/shrink.mjs',p,'docs/models/studies/'+kind+'.glb','1','512'],{stdio:'inherit'});
let html=fs.readFileSync('docs/index.html','utf8');
html=html.replace(/<script>\s*\/\/ Telegram[\s\S]*?<\/script>/,'');
html=html.replace('<title>Белая Сова · Спуск</title>','<title>Белая Сова · Проба анимации</title>');
html=html.replace("import * as THREE from 'three';","import * as THREE from 'three';\nimport {attachHeroMotion,poseHeroMotion} from './hero-motion.js';");
html=html.replace('const h=makeGlbHero(k,g);','const h=makeGlbHero(k,g);attachHeroMotion(h);');
html=html.replace('function poseGlb(h,p,mode,t){','function poseGlb(h,p,mode,t){\n  if(poseHeroMotion(h,p,mode,t))return;');
html=html.replace("R.fp.air=o.air>0?1:0;R.fp.ang=ang;","R.fp.air=o.air>0?1:0;R.fp.airT=o.airT||0;R.fp.airMax=o.airMax||1;R.fp.ang=ang;");
// Preview never writes results, Telegram saves, or the player's existing progress.
html=html.replace(/<script src="https:\/\/telegram.org\/js\/telegram-web-app.js"><\/script>/,'');
html=html.replace("const SKEY='bsova_'","const SKEY='bsova_motion_test_'");
html=html.replace(/const API_URL=[^;]+;/,"const API_URL='';");
html=html.replace("function frame(now){","function frame(now){if(window.__motionPause){last=now;requestAnimationFrame(frame);return;}");
const testUI=`
const testPanel=document.createElement('div');testPanel.id='motion-test-controls';
testPanel.style.cssText='position:fixed;bottom:8px;left:8px;right:8px;z-index:9999;background:#ffffffeb;padding:8px;border-radius:12px;color:#243f4f;font:11px system-ui';
testPanel.innerHTML='<div id="motion-test-status" style="margin-bottom:6px">Загрузка новых движений…</div><div style="display:flex;gap:4px;flex-wrap:wrap"><button id="test-bear">Мишаня</button><button id="test-owl">Сова</button><button id="test-ride">Стойка</button><button id="test-left">Влево</button><button id="test-right">Вправо</button><button id="test-air">Прыжок</button><button id="test-land">Посадка</button><button id="test-play">Играть</button></div>';
document.body.appendChild(testPanel);
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
let portable=html.replace('<head>','<head><base href="https://magliot.github.io/belaya-sova/">');
portable=portable.replaceAll('localStorage','previewStorage').replaceAll('sessionStorage','previewStorage');
const module=fs.readFileSync('docs/hero-motion.js','utf8');portable=portable.replace("'./hero-motion.js'",JSON.stringify('data:text/javascript;base64,'+Buffer.from(module).toString('base64')));
const b64=Object.fromEntries(['bear','owl'].map(k=>[k,fs.readFileSync('docs/models/studies/'+k+'.glb').toString('base64')]));
portable=portable.replace('<head>','<head><script>const previewStorage={getItem(){return null},setItem(){},removeItem(){}};window.__MOTION_STUDIES='+JSON.stringify(b64)+';</script>');
fs.mkdirSync('art/animation-integration',{recursive:true});
const portableFrame=`<!doctype html><html lang="ru"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Белая Сова · Проверка движений</title><style>body{margin:0;background:#203440;display:grid;place-items:center;min-height:100dvh}iframe{width:375px;height:812px;max-width:100vw;max-height:100dvh;border:0;background:#dcefff}</style></head><body><iframe title="Игра · 375×812"></iframe><script>document.querySelector('iframe').srcdoc=new TextDecoder().decode(Uint8Array.from(atob(${JSON.stringify(Buffer.from(portable).toString('base64'))}),c=>c.charCodeAt(0)));</script></body></html>`;
fs.writeFileSync('art/animation-integration/phone-preview.html',portableFrame);
console.log('Built isolated game page and portable review; docs/index.html unchanged.');

fs.writeFileSync('docs/animation-phone.html',`<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Белая Сова · Проверка на телефоне</title><style>body{margin:0;background:#203440;display:grid;place-items:center;min-height:100vh}iframe{width:375px;height:812px;max-width:100vw;max-height:100dvh;border:0;background:#dcefff}</style><iframe title="Проверка анимаций 375×812" src="animation-test.html"></iframe></html>`);
