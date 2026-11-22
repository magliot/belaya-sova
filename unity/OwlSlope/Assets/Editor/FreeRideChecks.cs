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
    static void Start(MountainRace race,string hero,int seed,float at=Entry){
        race.Menu();race.ChooseHero(hero);
        race.StartRace(JsonUtility.ToJson(new MountainRace.Settings{mode="review",seed=seed,sensitivity=4}));
        race.Seek(at);race.RideFromReview();
        Need(race.Status.phase=="play"&&race.World.Mode=="normal"&&race.Status.stage==8,"normal-course powder stage");
        if(at==Entry)Need(race.World.Items.Any(o=>o.kind=="arch"&&o.variant=="arch8"),"missing entry arch");
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
        Need(race.World.Items.All(o=>o.distance<Entry||o.kind=="coin"||o.kind=="rock"||o.kind=="arch"||o.kind=="snowBoulder"||o.kind=="npc"),"mixed regular course objects into powder");
        var fruit=race.World.Items.Where(o=>o.kind=="coin"&&o.air&&o.distance>gap.lipD&&o.distance<gap.endD).ToArray();
        Need(fruit.Length==3,"unmarked lip");
        float peak=0,depth=0,landing=0,tapMetre=0;bool airborne=false,early=false,prompt=false;
        string shots=(path==Path.Jump||path==Path.NoJump)&&seed==1911?Environment.GetEnvironmentVariable("SOVA_FREERIDE_SHOTS"):null;
        float lastOpen=gap.lipD;
        for(float d=gap.lipD+.1f;d<gap.endD;d+=.1f)if(gap.OverOpening(d,gap.lane))lastOpen=d;
        float tapTarget=Mathf.Lerp(gap.lipD,lastOpen,seed==4242?.73f:hero=="bear"?.60f:.42f);
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
            if(!early&&d>=gap.lipD-8){
                race.BeginTap(0);early=true;
                if(path!=Path.NoJump)race.EndTap(0);
            }
            if(race.Status.freeRideTrickReady&&gap.OverOpening(d,race.Status.riderLane)&&
                d>=tapTarget&&tapMetre==0){
                Need(race.Status.air&&race.Altitude>.25f&&race.Status.tricks==0,"trick must follow visible takeoff");
                if(path==Path.Jump){race.BeginTap(1);race.EndTap(1);tapMetre=d;}
                if(path==Path.NoJump){race.HoldAirTap(0);race.EndTap(0);tapMetre=d;}
            }
            if(race.Status.helmets==0)x=0;
            race.DragSteer(x/(27*.79f));race.Advance(1f/30);
            prompt|=race.Status.freeRideTrickReady;
            if(!String.IsNullOrEmpty(shots)){
                if(path==Path.Jump&&d<gap.lipD-13&&race.Status.metres>=gap.lipD-13)RiderCourseChecks.Shot(race.View,"01-approach",shots,384,832);
                if(path==Path.Jump&&d<gap.SectionDistance(4)&&race.Status.metres>=gap.SectionDistance(4))RiderCourseChecks.Shot(race.View,hero+"-jump",shots,384,832);
                if(path==Path.NoJump&&depth<2.8f&&race.Altitude<=-2.8f)RiderCourseChecks.Shot(race.View,"03-fall",shots,384,832);
            }
            peak=Mathf.Max(peak,race.Altitude);
            depth=Mathf.Max(depth,-race.Altitude);
            if(race.Altitude< -1){
                Need(gap.OverOpening(race.Status.metres,race.Status.riderLane),"fall crossed through solid snow");
                Need(fruit.All(o=>!o.Visual||!o.Visual.gameObject.activeInHierarchy),"fruit obscures the fall into this crevasse");
            }
            if(race.Altitude>.06f)airborne=true;
            else if(airborne&&landing==0)landing=race.Status.metres;
        }
        Need(race.Status.phase=="play"&&race.Status.metres>=gap.endD+18,"could not reach apron: "+path);
        if(path==Path.Jump){
            Need(peak>1&&landing>=gap.endD&&race.Status.helmets==1,"jump failed, peak "+peak+" landing "+landing+" lip "+gap.lipD);
            Need(fruit.All(o=>o.consumed),"missed airborne mandarin "+hero+" "+seed);
            Need(prompt&&tapMetre>gap.lipD&&race.Status.tricks==1,"one airborne tap should complete one trick");
        }else if(path==Path.Bypass)Need(!prompt&&!airborne&&depth==0&&race.Status.helmets==1,"bypass prompted a jump, launched or lost a helmet");
        else{
            Need(airborne==(path==Path.NoJump)&&depth>=4&&race.Status.helmets==0&&!race.Status.freeRideInCrevasse,"fall must drop below the cornice, cost one helmet, and recover: "+path);
            Need(peak<.85f,"passive glide should skim the lip until a trick is requested");
            Need(fruit.All(o=>!o.consumed)&&race.Status.tricks==0,"fall collected an airborne reward");
        }
        Debug.Log("FREE RIDE "+hero+" seed "+seed+" "+path+" lip "+gap.lipD.ToString("F1")+".."+gap.endD.ToString("F1")+
            " lane "+gap.lane.ToString("F1")+" peak "+peak.ToString("F2")+" landing "+landing.ToString("F1")+
            " tap "+tapMetre.ToString("F1")+" air fruit "+fruit.Count(o=>o.consumed)+"/3 helmets "+race.Status.helmets+" drop "+depth.ToString("F2"));
    }
    static void RollingSnow(MountainRace race,bool collide){
        const float crossing=Entry+300;
        Start(race,"bear",1911,crossing+50);
        race.World.TryFreeRideGap(crossing+70,out var gap);
        var snow=race.World.Items.First(o=>o.kind=="snowBoulder"&&o.distance>=crossing);
        float first=snow.distance,rotation=0,volume=0;
        var orientation=snow.Visual.transform.rotation;
        for(int i=0;i<570&&race.Status.phase=="play"&&race.Status.metres<crossing+197;i++){
            float d=race.Status.metres;
            float lane=d<gap.endD+2?gap.bypassLane:collide?snow.lane:-7.5f;
            race.DragSteer(lane/(27*.79f));race.Advance(1f/30);
            volume=Mathf.Max(volume,race.Status.boulderNear);
            if(snow.Visual)rotation=Mathf.Max(rotation,Quaternion.Angle(orientation,snow.Visual.transform.rotation));
            if(collide&&snow.judged||!collide&&race.Status.metres>snow.distance+3)break;
        }
        Need(snow.distance-first>12&&rotation>75&&volume>.65f,"crossing boulder travel "+(snow.distance-first)+" rotation "+rotation+" proximity "+volume);
        Need(snow.judged==collide&&race.Status.phase=="play"&&race.Status.helmets==(collide?0:1),
            "snow collision/bypass "+collide+" at "+race.Status.metres+", ball "+snow.distance+"/"+snow.lane+", "+race.Status.message);
        Debug.Log("ROLLING SNOW PASS "+(collide?"collision":"bypass")+", helmets "+race.Status.helmets+", proximity "+volume.ToString("F2"));
    }
    static void Pursuit(MountainRace race,int seed,bool evade){
        Start(race,"owl",seed,Entry+119);
        var snow=race.World.Items.First(o=>o.kind=="snowBoulder"&&o.distance<Entry+150);
        race.World.TryFreeRideGap(Entry+80,out var gap);
        float clearLane=3.75f*Mathf.Sign(gap.lane);
        float warned=-1,contact=-1;
        for(int i=0;i<240&&race.Status.phase=="play";i++){
            if(race.Status.boulderChasing&&warned<0)warned=race.Status.elapsed;
            float aim=evade&&warned>=0&&race.Status.elapsed-warned>.40f?clearLane:0;
            race.DragSteer(aim/(27*.79f));race.Advance(1f/60);
            if(snow.judged){contact=race.Status.elapsed;break;}
            if(snow.distance>race.Status.metres+4&&snow.distance>Entry+155)break;
        }
        Need(warned>=0&&snow.judged!=evade&&race.Status.helmets==(evade?1:0),
            "rear pursuit seed "+seed+" evade "+evade+" at "+race.Status.metres+" ball "+snow.distance+" warning "+warned);
        if(!evade)Need(contact-warned>1.15f,"no reaction window for pursuit");
        Debug.Log("PURSUIT "+seed+" "+(evade?"evaded":"hit")+" at "+race.Status.metres.ToString("F1")+
            " m, warning to impact "+(contact<0?"—":(contact-warned).ToString("F2"))+" s");
    }
    static void Transverse(MountainRace race,int seed,bool bypass){
        Start(race,bypass?"bear":"owl",seed,Entry+165);
        int count=0,leftShelves=0;
        for(int i=0;i<16;i++){
            Need(race.World.TryFreeRideGap(Entry+150*i+75,out var hole),"missing section "+i);
            if(!hole.transverse)continue;
            count++;
            if(hole.bypassLane<0)leftShelves++;
            float mid=hole.SectionDistance(4),width=race.World.WidthAt(mid)*.5f;
            var banks=hole.CrossSection(mid);
            float left=Mathf.Max(0,banks.x+width),right=Mathf.Max(0,width-banks.y);
            Need(Mathf.Min(left,right)==0&&Mathf.Max(left,right)>2.8f&&Mathf.Max(left,right)<4.1f&&
                !hole.OverOpening(mid,hole.bypassLane)&&hole.OverOpening(mid,0),"transverse shelf width "+left+"/"+right);
        }
        Need(count==4&&leftShelves==2,"two shelves on each side per visit");
        race.World.TryFreeRideGap(Entry+225,out var gap);
        var outcrops=race.World.Items.Where(o=>o.kind=="rock"&&o.distance>=gap.lipD-11&&o.distance<=gap.endD+5&&o.lane*gap.bypassLane>0).ToArray();
        Need(outcrops.Length==3&&outcrops.All(o=>Mathf.Abs(o.lane-gap.bypassLane)>o.width*.5f+1.2f),"outcrop intrudes into the snow shelf");
        if(!bypass)CheckRenderedCut(race.World,gap);
        float peak=0;bool tapped=false;
        for(int i=0;i<700&&race.Status.phase=="play"&&race.Status.metres<gap.endD+20;i++){
            float line=bypass?gap.bypassLane:gap.lane;
            race.DragSteer(line/(27*.79f));
            if(!bypass&&!tapped&&race.Status.freeRideTrickReady&&race.Status.metres>gap.lipD+5){
                race.BeginTap(1);race.EndTap(1);tapped=true;
            }
            race.Advance(1f/60);peak=Mathf.Max(peak,race.Altitude);
        }
        Need(race.Status.metres>=gap.endD+20&&race.Status.helmets==1&&
            (bypass?peak<.06f&&race.Status.tricks==0:tapped&&peak>1&&race.Status.tricks==1),"blocked crossing "+seed+" bypass "+bypass+" at "+race.Status.metres);
        Debug.Log("WIDE FREE RIDE seed "+seed+" "+gap.lipD.ToString("F1")+".."+gap.endD.ToString("F1")+" shelf lane "+gap.bypassLane.ToString("F1")+
            " "+(bypass?"ground bypass":"air trick")+" helmets "+race.Status.helmets);
    }
    static void AlpineTraffic(MountainRace race,bool board){
        Start(race,"owl",1911,Entry+(board?158:6));
        var npc=race.World.Items.Find(o=>o.kind=="npc"&&o.sb==board&&o.distance>race.Status.metres&&o.distance<race.Status.metres+65);
        Need(npc!=null,"missing moving powder rider");
        float start=npc.distance,low=npc.lane,high=npc.lane,visibleAt=0;
        for(int i=0;i<105;i++){
            race.Advance(1f/60);low=Mathf.Min(low,npc.lane);high=Mathf.Max(high,npc.lane);
            var v=race.View.WorldToViewportPoint(npc.Visual.bounds.center);
            if(v.z>5&&v.x>.025f&&v.x<.975f&&v.y>.025f&&v.y<.975f)visibleAt=race.Status.metres;
        }
        Need(npc.distance-start>8&&high-low>.28f&&visibleAt>0,"powder rider must descend, turn and enter the camera: "+npc.variant);
        Debug.Log("POWDER TRAFFIC "+npc.variant+" visible from "+visibleAt.ToString("F1")+" m, travelled "+(npc.distance-start).ToString("F1")+" m");
        if(board){
            Need(race.World.TryFreeRideGap(Entry+225,out var cut),"missing boarder's jump");
            float peak=0,time=race.Status.elapsed;
            // Keep the streamed actor in view through touchdown. A faster player
            // can otherwise finish the pass and unload the boarder mid-jump.
            for(int i=0;i<500&&npc.distance<cut.endD+4;i++){
                time+=1f/60;
                race.World.Refresh(Mathf.Max(race.Status.metres,npc.distance-12),time,cut.bypassLane,true);
                if(npc.Visual&&cut.OverOpening(npc.distance,npc.lane)){
                    float y=npc.Visual.transform.position.y-race.World.SnowSurface(npc.distance,npc.lane).y;
                    peak=Mathf.Max(peak,y);
                    Need(Mathf.Abs(npc.lane-cut.bypassLane)>3,"boarder blocks the only snow shelf");
                }
            }
            float landed=npc.Visual.transform.position.y-race.World.SnowSurface(npc.distance,npc.lane).y;
            Need(npc.distance>=cut.endD+3&&peak>1&&Mathf.Abs(landed)<.05f,"boarder touchdown "+npc.distance+" peak "+peak+" snow offset "+landed);
        }
    }
    public static void Run(MountainRace race){
        foreach(int seed in new[]{1911,4242}){Transverse(race,seed,false);Transverse(race,seed,true);}
        foreach(int seed in new[]{1911,4242}){Pursuit(race,seed,false);Pursuit(race,seed,true);}
        RollingSnow(race,true);RollingSnow(race,false);
        AlpineTraffic(race,false);AlpineTraffic(race,true);
        FirstCut(race,"owl",1911,Path.Jump);FirstCut(race,"bear",1911,Path.Bypass);
        FirstCut(race,"bear",1911,Path.Miss);FirstCut(race,"owl",1911,Path.NoJump);FirstCut(race,"bear",4242,Path.Jump);
        FirstCut(race,"bear",1911,Path.Jump);FirstCut(race,"bear",1911,Path.NoJump);
        // A second fall has no trial helmet. No pickup or subsequent tick may rescue it.
        Start(race,"bear",1911,Entry+166);
        int deepestFalls=0;bool atDepth=false;
        for(int i=0;i<900&&race.Status.phase=="play";i++){
            float d=race.Status.metres;
            Need(race.World.TryFreeRideGap(d,out var gap),"left the course before the second fall");
            float aim=d>=gap.lipD-32&&d<gap.endD?gap.lane:0;
            if(race.Status.boulderChasing)aim=race.Status.boulderLine>0?-5:5;
            race.DragSteer(aim/(27*.79f));race.Advance(1f/30);
            if(race.Altitude< -4&&!atDepth)deepestFalls++;
            atDepth=race.Altitude< -4;
        }
        Need(deepestFalls==2&&race.Status.phase=="over"&&!race.Status.rankedResult,"second crevasse fall: depth events "+deepestFalls+" at "+race.Status.metres+" "+race.Status.message);
        Start(race,"owl",1911);
        Need(race.World.TryFreeRideGap(Entry+80,out var first),"missing replay");
        Need(Mathf.Abs(first.lipD-Entry-83.81f)<.02f&&Mathf.Abs(first.lane+4.833f)<.02f,"seed 1911 changed");
        float lastCommandLip=-1;int jumps=0;
        for(int i=0;i<5700&&race.Status.phase=="play"&&race.Status.metres<RaceWorld.LapLength+3;i++){
            float d=race.Status.metres;
            if(!race.World.TryFreeRideGap(d,out var gap)){race.DragSteer(0);race.Advance(1f/30);continue;}
            float ahead=gap.lipD-d;
            float target=ahead<40&&d<gap.endD+12?gap.lane:0;
            var rolling=race.World.Items.FirstOrDefault(o=>o.kind=="snowBoulder"&&!o.PursuesRider&&!o.consumed&&o.distance>d-3&&o.distance<d+29);
            if(rolling!=null&&!(ahead<32&&d<gap.endD+10)&&race.World.TryFreeRideGap(rolling.distance-55,out var source))
                target=7.5f*Mathf.Sign(source.lane);
            if(race.Status.boulderChasing&&race.World.TryFreeRideGap(d-55,out var pursuingSection))
                target=Mathf.Clamp(race.Status.boulderLine+4*Mathf.Sign(pursuingSection.lane),-9,9);
            if(ahead>20||d>gap.endD+12){
                var rock=race.World.Items.Where(o=>(o.kind=="rock"||o.kind=="npc")&&o.distance>d-2&&o.distance<d+16&&
                    Mathf.Abs(target-o.lane)<o.width*.5f+1.15f).OrderBy(o=>o.distance).FirstOrDefault();
                if(rock!=null)target=rock.lane+(rock.lane>race.Lane?-1:1)*(rock.width*.5f+1.8f);
            }
            race.DragSteer(target/(27*.79f));
            if(race.Status.freeRideTrickReady&&d>=gap.SectionDistance(2)&&lastCommandLip!=gap.lipD){
                race.AirTrick("tap");lastCommandLip=gap.lipD;jumps++;
            }
            int helmets=race.Status.helmets;race.Advance(1f/30);
            if(helmets!=race.Status.helmets)Debug.Log("Free Ride full route contact at "+d+" lane "+race.Lane+" target "+target+" near "+string.Join(",",race.World.Items.Where(o=>o.judged&&Mathf.Abs(o.distance-d)<3).Select(o=>o.kind+"/"+o.lane))+" "+race.Status.message);
        }
        Need(race.Status.phase=="play"&&race.Status.stage==0&&race.Status.metres>=RaceWorld.LapLength&&!race.Status.rankedResult&&
            jumps==16&&race.Status.tricks==jumps&&race.Status.helmets==1,
            "failed full descent: "+race.Status.phase+" "+race.Status.message+" at "+race.Status.metres+", completed "+race.Status.tricks+"/"+jumps+" tricks; helmets "+race.Status.helmets);
        Debug.Log("FREE RIDE all 16 cornices jumped on command; next ordinary lap");
        Debug.Log("FREE RIDE PASS ordinary menu, both heroes, two seeds, jump/bypass/side entry, 22.5 km lap transition");
        race.Menu();
    }
}
