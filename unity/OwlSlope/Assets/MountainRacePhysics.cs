using UnityEngine;

public partial class MountainRace {
    static readonly float[] StageSpeed={1,.9f,.86f,1,.9f,1.15f,1,1,1};
    static readonly int[] TrickScores={120,170,170,150,230,100,210,240};
    static readonly float[] TrickTimes={1,1.15f,1.15f,1.05f,1.35f,.8f,1.3f,1.4f};
    static float StepBlend(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
    static float ParkPace(float d){
        return (26+3*RaceWorld.Grade(d))*(d>=RaceWorld.LapLength?1.08f:1);
    }
    float LaneLimit(float d){
        if(World.AlpineAt(Status.metres))return World.WidthAt(d)*.5f;
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
    void Step(float dt,float keyboard,bool drawWorld){
        var s=Status;
        previousAltitude=Altitude;
        s.elapsed+=dt;
        if(freeRideFalling){StepCrevasseFall(dt,drawWorld);return;}
        if(crashTimer>0){
            crashTimer-=dt;speed*=Mathf.Pow(.15f,dt);
            World.Refresh(s.metres,s.elapsed,lane,drawWorld);
            if(crashTimer<=0){s.rankedResult=settings.mode=="normal"&&s.mode=="normal";Finish(false,s.message,fell:true);}return;
        }
        if(BonusMode)s.elapsed=Mathf.Min(BonusSeconds,s.elapsed);
        if(s.elapsed>noticeUntil)s.message="";
        float d=s.metres,oldLane=lane;
        int stage=BonusMode?0:RaceWorld.StageAt(d);
        if(stage!=bestStage){
            bestStage=s.stage=stage;SelectChallenge(stage);Cue("stage");
            if(World.AlpineAt(d)){
                chaseLeft=s.avalanche=0;
                chaseNext=d-Mathf.Repeat(d,RaceWorld.StageLength)+RaceWorld.StageLength+900;
            }
        }
        if(StepFisStop(dt,keyboard,drawWorld))return;
        float previousSpeed=speed;
        float grade=.29f+.22f*Mathf.Sin(d/42)+.09f*Mathf.Sin(d/17+1);
        baseSpeed=Mathf.Min(230+d*.085f,560)*(.8f+.7f*grade)*StageSpeed[stage]*(d>=RaceWorld.LapLength?1.08f:1)*1.1f/20;
        float offTrail=stage==2&&!airborne?World.MogulRoughness(d,lane):0;
        if(stage==2)baseSpeed*=1-.13f*offTrail;
        if(BonusMode)baseSpeed=BonusSpeed(s.elapsed);
        bool freeRide=World.AlpineAt(Status.metres);
        if(freeRide)baseSpeed=World.FreeRidePace(d);
        bool snowpark=stage==3&&!BonusMode;
        if(snowpark)baseSpeed=ParkPace(d);
        float targetSpeed=baseSpeed*(boost>0?(BonusMode?BonusBoost:1.65f):1)*(slow>0?.62f:1)*(fall>0?FallSlow():1);
        if(airborne&&parkFlight){
            // No ground traction in the air. Finishing a trick or losing coffee cannot brake a flying rider.
            speed=flightSpan/airMax;
        }else if(freeRide){
            speed=Mathf.MoveTowards(speed,targetSpeed,(fall>0?22:6)*dt);
        }else if(snowpark){
            targetSpeed=Mathf.Min(targetSpeed,37)*(1-.035f*StepBlend(rampPhase));
            speed=Mathf.MoveTowards(speed,targetSpeed,(speed>targetSpeed?9:6)*dt);
        }else if((s.mode=="normal"||s.mode=="free")&&d>40&&fall<=0){
            // Everyone leaves the start at the same pace. The deck gains speed sooner on a steep line.
            if(Fireboard)targetSpeed*=1+.10f*Mathf.InverseLerp(.16f,.55f,grade);
            float acceleration=Fireboard?16+FreeTuning*2:CarveSkis?4.6f:8;
            speed=Mathf.MoveTowards(speed,targetSpeed,(speed<targetSpeed?acceleration:19)*dt);
        }else speed=targetSpeed;
        speed=FisApproachSpeed(previousSpeed,speed,dt);
        float wind=s.blizzard*(Mathf.Sin(s.elapsed*.55f)*75+Mathf.Sin(s.elapsed*1.7f)*25)/20;
        float heading=World.HeadingAt(d);
        bool controlled=dragging||keyboard!=0||patrol==PatrolPhase.Braking;
        float target=keyboard!=0?lane+keyboard*3.5f:dragTarget;
        if(dragging)dragTarget=Mathf.Clamp(dragTarget+wind*.5f*dt,-LaneLimit(d),LaneLimit(d));
        float rough=offTrail>0?offTrail*(.35f+.65f*MogulShape(d,lane)):0;
        if(fall>0){velocity=slip=0;travelYaw=heading;}
        else if(controlled){
            float response=(Status.mode=="tubing"?.76f:Hero=="owl"?1.15f:1)*(airborne?.45f:1)*(wobble>0?.5f:1)*(1-.38f*rough);
            if(Fireboard)response*=.85f;
            float cap=21.5f*Mathf.Sqrt(sensitivity);
            float want=Mathf.Clamp((target-lane)*(3+5*sensitivity),-cap,cap)*response;
            velocity=Mathf.Lerp(velocity,want,dt*(Status.mode=="tubing"?3.4f:5+6*sensitivity));
            travelYaw=heading+Mathf.Atan2(velocity,speed);
            float curve=Mathf.DeltaAngle(World.HeadingAt(d-1)*Mathf.Rad2Deg,World.HeadingAt(d+1)*Mathf.Rad2Deg)*Mathf.Deg2Rad/
                Mathf.Max(1,(World.SampleGround(d+1,0)-World.SampleGround(d-1,0)).magnitude);
            float bank=(World.SampleGround(d,lane+.1f).y-World.SampleGround(d,lane-.1f).y)/.2f;
            slip=airborne?0:Mathf.Lerp(slip,Mathf.Clamp(( -curve*speed*speed-9.81f*bank)*.24f,-1.8f,1.8f),4*dt);
            if(CarveSkis&&!airborne)slip*=Mathf.Exp(-(7+FreeTuning*2)*dt);
        }else{
            velocity=Status.mode=="tubing"?velocity*Mathf.Exp(-2.4f*dt):0;
            float delta=Mathf.DeltaAngle(heading*Mathf.Rad2Deg,travelYaw*Mathf.Rad2Deg)*Mathf.Deg2Rad;
            slip=Mathf.Clamp(Mathf.Tan(Mathf.Clamp(delta,-.7f,.7f))*speed,-16,16);
        }
        float rut=rough>0?rough*(2.7f*Mathf.Sin(.39f*d+lane)+1.35f*Mathf.Sin(.73f*d-.67f*lane)):0;
        lane=Mathf.Clamp(lane+dt*(velocity+slip+wind*.5f+rut),-LaneLimit(d),LaneLimit(d));
        angle=Mathf.Lerp(angle,Mathf.Clamp((velocity+slip*.5f)/21.5f,-1,1),dt*10);
        if(!edgeReturn&&Mathf.Abs(lane)>=LaneLimit(d)-.001f&&fall<=0){
            edgeOrigin=lane;Hit("edge");edgeReturn=fall>0;if(!BonusMode)travelYaw=heading;
        }
        if(edgeReturn){float t=Mathf.Clamp01((1-fall/FallTime)*1.7f);lane=Mathf.Lerp(edgeOrigin,0,StepBlend(t));dragTarget=lane;if(t>=1)edgeReturn=false;}
        float next=d+speed*dt;
        if(airborne&&parkFlight)next=flightStart+speed*(airTime+dt);
        s.metres=s.mode=="sprint"?Mathf.Min(SprintLength,next):next;
        TickAir(dt);
        World.SetThrowTarget(lane,speed,velocity+slip,Altitude);
        World.Refresh(s.metres,s.elapsed,lane,drawWorld);
        if(crashTimer<=0)CheckItems(d,oldLane);
        if(s.phase!="play"||crashTimer>0)return;
        TickTimers(dt);
        if(!BonusMode&&!freeRide){Avalanche(dt);TickChallenge();FisHints(d,s.metres);}
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
        wobble=Mathf.Max(0,wobble-dt);fall=Mathf.Max(0,fall-dt);
    }
    void BeginFlight(RaceItem item){
        airborne=true;parkFlight=item.park;trickLine=item.trickLine;prizeLine=item.prizeLine;
        lipHeight=item.height;flightStart=item.distance+item.length*.5f;
        if(parkFlight){
            // A slow rider lands short, without stretching the same jump into slow motion.
            float maxTime=1.1f+.34f*item.lift;
            flightSpan=Mathf.Min(item.span,Mathf.Max(1,speed)*maxTime);
            flightLift=item.lift*(flightSpan/item.span);
            airMax=flightSpan/Mathf.Max(1,speed);
        }else{
            flightSpan=0;flightLift=2.3f*Mathf.Sqrt(Mathf.Max(.1f,lipHeight)/.75f);
            airMax=(Hero=="owl"?1.45f:.85f)*(1+(speed*20-245)/1400)*(.75f+.25f*flightLift/2.3f);
        }
        airTime=Mathf.Max(0,(Status.metres-flightStart)/Mathf.Max(1,speed));trickProgress=-1;flightTricks=0;
        if(!BonusMode)ChooseAirPose(airMax-airTime);
        Notice(prizeLine?"Два трюка за полёт":trickLine?"Тап в воздухе — оборот":"Прыжок!","jump");
    }
    float FlightHeight(float d){
        if(parkFlight&&World.AlpineAt(Status.metres))return Mathf.Max(0,FreeRideClearance(d,lane));
        float t=parkFlight?Mathf.Clamp01((d-flightStart)/Mathf.Max(.01f,flightSpan)):Mathf.Clamp01(airTime/Mathf.Max(.01f,airMax));
        if(!parkFlight)return lipHeight*(1-t)+flightLift*Mathf.Sin(Mathf.PI*t);
        return RaceWorld.KickerArc(t,lipHeight,flightLift);
    }
    public void AirTrick(string gesture){
        if(!airborne&&World.AlpineAt(Status.metres)){
            if((gesture=="tap"||gesture=="up"||gesture=="big-up"||gesture=="down"||gesture=="big-down")&&FreeRideJumpWindow(out float lip,1.35f)){
                freeRideQueuedLip=lip;freeRideQueuedTrick=gesture;nextHud=0;
            }
            return;
        }
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
        float preferred=type==5?.8f:TrickTimes[type]*.94f;
        if(prizeLine)preferred=Mathf.Clamp(airMax*.32f,minimum,preferred);
        else if(parkFlight&&flightTricks==0)preferred=Mathf.Clamp(airMax*.46f-.07f,minimum,preferred);
        trickLength=Mathf.Clamp(airMax-airTime-.10f,minimum,preferred);
        trickProgress=0;RequestAirPose(trickLength);
        Notice(AirPoseName,"trick");
    }
    void TickAir(float dt){
        if(!airborne)return;
        airTime+=dt;
        if(trickProgress>=0){
            trickProgress+=dt/trickLength;
            if(trickProgress>=1)AwardTrick(false);
        }
        if(parkFlight&&World.AlpineAt(Status.metres)){
            if(airTime<.06f||FreeRideClearance(Status.metres,lane)>0)return;
            if(World.TryFreeRideGap(Status.metres,out var gap)&&gap.OverOpening(Status.metres,lane)){
                FallIntoFreeRideGap(gap);return;
            }
        }else if(airTime+.00001f<airMax)return;
        if(trickProgress>=.7f)AwardTrick(true);
        else if(trickProgress>=0){Status.combo=0;wobble=.6f;slow=.5f;Notice("Не докрутил — серия сброшена","warn");}
        else {Status.combo+=flightTricks;Status.maxCombo=Mathf.Max(Status.maxCombo,Status.combo);Cue("land");}
        if(!BonusMode)EndAirPose();
        airborne=parkFlight=false;trickProgress=-1;lastLanding=Status.elapsed;
    }
    void AwardTrick(bool landing){
        Status.tricks++;flightTricks++;trickProgress=-1;
        bonusPoints+=Mathf.RoundToInt(TrickScores[trickType]*Mult*(Status.stage==3?2:1)*(!landing&&flightTricks>1?1.5f:1));
        Cue("trickDone");
    }
    void Hit(string kind){
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
        if(Status.helmets>0){
            Status.helmets--;fall=FallTime;fallSide=random.Next(2)==0?-1:1;airborne=parkFlight=false;
            trickProgress=-1;flightTricks=0;dragTarget=lane;
            Notice("Каска спасла! Осталось: "+Status.helmets,"helmet");return;
        }
        crashTimer=1.5f;airborne=false;trickProgress=-1;fallSide=1;
        string msg;
        switch(kind){
            case "snowBoulder":msg="Сбил снежный ком";break;
            case "crevasse":msg="Срыв в расщелину";break;
            case "edge":msg="Снежный борт! Рули на поворотах";break;case "rock":msg="Камень на трассе";break;
            case "avalanche":msg="Лавина догнала";break;case "cat":msg="Столкновение с ратраком";break;
            case "npc":msg="Держи дистанцию при обгоне";break;case "tube":msg="Плюшка пересекла твою линию";break;
            case "tree":msg="Столкновение с деревом";break;case "mogulYeti":case "yeti":msg="Столкновение с йети";break;
            case "gate":msg="Задел стойку ворот";break;case "dog":msg="Столкновение с собакой";break;
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
        if(Status.metres-chaseDistance<.75f&&fall<=0){Hit("avalanche");chaseDistance=Status.metres-21;}
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
