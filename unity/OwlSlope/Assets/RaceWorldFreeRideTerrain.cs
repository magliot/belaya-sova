using System.Collections.Generic;
using UnityEngine;

public partial class RaceWorld {
    // The central 24 metres are rideable. The same mesh continues up both walls of the cirque.
    static readonly float[] alpineLanes = {
        -112,-98,-87,-78,-70,-62,-55,-49,-44,-40,-36,-32,-29,-26,-23,-20,-18,-16,-14,-12,-10,-8,-6,-4,-2,0,
        2,4,6,8,10,12,14,16,18,20,23,26,29,32,36,40,44,49,55,62,70,78,87,98,112
    };
    const int AlpineRows=151;
    const int FractureBand=16;
    GameObject alpineRun;
    Mesh alpineMesh;
    readonly List<Vector3> alpineVertices=new List<Vector3>(6200);
    readonly List<Vector2> alpineUV=new List<Vector2>(6200);
    readonly List<Color32> alpineColors=new List<Color32>(8600);
    readonly List<int> powderFaces=new List<int>(23500),slateFaces=new List<int>(9500),iceFaces=new List<int>(1800);
    float alpinePhase,alpinePhaseB,alpineHeight,alpineAcross,alpineBank,pillowA,pillowB;
    float valleyOpening;
    bool alpineNearLip;
    FreeRideGap alpineGap;

    struct Ridge {
        public float shoulder,spur,peak,crest;
    }
    Ridge alpineLeft,alpineRight;
    int alpineStart=-1;
    float entranceX,exitDrift;
    static float PisteCentre(float d)=>36*(1-Mathf.Cos(d/52))+16*(Mathf.Cos(d/97)-1);
    float BoundaryCentre(float d){
        float x=PisteCentre(d),slope=0,second=0;
        if(d>=LapLength)EndlessRoad(d,ref x,ref slope,ref second);
        return x;
    }
    float CirqueCentre(float t)=>18*(Mathf.Sin(t/76+alpinePhase)-Mathf.Sin(alpinePhase))
        +14*(Mathf.Cos(t/121+alpinePhaseB)-Mathf.Cos(alpinePhaseB));
    void BlendAlpineCentre(float d,ref float x,ref float slope,ref float second){
        int start=Mathf.FloorToInt(d/StageLength)*StageLength;
        if(start!=alpineStart){
            alpineStart=start;entranceX=BoundaryCentre(start);
            exitDrift=(BoundaryCentre(start+StageLength)-entranceX-CirqueCentre(StageLength))/StageLength;
        }
        float t=d-start, target=entranceX+CirqueCentre(t)+t*exitDrift;
        float a=t/76+alpinePhase,b=t/121+alpinePhaseB;
        float ds=18f/76*Mathf.Cos(a)-14f/121*Mathf.Sin(b)+exitDrift;
        float dd=-18f/(76*76)*Mathf.Sin(a)-14f/(121*121)*Mathf.Cos(b);
        const float fade=140;
        float u=Mathf.Clamp01(Mathf.Min(t,StageLength-t)/fade);
        float blend=u*u*u*(10+u*(-15+6*u));
        float dw=30*u*u*(1-u)*(1-u)/fade*(t<StageLength/2?1:-1);
        float d2w=60*u*(1-u)*(1-2*u)/(fade*fade);
        second=Mathf.Lerp(second,dd,blend)+2*dw*(ds-slope)+d2w*(target-x);
        slope=Mathf.Lerp(slope,ds,blend)+dw*(target-x);x=Mathf.Lerp(x,target,blend);
    }

