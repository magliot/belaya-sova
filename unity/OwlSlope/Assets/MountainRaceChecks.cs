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
    string VerifySnowballThrows(){
        int onTarget=0,hits=0;
        for(int sample=0;sample<20;sample++){
            StartRace("{\"mode\":\"normal\",\"seed\":"+(1911+sample)+",\"helmets\":1}");
            Checkpoint(106,0);
            var sender=World.Items.Find(o=>o.kind=="snowman"&&o.distance>135&&o.distance<182);
            Check(sender!=null,"first training snowman missing");
            Checkpoint(sender.distance-30,0);
            RaceItem shot=null;bool hit=false;
            for(int tick=0;tick<300&&Status.metres<sender.distance+3&&Status.phase=="play";tick++){
                Advance(1f/60);
                if(shot==null)shot=World.Items.Find(o=>o.kind=="ball"&&Mathf.Abs(o.originD-sender.originD)<.01f);
                if(!hit&&snowPuffs&&snowPuffs.particleCount>0){
                    hit=true;
                    Check(Status.helmets==1&&wobble==0&&slow==0,"snowball should only shower the player with snow");
                }
            }
            Check(sender.threwSnowball&&shot!=null,"snowman did not throw for seed "+(1911+sample));
            bool aimed=Mathf.Abs(shot.throwEnd.x)<1;
            if(aimed)onTarget++;
            if(hit)hits++;
            Check(aimed==hit,"throw and physical contact disagree for seed "+(1911+sample));
        }
        Check(hits>=11&&hits<20,"sample must contain both hits and misses: "+hits);
        return "training throws "+hits+"/20 physical hits, "+onTarget+" aimed";
    }
    string VerifySledSeat(string hero){
        Menu();ChooseHero(hero);StartRace("{\"mode\":\"sled\",\"seed\":1911}");
        float maxGrip=0,maxBoot=0;
        foreach(float steer in new[]{0f,-.80f,.80f}){
            angle=steer;RenderRace(0,true);
            var pelvis=actorRoot.InverseTransformPoint(seatBones["Hips"].position);
            var head=actorRoot.InverseTransformPoint(seatBones["Head"].position);
            var body=sled.GetComponent<MeshFilter>().sharedMesh.bounds;
            Check(body.size.z>2*body.size.y,hero+" sled runners are vertical");
            Check(Mathf.Abs(pelvis.z+.46f)<.025f&&Mathf.Abs(pelvis.y-.92f)<.025f&&head.y>1.44f,
                hero+" must sit on the rear saddle with head above windshield: "+pelvis+" / "+head);
            foreach(var limb in seatedLimbs){
                var palm=actorRoot.InverseTransformPoint(limb.palm.position);
                var boot=actorRoot.InverseTransformPoint(limb.ankle.position);
                var toe=actorRoot.InverseTransformPoint(limb.toe.position);
                maxGrip=Mathf.Max(maxGrip,Vector3.Distance(palm,new Vector3(limb.side*.355f,1.13f,-.06f)));
                maxBoot=Mathf.Max(maxBoot,Vector3.Distance(boot,new Vector3(limb.side*.335f,.665f,-.42f)));
                Check(toe.z>boot.z+.10f&&toe.y>.43f&&toe.y<.60f,hero+" foot off board: "+boot+" to "+toe);
            }
        }
        string detail=hero+" seated grip "+maxGrip.ToString("F3")+" m / ankle "+maxBoot.ToString("F3")+" m";
        Check(maxGrip<.095f&&maxBoot<.065f,detail);
        return detail;
    }
    public string VerifyMechanics(){
        Initialize();var report=new List<string>();
        try{
            Menu();ChooseHero("owl");var front=MenuActor.localRotation;
            MenuSpin(31);var dragged=MenuActor.localRotation;Check(Quaternion.Angle(front,dragged)>15,"menu rotation");
            EndMenuSpin();RenderMenu(1f/60);Check(Quaternion.Angle(MenuActor.localRotation,dragged)>1,"menu rotation inertia");
            ChooseHero("bear");Check(Quaternion.Angle(front,MenuActor.localRotation)<2,"new hero starts facing the camera");
            report.Add("menu drag, inertia and hero reset");
            report.Add(VerifySledSeat("bear"));report.Add(VerifySledSeat("owl"));
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
            report.Add(VerifySnowballThrows());
            StartRace("{\"mode\":\"sprint\",\"seed\":1911}");
            Checkpoint(1199,0);Advance(.2f);Check(Status.phase=="finish"&&Status.metres==SprintLength,"sprint finish");
            report.Add("sprint 1200m");
            foreach(var mode in new[]{"tubing","sled"}){
                StartRace("{\"mode\":\""+mode+"\",\"seed\":1911}");
                var item=World.Items.Find(o=>o.kind==(mode=="sled"?"snowman":"coin"));
                Check(item!=null,mode+" pickup missing");
                Checkpoint(item.distance,item.lane);Advance(1f/60);
                Check(mode=="sled"?Status.snowmen>0:Status.coins>0,mode+" bonus pickup");
                if(mode=="tubing")Check(mandarinPeels&&mandarinPeels.particleCount>0&&mandarinPeels.GetComponent<ParticleSystemRenderer>().renderMode==ParticleSystemRenderMode.Mesh,"no curled mandarin peels at contact");
                Status.elapsed=74.96f;Advance(.1f);
                Check(Status.phase=="finish"&&Mathf.Abs(Status.elapsed-75)<.001f,mode+" limit");
                report.Add(mode+" pickup and 75s finish");
            }
        }finally{Menu();}
        return String.Join(" | ",report);
    }
}
#endif
