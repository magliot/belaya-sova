"""Build the low-poly forest patrol in Blender. Local +Y points towards the nose."""
import sys
import bpy
from math import radians
from mathutils import Vector
from pathlib import Path

bpy.ops.wm.read_factory_settings(use_empty=True)
def paint(name, rgb, glow=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*rgb, 1)
    mat.use_nodes = True
    bs = mat.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*rgb, 1)
    bs.inputs['Roughness'].default_value = .52
    if glow:
        bs.inputs['Emission Color'].default_value = (*rgb, 1)
        bs.inputs['Emission Strength'].default_value = glow
    return mat

ink = paint('Graphite', (.028,.047,.067))
steel = paint('Suspension', (.21,.31,.36))
orange = paint('Rescue orange', (1,.155,.016))
ice = paint('Ice blue visor', (.045,.36,.5))
ivory = paint('Ivory stripe', (.96,.93,.72))
lamp = paint('Headlamp', (1,.82,.37), 1.6)
jacket = paint('Patrol jacket', (.018,.28,.35))

def mesh(name, vertices, faces, mat):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.materials.append(mat)
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return obj

def hull(name, rings, mat):
    # Each section lists the longitudinal position, half width, bottom and top.
    vertices = []
    for y,w,b,t in rings:
        vertices.extend([(-w*.83,y,b),(w*.83,y,b),(w,y,(b+t)*.5),
                         (w*.74,y,t),(-w*.74,y,t),(-w,y,(b+t)*.5)])
    faces = [tuple(reversed(range(6)))]
    for i in range(len(rings)-1):
        for j in range(6):
            a = i*6+j
            b = i*6+(j+1)%6
            faces.append((a,b,b+6,a+6))
    end = len(vertices)-6
    faces.append(tuple(range(end,end+6)))
    return mesh(name, vertices, faces, mat)

