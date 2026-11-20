using System;
using UnityEngine;
using UnityEngine.Rendering;

// A deliberately small playable comparison scene, fed by the browser camera fixture.
public partial class SlopePilot : MonoBehaviour {
    enum RideState { Ready, Riding, Finished }
    RideState state;
    readonly float[] gateDistances={223,247,278}, gateLanes={-3.4f,3.4f,-2.8f};
    const float fruitAt=263, finishAt=294;
    const float takeoff=255, rampLength=3.2f, lipHeight=.75f, rampHalfWidth=1.85f;
    float velocity,slip,travelYaw,sensitivity=.79f,dragOrigin,dragTarget,flightTime,rideTime,trickTime=-1;
    bool dragging;
    int nextGate,gates;
    bool fruit;
    float feedbackUntil;
    string feedback;
    GameObject mandarin;
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void SlalomHUD(int phase,int score,int metres,int caught,string hint);
    int lastPhase=-1,lastMetres=-1;
    void LateUpdate(){
        int phase=paused?3:(int)state,metres=Mathf.CeilToInt(finishAt-distance);
        if(phase==lastPhase&&metres==lastMetres)return;
        lastPhase=phase;lastMetres=metres;
        SlalomHUD(phase,gates,metres,fruit?1:0,state==RideState.Riding?Target():"");
    }
#endif
    public bool Finished=>state==RideState.Finished;
    public int GatesPassed=>gates;
    public bool CaughtFruit=>fruit;
    [Serializable] public class Fixture { public float fov, heading; public float[] eye, direction, up, actor; public Vector3[] points, props; }
    Fixture f; Camera cam; GameObject owl; AnimationClip clip; Vector3 baseActor; float distance=200, lane, steer, jump=-1, landed=10; bool paused; public Camera View=>cam; public float Distance=>distance; public float Lane=>lane; public bool Airborne=>jump>=0;
    static Vector3 Flip(float[] a)=>new Vector3(a[0],a[1],-a[2]);
    // All geometry uses the same lateral samples and 2 m longitudinal spacing as courseGround.
    Vector3 Ground(float d,float lateral){
        float row=(d-185)/2, magnitude=Mathf.Abs(lateral);
        float col=3+Mathf.Sign(lateral)*(magnitude<=9?magnitude/4.5f:2+(magnitude-9)/5);
        int i=Mathf.Clamp(Mathf.FloorToInt(row),0,59),j=Mathf.Clamp(Mathf.FloorToInt(col),0,5);
        return Vector3.LerpUnclamped(Vector3.LerpUnclamped(f.points[i*7+j],f.points[i*7+j+1],col-j),
            Vector3.LerpUnclamped(f.points[(i+1)*7+j],f.points[(i+1)*7+j+1],col-j),row-i);
    }
    Material Surface(Color color){var source=Resources.Load<Material>("SlopeLit");var mat=source?new Material(source):new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=color;return mat;}
    void Paint(GameObject model,string key){Texture color=Resources.Load<Texture2D>("Art/"+key);if(!color)throw new Exception("No albedo for "+key);foreach(var r in model.GetComponentsInChildren<Renderer>()){var ms=r.sharedMaterials;for(int i=0;i<ms.Length;i++){string n=ms[i].name.ToLowerInvariant();var m=Surface(Color.white);if(n.Contains("rubber"))m.color=new Color(.12f,.14f,.16f);else if(n.Contains("board"))m.color=new Color(.09f,.30f,.8f);else m.mainTexture=color;m.SetFloat("_Smoothness",.17f);ms[i]=m;}r.sharedMaterials=ms;}}
    GameObject Model(string key,Vector3 position,float size,bool longest){var parent=new GameObject(key); parent.transform.SetParent(transform);parent.transform.position=position;var mesh=Instantiate(Resources.Load<GameObject>(key),parent.transform); mesh.transform.localRotation=Quaternion.Euler(0,key=="Rock"?90:0,0)*mesh.transform.localRotation;Paint(mesh,key);var rs=mesh.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs){b.Encapsulate(r.bounds);r.shadowCastingMode=ShadowCastingMode.On;}float factor=size/(longest?Mathf.Max(b.size.x,Mathf.Max(b.size.y,b.size.z)):b.size.y);mesh.transform.position+=new Vector3(position.x-b.center.x,position.y-b.min.y,position.z-b.center.z);parent.transform.localScale=Vector3.one*factor;return mesh;}
    public void Setup(){if(cam)return; f=JsonUtility.FromJson<Fixture>(Resources.Load<TextAsset>("Slope").text);
        BuildLandscape();
        Model("Rock",f.props[1],1.2f,true);Model("Snowman",Ground(226,-7.6f),2.5f,false);
        baseActor=Flip(f.actor);owl=Instantiate(Resources.Load<GameObject>("Owl"),transform);Paint(owl,"Owl");owl.transform.localScale=Vector3.one*1.32f;foreach(var c in Resources.LoadAll<AnimationClip>("Owl"))if(!c.name.StartsWith("__preview")){clip=c;break;}if(!clip)throw new Exception("Owl FBX has no animation");
        cam=new GameObject("Game camera").AddComponent<Camera>();cam.transform.SetParent(transform);cam.fieldOfView=f.fov;cam.nearClipPlane=.05f;cam.farClipPlane=400;cam.backgroundColor=new Color(.43f,.69f,.86f);cam.clearFlags=CameraClearFlags.SolidColor;cam.transform.SetPositionAndRotation(Flip(f.eye),Quaternion.LookRotation(Flip(f.direction),Flip(f.up)));
        SetDaylight();BuildCourse();Pose(0);}
    GameObject Marker(string label,PrimitiveType type,float d,float x,float height,Vector3 size,Material material){
        var obj=GameObject.CreatePrimitive(type);obj.name=label;obj.transform.SetParent(transform);
        obj.transform.position=Ground(d,x)+Vector3.up*height;
        obj.transform.rotation=Quaternion.LookRotation(Ground(d+.2f,x)-Ground(d,x),Vector3.up);
        obj.transform.localScale=size;obj.GetComponent<Renderer>().sharedMaterial=material;
        return obj;
    }
    void BuildCourse(){
        var blue=Surface(new Color(.04f,.32f,.64f));var red=Surface(new Color(.89f,.12f,.06f));
        var gold=Surface(new Color(1f,.56f,.035f));var ink=Surface(new Color(.08f,.16f,.23f));var white=Surface(Color.white);
        for(float d=204;d<finishAt;d+=4)foreach(int side in new[]{-1,1})
            Marker("Piste edge",PrimitiveType.Cube,d,side*8.4f,.06f,new Vector3(.16f,.07f,1.5f),blue);
        for(int i=0;i<gateDistances.Length;i++){
            float d=gateDistances[i],laneX=gateLanes[i];var color=i%2==0?blue:red;
            foreach(int side in new[]{-1,1}){
                float x=laneX+side*2;
                Marker("Gate pole",PrimitiveType.Cylinder,d,x,1.15f,new Vector3(.12f,1.15f,.12f),color);
                Marker("Gate flag",PrimitiveType.Cube,d,x+side*.48f,1.89f,new Vector3(.99f,.65f,.08f),color);
                Marker("Flag stripe",PrimitiveType.Cube,d-.055f,x+side*.48f,2.03f,new Vector3(.97f,.095f,.055f),white);
            }
        }
        BuildRamp(gold);
        mandarin=Model("Mandarin",Ground(fruitAt,0)+Vector3.up*2.96f,.5f,false).transform.parent.gameObject;
        for(int i=0;i<16;i++)Marker("Finish stripe",PrimitiveType.Cube,finishAt,i-7.5f,.11f,new Vector3(1.01f,.12f,.8f),i%2==0?ink:white);
        foreach(int side in new[]{-1,1})Marker("Finish post",PrimitiveType.Cylinder,finishAt,side*8.2f,1.25f,new Vector3(.19f,1.25f,.19f),ink);
    }
    void Start(){
        Setup();
#if UNITY_STANDALONE
        if(Array.IndexOf(Environment.GetCommandLineArgs(), "--owl-smoke")>=0) StartCoroutine(CheckBuiltPlayer());
#endif
        Debug.Log("OWL SLOPE READY");
    }
    System.Collections.IEnumerator CheckBuiltPlayer(){yield return null;ResetRide();Transform hip=null;foreach(var t in owl.GetComponentsInChildren<Transform>())if(t.name.EndsWith("Hips")){hip=t;break;}
        if(!hip){Debug.LogError("OWL PLAYER: missing animated hips");Application.Quit(2);yield break;}var q=hip.localRotation;var p=hip.localPosition;Tick(.05f,4);
        bool moved=Quaternion.Angle(q,hip.localRotation)>.1f||(p-hip.localPosition).magnitude>.0001f;bool air=false;
        while(!Finished){Tick(1f/60,Distance<232?4:0);air|=Airborne;}
        bool ok=moved&&air&&!Airborne&&CaughtFruit;ResetRide();if(ok)Debug.Log("OWL PLAYER PASS: bones, steering, jump, landing, reset");else Debug.LogError("OWL PLAYER FAIL: bones="+moved+", jump="+air);Application.Quit(ok?0:3);}

    void Pose(float dt){Vector3 mid=Ground(distance,0),now=Ground(distance,lane);Vector3 delta=now-Ground(200,0), forward=Ground(distance+.4f,lane)-now;
        float u=Airborne?jump/flightTime:0, ramp=RampAt(distance,lane),edge=Mathf.Clamp01((Mathf.Abs(steer)-.1f)/.9f);
        float sample=trickTime>=0?6+trickTime/.48f:Airborne?3.25f+.83f*u:landed<.77f?4.08f+landed:ramp>0?2.7f+.51f*ramp/lipHeight:
            Mathf.Abs(steer)<.1f?2.79f+.06f*Mathf.Sin(rideTime*2.5f):steer>0?.8f+.7f*edge:2.7f-.55f*edge;
        float authored=sample>3.25f&&sample<4.08f?.28f*4*u*(1-u)*1.32f:0;
        float lift=Airborne?lipHeight*(1-u)+2.3f*Mathf.Sin(Mathf.PI*u)-authored:ramp;
        clip.SampleAnimation(owl,sample);owl.transform.localScale=Vector3.one*1.32f;owl.transform.position=baseActor+delta+Vector3.up*lift;
        owl.transform.rotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.Euler(0,steer*24,-steer*16)*Quaternion.Euler(0,90,0);
        var bend=Quaternion.AngleAxis((Heading(distance)-Heading(200))*Mathf.Rad2Deg,Vector3.up);
        cam.transform.position=mid+bend*(Flip(f.eye)-Ground(200,0))+(now-mid)*.6f;
        cam.transform.rotation=bend*Quaternion.LookRotation(Flip(f.direction),Flip(f.up));}
    public void ResetRide(){distance=200;lane=steer=velocity=slip=rideTime=0;jump=trickTime=-1;landed=10;dragging=paused=false;travelYaw=Heading(distance);nextGate=gates=0;fruit=false;feedback=null;mandarin.SetActive(true);state=RideState.Riding;Pose(0);}
    public void BeginRide(){ResetRide();}
    public void PauseRide(){if(state==RideState.Riding){paused=!paused;dragging=false;}}
    public void SetSensitivity(float value){sensitivity=.35f+.11f*Mathf.Clamp(value,1,10);}
    public void BeginSteer(){if(state==RideState.Riding&&!paused){dragging=true;dragOrigin=dragTarget=lane;}}
    public void DragSteer(float fraction){if(dragging)dragTarget=Mathf.Clamp(dragOrigin+fraction*400*.05f*1.35f*sensitivity,-9.5f,9.5f);}
    public void ReleaseSteer(){dragging=false;}
    public void AirTap(){if(state==RideState.Riding&&!paused&&Airborne&&trickTime<0&&flightTime-jump>.48f){trickTime=0;Notice("Грэб!");}}
