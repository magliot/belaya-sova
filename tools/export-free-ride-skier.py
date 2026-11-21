"""From the repository root: blender --factory-startup -b -P tools/export-free-ride-skier.py."""
import json
import math
import re
import struct
import sys
import uuid
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.dont_write_bytecode = True
from free_ride_skier_poles import add_poles

source = Path('art/freeride-skier/Ski_Backflip.glb').resolve()
resources = Path('unity/OwlSlope/Assets/Resources').resolve()
asset = resources / 'FreeRideSkier.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 30
bpy.ops.import_scene.gltf(filepath=str(source))
rig = bpy.data.objects['Skier_Rig']
body = bpy.data.objects['Skier_Game']
action = rig.animation_data.action
scene.name = 'Ski_Backflip'
first, last = map(round, action.frame_range)
assert (first, last) == (0, 95), (first, last)
scene.frame_start, scene.frame_end = first, last
root = rig.pose.bones['root']
feet = [rig.pose.bones['foot.' + side] for side in ('L', 'R')]
root_rest = root.bone.matrix_local
sole_points = []
for foot in feet:
    group = body.vertex_groups[foot.name].index
    points = [v.co.copy() for v in body.data.vertices if v.co.z < .011 and
              any(g.group == group and g.weight > .95 for g in v.groups)]
    assert len(points) > 5, (foot.name, len(points))
    sole_points.append(points)


def skin_matrix(bone):
    return bone.matrix @ bone.bone.matrix_local.inverted()


def ski_pitch(frame):
    scene.frame_set(frame)
    tangent = skin_matrix(feet[0]).to_3x3() @ Vector((1, 0, 0))
    return math.atan2(tangent.z, tangent.x)


# In the source film each foot follows the grade of the filmed approach/landing.
# Unity supplies that grade, the jump arc, speed and world position for each seed.
# Keep the authored turn and limb curves; fit the root's pivot to the skier's hips.
approach_pitch, landing_pitch = ski_pitch(first), ski_pitch(last)
takeoff, contact = 22, 72
pivot = rig.data.bones['pelvis'].head_local.copy()
poses, ground_offset = [], []
for frame in range(first, last + 1):
    scene.frame_set(frame)
    t = min(1, max(0, (frame - takeoff) / (contact - takeoff)))
    t = t * t * (3 - 2 * t)
    grade = approach_pitch * (1 - t) + landing_pitch * t
    original = skin_matrix(root)
    orientation = Quaternion((0, 1, 0), grade) @ original.to_quaternion()
    fixed = Matrix.LocRotScale(pivot - orientation @ pivot, orientation, Vector((1,1,1)))
    to_fixed = fixed @ original.inverted()
    bases = [to_fixed @ skin_matrix(foot) for foot in feet]
    ground_offset.append(-min((base @ p).z for base, points in zip(bases, sole_points) for p in points))
    poses.append(fixed)

previous = None
for frame, fixed in enumerate(poses):
    t = min(1, max(0, (frame - takeoff) / (contact - takeoff)))
    # Use the actual sole only while grounded. In the air a tucked leg must be free
    # to come up, and the head must be able to pass underneath the skis.
    dz = (ground_offset[frame] if not takeoff < frame < contact else
          (1-t)*ground_offset[takeoff] + t*ground_offset[contact])
    root.matrix = Matrix.Translation((0,0,dz)) @ fixed @ root_rest
    q = root.rotation_quaternion.copy()
    if previous is not None:
        q.make_compatible(previous)
        root.rotation_quaternion = q
    previous = q
    root.keyframe_insert('location', frame=frame, group=root.name)
    root.keyframe_insert('rotation_quaternion', frame=frame, group=root.name)

scene.frame_set(first)
bpy.context.view_layer.update()
poles = add_poles(rig, body)
for obj in bpy.context.selected_objects:
    obj.select_set(False)
for obj in (rig, body, *poles):
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(asset), use_selection=True,
                         object_types={'ARMATURE','MESH','EMPTY'}, path_mode='STRIP',
                         add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
                         bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0)


def meta(path, template):
    target = Path(str(path) + '.meta')
    if not target.exists():
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'belaya-sova/' + path.name).hex
        target.write_text(re.sub(r'(?m)^guid: .*$', 'guid: ' + guid, template.read_text()), encoding='utf-8')


glb = source.read_bytes()
json_size = struct.unpack_from('<I', glb, 12)[0]
data = json.loads(glb[20:20 + json_size])
pbr = data['materials'][0]['pbrMetallicRoughness']
image = data['images'][data['textures'][pbr['baseColorTexture']['index']]['source']]
view = data['bufferViews'][image['bufferView']]
start = 28 + json_size + view.get('byteOffset', 0)
texture = resources / 'Art/FreeRideSkierAlbedo.png'
texture.write_bytes(glb[start:start + view['byteLength']])
rows = [{'name': data['materials'][0]['name'], 'rgba': [1,1,1,1], 'texture': 'Art/FreeRideSkierAlbedo'}]
for material in poles[0].data.materials:
    rows.append({'name': material.name, 'rgba': list(material.diffuse_color), 'texture': ''})
palette = resources / 'Art/FreeRideSkierMaterials.json'
palette.write_text(json.dumps({'entries': rows}, ensure_ascii=False), encoding='utf-8')
meta(asset, resources / 'Bear.fbx.meta')
meta(texture, resources / 'Art/Fir.jpg.meta')
meta(palette, resources / 'Slope.json.meta')
print('Free Ride skier exported', len(body.data.vertices), 'body vertices;', first, last, 'frames;', asset.stat().st_size, 'FBX bytes')
