"""Run from repository root with blender --background --python tools/unity-export.py."""
from pathlib import Path
import bpy,re,base64,tempfile,struct,json,sys,uuid
root=Path('unity/OwlSlope/Assets/Resources');root.mkdir(parents=True,exist_ok=True)
only=set(sys.argv[sys.argv.index('--')+1:]) if '--' in sys.argv else set()
models=[('Owl',None,'art/owl-animation-pilot/sova-board-study.glb'),('Fir','fir','docs/models/trees.js'),('Rock','rock_tall','docs/models/winter.js'),('Snowman','snowman','docs/models/winter.js'),('Mandarin','mandarin','docs/models/props.js'),
 ('Bear',None,'art/animation-pilot/mishanya-ski-study.glb'),('OwlRide',None,'art/owl-animation-pilot/sova-board-study.glb'),('BearMenu',None,'docs/models/bear-menu.glb'),('OwlMenu',None,'docs/models/owl-menu.glb')]

def meta(path,template):
 target=Path(str(path)+'.meta')
 if not target.exists():
  guid=uuid.uuid5(uuid.NAMESPACE_URL,'belaya-sova/'+path.as_posix()).hex
  target.write_text(re.sub(r'(?m)^guid: .*$', 'guid: '+guid,template.read_text()), encoding='utf-8')

for name,key,source in models:
 # Extract the existing owl FBX's original skin and snowboard maps separately, too.
 if name=='Owl' and 'OwlMaterials' in only: materials_only=True
 else: materials_only=False
 if only and name not in only and not materials_only:continue
 bpy.ops.wm.read_factory_settings(use_empty=True)
 with tempfile.TemporaryDirectory() as d:
  f=Path(source)
  if key:
   b=re.search(r'__GLB(?:\.|\[")'+key+r'(?:"\])?\s*=\s*"([A-Za-z0-9+/=]+)"',f.read_text()).group(1);f=Path(d)/'asset.glb';f.write_bytes(base64.b64decode(b))
  data=f.read_bytes();n=struct.unpack_from('<I',data,12)[0];doc=json.loads(data[20:20+n]);binary=data[28+n:];textures=root/'Art';textures.mkdir(exist_ok=True)
  if name in ('Bear','Owl','BearMenu','OwlMenu'):
   material_rows=[]
   for index, mat in enumerate(doc['materials']):
    props=mat.get('pbrMetallicRoughness',{}); tex=props.get('baseColorTexture')
    row={'name':mat['name'],'rgba':props.get('baseColorFactor',[1,1,1,1]),'texture':''}
    if tex:
     img=doc['images'][doc['textures'][tex['index']]['source']];view=doc['bufferViews'][img['bufferView']];offset=view.get('byteOffset',0)
     image_key=name+'Race'+str(index);path=textures/(image_key+('.png' if img['mimeType']=='image/png' else '.jpg'))
     path.write_bytes(binary[offset:offset+view['byteLength']]);meta(path,textures/'Fir.jpg.meta');row['texture']='Art/'+image_key
    material_rows.append(row)
   path=textures/(name+'Materials.json');path.write_text(json.dumps({'entries':material_rows},ensure_ascii=False),encoding='utf-8');meta(path,root/'Slope.json.meta')
  if materials_only:continue
  for mat in doc['materials'] if name in ('Owl','Fir','Rock','Snowman','Mandarin') else []:
   texture=mat.get('pbrMetallicRoughness',{}).get('baseColorTexture');
   if texture is None:continue
   image=doc['images'][doc['textures'][texture['index']]['source']];view=doc['bufferViews'][image['bufferView']];o=view.get('byteOffset',0);(textures/(name+('.png' if image['mimeType']=='image/png' else '.jpg'))).write_bytes(binary[o:o+view['byteLength']]);break
  if name=='BearMenu':
   # The original menu uses skiReady on the six arm bones to seat each pole in its glove.
   for node in doc['nodes']:
    if node.get('extras',{}).get('skiReady'):node['rotation']=node['extras']['skiReady']
   chunk=json.dumps(doc,separators=(',',':')).encode();chunk+=b' '*(-len(chunk)%4)
   f=Path(d)/'menu.glb';f.write_bytes(struct.pack('<4sIIII',b'glTF',2,28+len(chunk)+len(binary),len(chunk),0x4e4f534a)+chunk+struct.pack('<II',len(binary),0x004e4942)+binary)
  bpy.ops.import_scene.gltf(filepath=str(f.resolve()));bpy.ops.object.select_all(action='DESELECT');names={n.get('name') for n in doc['nodes']}
  if name in ('OwlRide','Bear'):
   # Match the authored skin and equipment keys, including the complete left-edge cycle.
   # The default 250-frame Blender export ends partway through the new animations.
   import math
   actions=[o.animation_data.action for o in bpy.context.scene.objects if o.animation_data and o.animation_data.action]
   bpy.context.scene.frame_end=math.ceil(max(a.frame_range[1] for a in actions))
  for obj in bpy.context.scene.objects:
   # The glTF importer creates an Icosphere solely as a custom bone display shape.
   if obj.name in names or obj.type=='ARMATURE':obj.select_set(True)
  dest=root/(name+'.fbx')
  bpy.ops.export_scene.fbx(filepath=str(dest.resolve()),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},path_mode='STRIP',embed_textures=False,add_leaf_bones=False,bake_anim=name in ('Owl','Bear','OwlRide'),bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
  meta(dest,root/('Owl.fbx.meta' if name in ('Owl','Bear','OwlRide') else 'Fir.fbx.meta'))
