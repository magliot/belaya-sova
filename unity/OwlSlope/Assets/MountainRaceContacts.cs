using UnityEngine;

public partial class MountainRace {
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
        rampHeight=0;airborne=false;
        foreach(var o in World.Items){
            if((o.kind!="ramp"&&o.kind!="rail")||Mathf.Abs(lane-o.lane)>o.width*.5f)continue;
            float lip=o.distance+o.length*.5f;
            if(Mathf.Abs(Status.metres-o.distance)<o.length*.5f)rampHeight=SurfaceHeight(o,Status.metres);
            if(o.park&&o.kind=="ramp"&&Status.metres>lip&&Status.metres<lip+o.span*(Hero=="owl"?1.09f:1)){
                BeginFlight(o);airTime=airMax*Mathf.Clamp01((Status.metres-flightStart)/flightSpan);return;
            }
        }
    }
    void CheckItems(float before,float fromLane){
        float d=Status.metres;rampHeight=rampPhase=0;int letterChange=0;
        Status.engineNear=0;Status.enginePan=0;
        foreach(var o in World.Items){
            if(o.consumed)continue;
            float dx=lane-o.lane,dy=d-o.distance,abs=Mathf.Abs(dx),ahead=-dy;
            if(o.kind=="cat"||o.kind=="snowmobile"){
                float vol=1-Mathf.Clamp01(Mathf.Sqrt(dx*dx*2.56f+dy*dy)/35);
                if(vol>Status.engineNear){Status.engineNear=vol;Status.enginePan=Mathf.Clamp(-dx/8,-.9f,.9f);}
            }
            if(ahead>12||ahead< -9) {
                // Park ramps and very long boxes still have drivable surfaces within this window.
                if(Mathf.Abs(dy)>o.length*.5f+1)continue;
            }
            float square=dx*dx+dy*dy;
            bool cross=before<o.distance&&d>=o.distance;
            switch(o.kind){
              case "ramp":case "rail":{
                float end=o.distance+o.length*.5f;
                if(!airborne&&fall<=0&&abs<o.width*.5f){
                    if(Mathf.Abs(dy)<=o.length*.5f){rampHeight=SurfaceHeight(o,d);rampPhase=Mathf.Clamp01(dy/o.length+.5f);o.ridden=true;}
                    if(before<end&&d>=end){
                        if(o.kind=="ramp"){BeginFlight(o);rampHeight=0;}
                        else if(o.ridden&&!o.judged){o.judged=true;Status.grinds++;bonusPoints+=120*Mult;Notice("Бокс +"+120*Mult,"trickDone");}
                    }
                }break;}
              case "gate":
                if(!o.judged&&cross){
                    o.judged=true;
                    float crossLane=Mathf.Lerp(fromLane,lane,(o.distance-before)/Mathf.Max(.001f,d-before));
                    if(Mathf.Abs(crossLane-o.lane)<o.width*.5f){
                        Status.gates++;Status.combo++;consecutiveGates++;Status.maxCombo=Mathf.Max(Status.maxCombo,Status.combo);
                        bonusPoints+=25*Mult;Cue("gate");
                    }else{consecutiveGates=Status.combo=0;Cue("miss");}
                }
                if(!airborne&&!o.stoneGate&&wobble<=0&&Mathf.Abs(dy)<.3f&&Mathf.Abs(abs-o.width*.5f)<.3f){
                    wobble=.4f;velocity*=-.4f;Cue("wobble");
                }break;
              case "airRing":
                if(!o.judged&&(!o.needsTrick||(trickLine&&flightTricks>0))&&TouchAir(o,before,fromLane,Mathf.Max(.6f,o.width*.5f-.1f))){
                    o.judged=true;Status.rings++;bonusPoints+=Mathf.Max(1,o.points)*Mult;Notice("Воздушное кольцо!","gate");
                }break;
              case "coin":case "thermos":case "helmet":case "magnet":case "feather":
                if(o.kind=="feather"&&(!prizeLine||flightTricks<2))break;
                float radius=o.kind=="coin"?1.1f:1.2f;
                if(o.kind=="coin"&&!o.air&&magnet>0&&square<8.5f*8.5f){
                    float length=Mathf.Sqrt(square);
                    if(length>.0001f){
                        float k=Mathf.Min(1,Mathf.Max(length*8,(speed+17.5f))*(d-before)/Mathf.Max(1,speed)/length);
                        o.lane+=dx*k;o.distance+=dy*k;square*=1-k;square*=1-k;
                    }
                }
                if(o.air?!TouchAir(o,before,fromLane,1.12f):square>=radius*radius)break;
                o.Consume();
                switch(o.kind){
                    case "coin":Status.coins++;if(BonusMode)BonusReward(15);else bonusPoints+=5;Cue("coin");break;
                    case "thermos":boost=4.5f+settings.boost*.8f;invincible=Mathf.Max(invincible,boost);Notice("Кофе · ускорение +65%","power");break;
                    case "helmet":if(Status.helmets<3)Status.helmets++;else bonusPoints+=50;Notice("Каска EMCO","power");break;
                    case "magnet":magnet=7+settings.mag*2;Notice("Магнит для мандаринов","power");break;
                    case "feather":Status.feather=true;Status.feathers++;bonusPoints+=300*Mult;Notice("Золотое перо! Открыты оба бонуса","feather");break;
                }break;
              case "goldenOwl":
                if(square<1.5f*1.5f&&Mathf.Abs((airborne?FlightHeight(d):0)+1-o.height)<1.7f){
                    o.Consume();Status.goldenOwl=true;bonusPoints+=500*Mult;Notice("Золотая сова! Снегоход открыт","chime");}
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
                if(square<1.69f){o.Consume();bonusPoints+=80*Mult;Rule(9);Notice("Позвал патруль!","polite");}break;
              case "snowman":
                if(Status.mode=="sled"&&square<2.25f){o.Consume();Status.snowmen++;BonusReward(90);Notice("Снеговик!","smash");}
                else if(!airborne&&square<1)Hit(o.kind,o);break;
              case "ball":case "dog":
                if(!airborne&&square<(o.kind=="dog"?.85f*.85f:.7f*.7f)){
                    o.Consume();wobble=.6f;slow=o.kind=="dog"?.6f:.4f;Status.combo=0;Notice(o.kind=="dog"?"Осторожно, собака!":"Попали снежком","wobble");}break;
              case "mogul":
                if(!airborne&&square<Mathf.Pow(.45f+o.width*.5f,2)){
                    if(Hero=="bear"||boost>0){o.Consume();bonusPoints+=15;Cue("smash");}
                    else if(!o.judged){o.judged=true;slow=Mathf.Max(slow,.25f);rampHeight=Mathf.Max(rampHeight,.24f);}
                }break;
              case "npc":
                if(!airborne&&square<(o.kid?.49f:.9025f)){Hit(o.kind,o);break;}
                if(!o.judged&&dy>0){
                    o.judged=true;
                    if(airborne)break;
                    if(abs<2){if(Status.elapsed-lastClose>2.5f){lastClose=Status.elapsed;Status.combo=0;challengeClean=d;Notice("Объезжай с запасом","warn");}}
                    else if(abs<6){Status.polite++;bonusPoints+=30*Mult;Rule(o.walker?7:o.sit?6:o.kid||o.instr?2:4);if(Status.polite>=5)Rule(3);Notice("Вежливый обгон","polite");}
                }break;
              case "mogulYeti":case "snowmobile":
                if(abs<(o.kind=="mogulYeti"?1.45f:1.10f)&&Mathf.Abs(dy)<(o.kind=="mogulYeti"?1.1f:1.9f)
                   && Mathf.Abs((airborne?FlightHeight(d):0)-o.offset)<(o.kind=="mogulYeti"?2.7f:1.8f))Hit(o.kind,o);
                if(o.kind=="mogulYeti"&&!o.judged&&dy>1.1f){o.judged=true;if(Status.encounters==0)Status.encounters++;}break;
              case "yeti":
                if(!o.judged&&dy> -3){o.judged=true;Status.encounters++;bonusPoints+=500*Mult;Notice("Встреча с йети!","chime");}break;
              case "fence":case "cat":case "tree":case "rock":case "cannon":case "tube":
                float halfWidth=o.width*.5f+.36f,halfLength=Mathf.Max(.38f,o.length*.5f);
                if(!airborne&&abs<halfWidth&&Mathf.Abs(dy)<halfLength){Hit(o.kind,o);break;}
                if(!o.judged&&dy>halfLength){o.judged=true;
                    if(o.kind=="fence")Rule(8);
                    if(o.kind=="cat"&&abs>halfWidth+1){
                        if(o.velocity<0||o.pace<0)Rule(5);else{Status.polite++;bonusPoints+=40*Mult;Cue("polite");}
                    }
                }break;
            }
            if(crashTimer>0)break;
        }
        if(letterChange!=0)ScheduleLetter(d,letterChange<0);
    }
}
