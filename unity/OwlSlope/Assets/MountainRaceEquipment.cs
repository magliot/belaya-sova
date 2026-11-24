using UnityEngine;

public partial class MountainRace {
    const int MaxHelmetsPerRide=3;
    int helmetsIssued;
    ParticleSystem kitSparks;
    float nextKitSpark;
    bool Fireboard=>!BonusMode&&settings.ownedEquipment&&Hero=="owl"&&settings.equipment=="fireboard";
    bool CarveSkis=>!BonusMode&&settings.ownedEquipment&&Hero=="bear"&&settings.equipment=="carveSkis";
    float KitAcceleration(float stock)=>Fireboard?stock+8+2*KitTuning:CarveSkis?stock*.575f:stock;
    int KitTuning=>Mathf.Clamp(settings.tuning,0,2);
    int HeadTuning=>Mathf.Clamp(settings.headTuning,0,2);
    bool OrangeHelmet=>!BonusMode&&Hero=="bear"&&settings.headgear=="orangeHelmet";
    // Existing purchases and tuning use the original saved item ID.
    bool PinkHelmet=>!BonusMode&&Hero=="owl"&&settings.headgear=="pinkGoggles";
    float GroundFruitRadius=>1.1f+(PinkHelmet?.25f+.20f*HeadTuning:0);
    int MainUpgrade(int level)=>CourseMode=="normal"?Mathf.Clamp(level,0,5):0;

    public void PreviewGear(string json){
        if(!IsMenu&&!Review)return;
        var preview=JsonUtility.FromJson<Settings>(json);
        if(preview==null)return;
        settings.equipment=preview.equipment;settings.ownedEquipment=preview.ownedEquipment;settings.tuning=preview.tuning;
        settings.headgear=preview.headgear;settings.headTuning=preview.headTuning;
        if(IsMenu&&settings.inspect!=preview.inspect){menuYaw=menuVelocity=0;spinHeld=false;}
        settings.inspect=IsMenu?preview.inspect:"";
        PaintKit(rideModel);PaintKit(stowed);PaintHeadwear(rideModel);PaintHeadwear(menuModel);
        if(IsMenu)RenderMenu(0);
    }

    void PaintHeadwear(Transform model){
        if(!model)return;
        bool colored=Hero=="owl"?settings.headgear=="pinkGoggles":settings.headgear=="orangeHelmet";
        foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
            var paint=skin.sharedMaterial;
            if(!paint||!paint.name.StartsWith("tripo_material")||!paint.mainTexture)continue;
            MaterialPropertyBlock block=null;
            if(colored){
                var variant=Resources.Load<Texture2D>("Art/"+paint.mainTexture.name+"Shop");
                if(variant){block=new MaterialPropertyBlock();block.SetTexture("_BaseMap",variant);}
            }
            skin.SetPropertyBlock(block,0);
        }
    }

    void PaintKit(Transform root){
        if(!root)return;
        foreach(var part in root.GetComponentsInChildren<Transform>(true)){
            bool board=part.name=="Snowboard"||part.name=="Snowboard(Clone)";
            bool ski=part.name.EndsWith("_Ski")||part.name.EndsWith("_Ski(Clone)");
            if(!board&&!ski)continue;
            bool owned=settings.ownedEquipment&&(board?Fireboard:CarveSkis);
            MaterialPropertyBlock paint=null;
            if(owned){
                paint=new MaterialPropertyBlock();
                paint.SetColor("_BaseColor",board?new Color(1,.25f,.57f):new Color(.09f,.91f,1));
                paint.SetFloat("_Smoothness",.64f);
            }
            foreach(var renderer in part.GetComponentsInChildren<Renderer>(true))renderer.SetPropertyBlock(paint);
        }
    }

    void StepEquipmentSparks(float dt){
        if(!settings.ownedEquipment||(!Fireboard&&!CarveSkis))return;
        if(kitSparks&&kitSparks.particleCount>0)kitSparks.Simulate(dt,false,false,false);
        if(Status.phase!="play"||fall>0||crashTimer>0||Status.elapsed<nextKitSpark)return;
        if(CarveSkis&&(airborne||Mathf.Abs(angle)<.28f))return;
        nextKitSpark=Status.elapsed+(Fireboard?(airborne?.11f:.28f):.16f);
        if(!kitSparks)kitSparks=MakeBurst("Искры снаряжения","FestivalSpark");
        Quaternion slope=World.SnowFrame(Status.metres,lane);
        var position=World.SnowSurface(Status.metres,lane)+slope*new Vector3(0,Altitude+.15f,-.62f);
        for(int i=0;i<(Fireboard?7:3);i++){
            float a=i*2.40f+Status.elapsed*4;
            Color color=Fireboard?Color.Lerp(new Color(1,.19f,.50f),new Color(1,.84f,.15f),.5f+.5f*Mathf.Sin(a)):new Color(.27f,.9f,1);
            kitSparks.Emit(new ParticleSystem.EmitParams{position=position,
                velocity=slope*new Vector3(Mathf.Cos(a)*1.9f,1.4f+.9f*Mathf.Sin(a),-1.2f+Mathf.Sin(a)),
                startLifetime=Fireboard?.53f:.32f,startSize=Fireboard?.12f:.075f,startColor=color},1);
        }
    }
