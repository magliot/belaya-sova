using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CaptureMountainRace {
    const string Pending="Owl.Mountain.Capture";
    const int W=390,H=780;
    static CaptureMountainRace(){EditorApplication.update+=WhenReady;}
    public static void Run(){
        SessionState.SetBool(Pending,true);
        EditorSceneManager.OpenScene("Assets/Mountain.unity");EditorApplication.EnterPlaymode();
    }
    static void Save(MountainRace race,string file){
        var cam=race.View;float aspect=cam.aspect;
        var prev=RenderTexture.active;var oldTarget=cam.targetTexture;
        var rt=RenderTexture.GetTemporary(W,H,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,1);
        Texture2D pixels=null;
        try{
            cam.aspect=(float)W/H;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
            pixels=new Texture2D(W,H,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,W,H),0,0);pixels.Apply();
            if(race.IsMenu){
                var actor=race.MenuActor;var ground=race.World.SampleGround(0,0);
                foreach(var sr in actor.GetComponentsInChildren<SkinnedMeshRenderer>()){
                    var mesh=new Mesh();sr.BakeMesh(mesh);float low=float.PositiveInfinity,high=float.NegativeInfinity;
                    var basis=Matrix4x4.TRS(sr.transform.position,sr.transform.rotation,Vector3.one);
                    foreach(var v in mesh.vertices){float h=basis.MultiplyPoint3x4(v).y;low=Mathf.Min(low,h);high=Mathf.Max(high,h);}
                    Debug.Log("MOUNTAIN FIT "+race.Hero+" mesh "+sr.name+" ground "+ground.y+" actorLocal "+actor.localPosition+
                        " skin height "+low+".."+high);
                    UnityEngine.Object.Destroy(mesh);
                }
            }
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../course-checks/mountain-race/editor"));
            Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,file+".png"),pixels.EncodeToPNG());
            Debug.Log("MOUNTAIN FRAME "+file+" "+race.Hero+" "+race.Status.metres.ToString("F1")+"m altitude "+race.Altitude.ToString("F2"));
        }finally{
            RenderTexture.active=prev;cam.targetTexture=oldTarget;cam.aspect=aspect;RenderTexture.ReleaseTemporary(rt);
            if(pixels)UnityEngine.Object.Destroy(pixels);
        }
    }
    static void Checkpoint(MountainRace race,float d,float x){
        typeof(MountainRace).GetMethod("Checkpoint",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(race,new object[]{d,x});
        // Re-anchor the actual chase camera to the checkpoint, as the menu does at the start.
        typeof(MountainRace).GetMethod("RenderRace",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(race,new object[]{0f,true});
    }
    static void AirFrame(MountainRace race,string hero){
        race.Menu();race.ChooseHero(hero);race.StartRace("{\"mode\":\"normal\",\"seed\":1911,\"sensitivity\":4}");
        Checkpoint(race,7620,0);var ramp=race.World.Items.Find(o=>o.kind=="ramp"&&o.prizeLine);
        Checkpoint(race,ramp.distance-ramp.length*.5f-1,ramp.lane);
        for(int i=0;i<180&&race.Altitude<ramp.height+.15f;i++)race.Advance(1f/60);
        if(race.Altitude<ramp.height+.15f)throw new Exception("No launch to photograph");
        race.AirTrick("tap");
        for(int i=0;i<(hero=="bear"?10:12);i++)race.Advance(1f/60);
        Save(race,hero=="bear"?"05-bear-shifty":"06-owl-indy");
    }
    static void WhenReady(){
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||!SessionState.GetBool(Pending,false))return;
        var race=UnityEngine.Object.FindFirstObjectByType<MountainRace>();if(!race||!race.View)return;
        SessionState.EraseBool(Pending);
        try{
            race.Menu();race.ChooseHero("bear");Save(race,"01-menu-bear");
            race.ChooseHero("owl");Save(race,"02-menu-owl");
            race.ChooseHero("bear");race.StartRace("{\"mode\":\"normal\",\"seed\":1911,\"sensitivity\":4}");
            race.BeginSteer();for(int i=0;i<67;i++)race.Advance(1f/60);Save(race,"03-training");
            Checkpoint(race,2474,0);race.Advance(1f/60);Save(race,"04-forest-entry");
            AirFrame(race,"bear");AirFrame(race,"owl");Debug.Log("MOUNTAIN CAPTURE PASS");EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e.InnerException??e);EditorApplication.Exit(2);}
    }
}
