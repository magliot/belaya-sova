using UnityEngine;

public partial class RaceWorld {
    GameObject CreateConePair(){
        var pair=new GameObject("Training cones");
        pair.transform.SetParent(transform,false);
        CreateVisual("trainingCone",pair.transform);
        CreateVisual("trainingCone",pair.transform);
        return pair;
    }

    void StandCones(RaceItem gate){
        gate.obj.transform.position=SnowSurface(gate.distance,gate.lane);
        for(int i=0;i<2;i++){
            float x=gate.lane+(i-.5f)*gate.width;
            gate.obj.transform.GetChild(i).SetPositionAndRotation(SnowSurface(gate.distance,x),SnowFrame(gate.distance,x));
        }
    }

    void PoseKnockedCone(RaceItem gate,float elapsed){
        int side=gate.knockedConeSide;
        float t=Mathf.Clamp01((elapsed-gate.coneHitAt)/.82f);
        float slide=t*(2-t);
        float x=gate.lane+side*(gate.width*.5f+1.85f*slide);
        float d=gate.distance+1.48f*slide;
        float tilt=Mathf.Lerp(0,105,Mathf.SmoothStep(0,1,Mathf.Min(1,t*1.5f)));
        float hop=t<.64f?1.78f*t*(1-t/.64f):1.4f*(t-.64f)*(1-t);
        // The corner of the weighted foot is the first point to settle back on the snow.
        float support=.395f*Mathf.Sin(tilt*Mathf.Deg2Rad);
        var snow=SnowFrame(d,x);
        var cone=gate.obj.transform.GetChild(side<0?0:1);
        cone.SetPositionAndRotation(SnowSurface(d,x)+snow*Vector3.up*(support+hop),
            snow*Quaternion.Euler(0,side*18*slide,-side*tilt));
    }
}
