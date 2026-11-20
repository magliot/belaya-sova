using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CheckSlopeEffects {
    const string Pending="Owl.SlopeEffects";
    static CheckSlopeEffects(){EditorApplication.update+=Check;}
    public static void Run(){
        SessionState.SetBool(Pending,true);EditorSceneManager.OpenScene("Assets/Mountain.unity");EditorApplication.EnterPlaymode();
    }
    static void Need(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
    static void Check(){
        if(!EditorApplication.isPlaying||!SessionState.GetBool(Pending,false))return;
        var race=UnityEngine.Object.FindFirstObjectByType<MountainRace>();if(!race||race.World==null)return;
        SessionState.EraseBool(Pending);
        try{
            race.StartRace("{\"mode\":\"review\",\"seed\":1911}");race.Seek(75);
            var cannon=race.World.Items.Find(o=>o.kind=="cannon"&&Mathf.Abs(o.distance-83.5f)<1);
            var spray=cannon?.Visual?.GetComponentInChildren<ParticleSystem>();
            Need(spray&&spray.isPlaying,"Missing running fan jet");
            var flakes=new ParticleSystem.Particle[200];int seen=spray.GetParticles(flakes);
            Need(seen>25,"Empty cannon cone after being pooled or sought");
            float reach=0;
            for(int i=0;i<seen;i++)reach=Mathf.Max(reach,flakes[i].position.z);
            Need(reach>4,"The plume must stream out of the fan across the road");
            Debug.Log("CANNON WEB MODEL seeded particles "+seen+" reach "+reach.ToString("F1")+"m");
            race.Seek(10104);Need(RenderSettings.fog&&RenderSettings.fogEndDistance>=40&&RenderSettings.fogEndDistance<=60,"Summit visibility");
            race.Seek(15220);
            var plough=race.World.Items.Find(o=>o.kind=="cat"&&Mathf.Abs(o.distance-race.Status.metres)<60);
            Need(plough!=null&&plough.Visual,"Missing night plough");
            var optics=plough.Visual.GetComponent<GroomerOptics>();Need(optics,"Missing fixed light housing");
            var beam=optics.GetComponentInChildren<Light>();
            Need(beam.enabled&&beam.type==LightType.Spot&&beam.range>=20&&beam.transform.localPosition.y>1,"Missing raised working headlights");
            var beacon=optics.transform.Find("Rotating amber beacon");
            Need(beacon&&Mathf.Abs(beacon.localPosition.y-3.83f)<.03f,"Amber light detached from original roof lens");
            Need(Vector3.Dot(beam.transform.forward,plough.Visual.transform.up)<0,"Floodlights point above the snow");
            Need(RenderSettings.fogEndDistance<61,"Night must hide the far road");
            race.Seek(17525);
            var festival=race.transform.Find("Distant fireworks").GetComponent<ParticleSystem>();
            int events=0;
            for(int i=0;i<45;i++){race.Seek(17525+i);if(festival.particleCount>30)events++;}
            Need(events>10,"Missing sustained holiday display");
            Debug.Log("SLOPE EFFECTS PASS: directed cannon, limited summit and night visibility, cab-mounted optics and festival");
            EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
    }
}
