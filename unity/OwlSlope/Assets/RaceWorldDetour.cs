using UnityEngine;

public partial class RaceWorld {
    static Mesh detourArrow;
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
