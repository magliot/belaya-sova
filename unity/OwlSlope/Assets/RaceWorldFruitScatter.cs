using UnityEngine;

public partial class RaceWorld {
    // Ground fruit is streamed with its own row. Use authored spawn coordinates here:
    // an earlier skier may already have moved by the time this row enters the camera.
    void ScatterGroundFruit(int first,int segment,float start,float length){
        if(length<14)return;
        int end=Items.Count,groups=Mathf.Max(1,Mathf.RoundToInt(length/25));
        // A groomer can drive out of the previous row and a ramp can land in the next one.
        // Sample the authored neighbours even on a direct seek. These temporary descriptors
        // are never instantiated, animated, filtered into the world or kept in the stream.
        if(segment>0)BuildRow(Segment(start-.5f),true);
        BuildRow(Following(segment),true);
        int hazardsEnd=Items.Count;
        float cell=length/groups;
        for(int group=0;group<groups;group++){
            int stream=1200+group*40;
            float Random(int n)=>Roll(seed,segment,stream+n);
            int count=1+Mathf.Min(3,Mathf.FloorToInt(Random(0)*4));
            float step=3.15f+Random(1)*.95f;
            float halfSpan=(count-1)*step*.5f;
            float low=start+group*cell+5+halfSpan,high=start+(group+1)*cell-5-halfSpan;
            if(high<low){count=1;halfSpan=0;low=start+group*cell+5;high=start+(group+1)*cell-5;}
            float centre=Mathf.Lerp(low,high,Random(2));
            float slope=Mathf.Lerp(-.12f,.12f,Random(3));
            var gate=Random(4)<.43f?GateInCell(first,end,start+group*cell,cell):null;
            if(gate!=null)centre=Mathf.Clamp(gate.originD,low,high);
            float halfWidth=WidthAt(centre)*.5f-1.35f;
            // Each group has a separate lateral target. Alternate candidates are considered
            // only when the complete approach, pickup group or run-out crosses a solid.
            for(int attempt=0;attempt<12;attempt++){
                float lane=attempt==0&&gate!=null?gate.originX+(Random(5)-.5f)*.7f:
                    Mathf.Lerp(-halfWidth,halfWidth,Random(6+attempt));
                if(centre<32&&Mathf.Abs(lane)>(centre-halfSpan)*.34f)continue;
                if(!FruitCorridorClear(first,hazardsEnd,centre,lane,slope,halfSpan))continue;
                for(int i=0;i<count;i++){
                    float offset=i*step-halfSpan;
                    Add("coin",centre+offset,lane+slope*offset,1.1f,0);
                }
                break;
            }
        }
        Items.RemoveRange(end,hazardsEnd-end);
    }
    RaceItem GateInCell(int first,int end,float start,float length){
        for(int i=first;i<end;i++){
            var o=Items[i];
            if(o.kind=="gate"&&o.originD>=start&&o.originD<start+length)return o;
        }
        return null;
    }
    bool FruitCorridorClear(int first,int end,float centre,float lane,float slope,float halfSpan){
        const float approach=7,runOut=6;
        float entry=centre-halfSpan-approach,exit=centre+halfSpan+runOut;
        float width=Mathf.Min(WidthAt(entry),WidthAt(exit));
        if(Mathf.Abs(lane)+Mathf.Abs(slope)*halfSpan>width*.5f-1.15f)return false;
        for(int i=first;i<end;i++){
            var o=Items[i];float x=o.originX,z=o.originD;
            float left,right,before,after;
            switch(o.kind){
                case "gate":
                    if(o.stoneGate||z<entry-1||z>exit+1)continue;
                    float gateX=lane+slope*Mathf.Clamp(z-centre,-halfSpan,halfSpan);
                    if(Mathf.Abs(Mathf.Abs(gateX-x)-o.width*.5f)<1.0f)return false;
                    continue;
                case "arch":case "startArch":
                    if(z>=entry-1&&z<=exit+1&&Mathf.Abs(Mathf.Abs(lane)-7)<1.25f)return false;
                    continue;
                case "npc":{
                    if(o.mergeGateMetre>0){
                        left=x-Mathf.Min(x,o.mergeGateLane)+1.7f;
                        right=Mathf.Max(x,o.mergeGateLane)-x+1.7f;
                        before=1.5f;after=o.mergeGateMetre-z+10;break;
                    }
                    // Include the carve and forward travel of a visible moving skier.
                    float swing=o.pace>0?Mathf.Clamp(o.amp*1.45f+.35f,.6f,2.7f):0;
                    left=right=1.4f+swing;before=1.1f;after=1.1f+Mathf.Max(0,o.pace)*11;break;
                }
                case "tube":
                    float edgeA=o.side*o.from,edgeB=o.side*o.to;
                    left=x-Mathf.Min(edgeA,edgeB)+2.1f;right=Mathf.Max(edgeA,edgeB)-x+2.1f;
                    before=10;after=o.pathSpan+6;break;
                case "cat":left=right=o.width*.5f+2;before=3+Mathf.Max(0,-o.pace)*8;after=3+Mathf.Max(0,o.pace)*8;break;
                case "snowmobile":left=right=2.5f;before=4;after=10;break;
                case "mogulYeti":
                    // At the crossing a moving target can occupy either side.
                    left=right=20;before=7;after=9;break;
                case "dog":left=right=4.8f;before=1.8f;after=9;break;
                case "yeti":left=right=2.7f;before=after=1.7f;break;
                case "rock":case "tree":case "mogul":case "snowman":case "cannon":case "fence":case "festivalFountain":
                    left=right=o.width*.5f+1.05f;
                    before=after=Mathf.Max(1,o.length*.5f)+.9f;break;
                case "ramp":case "rail":
                    left=right=o.width*.5f+1.05f;before=o.length*.5f+1;
                    after=o.length*.5f+o.span+1;break;
                case "thermos":case "helmet":case "aid":case "magnet":case "gift":
                    if(Mathf.Abs(z-centre)>halfSpan+1.2f)continue;
                    left=right=1.25f;before=after=1.1f;break;
                default:continue;
            }
            float overlapStart=Mathf.Max(entry,z-before),overlapEnd=Mathf.Min(exit,z+after);
            if(overlapStart>overlapEnd)continue;
            float a=lane+slope*Mathf.Clamp(overlapStart-centre,-halfSpan,halfSpan);
            float b=lane+slope*Mathf.Clamp(overlapEnd-centre,-halfSpan,halfSpan);
            if(Mathf.Min(a,b)<=x+right&&Mathf.Max(a,b)>=x-left)return false;
        }
        return true;
    }
}
