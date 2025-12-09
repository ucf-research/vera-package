using UnityEngine;
using VERA;

internal static class VERAAutoBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitializeVERALogger()
    {
        if (GameObject.FindAnyObjectByType<VERALogger>() == null)
        {
            var go = new GameObject("VERALogger");
            go.AddComponent<VERALogger>();
            go.AddComponent<VERAWebXRHookup>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
