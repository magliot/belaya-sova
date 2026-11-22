using UnityEngine;

public partial class RaceWorld {
    public const int FreeRideLength=StageLength;
    public const int FreeRideSectionLength=150;
    public const int FreeRideGapCount=FreeRideLength/FreeRideSectionLength;
    public struct FreeRideGap {
        public const int ShapeSegments=8;
        public float lipD,endD,lane,halfWidth,bypassLane;
        public bool transverse;
        Vector2[] banks;
        internal void Shape(int seed,int section){
            banks=new Vector2[ShapeSegments+1];
            float slant=1.9f+1.75f*Roll(seed,section,1340);
            float bend=Mathf.Lerp(-1.1f,1.25f,Roll(seed,section,1341));
            float phase=Roll(seed,section,1342)*2*Mathf.PI,side=Mathf.Sign(lane);
            for(int i=0;i<=ShapeSegments;i++){
                float t=(float)i/ShapeSegments;
                if(transverse){
                    // The cut reaches into the rock on one side. The other bank
                    // leaves a skiable shelf all the way from approach to runout.
                    float open=Smooth(t/.26f)*Smooth((1-t)/.24f);
                    float shelfSide=Mathf.Sign(bypassLane);
                    float shelf=shelfSide*(2*Mathf.Abs(bypassLane)-12+.20f*Mathf.Sin(phase+8*t));
                    float rock=-shelfSide*(13.1f+.6f*Mathf.Sin(phase+5*t));
                    banks[i]=Vector2.Lerp(new Vector2(lane,lane),new Vector2(Mathf.Min(shelf,rock),Mathf.Max(shelf,rock)),open);
                    continue;
                }
                float bulge=i==0||i==ShapeSegments?0:Mathf.Sin(Mathf.PI*t);
                float width=halfWidth*Mathf.Pow(bulge,.64f)*(.85f+.28f*Roll(seed,section,1350+i));
                float shift=slant*t+bulge*(bend+.53f*Mathf.Sin(2*Mathf.PI*t+phase));
                float centre=side*Mathf.Max(width+1.05f,Mathf.Abs(lane)+shift);
                float left=centre-width,right=centre+width;
                if(section>=2&&i>0&&i<ShapeSegments){
                    // The marked takeoff line must lead over open ice, including at the midpoint that earns the landing award.
                    float margin=.70f*bulge;
                    left=Mathf.Min(left,lane-margin);right=Mathf.Max(right,lane+margin);
                }
                banks[i]=new Vector2(left,right);
            }
        }
        public float SectionDistance(int index)=>Mathf.Lerp(lipD,endD,(float)index/ShapeSegments);
        public Vector2 CrossSection(float d){
            float u=Mathf.InverseLerp(lipD,endD,d)*ShapeSegments;
            int i=Mathf.Min(Mathf.FloorToInt(u),ShapeSegments-1);
            return Vector2.LerpUnclamped(banks[i],banks[i+1],u-i);
        }
        public float LeftAt(float d)=>CrossSection(d).x;
        public float RightAt(float d)=>CrossSection(d).y;
        public float CentreAt(float d){var b=CrossSection(d);return (b.x+b.y)*.5f;}
        public float HalfWidthAt(float d){var b=CrossSection(d);return (b.y-b.x)*.5f;}
        public bool OverOpening(float d,float x){
            if(d<=lipD||d>=endD)return false;
            var b=CrossSection(d);return x>b.x&&x<b.y;
        }
        // The snow at the pointed uphill end forms a takeoff. Return the actual
        // lip at this lateral position, including the oblique leading edges.
        public bool TryFrontLip(float x,out float metre){
            for(int i=0;i<3;i++){
                Vector2 a=banks[i],b=banks[i+1];
                if(x>=a.x&&x<=a.y){metre=SectionDistance(i);return true;}
                if(x<b.x||x>b.y)continue;
                float t=x<a.x?(x-a.x)/(b.x-a.x):(x-a.y)/(b.y-a.y);
                metre=Mathf.Lerp(SectionDistance(i),SectionDistance(i+1),t);return true;
            }
            metre=lipD;return false;
        }
    }
    readonly FreeRideGap[] freeRideGaps=new FreeRideGap[FreeRideGapCount];
    int freeRideSeed,freeRideVisit;
    bool gapsReady;

