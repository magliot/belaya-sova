using System;
using System.Collections.Generic;
using UnityEngine;

// Distances and lanes are metres. The archived simulation uses 20 units per metre.
[DisallowMultipleComponent]
public partial class MountainRace : MonoBehaviour {
    [Serializable] public class Settings {
        public string mode="normal",reviewRide="normal";
        public string equipment="standard";
        public bool ownedEquipment;
        public int tuning;
        public int seed=1911, sensitivity=4, helmets, boost, mag, collectedLetters;
        public bool showFisHints;
    }
    [Serializable] public class State {
        public string phase="menu",hero="bear",mode="normal",courseMode="normal",message="",cue="",nextLetter="";
        public int seed,score,coins,helmets,stage,gates,tricks,rings,combo,maxCombo,polite,gifts,snowmen,
            feathers,encounters,letters,lettersTotal,speed,multiplier=1,eventId,grinds,horns;
        public float metres,elapsed,engineNear,enginePan,avalanche,blizzard,edge,boostSeconds,magnetSeconds,chaseSeconds,chaseGap;
        public int letterSide,bonusLocation;
        public float riderLane;
        public int scoreDistance,scoreFruit,scoreGates,scoreRings,scoreFis;
        public bool rankedResult;
        public bool air,boost,feather,goldenOwl,tubingUnlocked,sledUnlocked;
        public int[] fis=Array.Empty<int>();
        public int[] fisContexts=Array.Empty<int>();
        public string fisAction="";
        public bool fisCanLeave;
    }
    public State Status {get;private set;}=new State();
    public bool IsMenu=>Status.phase=="menu";
    public string Hero=>Status.hero;
    public Transform MenuActor=>menuModel;
    public float Lane=>lane;
    public float Altitude=>airborne?FlightHeight(Status.metres):rampHeight;
    public RaceWorld World {get;private set;}
    public Camera View {get;private set;}
    Settings settings=new Settings();
    System.Random random;
    readonly List<int> earnedRules=new List<int>(10);
    int bonusPoints, serial;
    bool airborne,dragging,edgeReturn;
    float lane,velocity,slip,travelYaw,dragStart,dragTarget,angle,sensitivity;
    float speed,baseSpeed,boost,magnet,slow,wobble,fall,fallSide,edgeOrigin,previousAltitude;
    float airTime,airMax,lipHeight,flightStart,flightSpan,flightLift,trickProgress,trickLength;
    bool parkFlight,trickLine,prizeLine;
    int flightTricks,trickType,poseStyle;
    float rampHeight,rampPhase,lastLanding,crashTimer,cleanStart,lastClose;
    float chaseNext,chaseDistance,chaseLeft,noticeUntil,lastReward;
    float reviewRate, reviewLane,nextHud;
    string reviewEffect="normal";
    int rewardChain,challenge,challengeNeed,challengeBaseline,consecutiveGates,bestStage;
    bool challengeDone;
    float challengeClean;
    string CourseMode=>Review?settings.reviewRide:Status.mode=="free"?"normal":Status.mode;
    bool BonusMode=>CourseMode=="sled"||CourseMode=="tubing";
    const float BonusSeconds=75, BonusBoost=1.22f;
    float BonusLaunch=>CourseMode=="sled"?22:20;
    float BonusGain=>CourseMode=="sled"?10:8;
    float BonusSpeed(float elapsed)=>BonusLaunch+BonusGain*Mathf.Clamp01(elapsed/BonusSeconds);
    bool Review=>Status.mode=="review";
    int Mult=>Mathf.Min(10,1+Status.combo/5);
    const float FallTime=1.7f;
    const int SprintLength=1200;
    static readonly string[] Words={"EMCO","СОВА","ШАХТЕРСК"};
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void MountainState(string json);
#endif

