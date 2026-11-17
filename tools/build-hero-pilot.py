"""Reproducible, isolated animation study. Blender 4.3; no live game changes.
blender -b -t 4 --python tools/build-hero-pilot.py -- art/animation-pilot
"""
import bpy, math, sys, os, json
from mathutils import Vector, Matrix, Quaternion
OUT=os.path.abspath(sys.argv[sys.argv.index('--')+1]); os.makedirs(OUT,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=os.path.join(OUT,'source/bear.glb'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
body=next(o for o in bpy.context.scene.objects if o.type=='MESH' and len(o.vertex_groups)>0)
for o in list(bpy.context.scene.objects):
    if o.type=='MESH' and o!=body: bpy.data.objects.remove(o,do_unlink=True)
rig.name='Mishanya_Rig'; body.name='Mishanya_Mesh'
P=rig.pose.bones
def bone(n): return P['mixamorig:'+n]
rest={b.name:b.bone.matrix_local.copy() for b in P}
def loc(n): return rest['mixamorig:'+n].translation.copy()
for b in P: b.rotation_mode='QUATERNION'
# Repair cross-body weight contamination found in the imported auto rig.
# Back-panel vertices had both forearm and thigh influences (see audit notes).
import numpy as np
weights=np.zeros((len(body.data.vertices),len(body.vertex_groups)),dtype=float)
coords=[body.matrix_world@v.co for v in body.data.vertices]
for v in body.data.vertices:
    for g in v.groups: weights[v.index,g.group]=g.weight
def ramp(x,a,b):
    t=min(1,max(0,(x-a)/(b-a)));return t*t*(3-2*t)
for i,w in enumerate(coords):
    for g in body.vertex_groups:
        name=g.name
        if any(x in name for x in ['UpLeg','Leg','Foot','Toe']): weights[i,g.index]*=1-ramp(w.z,.34,.40)
        if any(x in name for x in ['Arm','Hand','Shoulder']):
            core=(1-ramp(abs(w.x),.115,.185))*(1-ramp(w.z,.51,.59))
            weights[i,g.index]*=1-core
# Smooth along short mesh edges, preserving anatomical boundaries above.
neighbors=[[] for _ in coords]
for edge in body.data.edges:
    a,b=edge.vertices
    if (coords[a]-coords[b]).length<.035:neighbors[a].append(b);neighbors[b].append(a)
for _ in range(3):
    new=weights.copy()
    for i,ns in enumerate(neighbors):
        if ns:new[i]=.65*weights[i]+.35*np.mean(weights[ns],axis=0)
    weights=new
for i,w in enumerate(coords):
    # Backpack shell: rigid main body, soft attachment band at its front edge.
    bag=ramp(w.y,.08,.13)*(1-ramp(abs(w.x),.17,.21))*ramp(w.z,.28,.34)*(1-ramp(w.z,.69,.75))
    target=body.vertex_groups['mixamorig:Spine2'].index
    weights[i]*=1-bag;weights[i,target]+=bag
    # Match the four-influence runtime rather than relying on Blender-only skinning.
    keep=np.argsort(weights[i])[-4:];mask=np.zeros(weights.shape[1]);mask[keep]=weights[i,keep]
    weights[i]=mask/max(mask.sum(),1e-12)
for v in body.data.vertices:
    for g in list(v.groups):body.vertex_groups[g.group].remove([v.index])
    for idx in np.flatnonzero(weights[v.index]>1e-8):body.vertex_groups[int(idx)].add([v.index],float(weights[v.index,idx]),'REPLACE')
# Lock the solid boot region to each ankle, feathering only the cuff boundary.
# The original source GLB is retained alongside this derivative.
changed=0
for v in body.data.vertices:
    w=body.matrix_world@v.co
    if w.z<.185:
        a=min(1,max(0,(.185-w.z)/.035))
        target=body.vertex_groups['mixamorig:'+('LeftFoot' if w.x>0 else 'RightFoot')]
        old={g.group:g.weight for g in v.groups}
        for idx,val in old.items(): body.vertex_groups[idx].add([v.index],val*(1-a),'REPLACE')
        target.add([v.index],old.get(target.index,0)*(1-a)+a,'REPLACE'); changed+=1
for v in body.data.vertices:
    influences=sorted([(g.group,g.weight) for g in v.groups if g.weight>1e-8],key=lambda x:x[1],reverse=True)[:4]
    total=sum(w for _,w in influences)
    for g in list(v.groups):body.vertex_groups[g.group].remove([v.index])
    for idx,w in influences:body.vertex_groups[idx].add([v.index],w/total,'REPLACE')
# Keep the ordinary linear skinning used by Three.js: no render-only DQ fix.
for mod in body.modifiers:
    if mod.type=='ARMATURE': mod.use_deform_preserve_volume=False
def material(name,color,metal=0,rough=.5):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Metallic'].default_value=metal; bs.inputs['Roughness'].default_value=rough
    return m
red=material('Ski — vermilion',(.7,.045,.025)); dark=material('Rubber',(.018,.026,.04)); steel=material('Aluminium',(.24,.32,.39),.7)
snow=material('Snow',(.65,.75,.82),0,.9)
def cube(name,position,scale,mat,bevel=.01):
    bpy.ops.mesh.primitive_cube_add(size=1,location=position); o=bpy.context.object; o.name=name; o.dimensions=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(mat)
    if bevel: m=o.modifiers.new('Soft edges','BEVEL'); m.width=bevel; m.segments=3; o.modifiers.new('Normals','WEIGHTED_NORMAL')
    return o
skis=[]; poles=[]
for side in ['Left','Right']:
    foot=rig.matrix_world@loc(side+'Foot')
    ski=cube(side+'_Ski',(foot.x,foot.y-.03,-.011),(.078,.85,.022),red)
    skis.append((side,ski,ski.matrix_world.copy()))
    binding=cube(side+'_Binding',(foot.x,foot.y,.037),(.072,.14,.028),dark,.006)
    skis.append((side,binding,binding.matrix_world.copy()))
    # A fixed-length pole, with a local origin at the grip.
    parent=bpy.data.objects.new(side+'_Pole',None); bpy.context.collection.objects.link(parent)
    for name,z,r,depth,mat in [('Shaft',-.20,.004,.4,steel),('Grip',-.025,.012,.065,dark),('Basket',-.375,.025,.008,dark)]:
        bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=r,depth=depth,location=(0,0,z))
        o=bpy.context.object; o.name=side+'_'+name; o.data.materials.append(mat); o.parent=parent
    poles.append((side,parent))
def set_world(n,m): bone(n).matrix=rig.matrix_world.inverted()@m
def aim(n,start,end):
    b=bone(n); rm=rig.matrix_world@rest[b.name]
    axis=rm.to_3x3()@Vector((0,1,0)); q=axis.rotation_difference((end-start).normalized())
    set_world(n,Matrix.Translation(start)@q.to_matrix().to_4x4()@rm.to_3x3().to_4x4())
def solve(a,c,L1,L2,bend):
    d=c-a; l=min(d.length,L1+L2-1e-5); axis=d.normalized()
    h=(L1*L1-L2*L2+l*l)/(2*l); height=math.sqrt(max(0,L1*L1-h*h))
    perpendicular=(bend-axis*bend.dot(axis)).normalized()
    return a+axis*h+perpendicular*height
def turn(n,axis,angle):
    b=bone(n); local=rest[b.name].to_3x3().inverted()@Vector(axis)
    b.rotation_quaternion=Quaternion(local,math.radians(angle))@b.rotation_quaternion
def smooth(t): return t*t*(3-2*t)
# time, compression, lateral weight shift, body lean, hand anticipation, airborne height
keys=[(0,.025,0,9,0,0),(.8,.027,0,9,0,0),(1.5,.038,.035,13,-.005,0),
      (2.15,.035,-.032,11,.005,0),(2.7,.025,0,9,0,0),
      (3.05,.065,0,18,.025,0),(3.25,.009,0,5,-.02,0),
      (3.5,.045,0,8,-.035,.23),(3.7,.05,0,10,-.02,.28),
      (3.98,.018,0,6,.005,.10),(4.08,.018,0,7,.012,0),
      (4.23,.072,0,22,.025,0),(4.48,.035,0,12,-.01,0),
      (4.85,.022,0,8,0,0),(5.3,.025,0,9,0,0),(6,.025,0,9,0,0)]
FPS=30; END=181
def sample(t):
    for a,b in zip(keys,keys[1:]):
        if a[0]<=t<=b[0]:
            k=smooth((t-a[0])/(b[0]-a[0])); return [x+(y-x)*k for x,y in zip(a[1:],b[1:])]
    return list(keys[-1][1:])
audit=[]
for frame in range(1,END+1):
    bpy.context.scene.frame_set(frame); t=(frame-1)/FPS
    comp,shift,lean,hand,air=sample(t)
    # One continuous arc: no artificial pauses at intermediate height keys.
    if 3.25<t<4.08:
        u=(t-3.25)/(.83);air=4*.28*u*(1-u);flight=math.sin(math.pi*u)**2
    else:air=0;flight=0
    for b in P: b.matrix_basis=Matrix.Identity(4)
    # Root translation is authored once, then exported with the clip.
    b=bone('Hips'); m=rest[b.name].copy(); m.translation+=Vector((shift,.012,-comp+air+.025)); b.matrix=m
    turn('Spine',(1,0,0),lean*.5); turn('Spine1',(1,0,0),lean*.3); turn('Spine2',(1,0,0),lean*.2)
    turn('Neck',(1,0,0),-lean*.35); turn('Head',(1,0,0),-lean*.35)
    turn('Spine2',(0,1,0),-shift*90)
    bpy.context.view_layer.update()
    errors=[]
    for side in ['Left','Right']:
        hip=rig.matrix_world@bone(side+'UpLeg').head
        ankle=rig.matrix_world@loc(side+'Foot')+Vector((0,0,air+.025+.025*flight))
        l1=(loc(side+'Leg')-loc(side+'UpLeg')).length; l2=(loc(side+'Foot')-loc(side+'Leg')).length
        knee=solve(hip,ankle,l1,l2,Vector((0,-1,0)))
        aim(side+'UpLeg',hip,knee); bpy.context.view_layer.update()
        aim(side+'Leg',knee,ankle); bpy.context.view_layer.update()
        footmatrix=rig.matrix_world@rest[bone(side+'Foot').name]
        footmatrix=Matrix.Rotation(math.radians(-12*flight),4,'X')@footmatrix;footmatrix.translation=ankle
        set_world(side+'Foot',footmatrix); bpy.context.view_layer.update()
        # Each boot stays rigid; during flight the entire boot/ski pitches as one.
        errors.append(((rig.matrix_world@bone(side+'Foot').head)-ankle).length)
        upper=rig.matrix_world@bone(side+'Arm').head
        sign=1 if side=='Left' else -1
        wrist=Vector((sign*(.255+.025*flight)+shift*.6,-.115+hand,.445-comp*.65+air+.075*flight))
        l1=(loc(side+'ForeArm')-loc(side+'Arm')).length; l2=(loc(side+'Hand')-loc(side+'ForeArm')).length
        if (wrist-upper).length>l1+l2-.002: wrist=upper+(wrist-upper).normalized()*(l1+l2-.002)
        elbow=solve(upper,wrist,l1,l2,Vector((sign,0,-.3)))
        aim(side+'Arm',upper,elbow); bpy.context.view_layer.update()
        aim(side+'ForeArm',elbow,wrist); bpy.context.view_layer.update()
        # Retain a neutral mitten orientation instead of curling each finger chain.
        hm=rig.matrix_world@rest[bone(side+'Hand').name]; hm.translation=wrist
        set_world(side+'Hand',hm); bpy.context.view_layer.update()
    for side,o,base in skis:
        o.matrix_world=(rig.matrix_world@bone(side+'Foot').matrix)@(rig.matrix_world@rest[bone(side+'Foot').name]).inverted()@base
        o.rotation_mode='QUATERNION'; o.keyframe_insert('location',frame=frame);o.keyframe_insert('rotation_quaternion',frame=frame)
    for side,o in poles:
        wrist=rig.matrix_world@bone(side+'Hand').head
        o.location=wrist+Vector((0,-.015,-.045))
        o.rotation_mode='QUATERNION'; o.rotation_quaternion=Quaternion((1,0,0),math.radians(24+comp*100))
        o.keyframe_insert('location',frame=frame);o.keyframe_insert('rotation_quaternion',frame=frame)
    for b in P:
        b.keyframe_insert('location',frame=frame); b.keyframe_insert('rotation_quaternion',frame=frame)
    audit.append({'frame':frame,'foot_target_error':max(errors),'height':air,'compression':comp})
rig.animation_data.action.name='Mishanya_SkiStudy_v01'
scene=bpy.context.scene; scene.frame_start=1;scene.frame_end=END;scene.render.fps=FPS
for f,n in [(1,'СТОЙКА'),(25,'ПОВОРОТ'),(82,'ПОДГОТОВКА'),(99,'ОТРЫВ'),(112,'ПОЛЁТ'),(123,'КОНТАКТ'),(128,'АМОРТИЗАЦИЯ'),(146,'ВОССТАНОВЛЕНИЕ')]: scene.timeline_markers.new(n,frame=f)
scene.frame_set(1)
# Export an independently reviewable sample. Include rigid equipment and its baked tracks.
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.scene.objects:
    if o==rig or o==body or o.name.endswith(('_Ski','_Binding','_Pole','_Shaft','_Grip','_Basket')): o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT,'mishanya-ski-study.glb'),use_selection=True,export_format='GLB',export_animations=True,export_animation_mode='SCENE',export_frame_range=True,export_force_sampling=True,export_skins=True,export_morph=False)
