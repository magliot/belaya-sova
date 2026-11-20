using UnityEngine;

public partial class MountainRace {
    const int MaxHelmetsPerRide=2;
    int helmetsIssued;
    ParticleSystem kitSparks;
    float nextKitSpark;
    bool Fireboard=>!BonusMode&&Hero=="owl"&&settings.equipment=="fireboard";
    bool CarveSkis=>!BonusMode&&Hero=="bear"&&settings.equipment=="carveSkis";
    int FreeTuning=>settings.ownedEquipment?FreeUpgrade(settings.tuning):0;
    int FreeUpgrade(int level)=>Status.mode=="free"?Mathf.Clamp(level,0,5):0;

    public void PreviewGear(string json){
        if(!IsMenu)return;
        var preview=JsonUtility.FromJson<Settings>(json);
        if(preview==null)return;
        settings.equipment=preview.equipment;settings.ownedEquipment=preview.ownedEquipment;
        PaintKit(rideModel);PaintKit(stowed);
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
        (float speed,float slide,float turn) DriveKit(string hero,string kit,string mode,bool owned){
            Menu();ChooseHero(hero);
            string config=JsonUtility.ToJson(new Settings{mode=mode,seed=1911,equipment=kit,ownedEquipment=owned,tuning=2,helmets=50,boost=5,mag=5});
            void CheckFinish(Transform root,string scene){
                var part=System.Array.Find(root.GetComponentsInChildren<Transform>(true),t=>
                    hero=="owl"?t.name.StartsWith("Snowboard")&&t.GetComponent<Renderer>():t.name.StartsWith("Left_Ski")&&t.GetComponent<Renderer>());
                Check(part&&part.GetComponent<Renderer>().HasPropertyBlock()==owned,"equipment finish in "+scene+" "+hero);
            }
            PreviewGear(config);CheckFinish(stowed,"menu");
            StartRace(config);CheckFinish(rideModel,"race");
            Check((Status.helmets==1)==(mode=="free"),"starting helmet entitlement");
            Check(FreeUpgrade(settings.boost)==(mode=="free"?5:0),"purchased coffee affected ranking");
            Checkpoint(100,0);speed=13;boost=4;slip=1.5f;DragSteer(.08f);
            for(int tick=0;tick<12;tick++)Advance(1f/60);
            Check(Status.phase=="play"&&fall<=0,"kit measurement interrupted");
            if(owned&&kit=="fireboard")Check(kitSparks&&kitSparks.particleCount>0,"personal board fireworks missing");
            return(speed,Mathf.Abs(slip),lane);
        }
        var basic=DriveKit("owl","standard","normal",false);
        var loan=DriveKit("owl","fireboard","normal",false);
        var own=DriveKit("owl","fireboard","normal",true);
        var free=DriveKit("owl","fireboard","free",true);
        Check(Mathf.Abs(own.speed-loan.speed)<.001f&&Mathf.Abs(own.turn-loan.turn)<.001f,"ranking gear differs from rental");
        Check(loan.speed>basic.speed+.4f&&loan.turn<basic.turn&&free.speed>loan.speed+.2f,"board acceleration/steering tradeoff");
        var basicSki=DriveKit("bear","standard","normal",false);
        var carve=DriveKit("bear","carveSkis","normal",false);
        Check(carve.speed<basicSki.speed-.25f&&carve.slide<basicSki.slide*.75f,"ski edge and acceleration tradeoff");
        Menu();StartRace("{\"mode\":\"normal\",\"seed\":1911,\"helmets\":40}");
        Checkpoint(4,0);
        void ContactHelmet(){
            var item=World.Add("helmet",Status.metres,lane,.5f,.7f);
            CheckItems(Status.metres-.01f,lane);
            Check(item.consumed,"helmet pickup missed");
        }
        for(int i=0;i<MaxHelmetsPerRide;i++){
            ContactHelmet();ContactHelmet();
            Check(Status.helmets==1&&helmetsIssued==i+1,"helmets stacked");
            Hit("npc");Check(Status.helmets==0&&fall>0,"helmet did not absorb a fall");fall=0;
        }
        ContactHelmet();Check(Status.helmets==0&&helmetsIssued==MaxHelmetsPerRide,"attempt gained a third helmet");
        // Resolve the final unprotected impact via the production collision and finish path.
        Hit("npc");for(int i=0;i<150&&Status.phase=="play";i++)Advance(1f/60);
        Check(Status.phase=="over","third fall extended the attempt");
        Debug.Log("EQUIPMENT PASS ranked loan/own="+loan.speed+"/"+own.speed+" m/s, tuned free="+free.speed+
            "; basic/carve lateral slip="+basicSki.slide+"/"+carve.slide+" m/s");
        Menu();
    }
#endif
}
