using UnityEngine;

public partial class MountainRace {
    float freeRideOriginY,freeRideVertical,freeRideTakeoffSpeed;
    float freeRideTrickUntil,freeRideTrickAt,freeRideDropSince,freeRideEntryMetre,freeRideEntryLane,freeRideEntryHeight;
    bool freeRideAwaitingTrick,freeRideFalling,crossedGapMiddle;
    RaceWorld.FreeRideGap flightGap,fallingGap;
    readonly System.Collections.Generic.List<RaceItem> crossedFruit=new System.Collections.Generic.List<RaceItem>(3);

    void ResetFreeRideInput(){
        freeRideAwaitingTrick=freeRideFalling=crossedGapMiddle=false;crossedFruit.Clear();
    }

    float FreeRideClearance(float d,float x){
        float time=(d-flightStart)/freeRideTakeoffSpeed;
        float clearance=RaceWorld.FreeRideFlightY(freeRideOriginY,freeRideVertical,time)-World.SampleGround(d,x).y;
        return RaceWorld.FreeRideHeight(clearance,StepBlend((time-freeRideTrickAt)/.31f));
    }
    void LaunchFromCornice(RaceWorld.FreeRideGap gap,float originD,float x,float pace){
        airborne=parkFlight=true;trickLine=prizeLine=parkTapQueued=false;
        freeRideTrickAt=float.PositiveInfinity;
        flightStart=originD;freeRideTakeoffSpeed=Mathf.Max(1,pace);
        freeRideOriginY=World.SampleGround(flightStart,x).y;
        freeRideVertical=World.FreeRideLaunchVelocity(gap,flightStart,x,freeRideTakeoffSpeed);
        // Predict the available trick window along the takeoff heading. Actual
        // touchdown uses the snow under the rider each tick, even after steering.
        float span=1;
        while(span<65&&FreeRideClearance(flightStart+span,x)>0)span+=1;
        float lo=span-1,hi=span;
        for(int i=0;i<7;i++){
            float mid=(lo+hi)*.5f;
            if(FreeRideClearance(flightStart+mid,x)>0)lo=mid;else hi=mid;
        }
        flightSpan=Mathf.Max(.05f,hi);airMax=flightSpan/freeRideTakeoffSpeed;
        airTime=Mathf.Max(0,(Status.metres-flightStart)/freeRideTakeoffSpeed);
        lipHeight=rampHeight=rampPhase=0;flightTricks=0;trickProgress=-1;
        flightGap=gap;crossedFruit.Clear();crossedGapMiddle=false;freeRideAwaitingTrick=true;airPose=-1;
        // Leave enough snow beyond the far wall to finish a late trick at downhill speed.
        freeRideTrickUntil=Mathf.Min(airMax-TurnSeconds,(gap.endD-1.2f-originD)/freeRideTakeoffSpeed);
        if(!Review)Notice("Тап в воздухе — трюк","jump");
    }
    void StartCorniceTrick(float startTime){
        freeRideAwaitingTrick=false;freeRideTrickAt=startTime;
        ChooseAirPose(TurnSeconds);
        poseStart=startTime;trickLength=TurnSeconds;
    }
    void TrackGapCrossing(float before,float fromLane){
        float middle=flightGap.SectionDistance(4);
        if(before<middle&&Status.metres>=middle){
            float t=Mathf.InverseLerp(before,Status.metres,middle);
            crossedGapMiddle=flightGap.OverOpening(middle,Mathf.Lerp(fromLane,lane,t));
        }
    }
    void RewardGapLanding(){
        if(!crossedGapMiddle||flightTricks==0||Status.metres<flightGap.endD||fall>0||crashTimer>0||Review)return;
        int fruit=flightGap.transverse?15:10;
        Status.coins+=fruit;Status.crevasseCoins+=fruit;
        Notice("Перелёт! +"+fruit+" мандаринов","chime");
        crossedGapMiddle=false;
    }
    void FallIntoFreeRideGap(RaceWorld.FreeRideGap gap){
        if(freeRideFalling)return;
        freeRideEntryHeight=Altitude;
        fallingGap=gap;freeRideFalling=true;freeRideAwaitingTrick=false;crossedFruit.Clear();
        freeRideDropSince=Status.elapsed;freeRideEntryMetre=Status.metres;freeRideEntryLane=lane;
        Hit("crevasse");
        // The rider is already below these prizes. Clear the sightline into this
        // fissure without consuming the fruit or running its reward effects.
        foreach(var item in World.Items){
            if(item.kind=="coin"&&item.air&&item.distance>gap.lipD&&item.distance<gap.endD&&item.obj)
                item.obj.SetActive(false);
        }
        velocity=slip=angle=0;edgeReturn=false;rampHeight=freeRideEntryHeight;nextHud=0;
    }
    void StepCrevasseFall(float dt,bool drawWorld){
        float age=Status.elapsed-freeRideDropSince;
        // Enter along the current direction, then sink into the wider middle of the fissure.
        float middle=Mathf.Clamp(freeRideEntryMetre+3.5f,fallingGap.SectionDistance(4),fallingGap.endD-.7f);
        float into=1-Mathf.Pow(Mathf.Clamp01(1-age/.72f),2);
        Status.metres=Mathf.Lerp(freeRideEntryMetre,middle,into);
        Vector2 banks=fallingGap.CrossSection(Status.metres);
        float middleLane=(banks.x+banks.y)*.5f;
        lane=Mathf.Clamp(Mathf.Lerp(freeRideEntryLane,middleLane,into),banks.x,banks.y);
        rampHeight=Mathf.Max(-5f,freeRideEntryHeight-11.5f*age*age);
        speed=0;dragTarget=lane;
        if(crashTimer>0){
            crashTimer-=dt;
            if(crashTimer<=0){freeRideFalling=false;Status.rankedResult=settings.mode=="normal"&&Status.mode=="normal";Finish(false,"Срыв в расщелину",fell:true);}
        }else{
            fall=Mathf.Max(0,fall-dt);
            if(age>=1.35f){
                Status.metres=fallingGap.endD+3;
                lane=dragTarget=Mathf.Clamp(freeRideEntryLane,-6,6);
                travelYaw=World.HeadingAt(Status.metres);speed=12;
                rampHeight=0;fall=0;freeRideFalling=false;lastLanding=Status.elapsed;
            }
        }
        World.Refresh(Status.metres,Status.elapsed,lane,drawWorld);
    }
    void FreeRideContact(float before,float fromLane){
        if(freeRideFalling)return;
        float d=Status.metres;rampHeight=rampPhase=0;
        if(airborne||fall>0||crashTimer>0||!World.TryFreeRideGap(d,out var gap))return;
        if(!gap.OverOpening(d,lane))return;
        if(!gap.OverOpening(before,fromLane)&&before<gap.SectionDistance(3)){
            float lo=0,hi=1;
            for(int i=0;i<9;i++){
                float mid=(lo+hi)*.5f;
                if(gap.OverOpening(Mathf.Lerp(before,d,mid),Mathf.Lerp(fromLane,lane,mid)))hi=mid;else lo=mid;
            }
            float entry=Mathf.Lerp(before,d,hi),x=Mathf.Lerp(fromLane,lane,hi);
            if(gap.TryFrontLip(x,out float lip)&&Mathf.Abs(entry-lip)<.035f){
                LaunchFromCornice(gap,entry,x,speed);return;
            }
        }
        FallIntoFreeRideGap(gap);
    }
    void PreviewFreeRideJump(){
        float d=Status.metres;
        if(!World.TryFreeRideGap(d,out var gap)||d<=gap.lipD||d>gap.endD+30||!gap.TryFrontLip(lane,out float lip)||d<=lip)return;
        speed=World.FreeRidePace(lip);LaunchFromCornice(gap,lip,lane,speed);
        if(airTime>=airMax){airborne=parkFlight=freeRideAwaitingTrick=false;}
        else if(airTime>freeRideTrickUntil*.55f){
            StartCorniceTrick(freeRideTrickUntil*.55f);
        }
    }
}
