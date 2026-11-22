using UnityEngine;
using UnityEngine.Rendering;

public partial class MountainRace {
    Light sun,headlight;Material raceSky,flakePaint;
    readonly Light[] nightLamps=new Light[4];
    ParticleSystem flakes,fireworks;
    int tubePanoramaPair=-1,previousSkyFrom=-1,previousSkyTo=-1;
    float previousSkyMix=-1,weatherMetre=float.NaN,festivalVisibility;
    Texture2D[] tubePanoramas;

    readonly struct SkyPalette {
        public readonly Color horizon,zenith,sun,tint;
        public readonly float sunlight,ambient;
        public SkyPalette(Color horizon,Color zenith,Color sun,Color tint,float sunlight,float ambient){
            this.horizon=horizon;this.zenith=zenith;this.sun=sun;this.tint=tint;this.sunlight=sunlight;this.ambient=ambient;
        }
    }
    static readonly SkyPalette[] skies={
        new SkyPalette(new Color(.82f,.9f,.97f),new Color(.38f,.67f,.91f),new Color(1,.96f,.9f),Color.white,.78f,.79f),
        new SkyPalette(new Color(.99f,.77f,.66f),new Color(.39f,.60f,.83f),new Color(1,.82f,.69f),new Color(.98f,.86f,.87f),.83f,.73f),
        new SkyPalette(new Color(.76f,.81f,.85f),new Color(.59f,.67f,.74f),new Color(.85f,.91f,1),new Color(.74f,.82f,.88f),.57f,.74f),
        new SkyPalette(new Color(.027f,.052f,.103f),new Color(.007f,.014f,.042f),new Color(.43f,.58f,.81f),new Color(.105f,.17f,.29f),.17f,.18f),
        new SkyPalette(new Color(.96f,.67f,.68f),new Color(.33f,.36f,.57f),new Color(1,.78f,.69f),new Color(.86f,.72f,.81f),.76f,.70f),
        new SkyPalette(new Color(.73f,.84f,.94f),new Color(.18f,.45f,.72f),new Color(1,.91f,.78f),new Color(.93f,.97f,1),.88f,.72f)
    };
    const int ClearSky=0,ParkSky=1,StormSky=2,NightSky=3,HolidaySky=4,PowderSky=5;
    static readonly int[] stageSky={ClearSky,ClearSky,ClearSky,ParkSky,StormSky,ClearSky,NightSky,HolidaySky,PowderSky};

