using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class SummitRewardChecks {
    static bool Reward(RaceItem o)=>o.kind=="aid"||o.kind=="gift";
    static void Need(bool ok,string message){if(!ok)throw new InvalidOperationException("Summit route: "+message);}
    static void Ride(MountainRace race,int seed,int start,int finish){
        race.Menu();race.ChooseHero(seed==1911?"owl":"bear");
        race.StartRace("{\"mode\":\"review\",\"reviewRide\":\"normal\",\"sensitivity\":4,\"seed\":"+seed+"}");
        race.Seek(start);race.ReviewLane(race.World.SummitLine(start));race.RideFromReview();race.BeginSteer();
        var reached=new HashSet<RaceItem>();float origin=race.Lane;
        for(int i=0;i<700&&race.Status.phase=="play"&&race.Status.metres<finish;i++){
            foreach(var o in race.World.Items)if(Reward(o)&&o.distance>=start+2&&o.distance<finish-1)reached.Add(o);
            float lead=Mathf.Clamp(race.Status.speed/3.6f*.21f,3.5f,8.2f);
            race.DragSteer((race.World.SummitLine(race.Status.metres+lead)-origin)/(27*.79f));
            race.Advance(1f/60);
        }
        Need(race.Status.metres>=finish&&race.Status.helmets==1,"no clean exit "+seed+" @"+start);
        Need(reached.All(o=>o.consumed),"missed summit gifts or aid "+seed+" @"+start+" "+
            string.Join(",",reached.Where(o=>!o.consumed).Select(o=>o.kind+"@"+o.distance)));
    }
    public static void Run(MountainRace race){
        Ride(race,1911,10480,10512);Ride(race,1911,10659,10688);Ride(race,1911,11230,11259);
        Ride(race,4903,30480,30520);
        var world=race.World;
        foreach(int seed in new[]{1911,8573})foreach(int lap in new[]{0,1}){
            world.Configure(seed,"normal");int count=0;
            int start=10000+20000*lap;
            for(int metres=start;metres<start+2500;metres+=50){
                world.Refresh(metres,metres/20f,0,false);
                foreach(var o in world.Items.Where(o=>o.distance>=metres&&o.distance<metres+50)){
                    if(Reward(o)){
                        Need(Mathf.Abs(o.lane-world.SummitLine(o.distance))<.055f,"split reward at "+o.distance);count++;
                    }
                    if(o.kind=="helmet"||o.kind=="thermos")Need(Mathf.Abs(o.lane-world.SummitLine(o.distance))>2.8f,"forced boost at "+o.distance);
                }
            }
            Need(count>0,"missing gifts and aid on visit "+lap+" seed "+seed);
        }
        race.Menu();
        Debug.Log("SUMMIT ROUTE PASS: archived and generated visits across two seeds, four clear passages in play, first-aid detour joined and optional boosts separate.");
    }
}
