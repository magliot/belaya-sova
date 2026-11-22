using UnityEngine;
using UnityEngine.Rendering;

public partial class RaceWorld {
    // The guide line runs through the middle of each pair of fountains.
    public float HolidayLine(float metres){
        float local=Mathf.Repeat(metres,StageLength);
        int visit=Mathf.FloorToInt(metres/LapLength);
        float direction=Roll(seed,visit,718)<.5f?-1:1;
        float amplitude=3.65f+.62f*Mathf.Sin(local/95+Roll(seed,visit,719)*6.28f);
        float turn=Mathf.Sin(local*Mathf.PI/50);
        // Keep the outside shoulder clear for braking and calling patrol beside the aid station.
        if(Mathf.FloorToInt(local/50)%6==2)amplitude=Mathf.Lerp(amplitude,2,turn*turn);
        return direction*amplitude*turn;
    }

    void Holiday(float y,int row){
        float start=y*.05f,apex=start+25,outer=Mathf.Sign(HolidayLine(apex));
        float centre=HolidayLine(apex),halfSpan=3.15f+.38f*Roll(seed,Mathf.FloorToInt(start),723);
        foreach(int side in sides){
            var pot=Add("festivalFountain",apex,centre+side*halfSpan,1.04f,.78f);
            pot.side=side;pot.length=1.04f;
        }
        Add("gate",apex,centre,2*(halfSpan-.52f),0,"fountainGate");
        for(int i=0;i<6;i++){
            float at=start+5+8*i,x=HolidayLine(at);
            if(row%6==2&&i==4){
                Add("aid",at,x,1.2f,.85f);
                Person(200+20*(x+outer*2.45f),20*(at+1),0,"sitter",0,0);
            }else if(row%2==0&&i==3)Add("gift",at,x,1.2f,.85f);
        }
        if(row%5==1){
            float at=start+37;
            Add(row%10==1?"thermos":"helmet",at,HolidayLine(at)-outer*4.1f,1.2f,.85f);
        }
        var fir=Add("tree",start+40,-outer*7.8f,1.4f,4.2f,"holidayFir");
        fir.yaw=row*57;fir.scale=1.17f+.19f*Roll(seed,Mathf.FloorToInt(start),720);fir.offset=-.09f;
        int scene=(row+Mathf.FloorToInt(start/LapLength))%7;
        float xEdge=200+outer*151;
        switch(scene){
            case 0:case 3:Person(xEdge,y+160,55,scene==0?"skier":"snowboarder",13,row*.81f);break;
            case 1:case 5:Snowman(xEdge,y+200);break;
            case 2:Person(200-outer*154,y+200,43,"kid",9,row);break;
            case 4:Entry("dog",200+outer*198,y+170,24,.8f);break;
            case 6:Rock(xEdge,y+220,true,15);break;
        }
    }

    void PoseTippedFountain(RaceItem pot,float elapsed){
        float t=Smooth((elapsed-pot.fountainHitAt)/.42f);
        float lane=pot.originX+pot.side*.55f*t,d=pot.originD+.45f*t;
        Quaternion snow=SnowFrame(d,lane);
        pot.obj.transform.SetPositionAndRotation(SnowSurface(d,lane)+snow*Vector3.up*(.30f*t),
            snow*Quaternion.Euler(12*t,0,-pot.side*78*t));
    }

    static Material fountainSpark;
    void StartFountain(RaceItem item){
        var particles=item.obj.GetComponentInChildren<ParticleSystem>();
        if(!particles){
            var nozzle=new GameObject("Праздничные искры");nozzle.transform.SetParent(item.obj.transform,false);
            nozzle.transform.localPosition=Vector3.up*.77f;nozzle.transform.localRotation=Quaternion.Euler(-90,0,0);
            particles=nozzle.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.duration=2;main.loop=true;
            main.simulationSpace=ParticleSystemSimulationSpace.Local;main.maxParticles=78;
            main.startLifetime=new ParticleSystem.MinMaxCurve(.48f,.86f);main.startSpeed=new ParticleSystem.MinMaxCurve(4.1f,6.6f);
            main.startSize=new ParticleSystem.MinMaxCurve(.09f,.17f);main.gravityModifier=.81f;
            var cone=particles.shape;cone.enabled=true;cone.shapeType=ParticleSystemShapeType.Cone;cone.radius=.095f;cone.angle=15;
            var emission=particles.emission;emission.rateOverTime=84;
            var shrink=particles.sizeOverLifetime;shrink.enabled=true;
            shrink.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,.18f));
            var renderer=particles.GetComponent<ParticleSystemRenderer>();
            if(!fountainSpark)fountainSpark=new Material(Resources.Load<Shader>("FestivalSpark"));
            renderer.sharedMaterial=fountainSpark;renderer.renderMode=ParticleSystemRenderMode.Stretch;
            renderer.velocityScale=.044f;renderer.lengthScale=1.20f;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        particles.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.useAutoRandomSeed=false;particles.randomSeed=(uint)Mathf.FloorToInt(item.distance)+1;
        var color=particles.main;
        color.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.9f,.37f),item.side>0?new Color(1,.27f,.45f):new Color(.22f,1,.94f));
        particles.Simulate(.85f,false,true,false);particles.Play(false);item.spray=particles;
    }
}
