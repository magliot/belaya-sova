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
        for(int i=1;i<=60;i++)race.World.Refresh(0,i/60f);
        Require(board.distance>start+1 && Mathf.Abs(board.lane-x)>.03f,"Distant rider waited for the player's camera");
        race.StartRace("{\"mode\":\"normal\",\"seed\":1911}");
        RaceItem tube=null;
        for(int d=100;d<1600&&tube==null;d+=12){
            race.World.Refresh(d,d/14f);
            tube=race.World.Items.FirstOrDefault(o=>o.kind=="tube"&&o.distance>d+56);
            if(tube==null)continue;
            float at=tube.distance;
            for(int i=1;i<=30;i++)race.World.Refresh(d,d/14f+i/30f);
            Require(tube.distance>at+.8f,"Distant tube stopped before entering the lane");
        }
        Require(tube!=null,"Missing early tube approach");
        race.StartRace("{\"mode\":\"tubing\",\"seed\":1911}");
        race.BeginSteer();race.DragSteer(.21f);for(int i=0;i<28;i++)race.Advance(1f/60);
        var rider=race.transform.Find("Selected rider").GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.sharedMesh.name.StartsWith("tube"));
        Require(rider.GetBlendShapeWeight(rider.sharedMesh.GetBlendShapeIndex("Carve"))>3,"Steering did not lean the seated rider");
        Require(rider.sharedMaterials.Any(m=>m.GetTexture("_BaseMap")&&m.GetTexture("_BaseMap").name.Contains("jacket")),"Missing original upholstered rider texture");
        if(capture){
            Shot(race.View,"02-bonus-chase");
            var t=rider.transform;var at=t.TransformPoint(new Vector3(0,.72f,0));
            var eye=t.TransformPoint(new Vector3(1.9f,1.55f,-2.9f));
            race.View.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(at-eye,Vector3.up));
            Shot(race.View,"03-rider-fit");
        }
        Debug.Log("RIDER COURSE PASS seed 1911: owl leading edge "+downhill.ToString("F2")+
            ", board at "+start.ToString("F1")+" m, plus independent tubing approach and seated steering");
        race.Menu();
    }
}
