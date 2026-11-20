"""Compact piste groomer 12A, forest patrol 13C and bonus snowmobile 14A.

From the repo root run Blender -b --python tools/build-unity-vehicles.py
To export hand edits from art/course-kit/vehicles.blend, add -- --export-edited.
Blender +Y points towards the nose, +Z up. The cab lighting anchors are in
GroomerOptics.cs. The open saddle and grips also serve the bonus hero rig.
"""
import base64
from collections import defaultdict
import math
from pathlib import Path
import re
import struct
import sys
import tempfile

import bmesh
import bpy
from mathutils import Vector

ROOT = Path.cwd()
ART = ROOT / 'art/course-kit/vehicles.blend'
RES = ROOT / 'unity/OwlSlope/Assets/Resources'
MODELS = RES / 'Race/Models'


def paint(name, rgb, emission=0):
    mat = bpy.data.materials.new(name)
    srgb = [int(rgb[i:i+2], 16)/255 for i in (0, 2, 4)]
    linear = [v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in srgb]
    mat.diffuse_color = (*linear, 1)
    mat['emission'] = emission
    mat.use_nodes = True
    bs = mat.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = mat.diffuse_color
    bs.inputs['Roughness'].default_value = .44
    if emission:
        bs.inputs['Emission Color'].default_value = mat.diffuse_color
        bs.inputs['Emission Strength'].default_value = emission
    return mat


def mesh(name, verts, faces, mat):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.materials.append(mat)
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return obj


def finish(o, radius=0):
    if radius:
        bevel = o.modifiers.new('Rolled edge', 'BEVEL')
        bevel.width, bevel.segments = radius, 2
        bevel.limit_method = 'ANGLE'
        bevel.angle_limit = math.radians(24)
        bevel.harden_normals = True
    return o


def box(name, centre, size, mat, radius=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=centre)
    o = bpy.context.object
    o.name, o.dimensions = name, size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(mat)
    return finish(o, radius)


def bar(name, a, b, radius, mat, sides=10):
    a, b = Vector(a), Vector(b)
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=(b-a).length,
                                        location=(a+b)/2)
    o = bpy.context.object
    o.name = name
    o.rotation_euler = (b-a).to_track_quat('Z', 'Y').to_euler()
    o.data.materials.append(mat)
    for face in o.data.polygons:
        face.use_smooth = len(face.vertices) == 4
    return o


def pipe(name, points, radius, mat):
    for a,b in zip(points, points[1:]):
        bar(name, a, b, radius, mat)


def shell(name, sections, mat, radius=0):
    """Rounded octagonal transverse frames: y, bottom width, top width, bottom, top."""
    verts = []
    for y,w,t,lo,hi in sections:
        r = min(w*.23, (hi-lo)*.28)
        verts.extend([(-w+r,y,lo),(w-r,y,lo),(w,y,lo+r),(t,y,hi-r),
                      (t-r,y,hi),(-t+r,y,hi),(-t,y,hi-r),(-w,y,lo+r)])
    end = len(verts)-8
    faces = [tuple(reversed(range(8))),tuple(range(end,end+8))]
    for a in range(0,end,8):
        for j in range(8):
            b=a+(j+1)%8
            faces.append((a+j,b,b+8,a+j+8))
    return finish(mesh(name,verts,faces,mat),radius)


