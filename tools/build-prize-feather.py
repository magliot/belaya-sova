"""Build the faceted gold feather from reference 36B. Run in Blender from repo root."""
import bpy
from pathlib import Path
from mathutils import Vector

bpy.ops.wm.read_factory_settings(use_empty=True)
colors = [(1, .54, .025), (.83, .28, .012), (1, .77, .11)]
materials = []
for name, color in zip(('Gold', 'Amber', 'Sunlit edge'), colors):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1)
    material.use_nodes = True
    shader = material.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Metallic'].default_value = .32
    shader.inputs['Roughness'].default_value = .32
    materials.append(material)

def spine(t):
    return Vector((.68*t-.18*t*t, 0, t))

def blade(name, a, b, width, height):
    axis = b-a
    cross = Vector((axis.z, 0, -axis.x)).normalized()*width
    center = a+.53*axis
    vertices = [a, center+cross, b, center-cross, center+Vector((0,-height,0)), center+Vector((0,height,0))]
    faces = [(i,(i+1)%4,4) for i in range(4)]+[((i+1)%4,i,5) for i in range(4)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for mat in materials: mesh.materials.append(mat)
    for p in mesh.polygons: p.material_index = [0,2,1,0][p.index%4]

for i in range(5):
    t = .12+i*.15
    width = [.16,.24,.28,.23,.13][i]
    for side in [-1,1]:
        blade('Vane', spine(t), spine(t+.26)+Vector((side*width,0,.032*side)), .065, .043)
blade('Tip',spine(.69),spine(1.15),.095,.052)
blade('Quill',spine(-.09)+Vector((0,-.033,0)),spine(.94)+Vector((0,-.037,0)),.031,.034)
source=Path('art/air-park/prize-feather.blend').resolve()
bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
bpy.ops.export_scene.gltf(filepath=str(Path('docs/models/prize-feather.glb').resolve()),export_format='GLB')
