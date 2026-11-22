"""Blender 5: blender --factory-startup -b <Mishanya ski programme.blend> -P tools/export-mishanya-jumps.py

Export the seven authored NLA jumps, with the mounted equipment, as one synchronized
FBX timeline. Remove only the filmed trajectory and grade: Unity supplies the snow
surface, heading and ballistic movement. Frame 0 in the FBX is Blender frame 184.
"""
from pathlib import Path
import bpy
from mathutils import Matrix, Vector

scene = bpy.context.scene
rig = bpy.data.objects["Mishanya_Rig"]
root = rig.pose.bones[0]
skis = [bpy.data.objects[side + "_Ski"] for side in ("Left", "Right")]
track = rig.animation_data.nla_tracks[0]
strips = list(track.strips)[3:10]
assert [s.action.name[:2] for s in strips] == ["04", "05", "06", "07", "08", "09", "10"]
scene.frame_set(1)
anchor = rig
while anchor.parent:
    anchor = anchor.parent
anchor.animation_data_clear()
anchor.matrix_world = Matrix.Identity(4)
scene.frame_set(1)
basis = rig.matrix_world.copy()
rest = root.bone.matrix_local.copy()
centre = Vector((0, 0, .34))
rig.animation_data.use_nla = False
for strip in strips:
    action = strip.action
    rig.animation_data.action = action
    first, last = map(round, action.frame_range)
    source = []
    for frame in range(first, last + 1):
        scene.frame_set(frame)
        original = basis @ root.matrix @ rest.inverted() @ basis.inverted()
        if frame == first:
            start_rotation = original.to_quaternion()
        orientation = original.to_quaternion() @ start_rotation.inverted()
        fixed = Matrix.Translation(centre - orientation @ centre) @ orientation.to_matrix().to_4x4()
        correction = fixed @ original.inverted()
        sole = min((correction @ ski.matrix_world @ v.co).z for ski in skis for v in ski.data.vertices)
        source.append((fixed, sole))
    previous = None
    for frame, (fixed, sole) in enumerate(source, first):
        # Correct the base at either end. A skater's knees and skis are free to tuck in the air.
        t = (frame - first) / (last - first)
        dz = -(source[0][1]*(1-t) + source[-1][1]*t)
        root.matrix = basis.inverted() @ Matrix.Translation((0, 0, dz)) @ fixed @ basis @ rest
        quat = root.rotation_quaternion.copy()
        if previous is not None:
            quat.make_compatible(previous)
        root.rotation_quaternion = quat
        previous = quat
        root.keyframe_insert(data_path="rotation_quaternion", frame=frame)
        root.keyframe_insert(data_path="location", frame=frame)
    print("Prepared", action.name, first, last)
rig.animation_data.action = None
rig.animation_data.use_nla = True
scene.frame_start, scene.frame_end = 184, 674
scene.frame_set(scene.frame_start)
bpy.ops.object.select_all(action='DESELECT')
for obj in [rig, *rig.children_recursive]:
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
output = Path("unity/OwlSlope/Assets/Resources/Bear.fbx").resolve()
bpy.ops.export_scene.fbx(filepath=str(output), use_selection=True, object_types={'ARMATURE','MESH','EMPTY'},
                        axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim_use_all_actions=False,
                        bake_anim_use_nla_strips=False, bake_anim_simplify_factor=.3, path_mode='STRIP',
                        use_custom_props=False, mesh_smooth_type='FACE')
print("Exported", output)
