using System;
using System.Collections.Generic;
using UnityEngine;

public partial class RaceWorld {
    [Serializable] class LegacyObject {
        public string type;
        public float x,y,w,hh,x1,x2,vy,amp,ph,r,gap,vx,face,laneAt,laneBase,laneMargin;
        public int col,profile,vi,fenceStyle;
        public bool sb,stand,kid,instr,sit,walker,rockWide,air;
    }
    [Serializable] class Bump {public float x,y,R,Rx,Ry,A;public int profile;}
    [Serializable] class Field {public float y0,y1;public Bump[] b;}
    [Serializable] class Layout {public LegacyObject[] objects;public Field[] patches;}
    static readonly int[] layoutSeeds={19,1911,1927,4903};
    static readonly Dictionary<int,Layout> upperLayouts=new Dictionary<int,Layout>();
    readonly Dictionary<int,Layout> generatedLayouts=new Dictionary<int,Layout>();
    int generatedSeed,generatedVisit;
    Layout GetLayout(int stage,int visit){
        int choice=visit==0?Array.IndexOf(layoutSeeds,seed):-1;
        if(choice<0){
            if(generatedSeed!=seed||generatedVisit!=visit){generatedLayouts.Clear();generatedSeed=seed;generatedVisit=visit;}
            if(!generatedLayouts.TryGetValue(stage,out var generated)){
                int layoutSeed=visit==0?seed:unchecked(seed^(visit*164579721));
                generated=new UpperBuilder(layoutSeed,stage).Generate(stage==4&&visit==0?ReferenceLayout(4,0):null);
                generatedLayouts.Add(stage,generated);
            }
            return generated;
        }
        return ReferenceLayout(stage,choice);
    }
    static Layout ReferenceLayout(int stage,int choice){
        int key=stage*10+choice;
        if(!upperLayouts.TryGetValue(key,out var layout)){
            var source=Resources.Load<TextAsset>("Race/Layouts/stage"+stage+"-"+layoutSeeds[choice]);
            if(!source)throw new InvalidOperationException("Missing Three.js layout for stage "+stage);
            layout=JsonUtility.FromJson<Layout>(source.text);upperLayouts.Add(key,layout);
        }
        return layout;
    }
    struct SummitTurn {public float stone,lane;}
    readonly Dictionary<float,SummitTurn> summitTurns=new Dictionary<float,SummitTurn>();

    float SummitBase(float metres){
        float local=Mathf.Repeat(metres,2500),phase=Roll(seed,Mathf.FloorToInt(metres/20000),736)*6.28f;
        return (3.0f*Mathf.Sin(local/71+phase)+.7f*Mathf.Sin(local/41-phase))*Smooth(local/33)*Smooth((2500-local)/35);
    }

    public float SummitLine(float metres){
        float lane=SummitBase(metres),weight=0,target=0;
        foreach(var pair in summitTurns){
            float d=Mathf.Abs(metres-pair.Key);
            if(d>=46)continue;
            float blend=Smooth((46-d)/40);
            weight+=blend;target+=pair.Value.lane*blend;
        }
        return weight>0?Mathf.Lerp(lane,target/weight,Mathf.Min(weight,1)):lane;
    }

    void PlanSummitTurns(Layout layout,float start,float delta){
        // Lock the bypass before emitting any rewards in its approach. It stays fixed while in view.
        foreach(var marker in layout.objects){
            if(marker.laneAt<=0||marker.y!=marker.laneAt||marker.type!="rock")continue;
            float at=(marker.y+delta)*.05f;
            if(at<start-46||at>start+96||summitTurns.ContainsKey(at))continue;
            float stone=(Mathf.Clamp(marker.laneBase+spawnLane*20,marker.laneMargin,400-marker.laneMargin)-200)*.05f;
            float natural=SummitBase(at),side=natural<stone?-1:1;
            if(Mathf.Abs(stone+side*2.95f)>6.3f)side=-side;
            float lane=Mathf.Abs(natural-stone)>=2.95f?natural:stone+side*2.95f;
            // Two adjacent stones form one chicane with a continuous clear side.
            foreach(var other in summitTurns){
                if(Mathf.Abs(at-other.Key)>=55)continue;
                lane=other.Value.lane;
                if(Mathf.Abs(stone-lane)<2.65f)stone=lane+(stone<lane?-2.95f:2.95f);
                break;
            }
            summitTurns[at]=new SummitTurn{stone=stone,lane=lane};
        }
    }

    static float BesideSummitLine(float x,float lane,float clearance){
        if(Mathf.Abs(x-lane)>=clearance)return x;
        float side=x>=lane?1:-1;
        if(Mathf.Abs(lane+side*clearance)>10)side=-side;
        return lane+side*clearance;
    }

