#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Forces Active Input Handling to "Input Manager (Old)" via PlayerSettings API.
/// Run once via Tools > Tanks > Force Old Input System, then delete this file.
/// </summary>
public static class ForceOldInputSystem
{
    [MenuItem("Tools/Tanks/Force Old Input System")]
    public static void Apply()
    {
#pragma warning disable CS0618
        PlayerSettings.SetPropertyInt(
            "activeInputHandler",
            0,                           // 0 = Old, 1 = New, 2 = Both
            BuildTargetGroup.Standalone
        );
#pragma warning restore CS0618

        AssetDatabase.SaveAssets();
        Debug.Log("[ForceOldInputSystem] Active Input Handling set to Input Manager (Old). Please restart the Editor.");
        EditorUtility.DisplayDialog(
            "Input System",
            "Active Input Handling set to Input Manager (Old).\n\nPlease restart the Unity Editor for the change to take effect.",
            "OK"
        );
    }
}
#endif
