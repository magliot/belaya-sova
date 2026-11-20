using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class SlopePilot {
    Vector3 LandscapeGround(float d,float x){
        float bank=Mathf.Clamp(x,-14,14);
        Vector3 right=Ground(d,4.5f)-Ground(d,-4.5f);right.y=0;
        return Ground(d,bank)+right.normalized*(x-bank)+Vector3.up*Mathf.Abs(x-bank)*.12f;
    }
    void BuildLandscape(){
        float[] offsets={-55,-28,-19,-14,-10.5f,-9,-4.5f,0,4.5f,9,10.5f,14,19,28,55};
        var points=new List<Vector3>();var uv=new List<Vector2>();
        var piste=new List<int>();var banks=new List<int>();
        for(int row=0;row<=116;row++){
            float d=185+2*row;
            for(int col=0;col<offsets.Length;col++){
                float x=offsets[col];
                // Outside the sampled banks the hillside flattens into woods.
                points.Add(LandscapeGround(d,x));
                uv.Add(new Vector2(x/4.2f,d/10));
                if(row==116||col==offsets.Length-1)continue;
                int i=row*offsets.Length+col,n=i+offsets.Length;
                var faces=x>=-10.5f&&x<10.5f?piste:banks;
                faces.AddRange(new[]{i,n,i+1,i+1,n,n+1});
            }
        }
        var mesh=new Mesh{name="Groomed piste and powder banks",subMeshCount=2};
        mesh.SetVertices(points);mesh.SetUVs(0,uv);mesh.SetTriangles(piste,0);mesh.SetTriangles(banks,1);mesh.RecalculateNormals();
        var terrain=new GameObject("Snow");terrain.transform.SetParent(transform);
        terrain.AddComponent<MeshFilter>().sharedMesh=mesh;
        terrain.AddComponent<MeshRenderer>().sharedMaterials=new[]{Snow("Piste",new Color(.91f,.94f,.98f)),Snow("Powder",new Color(.85f,.90f,.96f))};

        var fir=Model("Fir",Ground(212,-7),2.2f,false).transform.parent;
        var random=new System.Random(1911);
        foreach(int side in new[]{-1,1}){
            for(float d=190+(side+1)*5;d<360;d+=14+random.Next(11)){
                int count=2+random.Next(3);
                for(int i=0;i<count;i++){
                    float x=side*(10.9f+i*.95f+(float)random.NextDouble()*1.6f);
                    var tree=Instantiate(fir,transform);
                    tree.position=LandscapeGround(d+i*2.2f,x);
                    tree.Rotate(Vector3.up,random.Next(360),Space.World);
                    tree.localScale=fir.localScale*(1.6f+(float)random.NextDouble()*1.5f);
                }
            }
        }
    }

    Material Snow(string key,Color tint){
        var mat=Surface(tint);var texture=Resources.Load<Texture2D>("Art/"+key);
        texture.anisoLevel=4;mat.mainTexture=texture;mat.SetFloat("_Smoothness",.05f);return mat;
    }

    void SetDaylight(){
        var sky=new Material(Resources.Load<Shader>("SlopeSky"));
        sky.SetTexture("_Ridge",Resources.Load<Texture2D>("Art/Ridge52B"));
        var forward=Ground(245,0)-Ground(225,0);forward.y=0;
        sky.SetVector("_Forward",forward.normalized);RenderSettings.skybox=sky;cam.clearFlags=CameraClearFlags.Skybox;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.59f,.66f,.76f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=52;
        RenderSettings.fogEndDistance=185;RenderSettings.fogColor=new Color(.75f,.84f,.92f);
        var sun=new GameObject("Winter sun").AddComponent<Light>();sun.transform.SetParent(transform);
        sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;sun.intensity=.77f;sun.color=new Color(1,.96f,.90f);
        sun.transform.rotation=Quaternion.Euler(38,-38,0);RenderSettings.sun=sun;
    }

    void BuildRamp(Material lip){
        float start=takeoff-rampLength;
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        foreach(float d in new[]{start,start+1,start+2,takeoff,takeoff+.04f}){
            float h=d>takeoff?0:RampAt(d,0);
            foreach(float x in new[]{-rampHalfWidth-.38f,-rampHalfWidth,rampHalfWidth,rampHalfWidth+.38f}){
                vertices.Add(Ground(d,x)+Vector3.up*(.024f+(Mathf.Abs(x)>rampHalfWidth?0:h)));
                uv.Add(new Vector2(x/4.2f,d/10));
            }
        }
        for(int row=0;row<4;row++)for(int col=0;col<3;col++){
            int i=row*4+col;triangles.AddRange(new[]{i,i+4,i+1,i+1,i+4,i+5});
        }
        var mesh=new Mesh{name="Kicker"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();
        var ramp=new GameObject("Snow kicker");ramp.transform.SetParent(transform);ramp.AddComponent<MeshFilter>().sharedMesh=mesh;
        ramp.AddComponent<MeshRenderer>().sharedMaterial=Snow("Piste",new Color(.84f,.90f,.99f));
        Marker("Takeoff lip",PrimitiveType.Cube,takeoff-.04f,0,lipHeight+.045f,new Vector3(2*rampHalfWidth,.05f,.14f),lip);
    }
}