    void PrepareAlpineProfile(){
        alpineStart=-1;
        alpinePhase=Roll(seed,0,891)*Mathf.PI*2;
        alpinePhaseB=Roll(seed,0,892)*Mathf.PI*2;
    }
    static Ridge SampleRidge(float d,float phase){
        float spur=Mathf.Sin(d*.028f+phase),summit=Mathf.Abs(Mathf.Sin(d*.022f+phase*1.5f));
        return new Ridge{
            shoulder=1.0f+.22f*Mathf.Cos(d*.016f+phase),
            spur=10+12*spur+4*Mathf.Cos(d*.069f+phase),
            peak=59+22*(1-summit)+8*Mathf.Sin(d*.057f+phase),
            crest=1-Mathf.Abs(Mathf.Sin(d*.045f+phase))};
    }
    static float AlpineBlend(float d){
        float local=Mathf.Repeat(d,StageLength);
        return Smooth(local/55)*Smooth((StageLength-local)/55);
    }
    static float PisteHeight(float d)=>-.29f*d+9.24f*(Mathf.Cos(d/42)-1)+1.53f*(Mathf.Cos(d/17+1)-Mathf.Cos(1));
    float CourseHeight(float d){
        float normal=PisteHeight(d);
        if(mode!="normal"||d<AlpineStage*StageLength)return normal;
        float laps=Mathf.Floor(d/LapLength),local=Mathf.Max(0,Mathf.Repeat(d,LapLength)-AlpineStage*StageLength);
        // Integrate the additional grade with level tangents at each stage boundary.
        const float fade=45;
        float Integrated(float t)=>t<=0?0:t<fade?t*t/(2*fade):t-fade/2;
        float drop=Integrated(local)-Integrated(local-(StageLength-fade));
        float priorDrop=.48f*laps*(StageLength-fade);
        float joined=normal-priorDrop-.48f*drop;
        if(local<=0)return joined;
        float alpine=PisteHeight(d-local)-priorDrop-.77f*local
            -18.02f*(Mathf.Sin(local/106+alpinePhase)-Mathf.Sin(alpinePhase))
            +8.16f*(Mathf.Cos(local/51+alpinePhaseB)-Mathf.Cos(alpinePhaseB));
        return Mathf.Lerp(joined,alpine,Smooth(local/68)*Smooth((StageLength-local)/130));
    }
    void SampleAlpineProfile(float worldD){
        float d=Mathf.Repeat(worldD,StageLength);
        alpineAcross=1/Mathf.Sqrt(1+dx*dx);
        alpineHeight=CourseHeight(worldD);
        float tail=Mathf.Clamp01((d-(StageLength-110))/85);
        valleyOpening=Smooth(tail);

        alpineBank=Mathf.Sin(d*.026f+alpinePhaseB)*.045f;
        pillowA=d*.10f+alpinePhase;pillowB=d*.052f+alpinePhaseB;
        alpineLeft=SampleRidge(d,alpinePhase+.7f);alpineRight=SampleRidge(d,alpinePhaseB+3.3f);
        alpineNearLip=TryFreeRideGap(worldD,out alpineGap)&&worldD>=alpineGap.lipD-7&&worldD<alpineGap.lipD+8;
    }
    Vector3 AlpineGround(float d,float lane){
        float side=Mathf.Abs(lane)*(1-.26f*valleyOpening);
        Ridge ridge=lane<0?alpineLeft:alpineRight;
        float dunes=.22f*Mathf.Cos(lane*.26f+pillowA)+.32f*Mathf.Sin(lane*.17f-pillowB);
        float y=alpineHeight+dunes+alpineBank*Mathf.Clamp(lane,-15,15);
        y+=ridge.shoulder*side*side*.009f*(1-Smooth((side-19)/15));
        y+=ridge.spur*Smooth((side-12)/21)*(1-Smooth((side-34)/20));
        y+=ridge.peak*Smooth((side-36)/51)*(1-.49f*Smooth((side-87)/25));
        y+=ridge.crest*7*Smooth((side-47)/20);
        if(side>15){
            float flutes=1-Mathf.Abs(Mathf.Sin(lane*.18f+d*.068f+alpinePhase+.57f*Mathf.Sin(d*.082f)));
            float rocks=flutes*(2.2f+.9f*Mathf.Cos(lane*.31f-d*.114f))+.8f*Mathf.Sin(lane*.32f+d*.16f);
            y+=rocks*Smooth((side-15)/12);
        }
        if(alpineNearLip){
            float lip=alpineGap.lipD;
            float cross=Smooth((alpineGap.halfWidth+.65f-Mathf.Abs(lane-alpineGap.lane))/1.25f);
            y+=.72f*cross*Smooth((d-lip+7)/7)*(1-Smooth((d-lip)/8));
        }
        float bank=.12f*(float)System.Math.Tanh(-curve*10/.12f);
        float shoulder=Smooth((Mathf.Abs(lane)-9.4f)/5.8f)*(3.4f+.65f*Mathf.Sin(d/23+(lane<0?1.4f:0))+.25f*Mathf.Cos(d/9));
        y=Mathf.Lerp(alpineHeight+bank*Mathf.Clamp(lane,-14,14)+shoulder,y,AlpineBlend(d));
        // The distant walls run down the valley. Extrapolating a ski's road
        // normal a hundred metres into the rock folds the outer mesh on bends.
        return new Vector3(cx+lane*alpineAcross,y,d-Mathf.Clamp(lane,-16,16)*dx*alpineAcross);
    }

