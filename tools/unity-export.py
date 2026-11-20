"""Run from repository root with blender --background --python tools/unity-export.py."""
from pathlib import Path
import bpy,re,base64,tempfile,struct,json,sys
root=Path('unity/OwlSlope/Assets/Resources');root.mkdir(parents=True,exist_ok=True)
only=set(sys.argv[sys.argv.index('--')+1:]) if '--' in sys.argv else set()
for name,key,source in [('Owl',None,'art/owl-animation-pilot/sova-board-study.glb'),('Fir','fir','docs/models/trees.js'),('Rock','rock_tall','docs/models/winter.js'),('Snowman','snowman','docs/models/winter.js'),('Mandarin','mandarin','docs/models/props.js')]:
 if only and name not in only:continue
 bpy.ops.wm.read_factory_settings(use_empty=True)
 with tempfile.TemporaryDirectory() as d:
  f=Path(source)
  if key:
   b=re.search(r'__GLB(?:\.|\[")'+key+r'(?:"\])?\s*=\s*"([A-Za-z0-9+/=]+)"',f.read_text()).group(1);f=Path(d)/'asset.glb';f.write_bytes(base64.b64decode(b))
  data=f.read_bytes();n=struct.unpack_from('<I',data,12)[0];doc=json.loads(data[20:20+n]);binary=data[28+n:];textures=root/'Art';textures.mkdir(exist_ok=True)
  for mat in doc['materials']:
   texture=mat.get('pbrMetallicRoughness',{}).get('baseColorTexture');
   if texture is None:continue
   image=doc['images'][doc['textures'][texture['index']]['source']];view=doc['bufferViews'][image['bufferView']];o=view.get('byteOffset',0);(textures/(name+('.png' if image['mimeType']=='image/png' else '.jpg'))).write_bytes(binary[o:o+view['byteLength']]);break
  bpy.ops.import_scene.gltf(filepath=str(f.resolve()));bpy.ops.object.select_all(action='DESELECT');names={n.get('name') for n in doc['nodes']}
  for obj in bpy.context.scene.objects:
   # The glTF importer creates an Icosphere solely as a custom bone display shape.
   if obj.name in names or obj.type=='ARMATURE':obj.select_set(True)
  bpy.ops.export_scene.fbx(filepath=str((root/(name+'.fbx')).resolve()),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},path_mode='STRIP',embed_textures=False,add_leaf_bones=False,bake_anim=name=='Owl',bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
