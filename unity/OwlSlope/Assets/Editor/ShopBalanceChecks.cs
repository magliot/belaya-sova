using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class ShopBalanceChecks {
    [Serializable] public class InputSample { public float at,drag; public bool steer; }
    [Serializable] public class Ride {
        public int seed,coins,metres,score;
        public string hero;
        public List<InputSample> input=new List<InputSample>();
    }
    [Serializable] public class Series { public List<Ride> rides=new List<Ride>(); }
    // A repeatable sample of ordinary play: sight the nearest fruit, steer in short drags,
    // and release between groups. No seeking, knowledge of future hazards, or forced finish.
    public static void Run(MountainRace race){
        var harvest=new List<int>();
        var series=new Series();
        foreach(int seed in new[]{19,783,1911,1927,4903,8573}){
            string hero=harvest.Count%2==0?"bear":"owl";
            race.Menu();race.ChooseHero(hero);
            race.StartRace("{\"mode\":\"normal\",\"seed\":"+seed+",\"sensitivity\":4}");
            var ride=new Ride{hero=hero,seed=seed};series.rides.Add(ride);
            bool careful=harvest.Count%2==1;
            int frame=0;const float delta=1f/20;
            while(race.Status.phase=="play"&&race.Status.metres<3500&&frame<15000){
                int interval=6+seed%5;
                if(frame%interval==0){
                    race.ReleaseSteer();
                    float sight=(careful?17:12)+seed%8;
                    var fruit=race.World.Items.Where(o=>!o.consumed&&o.kind=="coin"&&!o.air&&
                        o.distance>race.Status.metres+2&&o.distance<race.Status.metres+sight)
                        .OrderBy(o=>o.distance).FirstOrDefault();
                    float aim=fruit!=null?fruit.lane:race.Lane;
                    var person=race.World.Items.Where(o=>(o.kind=="npc"||o.kind=="tube"||o.kind=="cat")&&
                        o.distance>=race.Status.metres-.5f&&o.distance<race.Status.metres+(careful?11:5)&&Mathf.Abs(o.lane-aim)<2.3f)
                        .OrderBy(o=>o.distance).FirstOrDefault();
                    if(person!=null)aim=person.lane+(person.lane>0?-2.9f:2.9f);
                    var sample=new InputSample{at=race.Status.metres,steer=fruit!=null||person!=null};
                    if(sample.steer){
                        float error=Mathf.Sin(race.Status.elapsed*1.3f+seed)*(careful?.24f:.52f);
                        sample.drag=(aim+error-race.Lane)/(27*.79f);
                        race.BeginSteer();race.DragSteer(sample.drag);
                    }
                    ride.input.Add(sample);
                }
                race.Advance(delta);frame++;
            }
            if(race.Status.phase!="over")throw new Exception("Ordinary economy sample did not reach a fall, seed "+seed);
            var s=race.Status;harvest.Add(s.coins);
            ride.coins=s.coins;ride.score=s.score;ride.metres=(int)s.metres;
            Debug.Log("SHOP RIDE seed="+seed+" hero="+hero+" metres="+(int)s.metres+" fruit="+s.coins+" score="+s.score+
                " gates="+s.gates+" rings="+s.rings+" fis="+s.fis.Length+" | "+JsonUtility.ToJson(s));
        }
        var ordered=harvest.OrderBy(n=>n).ToArray();
        File.WriteAllText(Path.Combine(Path.GetTempPath(),"OwlSaleRides.json"),JsonUtility.ToJson(series));
        Debug.Log("SHOP HARVEST median="+.5f*(ordered[2]+ordered[3])+" total="+harvest.Sum()+" runs="+harvest.Count);
        race.VerifyEquipment();
    }
}
