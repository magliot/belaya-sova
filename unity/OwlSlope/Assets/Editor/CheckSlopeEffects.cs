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
    public static void CourseMarkers(MountainRace race,string capture=null){
        race.StartRace("{\"mode\":\"review\",\"seed\":1911}");
        var camera=race.View;float oldAspect=camera.aspect;camera.aspect=.5f;
        try{
            void Frame(string name){if(!string.IsNullOrEmpty(capture))RiderCourseChecks.Shot(camera,name,capture,390,780);}
            foreach(float metre in new[]{83.5f,208.5f}){
                race.Seek(metre-14);
                var gun=race.World.Items.Find(o=>o.kind=="cannon"&&Mathf.Abs(o.distance-metre)<.1f);
                var spray=gun?.Visual?.GetComponentInChildren<ParticleSystem>();
                Need(spray&&spray.isPlaying&&spray.emission.rateOverTime.constant>=150,"Inactive training cannon at "+metre);
                Need(Vector3.Dot(spray.transform.forward,race.World.SnowSurface(metre,0)-gun.Visual.transform.position)>3,
                    "Training cannon sprays away from the piste");
            }
            Frame("training-snow");
            race.Seek(280);
            Need(race.World.Items.Exists(o=>o.sit&&Mathf.Abs(o.distance-289)<2)&&race.World.Items.Exists(o=>o.kind=="aid"&&Mathf.Abs(o.distance-289)<2),"Missing patient or first aid");
            Need(!race.World.transform.Find("Карман для вызова патруля"),"Separate patrol sign on piste");
            race.Seek(308);Frame("training-sightline");
            float xLeft=camera.WorldToViewportPoint(race.World.SnowSurface(318,-6)).x;
            float xRight=camera.WorldToViewportPoint(race.World.SnowSurface(318,6)).x;
            Need(xLeft>0&&xRight<1,"12 m lateral corridor missing on 390x780: "+xLeft+".."+xRight);
            race.RideFromReview();race.BeginSteer();race.DragSteer(.27f);
            for(int i=0;i<21;i++){
                race.Advance(1f/60);
                var crossing=camera.WorldToViewportPoint(race.World.SnowSurface(race.Status.metres+10,race.Lane+3));
                // The 21b camera lets the rider approach the edge during a hard traverse.
                // Its framing is checked against the original recordings in ChaseCameraChecks.
                Need(crossing.z>0&&crossing.x>0&&crossing.x<1,"Upcoming crossing left the portrait frame during a fast side-step");
            }
            race.StartRace("{\"mode\":\"review\",\"seed\":1911}");race.Seek(15455);
            int air=0,ground=0,rings=0;
            foreach(var item in race.World.Items){
                if(item.kind!="coin"&&item.kind!="airRing")continue;
                var glow=item.Visual.transform.Find("Night glow");
                Need(glow&&glow.gameObject.activeInHierarchy&&glow.GetComponent<Renderer>().sharedMaterial.shader.isSupported,"Night target glow missing: "+item.kind);
                if(item.kind=="airRing")rings++;else if(item.air)air++;else ground++;
            }
            Need(rings>0&&air>0&&ground>0,"Night ring and fruit fixture incomplete");Frame("night-ring-approach");
            race.Seek(15476);Frame("night-ring");
            race.Seek(308);
            foreach(var item in race.World.Items){
                if(item.kind!="coin")continue;
                var glow=item.Visual.transform.Find("Night glow");Need(!glow||!glow.gameObject.activeSelf,"Pooled night halo shown during day");
            }
            Debug.Log("SIGHTLINE PASS 390x780 12-m corridor at 318 m: "+xLeft.ToString("F2")+".."+xRight.ToString("F2")+
                "; cannons 83.5/208.5; night at 15455 m "+ground+" ground/"+air+" air fruit and "+rings+" rings");
        }finally{camera.aspect=oldAspect;}
    }
    static void Check(){
        if(!EditorApplication.isPlaying||!SessionState.GetBool(Pending,false))return;
        var race=UnityEngine.Object.FindFirstObjectByType<MountainRace>();if(!race||race.World==null)return;
        SessionState.EraseBool(Pending);
        try{
            CourseMarkers(race,Environment.GetEnvironmentVariable("SOVA_EFFECTS_CAPTURE"));
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
