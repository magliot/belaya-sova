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
    const int MogulRunLength=StageLength/117*117;
    public float MogulLine(float d){
        float local=Mathf.Repeat(d,StageLength);
        float phase=Roll(seed,Mathf.FloorToInt(d/StageLength),612)*6.283185f;
        return 3.55f*Mathf.Sin(local/38+phase)+1.48f*Mathf.Sin(local/68-phase*.7f);
    }
    public float MogulRoughness(float d,float lane){
        float local=Mathf.Repeat(d,StageLength);
        float rough=Smooth((Mathf.Abs(lane-MogulLine(d))-1.52f)/2.25f);
        return rough*Smooth(local/16)*Smooth((MogulRunLength-local)/24);
    }
    void EndlessRoad(float d,ref float x,ref float slope,ref float second){
        const float spacing=154,transition=195;
        float distance=d-LapLength;
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
    void EndlessScene(int fullStage,int n){
        int stage=fullStage%StageCount,rowsPerScene=StageLength/(5*50);
        int section=n/rowsPerScene,row=n%rowsPerScene;
        if(row>=8)row=row>=rowsPerScene-2?8:2+(row-8)%5;
        int scenario=RouteScenario(seed,stage,fullStage/StageCount,section);
        int key=fullStage*(StageLength/50)+n;
        float direction=Roll(seed,fullStage*5+section,644)<.5f?-1:1;
        float start=fullStage*StageLength+n*50;
        float At(float offset)=>(start+offset)*20;
        float centre=200+direction*Mathf.Lerp(-50,54,Roll(seed,key,651));
        float outside=200-direction*Mathf.Lerp(65,98,Roll(seed,key,652));
        if(row==8||row==9)return; // Clear approach to the next scene.
        if(row==0)return;
        if(row==1){
            if(stage==1){
                foreach(int flank in sides){
                    var oak=Tree(200+flank*126,At(25),1,1.1f);
                    oak.variant="canopyPine";oak.lean=-9;oak.yaw=flank<0?0:180;
                }
            }else Gate(centre,At(12),150,scenario);
            return;
        }
        if(row==2)Entry(section%2==0?"thermos":"helmet",outside,At(23),24,.85f);
        if(row==4||row==6)Entry(row==4?"gift":"magnet",outside,At(13),24,.85f);
        if(row==2||row==5)Gate(centre,At(7),122,row);
        if(row==7)return;
        float at=At(29);
        if(stage==1){
            switch(scenario){
                case 0:
                    Tree(outside,at,row%3,1.08f);
                    if(row%2==0)Tree(outside+direction*21,At(34),1,.9f);
                    break;
                case 1:
                    if(row%2==0){
                        Person(centre,at,44,"kid",10,row);
                        Person(centre+direction*28,At(35),45,"instructor",10,row);
                    }else Tree(outside,at,1,1.16f);
                    break;
                case 2:
                    if(row%2==1)foreach(int flank in sides){
                        var tree=Tree(centre+flank*71,at+flank*15,1,1.01f);
                        tree.variant="canopyFir";tree.lean=-12;tree.yaw=flank<0?0:180;
                    }else Tree(outside,at,1,1.05f);
                    break;
                case 3:
                    if(row==3||row==6)TrailActor("snowmobile",at,(int)direction);
                    else{Tree(outside,at,2,.94f);Tree(200+direction*125,At(33),0,1.05f);}
                    break;
                default:
                    if(row==3||row==6)Kicker(centre,at,53,76,.82f);
                    else if(row%2==0)Tree(outside,at,1,1.05f);
                    else Person(200+direction*110,at,47,"snowboarder",12,row);
                    break;
            }
        }else{
            switch(scenario){
                case 0:if(row!=2&&row!=5)Gate(centre,at,102,row);break;
                case 1:if(row==3||row==6)Cat(centre,at,row==3?-48:62);break;
                case 2:if(row==3||row==6)TrailActor("mogulYeti",at,(int)direction);break;
                case 3:
                    if(row==3||row==6){
                        var jump=Kicker(centre,at,68,98,.88f);
                        jump.park=true;jump.span=18;jump.lift=1.65f;
                        var ring=Entry("airRing",centre,at+49+180,62,.5f*jump.height+jump.lift+.9f,"ring0");
                        ring.air=true;ring.points=45;
                    }
                    break;
                default:if(row%2==0)Person(centre,at,58,row==4?"skier":"snowboarder",14,row);break;
            }
        }
    }
}
