using UnityEngine;

public partial class MountainRace {
    // Inclusive Blender scene frames, exported together so skis, bindings and
    // glove-mounted poles are sampled at precisely the same instant as the skin.
    static readonly int[] BearJumpStart={442,519};
    static readonly int[] BearJumpEnd={518,600};
    System.Random jumpRandom;
    int airPose=-1,lastBearJump=-1;
    float poseStart,poseDuration,poseFrom,poseTo,switchStarted,switchFrame;
    const float SwitchRecovery=.38f;
    const float BearCrouch=(194-184)/30f;

    void ResetJumpPoses(){
        airPose=lastBearJump=-1;switchStarted=float.NegativeInfinity;
        jumpRandom=new System.Random(settings.seed^48173);
    }
    void ChooseAirPose(float duration,bool finishOnSnow=false){
        if(jumpRandom==null)ResetJumpPoses();
        airPose=0;
        if(Hero=="bear"){
            if(Review)airPose=RaceWorld.Roll(settings.seed,Mathf.FloorToInt(flightStart),492)<.5f?0:1;
            else airPose=lastBearJump=lastBearJump<0?jumpRandom.Next(2):1-lastBearJump;
        }
        poseStart=Review?0:airTime;poseDuration=Mathf.Max(.1f,duration);poseFrom=0;
        poseTo=finishOnSnow&&Hero=="bear"&&airPose==0?.52f:1;
        switchStarted=float.NegativeInfinity;
    }
    void EndAirPose(){
        if(Hero=="bear"&&airPose==0&&poseTo<1){
            switchFrame=PoseTime;switchStarted=Status.elapsed;
        }
    }
    float PoseTime=>Mathf.Lerp(poseFrom,poseTo,(airTime-poseStart)/Mathf.Max(.1f,poseDuration));
    string AirPoseName=>Hero=="owl"?"Оборот вправо":airPose==0?"Вращение 360°":"Сальто назад";
    void RequestAirPose(float length){
        if(flightTricks==0&&airPose>=0){
            // Continue the movement already visible on takeoff. A tap changes the
            // time left for completion without jumping back to its first pose.
            poseFrom=PoseTime;poseTo=1;poseStart=airTime;poseDuration=length;
        }else{
            ChooseAirPose(length);
        }
    }
    Vector3 SnowboardTurn(){
        if(!airborne||Hero!="owl"||airPose<0||BonusMode)return Vector3.zero;
        float t=PoseTime;
        float spin=360*(t-.82f*Mathf.Sin(2*Mathf.PI*t)/(2*Mathf.PI));
        return new Vector3(0,spin,8*Mathf.Sin(t*Mathf.PI));
    }
    float SampleBearPose(){
        float age=Status.elapsed-switchStarted;
        bool recovery=!airborne&&age>=0&&age<SwitchRecovery&&fall<=0&&crashTimer<=0;
        if(!recovery&&(!airborne||airPose<0)){
            rideClip.SampleAnimation(rideModel.gameObject,BearCrouch);
            return 0;
        }
        float t=recovery?Mathf.Lerp(switchFrame,1,StepBlend((age-.055f)/(SwitchRecovery-.055f))):PoseTime;
        float frame=Mathf.Lerp(BearJumpStart[airPose],BearJumpEnd[airPose],t);
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
