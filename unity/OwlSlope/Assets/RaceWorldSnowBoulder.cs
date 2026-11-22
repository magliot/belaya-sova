using UnityEngine;

public partial class RaceWorld {
    static Mesh boulderMesh;
    void SpawnSnowBoulder(float start,int section){
        if(section*FreeRideSectionLength+120>=StageLength)return;
        if(!TryFreeRideGap(start,out var gap))return;
        int key=Mathf.FloorToInt(start);
        float radius=Mathf.Lerp(.89f,1.20f,Roll(seed,key,1400));
        float side=Mathf.Sign(gap.lane);
        var o=Add("snowBoulder",start+117,side*7.7f,2*radius,2*radius);
        o.scale=radius;o.side=side;o.vCruise=8+3*Roll(seed,key,1401);
        o.phase=Roll(seed,key,1402)*Mathf.PI*2;
    }
    static void RollSnowBoulder(RaceItem o,float player,float dt){
        if(o.age==0&&o.originD-player>68)return;
        o.age+=dt;
        // Start on a shoulder, roll across the fall line, then carry on downhill
        // into the opposite bank before the next fracture. Centre remains an exit.
        o.pace=Mathf.MoveTowards(o.pace,o.vCruise,4.5f*dt);
        float step=o.pace*dt;
        o.distance+=step;
        float t=Mathf.Clamp01((o.distance-o.originD)/62);
        o.lane=o.side*(Mathf.Lerp(7.7f,-13f,Smooth(t))+.35f*Mathf.Sin(t*6.28f+o.phase)*Mathf.Sin(t*Mathf.PI));
        o.stride=Mathf.Repeat(o.stride+step/o.scale*Mathf.Rad2Deg,360);
        if(t>=1)o.Consume();
    }
    GameObject MakeSnowBoulder(){
        if(!boulderMesh){
            boulderMesh=Instantiate(UnitSphere());boulderMesh.name="Packed rolling snow";
            var vertices=boulderMesh.vertices;var colors=new Color32[vertices.Length];
            for(int i=0;i<vertices.Length;i++){
                var p=vertices[i];
                float crust=Mathf.Sin(p.x*6+p.y*3)*Mathf.Cos(p.z*5-p.y*2);
                float band=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.36f,.91f,Mathf.Sin(7*p.y+2*p.z+2*crust)));
                vertices[i]=p*(.96f+.065f*crust);
                colors[i]=Color.Lerp(new Color(.94f,.97f,1),new Color(.51f,.69f,.81f),band);
            }
            boulderMesh.vertices=vertices;boulderMesh.colors32=colors;boulderMesh.RecalculateNormals();boulderMesh.RecalculateBounds();
        }
        var rock=new GameObject("Снежный ком");rock.transform.SetParent(transform,false);
        rock.AddComponent<MeshFilter>().sharedMesh=boulderMesh;
        rock.AddComponent<MeshRenderer>().sharedMaterial=Paint("",0,true,0);
        return rock;
    }
}
