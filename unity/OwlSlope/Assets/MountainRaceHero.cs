using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class MountainRace {
    [Serializable] class MaterialRows {public MaterialRow[] entries;}
    [Serializable] class MaterialRow {public string name,texture;public float[] rgba;}
    readonly Dictionary<string,Material> actorPaint=new Dictionary<string,Material>();
    readonly Dictionary<string,GameObject> instances=new Dictionary<string,GameObject>();
    readonly Dictionary<string,float> menuLifts=new Dictionary<string,float>();
    readonly Dictionary<Transform,Quaternion> menuRest=new Dictionary<Transform,Quaternion>();
    readonly Dictionary<string,Transform> seatBones=new Dictionary<string,Transform>();
    Transform actorRoot,menuModel,rideModel,stowed,menuShadow;
    AnimationClip rideClip;
    GameObject tube,sled;
    float camLane,camY;
    float menuYaw,menuVelocity,menuMouseX;
    bool spinHeld;
    MountainDances dances;
    string activeEquipment;
    static void Discard(UnityEngine.Object generated){
        if(Application.isPlaying)Destroy(generated);else DestroyImmediate(generated);
    }

    void SetupCamera(){
        var go=new GameObject("Mountain camera");go.transform.SetParent(transform,false);
        View=go.AddComponent<Camera>();View.tag="MainCamera";View.fieldOfView=72;
        // The chase and portrait cameras both stand metres from the rider. Give the
        // depth buffer more precision where skis, treads and snow meet down the slope.
        View.nearClipPlane=.35f;View.farClipPlane=250f;
        dances=GetComponent<MountainDances>();
        actorRoot=new GameObject("Selected rider").transform;actorRoot.SetParent(transform,false);
        SetupWeather();SetupTracks();
        var contact=new GameObject("Ground contact");menuShadow=contact.transform;menuShadow.SetParent(actorRoot,false);
        var quad=splash.GetComponent<MeshFilter>().sharedMesh;contact.AddComponent<MeshFilter>().sharedMesh=quad;
        var sr=contact.AddComponent<MeshRenderer>();sr.shadowCastingMode=ShadowCastingMode.Off;sr.receiveShadows=false;
        sr.sharedMaterial=new Material(Resources.Load<Shader>("MountainRaceSnow")){mainTexture=Resources.Load<Texture2D>("Art/Race-contact-shadow")};
        menuShadow.localScale=new Vector3(1.70f,.98f,1);menuShadow.localRotation=Quaternion.Euler(90,0,0);
        menuShadow.localPosition=new Vector3(0,.015f,0);
    }
    GameObject Instance(string key){
        if(instances.TryGetValue(key,out var existing)){existing.SetActive(true);return existing;}
        var prefab=Resources.Load<GameObject>(key);
        if(!prefab)throw new InvalidOperationException("Не загружена модель "+key);
        var instance=Instantiate(prefab,actorRoot);instance.name=key;
        var metadata=Resources.Load<TextAsset>("Art/"+(key=="OwlRide"?"Owl":key)+"Materials");
        if(!metadata)throw new InvalidOperationException("Не загружены материалы "+key);
        var palette=JsonUtility.FromJson<MaterialRows>(metadata.text);
        foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true)){
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++){
                var label=materials[i]?materials[i].name:"";
                var source=Array.Find(palette.entries,m=>label.StartsWith(m.name,StringComparison.OrdinalIgnoreCase));
                if(source==null)source=Array.Find(palette.entries,m=>label.Replace("_"," ").StartsWith(m.name.Replace("_"," "),StringComparison.OrdinalIgnoreCase));
                if(source==null)source=palette.entries[0];
                string id=key+"/"+source.name;
                if(!actorPaint.TryGetValue(id,out var paint)){
                    paint=new Material(Resources.Load<Material>("SlopeLit"));paint.name=source.name;
                    Color color=new Color(source.rgba[0],source.rgba[1],source.rgba[2],source.rgba[3]);
                    paint.color=color.gamma;
                    if(!String.IsNullOrEmpty(source.texture))paint.mainTexture=Resources.Load<Texture2D>(source.texture);
                    paint.SetFloat("_Smoothness",.22f);
                    if(source.name=="Headlamp"){paint.EnableKeyword("_EMISSION");paint.SetColor("_EmissionColor",new Color(1,.83f,.45f)*2);}
                    actorPaint.Add(id,paint);
                }materials[i]=paint;
            }
            renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.On;
            if(renderer is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;
        }
        var animation=instance.GetComponent<Animation>();if(animation)animation.enabled=false;
        var animator=instance.GetComponent<Animator>();if(animator)animator.enabled=false;
        instances[key]=instance;return instance;
    }
    static float MeasureFeet(Transform model){
        float bottom=float.PositiveInfinity;
        foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
            // Gloves and pole tips do not determine where the soles meet the snow.
            var mesh=new Mesh();skin.BakeMesh(mesh);
            // BakeMesh already returns deformed vertices in metres. FBX mesh nodes
            // carry a centimetre scale of 100; applying it here moved the bear below the ground.
            var bakedToModel=model.worldToLocalMatrix*Matrix4x4.TRS(skin.transform.position,skin.transform.rotation,Vector3.one);
            foreach(var vertex in mesh.vertices)bottom=Mathf.Min(bottom,bakedToModel.MultiplyPoint3x4(vertex).y);
            Discard(mesh);
        }
        return float.IsInfinity(bottom)?0:-bottom;
    }
    void PrepareActors(){
        string key=Hero=="bear"?"Bear":"Owl";
        menuYaw=menuVelocity=0;spinHeld=false;
        ClearTracks();
        View.cullingMask=IsMenu?1<<8:~0;menuShadow.gameObject.SetActive(IsMenu);
        foreach(var joint in seatBones.Values)joint.localRotation=menuRest[joint];
        seatBones.Clear();
        if(flakes.isPaused)flakes.Play();
        if(fireworks.isPaused)fireworks.Play();
        foreach(var item in instances.Values)item.SetActive(false);
        if(tube){tube.SetActive(false);Discard(tube);tube=null;}
        if(stowed){stowed.gameObject.SetActive(false);Discard(stowed.gameObject);stowed=null;}
        activeEquipment=null;
        string rigKey=Hero=="owl"?"OwlRide":key;
        rideModel=Instance(rigKey).transform;
        rideClip=Array.Find(Resources.LoadAll<AnimationClip>(rigKey),c=>!c.name.StartsWith("__preview"));
        if(!rideClip)throw new InvalidOperationException("Не загружена анимация "+key);
        menuModel=Instance(key+"Menu").transform;
        menuModel.localPosition=Vector3.zero;menuModel.localRotation=Quaternion.identity;menuModel.localScale=Vector3.one;
        foreach(var joint in menuModel.GetComponentsInChildren<Transform>(true)){
            if(joint.name.EndsWith("_Pole"))joint.gameObject.SetActive(CourseMode!="sled");
            if(CourseMode!="sled")continue;
            string bone=joint.name.Replace("mixamorig:","").Replace("mixamorig_","");
            switch(bone){
            case "LeftUpLeg":case "RightUpLeg":case "LeftLeg":case "RightLeg":case "LeftArm":case "RightArm":
            case "LeftForeArm":case "RightForeArm":case "Head":case "Spine":
                seatBones[bone]=joint;if(!menuRest.ContainsKey(joint))menuRest.Add(joint,joint.localRotation);break;
            }
        }
        if(!menuLifts.ContainsKey(key))menuLifts[key]=MeasureFeet(menuModel);
        menuModel.gameObject.SetActive(IsMenu||CourseMode=="sled");
        rideModel.gameObject.SetActive(!IsMenu&&!BonusMode);
        if(IsMenu){
            PlaceMenuEquipment();
            foreach(var child in actorRoot.GetComponentsInChildren<Transform>(true))child.gameObject.layer=8;
            return;
        }
        if(CourseMode=="sled"){sled=Instance("PlayerSled");activeEquipment="sled";}
        if(CourseMode=="tubing"){
            string[] models={"tube60B","tube60C","tube61A","tube61C"};
            int index=(Hero=="owl"?2:0)+(RaceWorld.Roll(settings.seed,0,75)>=.5f?1:0);
            tube=World.CreateVisual(models[index],actorRoot);tube.transform.localScale=Vector3.one*.84f;activeEquipment="tube";
        }
    }
    void PlaceMenuEquipment(){
        stowed=new GameObject("Equipment in the snow").transform;stowed.SetParent(actorRoot,false);
        if(Hero=="owl"){
            rideClip.SampleAnimation(rideModel.gameObject,2.77f);
            var assembly=new GameObject("Snowboard and bindings").transform;assembly.SetParent(stowed,false);
            foreach(var source in rideModel.GetComponentsInChildren<Transform>(true)){
                if(source.name!="Snowboard"&&source.name!="Left_Binding"&&source.name!="Right_Binding")continue;
                var copy=Instantiate(source.gameObject,assembly).transform;
                copy.localPosition=rideModel.InverseTransformPoint(source.position);
                copy.localRotation=Quaternion.Inverse(rideModel.rotation)*source.rotation;
                var original=source.lossyScale;var baseScale=rideModel.lossyScale;
                copy.localScale=new Vector3(original.x/baseScale.x,original.y/baseScale.y,original.z/baseScale.z);
            }
            // The real deck is long on the authored X axis. Turn its printed upper surface and
            // both bindings towards the menu camera, then plant its tail in the snow.
            assembly.localRotation=Quaternion.Euler(0,-15,-6)*Quaternion.LookRotation(Vector3.right,Vector3.forward);
            var meshes=assembly.GetComponentsInChildren<MeshFilter>();
            var box=ModelBounds(stowed,meshes);assembly.localScale=Vector3.one*(1.46f/box.size.y);
            box=ModelBounds(stowed,meshes);
            assembly.localPosition+=new Vector3(.77f-box.center.x,-box.min.y-.045f,.04f-box.center.z);
            return;
        }
        string[] names={"Left_Ski","Right_Ski"};
        int number=0;
        foreach(var source in rideModel.GetComponentsInChildren<Transform>(true)){
            if(Array.IndexOf(names,source.name)<0)continue;
            var copy=Instantiate(source.gameObject,stowed).transform;
            copy.gameObject.SetActive(true);copy.localPosition=Vector3.zero;copy.localRotation=Quaternion.identity;
            var meshes=copy.GetComponentsInChildren<MeshFilter>();
            if(meshes.Length==0)continue;
            Bounds bounds=ModelBounds(stowed,meshes);var size=bounds.size;
            var axis=size.z>size.x&&size.z>size.y?Vector3.forward:size.x>size.y?Vector3.right:Vector3.up;
            copy.localRotation=Quaternion.AngleAxis(-7,Vector3.forward)*Quaternion.FromToRotation(axis,Vector3.up);
            bounds=ModelBounds(stowed,meshes);
            if(bounds.size.y>.001f)copy.localScale*=1.60f/bounds.size.y;
            bounds=ModelBounds(stowed,meshes);
            copy.localPosition+=new Vector3(.77f+number*.12f-bounds.center.x,-bounds.min.y-.07f,.05f-bounds.center.z);number++;
        }
    }
    static Bounds ModelBounds(Transform space,MeshFilter[] meshes){
        var bounds=new Bounds();bool set=false;
        foreach(var mesh in meshes){
            var b=mesh.sharedMesh.bounds;
            for(int k=0;k<8;k++){
                var corner=b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));
                var point=space.InverseTransformPoint(mesh.transform.TransformPoint(corner));
                if(set)bounds.Encapsulate(point);else{bounds=new Bounds(point,Vector3.zero);set=true;}
            }
        }return bounds;
    }
    void RenderMenu(float dt){
        if(!menuModel)return;
        if(dances&&dances.IsDancing){
            spinHeld=false;menuVelocity=0;menuYaw=Mathf.LerpAngle(menuYaw,0,Mathf.Clamp01(dt*6));
        }else if(!spinHeld&&Mathf.Abs(menuVelocity)>.001f){
            // Match the old .94 damping per 60Hz frame, including at mobile frame rates.
            float damping=Mathf.Pow(.94f,dt*60);
            menuYaw+=menuVelocity*(1-damping)/.06f;menuVelocity*=damping;
        }
        string key=Hero=="bear"?"Bear":"Owl";float scale=Hero=="bear"?1.3f:1.32f;
        Vector3 ground=World.SampleGround(0,0);
        actorRoot.SetPositionAndRotation(ground,Quaternion.Euler(0,World.HeadingAt(0)*Mathf.Rad2Deg,0));
        menuModel.localScale=Vector3.one*scale;menuModel.localPosition=Vector3.up*menuLifts[key]*scale;
        menuModel.localRotation=Quaternion.Euler(0,menuYaw+Mathf.Sin(Time.time*.5f)*3,0);
        View.fieldOfView=58;
        Vector3 eye=ground+actorRoot.rotation*new Vector3(.17f,1.42f,3.62f);
        Vector3 aim=ground+actorRoot.rotation*new Vector3(.17f,.30f,0);
        View.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(aim-eye,Vector3.up));
        UpdateWeather();
    }
    public void MenuSpin(float dx){
        if(!IsMenu||(dances&&dances.IsDancing))return;
        spinHeld=true;menuVelocity=-dx*.012f*Mathf.Rad2Deg;menuYaw+=menuVelocity;
        RenderMenu(0);
    }
    public void EndMenuSpin(){spinHeld=false;}
    void NativeMenuPointer(){
#if !UNITY_WEBGL || UNITY_EDITOR
        float x=Input.mousePosition.x;
        if(Input.GetMouseButtonDown(0)){menuMouseX=x;menuVelocity=0;spinHeld=true;}
        if(Input.GetMouseButton(0)&&spinHeld){MenuSpin(x-menuMouseX);menuMouseX=x;}
        if(Input.GetMouseButtonUp(0))EndMenuSpin();
#endif
    }
    void RenderRace(float dt,bool snap){
        if(!rideModel)return;
        if(IsMenu){RenderMenu(dt);return;}
        float d=Status.metres,alt=Altitude;
        Vector3 ground=World.SampleGround(d,lane);
        Vector3 forward=World.SampleGround(d+.35f,lane)-ground;
        Vector3 right=World.SampleGround(d,lane+.2f)-ground;
        var slopeUp=Vector3.Cross(forward,right).normalized;
        if(slopeUp.y<0)slopeUp=-slopeUp;
        float u=airborne?Mathf.Clamp01(airTime/Mathf.Max(.01f,airMax)):0;
        float sample=PoseTime(u);
        float rotation=fall>0?(1-fall/FallTime):crashTimer>0?Mathf.Min(.7f,(1.5f-crashTimer)/1.4f):0;
        Vector3 flip=Vector3.zero;
        if(rotation>0){float twirl=Mathf.Clamp01(rotation/.4f);flip.x=-360*(1-Mathf.Pow(1-twirl,3));flip.z=fallSide*75*(rotation<.4f?twirl:rotation<.72f?1:(1-rotation)/.28f);}
        else if(trickProgress>=0&&poseStyle<0)flip=TrickEuler(trickType,trickProgress);
        var orientation=Quaternion.LookRotation(forward,slopeUp)*Quaternion.Euler(0,angle*24,-angle*16);
        if(activeEquipment=="tube"){
            ground=World.SnowSurface(d,lane);
            orientation=World.SnowFrame(d,lane)*Quaternion.Euler(0,angle*24,0);
        }
        Vector3 pivot=Vector3.up*.62f;
        actorRoot.SetPositionAndRotation(ground+Vector3.up*alt+orientation*(pivot-Quaternion.Euler(flip)*pivot),orientation*Quaternion.Euler(flip));
        if(activeEquipment=="tube"){
            tube.transform.localRotation=Quaternion.Euler(0,180-Mathf.Atan2(velocity,speed)*Mathf.Rad2Deg*.86f,-angle*2.2f);
            var skin=tube.GetComponent<SkinnedMeshRenderer>();
            if(skin&&skin.sharedMesh.blendShapeCount>=2){skin.SetBlendShapeWeight(0,28+24*Mathf.Abs(angle));skin.SetBlendShapeWeight(1,angle*95);}
        }else if(activeEquipment=="sled"){
            sled.transform.localPosition=Vector3.zero;sled.transform.localRotation=Quaternion.identity;
            menuModel.localPosition=new Vector3(0,.55f,.25f);menuModel.localScale=Vector3.one;
            menuModel.localRotation=Quaternion.Euler(8,180,-angle*9);
            foreach(var entry in seatBones){
                bool left=entry.Key.StartsWith("Left",StringComparison.Ordinal);string name=entry.Key;
                Vector3 bend=name.EndsWith("UpLeg")?new Vector3(-47,0,0):name.EndsWith("Leg")?new Vector3(64,0,0):
                    name.EndsWith("ForeArm")?new Vector3(-34,0,0):name.EndsWith("Arm")?new Vector3(-35+(left?1:-1)*5*angle,0,left?9:-9):
                    name=="Head"?new Vector3(-4,-13*angle,3*angle):new Vector3(6+3*Mathf.Abs(angle),-7*angle,-9*angle);
                entry.Value.localRotation=menuRest[entry.Value]*Quaternion.Euler(bend);
            }
        }else{
            rideClip.SampleAnimation(rideModel.gameObject,sample);
            float scale=Hero=="bear"?1.30f:1.32f;
            rideModel.localScale=Vector3.one*scale;
            float authored=airborne&&sample>=3.25f&&sample<=4.08f?.28f*4*u*(1-u)*scale:0;
            rideModel.localPosition=Vector3.up*((Hero=="bear"?.022f:0)-authored);
            // The Blender FBX import faces +Z on the ski rig; downhill is +Z in Unity.
            rideModel.localRotation=Quaternion.Euler(0,Hero=="bear"?0:-90,0);
        }
        float follow=snap?1:Mathf.Min(1,dt*5);camLane=Mathf.Lerp(camLane,lane,follow);
        Vector3 eye=World.SampleGround(d-6.4f,camLane*.63f);
        float targetY=eye.y+2.65f+(airborne?alt*.48f:rampHeight*.6f);
        camY=Mathf.Lerp(camY,targetY,snap?1:Mathf.Min(1,dt*7));eye.y=camY;
        Vector3 aim=World.SampleGround(d+7.7f,camLane*.72f)+Vector3.up*(1.12f+alt*.48f);
        View.fieldOfView=Mathf.Lerp(View.fieldOfView,boost>0?78:72,snap?1:Mathf.Min(1,dt*3));
        View.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(aim-eye,Vector3.up));
        UpdateWeather();UpdateTracks();
    }
    float PoseTime(float u){
        if(airborne&&trickProgress>=0&&poseStyle>=0)return 6+poseStyle+Mathf.Min(.999f,trickProgress);
        if(airborne)return 3.25f+.83f*u;
        float since=Status.elapsed-lastLanding;
        if(since<.77f)return 4.08f+since;
        if(rampHeight>.025f)return 2.7f+.51f*rampPhase;
        float magnitude=Mathf.Abs(angle);
        if(magnitude<.1f)return 2.7f+(.11f+.059f*Mathf.Sin(Status.elapsed*2.5f)+.024f*Mathf.Sin(Status.elapsed*3.8f))*(1-magnitude/.1f);
        float edge=(magnitude-.10f)/.90f;return angle>0?.8f+.7f*edge:2.7f-.55f*edge;
    }
    static Vector3 TrickEuler(int type,float t){
        float full=t*360,arc=Mathf.Sin(t*Mathf.PI);
        switch(type){case 0:return new Vector3(0,full,0);case 1:return new Vector3(-full,0,0);
            case 2:return new Vector3(full,0,0);case 3:return new Vector3(0,0,full);
            case 4:return new Vector3(arc*54,full*2,0);case 6:return new Vector3(0,full*2,0);
            case 7:return new Vector3(-full,full,arc*23);default:return Vector3.zero;}
    }
}
