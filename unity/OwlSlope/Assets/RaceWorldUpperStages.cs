using System;
using System.Collections.Generic;
using UnityEngine;

public partial class RaceWorld {
    [Serializable] class LegacyObject {
        public string type;
        public float x,y,w,hh,x1,x2,vy,amp,ph,r,gap,vx,face;
        public int col,profile,vi,fenceStyle;
        public bool sb,stand,kid,instr,sit,walker,rockWide,air;
    }
    [Serializable] class Bump {public float x,y,R,Rx,Ry,A;public int profile;}
    [Serializable] class Field {public float y0,y1;public Bump[] b;}
    [Serializable] class Layout {public LegacyObject[] objects;public Field[] patches;}
    static readonly int[] layoutSeeds={19,1911,1927,4903};
    static readonly Dictionary<int,Layout> upperLayouts=new Dictionary<int,Layout>();
    readonly Dictionary<int,Layout> generatedLayouts=new Dictionary<int,Layout>();
    int generatedSeed;
    Layout GetLayout(int stage){
        int choice=Array.IndexOf(layoutSeeds,seed);
        if(choice<0){
            if(generatedSeed!=seed){generatedLayouts.Clear();generatedSeed=seed;}
            if(!generatedLayouts.TryGetValue(stage,out var generated)){
                generated=new UpperBuilder(seed,stage).Generate(stage==4?ReferenceLayout(4,0):null);
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
    void Mountain(float y,int row){UpperRow(4,y,row);}
    void Holiday(float y,int row){UpperRow(7,y,row);}
    void UpperRow(int stage,float y,int row){
        var layout=GetLayout(stage);float original=stage*50000+row*1000,delta=y-original;
        foreach(var f in layout.objects){
            if(f.y<original||f.y>=original+1000)continue;
            float at=f.y+delta;
            switch(f.type){
                case "npc":{
                    string model=f.sit?"sitter":f.sb?(f.instr?"boardInstructor":f.kid?"boardKid":"snowboarder"):
                        f.kid?"kid":f.instr?"instructor":"skier";
                    var o=Person(f.x,at,f.vy,model,f.amp,f.ph);o.walker=f.walker;o.yaw=f.face*Mathf.Rad2Deg;
                    if(f.walker)o.variant="walker";else if(model=="boardKid"&&f.vi%2==1)o.variant="boardKid1";break;}
                case "ramp":Kicker(f.x,at,f.w>0?f.w:54,64,f.hh>0?f.hh:.75f);break;
                case "gate":Entry("gate",f.x,at,f.gap,0,f.col!=0?"gateBlue":"gateRed");break;
                case "snowman":Snowman(f.x,at);break;
                case "rock":Rock(f.x,at,f.rockWide,f.r);break;
                case "mogul":Entry("mogul",f.x,at,2*f.r,.4f,"mound"+Mathf.Clamp(f.profile,0,2));break;
                case "cannon":Entry("cannon",f.x,at,28,2,f.x<200?"cannonLeft":"cannonRight");break;
                case "fence":Entry("fence",f.x,at,f.x2-f.x1,1.4f,f.fenceStyle>0?"fence"+f.fenceStyle:"fence");break;
                case "cat":Cat(f.x,at,f.vy);break;
                case "dog":Entry("dog",f.x,at,24,.8f);break;
                case "yeti":Entry("yeti",f.x,at,38,2);break;
                case "coin":{var o=Entry("coin",f.x,at,22,f.air?1.8f:0);o.air=f.air;break;}
                case "aid":case "thermos":case "gift":case "helmet":case "magnet":Entry(f.type,f.x,at,24,.85f);break;
            }
        }
    }
    float SummitField(float d,float lane){
        float y=(Mathf.Repeat(d,20000))*20,x=200+lane*20;
        foreach(var p in GetLayout(4).patches){
            if(y<p.y0||y>p.y1)continue;float h=0;
            foreach(var b in p.b){
                float dx=(x-b.x)/(b.Rx>0?b.Rx:b.R),dy=(y-b.y)/(b.Ry>0?b.Ry:b.R);
                if(b.profile==2)dx-=.28f*dy;float q=dx*dx+dy*dy;if(q>=1)continue;
                h=Mathf.Max(h,b.A*Mathf.Pow(Mathf.Cos(Mathf.Sqrt(q)*Mathf.PI*.5f),b.profile==1?1.55f:2));
            }return h;
        }return 0;
    }
}
