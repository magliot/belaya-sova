using UnityEngine;

public partial class RaceWorld {
    float targetLane,targetSpeed=14,targetSideSpeed,targetAltitude;

    public void SetThrowTarget(float lane,float speed,float sideSpeed,float altitude){
        targetLane=lane;targetSpeed=Mathf.Max(0,speed);targetSideSpeed=sideSpeed;targetAltitude=altitude;
    }
    void ThrowSnowball(RaceItem o,float player,float dt){
        float ahead=o.distance-player;
        o.cooldown=Mathf.Max(0,o.cooldown-dt);
        o.yaw=(o.variant=="snowman"?180:0)+Mathf.Atan2(o.lane-targetLane,Mathf.Max(3,ahead))*Mathf.Rad2Deg;
        if(!o.threwSnowball&&dt>0){
            if(o.windup>0){
                o.windup=Mathf.Max(0,o.windup-dt);
                if(o.windup==0&&ahead>0){
                    LaunchSnowball(o,player);o.threwSnowball=true;o.cooldown=1.05f;
                }
            }else if(ahead>2&&ahead<Mathf.Clamp(targetSpeed*1.4f+4,19,46))o.windup=.40f;
        }
        // The variants with rigid arms also tip back, then follow through.
        o.lean=(o.lane<targetLane?1:-1)*(o.windup>0?-8*Mathf.Sin(o.windup/.4f*Mathf.PI):
            7*Mathf.Clamp01((o.cooldown-.78f)/.27f));
    }
    void LaunchSnowball(RaceItem sender,float player){
        int id=Mathf.RoundToInt(sender.originD*20)+Mathf.RoundToInt(sender.originX*100);
        float duration=Mathf.Clamp((sender.distance-player)/Mathf.Max(8,targetSpeed+9),.46f,.86f);
        float along=player+targetSpeed*duration;
        float scatter=Roll(seed,id,402)*2-1;
        bool wide=Roll(seed,id,404)<.05f;
        float offset=wide?(scatter<0?-3.4f:3.4f):scatter*.18f;
        float across=LeadThrow(duration)+offset;
        float hand=Mathf.Sign(across-sender.lane)*(sender.variant=="snowman"?.77f:.50f);
        float height=sender.variant=="snowman"?1.5f:1.04f;
        var ball=Add("ball",sender.distance,sender.lane+hand,.42f,height);
        ball.throwStartHeight=height;ball.throwEndHeight=targetAltitude+1.02f;
        ball.throwEnd=new Vector2(across,along);ball.throwDuration=duration;ball.throwAimOffset=offset;ball.scale=1.35f;
        Place(ball);
    }
    float LeadThrow(float remaining){
        return Mathf.Clamp(targetLane+Mathf.Clamp(targetSideSpeed*remaining,-3.4f,3.4f),-12,12);
    }
    void MoveSnowball(RaceItem ball,float player,float dt){
        ball.previousBallHeight=ball.height;
        ball.age+=dt;
        float t=ball.age/ball.throwDuration;
        // Carry the current carve into the first part of the arc. The final approach
        // is committed, so the snowball crosses the rider visibly instead of snapping to them.
        float remaining=ball.throwDuration-ball.age;
        if(remaining>.17f){
            float blend=1-Mathf.Exp(-dt*18);
            var aim=new Vector2(LeadThrow(remaining)+ball.throwAimOffset,player+targetSpeed*remaining);
            ball.throwEnd=Vector2.Lerp(ball.throwEnd,aim,blend);
            ball.throwEndHeight=Mathf.Lerp(ball.throwEndHeight,targetAltitude+1.02f,blend);
        }
        ball.lane=Mathf.LerpUnclamped(ball.originX,ball.throwEnd.x,t);
        ball.distance=Mathf.LerpUnclamped(ball.originD,ball.throwEnd.y,t);
        ball.height=Mathf.LerpUnclamped(ball.throwStartHeight,ball.throwEndHeight,t)
            +4*1.30f*t*(1-t);
        if(ball.height<=.04f||t>1.55f)ball.Consume();
    }
}
