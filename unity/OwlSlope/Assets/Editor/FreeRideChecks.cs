using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FreeRideChecks {
    const string pending="Owl.CheckFreeRide";
    static FreeRideChecks(){EditorApplication.update+=WhenReady;}
    public static void Batch(){
        SessionState.SetBool(pending,true);
        EditorSceneManager.OpenScene("Assets/Mountain.unity");EditorApplication.EnterPlaymode();
    }
    static void WhenReady(){
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||!SessionState.GetBool(pending,false))return;
        var race=UnityEngine.Object.FindFirstObjectByType<MountainRace>();
        if(!race||!race.View)return;
        SessionState.EraseBool(pending);
        try{Run(race);EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
    }
    static void Need(bool condition,string detail){if(!condition)throw new InvalidOperationException("Free Ride: "+detail);}
    static void Start(MountainRace race,string hero,int seed){
        race.Menu();race.ChooseHero(hero);
        race.StartRace("{\"mode\":\"freeride\",\"seed\":"+seed+",\"sensitivity\":4,\"helmets\":1}");
        Need(race.Status.phase=="play"&&race.World.Mode=="freeride","menu did not launch freeride");
        Need(race.Status.helmets==1,"expected a trial helmet");race.BeginSteer();
    }
    enum Path {Jump,Bypass,Miss}
    static void CheckRenderedCut(RaceWorld world,RaceWorld.FreeRideGap gap){
        var terrain=world.GetComponentsInChildren<MeshFilter>().First(m=>m.sharedMesh&&m.sharedMesh.name=="Alpine snow, exposed slate and crevasses");
        var collider=terrain.gameObject.AddComponent<MeshCollider>();
        try{
            collider.sharedMesh=terrain.sharedMesh;
            Physics.SyncTransforms();
            float d=.5f*(gap.lipD+gap.endD);
            float Depth(float metres,float x){
                var support=world.SnowSurface(metres,x);
                Need(collider.Raycast(new Ray(support+Vector3.up*12,Vector3.down),out var hit,28),"no rendered surface at "+metres+" / "+x);
                return support.y-hit.point.y;
            }
            float cut=Depth(d,gap.CentreAt(d)),rim=Depth(d,gap.RightAt(d)+1);
            float approach=Depth(gap.lipD-1,gap.lane),landing=Depth(gap.endD+1,gap.CentreAt(gap.endD));
            Need(cut>3&&Mathf.Abs(rim)<.06f&&Mathf.Abs(approach)<.06f&&Mathf.Abs(landing)<.06f,
                "rendered crevasse differs from jump: depth "+cut+", rim "+rim+", approach "+approach+", landing "+landing);
            foreach(float t in new[]{.16f,.36f,.57f,.79f,.90f}){
                float at=Mathf.Lerp(gap.lipD,gap.endD,t);
                var banks=gap.CrossSection(at);float probe=Mathf.Min(.46f,(banks.y-banks.x)*.25f);
                if(banks.y-banks.x<2)continue;
                foreach(float side in new[]{-1,1}){
                    float edge=side<0?banks.x:banks.y;
                    Need(Mathf.Abs(Depth(at,edge+side*.35f))<.08f&&Depth(at,edge-side*probe)>.15f,
                        "rendered snow misses the curved rim at "+at+" m, side "+side+" x="+edge);
                }
            }
            Debug.Log("FREE RIDE MESH measured ice floor "+cut.ToString("F2")+" m below the snow and bank support "+rim.ToString("F3")+" m");
        }finally{UnityEngine.Object.DestroyImmediate(collider);}
    }
    static void FirstCut(MountainRace race,string hero,int seed,Path path){
        Start(race,hero,seed);
        Need(race.World.TryFreeRideGap(80,out var gap),"missing first crevasse");
        if(path==Path.Jump)CheckRenderedCut(race.World,gap);
        Need(race.World.Items.All(o=>o.kind=="coin"||o.kind=="rock"),"mixed regular course objects into powder");
        var fruit=race.World.Items.Where(o=>o.kind=="coin"&&o.air&&o.distance>gap.lipD&&o.distance<gap.endD).ToArray();
        Need(fruit.Length==3,"unmarked lip");
        float peak=0,landing=0,inside=float.NegativeInfinity;bool airborne=false;
        string shots=path==Path.Jump&&seed==1911?Environment.GetEnvironmentVariable("SOVA_FREERIDE_SHOTS"):null;
        float inner=float.PositiveInfinity,sign=Mathf.Sign(gap.lane);
        for(int knot=0;knot<=3;knot++){
            var banks=gap.CrossSection(gap.SectionDistance(knot));
            inner=Mathf.Min(inner,Mathf.Abs(sign<0?banks.y:banks.x));
        }
        for(int i=0;i<700&&race.Status.phase=="play"&&race.Status.metres<gap.endD+18;i++){
            float d=race.Status.metres;
            float x=gap.lane;
            if(path==Path.Bypass)x=-Mathf.Sign(gap.lane)*2.8f;
            if(path==Path.Miss)x=d<gap.SectionDistance(2)?sign*(inner-.65f):gap.CentreAt(gap.SectionDistance(5))+sign*1.4f;
            if(race.Status.helmets==0)x=0;
            race.DragSteer(x/(27*.79f));race.Advance(1f/30);
            if(d>gap.lipD&&d<gap.endD){
                var banks=gap.CrossSection(race.Status.metres);
                inside=Mathf.Max(inside,Mathf.Min(race.Lane-banks.x,banks.y-race.Lane));
            }
            if(!String.IsNullOrEmpty(shots)){
                if(d<gap.lipD-13&&race.Status.metres>=gap.lipD-13)RiderCourseChecks.Shot(race.View,"01-approach",shots,384,832);
                if(d<gap.SectionDistance(4)&&race.Status.metres>=gap.SectionDistance(4))RiderCourseChecks.Shot(race.View,"02-jump",shots,384,832);
            }
            peak=Mathf.Max(peak,race.Altitude);
            if(race.Altitude>.06f)airborne=true;
            else if(airborne&&landing==0)landing=race.Status.metres;
        }
        Need(race.Status.phase=="play"&&race.Status.metres>=gap.endD+18,"could not reach apron: "+path);
        if(path==Path.Jump){
            Need(peak>.65f&&landing>=gap.endD&&race.Status.helmets==1,"jump failed, peak "+peak+" landing "+landing+" lip "+gap.lipD);
            Need(fruit.All(o=>o.consumed),"missed airborne mandarin "+hero+" "+seed);
        }else if(path==Path.Bypass)Need(!airborne&&race.Status.helmets==1,"bypass launched or lost a helmet");
        else Need(!airborne&&race.Status.helmets==0,"side entry must cost one helmet: overlap "+inside+", height "+peak+", helmets "+race.Status.helmets);
        Debug.Log("FREE RIDE "+hero+" seed "+seed+" "+path+" lip "+gap.lipD.ToString("F1")+".."+gap.endD.ToString("F1")+
            " lane "+gap.lane.ToString("F1")+" peak "+peak.ToString("F2")+" landing "+landing.ToString("F1")+
            " air fruit "+fruit.Count(o=>o.consumed)+"/3 helmets "+race.Status.helmets);
    }
    public static void Run(MountainRace race){
        FirstCut(race,"owl",1911,Path.Jump);FirstCut(race,"bear",1911,Path.Bypass);
        FirstCut(race,"bear",1911,Path.Miss);FirstCut(race,"bear",4242,Path.Jump);
        Start(race,"owl",1911);
        Need(race.World.TryFreeRideGap(80,out var first),"missing replay");
        Need(Mathf.Abs(first.lipD-83.81f)<.02f&&Mathf.Abs(first.lane+4.833f)<.02f,"seed 1911 changed");
        for(int i=0;i<950&&race.Status.phase=="play";i++){race.DragSteer(0);race.Advance(.10f);}
        Need(race.Status.phase=="finish"&&race.Status.metres==RaceWorld.FreeRideLength&&!race.Status.rankedResult,
            "failed full unranked descent: "+race.Status.phase+" at "+race.Status.metres);
        Debug.Log("FREE RIDE PASS ordinary menu, both heroes, two seeds, jump/bypass/side entry, 1200 m finish");
        race.Menu();
    }
}
