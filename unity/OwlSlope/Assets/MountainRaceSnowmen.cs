using UnityEngine;

public partial class MountainRace {
    bool SnowballContact(RaceItem ball,float before,float fromLane){
        if(!ball.positioned)return false; // Newly thrown this step; its first sweep starts at the hand.
        // Test the ball and the rider over the same fixed step, including a turn or a jump.
        var from=new Vector3(fromLane-ball.previousLane,(previousAltitude+1.02f-ball.previousBallHeight)/.82f,before-ball.previousDistance);
        var to=new Vector3(lane-ball.lane,(Altitude+1.02f-ball.height)/.82f,Status.metres-ball.distance);
        var delta=to-from;
        float closest=Mathf.Clamp01(-Vector3.Dot(from,delta)/Mathf.Max(.000001f,delta.sqrMagnitude));
        return (from+delta*closest).sqrMagnitude<=1.03f*1.03f;
    }

    sealed class SnowmanPiece {
        public Transform pivot;
        public Vector3 centre,groundShift,axis;
        public float radius,metres,lane,height,lift,speed,slide,spin,time;
        public bool bounced;
    }
    // During the fastest bonus run at most two freshly struck snowmen are visible together.
    readonly SnowmanPiece[] snowmanPieces=new SnowmanPiece[9];
    static readonly string[] snowmanParts={"snowmanBase","snowmanMiddle","snowmanHead"};
    int nextSnowmanPiece;

    SnowmanPiece CreateSnowmanPiece(string model){
        var pivot=new GameObject(model+" pivot").transform;
        pivot.SetParent(transform,false);
        var piece=World.CreateVisual(model,pivot);
        var bounds=piece.GetComponent<MeshFilter>().sharedMesh.bounds;
        piece.transform.localPosition=-bounds.center;
        return new SnowmanPiece{pivot=pivot,centre=bounds.center,radius=Mathf.Min(bounds.extents.y,bounds.extents.z)};
    }
    void ScatterSnowman(RaceItem snowman){
        var source=snowman.Visual.transform;
        float side=lane>snowman.lane?-1:1;
        for(int i=0;i<snowmanParts.Length;i++){
            int slot=nextSnowmanPiece+i;
            var p=snowmanPieces[slot]??(snowmanPieces[slot]=CreateSnowmanPiece(snowmanParts[i]));
            Vector3 centre=source.TransformPoint(p.centre);
            p.metres=snowman.distance;p.lane=snowman.lane;
            p.groundShift=centre-World.SnowSurface(p.metres,p.lane);p.height=p.groundShift.y;p.groundShift.y=0;
            p.slide=side*(i==1?-4.6f:i==0?3.3f:5.8f);
            p.speed=speed*(.72f+i*.085f);p.lift=2.8f+i*1.35f;
            p.axis=source.rotation*new Vector3(.6f,side*.3f,side*(i==1?-1:1)).normalized;
            p.spin=110+i*77;p.time=0;p.bounced=false;
            p.pivot.localScale=source.lossyScale;
            p.pivot.SetPositionAndRotation(centre,source.rotation);p.pivot.gameObject.SetActive(true);
        }
        nextSnowmanPiece=(nextSnowmanPiece+3)%snowmanPieces.Length;
    }
    void StepSnowmanPieces(float dt){
        foreach(var p in snowmanPieces){
            if(p==null||!p.pivot.gameObject.activeSelf)continue;
            p.time+=dt;
            if(p.time>=1.65f){p.pivot.gameObject.SetActive(false);continue;}
            p.metres+=p.speed*dt;p.lane+=p.slide*dt;
            p.height+=p.lift*dt-7*dt*dt;p.lift-=14*dt;
            float ground=p.radius*p.pivot.localScale.y;
            if(p.height<ground){
                p.height=ground;
                p.lift=p.bounced?0:-.32f*p.lift;
                p.bounced=true;
                float friction=Mathf.Exp(-2.3f*dt);
                p.speed*=friction;p.slide*=friction;p.spin*=friction;
            }
            p.pivot.Rotate(p.axis,p.spin*dt,Space.World);
            p.pivot.position=World.SnowSurface(p.metres,p.lane)+p.groundShift+Vector3.up*p.height;
        }
    }
    void ClearSnowmanPieces(){
        foreach(var p in snowmanPieces)if(p!=null)p.pivot.gameObject.SetActive(false);
        nextSnowmanPiece=0;
    }
}
