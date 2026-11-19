using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
public static class BakeSlope {
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
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);var pilot=new GameObject("Playable slope").AddComponent<SlopePilot>();const string file="Assets/Slope.unity";EditorSceneManager.SaveScene(scene,file);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(file,true)};AssetDatabase.SaveAssets();pilot.Setup();var cam=pilot.View;var rt=new RenderTexture(375,667,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(375,667,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,375,667),0,0);tex.Apply();var dest=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/review/unity.png"));File.WriteAllBytes(dest,tex.EncodeToPNG());Debug.Log("OWL FRAME "+dest);cam.targetTexture=null;RenderTexture.active=null;rt.Release();pilot.Tick(1f/60,1,true);if(!pilot.Airborne)throw new System.Exception("jump");for(int i=0;i<90;i++)pilot.Tick(1f/60,1,false);if(pilot.Airborne||pilot.Lane<4||pilot.Distance<210)throw new System.Exception("movement");pilot.ResetRide();if(pilot.Distance!=200||pilot.Lane!=0)throw new System.Exception("reset");Debug.Log("OWL movement/landing/reset passed");
 }
}
