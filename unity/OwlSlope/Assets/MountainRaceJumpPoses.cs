using UnityEngine;

public partial class MountainRace {
    // Inclusive Blender scene frames, exported together so skis, bindings and
    // glove-mounted poles are sampled at precisely the same instant as the skin.
    static readonly int[] BearJumpStart={184,236,299,369,442,519,601};
    static readonly int[] BearJumpEnd={235,298,368,441,518,600,674};
    static readonly string[] BearJumpNames={"Подскок с двух лыж","Прямой прыжок","Шифти","Спред-игл","Вращение 360°","Сальто назад","180° в свитч"};
    static readonly string[] OwlJumpNames={"Олли","Indy","Melon","Frontside 360 · Indy","Backside 360 · Melon"};
    int[] jumpOrder;
    System.Random jumpRandom;
    int jumpSlot,airPose=-1;
    float poseStart,poseDuration,stanceYaw;
    const float BearCrouch=(194-184)/30f;

    void ResetJumpPoses(){
        int count=Hero=="bear"?BearJumpNames.Length:OwlJumpNames.Length;
        jumpOrder=new int[count];jumpSlot=count;airPose=-1;stanceYaw=0;
        jumpRandom=new System.Random(settings.seed^(Hero=="bear"?48173:95311));
    }
    void ChooseAirPose(float duration){
        if(jumpOrder==null)ResetJumpPoses();
        if(Review){
            airPose=Mathf.Min(jumpOrder.Length-1,(int)(jumpOrder.Length*RaceWorld.Roll(settings.seed,Mathf.FloorToInt(flightStart),492)));
            poseStart=0;poseDuration=airMax;return;
        }
        if(jumpSlot==jumpOrder.Length){
            for(int i=0;i<jumpOrder.Length;i++)jumpOrder[i]=i;
            for(int i=jumpOrder.Length-1;i>0;i--){
                int j=jumpRandom.Next(i+1);
                int previous=jumpOrder[j];jumpOrder[j]=jumpOrder[i];jumpOrder[i]=previous;
            }
            if(jumpOrder[0]==airPose){int j=1+jumpRandom.Next(jumpOrder.Length-1);jumpOrder[0]=jumpOrder[j];jumpOrder[j]=airPose;}
            jumpSlot=0;
        }
        airPose=jumpOrder[jumpSlot++];poseStart=airTime;poseDuration=Mathf.Max(.1f,duration);
    }
    void EndAirPose(){
        if(Hero=="bear"&&airPose==6)stanceYaw=Mathf.Repeat(stanceYaw+180,360);
    }
    float PoseTime=>Mathf.Clamp01((airTime-poseStart)/Mathf.Max(.1f,poseDuration));
    string AirPoseName=>Hero=="bear"?BearJumpNames[airPose]:OwlJumpNames[airPose];
    void RequestAirPose(float length){
        if(flightTricks==0&&airPose>=0){
            // Continue the movement already visible on takeoff. A tap changes the
            // time left for completion without jumping back to its first pose.
            float t=PoseTime;
            poseDuration=length/Mathf.Max(.03f,1-t);poseStart=airTime-t*poseDuration;
        }else{
            EndAirPose();ChooseAirPose(length);
        }
    }
    Vector3 SnowboardTurn(){
        if(!airborne||Hero!="owl"||airPose<3||BonusMode)return Vector3.zero;
        float t=PoseTime;
        float spin=360*(t-.82f*Mathf.Sin(2*Mathf.PI*t)/(2*Mathf.PI));
        return new Vector3(0,airPose==3?spin:-spin,(airPose==3?8:-9)*Mathf.Sin(t*Mathf.PI));
    }
    float SampleBearPose(){
        if(!airborne||airPose<0){
            rideClip.SampleAnimation(rideModel.gameObject,BearCrouch);
            return 0;
        }
        float frame=Mathf.Lerp(BearJumpStart[airPose],BearJumpEnd[airPose],PoseTime);
        float t=PoseTime, mix=StepBlend(t/.10f);
        if(airPose!=6)mix*=StepBlend((1-t)/.12f);
        ridePose.Sample(rideClip,rideModel.gameObject,BearCrouch,(frame-184)/30,mix);
        return 0;
    }
    float SampleOwlJump(float flight){
        float baseline=3.25f+.83f*flight;
        if(airPose<=0){rideClip.SampleAnimation(rideModel.gameObject,baseline);return .28f*4*flight*(1-flight);}
        float t=PoseTime,mix=StepBlend(t/.25f)*StepBlend((1-t)/.29f);
        bool toe=airPose==1||airPose==3;
        ridePose.Sample(rideClip,rideModel.gameObject,baseline,(toe?6:7)+Mathf.Min(.966f,t),mix);
        return .28f*4*flight*(1-flight)*(1-mix);
    }
}
