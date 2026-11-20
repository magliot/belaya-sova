using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RiderCourseChecks {
    const string Key="Owl.Mountain.RiderFilm";
    static RiderCourseChecks(){EditorApplication.update+=WhenReady;}
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    public static void Run(){
        SessionState.SetBool(Key,true);
        EditorSceneManager.OpenScene("Assets/Mountain.unity");
        EditorApplication.EnterPlaymode();
    }
    static void WhenReady(){
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||!SessionState.GetBool(Key,false))return;
        var race=UnityEngine.Object.FindFirstObjectByType<MountainRace>();if(!race||!race.View)return;
        SessionState.EraseBool(Key);
        try{Verify(race,true);EditorApplication.Exit(0);}
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(2);}
    }
    static void Shot(Camera camera,string name){
        int w=540,h=800;
        var old=camera.targetTexture;var prev=RenderTexture.active;float aspect=camera.aspect;
        var rt=RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);
        var image=new Texture2D(w,h,TextureFormat.RGB24,false);
        try{
            camera.aspect=(float)w/h;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,w,h),0,0);image.Apply();
            var dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../course-checks/mountain-race/riders-20i"));
            Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,name+".png"),image.EncodeToPNG());
        }finally{
            camera.targetTexture=old;camera.aspect=aspect;RenderTexture.active=prev;
            RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(image);
        }
    }
    public static void Verify(MountainRace race,bool capture=false){
        race.Menu();race.ChooseHero("owl");race.StartRace("{\"mode\":\"normal\",\"seed\":1911}");
        var actor=race.transform.Find("Selected rider/OwlRide");
        Require(actor&&actor.gameObject.activeInHierarchy,"Missing owl ride rig");
        var heading=(race.World.SnowSurface(1,0)-race.World.SnowSurface(0,0)).normalized;
        float downhill=Vector3.Dot(-actor.right,heading);
        Require(downhill>.95f,"Owl leading edge points uphill");
        if(capture)Shot(race.View,"01-downhill-owl");
        var movers=race.World.Items.Where(o=>o.kind=="npc"&&o.distance>55&&!o.stand&&!o.sit).ToArray();
        Require(movers.Length>0,"Missing distant moving pupil for seed 1911");
        var board=movers.First();float start=board.distance,x=board.lane;
        var riders=race.World.Items.Where(o=>o.kind=="npc"&&!o.stand&&!o.sit&&o.Visual is SkinnedMeshRenderer).Take(4).ToArray();
        Require(riders.Any(o=>o.kid)&&riders.Any(o=>o.sb),"missing downhill skiers or snowboarder in action check");
        var min=Enumerable.Repeat(200f,riders.Length).ToArray();var max=Enumerable.Repeat(-200f,riders.Length).ToArray();
        var slow=Enumerable.Repeat(100f,riders.Length).ToArray();var fast=new float[riders.Length];
        var left=Enumerable.Repeat(180f,riders.Length).ToArray();var right=Enumerable.Repeat(-180f,riders.Length).ToArray();
        var lastD=riders.Select(o=>o.distance).ToArray();
        var stationary=race.World.Items.First(o=>o.kind=="npc"&&(o.stand||o.sit));
        var station=new Vector2(stationary.lane,stationary.distance);
        for(int i=1;i<=330;i++){
            race.World.Refresh(0,i/60f);
            for(int j=0;j<riders.Length;j++){
                var skin=(SkinnedMeshRenderer)riders[j].Visual;var mesh=skin.sharedMesh;
                float swing=skin.GetBlendShapeWeight(mesh.GetBlendShapeIndex("ReachRight"))-skin.GetBlendShapeWeight(mesh.GetBlendShapeIndex("ReachLeft"));
                min[j]=Mathf.Min(min[j],swing);max[j]=Mathf.Max(max[j],swing);
                float v=(riders[j].distance-lastD[j])*60;lastD[j]=riders[j].distance;
                var ground=race.World.SnowSurface(riders[j].distance,riders[j].lane);
                Require(Vector3.Distance(ground,skin.transform.position)<.02f,"moving equipment lifted off the beginner piste");
                float face=Mathf.DeltaAngle(0,(Quaternion.Inverse(race.World.SnowFrame(riders[j].distance,riders[j].lane))*skin.transform.rotation).eulerAngles.y);
                slow[j]=Mathf.Min(slow[j],v);fast[j]=Mathf.Max(fast[j],v);left[j]=Mathf.Min(left[j],face);right[j]=Mathf.Max(right[j],face);
            }
        }
        for(int j=0;j<riders.Length;j++){
            Require(max[j]-min[j]>55,"fixed arm pose on downhill "+riders[j].variant);
            Require(fast[j]-slow[j]>.1f&&right[j]-left[j]>12,"constant tempo or heading on "+riders[j].variant);
            var mesh=((SkinnedMeshRenderer)riders[j].Visual).sharedMesh;
            var reach=new Vector3[mesh.vertexCount];mesh.GetBlendShapeFrameVertices(mesh.GetBlendShapeIndex("ReachRight"),0,reach,null,null);
            var verts=mesh.vertices;
            Require(reach.Any(v=>v.magnitude>.07f),riders[j].variant+" arm or pole swing too small");
            for(int k=0;k<verts.Length;k++)if(verts[k].y<.14f&&Mathf.Abs(verts[k].x)<.36f)
                Require(reach[k].sqrMagnitude<.00001f,"arm action deformed downhill equipment");
        }
        Require(board.distance>start+1 && Mathf.Abs(board.lane-x)>.03f,"Distant rider waited for the player's camera");
        Require(station==new Vector2(stationary.lane,stationary.distance),"side group left its station");
        race.StartRace("{\"mode\":\"normal\",\"seed\":1911}");
        RaceItem tube=null;
        for(int d=100;d<1600&&tube==null;d+=12){
            race.World.Refresh(d,d/14f);
            tube=race.World.Items.FirstOrDefault(o=>o.kind=="tube"&&o.distance>d+56);
            if(tube==null)continue;
            float at=tube.distance;
            var seat=(SkinnedMeshRenderer)tube.Visual;int carve=seat.sharedMesh.GetBlendShapeIndex("Carve");
            float initialYaw=seat.transform.eulerAngles.y,turn=0,lo=100,hi=-100;
            for(int i=1;i<=240;i++){
                race.World.Refresh(d,d/14f+i/30f);
                turn=Mathf.Max(turn,Mathf.Abs(Mathf.DeltaAngle(initialYaw,seat.transform.eulerAngles.y)));
                lo=Mathf.Min(lo,seat.GetBlendShapeWeight(carve));hi=Mathf.Max(hi,seat.GetBlendShapeWeight(carve));
                Require(Vector3.Distance(seat.transform.position,race.World.SnowSurface(tube.distance,tube.lane))<.02f,"tube floating during its traverse");
            }
            Require(turn>5&&hi-lo>15,"tube glided without steering and bracing");
            Require(tube.distance>at+.8f,"Distant tube stopped before entering the lane");
        }
        Require(tube!=null,"Missing early tube approach");
        race.StartRace("{\"mode\":\"tubing\",\"seed\":1911}");
        race.BeginSteer();race.DragSteer(.21f);for(int i=0;i<28;i++)race.Advance(1f/60);
        var root=race.transform.Find("Selected rider");
        var rider=root.GetComponentsInChildren<MeshRenderer>().First(s=>s.name.StartsWith("playerTube"));
        Require(race.MenuActor.gameObject.activeInHierarchy&&race.MenuActor.name=="OwlMenu","Missing selected owl on the inflatable");
        Require(Vector3.Angle(race.MenuActor.up,root.up)>2,"Steering did not lean the selected rider");
        if(capture){
            Shot(race.View,"02-bonus-chase");
            var t=rider.transform;var at=t.TransformPoint(new Vector3(0,.72f,0));
            var eye=t.TransformPoint(new Vector3(1.9f,1.55f,-2.9f));
            race.View.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(at-eye,Vector3.up));
            Shot(race.View,"03-rider-fit");
        }
        Debug.Log("RIDER COURSE PASS seed 1911: owl leading edge "+downhill.ToString("F2")+
            ", board at "+start.ToString("F1")+" m, plus independent tubing approach and seated steering");
        WeatherFrontChecks.Verify(race);
        race.Menu();
    }
}
