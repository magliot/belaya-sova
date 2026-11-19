"""Bake the 27B rescue coat into the existing animated dog's UV atlas.
Usage: python tools/paint-rescue-dog.py original.glb rescue.glb
"""
import io,json,struct,sys
from pathlib import Path
import numpy as np
from PIL import Image,ImageFilter
raw=Path(sys.argv[1]).read_bytes()
n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);data=bytearray(raw[28+n:])
def attribute(i,width,dtype):
    a=doc['accessors'][i];v=doc['bufferViews'][a['bufferView']]
    return np.ndarray((a['count'],width),dtype,buffer=data,offset=v.get('byteOffset',0)+a.get('byteOffset',0),
        strides=(v.get('byteStride',width*np.dtype(dtype).itemsize),np.dtype(dtype).itemsize))
mesh=doc['meshes'][0]['primitives'][0];attrs=mesh['attributes']
tex=doc['bufferViews'][doc['images'][0]['bufferView']];offset=tex.get('byteOffset',0)
image=Image.open(io.BytesIO(data[offset:offset+tex['byteLength']])).convert('RGB')
w,h=image.size;orig=np.asarray(image);paint=orig.copy()
verts=attribute(attrs['POSITION'],3,'<f4');uv=attribute(attrs['TEXCOORD_0'],2,'<f4')*[w-1,h-1]
for tri in attribute(mesh['indices'],1,'<u2').reshape(-1,3):
    a,b,c=uv[tri];u,v=b-a,c-a;det=u[0]*v[1]-u[1]*v[0]
    if abs(det)<1e-7:continue
    lo=np.maximum(0,np.floor(np.min(uv[tri],axis=0)).astype(int)-1);hi=np.minimum([w-1,h-1],np.ceil(np.max(uv[tri],axis=0)).astype(int)+1)
    yy,xx=np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1];dx,dy=xx+.5-a[0],yy+.5-a[1]
    s=(dx*v[1]-dy*v[0])/det;t=(dy*u[0]-dx*u[1])/det
    mask=(s>=-.015)&(t>=-.015)&(s+t<=1.015)
    if not mask.any():continue
    row,col=yy[mask],xx[mask];point=verts[tri[0]]+s[mask,None]*(verts[tri[1]]-verts[tri[0]])+t[mask,None]*(verts[tri[2]]-verts[tri[0]])
    x,y,z=point.T;rgb=orig[row,col].astype(float);light=rgb.mean(axis=1);shade=np.clip(.54+light/310,.56,1.13)
    white=(y<.29)|((z<-.13)&(np.abs(x)<.08+.05*np.cos(8*y)))|((z<-.12)&(y>.28)&(y<.47))|((z>.15)&(np.sin(z*12+y*5)>0)&(y<.58))
    coat=(z>-.16)&(z<.34)&(y>.39)&(y<.81)
    strap=coat&((abs(z+.145)<.018)|(abs(z-.32)<.018)|(y<.413))
    cross=coat&(np.abs(x)>.10)&(((abs(z-.06)<.024)&(abs(y-.565)<.09))|((abs(y-.565)<.024)&(abs(z-.06)<.09)))
    for m,color in [(white&(light>36),[244,233,216]),(coat,[216,44,32]),(strap,[41,47,56]),(cross,[255,242,215])]:
        rgb[m]=shade[m,None]*np.array(color)
    paint[row,col]=np.uint8(np.clip(rgb,0,255))
out=io.BytesIO();Image.fromarray(paint).save(out,'JPEG',quality=93);new=out.getvalue();data+=b'\0'*(-len(data)%4)
tex.update(byteOffset=len(data),byteLength=len(new));data+=new;data+=b'\0'*(-len(data)%4)
doc['buffers'][0]['byteLength']=len(data);j=json.dumps(doc,separators=(',',':')).encode();j+=b' '*(-len(j)%4)
Path(sys.argv[2]).write_bytes(struct.pack('<4sIIII',b'glTF',2,28+len(j)+len(data),len(j),0x4e4f534a)+j+struct.pack('<II',len(data),0x004e4942)+data)
