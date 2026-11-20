using UnityEngine;
using UnityEngine.Rendering;

public partial class RaceWorld {
    const int TowerCount=7,ChairCount=8;
    GameObject lift;
    readonly Transform[] towers=new Transform[TowerCount],chairs=new Transform[ChairCount*2];
    readonly Vector3[,] anchors=new Vector3[2,TowerCount];
    readonly LineRenderer[] cables=new LineRenderer[2];
    Material cablePaint;
    int firstTower=int.MinValue,liftSeed;
    void PrepareLift(){
        lift=new GameObject("Кресельный подъёмник");lift.transform.SetParent(transform,false);
        if(!cablePaint){
            cablePaint=new Material(Paint("",1,false,0));cablePaint.name="Lift cable";
            cablePaint.SetFloat("_FadeCable",1);
        }
        for(int i=0;i<towers.Length;i++)towers[i]=CreateVisual("liftTower",lift.transform).transform;
        for(int line=0;line<2;line++){
            var go=new GameObject("Трос");go.transform.SetParent(lift.transform,false);
            var wire=go.AddComponent<LineRenderer>();wire.sharedMaterial=cablePaint;
            wire.startColor=wire.endColor=new Color32(74,88,102,230);wire.startWidth=wire.endWidth=.07f;
            wire.positionCount=TowerCount;wire.useWorldSpace=true;wire.shadowCastingMode=ShadowCastingMode.Off;
            cables[line]=wire;
            for(int i=0;i<ChairCount;i++){
                int ID=line*ChairCount+i;
                string key=Roll(seed,i,216+line)<(line==0?.70f:.15f)?"liftChair":"emptyChair";
                chairs[ID]=CreateVisual(key,lift.transform).transform;
            }
        }
        liftSeed=seed;
    }
    void RefreshLift(float d,float elapsed){
        if(mode=="tubing"){if(lift&&lift.activeSelf)lift.SetActive(false);return;}
        if(lift&&liftSeed!=seed){DisposeOwned(lift);lift=null;firstTower=int.MinValue;}
        if(!lift)PrepareLift();else if(!lift.activeSelf)lift.SetActive(true);
        int first=Mathf.FloorToInt((d-35)/35);
        if(first!=firstTower){
            firstTower=first;
            for(int i=0;i<TowerCount;i++){
                float at=(first+i)*35;var pos=SampleGround(at,15.4f);
                towers[i].SetPositionAndRotation(pos,Quaternion.Euler(0,HeadingAt(at)*Mathf.Rad2Deg,0));
                for(int line=0;line<2;line++){
                    var top=SampleGround(at,line==0?14.4f:16.4f);top.y=pos.y+8.4f;
                    anchors[line,i]=top;cables[line].SetPosition(i,top);
                }
            }
        }
        const float span=(TowerCount-1)*35;
        for(int line=0;line<2;line++)for(int i=0;i<ChairCount;i++){
            float at=(i*25+(line==1?12:0))+(line==0?-2.6f:2.6f)*elapsed;
            float z=Mathf.Repeat(at-first*35,ChairCount*25);
            int tower=Mathf.Min(TowerCount-2,(int)(z/35));float fraction=(z-tower*35)/35;
            var chair=chairs[line*ChairCount+i];
            chair.SetPositionAndRotation(Vector3.Lerp(anchors[line,tower],anchors[line,tower+1],fraction),
                Quaternion.Euler(0,HeadingAt(first*35+Mathf.Min(z,span))*Mathf.Rad2Deg+180*line,0));
        }
    }
}
