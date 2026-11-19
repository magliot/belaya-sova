"""Build the reference-selected ski-area props in real-world metres. +Z up, -Y front."""
import bpy, math, random
from pathlib import Path
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
palette={}
def mat(name,rgb):
    if name not in palette:
        m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*rgb,1)
        bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*rgb,1)
        bs.inputs['Roughness'].default_value=.73
        if name=='Turquoise':
            bs.inputs['Emission Color'].default_value=(*rgb,1);bs.inputs['Emission Strength'].default_value=.42
        palette[name]=m
    return palette[name]
snow=mat('Snow light',(.89,.96,1));shade=mat('Snow shadow',(.52,.72,.89))
pine=mat('Blue spruce',(.025,.15,.12));leaf=mat('Needle tips',(.065,.28,.16));wood=mat('Larch',(.36,.163,.071))
steel=mat('Galvanized steel',(.27,.38,.47));ink=mat('Graphite',(.035,.057,.079));gold=mat('Amber',(.98,.42,.013));ice=mat('Turquoise',(.03,.8,.84));red=mat('Lacquer',(.78,.027,.026))
def part(o,m):o.data.materials.append(m);return o
def box(pos,size,m,angle=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.dimensions=size;o.rotation_euler.y=angle;return part(o,m)
def beam(a,b,r,m,r2=None):
    a,b=Vector(a),Vector(b);bpy.ops.mesh.primitive_cone_add(vertices=8,radius1=r,radius2=r if r2 is None else r2,depth=(a-b).length,location=(a+b)/2)
    o=bpy.context.object;o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return part(o,m)
def oval(pos,scale,m,sub=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub,location=pos);o=bpy.context.object;o.scale=scale
    for face in o.data.polygons:face.use_smooth=True
    return part(o,m)
done=[]
def pack(name):
    parts=[o for o in bpy.context.scene.objects if o not in done];bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=bpy.context.object
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);o.name=name;done.append(o)
for family in [19,20,21]:
    for variant in range(3):
        rng=random.Random(family*11+variant);h={19:3.7,20:7.2,21:2.45}[family]
        sway=[0,.035,.23][variant]*h
        beam((0,0,0),(sway*.48,0,h*.55),.085*h,wood,.045*h)
        if family==20:
            # A single stem, forked crown and windswept crown respectively.
            base=Vector((sway*.48,0,h*.55))
            for j in range(7):
                a=j*2.4;spread=h*(.14 if not variant else .235);z=h*(.64+.31*j/6)
                x=spread*math.cos(a)+sway*j/6;y=spread*.75*math.sin(a)
                elbow=Vector((x*.65,y*.65,z-.6));tip=Vector((x,y,z))
                beam(base,elbow,.18,wood,.10);beam(elbow,tip,.11,wood,.053)
                oval(tip,(1.12,.94,.46),pine,2);oval(tip+Vector((.02,0,.22)),(1.08,.90,.38),snow,2)
        else:
            beam((sway*.48,0,h*.55),(sway,0,h*.97),h*.05,wood,h*.012)
            layers=6 if family==19 else 4
            for j in range(layers):
                t=j/layers;z=h*(.15+.77*t);r=h*(.31 if variant==1 else .255)*(1-t*.84)
                for k in range(7):
                    a=k*math.tau/7+j*.71;rr=r*(.62+.15*rng.random())
                    x=sway*t*t+math.cos(a)*rr;y=math.sin(a)*rr
                    q=oval((x,y,z+.02*h),(r*.61,r*.42,r*.53),leaf if k%3==0 else pine,2);q.rotation_euler.z=a
                    q=oval((x,y,z+.096*h),(r*.61,r*.43,r*(.48 if variant==1 else .36)),snow if k%4 else shade,2);q.rotation_euler.z=a
            beam((sway,0,h*.78),(sway+.04,0,h),.105*h,snow,0)
        pack(f'tree{family}{"ABC"[variant]}')
# C: lattice lift tower and sheave assemblies at exactly the existing cable elevation.
for x in [-1,1]:
    for y in [-1,1]:
        beam((x*.93,y*.74,0),(x*.49,y*.43,8.1),.075,steel)
        for j in range(4):
            z=j*1.94;w=.91-.105*j;w2=w-.105
            beam((x*w,y*w*.78,z+.1),(-x*w2,y*w2*.78,z+1.99),.043,steel)
            beam((x*w,y*w*.78,z+.1),(x*w2,-y*w2*.78,z+1.99),.043,steel)
for x in [-1.05,1.05]:
    box((x,0,8.36),(.22,2.5,.24),steel)
    for y in [-.88,-.28,.32,.92]:beam((x-.13,y,8.34),(x+.13,y,8.34),.24,ink)
box((0,0,8.09),(3.0,.45,.31),steel);pack('tower16C')
# Lamp silhouettes: bent steel, a timber post and a two-sided fork.
for v in range(3):
    beam((0,0,0),(0,0,4.45),.145 if v==1 else .086,wood if v==1 else steel,.12 if v==1 else .07)
    for side in ([-1,1] if v==2 else [-1]):
        beam((0,0,4.05),(side*.37,0,4.91),.076,steel);beam((side*.37,0,4.91),(side*1.15,0,4.98),.068,steel)
        oval((side*1.15,0,4.91),(.35,.18,.15),ink);oval((side*1.15,0,4.82),(.25,.146,.073),gold)
    pack('lamp18'+'ABC'[v])
# A selected ochre/navy blower with visible intake and impeller.
box((0,0,.12),(1.1,1,.24),ink);beam((0,0,.19),(0,0,1.42),.19,steel)
beam((0,.58,1.6),(0,-.52,1.89),.58,gold,.68)
beam((0,-.52,1.89),(0,-.57,1.91),.58,ink)
for j in range(6):
    a=j*math.pi/3;blade=oval((math.sin(a)*.31,-.59,1.91+math.cos(a)*.31),(.11,.035,.24),steel)
    blade.rotation_euler.y=a+.53
oval((0,-.62,1.91),(.14,.09,.14),gold)
pack('cannon25A')
# 34B open horseshoe, cyan luminous bands, brushed tips.
pts=[(-.33,0,-.32),(-.34,0,-.15),(-.34,0,.04)]+[(.34*math.cos(math.pi-j*math.pi/12),0,.04+.34*math.sin(j*math.pi/12)) for j in range(1,13)]+[(.34,0,-.15),(.33,0,-.32)]
for j,(a,b) in enumerate(zip(pts,pts[1:])):
    beam(a,b,.125,ink if j in [0,len(pts)-2] else red)
for x in [-.34,.34]:
    beam((x,0,-.23),(x,0,-.16),.129,ice);beam((x,0,-.32),(x,0,-.285),.127,steel)
pack('magnet34B')
out=Path('art/course-kit');out.mkdir(exist_ok=True,parents=True)
bpy.ops.export_scene.gltf(filepath=str(Path('docs/models/course-kit.glb').resolve()),export_format='GLB',export_animations=False)
for i,o in enumerate(done):o.location=((i%5)*5,(i//5)*7,0)
bpy.ops.wm.save_as_mainfile(filepath=str((out/'slope-props.blend').resolve()),compress=True)
