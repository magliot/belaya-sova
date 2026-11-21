using UnityEngine;
using UnityEngine.Rendering;

public partial class RaceWorld {
    static Mesh gateLenses;
    static readonly Material[] gateLampPaint=new Material[2];
    static Mesh ringGlowBand;
    static Material mandarinGlow,ringGlow;
    static MaterialPropertyBlock nightFruitSurface;

    void SetNightGlow(RaceItem item){
        bool night=mode=="normal"&&StageAt(item.originD)==6;
        var marker=item.obj.transform.Find("Night glow");
        if(item.kind=="coin"&&(night||marker)){
            if(nightFruitSurface==null){
                nightFruitSurface=new MaterialPropertyBlock();
                nightFruitSurface.SetFloat("_Glow",.75f);
            }
            item.Visual.SetPropertyBlock(night?nightFruitSurface:null);
        }
        if(marker){marker.gameObject.SetActive(night);return;}
        if(!night)return;
        bool ring=item.kind=="airRing";
        // Preserve the centre of the flight target. The soft band follows its actual rim.
        if(ring&&!ringGlowBand){
            const int sides=64;var vertices=new Vector3[(sides+1)*2];var uv=new Vector2[vertices.Length];
            var triangles=new int[sides*6];
            for(int i=0;i<=sides;i++){
                float a=i*2*Mathf.PI/sides;
                var radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);int k=i*2;
                vertices[k]=radial*1.12f;vertices[k+1]=radial*1.98f;
                uv[k]=Vector2.zero;uv[k+1]=Vector2.right;
                if(i==sides)break;
                int t=i*6;
                triangles[t]=k;triangles[t+1]=k+2;triangles[t+2]=k+1;
                triangles[t+3]=k+1;triangles[t+4]=k+2;triangles[t+5]=k+3;
            }
            ringGlowBand=new Mesh{name="Night flight ring glow",vertices=vertices,uv=uv,triangles=triangles};
            ringGlowBand.RecalculateNormals();
        }
        if(!mandarinGlow){
            var shader=Resources.Load<Shader>("Race/NightMarker");
            mandarinGlow=new Material(shader){name="Mandarin amber halo"};
            mandarinGlow.SetColor("_Tint",new Color(1,.49f,.12f));
            ringGlow=new Material(shader){name="Night flight ring light"};
            ringGlow.SetFloat("_Ring",1);ringGlow.SetColor("_Tint",new Color(.32f,.95f,1));
        }
        var child=new GameObject("Night glow");child.transform.SetParent(item.obj.transform,false);
        if(ring)child.transform.localPosition=Vector3.back*.13f;
        else child.transform.localScale=Vector3.one*.55f;
        child.AddComponent<MeshFilter>().sharedMesh=ring?ringGlowBand:UnitSphere();
        var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=ring?ringGlow:mandarinGlow;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }

    static void AddGateLamps(GameObject gate,bool blue){
        // Both lamp quads share a draw call. UV expands each quad in camera space at the pole cap.
        if(!gateLenses){
            var verts=new Vector3[8];var uv=new Vector2[8];
            for(int i=0;i<8;i++){
                verts[i]=new Vector3(i<4?-2.64f:2.64f,1.66f,0);
                uv[i]=new Vector2((i&1)==0?0:1,(i&2)==0?0:1);
            }
            gateLenses=new Mesh{name="Gate cap reflectors",vertices=verts,uv=uv,
                triangles=new[]{0,1,2,2,1,3,4,5,6,6,5,7},bounds=new Bounds(new Vector3(0,1.66f,0),new Vector3(7,2,2))};
        }
        int index=blue?1:0;
        if(!gateLampPaint[index]){
            var mat=new Material(Resources.Load<Shader>("Race/GateLamp"));
            mat.SetColor("_Tint",blue?new Color(.20f,.87f,1):new Color(1,.35f,.16f));gateLampPaint[index]=mat;
        }
        var child=new GameObject("Огни на стойках");child.transform.SetParent(gate.transform,false);
        child.AddComponent<MeshFilter>().sharedMesh=gateLenses;
        var r=child.AddComponent<MeshRenderer>();r.sharedMaterial=gateLampPaint[index];
        r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
    }

    static Material printInk,gunSnow;
    static readonly int[] footprintTriangles=MakePrintTriangles();

    static int[] MakePrintTriangles(){
        var triangles=new int[16*6];
        for(int i=0;i<16;i++){
            int a=i*4,b=i*6;
            triangles[b]=a;triangles[b+1]=a+1;triangles[b+2]=a+2;
            triangles[b+3]=a;triangles[b+4]=a+2;triangles[b+5]=a+3;
        }
        return triangles;
    }
    static Vector2 Step(RaceItem o,float progress){
        float curve=Smooth(progress);
        float across=o.route==0?173-292*curve:o.route==1?154-96*curve:145-114*curve;
        float along=o.route==0?75*progress:o.route==1?310-465*progress:-150+475*progress;
        return new Vector2(o.side*(across+13*Mathf.Sin(progress*2*Mathf.PI+o.phase))*.05f,o.originD+along*.05f);
    }
    void ShowPrints(RaceItem o,float t){
        int shown=Mathf.FloorToInt(t*16);
        if(o.printCount==shown)return;
        if(!o.prints){
            o.prints=new GameObject("Следы йети");o.prints.transform.SetParent(transform,false);
            o.printMesh=new Mesh{name="Следы йети"};var points=new Vector3[64];var uv=new Vector2[64];var tint=new Color32[64];
            for(int i=0;i<16;i++){
                float u=(i+.5f)/16;var p=Step(o,u);var h=(Step(o,(i+.6f)/16)-p).normalized;
                bool airborne=o.leap&&1.2f*Mathf.Sin(Mathf.PI*Mathf.Clamp01((u-.41f)/.24f))>.2f;
                var w=new Vector2(h.y,-h.x);
                for(int j=0;j<4;j++){
                    var xy=new Vector2(j==1||j==2?1:0,j>=2?1:0);
                    var foot=p+w*((xy.x-.5f)*.6f+(i%2==0?-.3f:.3f))+h*((xy.y-.5f)*.9f);
                    int n=4*i+j;points[n]=SampleGround(foot.y,foot.x)+Vector3.up*.08f;uv[n]=xy;
                    tint[n]=new Color32(131,159,184,airborne?(byte)0:(byte)232);
                }
            }
            o.printMesh.vertices=points;o.printMesh.colors32=tint;o.printMesh.uv=uv;o.printMesh.triangles=footprintTriangles;o.printMesh.RecalculateBounds();
            o.prints.AddComponent<MeshFilter>().sharedMesh=o.printMesh;
            if(!printInk){
                printInk=new Material(Resources.Load<Shader>("Race/WorldObject"));
                printInk.SetTexture("_BaseMap",Resources.Load<Texture2D>("Race/Forest/yeti-print-56c"));
                printInk.SetFloat("_Lit",0);printInk.SetFloat("_ZWrite",0);
                printInk.SetFloat("_SrcBlend",(int)BlendMode.SrcAlpha);printInk.SetFloat("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
                printInk.renderQueue=(int)RenderQueue.Transparent-1;
            }
            var renderer=o.prints.AddComponent<MeshRenderer>();renderer.sharedMaterial=printInk;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        o.printCount=shown;o.printMesh.SetTriangles(footprintTriangles,0,6*shown,0,false);
    }
    void AddCannonSpray(RaceItem o){
        float side=o.lane<0?1:-1;
        var go=new GameObject("Snow stream");go.transform.SetParent(o.obj.transform,false);
        // On the 25A mesh the face of the fan is at x=0.59, centred at y=1.91.
        go.transform.localPosition=new Vector3(side*.65f,1.91f,0);
        go.transform.localRotation=Quaternion.LookRotation(new Vector3(side,.31f,0));
        var spray=go.AddComponent<ParticleSystem>();spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=spray.main;main.startLifetime=new ParticleSystem.MinMaxCurve(1.0f,1.35f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(8.7f,11.5f);main.startSize=new ParticleSystem.MinMaxCurve(.095f,.23f);
        main.maxParticles=250;main.startColor=new Color(.88f,.95f,1,.92f);main.gravityModifier=.50f;
        main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.Local;
        var shape=spray.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Cone;
        shape.angle=10;shape.radius=.29f;shape.radiusThickness=1;
        var emission=spray.emission;emission.rateOverTime=170;
        var size=spray.sizeOverLifetime;size.enabled=true;
        size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.72f),new Keyframe(.6f,1.65f),new Keyframe(1,2.2f)));
        var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.78f,.89f,1),1)},
            new[]{new GradientAlphaKey(.86f,0),new GradientAlphaKey(.73f,.65f),new GradientAlphaKey(0,1)});
        var color=spray.colorOverLifetime;color.enabled=true;color.color=gradient;
        if(!gunSnow)gunSnow=new Material(Resources.Load<Shader>("MountainRaceFlake"));
        var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=gunSnow;renderer.shadowCastingMode=ShadowCastingMode.Off;
        o.spray=spray;
    }
    public void PauseCannons(bool pause){
        foreach(var item in Items)if(item.spray){
            if(pause)item.spray.Pause(false);else item.spray.Play(false);
        }
    }
}
