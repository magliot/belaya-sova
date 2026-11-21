using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class ChaseCameraChecks {
    static readonly int[] MixedTicks={1,3,4};

    // Use the same public controls and simulation steps in both the reference build
    // and the current build. A tick is 1/120 s. Capture through reversals and release.
    internal static void Drive(MountainRace race,Action<string,Transform,Camera> capture){
        var camera=race.View;
        float oldAspect=camera.aspect;camera.aspect=.5f;
        try{
            foreach(string hero in new[]{"bear","owl"}){
                foreach(string section in new[]{"start","bend","tube"}){
                    race.Menu();race.ChooseHero(hero);
                    string mode=section=="tube"?"tubing":section=="bend"?"review":"normal";
                    race.StartRace("{\"mode\":\""+mode+"\",\"seed\":1911}");
                    if(section=="bend"){race.Seek(275);race.RideFromReview();}
                    var rider=race.transform.Find("Selected rider");
                    for(int tick=0,frame=0;;frame++){
                        if(tick==0||tick==56||tick==64||tick==96||tick==104||tick==128){
                            if(race.Status.phase!="play")throw new InvalidOperationException("Ride interrupted at "+race.Status.metres+" m");
                            capture(hero+"-"+section+"-"+tick,rider,camera);
                        }
                        if(tick==128)break;
                        if(tick==16){race.BeginSteer();race.DragSteer(.25f);}
                        if(tick==56)race.DragSteer(-.22f);
                        if(tick==96)race.ReleaseSteer();
                        int ticks=section=="bend"?MixedTicks[frame%MixedTicks.Length]:2;
                        race.Advance(ticks/120f);tick+=ticks;
                    }
                }
            }
        }finally{camera.aspect=oldAspect;}
    }

    static void CheckPortrait(string sample,Transform rider,Camera camera,Vector3 oldEye){
        var skin=rider.GetComponentInChildren<SkinnedMeshRenderer>();
        var mesh=new Mesh();
        Vector3 eye=camera.transform.position;
        try{
            skin.BakeMesh(mesh);
            var vertices=mesh.vertices;
            var basis=Matrix4x4.TRS(skin.transform.position,skin.transform.rotation,Vector3.one);
            float BodyPixels(){
                float bottom=float.PositiveInfinity,top=float.NegativeInfinity;
                foreach(var v in vertices){
                    float y=camera.WorldToViewportPoint(basis.MultiplyPoint3x4(v)).y;
                    bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);
                }
                return (top-bottom)*780;
            }
            string capture=Environment.GetEnvironmentVariable("SOVA_CAMERA_DISTANCE_CAPTURE");
            float now=BodyPixels();
            if(!string.IsNullOrEmpty(capture))RiderCourseChecks.Shot(camera,sample+"-new",capture,390,780);
            camera.transform.position=oldEye;
            float before=BodyPixels();
            if(!string.IsNullOrEmpty(capture))RiderCourseChecks.Shot(camera,sample+"-21b",capture,390,780);
            float reduction=1-now/before;
            if(!(before>30&&reduction>.055f&&reduction<.12f))
                throw new InvalidOperationException($"Portrait rider scale {sample}: {before:F1} to {now:F1} pixels");
            Debug.Log($"PORTRAIT DISTANCE {sample}, 390x780: body {before:F1} to {now:F1} px, reduced {100*reduction:F1}%");
        }finally{camera.transform.position=eye;UnityEngine.Object.Destroy(mesh);}
    }

    public static void Run(MountainRace race){
        // Recorded on the unmodified 21b runtime (4cf70bb) with Drive above.
        // Compare world-space output, including the original camera's return after release.
        var reference=File.ReadAllLines(Path.Combine(Application.dataPath,"Editor","Camera21b.csv"));
        int row=1;float maxRider=0,maxEye=0,maxAngle=0,maxFov=0;
        Drive(race,(sample,rider,camera)=>{
            var values=reference[row++].Split(',');
            float At(int i)=>float.Parse(values[i],CultureInfo.InvariantCulture);
            Vector3 Position(int i)=>new Vector3(At(i),At(i+1),At(i+2));
            if(sample!=values[0])throw new InvalidDataException("Unexpected 21b reference row for "+sample);
            float riderError=Vector3.Distance(rider.position,Position(1));
            var rotation=new Quaternion(At(7),At(8),At(9),At(10));
            Vector3 oldEye=Position(4);
            float eyeError=Vector3.Distance(camera.transform.position,oldEye+rotation*(Vector3.back*.70f));
            float angle=Quaternion.Angle(camera.transform.rotation,rotation.normalized);
            float fov=Mathf.Abs(camera.fieldOfView-At(11));
            if(!(riderError<.01f&&eyeError<.01f&&angle<.08f&&fov<.02f))
                throw new InvalidOperationException($"21b mismatch {sample}: rider {riderError:F4} m, eye {eyeError:F4} m, rotation {angle:F3} deg, FOV {fov:F3} deg");
            maxRider=Mathf.Max(maxRider,riderError);maxEye=Mathf.Max(maxEye,eyeError);
            maxAngle=Mathf.Max(maxAngle,angle);maxFov=Mathf.Max(maxFov,fov);
            if(sample.EndsWith("-start-0",StringComparison.Ordinal)||sample.EndsWith("-bend-56",StringComparison.Ordinal))
                CheckPortrait(sample,rider,camera,oldEye);
        });
        if(row!=reference.Length)throw new InvalidDataException("Unused 21b steering samples");
        Debug.Log($"21B STEERING PASS {row-1} samples, both riders, normal start, bend at 275 m and tubing. Max rider {maxRider:F4} m, dolly error {maxEye:F4} m, rotation {maxAngle:F3} deg, FOV {maxFov:F3} deg");
    }
}
