using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class RaceWorld {
    // Tree cover, height of the far shoulder, exposed rock and lights in the valley.
    static readonly Vector4[] sceneryProfiles={
        new Vector4(.55f,18,.17f,0),new Vector4(.99f,24,.12f,0),
        new Vector4(.36f,21,.31f,0),new Vector4(.48f,15,.18f,0),
        new Vector4(.025f,17,.47f,0),new Vector4(.12f,38,1,0),
        new Vector4(.63f,23,.28f,0),new Vector4(.57f,16,.18f,1),
        new Vector4(0,27,.54f,0)
    };
    static Vector4 LandscapeTuning(float d){
        if(d<90)return sceneryProfiles[0];
        int boundary=Mathf.FloorToInt((d+82)/StageLength);
        if(boundary==0)return sceneryProfiles[0];
        float t=Smooth((d-boundary*StageLength+82)/138);
        return Vector4.Lerp(sceneryProfiles[(boundary-1)%StageCount],sceneryProfiles[boundary%StageCount],t);
    }
    static bool NearPortal(float d,float radius){
        if(d<StageLength/2)return Mathf.Abs(d-12)<radius;
        return Mathf.Abs(d-Mathf.Round(d/StageLength)*StageLength)<radius;
    }
    void AddEntrySpruces(int row,int side){
        float d=row*12;
        if(d<0)return;
        int stage=StageAt(d);
        float entry=d<StageLength?12:Mathf.Floor(d/StageLength)*StageLength;
        float after=d-entry;
        if(after<7||after>162||stage==4||stage==5||stage==8)return;
        int run=Mathf.FloorToInt(after/12);
        if(stage!=1&&stage!=6&&run%4!=1)return;
        if((run+(side<0?1:3))%5==0)return;
        int count=stage==1?3:2;
        for(int i=0;i<count;i++){
            float along=d+i*2.8f+Roll(seed,row,458+i+side)*4;
            float lane=WidthAt(along)*.5f+(stage==1?1.8f:3.4f)+i*2.4f+Roll(seed,row,470+i+side)*2;
            float size=(i==0?1.8f:2.2f)+Roll(seed,row,480+i+side)*1.28f;
            var pos=SnowSurface(along,side*lane)-valleyOrigin-Vector3.up*(.12f+.23f*size);
            float height=3.7f*size,half=height*spruceAspect*.5f*(i==2?1.17f:1);
            int index=standVertices.Count;
            Corner(-half,0,0,0);Corner(-half,height,0,1);Corner(half,height,1,1);Corner(half,0,1,0);
            standFaces.Add(index);standFaces.Add(index+1);standFaces.Add(index+2);
            standFaces.Add(index);standFaces.Add(index+2);standFaces.Add(index+3);
            void Corner(float x,float y,float u,float v){
                standVertices.Add(pos+new Vector3(x,y,0));standOffsets.Add(new Vector2(x,y));
                standUV.Add(new Vector2(u,v));standColors.Add(new Color32(255,255,255,255));
            }
        }
    }
    float ValleyRelief(float d,float lane){
        var style=LandscapeTuning(d);
        float a=Mathf.Abs(lane),phase=(lane<0?2.4f:0)+alpinePhase;
        float spur=.5f+.5f*Mathf.Sin(d*.021f+phase+Mathf.Sin(d*.009f)*1.18f);
        float ridge=.53f+.36f*Mathf.Sin(d*.014f+phase)+.11f*Mathf.Cos(d*.048f+phase);
        float near=(2+spur*5+style.z*spur*9)*Smooth((a-16)/20)*(1-Smooth((a-38)/17));
        float hollow=(3+style.w*7)*Smooth((a-29)/21)*(1-Smooth((a-59)/20));
        float wall=(6+style.y*ridge)*Smooth((a-48)/42)*(1-.39f*Smooth((a-110)/41));
        return near-hollow+wall;
    }

    // Six 64 m strips cover the mobile camera. Only the entering strip is rebuilt.
    // Each has one shared opaque material and an optional second draw for warm windows.
    const int ValleyTileLength=64,ValleyTileCount=6;
    sealed class ValleyTile {
        public int index=int.MinValue;
        public GameObject root;
        public Mesh mesh;
        public MeshRenderer renderer;
        public Mesh stand;
        public MeshRenderer standRenderer;
    }
    readonly ValleyTile[] valleyTiles=new ValleyTile[ValleyTileCount];
    readonly List<Vector3> valleyVertices=new List<Vector3>(4096);
    readonly List<Color32> valleyColors=new List<Color32>(4096);
    readonly List<int> valleyFaces=new List<int>(4096),valleyLamps=new List<int>(144);
    Material[] valleyDay,valleyLit;
    Material sprucePaint;
    float spruceAspect;
    readonly List<Vector3> standVertices=new List<Vector3>(160);
    readonly List<Vector2> standOffsets=new List<Vector2>(160),standUV=new List<Vector2>(160);
    readonly List<Color32> standColors=new List<Color32>(160);
    readonly List<int> standFaces=new List<int>(240);
    Vector3 valleyOrigin;
    static readonly Color32 trunkPaint=new Color32(90,76,60,255),needles=new Color32(53,96,94,255),
        snowShade=new Color32(157,188,206,255),snowTop=new Color32(228,240,246,255),
        slateA=new Color32(58,70,84,255),slateB=new Color32(108,117,131,255),
        timber=new Color32(123,79,57,255),window=new Color32(255,194,94,255);
    void ClearLandscape(){
        foreach(var tile in valleyTiles)if(tile!=null){tile.index=int.MinValue;tile.root.SetActive(false);}
    }
    void OnDestroy(){
        foreach(var tile in valleyTiles)if(tile!=null){
            DisposeOwned(tile.mesh);if(tile.stand)DisposeOwned(tile.stand);
        }
        if(sprucePaint)DisposeOwned(sprucePaint);
    }
    void BuildEntryStand(ValleyTile tile,float start){
        if(!sprucePaint){
            var tex=Resources.Load<Texture2D>("Race/Forest/forestCard00");spruceAspect=(float)tex.width/tex.height;
            sprucePaint=new Material(Resources.Load<Shader>("Race/WorldObject"));sprucePaint.SetTexture("_BaseMap",tex);
            sprucePaint.SetFloat("_Lit",.2f);sprucePaint.SetFloat("_LeafCoverage",1);sprucePaint.SetFloat("_GrovePivots",1);
        }
        standVertices.Clear();standOffsets.Clear();standUV.Clear();standColors.Clear();standFaces.Clear();
        for(int row=Mathf.CeilToInt(start/12);row*12<start+ValleyTileLength;row++){
            foreach(int side in sides)AddEntrySpruces(row,side);
        }
        if(standFaces.Count==0){if(tile.standRenderer)tile.standRenderer.enabled=false;return;}
        if(!tile.stand){
            var child=new GameObject("Spruces beside the entrance");child.transform.SetParent(tile.root.transform,false);
            tile.stand=new Mesh{name="Batched spruce crowns"};tile.stand.MarkDynamic();
            child.AddComponent<MeshFilter>().sharedMesh=tile.stand;tile.standRenderer=child.AddComponent<MeshRenderer>();
            tile.standRenderer.sharedMaterial=sprucePaint;tile.standRenderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        var mesh=tile.stand;mesh.Clear();mesh.SetVertices(standVertices);mesh.SetColors(standColors);
        mesh.SetUVs(0,standUV);mesh.SetUVs(1,standOffsets);mesh.SetTriangles(standFaces,0);
        var bounds=mesh.bounds;bounds.Expand(new Vector3(6,0,12));mesh.bounds=bounds;
        tile.standRenderer.enabled=true;
    }
    void UpdateLandscape(float distance){
        if(mode!="normal")return;
        int first=Mathf.FloorToInt((distance-42)/ValleyTileLength);
        for(int i=first;i<first+ValleyTileCount;i++){
            int slot=(i%ValleyTileCount+ValleyTileCount)%ValleyTileCount;
            var tile=valleyTiles[slot];
            if(tile==null){
                var root=new GameObject("Valley slopes");root.transform.SetParent(transform,false);
                var mesh=new Mesh{name="Spruce groves and outcrops"};mesh.MarkDynamic();
                root.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=root.AddComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;
                if(valleyDay==null){
                    valleyDay=new[]{Paint("",0,true,0)};
                    valleyLit=new[]{valleyDay[0],Paint("",0,false,222)};
                }
                tile=valleyTiles[slot]=new ValleyTile{root=root,mesh=mesh,renderer=renderer};
            }
            if(tile.index==i)continue;
            tile.index=i;BuildValleyTile(tile);
        }
    }
    void ValleyFace(Vector3 a,Vector3 b,Vector3 c,Color32 color,bool lamp=false){
        int start=valleyVertices.Count;
        valleyVertices.Add(a);valleyVertices.Add(b);valleyVertices.Add(c);
        valleyColors.Add(color);valleyColors.Add(color);valleyColors.Add(color);
        var indices=lamp?valleyLamps:valleyFaces;
        indices.Add(start);indices.Add(start+1);indices.Add(start+2);
    }
    static Vector3 Around(Vector3 p,float angle,float width,float depth,float height=0){
        return p+new Vector3(Mathf.Cos(angle)*width,height,Mathf.Sin(angle)*depth);
    }
    void Fir(Vector3 p,float height,float turn,bool far){
        float width=height*(.175f+.028f*Mathf.Sin(turn*4));
        var foot=p-Vector3.up*.35f;
        var crown=p+Vector3.up*(height*.62f);
        for(int i=0;i<4;i++){
            float a=turn+i*Mathf.PI*.5f,b=a+Mathf.PI*.5f;
            ValleyFace(Around(foot,b,width*.12f,width*.13f),Around(foot,a,width*.12f,width*.13f),crown,trunkPaint);
        }
        int tiers=far?2:3,sides=far?4:6;
        for(int tier=0;tier<tiers;tier++){
            float t=(float)tier/tiers,r=width*(1-t*.76f);
            var centre=p+Vector3.up*(height*(.15f+t*.69f));
            var peak=p+Vector3.up*(height*(tier==tiers-1?1:.54f+t*.58f));
            float twist=turn+tier*.78f;
            for(int i=0;i<sides;i++){
                float a=twist+i*2*Mathf.PI/sides,b=twist+(i+1)*2*Mathf.PI/sides;
                float edge=.75f+.28f*Mathf.Abs(Mathf.Sin(3*a+turn));
                var left=Around(centre,a,r,r*edge,r*.16f*Mathf.Sin(a*2));
                var right=Around(centre,b,r,r*(.75f+.28f*Mathf.Abs(Mathf.Sin(3*b+turn))),r*.16f*Mathf.Sin(b*2));
                var sag=Around(centre,(a+b)*.5f,r*1.08f,r*.98f,-r*.24f);
                var cap=Vector3.Lerp(sag,peak,.32f+.12f*Mathf.Cos(a));
                ValleyFace(left,cap,sag,needles);ValleyFace(sag,cap,right,needles);
                ValleyFace(left,peak,cap,i%3==0?snowShade:snowTop);
                ValleyFace(cap,peak,right,i%3==1?snowTop:snowShade);
            }
        }
    }
    void Outcrop(Vector3 p,float r,float h,float a){
        var centre=p+new Vector3(r*.16f,h,r*-.10f);
        const int n=7;
        for(int i=0;i<n;i++){
            Vector3 Foot(int j){
                float b=a+(j%n)*2*Mathf.PI/n,span=r*(.82f+.19f*Mathf.Sin(3*b+a));
                return Around(p,b,span,span*.78f,-r*.85f);
            }
            Vector3 Rim(int j){
                float b=a+(j%n)*2*Mathf.PI/n;
                float broken=.66f+.19f*Mathf.Sin(3*b+a);
                return Around(centre,b,r*broken,r*broken*.71f,h*(.27f*Mathf.Cos(2*b+a)-.31f));
            }
            var l=Foot(i);var k=Foot(i+1);var u=Rim(i);var v=Rim(i+1);
            var ledge=Vector3.Lerp(l,u,.52f)+new Vector3(r*.09f,0,-r*.12f);
            var end=Vector3.Lerp(k,v,.54f)+new Vector3(r*.09f,0,-r*.12f);
            var cut=Vector3.Lerp(ledge,end,.37f)+Vector3.down*(r*.13f);
            ValleyFace(l,ledge,cut,slateA);ValleyFace(l,cut,k,slateB);ValleyFace(k,cut,end,slateA);
            ValleyFace(ledge,u,cut,slateB);ValleyFace(cut,u,v,slateA);ValleyFace(cut,v,end,slateB);
            var snow=Vector3.Lerp(u,ledge,.12f+.19f*Mathf.Abs(Mathf.Sin(a+i)));
            ValleyFace(u,snow,v,snowShade);ValleyFace(u,centre,v,snowTop);
        }
    }
    void Cabin(Vector3 p,float s){
        Vector3 V(float x,float y,float z)=>p+new Vector3(x,y,z)*s;
        var left=V(-1.9f,1.7f,-1.6f);var right=V(1.9f,1.7f,-1.6f);
        var ridge=V(0,3.05f,-1.8f);
        ValleyFace(V(-1.9f,-1,-1.6f),left,right,timber);
        ValleyFace(V(-1.9f,-1,-1.6f),right,V(1.9f,-1,-1.6f),timber);
        ValleyFace(left,ridge,right,timber);
        foreach(int side in sides){
            var foot=V(side*1.9f,-1,1.6f);var topWall=V(side*1.9f,1.7f,1.6f);
            var near=V(side*1.9f,-1,-1.6f);
            ValleyFace(foot,near,topWall,timber);ValleyFace(near,V(side*1.9f,1.7f,-1.6f),topWall,timber);
            var e=V(side*2.2f,1.7f,-1.8f);var f=V(side*2.2f,1.7f,1.8f);
            var top=V(0,3.05f,1.8f);
            ValleyFace(e,top,ridge,snowTop);ValleyFace(f,top,e,snowShade);
            var a=V(side*1.12f-.34f,.55f,-1.62f);var b=a+Vector3.right*s*.68f;
            var c=b+Vector3.up*s*.79f;
            ValleyFace(a,c,b,window,true);ValleyFace(a,a+Vector3.up*s*.79f,c,window,true);
            var g=V(side*1.92f,.56f,-.12f);var k=g+Vector3.forward*s*.92f;
            ValleyFace(g,k,k+Vector3.up*s*.79f,window,true);ValleyFace(g,k+Vector3.up*s*.79f,g+Vector3.up*s*.79f,window,true);
        }
    }
    void BuildValleyTile(ValleyTile tile){
        int tileIndex=tile.index;
        float start=tileIndex*ValleyTileLength;
        valleyOrigin=SampleGround(start+32,0);tile.root.transform.localPosition=valleyOrigin;
        valleyVertices.Clear();valleyColors.Clear();valleyFaces.Clear();valleyLamps.Clear();
        foreach(int side in sides){
            for(int band=0;band<4;band++){
                int key=tileIndex*16+band*2+(side<0?1:0);
                float R(int channel)=>Roll(seed,key,600+channel);
                float along=start+6+R(0)*51;
                var style=LandscapeTuning(along);
                float lane=(band==0?19:band==1?34:band==2?58:81)+R(2)*(band==0?7:12);
                int count=Mathf.RoundToInt((band==0?4:band==1?7:11)*style.x*(.4f+R(3)));
                if(R(4)<.23f||AlpineAt(along))count=0;
                for(int j=0;j<count;j++){
                    float d=along+(R(12+j*3)-.5f)*29;
                    float x=side*(lane+(R(13+j*3)-.5f)*(band==0?5:18));
                    float turn=R(14+j*3)*Mathf.PI*2;
                    if(AlpineAt(d))continue;
                    Fir(SnowSurface(d,x)-valleyOrigin-Vector3.up*.18f,4.3f+R(65+j)*5.8f,turn,band>=2);
                }
                int crags=Mathf.RoundToInt(style.z*(band<2?4:2)*(.45f+R(6)));
                for(int j=0;j<crags;j++){
                    float d=along+(R(74+j)-.5f)*25,x=side*(lane+R(80+j)*5);
                    float r=(band==0?1.4f:2.7f)+R(85+j)*(1.6f+style.z*3.8f);
                    float turn=R(95+j)*6;
                    Outcrop(SnowSurface(d,x)-valleyOrigin,r,r*(.48f+R(90+j)*(.6f+style.z)),turn);
                    if(band<2&&style.z>.6f){
                        float dz=r*(1.0f+.6f*R(110+j)),across=x-side*r*.51f;
                        Outcrop(SnowSurface(d+dz,across)-valleyOrigin,r*.56f,r*.51f,turn+1.52f);
                        Outcrop(SnowSurface(d-dz,x)-valleyOrigin,r*.44f,r*.34f,turn-.91f);
                    }
                }
                if(band==1&&StageAt(along)==7&&Mathf.Repeat(along,StageLength)<StageLength-220&&R(8)<style.w*.87f){
                    for(int j=0;j<3;j++){
                        float d=along+j*9,x=side*(lane+8+j%2*8);
                        Cabin(SnowSurface(d,x)-valleyOrigin,.85f+R(100+j)*.45f);
                    }
                }
            }
            // A settlement in the hollow beyond the festival gate. Break the roofs
            // into hamlets along the bend; each window is attached to its wall.
            float local=Mathf.Repeat(start+32,StageLength);
            if(StageAt(start+32)==7&&local<240){
                for(int j=0;j<5;j++){
                    int key=tileIndex*13+j+(side<0?6:0);
                    float d=start+8+j*10+Roll(seed,key,976)*4;
                    float x=side*(20+(j%3)*6+Roll(seed,key,977)*3);
                    Cabin(SnowSurface(d,x)-valleyOrigin,1.05f+Roll(seed,key,978)*.38f);
                }
            }
        }
        var mesh=tile.mesh;mesh.Clear();
        mesh.SetVertices(valleyVertices);mesh.SetColors(valleyColors);
        mesh.subMeshCount=valleyLamps.Count>0?2:1;mesh.SetTriangles(valleyFaces,0,false);
        if(valleyLamps.Count>0)mesh.SetTriangles(valleyLamps,1,false);
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        tile.renderer.sharedMaterials=valleyLamps.Count>0?valleyLit:valleyDay;
        BuildEntryStand(tile,start);
        tile.root.SetActive(valleyVertices.Count>0||standFaces.Count>0);
    }
}
