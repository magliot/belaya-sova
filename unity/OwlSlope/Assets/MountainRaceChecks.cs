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
        if(CourseMode=="normal"&&RaceWorld.StageAt(metres)==3)speed=ParkPace(metres);
        World.Refresh(metres,Status.elapsed);BeginSteer();
    }
    string VerifyPrize(string hero,int taps,int lap=0,bool coffee=false){
        Menu();ChooseHero(hero);StartRace("{\"mode\":\"normal\",\"seed\":1911,\"sensitivity\":4}");
        Checkpoint(7620+lap*20000,0);
        var ramp=World.Items.Find(o=>o.kind=="ramp"&&o.prizeLine);
        var feather=World.Items.Find(o=>o.kind=="feather");
        Check(ramp!=null&&feather!=null,"prize row missing from seed 1911");
        float target=feather.distance;
        Checkpoint(ramp.distance-ramp.length*.5f-1,ramp.lane);
        if(coffee){boost=.54f;speed=37;}
        int gestures=0;
        float takeoffSpeed=0,crestSpeed=0,flightDuration=0,nextTap=.10f;
        bool wasTurning=false,landed=false;float exitDistance=ramp.distance+ramp.length*.5f+ramp.span+2;
        for(int frame=0;frame<900&&Status.metres<exitDistance&&Status.phase=="play";frame++){
            if(airborne){
                if(takeoffSpeed==0){takeoffSpeed=speed;flightDuration=airMax;}
                if(Mathf.Abs(airTime/airMax-.5f)<.015f)crestSpeed=speed;
                Check(Mathf.Approximately(airMax,flightDuration),"airtime changed after takeoff");
                Check(Mathf.Abs(speed-takeoffSpeed)<.05f,"horizontal momentum changed in midair");
                if(wasTurning&&trickProgress<0)nextTap=airTime+.12f;
            }
            if(airborne&&airTime>=nextTap&&trickProgress<0&&gestures<taps){AirTrick("tap");if(trickProgress>=0)gestures++;}
            wasTurning=trickProgress>=0;
            bool flying=airborne;float metres=Status.metres,oldSpeed=speed;
            Advance(1f/60);
            if(flying&&!airborne){
                landed=true;
                Check(Mathf.Abs(speed-oldSpeed)<.05f&&Mathf.Abs(Status.metres-metres-oldSpeed/60)<.012f,
                    "landing jumped in distance or speed");
            }else if(landed)Check(Mathf.Abs(speed-oldSpeed)<.17f,"ground speed snapped on the frame after landing");
        }
        Publish();
        string result=hero+" @"+target.ToString("F1")+"m, "+Status.tricks+" tricks, "+Status.rings+" rings, feather "+Status.feather+
            ", takeoff/crest "+takeoffSpeed.ToString("F1")+"/"+crestSpeed.ToString("F1")+" m/s, flight "+flightDuration.ToString("F2")+" s";
        Check(Status.phase=="play"&&landed&&Status.metres>=exitDistance,result+" failed to land: "+Status.message);
        Check(flightDuration>1.35f&&flightDuration<2.36f&&Mathf.Abs(crestSpeed-takeoffSpeed)<.05f,result+" park flight");
        if(coffee)Check(takeoffSpeed>34&&boost==0,result+" airborne coffee expiry");
        Check(Status.tricks==taps,result+" gesture count");
        Check(Status.feather==(taps==2)&&Status.tubingUnlocked==Status.feather&&Status.sledUnlocked==Status.feather,result+" feather gate");
        Check(Status.rings==(taps==0?0:1),result+" upper ring");
        return result;
    }
    string VerifyParkJumps(int lap){
        Menu();ChooseHero("bear");StartRace("{\"mode\":\"normal\",\"seed\":1911,\"sensitivity\":4}");
        float start=7500+lap*20000,end=start+2500;
        Checkpoint(start,0);chaseNext=end+900;
        var features=new HashSet<RaceItem>();
        for(int frame=0;frame<3300&&Status.phase=="play"&&Status.metres<end;frame++){
            foreach(var item in World.Items){
                if(item.distance<start||item.distance>=end)continue;
                if(item.kind=="ramp"||item.kind=="gift"||item.kind=="aid"||item.air&&item.kind=="coin")features.Add(item);
            }
            // Follow the jump faces and their airborne targets, leaving scattered ground fruit optional.
            float look=Status.metres+speed*.16f;
            RaceItem previous=null,next=null;
            foreach(var item in features){
                if(item.distance<=look&&(previous==null||item.distance>previous.distance))previous=item;
                else if(item.distance>look&&(next==null||item.distance<next.distance))next=item;
            }
            if(next!=null){
                float x=previous==null?next.lane:Mathf.Lerp(previous.lane,next.lane,Mathf.InverseLerp(previous.distance,next.distance,look));
                DragSteer((x-dragStart)/(27*sensitivity));
            }
            Advance(1f/30);
        }
        Check(Status.metres>=end&&Status.phase=="play","failed to ride park jumps at "+Status.metres+" "+Status.message);
        int targets=0,collected=0;bool hasKit=false;
        foreach(var item in features){
            if(item.air&&item.kind=="coin"){targets++;if(item.consumed)collected++;}
            if(item.kind=="aid"&&item.consumed)hasKit=true;
        }
        Check(targets>=50&&collected>=targets*.85f&&Status.gifts>0&&hasKit,
            "park jump targets "+collected+"/"+targets+", gifts "+Status.gifts+", aid "+hasKit);
        return "park lap "+(lap+1)+": "+collected+"/"+targets+" airborne fruit, rescue and gifts";
    }
    public string VerifySnowmen(){
        const int samples=100;
        int hits=0,wide=0;
        for(int sample=0;sample<samples;sample++){
            Menu();ChooseHero(sample%2==0?"bear":"owl");
            StartRace("{\"mode\":\"normal\",\"seed\":"+(1911+sample)+"}");
            Checkpoint(106,0);
            var sender=World.Items.Find(o=>o.kind=="snowman"&&o.distance>135&&o.distance<182);
            Check(sender!=null,"first training snowman missing");
            Checkpoint(sender.distance-30,0);
            RaceItem shot=null;
            if(sample%4==2)boost=2.5f;
            for(int tick=0;tick<300&&Status.metres<sender.distance+3&&Status.phase=="play";tick++){
                float aim=sample%3==0?0:sample%3==1?2.7f*Mathf.Sin(tick/30f+sample):Mathf.Lerp(-2,2,Mathf.PingPong(tick/40f,1));
                DragSteer(aim/(27*sensitivity));
                Advance(1f/60);
                if(shot==null)shot=World.Items.Find(o=>o.kind=="ball"&&Mathf.Abs(o.originD-sender.originD)<.01f);
                if(shot!=null&&shot.consumed)break;
            }
            Check(sender.threwSnowball&&shot!=null,"snowman did not throw for seed "+(1911+sample));
            if(Mathf.Abs(shot.throwAimOffset)>1)wide++;
            if(shot.judged){
                hits++;
                Check(snowPuffs&&snowPuffs.particleCount>0&&Status.helmets==0&&crashTimer==0&&fall==0&&wobble==0&&slow==0,"snowball did more than shower the rider");
            }
        }
        Check(hits>=90&&hits<=99&&wide>0,"moving riders: "+hits+"/"+samples+" physical hits, "+wide+" deliberate misses");
        foreach(string mode in new[]{"sled","tubing"}){
            StartRace("{\"mode\":\""+mode+"\",\"seed\":1911}");
            SnowmanPiece first=null;
            for(int retry=0;retry<2;retry++){
                Check(System.Array.TrueForAll(snowmanPieces,p=>p==null||!p.pivot.gameObject.activeSelf),"debris survived restart");
                Checkpoint(mode=="sled"?1:75,0);
                var target=World.Items.Find(o=>o.kind=="snowman"&&o.distance>Status.metres);
                Check(target!=null,mode+" target missing");Checkpoint(target.distance,target.lane);
                Advance(1f/60);
                Check(target.consumed&&Status.snowmen==1&&bonusPoints>=90,mode+" snowman paid incorrectly");
                Check(System.Array.FindAll(snowmanPieces,p=>p!=null&&p.pivot.gameObject.activeSelf).Length==3,"expected three mesh pieces");
                for(int i=0;i<3;i++){
                    var piece=snowmanPieces[i].pivot.GetChild(0);
                    Check(piece.name==snowmanParts[i]&&piece.GetComponent<MeshFilter>().sharedMesh.subMeshCount==2,"snowman lost skin or cap");
                }
                if(first!=null)Check(first==snowmanPieces[0],"unnecessary new fragments on restart");
                first=snowmanPieces[0];
                StepSnowmanPieces(.27f);
                Check((snowmanPieces[0].pivot.position-snowmanPieces[1].pivot.position).magnitude>1.6f,"snowman still in one silhouette");
                for(int i=0;i<95;i++)StepSnowmanPieces(1f/60);
                Check(!first.pivot.gameObject.activeSelf,"fragments did not retire");RestartRide();
            }
        }
        return "snowmen: "+hits+"/"+samples+" physical hits, "+wide+" deliberate misses; sled and tubing three textured pieces on impact and retry";
    }
    string VerifyBodyContacts(){
        foreach(string hero in new[]{"bear","owl"}){
            Menu();ChooseHero(hero);
            foreach(var sample in new[]{("npc","snowboarder",.88f,true),("npc","skier",1.55f,false),
                ("yeti","yeti",1.76f,true),("yeti","yeti",2.65f,false),("tube","tube60B",1.05f,true)}){
                StartRace("{\"mode\":\"normal\",\"seed\":1911,\"helmets\":1}");
                // The first unobstructed metres isolate the mesh and the coffee contact, using normal Step and CheckItems.
                var actor=World.Add(sample.Item1,6,0,2,2,sample.Item2);
                actor.stand=true;
                if(actor.kind=="tube"){actor.tau=2;actor.vCruise=2;actor.vStart=1.5f;actor.pathSpan=12;actor.release=6;actor.side=1;}
                World.Add("thermos",2,sample.Item3,1,.85f);
                Checkpoint(.1f,sample.Item3);Status.helmets=2;
                for(int tick=0;tick<14;tick++)Advance(1f/30);
                Check(boost>0,"coffee not active at contact");
                Check(Status.helmets==(sample.Item4?1:2),hero+" "+sample.Item2+" lateral offset "+sample.Item3+" helmets "+Status.helmets);
                if(sample.Item4)Check(!actor.consumed,"contact deleted the actor");
            }
        }
        // A moving arm can cross between the start and end of the tick. A clear jump must pass above it.
        var crossing=new RaceItem{kind="mogulYeti",lane=3,previousLane=-3,distance=10,previousDistance=10};
        lane=0;Status.metres=10;Status.helmets=2;fall=0;airborne=false;rampHeight=previousAltitude=0;
        Check(HandleSolid(crossing,10,0,1.7f,.96f,3.2f,0,true)&&Status.helmets==1,"crossing yeti tunnelled");
        fall=0;
        Check(HandleSolid(crossing,10,0,1.7f,.96f,3.2f,0,true)&&Status.helmets==1,"same overlap charged twice");
        previousAltitude=rampHeight=3.7f;
        Check(!HandleSolid(crossing,10,0,1.7f,.96f,3.2f,0,true)&&Status.helmets==1,"jump over yeti lost helmet");
        previousAltitude=rampHeight=0;
        Check(HandleSolid(crossing,10,0,1.7f,.96f,3.2f,0,true)&&Status.helmets==0,"separate contact did not charge a second helmet");
        return "both heroes: side hits, clear passes, coffee, swept crossing, height and one helmet per contact";
    }
    string VerifyCourseMarkers(){
        foreach(string hero in new[]{"bear","owl"}){
            Menu();ChooseHero(hero);
            foreach(int choice in new[]{-1,0,1,2}){
                StartRace("{\"mode\":\"normal\",\"seed\":1911}");
                Checkpoint(335,0);
                var gate=World.Items.Find(o=>o.kind=="gate"&&Mathf.Abs(o.distance-347.5f)<.01f);
                Check(gate!=null&&gate.variant=="trainingCones"&&gate.obj.transform.childCount==2,"training cone pair missing");
                var left=gate.obj.transform.GetChild(0);var right=gate.obj.transform.GetChild(1);
                var a=left.position;var b=right.position;
                Check(Vector3.Distance(a,World.SnowSurface(gate.distance,gate.lane-gate.width*.5f))<.02f&&
                      Vector3.Distance(b,World.SnowSurface(gate.distance,gate.lane+gate.width*.5f))<.02f,"cone floats above sloped snow");
                bool clip=Mathf.Abs(choice)==1;
                Checkpoint(gate.distance-2,gate.lane+choice*(gate.width*.5f));Status.helmets=1;
                for(int i=0;i<33;i++)Advance(1f/60);
                Check(fall==0&&Status.helmets==1&&gate.judged,"soft marker charged a helmet");
                Check(Status.gates==(choice==0?1:0),"clipped or missed training gate awarded points");
                Check(gate.knockedConeSide==(clip?choice:0),"wrong cone reacted");
                Check(((left.position-a).magnitude>.3f)==(choice==-1)&&((right.position-b).magnitude>.3f)==(choice==1),
                    "cone displacement or untouched partner incorrect");
                if(clip){
                    var moved=choice<0?left:right;
                    Check(Vector3.Angle(moved.up,Vector3.up)>30,"struck cone stayed upright");
                }
            }
        }
        RestartRide();Checkpoint(20335,0);
        Check(World.Items.Exists(o=>Mathf.Abs(o.distance-20347.5f)<.01f&&o.variant=="trainingCones"&&o.Visual),"second lap cones");
        RestartRide();Checkpoint(15110,0);
        var flags=World.Items.Find(o=>o.kind=="gate"&&o.distance>15110);
        Check(flags!=null&&flags.variant.EndsWith("Night"),"night flag gate missing");
        Checkpoint(flags.distance-2,flags.lane+flags.width*.5f);Status.helmets=1;
        for(int i=0;i<15;i++)Advance(1f/60);
        Check(flags.judged&&Status.gates==0&&Status.helmets==1&&fall==0,"flag pole contact");
        RestartRide();Checkpoint(12507,0);
        var rock=World.Items.Find(o=>o.kind=="rock"&&o.variant.StartsWith("gateRock")&&o.distance>=12507);
        Check(rock!=null,"black gate boulder missing");
        Checkpoint(rock.distance-1,rock.lane);Status.helmets=1;Advance(.12f);
        Check(Status.helmets==0&&fall>0,"black gate lost its solid boulders");
        return "training cones: both heroes, both posts kicked separately, clean/skip scoring, lap two, soft night flags and solid black rocks";
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
            report.Add(VerifyPrize("bear",2,1));report.Add(VerifyPrize("owl",2,1,true));
            report.Add(VerifyParkJumps(0));report.Add(VerifyParkJumps(1));
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
            report.Add(VerifyBodyContacts());
            report.Add(VerifyCourseMarkers());
            report.Add(VerifyFisSituations());
            report.Add(VerifySnowmen());
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
