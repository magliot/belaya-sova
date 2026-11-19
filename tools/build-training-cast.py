"""Blender source for the local training-slope cast. Run from repository root."""
import bpy,math,subprocess,tempfile
from pathlib import Path
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
out=Path('art/training-slope');out.mkdir(parents=True,exist_ok=True)
palette={}
def mat(name,rgb):
 if name not in palette:
  m=bpy.data.materials.new(name);m.diffuse_color=(*rgb,1);m.use_nodes=True
  p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*rgb,1);p.inputs['Roughness'].default_value=.58;palette[name]=m
 return palette[name]
rubber=mat('Graphite',(.024,.037,.054));navy=mat('Midnight',(.037,.059,.13));snow=mat('Ice white',(.92,.95,.98));skin=mat('Warm cheek',(.9,.51,.29));hair=mat('Chestnut',(.117,.044,.018))
yellow=mat('Honey',(.96,.44,.019));green=mat('Pine',(.085,.24,.105));blue=mat('Cobalt',(.036,.15,.74));pink=mat('Berry',(.57,.052,.31));red=mat('Scarlet',(.82,.045,.035));amber=mat('Amber lens',(1,.27,.018));ice=mat('Sky lens',(.15,.62,.93))
parts=[]
def finish(o,m):
 o.data.materials.append(m)
 for p in o.data.polygons:p.use_smooth=True
 parts.append(o);return o
def egg(pos,scale,m):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=10,location=pos);o=bpy.context.object;o.scale=scale;return finish(o,m)
def link(a,b,r,m,flatten=1):
 a,b=Vector(a),Vector(b);o=egg((a+b)*.5,(r,r*flatten,(a-b).length*.5+r*.46),m);o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o
def cyl(pos,r,h,m,top=None):
 bpy.ops.mesh.primitive_cone_add(vertices=20,radius1=r,radius2=r if top is None else top,depth=h,location=pos);return finish(bpy.context.object,m)
def tube(m):
 bpy.ops.mesh.primitive_torus_add(major_segments=48,minor_segments=14,major_radius=.66,minor_radius=.262,location=(0,0,.34));o=finish(bpy.context.object,m);o.scale=(1.12,1,.97);o.data.materials.append(navy);o.data.materials.append(snow)
 for p in o.data.polygons:
  x,y,z=p.center;slot=(math.atan2(y,x)/math.tau*8+8)%2;p.material_index=1 if z<-.075 else 2 if slot<.14 else int(slot>1)
 egg((0,0,.22),(.92,.82,.16),rubber)
 for s in [-1,1]:
  for a,b in [((s*.78,-.16,.52),(s*.78,-.12,.65)),((s*.78,-.12,.65),(s*.78,.10,.65)),((s*.78,.10,.65),(s*.78,.14,.52))]:link(a,b,.03,rubber)
def pack(name):
 bpy.ops.object.select_all(action='DESELECT')
 for o in parts:o.select_set(True)
 bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);parts.clear()
 return o
for i,(name,col,coat) in enumerate([('tube60B',yellow,yellow),('tube60C',green,green),('tube61A',blue,blue),('tube61C',pink,pink)]):
 tube(col);egg((0,.17,.70),(.365,.255,.42),coat);egg((0,.20,1.03),(.275,.26,.15),coat)
 link((0,-.084,.53),(0,-.084,.93),.014,snow)
 for s in [-1,1]:
  link((s*.17,.05,.53),(s*.265,-.45,.52),.17,navy);link((s*.265,-.46,.51),(s*.32,-.8,.32),.139,navy)
  egg((s*.32,-.83,.30),(.154,.19,.14),rubber);egg((s*.32,-.88,.248),(.16,.185,.045),snow)
  link((s*.30,.13,.92),(s*.54,.10,.72),.126,coat);link((s*.55,.10,.72),(s*.72,-.05,.67),.103,coat)
  egg((s*.73,-.08,.665),(.11,.10,.085),rubber)
 egg((0,.105,1.17),(.193,.175,.214),skin);egg((0,-.062,1.14),(.053,.051,.051),skin)
 if i==0: egg((0,-.016,1.062),(.159,.121,.074),hair)
 if i>1:egg((.11,.31,1.11),(.11,.15,.105),hair)
 egg((0,.122,1.32),(.22,.202,.146),col);egg((0,-.038,1.273),(.186,.074,.080),rubber);egg((0,-.078,1.28),(.152,.047,.053),ice if i>1 else amber)
 for s in [-1,1]:egg((s*.074,-.045,1.194),(.018,.014,.024),rubber)
 rider=pack(name)
 rider.shape_key_add(name='Basis')
 lean=rider.shape_key_add(name='Carve')
 absorb=rider.shape_key_add(name='Absorb')
 for j,v in enumerate(rider.data.vertices):
  x,y,z=v.co
  # Keep the two mittens on their handles. The shoulder, head and jacket
  # move together above the hips, with progressively less travel near each grip.
  weight=max(0,z-.59)*max(0,min(1,(.64-abs(x))/.22))
  lean.data[j].co.x+=weight*.35
  lean.data[j].co.z-=weight*x*.17
  absorb.data[j].co.z-=weight*.21
  absorb.data[j].co.y-=weight*.14
