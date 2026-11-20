using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RaceItem {
    public string kind,variant,character;
    public float distance,lane,width,height,length,lift,span;
    public bool consumed,judged,ridden,park,needsTrick,prizeLine,trickLine,air,stoneGate;
    public bool sb,kid,stand,sit,instr,walker;
    public int points;
    internal float originD,originX,scale=1,side,phase,age,pace,amp,yaw,lean,velocity,offset,windup,cooldown,stride;
    internal float release,from,to,vStart,vCruise,tau,pathSpan,airStart,airSpan,airLift,airBase;
    internal int route;
    internal bool leap,small,decor,bypass;
    internal bool movingPoseReady;
    internal bool threwSnowball;
    internal Vector2 throwEnd;
    internal float throwDuration,throwEndHeight,throwStartHeight;
    internal GameObject obj;
    internal GameObject prints;
    internal Mesh printMesh;
    internal int printCount=-1;
    public Renderer Visual=>obj?obj.GetComponent<Renderer>():null;
    internal SkinnedMeshRenderer skin;
    internal ParticleSystem spray;
    public void Consume(){consumed=true;if(obj)obj.SetActive(false);}
}

public partial class RaceWorld : MonoBehaviour {
    public readonly List<RaceItem> Items=new List<RaceItem>();
    readonly List<RaceItem> scenery=new List<RaceItem>();
    readonly Dictionary<string,Stack<GameObject>> pool=new Dictionary<string,Stack<GameObject>>();
    const float Ahead=115,Behind=30;
    int seed,chunk=int.MinValue,firstSegment=int.MaxValue,lastSegment=int.MinValue;
    float lastDistance,lastTime,spawnLane,letterDistance=float.PositiveInfinity;
    string mode,letter;
    bool letterAdded;
    public string Mode=>mode;
    public static int StageAt(float d)=>Mathf.FloorToInt(Mathf.Max(0,d)/2500)%8;
    public static float Roll(int seed,int row,int stream){
        unchecked{
            uint n=(uint)seed ^ ((uint)(row+1)*0x9e3779b1u) ^ ((uint)(stream+1)*0x85ebca6bu);
            n=(n^(n>>16))*0x21f0aaadu;n=(n^(n>>15))*0x735a2d97u;
            return (float)((n^(n>>15))/4294967296.0);
        }
    }
    public void Configure(int seed,string mode){
        ClearObjects();this.seed=seed;this.mode=mode??"normal";lastDistance=lastTime=0;snowVertices=null;
        firstSegment=int.MaxValue;lastSegment=int.MinValue;chunk=int.MinValue;
        letter=null;letterAdded=false;letterDistance=float.PositiveInfinity;
        firstTower=int.MinValue;BuildTurns();Refresh(0,0);
    }
    public void SetLetter(float metres,string character){
        if(letterDistance==metres&&letter==character)return;
        for(int i=Items.Count-1;i>=0;i--)if(Items[i].kind=="letter"){Release(Items[i]);Items.RemoveAt(i);}
        letter=character;letterDistance=metres;letterAdded=false;
    }
    void ClearObjects(){foreach(var i in Items)Release(i);Items.Clear();foreach(var i in scenery)Release(i);scenery.Clear();}
    static void DisposeOwned(UnityEngine.Object value){
        if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);
    }
    void Release(RaceItem item){
        if(item.prints){DisposeOwned(item.printMesh);DisposeOwned(item.prints);}
        if(!item.obj)return;
        if(item.kind=="ramp"||item.kind=="rail"){
            item.obj.SetActive(false);DisposeOwned(item.obj.GetComponent<MeshFilter>().sharedMesh);DisposeOwned(item.obj);return;
        }
        if(!pool.TryGetValue(item.variant,out var stack))pool[item.variant]=stack=new Stack<GameObject>();
        item.obj.SetActive(false);stack.Push(item.obj);
    }
    static float RowLength(int stage,string mode){
        if(mode=="sprint")return 75;if(mode=="tubing")return 33;if(mode=="sled")return 31;
        switch(stage){case 0:return 62.5f;case 1:return 50;case 2:return 117;case 3:return 75;case 5:case 6:return 90;default:return 50;}
    }
    int Segment(float d){
        d=Mathf.Max(0,d);
        if(mode=="sprint"||mode=="tubing"||mode=="sled")return Mathf.FloorToInt(d/RowLength(0,mode));
        int stage=Mathf.FloorToInt(d/2500);
        return stage*1000+Mathf.FloorToInt((d-stage*2500)/RowLength(stage%8,mode));
    }
    int Following(int seg){
        if(mode=="sprint"||mode=="tubing"||mode=="sled")return seg+1;
        int stage=seg/1000,index=seg%1000;
        return (index+1)*RowLength(stage%8,mode)>=2500?(stage+1)*1000:seg+1;
    }
    public void Refresh(float distance,float elapsed)=>Refresh(distance,elapsed,0);
    public void Refresh(float distance,float elapsed,float riderLane){
        spawnLane=mode=="normal"?riderLane:0;
        distance=Mathf.Max(0,distance);float dt=Mathf.Clamp(elapsed-lastTime,0,.15f);
        bool seek=distance<lastDistance-.001f||distance>lastDistance+Ahead;
        if(seek){ClearObjects();firstSegment=int.MaxValue;lastSegment=int.MinValue;letterAdded=false;lastDistance=distance;chunk=int.MinValue;dt=0;}
        int first=Segment(distance-Behind),last=Segment(distance+Ahead);
        if(firstSegment==int.MaxValue)firstSegment=first;
        for(int s=lastSegment==int.MinValue?first:Following(lastSegment);s<=last;s=Following(s)){BuildRow(s);lastSegment=s;}
        if(!letterAdded&&!string.IsNullOrEmpty(letter)&&letterDistance<=distance+Ahead&&letterDistance>=distance-Behind){
            var o=Add("letter",letterDistance,0,1.3f,1.3f,"letter"+(int)letter[0]);o.character=letter;letterAdded=true;
        }
        int next=Mathf.FloorToInt(distance/14);
        if(next!=chunk){chunk=next;UpdateTerrain(next*14);UpdateScenery(distance);}
        RefreshLift(distance,elapsed);
        for(int i=Items.Count-1;i>=0;i--){
            var o=Items[i];if(o.consumed||Mathf.Max(o.distance,o.originD)+o.length*.5f+o.span<distance-Behind){
                Release(o);Items.RemoveAt(i);continue;
            }
            MoveItem(o,distance,elapsed,dt);Place(o);
        }
        lastDistance=distance;lastTime=elapsed;
    }
    internal RaceItem Add(string kind,float d,float x,float width,float height,string visual=null){
        var o=new RaceItem{kind=kind,variant=visual??kind,distance=d,originD=d,lane=x,originX=x,width=width,height=height};Items.Add(o);return o;
    }
    GameObject GetVisual(RaceItem o){
        if(o.kind=="ramp"||o.kind=="rail")return Ramp(o);
        if(pool.TryGetValue(o.variant,out var stack)&&stack.Count>0){var reuse=stack.Pop();reuse.SetActive(true);return reuse;}
        return o.variant.StartsWith("forestCard")?ForestCard(o.variant):CreateVisual(o.variant,transform);
    }
    void Place(RaceItem o){
        if(o.stoneGate)return; // Scoring plane between two individually grounded, illuminated boulders.
        if(o.obj&&!Moves(o.kind))return;
        if(!o.obj){
            o.obj=GetVisual(o);o.skin=o.obj.GetComponent<SkinnedMeshRenderer>();o.obj.name=o.kind+" · "+o.variant;
            if(o.kind=="cannon"){
                o.spray=o.obj.GetComponentInChildren<ParticleSystem>();
                if(!o.spray)AddCannonSpray(o);
                o.spray.Simulate(1.05f,false,true,false);
                o.spray.Play(false);
            }
        }
        bool onRunners=o.kind=="tube"||o.kind=="npc"||o.kind=="cat"||o.kind=="snowmobile";
        Quaternion attitude;
        if(o.kind=="ramp"||o.kind=="rail")attitude=Quaternion.identity; // Mesh already follows the snow.
        else if(onRunners){
            // Yaw about the snow normal. Turning a downhill-facing mesh around must never reverse its bank or grade.
            float roll=o.kind=="tube"?o.lean*.15f:o.lean;
            attitude=SnowFrame(o.distance,o.lane)*Quaternion.Euler(0,o.yaw,roll);
        }else attitude=Quaternion.Euler(0,HeadingAt(o.distance)*Mathf.Rad2Deg+o.yaw,o.lean);
        float y=o.offset;
        if(o.air||o.kind=="ball"||o.kind=="letter"||o.kind=="goldenOwl")y+=o.height;
        else if(o.kind=="coin")y+=.6f;else if(o.kind=="thermos"||o.kind=="helmet"||o.kind=="magnet"||o.kind=="aid"||o.kind=="gift")y+=.84f;
        o.obj.transform.SetPositionAndRotation(SnowSurface(o.distance,o.lane)+Vector3.up*y,attitude);
        float sx=o.scale;
        if(o.kind=="gate")sx=o.width/(o.stoneGate?3.9f:5);
        if(o.kind=="fence")sx=o.width/5;
        o.obj.transform.localScale=new Vector3(sx,o.scale,o.scale);
        if(o.skin){
            if(o.kind=="tube"){
                PoseTubeRider(o.skin,o.lean/16,.18f+Mathf.Abs(o.lean)/29);
                return;
            }
            var m=o.skin.sharedMesh;
            for(int i=0;i<m.blendShapeCount;i++){
                var k=m.GetBlendShapeName(i);float v=k=="Carve"||k=="1"?o.lean/16:
                    k=="Absorb"||k=="0"?.18f+Mathf.Abs(o.lean)/29:0;
                if(o.kind=="dog"&&k.StartsWith("Run",StringComparison.Ordinal)){
                    float delta=Mathf.Abs(Mathf.Repeat(o.age*5.1f-(k[k.Length-1]-'0')+2,4)-2);
                    v=Mathf.Max(0,1-delta);
                }else if(o.kind=="mogulYeti"&&(k=="StrideL"||k=="StrideR"))v=Mathf.Max(0,o.stride*(k=="StrideL"?1:-1));
                else if(o.kind=="yeti"&&k==(o.lane<0?"Wave0":"Wave1"))v=.85f+.14f*Mathf.Sin(o.age*8);
                else if(o.kind=="snowman"){
                    bool hand=k.EndsWith(o.lane<0?"0":"1",StringComparison.Ordinal);
                    if(hand&&k.StartsWith("Ready",StringComparison.Ordinal))v=o.windup>0?1:0;
                    if(hand&&k.StartsWith("Throw",StringComparison.Ordinal))v=Mathf.Clamp01((o.cooldown-.73f)/.32f);
                }
                o.skin.SetBlendShapeWeight(i,Mathf.Clamp(v,-1,1)*100);
            }
        }
    }
    public static void PoseTubeRider(SkinnedMeshRenderer skin,float lean,float compression){
        var mesh=skin.sharedMesh;
        int carve=mesh.GetBlendShapeIndex("Carve"),absorb=mesh.GetBlendShapeIndex("Absorb");
        if(carve>=0)skin.SetBlendShapeWeight(carve,Mathf.Clamp(lean,-1,1)*100);
        if(absorb>=0)skin.SetBlendShapeWeight(absorb,Mathf.Clamp01(compression)*100);
    }
    static bool Moves(string kind){
        switch(kind){
            case "npc":case "tube":case "cat":case "snowmobile":case "mogulYeti":case "yeti":case "dog":case "ball":case "snowman":
            case "airRing":case "coin":case "feather":case "gift":case "thermos":case "aid":case "helmet":case "magnet":return true;
            default:return false;
        }
    }
}
