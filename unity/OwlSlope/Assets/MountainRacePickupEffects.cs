using UnityEngine;
using UnityEngine.Rendering;

public partial class MountainRace {
    ParticleSystem mandarinStars,snowPuffs;

    ParticleSystem MakeBurst(string label,string shader){
        var go=new GameObject(label);go.transform.SetParent(transform,false);
        var ps=go.AddComponent<ParticleSystem>();
        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;
        main.playOnAwake=false;main.maxParticles=112;main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.gravityModifier=.45f;main.startSpeed=0;
        var emission=ps.emission;emission.enabled=false;
        var shape=ps.shape;shape.enabled=false;
        var size=ps.sizeOverLifetime;size.enabled=true;
        size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,.08f));
        var r=ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial=new Material(Resources.Load<Shader>(shader));r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        r.renderMode=ParticleSystemRenderMode.Billboard;
        return ps;
    }
    void Burst(RaceItem item,bool fruit){
        var ps=fruit?mandarinStars:snowPuffs;
        if(!ps){
            ps=fruit?MakeBurst("Мандарин · искры","MountainRaceSpark"):MakeBurst("Попадание снежка","MountainRaceFlake");
            if(fruit)mandarinStars=ps;else snowPuffs=ps;
        }
        var rider=World.SnowSurface(Status.metres,lane)+Vector3.up*(Altitude+1.06f);
        var origin=fruit&&item.Visual?item.Visual.transform.position+Vector3.up*.32f:rider;
        var forward=World.SnowFrame(Status.metres,lane)*Vector3.forward;
        var right=Vector3.Cross(Vector3.up,forward).normalized;
        int count=fruit?10:15;
        for(int i=0;i<count;i++){
            float angle=(i+.35f*Status.coins)*2.39996f;
            Vector3 spread=right*Mathf.Cos(angle)+Vector3.up*Mathf.Sin(angle);
            var p=new ParticleSystem.EmitParams{
                position=origin+(fruit?.33f:.10f)*spread,
                velocity=forward*(speed*(fruit?.70f:.82f))+spread*((fruit?2.35f:1.7f)+.21f*(i%4))+Vector3.up*1.1f,
                startLifetime=.36f+.035f*(i%6),startSize=(fruit?.36f:.17f)+.024f*(i%4),
                startColor=fruit?(i%3==0?new Color32(255,227,95,255):new Color32(255,142,29,255)):
                    (i%3==0?new Color32(145,208,248,235):new Color32(248,252,255,245)),
                rotation=i*31f
            };
            ps.Emit(p,1);
        }
    }
    void StepBursts(float dt){
        if(mandarinStars&&mandarinStars.particleCount>0)mandarinStars.Simulate(dt,false,false,false);
        if(snowPuffs&&snowPuffs.particleCount>0)snowPuffs.Simulate(dt,false,false,false);
    }
    void ClearBursts(){
        if(mandarinStars)mandarinStars.Clear();
        if(snowPuffs)snowPuffs.Clear();
    }
}
