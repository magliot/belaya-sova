using System;
using UnityEngine;
using UnityEngine.Rendering;

// A deliberately small playable comparison scene, fed by the browser camera fixture.
public class SlopePilot : MonoBehaviour {
    enum RideState { Ready, Riding, Finished }
    RideState state;
    readonly float[] gateDistances={223,247,278}, gateLanes={-3.4f,3.4f,-2.8f};
    const float fruitAt=263, finishAt=294;
    const float takeoff=fruitAt-2.8f;
    int nextGate,gates;
    bool fruit;
    float feedbackUntil;
    string feedback;
    GameObject mandarin;
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void SlalomHUD(int phase,int score,int metres,int caught,string hint);
    int lastPhase=-1,lastMetres=-1;
    bool jumpRequested;
    public void JumpFromButton(){if(state==RideState.Riding&&!paused)jumpRequested=true;}
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
    Vector3 Ground(float d,float lateral){float row=Mathf.Clamp((d-185)/2,0,59.999f), col=Mathf.Clamp(3+lateral/4.5f,1,5); int i=(int)row,j=(int)col;return Vector3.Lerp(Vector3.Lerp(f.points[i*7+j],f.points[i*7+j+1],col-j),Vector3.Lerp(f.points[(i+1)*7+j],f.points[(i+1)*7+j+1],col-j),row-i);}
    Material Surface(Color color){var source=Resources.Load<Material>("SlopeLit");var mat=source?new Material(source):new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=color;return mat;}
    void Paint(GameObject model,string key){Texture color=Resources.Load<Texture2D>("Art/"+key);if(!color)throw new Exception("No albedo for "+key);foreach(var r in model.GetComponentsInChildren<Renderer>()){var ms=r.sharedMaterials;for(int i=0;i<ms.Length;i++){string n=ms[i].name.ToLowerInvariant();var m=Surface(Color.white);if(n.Contains("rubber"))m.color=new Color(.12f,.14f,.16f);else if(n.Contains("board"))m.color=new Color(.09f,.30f,.8f);else m.mainTexture=color;m.SetFloat("_Smoothness",.17f);ms[i]=m;}r.sharedMaterials=ms;}}
    GameObject Model(string key,Vector3 position,float size,bool longest){var parent=new GameObject(key); parent.transform.SetParent(transform);parent.transform.position=position;var mesh=Instantiate(Resources.Load<GameObject>(key),parent.transform); mesh.transform.localRotation=Quaternion.Euler(0,key=="Rock"?90:0,0)*mesh.transform.localRotation;Paint(mesh,key);var rs=mesh.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs){b.Encapsulate(r.bounds);r.shadowCastingMode=ShadowCastingMode.On;}float factor=size/(longest?Mathf.Max(b.size.x,Mathf.Max(b.size.y,b.size.z)):b.size.y);mesh.transform.position+=new Vector3(position.x-b.center.x,position.y-b.min.y,position.z-b.center.z);parent.transform.localScale=Vector3.one*factor;return mesh;}
    public void Setup(){if(cam)return; f=JsonUtility.FromJson<Fixture>(Resources.Load<TextAsset>("Slope").text);var snow=new GameObject("200 m • snow");snow.transform.SetParent(transform);var geom=new Mesh();var tri=new int[60*6*6];int at=0;for(int i=0;i<60;i++)for(int j=0;j<6;j++){int a=i*7+j;foreach(int v in new[]{a,a+7,a+1,a+1,a+7,a+8}) tri[at++]=v;}geom.vertices=f.points;geom.triangles=tri;geom.RecalculateNormals();geom.RecalculateBounds();snow.AddComponent<MeshFilter>().sharedMesh=geom;snow.AddComponent<MeshRenderer>().sharedMaterial=Surface(new Color(.76f,.85f,.92f));
        Model("Fir",f.props[0],2.2f,false);Model("Rock",f.props[1],1.2f,true);Model("Snowman",f.props[2],2.5f,false);
        baseActor=Flip(f.actor);owl=Instantiate(Resources.Load<GameObject>("Owl"),transform);Paint(owl,"Owl");owl.transform.localScale=Vector3.one*1.32f;foreach(var c in Resources.LoadAll<AnimationClip>("Owl"))if(!c.name.StartsWith("__preview")){clip=c;break;}if(!clip)throw new Exception("Owl FBX has no animation");
        cam=new GameObject("Game camera").AddComponent<Camera>();cam.transform.SetParent(transform);cam.fieldOfView=f.fov;cam.nearClipPlane=.05f;cam.farClipPlane=400;cam.backgroundColor=new Color(.43f,.69f,.86f);cam.clearFlags=CameraClearFlags.SolidColor;cam.transform.SetPositionAndRotation(Flip(f.eye),Quaternion.LookRotation(Flip(f.direction),Flip(f.up)));
        var sun=new GameObject("Winter sun").AddComponent<Light>();sun.transform.SetParent(transform);sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;sun.intensity=.92f;sun.color=new Color(1,.94f,.84f);sun.transform.rotation=Quaternion.Euler(40,-35,0);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.73f,.82f);BuildCourse();Pose(0);}
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
                Marker("Gate flag",PrimitiveType.Cube,d,x+side*.42f,1.89f,new Vector3(.9f,.6f,.08f),color);
            }
        }
        Marker("Jump line",PrimitiveType.Cube,takeoff,0,.08f,new Vector3(4.8f,.12f,.34f),gold);
        mandarin=Marker("Mandarin",PrimitiveType.Sphere,fruitAt,0,1.75f,new Vector3(.83f,.72f,.83f),Surface(new Color(1f,.26f,.005f)));
        var leaf=Marker("Leaf",PrimitiveType.Sphere,fruitAt,.12f,2.10f,new Vector3(.32f,.10f,.16f),Surface(new Color(.1f,.45f,.09f)));
        leaf.transform.SetParent(mandarin.transform,true);
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
        if(!hip){Debug.LogError("OWL PLAYER: missing animated hips");Application.Quit(2);yield break;}var q=hip.localRotation;var p=hip.localPosition;Tick(.15f,1,true);
        bool moved=Quaternion.Angle(q,hip.localRotation)>.1f||(p-hip.localPosition).magnitude>.0001f;bool air=Airborne;for(int i=0;i<75;i++)Tick(1f/60,1,false);bool ok=moved&&air&&!Airborne&&Lane>3;ResetRide();if(ok)Debug.Log("OWL PLAYER PASS: bones, steering, jump, landing, reset");else Debug.LogError("OWL PLAYER FAIL: bones="+moved+", jump="+air);Application.Quit(ok?0:3);}

    void Pose(float dt){Vector3 mid=Ground(distance,0),now=Ground(distance,lane);Vector3 delta=now-Ground(200,0), forward=Ground(distance+.4f,lane)-now;
        float u=jump<0?0:jump/.8f, arc=4*u*(1-u),sample=jump>=0?3.25f+.83f*u:landed<.77f?4.08f+landed:steer>=0?.8f+.7f*steer:2.7f+.55f*steer;
        clip.SampleAnimation(owl,sample);owl.transform.localScale=Vector3.one*1.32f;owl.transform.position=baseActor+delta+Vector3.up*arc*(1.1f-.28f*1.32f);owl.transform.rotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.Euler(0,90,0);
        cam.transform.position=Flip(f.eye)+mid-Ground(200,0)+(now-mid)*.6f;}
    public void ResetRide(){distance=200;lane=steer=0;jump=-1;landed=10;paused=false;nextGate=gates=0;fruit=false;feedback=null;mandarin.SetActive(true);state=RideState.Riding;Pose(0);}
    public void BeginRide(){ResetRide();}
    public void PauseRide(){if(state==RideState.Riding)paused=!paused;}
    void Update(){
        if(!cam||state!=RideState.Riding)return;
        if(Input.GetKeyDown(KeyCode.P))PauseRide();if(paused)return;
        float input=(Input.GetKey(KeyCode.RightArrow)||Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.LeftArrow)||Input.GetKey(KeyCode.A)?1:0);
        bool hop=Input.GetKeyDown(KeyCode.Space);
