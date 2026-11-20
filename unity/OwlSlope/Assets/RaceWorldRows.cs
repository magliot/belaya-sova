using UnityEngine;

public partial class RaceWorld {
    // Legacy generators specify x, y, width and span in twentieths of a metre.
    // Convert as objects enter the Unity world; from here on collisions share mesh coordinates.
    RaceItem Entry(string kind,float x,float y,float w,float h,string model=null){
        return Add(kind,y*.05f,(x-200)*.05f,w*.05f,h,model);
    }
    void Coins(float x,float y,int count,float gap){for(int i=0;i<count;i++)Entry("coin",x,y+i*gap,22,0);}
    RaceItem Gate(float x,float y,float gap,int color,bool stone=false){
        var o=Entry("gate",x,y,stone?gap:Mathf.Round(gap*.90f),0,stone?"stoneGate":color%2==0?"gateRed":"gateBlue");
        o.stoneGate=stone;return o;
    }
    RaceItem Person(float x,float y,float speed,string model,float amp,float phase){
        var o=Entry("npc",x,y,25,1.55f,model);o.pace=speed*.05f;o.amp=amp*.05f;o.phase=phase;
        o.sb=model.StartsWith("board")||model=="snowboarder";o.kid=model=="kid"||model=="boardKid";
        o.instr=model=="instructor"||model=="boardInstructor";o.sit=model=="sitter";o.stand=speed==0&&!o.sit;
        o.activation=(y-640)*.05f;return o;
    }
    void Tree(float x,float y,int family,float size){
        int v=Mathf.Min(2,(int)(Roll(seed,Mathf.FloorToInt(y)+Mathf.RoundToInt(x),91)*3));
        var o=Entry("tree",x,y,family==1?36:24,2.2f,"tree"+family+v);o.scale=size;o.yaw=-Mathf.Repeat(y*.13f,6.28f)*Mathf.Rad2Deg;
    }
    RaceItem Cat(float x,float y,float speed,bool small=false){
        string model=small&&((Mathf.FloorToInt(y/1250)/12+seed)&1)==0?"cat12A":speed>0?"rattForward":"rattOncoming";
        var o=Entry("cat",x,y,small?46:76,2.8f,model);o.pace=speed*.05f;o.small=small;o.length=small?3.7f:6;return o;
    }
    RaceItem Kicker(float x,float y,float w,float length,float height){
        var o=Entry("ramp",x,y,w,height);o.length=length*.05f;return o;
    }
    void Box(float x,float y,float w,float length,float height){
        var o=Entry("rail",x,y,w,height);o.length=length*.05f;
    }
    RaceItem Snowman(float x,float y,string variant="snowman"){return Entry("snowman",x,y,32,1.6f,variant);}
    RaceItem Rock(float x,float y,bool wide,float r=18){return Entry("rock",x,y,r*2,1.2f,wide?"rockWide":"rockTall");}
    void Rescue(float x,float y){Entry("aid",x,y,24,.8f);Person(x+(x<200?-36:36),y+12,0,"sitter",0,0);}
    void TrailActor(string kind,float y,int side){
        var o=Entry(kind,200+side*(kind=="snowmobile"?63:173),y,kind=="snowmobile"?44:58,kind=="snowmobile"?1.8f:2.7f,kind=="snowmobile"?"snowmobile":"yeti");
        o.side=side;o.route=Mathf.Min(2,(int)(Roll(seed,Mathf.RoundToInt(y),101)*3));
        o.leap=Roll(seed,Mathf.RoundToInt(y),102)<.55f;o.phase=Roll(seed,Mathf.RoundToInt(y),103)*6.28f;
    }
    static readonly string[] Tubes={"tube60B","tube60C","tube61A","tube61C"};
    RaceItem Tube(float x,float y,int side,string model){
        var o=Entry("tube",x,y,44,1.15f,model);o.side=side;return o;
    }
    void BuildRow(int seg){
        int start=Items.Count;
        if(seg==0&&mode!="sled"&&mode!="tubing")Add("startArch",12,0,12,5);
        if(mode=="sprint"){Sprint(seg);Filter(start);return;}
        if(mode=="tubing"){Tubing(seg);Filter(start);return;}
        if(mode=="sled"){Sled(seg);Filter(start);return;}
        int fullStage=seg/1000,n=seg%1000,stage=fullStage%8;
        float d=2500*fullStage+n*RowLength(stage,mode),y=d*20;
        bool full=d+RowLength(stage,mode)<=(fullStage+1)*2500+.01f;
        if(n==0&&fullStage>0)Entry("arch",200,y,280,5.6f,"arch"+stage);
        switch(stage){
            case 0:Training(Mathf.FloorToInt(y/1250),y);break;
            case 1:Forest(y,n,false);break;
            case 2:if(full)Rollers(y,n);else Coins(200,y+100,4,125);break;
            case 3:if(full)Park(y,n);break;
            case 4:Mountain(y,n);break;
            case 5:if(full)Black(y,n,fullStage);break;
            case 6:if(full)Night(y,n,fullStage);break;
            case 7:Holiday(y,n);break;
        }
        Filter(start);
    }
    static bool Solid(RaceItem o){
        switch(o.kind){case "npc":return o.sit||o.stand;
            case "tree":case "rock":case "ramp":case "rail":case "snowman":case "aid":case "cannon":case "fence":case "mogul":return true;
            default:return false;}
    }
    void Filter(int start){
        for(int i=start;i<Items.Count;i++){
            var a=Items[i];bool gate=a.kind=="gate"&&!a.stoneGate;if(!Solid(a)&&!gate)continue;
            bool overlaps=false;float z=a.length>0?a.length*.5f:a.kind=="rock"?1.2f:.7f;
            float stageDist=Mathf.Round(a.distance/2500)*2500;
            if(mode!="tubing"&&mode!="sled"&&mode!="sprint"&&stageDist>0&&Mathf.Abs(a.distance-stageDist)<z+.55f&&Mathf.Abs(Mathf.Abs(a.lane)-7)<a.width*.5f+.55f)overlaps=true;
            for(int j=0;j<i&&!overlaps;j++){
                var b=Items[j];if(Mathf.Abs(b.distance-a.distance)>z+(b.length>0?b.length*.5f:1)+.15f)continue;
                if(gate&&Solid(b))overlaps=Mathf.Abs(Mathf.Abs(a.lane-b.lane)-a.width*.5f)<b.width*.5f+.45f;
                else if(gate&&b.kind=="gate"&&!b.stoneGate)overlaps=Mathf.Abs(Mathf.Abs(a.lane-b.lane)-Mathf.Abs(a.width-b.width)*.5f)<.75f;
                else if(!gate&&b.kind=="gate"&&!b.stoneGate)overlaps=Mathf.Abs(Mathf.Abs(a.lane-b.lane)-b.width*.5f)<a.width*.5f+.45f;
                else if(b.bypass)overlaps=gate?Mathf.Abs(Mathf.Abs(a.lane-b.lane)-a.width*.5f)<1.1f:
                    Mathf.Abs(a.lane-b.lane)<a.width*.5f+.8f;
                else if(Solid(b))overlaps=Mathf.Abs(a.lane-b.lane)<(a.width+b.width)*.5f+.15f;
            }
            if(overlaps)Items.RemoveAt(i--);
        }
    }
    void Training(int n,float y){
        float R(int k)=>Roll(seed,n,k);
        int side=R(13)<.5f?-1:1;
        foreach(int s in sides)Entry("boundaryPole",200+s*196,y+170,1,1.1f);
        if(n%8==1)Entry("cannon",n%16==1?38:362,y+420,28,2,n%16==1?"cannonLeft":"cannonRight");
        if(n%12==4)Rescue(94,y+780);
        float offset=560+Mathf.Floor(R(9)*260),open=200-side*(52+R(6)*34);
        string kind=n==0?"tube":n==1?"gate":n%12==2?"snowman":n%12==7?"cat":n%6==4?"rest":n%3==0?"tube":R(2)<.48f?"gate":R(3)<.52f?"tube":"snowman";
        float at=y+(n==0?332:180)+Mathf.Floor(R(40)*58),ph=R(41)*Mathf.PI*2;
        void Pupil(float dy,float x,float v,string name){var o=Person(200-side*x,at+dy,v,name,v==0?0:n%5==1?28:23,ph);o.activation=(at-640)*.05f;}
        if(n%5==0){Pupil(0,n==0?88:104,42,"kid");Pupil(88,n==0?103:94,42,"instructor");}
        if(n%5==1){Pupil(-40,109,49,"snowboarder");Pupil(48,146,0,"boardKid");Pupil(95,129,0,"boardInstructor");}
        if(n%5==3&&R(43)<.68f){Pupil(0,151,0,"sitter");Pupil(47,127,0,"skier");}
        if(n==0)Coins(200,220,4,55);
        if(kind=="tube"){
            int route=Mathf.Min(2,(int)(R(14)*3));var o=Tube(200+side*85,y+offset,side,Tubes[Mathf.Min(3,(int)(R(5)*4))]);
            o.phase=R(4);o.release=Mathf.Max(0,y+offset-750-R(15)*320)*.05f;o.vStart=(route==1?28:8)*.05f;
            o.vCruise=(62+R(16)*39)*.05f;o.tau=1.4f+R(17);o.pathSpan=((route==2?250:190)+R(18)*140)*.05f;
            o.from=(route==0?132:route==1?54:141)*.05f;o.to=(route==0?42:route==1?91:-17)*.05f;Coins(open,y+offset-175,4,55);
        }else if(kind=="cat"){var o=Cat(200+side*96,y+offset,60,true);o.side=side;Coins(open,y+offset-145,4,55);}
        else if(kind=="gate"){Gate(open,y+offset,118,n);Coins(open,y+offset-160,3,55);}
        else if(kind=="snowman"){int id=n%12==2?n/12:(int)(R(8)*3);Snowman(200+side*108,y+offset,"snow26"+"ABC"[(int)((uint)(id+seed)%3)]);Coins(open,y+offset-160,3,55);}
        else{Coins(200,y+280,6,55);if(n==4||R(11)<.48f)Entry(n==4||R(12)<.4f?"helmet":"magnet",200,y+685,22,.85f);}
        if(n>4&&n%5==1)Gate(open,y+1050,130,n%2);
        if(n%6==1)Entry("thermos",open,y+950,22,.85f);
    }
    void Forest(float y,int n,bool sprint){
        int id=Mathf.FloorToInt(y/1000);float R(int k)=>Roll(seed,id,k);int side=R(31)>.5f?1:-1;
        if(n==49){Coins(200,y+70,3,100);return;}
        float line=200-side*(56+R(32)*17),j=Mathf.Floor(R(35)*46);
        Tree(200+side*145,y+260+j,1,.97f);Tree(200+side*(51+R(34)*20),y+332+j,2,.90f);
        if(!sprint&&n%3==1)Tree(200+side*R(36)*16,y+451+j,0,.83f);
        Tree(200+side*161,y+409+j,0,1.13f);Tree(200-side*148,y+750-j,0,1.02f);
        if(R(39)>.30f)Tree(200-side*121,y+822-j,2,.88f);
        if(sprint?n==3:n%3==0)TrailActor("snowmobile",y+610,side);
        if(!sprint){
            if(n%3!=0)Person(line,y+654,52,n%3==1?"snowboarder":"skier",14,R(41)*6);
            if(n%8==3)Kicker(line,y+180,54,64,.7f);if(n%7==2)Entry("mogul",200+side*43,y+732,28,.4f,"mound"+n%3);
        }
        for(int i=0;i<6;i++)Entry("coin",200+(line-200)*Mathf.Sin((i+1)*Mathf.PI/8),y+78+i*94,22,0);
        if(n%3!=1&&R(38)<.23f)Gate(line,y+558,112,side>0?1:0);
    }
    static float Valley(float y)=>120+28*Mathf.Sin(y/460);
    void Rollers(float y,int n){
        if(n%2==0)TrailActor("mogulYeti",y+1040,n%4==0?1:-1);
        for(int i=0;i<12;i++){float at=y+150+i*175;if(i%3==0)Entry("coin",Valley(at),at,22,0);Entry("coin",i%2==1?255:285,at,22,0);}
        if(n%4==1){
            int side=Roll(seed,n,68)<.5f?-1:1;var o=Tube(200+side*100,y+920,side,Tubes[Mathf.Min(3,(int)(Roll(seed,n,69)*4))]);
            o.phase=.5f;o.release=(y+160)*.05f;o.vStart=.9f;o.vCruise=3.8f;o.tau=2;o.pathSpan=12.75f;o.from=5.6f;o.to=1.4f;
        }else if(n%4==3)Person(265,y+890,64,"snowboarder",23,n);
        float line=Valley(y+1600);if(n%3==1)Box(line,y+1600,44,160,.52f);else Gate(line,y+1600,100,n);
        if(n%3==2)Person(278,y+470,57,"skier",12,n);
        string reward=new[]{"thermos","magnet",null,"thermos",null,"gift",null,"helmet"}[n%8];
        if(reward!=null)Entry(reward,Valley(y+1990),y+1990,24,.85f);
    }
    void Park(float y,int section,int shortTier=-1){
        int n=Mathf.FloorToInt(y/1500);int tier=shortTier>=0?shortTier:section%3;
        bool trick=shortTier<0&&section%6==0,prize=shortTier<0&&section%9==2;
        if(shortTier<0&&section%9==3)return;
        int line=prize?2:trick?1:tier;float w=new[]{110,99,90}[line],len=new[]{125,160,200}[line],height=new[]{.95f,1.60f,2.35f}[line];
        float span=prize?1740:trick?760:new[]{395,515,660}[line],lift=new[]{1.65f,2.60f,3.65f}[line];
        int side=shortTier<0?(Roll(seed,n,88)<.5f?1:-1):(n%2==1?1:-1);
        float x=200+side*new[]{24,46,55}[tier],at=y+310,lip=at+len*.5f;
        var ramp=Kicker(x,at,w,len,height);ramp.park=true;ramp.span=span*.05f;ramp.lift=lift;ramp.trickLine=trick||prize;ramp.prizeLine=prize;
        void Target(string kind,float t,float dx=0,bool upper=false){
            float h=(1-t)*(height+4*lift*t)+.9f+(upper?2.7f*Mathf.Sin(Mathf.PI*t):0);
            var o=Entry(kind,x+dx,lip+t*span,kind=="airRing"?62:22,h,kind=="airRing"?"ring"+tier:kind);
            o.air=true;o.needsTrick=upper&&kind=="airRing";o.points=new[]{45,70,100}[line];o.prizeLine=prize;
        }
        Target("coin",.16f);Target("coin",.31f);Target("airRing",prize?.43f:trick?.68f:.48f,side*tier*5,trick||prize);
        if(prize)Target("feather",.76f,0,true);
        string reward=shortTier>=0?new[]{"coin","thermos","helmet"}[tier]:n%29==0?"helmet":n%7==0?"thermos":"coin";
        Target(reward,trick?.55f:.68f,0,trick);Target("coin",.82f,0,trick);
        if(tier==0)Box(200-side*103,at+90,44,190,.65f);
        if(shortTier<0&&section%3!=2){Person(200-side*133,y+640,48,section%3==1?"snowboarder":"skier",14,n);
            if(section%6==4)Entry("mogul",200+side*133,y+1080,24,.4f,"mound0");}
    }
    void Black(float y,int n,int stage){
        int side=(n%2==1?1:-1)*(Roll(seed,stage,98)<.5f?-1:1);
        for(int j=0;j<3;j++){
            float at=y+360+j*525;int turn=(j%2==1?-1:1)*side;float open=200-turn*53;Coins(open,at-207,3,70);
            if(j==n%3||j==1){Rock(open-64,at,-1==turn);Rock(open+64,at,1==turn);Gate(open,at,78,0,true);}
            else{Rock(200+turn*23,at,true,20);Rock(200+turn*103,at+67,false,15);}
        }
    }
    void Night(float y,int n,int stage){
        int p=n%8,side=(n%2==1?1:-1)*(Roll(seed,stage,98)<.5f?-1:1);float open=200-side*64;
        if(p==5){Park(y+190,n,1);return;}Coins(open,y+135,4,82);
        if(n%4==2)Cat(200+side*83,y+1160,-48);
        else if(n%2==0)TrailActor("mogulYeti",y+1020,side);
        else Person(200+side*94,y+650,p==3?68:54,p==3||p==7?"snowboarder":"skier",22,n);
        if(n%2==1||p==0)Gate(open,y+570,112,n);
        string item=p==0?"thermos":p==2?"magnet":p==6?(n%16==14?"helmet":"gift"):null;if(item!=null)Entry(item,open,y+1410,24,.85f);
    }
    void Sprint(int n){
        float y=n*1500;if(n>16)return;if(n==16){Entry("finish",200,y,320,4.5f);return;}
        if(n==3||n==8||n==12)Forest(y,n,true);
        else if(n==5||n==10||n==14)Park(y,n,n==5?0:n==10?1:2);
        else if(n==2||n==9){Coins(200,y,4,100);Entry("thermos",200,y+420,24,.85f);}
        else if(n==7){Snowman(269,y+440);Coins(150,y+100,5,115);}
        else for(int j=0;j<3;j++){float x=200+((n+j)%2==1?48:-48);Gate(x,y+300+j*415,138,j);Entry("coin",x,y+300+j*415,22,0);}
    }
    void Tubing(int n){
        float y=n*660;int phase=y<7200?0:y<15200?1:2,sign=(n/(phase==1?2:1))%2==1?1:-1;
        float x=200+sign*(38+Roll(seed,n,55)*65);Coins(x,y+90,3,78);
        if(n%3==1)Gate(x,y+365,phase==1?94:122,n);if(n%4==3)Entry("gift",x,y+435,24,.85f);
        if(phase==1)foreach(int side in sides){float r=Roll(seed,n,60+side);Tree(200+side*(146+r*25),y+90+r*310,r>.59f?2:r<.44f?1:0,1.4f);Tree(200+side*(193+r*12),y+460,1,1.6f);}
    }
    void Sled(int n){
        int phase=Mathf.Min(2,n/9);int[] pattern=phase==0?new[]{1,0,1,2}:phase==1?new[]{0,1,2,1}:new[]{2,1,0,2,1,0};
        int l=pattern[n%pattern.Length];if(Roll(seed,phase,87)>=.5f)l=2-l;float y=n*620,x=135+l*65;
        Coins(x,y+100,3,75);if(n%3==2)Entry("gift",x,y+370,24,.85f);else Snowman(x,y+370);
        if(phase==2)Entry("gift",135+((l+1)%3)*65,y+510,24,.85f);
    }
}