#if !UNITY_WEBGL || UNITY_EDITOR
    Vector2 pointerStart;
    void MouseSteering(){
        if(Input.GetMouseButtonDown(0)){pointerStart=Input.mousePosition;BeginSteer();}
        if(Input.GetMouseButton(0))DragSteer((Input.mousePosition.x-pointerStart.x)/Screen.width);
        if(Input.GetMouseButtonUp(0)){if(Vector2.Distance(Input.mousePosition,pointerStart)<20)AirTap();ReleaseSteer();}
    }
#endif
    void Update(){
        if(!cam||state!=RideState.Riding)return;
        if(Input.GetKeyDown(KeyCode.P))PauseRide();if(paused)return;
        float input=(Input.GetKey(KeyCode.RightArrow)||Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.LeftArrow)||Input.GetKey(KeyCode.A)?1:0);
        if(Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.UpArrow))AirTap();
#if !UNITY_WEBGL || UNITY_EDITOR
        MouseSteering();
#endif
        AdvanceInput(Mathf.Min(Time.deltaTime,.12f),input);
    }
    public void AdvanceInput(float elapsed,float keyboard){
        // Substep the same dynamics at low frame rates, then sample the rig only once.
        float frameTime=elapsed;
        while(elapsed>.00001f&&!Finished){
            float? target=dragging?dragTarget:(float?)null;
            if(keyboard!=0)target=lane+keyboard*3.5f;
            float dt=Mathf.Min(elapsed,1f/60);StepRide(dt,target);elapsed-=dt;
        }
        Pose(frameTime);
    }
    void Notice(string message){feedback=message;feedbackUntil=distance+7;}
    float Heading(float d){Vector3 tangent=Ground(d+1,0)-Ground(d-1,0);return Mathf.Atan2(tangent.x,tangent.z);}
    float RampAt(float d,float x){return d>takeoff||Mathf.Abs(x)>rampHalfWidth?0:lipHeight*Mathf.Clamp01(1+(d-takeoff)/rampLength);}
    // Distances and lanes are in metres. The browser uses 20 game units per metre.
    public void Tick(float dt,float? target){StepRide(dt,target);Pose(dt);}
    void StepRide(float dt,float? target){
        float grade=.29f+.22f*Mathf.Sin(distance/42)+.09f*Mathf.Sin(distance/17+1);
        float speed=(230+distance*.085f)*(.8f+.7f*grade)*1.1f/20, heading=Heading(distance);
        if(target.HasValue){
            float limit=21.5f*Mathf.Sqrt(sensitivity);
            float want=Mathf.Clamp((target.Value-lane)*(3+5*sensitivity),-limit,limit)*1.15f*(Airborne?.45f:1);
            velocity=Mathf.Lerp(velocity,want,dt*(5+6*sensitivity));travelYaw=heading+Mathf.Atan2(velocity,speed);
            float curvature=(Heading(distance+1)-Heading(distance-1))/(Ground(distance+1,0)-Ground(distance-1,0)).magnitude;
            float bank=(Ground(distance,lane+.1f).y-Ground(distance,lane-.1f).y)/.2f;
            slip=Airborne?0:Mathf.Lerp(slip,Mathf.Clamp(( -curvature*speed*speed-9.81f*bank)*.24f,-1.8f,1.8f),4*dt);
        }else{velocity=0;slip=Mathf.Tan(Mathf.Clamp(travelYaw-heading,-.7f,.7f))*speed;}
        steer=Mathf.Lerp(steer,Mathf.Clamp((velocity+slip*.5f)/21.5f,-1,1),dt*10);
        lane=Mathf.Clamp(lane+(velocity+slip)*dt,-9.5f,9.5f);
        float before=distance;distance=Mathf.Min(distance+speed*dt,finishAt);rideTime+=dt;
        if(Airborne){jump+=dt;if(trickTime>=0){trickTime+=dt;if(trickTime>=.48f)trickTime=-1;}
            if(jump>=flightTime){jump=trickTime=-1;landed=0;}}
        else if(before<takeoff&&distance>=takeoff&&Mathf.Abs(lane)<=rampHalfWidth){
            flightTime=1.45f*(1+(speed*20-245)/1400);jump=(distance-takeoff)/speed;
        }else landed+=dt;
        if(nextGate<gateDistances.Length&&distance>=gateDistances[nextGate]){
            bool passed=Mathf.Abs(lane-gateLanes[nextGate])<=2;
            if(passed)gates++;nextGate++;
            Notice((passed?"Ворота пройдены":"Мимо ворот")+(nextGate==2?" · трамплин по центру":""));
        }
        if(before<fruitAt&&distance>=fruitAt){
            fruit=Mathf.Abs(lane)<=1.8f&&Airborne&&jump/flightTime>.18f&&jump/flightTime<.85f;
            if(fruit)mandarin.SetActive(false);Notice(fruit?"Мандарин пойман!":"Мандарин пропущен");
        }
        if(distance>=finishAt)state=RideState.Finished;
    }
    string Target(){
        if(feedback!=null&&distance<feedbackUntil)return feedback;
        if(Airborne&&distance<fruitAt)return "В полёте можно тапнуть для грэба";
        if(nextGate==2&&distance<fruitAt)return "Трамплин по центру";
        if(nextGate<gateDistances.Length)return gateLanes[nextGate]<0?"Ворота слева":"Ворота справа";
        return "К финишной полосе";
    }
