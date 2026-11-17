import bpy,sys,os,json
out=os.path.abspath(sys.argv[sys.argv.index('--')+1]);scene=bpy.context.scene
body=bpy.data.objects['Mishanya_Mesh'];rig=bpy.data.objects['Mishanya_Rig']
edges=[(e.vertices[0],e.vertices[1]) for e in body.data.edges]
rest=[body.matrix_world@v.co for v in body.data.vertices]
records=[]
for frame in [1,46,65,92,99,112,123,128,146,181]:
 scene.frame_set(frame); dg=bpy.context.evaluated_depsgraph_get(); ob=body.evaluated_get(dg);mesh=ob.to_mesh(); posed=[ob.matrix_world@v.co for v in mesh.vertices]
 ratios=[]; rigid_boot=[]
 for a,b in edges:
  length=(rest[a]-rest[b]).length
  if length<1e-5:continue
  ratio=(posed[a]-posed[b]).length/length;ratios.append(ratio)
  if max(rest[a].z,rest[b].z)<.15 and rest[a].x*rest[b].x>0:rigid_boot.append(abs(ratio-1))
 ratios.sort();records.append({'frame':frame,'edge_ratio_min':ratios[0],'edge_ratio_p99':ratios[int(len(ratios)*.99)],'edge_ratio_max':ratios[-1],'rigid_boot_edge_error':max(rigid_boot)})
 ob.to_mesh_clear()
json.dump(records,open(os.path.join(out,'deformation-check.json'),'w'),indent=2)
print('DEFORMATION',json.dumps(records))
