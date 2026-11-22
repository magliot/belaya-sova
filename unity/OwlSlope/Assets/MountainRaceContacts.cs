using UnityEngine;

public partial class MountainRace {
    RaceItem touchedFeather;
    void CollectFeather(RaceItem item){
        item.Consume();Status.feather=true;Status.feathers++;bonusPoints+=300*Mult;
        Notice("Золотое перо +1","feather");
    }
    void ScheduleLetter(float d,bool missed){
        if(CourseMode!="normal")return;
        int i=Status.lettersTotal;
        for(int word=0;word<Words.Length;word++){
            if(i<Words[word].Length){
                float when=missed?d+1000:Mathf.Max(1500+i*1500+word*500,d==0?0:d+800);
                World.SetLetter(when,Words[word][i].ToString());return;
            }
            i-=Words[word].Length;
        }
        World.SetLetter(float.PositiveInfinity,null);
    }
    void BonusReward(int points){
        rewardChain=Status.elapsed-lastReward<=3?rewardChain+1:1;lastReward=Status.elapsed;
        bonusPoints+=points*Mathf.Min(4,1+rewardChain/5);
    }
    bool TouchAir(RaceItem o,float before,float fromLane,float radius){
        if(!airborne||before>=o.distance||Status.metres<o.distance)return false;
        float k=(o.distance-before)/Mathf.Max(.001f,Status.metres-before);
        float dx=Mathf.Lerp(fromLane,lane,k)-o.lane,dy=FlightHeight(o.distance)+.9f-o.height;
        return dx*dx+dy*dy<radius*radius;
    }
    float SurfaceHeight(RaceItem o,float d){
        float t=Mathf.Clamp01((d-o.distance)/Mathf.Max(.01f,o.length)+.5f);
        if(o.kind=="rail")return o.height*Mathf.Min(1,Mathf.Min(t,1-t)*o.length/2);
        return o.height*t*(o.park?.65f+.35f*t:1);
    }
    void ReviewContact(){
        rampHeight=0;airborne=parkFlight=false;ResetFreeRideInput();
        if(World.AlpineAt(Status.metres)){PreviewFreeRideJump();return;}
        foreach(var o in World.Items){
            if((o.kind!="ramp"&&o.kind!="rail")||Mathf.Abs(lane-o.lane)>o.width*.5f)continue;
            float lip=o.distance+o.length*.5f;
            if(Mathf.Abs(Status.metres-o.distance)<o.length*.5f)rampHeight=SurfaceHeight(o,Status.metres);
            if(o.park&&o.kind=="ramp"&&Status.metres>lip&&Status.metres<lip+o.span){
                BeginFlight(o);
                if(Status.metres<lip+flightSpan)return;
                airborne=false;
            }
        }
    }
    static bool ClipContact(float start,float end,float low,float high,ref float enter,ref float exit){
        float delta=end-start;
        if(Mathf.Abs(delta)<.00001f)return start>=low&&start<=high;
        float a=(low-start)/delta,b=(high-start)/delta;
        enter=Mathf.Max(enter,Mathf.Min(a,b));exit=Mathf.Min(exit,Mathf.Max(a,b));
        return enter<=exit;
    }
    bool TouchesBody(RaceItem o,float before,float fromLane,float halfWidth,float halfLength,float height,float post,bool round){
        // Both bodies move during a physics tick. Clip first to the height interval,
        // then test the corresponding part of the sweep against the footprint.
        float enter=0,exit=1;
        bool hit=ClipContact(previousAltitude-o.previousOffset,Altitude-o.offset,-1.45f,height,ref enter,ref exit);
        if(hit){
            float a=o.yaw*Mathf.Deg2Rad,c=Mathf.Cos(a),s=Mathf.Sin(a);
            var origin=new Vector2(fromLane-o.previousLane-post,before-o.previousDistance);
            var end=new Vector2(lane-o.lane-post,Status.metres-o.distance);
            float bodyWidth=Hero=="bear"?.53f:.57f,bodyLength=Hero=="bear"?.74f:.66f;
            halfWidth+=Mathf.Abs(c)*bodyWidth+Mathf.Abs(s)*bodyLength;
            halfLength+=Mathf.Abs(s)*bodyWidth+Mathf.Abs(c)*bodyLength;
            Vector2 Local(Vector2 v)=>new Vector2((v.x*c-v.y*s)/halfWidth,(v.x*s+v.y*c)/halfLength);
            var p=Local(origin);var q=Local(end);
            if(round){
                var delta=q-p;
                float t=Mathf.Clamp(-Vector2.Dot(p,delta)/Mathf.Max(.000001f,delta.sqrMagnitude),enter,exit);
                hit=(p+t*delta).sqrMagnitude<=1;
            }else hit=ClipContact(p.x,q.x,-1,1,ref enter,ref exit)&&ClipContact(p.y,q.y,-1,1,ref enter,ref exit);
        }
        return hit;
    }
    bool HandleSolid(RaceItem o,float before,float fromLane,float halfWidth,float halfLength,float height,float post=0,bool round=false){
        bool hit=TouchesBody(o,before,fromLane,halfWidth,halfLength,height,post,round);
        if(hit&&!o.touchingPlayer){o.judged=true;Hit(o.kind);}
        o.touchingPlayer=hit;
        return hit;
    }
    void UpdateRidingSurface(float before,float d){
        rampHeight=rampPhase=0;
        if(airborne||fall>0)return;
        foreach(var o in World.Items){
            if(o.consumed||(o.kind!="ramp"&&o.kind!="rail")||Mathf.Abs(lane-o.lane)>=o.width*.5f)continue;
            float halfLength=o.length*.5f,end=o.distance+halfLength;
            if(Mathf.Abs(d-o.distance)<=halfLength){
                rampHeight=SurfaceHeight(o,d);rampPhase=Mathf.Clamp01((d-o.distance)/o.length+.5f);o.ridden=true;
            }
            if(before>=end||d<end)continue;
            if(o.kind=="ramp"){BeginFlight(o);rampHeight=0;return;}
            if(o.ridden&&!o.judged){o.judged=true;Status.grinds++;bonusPoints+=120*Mult;Notice(CourseMode=="normal"?"Скольжение по боксу":"Бокс +"+120*Mult,"trickDone");}
        }
    }
    void CollectCoin(RaceItem o){
        o.Consume();Status.coins++;Burst(o,true);
        if(BonusMode)BonusReward(15);else bonusPoints+=5;
        Cue("coin");
    }
    void CheckItems(float before,float fromLane){
        float d=Status.metres;int letterChange=0;
        if(World.AlpineAt(Status.metres)){
            FreeRideContact(before,fromLane);
            if(freeRideFalling)return;
        }
        else UpdateRidingSurface(before,d);
        Status.boulderChasing=false;Status.boulderNear=0;Status.engineNear=0;Status.enginePan=0;
        foreach(var o in World.Items){
            if(o.consumed)continue;
            float dx=lane-o.lane,dy=d-o.distance,abs=Mathf.Abs(dx),ahead=-dy;
            if(o.kind=="snowBoulder"){
                float distance=Mathf.Sqrt(dx*dx+dy*dy);
                Status.boulderNear=Mathf.Max(Status.boulderNear,1-Mathf.Clamp01(distance/42));
                if(o.PursuesRider&&o.age>0&&!o.judged&&dy> -3&&dy<28){Status.boulderChasing=true;Status.boulderLine=o.to;}
            }
            if(o.kind=="npc"&&!o.judged)ObserveRider(o,ahead,dx);
            if(o.kind=="cat"||o.kind=="snowmobile"){
                float vol=1-Mathf.Clamp01(Mathf.Sqrt(dx*dx*2.56f+dy*dy)/(o.kind=="cat"?56:35));
                if(vol>Status.engineNear){Status.engineNear=vol;Status.enginePan=Mathf.Clamp(-dx/8,-.9f,.9f);}
                float closingSpeed=speed-o.pace;
                if(o.kind=="cat"&&!o.honked&&closingSpeed>0&&ahead>0&&ahead<Mathf.Clamp(closingSpeed*1.8f,15,48)){
                    o.honked=true;Status.horns++;
                }
            }
            if(ahead>12||ahead< -9) {
                // Park ramps and very long boxes still have drivable surfaces within this window.
                if(Mathf.Abs(dy)>o.length*.5f+1){o.touchingPlayer=false;continue;}
            }
            float square=dx*dx+dy*dy;
            bool cross=before<o.distance&&d>=o.distance;
            switch(o.kind){
              case "snowBoulder":HandleSolid(o,before,fromLane,o.scale,o.scale,1.9f*o.scale,0,true);break;
              case "festivalFountain":
                if(!o.judged&&TouchesBody(o,before,fromLane,.52f,.52f,.78f,0,false)){
                    o.judged=true;o.fountainHitAt=Status.elapsed;
                    if(o.spray)o.spray.Stop(true,ParticleSystemStopBehavior.StopEmitting);
                    Hit(o.kind);
                }
                break;
              case "gate":
                if(!o.judged&&!o.ScoringPlane){
                    int side=dx<0?-1:1;
                    bool cone=o.variant=="trainingCones";
                    if(TouchesBody(o,before,fromLane,cone?RaceWorld.ConeHalfWidth:.105f,cone?RaceWorld.ConeHalfWidth:.12f,cone?RaceWorld.ConeHeight:1.65f,side*o.width*.5f,false)){
                        o.judged=true;consecutiveGates=Status.combo=0;
                        if(cone){o.knockedConeSide=side;o.coneHitAt=Status.elapsed;}
                        break;
                    }
                }
                if(!o.judged&&cross){
                    o.judged=true;
                    float crossLane=Mathf.Lerp(fromLane,lane,(o.distance-before)/Mathf.Max(.001f,d-before));
                    if(fall>0||crashTimer>0)break;
                    if(Mathf.Abs(crossLane-o.lane)<o.width*.5f){
                        Status.gates++;Status.combo++;consecutiveGates++;Status.maxCombo=Mathf.Max(Status.maxCombo,Status.combo);
                        bonusPoints+=25*Mult;Cue("gate");
                    }else {
                        consecutiveGates=Status.combo=0;
                        if(CourseMode!="normal")Cue("miss");
                    }
                }
                break;
              case "airRing":
                if(!o.judged&&(!o.needsTrick||(trickLine&&flightTricks>0))&&TouchAir(o,before,fromLane,Mathf.Max(.6f,o.width*.5f-.1f))){
                    o.judged=true;Status.rings++;
                    bonusPoints+=Mathf.Max(1,o.points)*Mult;Notice("Воздушное кольцо!","gate");
                }break;
              case "feather":
                if(prizeLine&&flightTricks>=1&&TouchAir(o,before,fromLane,1.12f)){
                    if(flightTricks>=2)CollectFeather(o);
                    else if(trickProgress>=0)touchedFeather=o;
                }
                break;
              case "coin":case "thermos":case "helmet":case "magnet":
                float radius=o.kind=="coin"?GroundFruitRadius:1.2f;
                if(o.kind=="coin"&&!o.air&&magnet>0&&square<8.5f*8.5f){
                    float length=Mathf.Sqrt(square);
                    if(length>.0001f){
                        float k=Mathf.Min(1,Mathf.Max(length*8,(speed+17.5f))*(d-before)/Mathf.Max(1,speed)/length);
                        o.lane+=dx*k;o.distance+=dy*k;square*=1-k;square*=1-k;
                    }
                }
                if(o.air?!TouchAir(o,before,fromLane,1.12f):square>=radius*radius)break;
                if(o.kind=="coin"){
                    if(o.air&&freeRideAwaitingTrick){if(!crossedFruit.Contains(o))crossedFruit.Add(o);}
                    else CollectCoin(o);
                    break;
                }
                o.Consume();
                switch(o.kind){
                    case "thermos":boost=4.5f+MainUpgrade(settings.boost)*.8f;Notice(BonusMode?"Кофе · ускорение +22%":"Кофе · ускорение +65%","power");break;
                    case "helmet":
                        if(Status.helmets==0&&helmetsIssued<MaxHelmetsPerRide){Status.helmets=1;helmetsIssued++;Notice("Каска EMCO","power");}
                        break;
                    case "magnet":magnet=7+MainUpgrade(settings.mag)*2;Notice("Магнит для мандаринов","power");break;
                }break;
              case "goldenOwl":
                if(square<1.5f*1.5f&&Mathf.Abs((airborne?FlightHeight(d):0)+1-o.height)<1.7f){
                    o.Consume();Status.goldenOwl=true;bonusPoints+=500*Mult;Notice("Золотая сова!","chime");}
                break;
              case "letter":
                if(square<1.4f*1.4f&&(!o.air||airborne)){
                    o.Consume();Status.letters++;Status.lettersTotal++;
                    bool word=Status.lettersTotal==4||Status.lettersTotal==8||Status.lettersTotal==16;
                    bonusPoints+=(word?1000:200)*Mult;Notice(word?"Слово собрано!":"Буква "+o.character,"chime");letterChange=1;
                }else if(!o.judged&&dy>2){o.judged=true;letterChange=-1;}break;
              case "gift":
                if(square<1.44f){o.Consume();Status.gifts++;if(BonusMode)BonusReward(120);else bonusPoints+=50*Mult;Notice("Подарок!","power");}break;
              case "aid":
                if(square<1.69f){o.Consume();bonusPoints+=80*Mult;Notice("Аптечка подобрана","power");}break;
              case "snowman":
                if(BonusMode&&Altitude<1.5f&&square<2.25f){ScatterSnowman(o);o.Consume();Status.snowmen++;BonusReward(90);Notice("Снеговик!","smash");}
                else if(o.variant=="snowman")HandleSolid(o,before,fromLane,.89f,.72f,2.50f,0,true);
                else HandleSolid(o,before,fromLane,.62f,.52f,1.67f,0,true);break;
              case "ball":
                if(SnowballContact(o,before,fromLane)){
                    o.judged=true;o.Consume();Burst(o,false);Cue("snowball");
                }break;
              case "dog":
                HandleSolid(o,before,fromLane,.20f,.40f,.8f,0,true);break;
              case "mogul":
                if(!airborne&&square<Mathf.Pow(.45f+o.width*.5f,2)){
                    if(Hero=="bear"||boost>0){BreakSnowMound(o);o.Consume();bonusPoints+=15;Cue("smash");}
                    else if(!o.judged){o.judged=true;slow=Mathf.Max(slow,.25f);rampHeight=Mathf.Max(rampHeight,.24f);}
                }break;
              case "npc":
                if(HandleSolid(o,before,fromLane,o.sb&&o.kid?.35f:.55f,o.sit?.44f:o.sb?.78f:.64f,o.sit?.95f:1.4f,0,true))break;
                if(!o.judged&&dy>1.65f){
                    o.judged=true;
                    if(airborne)break;
                    if(Mathf.Min(o.fisClearance,abs)<2){if(Status.elapsed-lastClose>2.5f){lastClose=Status.elapsed;Status.combo=0;challengeClean=d;Notice("Объезжай с запасом","warn");}}
                    else SafePass(o);
                }break;
              case "mogulYeti":case "snowmobile":
                bool yeti=o.kind=="mogulYeti";
                if(HandleSolid(o,before,fromLane,yeti?1.70f:.64f,yeti?.96f:1.50f,yeti?3.2f:1.88f,0,yeti))break;
                if(o.kind=="mogulYeti"&&!o.judged&&dy>1.1f){o.judged=true;if(Status.encounters==0)Status.encounters++;}break;
              case "yeti":
                if(HandleSolid(o,before,fromLane,1.7f,.96f,3.2f,0,true))break;
                if(!o.judged&&dy>2){o.judged=true;Status.encounters++;bonusPoints+=500*Mult;Notice("Встреча с йети!","chime");}break;
              case "fence":case "cat":case "tree":case "rock":case "cannon":case "tube":
                float halfWidth=o.width*.5f,halfLength=o.length*.5f,top=o.height;
                switch(o.kind){
                    case "tube":halfWidth=1.03f*o.scale;halfLength=.92f*o.scale;top=1.73f*o.scale;break;
                    case "cat":halfWidth=o.small?1.84f:2.1f;halfLength=o.small?2.85f:3.3f;top=o.small?3.56f:4;break;
                    case "tree":halfLength=halfWidth;top=Mathf.Max(3.6f,o.height)*o.scale;break;
                    case "cannon":halfLength=.68f;top=2.55f;break;
                    case "rock":
                        bool wide=o.variant=="rockWide"||o.variant=="gateRockWide";
                        halfWidth=(wide?.77f:.42f)*o.scale;halfLength=(wide?.72f:.39f)*o.scale;
                        top=(wide?.87f:1.52f)*o.scale;break;
                    case "fence":halfLength=.12f;break;
                }
                bool rounded=o.kind=="tube"||o.kind=="rock"||o.kind=="tree";
                if(HandleSolid(o,before,fromLane,halfWidth,halfLength,top,0,rounded))break;
                if(!o.judged&&dy>halfLength){o.judged=true;
                    if(o.kind=="fence"&&!airborne&&Mathf.Abs(o.lane)>.01f){
                        float margin=-Mathf.Sign(o.lane)*dx-halfWidth;
                        if(margin>=.8f&&margin<=4.4f)FisRule(8,o.lane<0?1:2,"Объезд по стрелке");
                    }
                    if(o.kind=="cat"&&abs>halfWidth+1){
                        if(o.pace>=0){Status.polite++;bonusPoints+=40*Mult;Cue("polite");}
                    }
                }break;
            }
            if(crashTimer>0)break;
        }
        if(letterChange!=0)ScheduleLetter(d,letterChange<0);
    }
}
