"""Blender background script. Add --export-edited to bake the editable cone again."""
from math import cos, pi, sin
from pathlib import Path
from struct import pack
import sys

import bpy

root = Path(__file__).resolve().parents[1]
source = root / "art/training-slope/training-cone.blend"
target = root / "unity/OwlSlope/Assets/Resources/Race/Models/trainingCone.bytes"
palette = {"Rubber": (44, 56, 69), "Orange": (255, 91, 23),
           "Sleeve": (251, 247, 227), "Lip": (238, 65, 18), "Inside": (108, 44, 32)}


def linear(channel):
    s = channel / 255
    return s / 12.92 if s <= .04045 else ((s + .055) / 1.055) ** 2.4


def srgb(value):
    s = 12.92 * value if value <= .0031308 else 1.055 * value ** (1/2.4) - .055
    return round(255 * max(0, min(1, s)))


def make_cone():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    verts, faces, face_mats = [], [], []
    def ring(points, z):
        out = list(range(len(verts), len(verts) + len(points)))
        verts.extend((x, y, z) for x, y in points)
        return out
    def face(indices, material):
        faces.append(indices)
        face_mats.append(material)
    def join(bottom, top, material):
        for i in range(len(top)):
            j = (i + 1) % len(top)
            face((bottom[i], bottom[j], top[j], top[i]), material)

    # Octagonal cuts keep the heavy square foot recognisable at a distance.
    corners = [(1, .75), (.75, 1), (-.75, 1), (-1, .75),
               (-1, -.75), (-.75, -1), (.75, -1), (1, -.75)]
    prev = None
    for z, radius, mat in [(0, .376, "Rubber"), (.018, .395, "Rubber"),
                           (.079, .395, "Rubber"), (.102, .37, "Rubber"),
                           (.12, .34, "Lip")]:
        level = ring([(x * radius, y * radius) for x, y in corners], z)
        if prev is None:
            face(tuple(reversed(level)), "Rubber")
        else:
            join(prev, level, mat)
        prev = level
    face(prev, "Lip")

    circle = [(cos(i*pi/12), sin(i*pi/12)) for i in range(24)]
    prev = None
    # The sleeves follow the taper. A rolled top and recessed throat leave a visible opening.
    for z, radius, mat in [
        (.115,.321,"Lip"),(.145,.324,"Lip"),(.173,.301,"Orange"),
        (.416,.243,"Orange"),(.546,.212,"Sleeve"),(.724,.169,"Orange"),
        (.837,.142,"Sleeve"),(1.10,.079,"Orange"),(1.13,.078,"Lip"),
        (1.145,.068,"Orange"),(1.145,.043,"Orange"),(1.067,.047,"Inside")
    ]:
        level = ring([(x * radius, y * radius) for x, y in circle], z)
        if prev is not None:
            join(prev, level, mat)
        prev = level
    face(prev, "Inside")

    mesh = bpy.data.meshes.new("Striped safety cone")
    mesh.from_pydata(verts, [], faces)
    obj = bpy.data.objects.new("Training cone", mesh)
    bpy.context.collection.objects.link(obj)
    for name, color in palette.items():
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (*map(linear, color), 1)
        mesh.materials.append(mat)
    for polygon, key in zip(mesh.polygons, face_mats):
        polygon.material_index = list(palette).index(key)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(source), compress=True)


if "--export-edited" in sys.argv:
    bpy.ops.wm.open_mainfile(filepath=str(source))
else:
    make_cone()

# A single material and vertex-coloured indexed mesh in the course model format.
obj = bpy.data.objects["Training cone"]
mesh = obj.data
mesh.calc_loop_triangles()
points, indices = {}, []
for tri in mesh.loop_triangles:
    material = mesh.materials[tri.material_index]
    rgba = tuple(srgb(v) for v in material.diffuse_color[:3]) + (255,)
    for corner in (0, 2, 1):
        loop = tri.loops[corner]
        p = mesh.vertices[mesh.loops[loop].vertex_index].co
        n = mesh.corner_normals[loop].vector
        key = (tuple((p.x, p.z, p.y)), tuple(round(v*127) for v in (n.x,n.z,n.y)), rgba)
        if key not in points:
            points[key] = len(points)
        indices.append(points[key])

data = bytearray(pack("<6i", 1, 0, 0, 1, 0, len(points)))
for position, _, _ in points:
    data += pack("<3f", *position)
for _, normal, _ in points:
    data += pack("<3b", *normal)
data += bytes(len(points)*8)  # UVs for the untextured shared course shader
for _, _, color in points:
    data += bytes(color)
data += pack("<i", len(indices)) + pack(f"<{len(indices)}i", *indices) + pack("<i", 0)
target.write_bytes(data)
print("TRAINING CONE", len(indices)//3, "triangles,", len(data), "bytes")
