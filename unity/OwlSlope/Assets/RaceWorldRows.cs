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
        if(!stone&&mode=="normal"&&StageAt(o.distance)==6)o.variant+="Night";
        o.stoneGate=stone;return o;
    }
    RaceItem Person(float x,float y,float speed,string model,float amp,float phase){
        var o=Entry("npc",x,y,25,1.55f,model);o.pace=speed*.05f;o.amp=amp*.05f;o.phase=phase;
        o.sb=model.StartsWith("board")||model=="snowboarder";o.kid=model=="kid"||model=="boardKid";
        o.instr=model=="instructor"||model=="boardInstructor";o.sit=model=="sitter";o.stand=speed==0&&!o.sit;
        return o;
    }
    RaceItem Tree(float x,float y,int family,float size){
        int v=Mathf.Min(2,(int)(Roll(seed,Mathf.FloorToInt(y)+Mathf.RoundToInt(x),91)*3));
        var o=Entry("tree",x,y,family==1?36:24,2.2f,"tree"+family+v);o.scale=size;o.yaw=-Mathf.Repeat(y*.13f,6.28f)*Mathf.Rad2Deg;
        o.offset=-.14f*size;return o;
    }
    RaceItem Cat(float x,float y,float speed,bool small=false){
        string model=small?"cat12A":speed>0?"rattForward":"rattOncoming";
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
        float rowLength=RowLength(fullStage,mode),d=2500*fullStage+n*rowLength,y=d*20;
        bool full=d+rowLength<=(fullStage+1)*2500+.01f;
        if(n==0&&fullStage>0)Entry("arch",200,y,280,5.6f,"arch"+stage);
        if(fullStage>=8&&(stage==1||stage==6)){EndlessFork(fullStage,n);Filter(start);return;}
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
        if(n%8==1)Entry("cannon",n%16==1?38:362,y+420,28,2,n%16==1?"cannonLeft":"cannonRight");
        if(n%12==4)Rescue(94,y+780);
        float offset=560+Mathf.Floor(R(9)*260),open=200-side*(52+R(6)*34);
        string kind=n==0?"tube":n==1?"gate":n%12==2?"snowman":n%12==7?"cat":n%6==4?"rest":n%3==0?"tube":R(2)<.48f?"gate":R(3)<.52f?"tube":"snowman";
        float at=y+(n==0?332:180)+Mathf.Floor(R(40)*58),ph=R(41)*Mathf.PI*2;
        void Pupil(float dy,float x,float v,string name){Person(200-side*x,at+dy,v,name,v==0?0:n%5==1?28:23,ph);}
        if(n%5==0){Pupil(0,n==0?88:104,42,"kid");Pupil(88,n==0?103:94,42,"instructor");}
        if(n%5==1){Pupil(-40,109,49,"snowboarder");Pupil(48,146,0,"boardKid");Pupil(95,129,0,"boardInstructor");}
        if(n%5==3&&R(43)<.68f){Pupil(0,151,0,"sitter");Pupil(47,127,0,"skier");}
        if(n==0)Coins(200,220,4,55);
        if(kind=="tube"){
            int route=Mathf.Min(2,(int)(R(14)*3));var o=Tube(200+side*85,y+offset,side,Tubes[Mathf.Min(3,(int)(R(5)*4))]);
            o.phase=R(4);o.release=Mathf.Max(0,y+offset-750-R(15)*320)*.05f;o.vStart=(route==1?28:8)*.05f;
            o.vCruise=(62+R(16)*39)*.05f;o.tau=1.4f+R(17);o.pathSpan=((route==2?250:190)+R(18)*140)*.05f;
            o.from=(route==0?132:route==1?54:141)*.05f;o.to=(route==0?42:route==1?91:-17)*.05f;Coins(open,y+offset-175,4,55);
        }else if(kind=="cat"){
            bool uphill=((seed+n/12)&1)!=0;
            Cat(200+side*96,y+offset,uphill?-60:60,true);Coins(open,y+offset-145,4,55);
        }
        else if(kind=="gate"){Gate(open,y+offset,118,n);Coins(open,y+offset-160,3,55);}
        else if(kind=="snowman"){int id=n%12==2?n/12:(int)(R(8)*3);Snowman(200+side*108,y+offset,"snow26"+"ABC"[(int)((uint)(id+seed)%3)]);Coins(open,y+offset-160,3,55);}
        else{Coins(200,y+280,6,55);if(n==4||R(11)<.48f)Entry(n==4||R(12)<.4f?"helmet":"magnet",200,y+685,22,.85f);}
        if(n%12==3)Entry("mogul",200+side*23,y+520,32,.5f,"mound"+(n/12)%3);
        if(n>4&&n%5==1)Gate(open,y+1050,130,n%2);
        if(n%6==1)Entry("thermos",open,y+950,22,.85f);
    }
    void Forest(float y,int n,bool sprint){
        int id=Mathf.FloorToInt(y/1000);float R(int k)=>Roll(seed,id,k);int side=R(31)>.5f?1:-1;
        if(n==49){Coins(200,y+70,3,100);return;}
        float line=200-side*(56+R(32)*17),j=Mathf.Floor(R(35)*46);
        bool canopy=!sprint&&n%4==0;
        if(canopy){
            float centre=200+side*(12+R(34)*13);
            bool pines=n%8==0;
            foreach(int edge in sides){
                float size=pines?1.01f+.10f*R(52+edge):1.08f;
                var stem=Tree(centre+edge*(pines?95:99),y+370+j+edge*12,1,size);
                stem.variant=pines?"canopyPine":"canopyFir";stem.lean=pines?-11:-17;
                stem.yaw=edge<0?0:180;stem.offset=-.24f;
            }
            line=centre;
        }else{
            Tree(200+side*145,y+260+j,1,.97f);Tree(200+side*(51+R(34)*20),y+332+j,2,.90f);
            if(!sprint&&n%3==1)Tree(200+side*R(36)*16,y+451+j,0,.83f);
        }
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
    float Valley(float y)=>200+20*MogulLine(y*.05f);
    void Rollers(float y,int n){
        if(n%2==0)TrailActor("mogulYeti",y+1040,n%4==0?1:-1);
        for(int i=0;i<9;i++){float at=y+130+i*249;Entry("coin",Valley(at),at,22,0);}
        float heapAt=y+615,heapLine=Valley(heapAt);
        int heapSide=heapLine>200?-1:1;
        Entry("mogul",heapLine+heapSide*77,heapAt,34,.60f,"mound"+n%3);
        if(n%3==2){float rockAt=y+1370,track=Valley(rockAt);Rock(track+(track>200?-84:84),rockAt,true);}
        if(n%4==1){
            int side=Roll(seed,n,68)<.5f?-1:1;var o=Tube(200+side*100,y+920,side,Tubes[Mathf.Min(3,(int)(Roll(seed,n,69)*4))]);
            o.phase=.5f;o.release=(y+160)*.05f;o.vStart=.9f;o.vCruise=3.8f;o.tau=2;o.pathSpan=12.75f;o.from=5.6f;o.to=1.4f;
        }else if(n%4==3)Person(265,y+890,64,"snowboarder",23,n);
        float line=Valley(y+1600);if(n%3==1)Box(line+(line>200?-84:84),y+1600,44,160,.52f);else Gate(line,y+1600,105,n);
        if(n%3==2)Person(278,y+470,57,"skier",12,n);
        string reward=new[]{"thermos","magnet",null,"thermos",null,"gift",null,"helmet"}[n%8];
        if(reward!=null)Entry(reward,Valley(y+1990),y+1990,24,.85f);
    }
    void Park(float y,int section,int shortTier=-1){
        int n=Mathf.FloorToInt(y/1500);int tier=shortTier>=0?shortTier:section%3;
        bool trick=shortTier<0&&section%6==0,prize=shortTier<0&&section%9==2;
        if(shortTier<0&&section%9==3)return;
        int line=prize?2:trick?1:tier;float w=new[]{110,99,90}[line],len=new[]{125,160,200}[line],height=new[]{.95f,1.60f,2.35f}[line];
        float span=prize?1120:trick?760:new[]{395,515,660}[line],lift=new[]{1.65f,2.60f,3.65f}[line];
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
            if(j==n%3||j==1){
                foreach(int edge in sides){
                    bool wide=edge==turn;
                    float scale=wide?1.85f:1.65f;
                    float radius=scale*(wide?.77f:.42f);
                    var stone=Rock(open+edge*(41+radius*20),at,wide,radius*20);
                    stone.variant=wide?"gateRockWide":"gateRockTall";stone.scale=scale;
                    stone.offset=-.13f;stone.length=2*radius;
                }
                Gate(open,at,78,0,true);
            }
            else{Rock(200+turn*23,at,true,20);Rock(200+turn*103,at+67,false,15);}
        }
        // Only one moving party per section; its home line is outside all stone openings.
        float atTraffic=y+280+Roll(seed,n,417)*170;
        if(n%5==3){
            var tube=Tube(200-side*147,atTraffic, -side,Tubes[(int)(Roll(seed,n,418)*3.99f)]);
            tube.release=(atTraffic-780)*.05f;tube.vStart=1.8f;tube.vCruise=3.6f;tube.tau=2.4f;
            tube.from=8.0f;tube.to=7.3f;tube.pathSpan=17;
        }else if(n%5!=4){
            string model=n%5==1?"snowboarder":n%5==2?"kid":"skier";
            Person(200-side*(149+Roll(seed,n,419)*15),atTraffic,model=="kid"?46:62,model,9,Roll(seed,n,420)*6.28f);
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
    float BonusLine(float row){
        float arc=Mathf.Sin(row*.96f+Roll(seed,0,55)*6.28f);
        float weave=Mathf.Sin(row*.41f+Roll(seed,0,87)*6.28f);
        return 200+Mathf.Min(row,1)*(arc*58+weave*26);
    }
    void BonusKicker(float y,float x,bool motor){
        float height=motor?1.12f:.92f,lift=motor?2.1f:1.8f;
        var ramp=Kicker(x,y+148,98,96,height);
        ramp.park=true;ramp.span=motor?19:20;ramp.lift=lift;
        Coins(x,y+22,2,54);
        for(int i=1;i<=4;i++){
            float t=i*.18f;
            var fruit=Entry("coin",x,y+196+20*ramp.span*t,22,(1-t)*(height+4*lift*t)+.9f);
            fruit.air=true;
        }
    }
    void BonusRow(int n,bool motor){
        float length=motor?620:660,y=n*length,x=BonusLine(n+.45f);
        bool jump=n%5==(motor?2:1);
        if(jump)BonusKicker(y,x,motor);
        else{
            for(int i=0;i<5;i++){
                float t=.065f+i*.19f;
                Entry("coin",BonusLine(n+t),y+t*length,22,0);
            }
            if((motor&&n%3!=1)||(!motor&&n%5==3))Snowman(BonusLine(n+.55f),y+length*.55f);
            if(!motor&&n%3!=1)Gate(BonusLine(n+.58f),y+length*.58f,122,n);
            if(n%2==0||motor)Entry("gift",BonusLine(n+.93f),y+length*.93f,24,.85f);
        }
        if(n%6==0||n%6==3){
            float side=x>=200?-1:1;
            // A short optional outside line. It rejoins the main fruit trail before the next obstacle.
            float spare=BonusLine(n+.65f)+side*43;
            Entry("coin",spare-side*14,y+length*.43f,22,0);
            Entry(n%6==0?"thermos":"magnet",spare,y+length*.65f,24,.8f);
        }
    }
    public static float TubeWoods(float metres)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(410,710,metres))*
        (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1020,1360,metres)));
    void Tubing(int n){
        BonusRow(n,false);
        float y=n*660,woods=TubeWoods(y/20);
        foreach(int side in sides){
            float r=Roll(seed,n,60+side);
            if(r<woods){
                Tree(200+side*(147+r*24),y+92+r*300,r>.59f?2:r<.44f?1:0,1.30f);
                Tree(200+side*(194+r*14),y+454,1,1.50f);
            }
        }
    }
    void Sled(int n){BonusRow(n,true);}
}
