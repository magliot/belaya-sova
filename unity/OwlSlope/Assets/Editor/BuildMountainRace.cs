using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildMountainRace {
    [MenuItem("Owl/Build full game for Web")]
    public static void Web(){
        if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL,BuildTarget.WebGL))
            throw new InvalidOperationException("Unity 6000.3.27f1 Web Build Support is required");
        string destination=Environment.GetEnvironmentVariable("SOVA_RACE_OUTPUT");
        if(String.IsNullOrEmpty(destination))destination=Path.GetFullPath("Builds/MountainWeb");
        var template=PlayerSettings.WebGL.template;
        var hashes=PlayerSettings.WebGL.nameFilesAsHashes;
        var caching=PlayerSettings.WebGL.dataCaching;
        var compression=PlayerSettings.WebGL.compressionFormat;
        var fallback=PlayerSettings.WebGL.decompressionFallback;
        try{
            PlayerSettings.WebGL.template="PROJECT:MountainRace";
            PlayerSettings.WebGL.nameFilesAsHashes=true;
            // The web shell stages and caches the player before Unity starts, including in private WebViews.
            PlayerSettings.WebGL.dataCaching=false;
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback=true;
            BuildReport result=BuildPipeline.BuildPlayer(new[]{"Assets/Mountain.unity"},destination,BuildTarget.WebGL,BuildOptions.None);
            if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Mountain Web build: "+result.summary.result);
            Debug.Log("MOUNTAIN WEB BUILT "+result.summary.totalSize+" bytes at "+destination);
        }finally{
            PlayerSettings.WebGL.template=template;
            PlayerSettings.WebGL.nameFilesAsHashes=hashes;
            PlayerSettings.WebGL.dataCaching=caching;
            PlayerSettings.WebGL.compressionFormat=compression;
            PlayerSettings.WebGL.decompressionFallback=fallback;
        }
    }
}