# Blender exports one action per object. Merge synchronized body/equipment tracks
# into one portable study clip without changing geometry or skin data.
import struct
glb_path=os.path.join(OUT,'mishanya-ski-study.glb');raw=open(glb_path,'rb').read();size=struct.unpack_from('<I',raw,12)[0]
doc=json.loads(raw[20:20+size]);merged={'name':'Mishanya_SkiStudy_v01','samplers':[],'channels':[]}
for animation in doc.get('animations',[]):
    offset=len(merged['samplers']);merged['samplers']+=animation['samplers']
    for channel in animation['channels']:channel['sampler']+=offset;merged['channels'].append(channel)
doc['animations']=[merged];js=json.dumps(doc,separators=(',',':')).encode();js+=b' '*((-len(js))%4);tail=raw[20+size:]
open(glb_path,'wb').write(struct.pack('<4sIIII',b'glTF',2,20+len(js)+len(tail),len(js),0x4e4f534a)+js+tail)
cube('Snow stage',(0,0,-.012),(200,200,.024),snow,.0)
world=bpy.data.worlds.new('Winter studio');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.19,.26,.36,1);world.node_tree.nodes['Background'].inputs[1].default_value=.5
def area(name,pos,power,size):
    bpy.ops.object.light_add(type='AREA',location=pos); o=bpy.context.object;o.name=name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,.5))-o.location).to_track_quat('-Z','Y').to_euler()
area('Key',(-2,-3,4),350,3);area('Fill',(3,-1,2),180,3);area('Rim',(0,3,3),400,2)
bpy.ops.object.camera_add(location=(1.5,-2.7,1.35));cam=bpy.context.object;cam.name='Preview camera';cam.rotation_euler=(Vector((0,0,.62))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1.8;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=20;scene.cycles.use_denoising=False
scene.render.resolution_x=640;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
scene.render.film_transparent=False
json.dump({'boot_vertices_reweighted':changed,'max_foot_target_error':max(x['foot_target_error'] for x in audit),'frames':audit},open(os.path.join(OUT,'audit.json'),'w'),indent=2)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'mishanya-ski-study.blend'))
for f in [1,46,92,112,128]:
    scene.frame_set(f);scene.render.filepath=os.path.join(OUT,f'pose-{f:03d}.png');bpy.ops.render.render(write_still=True)
