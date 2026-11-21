using System;
using UnityEngine;

// Screen-space measurements come from the live render camera, between the 10 Hz HUD snapshots.
public static class ChaseCameraChecks {
    const int Width=390,Hz=120,End=128;
    sealed class Ride {
        public float range,step,reverse,release,excess,pan;
        public float[] samples=new float[End/8+1];
    }
    static void Need(bool ok,string message){if(!ok)throw new InvalidOperationException("Chase camera: "+message);}
    // A little lateral motion belongs to the rider crossing the piste. Isolate screen
    // movement in the opposite direction or beyond 10% of the rider's actual traverse.
    static float Excess(float pixels,float laneChange,float pixelsPerMetre){
        float allowance=.1f*laneChange*pixelsPerMetre;
        return Mathf.Abs(pixels-Mathf.Clamp(pixels,Mathf.Min(0,allowance),Mathf.Max(0,allowance)));
    }

    static Ride Gesture(MountainRace race,string hero,int start,int[] cadence){
        race.Menu();race.ChooseHero(hero);
        if(start==0)race.StartRace("{\"mode\":\"normal\",\"seed\":1911}");
        else {
            race.StartRace("{\"mode\":\"review\",\"seed\":1911}");
            race.Seek(start);race.RideFromReview();
        }
        var camera=race.View;
        var rider=race.transform.Find("Selected rider");
        float PixelX()=>Width*camera.WorldToViewportPoint(rider.position+Vector3.up*.85f).x;
        var result=new Ride();
        float previous=PixelX(),min=previous,max=previous,releaseX=0,reverseX=0,gestureLane=0;
        result.samples[0]=previous;
        float startLane=race.Lane,maxLane=startLane,minLane=startLane;
        for(int tick=0,frame=0;tick<End;frame++){
            if(tick==16){race.BeginSteer();race.DragSteer(.25f);}
            if(tick==56){reverseX=previous;gestureLane=race.Lane;race.DragSteer(-.22f);}
            if(tick==96){releaseX=previous;gestureLane=race.Lane;race.ReleaseSteer();}
            int ticks=cadence[frame%cadence.Length];
            race.Advance((float)ticks/Hz);
            tick+=ticks;
            float x=PixelX(),d=race.Status.metres;
            Need(race.Status.phase=="play"&&float.IsFinite(x),"stopped at "+d+" m");
            min=Mathf.Min(min,x);max=Mathf.Max(max,x);
            result.step=Mathf.Max(result.step,Mathf.Abs(x-previous)*2/ticks);previous=x;
            if(tick>56&&tick<=96)result.reverse=Mathf.Max(result.reverse,Mathf.Abs(x-reverseX));
            if(tick>96)result.release=Mathf.Max(result.release,Mathf.Abs(x-releaseX));
            if(tick>56){
                Vector3 across=race.World.SampleGround(d,race.Lane+1)-race.World.SampleGround(d,race.Lane);
                float pixelsPerMetre=Width*camera.WorldToViewportPoint(rider.position+Vector3.up*.85f+across).x-x;
                float sinceGesture=x-(tick<=96?reverseX:releaseX);
                result.excess=Mathf.Max(result.excess,Excess(sinceGesture,race.Lane-gestureLane,pixelsPerMetre));
            }
            if(tick%8==0)result.samples[tick/8]=x;
            maxLane=Mathf.Max(maxLane,race.Lane);minLane=Mathf.Min(minLane,race.Lane);
            Vector3 road=race.World.SampleGround(d+10,race.Lane)-race.World.SampleGround(d-8,race.Lane);
            float pan=Vector2.Angle(new Vector2(road.x,road.z),new Vector2(camera.transform.forward.x,camera.transform.forward.z));
            result.pan=Mathf.Max(result.pan,pan);
        }
        result.range=max-min;
        Need(maxLane-startLane>2&&minLane<startLane-2,hero+" did not cross both sides at "+start+" m");
        if(start>0)Need(Mathf.Abs(race.World.HeadingAt(start)-race.World.HeadingAt(race.Status.metres))*Mathf.Rad2Deg>5,"test section stopped bending");
        return result;
    }
    static void TubeRelease(MountainRace race){
        foreach(float direction in new[]{-1f,1f}){
            race.Menu();race.StartRace("{\"mode\":\"tubing\",\"seed\":1911}");
            race.BeginSteer();race.DragSteer(direction*.22f);
            const float dt=1f/60;
            for(int i=0;i<12;i++)race.Advance(dt);
            float previous=race.Lane;race.Advance(dt);
            float released=race.Lane,heldSpeed=(released-previous)/dt;
            race.ReleaseSteer();race.Advance(dt);
            float glideSpeed=(race.Lane-released)/dt,ratio=glideSpeed/heldSpeed;
            Debug.LogFormat("TUBE RELEASE direction={0} held={1:F2}m/s released={2:F2}m/s ratio={3:F3}",direction,heldSpeed,glideSpeed,ratio);
            Need(heldSpeed*direction>3&&ratio>.65f&&ratio<1.07f,"tube release changed lateral speed by a factor of "+ratio);
        }
    }
    public static void Run(MountainRace race){
        Camera camera=race.View;float oldAspect=camera.aspect;camera.aspect=.5f;
        try{
            float maxStep=0,maxExcess=0,maxCadence=0,maxPan=0;
            foreach(string hero in new[]{"bear","owl"})foreach(int start in new[]{0,275}){
                var even=Gesture(race,hero,start,new[]{2});
                var uneven=Gesture(race,hero,start,new[]{1,3,4});
                float diff=0;
                for(int i=0;i<even.samples.Length;i++)diff=Mathf.Max(diff,Mathf.Abs(even.samples[i]-uneven.samples[i]));
                maxStep=Mathf.Max(maxStep,even.step,uneven.step);
                maxExcess=Mathf.Max(maxExcess,even.excess,uneven.excess);
                maxPan=Mathf.Max(maxPan,even.pan,uneven.pan);
                maxCadence=Mathf.Max(maxCadence,diff);
                Debug.LogFormat("CHASE TRACE {0} at {1} m, 390x780: range={2:F2}px step60={3:F2}px reverse={4:F2}px release={5:F2}px yaw={6:F2}deg mixedDelta={7:F2}px excess={8:F2}px",
                    hero,start,even.range,even.step,even.reverse,even.release,even.pan,diff,Mathf.Max(even.excess,uneven.excess));
            }
            TubeRelease(race);
            Need(maxStep<1.6f&&maxExcess<2&&maxCadence<1&&maxPan<1,
                $"maxima at 390x780: step60={maxStep:F2}, excess={maxExcess:F2}, mixedDelta={maxCadence:F2}, yaw={maxPan:F2}");
            Debug.Log("CHASE CAMERA PASS both riders, straight and bend, 60 Hz and uneven 8/25/33 ms frames");
        }finally{camera.aspect=oldAspect;}
    }
}