def profile(name, yz, x1, x2, mat, radius=0):
    """Extrude a longitudinal side silhouette between the two x planes."""
    n=len(yz)
    verts=[(x,y,z) for x in (x1,x2) for y,z in yz]
    faces=[tuple(range(n)),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return finish(mesh(name,verts,faces,mat),radius)


def tyre(name, cx, cy, cz, width, radius):
    for label, r, w, mat in [(' rubber',radius,width,rubber),(' rim',.71*radius,width+.011,steel),
                             (' hub',radius*.31,width+.022,graphite)]:
        bar(name+label,(cx-w/2,cy,cz),(cx+w/2,cy,cz),r,mat,16)


def belt(name, cx, width, back, front, cz, r, wheels):
    # A continuous belt with an open side reveals the idlers. Each grouser
    # lies tangent to the belt rather than floating near the wheels.
    points = []
    for y,angles in [(front,range(-90,91,18)),(back,range(90,271,18))]:
        points.extend((y+r*math.cos(math.radians(a)),cz+r*math.sin(math.radians(a))) for a in angles)
    centre=(back+front)*.5
    inner=[(centre+(y-centre)*.95,cz+(z-cz)*.73) for y,z in points]
    n=len(points)
    verts=[(x,y,z) for x in (cx-width/2,cx+width/2) for curve in (points,inner) for y,z in curve]
    faces=[]
    for i in range(n):
        j=(i+1)%n
        for a,b in [(0,n),(0,2*n),(n,3*n),(2*n,3*n)]:
            faces.append((a+i,a+j,b+j,b+i))
    mesh(name+' continuous belt',verts,faces,rubber)
    for a,b in zip(points,points[1:]+points[:1]):
        dy,dz=b[0]-a[0],b[1]-a[1]
        count=max(1,round(math.hypot(dy,dz)/(r*.68)))
        for k in range(count):
            u=(k+.5)/count
            rib=box(name+' grouser',(cx,a[0]+dy*u,a[1]+dz*u),(width*1.025,r*.19,r*.13),track_edge)
            rib.rotation_euler.x=math.atan2(dz,dy)
    for i in range(wheels):
        y=back+(front-back)*i/(wheels-1)
        tyre(name+' road wheel',cx,y,cz,width*.90,min(r*.72,(front-back)/(wheels-1)*.49))


def winter_patch(name, centre, radii):
    # A single softly scalloped cap per panel keeps the snow graphic readable.
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=7,location=centre)
    o=bpy.context.object
    o.name,o.scale=name,radii
    for v in o.data.vertices:
        x,y,z=v.co
        wave=1+.095*math.sin(3*math.atan2(y,x)+.4)+.043*math.cos(5*math.atan2(y,x))
        v.co=(x*wave,y*wave,z*(1+.11*x))
    for face in o.data.polygons: face.use_smooth=True
    o.data.materials.append(snow)
    return o


