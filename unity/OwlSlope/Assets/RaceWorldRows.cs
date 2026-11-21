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
    void BuildRow(int seg,bool geometryOnly=false){
        int start=Items.Count;
        if(seg==0&&mode!="sled"&&mode!="tubing")Add("startArch",12,0,12,5);
        if(mode=="sprint"){Sprint(seg);Filter(start);return;}
        if(mode=="tubing"){Tubing(seg);Filter(start);return;}
        if(mode=="sled"){Sled(seg);Filter(start);return;}
        int fullStage=seg/1000,n=seg%1000,stage=fullStage%8;
        float rowLength=RowLength(fullStage,mode),d=2500*fullStage+n*rowLength,y=d*20;
        bool full=d+rowLength<=(fullStage+1)*2500+.01f;
        if(n==0&&fullStage>0)Entry("arch",200,y,280,5.6f,"arch"+stage);
        if(fullStage>=8&&(stage==1||stage==6))EndlessScene(fullStage,n);
        else switch(stage){
            case 0:
                if(n==5||n==6)TrainingTraffic(d,fullStage,n==6);
                else Training(Mathf.FloorToInt(y/1250),y);
                break;
            case 1:Forest(y,n,false);break;
            case 2:if(full)Rollers(y,n);break;
            case 3:
                if(full)Park(y,n);
                break;
            case 4:Mountain(y,n);break;
            case 5:if(full)Black(y,n,fullStage);break;
            case 6:if(full)Night(y,n,fullStage);break;
            case 7:Holiday(y,n);break;
        }
        if(geometryOnly)return;
        Filter(start);
        ScatterGroundFruit(start,seg,d,Mathf.Min(rowLength,(fullStage+1)*2500-d));
    }
    static bool Solid(RaceItem o){
        switch(o.kind){case "npc":return o.sit||o.stand;
            case "tree":case "rock":case "ramp":case "rail":case "snowman":case "aid":case "cannon":case "fence":case "mogul":case "festivalFountain":return true;
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
                else if(Solid(b))overlaps=Mathf.Abs(a.lane-b.lane)<(a.width+b.width)*.5f+.15f;
            }
            if(overlaps)Items.RemoveAt(i--);
        }
    }
    // A single training gate with room to pass a skier on either side of the poles.
    void TrainingTraffic(float start,int fullStage,bool followingGate){
        int key=fullStage*40+5;
        float side=Roll(seed,key,783)<.5f?-1:1;
        float centre=side*(.8f+.95f*Roll(seed,key,785));
        if(followingGate){
            Add("gate",start+25,-centre*.82f,5.6f,0,"gateBlue");
            float phase=Roll(seed,key,787)*2*Mathf.PI;
            Person(200+side*132,(start+8)*20,49,"snowboarder",13,phase);
            Person(200+side*157,(start+12)*20,0,"boardKid",0,phase);
            Person(200+side*172,(start+15)*20,0,"boardInstructor",0,phase);
            return;
        }
        float gateAt=start+35;
        Add("gate",gateAt,centre,6.2f,0,"gateRed");
        var skier=Person(200+20*(centre+side*4.45f),(gateAt-27.5f)*20,
            43,"skier",12,Roll(seed,key,786)*2*Mathf.PI);
        skier.mergeGateMetre=gateAt;skier.mergeGateLane=centre;skier.side=side;
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
        if(n%5==0){Pupil(0,n==0?-108:104,42,"kid");Pupil(88,n==0?-137:94,42,"instructor");}
        if(n%5==1){Pupil(-40,109,49,"snowboarder");Pupil(48,146,0,"boardKid");Pupil(95,129,0,"boardInstructor");}
        if(n%5==3&&R(43)<.68f){Pupil(0,151,0,"sitter");Pupil(47,127,0,"skier");}
        if(kind=="tube"){
            int route=Mathf.Min(2,(int)(R(14)*3));var o=Tube(200+side*85,y+offset,side,Tubes[Mathf.Min(3,(int)(R(5)*4))]);
            o.phase=R(4);o.release=Mathf.Max(0,y+offset-750-R(15)*320)*.05f;o.vStart=(route==1?28:8)*.05f;
            o.vCruise=(62+R(16)*39)*.05f;o.tau=1.4f+R(17);o.pathSpan=((route==2?250:190)+R(18)*140)*.05f;
            o.from=(route==0?132:route==1?54:141)*.05f;o.to=(route==0?42:route==1?91:-17)*.05f;
        }else if(kind=="cat"){
            bool uphill=((seed+n/12)&1)!=0;
            Cat(200+side*96,y+offset,uphill?-60:60,true);
        }
        else if(kind=="gate"){Gate(open,y+offset,118,n);}
        else if(kind=="snowman"){int id=n%12==2?n/12:(int)(R(8)*3);Snowman(200+side*108,y+offset,"snow26"+"ABC"[(int)((uint)(id+seed)%3)]);}
        else{
            bool rescue=n%12==4;
            if(n==4||R(11)<.48f)Entry(n==4||R(12)<.4f?"helmet":"magnet",rescue?197:256,y+685,22,.85f);
        }
        if(n%12==3)Entry("mogul",200+side*23,y+520,32,.5f,"mound"+(n/12)%3);
        if(n>4&&n%5==1)Gate(open,y+1050,130,n%2);
        if(n%6==1)Entry("thermos",open+side*61,y+950,22,.85f);
    }
    void Forest(float y,int n,bool sprint){
        int id=Mathf.FloorToInt(y/1000);float R(int k)=>Roll(seed,id,k);int side=R(31)>.5f?1:-1;
        if(n==49)return;
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
        if(sprint)for(int i=0;i<6;i++)Entry("coin",200+(line-200)*Mathf.Sin((i+1)*Mathf.PI/8),y+78+i*94,22,0);
        if(n%3!=1&&R(38)<.23f)Gate(line,y+558,112,side>0?1:0);
    }
    float Valley(float y)=>200+20*MogulLine(y*.05f);
    void Rollers(float y,int n){
        if(n%2==0)TrailActor("mogulYeti",y+1040,n%4==0?1:-1);
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
        if(reward!=null){
            float prizeLane=Valley(y+1990);
            if(reward!="gift")prizeLane+=prizeLane>200?-65:65;
            Entry(reward,prizeLane,y+1990,24,.85f);
        }
    }
    internal static float KickerArc(float t,float lip,float lift)=>(1-t)*(lip+4*lift*t);
    float ParkLane(float y,int section){
        if(section<0)return 200;
        int tier=section%3,side=Roll(seed,Mathf.FloorToInt(y/1500),88)<.5f?1:-1;
        return 200+side*(tier==0?24:tier==1?46:55);
    }
    void Park(float y,int section,int shortTier=-1){
        bool fullPark=shortTier<0;
        int n=Mathf.FloorToInt(y/1500),tier=fullPark?section%3:shortTier;
        bool trick=fullPark&&section%6==0,prize=fullPark&&section%9==2;
        int line=prize?2:trick?1:tier;
        float x=fullPark?ParkLane(y,section):200+(n%2==1?1:-1)*(tier==0?24:tier==1?46:55);
        int side=x<200?-1:1;
        if(fullPark){
            if(section%4==1)Entry(section%8==1?"thermos":"helmet",x+side*79,y+117,24,.9f);
        }else if(tier>0){
            if(mode=="sprint")Entry("coin",x,y+90,22,0);
            Entry(tier==1?"thermos":"helmet",x+side*76,y+90,24,.85f);
        }
        bool groundLine=fullPark&&(section%9==3||tier==0&&!trick);
        if(groundLine){
            if(section%6==3)Box(x,y+610,59,180,.48f);
            Rescue(x,y+900);
            Entry("gift",x,y+1040,24,.85f);
            return;
        }
        float w=line==2?96:112,len=line==0?125:line==1?160:200,height=line==0?.95f:line==1?1.6f:2.35f;
        float span=prize?1100:line==0?395:line==1?520:680,lift=line==0?1.65f:line==1?2.6f:3.65f;
        float at=y+310,lip=at+len*.5f;
        var ramp=Kicker(x,at,w,len,height);ramp.park=true;ramp.span=span*.05f;ramp.lift=lift;ramp.trickLine=trick||prize;ramp.prizeLine=prize;
        void Target(string kind,float t){
            float h=KickerArc(t,height,lift)+.9f;
            var o=Entry(kind,x,lip+t*span,kind=="airRing"?62:22,h,kind=="airRing"?"ring"+tier:kind);
            o.air=true;o.needsTrick=(trick||prize)&&kind=="airRing";o.points=line==2?100:line==1?70:45;o.prizeLine=prize;
        }
        Target("coin",.16f);Target("coin",.31f);Target("airRing",prize?.52f:trick?.68f:.48f);
        Target("coin",.64f);Target("coin",.82f);
        if(prize)Target("feather",.92f);
        if(fullPark&&section%4==0&&lip+span+48<y+1460)Entry("gift",x,lip+span+48,24,.85f);
        if(fullPark&&section%3!=2){Person(200-side*133,y+640,48,section%3==1?"snowboarder":"skier",14,n);
            if(section%6==4)Entry("mogul",200+side*133,y+1080,24,.4f,"mound0");}
    }
    void Black(float y,int n,int stage){
        int side=(n%2==1?1:-1)*(Roll(seed,stage,98)<.5f?-1:1);
        for(int j=0;j<3;j++){
            float at=y+360+j*525;int turn=(j%2==1?-1:1)*side;float open=200-turn*53;
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
        if(p==5){Park(y+190,n,1);return;}
        if(n%4==2)Cat(200+side*83,y+1160,-48);
        else if(n%2==0)TrailActor("mogulYeti",y+1020,side);
        else Person(200+side*94,y+650,p==3?68:54,p==3||p==7?"snowboarder":"skier",22,n);
        if(n%2==1||p==0)Gate(open,y+570,112,n);
        string item=p==0?"thermos":p==2?"magnet":p==6?(n%16==14?"helmet":"gift"):null;
        if(item!=null){
            Entry(item,item=="gift"?open:open+side*65,y+1410,24,.85f);
        }
    }
    void Sprint(int n){
        float y=n*1500;if(n>16)return;if(n==16){Entry("finish",200,y,320,4.5f);return;}
        if(n==3||n==8||n==12)Forest(y,n,true);
        else if(n==5||n==10||n==14)Park(y,n,n==5?0:n==10?1:2);
        else if(n==2||n==9){Coins(200,y,5,100);Entry("thermos",265,y+400,24,.85f);}
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
