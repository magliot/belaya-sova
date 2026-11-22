using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class HeroMotionChecks {
    public static void JumpLibrary(MountainRace race,string hero){
        const int count=2;
        race.Menu();race.ChooseHero(hero);
        race.StartRace("{\"mode\":\"normal\",\"seed\":1911}");
        var checkpoint=typeof(MountainRace).GetMethod("Checkpoint",BindingFlags.NonPublic|BindingFlags.Instance);
        var selected=typeof(MountainRace).GetField("airPose",BindingFlags.NonPublic|BindingFlags.Instance);
        var rider=race.transform.Find("Selected rider/"+(hero=="bear"?"Bear":"OwlRide"));
        var deck=rider.GetComponentsInChildren<Transform>().First(p=>p.name==(hero=="bear"?"Left_Ski":"Snowboard"));
        var hips=rider.GetComponentsInChildren<Transform>().First(p=>p.name.EndsWith("Hips"));
        var seen=new HashSet<int>();var sequence=new List<int>();int previous=-1;
        string shots=Environment.GetEnvironmentVariable("SOVA_JUMP_SHOTS");
        for(int jump=0;jump<=count;jump++){
            checkpoint.Invoke(race,new object[]{7620f+jump*RaceWorld.LapLength,0f});
            var ramp=race.World.Items.First(o=>o.kind=="ramp"&&o.prizeLine);
            float lip=ramp.distance+ramp.length*.5f;
            checkpoint.Invoke(race,new object[]{lip-1,ramp.lane});
            race.Advance(1f/60);
            Quaternion CourseGear()=>Quaternion.Inverse(race.World.SnowFrame(race.Status.metres,ramp.lane))*deck.rotation;
            int pose=-1,groundFrames=0;float turn=0,clockwise=0,pitch=0,landingTurn=0;
            Quaternion baseline=CourseGear(),relative=baseline;bool photo=false;
            for(int frame=0;frame<420;frame++){
                race.Advance(1f/60);
                if(!race.Status.air&&pose<0)continue;
                Quaternion heading=CourseGear();
                var delta=heading*Quaternion.Inverse(relative);
                delta.ToAngleAxis(out float degrees,out var axis);
                if(axis.sqrMagnitude>.01f){
                    clockwise+=Mathf.DeltaAngle(0,degrees)*axis.y;
                    pitch+=Mathf.DeltaAngle(0,degrees)*axis.x;
                }
                turn+=Quaternion.Angle(heading,relative);
                relative=heading;pose=(int)selected.GetValue(race);
                if(!race.Status.air){
                    if(groundFrames++==0)landingTurn=Quaternion.Angle(heading,baseline);
                    if(groundFrames>=32)break;
                }
                if(rider.InverseTransformPoint(hips.position).magnitude>1.7f)
                    throw new Exception("Baked film trajectory still moves "+hero+" out of its flight");
                if(!photo&&!String.IsNullOrEmpty(shots)&&race.Status.metres>lip+ramp.span*.49f){
                    RiderCourseChecks.Shot(race.View,hero+"-"+pose,shots,384,832);photo=true;
                }
            }
            if(pose<0||pose>=count||count>1&&pose==previous||jump<count&&!seen.Add(pose))throw new Exception("Unexpected jump "+hero+" "+pose);
            float finalHeading=Quaternion.Angle(CourseGear(),baseline);
            if(groundFrames<32||turn<280||finalHeading>15||hero=="owl"&&(pose==0?clockwise<300:pitch> -300)||hero=="bear"&&pose==0&&landingTurn<95)
                throw new Exception(hero+" jump "+pose+" turn "+turn+" clockwise "+clockwise+" pitch "+pitch+" at touchdown "+landingTurn+" after recovery "+finalHeading);
            previous=pose;sequence.Add(pose);
        }
        if(race.Status.tricks!=0)throw new Exception("Automatic visuals received gesture points");
        Debug.Log("JUMP LIBRARY PASS "+hero+" "+String.Join(",",sequence)+"; full deck, mounted equipment, automatic jumps award zero tricks");
    }
    // Follow the actual boot vertices through takeoff, both trick transitions and
    // the landing. Testing relative to the deck also catches a binding that moves
    // together with a boot while both leave the visible board.
    public static void OnAir(MountainRace race,string hero){
        race.RestartRide();
        var checkpoint=typeof(MountainRace).GetMethod("Checkpoint",BindingFlags.NonPublic|BindingFlags.Instance);
        checkpoint.Invoke(race,new object[]{7620f,0f});
        var ramp=race.World.Items.First(o=>o.kind=="ramp"&&o.prizeLine);
        checkpoint.Invoke(race,new object[]{ramp.distance-ramp.length*.5f-2,ramp.lane});
        race.Advance(1f/60);
        var rider=race.transform.Find("Selected rider/"+(hero=="owl"?"OwlRide":"Bear"));
        var rig=rider.GetComponentsInChildren<Transform>();
        var skin=rider.GetComponentsInChildren<SkinnedMeshRenderer>().First();
        var board=rig.FirstOrDefault(j=>j.name=="Snowboard");
        var gear=new[]{board?board:rig.First(j=>j.name=="Left_Ski"),board?board:rig.First(j=>j.name=="Right_Ski")};
        var feet=new[]{rig.First(j=>j.name.EndsWith("LeftFoot")),rig.First(j=>j.name.EndsWith("RightFoot"))};
        var mesh=new Mesh();var vertices=new List<Vector3>();
        var ids=new List<int>[] {new List<int>(),new List<int>()};
        var baseline=new List<Vector3>[] {new List<Vector3>(),new List<Vector3>()};
        Matrix4x4 Bake(){
            skin.BakeMesh(mesh);mesh.GetVertices(vertices);
            return Matrix4x4.TRS(skin.transform.position,skin.transform.rotation,Vector3.one);
        }
        Vector3 Relative(int side,Vector3 point)=>Quaternion.Inverse(gear[side].rotation)*(point-gear[side].position);
        float error=0,heightError=0,worstAt=0;
        try{
            var basis=Bake();var up=rider.up;
            var normal=gear.Select(g=>Quaternion.Inverse(g.rotation)*up).ToArray();
            bool Left(Vector3 v)=>(v-feet[0].position).sqrMagnitude<(v-feet[1].position).sqrMagnitude;
            var points=vertices.Select(v=>basis.MultiplyPoint3x4(v)).ToArray();
            for(int side=0;side<2;side++){
                var low=points.Where(p=>Left(p)==(side==0)).Min(p=>Vector3.Dot(p,up));
                for(int i=0;i<points.Length;i++)
                    if(Left(points[i])==(side==0)&&Vector3.Dot(points[i],up)<low+.015f){
                        ids[side].Add(i);baseline[side].Add(Relative(side,points[i]));
                    }
                if(ids[side].Count<12)throw new Exception(hero+" actual sole not found on side "+side);
            }
            float lip=ramp.distance+ramp.length*.5f,landing=lip+ramp.span;
            var turns=new float[2];
            Quaternion CoursePose()=>Quaternion.Inverse(race.World.SnowFrame(race.Status.metres,ramp.lane))*rider.parent.rotation;
            for(int frame=0;frame<900&&race.Status.phase=="play"&&race.Status.metres<landing+4;frame++){
                int trick=race.Status.tricks;
                var previous=CoursePose();
                if(race.Status.metres>lip&&(trick==0||trick==1&&race.Status.metres>lip+.28f*ramp.span))race.AirTrick("tap");
                race.Advance(1f/60);basis=Bake();
                if(race.Status.metres>lip&&trick<2)turns[trick]+=Quaternion.Angle(previous,CoursePose());
                for(int side=0;side<2;side++)for(int k=0;k<ids[side].Count;k++){
                    var difference=Relative(side,basis.MultiplyPoint3x4(vertices[ids[side][k]]))-baseline[side][k];
                    heightError=Mathf.Max(heightError,Mathf.Abs(Vector3.Dot(difference,normal[side])));
                    if(difference.magnitude>error){error=difference.magnitude;worstAt=race.Status.metres;}
                }
            }
            string detail=hero+" "+ids[0].Count+"/"+ids[1].Count+" sole vertices, max deck-space drift "+error.ToString("F3")+
                " m, perpendicular "+heightError.ToString("F3")+" m @ "+worstAt.ToString("F1")+" m; whole-rig turns "+
                turns[0].ToString("F0")+"/"+turns[1].ToString("F0")+" degrees";
            if(race.Status.tricks!=2||race.Status.metres<landing+4||error>.027f||heightError>.016f)
                throw new Exception("Airborne equipment: "+detail);
            Debug.Log("AIRBORNE SOLES PASS "+detail);
        }finally{UnityEngine.Object.Destroy(mesh);}
    }
    // Measure actual imported FBX poses in the same player loop as a touch ride.
    public static void OnSnow(MountainRace race,string hero){
        string key=hero=="bear"?"Bear":"OwlRide";
        var clip=Resources.LoadAll<AnimationClip>(key).First(c=>!c.name.StartsWith("__preview"));
        if(clip.length<14)throw new Exception(key+" FBX is missing the left turn cycle");
        var rider=race.transform.Find("Selected rider/"+key);
        var rig=rider.GetComponentsInChildren<Transform>();
        Transform Bone(string suffix)=>rig.First(j=>j.name.EndsWith(suffix,StringComparison.Ordinal));
        var hips=Bone("Hips");var hands=new[]{Bone("LeftHand"),Bone("RightHand")};
        var first=hands.Select(j=>rider.InverseTransformPoint(j.position)).ToArray();
        var travel=new float[2];
        var feet=new[]{Bone("LeftFoot"),Bone("RightFoot")};
        var bindings=new[]{Bone("Left_Binding"),Bone("Right_Binding")};
        // FBX nodes can carry centimetre scales. Compare offsets in metres.
        Vector3 Offset(int side)=>Quaternion.Inverse(bindings[side].rotation)*(feet[side].position-bindings[side].position);
        var offset=new[]{Offset(0),Offset(1)};
        float drift=0,low=float.MaxValue,high=float.MinValue;
        race.BeginSteer();
        for(int frame=0;frame<110;frame++){
            if(frame==82)race.DragSteer(.14f);
            race.Advance(1f/60);
            var pelvis=rider.InverseTransformPoint(hips.position);
            low=Mathf.Min(low,pelvis.y);high=Mathf.Max(high,pelvis.y);
            for(int side=0;side<2;side++){
                travel[side]=Mathf.Max(travel[side],Vector3.Distance(first[side],rider.InverseTransformPoint(hands[side].position)));
                drift=Mathf.Max(drift,Vector3.Distance(offset[side],Offset(side)));
            }
        }
        string detail=key+" wrists "+travel[0].ToString("F3")+"/"+travel[1].ToString("F3")+
            " m, compression "+(high-low).ToString("F3")+" m, boot/binding error "+drift.ToString("F3")+" m";
        bool stance=hero=="bear"?Mathf.Max(travel[0],travel[1])<.012f&&high-low<.01f:Mathf.Min(travel[0],travel[1])>=.025f&&high-low>=.020f;
        if(!stance||drift>.022f)
            throw new Exception("Ride motion or attachment: "+detail);
        Debug.Log("HERO MOTION PASS "+detail);
    }
}
