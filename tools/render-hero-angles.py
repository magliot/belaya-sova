import bpy,os,sys
from mathutils import Vector
out=os.path.abspath(sys.argv[sys.argv.index('--')+1]);s=bpy.context.scene;s.cycles.use_denoising=False
s.cycles.samples=24;s.render.resolution_x=375;s.render.resolution_y=812;s.camera.data.ortho_scale=1.95
for name,pos in [('side',(3,0,1.1)),('back',(1,3,1.2))]:
 s.camera.location=pos;s.camera.rotation_euler=(Vector((0,0,.6))-s.camera.location).to_track_quat('-Z','Y').to_euler()
 s.frame_set(128);s.render.filepath=os.path.join(out,name+'-final.png');bpy.ops.render.render(write_still=True)
