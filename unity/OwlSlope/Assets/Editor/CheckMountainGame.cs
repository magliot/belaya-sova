using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch entry. Keep Play Mode active for Destroy(), skinned meshes, camera and game code.
[InitializeOnLoad]
public static class CheckMountainGame {
    const string pending="Owl.Mountain.Acceptance";
    static CheckMountainGame(){EditorApplication.update+=CheckWhenReady;}
    public static void Run(){
        SessionState.SetBool(pending,true);
        EditorSceneManager.OpenScene("Assets/Mountain.unity");
        EditorApplication.EnterPlaymode();
    }
    public static void Snowmen(){
        SessionState.SetBool("Owl.CheckSnowmen",true);Run();
    }
    static void CheckWhenReady(){
        if(EditorApplication.isPlaying&&!EditorApplication.isCompiling&&SessionState.GetBool(pending,false)
            &&UnityEngine.Object.FindFirstObjectByType<MountainRace>()?.View!=null)
            Validate();
    }
    static void Require(bool condition,string detail){if(!condition)throw new Exception(detail);}
    static void Validate(){
        SessionState.EraseBool(pending);
        try{
            var race=UnityEngine.Object.FindFirstObjectByType<MountainRace>();
            race.Initialize();
            if(SessionState.GetBool("Owl.CheckSnowmen",false)){
                SessionState.EraseBool("Owl.CheckSnowmen");
                Debug.Log("SNOWMEN PASS "+race.VerifySnowmen());EditorApplication.Exit(0);return;
            }
            ShopBalanceChecks.Run(race);
            foreach(string hero in new[]{"bear","owl"}){
                race.Menu();race.ChooseHero(hero);
                Require(race.MenuActor&&race.MenuActor.gameObject.activeInHierarchy,"Missing menu actor "+hero);
                race.StartRace("{\"mode\":\"normal\",\"seed\":1911,\"helmets\":1}");
                Require(race.Hero==hero&&race.World.Items.Any(o=>o.kind=="npc"),"Normal menu entry failed for "+hero);
                var people=race.World.Items.Where(o=>o.kind=="npc").ToArray();
                Require(people.Any(o=>o.sb)&&people.Any(o=>!o.sb),"Skiers and boarders missing from normal start");
                race.BeginSteer();race.DragSteer(.1f);
                for(int i=0;i<36;i++)race.Advance(1f/60);
                race.ReleaseSteer();
                Require(race.Lane>.5f&&race.Status.metres>4,"Drag failed for "+hero);
                race.PauseRide();float paused=race.Status.metres;race.Advance(.5f);
                Require(race.Status.phase=="pause"&&Mathf.Approximately(paused,race.Status.metres),"Pause advanced");
                race.ResumeRide();race.Advance(.1f);Require(race.Status.metres>paused,"Resume failed");
                race.RestartRide();Require(race.Status.phase=="play"&&race.Status.metres<.01f,"Restart failed");
                HeroMotionChecks.OnSnow(race,hero);
                HeroMotionChecks.OnAir(race,hero);
            }
            // Tests are compiled only for the Editor and exercise the same native generators and contacts.
            var worldChecks=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RaceWorldChecks")).First(t=>t!=null);
            worldChecks.GetMethod("RunAt",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{race.World});
            Debug.Log("MOUNTAIN WORLD CHECKS PASS");
            var verdict=typeof(MountainRace).GetMethod("VerifyMechanics").Invoke(race,null);
            Debug.Log("MOUNTAIN GAME CHECKS PASS "+verdict);
            CourseRideChecks.Start(race);
        }catch(Exception e){Debug.LogException(e.InnerException??e);EditorApplication.Exit(2);}
    }
}
