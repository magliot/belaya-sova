using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Meshes are baked from the approved game factories, so cloned people keep their skis,
// baked equipment pose, original UV transforms, diffuse maps and turning morph targets.
public partial class RaceWorld {
    sealed class Model {
        public Mesh mesh;
        public Material[] materials;
        public Bounds glow;
    }
    static readonly Dictionary<string,Model> models=new Dictionary<string,Model>();
    static readonly Dictionary<string,Material> paints=new Dictionary<string,Material>();
    static string Text(BinaryReader input){return Encoding.UTF8.GetString(input.ReadBytes(input.ReadInt32()));}
    static Material Paint(string map,int blend,bool lit,int glow){
        string key=map+"/"+blend+"/"+lit+"/"+glow;
        if(paints.TryGetValue(key,out var shared))return shared;
        var m=new Material(Resources.Load<Shader>("Race/WorldObject"));m.name="Course "+key;
        if(map.Length>0)m.SetTexture("_BaseMap",Resources.Load<Texture2D>(map.StartsWith("Art/",StringComparison.Ordinal)?map:"Race/Textures/"+map));
        m.SetFloat("_Lit",lit?1:0);m.SetFloat("_Glow",glow/255f);
        if(blend>0){m.SetFloat("_ZWrite",0);m.SetFloat("_SrcBlend",(int)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend",(int)(blend==2?BlendMode.One:BlendMode.OneMinusSrcAlpha));
            m.renderQueue=(int)RenderQueue.Transparent;}
        paints.Add(key,m);return m;
    }
    static Model LoadModel(string key){
        if(models.TryGetValue(key,out var saved))return saved;
        var asset=Resources.Load<TextAsset>("Race/Models/"+key);
        if(!asset)throw new InvalidDataException("Missing course model: "+key);
        var positions=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color32>();
        var mats=new List<Material>();var faces=new List<List<int>>();var shapes=new Dictionary<string,Dictionary<int,Vector3>>();
        Bounds pickupGlow=default;
        bool pickup=key=="thermos"||key=="helmet"||key=="aid"||key=="gift"||key=="feather";
        using(var input=new BinaryReader(new MemoryStream(asset.bytes))){
            int parts=input.ReadInt32();
            for(int i=0;i<parts;i++){
                string map=Text(input);int blend=input.ReadInt32();bool lit=input.ReadInt32()!=0;int glow=input.ReadInt32();
                int count=input.ReadInt32(),first=positions.Count;
                // The old renderer supplied a camera-facing sprite and placed a painted shadow on a flat floor.
                // A rotating Unity mesh cannot use either of these planes on the sloped snow.
                bool glowSprite=pickup&&blend==2&&count==4&&map=="d4b9333d982a875f";
                bool shadowCard=key.StartsWith("tube",StringComparison.Ordinal)&&blend==1&&map=="d2e6db75e1a2a063";
                bool omit=glowSprite||shadowCard;
                for(int j=0;j<count;j++)positions.Add(new Vector3(input.ReadSingle(),input.ReadSingle(),input.ReadSingle()));
                for(int j=0;j<count;j++)normals.Add(new Vector3(input.ReadSByte(),input.ReadSByte(),input.ReadSByte()).normalized);
                for(int j=0;j<count;j++)uv.Add(new Vector2(input.ReadSingle(),input.ReadSingle()));
                for(int j=0;j<count;j++)colors.Add(new Color32(input.ReadByte(),input.ReadByte(),input.ReadByte(),input.ReadByte()));
                int indices=input.ReadInt32();
                if(omit)input.BaseStream.Seek(indices*4,SeekOrigin.Current);
                else{
                    var mat=Paint(map,blend,lit,glow);int group=mats.IndexOf(mat);
                    if(group<0){group=mats.Count;mats.Add(mat);faces.Add(new List<int>());}
                    for(int j=0;j<indices;j++)faces[group].Add(first+input.ReadInt32());
                }
                int morphs=input.ReadInt32();for(int m=0;m<morphs;m++){
                    string name=Text(input);if(!shapes.TryGetValue(name,out var delta)){delta=new Dictionary<int,Vector3>();shapes.Add(name,delta);}
                    for(int j=0;j<count;j++){var v=new Vector3(input.ReadInt16(),input.ReadInt16(),input.ReadInt16())*.0001f;if(v!=Vector3.zero)delta[first+j]=v;}
                }
                if(omit){
                    if(glowSprite){pickupGlow=new Bounds(positions[first],Vector3.zero);
                        for(int j=1;j<count;j++)pickupGlow.Encapsulate(positions[first+j]);}
                    positions.RemoveRange(first,count);normals.RemoveRange(first,count);uv.RemoveRange(first,count);colors.RemoveRange(first,count);
                }
            }
        }
        var mesh=new Mesh{name=key,indexFormat=positions.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
        mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.subMeshCount=mats.Count;
        for(int i=0;i<faces.Count;i++)mesh.SetTriangles(faces[i],i,false);
        foreach(var shape in shapes){
            var v=new Vector3[positions.Count];foreach(var pair in shape.Value)v[pair.Key]=pair.Value;
            mesh.AddBlendShapeFrame(shape.Key,100,v,null,null);
        }
        AddRiderPoses(mesh,key,positions,normals);
        if(key=="startArch"){
            // Fade the support beam with the lettering as the camera passes underneath.
            // A solid beam without its sign otherwise draws a screen-wide orange line.
            for(int i=0;i<mats.Count;i++){
                var banner=new Material(mats[i]);banner.EnableKeyword("OWL_START_BANNER");
                banner.SetFloat("_ZWrite",1);banner.SetFloat("_SrcBlend",(int)BlendMode.SrcAlpha);
                banner.SetFloat("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
                banner.renderQueue=(int)RenderQueue.Transparent;mats[i]=banner;
            }
        }
        mesh.RecalculateBounds();
        if(key=="magnet")pickupGlow=new Bounds(mesh.bounds.center,Vector3.one*1.85f);
        var model=new Model{mesh=mesh,materials=mats.ToArray(),glow=pickupGlow};models.Add(key,model);return model;
    }
    static Mesh glowShell;
    static readonly Dictionary<string,Material> glowPaints=new Dictionary<string,Material>();
    internal static Mesh UnitSphere(){
        if(!glowShell){
            const int rings=12,sides=20;
            var v=new List<Vector3>();var ix=new List<int>();
            for(int r=0;r<=rings;r++)for(int s=0;s<sides;s++){
                float a=Mathf.PI*r/rings,b=2*Mathf.PI*s/sides;
                v.Add(new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b)));
                if(r==0)continue;
                int i=(r-1)*sides+s,k=(r-1)*sides+(s+1)%sides;
                ix.Add(i);ix.Add(k);ix.Add(i+sides);ix.Add(k);ix.Add(k+sides);ix.Add(i+sides);
            }
            glowShell=new Mesh{name="Pickup glow sphere"};glowShell.SetVertices(v);glowShell.SetNormals(v);glowShell.SetTriangles(ix,0);
        }
        return glowShell;
    }
    static void AddPickupGlow(GameObject item,Bounds original){
        if(!glowPaints.TryGetValue(item.name,out var paint)){
            paint=new Material(Resources.Load<Shader>("Race/PickupGlow"));
            var tint=item.name=="helmet"?new Color(.40f,.82f,1):item.name=="aid"?new Color(.40f,1,.65f):
                item.name=="gift"?new Color(1,.49f,.68f):item.name=="magnet"?new Color(.80f,.37f,1):new Color(1,.73f,.26f);
            paint.SetColor("_Tint",tint);glowPaints.Add(item.name,paint);
        }
        var shell=new GameObject("Pickup glow");shell.transform.SetParent(item.transform,false);
        shell.transform.localPosition=original.center;shell.transform.localScale=Vector3.one*Mathf.Max(original.extents.x,original.extents.y);
        shell.AddComponent<MeshFilter>().sharedMesh=UnitSphere();
        var sr=shell.AddComponent<MeshRenderer>();sr.sharedMaterial=paint;sr.shadowCastingMode=ShadowCastingMode.Off;sr.receiveShadows=false;
    }
    public GameObject CreateVisual(string key,Transform parent){
        bool gateStone=key=="gateRockWide"||key=="gateRockTall";
        bool nightGate=key=="gateBlueNight"||key=="gateRedNight";
        string source=nightGate?key.Substring(0,key.Length-5):key;
        var model=LoadModel(gateStone?(key=="gateRockWide"?"rockWide":"rockTall"):source);
        var obj=new GameObject(key);obj.transform.SetParent(parent,false);Renderer renderer;
        if(model.mesh.blendShapeCount>0){
            var skin=obj.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=model.mesh;skin.localBounds=model.mesh.bounds;renderer=skin;
        }else{obj.AddComponent<MeshFilter>().sharedMesh=model.mesh;renderer=obj.AddComponent<MeshRenderer>();}
        renderer.sharedMaterials=model.materials;
        if(nightGate){
            if(!nightGatePaint.TryGetValue(key,out var stripes)){
                stripes=new Material[model.materials.Length];
                for(int i=0;i<stripes.Length;i++){
                    stripes[i]=new Material(model.materials[i]);stripes[i].EnableKeyword("OWL_NIGHT_GATE");
                    stripes[i].SetColor("_GateTint",key=="gateBlueNight"?new Color(.20f,.85f,1):new Color(1,.40f,.23f));
                }
                nightGatePaint.Add(key,stripes);
            }
            renderer.sharedMaterials=stripes;AddGateLamps(obj,key=="gateBlueNight");
        }
        if(gateStone){
            if(!gateStones.TryGetValue(key,out var stonePaint)){
                stonePaint=new Material(model.materials[0]);stonePaint.SetFloat("_StoneRim",1);
                gateStones.Add(key,stonePaint);
            }
            renderer.sharedMaterial=stonePaint;
            var bounds=model.mesh.bounds;
            AddPickupGlow(obj,bounds);
            var halo=obj.transform.Find("Pickup glow");
            halo.GetComponent<Renderer>().sharedMaterial.SetColor("_Tint",new Color(.09f,.53f,.69f));
            halo.localScale=bounds.extents*1.55f;
        }
        if(model.glow.size.sqrMagnitude>0)AddPickupGlow(obj,model.glow);
        return obj;
    }
    static readonly Dictionary<string,Material> gateStones=new Dictionary<string,Material>();
    static readonly Dictionary<string,Material[]> nightGatePaint=new Dictionary<string,Material[]>();
}
