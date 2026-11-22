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
    const int Entry=RaceWorld.AlpineStage*RaceWorld.StageLength;
    static void Start(MountainRace race,string hero,int seed){
        race.Menu();race.ChooseHero(hero);
        race.StartRace(JsonUtility.ToJson(new MountainRace.Settings{mode="review",seed=seed,sensitivity=4}));
        race.Seek(Entry);race.RideFromReview();
        Need(race.Status.phase=="play"&&race.World.Mode=="normal"&&race.Status.stage==8,"normal-course powder stage");
        Need(race.World.Items.Any(o=>o.kind=="arch"&&o.variant=="arch8"),"missing entry arch");
        race.BeginSteer();
    }
    enum Path {Jump,Bypass,Miss,NoJump}
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
        Need(race.World.TryFreeRideGap(Entry+80,out var gap),"missing first crevasse");
        if(path==Path.Jump)CheckRenderedCut(race.World,gap);
        Need(race.World.Items.All(o=>o.distance<Entry||o.kind=="coin"||o.kind=="rock"||o.kind=="arch"||o.kind=="snowBoulder"),"mixed regular course objects into powder");
        var fruit=race.World.Items.Where(o=>o.kind=="coin"&&o.air&&o.distance>gap.lipD&&o.distance<gap.endD).ToArray();
        Need(fruit.Length==3,"unmarked lip");
        float peak=0,depth=0,landing=0;bool airborne=false,pressed=false,queued=false,prompt=false;
        string shots=(path==Path.Jump||path==Path.NoJump)&&seed==1911?Environment.GetEnvironmentVariable("SOVA_FREERIDE_SHOTS"):null;
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
            if(!pressed&&d>=gap.lipD-12){
                pressed=true;
                if(path==Path.Jump||path==Path.Miss)race.AirTrick(hero=="bear"?"up":"tap");
            }
            if(path==Path.NoJump&&d==Entry)race.AirTrick("tap"); // An early tap cannot arm a later cornice.
            if(race.Status.helmets==0)x=0;
            race.DragSteer(x/(27*.79f));race.Advance(1f/30);
            queued|=race.Status.freeRideJumpQueued;
            prompt|=race.Status.freeRideJumpReady;
            if(!String.IsNullOrEmpty(shots)){
                if(path==Path.Jump&&d<gap.lipD-13&&race.Status.metres>=gap.lipD-13)RiderCourseChecks.Shot(race.View,"01-approach",shots,384,832);
                if(path==Path.Jump&&d<gap.SectionDistance(4)&&race.Status.metres>=gap.SectionDistance(4))RiderCourseChecks.Shot(race.View,hero+"-jump",shots,384,832);
                if(path==Path.NoJump&&depth<2.8f&&race.Altitude<=-2.8f)RiderCourseChecks.Shot(race.View,"03-fall",shots,384,832);
            }
            peak=Mathf.Max(peak,race.Altitude);
            depth=Mathf.Max(depth,-race.Altitude);
            if(race.Altitude< -1)Need(fruit.All(o=>!o.Visual||!o.Visual.gameObject.activeInHierarchy),"fruit obscures the fall into this crevasse");
            if(race.Altitude>.06f)airborne=true;
            else if(airborne&&landing==0)landing=race.Status.metres;
        }
        Need(race.Status.phase=="play"&&race.Status.metres>=gap.endD+18,"could not reach apron: "+path);
        if(path==Path.Jump){
            Need(peak>.65f&&landing>=gap.endD&&race.Status.helmets==1,"jump failed, peak "+peak+" landing "+landing+" lip "+gap.lipD);
            Need(fruit.All(o=>o.consumed),"missed airborne mandarin "+hero+" "+seed);
            Need(prompt&&queued&&race.Status.tricks==1,"one requested takeoff should complete one trick");
        }else if(path==Path.Bypass)Need(!prompt&&!airborne&&depth==0&&race.Status.helmets==1,"bypass prompted a jump, launched or lost a helmet");
        else{
            Need(!airborne&&depth>=4&&race.Status.helmets==0&&!race.Status.freeRideInCrevasse,"fall must drop below the cornice, cost one helmet, and recover: "+path);
            Need(fruit.All(o=>!o.consumed)&&race.Status.tricks==0,"fall collected an airborne reward");
        }
        Debug.Log("FREE RIDE "+hero+" seed "+seed+" "+path+" lip "+gap.lipD.ToString("F1")+".."+gap.endD.ToString("F1")+
            " lane "+gap.lane.ToString("F1")+" peak "+peak.ToString("F2")+" landing "+landing.ToString("F1")+
            " air fruit "+fruit.Count(o=>o.consumed)+"/3 helmets "+race.Status.helmets+" drop "+depth.ToString("F2"));
    }
    static void RollingSnow(MountainRace race,bool collide){
        Start(race,"bear",1911);
        var snow=race.World.Items.First(o=>o.kind=="snowBoulder");
        float first=snow.distance,rotation=0,volume=0;
        var orientation=snow.Visual.transform.rotation;
        for(int i=0;i<570&&race.Status.phase=="play"&&race.Status.metres<Entry+184;i++){
            float d=race.Status.metres;
            float lane=d<Entry+101?3.3f:collide?snow.lane:-7.5f;
            race.DragSteer(lane/(27*.79f));race.Advance(1f/30);
            volume=Mathf.Max(volume,race.Status.boulderNear);
            if(snow.Visual)rotation=Mathf.Max(rotation,Quaternion.Angle(orientation,snow.Visual.transform.rotation));
            if(collide&&snow.judged||!collide&&race.Status.metres>snow.distance+3)break;
        }
        Need(snow.distance-first>12&&rotation>75&&volume>.65f,"boulder did not travel, rotate or approach audibly");
        Need(snow.judged==collide&&race.Status.phase=="play"&&race.Status.helmets==(collide?0:1),
            "snow collision/bypass "+collide+" at "+race.Status.metres+", ball "+snow.distance+"/"+snow.lane+", "+race.Status.message);
        Debug.Log("ROLLING SNOW PASS "+(collide?"collision":"bypass")+", helmets "+race.Status.helmets+", proximity "+volume.ToString("F2"));
    }
    public static void Run(MountainRace race){
        FirstCut(race,"owl",1911,Path.Jump);FirstCut(race,"bear",1911,Path.Bypass);
        FirstCut(race,"bear",1911,Path.Miss);FirstCut(race,"owl",1911,Path.NoJump);FirstCut(race,"bear",4242,Path.Jump);
        // A second fall has no trial helmet. No pickup or subsequent tick may rescue it.
        Start(race,"bear",1911);
        int deepestFalls=0;bool atDepth=false;
        for(int i=0;i<900&&race.Status.phase=="play";i++){
            float d=race.Status.metres;
            Need(race.World.TryFreeRideGap(d,out var gap),"left the course before the second fall");
            race.DragSteer((d>=gap.lipD-32&&d<gap.endD?gap.lane:0)/(27*.79f));race.Advance(1f/30);
            if(race.Altitude< -4&&!atDepth)deepestFalls++;
            atDepth=race.Altitude< -4;
        }
        Need(deepestFalls==2&&race.Status.phase=="over"&&!race.Status.rankedResult,"second fall should finish the trial below the snow");
        Start(race,"owl",1911);
        Need(race.World.TryFreeRideGap(Entry+80,out var first),"missing replay");
        Need(Mathf.Abs(first.lipD-Entry-83.81f)<.02f&&Mathf.Abs(first.lane+4.833f)<.02f,"seed 1911 changed");
        float lastCommandLip=-1;int jumps=0;
        for(int i=0;i<5700&&race.Status.phase=="play"&&race.Status.metres<RaceWorld.LapLength+3;i++){
            float d=race.Status.metres;
            if(!race.World.TryFreeRideGap(d,out var gap)){race.DragSteer(0);race.Advance(1f/30);continue;}
            float ahead=gap.lipD-d;
            float target=ahead<40&&d<gap.endD+12?gap.lane:0;
            var rolling=race.World.Items.FirstOrDefault(o=>o.kind=="snowBoulder"&&!o.consumed&&o.distance>d-3&&o.distance<d+29);
            if(rolling!=null&&!(ahead<32&&d<gap.endD+10)&&race.World.TryFreeRideGap(rolling.distance-55,out var source))
                target=7.5f*Mathf.Sign(source.lane);
            race.DragSteer(target/(27*.79f));
            if(ahead<=9&&ahead>0&&lastCommandLip!=gap.lipD){
                race.AirTrick("tap");lastCommandLip=gap.lipD;jumps++;
            }
            race.Advance(1f/30);
        }
        Need(race.Status.phase=="play"&&race.Status.stage==0&&race.Status.metres>=RaceWorld.LapLength&&!race.Status.rankedResult&&
            jumps==16&&race.Status.tricks==jumps&&race.Status.helmets==1,
            "failed full descent: "+race.Status.phase+" "+race.Status.message+" at "+race.Status.metres+", completed "+race.Status.tricks+"/"+jumps+" tricks; helmets "+race.Status.helmets);
        Debug.Log("FREE RIDE all 16 cornices jumped on command; next ordinary lap");
        RollingSnow(race,true);RollingSnow(race,false);
        Debug.Log("FREE RIDE PASS ordinary menu, both heroes, two seeds, jump/bypass/side entry, 22.5 km lap transition");
        race.Menu();
    }
}
