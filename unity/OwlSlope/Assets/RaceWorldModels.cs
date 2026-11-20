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
    }
    static readonly Dictionary<string,Model> models=new Dictionary<string,Model>();
    static readonly Dictionary<string,Material> paints=new Dictionary<string,Material>();
    static string Text(BinaryReader input){return Encoding.UTF8.GetString(input.ReadBytes(input.ReadInt32()));}
    static Material Paint(string map,int blend,bool lit,int glow){
        string key=map+"/"+blend+"/"+lit+"/"+glow;
        if(paints.TryGetValue(key,out var shared))return shared;
        var m=new Material(Resources.Load<Shader>("Race/WorldObject"));m.name="Course "+key;
        if(map.Length>0)m.SetTexture("_BaseMap",Resources.Load<Texture2D>("Race/Textures/"+map));
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
        using(var input=new BinaryReader(new MemoryStream(asset.bytes))){
            int parts=input.ReadInt32();
            for(int i=0;i<parts;i++){
                string map=Text(input);int blend=input.ReadInt32();bool lit=input.ReadInt32()!=0;int glow=input.ReadInt32();
                var mat=Paint(map,blend,lit,glow);int group=mats.IndexOf(mat);
                if(group<0){group=mats.Count;mats.Add(mat);faces.Add(new List<int>());}
                int count=input.ReadInt32(),first=positions.Count;
                for(int j=0;j<count;j++)positions.Add(new Vector3(input.ReadSingle(),input.ReadSingle(),input.ReadSingle()));
                for(int j=0;j<count;j++)normals.Add(new Vector3(input.ReadSByte(),input.ReadSByte(),input.ReadSByte()).normalized);
                for(int j=0;j<count;j++)uv.Add(new Vector2(input.ReadSingle(),input.ReadSingle()));
                for(int j=0;j<count;j++)colors.Add(new Color32(input.ReadByte(),input.ReadByte(),input.ReadByte(),input.ReadByte()));
                int indices=input.ReadInt32();for(int j=0;j<indices;j++)faces[group].Add(first+input.ReadInt32());
                int morphs=input.ReadInt32();for(int m=0;m<morphs;m++){
                    string name=Text(input);if(!shapes.TryGetValue(name,out var delta)){delta=new Dictionary<int,Vector3>();shapes.Add(name,delta);}
                    for(int j=0;j<count;j++){var v=new Vector3(input.ReadInt16(),input.ReadInt16(),input.ReadInt16())*.0001f;if(v!=Vector3.zero)delta[first+j]=v;}
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
        mesh.RecalculateBounds();var model=new Model{mesh=mesh,materials=mats.ToArray()};models.Add(key,model);return model;
    }
    public GameObject CreateVisual(string key,Transform parent){
        var model=LoadModel(key);var obj=new GameObject(key);obj.transform.SetParent(parent,false);Renderer renderer;
        if(model.mesh.blendShapeCount>0){
            var skin=obj.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=model.mesh;skin.localBounds=model.mesh.bounds;renderer=skin;
        }else{obj.AddComponent<MeshFilter>().sharedMesh=model.mesh;renderer=obj.AddComponent<MeshRenderer>();}
        renderer.sharedMaterials=model.materials;return obj;
    }
}
