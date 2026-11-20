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
        float takeoffSpeed=0,crestSpeed=0,flightDuration=0;
        for(int frame=0;frame<900&&Status.metres<target+2&&Status.phase=="play";frame++){
            if(airborne){
                if(takeoffSpeed==0){takeoffSpeed=speed;flightDuration=airMax;}
                if(Mathf.Abs(airTime/airMax-.5f)<.015f)crestSpeed=speed;
                Check(Mathf.Approximately(airMax,flightDuration),"airtime changed after takeoff");
            }
            if(airborne&&trickProgress<0&&gestures<taps){AirTrick("tap");if(trickProgress>=0)gestures++;}
            Advance(1f/60);
        }
        Publish();
        string result=hero+" @"+target.ToString("F1")+"m, "+Status.tricks+" tricks, "+Status.rings+" rings, feather "+Status.feather+
            ", takeoff/crest "+takeoffSpeed.ToString("F1")+"/"+crestSpeed.ToString("F1")+" m/s, flight "+flightDuration.ToString("F2")+" s";
        Check(Status.phase=="play"&&Status.metres>=target,result+" failed to reach target: "+Status.message);
        Check(takeoffSpeed<=26.1f&&crestSpeed>.70f*takeoffSpeed&&crestSpeed<.80f*takeoffSpeed,result+" park glide");
        Check(Status.tricks==taps,result+" gesture count");
        Check(Status.feather==(taps==2)&&Status.tubingUnlocked==Status.feather&&Status.sledUnlocked==Status.feather,result+" feather gate");
        Check(Status.rings==(taps==0?0:1),result+" upper ring");
        return result;
    }
    string VerifySnowballThrows(){
        int onTarget=0,hits=0;
        for(int sample=0;sample<=20;sample++){
            bool dodge=sample==20;
            StartRace("{\"mode\":\"normal\",\"seed\":"+(1911+sample)+",\"helmets\":1}");
            Checkpoint(106,0);
            var sender=World.Items.Find(o=>o.kind=="snowman"&&o.distance>135&&o.distance<182);
            Check(sender!=null,"first training snowman missing");
            Checkpoint(sender.distance-30,0);
            RaceItem shot=null;bool hit=false;
            for(int tick=0;tick<300&&Status.metres<sender.distance+3&&Status.phase=="play";tick++){
                Advance(1f/60);
                if(shot==null)shot=World.Items.Find(o=>o.kind=="ball"&&Mathf.Abs(o.originD-sender.originD)<.01f);
                if(dodge&&shot!=null)DragSteer(sender.lane>0?-.30f:.30f);
                if(!hit&&snowPuffs&&snowPuffs.particleCount>0){
                    hit=true;
                    Check(Status.helmets==1&&wobble==0&&slow==0,"snowball should only shower the player with snow");
                }
            }
            Check(sender.threwSnowball&&shot!=null,"snowman did not throw for seed "+(1911+sample));
            bool aimed=Mathf.Abs(shot.throwEnd.x)<1;
            if(dodge){Check(aimed&&!hit,"committed throw followed a last-second dodge");continue;}
            if(aimed)onTarget++;
            if(hit)hits++;
            Check(aimed==hit,"throw and physical contact disagree for seed "+(1911+sample));
        }
        Check(hits>=19,"snowmen missed too many steady riders: "+hits);
        return "training throws "+hits+"/20 physical hits, "+onTarget+" aimed; late dodge escaped";
    }
    string VerifyTrainingEncounters(){
        Menu();ChooseHero("bear");StartRace("{\"mode\":\"normal\",\"seed\":1911}");
        Checkpoint(195,0);
        var mound=World.Items.Find(o=>o.kind=="mogul"&&o.distance<250);
        Check(mound!=null&&mound.Visual&&mound.variant=="mound0","training mound model missing");
        Checkpoint(mound.distance-1.6f,mound.lane);
        int points=bonusPoints;
        for(int i=0;i<12;i++)Advance(1f/60);
        Check(mound.consumed&&moundChips&&moundChips.particleCount>0&&bonusPoints>=points+15&&fall==0,"bear did not break mound into snow chips");
        string result="breakable mound "+mound.originD.ToString("F1")+"m";
        foreach(int section in new[]{7,19}){
            Checkpoint(section*62.5f-24,0);
            var cat=World.Items.Find(o=>o.kind=="cat"&&o.originD>section*62.5f);
            Check(cat!=null&&cat.small&&(section==7?cat.pace<0:cat.pace>0),"training groomer direction");
            Checkpoint(cat.distance-46,0);
            Advance(1f/60);
            float start=cat.distance,far=Status.engineNear;int horns=Status.horns;
            for(int frame=0;frame<460&&Status.metres<cat.distance+4;frame++)Advance(1f/60);
            float front=Vector3.Dot(cat.obj.transform.forward,World.SnowFrame(cat.distance,cat.lane)*Vector3.forward);
            Check((cat.distance-start)*cat.pace>0&&front*cat.pace>0,"groomer travelled backwards");
            Check(Status.horns==horns+1&&Status.engineNear>Mathf.Max(.56f,far),"groomer approach or horn missing");
            result+=" / "+(cat.pace<0?"uphill ":"downhill ")+cat.originD.ToString("F1")+"m";
        }
        return result;
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
    string VerifyBonusJump(string mode,string hero){
        Menu();ChooseHero(hero);StartRace("{\"mode\":\""+mode+"\",\"seed\":1911}");
        float gripError=0;
        if(mode=="tubing"){
            Check(menuModel.gameObject.activeInHierarchy&&menuModel.name==(hero=="owl"?"OwlMenu":"BearMenu"),"wrong seated hero");
            Check(tube.GetComponent<MeshFilter>().sharedMesh.bounds.max.y<.70f,"a second rider was baked into the playable tube");
            foreach(float steer in new[]{0f,-.8f,.8f}){
                angle=steer;RenderRace(0,true);
                Vector3 hips=actorRoot.InverseTransformPoint(seatBones["Hips"].position);
                Check(Mathf.Abs(hips.y-.355f)<.015f,"rider left inflatable seat");
                foreach(var limb in seatedLimbs){
                    var hand=actorRoot.InverseTransformPoint(limb.palm.position);
                    var ankle=actorRoot.InverseTransformPoint(limb.ankle.position);
                    var toe=actorRoot.InverseTransformPoint(limb.toe.position);
                    gripError=Mathf.Max(gripError,Vector3.Distance(hand,TubeGrip(limb.side)));
                    Check(toe.z>ankle.z+.12f&&ankle.y>.32f,"tubing boots left the front rim "+hero);
                }
            }
            Check(gripError<.095f,"tube handle "+hero+" "+gripError+" m");
        }
        var ramp=World.Items.Find(o=>o.kind=="ramp");Check(ramp!=null,"missing "+mode+" kicker");
        var fruit=World.Items.FindAll(o=>o.air&&o.distance>ramp.distance&&o.distance<ramp.distance+ramp.length+ramp.span);
        Check(fruit.Count==4,"missing air fruit on "+mode+" kicker");
        Checkpoint(ramp.distance-4,ramp.lane);
        float peak=0;bool landed=false;
        for(int i=0;i<155;i++){
            Advance(1f/60);peak=Mathf.Max(peak,Altitude);
            if(peak>1.5f&&!airborne){landed=true;break;}
        }
        Check(landed&&fruit.TrueForAll(o=>o.consumed),mode+" missed the reward arc "+hero+
            " at "+ramp.distance+" m (collected "+fruit.FindAll(o=>o.consumed).Count+", apex "+peak+")");
        return mode+" "+hero+" kicker "+ramp.distance.ToString("F1")+" m, 4 airborne fruit, apex "+peak.ToString("F2")+
            " m"+(mode=="tubing"?", max handle gap "+gripError.ToString("F3")+" m":"");
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
            foreach(var vehicle in new[]{"tubing","sled"})foreach(var rider in new[]{"owl","bear"})
                report.Add(VerifyBonusJump(vehicle,rider));
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
            report.Add(VerifyTrainingEncounters());
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
                RestartRide();BeginSteer();
                // Follow the visible fruit line through the streaming world for the entire timed round.
                for(int tick=0;tick<760&&Status.phase=="play";tick++){
                    RaceItem nearest=null;
                    foreach(var candidate in World.Items){
                        if(candidate.kind!="coin"||candidate.consumed||candidate.distance<Status.metres+4)continue;
                        if(nearest==null||candidate.distance<nearest.distance)nearest=candidate;
                    }
                    if(nearest!=null)DragSteer((nearest.lane-dragStart)/(27*sensitivity));
                    Advance(.1f);
                }
                Check(Status.phase=="finish"&&Mathf.Abs(Status.elapsed-75)<.001f,mode+" limit");
                Check(Status.metres>1450&&Status.coins>70&&Status.gifts>3&&Status.snowmen>2&&Status.score>Status.metres+1500,
                    mode+" incomplete reward line: "+Status.metres+" m, "+Status.coins+" fruit, "+Status.gifts+" gifts, "+Status.snowmen+" snowmen");
                report.Add(mode+" full 75 s, "+(int)Status.metres+" m, "+Status.coins+" fruit, "+Status.gifts+" gifts, "+Status.snowmen+" snowmen, "+Status.score+" points");
            }
        }finally{Menu();}
        return String.Join(" | ",report);
    }
}
#endif