    // Both mesh cutting and riding contacts read the same seeded ice fracture.
    // Include the approaches in the lookup so the snow can rise into a natural lip.
    public bool TryFreeRideGap(float d,out FreeRideGap gap){
        gap=default;
        if(!AlpineAt(d))return false;
        int visit=Mathf.FloorToInt(d/LapLength);
        int origin=visit*LapLength+AlpineStage*StageLength;
        int section=Mathf.FloorToInt((d-origin)/FreeRideSectionLength);
        if(section>=freeRideGaps.Length)return false; // Final runout into the next lap.
        int layout=unchecked(seed+visit*4651);
        if(!gapsReady||freeRideSeed!=seed||freeRideVisit!=visit){
            for(int i=0;i<freeRideGaps.Length;i++){
                float side=Roll(layout,i,1300)<.5f?-1:1;
                float lip=origin+i*FreeRideSectionLength+(i==0?85:75)+(2*Roll(layout,i,1301)-1)*(i==0?3:16);
                bool transverse=i%2==1;
                if(transverse){
                    float firstSide=Roll(layout,1,1300)<.5f?-1:1;
                    side=i%4==1?firstSide:-firstSide;
                }
                float length=(transverse&&i>1?23.5f:18)+4*Roll(layout,i,1302);
                freeRideGaps[i]=new FreeRideGap{lipD=lip,endD=lip+length,transverse=transverse,
                    lane=side*((transverse?1:4)+2.4f*Roll(layout,i,1303)),halfWidth=2.55f+.75f*Roll(layout,i,1304),
                    bypassLane=-side*(transverse?(i>1?10.40f:10.2f)+.24f*Roll(layout,i,1345):3.2f)};
                freeRideGaps[i].Shape(layout,i);
            }
            gapsReady=true;freeRideSeed=seed;freeRideVisit=visit;
        }
        gap=freeRideGaps[section];return true;
    }
    public float FreeRidePace(float d){
        float grade=(SampleGround(d-3,0).y-SampleGround(d+3,0).y)/6;
        return 19+6*Mathf.Clamp01(grade);
    }

