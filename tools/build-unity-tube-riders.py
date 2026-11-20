"""Rebuild the four seated riders in Blender 5.2, from the game's original character and tubes.
Run from the repo root: blender -b --factory-startup --python tools/build-unity-tube-riders.py
"""
from collections import defaultdict
from pathlib import Path
import base64
import math
import re
import struct
import tempfile
import uuid

import bmesh
import bpy
import numpy as np
from mathutils import Vector

root = Path.cwd()
art = root / "art/training-slope"
assets = root / "unity/OwlSlope/Assets/Resources/Race"
models = assets / "Models"
textures = assets / "Textures"
source_skin = "468b037b9d8959e8"
variants = [
    ("tube60B", (1.0, .47, .047)),
    ("tube60C", None),
    ("tube61A", (.12, .39, .94)),
    ("tube61C", (.81, .19, .51)),
]

def material(name, color):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    return m

def padded_block(name, center, size, color, bevel):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mod = obj.modifiers.new("Rounded seam", "BEVEL")
    mod.width, mod.segments = bevel, 4
    bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(color)
    return obj

def strap(start, end, width, color):
    a,b = Vector(start),Vector(end)
    o = padded_block("Handle webbing", (a+b)/2, (width, .045, (b-a).length+.025), color, .018)
    o.rotation_euler = (b-a).to_track_quat('Z','Y').to_euler()
    return o

def keep_inflatable(obj):
    # Work on the original sewn ring and its rubber floor. The old mannequin
    # and its outstretched handles are separate mesh islands in the .blend.
    obj.shape_key_clear()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    remaining = set(bm.verts)
    remove = []
    while remaining:
        first = remaining.pop()
        stack, component = [first], []
        while stack:
            v = stack.pop()
            component.append(v)
            for edge in v.link_edges:
                other = edge.other_vert(v)
                if other in remaining:
                    remaining.remove(other)
                    stack.append(other)
        xs = [v.co.x for v in component]
        if max(xs)-min(xs) < 1.7:
            remove.extend(component)
    bmesh.ops.delete(bm, geom=remove, context="VERTS")
    assert len(bm.verts)>500
    bm.to_mesh(obj.data)
    bm.free()
    obj.location = (0,0,-.055)

def make_skin(key, rgb):
    original = bpy.data.images.load(str(textures / (source_skin+".png")), check_existing=True)
    image = original
    map_name=source_skin
    if rgb:
        width,height=original.size
        colors=np.empty(width*height*4,dtype=np.float32)
        original.pixels.foreach_get(colors)
        colors=colors.reshape((-1,4))
        r,g,b=colors[:,0].copy(),colors[:,1].copy(),colors[:,2].copy()
        mask=np.clip((g-np.maximum(r,b)) / np.maximum(g,.003)*3.8,0,1)
        # Replace just green fabric. Retain the baked folds and the original
        # face, goggles, hair, zips and black gloves on the same atlas.
        values=np.maximum(g,.0001)[:,None]*np.asarray(rgb)[None,:]*1.9
        colors[:,:3] = colors[:,:3]*(1-mask[:,None]) + values*mask[:,None]
        map_name=key+"-jacket"
        image=bpy.data.images.new(map_name,width,height,alpha=True)
        image.pixels.foreach_set(colors.ravel())
        destination=textures/(map_name+".png")
        image.filepath_raw=str(destination);image.file_format="PNG";image.save()
        import_meta=Path(str(destination)+".meta")
        if not import_meta.exists():
            template=(textures/(source_skin+".png.meta")).read_text()
            guid=uuid.uuid5(uuid.NAMESPACE_URL,"belaya-sova/race/"+map_name).hex
            import_meta.write_text(re.sub(r"(?m)^guid: .*$","guid: "+guid,template),encoding="utf8")
    mat=material(key+" face and jacket",(1,1,1))
    mat.use_nodes=True
    node=mat.node_tree.nodes.new("ShaderNodeTexImage");node.image=image
    mat.node_tree.links.new(node.outputs["Color"],mat.node_tree.nodes.get("Principled BSDF").inputs["Base Color"])
    mat["map_name"]=map_name
    image.pack()
    return mat

def character(original,key,color):
    body=original.copy();body.data=original.data.copy();bpy.context.collection.objects.link(body)
    body.name=key+" seated rider"
    bm=bmesh.new();bm.from_mesh(body.data)
    # The former prop joined boots to the snowboard. Cut back to the cuffs
    # and fit winter boots for tubing beneath the pants.
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y<-.155 and v.co.z<.162],context="VERTS")
    for v in bm.verts:
        x,y,z=v.co
        # Pull the palms forwards over the side straps while the shoulders
        # stay relaxed, and set the seated pelvis in the centre of the ring.
        glove=max(0,min(1,(abs(x)-.23)/.16))*max(0,min(1,(.40-z)/.18))
        v.co=(1.63*x,1.73*y-.20-glove*.075,.372+1.52*z)
    bm.to_mesh(body.data);bm.free()
    body.data.materials.clear();body.data.materials.append(make_skin(key,color))
    for poly in body.data.polygons:poly.use_smooth=True
    return body

def to_srgb(value):
    return round(255*max(0,min(1,12.92*value if value<.0031308 else 1.055*value**(1/2.4)-.055)))

