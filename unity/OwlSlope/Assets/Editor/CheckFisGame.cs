using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CheckFisGame {
    const string Pending="Owl.CheckFis";
    static CheckFisGame(){EditorApplication.update+=CheckWhenPlaying;}
    public static void Run(){
        SessionState.SetBool(Pending,true);
        EditorSceneManager.OpenScene("Assets/Mountain.unity");
        EditorApplication.EnterPlaymode();
    }
    static void CheckWhenPlaying(){
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||!SessionState.GetBool(Pending,false))return;
        var race=Object.FindFirstObjectByType<MountainRace>();
        if(!race||!race.View)return;
        SessionState.EraseBool(Pending);
        try{Debug.Log("MOUNTAIN FIS PASS "+race.VerifyFisSituations());EditorApplication.Exit(0);}
        catch(System.Exception error){Debug.LogException(error);EditorApplication.Exit(2);}
    }
}
