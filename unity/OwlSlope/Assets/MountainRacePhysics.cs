using UnityEngine;

public partial class MountainRace {
    static readonly float[] StageSpeed={1,.9f,.86f,1,.9f,1.15f,1,1};
    static readonly int[] TrickScores={120,170,170,150,230,100,210,240};
    static readonly float[] TrickTimes={1,1.15f,1.15f,1.05f,1.35f,.8f,1.3f,1.4f};
    static readonly string[] TrickNames={"Вертушка 360","Сальто назад","Сальто вперёд","Бочка","Корк 720","Грэб","Вертолёт 720","Мисти-флип"};
    static float StepBlend(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
    float LaneLimit(float d){
        if(Status.mode=="tubing")return 8.1f+1.6f;
        float local=Mathf.Repeat(d,2500);
        float forest=Status.stage==1?StepBlend(local/35)*StepBlend((2500-local)/35):0;
        return .5f*(21-7.5f*forest)+1.6f;
    }
    float FallSlow(){
        float t=1-fall/FallTime;
        if(t<.4f)return Mathf.Lerp(.6f,.1f,t/.4f);
        return t<.72f?.06f:Mathf.Lerp(.06f,.85f,(t-.72f)/.28f);
    }
    void Step(float dt,float keyboard){
        var s=Status;
        s.elapsed+=dt;
        if(crashTimer>0){
            crashTimer-=dt;speed*=Mathf.Pow(.15f,dt);s.metres+=speed*dt;
            if(crashTimer<=0)Finish(false,s.message);return;
        }
        if(BonusMode)s.elapsed=Mathf.Min(75,s.elapsed);
        if(s.elapsed>noticeUntil)s.message="";
        float d=s.metres,oldLane=lane;
        int stage=Mathf.FloorToInt(d/2500)%8;
        if(stage!=bestStage){bestStage=s.stage=stage;SelectChallenge(stage);Cue("stage");}
        float grade=.29f+.22f*Mathf.Sin(d/42)+.09f*Mathf.Sin(d/17+1);
        baseSpeed=Mathf.Min(230+d*.085f,560)*(.8f+.7f*grade)*StageSpeed[stage]*(1+Mathf.Floor(d/20000)*.08f)*1.1f/20;
        if(stage==2)baseSpeed*=1+.08f*Mathf.Clamp01((lane+.25f)/2.25f);
        if(BonusMode)baseSpeed=(270+80*s.elapsed/75)/20;
        speed=baseSpeed*(boost>0?1.65f:1)*(slow>0?.62f:1)*(fall>0?FallSlow():1);
        float wind=stage==4&&!BonusMode?(Mathf.Sin(s.elapsed*.55f)*75+Mathf.Sin(s.elapsed*1.7f)*25)/20:0;
        float heading=World.HeadingAt(d);
        bool controlled=dragging||keyboard!=0;
        float target=keyboard!=0?lane+keyboard*3.5f:dragTarget;
        if(dragging)dragTarget=Mathf.Clamp(dragTarget+wind*.5f*dt,-LaneLimit(d),LaneLimit(d));
        float rough=stage==2&&!airborne?MogulShape(d,lane):0;
        if(fall>0){velocity=slip=0;travelYaw=heading;}
        else if(controlled){
            float response=(Status.mode=="tubing"?.76f:Hero=="owl"?1.15f:1)*(airborne?.45f:1)*(wobble>0?.5f:1)*(1-.38f*rough);
            float cap=21.5f*Mathf.Sqrt(sensitivity);
            float want=Mathf.Clamp((target-lane)*(3+5*sensitivity),-cap,cap)*response;
            velocity=Mathf.Lerp(velocity,want,dt*(Status.mode=="tubing"?3.4f:5+6*sensitivity));
            travelYaw=heading+Mathf.Atan2(velocity,speed);
            float curve=Mathf.DeltaAngle(World.HeadingAt(d-1)*Mathf.Rad2Deg,World.HeadingAt(d+1)*Mathf.Rad2Deg)*Mathf.Deg2Rad/
                Mathf.Max(1,(World.SampleGround(d+1,0)-World.SampleGround(d-1,0)).magnitude);
            float bank=(World.SampleGround(d,lane+.1f).y-World.SampleGround(d,lane-.1f).y)/.2f;
            slip=airborne?0:Mathf.Lerp(slip,Mathf.Clamp(( -curve*speed*speed-9.81f*bank)*.24f,-1.8f,1.8f),4*dt);
        }else{
            velocity=Status.mode=="tubing"?velocity*Mathf.Exp(-2.4f*dt):0;
            float delta=Mathf.DeltaAngle(heading*Mathf.Rad2Deg,travelYaw*Mathf.Rad2Deg)*Mathf.Deg2Rad;
            slip=Mathf.Clamp(Mathf.Tan(Mathf.Clamp(delta,-.7f,.7f))*speed,-16,16);
        }
        float rut=rough>0?Mathf.Clamp((MogulShape(d,lane-.2f)-MogulShape(d,lane+.2f))*16,-4.3f,4.3f):0;
        lane=Mathf.Clamp(lane+dt*(velocity+slip+wind*.5f+rut),-LaneLimit(d),LaneLimit(d));
        angle=Mathf.Lerp(angle,Mathf.Clamp((velocity+slip*.5f)/21.5f,-1,1),dt*10);
        if(!edgeReturn&&Mathf.Abs(lane)>=LaneLimit(d)-.001f&&fall<=0){
            edgeOrigin=lane;Hit("edge",null);edgeReturn=fall>0;travelYaw=heading;
        }
        if(edgeReturn){float t=Mathf.Clamp01((1-fall/FallTime)*1.7f);lane=Mathf.Lerp(edgeOrigin,0,StepBlend(t));dragTarget=lane;if(t>=1)edgeReturn=false;}
        float next=d+speed*dt;s.metres=Status.mode=="sprint"?Mathf.Min(SprintLength,next):next;
        TickAir(dt);
        if(crashTimer<=0)CheckItems(d,oldLane);
        if(s.phase!="play"||crashTimer>0)return;
        TickTimers(dt);
        if(!BonusMode){Avalanche(dt);TickChallenge();if(s.metres-cleanStart>=1500)Rule(1);}
        if(BonusMode&&s.elapsed>=75)Finish(true,s.mode=="tubing"?"Финиш у бухты!":"Снежная доставка завершена!");
        else if(s.mode=="sprint"&&s.metres>=SprintLength)Finish(true,"Короткий спуск пройден!");
    }
    float MogulShape(float d,float x){
        // Measure local bumps on the generated terrain, removing its base gradient.
        float y=World.SampleGround(d,x).y;
        float chord=.5f*(World.SampleGround(d-1.6f,x).y+World.SampleGround(d+1.6f,x).y);
        return Mathf.Clamp01((y-chord)*1.7f);
    }
    void TickTimers(float dt){
        boost=Mathf.Max(0,boost-dt);magnet=Mathf.Max(0,magnet-dt);slow=Mathf.Max(0,slow-dt);
        wobble=Mathf.Max(0,wobble-dt);fall=Mathf.Max(0,fall-dt);invincible=Mathf.Max(0,invincible-dt);
    }
    void BeginFlight(RaceItem item){
        airborne=true;parkFlight=item.park;trickLine=item.trickLine;prizeLine=item.prizeLine;
        lipHeight=item.height;flightStart=item.distance+item.length*.5f;
        flightSpan=item.span*(Hero=="owl"?1.09f:1);flightLift=item.park?item.lift:2.3f*Mathf.Sqrt(Mathf.Max(.1f,lipHeight)/.75f);
        airMax=parkFlight?flightSpan/Mathf.Max(1,speed):(Hero=="owl"?1.45f:.85f)*(1+(speed*20-245)/1400)*(.75f+.25f*flightLift/2.3f);
        airTime=Mathf.Max(0,(Status.metres-flightStart)/Mathf.Max(1,speed));trickProgress=-1;trickAt=-1;flightTricks=0;
        Notice(prizeLine?"Два трюка за полёт":trickLine?"Тап в воздухе — грэб":"Прыжок!","jump");
    }
    float FlightHeight(float d){
        float t=parkFlight?Mathf.Clamp01((d-flightStart)/Mathf.Max(.01f,flightSpan)):Mathf.Clamp01(airTime/Mathf.Max(.01f,airMax));
        if(!parkFlight)return lipHeight*(1-t)+flightLift*Mathf.Sin(Mathf.PI*t);
        float rise=trickAt<0?0:StepBlend((d-trickAt)/Mathf.Max(.1f,flightSpan*.25f));
        return (1-t)*(lipHeight+4*flightLift*t)+2.7f*Mathf.Sin(Mathf.PI*t)*rise;
    }
    public void AirTrick(string gesture){
        if(!airborne||Status.phase!="play"||trickProgress>=0||Status.mode=="tubing"||Status.mode=="sled")return;
        int type;
        switch(gesture){
          case "up":type=1;break;case "big-up":type=7;break;case "down":type=5;break;case "big-down":type=2;break;
          case "left":type=3;break;case "big-left":type=4;break;case "right":type=0;break;case "big-right":type=6;break;
          default:type=5;break;
        }
        float duration=Hero=="bear"?.42f:.5f;
        if(airMax-airTime<duration*TrickTimes[type]*.8f)type=5;
        if(airMax-airTime<duration*TrickTimes[type]*.8f)return;
        trickType=type;poseStyle=type==5?flightTricks%2:-1;
        float minimum=duration*TrickTimes[type];
        // Small kickers still accept a quick gesture. On longer flights the rider
        // can extend and hold the pose, then unwind with room to absorb the landing.
        float preferred=type==5?(Hero=="bear"?.76f:.82f):minimum*1.45f;
        trickLength=Mathf.Clamp(airMax-airTime-.13f,minimum,preferred);
        trickProgress=0;
        if(trickAt<0&&trickLine)trickAt=Status.metres;
        string name=poseStyle<0?TrickNames[type]:Hero=="bear"?(poseStyle==0?"Shifty":"Spread eagle"):(poseStyle==0?"Indy":"Melon");
        Notice(name,"trick");
    }
    void TickAir(float dt){
        if(!airborne)return;
        if(parkFlight){airMax=flightSpan/Mathf.Max(speed,1);airTime=airMax*Mathf.Clamp01((Status.metres-flightStart)/flightSpan);}
        else airTime+=dt;
        if(trickProgress>=0){
            trickProgress+=dt/trickLength;
            if(trickProgress>=1)AwardTrick(false);
        }
        if(airTime+.00001f<airMax)return;
        if(trickProgress>=.7f)AwardTrick(true);
        else if(trickProgress>=0){Status.combo=0;wobble=.6f;slow=.5f;Notice("Не докрутил — серия сброшена","warn");}
        else {Status.combo+=flightTricks;Status.maxCombo=Mathf.Max(Status.maxCombo,Status.combo);Cue("land");}
        airborne=parkFlight=false;trickProgress=-1;lastLanding=Status.elapsed;
    }
    void AwardTrick(bool landing){
        Status.tricks++;flightTricks++;trickProgress=-1;
        bonusPoints+=Mathf.RoundToInt(TrickScores[trickType]*Mult*(Status.stage==3?2:1)*(!landing&&flightTricks>1?1.5f:1));
        Cue("trickDone");
    }
    void Hit(string kind,RaceItem item){
        if(Review||fall>0||crashTimer>0)return;
        challengeClean=cleanStart=Status.metres;
        if(invincible>0&&kind!="edge"){
            if(item!=null)item.Consume();Cue("smash");return;
        }
        if(Status.helmets>0){
            Status.helmets--;fall=FallTime;fallSide=random.Next(2)==0?-1:1;invincible=FallTime+1.1f;airborne=parkFlight=false;
            trickProgress=-1;flightTricks=0;dragTarget=lane;
            if(item!=null)item.Consume();Notice("Каска спасла! Осталось: "+Status.helmets,"helmet");return;
        }
        crashTimer=1.5f;airborne=false;trickProgress=-1;fallSide=1;
        string msg;
        switch(kind){
            case "edge":msg="Снежный борт! Рули на поворотах";break;case "rock":msg="Камень на трассе";break;
            case "avalanche":msg="Лавина догнала";break;case "cat":msg="Столкновение с ратраком";break;
            case "npc":msg="Держи дистанцию при обгоне";break;case "tube":msg="Плюшка пересекла твою линию";break;
            case "tree":msg="Столкновение с елью";break;case "mogulYeti":msg="Йети пересёк трассу";break;
            case "snowmobile":msg="Столкновение со снегоходом";break;case "snowman":msg="Столкновение со снеговиком";break;
            default:msg="Столкновение на трассе";break;
        }
        Notice(msg,"crash");
    }
    void Avalanche(float dt){
        if(chaseLeft<=0){
            if(Status.metres<chaseNext)return;
            chaseLeft=20;chaseDistance=Status.metres-28;chaseNext=Status.metres+1300+random.Next(600);Notice("Лавина! Держи скорость","warn");
        }
        chaseLeft-=dt;chaseDistance+=baseSpeed*.94f*dt;
        Status.avalanche=Mathf.Clamp01(1-(Status.metres-chaseDistance)/28);
        if(Status.metres-chaseDistance<.75f&&fall<=0){Hit("avalanche",null);chaseDistance=Status.metres-21;}
        if(chaseLeft<=0&&crashTimer<=0){bonusPoints+=600*Mult;Status.avalanche=0;Notice("Ушёл от лавины!","chime");}
    }
    void SelectChallenge(int stage){
        if(random==null)return;
        challenge=Status.mode=="sprint"?1:random.Next(5);consecutiveGates=0;challengeDone=false;challengeClean=Status.metres;
        int[] needs={25,6,500,4,3};challengeNeed=Mathf.RoundToInt(needs[challenge]*(1+.12f*stage));challengeBaseline=Progress();
    }
    int Progress(){
        switch(challenge){case 0:return Status.coins;case 1:return consecutiveGates;
            case 2:return Mathf.FloorToInt(Status.metres-challengeClean);case 3:return Status.tricks;default:return Status.polite;}
    }
    void TickChallenge(){
        if(challengeDone||Progress()-challengeBaseline<challengeNeed)return;
        challengeDone=true;bonusPoints+=(300+100*Status.stage)*Mult;Notice("Испытание этапа пройдено!","chime");
    }
}
