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
        float pressure=Mathf.Clamp(o.lean/(o.sb?14:10),-1,1);
        float cycle=o.age*(o.kid?2.85f:o.sb?1.95f:2.55f)+o.phase;
        float transfer=Mathf.Sin(cycle);
        float wave=.5f+.5f*Mathf.Sin(2*cycle-.8f);
        // Extension between edges, compression on a bump, a tuck at the apex and a deep landing.
        float flight=o.airSpan>0?Mathf.Sin(Mathf.PI*Mathf.Clamp01((o.distance-o.airStart)/o.airSpan)):0;
        float landing=Mathf.Clamp01(1-(o.age-o.landedAt)/.56f);
        float compression=Mathf.Clamp01(.08f+.23f*wave+.48f*Mathf.Abs(pressure)+
            .18f*Mathf.Clamp01(o.offset)+.42f*flight+.67f*landing);
        float reach=Mathf.Clamp(.77f*transfer+.62f*pressure,-1,1)*(1-.42f*flight);
        var skin=o.skin;var mesh=skin.sharedMesh;
        skin.SetBlendShapeWeight(mesh.GetBlendShapeIndex("0"),compression*100);
        skin.SetBlendShapeWeight(mesh.GetBlendShapeIndex("1"),(pressure*.82f+.16f*transfer)*100);
        skin.SetBlendShapeWeight(mesh.GetBlendShapeIndex("ReachLeft"),Mathf.Max(0,-reach)*100);
        skin.SetBlendShapeWeight(mesh.GetBlendShapeIndex("ReachRight"),Mathf.Max(0,reach)*100);
    }
    void MoveItem(RaceItem o,float player,float elapsed,float dt){
        switch(o.kind){
            case "snowBoulder":RollSnowBoulder(o,player,dt);return;
            case "npc":MoveRider(o,player,elapsed,dt);return;
            case "yeti":o.age+=dt;return;
            case "tube":MoveTube(o,player,dt);return;
            case "cat":{
                o.distance+=o.pace*dt;
                float t=(o.distance-o.originD)/8,side=o.originX<0?1:-1;
                o.lane=o.originX+side*.85f*(1-Mathf.Cos(t));
                // The large uphill cab is already reversed in its mesh. The compact cab faces downhill.
                o.yaw=Mathf.Atan(side*.85f/8*Mathf.Sin(t))*Mathf.Rad2Deg+(o.small&&o.pace<0?180:0);
                return;}
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
            case "ball":MoveSnowball(o,player,dt);return;
            case "snowman":ThrowSnowball(o,player,dt);return;
            case "airRing":o.lean=Mathf.Sin(elapsed*.65f+o.originD)*3;return;
            case "coin":case "feather":case "gift":case "thermos":case "aid":case "helmet":case "magnet":
                o.yaw=o.kind=="coin"?Mathf.Sin(elapsed*2.5f+o.lane)*34:elapsed*95;o.offset=.075f*Mathf.Sin(elapsed*3+o.lane);return;
        }
    }
    void MoveTube(RaceItem o,float player,float dt){
        bool entering=!o.movingPoseReady;
        if(entering){
            o.age=Mathf.Clamp((player-o.release)/14.5f,-12,16);
            o.movingPoseReady=true;
        }
        // The scheduled point starts acceleration. Higher up the bank the tube
        // is already gliding into its line, even when seen across a long bend.
        o.age+=dt;
        float entrySpeed=Mathf.Max(1.25f,o.vStart);
        float decay=Mathf.Exp(-Mathf.Max(0,o.age)/o.tau);
        float d=o.age<0?entrySpeed*o.age:o.vCruise*o.age-(o.vCruise-entrySpeed)*o.tau*(1-decay);
        float t=Mathf.Clamp01(d/o.pathSpan),delta=o.to-o.from;
        // A quintic crossing keeps lateral acceleration continuous at both ends. A smaller
        // traverse persists on the approach and rollout, instead of locking the tube to a rail.
        float curve=t*t*t*(10+t*(-15+6*t));
        float weave=d*.27f+o.phase,wiggle=.67f;
        float tangent=o.side*(delta*30*t*t*(1-t)*(1-t)/o.pathSpan+wiggle*.27f*Mathf.Cos(weave));
        float speed=o.vCruise-(o.vCruise-entrySpeed)*decay;
        float curvature=o.side*(delta*60*t*(t-1)*(2*t-1)/(o.pathSpan*o.pathSpan)-wiggle*.27f*.27f*Mathf.Sin(weave));
        o.lane=o.side*(o.from+delta*curve+wiggle*Mathf.Sin(weave));o.distance=o.originD+d;
        float drift=Mathf.Clamp(curvature*speed*.47f,-.24f,.24f);
        float heading=180+(Mathf.Atan(tangent)-drift)*Mathf.Rad2Deg;
        float load=16*Mathf.Clamp(-curvature*speed*speed*.97f,-1,1);
        o.yaw=entering?heading:Mathf.LerpAngle(o.yaw,heading,1-Mathf.Exp(-6*dt));
        o.lean=entering?load:Mathf.Lerp(o.lean,load,1-Mathf.Exp(-4*dt));o.scale=.84f;
    }
    void MoveRider(RaceItem o,float player,float elapsed,float dt){
        if(o.walker){o.distance+=o.pace*dt;o.lean=1.5f*Mathf.Sin(elapsed*2);return;}
        if(o.pace<=0||o.stand||o.sit){
            float f=elapsed*.75f+o.phase;o.yaw=18*Mathf.Sin(f)+(o.instr&&o.stand?145:0);o.lean=2.5f*Mathf.Sin(f*1.9f);
            o.offset=o.kid&&o.stand?.16f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(f*3)),10):0;return;
        }
        bool starting=!o.movingPoseReady;
        if(starting){
            o.movingPoseReady=true;
            int row=Mathf.RoundToInt(o.originD*4+o.originX);
            o.age=4*Roll(seed,row,317);
            o.turnRate=(o.sb?.91f:o.kid?1.18f:1.48f)*Mathf.Lerp(.82f,1.18f,Roll(seed,row,318));
            o.glidePace=o.pace*(o.kid?1.10f:o.sb?1.45f:1.33f);
        }
        o.age+=dt;
        float rhythm=o.age*o.turnRate+o.phase+.48f*Mathf.Sin(o.age*.47f+o.phase);
        float width=Mathf.Clamp(o.amp*(o.sb?1.45f:1.22f)+.15f,.30f,2.45f);
        float edge=Mathf.Sin(rhythm);
        float target=o.originX+width*edge;
        if(o.mergeGateMetre>0){
            // A real downhill traverse: progress is the skier's own travelled distance.
            // A player who slows down can watch the skier clear the opening.
            float approach=Smooth((o.distance-o.mergeGateMetre+26)/22);
            float exit=Smooth((o.distance-o.mergeGateMetre-3)/22);
            target=Mathf.Lerp(o.originX,o.mergeGateLane+o.side*.88f,approach)
                +o.side*exit*2.2f+width*edge*Mathf.Lerp(.18f,1,exit);
        }
        float shoulder=Mathf.Min(8.5f,WidthAt(o.distance)*.5f-.55f);
        if(starting){
            o.lane=Mathf.Clamp(target,-shoulder,shoulder);
            o.velocity=Mathf.Clamp(width*o.turnRate*Mathf.Cos(rhythm),-o.glidePace*.80f,o.glidePace*.80f);
            o.yaw=Mathf.Atan2(o.velocity,o.glidePace)*Mathf.Rad2Deg;o.lean=edge*(o.sb?12:8);
        }
        if(dt<=0)return;
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
        target=Mathf.Clamp(target+Mathf.Clamp(influence,-2.5f,2.5f)*1.3f,-shoulder,shoulder);
        float a=Mathf.Clamp((target-o.lane)*10-o.velocity*4.4f,-6.4f,6.4f);
        float rolling=o.kid?1.13f:o.sb?1.54f:1.41f;
        float desiredPace=o.pace*rolling*(1+.36f*(Grade(o.distance)-.29f)-.22f*Mathf.Abs(edge));
        o.glidePace=Mathf.MoveTowards(o.glidePace,desiredPace,dt*(o.kid?.60f:1.05f));
        o.velocity=Mathf.Clamp(o.velocity+a*dt,-o.glidePace*.92f,o.glidePace*.92f);
        o.lane=Mathf.Clamp(o.lane+o.velocity*dt,-shoulder,shoulder);
        o.distance+=o.glidePace*dt;
        o.yaw=Mathf.Atan2(o.velocity,o.glidePace)*Mathf.Rad2Deg;
        o.lean=Mathf.Lerp(o.lean,Mathf.Clamp(-a*(o.sb?6.6f:5.2f),-18,18),1-Mathf.Exp(-5*dt));o.offset=0;
        if(o.airSpan>0){float t=(o.distance-o.airStart)/o.airSpan;
            o.offset=(1-t)*(o.airBase+4*o.airLift*t);if(t>=1){o.airSpan=0;o.offset=0;o.landedAt=o.age;}return;}
        foreach(var q in Items){
            if(q.kind!="ramp"||Mathf.Abs(q.lane-o.lane)>q.width*.5f)continue;
            float lip=q.distance+q.length*.5f;
            if(previous<lip&&o.distance>=lip){
                // Each rider leaves at their own speed. A pupil on a large park kicker
                // lands in front of it rather than following the much faster player's arc.
                o.airStart=lip;o.offset=o.airBase=q.height;
                float riseSpeed=Mathf.Clamp(1.8f+.32f*o.glidePace+.50f*q.height,2,5.2f);
                float seconds=(riseSpeed+Mathf.Sqrt(riseSpeed*riseSpeed+19.62f*q.height))/9.81f;
                o.airSpan=o.glidePace*seconds;o.airLift=1.22625f*seconds*seconds;return;
            }
            if(o.distance>=lip-q.length&&o.distance<=lip){float t=1+(o.distance-lip)/q.length;o.offset=q.height*t*(q.park?.65f+.35f*t:1);return;}
        }
    }
}
