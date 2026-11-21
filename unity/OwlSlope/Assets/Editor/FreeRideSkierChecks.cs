using System;
using System.Linq;
using UnityEngine;

public static class FreeRideSkierChecks {
    static void Need(bool ok,string reason){if(!ok)throw new InvalidOperationException("Free Ride skier: "+reason);}

    static Vector3[] SkinPoints(SkinnedMeshRenderer skin,Mesh baked){
        skin.BakeMesh(baked,false);
        var points=baked.vertices;
        // This metre-authored FBX has a centimetre-scale mesh node. BakeMesh(false)
        // already returns skinning in metres, as on the other two rider models.
        var matrix=Matrix4x4.TRS(skin.transform.position,skin.transform.rotation,Vector3.one);
        for(int i=0;i<points.Length;i++)points[i]=matrix.MultiplyPoint3x4(points[i]);
        return points;
    }
    static Vector3 Farthest(Vector3[] points,Vector3 target){
        Vector3 best=points[0];
        foreach(var point in points)if((point-target).sqrMagnitude>(best-target).sqrMagnitude)best=point;
        return best;
    }
    static void CheckSole(MountainRace race,Transform chassis,Vector3[] vertices){
        var terrain=race.World.GetComponentsInChildren<MeshFilter>().Single(m=>m.sharedMesh&&m.sharedMesh.name=="Alpine snow, exposed slate and crevasses");
        var ground=terrain.gameObject.AddComponent<MeshCollider>();
        try{
            ground.sharedMesh=terrain.sharedMesh;Physics.SyncTransforms();
            for(int sign=-1;sign<=1;sign+=2){
                float lowest=float.PositiveInfinity;Vector3 sole=Vector3.zero;
                foreach(var world in vertices){
                    var p=chassis.InverseTransformPoint(world);
                    if(sign*p.x<.02f||Mathf.Abs(p.z)>.25f||p.y>=lowest)continue;
                    lowest=p.y;sole=world;
                }
                Need(!float.IsInfinity(lowest),"missing runner");
                Need(ground.Raycast(new Ray(sole+Vector3.up*2,Vector3.down),out var hit,4),"missing snow under runner");
                float height=sole.y-hit.point.y;
                Debug.Log($"FREE RIDE SKI CONTACT {race.Status.metres:F1} m, side {sign}, sole height {height:F3} m, local {lowest:F3} m");
                Need(height>-.007f&&height<.055f,"ski base offset from visible snow "+height);
            }
        }finally{UnityEngine.Object.DestroyImmediate(ground);}
    }
    // Measure the rendered pole in the rendered glove, including baked skin deformation.
    static Vector3 Grip(SkinnedMeshRenderer pole,Transform wrist,Vector3[] body,Mesh baked){
        var points=SkinPoints(pole,baked);
        Vector3 tip=Farthest(points,wrist.position),top=Farthest(points,tip);
        float length=Vector3.Distance(top,tip);
        Need(length>.98f&&length<1.32f,"shaft length "+length);
        var direction=(tip-top).normalized;
        int n=0;var cap=Vector3.zero;
        foreach(var point in points)if(Vector3.Dot(point-top,direction)<.013f){cap+=point;n++;}
        Vector3 palm=cap/n+direction*.087f;
        float skinDistance=float.MaxValue;
        foreach(var point in body)skinDistance=Mathf.Min(skinDistance,Vector3.Distance(palm,point));
        Need(Vector3.Distance(palm,wrist.position)<.16f&&skinDistance<.085f,"handle missed mitten: "+skinDistance);
        return wrist.InverseTransformPoint(palm);
    }
    public static void Run(MountainRace race){
        race.Menu();race.ChooseHero("owl");
        race.StartRace(JsonUtility.ToJson(new MountainRace.Settings{mode="freeride",freeRideSkier=true,seed=1911}));
        var model=GameObject.Find("FreeRideSkier").transform;
        var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();
        var parts=model.GetComponentsInChildren<Transform>();
        var body=skins.Single(r=>r.name=="Skier_Game");
        var poles=new[]{skins.Single(r=>r.name=="Pole.L"),skins.Single(r=>r.name=="Pole.R")};
        var hands=new[]{parts.Single(t=>t.name=="hand.L"),parts.Single(t=>t.name=="hand.R")};
        var head=parts.Single(t=>t.name=="head");var hips=parts.Single(t=>t.name=="pelvis");
        Transform chassis=model.parent;
        var mitten=(hands[0].position+hands[1].position)*.5f;
        float ahead=chassis.InverseTransformPoint(mitten).z-chassis.InverseTransformPoint(hips.position).z;
        Debug.Log("FREE RIDE SKIER glove lead on skis: "+ahead.ToString("F3")+" m");
        Need(ahead>.08f,"skier facing uphill: "+ahead);
        Need(race.World.TryFreeRideGap(80,out var gap),"missing first cornice");
        var baked=new Mesh();var localGrips=new Vector3[2];
        string film=Environment.GetEnvironmentVariable("SOVA_FREERIDE_SKIER_FILM");int frame=0,check=0;
        float[] at={8,70,84.6f,92,97,106,118};
        bool armed=false,inverted=false;
        float gripDrift=0;
        try{
            race.BeginSteer();
            for(int i=0;i<420&&race.Status.phase=="play"&&race.Status.metres<121;i++){
                float d=race.Status.metres;
                race.DragSteer((d>29?gap.lane:0)/(27*.79f));
                if(!armed&&d>gap.lipD-10){race.AirTrick("up");armed=true;}
                race.Advance(1f/30);
                var relative=chassis.InverseTransformVector(head.position-hips.position);
                if(relative.y<-.17f&&race.Altitude>.35f)inverted=true;
                if(check<at.Length&&race.Status.metres>=at[check]){
                    var vertices=SkinPoints(body,baked);
                    if(check==1||check==at.Length-1)CheckSole(race,chassis,vertices);
                    for(int s=0;s<2;s++){
                        var grip=Grip(poles[s],hands[s],vertices,baked);
                        if(check==0)localGrips[s]=grip;
                        else gripDrift=Mathf.Max(gripDrift,Vector3.Distance(hands[s].TransformPoint(localGrips[s]),hands[s].TransformPoint(grip)));
                    }
                    check++;
                }
                if(!string.IsNullOrEmpty(film)&&race.Status.metres>=45)
                    RiderCourseChecks.Shot(race.View,(frame++).ToString("D4"),film,390,780);
            }
            Need(inverted&&race.Status.tricks==1&&race.Status.helmets==1,"authored inversion, landing or score");
            Need(check==at.Length&&gripDrift<.018f,"pole movement in gloves: "+gripDrift);
            Debug.Log("FREE RIDE SKIER RIG PASS 7 sampled poses, both handles, max drift "+gripDrift.ToString("F4")+" m, full authored inversion");
        }finally{UnityEngine.Object.DestroyImmediate(baked);race.Menu();}
        Need(race.Hero=="owl"&&!model.gameObject.activeSelf,"guest leaked into selected profile");
    }
}