def box(name, pos, size, mat, tilt=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    obj.rotation_euler.x = radians(tilt)
    obj.data.materials.append(mat)
    return obj

def ellipse(name, pos, size, mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, location=pos)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    obj.data.materials.append(mat)
    return obj

def link(name, a, b, width, mat):
    a,b = Vector(a),Vector(b)
    bpy.ops.mesh.primitive_cylinder_add(vertices=8,radius=width,depth=(b-a).length,location=(a+b)/2)
    obj=bpy.context.object
    obj.name=name
    obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    obj.data.materials.append(mat)

hull('Rear track',[(-1.42,.31,.17,.43),(-1.23,.34,.065,.53),(-.05,.34,.065,.51),(.25,.31,.21,.48)],ink)
for i in range(8):
    box('Track tread',(0,-1.24+i*.155,.09),(.72,.048,.055),steel)
hull('Deck',[(-1.45,.43,.43,.51),(-.95,.52,.42,.61),(.52,.54,.40,.67),(.94,.32,.48,.65)],ink)
patrol='--player' not in sys.argv
rescue=paint('Turquoise rescue panel',(.015,.54,.54))
cherry=paint('Patrol coral',(.84,.028,.027))
hull('Cowling',[(-.16,.43,.51,.78),(.24,.56,.50,.98),(.76,.51,.48,.90),(1.18,.25,.52,.73)],ivory if patrol else orange)
hull('Saddle',[(-1.22,.30,.52,.67),(-.9,.34,.55,.84),(-.18,.26,.60,.91),(.03,.22,.62,.80)],ink)
hull('Windscreen',[(.30,.39,.97,1.30),(.44,.43,.90,1.26),(.72,.35,.89,1.04)],ice)
for side in (-1,1):
    blade=hull('Runner',[(-.23,.093,.065,.13),(.23,.12,.035,.12),(1.22,.11,.055,.14),(1.48,.08,.15,.22),(1.57,.052,.28,.32)],ink)
    blade.location.x=side*.54
    link('Suspension arm',(side*.29,.52,.49),(side*.54,.62,.10),.055,steel)
    link('Suspension brace',(side*.39,-.10,.45),(side*.54,.64,.13),.04,steel)
    ellipse('Light',(side*.24,1.01,.76),(.115,.09,.072),lamp)
    box('Side stripe',(side*.50,.43,.73),(.028,.39,.049),ivory,-8)
    if '--player' in sys.argv:
        for index in range(4):
            y=-1.12+index*.31
            ellipse('Bogey wheel',(side*.325,y,.29),(.059,.155,.156),ink)
            ellipse('Hub',(side*.38,y,.29),(.019,.073,.073),steel)
        for index in range(6):
            y=.25+index*.068
            link('Intake louvre',(side*.539,y,.66),(side*.52,y+.052,.78),.017,ink)
        link('Orange shock',(side*.34,.37,.53),(side*.54,.64,.16),.061,orange)
        link('Runner loop',(side*.54,1.28,.13),(side*.54,1.47,.34),.031,ink)
        link('Runner loop',(side*.54,1.47,.34),(side*.54,1.56,.30),.031,ink)
        box('Footboard',(side*.44,-.53,.49),(.22,1.05,.065),steel)
        for bar in range(5):box('Boot grip',(side*.44,-.85+bar*.15,.53),(.21,.035,.02),ink)
        ellipse('Amber cushion',(side*.235,-1.03,.67),(.11,.29,.09),orange)
        continue
    hip=(side*.18,-.65,.84);knee=(side*.30,-.18,.70);boot=(side*.38,-.29,.48)
    link('Trouser',hip,knee,.115,ink);link('Shin',knee,boot,.096,jacket)
    ellipse('Boot',boot,(.12,.20,.084),ink)
    shoulder=(side*.245,-.44,1.38);elbow=(side*.34,-.31,1.19);hand=(side*.32,.046,1.13)
    link('Sleeve',shoulder,elbow,.10,jacket);link('Forearm',elbow,hand,.085,orange)
    ellipse('Glove',hand,(.09,.10,.085),ink)
link('Handlebar',(-.37,.04,1.12),(.37,.04,1.12),.029,steel)
link('Column',(0,.19,.81),(0,.04,1.12),.042,ink)
if patrol:
    for side in [-1,1]:
        box('Rescue pannier',(side*.42,-.96,.81),(.26,.65,.46),rescue)
        box('Cross horizontal',(side*.556,-.96,.84),(.018,.30,.074),ivory)
        box('Cross vertical',(side*.558,-.96,.84),(.020,.076,.26),ivory)
        box('Hood slash',(side*.43,.60,.74),(.20,.56,.086),cherry,-12)
    torso=ellipse('Driver', (0,-.53,1.12),(.29,.22,.38),cherry)
    torso.rotation_euler.x=radians(-14)
    ellipse('Safety bib',(0,-.49,1.29),(.256,.207,.15),orange)
    ellipse('Helmet',(0,-.40,1.65),(.215,.22,.235),ivory)
    ellipse('Goggles',(0,-.24,1.67),(.201,.10,.11),ink)
    ellipse('Goggle lens',(0,-.205,1.68),(.17,.088,.072),ice)
box('Tail lamp',(0,-1.40,.54),(.28,.04,.065),orange)
if '--player' in sys.argv:
    hull('Black hood',[(.36,.35,.98,1.02),(.67,.38,.94,1.01),(1.13,.17,.76,.83)],ink)
    hull('Tail apron',[(-1.46,.38,.46,.62),(-1.16,.44,.49,.69),(-.98,.38,.55,.68)],orange)
    box('Rear reflector',(0,-1.46,.58),(.23,.018,.039),lamp)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.join()
obj=bpy.context.object
obj.name='Player sled' if '--player' in sys.argv else 'Forest patrol'
# Recalculate normals after joining hand-lofted sections.
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')
target=Path(__file__).resolve().parents[1]/'docs/models/forest-patrol.glb'
if '--player' in sys.argv:
    target=target.with_name('player-sled.glb')
    bpy.ops.wm.save_as_mainfile(filepath=str(target.parents[1].parent/'art/golden-owl/player-sled.blend'),compress=True)
if patrol:bpy.ops.wm.save_as_mainfile(filepath=str(target.parents[1].parent/'art/course-kit/patrol-13c.blend'),compress=True)
bpy.ops.export_scene.gltf(filepath=str(target),export_format='GLB',export_animations=False)
