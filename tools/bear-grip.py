"""Rigid equipment built in the palm's frame. Blender coordinates: forward -Y, up Z."""
import bpy,bmesh,math
from mathutils import Vector,Matrix

def wrist_frame(wrist,direction):
    f=direction.normalized();right=Vector((0,0,1)).cross(f).normalized()
    m=Matrix((right,-f,right.cross(-f))).transposed().to_4x4();m.translation=wrist
    return m

def fit_gloves(rig,body,material):
    bm=bmesh.new();bm.from_mesh(body.data);world=body.matrix_world;inv=world.inverted()
    mat=material('Graphite insulated leather',(.034,.045,.060),.05,.64)
    pad=material('Knuckle reinforcement',(.060,.076,.094),.06,.55)
    cuff=material('Glove cuff',(.022,.029,.042),0,.82)
    red=material('Safety vermilion',(.88,.11,.022),.05,.38)
    alloy=material('Brushed pole alloy',(.26,.35,.43),.54,.4)
    grip=material('Pole grip rubber',(.014,.019,.027),0,.83)
    objects=[];bases={}
    for side,sign in [('Left',1),('Right',-1)]:
        joint='mixamorig:'+side+'Hand'; fore='mixamorig:'+side+'ForeArm'
        w=rig.matrix_world@rig.data.bones[joint].head_local
        e=rig.matrix_world@rig.data.bones[fore].head_local;axis=(w-e).normalized()
        base=wrist_frame(w,axis);bases[side]=base
        # Bisect just the sleeve, retaining its original UVs and red jacket silhouette.
        verts={v for v in bm.verts if (world@v.co).x*sign>.201 and .245<(world@v.co).z<.59}
        geom=list(verts)+[ed for ed in bm.edges if all(v in verts for v in ed.verts)]+[p for p in bm.faces if all(v in verts for v in p.verts)]
        cut=w-axis*.013
        bmesh.ops.bisect_plane(bm,geom=geom,dist=.000001,plane_co=inv@cut,plane_no=world.to_3x3().transposed()@axis,clear_outer=True)
        root=bpy.data.objects.new(side+'_Glove',None);bpy.context.collection.objects.link(root)
        root.parent=rig;root.parent_type='BONE';root.parent_bone=joint
        bpy.context.view_layer.update();root.matrix_world=base;objects.append(root)
        def mesh(name,verts,faces,mt,parent=root):
            data=bpy.data.meshes.new(side+' '+name);data.from_pydata(verts,[],faces);data.update()
            ob=bpy.data.objects.new(side+'_'+name,data);bpy.context.collection.objects.link(ob)
            ob.parent=parent;ob.data.materials.append(mt)
            for p in data.polygons:p.use_smooth=True
            objects.append(ob);return ob
        def oval(name,pos,scale,mt):
            bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12)
            ob=bpy.context.object;ob.name=side+'_'+name;ob.parent=root;ob.location=pos;ob.scale=scale
            ob.data.materials.append(mt)
            for p in ob.data.polygons:p.use_smooth=True
            objects.append(ob);return ob
        # The cuff covers the cut, even when the wrist flexes. The fingers wrap
        # around a real bore, not a painted dot or a pole through a solid fist.
        oval('Cuff',(0,.015,0),(.059,.041,.057),cuff)
        oval('Palm',(sign*.017,-.019,.002),(.041,.050,.047),mat)
        def wrap(name,start,end,radius,width,height,z,mt):
            vv=[];ff=[];n=25;k=32
            for i in range(n):
                t=i/(n-1);a=math.radians(start+(end-start)*t)
                taper=.48+.52*math.sin(math.pi*t)**.30
                for j in range(k):
                    b=math.tau*j/k;rr=radius+width*taper*math.cos(b);zz=z+height*taper*math.sin(b)
                    if name=='FingerPad' and math.cos(b)>0:
                        # Three soft creases separate the four curled fingers across the outside.
                        rr-=.004*math.cos(b)*sum(math.exp(-((zz-h)/.0027)**2) for h in [-.024,-.006,.012])
                    vv.append((sign*rr*math.cos(a),-.067+rr*math.sin(a),zz))
            for i in range(n-1):
                for j in range(k):
                    a=i*k+j;b=i*k+(j+1)%k;ff.append((a,b,b+k,a+k))
            ff.extend([tuple(reversed(range(k))),tuple(range((n-1)*k,n*k))])
            ob=mesh(name,vv,ff,mt);norm=bmesh.new();norm.from_mesh(ob.data);bmesh.ops.recalc_face_normals(norm,faces=norm.faces[:]);norm.to_mesh(ob.data);norm.free()
        wrap('FingerPad',75,-151,.028,.016,.038,-.006,mat)
        wrap('ThumbPad',116,230,.028,.017,.023,.013,pad)
        oval('Knuckle',(sign*.037,-.058,.007),(.009,.022,.026),pad)
        oval('CuffTab',(sign*.040,.001,.034),(.012,.016,.007),red)
        pole=bpy.data.objects.new(side+'_Pole',None);bpy.context.collection.objects.link(pole);pole.parent=root;pole.location=(0,-.067,0);objects.append(pole)
        def lathe(name,profile,mt):
            vv=[];ff=[];n=16
            for z,r in profile:
                for j in range(n):
                    a=math.tau*j/n;vv.append((r*math.cos(a),r*math.sin(a),z))
            for i in range(len(profile)-1):
                for j in range(n):
                    a=i*n+j;b=i*n+(j+1)%n;ff.append((a,b,b+n,a+n))
            ff.extend([tuple(reversed(range(n))),tuple(range(len(vv)-n,len(vv)))])
            return mesh(name,vv,ff,mt,pole)
        lathe('Grip',[(-.057,.010),(-.048,.014),(.031,.014),(.045,.017),(.049,.012)],grip)
        lathe('GripCap',[(.046,.016),(.051,.016),(.054,.011)],red)
        lathe('Shaft',[(-.46,.002),(-.43,.004),(-.18,.005),(-.045,.006)],alloy)
        lathe('Band',[(-.17,.0056),(-.144,.0056)],red)
        lathe('Basket',[(-.43,.007),(-.424,.022),(-.418,.022),(-.415,.006)],cuff)
    bm.to_mesh(body.data);bm.free();body.data.update()
    return objects,bases
