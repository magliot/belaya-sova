using UnityEngine;

public partial class RaceWorld {
    // Each visit uses all five scenarios. The next visit starts one or two slots
    // ahead, clear of the previous ending (slots 3 and 4). It can be sought directly.
    public static int RouteScenario(int seed,int stage,int visit,int section){
        int index=visit-1;
        int Rotation(int i)=>i+i/2+((i%2==1&&Roll(seed,i/2,630+stage)>.5f)?1:0);
        int rotation=Rotation(index),step=index==0?1:rotation-Rotation(index-1);
        // With a one-slot step the second section may move one place without
        // touching the previous last slot. The final two slots stay reserved.
        if(step==1&&Roll(seed,index,660+stage)>.5f&&(section==1||section==2))section=3-section;
        int slot=rotation+section;
        int first=Mathf.Min(4,(int)(Roll(seed,stage,642)*5));
        int stride=1+Mathf.Min(3,(int)(Roll(seed,stage,643)*4));
        return (first+(slot%5)*stride)%5;
    }
    public float MogulLine(float d){
        float local=Mathf.Repeat(d,2500);
        float phase=Roll(seed,Mathf.FloorToInt(d/2500),612)*6.283185f;
        return 3.55f*Mathf.Sin(local/38+phase)+1.48f*Mathf.Sin(local/68-phase*.7f);
    }
    public float MogulRoughness(float d,float lane){
        float local=Mathf.Repeat(d,2500);
        float rough=Smooth((Mathf.Abs(lane-MogulLine(d))-1.52f)/2.25f);
        int row=Mathf.FloorToInt(local/117);
        if(ChoiceRow(2,row)&&(row+1)*117<=2500){
            float spread=ChoiceSpread((local-row*117)/117);
            float split=Smooth((Mathf.Abs(Mathf.Abs(lane)-spread)-1.4f)/2.25f);
            rough=Mathf.Lerp(rough,split,Smooth(spread/1.2f));
        }
        return rough*Smooth(local/16)*Smooth((2457-local)/24);
    }
    void EndlessRoad(float d,ref float x,ref float slope,ref float second){
        const float spacing=154,transition=195;
        float distance=d-20000;
        int knot=Mathf.FloorToInt(distance/spacing);
        float t=distance/spacing-knot;
        float a=Roll(seed,knot,655)*65-32.5f,b=Roll(seed,knot+1,655)*65-32.5f,delta=b-a;
        float side=a+delta*t*t*t*(10+t*(-15+6*t));
        float ds=delta*30*t*t*(1-t)*(1-t)/spacing;
        float dds=delta*60*t*(t-1)*(2*t-1)/(spacing*spacing);
        // The quintic fade matches position, tangent and curvature at the seam.
        float u=Mathf.Clamp01(distance/transition);
        float f=u*u*u*(10+u*(-15+6*u)),df=30*u*u*(1-u)*(1-u)/transition;
        float ddf=60*u*(u-1)*(2*u-1)/(transition*transition);
        second+=f*dds+2*df*ds+ddf*side;slope+=f*ds+df*side;x+=f*side;
    }
    static float ForkLane(float at,float side){
        float fork=Smooth((at-42)/90)*Smooth((425-at)/90);
        return side*fork*(3.75f+.52f*Mathf.Sin(at/63));
    }
    float ForkTrack(float d){
        if(mode!="normal"||d<0)return 0;
        int index=Mathf.FloorToInt(d/2500),stage=index%8;
        if(index>=8&&(stage==1||stage==6))return ForkLane(Mathf.Repeat(d,500),1);
        if(stage==3)return 0;
        float length=RowLength(index,mode),local=d-index*2500;
        int row=Mathf.FloorToInt(local/length);
        return ChoiceRow(index,row)&&(row+1)*length<=2500?ChoiceSpread((local-row*length)/length):0;
    }
    void EndlessFork(int fullStage,int n){
        int stage=fullStage%8,section=n/10,row=n%10;
        int scenario=RouteScenario(seed,stage,fullStage/8,section);
        float side=Roll(seed,fullStage*5+section,644)<.5f?-1:1;
        float start=(fullStage*2500+section*500)*20,local=row*50;
        float At(float offset)=>start+(local+offset)*20;
        float X(float offset,float edge)=>200+20*ForkLane(local+offset,edge);
        // The split is visible well before either commitment. Both choices have
        // a free line, a distinct reward, and 75 m to rejoin before the next scene.
        for(int i=0;i<3;i++){
            float offset=8+i*15;
            var fruit=Entry("coin",X(offset,side),At(offset),22,0);
            if(row>=2&&row<=6)fruit.line=CourseLine.Precision;
            if(row>=2&&row<=6&&i==2)Entry("coin",X(offset,-side),At(offset),22,0).line=CourseLine.Wide;
            if(row==2&&i==1)Entry(section%2==0?"thermos":"helmet",X(offset,-side),At(offset),24,.85f);
        }
        if(row==8)return;
        if(row>=7)return;
        if(row<2){
            if(row==1){
                if(stage==1){
                    foreach(int flank in sides){var oak=Tree(200+flank*126,At(25),1,1.1f);oak.variant="canopyPine";oak.lean=-9;oak.yaw=flank<0?0:180;}
                }else Gate(200,At(12),160,scenario);
            }
            return;
        }
        float fast=X(24,side),quiet=X(24,-side),y=At(24);
        void ForkGate(float x,float at,float gap,int color){
            var gate=Gate(x,at,gap,color);bool precise=(x-200)*side>0;
            gate.line=precise?CourseLine.Precision:CourseLine.Wide;gate.points=precise?45:18;
        }
        if(row==4||row==6)Entry(row==4?"gift":"magnet",X(15,row==4?side:-side),At(15),24,.85f);
        if(row==2||row==5)ForkGate(X(7,side),At(7),91,row);
        if(row==2||row==5)ForkGate(X(7,-side),At(7),119,row+1);
        if(stage==1){
            // Timber chicane / ski school / canopy slalom / sled clearing / jump trail.
            switch(scenario){
                case 0:
                    Tree(200+side*(row%2==0?10:-12),y,row%3,1.08f);
                    if(row%2==0)ForkGate(fast,At(38),89,row);break;
                case 1:
                    if(row%2==0){Person(fast,y,44,"kid",10,row);Person(fast+side*28,At(29),45,"instructor",10,row);}
                    else Tree(200,y,1,1.16f);break;
                case 2:
                    if(row%2==1){foreach(int flank in sides){var tree=Tree(fast+flank*71,y+flank*15,1,1.01f);tree.variant="canopyFir";tree.lean=-12;tree.yaw=flank<0?0:180;}}
                    else ForkGate(quiet,y,92,row);break;
                case 3:
                    if(row==3||row==6)TrailActor("snowmobile",y,(int)side);
                    else{Tree(200,y,2,.94f);Tree(200+side*125,At(30),0,1.05f);}break;
                default:
                    if(row==3||row==6)Kicker(fast,y,53,76,.82f);
                    else if(row%2==0)Tree(200,y,1,1.05f);
                    else Person(200+side*110,y,47,"snowboarder",12,row);break;
            }
        }else{
            // Lamp slalom / working snowcat / Yeti crossing / kicker line / night club.
            switch(scenario){
                case 0:ForkGate(fast+(row%2==0?15:-15),y,89,row);
                    if(row%2==1)ForkGate(quiet,At(37),122,row+1);break;
                case 1:if(row==3||row==6)Cat(fast,y,row==3?-48:62);else ForkGate(quiet,y,110,row);break;
                case 2:if(row==3||row==6)TrailActor("mogulYeti",y,(int)side);else ForkGate(quiet,y,107,row);break;
                case 3:
                    if(row==3||row==6){var jump=Kicker(fast,y,68,98,.88f);jump.park=true;jump.span=18;jump.lift=1.65f;
                        var ring=Entry("airRing",fast,y+49+180,62,.5f*jump.height+jump.lift+.9f,"ring0");ring.air=true;ring.points=45;}
                    else ForkGate(quiet,y,116,row);break;
                default:if(row%2==0)Person(fast,y,58,row==4?"skier":"snowboarder",14,row);else ForkGate(fast,y,100,row);break;
            }
        }
    }
}
