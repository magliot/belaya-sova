using UnityEngine;
using UnityEngine.Rendering;

public partial class MountainRace {
    const int TrackStamps=100;
    readonly Vector3[] trackVertices=new Vector3[4*TrackStamps];
    Mesh trackMesh;
    Transform splash;
    Material splashPaint;
    ParticleSystem carvingSnow;
    int trackIndex;
    float previousStamp=-1,splashStart=-10;
    static readonly Vector2[] StampUV={Vector2.zero,Vector2.right,Vector2.one,Vector2.up};

    void SetupTracks(){
        var trackObject=new GameObject("Ski and snowboard grooves");trackObject.transform.SetParent(transform,false);
        trackMesh=new Mesh{name="Snow grooves"};trackMesh.MarkDynamic();
        var uv=new Vector2[4*TrackStamps];var triangles=new int[6*TrackStamps];
        for(int i=0;i<TrackStamps;i++){
            int v=i*4,t=i*6;
            for(int j=0;j<4;j++)uv[v+j]=StampUV[j];
            triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v+2;triangles[t+4]=v+3;triangles[t+5]=v;
        }
        trackMesh.vertices=trackVertices;trackMesh.uv=uv;trackMesh.triangles=triangles;
        trackObject.AddComponent<MeshFilter>().sharedMesh=trackMesh;
        var r=trackObject.AddComponent<MeshRenderer>();r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        var shader=Resources.Load<Shader>("MountainRaceSnow");
        r.sharedMaterial=new Material(shader){mainTexture=Resources.Load<Texture2D>("Art/Race-groove"),color=new Color(.8f,.89f,.96f,.38f)};
        var spray=new GameObject("Landing snow chips 58A");splash=spray.transform;splash.SetParent(transform,false);
        var plane=new Mesh{name="Snow spray plane",vertices=new[]{new Vector3(-.5f,-.5f),new Vector3(.5f,-.5f),new Vector3(.5f,.5f),new Vector3(-.5f,.5f)},uv=StampUV,triangles=new[]{0,1,2,2,3,0}};
        spray.AddComponent<MeshFilter>().sharedMesh=plane;
        splashPaint=new Material(shader){mainTexture=Resources.Load<Texture2D>("Art/Race-chips-58a")};
        var sr=spray.AddComponent<MeshRenderer>();sr.sharedMaterial=splashPaint;sr.shadowCastingMode=ShadowCastingMode.Off;sr.receiveShadows=false;
        spray.SetActive(false);
    }
    void ClearTracks(){
        ClearBursts();
        trackIndex=0;previousStamp=-1;splashStart=-10;
        System.Array.Clear(trackVertices,0,trackVertices.Length);trackMesh.vertices=trackVertices;splash.gameObject.SetActive(false);
    }
    void Stamp(float x,float d,float width,float length){
        var centre=World.SampleGround(d,x)+Vector3.up*.045f;
        Vector3 front=(World.SampleGround(d+.2f,x+angle*.115f)-World.SampleGround(d-.2f,x-angle*.115f)).normalized;
        Vector3 side=(World.SampleGround(d,x+.1f)-World.SampleGround(d,x-.1f)).normalized;
        for(int i=0;i<4;i++){
            var uv=StampUV[i];trackVertices[trackIndex*4+i]=centre+front*((uv.y-.5f)*length)+side*((uv.x-.5f)*width);
        }
        trackIndex=(trackIndex+1)%TrackStamps;
    }
    void UpdateTracks(){
        if(IsMenu||Review)return;
        float d=Status.metres;
        SprayFromCarvingRiders(d);
        if(patrol==PatrolPhase.Walking&&d-previousStamp>=.65f){
            float side=(Mathf.FloorToInt((d-walkStart)/.65f)%2==0)?-.13f:.13f;
            Stamp(lane+side,d,.16f,.33f);previousStamp=d;
            trackMesh.vertices=trackVertices;trackMesh.bounds=new Bounds(World.SampleGround(d,lane),new Vector3(120,120,120));
        }else if(patrol!=PatrolPhase.Walking&&!airborne&&fall<=0&&rampHeight<.12f&&d-previousStamp>=.28f){
            float len=Mathf.Clamp(d-previousStamp+.12f,.56f,1.65f);
            if(CourseMode=="freeride")SprayPlayerPowder(d);
            if(Hero=="bear"&&!BonusMode){Stamp(lane-.2f,d,.11f,len);Stamp(lane+.2f,d,.11f,len);}
            else Stamp(lane,d,BonusMode?.63f:.34f,len);
            previousStamp=d;trackMesh.vertices=trackVertices;
            // The camera can only see recent tracks. Avoid culling from stale world-space bounds.
            trackMesh.bounds=new Bounds(World.SampleGround(d,lane),new Vector3(120,120,120));
        }
        float age=Status.elapsed-lastLanding;
        if(age>=0&&age<.32f){
            if(splashStart!=lastLanding){splashStart=lastLanding;splash.position=World.SampleGround(d,lane)+Vector3.up*.38f;}
            splash.gameObject.SetActive(true);splash.rotation=View.transform.rotation;
            splash.localScale=new Vector3(1.6f+age*3,.72f+age*2,1);splashPaint.color=new Color(1,1,1,.68f*(1-age/.32f));
        }else if(splash.gameObject.activeSelf)splash.gameObject.SetActive(false);
    }
    void SprayPlayerPowder(float distance){
        if(!carvingSnow)carvingSnow=MakeBurst("Пудра из-под канта","MountainRaceFlake");
        float pressure=Mathf.Clamp01(Mathf.Abs(velocity)/6);
        var origin=World.SampleGround(distance-.30f,lane);
        var forward=(World.SampleGround(distance+.2f,lane)-World.SampleGround(distance-.2f,lane)).normalized;
        var right=Vector3.Cross(Vector3.up,forward).normalized;
        for(int side=-1;side<=1;side+=2){
            float kick=side*velocity<0?pressure:.12f;
            var snow=new ParticleSystem.EmitParams{position=origin+side*.24f*right+Vector3.up*.13f,
                velocity=side*right*(.68f+2*kick)+Vector3.up*(.8f+1.65f*kick)-forward*.75f,
                startLifetime=.34f+.21f*kick,startSize=.20f+.23f*kick,startColor=new Color(.84f,.93f,1,.62f)};
            carvingSnow.Emit(snow,1);
        }
    }
    void SprayFromCarvingRiders(float player){
        foreach(var o in World.Items){
            bool tube=o.kind=="tube";
            if(!tube&&o.kind!="npc"||!o.obj||o.consumed||o.stand||o.sit||o.walker||o.offset>.12f)continue;
            float ahead=o.distance-player;
            if(ahead< -4||ahead>52||Mathf.Abs(o.lean)<5)continue;
            if(!float.IsNaN(o.sprayDistance)&&o.distance-o.sprayDistance<.48f)continue;
            o.sprayDistance=o.distance;
            if(!carvingSnow)carvingSnow=MakeBurst("Пудра из-под канта","MountainRaceFlake");
            var pose=o.obj.transform;
            var back=pose.forward*(tube?1:-1);
            var outside=pose.right*((tube?-1:1)*Mathf.Sign(o.lean));
            var snow=pose.position+pose.up*.08f+back*(tube?.45f:.37f)+outside*(tube?.47f:.22f);
            float pressure=Mathf.Clamp01(Mathf.Abs(o.lean)/16);
            var tint=Color.Lerp(new Color(.54f,.64f,.77f,.47f),new Color(.87f,.96f,1,.66f),
                Mathf.InverseLerp(.17f,.77f,sun.intensity));
            for(int j=0;j<3;j++){
                carvingSnow.Emit(new ParticleSystem.EmitParams{position=snow+back*(j*.045f),
                    velocity=outside*(.8f+.42f*j+pressure)+pose.up*(.45f+.29f*j)+back*.65f,
                    startSize=.105f+.04f*j+.05f*pressure,startLifetime=.34f+.08f*j,startColor=tint},1);
            }
        }
    }
}
