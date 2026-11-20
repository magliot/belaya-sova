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
    public static void Run(MountainRace race){
        var world=race.World;
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
        // Run the ordinary state machine across the first loop boundary and into each branch.
        race.Status.metres=19998;race.Advance(.17f);
        Need(race.Status.phase=="play"&&race.Status.metres>20000,"ordinary race stops at 20 km");
        foreach(int start in new[]{22500,35000}){
            race.Status.metres=start+92;race.Advance(.04f);
            Need(race.Status.stage==RaceWorld.StageAt(start),"stage after ordinary advance");
            var lines=world.Items.Where(o=>o.kind=="coin"&&Mathf.Abs(o.distance-start-123)<.04f).ToArray();
            Need(lines.Length==2&&lines[0].lane*lines[1].lane< -7,"both fork lines must be visible on approach at "+start);
            Need(lines.All(o=>o.Visual&&o.Visual.gameObject.activeInHierarchy),"invisible split pickups");
            if(start==35000)Need(world.Items.Any(o=>o.variant.EndsWith("Night")&&o.Visual&&o.distance>=start+100),"dark fork missing illumination");
            race.Status.metres=start+410;race.Advance(.04f);
            Need(world.Items.Any(o=>o.kind=="coin"&&Mathf.Abs(o.distance-start-458)<.04f&&Mathf.Abs(o.lane)<.05f),"branches fail to rejoin");
            Need(!world.Items.Any(o=>o.distance>start+425&&o.distance<start+500&&o.kind!="coin"),"recovery obstructed");
            string one=Signature(world,1911,start+115);
            Need(one==Signature(world,1911,start+115),"seeded replay changed");
            float h1=world.HeadingAt(start+240);
            Signature(world,4903,start+115);float h2=world.HeadingAt(start+240);
            Need(Mathf.Abs(h1-h2)>.0005f,"bend geometry ignores seed");
        }
        race.StartRace("{\"mode\":\"normal\",\"seed\":1911}");race.Status.metres=5013;race.Advance(.035f);
        var coins=world.Items.Where(o=>o.kind=="coin"&&o.distance>5020&&o.distance<5110).ToArray();
        Need(coins.Length>=5&&coins.All(o=>Mathf.Abs(o.lane-world.MogulLine(o.distance))<.02f),"mogul rewards miss the groomed trail");
        Need(world.Items.Any(o=>o.kind=="mogul"&&o.Visual&&Mathf.Abs(o.lane-world.MogulLine(o.distance))>3),"mound blocks the groomed trail");
        var piste=world.transform.Find("Groomed run").GetComponent<MeshFilter>().sharedMesh;
        Need(piste.subMeshCount==4&&piste.GetIndexCount(2)>0,"groomed strip has no visible geometry");
        for(int d=5020;d<5200;d+=3){
            float path=world.MogulLine(d);Need(world.MogulRoughness(d,path)==0&&world.MogulRoughness(d,path+5)>.85f,"flat and rough lanes lack contrast");
        }
        race.StartRace("{\"mode\":\"review\",\"seed\":1911}");
        foreach(int start in new[]{22500,35000}){
            race.Seek(start+92);Need(race.Status.metres==start+92,"inspector clipped a late stage");
            var markers=world.Items.Where(o=>o.kind=="coin"&&Mathf.Abs(o.distance-start-123)<.04f).ToArray();
            Need(piste.GetIndexCount(3)>0,"fork has no packed snow tracks");
            Need(markers.Length==2,"inspector and ordinary fork differ");
            foreach(var mark in markers){
                var p=race.View.WorldToViewportPoint(mark.Visual.bounds.center);
                Need(p.z>15&&p.x>.01f&&p.x<.99f&&p.y>.01f&&p.y<.99f,"fork outside portrait game camera at "+start);
            }
        }
        Debug.Log("ENDLESS COURSE PASS: 5 forest and 5 night scenarios, 80 seeded segments per theme/seed, both branches and recovery, ordinary 20 km transition, mogul trail and review.");
    }
}