    internal const float FreeRideGravity=9.81f;
    internal float FreeRideLaunchVelocity(FreeRideGap gap,float originD,float x,float pace){
        // Takeoff follows the lip's fixed launch angle. A slower rider covers less
        // ground. At cruise speed the landing zone begins beyond the far wall.
        float span=gap.endD-originD+16;
        float cruise=FreeRidePace(originD);
        float drop=SampleGround(originD+span,x).y-SampleGround(originD,x).y;
        return pace*(drop/span+.5f*FreeRideGravity*span/(cruise*cruise));
    }
    internal static float FreeRideFlightY(float originY,float vertical,float seconds){
        return originY+vertical*seconds-.5f*FreeRideGravity*seconds*seconds;
    }
    internal static float FreeRideHeight(float clearance,float trickBlend){
        if(clearance<=0)return clearance;
        float glide=.82f*(1-Mathf.Exp(-clearance/.90f));
        float leap=2.6f*(1-Mathf.Exp(-clearance/2.6f));
        return Mathf.Lerp(glide,leap,trickBlend);
    }
    void BuildFreeRideSection(int section,float start){
        if(section%2==0||section%6==3)SpawnSnowBoulder(start,section);
        if(!TryFreeRideGap(start,out var gap))return;
        float side=Mathf.Sign(gap.lane);
        // Keep rock and moving traffic off the narrow snow shelf.
        void Boulder(float d,float x,float size,bool slab){
            var o=Add("rock",d,x,(slab?1.54f:.84f)*size,(slab?.87f:1.52f)*size,slab?"rockWide":"rockTall");
            o.scale=size;o.yaw=Roll(seed,section,1320+Mathf.RoundToInt(d))*150;
            o.offset=-.2f*size;
        }
        Boulder(start+23+6*Roll(seed,section,1310),side*(8.4f+Roll(seed,section,1311)),1.75f+.7f*Roll(seed,section,1312),false);
        if(section%3!=1)Boulder(start+34,side*10.1f,1.7f,true);
        // Offset outcrops tighten alternate approaches, well above the lip and the single exit shelf.
        if(!gap.transverse&&section%3!=0){
            float spur=-(3.0f+1.1f*Roll(seed,section,1381))*side;
            Boulder(start+19+4*Roll(seed,section,1380),spur,1.48f,false);
            if(section%3==2)Boulder(start+37,side*(4.7f+Roll(seed,section,1382)),1.50f,true);
        }
        if(gap.transverse){
            float shelfSide=Mathf.Sign(gap.bypassLane);
            Boulder(gap.lipD-10,shelfSide*14.1f,1.85f,false);
            Boulder(gap.SectionDistance(4)+2,shelfSide*15.2f,2.35f,true);
            Boulder(gap.endD+4,shelfSide*14.3f,1.55f,false);
        }
        Boulder(gap.endD+27+6*Roll(seed,section,1313),-side*(4+2.8f*Roll(seed,section,1314)),1.85f,true);
        if(section%4!=3){
            bool board=section%4==1;
            float x=(board?side:-side)*(5.8f+Roll(seed,section,1361));
            float d=start+31+12*Roll(seed,section,1362);
            Person(200+20*x,20*d,155+34*Roll(seed,section,1363),board?"snowboarder":"skier",
                24+8*Roll(seed,section,1364),6*Roll(seed,section,1365));
        }

        if(section>=2){
            // A staggered entry and a clear takeoff. The next obstruction is beside the landing,
            // leaving both the marked flight and the narrow snow shelf open.
            Boulder(start+10,side*Mathf.Lerp(-.7f,1.4f,Roll(seed,section,1412)),1.45f,true);
            Boulder(start+48,-side*(3.5f+1.4f*Roll(seed,section,1413)),1.65f,false);
            if(section%3!=0)Snowman(200+20*(gap.lane-side*3.65f),(gap.endD+17)*20);
        }

        int approach=2+Mathf.Min(2,(int)(3*Roll(seed,section,1315)));
        for(int i=0;i<approach;i++)Add("coin",start+15+i*3.5f,side*(.45f+i*.30f),1.1f,0);
        for(int i=0;i<3;i++)Add("coin",gap.lipD-16+i*5,gap.lane-side*(2-i)*.40f,1.1f,0);
        float cruise=FreeRidePace(gap.lipD);
        float originY=SampleGround(gap.lipD,gap.lane).y;
        float vertical=FreeRideLaunchVelocity(gap,gap.lipD,gap.lane,cruise);
        for(int i=0;i<3;i++){
            float d=Mathf.Lerp(gap.lipD,gap.endD,.20f+i*.31f);
            float y=FreeRideFlightY(originY,vertical,(d-gap.lipD)/cruise)-SampleGround(d,gap.lane).y;
            // A centred rider can reach this fruit with an early or late tap.
            Add("coin",d,gap.lane,1.1f,FreeRideHeight(y,.5f)+.9f).air=true;
        }
        int count=2+(section%2);
        if(gap.transverse){
            for(int i=0;i<4;i++){
                float d=gap.lipD-28+i*10;
                Add("coin",d,gap.bypassLane*Mathf.Lerp(.66f,1,Mathf.Clamp01(i*.5f)),1.1f,0);
            }
            Add("coin",gap.endD-4,gap.bypassLane,1.1f,0);
        }else{
            float lane=-side*(2.7f+Roll(seed,section,1316)*1.9f);
            for(int i=0;i<count;i++)Add("coin",gap.lipD+1+i*4.2f,lane-side*i*.15f,1.1f,0);
        }
        for(int i=0;i<2;i++)Add("coin",gap.endD+15+i*4,gap.lane-side*i*.5f,1.1f,0);
    }
}