    void WeatherFront(out int from,out int to,out float mix){
        from=to=ClearSky;mix=1;
        if(CourseMode=="tubing"){
            to=ParkSky;mix=StepBlend(Mathf.InverseLerp(900,1470,Status.metres));return;
        }
        if(IsMenu||CourseMode!="normal")return;
        // The front rolls in 220 m before its arch; the light and visibility settle 80 m after it.
        int next=Mathf.FloorToInt((Status.metres+220)/RaceWorld.StageLength);
        if(next==0)return;
        from=stageSky[(next-1)%RaceWorld.StageCount];to=stageSky[next%RaceWorld.StageCount];
        if(from!=to)mix=StepBlend(Mathf.InverseLerp(-220,80,Status.metres-next*RaceWorld.StageLength));
    }
    static float WeatherWeight(int sky,int from,int to,float t)=>Mathf.Lerp(from==sky?1:0,to==sky?1:0,t);

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
        headlight.type=LightType.Spot;headlight.spotAngle=65;headlight.innerSpotAngle=32;headlight.range=31;headlight.color=new Color(1,.89f,.72f);
        for(int i=0;i<nightLamps.Length;i++){
            var lamp=new GameObject("Night lamp pool").AddComponent<Light>();
            lamp.transform.SetParent(transform,false);lamp.type=LightType.Point;lamp.range=15;
            lamp.color=new Color(1,.76f,.46f);lamp.enabled=false;nightLamps[i]=lamp;
        }
        flakePaint=new Material(Resources.Load<Shader>("MountainRaceFlake"));
        flakes=Particles("Windblown snow",760);fireworks=Particles("Distant fireworks",FestivalParticleLimit);
        fireworks.GetComponent<ParticleSystemRenderer>().sharedMaterial=new Material(Resources.Load<Shader>("FestivalSpark"));
        flakes.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=flakes.main;main.simulationSpace=ParticleSystemSimulationSpace.Local;
        main.startSize=new ParticleSystem.MinMaxCurve(.05f,.135f);
        main.startLifetime=new ParticleSystem.MinMaxCurve(.9f,1.55f);main.startSpeed=0;main.startColor=new Color(.97f,.99f,1,.88f);
        var shape=flakes.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(15,7,16);
        var wind=flakes.velocityOverLifetime;wind.enabled=true;wind.space=ParticleSystemSimulationSpace.Local;
        wind.x=-.9f;wind.y=-2.8f;wind.z=-1;flakes.Play(false);
        var renderer=flakes.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Stretch;
        renderer.lengthScale=1.25f;renderer.velocityScale=.012f;renderer.minParticleSize=.0015f;renderer.maxParticleSize=.025f;
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
        bool tubeMode=CourseMode=="tubing",sledMode=CourseMode=="sled";
        raceSky.SetFloat("_Menu",IsMenu?1:0);raceSky.SetFloat("_Tube",tubeMode?1:0);
        if(tubeMode){
            if(tubePanoramas==null)tubePanoramas=new[]{Resources.Load<Texture2D>("Art/Race-tubing-62a"),
                Resources.Load<Texture2D>("Art/Race-tubing-62b"),Resources.Load<Texture2D>("Art/Race-tubing-62c")};
            bool coast=Status.metres>=860;
            int pair=coast?1:0;
            if(pair!=tubePanoramaPair){
                raceSky.SetTexture("_Route",tubePanoramas[pair]);raceSky.SetTexture("_RouteNext",tubePanoramas[pair+1]);
                tubePanoramaPair=pair;
            }
            float progress=coast?Mathf.InverseLerp(1020,1360,Status.metres):Mathf.InverseLerp(420,740,Status.metres);
            raceSky.SetFloat("_RouteBlend",Mathf.SmoothStep(0,1,progress));
        }
        float yaw=World.HeadingAt(Status.metres)*Mathf.Rad2Deg;
        raceSky.SetVector("_Forward",tubeMode?Vector3.forward:Quaternion.Euler(0,yaw*.75f,0)*Vector3.forward);raceSky.SetFloat("_Clock",Status.elapsed);
        WeatherFront(out int from,out int to,out float mix);
        float night=WeatherWeight(NightSky,from,to,mix),storm=WeatherWeight(StormSky,from,to,mix);
        Status.blizzard=storm;
        float holiday=WeatherWeight(HolidaySky,from,to,mix);
        if(holiday==0&&festivalVisibility>0)fireworks.Clear();
        festivalVisibility=holiday;
        bool skyChanged=from!=previousSkyFrom||to!=previousSkyTo||mix!=previousSkyMix;
        if(skyChanged){
            if((previousSkyTo==PowderSky)!=(to==PowderSky))
                sun.transform.rotation=to==PowderSky?Quaternion.Euler(42,-109,0):Quaternion.Euler(38,-38,0);
            previousSkyFrom=from;previousSkyTo=to;previousSkyMix=mix;
            raceSky.SetFloat("_Powder",to==PowderSky?1:0);
            var a=skies[from];var b=skies[to];
            Color horizon=Color.Lerp(a.horizon,b.horizon,mix);
            sun.color=Color.Lerp(a.sun,b.sun,mix);sun.intensity=Mathf.Lerp(a.sunlight,b.sunlight,mix);
            RenderSettings.ambientLight=new Color(.75f,.84f,.96f)*Mathf.Lerp(a.ambient,b.ambient,mix);
            RenderSettings.fogColor=horizon;raceSky.SetColor("_Horizon",horizon);
            raceSky.SetColor("_Zenith",Color.Lerp(a.zenith,b.zenith,mix));raceSky.SetColor("_Tint",Color.Lerp(a.tint,b.tint,mix));
            raceSky.SetFloat("_Storm",storm);raceSky.SetFloat("_Night",night);Shader.SetGlobalFloat("_OwlNight",night);
        }
        var forward=Vector3.ProjectOnPlane(View.transform.forward,Vector3.up).normalized;
        var orientation=Quaternion.LookRotation(forward);
        float gust=.75f*Mathf.Sin(Status.elapsed*.55f)+.25f*Mathf.Sin(Status.elapsed*1.7f);
        if(skyChanged||storm>0){
            var emission=flakes.emission;var wind=flakes.velocityOverLifetime;
            float strength=Mathf.Abs(gust);
            emission.rateOverTime=Mathf.Lerp(5+18*holiday,310+190*strength,storm);
            wind.x=Mathf.Lerp(-.9f,gust*11,storm);wind.y=Mathf.Lerp(-2.8f,-3.5f-strength,storm);
            wind.z=Mathf.Lerp(-2,-Mathf.Clamp(speed*.67f,6,17),storm);
            RenderSettings.fogStartDistance=Mathf.Lerp(Mathf.Lerp(52,12,night),16-5*strength,storm);
            RenderSettings.fogEndDistance=to==PowderSky?239:Mathf.Lerp(Mathf.Lerp(185,59,night),58-13*strength,storm);
        }
        flakes.transform.SetPositionAndRotation(View.transform.position+forward*9+Vector3.up-
            orientation*Vector3.right*(storm*gust*3),orientation);
        if(float.IsNaN(weatherMetre)||Mathf.Abs(weatherMetre-Status.metres)>45){
            flakes.Simulate(Mathf.Lerp(.2f,.85f,storm),false,true,false);flakes.Play(false);
        }
        weatherMetre=Status.metres;
        var position=World.SampleGround(Status.metres,lane);
        headlight.enabled=!IsMenu&&(night>.001f||sledMode);
        if(headlight.enabled){headlight.intensity=sledMode?19:14*night;headlight.range=sledMode?31:19;headlight.spotAngle=sledMode?65:55;
            var origin=position+Vector3.up*1.55f;
            headlight.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(World.SampleGround(Status.metres+13,lane)-origin));}
        int firstRow=Mathf.FloorToInt((Status.metres+8)/36);
        for(int i=0;i<nightLamps.Length;i++){
            var lamp=nightLamps[i];lamp.enabled=night>.001f&&!IsMenu;
            if(!lamp.enabled)continue;
            float distance=(firstRow+i/2)*36, ahead=distance-Status.metres;
            float intensity=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-9,-2,ahead))*
                Mathf.SmoothStep(0,1,Mathf.InverseLerp(44,28,ahead));
            lamp.transform.position=World.SampleGround(distance,(i%2==0?-1:1)*10.9f)+Vector3.up*4.5f;
            lamp.intensity=18*intensity*night;
        }
        foreach(var item in World.Items){
            if(item.kind!="cat"||!item.obj||item.consumed)continue;
            if(!item.obj.TryGetComponent<GroomerOptics>(out var optics)){
                optics=item.obj.AddComponent<GroomerOptics>();optics.Fit(item.variant);
            }
            optics.IlluminateRoad(night*Mathf.InverseLerp(62,52,Mathf.Abs(item.distance-Status.metres)));
        }
        if(festivalVisibility>0){festivalClock=Status.elapsed;DrawFestival(festivalClock);}
    }
}
