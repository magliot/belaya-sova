"""Prepare game textures from the artist's approved cards; coordinates are within a single cell."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage
src=Path('Belaya_Sova_VSCode/references/sheets')
out=Path('docs/art/slope');out.mkdir(exist_ok=True,parents=True)
def cell(sheet,rows,row,col):
    im=Image.open(next(src.glob(f'{sheet:02d}-*.jpg'))).convert('RGB')
    w,h=im.size
    return im.crop((round(w*col/3)+4,round(h*row/rows)+4,round(w*(col+1)/3)-4,round(h*(row+1)/rows)-4))
def cutout(im,threshold=18):
    rgb=np.array(im).astype(float)
    # The printed studio backdrop changes slightly down each card.
    edge=np.percentile(np.concatenate([rgb[:,:5],rgb[:,-5:]],axis=1),50,axis=1)
    edge=ndimage.median_filter(edge,size=(11,1))
    delta=np.max(np.abs(rgb-edge[:,None,:]),axis=2)
    possible=delta<threshold
    seeds=np.zeros_like(possible);seeds[[0,-1],:]=1;seeds[:,[0,-1]]=1
    bg=ndimage.binary_propagation(seeds&possible,mask=possible)
    alpha=Image.fromarray((~bg*255).astype('uint8')).filter(ImageFilter.GaussianBlur(.46))
    img=im.copy();img.putalpha(alpha);return img
def save(img,name):
    img.save(out/(name+'.webp'),quality=91,method=6)
lodge=cutout(cell(16,4,0,2));save(lodge.crop(lodge.getbbox()),'village-51c')
for i,suffix in enumerate('abc'):
    im=cell(16,4,1,i);cut=cutout(im,16);rgba=np.array(cut);h,w=rgba.shape[:2]
    # Continue the snowy foot of each ridge down to the terrain silhouette.
    rgb=np.array(im).astype(float);yy,xx=np.indices((h,w))
    top=np.argmax(rgba[:,:,3]>190,axis=0)
    # Below the ridge silhouette the entire valley is opaque. Blend the pale studio
    # background into a snow shelf instead of extruding the final pixel of each column.
    foot=np.clip((yy/h-.63)/.29,0,1)[:,:,None]
    snow=np.array([198,219,236])+4*np.sin(xx/72+yy/39)[:,:,None]
    rgba[:,:,:3]=(rgb*(1-foot)+snow*foot).astype('uint8')
    rgba[:,:,3]=np.where(yy>top[None,:]+2,255,rgba[:,:,3])
    edge=np.clip(np.minimum(xx,w-1-xx)/(w*.06),0,1)
    rgba[:,:,3]=(rgba[:,:,3]*edge).astype('uint8')
    save(Image.fromarray(rgba),'ridge-52'+suffix)
    tube=cell(19,1,0,i)
    w,h=tube.size;strip=tube.crop((0,int(h*.12),w,int(h*.46)))
    y,x=np.indices((strip.height,strip.width));t=np.minimum(np.minimum(x,strip.width-x)/36,np.minimum(y/40,(strip.height-y)/36))
    strip.putalpha(Image.fromarray(np.uint8(np.clip(t,0,1)*255)));save(strip,'tubing-62'+suffix)
    for row in range(3):
        tree=cutout(cell(8,3,row,i));save(tree.crop(tree.getbbox()),f'forest-{19+row}'+suffix)
    aur=np.array(cell(16,4,3,i)).astype(float)
    bg=np.median(aur[:12],axis=(0,1));signal=np.maximum(0,np.max(aur-bg,axis=2))
    em=Image.fromarray(np.uint8(np.clip((aur-bg*.65)*2,0,255)));em.putalpha(Image.fromarray(np.uint8(np.clip((signal-4)*4,0,220))))
    save(em,'aurora-54'+suffix)
for row,suffix,name in [(1,2,'yeti-print-56c'),(2,1,'snowball-57b'),(3,0,'snow-chips-58a')]:
    im=cutout(cell(17,5,row,suffix));save(im.crop(im.getbbox()),name)


# The glass plate and its movable counters come from 59B. Reconstruct the ice
# under the printed example numbers by interpolating nearby horizontal colour.
hud=cell(17,5,4,1)
plate=np.array(hud)
for left,top,right,bottom in [(84,74,237,129),(37,156,173,201),(241,161,284,199)]:
    band=plate[top:bottom,left-3:right+3].copy()
    for x in range(left,right):
        t=(x-left+1)/(right-left+1)
        plate[top:bottom,x]=band[:,:3].mean(axis=1)*(1-t)+band[:,-3:].mean(axis=1)*t
helmet=hud.crop((36,157,83,201))
icon=np.array(helmet); yy,xx=np.indices(icon.shape[:2]);alpha=np.clip((1-((xx-23)/22)**2-((yy-21)/20)**2)*12,0,1)
helmet.putalpha(Image.fromarray((alpha*255).astype('uint8')));save(helmet,'helmet-59b')
base=cutout(Image.fromarray(plate),12);save(base.crop((8,17,310,240)),'hud-59b')
