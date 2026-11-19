// Local course inspection; the game disables leaderboard and persistence in this mode.
export function mountCourseReview(api){
 const box=document.createElement('section');box.id='course-review';
 box.setAttribute('aria-label','Осмотр трассы');
 const style=document.createElement('style');style.textContent=`
 #course-review{position:absolute;bottom:calc(var(--sb) + 8px);left:8px;right:8px;z-index:30;padding:9px;border:1px solid #d4bb92;border-radius:15px;background:#fff8eae8;color:#29475f;font:11px Rubik,system-ui,sans-serif;backdrop-filter:blur(8px)}
 #course-review header,#course-review .review-row{display:flex;align-items:center;gap:6px;margin-bottom:6px}
 #course-review header strong{flex:1}#course-review button,#course-review select{min-height:34px;border:1px solid #d9c3a0;border-radius:9px;background:#fffdf7;color:#29475f;padding:5px 7px;font:600 11px Rubik,system-ui,sans-serif}
 #course-review select{min-width:0;flex:1}#course-review input{min-width:0;flex:1;accent-color:#d98a26}
 #course-review [data-r=distance]{width:62px;flex:none}#course-review .review-row:last-child{margin:0}
 #course-review output{font-variant-numeric:tabular-nums;white-space:nowrap}
 `;document.head.appendChild(style);
 box.innerHTML=`<header><strong>Осмотр · без рекордов</strong><button data-r="fold" aria-expanded="true">Свернуть</button><button data-r="exit">В игру</button></header>
 <div data-r="body"><div class="review-row"><select data-r="stage" aria-label="Этап"></select><select data-r="hero" aria-label="Герой"><option value="bear">Мишаня</option><option value="owl">Сова</option></select></div>
 <div class="review-row"><input data-r="seek" aria-label="Расстояние" type="range" min="0" max="19999" step="1"><input data-r="distance" aria-label="Перейти к метру" type="number" min="0" max="19999"><span>м</span></div>
 <div class="review-row"><button data-r="back">−100 м</button><button data-r="play">Поехать</button><button data-r="step">Кадр</button><button data-r="next">+100 м</button><select data-r="rate" aria-label="Скорость просмотра"><option value="0.25">¼×</option><option value="1" selected>1×</option><option value="3">3×</option><option value="6">6×</option></select></div>
 <div class="review-row"><span>Позиция</span><input data-r="x" aria-label="Положение поперёк трассы" type="range" min="25" max="375" value="200"><select data-r="effect" aria-label="Проверить событие"><option value="normal">Обычный спуск</option><option value="coffee">Кофе</option><option value="avalanche">Лавина</option><option value="jump">Прыжок</option></select></div></div>`;
 document.getElementById('wrap').appendChild(box);
 const el=k=>box.querySelector('[data-r="'+k+'"]');
 el('x').value=api.state().lane;
 api.stages.forEach((name,i)=>el('stage').add(new Option((i+1)+' · '+name,String(i))));
 if(api.ride){
  el('distance').max=el('seek').max=1161;
  el('stage').hidden=el('effect').hidden=true;
  box.querySelector('header strong').textContent=(api.ride==='sled'?'Снегоход':'Плюшки')+' · осмотр';
 }
 el('stage').onchange=()=>api.seek(+el('stage').value*2500+10);
 for(const id of ['seek','distance'])el(id).onchange=()=>api.seek(+el(id).value);
 el('x').oninput=()=>api.position(+el('x').value);
 el('hero').onchange=()=>api.hero(el('hero').value);
 el('rate').onchange=()=>api.rate(+el('rate').value);
 el('effect').onchange=()=>api.effect(el('effect').value);
 el('play').onclick=()=>api.play();el('step').onclick=()=>api.step();
 el('back').onclick=()=>api.seek(api.state().distance-100);el('next').onclick=()=>api.seek(api.state().distance+100);
 el('exit').onclick=()=>api.exit();
 el('fold').onclick=()=>{const open=el('body').hidden;el('body').hidden=!open;el('fold').textContent=open?'Свернуть':'Развернуть';el('fold').setAttribute('aria-expanded',String(open));};
 let stamp=0;
 function refresh(now){const s=api.state();if(now-stamp>150){stamp=now;el('play').textContent=s.playing?'Стоп':'Поехать';
  if(document.activeElement!==el('seek'))el('seek').value=Math.floor(s.distance);
  if(document.activeElement!==el('distance'))el('distance').value=Math.floor(s.distance);
  el('stage').value=String(Math.min(7,Math.floor(s.distance/2500)));el('hero').value=s.hero;}
  requestAnimationFrame(refresh);}
 api.seek(Number(new URLSearchParams(location.search).get('at'))||0);requestAnimationFrame(refresh);
}
