using UnityEngine;
using UnityEngine.Rendering;

public partial class MountainRace {
    ParticleSystem mandarinPeels,snowPuffs,moundChips;

    static Mesh CurlPeel(){
        // A tapered ribbon cut from a mandarin. The cross curve gives the pith a concave face.
        const int sections=8;
        var vertices=new Vector3[(sections+1)*3];var uv=new Vector2[vertices.Length];
        var faces=new int[sections*12];int index=0;
        for(int j=0;j<=sections;j++){
            float t=(float)j/sections,angle=Mathf.Lerp(-1.55f,2.45f,t);
            float width=.045f+.22f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(Mathf.PI*t)),.78f);
            for(int i=0;i<3;i++){
                float side=i-1,radius=.40f+side*side*.062f;
                vertices[j*3+i]=new Vector3(radius*Mathf.Sin(angle),side*width+(t-.5f)*.29f,radius*Mathf.Cos(angle)-.13f);
                uv[j*3+i]=new Vector2(i*.5f,t);
                if(j==sections||i==2)continue;
                int a=j*3+i;
                faces[index++]=a;faces[index++]=a+3;faces[index++]=a+1;
                faces[index++]=a+1;faces[index++]=a+3;faces[index++]=a+4;
            }
        }
        var mesh=new Mesh{name="Завиток мандариновой кожуры",vertices=vertices,uv=uv,triangles=faces};
        mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }

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
        var ps=fruit?mandarinPeels:snowPuffs;
        if(!ps){
            if(fruit){
                ps=mandarinPeels=MakeBurst("Кожура мандарина","MandarinPeel");
                var r=ps.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Mesh;r.mesh=CurlPeel();
                var main=ps.main;main.gravityModifier=1.25f;main.startRotation3D=true;
                var spin=ps.rotationOverLifetime;spin.enabled=true;spin.separateAxes=true;
                spin.x=3.8f;spin.y=2.9f;spin.z=-4.2f;
            }else ps=snowPuffs=MakeBurst("Попадание снежка","MountainRaceFlake");
        }
        var rider=World.SnowSurface(Status.metres,lane)+Vector3.up*(Altitude+1.06f);
        var origin=fruit&&item.Visual?item.Visual.transform.position+Vector3.up*.32f:rider;
        var forward=World.SnowFrame(Status.metres,lane)*Vector3.forward;
        var right=Vector3.Cross(Vector3.up,forward).normalized;
        int count=fruit?6:15;
        for(int i=0;i<count;i++){
            float angle=(i+.35f*Status.coins)*2.39996f;
            Vector3 spread=right*Mathf.Cos(angle)+Vector3.up*Mathf.Sin(angle);
            var p=new ParticleSystem.EmitParams{
                position=origin+(fruit?.33f:.10f)*spread,
                velocity=forward*(speed*(fruit?.77f:.82f))+spread*((fruit?2.35f:1.7f)+.21f*(i%4))+Vector3.up*(fruit?2.5f:1.1f),
                startLifetime=(fruit?.55f:.36f)+.035f*(i%6),startSize=(fruit?.30f:.17f)+.024f*(i%4),
                startColor=fruit?new Color32(255,255,255,255):
                    (i%3==0?new Color32(145,208,248,235):new Color32(248,252,255,245)),
                rotation=i*31f
            };
            if(fruit)p.rotation3D=new Vector3(37*i,23*i+13*Status.coins,71*i);
            ps.Emit(p,1);
        }
    }
    void BreakSnowMound(RaceItem mound){
        if(!moundChips){
            moundChips=MakeBurst("Кусочки разбитого наста","Race/WorldObject");
            var renderer=moundChips.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode=ParticleSystemRenderMode.Mesh;
            var shard=new Mesh{name="Осколок наста"};
            var a=new Vector3(-.56f,-.22f,-.35f);var b=new Vector3(.48f,-.27f,-.24f);
            var c=new Vector3(.12f,-.31f,.62f);var tip=new Vector3(-.16f,.64f,.04f);
            shard.vertices=new[]{a,b,c,a,tip,b,b,tip,c,c,tip,a};
            shard.triangles=new[]{0,1,2,3,4,5,6,7,8,9,10,11};
            shard.RecalculateNormals();shard.RecalculateBounds();renderer.mesh=shard;
            var main=moundChips.main;main.startRotation3D=true;main.gravityModifier=1.65f;
            var rotation=moundChips.rotationOverLifetime;rotation.enabled=true;rotation.separateAxes=true;
            rotation.x=4.2f;rotation.y=-2.8f;rotation.z=3.6f;
        }
        var frame=World.SnowFrame(mound.distance,mound.lane);
        var origin=World.SnowSurface(mound.distance,mound.lane)+Vector3.up*.23f;
        for(int i=0;i<18;i++){
            float side=i%2==0?-1:1;
            var outward=new Vector3(side*(1.7f+.8f*(i%4)),2.6f+.86f*(i%5),speed*(.33f+.05f*(i%7)));
            moundChips.Emit(new ParticleSystem.EmitParams{
                position=origin+frame*new Vector3(side*.19f,.03f*(i%4),.12f*(i%3)),
                velocity=frame*outward,startSize=.15f+.055f*(i%5),startLifetime=.52f+.05f*(i%6),
                rotation3D=new Vector3(i*19,i*71,i*37),
                startColor=i%3==0?new Color32(147,200,227,255):new Color32(238,250,255,255)
            },1);
        }
    }
    void StepBursts(float dt){
        StepEquipmentSparks(dt);
        if(carvingSnow&&carvingSnow.particleCount>0)carvingSnow.Simulate(dt,false,false,false);
        if(mandarinPeels&&mandarinPeels.particleCount>0)mandarinPeels.Simulate(dt,false,false,false);
        if(snowPuffs&&snowPuffs.particleCount>0)snowPuffs.Simulate(dt,false,false,false);
        if(moundChips&&moundChips.particleCount>0)moundChips.Simulate(dt,false,false,false);
    }
    void ClearBursts(){
        if(kitSparks)kitSparks.Clear();
        nextKitSpark=0;
        if(carvingSnow)carvingSnow.Clear();
        if(mandarinPeels)mandarinPeels.Clear();
        if(snowPuffs)snowPuffs.Clear();
        if(moundChips)moundChips.Clear();
    }
}
