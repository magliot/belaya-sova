"""Build the phone review and handoff from the signed reference index and real game captures."""
from collections import Counter
from html import escape
from pathlib import Path
import json,re
from PIL import Image
root=Path('Belaya_Sova_VSCode/references')
dest=Path('docs/review')
index=re.findall(r'^\| (\d\d) \| (.*?) \| (.*?) \| \[.*?\]\((.*?)\)',(root/'INDEX.md').read_text(encoding='utf8'),re.M)
stops=[line.split('|') for line in Path('tools/visual-stops.tsv').read_text(encoding='utf8').strip().splitlines()]
assert len(index)==len(stops)==62
observations={(v['n'],v['index']):v['observed'] for v in json.loads(Path('course-checks/art-update/cards.json').read_text(encoding='utf8'))}
assert len(observations)==sum(len(views.split()) for _,_,views,_ in stops if views!='—') and all(observations.values())
freq=Counter(row[-1] for row in index);offset=Counter()
saved={'frames':[], 'summary':[]}
host='http://127.0.0.1:8766/'
for (num,name,choice,source),(key,asset,views,description) in zip(index,stops):
    assert key==num
    i=int(num);sheet=Image.open(root/source).convert('RGB');w,h=sheet.size
    row=offset[source];offset[source]+=1
    variants=[v for v in 'ABC' if re.search(r'\b'+v+r'\b',choice)]
    if 'Все три' in choice:variants=list('ABC')
    if num in ['01','02']:
        thumb=sheet.copy();thumb.thumbnail((210,160))
    else:
        selected=variants or list('ABC')
        y0=round(h*row/freq[source]);y1=round(h*(row+1)/freq[source])
        thumb=Image.new('RGB',(110*len(selected),125),'#d9e5ec')
        for j,v in enumerate(selected):
            col=ord(v)-65;cell=sheet.crop((round(w*col/3)+2,y0+2,round(w*(col+1)/3)-2,y1-2))
            cell.thumbnail((109,125));thumb.paste(cell,(j*110+(110-cell.width)//2,(125-cell.height)//2))
    thumb.save(dest/'cards'/('ref-'+num+'.webp'),quality=86)
    links=[];mdlinks=[];shots=[];confirmed=[]
    for n,point in enumerate([] if views=='—' else views.split()):
        where,_,mode=point.partition('@');d,_,lane=where.partition(':')
        url='?preview=course&seed=1911&at='+d+('&lane='+lane if lane else '')+('&hero=owl' if mode=='owl' else '&ride='+mode if mode else '')
        confirmed.append(observations[(i,n)])
        label=f'{d} м';links.append(f'<a href="../{escape(url)}">{label}</a>');mdlinks.append(f'[{label}]({host}{url})')
        photo=f'cards/{num}-{n}.webp';assert (dest/photo).is_file(),photo
        shots.append(f'<a href="{photo}" aria-label="Игровой кадр на {label}"><img loading="lazy" src="{photo}" alt="Склон на {label}"></a>')
    status='Сохранена' if 'Текущая версия' in choice else 'Убрана по решению' if views=='—' else 'Подключено'
    chosen=choice if not variants or views=='—' else ' / '.join(variants)+(' и исходный ратрак' if i==12 else '')
    seen=' / '.join(dict.fromkeys(confirmed)) or ('Слой снят' if i==51 else 'Спавн отключён')
    saved['summary'].append(f"| {num} | {name} | {choice} | {status} | {asset} | {seen} | {description} {' · '.join(mdlinks)} |")
    saved['frames'].append(f"""<article id="r{num}"><h2>{num} · {escape(name)}</h2><p class="choice">{escape(chosen)}</p><img class="ref" loading="lazy" src="cards/ref-{num}.webp" alt="Лист художника"><div class="shots">{''.join(shots)}</div><p>{escape(description)}</p><small>{escape(asset)}</small><nav>{' '.join(links)}</nav></article>""")
md='''# Проверка художественных решений

Ветка `local/training-slope`, seed 1911. Источник решений — [индекс художника](../Belaya_Sova_VSCode/references/INDEX.md). [Референсы рядом с кадрами из игрового WebGL и переходы на трассу](http://127.0.0.1:8766/review/visual.html). Метры в таблице ведут к точке обзора перед объектом. Запись прыжков использует одну камеру, высокий трамплин и физику 60 Гц.

| № | Объект | Решение из индекса | Состояние | Ассет или игровая функция | Наблюдение при запуске | Где смотреть |
| --- | --- | --- | --- | --- | --- | --- |
'''
Path('handoff/VISUAL-62.md').write_text(md+'\n'.join(saved['summary'])+'\n',encoding='utf8')
header='''<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Белая Сова · 62 карточки</title>
<style>*{box-sizing:border-box}body{margin:0;background:#e9f1f6;color:#132d46;font:16px system-ui,sans-serif}main{max-width:1200px;margin:auto;padding:clamp(14px,3vw,44px)}h1{font-size:clamp(30px,5vw,54px);margin:0}a{color:#174c79;font-weight:650}header nav,article nav{display:flex;gap:14px;flex-wrap:wrap;margin:14px 0}header p{max-width:56em}video{display:block;background:#1a2c45;width:min(100%,410px);border-radius:12px}details{margin:15px 0}summary{cursor:pointer;color:#174c79;font-weight:650}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,305px),1fr));gap:15px}article{border-radius:14px;background:white;padding:17px;min-width:0}h2{font-size:19px;margin:0}p{line-height:1.45}.choice{margin:3px 0;color:#466582}.ref{width:100%;height:105px;object-fit:contain;background:#e5edf3;border-radius:8px;margin:8px 0}.shots{display:flex;align-items:start;gap:4px}.shots a{max-width:33%;flex:0 1 auto}.shots img{width:100%;display:block;border-radius:5px}.previews{display:flex;gap:12px}.previews figure{margin:0;max-width:185px;flex:1;min-width:0}.previews img{width:100%;border-radius:9px}.previews figcaption{font-size:13px;margin-top:3px}input{width:100%;max-width:470px;padding:12px;border-radius:10px;border:1px solid #90a4b4;margin:14px 0;font:inherit}small{overflow-wrap:anywhere;color:#546b7a}</style>
<main><header><h1>Белая Сова</h1><p>62 решения по референсам. В каждой карточке сверху расположен лист художника, ниже — кадры с игровой камеры. Нажмите на кадр для увеличения или на метры для запуска осмотра.</p>
<nav><a href="../?seed=1911">Играть · 1911</a><a href="#menu">Меню</a><a href="#ordinary-start">Старт за Сову</a><a href="#ordinary">Люди и въезды</a><a href="#jumps">Видео прыжков</a></nav>
<div class="previews"><figure id="menu"><img src="menu-visual.webp" alt="Меню игры на экране телефона"><figcaption>Меню после получения пера · 375 × 812</figcaption></figure></div>
<details id="riders"><summary>Попутчики, арка и оба неба · 16 секунд с камеры заезда</summary><video controls playsinline preload="none" src="riders-and-sky.mp4"></video></details>
<details id="ordinary-start"><summary>Сова · от кнопки «Поехали» до 85 м, одним дублем</summary><video controls playsinline preload="none" src="ordinary-start-1911.mp4"></video></details>
<details id="ordinary"><summary>Мишаня · фрагменты одного спуска до 7512 м</summary><video controls playsinline preload="none" src="ordinary-run-1911.mp4"></video></details>
<details id="jumps"><summary>Посмотреть три прыжка обоих героев</summary><video controls playsinline preload="none" src="jumps-1911.mp4"></video></details>
<label for="q">Найти карточку</label><br><input id="q" type="search" placeholder="Номер, название или модель"></header><section class="grid">'''
tail='''</section></main><script>function openVideo(){if(['#ordinary-start','#ordinary','#jumps','#riders'].includes(location.hash))document.querySelector(location.hash).open=true;}openVideo();addEventListener('hashchange',openVideo);document.querySelector('#q').addEventListener('input',e=>{const q=e.target.value.trim().toLowerCase();for(const a of document.querySelectorAll('article'))a.hidden=!a.textContent.toLowerCase().includes(q);});</script></html>'''
(dest/'visual.html').write_text(header+'\n'.join(saved['frames'])+tail,encoding='utf8')
