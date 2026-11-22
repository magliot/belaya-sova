using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FruitScatterChecks {
    const string pending="Owl.CheckFruitScatter";
    static FruitScatterChecks(){EditorApplication.update+=CheckBatch;}
    public static void Batch(){
        SessionState.SetBool(pending,true);
        EditorSceneManager.OpenScene("Assets/Mountain.unity");EditorApplication.EnterPlaymode();
    }
    static void CheckBatch(){
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||!SessionState.GetBool(pending,false))return;
        var race=UnityEngine.Object.FindFirstObjectByType<MountainRace>();
        if(!race||!race.View)return;
        SessionState.EraseBool(pending);
        try{Run(race);EditorApplication.Exit(0);}
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(2);}
    }
    static void Need(bool ok,string detail){
        if(!ok)throw new InvalidOperationException("Fruit scatter: "+detail);
    }
    static RaceItem[] Fruit(RaceWorld world,float start,float end){
        return world.Items.Where(o=>o.kind=="coin"&&!o.air&&o.distance>=start&&o.distance<end)
            .OrderBy(o=>o.distance).ToArray();
    }
    static string Signature(RaceWorld world,float from,float to){
        return string.Join(";",Fruit(world,from,to).Select(o=>Mathf.RoundToInt(o.distance*100)+","+Mathf.RoundToInt(o.lane*100)));
    }
    static void RideGroup(MountainRace race,string hero,int seed,bool park){
        race.Menu();race.ChooseHero(hero);
        race.StartRace("{\"mode\":\""+(park?"review":"normal")+"\",\"reviewRide\":\"normal\",\"sensitivity\":4,\"seed\":"+seed+"}");
        if(park)race.Seek(12000);
        var fruit=Fruit(race.World,park?12014:0,park?12110:105);
        Need(fruit.Length>=2,"empty stretch for "+seed);
        var group=new List<RaceItem>{fruit[0]};
        for(int i=1;i<fruit.Length&&fruit[i].distance-fruit[i-1].distance<5;i++)group.Add(fruit[i]);
        if(park){
            var first=group[0];
            race.ReviewLane(first.lane-Mathf.Sign(first.lane)*1.7f);
            race.Seek(first.distance-11);race.RideFromReview();
        }
        float origin=race.Lane;
        race.BeginSteer();
        var last=group[group.Count-1];int target=0;
        for(int frame=0;frame<750&&race.Status.phase=="play"&&race.Status.metres<last.distance+1.6f;frame++){
            if(target+1<group.Count&&group[target].distance<race.Status.metres+.9f)target++;
            race.DragSteer((group[target].lane-origin)/(27*.79f));race.Advance(1f/60);
        }
        Need(race.Status.phase=="play"&&group.All(o=>o.consumed),"unreachable cluster "+hero+" seed "+seed+" at "+last.distance);
        if(park)Need(race.Status.speed>=60&&race.Status.helmets==1,"loss of speed or helmet on park fruit");
        Debug.Log("FRUIT PICKUP "+hero+" seed "+seed+" "+group.Count+" fruit at "+group[0].distance.ToString("F1")+".."+last.distance.ToString("F1")+" m, "+race.Status.speed+" km/h");
    }
    public static void Run(MountainRace race){
        var world=race.World;var layouts=new HashSet<string>();
        int singles=0,groups=0,inGate=0,wide=0,total=0,sparsest=int.MaxValue;
        foreach(int seed in new[]{1911,783,4903}){
            world.Configure(seed,"normal");
            for(int d=0;d<=360;d+=18)world.Refresh(d,d/16f,2,false);
            var streamed=Signature(world,336,411);
            world.Configure(seed,"normal");world.Refresh(352,23,-5,false);
            Need(streamed.Length>0&&streamed==Signature(world,336,411),"seek changed neighbouring rows "+seed);
            layouts.Add(streamed);
            world.Refresh(312,21,0,false);
            Debug.Log("FRUIT LAYOUT seed "+seed+" from 312 to 375 m: "+string.Join("; ",Fruit(world,312,375).Select(o=>o.distance.ToString("F1")+"@"+o.lane.ToString("F1"))));
            for(int stage=0;stage<8;stage++){
                float start=stage*RaceWorld.StageLength;int fruitCount=0,left=0,right=0;
                for(int scan=0;scan<700;scan+=70){
                    float at=start+scan;world.Refresh(at,at/24f,0,false);
                    var fruit=Fruit(world,at,at+70);
                    foreach(var o in fruit){
                        fruitCount++;if(o.lane< -1.5f)left++;if(o.lane>1.5f)right++;
                        Need(Mathf.Abs(o.lane)<world.WidthAt(o.distance)*.5f-1.05f,"fruit on bank "+o.distance);
                        foreach(var obstacle in world.Items){
                            if(obstacle.kind=="gate"){
                                if(Mathf.Abs(obstacle.distance-o.distance)>3)continue;
                                var delta=Mathf.Abs(obstacle.lane-o.lane);
                                if(delta<obstacle.width*.5f-.7f)inGate++;
                                if(delta>obstacle.width*.5f+1.2f)wide++;
                            }else if(obstacle.kind=="tree"||obstacle.kind=="rock"||obstacle.kind=="fence"||obstacle.kind=="ramp"||obstacle.kind=="snowman"){
                                if(Mathf.Abs(obstacle.distance-o.distance)>Mathf.Max(.9f,obstacle.length*.5f))continue;
                                Need(Mathf.Abs(o.lane-obstacle.lane)>obstacle.width*.5f+.42f,"fruit over "+obstacle.kind+" "+o.distance);
                            }
                        }
                    }
                    for(int i=1;i<fruit.Length-1;i++){
                        float prev=fruit[i].distance-fruit[i-1].distance,next=fruit[i+1].distance-fruit[i].distance;
                        if(prev>5&&next>5)singles++;
                        if(prev<5){
                            groups++;
                            Need(Mathf.Abs(fruit[i].lane-fruit[i-1].lane)<=.13f*prev+.01f,"impossible turn in cluster at "+fruit[i].distance);
                        }
                    }
                }
                Need(fruitCount>=10&&left>0&&right>0,"empty side or stage "+stage+" seed "+seed+" total "+fruitCount+" left/right "+left+"/"+right);
                total+=fruitCount;sparsest=Mathf.Min(sparsest,fruitCount);
            }
            foreach(string hero in new[]{"owl","bear"}){RideGroup(race,hero,seed,false);RideGroup(race,hero,seed,true);}
        }
        Need(layouts.Count==3&&singles>20&&groups>100&&inGate>5&&wide>5,"variety or gate/outside distribution "+singles+","+groups+","+inGate+","+wide);
        race.Menu();
        Debug.Log("GROUND FRUIT PASS 3 seeds, all 8 stages, ordinary start both heroes, replay equals seek. "+total+" ground fruit over 16.8 km; minimum "+sparsest+" per sampled 700 m. Single/group members "+singles+"/"+groups+", gate/outside "+inGate+"/"+wide);
    }
}
