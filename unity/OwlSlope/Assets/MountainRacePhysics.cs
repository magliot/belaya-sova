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
        if(BonusMode)s.elapsed=Mathf.Min(BonusSeconds,s.elapsed);
        if(s.elapsed>noticeUntil)s.message="";
        float d=s.metres,oldLane=lane;
        int stage=BonusMode?0:Mathf.FloorToInt(d/2500)%8;
        if(stage!=bestStage){bestStage=s.stage=stage;SelectChallenge(stage);Cue("stage");}
        float grade=.29f+.22f*Mathf.Sin(d/42)+.09f*Mathf.Sin(d/17+1);
        baseSpeed=Mathf.Min(230+d*.085f,560)*(.8f+.7f*grade)*StageSpeed[stage]*(d>=20000?1.08f:1)*1.1f/20;
        float offTrail=stage==2&&!airborne?World.MogulRoughness(d,lane):0;
        if(stage==2)baseSpeed*=1-.13f*offTrail;
        if(BonusMode)baseSpeed=BonusSpeed(s.elapsed);
        bool snowpark=stage==3&&!BonusMode;
        if(snowpark)baseSpeed=Mathf.Min(baseSpeed,20+4*grade);
        float targetSpeed=baseSpeed*(boost>0?(BonusMode?BonusBoost:1.65f):1)*(slow>0?.62f:1)*(fall>0?FallSlow():1);
        if(snowpark){
            // The final metres up the kicker load the legs and trade some speed for height.
            targetSpeed=Mathf.Min(targetSpeed,26)*(1-.13f*StepBlend(rampPhase));
            speed=Mathf.MoveTowards(speed,targetSpeed,(speed>targetSpeed?9:6)*dt);
        }else speed=targetSpeed;
        if(airborne&&parkFlight){
            float u=Mathf.Clamp01((airTime+.5f*dt)/airMax);
            speed=flightSpan/airMax*(1+ParkGlide*Mathf.Cos(2*Mathf.PI*u));
        }
        float wind=stage==4&&!BonusMode?(Mathf.Sin(s.elapsed*.55f)*75+Mathf.Sin(s.elapsed*1.7f)*25)/20:0;
        float heading=World.HeadingAt(d);
        bool controlled=dragging||keyboard!=0;
        float target=keyboard!=0?lane+keyboard*3.5f:dragTarget;
        if(dragging)dragTarget=Mathf.Clamp(dragTarget+wind*.5f*dt,-LaneLimit(d),LaneLimit(d));
        float rough=offTrail>0?offTrail*(.35f+.65f*MogulShape(d,lane)):0;
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
        float rut=rough>0?rough*(2.7f*Mathf.Sin(.39f*d+lane)+1.35f*Mathf.Sin(.73f*d-.67f*lane)):0;
        lane=Mathf.Clamp(lane+dt*(velocity+slip+wind*.5f+rut),-LaneLimit(d),LaneLimit(d));
        angle=Mathf.Lerp(angle,Mathf.Clamp((velocity+slip*.5f)/21.5f,-1,1),dt*10);
        if(!edgeReturn&&Mathf.Abs(lane)>=LaneLimit(d)-.001f&&fall<=0){
            edgeOrigin=lane;Hit("edge",null);edgeReturn=fall>0;if(!BonusMode)travelYaw=heading;
        }
        if(edgeReturn){float t=Mathf.Clamp01((1-fall/FallTime)*1.7f);lane=Mathf.Lerp(edgeOrigin,0,StepBlend(t));dragTarget=lane;if(t>=1)edgeReturn=false;}
        float next=d+speed*dt;
        if(airborne&&parkFlight){
            float u=Mathf.Min(1,(airTime+dt)/airMax);
            // Integrate the horizontal velocity exactly, including any remainder past landing.
            next=flightStart+flightSpan*(u+ParkGlide*Mathf.Sin(2*Mathf.PI*u)/(2*Mathf.PI))+
                speed*Mathf.Max(0,airTime+dt-airMax);
        }
        s.metres=Status.mode=="sprint"?Mathf.Min(SprintLength,next):next;
        TickAir(dt);
        if(crashTimer<=0)CheckItems(d,oldLane);
        if(s.phase!="play"||crashTimer>0)return;
        TickTimers(dt);
        if(!BonusMode){Avalanche(dt);TickChallenge();if(s.metres-cleanStart>=1500)Rule(1);}
        if(BonusMode&&s.elapsed>=BonusSeconds)Finish(true,s.mode=="tubing"?"Финиш у бухты!":"Снежная доставка завершена!");
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
    // Entry and exit have the same longitudinal speed; the crest has about 23% less.
    const float ParkGlide=.13f;
    void BeginFlight(RaceItem item){
        airborne=true;parkFlight=item.park;trickLine=item.trickLine;prizeLine=item.prizeLine;
        lipHeight=item.height;flightStart=item.distance+item.length*.5f;
        flightSpan=item.span*(Hero=="owl"?1.09f:1);flightLift=item.park?item.lift:2.3f*Mathf.Sqrt(Mathf.Max(.1f,lipHeight)/.75f);
        airMax=parkFlight?flightSpan*(1+ParkGlide)/Mathf.Max(1,speed):(Hero=="owl"?1.45f:.85f)*(1+(speed*20-245)/1400)*(.75f+.25f*flightLift/2.3f);
        airTime=Mathf.Max(0,(Status.metres-flightStart)/Mathf.Max(1,speed));trickProgress=-1;trickAt=-1;flightTricks=0;
        Notice(prizeLine?"Два трюка за полёт":trickLine?"Тап в воздухе — оборот":"Прыжок!","jump");
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
          default:type=Hero=="owl"&&flightTricks%2==1?1:0;break;
        }
        float duration=Hero=="bear"?.42f:.5f;
        float minimum=duration*TrickTimes[type];
        if(airMax-airTime<minimum+.025f)return;
        trickType=type;
        bool spread=gesture=="tap"&&Hero=="bear"&&flightTricks%2==1;
        poseStyle=spread?1:type==5?flightTricks%2:-1;
        float preferred=type==5?.8f:TrickTimes[type]*.94f;
        if(parkFlight&&flightTricks==0)preferred=Mathf.Clamp(airMax*.46f-.07f,minimum,preferred);
        trickLength=Mathf.Clamp(airMax-airTime-.10f,minimum,preferred);
        trickProgress=0;
        if(trickAt<0&&trickLine)trickAt=Status.metres;
        string name=spread?"Spread eagle 360":poseStyle<0?TrickNames[type]:Hero=="bear"?(poseStyle==0?"Shifty":"Spread eagle"):(poseStyle==0?"Indy":"Melon");
        Notice(name,"trick");
    }
    void TickAir(float dt){
        if(!airborne)return;
        airTime+=dt;
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
        if(BonusMode){
            // Banks and trees break the chain and cost speed. The prize ride lasts its full 75 seconds.
            rewardChain=0;Status.combo=0;lastReward=-100;
            if(kind=="edge"){
                float side=Mathf.Sign(lane);
                lane=side*(LaneLimit(Status.metres)-.85f);
                velocity=-side*3;slip=-side;dragTarget=lane;
                travelYaw=World.HeadingAt(Status.metres)-side*.16f;
            }
            if(wobble<=0){slow=.45f;wobble=.7f;Notice("Серия сброшена","wobble");}
            return;
        }
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
        // Keep the pursuit matched to the kicker's slower flight line. Time in
        // the air must leave room for the mandatory two tricks before landing.
        float chasePace=airborne&&parkFlight?Mathf.Min(baseSpeed,speed):baseSpeed;
        chaseLeft-=dt;chaseDistance+=chasePace*.94f*dt;
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