    void Mountain(float y,int row){
        var layout=GetLayout(4,Mathf.FloorToInt(y/400000));
        float original=200000+row*1000,delta=y-original,start=y*.05f;
        if(row==0)summitTurns.Clear();
        PlanSummitTurns(layout,start,delta);
        foreach(var f in layout.objects){
            if(f.laneAt>0||f.y<original||f.y>=original+1000)continue;
            float at=f.y+delta,d=at*.05f,lane=SummitLine(d),x=(f.x-200)*.05f;
            float Clear(float clearance)=>200+20*BesideSummitLine(x,lane,clearance);
            switch(f.type){
                case "npc":{
                    string model=f.sit?"sitter":f.sb?(f.instr?"boardInstructor":f.kid?"boardKid":"snowboarder"):
                        f.kid?"kid":f.instr?"instructor":"skier";
                    float actorX=Clear(f.vy==0?2.2f:3.5f);
                    if(f.sit)foreach(var aid in layout.objects){
                        if(aid.type!="aid"||Mathf.Abs(aid.y+12-f.y)>2)continue;
                        float patientLane=SummitLine((aid.y+delta)*.05f)+(aid.x>200?2.45f:-2.45f);
                        actorX=200+20*patientLane;break;
                    }
                    var o=Person(actorX,at,f.vy,model,Mathf.Min(f.amp,21),f.ph);o.walker=f.walker;o.yaw=f.face*Mathf.Rad2Deg;
                    if(f.walker)o.variant="walker";else if(model=="boardKid"&&f.vi%2==1)o.variant="boardKid1";break;}
                case "ramp":Kicker(Clear(3.5f),at,f.w>0?f.w:54,64,f.hh>0?f.hh:.75f);break;
                case "gate":Entry("gate",200+20*lane,at,f.gap,0,f.col!=0?"gateBlue":"gateRed");break;
                case "snowman":Snowman(Clear(2.8f),at);break;
                case "rock":Rock(Clear(2.65f),at,f.rockWide,f.r);break;
                case "mogul":Entry("mogul",Clear(2.45f),at,2*f.r,.4f,"mound"+Mathf.Clamp(f.profile,0,2));break;
                case "cannon":Entry("cannon",Clear(2.8f),at,28,2,f.x<200?"cannonLeft":"cannonRight");break;
                case "fence":Entry("fence",Clear((f.x2-f.x1)*.025f+2.25f),at,f.x2-f.x1,1.4f,f.fenceStyle>0?"fence"+f.fenceStyle:"fence");break;
                case "cat":Cat(Clear(5.7f),at,f.vy);break;
                case "dog":Entry("dog",Clear(4.7f),at,24,.8f);break;
                case "yeti":Entry("yeti",Clear(3.65f),at,38,2);break;
                case "coin":Add("coin",d,lane,1.1f,0);break;
                case "aid":case "gift":Add(f.type,d,lane,1.2f,.85f);break;
                case "thermos":case "helmet":case "magnet":Entry(f.type,Clear(3.65f),at,24,.85f);break;
            }
        }
        // Add the stone, its three mandarins and any downstream boulder as one group.
        foreach(var f in layout.objects){
            if(f.laneAt<=0||f.laneAt<original||f.laneAt>=original+1000)continue;
            float d=(f.y+delta)*.05f;
            var turn=summitTurns[(f.laneAt+delta)*.05f];
            if(f.type=="coin")Add("coin",d,turn.lane,1.1f,0).bypass=true;
            else if(f.type=="rock"){
                float x=f.y==f.laneAt?turn.stone:BesideSummitLine((f.x-200)*.05f,SummitLine(d),2.75f);
                Rock(200+20*x,f.y+delta,f.rockWide,f.r);
            }
        }
        // Some of the archived sequences have no fruit between events. Leave a lead-in there.
        for(int i=0;i<3;i++){
            float d=start+8+16*i;
            bool occupied=Array.Exists(layout.objects,o=>Mathf.Abs((o.y+delta)*.05f-d)<8&&
                (o.type=="coin"||o.type=="aid"||o.type=="gift"));
            if(!occupied)Add("coin",d,SummitLine(d),1.1f,0);
        }
    }
    float SummitField(float d,float lane){
        float y=(Mathf.Repeat(d,20000))*20,x=200+lane*20;
        foreach(var p in GetLayout(4,Mathf.FloorToInt(d/20000)).patches){
            if(y<p.y0||y>p.y1)continue;float h=0;
            foreach(var b in p.b){
                float dx=(x-b.x)/(b.Rx>0?b.Rx:b.R),dy=(y-b.y)/(b.Ry>0?b.Ry:b.R);
                if(b.profile==2)dx-=.28f*dy;float q=dx*dx+dy*dy;if(q>=1)continue;
                h=Mathf.Max(h,b.A*Mathf.Pow(Mathf.Cos(Mathf.Sqrt(q)*Mathf.PI*.5f),b.profile==1?1.55f:2));
            }return h;
        }return 0;
    }
}
