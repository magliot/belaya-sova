"""Blender source for the smiling mogul yeti. Joined skin sections rotate at the shoulders."""
import bpy,math,random,sys
from pathlib import Path
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.preferences.filepaths.save_version=0
def color(n,rgb):
    m=bpy.data.materials.new(n);m.use_nodes=True;m.diffuse_color=(*rgb,1)
    shader=m.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=(*rgb,1)
    shader.inputs['Roughness'].default_value=.83;return m
fur=color('Ivory fur',(.88,.93,1));shadow=color('Blue underfur',(.52,.65,.84));face=color('Glacier blue',(.22,.39,.68));ink=color('Midnight eyes',(.025,.035,.069));teeth=color('Snow teeth',(1,.97,.90))
parts=[]
def oval(p,s,m,rot=0):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=7,location=p)
    o=bpy.context.object;o.scale=s;o.rotation_euler.y=rot;o.data.materials.append(m)
    for f in o.data.polygons:f.use_smooth=True
    parts.append(o);return o
def tuft(p,width,height,tilt=0,up=False):
    bpy.ops.mesh.primitive_cone_add(vertices=7,radius1=width if up else .006,radius2=.006 if up else width,depth=height,location=p)
    o=bpy.context.object;o.scale.y=.65;o.rotation_euler.y=tilt;o.data.materials.append(fur);parts.append(o)
def group(n,p):
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();ob=bpy.context.object
    bpy.context.scene.cursor.location=p;bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True);ob.name=n;parts.clear();return ob
oval((0,0,.52),(.39,.30,.41),shadow);oval((0,-.04,.60),(.385,.30,.39),fur)
for side in [-1,1]:
    oval((side*.23,-.04,.19),(.165,.17,.20),shadow);oval((side*.24,-.116,.084),(.166,.20,.091),fur)
    for j in range(3):oval((side*(.16+.063*j),-.26,.075),(.042,.055,.039),face)
for j in range(10):
    a=j*math.tau/10;tuft((.34*math.cos(a),.225*math.sin(a),.41),.091,.22,.25*math.cos(a))
oval((0,-.025,.79),(.316,.26,.274),fur);oval((0,-.233,.784),(.234,.092,.177),face)
oval((0,-.310,.741),(.12,.025,.053),ink)
for side in [-1,1]:
    oval((side*.08,-.328,.757),(.018,.010,.023),teeth)
    oval((side*.092,-.312,.835),(.043,.022,.051),ink)
    oval((side*.09-.009,-.333,.851),(.012,.006,.015),teeth)
    oval((side*.101,-.293,.893),(.075,.041,.030),face,side*-.25)
    for j in range(3):tuft((side*(.24+.020*j),-.093,.81-j*.095),.089,.18,side*-.6)
oval((0,-.326,.803),(.047,.026,.031),ink)
for i in range(4):tuft(((i-1.5)*.085,0,.997+.02*(i%2)),.069,.18,.36,True)
body=group('Yeti28C',(0,0,0))
for s,n in [(1,'WinterArmL'),(-1,'WinterArmR')]:
    p=(s*.293,-.025,.714)
    oval((s*.36,-.027,.64),(.15,.151,.18),fur,s*.36)
    oval((s*.438,-.087,.525),(.142,.148,.155),shadow)
    oval((s*.452,-.177,.497),(.114,.113,.114),face)
    for j in range(3):oval((s*(.401+j*.042),-.253,.503),(.027,.033,.047),face)
    for j in range(3):tuft((s*(.36+.04*j),-.11,.646-j*.037),.060,.13,s*-.3)
    arm=group(n,p);arm.parent=body
path=Path('art/course-kit/yeti-28c.blend').resolve();bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
bpy.ops.export_scene.gltf(filepath=str(Path(sys.argv[-1]).resolve()),export_format='GLB',export_animations=False)
