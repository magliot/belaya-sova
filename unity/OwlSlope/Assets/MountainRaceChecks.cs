#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public partial class MountainRace {
    static void Check(bool valid,string description){
        if(!valid)throw new InvalidOperationException("MOUNTAIN MECHANICS: "+description);
    }
    // Fast-forward to a reproducible checkpoint. The subsequent frames use the same
    // generator, collision shapes, input commands and simulation as a Web player.
    void Checkpoint(float metres,float x){
        Status.metres=metres;Status.elapsed=metres/18;
        lane=x;dragging=false;velocity=slip=0;travelYaw=World.HeadingAt(metres);
        World.Refresh(metres,Status.elapsed);BeginSteer();
    }
    string VerifyPrize(string hero,int taps){
        Menu();ChooseHero(hero);StartRace("{\"mode\":\"normal\",\"seed\":1911,\"sensitivity\":4}");
        Checkpoint(7620,0);
        var ramp=World.Items.Find(o=>o.kind=="ramp"&&o.prizeLine);
        var feather=World.Items.Find(o=>o.kind=="feather");
        Check(ramp!=null&&feather!=null,"prize row missing from seed 1911");
        float target=feather.distance;
        Checkpoint(ramp.distance-ramp.length*.5f-1,ramp.lane);
        int gestures=0;
        for(int frame=0;frame<900&&Status.metres<target+2&&Status.phase=="play";frame++){
            if(airborne&&trickProgress<0&&gestures<taps){AirTrick("tap");if(trickProgress>=0)gestures++;}
            Advance(1f/60);
        }
        Publish();
        string result=hero+" @"+target.ToString("F1")+"m, "+Status.tricks+" tricks, "+Status.rings+" rings, feather "+Status.feather;
        Check(Status.phase=="play"&&Status.metres>=target,result+" failed to reach target: "+Status.message);
        Check(Status.tricks==taps,result+" gesture count");
        Check(Status.feather==(taps==2)&&Status.tubingUnlocked==Status.feather&&Status.sledUnlocked==Status.feather,result+" feather gate");
        Check(Status.rings==(taps==0?0:1),result+" upper ring");
        return result;
    }
    public string VerifyMechanics(){
        Initialize();var report=new List<string>();
        try{
            Menu();ChooseHero("owl");var front=MenuActor.localRotation;
            MenuSpin(31);var dragged=MenuActor.localRotation;Check(Quaternion.Angle(front,dragged)>15,"menu rotation");
            EndMenuSpin();RenderMenu(1f/60);Check(Quaternion.Angle(MenuActor.localRotation,dragged)>1,"menu rotation inertia");
            ChooseHero("bear");Check(Quaternion.Angle(front,MenuActor.localRotation)<2,"new hero starts facing the camera");
            report.Add("menu drag, inertia and hero reset");
            report.Add(VerifyPrize("bear",2));report.Add(VerifyPrize("owl",2));report.Add(VerifyPrize("bear",1));report.Add(VerifyPrize("owl",0));
            StartRace("{\"mode\":\"normal\",\"seed\":1911}");
            Checkpoint(275,0);var helmet=World.Items.Find(o=>o.kind=="helmet"&&o.distance<315);
            Check(helmet!=null,"training helmet missing");
            Checkpoint(helmet.distance-1.4f,helmet.lane);Advance(.15f);
            Check(Status.helmets==1,"training helmet not collected");
            Checkpoint(1,0);var skier=World.Items.Find(o=>o.kind=="npc"&&!o.stand&&!o.sit);
            Check(skier!=null,"training moving NPC missing");
            Checkpoint(skier.distance,skier.lane);Advance(1f/60);
            Check(Status.helmets==0&&fall>0&&Status.phase=="play","helmet collision did not protect");
            RestartRide();Checkpoint(1,0);skier=World.Items.Find(o=>o.kind=="npc"&&!o.stand&&!o.sit);
            Checkpoint(skier.distance,skier.lane);Advance(1f/60);for(int i=0;i<17;i++)Advance(.1f);
            Check(Status.phase=="over","unprotected collision did not end the ride");
            report.Add("training pickup + protected and unprotected NPC collisions");
            StartRace("{\"mode\":\"sprint\",\"seed\":1911}");
            Checkpoint(1199,0);Advance(.2f);Check(Status.phase=="finish"&&Status.metres==SprintLength,"sprint finish");
            report.Add("sprint 1200m");
            foreach(var mode in new[]{"tubing","sled"}){
                StartRace("{\"mode\":\""+mode+"\",\"seed\":1911}");
                var item=World.Items.Find(o=>o.kind==(mode=="sled"?"snowman":"coin"));
                Check(item!=null,mode+" pickup missing");
                Checkpoint(item.distance,item.lane);Advance(1f/60);
                Check(mode=="sled"?Status.snowmen>0:Status.coins>0,mode+" bonus pickup");
                Status.elapsed=74.96f;Advance(.1f);
                Check(Status.phase=="finish"&&Mathf.Abs(Status.elapsed-75)<.001f,mode+" limit");
                report.Add(mode+" pickup and 75s finish");
            }
        }finally{Menu();}
        return String.Join(" | ",report);
    }
}
#endif
