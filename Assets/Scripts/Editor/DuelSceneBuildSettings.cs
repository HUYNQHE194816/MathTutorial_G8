#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Tự thêm các scene Đối kháng vào Build Settings nếu đã tạo mà chưa có.</summary>
[InitializeOnLoad]
public static class DuelSceneBuildSettings
{
    static readonly string[] Paths = { "Assets/Scenes/DuelLobbyScene.unity", "Assets/Scenes/DuelResultScene.unity" };

    static DuelSceneBuildSettings() { EditorApplication.delayCall += Ensure; }

    static void Ensure()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        bool changed = false;
        foreach (var p in Paths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(p) == null) continue;
            if (scenes.Any(s => s.path == p)) continue;
            scenes.Add(new EditorBuildSettingsScene(p, true));
            changed = true;
            Debug.Log("[Duel] Đã thêm " + p + " vào Build Settings.");
        }
        if (changed) EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif