"""Encode one normal-speed cycle and one half-speed review, with restrained labels."""
from pathlib import Path
import subprocess
out=Path('art/animation-pilot').resolve()
frames=out/'render-final'
missing=[n for n in range(1,181) if not (frames/f'frame-{n:04d}.png').exists()]
if missing:raise RuntimeError(f'Missing frames: {missing}')
font='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
filtergraph=f"""[0:v]hqdn3d=1.2:1.2:2:2,drawtext=fontfile={font}:text='МИШАНЯ · ПРОБА 01':fontsize=21:fontcolor=0x263b48:x=32:y=32,split=2[a][b];
[a]drawtext=fontfile={font}:text='Обычная скорость':fontsize=15:fontcolor=0x425867:x=32:y=64,setpts=PTS-STARTPTS[normal];
[b]setpts=2*(PTS-STARTPTS),drawtext=fontfile={font}:text='Замедление ×0.5':fontsize=15:fontcolor=0x425867:x=32:y=64[slow];
[normal][slow]concat=n=2:v=1:a=0,fps=30,format=yuv420p[v]"""
script=out/'encode-filter.txt';script.write_text(filtergraph)
subprocess.run(['ffmpeg','-y','-hide_banner','-loglevel','error','-framerate','30','-start_number','1','-i',str(frames/'frame-%04d.png'),'-filter_complex_script',str(script),'-map','[v]','-c:v','libx264','-preset','medium','-crf','20','-movflags','+faststart',str(out/'mishanya-ski-study.mp4')],check=True)
script.unlink()