for i,(name,scarf,hat) in enumerate([('snow26A',blue,red),('snow26B',red,rubber),('snow26C',yellow,blue)]):
 egg((0,0,.38),(.48,.47,.40),snow);egg((0,0,.86),(.35,.34,.32),snow);egg((0,0,1.25),(.276,.27,.273),snow)
 egg((0,0,1.05),(.315,.31,.088),scarf);link((.16,-.24,1.05),(.23,-.33,.77),.077,scarf,.56)
 for s in [-1,1]:
  link((s*.29,0,.85),(s*.61,-.015,1.11),.035,hair);link((s*.52,0,1.035),(s*.55,.035,1.19),.020,hair)
  egg((s*.095,-.242,1.28),(.025,.024,.031),rubber)
 for dz in [.81,.66,.45]:egg((0,-(.34 if dz>.75 else .405),dz),(.043,.043,.048),rubber)
 nose=cyl((0,-.33,1.21),.047,.20,amber,.004);nose.rotation_euler.x=math.pi/2
 if i==0:
  egg((0,0,1.443),(.274,.27,.143),hat);egg((0,0,1.59),(.075,.075,.075),snow)
 elif i==1:
  cyl((0,0,1.435),.318,.043,hat);cyl((0,0,1.56),.204,.24,hat,.229);cyl((0,0,1.48),.213,.055,red)
 else:
  for s in [-1,1]:egg((s*.259,0,1.29),(.085,.109,.135),hat)
  for j in range(10):
   a=j*math.pi/10;b=(j+1)*math.pi/10;link((.267*math.cos(a),0,1.29+.27*math.sin(a)),(.267*math.cos(b),0,1.29+.27*math.sin(b)),.030,hat)
 pack(name)
# Short-cab groomer 12A. Its nose points along -Y (+Z in the GLB).
def block(pos,size,material,bevel=.06,tilt=0):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 o.rotation_euler.x=tilt
 mod=o.modifiers.new('Rolled edges','BEVEL');mod.width=bevel;mod.segments=2
 bpy.ops.object.modifier_apply(modifier=mod.name)
 return finish(o,material)
steel=mat('Blade steel',(.27,.37,.45));glass=mat('Deep blue windscreen',(.035,.16,.24))
for side in [-1,1]:
 x=side*1.02
 block((x,.10,.49),(.67,3.64,.88),rubber,.28)
 for j in range(4):
  wheel=cyl((x+side*.24,-1.02+j*.73,.49),.34,.23,steel);wheel.rotation_euler.y=math.pi/2
 for j in range(15):
  for z in [.10,.88]:block((x,-1.42+j*.216,z),(.72,.095,.105),navy,.018)
 link((side*.70,-1.02,.56),(side*1.13,-1.99,.36),.11,steel)
block((0,.02,1.01),(2.13,3.26,.47),red,.13)
block((0,.90,1.48),(1.63,1.42,.63),red,.15)
for j in range(5):block((0,.54+j*.18,1.81),(1.11,.08,.065),rubber,.016)
block((0,-.56,1.85),(1.75,1.49,1.55),red,.20)
block((0,-1.27,1.99),(1.48,.14,.91),glass,.10,-.13)
block((0,.18,2.14),(1.38,.07,.61),glass,.08)
block((0,.225,2.15),(.05,.05,.64),rubber,.015)
for side in [-1,1]:
 block((side*.61,1.62,1.56),(.28,.075,.14),amber,.025)
for side in [-1,1]:
 block((side*.86,-.53,2.03),(.09,1.17,.77),glass,.07)
 block((side*.61,-1.33,1.46),(.35,.15,.23),snow,.04)
 block((side*.74,-1.19,2.54),(.19,.23,.13),snow,.025)
block((0,-.53,2.61),(1.83,1.54,.15),snow,.07)
cyl((0,-.40,2.76),.16,.21,amber)
# Five panels follow the concave blade. A bright cutting edge outlines its footprint.
for j in range(-2,3):
 x=j*.66;y=-2.0-.09*j*j;tilt=j*.17
 for z,width,height,m in [(.56,.18,.84,steel),(.19,.21,.095,snow)]:
  panel=block((x,y,z),(.735,width,height),m,.045,-.12);panel.rotation_euler.z=tilt
cat=pack('cat12A')
# Match the established machine's length, then apply the same small-vehicle scale in game.
cat.scale=(1.10,1.38,1.26)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
# Objects start at their local origin, with Z=0 on snow. Separate only in the editable sheet.
actors=list(bpy.context.scene.objects)
with tempfile.TemporaryDirectory() as staging:
 raw=Path(staging)/'training.glb'
 bpy.ops.export_scene.gltf(filepath=str(raw),export_format='GLB',export_animations=False)
 subprocess.run(['node','tools/shrink.mjs',str(raw),'docs/models/training-cast.glb','0.38','256'],check=True)
for n,o in enumerate(sorted(actors,key=lambda o:o.name)):o.location=((n%4)*2.6,(n//4)*2.6,0)
bpy.ops.wm.save_as_mainfile(filepath=str((out/'training-cast.blend').resolve()),compress=True)
