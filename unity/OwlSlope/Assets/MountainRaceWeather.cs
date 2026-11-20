using UnityEngine;
using UnityEngine.Rendering;

public partial class MountainRace {
    Light sun,headlight;Material raceSky,flakePaint;
    ParticleSystem flakes,fireworks;
    int weatherZone=-1;float nextBurst;string backdrop="";

    void SetupWeather(){
        raceSky=new Material(Resources.Load<Shader>("MountainRaceSky"));
        raceSky.SetTexture("_MenuArt",Resources.Load<Texture2D>("Art/Race-menu"));
        raceSky.SetTexture("_Ridge",Resources.Load<Texture2D>("Art/Race-ridge-52b"));
        raceSky.SetTexture("_LeftRidge",Resources.Load<Texture2D>("Art/Race-ridge-52a"));
        raceSky.SetTexture("_RightRidge",Resources.Load<Texture2D>("Art/Race-ridge-52c"));
        raceSky.SetTexture("_Aurora",Resources.Load<Texture2D>("Art/Race-aurora-54b"));
        raceSky.SetTexture("_AuroraA",Resources.Load<Texture2D>("Art/Race-aurora-54a"));
        raceSky.SetTexture("_AuroraC",Resources.Load<Texture2D>("Art/Race-aurora-54c"));
        RenderSettings.skybox=raceSky;View.clearFlags=CameraClearFlags.Skybox;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
        sun=new GameObject("Alpine sunlight").AddComponent<Light>();sun.transform.SetParent(transform,false);
        sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(38,-38,0);RenderSettings.sun=sun;
        headlight=new GameObject("Headlights").AddComponent<Light>();headlight.transform.SetParent(transform,false);
        headlight.type=LightType.Spot;headlight.spotAngle=53;headlight.range=33;headlight.color=new Color(1,.87f,.65f);
        flakePaint=new Material(Resources.Load<Shader>("MountainRaceFlake"));
        flakes=Particles("Falling snow",270);fireworks=Particles("Distant fireworks",420);
        var main=flakes.main;main.startSize=new ParticleSystem.MinMaxCurve(.035f,.09f);
        main.startLifetime=1.7f;main.startSpeed=0;main.startColor=new Color(.97f,.99f,1,.8f);
        var shape=flakes.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(16,7,20);
        var wind=flakes.velocityOverLifetime;wind.enabled=true;wind.space=ParticleSystemSimulationSpace.World;
        wind.x=-.9f;wind.y=-2.8f;wind.z=-1;
    }
    ParticleSystem Particles(string name,int count){
        var go=new GameObject(name);go.transform.SetParent(transform,false);
        var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.playOnAwake=false;main.maxParticles=count;main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startSpeed=0;var shape=ps.shape;shape.enabled=false;var emission=ps.emission;emission.rateOverTime=0;
        var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=flakePaint;renderer.renderMode=ParticleSystemRenderMode.Billboard;
        ps.Play();return ps;
    }
    void UpdateWeather(){
        int stage=Status.stage;
        bool tubeMode=CourseMode=="tubing",sledMode=CourseMode=="sled";
        int zone=sledMode?5:tubeMode?(Status.metres>=742?1:0):stage==3?1:stage==4?2:stage==6?3:stage==7?4:0;
        raceSky.SetFloat("_Menu",IsMenu?1:0);raceSky.SetFloat("_Tube",tubeMode?1:0);
        if(tubeMode){
            string landscape="Race-tubing-62"+(Status.metres<335?"a":Status.metres<736?"b":"c");
            if(landscape!=backdrop){backdrop=landscape;raceSky.SetTexture("_Route",Resources.Load<Texture2D>("Art/"+landscape));}
        }
        float yaw=World.HeadingAt(Status.metres)*Mathf.Rad2Deg;
        raceSky.SetVector("_Forward",Quaternion.Euler(0,yaw*.75f,0)*Vector3.forward);raceSky.SetFloat("_Clock",Status.elapsed);
        if(zone!=weatherZone){
            weatherZone=zone;
            Color horizon,zenith,light,tint;float level,ambient;
            switch(zone){
            case 1:horizon=new Color(.99f,.77f,.66f);zenith=new Color(.39f,.60f,.83f);light=new Color(1,.82f,.69f);level=.83f;ambient=.73f;tint=new Color(.98f,.86f,.87f);break;
            case 2:horizon=new Color(.77f,.83f,.88f);zenith=new Color(.55f,.65f,.75f);light=new Color(.85f,.91f,1);level=.64f;ambient=.83f;tint=new Color(.8f,.86f,.90f);break;
            case 3:horizon=new Color(.17f,.26f,.41f);zenith=new Color(.035f,.08f,.20f);light=new Color(.63f,.78f,1);level=.54f;ambient=.56f;tint=new Color(.36f,.46f,.64f);break;
            case 4:horizon=new Color(.96f,.67f,.68f);zenith=new Color(.33f,.36f,.57f);light=new Color(1,.78f,.69f);level=.76f;ambient=.70f;tint=new Color(.86f,.72f,.81f);break;
            default:horizon=new Color(.82f,.9f,.97f);zenith=new Color(.38f,.67f,.91f);light=new Color(1,.96f,.9f);level=.78f;ambient=.79f;tint=Color.white;break;
            }
            sun.color=light;sun.intensity=level;RenderSettings.ambientLight=new Color(.75f,.84f,.96f)*ambient;
            RenderSettings.fogColor=horizon;RenderSettings.fogStartDistance=zone==2?22:52;RenderSettings.fogEndDistance=zone==2?100:185;
            raceSky.SetColor("_Horizon",horizon);raceSky.SetColor("_Zenith",zenith);raceSky.SetColor("_Tint",tint);raceSky.SetFloat("_Night",zone==3?1:0);
            var emission=flakes.emission;emission.rateOverTime=zone==2?125:zone==4?23:5;
            nextBurst=Status.elapsed+.22f;fireworks.Clear();
        }
        flakes.transform.position=View.transform.position+View.transform.forward*8+Vector3.up*2;
        var position=World.SampleGround(Status.metres,lane);
        headlight.enabled=!IsMenu&&(zone==3||sledMode);
        if(headlight.enabled){headlight.intensity=sledMode?3.7f:2.8f;
            headlight.transform.SetPositionAndRotation(position+Vector3.up*1.1f,Quaternion.LookRotation(World.SampleGround(Status.metres+12,lane)-position));}
        if(zone==4&&Status.elapsed>=nextBurst){
            float t=nextBurst;nextBurst=Status.elapsed+1.6f;
            var centre=World.SampleGround(Status.metres+66,Mathf.Sin(t*2.3f)*21)+Vector3.up*(15+4*Mathf.Sin(t));
            Color color=Color.HSVToRGB(Mathf.Repeat(t*.317f,.99f),.72f,1);
            for(int i=0;i<54;i++){
                float z=1-2*(i+.5f)/54, a=i*2.39996f,r=Mathf.Sqrt(1-z*z);
                var p=new ParticleSystem.EmitParams{position=centre,velocity=new Vector3(Mathf.Cos(a)*r,z,Mathf.Sin(a)*r)*(4+(i%3)*.7f),
                    startSize=.36f,startLifetime=.9f+.14f*(i%3),startColor=i%5==0?Color.white:color};
                fireworks.Emit(p,1);
            }
        }
    }
}
