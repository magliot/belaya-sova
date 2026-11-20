using UnityEngine;
using UnityEngine.Rendering;

// Coordinates are in the baked mesh's local space. Work lights, lens glow and
// the amber beacon all stay on the cab when the body banks, turns or is pooled.
public sealed class GroomerOptics : MonoBehaviour {
    static Material headPaint,beaconPaint,mountPaint;
    static Mesh lampQuad,mountMesh;
    Light roadBeam;

    public void Fit(string model){
        bool small=model=="cat12A",oncoming=model=="rattOncoming";
        float front=oncoming?-1:1;
        if(!lampQuad){
            lampQuad=new Mesh{name="Lamp glow billboard"};
            lampQuad.vertices=new[]{new Vector3(-1,-1),new Vector3(-1,1),new Vector3(1,1),new Vector3(1,-1)};
            lampQuad.triangles=new[]{0,1,2,0,2,3};lampQuad.bounds=new Bounds(Vector3.zero,Vector3.one*2.1f);
            var shader=Resources.Load<Shader>("Race/GroomerLamp");
            headPaint=new Material(shader);headPaint.SetColor("_Tint",new Color(1,.88f,.55f));
            beaconPaint=new Material(shader);beaconPaint.SetColor("_Tint",new Color(1,.42f,.035f));beaconPaint.SetFloat("_Beacon",1);
            mountMesh=RaceWorld.UnitSphere();
            mountPaint=new Material(Resources.Load<Material>("SlopeLit"));mountPaint.color=new Color(.19f,.22f,.25f);
        }
        // Large textured groomer has an off-centre beacon. The compact variant's beacon sits on the roof centre line.
        var beacon=small?new Vector3(0,3.478f,.552f):new Vector3(oncoming?.350f:-.350f,3.83f,front*.578f);
        Lamp("Rotating amber beacon",beacon,front,.91f,beaconPaint);
        float y=small?1.84f:1.67f,z=front*(small?1.944f:1.82f);
        for(int side=-1;side<=1;side+=2){
            var p=new Vector3(side*(small?.675f:.88f),y,z);
            if(!small){
                var mount=new GameObject("Work light housing");mount.transform.SetParent(transform,false);
                mount.transform.localPosition=p-new Vector3(0,0,front*.065f);mount.transform.localScale=new Vector3(.22f,.145f,.125f);
                mount.AddComponent<MeshFilter>().sharedMesh=mountMesh;mount.AddComponent<MeshRenderer>().sharedMaterial=mountPaint;
            }
            Lamp("Cab headlight",p,front,1.04f,headPaint);
        }
        // One overlapping beam for the pair keeps additional-light cost low on mobile.
        var light=new GameObject("Twin headlight beam");light.transform.SetParent(transform,false);
        light.transform.localPosition=new Vector3(0,y,z);light.transform.localRotation=Quaternion.LookRotation(new Vector3(0,-.22f,front));
        roadBeam=light.AddComponent<Light>();roadBeam.type=LightType.Spot;roadBeam.spotAngle=62;roadBeam.innerSpotAngle=30;
        roadBeam.range=25;roadBeam.color=new Color(1,.88f,.64f);roadBeam.intensity=75;roadBeam.shadows=LightShadows.None;
        roadBeam.renderMode=LightRenderMode.ForcePixel;roadBeam.enabled=false;
    }
    public void IlluminateRoad(bool night){roadBeam.enabled=night;}
    void Lamp(string name,Vector3 anchor,float front,float radius,Material paint){
        var lamp=new GameObject(name);lamp.transform.SetParent(transform,false);
        lamp.transform.localPosition=anchor;lamp.transform.localRotation=Quaternion.LookRotation(Vector3.forward*front);
        lamp.transform.localScale=Vector3.one*radius;
        lamp.AddComponent<MeshFilter>().sharedMesh=lampQuad;var renderer=lamp.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=paint;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
}
