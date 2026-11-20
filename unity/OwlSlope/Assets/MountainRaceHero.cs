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
    sealed class SeatSide {
        public Transform shoulder,elbow,wrist,palm,hip,knee,ankle,toe;
        public float side;
        public SeatSide(Dictionary<string,Transform> bones,string prefix){
            shoulder=bones[prefix+"Arm"];elbow=bones[prefix+"ForeArm"];wrist=bones[prefix+"Hand"];
            if(!bones.TryGetValue(prefix+"HandMiddle1",out palm))palm=bones[prefix+"HandIndex1"];
            hip=bones[prefix+"UpLeg"];knee=bones[prefix+"Leg"];ankle=bones[prefix+"Foot"];toe=bones[prefix+"ToeBase"];
        }
    }
    SeatSide[] seatedLimbs;
    Quaternion seatFacing;
    Transform actorRoot,menuModel,rideModel,stowed,menuShadow;
    AnimationClip rideClip;
    readonly Dictionary<Transform,PoseBuffer> poseBuffers=new Dictionary<Transform,PoseBuffer>();
    PoseBuffer ridePose;
    // Sample the synchronized tracks for the skin and its rigid equipment together.
    // Cache only animated joints and equipment roots, with no per-frame allocations.
    sealed class PoseBuffer {
        readonly Transform[] joints;
        readonly Vector3[] positions;
        readonly Quaternion[] rotations;
        public PoseBuffer(Transform root){
            var moving=new List<Transform>();
            foreach(var part in root.GetComponentsInChildren<Transform>(true)){
                string name=part.name;
                if(name.StartsWith("mixamorig",StringComparison.OrdinalIgnoreCase)||name=="Snowboard"||
                   name.EndsWith("_Ski",StringComparison.Ordinal)||name.EndsWith("_Binding",StringComparison.Ordinal))moving.Add(part);
            }
            joints=moving.ToArray();positions=new Vector3[joints.Length];rotations=new Quaternion[joints.Length];
        }
        public void Sample(AnimationClip clip,GameObject model,float a,float b,float mix){
            if(mix<=.001f||mix>=.999f){clip.SampleAnimation(model,mix<.5f?a:b);return;}
            clip.SampleAnimation(model,a);
            for(int i=0;i<joints.Length;i++){positions[i]=joints[i].localPosition;rotations[i]=joints[i].localRotation;}
            clip.SampleAnimation(model,b);
            for(int i=0;i<joints.Length;i++){
                joints[i].SetLocalPositionAndRotation(Vector3.LerpUnclamped(positions[i],joints[i].localPosition,mix),
                    Quaternion.SlerpUnclamped(rotations[i],joints[i].localRotation,mix));
            }
        }
    }
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
        if(sled)sled.SetActive(false);
        if(tube){tube.SetActive(false);Discard(tube);tube=null;}
        if(stowed){stowed.gameObject.SetActive(false);Discard(stowed.gameObject);stowed=null;}
        activeEquipment=null;
        string rigKey=Hero=="owl"?"OwlRide":key;
        rideModel=Instance(rigKey).transform;
        if(!poseBuffers.TryGetValue(rideModel,out ridePose))poseBuffers.Add(rideModel,ridePose=new PoseBuffer(rideModel));
        rideClip=Array.Find(Resources.LoadAll<AnimationClip>(rigKey),c=>!c.name.StartsWith("__preview"));
        if(!rideClip)throw new InvalidOperationException("Не загружена анимация "+key);
        menuModel=Instance(key+"Menu").transform;
        menuModel.localPosition=Vector3.zero;menuModel.localRotation=Quaternion.identity;menuModel.localScale=Vector3.one;
        foreach(var joint in menuModel.GetComponentsInChildren<Transform>(true)){
            if(joint.name.EndsWith("_Pole"))joint.gameObject.SetActive(!BonusMode);
            if(!BonusMode)continue;
            string bone=joint.name;
            if(bone.StartsWith("mixamorig",StringComparison.Ordinal))bone=bone.Substring(9).TrimStart(':','_');
            switch(bone){
            case "Hips":case "Head":case "LeftUpLeg":case "RightUpLeg":case "LeftLeg":case "RightLeg":
            case "LeftFoot":case "RightFoot":case "LeftToeBase":case "RightToeBase":
            case "LeftArm":case "RightArm":case "LeftForeArm":case "RightForeArm":
            case "LeftHand":case "RightHand":case "LeftHandMiddle1":case "RightHandMiddle1":
            case "LeftHandIndex1":case "RightHandIndex1":
                seatBones[bone]=joint;if(!menuRest.ContainsKey(joint))menuRest.Add(joint,joint.localRotation);break;
            }
        }
        if(!menuLifts.ContainsKey(key))menuLifts[key]=MeasureFeet(menuModel);
        menuModel.gameObject.SetActive(IsMenu||BonusMode);
        rideModel.gameObject.SetActive(!IsMenu&&!BonusMode);
        PaintKit(rideModel);
        if(IsMenu){
            PlaceMenuEquipment();
            PaintKit(stowed);
            foreach(var child in actorRoot.GetComponentsInChildren<Transform>(true))child.gameObject.layer=8;
            return;
        }
        if(BonusMode){
            seatedLimbs=new[]{new SeatSide(seatBones,"Left"),new SeatSide(seatBones,"Right")};
            var left=seatedLimbs[0];var right=seatedLimbs[1];
            var toeDirection=actorRoot.InverseTransformDirection(left.toe.position-left.ankle.position+right.toe.position-right.ankle.position);
            seatFacing=Quaternion.Euler(0,-Mathf.Atan2(toeDirection.x,toeDirection.z)*Mathf.Rad2Deg,0);
            foreach(var limb in seatedLimbs)
                limb.side=Mathf.Sign((seatFacing*actorRoot.InverseTransformPoint(limb.shoulder.position)).x);
        }
        if(CourseMode=="sled"){
            if(!sled)sled=World.CreateVisual("playerSled",actorRoot);
            sled.SetActive(true);activeEquipment="sled";
        }
        if(CourseMode=="tubing"){
            string[] models={"playerTube60B","playerTube60C","playerTube61A","playerTube61C"};
            int index=(Hero=="owl"?2:0)+(RaceWorld.Roll(settings.seed,0,75)>=.5f?1:0);
            tube=World.CreateVisual(models[index],actorRoot);tube.transform.localScale=Vector3.one*.84f;activeEquipment="tube";
            tube.transform.localRotation=Quaternion.Euler(0,180,0);
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
        float rotation=fall>0?(1-fall/FallTime):crashTimer>0?Mathf.Min(.7f,(1.5f-crashTimer)/1.4f):0;
        Vector3 flip=Vector3.zero;
        if(rotation>0){float twirl=Mathf.Clamp01(rotation/.4f);flip.x=-360*(1-Mathf.Pow(1-twirl,3));flip.z=fallSide*75*(rotation<.4f?twirl:rotation<.72f?1:(1-rotation)/.28f);}
        else if(trickProgress>=0&&trickType!=5)flip=TrickEuler(trickType,trickProgress);
        else if(trickProgress>=0){
            // The toe/heel reach and ski shifty are deliberate downward gestures.
            // Turn the whole rig a little across the fall line to show the edge.
            float reach=StepBlend(trickProgress/.36f)*StepBlend((1-trickProgress)/.31f);
            float side=poseStyle==0?1:-1;
            flip=new Vector3(-3*reach,side*24*reach,-side*7*reach);
        }
        var orientation=Quaternion.LookRotation(forward,slopeUp)*Quaternion.Euler(0,angle*24,-angle*16);
        if(activeEquipment=="tube"){
            ground=World.SnowSurface(d,lane);
            orientation=World.SnowFrame(d,lane)*Quaternion.Euler(0,angle*24-Mathf.Atan2(velocity,speed)*Mathf.Rad2Deg*.86f,-angle*2.2f);
        }
        Vector3 pivot=Vector3.up*.62f;
        actorRoot.SetPositionAndRotation(ground+Vector3.up*alt+orientation*(pivot-Quaternion.Euler(flip)*pivot),orientation*Quaternion.Euler(flip));
        if(BonusMode){
            bool inflatable=activeEquipment=="tube";
            float compress=inflatable?Mathf.Max(airborne?.020f*Mathf.Sin(u*Mathf.PI):0,
                .026f*(1-Mathf.Clamp01((Status.elapsed-lastLanding)/.30f))):0;
            foreach(var bone in seatBones.Values)bone.localRotation=menuRest[bone];
            menuModel.localScale=Vector3.one*1.82f;
            menuModel.localRotation=Quaternion.Euler(inflatable?-6+Mathf.Abs(angle)*4+compress*80:19+Mathf.Abs(angle)*3,0,-angle*(inflatable?12:7))*seatFacing;
            var hips=seatBones["Hips"];
            var seat=inflatable?new Vector3(-angle*.028f,.355f-compress,-.042f):new Vector3(0,.92f,-.46f);
            menuModel.position+=actorRoot.TransformPoint(seat)-hips.position;
            foreach(var limb in seatedLimbs){
                float s=limb.side;
                var grip=actorRoot.TransformPoint(inflatable?TubeGrip(s):new Vector3(s*.355f,1.13f,-.060f));
                float palmLength=Vector3.Distance(limb.wrist.position,limb.palm.position);
                // The owl's right wing is weighted almost entirely to the wrist; its index root lies only 2 mm away.
                var wrist=grip+actorRoot.TransformDirection(new Vector3(0,.31f,-.951f))*palmLength;
                AimLimb(limb.shoulder,limb.elbow,limb.wrist,wrist,actorRoot.TransformDirection(new Vector3(s*.42f,-.55f,-.75f)));
                if(palmLength>.01f)AimChild(limb.wrist,limb.palm,grip);
                var ankle=actorRoot.TransformPoint(inflatable?new Vector3(s*.22f,.47f,.19f):new Vector3(s*.335f,.665f,-.42f));
                AimLimb(limb.hip,limb.knee,limb.ankle,ankle,actorRoot.TransformDirection(new Vector3(s*.22f,.16f,1)));
                var bootTip=actorRoot.TransformPoint(inflatable?new Vector3(s*.24f,.42f,.46f):new Vector3(s*.384f,.514f,-.19f));
                AimChild(limb.ankle,limb.toe,bootTip);
            }
        }else if(patrol==PatrolPhase.Walking){
            PoseFisWalker();
        }else{
            float liftedByClip=SampleRidePose(u);
            float scale=Hero=="bear"?1.30f:1.32f;
            rideModel.localScale=Vector3.one*scale;
            rideModel.localPosition=Vector3.up*((Hero=="bear"?.022f:0)-liftedByClip*scale);
            // The board is authored across local X with the owl looking towards -X.
            // Bring its leading boot and gaze onto the course's downhill +Z axis together.
            rideModel.localRotation=Quaternion.Euler(0,Hero=="bear"?0:90,0);
        }
        float follow=snap?1:Mathf.Min(1,dt*5);camLane=Mathf.Lerp(camLane,lane,follow);
        Vector3 eye=World.SampleGround(d-6.4f,camLane*.63f);
        float targetY=eye.y+2.65f+alt*.48f;
        camY=Mathf.Lerp(camY,targetY,snap?1:Mathf.Min(1,dt*7));eye.y=camY;
        Vector3 aim=World.SampleGround(d+7.7f,camLane*.72f)+Vector3.up*(1.12f+alt*.48f);
        View.fieldOfView=Mathf.Lerp(View.fieldOfView,boost>0?78:72,snap?1:Mathf.Min(1,dt*3));
        if(patrol==PatrolPhase.Looking){
            float glance=Mathf.Sin(Mathf.PI*Mathf.Clamp01((Status.elapsed-lookSince)/1.05f));
            aim=Vector3.Lerp(aim,World.SampleGround(d-10,lane*.5f)+Vector3.up,glance);
            eye+=World.SnowFrame(d,lane)*Vector3.right*(glance*1.25f);
        }
        View.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(aim-eye,Vector3.up));
        UpdateWeather();UpdateTracks();
    }
    // The original tube faces -Z; flip its asymmetric webbing positions with the rubber.
    static Vector3 TubeGrip(float s)=>new Vector3(s*.58f,.65f,s>0?-.08f:-.17f)*.84f;
    static void AimChild(Transform joint,Transform child,Vector3 target){
        joint.rotation=Quaternion.FromToRotation(child.position-joint.position,target-joint.position)*joint.rotation;
    }
    // Aim two rigid bone segments with their original lengths and explicit elbow/knee bend directions.
    static void AimLimb(Transform joint,Transform bend,Transform end,Vector3 target,Vector3 hint){
        float a=Vector3.Distance(joint.position,bend.position),b=Vector3.Distance(bend.position,end.position);
        var delta=target-joint.position;var direction=delta.normalized;
        float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.0001f,a+b-.0001f);
        float projection=(a*a-b*b+distance*distance)/(2*distance);
        var normal=Vector3.ProjectOnPlane(hint,direction).normalized;
        var pivot=joint.position+direction*projection+normal*Mathf.Sqrt(Mathf.Max(0,a*a-projection*projection));
        AimChild(joint,bend,pivot);
        AimChild(bend,end,target);
    }
    float SampleRidePose(float u){
        // Feet and equipment in the original jump study rise together. Remove its
        // baked vertical travel because the game supplies the actual ballistic path.
        if(airborne){
            float flight=3.25f+.83f*u,mix=0,trick=flight;
            if(trickProgress>=0&&poseStyle>=0){
                mix=StepBlend(trickProgress/.20f)*StepBlend((1-trickProgress)/.19f);
                trick=6+poseStyle+Mathf.Min(.966f,trickProgress);
            }
            ridePose.Sample(rideClip,rideModel.gameObject,flight,trick,mix);
            return .28f*4*u*(1-u)*(1-mix);
        }
        // Three 61-frame loops at 30 fps include the duplicate closing frame.
        const float span=61f/30;
        float glide=8+2*Mathf.Repeat(Status.elapsed*.28f+Status.metres*.011f,1);
        float edge=glide+(angle>0?span:2*span);
        float load=StepBlend(Mathf.Min(1,Mathf.Abs(angle)*1.15f));
        float since=Status.elapsed-lastLanding;
        if(since<.77f)
            ridePose.Sample(rideClip,rideModel.gameObject,4.08f+since,load>.5f?edge:glide,StepBlend((since-.48f)/.29f));
        else if(rampHeight>.005f)
            ridePose.Sample(rideClip,rideModel.gameObject,load>.5f?edge:glide,2.7f+.55f*rampPhase,StepBlend(rampPhase/.3f));
        else ridePose.Sample(rideClip,rideModel.gameObject,glide,edge,load);
        return 0;
    }
    static Vector3 TrickEuler(int type,float t){
        // Build and shed angular velocity progressively as the rider tucks and opens.
        float full=360*(t-.78f*Mathf.Sin(2*Mathf.PI*t)/(2*Mathf.PI)),arc=Mathf.Sin(t*Mathf.PI);
        switch(type){case 0:return new Vector3(-7*arc,full,10*arc);case 1:return new Vector3(-full,0,0);
            case 2:return new Vector3(full,0,0);case 3:return new Vector3(0,0,full);
            case 4:return new Vector3(arc*54,full*2,0);case 6:return new Vector3(0,full*2,0);
            case 7:return new Vector3(-full,full,arc*23);default:return Vector3.zero;}
    }
}
