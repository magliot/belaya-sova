using System;
using System.Collections.Generic;
using UnityEngine;

public partial class RaceWorld {
    // The old summit and holiday village used Math.random, with weighted sequences
    // between separately authored landmarks. Keep that generator repeatable for arbitrary seeds.
    sealed class UpperBuilder {
        readonly int seed,stage;
        uint state;
        float previousGate=200,previousCourse=-1e7f,lastLane;
        int gateColor;
        bool yeti;
        readonly List<LegacyObject> objects=new List<LegacyObject>();
        readonly List<Field> patches=new List<Field>();
        static readonly string[] summit={"gate","snowman","mogulBit","skier","board","sit","help","mogul","fence","coins","cat","ramp","lane"};
        static readonly int[] summitWeights={22,16,8,12,6,8,4,6,8,18,8,6,16};
        static readonly string[] town={"gate","gifts","skier","board","sbschool","kids","dog","ramp","coins","snowman","lane"};
        static readonly int[] townWeights={22,20,14,8,4,10,8,14,20,6,16};
        static readonly float[] rampWidth={60,44,36,30,48,40},rampHeight={.75f,.55f,.75f,.95f,1.1f,.9f};
        public UpperBuilder(int seed,int stage){this.seed=seed;this.stage=stage;state=unchecked((uint)(seed+stage));lastLane=stage*LegacyLayoutSpan;}
        float Rand(){unchecked{state=1664525u*state+1013904223u;}return (float)(state/4294967296.0);}
        float Range(float a,float b)=>Mathf.LerpUnclamped(a,b,Rand());
        int Index(int n)=>Mathf.Min(n-1,(int)(Rand()*n));
        float Side()=>Rand()<.5f?-1:1;
        LegacyObject Obj(string kind,float x,float y){
            var o=new LegacyObject{type=kind,x=x,y=y};objects.Add(o);return o;
        }
        void Coins(float x,float y,int n,float gap,float offset=0,bool air=false){
            for(int i=0;i<n;i++){var o=Obj("coin",Mathf.Clamp(x+i*offset,40,360),y+i*gap);o.air=air;}
        }
        void CoinLine(float y){if(Rand()>=.35f)Coins(Range(70,330),y,3,32,Range(-14,14));}
        LegacyObject Rider(float x,float y,float pace){
            var o=Obj("npc",x,y);o.vy=pace;int row=Mathf.RoundToInt(y+x);
            o.vi=Mathf.FloorToInt(Roll(seed,row,185)*7);o.ph=Roll(seed,row,181)*6;o.amp=15+25*Roll(seed,row,182);return o;
        }
        LegacyObject Stone(float x,float y){var o=Obj("rock",x,y);o.r=15;o.rockWide=Roll(seed,Mathf.FloorToInt(y),92)<.55f;return o;}
        string Pick(){
            var names=stage==4?summit:town;var weights=stage==4?summitWeights:townWeights;
            int sum=0;foreach(int n in weights)sum+=n;float r=Rand()*sum;
            for(int i=0;i<weights.Length;i++)if((r-=weights[i])<=0)return names[i];
            return names[names.Length-1];
        }
        static bool PersonKind(string k)=>k=="skier"||k=="board"||k=="kids"||k=="sbschool"||k=="sit"||k=="help";
        public Layout Generate(Layout landmarks,bool authoredSection){
            float origin=stage*LegacyLayoutSpan,y=origin+140,end=origin+StageLength*20;
            if(landmarks!=null){
                y=origin+(authoredSection?LegacyLayoutSpan:675*20);
                foreach(var o in landmarks.objects){
                    if(o.y>=y||o.laneAt>=y)continue;
                    objects.Add(o);
                    if(authoredSection&&o.laneAt>0)lastLane=Mathf.Max(lastLane,o.laneAt);
                }
                foreach(var patch in landmarks.patches)if(patch.y0<y)patches.Add(patch);
            }
            while(y<end){
                float speed=560*(.8f+.7f*Grade(y/20))*1.1f;
                bool laneDue=y-lastLane>=Mathf.Max(2400,speed*10);
                string k=laneDue?"lane":Pick();
                if(PersonKind(k)&&Rand()<.15f)k=Rand()<.3f?"gate":"coins";
                if(k=="gate"&&y-previousCourse<1000){k=Pick();if(k=="gate"||PersonKind(k)||k=="cat")k="coins";}
                float len=120;
                switch(k){
                    case "gate":previousCourse=y;len=Course(y,speed);break;
                    case "skier":Rider(Range(80,320),y,Range(95,140));break;
                    case "sit":{var o=Rider(Range(70,330),y,0);o.sit=true;o.amp=0;break;}
                    case "board":{
                        bool kid=Rand()<.35f;var o=Rider(Range(120,280),y,kid?Range(90,120):Range(110,155));
                        o.kid=kid;o.sb=true;o.amp=Range(55,90);break;}
                    case "help":{
                        float x=Rand()<.5f?83:317;Obj("aid",x,y);var o=Rider(x+(x<200?-36:36),y+12,0);o.amp=0;o.sit=true;break;}
                    case "sbschool":{
                        float x=Rand()<.5f?Range(90,115):Range(285,310);
                        var tutor=Rider(x,y+44,0);tutor.sb=tutor.instr=tutor.stand=true;tutor.amp=0;int n=2+Index(4);
                        for(int i=0;i<n;i++){float f=(i-(n-1)*.5f)/Mathf.Max(1,(n-1)*.5f);
                            var o=Rider(Mathf.Clamp(x+f*(10+n*5),40,360),y+Mathf.Abs(f)*9+Range(-3,3),0);
                            o.kid=true;o.sit=Rand()<.5f;o.sb=o.stand=!o.sit;o.face=Mathf.PI+f*.35f;o.amp=0;
                        }len+=80;break;}
                    case "kids":{
                        float x=Range(110,290),ph=Rand()*6;var lead=Rider(x,y,70);lead.instr=true;lead.amp=25;lead.ph=ph;
                        int n=1+Index(3);for(int q=1;q<=n;q++){var o=Rider(x,y-q*26,70);o.kid=true;o.amp=25;o.ph=ph;}
                        len+=30+n*12;break;}
                    case "ramp":{
                        float x=Range(90,310);int i=Index(6);var o=Obj("ramp",x,y);o.w=rampWidth[i];o.hh=rampHeight[i];
                        Coins(x,y+60,4,40,0,true);len+=90;break;}
                    case "lane":{
                        int first=objects.Count;bool aim=laneDue||Rand()<.6f;
                        float x=laneDue?200+Range(-10,10):aim?200+Range(-25,25):Range(70,330);
                        if(Mathf.Abs(x-200)<22)lastLane=y;Stone(x,y);float s=x<200?1:-1;
                        for(int i=0;i<3;i++)Obj("coin",Mathf.Clamp(x+s*Range(55,70),40,360),y-25+i*28);
                        if(Rand()<.45f)Stone(Mathf.Clamp(x+s*Range(105,145),50,350),y+Range(120,170));
                        if(aim)for(int i=first;i<objects.Count;i++){var o=objects[i];o.laneAt=y;o.laneBase=x;o.laneMargin=laneDue?30:50;}
                        len+=40;break;}
                    case "mogul":{
                        int n=3+Index(3);for(int i=0;i<n;i++){var o=Obj("mogul",Range(40,360),y+Range(-50,50));o.r=10;o.profile=Mathf.FloorToInt(Roll(seed,Mathf.RoundToInt(o.y),49)*3);}break;}
                    case "mogulBit":len=Bumps(y);break;
                    case "fence":{
                        float w=Range(80,190);bool left=Rand()<.5f;float a=left?12:388-w,b=left?12+w:388;
                        var o=Obj("fence",(a+b)*.5f,y);o.x1=a;o.x2=b;o.fenceStyle=Mathf.Min(2,(int)(Roll(seed,Mathf.FloorToInt(y+o.x),93)*3));
                        Coins(left?(b+388)*.5f:(12+a)*.5f,y-30,2,30);break;}
                    case "cat":{
                        var o=Rand()<.55f?Obj("cat",Rand()<.5f?Range(55,85):Range(315,345),y+200):Obj("cat",Range(90,310),y);
                        o.vy=o.y>y?-85:145;len+=60;break;}
                    case "dog":Obj("dog",Rand()<.5f?14:386,y+Range(0,60));CoinLine(y+80);break;
                    case "snowman":Obj("snowman",Rand()<.5f?Range(26,40):Range(360,374),y);CoinLine(y+50);break;
                    case "gifts":{
                        float x=Range(80,320),dx=Range(-25,25);for(int i=0;i<4;i++)Obj("gift",Mathf.Clamp(x+i*dx,40,360),y+i*45);break;}
                    default:CoinLine(y);break;
                }
                if(Rand()<.0156f)Obj("helmet",Range(60,340),y+len*.5f);
                else if(Rand()<.045f)Obj(Rand()<.5f?"thermos":"magnet",Range(60,340),y+len*.5f);
                if(stage==4&&!yeti&&Rand()<.007f){Obj("yeti",Rand()<.5f?22:378,y+len*.3f);yeti=true;}
                y=Mathf.Min(y+len,end);
            }
            return new Layout{objects=objects.ToArray(),patches=patches.ToArray()};
        }
        float Course(float y,float speed){
            int row=Mathf.FloorToInt(y),n=4+(int)(Roll(seed,row,0)*4);
            float last=Mathf.Clamp(previousGate,82,318),side=Roll(seed,row,1)<.5f?-1:1,at=y;
            for(int i=0;i<n;i++){
                int k=4*i+2;bool direct=i>0&&Roll(seed,row,k)<.25f;
                float x=direct?Mathf.Clamp(last+(Roll(seed,row,k+2)-.5f)*32,78,322):
                    Mathf.Clamp(164+Roll(seed,row,k+1)*72+side*(42+Roll(seed,row,k+2)*40),78,322);
                if(i>0)at+=speed*(1.02f+Mathf.Abs(x-last)/250+Roll(seed,row,k+3)*.35f);
                var o=Obj("gate",x,at);o.gap=81;gateColor^=1;o.col=gateColor;
                if(Roll(seed,row,k+31)<.26f)Obj("coin",x,at);if(!direct)side=-side;last=x;
            }
            previousGate=last;return at-y+speed*.95f;
        }
        float Bumps(float y){
            int rows=3+Index(2);float sx=Range(48,56),sy=Range(44,52),start=y+10;
            var bumps=new List<Bump>();
            for(int row=0;row<rows;row++)for(float x=26+(row%2==1?sx*.5f:0);x<=376;x+=sx){
                int p=row%3;bumps.Add(new Bump{x=x+Range(-5,5),y=start+30+row*sy+Range(-4,4),profile=p,
                    Rx=Range(27,33)*(p==1?1.24f:p==2?.92f:1),Ry=32*(p==1?.83f:p==2?1.14f:1),A=Range(.4f,.6f)*1.4f});
            }
            float end=start+rows*sy+50;
            patches.Add(new Field{y0=start,y1=end,b=bumps.ToArray()});Coins(Range(100,300),start+70,3,55);return end-y+60;
        }
    }
}
