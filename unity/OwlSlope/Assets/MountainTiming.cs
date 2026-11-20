using System.Collections;
using UnityEngine;

public sealed class MountainTiming : MonoBehaviour {
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void MountainFrameEnded();
    IEnumerator Start(){
        var end=new WaitForEndOfFrame();
        while(isActiveAndEnabled){yield return end;MountainFrameEnded();}
    }
#endif
}
