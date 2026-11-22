using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class RaceRouteChecks {
    static void Need(bool ok,string message){if(!ok)throw new InvalidOperationException("Endless course: "+message);}
    static string Signature(RaceWorld world,int seed,int marker){
        world.Configure(seed,"normal");world.Refresh(marker,50);
        return string.Join("|",world.Items.Select(o=>o.variant+"/"+o.lane.ToString("F2")+"/"+o.distance.ToString("F2")));
    }
    static void TrainingTraffic(MountainRace race){
        foreach(int seed in new[]{1911,19,783}){
            race.StartRace("{\"mode\":\"review\",\"seed\":"+seed+"}");
            race.Seek(304);
            var objects=race.World.Items;
            var gates=objects.Where(o=>o.kind=="gate"&&o.distance>=312&&o.distance<375).ToArray();
            Need(gates.Length==1&&Mathf.Abs(gates[0].distance-347.5f)<.01f,"expected one training gate on seed "+seed);
            var skier=objects.Single(o=>o.variant=="skier"&&o.distance>312&&o.distance<330);
            Need(objects.Any(o=>o.kind=="gate"&&Mathf.Abs(o.distance-400)<.01f),"missing next gate");
            foreach(var obj in new[]{skier,gates[0]}){
                var p=race.View.WorldToViewportPoint(obj.Visual.bounds.center);
                Need(p.z>10&&p.x>0&&p.x<1&&p.y>0&&p.y<1,"gate or skier hidden on approach for "+seed);
            }
            float initialD=skier.distance,initialSide=Mathf.Abs(skier.lane-gates[0].lane);
            for(int i=1;i<=720&&skier.distance<gates[0].distance;i++)race.World.Refresh(304,40+i/60f);
            float finalSide=Mathf.Abs(skier.lane-gates[0].lane);
            Need(skier.distance>initialD+20&&finalSide<initialSide-2&&finalSide>0.15f&&finalSide<2.2f,
                "skier did not glide into the opening for "+seed+" from "+initialSide+" to "+finalSide);
        }
    }
    public static void Run(MountainRace race){
        var world=race.World;
        TrainingTraffic(race);
        foreach(int seed in new[]{1911,4903,17,158,-603}){
            foreach(int stage in new[]{1,6}){
                int previous=-1,older=-1;
                for(int visit=1;visit<17;visit++){
                    var scenarios=new HashSet<int>();
                    for(int section=0;section<5;section++){
                        int current=RaceWorld.RouteScenario(seed,stage,visit,section);
                        Need(current!=previous&&current!=older,"repeated either recent scenario");scenarios.Add(current);
                        older=previous;previous=current;
                    }
                    Need(scenarios.Count==5,"missing scenarios on stage "+stage+" for "+seed);
                }
            }
        }
        race.StartRace("{\"mode\":\"normal\",\"hero\":\"bear\",\"seed\":1911}");
        // Run the ordinary state machine across the first loop boundary and into the later forest and night scenes.
        race.Status.metres=RaceWorld.LapLength-2;race.Advance(.17f);
        Need(race.Status.phase=="play"&&race.Status.metres>RaceWorld.LapLength,"ordinary race stops at 22.5 km");
        foreach(int start in new[]{25000,37500}){
            race.Status.metres=start+92;race.Advance(.04f);
            Need(race.Status.stage==RaceWorld.StageAt(start),"stage after ordinary advance");
            var gate=world.Items.Single(o=>o.kind=="gate"&&Mathf.Abs(o.distance-start-107)<.05f);
            Need(gate.Visual&&gate.Visual.gameObject.activeInHierarchy,"single gate missing on repeat at "+start);
            if(start==37500)Need(gate.variant.EndsWith("Night"),"night gate lamps missing");
            race.Status.metres=start+410;race.Advance(.04f);
            Need(!world.Items.Any(o=>o.distance>start+425&&o.distance<start+500&&o.kind!="coin"),"recovery obstructed");
            string one=Signature(world,1911,start+115);
            Need(one==Signature(world,1911,start+115),"seeded replay changed");
            float h1=world.HeadingAt(start+240);
            Signature(world,4903,start+115);float h2=world.HeadingAt(start+240);
            Need(Mathf.Abs(h1-h2)>.0005f,"bend geometry ignores seed");
        }
        race.StartRace("{\"mode\":\"normal\",\"seed\":1911}");race.Status.metres=5013;race.Advance(.035f);
        Need(world.Items.Any(o=>o.kind=="mogul"&&o.Visual&&Mathf.Abs(o.lane-world.MogulLine(o.distance))>3),"mound blocks the groomed trail");
        var piste=world.transform.Find("Groomed run").GetComponent<MeshFilter>().sharedMesh;
        Need(piste.subMeshCount==3&&piste.GetIndexCount(2)>0,"groomed strip has no visible geometry");
        for(int d=5020;d<5200;d+=3){
            float path=world.MogulLine(d);Need(world.MogulRoughness(d,path)==0&&world.MogulRoughness(d,path+5)>.85f,"flat and rough lanes lack contrast");
        }
        race.StartRace("{\"mode\":\"review\",\"seed\":1911}");
        foreach(int start in new[]{25000,37500}){
            race.Seek(start+92);Need(race.Status.metres==start+92,"inspector clipped a late stage");
            var gate=world.Items.Single(o=>o.kind=="gate"&&Mathf.Abs(o.distance-start-107)<.05f);
            var p=race.View.WorldToViewportPoint(gate.Visual.bounds.center);
            Need(p.z>10&&p.x>.01f&&p.x<.99f&&p.y>.01f&&p.y<.99f,"gate outside portrait game camera at "+start);
        }
        Debug.Log("OPEN COURSE PASS: one visible gate and a merging skier on three seeds, five repeat scenarios per theme, clear run-out and ordinary 22.5 km transition.");
    }
}