    void PrepareAlpineMesh(){
        alpineRun=new GameObject("Free Ride · powder cirque");
        alpineRun.transform.SetParent(transform,false);
        alpineMesh=new Mesh{name="Alpine snow, exposed slate and crevasses"};
        alpineMesh.MarkDynamic();alpineMesh.subMeshCount=3;
        alpineRun.AddComponent<MeshFilter>().sharedMesh=alpineMesh;
        var powder=SnowPaint("Powder",new Color(.94f,.965f,1));
        var exposed=new Material(Resources.Load<Shader>("Race/WorldObject"));
        exposed.SetTexture("_BaseMap",Resources.Load<Texture2D>("Art/Powder"));
        exposed.SetFloat("_Lit",.83f);
        alpineRun.AddComponent<MeshRenderer>().sharedMaterials=new[]{powder,exposed,exposed};
    }
    static void AlpineFace(List<int> indices,int a,int b,int c,int d){
        indices.Add(a);indices.Add(b);indices.Add(c);
        indices.Add(c);indices.Add(b);indices.Add(d);
    }
    static Vector2 AlpineMap(float d,float x)=>new Vector2(x/7,d/12);

    void UpdateAlpineTerrain(float centre){
        if(!alpineRun)PrepareAlpineMesh();
        if(!alpineRun.activeSelf)alpineRun.SetActive(true);
        alpineVertices.Clear();alpineUV.Clear();alpineColors.Clear();
        powderFaces.Clear();slateFaces.Clear();iceFaces.Clear();
        int cols=alpineLanes.Length;
        if(snowVertices==null||snowVertices.Length!=AlpineRows*cols)snowVertices=new Vector3[AlpineRows*cols];
        firstSnowRow=centre-50;
        for(int row=0;row<AlpineRows;row++){
            float d=firstSnowRow+row*2;
            SampleRoad(d);
            for(int j=0;j<cols;j++){
                float x=alpineLanes[j];
                Vector3 p=SampleGround(d,x);snowVertices[row*cols+j]=p;
                alpineVertices.Add(p);alpineUV.Add(AlpineMap(d,x));
            }
            for(int j=0;j<cols;j++){
                int a=row*cols+Mathf.Max(0,j-1),b=row*cols+Mathf.Min(cols-1,j+1);
                float steepness=Mathf.Abs(snowVertices[a].y-snowVertices[b].y)/(alpineLanes[b%cols]-alpineLanes[a%cols]);
                alpineColors.Add(MountainTint(d,alpineLanes[j],steepness));
            }
        }
        for(int row=0;row<AlpineRows-1;row++){
            float d=firstSnowRow+row*2;
            bool cut=TryFreeRideGap(d+1,out var gap)&&gap.lipD<d+2&&gap.endD>d;
            for(int j=0;j<cols-1;j++){
                int a=row*cols+j,b=a+cols;
                float x=alpineLanes[j],endX=alpineLanes[j+1];
                if(cut&&x>=-FractureBand&&endX<=FractureBand)continue;
                float offCentre=Mathf.Abs((x+endX)*.5f);
                AlpineFace(offCentre>=FractureBand?slateFaces:powderFaces,a,b,a+1,b+1);
            }
            if(cut)CutAlpineRow(d,gap);
        }
        alpineMesh.Clear(false);alpineMesh.subMeshCount=3;
        alpineMesh.SetVertices(alpineVertices);alpineMesh.SetUVs(0,alpineUV);alpineMesh.SetColors(alpineColors);
        alpineMesh.SetTriangles(powderFaces,0,false);alpineMesh.SetTriangles(slateFaces,1,false);
        alpineMesh.SetTriangles(iceFaces,2,false);alpineMesh.RecalculateNormals();alpineMesh.RecalculateBounds();
    }

    static Color32 MountainTint(float d,float x,float steepness){
        Color snow=new Color(.94f,.96f,.99f),shale=new Color(.43f,.50f,.59f);
        float drift=Mathf.Sin(x*.19f+d*.061f+1.35f*Mathf.Sin(d*.08f+x*.09f));
        float outcrop=.53f+.38f*drift+.33f*Mathf.Sin(d*.116f-x*.21f);
        float bare=Smooth((steepness-.33f)/.8f)*Smooth(outcrop)*Smooth((Mathf.Abs(x)-16)/8);
        return Color.Lerp(snow,shale,bare);
    }

