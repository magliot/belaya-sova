"""Run in the editable Sova board scene, or during build-hero-pilot.py."""
import bpy
from mathutils import Vector


def soles():
    scene=bpy.context.scene
    frame_before=scene.frame_current
    body=bpy.data.objects['Sova_Mesh']
    board=bpy.data.objects['Snowboard']
    rig=bpy.data.objects['Sova_Rig']
    try:
        scene.frame_set(1)
        def surface():
            obj=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
            return obj.data.vertices,board.matrix_world.inverted()@obj.matrix_world
        vertices,basis=surface()
        points=[basis@v.co for v in vertices]
        ids=[(i,p) for i,p in enumerate(points) if p.z<.030]
        if len(ids)<150:raise ValueError('Missing sole vertices in the stance')
        drift=0
        hip_clearance=1
        for frame in range(1,scene.frame_end+1):
            scene.frame_set(frame)
            vertices,basis=surface()
            for i,rest in ids:drift=max(drift,(basis@vertices[i].co-rest).length)
            if 181<=frame<=240:
                for side in ('Left','Right'):
                    hip=rig.matrix_world@rig.pose.bones['mixamorig:'+side+'UpLeg'].head
                    foot=rig.matrix_world@rig.pose.bones['mixamorig:'+side+'Foot'].head
                    hip_clearance=min(hip_clearance,hip.z-foot.z)
        if drift>.002 or hip_clearance<.045:
            raise ValueError(f'Sole drift {drift:.4f} m, hip above boot {hip_clearance:.4f} m')
        return {'vertices':len(ids),'frames':scene.frame_end,'maximum_deck_drift_metres':drift,
                'minimum_hip_above_ankle_metres':hip_clearance}
    finally:
        scene.frame_set(frame_before)


def check():
    scene=bpy.context.scene
    frame_before=scene.frame_current
    body=bpy.data.objects['Sova_Mesh']
    board=bpy.data.objects['Snowboard']
    errors={}
    try:
        for name,start,side,sign in [('Indy',181,'Left',1),('Melon',211,'Right',-1)]:
            group=body.vertex_groups['mixamorig:'+side+'Hand'].index
            feathers=[v.index for v in body.data.vertices if any(g.group==group and g.weight>.4 for g in v.groups)]
            peak_error=0
            for frame in range(start+12,start+22):
                scene.frame_set(frame)
                loaded=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
                edge=board.matrix_world@Vector((sign*.072,-sign*.156,.008))
                miss=min((loaded.matrix_world@loaded.data.vertices[i].co-edge).length for i in feathers)
                peak_error=max(peak_error,miss)
            if peak_error>.012:
                raise ValueError(f'{name}: lost edge contact, gap {peak_error:.4f} m')
            errors[name]={'frames':10,'maximum_gap_metres':peak_error}
        return errors
    finally:
        scene.frame_set(frame_before)


if __name__=='__main__':
    print('BOARD GRABS PASS',check(),'SOLES',soles())
