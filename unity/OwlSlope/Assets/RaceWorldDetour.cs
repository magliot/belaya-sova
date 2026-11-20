using UnityEngine;

public partial class RaceWorld {
    static Mesh detourArrow,patrolSign;
    void IdentifyPatrolPatients(int first){
        if(mode!="normal")return;
        for(int i=first;i<Items.Count;i++){
            var person=Items[i];if(!person.sit)continue;
            for(int j=first;j<Items.Count;j++){
                var kit=Items[j];
                if(kit.kind=="aid"&&Mathf.Abs(person.distance-kit.distance)<1.55f&&Mathf.Abs(person.lane-kit.lane)<3.3f){
                    person.patrolPatient=true;break;
                }
            }
        }
    }
    internal float PatrolShoulder(RaceItem sitter){
        float half=WidthAt(sitter.distance)*.5f;
        return Mathf.Sign(sitter.lane)*Mathf.Clamp(Mathf.Abs(sitter.lane)+2.4f,half-2.7f,half+.35f);
    }
    // A small red patrol cross points out the stopping bay from the normal gameplay camera.
    void MarkRescueBay(RaceItem sitter){
        if(!patrolSign){
            var vertices=new System.Collections.Generic.List<Vector3>();
            var colors=new System.Collections.Generic.List<Color32>();
            var triangles=new System.Collections.Generic.List<int>();
            void Panel(float x,float y,float width,float height,float z,Color32 tint){
                int v=vertices.Count;
                vertices.Add(new Vector3(x,y,z));vertices.Add(new Vector3(x,y+height,z));
                vertices.Add(new Vector3(x+width,y+height,z));vertices.Add(new Vector3(x+width,y,z));
                for(int k=0;k<4;k++)colors.Add(tint);
                triangles.AddRange(new[]{v,v+1,v+2,v,v+2,v+3});
            }
            var blue=new Color32(27,91,102,255);var red=new Color32(215,46,53,255);
            Panel(-.065f,0,.13f,1.9f,0,blue);Panel(-.62f,1.05f,1.24f,1.18f,-.02f,blue);
            Panel(-.54f,1.13f,1.08f,1.02f,-.035f,new Color32(250,251,241,255));
            Panel(-.13f,1.28f,.26f,.72f,-.05f,red);Panel(-.37f,1.51f,.74f,.26f,-.052f,red);
            patrolSign=new Mesh{name="Пункт помощи"};patrolSign.SetVertices(vertices);patrolSign.SetColors(colors);
            patrolSign.SetTriangles(triangles,0);patrolSign.RecalculateNormals();
        }
        float metre=sitter.distance-3.7f,lane=PatrolShoulder(sitter)+Mathf.Sign(sitter.lane)*1.15f;
        var marker=new GameObject("Карман для вызова патруля");marker.transform.SetParent(transform,false);
        marker.transform.SetPositionAndRotation(SnowSurface(metre,lane),SnowFrame(metre,lane));
        marker.AddComponent<MeshFilter>().sharedMesh=patrolSign;
        marker.AddComponent<MeshRenderer>().sharedMaterial=Paint("",0,false,0);sitter.rescueBay=marker;
    }
    void SetDetourArrow(RaceItem fence){
        var sign=fence.obj.transform.Find("Стрелка объезда");
        if(!sign){
            if(!detourArrow){
                detourArrow=new Mesh{name="Стрелка на сетке"};
                detourArrow.vertices=new[]{
                    new Vector3(-.68f,-.32f,0),new Vector3(-.68f,.32f,0),new Vector3(.68f,.32f,0),new Vector3(.68f,-.32f,0),
                    new Vector3(-.49f,-.08f,-.02f),new Vector3(-.49f,.08f,-.02f),new Vector3(.11f,.08f,-.02f),new Vector3(.11f,-.08f,-.02f),
                    new Vector3(.10f,-.24f,-.02f),new Vector3(.10f,.24f,-.02f),new Vector3(.49f,0,-.02f)};
                var colors=new Color32[11];
                for(int i=0;i<colors.Length;i++)colors[i]=i<4?new Color32(255,195,40,255):new Color32(16,33,55,255);
                detourArrow.colors32=colors;detourArrow.triangles=new[]{0,1,2,0,2,3,4,5,6,4,6,7,8,9,10};
                detourArrow.RecalculateNormals();
            }
            sign=new GameObject("Стрелка объезда").transform;sign.SetParent(fence.obj.transform,false);
            sign.gameObject.AddComponent<MeshFilter>().sharedMesh=detourArrow;
            sign.gameObject.AddComponent<MeshRenderer>().sharedMaterial=Paint("",0,false,0);
        }
        // Baked fences are five metres wide before scaling. Keep the arrow legible on narrow nets.
        sign.localPosition=new Vector3(0,1.38f,-.18f);
        sign.localScale=new Vector3((fence.lane<0?1:-1)*5/Mathf.Max(.3f,fence.width),1,1);
    }
}
