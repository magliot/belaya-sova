using System;
using System.Collections.Generic;
using UnityEngine;

public partial class RaceWorld {
    struct Turn {public float start,length,x,delta;}
    readonly List<Turn> turns=new List<Turn>();
    float lastTurnD,lastTurnX;
    float cacheD=float.NaN,cx,dx,curve;
    Mesh terrain;
    GameObject groomedRun;
    Material pisteMat,bankMat;
    static float Smooth(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
    void BuildTurns(){
        turns.Clear();cacheD=float.NaN;float d=0,x=0;int i=0;
        PrepareAlpineProfile();
        void AddTurn(float length,float end){turns.Add(new Turn{start=d,length=length,x=x,delta=end-x});d+=length;x=end;}
        float side=Roll(seed,0,0)>.5f?1:-1;
        AddTurn(22,0);
        while(d<StageLength-580){
            side=Mathf.Abs(x)>46?-Mathf.Sign(x):Roll(seed,i,2)<.55f?-side:side;
            AddTurn(100+Roll(seed,i,3)*37,x+side*(17+Roll(seed,i,4)*10));
            if(Roll(seed,i,5)<.56f)AddTurn(112+Roll(seed,i,6)*32,x-side*(18+Roll(seed,i,7)*10));
            AddTurn(12+Roll(seed,i,8)*42,x);i++;
        }
        lastTurnD=d;lastTurnX=x;
    }
    void SampleRoad(float d){
        if(d==cacheD)return;cacheD=d;
        float a=PisteCentre(d);
        float b=36f/52*Mathf.Sin(d/52)-16f/97*Mathf.Sin(d/97);
        float c=36f/(52*52)*Mathf.Cos(d/52)-16f/(97*97)*Mathf.Cos(d/97);
        cx=a;dx=b;float dd=c;
        if(d<StageLength){
            if(d<=0){cx=dx=curve=0;return;}
            int lo=0,hi=turns.Count;
            while(lo<hi){int mid=(lo+hi)/2;var t=turns[mid];if(d>=t.start+t.length)lo=mid+1;else hi=mid;}
            bool tail=lo==turns.Count;
            var seg=tail?new Turn{start=lastTurnD,length=StageLength-lastTurnD,x=lastTurnX,delta=a-lastTurnX}:turns[lo];
            float u=(d-seg.start)/seg.length,blend=u*u*u*(10+u*(-15+6*u));
            float vel=30*u*u*(u-1)*(u-1)/seg.length,accel=60*u*(u-1)*(2*u-1)/(seg.length*seg.length);
            cx=seg.x+seg.delta*blend;dx=seg.delta*vel;dd=seg.delta*accel;
            if(tail){dx+=blend*b;dd+=2*vel*b+blend*c;}
        }
        if(d>=LapLength&&mode=="normal")EndlessRoad(d,ref cx,ref dx,ref dd);
        if(AlpineAt(d))BlendAlpineCentre(d,ref cx,ref dx,ref dd);
        curve=dd/Mathf.Pow(1+dx*dx,1.5f);
        if(AlpineAt(d))SampleAlpineProfile(d);
    }
    public float HeadingAt(float metres){SampleRoad(metres);return Mathf.Atan(dx);}
    public float WidthAt(float metres){
        if(AlpineAt(metres))return 21+3*AlpineBlend(metres);
        if(mode=="tubing")return 16.2f;
        float blend=Smooth(Mathf.Repeat(metres,StageLength)/35)*Smooth((StageLength-Mathf.Repeat(metres,StageLength))/35);
        return StageAt(metres)==1?21-7.5f*blend:21;
    }
    public static float Grade(float d)=>.29f+.22f*Mathf.Sin(d/42)+.09f*Mathf.Sin(d/17+1);
    public Vector3 SampleGround(float metres,float lane){
        SampleRoad(metres);
        if(AlpineAt(metres))return AlpineGround(metres,lane);
        float d=metres,mag=Mathf.Abs(lane),h=Mathf.Atan(dx);
        float bank=.12f*(float)Math.Tanh(-curve*10/.12f);
        float height=CourseHeight(d);
        float shoulder=Smooth((mag-9.4f)/5.8f)*(3.4f+.65f*Mathf.Sin(d/23+(lane<0?1.4f:0))+.25f*Mathf.Cos(d/9));
        int stage=StageAt(d);float within=Mathf.Repeat(d,StageLength),blend=Smooth(within/35)*Smooth((StageLength-within)/35);
        if(stage==1)shoulder=Mathf.Lerp(shoulder,2.8f*Smooth((mag-6.6f)/5.8f),blend);
        if(stage==3)shoulder=Mathf.Lerp(shoulder,4.2f*Smooth((mag-6)/6),blend);
        if(mode=="tubing")shoulder+=.67f*Smooth((mag-7.3f)/.95f)*(1-Smooth((mag-8.3f)/1.6f));
        if(mode=="normal"&&stage==2&&within<MogulRunLength&&mag<16)height+=MogulHeight(d,lane);
        if(stage==4&&mag<18)height+=SummitField(d,lane);
        float lateral=lane*Mathf.Cos(h),along=lane*Mathf.Sin(h);
        if(mode=="normal"&&mag>16){
            height+=ValleyRelief(d,lane);
            float open=Smooth((mag-16)/36);
            lateral=Mathf.Lerp(lateral,lane,open);
            along=Mathf.Lerp(along,Mathf.Clamp(lane,-16,16)*Mathf.Sin(h),open);
        }
        return new Vector3(cx+lateral,height+bank*Mathf.Clamp(lane,-14,14)+shoulder,d-along);
    }
    float MogulHeight(float d,float x){
        float local=Mathf.Repeat(d,StageLength);int section=Mathf.FloorToInt(local/117),field=section%3;float rowD=local-section*117-7.5f;
        float h=0;
        for(int i=Mathf.Max(0,Mathf.FloorToInt(rowD/8.75f)-1);i<=Mathf.Min(11,Mathf.CeilToInt(rowD/8.75f)+1);i++){
            float shift=(i%2==1?1:-1)*(field==0?1.4f:field==1?2.45f:1.75f);
            foreach(int sign in sides){
                bool paired=field==1&&i%3==1;
                float ex=(x-sign*4.5f-shift)/(field==2?4.7f:paired?4.95f:5.75f);
                float ey=(rowD-i*8.75f-(paired&&sign>0?2.25f:0))/(field==2?8.1f:paired?6.75f:7.25f);
                int profile=(i+(sign>0?1:0))%3;if(profile==2)ex-=.28f*ey;float len=ex*ex+ey*ey;
                if(len<1){float amp=1.08f+Roll(seed,i+Mathf.RoundToInt((d-local+section*117)*20),sign>0?290:110)*(.22f+field*.09f);
                    h+=amp*Mathf.Pow(Mathf.Cos(Mathf.Sqrt(len)*Mathf.PI*.5f),profile==1?1.55f:2);}
            }
        }
        return h*1.22f*MogulRoughness(d,x);
    }
    static readonly int[] sides={-1,1};
    static readonly float[] columns={-152,-116,-90,-68,-52,-38,-26,-16,-13,-11,-9.4f,-7.5f,-6,-4.5f,-3,-1.5f,0,1.5f,3,4.5f,6,7.5f,9.4f,11,13,16,26,38,52,68,90,116,152};
    static readonly float[] bonusColumns={-54,-26,-16,-13,-11,-9.4f,-7.5f,-6,-4.5f,-3,-1.5f,0,1.5f,3,4.5f,6,7.5f,9.4f,11,13,16,26,54};
    float[] GroomedColumns=>mode=="normal"?columns:bonusColumns;
    Vector3[] snowVertices;
    float firstSnowRow;
    float MeshTrail(float d){
        if(mode!="normal"||StageAt(d)!=2)return 0;
        float local=Mathf.Repeat(d,StageLength);
        return MogulLine(d)*Smooth(local/16)*Smooth((MogulRunLength-local)/24);
    }
    float ColumnLane(int column,float width,float trail){
        if(usingAlpineMesh)return alpineLanes[column];
        float x=GroomedColumns[column];
        if(Mathf.Abs(x)>11)return x;
        float lane=x*width/22;
        return lane+trail*(1-Smooth((Mathf.Abs(x)-1.5f)/9.5f));
    }
    // Use the very triangles drawn under the runner, including banks, moguls and narrow forest stretches.
    public Vector3 SnowSurface(float d,float lane){
        float lower=Mathf.Floor(d*.5f)*2,t=(d-lower)*.5f;
        float width0=WidthAt(lower),width1=WidthAt(lower+2);
        float trail0=MeshTrail(lower),trail1=MeshTrail(lower+2);
        float LaneAt(int j)=>Mathf.Lerp(ColumnLane(j,width0,trail0),ColumnLane(j,width1,trail1),t);
        int cols=usingAlpineMesh?alpineLanes.Length:GroomedColumns.Length;
        int lo=0,hi=cols-1;
        while(hi-lo>1){int mid=(lo+hi)/2;if(lane<LaneAt(mid))hi=mid;else lo=mid;}
        float u=Mathf.InverseLerp(LaneAt(lo),LaneAt(hi),lane);
        int row=(int)((lower-firstSnowRow)*.5f);
        Vector3 p00,p10,p01,p11;
        if(snowVertices!=null&&row>=0&&row+1<snowVertices.Length/cols){
            int a=row*cols+lo,b=a+cols;
            p00=snowVertices[a];p10=snowVertices[a+1];p01=snowVertices[b];p11=snowVertices[b+1];
        }else{
            // A seeded row can queue a jump beyond the end of the current snow patch.
            p00=SampleGround(lower,ColumnLane(lo,width0,trail0));p10=SampleGround(lower,ColumnLane(hi,width0,trail0));
            p01=SampleGround(lower+2,ColumnLane(lo,width1,trail1));p11=SampleGround(lower+2,ColumnLane(hi,width1,trail1));
        }
        return u+t<=1?p00+(p10-p00)*u+(p01-p00)*t:p11+(p01-p11)*(1-u)+(p10-p11)*(1-t);
    }
    public Quaternion SnowFrame(float d,float lane){
        var forward=SnowSurface(d+.65f,lane)-SnowSurface(d-.65f,lane);
        var across=SnowSurface(d,lane+.55f)-SnowSurface(d,lane-.55f);
        return Quaternion.LookRotation(forward,Vector3.Cross(forward,across));
    }
    Material SnowPaint(string key,Color color){var m=new Material(Resources.Load<Material>("SlopeLit"));m.color=color;
        m.mainTexture=Resources.Load<Texture2D>("Art/"+key);m.SetFloat("_Smoothness",.06f);return m;}
    readonly byte[] markings=new byte[150];
    bool usingAlpineMesh;
    void UpdateTerrain(float centre){
        usingAlpineMesh=AlpineAt(centre)||AlpineAt(centre+84);
        if(usingAlpineMesh){
            if(groomedRun&&groomedRun.activeSelf)groomedRun.SetActive(false);
            UpdateAlpineTerrain(centre);return;
        }
        if(alpineRun&&alpineRun.activeSelf)alpineRun.SetActive(false);
        if(groomedRun&&!groomedRun.activeSelf)groomedRun.SetActive(true);
        // Continue the terrain behind the last streamed trees, past the end of the normal fog.
        const int rows=151;
        var lanes=GroomedColumns;
        int cols=lanes.Length;
        var points=new Vector3[rows*cols];var uv=new Vector2[points.Length];
        if(!terrain){
            var go=new GameObject("Groomed run");go.transform.SetParent(transform,false);
            groomedRun=go;
            terrain=new Mesh{name="Streaming piste"};terrain.MarkDynamic();terrain.subMeshCount=3;go.AddComponent<MeshFilter>().sharedMesh=terrain;
            pisteMat=SnowPaint("Piste",new Color(.91f,.94f,.98f));bankMat=SnowPaint("Powder",new Color(.85f,.90f,.96f));
            var packed=SnowPaint("Piste",new Color(.65f,.82f,.91f));
            go.AddComponent<MeshRenderer>().sharedMaterials=new[]{pisteMat,bankMat,packed};
        }
        bool reindex=terrain.vertexCount!=points.Length||terrain.GetIndexCount(0)==0;
        if(terrain.vertexCount!=0&&terrain.vertexCount!=points.Length){terrain.Clear();terrain.subMeshCount=3;}
        for(int i=0;i<rows;i++){
            float d=centre-50+2*i,half=WidthAt(d)*.5f,trail=MeshTrail(d);
            if(i<rows-1){
                byte mark=mode=="normal"&&StageAt(d)==2&&Mathf.Repeat(d,StageLength)<MogulRunLength?(byte)1:(byte)0;
                if(mark!=markings[i]){markings[i]=mark;reindex=true;}
            }
            for(int j=0;j<cols;j++){
                float x=ColumnLane(j,half*2,trail);
                points[i*cols+j]=SampleGround(d,x);uv[i*cols+j]=new Vector2(x/4.2f,d/10);
            }
        }
        snowVertices=points;firstSnowRow=centre-50;
        terrain.vertices=points;terrain.uv=uv;
        if(reindex){
            var middle=new List<int>(rows*84);var banks=new List<int>(rows*48);var packed=new List<int>();
            for(int i=0;i<rows-1;i++)for(int j=0;j<cols-1;j++){int a=i*cols+j;
                var indices=markings[i]==1&&lanes[j]>=-1.5f&&lanes[j]<1.5f?packed:lanes[j]>=-11&&lanes[j]<11?middle:banks;
                indices.Add(a);indices.Add(a+cols);indices.Add(a+1);
                indices.Add(a+1);indices.Add(a+cols);indices.Add(a+cols+1);}
            terrain.SetTriangles(middle,0,false);terrain.SetTriangles(banks,1,false);terrain.SetTriangles(packed,2,false);
        }
        terrain.RecalculateNormals();terrain.RecalculateBounds();
    }
    static readonly Dictionary<string,Mesh> cards=new Dictionary<string,Mesh>();
    static readonly Dictionary<string,Material> trees=new Dictionary<string,Material>();
    GameObject ForestCard(string key){
        if(!cards.TryGetValue(key,out var mesh)){
            var tex=Resources.Load<Texture2D>("Race/Forest/"+key);
            int family=key[key.Length-2]-'0';float h=family==0?3.7f:family==1?7.2f:2.45f,w=h*tex.width/tex.height*.5f;
            mesh=new Mesh{name=key};
            mesh.vertices=new[]{new Vector3(-w,0,0),new Vector3(-w,h,0),new Vector3(w,h,0),new Vector3(w,0,0)};
            mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
            mesh.colors=new[]{Color.white,Color.white,Color.white,Color.white};
            mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();
            // The shader turns the single drawing around its trunk. Bound every possible angle.
            mesh.bounds=new Bounds(Vector3.up*h*.5f,new Vector3(2*w,h,2*w));cards[key]=mesh;
            var material=new Material(Resources.Load<Shader>("Race/WorldObject"));material.SetTexture("_BaseMap",tex);
            material.SetFloat("_Lit",.2f);material.SetFloat("_LeafCoverage",1);trees[key]=material;
        }
        var obj=new GameObject(key);obj.transform.SetParent(transform,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
        var mr=obj.AddComponent<MeshRenderer>();mr.sharedMaterial=trees[key];mr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return obj;
    }
    int sceneryRow=int.MinValue;
    void UpdateScenery(float d){
        int start=Mathf.FloorToInt((d-25)/12),end=Mathf.CeilToInt((d+146)/12);
        for(int i=scenery.Count-1;i>=0;i--)if(scenery[i].distance<start*12||scenery[i].distance>(end+1)*12){Release(scenery[i]);scenery.RemoveAt(i);}
        if(scenery.Count==0)sceneryRow=start-1;
        for(int row=Mathf.Max(start,sceneryRow+1);row<=end;row++)foreach(int side in sides){
            if(AlpineAt(row*12))continue;
            int stage=StageAt(row*12);int count=1+(Roll(seed,row,100+side)>.38f?2:0);
            if(mode=="tubing"){
                float woods=TubeWoods(row*12);
                // Broad gaps open onto the ridge and bay; the same treeline closes in through the forest.
                count=Roll(seed,row,201+side)<.20f+.72f*woods?1+(int)(woods*2+.25f):0;
            }
            if(stage==1){
                int grove=Mathf.FloorToInt((row+(side<0?2:0))/5f);
                int phase=(row+(side<0?2:0))%5;
                count=phase<3?2+(Roll(seed,grove,207+side)>.45f?1:0):phase==3?1:0;
            }
            if(mode=="normal"){
                float woods=LandscapeTuning(row*12).x;
                // Both flanks have their own stands and clearings. Keep the first
                // view through a portal open while the next landscape fills in beyond.
                int group=Mathf.FloorToInt((row+(side<0?3:0))/4f);
                if(Roll(seed,group,392+side)>woods||NearPortal(row*12,17))count=0;
            }
            for(int i=0;i<count;i++){
                float t=row*12+i*1.88f+Roll(seed,row,120+i+side)*2;
                float offset=(stage==1?9.5f:stage==3?18:14.5f)+i*3.4f+Roll(seed,row,108+i+side)*5;
                int family=Mathf.Min(2,(int)(Roll(seed,row,130+i+side)*3));
                int v=Mathf.Min(2,(int)(Roll(seed,row,140+i+side)*3));
                float scale=.82f+Roll(seed,row,150+i+side)*.82f;
                float height=family==0?3.7f:family==1?7.2f:2.45f;
                var obj=new RaceItem{kind="woods",variant="forestCard"+family+v,lane=side*offset,distance=t,
                    scale=scale,offset=-.12f-height*scale*.085f,decor=true};
                scenery.Add(obj);Place(obj);
            }
            if(row%3==0&&(stage!=0||mode=="sled")&&mode!="tubing"&&(mode!="normal"||!NearPortal(row*12,19))){
                var lamp=new RaceItem{kind="lamp",variant="stageLamp"+stage,
                    distance=row*12,lane=side*11.6f,decor=true,yaw=90};scenery.Add(lamp);Place(lamp);
            }
        }
        sceneryRow=end;
    }
    GameObject Ramp(RaceItem item){
        bool rail=item.kind=="rail";const int rows=13;
        float half=item.width*.5f,len=item.length;
        float[] cross={-half,-half,-half*.90f,0,half*.90f,half,half};
        int cols=cross.Length;
        Vector3 origin=SnowSurface(item.distance,item.lane);
        var positions=new List<Vector3>();var triangles=new List<int>();var colors=new List<Color32>();var uv=new List<Vector2>();
        for(int r=0;r<=rows;r++){
            float t=(float)r/rows,y=rail?item.height*Mathf.Clamp01(Mathf.Min(t,1-t)*len/2):
                item.height*t*(item.park?.65f+.35f*t:1);
            float z=(t-.5f)*len;
            for(int c=0;c<cols;c++){
                float x=cross[c];bool skirt=c==0||c==cols-1;
                Vector3 basePoint=SnowSurface(item.distance+z,item.lane+x)-origin;
                positions.Add(basePoint+Vector3.up*(skirt?-.12f:y+.015f));uv.Add(new Vector2(x/4.2f,z/10));
                colors.Add(skirt?new Color32(44,71,96,255):c==1||c==cols-2||r==rows?new Color32(255,173,43,255):new Color32(225,241,255,255));
            }
        }
        for(int i=0;i<rows;i++)for(int j=0;j<cols-1;j++){int a=i*cols+j;triangles.AddRange(new[]{a,a+cols,a+1,a+1,a+cols,a+cols+1});}
        // Close the lip all the way down to the snow across the complete ramp width.
        int tip=rows*cols,baseStart=positions.Count;
        for(int j=1;j<cols-1;j++){
            positions.Add(positions[tip+j]-Vector3.up*(item.height*(rail?0:1)+.135f));
            uv.Add(uv[tip+j]);colors.Add(new Color32(77,102,127,255));
            if(j>1){int b=baseStart+j-2;triangles.AddRange(new[]{tip+j-1,tip+j,b,b,tip+j,b+1});}
        }
        var mesh=new Mesh{name=item.kind};mesh.SetVertices(positions);mesh.SetColors(colors);mesh.SetUVs(0,uv);
        mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        var obj=new GameObject(item.kind);obj.transform.SetParent(transform,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
        obj.AddComponent<MeshRenderer>().sharedMaterial=Paint("",0,true,0);return obj;
    }
}
