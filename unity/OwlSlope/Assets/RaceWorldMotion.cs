using System.Collections.Generic;
using UnityEngine;

public partial class RaceWorld {
    // The archived models merge jacket, mittens and equipment into one textured mesh.
    // Bake two opposing upper-body poses once per shared mesh. The player's rig is separate.
    static void AddRiderPoses(Mesh mesh,string key,List<Vector3> vertices,List<Vector3> normals){
        bool board=key=="snowboarder"||key=="boardKid"||key=="boardKid1"||key=="boardInstructor";
        bool pupil=key=="kid";
        if(!board&&!pupil&&key!="skier"&&key!="instructor")return;
        foreach(int side in new[]{-1,1}){
            var shift=new Vector3[vertices.Count];var normalShift=new Vector3[vertices.Count];
            for(int i=0;i<vertices.Count;i++){
                var p=vertices[i];
                Quaternion rotation;Vector3 pivot;
                if(board){
                    // Look into the next traverse. The leading arm opens and the trailing shoulder follows.
                    float chest=Smooth(Mathf.InverseLerp(.38f,.98f,p.y));
                    float arm=Smooth(Mathf.InverseLerp(.12f,.49f,Mathf.Abs(p.z)))*
                        Smooth(Mathf.InverseLerp(.48f,.76f,p.y));
                    float reach=arm*(p.z*side>0?1:.48f);
                    rotation=Quaternion.Euler(reach*side*(p.z>0?-11:11),side*(chest*18+reach*10),side*chest*4);
                    pivot=new Vector3(0,.75f,-.02f);
                }else{
                    // The adult's pole below the glove has the same shoulder pivot as the sleeve.
                    // Skis extend fore and aft at snow height; keep these vertices and the boots fixed.
                    float hand=Smooth(Mathf.InverseLerp(pupil?.23f:.15f,pupil?.45f:.34f,Mathf.Abs(p.x)));
                    hand*=1-Smooth(Mathf.InverseLerp(.82f,1.01f,p.y));
                    float pole=!pupil&&Mathf.Abs(p.x)>.39f&&Mathf.Abs(p.z)<.31f?1:0;
                    hand*=Mathf.Max(pole,Smooth(Mathf.InverseLerp(.27f,.56f,p.y)));
                    float swing=p.x*side>0?-26:16;
                    rotation=Quaternion.Euler(hand*swing,0,hand*side*(pupil?10:3));
                    pivot=new Vector3(Mathf.Sign(p.x)*.19f,pupil?.76f:.88f,-.16f);
                }
                shift[i]=pivot+rotation*(p-pivot)-p;
                normalShift[i]=rotation*normals[i]-normals[i];
            }
            mesh.AddBlendShapeFrame(side<0?"ReachLeft":"ReachRight",100,shift,normalShift,null);
        }
    }
    static void PoseMovingRider(RaceItem o){
        float pressure=Mathf.Clamp(o.lean/(o.sb?12:8),-1,1);
        float cycle=o.age*(o.kid?2.9f:o.sb?1.85f:2.4f)+o.phase;
        float transfer=Mathf.Sin(cycle);
        float wave=.5f+.5f*Mathf.Sin(2*cycle-.8f);
        // Extension between edges, compression on a bump, a tuck at the apex and a deep landing.
        float flight=o.airSpan>0?Mathf.Sin(Mathf.PI*Mathf.Clamp01((o.distance-o.airStart)/o.airSpan)):0;
        float landing=Mathf.Clamp01(1-(o.age-o.landedAt)/.56f);
        float compression=Mathf.Clamp01(.12f+.26f*wave+.31f*Mathf.Abs(pressure)+
            .12f*Mathf.Clamp01(o.offset)+.38f*flight+.61f*landing);
        float reach=Mathf.Clamp(.76f*transfer+.44f*pressure,-1,1)*(1-.55f*flight);
        var skin=o.skin;var mesh=skin.sharedMesh;
        skin.SetBlendShapeWeight(mesh.GetBlendShapeIndex("0"),compression*100);
        skin.SetBlendShapeWeight(mesh.GetBlendShapeIndex("1"),(pressure*.82f+.16f*transfer)*100);
        skin.SetBlendShapeWeight(mesh.GetBlendShapeIndex("ReachLeft"),Mathf.Max(0,-reach)*100);
        skin.SetBlendShapeWeight(mesh.GetBlendShapeIndex("ReachRight"),Mathf.Max(0,reach)*100);
    }
    void MoveItem(RaceItem o,float player,float elapsed,float dt){
        switch(o.kind){
            case "npc":MoveRider(o,player,elapsed,dt);return;
            case "yeti":o.age+=dt;return;
            case "tube":MoveTube(o,player,dt);return;
            case "cat":
                if(o.small){
                    float t=Smooth((player-o.originD+46)/61);
                    o.distance=o.originD+7.75f*t;o.lane=o.side*(4.8f+.7f*Mathf.Sin(Mathf.PI*t));
                    o.yaw=Mathf.Atan(o.side*14*Mathf.PI*Mathf.Cos(Mathf.PI*t)/155)*Mathf.Rad2Deg;
                }else{
                    o.distance+=o.pace*dt;float t=(o.distance-o.originD)/8,side=o.originX<0?1:-1;
                    o.lane=o.originX+side*.85f*(1-Mathf.Cos(t));o.yaw=Mathf.Atan(side*17/160*Mathf.Sin(t))*Mathf.Rad2Deg;
                }return;
            case "snowmobile":{
                float t=Mathf.Clamp01((player-o.originD+40)/52.5f),angle=t*2*Mathf.PI,b=Mathf.Sin(angle);
                o.lane=o.side*(63+15*t+16*b)*.05f;o.distance=o.originD+(165-210*t)*.05f;
                o.yaw=180-Mathf.Atan2(o.side*(15+32*Mathf.PI*Mathf.Cos(angle)),210)*Mathf.Rad2Deg;o.lean=o.side*b*5;return;}
            case "mogulYeti":{
                float t=Mathf.Clamp01((player-o.originD+73)/97.5f),v=Smooth(t),s=Mathf.Sin(t*2*Mathf.PI+o.phase);
                ShowPrints(o,t);
                float lateral=o.route==0?173-292*v:o.route==1?154-96*v:145-114*v;
                float forward=o.route==0?75*t:o.route==1?310-465*t:-150+475*t;
                float dx=o.side*((o.route==0?-292:o.route==1?-96:-114)*6*t*(1-t)+13*2*Mathf.PI*Mathf.Cos(t*2*Mathf.PI+o.phase));
                o.yaw=Mathf.Atan2(dx,o.route==0?75:o.route==1?-465:475)*Mathf.Rad2Deg;
                o.lane=o.side*(lateral+13*s)*.05f;o.distance=o.originD+forward*.05f;
                o.stride=Mathf.Sin(t*10*Mathf.PI);
                o.offset=(o.leap?1.2f*Mathf.Sin(Mathf.PI*Mathf.Clamp01((t-.41f)/.24f)):0)+.14f*Mathf.Abs(o.stride);o.lean=3*o.stride;return;}
            case "dog":{
                o.age+=dt;float t=o.age,side=o.originX<0?1:-1,ph=Roll(seed,Mathf.RoundToInt(o.originD*20),86)*6.28f;
                float x=o.originX+side*(48+40*Mathf.Sin(t*1.6f+ph)+25*Mathf.Sin(t*.67f+ph))*(1-Mathf.Exp(-t*1.5f))*.05f;
                float d=o.originD+1.7f*(Mathf.Sin(t*1.2f+ph)-Mathf.Sin(ph))+.95f*t;
                if(dt>0)o.yaw=Mathf.Atan2(x-o.lane,d-o.distance)*Mathf.Rad2Deg;
                o.distance=d;o.lane=x;o.offset=.055f*Mathf.Abs(Mathf.Sin(t*14));return;}
            case "ball":MoveSnowball(o,dt);return;
            case "snowman":ThrowSnowball(o,player,dt);return;
            case "airRing":o.lean=Mathf.Sin(elapsed*.65f+o.originD)*3;return;
            case "coin":case "feather":case "gift":case "thermos":case "aid":case "helmet":case "magnet":
                o.yaw=o.kind=="coin"?Mathf.Sin(elapsed*2.5f+o.lane)*34:elapsed*95;o.offset=.075f*Mathf.Sin(elapsed*3+o.lane);return;
        }
    }
    void MoveTube(RaceItem o,float player,float dt){
        if(!o.movingPoseReady){
            o.age=Mathf.Clamp((player-o.release)/14.5f,-12,16);
            o.movingPoseReady=true;
        }
        // The scheduled point starts acceleration. Higher up the bank the tube
        // is already gliding into its line, even when seen across a long bend.
        o.age+=dt;
        float entrySpeed=Mathf.Max(1.25f,o.vStart);
        float decay=Mathf.Exp(-Mathf.Max(0,o.age)/o.tau);
        float d=o.age<0?entrySpeed*o.age:o.vCruise*o.age-(o.vCruise-entrySpeed)*o.tau*(1-decay);
        float t=Mathf.Clamp01(d/o.pathSpan),delta=o.to-o.from,tail=Mathf.Max(0,d-o.pathSpan)/4.75f;
        float tangent=o.side*(delta*6*t*(1-t)/o.pathSpan+.2f*Mathf.Sin(tail)/4.75f);
        float speed=o.vCruise-(o.vCruise-entrySpeed)*decay;
        float curvature=t>0&&t<1?o.side*delta*6*(1-2*t)/(o.pathSpan*o.pathSpan):d>o.pathSpan?o.side*.2f*Mathf.Cos(tail)/(4.75f*4.75f):0;
        o.lane=o.side*(o.from+delta*Smooth(t)+.2f*(1-Mathf.Cos(tail)));o.distance=o.originD+d;
        o.yaw=180+(Mathf.Atan(tangent)-Mathf.Clamp(curvature*speed*.34f,-.16f,.16f))*Mathf.Rad2Deg;
        o.lean=14*Mathf.Clamp(-curvature*speed*speed*1.1f,-1,1);o.scale=.84f;
    }
    void MoveRider(RaceItem o,float player,float elapsed,float dt){
        if(o.walker){o.distance+=o.pace*dt;o.lean=1.5f*Mathf.Sin(elapsed*2);return;}
        if(o.pace<=0||o.stand||o.sit){
            float f=elapsed*.75f+o.phase;o.yaw=18*Mathf.Sin(f)+(o.instr&&o.stand?145:0);o.lean=2.5f*Mathf.Sin(f*1.9f);
            o.offset=o.kid&&o.stand?.16f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(f*3)),10):0;return;
        }
        if(!o.movingPoseReady){
            o.movingPoseReady=true;
            o.age=2.1f*Roll(seed,Mathf.RoundToInt(o.originD),317);
            o.velocity=Mathf.Clamp(.75f*o.amp*Mathf.Cos(o.age+o.phase),-o.pace*.62f,o.pace*.62f);
            o.yaw=Mathf.Atan2(o.velocity,o.pace)*Mathf.Rad2Deg;
            o.lean=-3*Mathf.Sin(o.age+o.phase);
        }
        if(dt<=0)return;o.age+=dt;
        float target=o.originX+o.amp*(o.sb?1.2f:1.08f)*Mathf.Sin(o.age+o.phase+.32f*Mathf.Sin(o.age*.54f+o.phase));
        float influence=0,previous=o.distance;
        foreach(var q in Items){
            if(q==o||q.consumed)continue;float ahead=q.distance-o.distance;
            if(ahead<-.6f||ahead>8.5f)continue;
            float k=1-Mathf.Max(0,ahead)/8.5f,r=0;
            switch(q.kind){
                case "tree":case "snowman":case "rock":case "cannon":case "fence":r=q.width*.5f+.7f;break;
                case "cat":r=q.width*.5f+1;break;
                case "npc":if(q.pace>=o.pace-.4f&&ahead>1.7f)continue;r=q.stand||q.sit?1.5f:1.2f;break;
                case "gate":if(Mathf.Abs(Mathf.Abs(o.lane-q.lane)-q.width*.5f)<.8f)influence+=Mathf.Sign(q.lane-o.lane)*k;continue;
                default:continue;
            }
            float dx=o.lane-q.lane;if(Mathf.Abs(dx)<r)influence+=(dx>=0?1:-1)*k*(2-Mathf.Abs(dx)/r);
        }
        target=Mathf.Clamp(target+Mathf.Clamp(influence,-2.5f,2.5f)*1.3f,-8.5f,8.5f);
        float a=Mathf.Clamp((target-o.lane)*9-o.velocity*4.6f,-6.4f,6.4f);
        o.velocity=Mathf.Clamp(o.velocity+a*dt,-o.pace*.87f,o.pace*.87f);
        o.lane=Mathf.Clamp(o.lane+o.velocity*dt,-8.5f,8.5f);float yaw=Mathf.Atan2(o.velocity,o.pace);
        o.distance+=o.pace*(1-.16f*Mathf.Abs(Mathf.Sin(yaw)))*dt;o.yaw=yaw*Mathf.Rad2Deg;
        o.lean=Mathf.Lerp(o.lean,-a/6.5f*(o.sb?14:9),1-Mathf.Exp(-5*dt));o.offset=0;
        if(o.airSpan>0){float t=(o.distance-o.airStart)/o.airSpan;
            o.offset=(1-t)*(o.airBase+4*o.airLift*t);if(t>=1){o.airSpan=0;o.offset=0;o.landedAt=o.age;}return;}
        foreach(var q in Items){
            if(q.kind!="ramp"||Mathf.Abs(q.lane-o.lane)>q.width*.5f)continue;
            float lip=q.distance+q.length*.5f;
            if(previous<lip&&o.distance>=lip){o.airStart=lip;o.airSpan=q.park?q.span:Mathf.Max(2,o.pace*.8f);o.airLift=q.park?q.lift:.8f;o.offset=o.airBase=q.height;return;}
            if(o.distance>=lip-q.length&&o.distance<=lip){float t=1+(o.distance-lip)/q.length;o.offset=q.height*t*(q.park?.65f+.35f*t:1);return;}
        }
    }
}
