import bpy,sys,os
from mathutils import Vector
out=os.path.abspath(sys.argv[sys.argv.index('--')+1])
s=bpy.context.scene;s.cycles.use_denoising=False;s.cycles.samples=24
s.render.resolution_x=600;s.render.resolution_y=750
s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGB'
s.render.filepath=os.path.join(out,'render-final','frame-');os.makedirs(os.path.dirname(s.render.filepath),exist_ok=True)
args=sys.argv[sys.argv.index('--')+1:]
s.frame_start=int(args[1]) if len(args)>1 else 1
s.frame_end=int(args[2]) if len(args)>2 else 180
bpy.ops.render.render(animation=True)
