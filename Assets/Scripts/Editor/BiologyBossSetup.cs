#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Tools > Biology Boss > Thiết lập tự động
/// 1) Tạo scene Assets/Scenes/BiologyBossScene.unity   2) Thêm vào Build Settings
/// 3) Mở khóa thẻ "Sinh" trong SubjectSelectScene để bấm là vào game.
/// </summary>
public static class BiologyBossSetup
{
    const string BossScene = "Assets/Scenes/BiologyBossScene.unity";
    const string SubjectScene = "Assets/Scenes/SubjectSelectScene.unity";

    [MenuItem("Tools/Biology Boss/Thiết lập tự động (scene + thẻ Sinh)")]
    public static void SetupAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        CreateScene(); AddToBuildSettings(); bool hooked = HookSubjectCard();
        EditorSceneManager.OpenScene(BossScene);
        EditorUtility.DisplayDialog("Biology Boss",
            "Đã tạo scene BiologyBossScene và thêm vào Build Settings.\n" +
            (hooked ? "Đã mở khóa thẻ Sinh trong SubjectSelectScene." : "Không tìm thấy thẻ Sinh trong SubjectSelectScene (bạn tự đặt sceneToLoad = BiologyBossScene, available = true).") +
            "\n\nBấm Play để chơi thử!", "OK");
    }

    [MenuItem("Tools/Biology Boss/Chỉ tạo scene")]
    public static void CreateSceneOnly() { if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return; CreateScene(); AddToBuildSettings(); EditorSceneManager.OpenScene(BossScene); }

    static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var go = new GameObject("BossFightManager"); var mgr = go.AddComponent<BossFightManager>();
        mgr.correctClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AudioBank/Correct.mp3");
        mgr.wrongClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AudioBank/Fail.mp3");
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) Object.DestroyImmediate(l.gameObject);
        var cam = Camera.main; if (cam != null) { cam.orthographic = true; cam.orthographicSize = 9.375f; cam.transform.position = new Vector3(0, 0, -10f); cam.backgroundColor = new Color(.05f, .04f, .09f); cam.clearFlags = CameraClearFlags.SolidColor; }
        EditorSceneManager.SaveScene(scene, BossScene);
    }

    static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == BossScene)) return;
        scenes.Add(new EditorBuildSettingsScene(BossScene, true)); EditorBuildSettings.scenes = scenes.ToArray();
    }

    static bool HookSubjectCard()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SubjectScene) == null) return false;
        var scene = EditorSceneManager.OpenScene(SubjectScene, OpenSceneMode.Single); bool found = false;
        foreach (var card in Object.FindObjectsByType<SubjectCard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (card.subjectName != "Sinh") continue;
            card.sceneToLoad = "ChapterSelectScene"; card.available = true; EditorUtility.SetDirty(card); found = true;
        }
        if (found) EditorSceneManager.SaveScene(scene);
        return found;
    }
}
#endif