    // Cut the snow at the same piecewise-linear cross sections used for contacts,
    // keeping the regular support grid available as a flight reference above the void.
    void CutAlpineRow(float row,FreeRideGap gap){
        float start=Mathf.Max(row,gap.lipD),end=Mathf.Min(row+2,gap.endD);
        WholeAlpineStrip(row,start);
        for(int i=0;i<FreeRideGap.ShapeSegments;i++){
            float d0=Mathf.Max(start,gap.SectionDistance(i)),d1=Mathf.Min(end,gap.SectionDistance(i+1));
            if(d1-d0<.0001f)continue;
            var limits0=gap.CrossSection(d0);var limits1=gap.CrossSection(d1);
            for(int x=-FractureBand;x<FractureBand;x+=2){
                ClipAlpineShelf(d0,d1,x,limits0.x,limits1.x,true);
                ClipAlpineShelf(d0,d1,x,limits0.y,limits1.y,false);
            }
            int begin=alpineVertices.Count;
            CrevasseProfile(d0,limits0,i);CrevasseProfile(d1,limits1,i);
            for(int j=0;j<8;j++)AlpineFace(j==3||j==4?slateFaces:iceFaces,begin+j,begin+9+j,begin+j+1,begin+10+j);
        }
        WholeAlpineStrip(end,row+2);
    }
    int AlpineVertex(float d,float x,float depth,Color32 tint){
        int index=alpineVertices.Count;
        alpineVertices.Add(SnowSurface(d,x)-depth*Vector3.up);
        alpineUV.Add(AlpineMap(d+depth,x));alpineColors.Add(tint);
        return index;
    }
    void WholeAlpineStrip(float d0,float d1){
        if(d1<=d0)return;
        for(int x=-FractureBand;x<FractureBand;x+=2){
            int a=AlpineVertex(d0,x,0,Color.white),b=AlpineVertex(d1,x,0,Color.white);
            int c=AlpineVertex(d0,x+2,0,Color.white),e=AlpineVertex(d1,x+2,0,Color.white);
            AlpineFace(powderFaces,a,b,c,e);
        }
    }
    // Clip each ordinary snow cell against one slanting bank. The intersection
    // also covers cells where the broken rim crosses a lane boundary mid-row.
    void ClipAlpineShelf(float d0,float d1,float x,float edge0,float edge1,bool left){
        if(left?Mathf.Max(edge0,edge1)<=x:Mathf.Min(edge0,edge1)>=x+2)return;
        int start=alpineVertices.Count;
        Vector2 prev=new Vector2(x+2,d0);
        float previousSide=(left?1:-1)*(prev.x-edge0);
        for(int k=0;k<4;k++){
            bool far=k==1||k==2;
            Vector2 next=new Vector2(k<2?x:x+2,far?d1:d0);
            float side=(left?1:-1)*(next.x-(far?edge1:edge0));
            if((previousSide<0&&side>0)||(previousSide>0&&side<0)){
                var meet=Vector2.Lerp(prev,next,previousSide/(previousSide-side));
                AlpineVertex(meet.y,meet.x,0,Color.white);
            }
            if(side<=0)AlpineVertex(next.y,next.x,0,Color.white);
            prev=next;previousSide=side;
        }
        for(int k=start+2;k<alpineVertices.Count;k++){powderFaces.Add(start);powderFaces.Add(k-1);powderFaces.Add(k);}
    }

    // A white shelf projects over blue ice. Fractured faces taper into dark rock
    // at the bottom. Their exact snow contact is the first/last point of the section.
    void CrevasseProfile(float d,Vector2 banks,int facet){
        float width=banks.y-banks.x;
        float depth=(7.4f+.35f*Mathf.Sin(d*.32f))*Smooth(width/1.45f);
        float band=.48f+.22f*Mathf.Sin(d*.91f),slab=.36f+.13f*Mathf.Sin(d*.71f);
        Color snow=new Color(.94f,.97f,1);
        float variation=.5f+.5f*Mathf.Sin((facet+1)*2.7f+d*.11f);
        Color ice=Color.Lerp(new Color(.52f,.74f,.84f),new Color(.67f,.82f,.87f),variation);
        Color stone=Color.Lerp(new Color(.29f,.40f,.48f),new Color(.38f,.48f,.54f),variation);
        Color deep=new Color(.22f,.31f,.38f);
        void Point(float fraction,float below,Color tint){
            AlpineVertex(d,Mathf.LerpUnclamped(banks.x,banks.y,fraction),below,tint);
        }
        Point(0,0,snow);
        Point(-.035f,Mathf.Min(depth*.14f,band),ice);
        Point(.11f,depth*slab,stone);
        Point(.36f,depth*.98f,deep);
        Point(.52f,depth,deep);
        Point(.65f,depth*.98f,deep);
        Point(.87f,depth*(slab+.14f),stone);
        Point(1.038f,Mathf.Min(depth*.17f,band+.15f),ice);
        Point(1,0,snow);
    }
}
