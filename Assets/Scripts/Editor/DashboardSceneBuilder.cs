#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Tools > KHTN 8 > Tạo DashboardScene
/// DashboardManager giờ tự dựng toàn bộ UI bằng code (UIKit + SceneShell, phong cách dark fantasy chung),
/// nên scene chỉ cần Camera + 1 GameObject DashboardManager. Lưu Assets/Scenes/DashboardScene.unity và thêm vào Build Settings.
/// Kèm menu thêm dữ liệu mẫu / xoá tiến độ (dùng khi Play để xem thử).
/// </summary>
public static class DashboardSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/DashboardScene.unity";

    [MenuItem("Tools/KHTN 8/Tạo DashboardScene")]
    public static void Build()
    {
        if (System.IO.File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Tạo DashboardScene", "DashboardScene đã tồn tại. Ghi đè?", "Ghi đè", "Huỷ"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) Object.DestroyImmediate(l.gameObject);
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = GameTheme.Ink;
            cam.orthographic = true;
        }

        var mgrGo = new GameObject("DashboardManager");
        mgrGo.AddComponent<DashboardManager>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureInBuildSettings();
        AssetDatabase.Refresh();
        Selection.activeGameObject = mgrGo;
        Debug.Log("[Dashboard] Đã tạo " + ScenePath + ". Play scene này (hoặc dùng menu 'Thêm dữ liệu mẫu') để xem thử.");
    }

    // ---------- Menu thử nghiệm ----------
    [MenuItem("Tools/KHTN 8/Thêm dữ liệu mẫu (học sinh hiện tại)")]
    private static void AddDemo()
    {
        ProgressSaveSystem.DebugAddDemoData();
    }

    [MenuItem("Tools/KHTN 8/Xoá tiến độ (học sinh hiện tại)")]
    private static void ResetProgress()
    {
        if (EditorUtility.DisplayDialog("Xoá tiến độ", "Xoá toàn bộ tiến độ đã lưu của học sinh hiện tại?", "Xoá", "Huỷ"))
            ProgressSaveSystem.ResetCurrent();
    }

    // ---------- Helpers ----------
    private static void EnsureInBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        int idx = scenes.FindIndex(s => s.path == ScenePath);
        var entry = new EditorBuildSettingsScene(ScenePath, true);
        if (idx >= 0) scenes[idx] = entry; else scenes.Add(entry);
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
