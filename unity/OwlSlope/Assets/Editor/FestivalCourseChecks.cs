using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class FestivalCourseChecks {
    const int Entry=7*RaceWorld.StageLength;
    static void Need(bool ok,string message){if(!ok)throw new InvalidOperationException("Holiday gates: "+message);}
    static void Review(MountainRace race,string hero,float metres){
        race.Menu();race.ChooseHero(hero);
        race.StartRace("{\"mode\":\"review\",\"reviewRide\":\"normal\",\"sensitivity\":4,\"seed\":1911}");
        race.Seek(metres);
    }
    static void AimAt(MountainRace race,string hero,int side,bool outside){
        Review(race,hero,Entry+15);
        var pair=race.World.Items.Where(o=>o.kind=="festivalFountain"&&Mathf.Abs(o.distance-Entry-25)<.1f).OrderBy(o=>o.lane).ToArray();
        Need(pair.Length==2,"missing first gate");
        var pot=pair[side<0?0:1];
        race.ReviewLane(pot.lane+(outside?side*2.2f:0));race.RideFromReview();race.BeginSteer();
        for(int i=0;i<160&&race.Status.metres<Entry+38&&race.Status.phase=="play";i++)race.Advance(1f/60);
        Need(race.Status.gates==0&&race.Status.helmets==(outside?1:0),"wrong outcome for "+hero+" side "+side+" outside "+outside+" at "+race.Status.metres);
        if(!outside){
            var struck=race.World.Items.First(o=>o.kind=="festivalFountain"&&Mathf.Abs(o.distance-Entry-25)<.1f&&Mathf.Abs(o.lane-pot.lane)<.1f);
            Need(struck.judged&&struck.Visual&&Vector3.Angle(struck.Visual.transform.up,Vector3.up)>36,"hit fountain did not tip");
        }
    }
    public static void Run(MountainRace race){
        foreach(var start in new[]{Entry,Entry+RaceWorld.LapLength}){
            Review(race,start==Entry?"bear":"owl",start+2);
            race.RideFromReview();race.BeginSteer();
            var items=new HashSet<RaceItem>();var world=race.World;
            float origin=race.Lane;
            for(int tick=0;tick<1100&&race.Status.phase=="play"&&race.Status.metres<start+149;tick++){
                foreach(var o in world.Items)if(o.distance>=start&&o.distance<start+150)items.Add(o);
                float lead=Mathf.Clamp(race.Status.speed/3.6f*.21f,3.5f,8.2f);
                race.DragSteer((world.HolidayLine(race.Status.metres+lead)-origin)/(27*.79f));
                race.Advance(1f/60);
            }
            Need(race.Status.phase=="play"&&race.Status.metres>=start+149&&race.Status.helmets==1,"obstructed route "+start);
            var gates=items.Where(o=>o.kind=="gate").ToArray();
            Need(gates.Length==3&&race.Status.gates==3&&gates.All(o=>o.variant=="fountainGate"&&o.judged),"three clean fountain gates");
            foreach(var gate in gates){
                var pair=items.Where(o=>o.kind=="festivalFountain"&&Mathf.Abs(o.distance-gate.distance)<.1f).OrderBy(o=>o.lane).ToArray();
                Need(pair.Length==2&&pair[0].lane<gate.lane-3&&pair[1].lane>gate.lane+3&&pair.All(o=>!o.judged),"fountains must frame a clear gate");
            }
            var prizes=items.Where(o=>o.kind=="gift"||o.kind=="aid").ToArray();
            Need(prizes.Length==3&&prizes.All(o=>o.consumed),"missed gifts or first-aid kit near "+start);
            Need(items.Any(o=>o.kind=="thermos"&&!o.consumed)&&items.Any(o=>o.variant=="holidayFir"),"coffee / decorated trees");
        }
        foreach(string hero in new[]{"bear","owl"}){
            AimAt(race,hero,-1,false);AimAt(race,hero,1,false);AimAt(race,hero,-1,true);
        }
        race.Menu();Debug.Log("HOLIDAY GATES PASS: 3 clean gates per visit, both posts tip and cost a helmet, outside pass intact, both heroes.");
    }
}
