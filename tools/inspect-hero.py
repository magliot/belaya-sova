import bpy, json, sys
from mathutils import Vector
path=sys.argv[sys.argv.index('--')+1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=path)
for o in bpy.context.scene.objects:
    print('OBJECT',o.name,o.type,tuple(o.dimensions),tuple(o.location),tuple(o.rotation_euler),tuple(o.scale))
    if o.type=='ARMATURE':
        for b in o.data.bones:
            print('BONE',b.name,'parent',b.parent.name if b.parent else '', 'head',tuple(round(v,4) for v in o.matrix_world@b.head_local),'tail',tuple(round(v,4) for v in o.matrix_world@b.tail_local))
    if o.type=='MESH':
        print('GROUPS',[(g.index,g.name) for g in o.vertex_groups])
        print('BOUNDS',[(round(min((o.matrix_world@v.co)[i] for v in o.data.vertices),4),round(max((o.matrix_world@v.co)[i] for v in o.data.vertices),4)) for i in range(3)])
