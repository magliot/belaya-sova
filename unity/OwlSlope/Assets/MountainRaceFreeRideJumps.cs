using UnityEngine;

public partial class MountainRace {
    float freeRideOriginY,freeRideVertical,freeRideTakeoffSpeed;

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
    }
    void FallIntoFreeRideGap(){
        Hit("crevasse");
        if(fall>0&&!edgeReturn){edgeOrigin=lane;edgeReturn=true;}
    }
    void FreeRideContact(float before,float fromLane){
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
                LaunchFromCornice(gap,entry,x,speed);Cue("jump");return;
            }
        }
        FallIntoFreeRideGap();
    }
    void PreviewFreeRideJump(){
        float d=Status.metres;
        if(!World.TryFreeRideGap(d,out var gap)||d<=gap.lipD||d>gap.endD+30||!gap.TryFrontLip(lane,out float lip)||d<=lip)return;
        speed=World.FreeRidePace(lip);LaunchFromCornice(gap,lip,lane,speed);
        if(airTime>=airMax){airborne=parkFlight=false;}
    }
}
