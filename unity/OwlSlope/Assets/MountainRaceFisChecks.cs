#if UNITY_EDITOR
using System;
using UnityEngine;

public partial class MountainRace {
    float CheckFisPatrol(float station){
        Checkpoint(station-22,0);
        var sitter=World.Items.Find(o=>o.sit&&Mathf.Abs(o.distance-station)<2);
        Check(sitter!=null&&sitter.patrolPatient&&World.Items.Exists(o=>o.kind=="aid"&&Mathf.Abs(o.distance-station)<2),
            "patient or first-aid kit missing at "+station);
        float edge=Shoulder(sitter);
        Checkpoint(station-18,0);FisAction("stop");FisAction("patrol");FisAction("witness");
        Check(patrol==PatrolPhase.Riding,"patrol called from the centre");
        Checkpoint(station-18,edge);speed=20;ReleaseSteer();FisAction("stop");
        Check(patrol==PatrolPhase.Braking,"no stop at "+station+" lane "+edge);
        for(int i=0;i<400&&patrol==PatrolPhase.Braking&&Status.phase=="play";i++)Advance(1f/60);
        Check(patrol==PatrolPhase.Stopped&&speed<.01f,"failed to stop at "+station+" phase "+patrol+" lane "+lane);
        View.aspect=.5f;RenderRace(0,true);
        var riderOnScreen=View.WorldToViewportPoint(actorRoot.position+Vector3.up*.9f);
        var patientOnScreen=View.WorldToViewportPoint(sitter.obj.transform.position+Vector3.up*.45f);
        View.ResetAspect();
        Check(riderOnScreen.x>.12f&&riderOnScreen.x<.88f&&riderOnScreen.y>0&&riderOnScreen.y<1
            &&patientOnScreen.x>.02f&&patientOnScreen.x<.98f&&patientOnScreen.y>0&&patientOnScreen.y<1,
            "portrait camera misses rider "+riderOnScreen+" or patient "+patientOnScreen);
        FisAction("witness");Check(patrol==PatrolPhase.Stopped,"reported without contacting patrol");
        FisAction("patrol");FisAction("witness");FisAction("walk");
        for(int i=0;i<390&&patrol==PatrolPhase.Walking&&Status.phase=="play";i++)Advance(1f/60);
        Check(patrol==PatrolPhase.Waiting&&keptToEdge,"could not finish walking past "+station);
        FisAction("look");FisAction("join");Check(patrol==PatrolPhase.Looking,"rejoined without looking");
        for(int i=0;i<480&&Status.phase=="play"&&Status.fisAction!="join";i++)Advance(1f/60);
        FisAction("join");Check(patrol==PatrolPhase.Riding&&Status.phase=="play","could not safely rejoin at "+station);
        return edge;
    }
    public string VerifyFisSituations(){
        Menu();ChooseHero("bear");StartRace("{\"mode\":\"normal\",\"seed\":1911,\"sensitivity\":4}");
        // Real school pupils. The first school group introduces only spacing and braking.
        BeginSteer();
        for(int i=0;i<900&&Status.metres<52&&Status.phase=="play";i++){Advance(1f/60);if(Status.fisAction=="brake")FisAction("brake");}
        Check(earnedRules.Count==2&&earnedRules.Contains(1)&&earnedRules.Contains(2),"school passage failed at "+Status.metres);
        float schoolEdge=CheckFisPatrol(289);
        foreach(int n in new[]{5,6,7,9,10})Check(earnedRules.Contains(n),"missing stop rule "+n);
        // Moving away after passing close cannot turn the encounter into a safe overtake.
        var closeSkier=new RaceItem{kind="npc",pace=3.4f};
        int passes=Status.polite,ruleCount=earnedRules.Count;
        ObserveRider(closeSkier,19,4.2f);ObserveRider(closeSkier,.4f,2.2f);ObserveRider(closeSkier,-1.5f,3.7f);
        SafePass(closeSkier);
        Check(Status.polite==passes&&earnedRules.Count==ruleCount,"close pass granted a reward");
        // Give the downhill school group room and rejoin at the next gate.
        Checkpoint(360,0);
        var boarder=World.Items.Find(o=>o.sb&&o.pace>0&&o.originD>=375&&o.originD<400);
        Check(boarder!=null,"moving boarder missing from the school");
        var nextGate=World.Items.Find(o=>o.kind=="gate"&&Mathf.Abs(o.originD-400)<.02f);
        Check(nextGate!=null,"missing gate beyond school group");
        Checkpoint(boarder.distance-22,nextGate.lane);
        for(int i=0;i<320&&!boarder.judged&&Status.phase=="play";i++){
            DragSteer((nextGate.lane-dragStart)/(27*sensitivity));Advance(1f/60);
        }
        Check(earnedRules.Contains(3)&&earnedRules.Contains(4),"safe line and overtake at "+Status.metres+" "+Status.phase);
        // Cross the safe end of the actual signed summit net, starting four metres before it.
        bool passedSign=false;
        for(float at=16040;at<5*RaceWorld.StageLength&&!passedSign;at+=60){
            Checkpoint(at,0);
            var fence=World.Items.Find(o=>o.kind=="fence"&&o.distance>=at&&Mathf.Abs(o.lane)>1);
            if(fence==null)continue;
            float x=fence.lane-Mathf.Sign(fence.lane)*(fence.width*.5f+1.5f);
            Check(Mathf.Abs(x)<LaneLimit(fence.distance),"marked bypass beyond the bank");
            Checkpoint(fence.distance-4,x);
            for(int i=0;i<120&&Status.metres<fence.distance+3&&!earnedRules.Contains(8)&&Status.phase=="play";i++)Advance(1f/60);
            passedSign=earnedRules.Contains(8);
        }
        int first=Array.Find(Status.fisContexts,c=>c/1000==9);
        float holidayEdge=CheckFisPatrol(28137);
        Check(earnedRules.FindAll(n=>n==9).Count==1&&Array.Exists(Status.fisContexts,c=>c/1000==9&&c!=first),
            "another accident must change context and only count once");
        Check(passedSign&&earnedRules.Count==10,"ten rules not completed with real controls: "+string.Join(",",earnedRules)+" at "+Status.metres+" "+Status.phase+" "+Status.message);
        RestartRide();Check(Status.fis.Length==0&&Status.fisContexts.Length==0,"FIS carried over into new score");
        return "10 FIS actions on real course; patrol at 289 m (lane "+schoolEdge.ToString("0.0")+
            ") / 28137 m (lane "+holidayEdge.ToString("0.0")+"), contexts 9001 and 9071, one rule per attempt";
    }
}
#endif
