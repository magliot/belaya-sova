using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The driver uses the ordinary steering input. Spread the run over editor frames
// so the engine releases transient meshes and effects exactly as during play.
[InitializeOnLoad]
public static class CourseRideChecks {
    const string pending="Owl.CourseRide";
    static MountainRace race;
    static float target,velocity,lastLane,lapApproach;
    static int stageSeen,lap;
    static readonly List<Obstacle> obstacles=new List<Obstacle>();
    static readonly List<Vector2> rewards=new List<Vector2>();
    struct Obstacle {public float lane,seconds,gap;}
    static CourseRideChecks(){EditorApplication.update+=Update;}
    public static void Run(){
        SessionState.SetBool(pending,true);
        EditorSceneManager.OpenScene("Assets/Mountain.unity");EditorApplication.EnterPlaymode();
    }
    public static void Start(MountainRace game){
        race=game;race.enabled=false;
        race.Menu();race.ChooseHero("bear");race.StartRace("{\"mode\":\"normal\",\"seed\":1911,\"sensitivity\":4,\"helmets\":1}");
        race.BeginSteer();target=velocity=lastLane=lapApproach=0;stageSeen=lap=0;motion.Clear();
    }
    static void Update(){
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        if(SessionState.GetBool(pending,false)){
            var game=UnityEngine.Object.FindFirstObjectByType<MountainRace>();
            if(!game||!game.View)return;
            SessionState.EraseBool(pending);Start(game);
        }
        if(!race)return;
        try{
            for(int i=0;i<64;i++){
                var s=race.Status;
                if(s.phase!="play"){
                    if(s.metres<RaceWorld.LapLength+100||stageSeen!=(1<<RaceWorld.StageCount)-1||!s.rankedResult)
                        throw new InvalidOperationException("Continuous run ended early at "+s.metres+" m, stage "+s.stage+" lane "+race.Lane+" · "+s.message);
                    Debug.Log("CONTINUOUS LAP PASS seed 1911, ordinary start: "+s.metres+" m, nine stages, lap "+lap+
                        ", "+s.coins+" mandarins, "+s.gates+" gates, "+s.rings+" rings, "+
                        s.score+" points = "+s.scoreDistance+" distance + "+s.scoreFruit+" fruit + "+s.scoreGates+" gates + "+
                        s.scoreRings+" rings + "+s.scoreFis+" FIS, finish: "+s.message);
                    race=null;EditorApplication.Exit(0);return;
                }
                if((stageSeen&(1<<s.stage))==0)Debug.Log("COURSE RIDE stage "+s.stage+" at "+s.metres+" m, "+s.coins+" mandarins");
                if(s.metres<RaceWorld.LapLength)lapApproach=s.speed;
                if(lap==1&&s.metres>=RaceWorld.LapLength){
                    if(Mathf.Abs(s.speed-lapApproach)>8)throw new InvalidOperationException("Discontinuous speed at the lap boundary");
                    Debug.Log( "COURSE RIDE crossed "+RaceWorld.LapLength+" m at "+lapApproach+" -> "+s.speed+" km/h");
                }
                stageSeen|=1<<s.stage;lap=1+Mathf.FloorToInt(s.metres/RaceWorld.LapLength);
                bool alpine=race.World.TryFreeRideGap(s.metres,out var gap);
                int rate=alpine?30:20;
                velocity=(race.Lane-lastLane)*rate;lastLane=race.Lane;
                if(alpine){
                    target=JumpRoute(race,target,velocity,gap);
                    if(s.freeRideTrickReady&&s.metres>=gap.SectionDistance(2)){race.BeginTap(1);race.EndTap(1);}
                }else target=s.metres>=RaceWorld.LapLength+100?20:SelectLane(race,target,velocity,rate);
                int helmets=s.helmets;bool wasRiding=s.phase=="play";
                race.DragSteer(target/(27*.79f));race.Advance(1f/rate);
                if(helmets>s.helmets||wasRiding&&s.phase=="over")Debug.Log("COURSE CONTACT "+s.metres+" x "+race.Lane+" target "+target+" "+s.message);
            }
        }catch(Exception error){race=null;Debug.LogException(error);EditorApplication.Exit(2);}
    }
    internal static float JumpRoute(MountainRace race,float target,float velocity,RaceWorld.FreeRideGap gap){
        float d=race.Status.metres,preferred=gap.lane;
        if(d>gap.endD+8&&race.World.TryFreeRideGap(d+85,out var next))preferred=next.lane;
        float planned=SelectLane(race,target,velocity,30,preferred);
        return d>=gap.lipD-17&&d<=gap.endD+4?gap.lane:planned;
    }
    static float SelectLane(MountainRace race,float target,float velocity,float sampleRate,float? jumpLine=null){
        var s=race.Status;float d=s.metres,lane=race.Lane,speed=Mathf.Max(10,s.speed/3.6f);
        float width=race.World.WidthAt(d+speed)*.5f-.35f;
        obstacles.Clear();rewards.Clear();
        if(s.boulderChasing)ObstacleAt(s.boulderLine,1.0f,2.45f);
        if(!jumpLine.HasValue&&race.World.TryFreeRideGap(d,out var cut)&&d>cut.lipD-45&&d<cut.endD){
            for(int knot=1;knot<RaceWorld.FreeRideGap.ShapeSegments;knot++){
                float at=cut.SectionDistance(knot);
                if(at<d-1)continue;
                var banks=cut.CrossSection(at);
                ObstacleAt((banks.x+banks.y)*.5f,(at-d)/speed,(banks.y-banks.x)*.5f+.55f);
            }
        }
        foreach(var item in race.World.Items){
            float dz=item.distance-d;
            if(item.consumed||dz< -8||dz>speed*2.15f+5)continue;
            var pos=item.Visual;
            if(item.kind=="coin"||item.kind=="helmet"){
                if(dz>2&&dz<speed*1.05f)rewards.Add(new Vector2(item.lane,item.kind=="helmet"&&s.helmets==0?4.5f:.19f));
                continue;
            }
            float radius,length=.8f;
            switch(item.kind){
                case "gate":
                    if(dz>2&&dz<speed)rewards.Add(new Vector2(item.lane,.15f)); // Markers yield. Solid stands have their own physical objects.
                    continue;
                case "npc":radius=1.44f;break;
                case "tree":radius=item.width*.5f+.75f;break;
                case "snowBoulder":radius=item.width*.5f+1;length=2;break;
                case "rock":radius=item.width*.5f+.72f;break;
                case "cat":radius=3.3f;length=3.7f;break;
                case "mogulYeti":case "yeti":radius=2.85f;length=2.05f;break;
                case "snowmobile":radius=2.30f;length=2.15f;break;
                case "dog":radius=1.18f;break;
                case "tube":radius=1.65f;length=1.65f;break;
                case "cannon":case "fence":radius=item.width*.5f+.88f;break;
                case "festivalFountain":radius=1.32f;break;
                case "snowman":radius=1.51f;break;
                default:continue;
            }
            if(!pos||dz< -length-1)continue;
            // Predict from the last two visible positions. At the far end of the
            // horizon curved NPC tracks are deliberately given extra clearance.
            float t=dz/speed,predicted=item.lane;
            if(motion.TryGetValue(item,out var old)){
                float vx=(item.lane-old.x)*sampleRate,vz=(item.distance-old.y)*sampleRate;
                t=dz/Mathf.Max(5,speed-vz);
                predicted+=Mathf.Clamp(vx,-8,8)*Mathf.Clamp(t,0,.75f);
                radius+=Mathf.Min(.6f,Mathf.Abs(vx)*Mathf.Clamp01(t)*.25f);
            }
            ObstacleAt(predicted,t,radius);
            if(length>1.5f)ObstacleAt(predicted,t+length/speed,radius);
        }
        // Only retain the streamed objects for the next control tick.
        motion.Clear();
        foreach(var item in race.World.Items)motion[item]=new Vector2(item.lane,item.distance);
        float best=float.PositiveInfinity,choice=lane;
        for(float x=-width;x<=width+.01f;x+=.32f){
            float cost=.15f*Mathf.Abs(x-lane)+.15f*Mathf.Abs(x-target)+.035f*Mathf.Abs(x);
            if(jumpLine.HasValue)cost+=2.5f*Mathf.Abs(x-jumpLine.Value);
            foreach(var o in obstacles){
                float t=Mathf.Max(.035f,o.seconds);
                float fraction=1-Mathf.Exp(-4.9f*t);
                float reach=Mathf.Max(0,t-.04f)*15f;
                float travelled=Mathf.Clamp((x-lane)*fraction,-reach,reach);
                float predicted=lane+travelled+velocity*t*Mathf.Exp(-7*t);
                float clearance=Mathf.Abs(predicted-o.lane)-o.gap;
                if(!jumpLine.HasValue)clearance+=Mathf.Clamp((t-.63f)*6.8f,0,4.2f);
                float danger=Mathf.Max(0,1.10f-clearance);
                cost+=danger*danger*65/(.33f+t*t);
            }
            foreach(var reward in rewards)cost-=reward.y*Mathf.Max(0,1.3f-Mathf.Abs(reward.x-x));
            if(cost<best){best=cost;choice=x;}
        }
        return choice;
    }
    static readonly Dictionary<RaceItem,Vector2> motion=new Dictionary<RaceItem,Vector2>();
    static void ObstacleAt(float x,float seconds,float gap){
        if(seconds>1.85f)return;
        obstacles.Add(new Obstacle{lane=x,seconds=seconds,gap=gap});
    }
}
