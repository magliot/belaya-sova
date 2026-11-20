using System;
using UnityEngine;
using UnityEngine.Rendering;

// A deliberately small playable comparison scene, fed by the browser camera fixture.
public class SlopePilot : MonoBehaviour {
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
        var sun=new GameObject("Winter sun").AddComponent<Light>();sun.transform.SetParent(transform);sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;sun.intensity=.92f;sun.color=new Color(1,.94f,.84f);sun.transform.rotation=Quaternion.Euler(40,-35,0);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.73f,.82f);Pose(0);}
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
    public void ResetRide(){distance=200;lane=steer=0;jump=-1;landed=10;Pose(0);}
    void Update(){if(!cam)return;if(Input.GetKeyDown(KeyCode.P))paused=!paused;if(paused)return;float dt=Mathf.Min(Time.deltaTime,.05f),input=(Input.GetKey(KeyCode.RightArrow)||Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.LeftArrow)||Input.GetKey(KeyCode.A)?1:0);bool hop=Input.GetKeyDown(KeyCode.Space);if(Input.touchCount>0){var p=Input.GetTouch(0);input=Mathf.Clamp((p.position.x/Screen.width-.5f)*3,-1,1);if(p.position.y>Screen.height*.65f&&p.phase==TouchPhase.Began)hop=true;}Tick(dt,input,hop);}
    public void Tick(float dt,float input,bool hop){if(hop&&jump<0)jump=0;steer=Mathf.Lerp(steer,input,1-Mathf.Exp(-12*dt));lane=Mathf.Clamp(lane+steer*dt*5,-8,8);distance+=7*dt;if(distance>294){ResetRide();return;}if(jump>=0){jump+=dt;if(jump>=.8f){jump=-1;landed=0;}}else landed+=dt;Pose(dt);}
    void OnGUI(){if(GUI.Button(new Rect(12,12,100,36),paused?"Resume":"Pause"))paused=!paused;if(GUI.Button(new Rect(120,12,80,36),"Reset"))ResetRide();}
}
