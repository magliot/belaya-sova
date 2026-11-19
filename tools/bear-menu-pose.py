"""Copy the lightweight ready pose to the menu export without loading a ride clip."""
import json,struct,pathlib
def read(p):
    b=pathlib.Path(p).read_bytes();n=struct.unpack_from('<I',b,12)[0]
    return json.loads(b[20:20+n]),b[20+n:]
def apply(ride,menu):
    src,buf=read(ride);dst,tail=read(menu);nodes={n['name']:n for n in dst['nodes']}
    clip=src['animations'][0];count=0
    for ch in clip['channels']:
        target=ch['target'];name=src['nodes'][target['node']]['name']
        if target['path']!='rotation' or name.split(':')[-1] not in {s+k for s in ('Left','Right') for k in ('Arm','ForeArm','Hand')}:continue
        acc=src['accessors'][clip['samplers'][ch['sampler']]['output']];bv=src['bufferViews'][acc['bufferView']]
        assert acc['componentType']==5126 and acc['type']=='VEC4'
        q=struct.unpack_from('<4f',buf,8+bv.get('byteOffset',0)+acc.get('byteOffset',0))
        nodes[name].setdefault('extras',{})['skiReady']=q;count+=1
    assert count==6,count
    j=json.dumps(dst,separators=(',',':')).encode();j+=b' '*(-len(j)%4)
    pathlib.Path(menu).write_bytes(struct.pack('<4sIIII',b'glTF',2,20+len(j)+len(tail),len(j),0x4e4f534a)+j+tail)
if __name__=='__main__':
    import sys
    apply(*sys.argv[1:])