#if UNITY_EDITOR
    public void VerifyEquipment(){
        (float speed,float slide,float turn) DriveKit(string hero,string kit,bool owned,int level=0){
            Menu();ChooseHero(hero);
            string config=JsonUtility.ToJson(new Settings{mode="normal",seed=1911,equipment=kit,ownedEquipment=owned,tuning=level,helmets=50,boost=5,mag=5});
            void CheckFinish(Transform root,string scene){
                string name=hero=="owl"?"Snowboard":"Left_Ski";
                var part=System.Array.Find(root.GetComponentsInChildren<Transform>(true),t=>t.name==name||t.name==name+"(Clone)");
                var renderer=part?part.GetComponentInChildren<Renderer>(true):null;
                Check(renderer&&renderer.HasPropertyBlock()==owned,"equipment finish in "+scene+" "+hero);
            }
            PreviewGear(config);CheckFinish(stowed,"menu");
            StartRace(config);CheckFinish(rideModel,"race");
            Check(Status.helmets==1&&MainUpgrade(settings.boost)==5,"main descent must use purchased helmet and boosts");
            Checkpoint(100,0);speed=13;boost=4;slip=1.5f;DragSteer(.08f);
            for(int tick=0;tick<12;tick++)Advance(1f/60);
            Check(Status.phase=="play"&&fall<=0,"kit measurement interrupted");
            if(owned&&kit=="fireboard")Check(kitSparks&&kitSparks.particleCount>0,"personal board fireworks missing");
            return(speed,Mathf.Abs(slip),lane);
        }
        var basic=DriveKit("owl","standard",false);
        var board=DriveKit("owl","fireboard",true);
        var tuned=DriveKit("owl","fireboard",true,2);
        Check(board.speed>basic.speed+.4f&&board.turn<basic.turn&&tuned.speed>board.speed+.2f,"board acceleration and tuning");
        var basicSki=DriveKit("bear","standard",false);
        var carve=DriveKit("bear","carveSkis",true,2);
        Check(carve.speed<basicSki.speed-.25f&&carve.slide<basicSki.slide*.75f,"ski edge and acceleration tradeoff");
        foreach(string hero in new[]{"bear","owl"}){
            Menu();ChooseHero(hero);
            var spec=new Settings{headgear=hero=="bear"?"orangeHelmet":"pinkGoggles",headTuning=2,inspect="headgear"};
            string json=JsonUtility.ToJson(spec);
            void CheckBody(Transform body){
                foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
                    if(!skin.sharedMaterial.name.StartsWith("tripo_material"))continue;
                    var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block,0);
                    Check(block.GetTexture("_BaseMap")!=null,"missing purchased headwear texture "+body.name);return;
                }
                Check(false,"missing head mesh "+body.name);
            }
            PreviewGear(json);CheckBody(menuModel);StartRace(json);CheckBody(rideModel);
            Check(hero=="bear"?Mathf.Abs(FallTime-1.25f)<.001f:Mathf.Abs(GroundFruitRadius-1.75f)<.001f,"headgear boost");
        }
        float[] trainingHelmets={284.25f,1784.25f,3659.25f};
        for(int stock=0;stock<=1;stock++){
            Menu();ChooseHero(stock==0?"bear":"owl");
            StartRace(JsonUtility.ToJson(new Settings{mode="normal",seed=1911,helmets=stock}));
            for(int count=stock+1;count<=MaxHelmetsPerRide;count++){
                float metre=trainingHelmets[count-stock-1];
                Checkpoint(metre-3,0);
                var item=World.Items.Find(o=>o.kind=="helmet"&&Mathf.Abs(o.distance-metre)<.1f);
                Check(item!=null,"missing course helmet at "+metre);
                Checkpoint(metre-1.4f,item.lane);
                Advance(.15f);
                CheckItems(Status.metres,lane);
                Check(item.consumed&&Status.helmets==count&&helmetsIssued==count,"each visible helmet must count exactly once");
            }
            Check(Status.helmets==3,"all three helmet slots must be usable");
            var extra=World.Add("helmet",Status.metres+1,lane,.5f,.7f);
            World.Refresh(Status.metres,Status.elapsed);
            Check(!World.Items.Contains(extra)&&!extra.obj,"unusable helmet presented to the rider");
            for(int remaining=MaxHelmetsPerRide-1;remaining>=0;remaining--){
                Hit("npc");Hit("npc");Check(Status.helmets==remaining&&fall>0,"one protection per impact");fall=0;
            }
            Check(World.HelmetLimitReached,"used protection reopened the quota");
        }
        // Resolve the final unprotected impact via the production collision and finish path.
        Hit("npc");for(int i=0;i<150&&Status.phase=="play";i++)Advance(1f/60);
        Check(Status.phase=="over","unprotected fourth fall extended the attempt");
        Debug.Log("EQUIPMENT PASS main stock/board/tuned="+basic.speed+"/"+board.speed+"/"+tuned.speed+
            "; basic/carve lateral slip="+basicSki.slide+"/"+carve.slide+" m/s");
        Menu();
    }
#endif
}
