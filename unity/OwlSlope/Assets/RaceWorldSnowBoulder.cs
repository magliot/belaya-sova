using UnityEngine;

public partial class RaceWorld {
    static Mesh boulderMesh;
    void SpawnSnowBoulder(float start,int section){
        if(section*FreeRideSectionLength+120>=StageLength)return;
        if(!TryFreeRideGap(start,out var gap))return;
        int key=Mathf.FloorToInt(start);
        float radius=Mathf.Lerp(.89f,1.20f,Roll(seed,key,1400));
        float side=Mathf.Sign(gap.lane);
        bool pursuit=section%4==0;
        float release=Mathf.Max(start+(pursuit?112:117),gap.endD+8);
        var o=Add("snowBoulder",release,side*(pursuit?8.8f:7.7f),2*radius,2*radius);
        o.route=pursuit?1:0;
        o.scale=radius;o.side=side;o.vCruise=(pursuit?32:8)+3*Roll(seed,key,1401);
        o.phase=Roll(seed,key,1402)*Mathf.PI*2;
        if(section%8==6){
            float r=radius*.74f;
            var second=Add("snowBoulder",release+12,side*7.7f,2*r,2*r);
            second.side=side;second.scale=r;second.vCruise=o.vCruise*.91f;second.phase=o.phase+1.7f;
        }
    }
    static void RollSnowBoulder(RaceItem o,float player,float riderLane,float dt){
        if(o.route==1){
            if(o.age==0){
                float lead=player-o.originD;
                if(lead<14||dt<=0)return;
                if(lead>29){o.Consume();return;}
                o.to=Mathf.Clamp(riderLane,-10.5f,10.5f);
                o.pace=19;
            }
            o.age+=dt;
            o.pace=Mathf.MoveTowards(o.pace,o.vCruise,8.5f*dt);
            float roll=o.pace*dt;
            o.distance+=roll;
            float travel=o.distance-o.originD;
            // The snowball commits to the player's old tracks and cannot steer after them.
            o.lane=Mathf.Lerp(o.originX,o.to,Smooth(travel/42));
            float escape=Smooth(Mathf.InverseLerp(90,125,travel));
            o.lane=Mathf.Lerp(o.lane,-o.side*15,escape);
            o.stride=Mathf.Repeat(o.stride+roll/o.scale*Mathf.Rad2Deg,360);
            if(travel>=125)o.Consume();
            return;
        }
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
