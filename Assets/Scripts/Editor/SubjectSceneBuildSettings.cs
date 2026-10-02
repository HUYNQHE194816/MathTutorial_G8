#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Tự thêm SubjectSelectScene vào Build Settings nếu chưa có.</summary>
[InitializeOnLoad]
public static class SubjectSceneBuildSettings
{
    private const string ScenePath = "Assets/Scenes/SubjectSelectScene.unity";

    static SubjectSceneBuildSettings()
    {
        EditorApplication.delayCall += Ensure;
    }

    private static void Ensure()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;

        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[SubjectSelect] Đã thêm " + ScenePath + " vào Build Settings.");
    }
}
#endif
