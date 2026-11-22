using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class FestivalCourseChecks {
    static void Need(bool ok,string message){if(!ok)throw new InvalidOperationException("Holiday slalom: "+message);}
    public static void Run(MountainRace race){
        foreach(var start in new[]{17500,17500+RaceWorld.LapLength}){
            race.Menu();race.ChooseHero(start==17500?"bear":"owl");
            race.StartRace("{\"mode\":\"review\",\"reviewRide\":\"normal\",\"sensitivity\":4,\"seed\":1911}");
            race.Seek(start+2);race.RideFromReview();race.BeginSteer();
            var items=new HashSet<RaceItem>();var world=race.World;
            float origin=race.Lane;
            for(int tick=0;tick<1000&&race.Status.phase=="play"&&race.Status.metres<start+149;tick++){
                foreach(var o in world.Items)if(o.distance>=start&&o.distance<start+150)items.Add(o);
                float lead=Mathf.Clamp(race.Status.speed/3.6f*.21f,3.5f,8.2f);
                race.DragSteer((world.HolidayLine(race.Status.metres+lead)-origin)/(27*.79f));
                race.Advance(1f/60);
            }
            Need(race.Status.phase=="play"&&race.Status.metres>=start+149&&race.Status.helmets==1,"obstructed route "+start);
            Need(items.All(o=>o.kind!="gate"),"a pole gate is still on the festive piste");
            var prizes=items.Where(o=>o.kind=="gift"||o.kind=="aid").ToArray();
            Need(prizes.Length==3&&prizes.All(o=>o.consumed),"missed gifts or first-aid kit near "+start+": "+
                string.Join(",",prizes.Where(o=>!o.consumed).Select(o=>o.kind+"@"+o.distance)));
            Need(items.Any(o=>o.kind=="thermos"&&!o.consumed),"coffee should be a separate choice");
            var pots=items.Where(o=>o.kind=="festivalFountain").OrderBy(o=>o.distance).ToArray();
            Need(pots.Length==3&&pots.All(o=>o.judged)&&pots[0].lane*pots[1].lane<0,"alternating fountain passage");
            Need(items.Any(o=>o.variant=="holidayFir"),"no decorated trees");
        }
        race.Menu();
        Debug.Log("HOLIDAY SLALOM PASS: first and second visit, gifts and aid, optional coffee, three fountains, intact helmet.");
    }
}
