using UnityEngine;

public partial class MountainRace {
    // Inclusive Blender scene frames, exported together so skis, bindings and
    // glove-mounted poles are sampled at precisely the same instant as the skin.
    static readonly int[] BearJumpStart={442,519};
    static readonly int[] BearJumpEnd={518,600};
    // In both FBX clips most of the angular travel is baked into the middle third.
    // Source times at equal intervals of ski rotation (with 18% weight to elapsed
    // time to retain the authored takeoff and extension). Measured on Left_Ski.
    static readonly float[] BearTurnFrames={0,.169f,.262f,.323f,.362f,.393f,.420f,.444f,.467f,.490f,.514f,.539f,.566f,.598f,.640f,.705f,1};
    System.Random jumpRandom;
    int airPose=-1,lastJump=-1;
    float poseStart,poseDuration,poseFrom,poseTo,switchStarted,switchFrame;
    const float SwitchRecovery=.38f;
    const float TurnSeconds=.78f;
    bool parkTapQueued;
    bool SnowParkFlight=>parkFlight&&Status.stage==3&&!BonusMode;
    const float BearCrouch=(194-184)/30f;

    void ResetJumpPoses(){
        airPose=lastJump=-1;switchStarted=float.NegativeInfinity;
        jumpRandom=new System.Random(settings.seed^48173);
    }
    void ChooseAirPose(float duration,bool finishOnSnow=false){
        if(jumpRandom==null)ResetJumpPoses();
        if(Review)airPose=RaceWorld.Roll(settings.seed,Mathf.FloorToInt(flightStart),492)<.5f?0:1;
        else airPose=lastJump=lastJump>=0?1-lastJump:Hero=="owl"?1:jumpRandom.Next(2);
        poseStart=Review?0:airTime;poseDuration=Mathf.Max(.1f,duration);poseFrom=0;
        poseTo=finishOnSnow&&Hero=="bear"&&airPose==0?.52f:1;
        if(SnowParkFlight)poseDuration=TurnSeconds*poseTo;
        switchStarted=float.NegativeInfinity;
    }
    void EndAirPose(){
        if(Hero=="bear"&&airPose==0&&poseTo<1){
            switchFrame=PoseTime;switchStarted=Status.elapsed;
        }
    }
    float PoseTime{
        get{
            if(SnowParkFlight&&airMax>1.1f&&poseTo<1){
                // Set the skis at takeoff, then turn into the switch landing.
                // On a high kicker the bear waits in the wind-up rather than
                // spending the flight backwards. A tap resumes from this pose.
                const float windup=.16f;
                float entry=Mathf.Clamp((airTime-poseStart)/TurnSeconds,0,windup);
                float finish=Mathf.Max(0,poseTo-windup-(airMax-airTime)/TurnSeconds);
                return Mathf.Min(poseTo,entry+finish);
            }
            return Mathf.Lerp(poseFrom,poseTo,(airTime-poseStart)/Mathf.Max(.001f,poseDuration));
        }
    }
    string AirPoseName=>airPose==1?"Сальто назад":Hero=="owl"?"Оборот вправо":"Вращение 360°";
    bool RequestParkPose(){
        // A tap claims the turn already in progress. Its clock keeps running at
        // the same rate, including the second rotation on the feather jump.
        bool continuing=flightTricks==0&&airPose>=0&&PoseTime<1;
        float from=continuing?PoseTime:0,seconds=(1-from)*TurnSeconds;
        if(airMax-airTime+.003f<seconds)return false;
        if(!continuing)ChooseAirPose(TurnSeconds);
        poseFrom=from;poseTo=1;poseStart=airTime;poseDuration=trickLength=Mathf.Max(.001f,seconds);
        return true;
    }
    void RequestAirPose(float length){
        if(flightTricks==0&&airPose>=0){
            // Continue the movement already visible on takeoff. A tap changes the
            // time left for completion without jumping back to its first pose.
            poseFrom=PoseTime;poseTo=1;poseStart=airTime;poseDuration=length;
        }else{
            ChooseAirPose(length);
        }
    }
    static float TurnPhase(float t)=>t-.38f*Mathf.Sin(2*Mathf.PI*t)/(2*Mathf.PI);
    Vector3 SnowboardTurn(){
        if(!airborne||Hero!="owl"||airPose<0||BonusMode)return Vector3.zero;
        float t=PoseTime;
        float degrees=360*TurnPhase(t),bank=Mathf.Sin(t*Mathf.PI);
        return airPose==1?new Vector3(-degrees,0,5*bank*bank):new Vector3(0,degrees,8*bank);
    }
    float SampleBearPose(){
        float age=Status.elapsed-switchStarted;
        bool recovery=!airborne&&age>=0&&age<SwitchRecovery&&fall<=0&&crashTimer<=0;
        if(!recovery&&(!airborne||airPose<0)){
            rideClip.SampleAnimation(rideModel.gameObject,BearCrouch);
            return 0;
        }
        float t=recovery?Mathf.Lerp(switchFrame,1,StepBlend((age-.055f)/(SwitchRecovery-.055f))):PoseTime;
        float index=t*(BearTurnFrames.Length-1);
        int key=Mathf.Clamp((int)index,0,BearTurnFrames.Length-2);
        float source=Mathf.Lerp(BearTurnFrames[key],BearTurnFrames[key+1],index-key);
        float frame=Mathf.Lerp(BearJumpStart[airPose],BearJumpEnd[airPose],source);
        float mix=StepBlend(t/.10f)*StepBlend((1-t)/.12f);
        ridePose.Sample(rideClip,rideModel.gameObject,BearCrouch,(frame-184)/30,mix);
        return 0;
    }
    float SampleOwlJump(float flight){
        float baseline=3.25f+.83f*flight;
        rideClip.SampleAnimation(rideModel.gameObject,baseline);
        return .28f*4*flight*(1-flight);
    }
}