#if UNITY_WEBGL && !UNITY_EDITOR
        hop|=jumpRequested;jumpRequested=false;
#endif
        if(Input.touchCount>0){var p=Input.GetTouch(0);input=Mathf.Clamp((p.position.x/Screen.width-.5f)*3,-1,1);hop|=p.position.y>Screen.height*.65f&&p.phase==TouchPhase.Began;}
        Tick(Mathf.Min(Time.deltaTime,.05f),input,hop);
    }
    void Notice(string message){feedback=message;feedbackUntil=distance+7;}
    public void Tick(float dt,float input,bool hop){
        if(hop&&jump<0)jump=0;steer=Mathf.Lerp(steer,input,1-Mathf.Exp(-12*dt));lane=Mathf.Clamp(lane+steer*dt*5,-8,8);
        float before=distance;distance=Mathf.Min(distance+7*dt,finishAt);
        if(jump>=0){jump+=dt;if(jump>=.8f){jump=-1;landed=0;}}else landed+=dt;
        if(nextGate<gateDistances.Length&&distance>=gateDistances[nextGate]){
            bool passed=Mathf.Abs(lane-gateLanes[nextGate])<=2;
            if(passed)gates++;nextGate++;Notice(passed?"Ворота пройдены":"Мимо ворот");
        }
        if(before<fruitAt&&distance>=fruitAt){
            fruit=Mathf.Abs(lane)<=1.8f&&jump>=.12f&&jump<=.68f;
            if(fruit)mandarin.SetActive(false);Notice(fruit?"Мандарин пойман!":"Мандарин пропущен");
        }
        if(distance>=finishAt)state=RideState.Finished;
        Pose(dt);
    }
    string Target(){
        if(feedback!=null&&distance<feedbackUntil)return feedback;
        if(nextGate==2&&distance<fruitAt)return "Прыгай у жёлтой линии";
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
        GUI.Label(new Rect(left+5,top+72,width-10,51),"Рули пальцем слева или справа.\nДля прыжка коснись верхней трети экрана.",hint);
        if(GUI.Button(new Rect(left+20,top+131,width-40,46),state==RideState.Ready?"Поехали":paused?"Продолжить":"Ещё заезд",button)){
            if(paused)PauseRide();else BeginRide();
        }
    }
#endif
}
