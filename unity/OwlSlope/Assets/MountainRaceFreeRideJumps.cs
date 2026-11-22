using UnityEngine;

public partial class MountainRace {
    float freeRideOriginY,freeRideVertical,freeRideTakeoffSpeed;
    float freeRideQueuedLip=float.NaN,freeRideDropSince,freeRideEntryMetre,freeRideEntryLane;
    string freeRideQueuedTrick;
    bool freeRideFalling;
    RaceWorld.FreeRideGap fallingGap;

    void ResetFreeRideInput(){
        freeRideQueuedLip=float.NaN;freeRideQueuedTrick=null;freeRideFalling=false;
    }
    bool FreeRideJumpWindow(out float gapID,float lateralMargin=.55f){
        gapID=float.NaN;
        if(!World.AlpineAt(Status.metres)||Status.phase!="play"||airborne||freeRideFalling||fall>0||crashTimer>0)return false;
        float d=Status.metres;
        if(!World.TryFreeRideGap(d,out var gap))return false;
        if(!gap.TryFrontLip(lane,out float localLip)&&!gap.TryFrontLip(lane-lateralMargin,out localLip)&&
            !gap.TryFrontLip(lane+lateralMargin,out localLip))return false;
        float ahead=localLip-d;
        if(ahead<0||ahead>Mathf.Clamp(speed*1.10f,14,27))return false;
        gapID=gap.lipD;return true;
    }

    float FreeRideClearance(float d,float x){
        float time=(d-flightStart)/freeRideTakeoffSpeed;
        return RaceWorld.FreeRideFlightY(freeRideOriginY,freeRideVertical,time)-World.SampleGround(d,x).y;
    }
    void LaunchFromCornice(RaceWorld.FreeRideGap gap,float originD,float x,float pace){
        airborne=parkFlight=true;trickLine=prizeLine=false;
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
        ChooseAirPose(airMax-airTime);
    }
    void FallIntoFreeRideGap(RaceWorld.FreeRideGap gap){
        if(freeRideFalling)return;
        fallingGap=gap;freeRideFalling=true;freeRideQueuedLip=float.NaN;
        freeRideDropSince=Status.elapsed;freeRideEntryMetre=Status.metres;freeRideEntryLane=lane;
        Hit("crevasse");
        // The rider is already below these prizes. Clear the sightline into this
        // fissure without consuming the fruit or running its reward effects.
        foreach(var item in World.Items){
            if(item.kind=="coin"&&item.air&&item.distance>gap.lipD&&item.distance<gap.endD&&item.obj)
                item.obj.SetActive(false);
        }
        velocity=slip=angle=0;edgeReturn=false;rampHeight=0;nextHud=0;
    }
    void StepCrevasseFall(float dt,bool drawWorld){
        float age=Status.elapsed-freeRideDropSince;
        // Enter along the current direction, then sink into the wider middle of the fissure.
        float middle=Mathf.Max(freeRideEntryMetre,fallingGap.SectionDistance(4));
        float into=1-Mathf.Pow(Mathf.Clamp01(1-age/.72f),2);
        Status.metres=Mathf.Lerp(freeRideEntryMetre,middle,into);
        lane=Mathf.Lerp(freeRideEntryLane,fallingGap.CentreAt(Status.metres),into);
        rampHeight=-Mathf.Min(5.0f,6.8f*age*age);
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
        float d=Status.metres;rampHeight=rampPhase=0;
        if(airborne||freeRideFalling||fall>0||crashTimer>0||!World.TryFreeRideGap(d,out var gap))return;
        if(freeRideQueuedLip==gap.lipD&&d>gap.SectionDistance(3)){freeRideQueuedLip=float.NaN;freeRideQueuedTrick=null;}
        if(!gap.OverOpening(d,lane))return;
        if(!gap.OverOpening(before,fromLane)&&before<gap.SectionDistance(3)){
            float lo=0,hi=1;
            for(int i=0;i<9;i++){
                float mid=(lo+hi)*.5f;
                if(gap.OverOpening(Mathf.Lerp(before,d,mid),Mathf.Lerp(fromLane,lane,mid)))hi=mid;else lo=mid;
            }
            float entry=Mathf.Lerp(before,d,hi),x=Mathf.Lerp(fromLane,lane,hi);
            if(freeRideQueuedLip==gap.lipD&&gap.TryFrontLip(x,out float lip)&&Mathf.Abs(entry-lip)<.035f){
                LaunchFromCornice(gap,entry,x,speed);freeRideQueuedLip=float.NaN;
                AirTrick(freeRideQueuedTrick);freeRideQueuedTrick=null;return;
            }
        }
        FallIntoFreeRideGap(gap);
    }
    void PreviewFreeRideJump(){
        float d=Status.metres;
        if(!World.TryFreeRideGap(d,out var gap)||d<=gap.lipD||d>gap.endD+30||!gap.TryFrontLip(lane,out float lip)||d<=lip)return;
        speed=World.FreeRidePace(lip);LaunchFromCornice(gap,lip,lane,speed);
        if(airTime>=airMax){airborne=parkFlight=false;}
    }
}