def encode(obj):
    mesh=obj.data;mesh.calc_loop_triangles()
    groups=defaultdict(list)
    for tri in mesh.loop_triangles:groups[tri.material_index].append(tri)
    raw=bytearray()
    def count(n):raw.extend(struct.pack('<i',n))
    def string(s):
        b=s.encode();count(len(b));raw.extend(b)
    count(len(groups))
    for slot, tris in groups.items():
        mat=mesh.materials[slot]
        uvs=mesh.uv_layers.active.data if mesh.uv_layers.active else None
        vertices={};index=[]
        for tri in tris:
            corners=[]
            for ix in tri.loops:
                loop=mesh.loops[ix];uv=tuple(uvs[ix].uv) if uvs else (0,0)
                k=(loop.vertex_index,*uv)
                if k not in vertices:vertices[k]=len(vertices)
                corners.append(vertices[k])
            index.extend((corners[0],corners[2],corners[1]))
        string(mat.get("map_name",""));count(0);count(1);count(0);count(len(vertices))
        for v,_,_ in vertices:
            x,y,z=mesh.vertices[v].co;raw.extend(struct.pack('<3f',x,z,y))
        for v,_,_ in vertices:
            x,y,z=mesh.vertices[v].normal;raw.extend(struct.pack('<3b',round(x*127),round(z*127),round(y*127)))
        for _,u,v in vertices:raw.extend(struct.pack('<2f',u,v))
        color=bytes([to_srgb(x) for x in mat.diffuse_color[:3]]+[255])
        raw.extend(color*len(vertices))
        count(len(index));raw.extend(struct.pack('<%di'%len(index),*index))
        count(2)
        for key in ("Carve","Absorb"):
            string(key)
            for v,_,_ in vertices:
                x,y,z=mesh.shape_keys.key_blocks[key].data[v].co-mesh.vertices[v].co
                raw.extend(struct.pack('<3h',round(x*10000),round(z*10000),round(y*10000)))
    (models/(obj.name+".bytes")).write_bytes(raw)

bpy.ops.wm.open_mainfile(filepath=str(art/"training-cast.blend"))
tubes={name:bpy.data.objects[name] for name,_ in variants}
for obj in list(bpy.context.scene.objects):
    if obj not in tubes.values():bpy.data.objects.remove(obj,do_unlink=True)
data=(root/"docs/models/npc.js").read_text()
asset=re.search(r'__GLB(?:\.|\[")sitter(?:"\])?\s*=\s*"([A-Za-z0-9+/=]+)"',data).group(1)
with tempfile.TemporaryDirectory() as folder:
    path=Path(folder)/"seated.glb";path.write_bytes(base64.b64decode(asset))
    bpy.ops.import_scene.gltf(filepath=str(path))
original=next(o for o in bpy.context.selected_objects if o.type=="MESH")
dark=material("Insulated boot",(.030,.041,.059))
sole=material("Winter tread",(.078,.09,.11))
weave=material("Graphite webbing",(.036,.045,.053))
trim=material("Light grey piping",(.46,.55,.63))
for i,(key,color) in enumerate(variants):
    tube=tubes[key];keep_inflatable(tube)
    body=character(original,key,color)
    pieces=[body]
    for side in (-1,1):
        # The asymmetric mittens of the scanned sitter meet their respective straps.
        hand_y=.06 if side<0 else .15
        x=.65*side
        pieces.extend([strap((x,hand_y-.13,.43),(x,hand_y-.08,.56),.073,weave),
                       strap((x,hand_y-.08,.56),(x,hand_y+.12,.56),.073,weave),
                       strap((x,hand_y+.12,.56),(x,hand_y+.16,.43),.073,weave)])
        boot_x=side*.293
        pieces.append(padded_block("Ankle", (boot_x,-.57,.60),(.255,.31,.25),dark,.095))
        pieces.append(padded_block("Toe box", (boot_x,-.69,.54),(.27,.395,.185),dark,.080))
        pieces.append(padded_block("Tread",(boot_x,-.70,.469),(.278,.407,.084),sole,.033))
        pieces.append(padded_block("Boot tongue trim",(boot_x,-.75,.618),(.18,.048,.022),trim,.008))
    bpy.ops.object.select_all(action='DESELECT')
    for o in [tube,*pieces]:o.select_set(True)
    bpy.context.view_layer.objects.active=tube
    bpy.ops.object.join()
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    tube.shape_key_add(name="Basis")
    carve=tube.shape_key_add(name="Carve");absorb=tube.shape_key_add(name="Absorb")
    for j,v in enumerate(tube.data.vertices):
        x,y,z=v.co
        weight=max(0,z-.56)*max(0,min(1,(.63-abs(x))/.22))
        carve.data[j].co.x+=weight*.27
        carve.data[j].co.z-=weight*x*.19
        absorb.data[j].co.z-=weight*.115
        absorb.data[j].co.y-=weight*.085
    encode(tube)
    tube.location.x=i*2.5
bpy.data.objects.remove(original,do_unlink=True)
for image in bpy.data.images:
    if image.users and image.filepath and not image.packed_file:image.pack()
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(art/"unity-tube-riders.blend"),compress=True)
