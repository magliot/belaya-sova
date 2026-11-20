using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Exercises the same component and camera used by the menu. Throws on the first regression.
public static class RaceWorldChecks {
    static void Require(bool passed,string message){
        if(!passed)throw new InvalidOperationException("Course check: "+message);
    }
    static string Layout(RaceWorld world){
        return string.Join("|",world.Items.Select(o=>o.kind+"/"+o.variant+"/"+
            Mathf.RoundToInt(o.distance*100)+"/"+Mathf.RoundToInt(o.lane*100)));
    }
    static void InFrame(Camera camera,RaceItem item,string label){
        Require(item!=null&&item.Visual&&item.Visual.gameObject.activeInHierarchy,label+" has no active renderer");
        var planes=GeometryUtility.CalculateFrustumPlanes(camera);
        Require(GeometryUtility.TestPlanesAABB(planes,item.Visual.bounds),label+" outside the portrait game camera");
        var p=camera.WorldToViewportPoint(item.Visual.bounds.center);
        Require(p.z>camera.nearClipPlane&&p.x>0&&p.x<1&&p.y>0&&p.y<1,label+" centre outside the portrait viewport");
    }
    static void StartBanner(MountainRace race,float at){
        race.Seek(at);
        var sign=race.World.Items.First(o=>o.kind=="startArch").Visual;
        Require(sign.sharedMaterials.Any(m=>m.IsKeywordEnabled("OWL_START_BANNER")),"start banner lacks proximity fade");
        var t=sign.transform;
        var a=race.View.WorldToViewportPoint(t.TransformPoint(new Vector3(-3.625f,4.6f,0)));
        var b=race.View.WorldToViewportPoint(t.TransformPoint(new Vector3(3.625f,4.6f,0)));
        if(at<=3)Require(a.x>.02f&&b.x<.98f&&b.x-a.x>.36f&&a.z>=14&&b.z>=14,"start banner clipped or faded at "+at+" m");
        else Require(a.z<7&&b.z<7,"start banner still covers camera on passing");
    }
    static void LaneStones(RaceWorld world,int seed,float marker){
        float Spawn(string mode,float approach){
            world.Configure(seed,mode);world.Refresh(marker-60,40,approach);
            var stone=world.Items.First(o=>o.kind=="rock"&&Mathf.Abs(o.distance-marker)<.02f);
            float position=stone.lane;world.Refresh(marker-59,40.05f,-approach);
            Require(position==stone.lane,"stone followed rider after spawn at "+marker);
            var coins=world.Items.Where(o=>o.kind=="coin"&&o.distance>=marker-1.26f&&o.distance<=marker+1.56f).ToArray();
            Require(coins.Length==3&&coins.All(o=>Mathf.Abs(o.lane-position)>1.9f),"stone bypass closed at "+marker);
            return position;
        }
        float left=Spawn("normal",-3),right=Spawn("normal",3),centre=Spawn("review",3);
        Require(Mathf.Abs(right-left-6)<.035f&&Mathf.Abs(right-centre-3)<.035f,"lane targeting at "+marker);
        Require(Mathf.Abs(Spawn("normal",-3)-left)<.001f,"seed+steering failed replay at "+marker);
    }
    public static void RunAt(RaceWorld world){
        var race=world.GetComponent<MountainRace>();
        Require(race&&race.View,"requires the initialized Mountain race scene");
        float aspect=race.View.aspect;
        try{
            race.View.aspect=390f/844;
            race.StartRace("{\"mode\":\"normal\",\"seed\":1911}");
            string first=Layout(world);
            var skier=world.Items.First(o=>o.kid&&!o.sb&&!o.stand);
            var board=world.Items.First(o=>o.sb&&!o.stand);
            var tube=world.Items.First(o=>o.kind=="tube");
            InFrame(race.View,skier,"moving pupil at "+skier.distance);
            InFrame(race.View,board,"downhill snowboarder at "+board.distance);
            Require(world.Items.Any(o=>o.sb&&o.stand)&&world.Items.Any(o=>o.instr),"missing school groups");
            float x=skier.lane,d=skier.distance,tubeD=tube.distance;
            for(int i=1;i<=60;i++)world.Refresh(i*.4f,i/30f);
            Require(skier.distance>d+.5f&&Mathf.Abs(skier.lane-x)>.04f,"skier did not carve");
            Require(tube.distance>tubeD+.5f,"tubing inertia did not advance");
            race.RestartRide();Require(Layout(world)==first,"seed 1911 restart differs");
            race.StartRace("{\"mode\":\"normal\",\"seed\":4903}");
            Require(Layout(world)!=first,"second seed repeats all training objects");
            race.StartRace("{\"mode\":\"review\",\"seed\":1911}");
            StartBanner(race,0);StartBanner(race,3);StartBanner(race,15);
            for(int stage=1;stage<8;stage++){
                float boundary=stage*2500;race.Seek(boundary-32);
                var arch=world.Items.FirstOrDefault(o=>o.kind=="arch"&&Mathf.Abs(o.distance-boundary)<.01f);
                InFrame(race.View,arch,"stage "+stage+" arch");
                var objects=new HashSet<string>();
                for(int step=0;step<12;step++){
                    world.Refresh(boundary+step*86,30+step);
                    foreach(var o in world.Items){
                        if(o.distance<boundary||o.distance>=boundary+1150)continue;
                        Require(o.Visual!=null&&o.Visual.bounds.size.sqrMagnitude>.001f,"missing mesh "+o.variant);
                        objects.Add(o.kind);
                    }
                }
                string[][] required={null,new[]{"tree","snowmobile","npc","ramp"},new[]{"tube","mogulYeti","rail","npc"},
                    new[]{"airRing","feather","ramp","rail","npc"},new[]{"rock","dog","fence","cannon","aid","snowman"},
                    new[]{"gate","rock"},new[]{"mogulYeti","cat","npc","airRing","thermos"},new[]{"gift","coin","npc"}};
                foreach(string kind in required[stage])Require(objects.Contains(kind),"stage "+stage+" lacks "+kind);
            }
            LaneStones(world,1911,10675);LaneStones(world,1911,17507);LaneStones(world,8573,10675);
            world.Configure(1911,"sprint");world.Refresh(1170,100);
            Require(world.Items.Any(o=>o.kind=="finish"&&o.distance==1200),"sprint finish line");
            world.Configure(1911,"tubing");world.Refresh(430,40);
            Require(world.Items.Any(o=>o.kind=="tree")&&world.Items.Any(o=>o.kind=="gate"),"tubing forest and slalom");
            world.Refresh(850,80);Require(world.Items.Any(o=>o.kind=="gift"),"tubing gift finish");
            world.Configure(1911,"sled");
            Require(world.Items.Any(o=>o.kind=="snowman")&&world.Items.Any(o=>o.kind=="gift"),"snowmobile targets and gifts");
            Debug.Log("RACE WORLD CHECK PASS: 8 stages, two seeds, signs, portrait people, moving riders, sprint and bonuses");
        }finally{race.View.aspect=aspect;race.Menu();}
    }
}
