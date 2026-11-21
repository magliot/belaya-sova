using System;
using System.Collections.Generic;
using UnityEngine;

public partial class MountainRace {
    enum PatrolPhase { Riding, Braking, Stopped, Called, Identified, Walking, Waiting, Looking }
    PatrolPhase patrol;
    RaceItem patient;
    readonly List<int> ruleContexts=new List<int>();
    float stopMetre,stopDeceleration,walkStart,lookSince,brakeUntil,brakeFrom,rejoinUntil,lastFisMessage;
    bool keptToEdge;
    bool FisCourse=>Status.mode=="normal"||Status.mode=="free";
    const float SafePassGap=3f;

    void ResetFis(){
        ShowFisWalker(false);
        patrol=PatrolPhase.Riding;patient=null;ruleContexts.Clear();
        brakeUntil=rejoinUntil=0;lastFisMessage=-5;
    }
    void FisRule(int rule,int situation,string message){
        if(!FisCourse||Status.phase!="play"||fall>0||crashTimer>0)return;
        int context=rule*1000+Status.stage*10+situation;
        if(ruleContexts.Contains(context))return;
        ruleContexts.Add(context);Status.fisContexts=ruleContexts.ToArray();
        if(!earnedRules.Contains(rule)){earnedRules.Add(rule);Status.fis=earnedRules.ToArray();}
        if(Status.elapsed-lastFisMessage>=1.8f){
            lastFisMessage=Status.elapsed;Notice("FIS "+rule+" · "+message,"chime");
        }
    }
    void ObserveRider(RaceItem rider,float ahead,float dx){
        float clearance=Mathf.Abs(dx);
        if(ahead>=13&&ahead<=24&&!airborne&&fall<=0){
            if(rider.fisApproachSide==0&&clearance>=SafePassGap){
                rider.fisApproachSide=dx>0?1:-1;
            }
        }
        if(Mathf.Abs(ahead)<=1.75f)rider.fisClearance=Mathf.Min(rider.fisClearance,clearance);
    }
    void SafePass(RaceItem rider){
        if(rider.fisClearance<SafePassGap||float.IsInfinity(rider.fisClearance))return;
        Status.polite++;
        if(!FisCourse)return;
        bool schoolGroup=Status.metres<120;
        int type=rider.kid?1:rider.sb?2:rider.instr?3:rider.walker?4:rider.sit?5:6;
        if(rider.kid||rider.instr||rider.stand||rider.sit||rider.walker){
            FisRule(1,schoolGroup?1:type,"Оставил место");
        }else if(!schoolGroup&&rider.pace>0){
            FisRule(4,type,"Обгон с запасом");
        }
        if(!schoolGroup&&!rider.stand&&!rider.sit&&!rider.walker&&rider.fisApproachSide*(lane-rider.lane)>=SafePassGap)
            FisRule(3,type,"Линия выбрана заранее");
    }
    void FisHints(float before,float now){
        if(!settings.showFisHints||!FisCourse)return;
        if(before<2&&now>=2)Notice("Оставь ученикам место для поворота");
        if(before<257&&now>=257)Notice("Помощь — у левого края");
    }
    float Shoulder(RaceItem sitter)=>World.PatrolShoulder(sitter);
    RaceItem NearbyPatient(){
        if(airborne||fall>0||crashTimer>0||chaseLeft>0)return null;
        foreach(var sitter in World.Items){
            if(!sitter.patrolPatient)continue;
            float ahead=sitter.distance-Status.metres;
            if(ahead<5||ahead>48||Mathf.Abs(lane-Shoulder(sitter))>1.45f)continue;
            return sitter;
        }
        return null;
    }
    int BrakingSituation(){
        if(Status.stage==4&&Status.blizzard>.35f)return 1;
        if(Status.stage==6)return 2;
        foreach(var o in World.Items){
            if(o.kind!="npc"||o.sit||o.distance<Status.metres+2||o.distance>Status.metres+38||Mathf.Abs(o.lane-lane)>6)continue;
            return Status.metres<120||o.kid||o.instr?3:4;
        }
        return 0;
    }
    bool ClearToRejoin(){
        foreach(var o in World.Items){
            if(o.kind!="cat"&&o.kind!="snowmobile"&&o.kind!="tube"&&(o.kind!="npc"||o.stand||o.sit))continue;
            float ahead=o.distance-Status.metres;
            if(ahead>=-12&&ahead<=10&&Mathf.Abs(o.lane-lane)<3.2f)return false;
        }
        return true;
    }
    void RefreshFisAction(){
        Status.fisAction="";Status.fisCanLeave=false;
        if(Status.phase!="play"||!FisCourse||!World||fall>0||crashTimer>0)return;
        Status.fisCanLeave=patrol>=PatrolPhase.Stopped;
        switch(patrol){
            case PatrolPhase.Riding:
                if(NearbyPatient()!=null)Status.fisAction="stop";
                else if(!airborne&&brakeUntil<=Status.elapsed&&BrakingSituation()!=0)Status.fisAction="brake";
                break;
            case PatrolPhase.Stopped:Status.fisAction="patrol";break;
            case PatrolPhase.Called:Status.fisAction="witness";break;
            case PatrolPhase.Identified:Status.fisAction="walk";break;
            case PatrolPhase.Waiting:Status.fisAction="look";break;
            case PatrolPhase.Looking:if(Status.elapsed-lookSince>=1&&ClearToRejoin())Status.fisAction="join";break;
        }
    }
    // Actions are rechecked against the current simulation: old HUD buttons cannot award a rule.
    public void FisAction(string action){
        RefreshFisAction();
        if(action=="leave"&&Status.fisCanLeave){LeavePatrol();Publish();return;}
        if(string.IsNullOrEmpty(action)||action!=Status.fisAction)return;
        switch(action){
            case "brake":
                brakeFrom=speed;brakeUntil=Status.elapsed+2.6f;break;
            case "stop":
                patient=NearbyPatient();if(patient==null)return;
                patrol=PatrolPhase.Braking;dragTarget=lane;stopMetre=patient.distance-3.7f;
                stopDeceleration=speed*speed/(2*Mathf.Max(.5f,stopMetre-Status.metres));break;
            case "patrol":
                patrol=PatrolPhase.Called;FisRule(9,1,"Патруль вызван");break;
            case "witness":
                patrol=PatrolPhase.Identified;FisRule(10,1,"Свидетель назвался");break;
            case "walk":
                patrol=PatrolPhase.Walking;walkStart=Status.metres;keptToEdge=true;ShowFisWalker(true);break;
            case "look":
                lookSince=Status.elapsed;patrol=PatrolPhase.Looking;Notice("Смотри вверх и вниз по склону");break;
            case "join":
                FisRule(5,1,"Безопасный выезд");LeavePatrol();break;
        }
        Publish();
    }
    void LeavePatrol(){
        ShowFisWalker(false);patrol=PatrolPhase.Riding;patient=null;
        brakeUntil=0;rejoinUntil=Status.elapsed+3;travelYaw=World.HeadingAt(Status.metres);
        dragTarget=lane;speed=Mathf.Max(2,speed);
    }
    readonly List<Transform> walkingJoints=new List<Transform>();
    readonly List<Quaternion> walkingRest=new List<Quaternion>();
    readonly List<Transform> walkingEquipment=new List<Transform>();
    void ShowFisWalker(bool show){
        if(!menuModel||!rideModel)return;
        if(!show){
            if(walkingJoints.Count==0)return;
            for(int i=0;i<walkingJoints.Count;i++)walkingJoints[i].localRotation=walkingRest[i];
            foreach(var part in walkingEquipment)part.gameObject.SetActive(true);
            walkingEquipment.Clear();walkingRest.Clear();walkingJoints.Clear();
        }else{
            foreach(var part in menuModel.GetComponentsInChildren<Transform>(true)){
                string n=part.name;
                if(n.EndsWith("UpLeg")||n.EndsWith("Leg")||n.EndsWith("Arm")){
                    walkingJoints.Add(part);walkingRest.Add(part.localRotation);
                }
                if((n.EndsWith("_Pole")||n.EndsWith("_Ski")||n.EndsWith("_Binding")||n=="Snowboard")&&part.gameObject.activeSelf){
                    walkingEquipment.Add(part);part.gameObject.SetActive(false);
                }
            }
        }
        menuModel.gameObject.SetActive(show);rideModel.gameObject.SetActive(!show);
    }
    void PoseFisWalker(){
        float step=Mathf.Sin((Status.metres-walkStart)*4.7f);
        for(int i=0;i<walkingJoints.Count;i++){
            var joint=walkingJoints[i];string n=joint.name;
            float wave=n.Contains("Left")?step:-step;
            float angle=n.EndsWith("UpLeg")?wave*19:n.EndsWith("Leg")?Mathf.Max(0,-wave)*22:n.EndsWith("ForeArm")?0:-wave*13;
            joint.localRotation=walkingRest[i]*Quaternion.Euler(angle,0,0);
        }
        string key=Hero=="bear"?"Bear":"Owl";float scale=Hero=="bear"?1.30f:1.32f;
        menuModel.localScale=Vector3.one*scale;
        menuModel.localRotation=Quaternion.identity;
        menuModel.localPosition=Vector3.up*(menuLifts[key]*scale+Mathf.Abs(step)*.027f);
    }
    float FisApproachSpeed(float previous,float desired,float dt){
        if(!FisCourse)return desired;
        if(patrol==PatrolPhase.Braking){
            if(fall>0||airborne||Mathf.Abs(lane-Shoulder(patient))>1.7f){LeavePatrol();return desired;}
            float remaining=Mathf.Max(0,stopMetre-Status.metres);
            return Mathf.Min(desired,Mathf.Sqrt(2*stopDeceleration*remaining),remaining/dt);
        }
        if(brakeUntil>Status.elapsed&&!airborne){
            float reduced=Mathf.MoveTowards(previous,Mathf.Min(desired,brakeFrom*.57f),12*dt);
            if(reduced<brakeFrom*.78f&&brakeUntil-Status.elapsed<2.25f){
                int context=BrakingSituation();
                if(context!=0)FisRule(2,context,"Скорость под контролем");
            }
            return reduced;
        }
        return Status.elapsed<Mathf.Max(rejoinUntil,brakeUntil+1.8f)&&Mathf.Max(rejoinUntil,brakeUntil)>0&&!airborne
            ?Mathf.MoveTowards(previous,desired,12*dt):desired;
    }
    bool StepFisStop(float dt,float keyboard,bool drawWorld){
        if(patrol==PatrolPhase.Riding)return false;
        if(patrol==PatrolPhase.Braking){
            if(Status.metres<stopMetre-.06f)return false;
            if(fall>0||airborne||Mathf.Abs(lane-Shoulder(patient))>1.7f){LeavePatrol();return false;}
            patrol=PatrolPhase.Stopped;velocity=slip=angle=0;dragTarget=lane;
            FisRule(6,1,"Остановка у края");
        }
        float before=Status.metres,oldLane=lane;
        bool walking=patrol==PatrolPhase.Walking;
        speed=walking?1.65f:0;
        if(walking){
            if(keyboard!=0)lane+=keyboard*2.1f*dt;
            else if(dragging)lane=Mathf.MoveTowards(lane,dragTarget,2.1f*dt);
            lane=Mathf.Clamp(lane,-LaneLimit(before)+.1f,LaneLimit(before)-.1f);
            keptToEdge&=Mathf.Abs(lane-Shoulder(patient))<1.7f;
            Status.metres+=speed*dt;
        }
        World.SetThrowTarget(lane,speed,0,0);World.Refresh(Status.metres,Status.elapsed,lane,drawWorld);
        CheckItems(before,oldLane);TickTimers(dt);
        if(fall>0||crashTimer>0){LeavePatrol();return true;}
        if(walking&&Status.metres-walkStart>=6){
            if(keptToEdge)FisRule(7,1,"Пешком по обочине");
            patrol=PatrolPhase.Waiting;speed=0;ShowFisWalker(false);
        }
        return true;
    }
}