#if !UNITY_WEBGL || UNITY_EDITOR
    void OnGUI(){
        // Size HUD and hit targets in a portrait-friendly logical coordinate system.
        float scale=Mathf.Clamp(Screen.width/375f, .8f, 2.5f);
        GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        float w=Screen.width/scale,h=Screen.height/scale;
        var title=new GUIStyle(GUI.skin.box){fontSize=18,alignment=TextAnchor.MiddleCenter};
        var button=new GUIStyle(GUI.skin.button){fontSize=18};
        if(state==RideState.Riding&&!paused){
            GUI.Box(new Rect((w-235)/2,10,235,56),gates+" / 3 ворот    ·    "+Mathf.RoundToInt(finishAt-distance)+" м\n"+Target(),title);
            if(GUI.Button(new Rect(12,h-62,82,46),"Пауза",button))PauseRide();
            return;
        }
        float width=Mathf.Min(340,w-24),left=(w-width)/2,top=(h-190)/2;
        string label=state==RideState.Ready?"Пройди 3 ворот\nи поймай мандарин в прыжке":paused?"Пауза":"Финиш  ·  Ворота "+gates+" из 3\nМандарин "+(fruit?"пойман":"пропущен");
        GUI.Box(new Rect(left,top,width,190),GUIContent.none);
        GUI.Label(new Rect(left+5,top+7,width-10,64),label,title);
        var hint=new GUIStyle(GUI.skin.label){fontSize=15,alignment=TextAnchor.MiddleCenter};
        GUI.Label(new Rect(left+5,top+72,width-10,51),"Веди пальцем влево или вправо.\nДля прыжка заезжай на трамплин.",hint);
        if(GUI.Button(new Rect(left+20,top+131,width-40,46),state==RideState.Ready?"Поехали":paused?"Продолжить":"Ещё заезд",button)){
            if(paused)PauseRide();else BeginRide();
        }
    }
#endif
}
