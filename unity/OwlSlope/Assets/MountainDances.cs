using System.Collections.Generic;
using UnityEngine;

// Menu celebrations use the same rig and model-space joint angles as danceGlb.
public class MountainDances : MonoBehaviour {
    static readonly string[] joints={"Hips","Spine","Head","LeftShoulder","RightShoulder","LeftArm","RightArm","LeftForeArm","RightForeArm","LeftUpLeg","RightUpLeg","LeftLeg","RightLeg"};
    readonly Dictionary<string,Transform> bones=new Dictionary<string,Transform>();
    readonly Dictionary<string,Quaternion> rest=new Dictionary<string,Quaternion>();
    MountainRace race;
    Transform actor;
    Vector3 origin;
    Quaternion facing;
    string dance;
    float start;
    public void Dance(string id){
        StopDance();
        race=GetComponent<MountainRace>();
        if(!race||!race.IsMenu||!race.MenuActor||System.Array.IndexOf(new[]{"sway","floss","hop","dab","champ"},id)<0)return;
        actor=race.MenuActor;origin=actor.localPosition;facing=actor.localRotation;
        foreach(var bone in actor.GetComponentsInChildren<Transform>(true)){
            foreach(string key in joints){
                if(bone.name!=key&&!bone.name.EndsWith(":"+key)&&bone.name!="mixamorig"+key)continue;
                bones[key]=bone;rest[key]=bone.localRotation;break;
            }
        }
        dance=id;start=Time.time;
    }
    public void StopDance(){
        if(actor){
            foreach(var pair in bones)if(pair.Value)pair.Value.localRotation=rest[pair.Key];
            actor.localPosition=origin;actor.localRotation=facing;
        }
        dance=null;actor=null;bones.Clear();rest.Clear();
    }
    void R(string key,float x,float y,float z){
        if(!bones.TryGetValue(key,out var bone))return;
        if(key.EndsWith("Arm"))z=-z;
        var basis=Quaternion.Inverse(bone.parent.rotation)*actor.rotation;
        var delta=Quaternion.AngleAxis(-x*Mathf.Rad2Deg,Vector3.right)*Quaternion.AngleAxis(-y*Mathf.Rad2Deg,Vector3.up)*Quaternion.AngleAxis(z*Mathf.Rad2Deg,Vector3.forward);
        bone.localRotation=basis*delta*Quaternion.Inverse(basis)*rest[key];
    }
    void LateUpdate(){
        if(dance==null)return;
        if(!actor||!race.IsMenu||race.MenuActor!=actor){StopDance();return;}
        float t=Time.time-start,s=Mathf.Sin(t*3.2f),bounce=0;
        foreach(var p in bones)p.Value.localRotation=rest[p.Key];
        if(dance=="sway"){
            R("Spine",0,0,.2f*s);R("Head",0,.25f*Mathf.Cos(t*3.2f),0);
            R("LeftArm",0,0,-2.3f+.35f*s);R("RightArm",0,0,2.3f+.35f*s);
            R("LeftForeArm",0,0,-.3f);R("RightForeArm",0,0,.3f);
            R("LeftUpLeg",-.15f-.15f*Mathf.Max(0,s),0,0);R("RightUpLeg",-.15f-.15f*Mathf.Max(0,-s),0,0);
            bounce=.05f*Mathf.Abs(Mathf.Sin(t*6.4f));
        }else if(dance=="floss"){
            float a=Mathf.Sin(t*7),b=Mathf.Cos(t*7);
            R("Hips",0,0,.18f*a);R("Spine",0,0,-.22f*a);R("Head",0,.2f*a,0);
            R("LeftShoulder",0,0,.06f*(1-a));R("RightShoulder",0,0,-.06f*(1+a));
            R("LeftArm",.75f*b,0,-.6f+1.05f*a);R("RightArm",-.75f*b,0,.6f+1.05f*a);bounce=Mathf.Abs(a)*.03f;
        }else if(dance=="hop"){
            float k=(1-Mathf.Cos(t*8.4f))*.5f;
            R("LeftShoulder",0,0,.25f*k);R("RightShoulder",0,0,-.25f*k);
            R("LeftArm",0,0,-.2f-1.85f*k);R("RightArm",0,0,.2f+1.85f*k);R("Head",-.15f*k,0,0);
            R("LeftUpLeg",-.2f*(1-k),0,.3f*k);R("RightUpLeg",-.2f*(1-k),0,-.3f*k);
            R("LeftLeg",.4f*(1-k),0,0);R("RightLeg",.4f*(1-k),0,0);bounce=.22f*k;
        }else if(dance=="dab"){
            float ph=(t%1.6f)/1.6f;
            if(ph<.55f){
                R("Spine",.15f,0,-.12f);R("Head",.45f,-.5f,0);
                R("RightArm",0,0,2.35f);R("RightForeArm",0,0,.1f);R("LeftArm",-1.35f,0,-1);R("LeftForeArm",-1.7f,0,0);
            }else{
                float k=Mathf.Sin((ph-.55f)/.45f*Mathf.PI*2);
                R("LeftArm",-.8f,0,-.3f+.3f*k);R("RightArm",-.8f,0,.3f-.3f*k);
                R("LeftForeArm",-.9f,0,0);R("RightForeArm",-.9f,0,0);
                R("LeftUpLeg",-.35f,0,0);R("RightUpLeg",-.35f,0,0);R("LeftLeg",.6f,0,0);R("RightLeg",.6f,0,0);bounce=Mathf.Abs(k)*.06f;
            }
        }else{
            float w=Mathf.Sin(t*12)*.35f;R("Spine",-.1f,0,.1f*s);R("Head",-.2f,0,0);
            R("LeftArm",0,0,-2.5f+w);R("RightArm",0,0,2.5f+w);
            actor.localRotation=facing*Quaternion.Euler(0,t*100,0);bounce=Mathf.Abs(Mathf.Sin(t*6.3f))*.08f;
        }
        actor.localPosition=origin+Vector3.up*bounce;
    }
    void OnDisable(){StopDance();}
}