def groomer():
    for side in (-1,1):
        belt('Crawler',side*.99,.59,-1.76,1.30,.64,.58,6)
    shell('Running chassis',[(-2.39,.91,.89,1.0,1.30),(-1.83,1.22,1.24,.85,1.49),
          (1.57,1.22,1.19,.94,1.51),(1.90,.89,.91,1.13,1.46)],graphite,.07)
    shell('Rear engine cowling',[(-2.37,.89,.84,1.29,1.70),(-2.17,1,1.02,1.27,1.86),
          (-1.10,1.03,1.01,1.28,1.92),(-.69,.93,.94,1.28,1.83)],coral,.065)
    for s in (-1,1):
        for y in [-1.99,-1.79,-1.59,-1.39]:
            box('Engine slot',(s*1.026,y,1.62),(.029,.117,.20),graphite,.028)
        box('Service ledge',(s*1.18,-.87,1.29),(.24,2.65,.10),steel,.036)
        box('Rear marker',(s*.72,-2.388,1.53),(.25,.045,.11),brake,.022)
    # Forward-raked windshield, broad rear glass and opaque rubber seals.
    shell('Enamel cab apron',[(-.76,.82,.85,1.34,1.69),(-.48,1,1,1.33,1.77),
          (1.38,.97,.93,1.37,2.06),(1.95,.88,.91,1.57,1.96)],coral,.07)
    shell('Cab glazing',[(-.62,.82,.79,1.66,2.90),(-.46,.94,.86,1.67,3.03),
          (.94,.94,.83,1.79,3.04),(1.79,.86,.83,1.91,2.05)],glass,.036)
    for s in (-1,1):
        pipe('Cab pillar',[(s*.86,-.56,1.68),(s*.84,-.49,3.00),(s*.84,.98,3.04),
                          (s*.86,1.81,1.99)],.078,coral)
        pipe('Door rubber',[(s*.93,-.41,1.73),(s*.94,.59,1.77),(s*.86,.28,3.00)],.037,graphite)
        bar('Mirror stem',(s*.86,.85,2.79),(s*1.09,.91,2.71),.039,graphite)
        box('Mirror',(s*1.11,.91,2.66),(.09,.21,.27),graphite,.037)
        box('Door handle',(s*.956,-.26,2.00),(.05,.24,.063),graphite,.02)
        box('Worklight housing',(s*.675,1.880,1.84),(.43,.16,.27),graphite,.05)
        box('Worklight lens',(s*.675,1.961,1.84),(.32,.041,.17),lamp,.031)
        box('Top work lamp',(s*.71,1.027,3.02),(.29,.17,.18),graphite,.036)
        box('Top lens',(s*.71,1.11,3.02),(.21,.042,.112),lamp,.02)
    shell('Cab roof',[(-.68,.86,.86,2.96,3.12),(-.42,.96,.96,2.96,3.16),
          (.92,.92,.92,3.00,3.16),(1.07,.84,.84,2.98,3.11)],coral,.064)
    bar('Beacon rubber foot',(0,.552,3.12),(0,.552,3.31),.194,graphite,16)
    bar('Amber beacon lens',(0,.552,3.29),(0,.552,3.56),.155,amber,16)
    winter_patch('Snow on left roof',(-.43,-.04,3.139),(.47,.61,.072))
    winter_patch('Snow on engine',(.30,-1.72,1.88),(.54,.47,.094))
    pipe('Windscreen wiper',[(-.64,1.744,2.093),(.09,1.54,2.35),(.47,1.405,2.52)],.027,graphite)
    pipe('Exhaust stack',[(.70,-1.5,1.74),(.70,-1.5,2.20),(.70,-1.64,2.30)],.078,graphite)
    for s in (-1,1):
        pipe('Lift arm',[(s*.63,1.21,1.04),(s*.77,1.94,.74),(s*1.0,2.63,.65)],.128,graphite)
        bar('Blade hydraulic ram',(s*.55,1.46,1.37),(s*.76,2.15,.96),.109,steel)
        bar('Blade piston',(s*.76,2.15,.96),(s*.95,2.71,.66),.062,chrome)
    # Rolled steel mouldboard. The swept wings and snow lip read from above.
    prof=[(3.37,.085),(3.41,.17),(3.15,.34),(2.88,.68),(2.85,.98),(2.99,1.24),
          (2.88,1.27),(2.73,1.0),(2.73,.68),(3.01,.27),(3.23,.10)]
    profile('Curved plough blade',prof,-1.82,1.82,steel,.036)
    box('Cutting edge',(0,3.386,.102),(3.67,.103,.093),chrome,.018)
    for x in [-1.1,-.48,.28,.96]:
        winter_patch('Snow rolled on blade',(x,3.27,.183),(.34,.118,.105))


