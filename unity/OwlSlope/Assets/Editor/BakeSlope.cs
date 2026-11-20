using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
public static class BakeSlope {
 [MenuItem("Owl/Build Telegram Web pilot")] public static void BuildWeb(){
  if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL,BuildTarget.WebGL))throw new System.Exception("Install Web Build Support for this Unity editor");
  if(Application.isBatchMode)CheckSlalom();
  var report=BuildPipeline.BuildPlayer(new[]{"Assets/Slope.unity"},"Builds/Web",BuildTarget.WebGL,BuildOptions.None);
  if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new System.Exception("Web build: "+report.summary.result);
  Debug.Log("OWL WEB BUILT: "+report.summary.totalSize+" bytes");
 }
 static void CheckSlalom(){
  var scene=EditorSceneManager.NewPreviewScene();
  try{
   var root=new GameObject("Slalom check");
   UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
   var pilot=root.AddComponent<SlopePilot>();pilot.Setup();
   var props=root.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Fir")||t.name=="Snowman")
      .SelectMany(t=>t.GetComponentsInChildren<Renderer>()).ToArray();
   foreach(var flag in root.transform.Cast<Transform>().Where(t=>t.name=="Gate flag").Take(2)){
    Vector3 sight=flag.position-pilot.View.transform.position;
    var ray=new Ray(pilot.View.transform.position,sight.normalized);
    if(props.Any(r=>r.bounds.IntersectRay(ray,out float hit)&&hit<sight.magnitude))
     throw new System.Exception("Scenery hides the first gate from the start camera");
   }
   // Browser update() at 200 m, 375 px viewport, sensitivity 4. Down at x=80,
   // drag to 140, release at 0.5 s. Compare across 30 and 60 Hz render loops.
   foreach(int hz in new[]{30,60}){
    pilot.ResetRide();pilot.BeginSteer();pilot.DragSteer(60f/375);
    float previous=0;
    foreach(var sample in new[]{new Vector3(.2f,2.461f,202.353f),new Vector3(.5f,3.714f,205.933f),new Vector3(1f,3.077f,212.036f)}){
     if(previous==.5f)pilot.ReleaseSteer();
     for(int i=0;i<Mathf.RoundToInt((sample.x-previous)*hz);i++)pilot.AdvanceInput(1f/hz,0);
     if(Mathf.Abs(pilot.Lane-sample.y)>.10f||Mathf.Abs(pilot.Distance-sample.z)>.05f)
      throw new System.Exception("Browser drag mismatch at "+sample.x+" s: "+pilot.Lane+" / "+pilot.Distance);
     previous=sample.x;
    }
   }
   Debug.Log("OWL INPUT PASS: 60 px drag and release match browser within 10 cm at 30/60 Hz.");
   foreach(bool steer in new[]{false,true}){
    pilot.ResetRide();
    for(int i=0;i<1000&&!pilot.Finished;i++){
     float d=pilot.Distance,target=d<223?-3.4f:d<247?3.4f:d<263?0:-2.8f;
     pilot.Tick(1f/60,steer?target:8);
    }
    if(!pilot.Finished||pilot.GatesPassed!=(steer?3:0)||pilot.CaughtFruit!=steer)
     throw new System.Exception("Slalom result: gates="+pilot.GatesPassed+", mandarin="+pilot.CaughtFruit+", finish="+pilot.Finished);
   }
   pilot.ResetRide();pilot.Tick(1f/60,0);
   for(int i=0;i<90;i++)pilot.Tick(1f/60,null);
   if(pilot.Lane<.25f)throw new System.Exception("Released rider followed the bend");
   Debug.Log("OWL SLALOM PASS: bypass 0/3, steered run 3/3, automatic takeoff, fruit, finish and coasting on a bend.");
  }finally{EditorSceneManager.ClosePreviewScene(scene);}
 }
 public static void RunAndBuild(){Run();BuildPlayer();}
 [MenuItem("Owl/Build Windows player")] public static void BuildPlayer(){
  PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.defaultScreenWidth=375;PlayerSettings.defaultScreenHeight=667;
  var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new System.Exception("Missing URP/Lit");
  // Keep the material referenced by the player so the shader used by the four runtime imports survives stripping.
  if(!AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/SlopeLit.mat")) AssetDatabase.CreateAsset(new Material(shader),"Assets/Resources/SlopeLit.mat");
  AssetDatabase.SaveAssets();var report=BuildPipeline.BuildPlayer(new[]{"Assets/Slope.unity"},"Builds/Windows/OwlSlope.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
  if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new System.Exception("Player build: "+report.summary.result);
 }
 [MenuItem("Owl/Build comparison scene")] public static void Run(){var pipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
  foreach(var key in new[]{"Fir","Rock","Snowman","Owl"}){var path="Assets/Resources/"+key+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);if(importer==null)throw new IOException(path);if(key=="Owl")importer.animationType=ModelImporterAnimationType.Legacy;importer.SaveAndReimport();}
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);var pilot=new GameObject("Playable slope").AddComponent<SlopePilot>();const string file="Assets/Slope.unity";EditorSceneManager.SaveScene(scene,file);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(file,true)};AssetDatabase.SaveAssets();pilot.Setup();var cam=pilot.View;var rt=new RenderTexture(375,667,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(375,667,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,375,667),0,0);tex.Apply();var dest=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/review/unity.png"));File.WriteAllBytes(dest,tex.EncodeToPNG());Debug.Log("OWL FRAME "+dest);cam.targetTexture=null;RenderTexture.active=null;rt.Release();for(int i=0;i<90;i++)pilot.Tick(1f/60,5);if(pilot.Airborne||pilot.Lane<4||pilot.Distance<210)throw new System.Exception("movement");pilot.ResetRide();if(pilot.Distance!=200||pilot.Lane!=0)throw new System.Exception("reset");Debug.Log("OWL movement/landing/reset passed");
 }
}