    void Start(){Initialize();}
    public void Initialize(){
        if(World)return;
#if UNITY_WEBGL && !UNITY_EDITOR
        // The HTML overlay owns keyboard focus while a settings or course-inspection field is edited.
        WebGLInput.captureAllKeyboardInput=false;
#endif
        World=GetComponent<RaceWorld>();
        if(!World)World=gameObject.AddComponent<RaceWorld>();
        SetupCamera();Menu();
        Debug.Log("MOUNTAIN RACE READY");
    }
    public void ChooseHero(string selected){
        if((selected!="owl"&&selected!="bear")||(!IsMenu&&!Review))return;
        Status.hero=selected;
        if(!World)return;
        PrepareActors();RenderRace(0,true);Publish();
    }
    public void Menu(){
        ShowFisWalker(false);
        dragging=false;reviewRate=0;airborne=false;
        Status=new State{hero=Hero,phase="menu"};
        if(!World)return;
        lane=angle=fall=0;World.Configure(1911,"normal");World.Refresh(0,0);
        PrepareActors();RenderRace(0,true);Publish();
    }
    public void StartRace(string json){
        if(!World)Initialize();
        var selected=String.IsNullOrEmpty(json)?new Settings():JsonUtility.FromJson<Settings>(json);
        if(selected==null)return;
        switch(selected.mode){case "normal":case "free":case "sprint":case "tubing":case "sled":case "review":break;default:selected.mode="normal";break;}
        switch(selected.reviewRide){case "sprint":case "tubing":case "sled":break;default:selected.reviewRide="normal";break;}
        settings=selected;ResetRun();
    }
    public void RestartRide(){ResetRun();}
    void ResetRun(){
        string hero=Hero;
        Status=new State{hero=hero,mode=settings.mode,seed=settings.seed,
            phase=settings.mode=="review"?"review":"play",lettersTotal=Mathf.Clamp(settings.collectedLetters,0,16)};
        Status.helmets=(Status.mode=="free"||Status.mode=="sprint")&&settings.helmets>0?1:0;
        helmetsIssued=Status.helmets;
        random=new System.Random(settings.seed);World.Configure(settings.seed,CourseMode);
        sensitivity=.35f+.11f*Mathf.Clamp(settings.sensitivity,1,10);
        lane=velocity=slip=angle=dragStart=dragTarget=0;edgeReturn=dragging=airborne=false;
        speed=baseSpeed=BonusMode?BonusLaunch:12;travelYaw=World.HeadingAt(0);boost=magnet=slow=wobble=fall=0;
        rampHeight=rampPhase=airTime=flightSpan=crashTimer=cleanStart=reviewRate=reviewLane=noticeUntil=0;
        parkFlight=trickLine=prizeLine=false;trickProgress=-1;lastLanding=-10;lastClose=-10;
        earnedRules.Clear();bonusPoints=flightTricks=rewardChain=consecutiveGates=0;lastReward=-100;
        ResetFis();
        chaseLeft=0;chaseNext=900;bestStage=0;nextHud=0;reviewEffect="normal";SelectChallenge(0);
        PrepareActors();ScheduleLetter(0,false);World.Refresh(0,0);RenderRace(0,true);
        Cue("start");Publish();
    }
    public void BeginSteer(){if(Status.phase=="play"){dragging=true;dragTarget=dragStart=lane;}}
    public void DragSteer(float delta){if(dragging)dragTarget=Mathf.Clamp(dragStart+delta*27*sensitivity,-LaneLimit(Status.metres),LaneLimit(Status.metres));}
    public void ReleaseSteer(){dragging=false;}
    public void PauseRide(){if(Status.phase=="play"){Status.phase="pause";ReleaseSteer();flakes.Pause();fireworks.Pause();World.PauseCannons(true);Publish();}else if(Review)ReviewRate(0);}
    public void ResumeRide(){if(Status.phase=="pause"){Status.phase="play";flakes.Play();fireworks.Play();World.PauseCannons(false);Publish();}}
    public void EndRide(){
        if(Status.phase!="play"&&Status.phase!="pause")return;
        if(Status.metres<30){Menu();return;}
        Finish(false,"Заезд завершён");
    }
    public void Seek(float metres){
        if(!Review)return;
        float end=BonusMode?Mathf.Ceil((BonusLaunch+.5f*BonusGain)*BonusSeconds*BonusBoost):CourseMode=="sprint"?1199:79999;
        Status.metres=Mathf.Clamp(metres,0,end);Status.elapsed=Status.metres/14;
        if(BonusMode){
            float accel=BonusGain/BonusSeconds;
            // The inspector spans the entire boost-reachable course. A practice ride from
            // the extra distance must still have time left before the 75-second finish.
            float nominal=Status.metres/BonusBoost;
            Status.elapsed=Mathf.Min(BonusSeconds,(Mathf.Sqrt(BonusLaunch*BonusLaunch+2*accel*nominal)-BonusLaunch)/accel);
            speed=BonusSpeed(Status.elapsed)*BonusBoost;
        }
        lane=reviewLane;angle=0;rampHeight=0;airborne=false;travelYaw=World.HeadingAt(Status.metres);
        Status.stage=BonusMode?0:Mathf.FloorToInt(Status.metres/2500)%8;
        if(Status.stage==3)speed=ParkPace(Status.metres);
        World.SetThrowTarget(lane,14,0,0);
        World.Refresh(Status.metres,Status.elapsed);ReviewContact();
        boost=reviewEffect=="coffee"?5:0;Status.avalanche=reviewEffect=="avalanche"?.85f:0;
        if(reviewEffect=="jump"){
            airborne=true;parkFlight=false;lipHeight=.9f;flightLift=2.15f;
            airMax=1.35f;airTime=airMax*(.1f+.8f*Mathf.PingPong(Status.elapsed*.8f,1));
        }
        RenderRace(0,true);Publish();
    }
    public void ReviewEffect(string effect){if(Review){reviewEffect=effect;Seek(Status.metres);}}
    public void ReviewRate(float value){if(Review)reviewRate=Mathf.Clamp(value,0,6);}
    public void ReviewStep(){if(Review)Seek(Status.metres+(BonusMode?speed:14)/30);}
    public void ReviewLane(float value){if(Review){reviewLane=Mathf.Clamp(value,-10,10);Seek(Status.metres);}}
    public void RideFromReview(){
        if(!Review||Status.phase!="review")return;
        reviewEffect="normal";Seek(Status.metres);
        Status.mode=settings.reviewRide;Status.phase="play";
        Status.helmets=BonusMode?0:1;
        helmetsIssued=Status.helmets;
        cleanStart=Status.metres;chaseNext=cleanStart+900;
        bestStage=Status.stage;SelectChallenge(bestStage);
        travelYaw=World.HeadingAt(cleanStart);nextHud=0;
        ScheduleLetter(cleanStart,false);PrepareActors();RenderRace(0,true);Publish();
    }
    public void SetSensitivity(float value){sensitivity=.35f+.11f*Mathf.Clamp(value,1,10);}
    void OnApplicationPause(bool paused){if(paused)PauseRide();}
    void Update(){
        if(!World)return;
        if(IsMenu){NativeMenuPointer();RenderMenu(Time.deltaTime);return;}
        if(Review){if(reviewRate>0)Seek(Status.metres+Time.deltaTime*(BonusMode?speed:14)*reviewRate);return;}
        if(Input.GetKeyDown(KeyCode.P)||Input.GetKeyDown(KeyCode.Escape)){if(Status.phase=="pause")ResumeRide();else PauseRide();}
        if(Status.phase!="play")return;
#if !UNITY_WEBGL || UNITY_EDITOR
        NativePointer();
#endif
        if(Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.UpArrow))AirTrick("tap");
        float key=(Input.GetKey(KeyCode.RightArrow)||Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.LeftArrow)||Input.GetKey(KeyCode.A)?1:0);
        Advance(Mathf.Min(.20f,Time.deltaTime),key);
    }
    // Public for editor simulation: the same fixed substeps run in the phone player.
    public void Advance(float elapsed,float keyboard=0){
        if(Status.phase!="play"||elapsed<=0)return;
        float left=elapsed;
        while(left>.00001f&&Status.phase=="play"){
            float dt=Mathf.Min(left,1f/60);left-=dt;Step(dt,keyboard,left<=.00001f);
        }
        StepBursts(elapsed);RenderRace(elapsed,false);
        if(Status.phase=="play"&&Status.elapsed>=nextHud){Publish();nextHud=Status.elapsed+.10f;}
    }
    void Cue(string cue){Status.cue=cue;Status.eventId=++serial;}
    void Notice(string text,string cue=""){
        Status.message=text;noticeUntil=Status.elapsed+1.8f;
        if(cue.Length>0)Cue(cue);nextHud=0;
    }
    void Publish(){
        RefreshFisAction();
        Status.scoreDistance=Mathf.FloorToInt(Status.metres);
        Status.scoreFruit=3*Status.coins;
        Status.scoreGates=8*Status.gates;
        Status.scoreRings=14*Status.rings;
        Status.scoreFis=25*Status.fis.Length;
        Status.score=CourseMode=="normal"?Status.scoreDistance+Status.scoreFruit+Status.scoreGates+Status.scoreRings+Status.scoreFis:Status.scoreDistance+bonusPoints;
        Status.speed=Mathf.RoundToInt(speed*3.6f);
        Status.air=airborne;Status.boost=boost>0;Status.edge=angle;Status.riderLane=lane;
        Status.courseMode=CourseMode;
        Status.bonusLocation=Status.metres<580?0:Status.metres<1190?1:2;
        Status.boostSeconds=Mathf.Max(0,boost);Status.magnetSeconds=Mathf.Max(0,magnet);
        Status.chaseSeconds=Mathf.Max(0,chaseLeft);Status.chaseGap=Mathf.Max(0,Status.metres-chaseDistance);
        Status.nextLetter="";Status.letterSide=0;
        if(!IsMenu && World) foreach(var item in World.Items){
            if(item.kind!="letter"||item.consumed||item.distance<=Status.metres||item.distance>=Status.metres+75)continue;
            Status.nextLetter=item.character;
            Status.letterSide=item.lane<lane?-1:1;
            break;
        }
        Status.multiplier=BonusMode?Mathf.Min(4,1+(Status.elapsed-lastReward<=3?rewardChain:0)/5):Mult;
        Status.tubingUnlocked=Status.feather;Status.sledUnlocked=Status.feather||Status.goldenOwl;
#if UNITY_WEBGL && !UNITY_EDITOR
        MountainState(JsonUtility.ToJson(Status));
#endif
    }
    void Finish(bool completed,string description){
        dragging=false;Status.phase=completed?"finish":"over";Status.message=description;Cue(completed?"finish":"crash");Publish();
    }
#if !UNITY_WEBGL || UNITY_EDITOR
    Vector2 mouseOrigin;float mouseTime;
    void NativePointer(){
        Vector2 position=Input.mousePosition;
        if(Input.GetMouseButtonDown(0)){mouseOrigin=position;mouseTime=Time.unscaledTime;BeginSteer();}
        if(Input.GetMouseButton(0))DragSteer((position.x-mouseOrigin.x)/Screen.width);
        if(!Input.GetMouseButtonUp(0))return;
        Vector2 delta=position-mouseOrigin;ReleaseSteer();
        if(delta.magnitude>42){
            string dir=Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?(delta.x<0?"left":"right"):(delta.y<0?"down":"up");
            AirTrick((delta.magnitude>110?"big-":"")+dir);
        }else if(Time.unscaledTime-mouseTime<.3f&&delta.magnitude<20)AirTrick("tap");
    }
#endif
}
