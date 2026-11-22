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
        Require(sign.sharedMaterials.All(m=>m.IsKeywordEnabled("OWL_START_BANNER")),"start arch beam and sign must fade together");
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
            Require(Mathf.Abs(world.SummitLine(marker)-position)>2.5f,"stone blocks its safe passage at "+marker);
            return position;
        }
        float left=Spawn("normal",-3),right=Spawn("normal",3),centre=Spawn("review",3);
        Require(Mathf.Abs(right-left)<.001f&&Mathf.Abs(right-centre)<.001f,"stone placement changed with steering at "+marker);
        Require(Mathf.Abs(Spawn("normal",-3)-left)<.001f,"seed+steering failed replay at "+marker);
    }
    static float RubberGap(Renderer rubber,MeshCollider piste){
        var skin=rubber as SkinnedMeshRenderer;
        var mesh=skin?new Mesh():rubber.GetComponent<MeshFilter>().sharedMesh;
        float low=float.PositiveInfinity;
        try{
            if(skin)skin.BakeMesh(mesh);
            foreach(var v in mesh.vertices){
                if(v.y>.12f)continue;
                var p=rubber.transform.TransformPoint(v);
                if(piste.Raycast(new Ray(p+Vector3.up*3,Vector3.down),out var hit,6))low=Mathf.Min(low,p.y-hit.point.y);
            }
        }finally{if(skin)UnityEngine.Object.Destroy(mesh);}
        return low;
    }
    public static void CheckSnowContact(MountainRace race){
        var world=race.World;
        race.StartRace("{\"mode\":\"normal\",\"seed\":1911}");
        var tube=world.Items.First(o=>o.kind=="tube");
        var piste=world.transform.Find("Groomed run").GetComponent<MeshFilter>();
        var surface=piste.gameObject.AddComponent<MeshCollider>();int measured=0;float maxError=0;
        float worstTilt=0,worstRubberGap=0;
        try{
            for(int i=1;i<=180;i++){
                world.Refresh(i*.25f,i/60f);
                var t=tube.Visual.transform;
                Require(Vector3.Distance(t.position,world.SnowSurface(tube.distance,tube.lane))<.005f,"NPC tube left the snow");
                worstTilt=Mathf.Max(worstTilt,Vector3.Angle(t.up,world.SnowFrame(tube.distance,tube.lane)*Vector3.up));
                if(i%45==0){
                    surface.sharedMesh=null;surface.sharedMesh=piste.sharedMesh;
                    worstRubberGap=Mathf.Max(worstRubberGap,Mathf.Abs(RubberGap((SkinnedMeshRenderer)tube.Visual,surface)));
                }
            }
            Require(worstTilt<3.5f,"NPC tube rolled under the snow by "+worstTilt+" degrees");
            Require(!tube.Visual.sharedMaterials.Any(m=>m.renderQueue>2500),"baked tube shadow intersects the snow");
            race.StartRace("{\"mode\":\"tubing\",\"seed\":1911}");race.BeginSteer();race.DragSteer(.22f);
            for(int i=0;i<36;i++)race.Advance(1f/60);
            var riderTube=race.transform.Find("Selected rider").GetComponentsInChildren<MeshRenderer>()
                .First(s=>s.name.StartsWith("playerTube",StringComparison.Ordinal));
            surface.sharedMesh=null;surface.sharedMesh=piste.sharedMesh;
            worstRubberGap=Mathf.Max(worstRubberGap,Mathf.Abs(RubberGap(riderTube,surface)));
            Require(worstRubberGap<.08f,"rubber/snow separation at contact "+worstRubberGap+" m");
            race.StartRace("{\"mode\":\"review\",\"seed\":1911}");
            foreach(float at in new[]{2530f,5100f,7620f,15500f}){
                race.Seek(at);surface.sharedMesh=null;surface.sharedMesh=piste.sharedMesh;
                foreach(var item in world.Items.Where(o=>o.kind=="ramp"||o.kind=="rail")){
                    if(item.distance<at-18||item.distance>at+105)continue;
                    var mesh=item.Visual.GetComponent<MeshFilter>().sharedMesh;
                    // Read actual bottom vertices on both sides of the mesh, raycast against the rendered snow.
                    var verts=mesh.vertices;var colors=mesh.colors32;
                    for(int i=0;i<verts.Length;i++)if(colors[i].r==44){
                        var p=item.Visual.transform.TransformPoint(verts[i]);
                        Require(surface.Raycast(new Ray(p+Vector3.up*5,Vector3.down),out var hit,12),"ramp lacks snow under footing");
                        maxError=Mathf.Max(maxError,Mathf.Abs(hit.point.y-p.y-.12f));measured++;
                    }
                }
            }
            Require(measured>=42&&maxError<.012f,"ramp/snow skirt contacts "+measured+", max error "+maxError);
        }finally{UnityEngine.Object.Destroy(surface);}
        foreach(var key in new[]{"thermos","helmet","aid","gift","feather","magnet"}){
            var item=world.CreateVisual(key,world.transform);var glow=item.transform.Find("Pickup glow");
            Require(glow&&glow.GetComponent<MeshFilter>().sharedMesh.bounds.size.z>1,"missing volume for "+key);
            Require(glow.GetComponent<Renderer>().sharedMaterial.shader.isSupported,"unsupported pickup shader "+key);
            UnityEngine.Object.Destroy(item);
        }
        Debug.Log("SNOW CONTACT PASS: tube max roll "+worstTilt.ToString("F2")+" degrees, max rubber/snow gap "+
            (worstRubberGap*1000).ToString("F0")+" mm; "+measured+
            " ramp-footing raycasts, max error "+(1000*maxError).ToString("F1")+" mm; 5 volumetric pickup meshes");
    }
    public static void RunAt(RaceWorld world){
        var race=world.GetComponent<MountainRace>();
        Require(race&&race.View,"requires the initialized Mountain race scene");
        float aspect=race.View.aspect;
        try{
            race.View.aspect=390f/844;
            race.StartRace("{\"mode\":\"normal\",\"seed\":1911}");
            string first=Layout(world);
            Require(world.Items.All(o=>o.kind!="boundaryPole")&&
                !world.GetComponentsInChildren<Renderer>().Any(r=>r.name=="lamp · stageLamp0"),"training edge posts remain");
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
            foreach(var jump in new[]{("normal",15459.5f,"thermos"),("sprint",750f,"thermos"),("sprint",1050f,"helmet")}){
                world.Configure(1911,jump.Item1);world.Refresh(jump.Item2,40);
                float at=jump.Item2+4.5f;
                var outer=world.Items.Single(o=>o.kind==jump.Item3&&Mathf.Abs(o.distance-at)<.02f);
                var ramp=world.Items.First(o=>o.kind=="ramp"&&o.distance>at&&o.distance<at+30);
                Require(!outer.air&&Mathf.Abs(outer.lane-ramp.lane)>3.5f,"short kicker forces "+jump.Item3);
                if(jump.Item1=="sprint")Require(world.Items.Any(o=>o.kind=="coin"&&Mathf.Abs(o.distance-at)<.02f),
                    "short sprint kicker lost its approach fruit");
                Require(world.Items.Count(o=>o.kind=="coin"&&o.air&&o.distance>at&&o.distance<at+60)==4,
                    "missing aerial fruit at "+at);
            }
            race.StartRace("{\"mode\":\"review\",\"seed\":1911}");
            StartBanner(race,0);StartBanner(race,3);StartBanner(race,15);
            for(int stage=1;stage<RaceWorld.StageCount;stage++){
                float boundary=stage*2500;race.Seek(boundary-32);
                var arch=world.Items.FirstOrDefault(o=>o.kind=="arch"&&Mathf.Abs(o.distance-boundary)<.01f);
                InFrame(race.View,arch,"stage "+stage+" arch");
                var objects=new HashSet<string>();
                for(int step=0;step<12;step++){
                    world.Refresh(boundary+step*86,30+step);
                    foreach(var o in world.Items){
                        if(o.distance<boundary||o.distance>=boundary+1150)continue;
                        if(!o.stoneGate)Require(o.Visual!=null&&o.Visual.bounds.size.sqrMagnitude>.001f,"missing mesh "+o.variant);
                        objects.Add(o.kind);
                    }
                }
                string[][] required={null,new[]{"tree","snowmobile","npc","ramp"},new[]{"tube","mogulYeti","rail","npc"},
                    new[]{"airRing","feather","ramp","rail","npc"},new[]{"rock","dog","fence","cannon","aid","snowman"},
                    new[]{"gate","rock","npc","tube"},new[]{"mogulYeti","cat","npc","airRing","thermos"},new[]{"gift","coin","npc"},
                    new[]{"coin","rock","snowBoulder","arch"}};
                foreach(string kind in required[stage])Require(objects.Contains(kind),"stage "+stage+" lacks "+kind);
            }
            world.Configure(1911,"normal");world.Refresh(15064,1000);
            var nightGate=world.Items.First(o=>o.kind=="gate"&&o.distance>15110);
            var caps=nightGate.Visual.transform.Find("Огни на стойках");
            Require(caps&&caps.GetComponent<MeshFilter>().sharedMesh.vertexCount==8&&caps.GetComponent<MeshRenderer>().sharedMaterial.shader.isSupported,
                "night slalom needs a distant light on both pole caps");
            LaneStones(world,1911,10675);LaneStones(world,8573,10675);
            FruitScatterChecks.Run(race);
            FestivalCourseChecks.Run(race);
            SummitRewardChecks.Run(race);
            foreach(int seed in new[]{1911,4903}){
                world.Configure(seed,"normal");
                foreach(var passage in new[]{(at:2510,tree:"canopyPine"),(at:2710,tree:"canopyFir")}){
                    world.Refresh(passage.at,passage.at/14f);
                    var canopy=world.Items.Where(o=>o.kind=="tree"&&o.variant==passage.tree&&o.distance>passage.at+7&&o.distance<passage.at+14)
                    .OrderBy(o=>o.lane).ToArray();
                    Require(canopy.Length==2&&canopy[1].lane-canopy[0].lane>8,"forest canopy requires two separated roots");
                    float chamber=0;
                    for(int i=0;i<canopy.Length;i++){
                        var stem=canopy[i];var root=stem.Visual.transform;
                        var across=world.SnowSurface(stem.distance,stem.lane+.5f)-world.SnowSurface(stem.distance,stem.lane-.5f);
                        var inward=Vector3.ProjectOnPlane(across,Vector3.up).normalized*(i==0?1:-1);
                        Require(Vector3.Dot(root.up,inward)>.12f&&stem.Visual.sharedMaterial.GetTexture("_BaseMap"),
                            "textured forest crowns must lean over the passage");
                        Require(root.position.y<=world.SnowSurface(stem.distance,stem.lane).y,"forest root above snow");
                        var mesh=root.GetComponent<MeshFilter>().sharedMesh;
                        float lowReach=0,highReach=0;
                        foreach(var v in mesh.vertices){
                            var delta=root.TransformVector(v);
                            if(delta.y<2.5f)lowReach=Mathf.Max(lowReach,Vector3.Dot(delta,inward));
                            if(delta.y>5)highReach=Mathf.Max(highReach,Vector3.Dot(delta,inward));
                        }
                        float halfWidth=(canopy[1].lane-canopy[0].lane)*.5f;
                        Require(halfWidth-lowReach>1.35f&&highReach>3.1f,"forest canopy corridor closes at rider height or lacks an overhanging crown");
                        chamber+=stem.lane*.5f;
                    }
                    Require(!world.Items.Any(o=>o.kind=="tree"&&Mathf.Abs(o.distance-canopy[0].distance)<3&&Mathf.Abs(chamber-o.lane)<2),
                        "trunk in forest passage");
                    Debug.Log("FOREST PASS seed "+seed+" "+passage.tree+" at "+canopy[0].distance+" / "+canopy[1].distance);
                }
                world.Refresh(12507,850);var gate=world.Items.First(o=>o.stoneGate&&o.distance>=12507);
                var stones=world.Items.Where(o=>o.kind=="rock"&&Mathf.Abs(o.distance-gate.distance)<.01f).ToArray();
                Require(stones.Length==2&&stones.All(o=>o.Visual.transform.Find("Pickup glow")&&o.Visual.bounds.size.y>1.5f),
                    "black run lacks two lit gate boulders");
                Require(stones.All(o=>Mathf.Abs(o.lane-gate.lane)-o.width*.5f>=gate.width*.5f),
                    "gate boulders obstruct scoring passage");
                Debug.Log("BLACK GATE PASS seed "+seed+" rock gate "+gate.distance+" m");
            }
            world.Configure(1911,"sprint");world.Refresh(1170,100);
            Require(world.Items.Any(o=>o.kind=="finish"&&o.distance==1200),"sprint finish line");
            world.Configure(1911,"tubing");world.Refresh(810,38);
            Require(world.Items.Any(o=>o.kind=="tree")&&world.Items.Any(o=>o.kind=="gate"),"tubing forest and slalom");
            world.Refresh(1690,70);Require(world.Items.Any(o=>o.kind=="gift"),"tubing gift finish");
            world.Configure(1911,"sled");
            Require(world.Items.Any(o=>o.kind=="snowman")&&world.Items.Any(o=>o.kind=="gift"),"snowmobile targets and gifts");
            CheckSnowContact(race);
            RiderCourseChecks.Verify(race);
            RaceRouteChecks.Run(race);
            Debug.Log("RACE WORLD CHECK PASS: 8 stages, two seeds, signs, portrait people, moving riders, sprint and bonuses");
        }finally{race.View.aspect=aspect;race.Menu();}
    }
}