def sled(patrol):
    accent = ice if patrol else orange
    belt('Drive track',0,.59,-1.16,-.06,.296,.255,5)
    shell('Tunnel',[(-1.43,.34,.37,.42,.57),(-1.05,.43,.42,.41,.62),
          (.26,.38,.42,.40,.67),(.78,.34,.42,.40,.61)],graphite,.025)
    shell('Sculpted seat',[(-1.23,.25,.21,.54,.67),(-.98,.31,.27,.54,.78),
          (-.58,.29,.26,.57,.75),(-.21,.22,.19,.61,.81),(.15,.18,.17,.67,.87)],rubber,.035)
    shell('Seat piping',[(-1.25,.24,.22,.52,.60),(-.96,.31,.29,.54,.64),
          (-.57,.30,.27,.56,.66),(-.17,.23,.21,.61,.71)],accent,.018)
    shell('Belly pan',[(-.30,.41,.41,.48,.65),(.18,.53,.53,.38,.73),
          (.63,.49,.54,.34,.73),(.99,.39,.43,.39,.64),(1.18,.20,.24,.48,.63)],graphite,.035)
    hood=[(-.19,.37,.29,.59,.82),(.17,.52,.34,.58,1.01),
          (.51,.52,.33,.57,.99),(.87,.41,.25,.55,.87),(1.20,.22,.14,.56,.70)]
    shell('Swept cowling',hood,
          ivory if patrol else orange,.028)
    shell('Central hood inlay',[(.19,.18,.19,.967,1.021),(.49,.22,.15,.91,1.027),
          (.83,.21,.12,.79,.906),(1.19,.095,.082,.646,.707)],coral if patrol else graphite,.016)
    # Bowed, thin laminated polycarbonate deflector and its moulded edging.
    def screen(x,h):
        return (x*(.306-.019*h),.444-.274*h-.074*x*x,.987+h*(.278-.057*x*x))
    strip=[screen(x,h) for h in (0,.5,1) for x in (-1,-.5,0,.5,1)]
    wind=mesh('Smoked deflector',strip,[(i,i+1,i+6,i+5) for i in (0,1,2,3,5,6,7,8)],smoke)
    wind.modifiers.new('Laminated edge','SOLIDIFY').thickness=.012
    pipe('Screen seal',[screen(x,0) for x in (-1,-.5,0,.5,1)],.011,graphite)
    points=[screen(x,h) for x,h in [(-.77,.22),(-.66,.20),(.45,.90),(.24,.92)]]
    mesh('Reflected sky',[(x,y+.009,z+.005) for x,y,z in points],[(0,1,2),(0,2,3)],glass)
    def hood_height(x,y):
        a,b=next((a,b) for a,b in zip(hood,hood[1:]) if a[0]<=y<=b[0])
        u=(y-a[0])/(b[0]-a[0])
        w,t,lo,hi=(a[i]+(b[i]-a[i])*u for i in range(1,5))
        r=min(w*.23,(hi-lo)*.28)
        if x<t-r:return hi
        if x<t:return hi-(x-t+r)
        return hi-r+(lo+2*r-hi)*(x-t)/(w-t)
    for s in (-1,1):
        # Upturned ski profiles with a continuous runner and a loop at the toe.
        yz=[(-.22,.11),(-.07,.079),(.44,.04),(1.08,.052),(1.37,.146),(1.55,.289),
            (1.58,.351),(1.51,.349),(1.31,.236),(1.02,.137),(.19,.118),(-.18,.16)]
        profile('Upturned runner',yz,s*.54-.092,s*.54+.092,rubber,.024)
        pipe('Ski loop',[(s*.54,1.13,.14),(s*.54,1.32,.313),(s*.54,1.49,.40),
                         (s*.54,1.55,.329)],.030,graphite)
        for anchor in (-.01,.76):
            bar('Wishbone',(s*.34,anchor,.49),(s*.54,.59,.147),.041,steel)
        bar('Shock housing',(s*.36,.34,.63),(s*.51,.54,.22),.059,accent)
        bar('Damper piston',(s*.51,.54,.22),(s*.54,.59,.143),.029,chrome)
        board=box('Running board',(s*.412,-.53,.487),(.19,1.09,.060),steel,.025)
        for j in range(5):
            box('Boot cleat',(s*.423,-.91+.16*j,.526),(.162,.038,.026),graphite,.008)
        profile('Speed panel',[(.12,.61),(.52,.65),(.76,.725),(.58,.76),(.29,.713)],
                s*.494,s*.521,ice if patrol else coral,.01)
        for j in range(3):
            bar('Side vent',(s*(.514-j*.012),.46+.078*j,.66),
                (s*(.510-j*.015),.54+.078*j,.72),.017,graphite,6)
        outline=[(.116,1.108),(.174,1.081),(.303,.881),(.377,.737),(.291,.766),(.196,.881)]
        cx=sum(x for x,y in outline)/len(outline);cy=sum(y for x,y in outline)/len(outline)
        for size,offset,mat in [(1,.018,graphite),(.77,.029,lamp)]:
            xy=[(cx,cy)]+[(cx+(x-cx)*size,cy+(y-cy)*size) for x,y in outline]
            verts=[(s*x,y,hood_height(x,y)+offset) for x,y in xy]
            mesh('Flush sweep headlight',verts,[(0,i+1,1+(i+1)%len(outline)) for i in range(len(outline))],mat)
        pipe('Grab bumper',[(s*.43,.87,.46),(s*.31,1.22,.51),(s*.12,1.27,.55)],.046,graphite)
        pipe('Grab rail',[(s*.36,-.94,.57),(s*.35,-1.29,.71),(s*.25,-1.39,.70)],.027,chrome)
    bar('Tail bumper',(-.25,-1.39,.70),(.25,-1.39,.70),.028,chrome)
    box('Tail light',(0,-1.412,.585),(.27,.034,.07),brake,.017)
    bar('Steering post',(0,.194,.85),(0,.04,1.092),.045,graphite)
    for s in (-1,1):
        pipe('Swept steering bar',[(0,.04,1.092),(s*.18,.03,1.095),(s*.251,-.04,1.13),
                                  (s*.42,-.077,1.13)],.024,chrome)
        bar('Grip',(s*.282,-.046,1.13),(s*.42,-.077,1.13),.039,graphite)
    if patrol:
        box('Medical pannier',(0,-1.127,.835),(.77,.46,.39),ice,.054)
        box('Lid',(0,-1.127,1.026),(.79,.478,.069),ice,.025)
        for s in (-1,1):
            box('Pannier webbing',(s*.236,-1.128,1.057),(.044,.462,.022),graphite,.008)
            box('Side medical cross',(s*.390,-1.127,.85),(.018,.227,.061),ivory,.007)
            box('Side medical cross',(s*.392,-1.127,.85),(.017,.061,.206),ivory,.007)
        box('Medical cross seam',(0,-1.359,.842),(.27,.017,.068),ivory,.007)
        box('Medical cross upright',(0,-1.361,.842),(.074,.018,.244),ivory,.007)
        rider()


