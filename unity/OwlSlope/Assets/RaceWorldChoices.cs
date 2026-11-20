using UnityEngine;

public partial class RaceWorld {
    // Keep the opening school groups, the park prize flight and the stage arches.
    // A choice occupies an entire row so unrelated figures cannot spawn in either opening.
    readonly System.Collections.Generic.Dictionary<int,bool> summitChoiceRows=new System.Collections.Generic.Dictionary<int,bool>();
    bool ChoiceRow(int fullStage,int row){
        switch(fullStage%8){
            case 0:return row>=5&&(row-5)%6==0;
            case 1:return row%8==5;
            case 4:{
                if(row%8!=5)return false;
                int key=fullStage*100+row;
                if(summitChoiceRows.TryGetValue(key,out bool free))return free;
                float start=200000+row*1000;
                var layout=GetLayout(4,fullStage/8);
                free=!System.Array.Exists(layout.objects,o=>o.y>=start&&o.y<start+1000&&
                    (o.laneAt>0||o.type=="aid"||o.type=="fence"));
                summitChoiceRows.Add(key,free);
                return free;
            }
            case 2:return row%4==3;
            case 5:return row%4==2;
            case 6:return row%6==3;
            case 7:return row%7==4&&row%6!=2;
            default:return false;
        }
    }
    static float ChoiceSpread(float t){
        return 3.55f*Smooth(t/.34f)*Smooth((1-t)/.28f);
    }
    void BuildChoice(int fullStage,int row,float start,float length){
        int stage=fullStage%8,lap=fullStage/8;
        float side=Roll(seed,stage*100+row,783)<.5f?-1:1;
        if(lap%2!=0)side=-side;
        float at=start+length*.56f;
        float hard=ChoiceSpread(.56f)*side,wide=-hard;
        float tightGap=3f-.17f*Mathf.Min(4,lap),wideGap=5.15f;
        for(int i=0;i<9;i++){
            float t=.15f+i*.085f;
            Add("coin",start+t*length,side*ChoiceSpread(t),1.1f,0).line=CourseLine.Precision;
            if(i==1||i==4||i==7)Add("coin",start+t*length,-side*ChoiceSpread(t),1.1f,0).line=CourseLine.Wide;
        }
        if(stage==7){
            // On both sides the player rounds an anchored fountain; the fruit shows the winning side.
            var inner=Add("festivalFountain",at,hard-side*1.96f,1.04f,.78f);
            inner.side=side;inner.length=1.04f;inner.line=CourseLine.Precision;
            // Stagger the two bases longitudinally to keep the start of each bend legible.
            var outer=Add("festivalFountain",at-8,wide+side*3.6f,1.04f,.78f);
            outer.side=-side;outer.length=1.04f;outer.line=CourseLine.Wide;
            var fir=Add("tree",start+length*.82f,side*8.3f,1.4f,4.2f,"holidayFir");
            fir.scale=1.15f;fir.offset=-.1f;return;
        }
        bool trees=stage==1,rocks=stage==5;
        foreach(var line in new[]{CourseLine.Wide,CourseLine.Precision}){
            bool precise=line==CourseLine.Precision;
            float x=precise?hard:wide,gap=precise?tightGap:wideGap;
            var gate=Add("gate",at,x,gap,0,trees||rocks?"stoneGate":(precise?"gateRed":"gateBlue")+(stage==6?"Night":""));
            gate.stoneGate=trees||rocks;gate.line=line;gate.points=precise?45:18;
            if(trees||rocks)foreach(int edge in sides){
                if(trees){
                    float stem=x+edge*(gap*.5f+.67f);
                    Tree(200+stem*20,at*20,precise?2:1,.9f);
                    // A second trunk beyond the target groups the trees and reveals the corridor.
                    Tree(200+stem*20,(at+4.8f)*20,precise?1:0,.85f);
                }else{
                    float radius=precise?.70f:.78f;
                    var stone=Rock(200+20*(x+edge*(gap*.5f+radius)),at*20,!precise,20*radius);
                    stone.variant=precise?"gateRockTall":"gateRockWide";
                    stone.scale=radius/(precise?.42f:.77f);stone.length=radius*2;stone.offset=-.09f;
                }
            }
        }
    }
    void ParkBypass(int first,RaceItem ramp,float from,float to,int side){
        // The high jump keeps its actual flight, ring, aerial fruit and feather.
        // Its broad alternative runs along the snow and rejoins the next landing.
        for(int i=first;i<Items.Count;i++)Items[i].line=CourseLine.Precision;
        float launch=ramp.distance+ramp.length*.5f,ground=-side*3.8f;
        float approach=(from-200)*.05f,exit=(to-200)*.05f;
        Add("coin",ramp.distance-9,Mathf.Lerp(approach,ground,.66f),1.1f,0).line=CourseLine.Wide;
        Add("coin",launch,ground,1.1f,0).line=CourseLine.Wide;
        Add("coin",launch+.72f*ramp.span,Mathf.Lerp(ground,exit,.17f),1.1f,0).line=CourseLine.Wide;
        var gate=Add("gate",launch+.44f*ramp.span,ground,4.9f,0,"gateBlue");
        gate.line=CourseLine.Wide;gate.points=18;
    }
}