def rider():
    # Reuse the sculpted sitter's original face, goggles and jacket folds.
    # Relocate the hips onto the saddle, straddle it with the knees, bring
    # both hands up and forwards to the bars, and fit boots to the boards.
    data=(ROOT/'docs/models/npc.js').read_text()
    asset=re.search(r'__GLB(?:\.|\[")sitter(?:"\])?\s*=\s*"([A-Za-z0-9+/=]+)"',data).group(1)
    with tempfile.TemporaryDirectory() as tmp:
        p=Path(tmp)/'sitter.glb';p.write_bytes(base64.b64decode(asset))
        bpy.ops.import_scene.gltf(filepath=str(p))
    body=next(o for o in bpy.context.selected_objects if o.type=='MESH')
    bm=bmesh.new();bm.from_mesh(body.data)
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.co.y<-.155 and v.co.z<.162],context='VERTS')
    for v in bm.verts:
        x,y,z=v.co
        reach=min(1,max(0,(abs(x)-.195)/.205))*min(1,max(0,(.65-z)/.34))
        lower=min(1,max(0,(-y-.06)/.20))*min(1,max(0,(.41-z)/.19))
        v.co=(-x*(1.21-.27*reach)-math.copysign(.105*lower,x),
              -y*1.16-.403+.20*z+.525*reach,
              .554+1.48*z+.345*reach-.18*lower)
    bm.to_mesh(body.data);bm.free();body.name='Patroller wearing ski goggles'
    for face in body.data.polygons:face.use_smooth=True
    coat=paint('Fabric and face','ffffff')
    coat['map_name']='tube60B-jacket'
    texture=RES/'Race/Textures/tube60B-jacket.png'
    image=bpy.data.images.load(str(texture),check_existing=True);image.pack()
    tex=coat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
    coat.node_tree.links.new(tex.outputs['Color'],coat.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
    body.data.materials.clear();body.data.materials.append(coat)
    for s in (-1,1):
        box('Insulated patrol boot',(s*.35,-.175,.603),(.222,.339,.160),graphite,.055)
        box('Boot cuff',(s*.34,-.233,.696),(.204,.193,.222),rubber,.045)
        box('Boot sole',(s*.35,-.16,.541),(.228,.344,.062),track_edge,.022)


def save_model(group, palette):
    # Index by split corner normal, UV and vertex. This retains windscreen
    # creases and smooth shock absorbers after joining into one Unity draw.
    objects=list(group.objects)
    dep=bpy.context.evaluated_depsgraph_get()
    batches={}
    for source in objects:
        obj=source.evaluated_get(dep)
        me=obj.to_mesh();me.calc_loop_triangles()
        linear=source.matrix_world.to_3x3().inverted().transposed()
        uv=me.uv_layers.active
        for tri in me.loop_triangles:
            if tri.area<1e-9:
                continue
            mat=me.materials[tri.material_index]
            tex=mat.get('map_name','')
            glow=round(255*mat.get('emission',0))
            batch=batches.setdefault((tex,glow),({},[]));points,indices=batch
            color=tuple(palette[mat.name])
            for k in (0,2,1):
                loop=tri.loops[k];i=me.loops[loop].vertex_index
                p=obj.matrix_world@me.vertices[i].co
                n=(linear@me.corner_normals[loop].vector).normalized()
                t=tuple(uv.data[loop].uv) if tex and uv else (0,0)
                point=((p.x,p.z,p.y),tuple(round(v*127) for v in (n.x,n.z,n.y)),t,color)
                if point not in points:points[point]=len(points)
                indices.append(points[point])
        obj.to_mesh_clear()
    buf=bytearray()
    def u(i):buf.extend(struct.pack('<i',i))
    u(len(batches));tris=0
    for (tex,glow),(points,indices) in batches.items():
        field=tex.encode();u(len(field));buf.extend(field);u(0);u(1);u(glow);u(len(points))
        for j,fmt in enumerate(('<3f','<3b','<2f','<4B')):
            for point in points:buf.extend(struct.pack(fmt,*point[j]))
        u(len(indices));buf.extend(struct.pack('<%di'%len(indices),*indices));u(0);tris+=len(indices)//3
    (MODELS/(group.name+'.bytes')).write_bytes(buf)
    print('VEHICLE',group.name,'triangles',tris,'bytes',len(buf),flush=True)


def switch(name):
    group=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(group)
    bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[name]


if '--export-edited' in sys.argv:
    bpy.ops.wm.open_mainfile(filepath=str(ART))
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coral=paint('Vermilion enamel','db3e2e');orange=paint('Tangerine enamel','ff7b20')
    ice=paint('Lagoon patrol','178992');ivory=paint('Warm ivory','f5eee1')
    graphite=paint('Deep navy trim','263847');rubber=paint('Graphite rubber','1f2a36')
    track_edge=paint('Tread edges','445565');steel=paint('Satin gunmetal','687f90')
    chrome=paint('Brushed alloy','a8bfc7');snow=paint('Lying snow','e8f6ff')
    glass=paint('Glacier blue glazing','538aa5');smoke=paint('Smoked polycarbonate','34546a')
    lamp=paint('Headlamp','ffe9ab',.55)
    amber=paint('Amber lens','ffa33b',.48);brake=paint('Rear lens','ff4e38',.28)
    switch('cat12A');groomer()
    switch('snowmobile');sled(True)
    switch('playerSled');sled(False)
    # Keep each vehicle at its own origin. Toggle collections in Blender to edit.
    bpy.data.collections['snowmobile'].hide_viewport=True
    bpy.data.collections['playerSled'].hide_viewport=True
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(ART),compress=True)

def srgb(c):
    return round(255*max(0,min(1,12.92*c if c<=.0031308 else 1.055*c**(1/2.4)-.055)))

palette={m.name:tuple(srgb(v) for v in m.diffuse_color[:3])+(255,) for m in bpy.data.materials}
for key in ('cat12A','snowmobile','playerSled'):
    col=bpy.data.collections[key];col.hide_viewport=False
    bpy.context.view_layer.update()
    save_model(col,palette)
