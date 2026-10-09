#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Tools > KHTN 8 > Tạo PhongAnScene
/// Tạo scene Assets/Scenes/PhongAnScene.unity (1 GameObject PhongAnManager đã gắn essay_problems.json)
/// và thêm vào Build Settings. Bấm Play để chơi thử.
/// </summary>
public static class PhongAnSetup
{
    const string ScenePath = "Assets/Scenes/PhongAnScene.unity";
    const string JsonPath = "Assets/DataBank/Essay/essay_problems.json";

    [MenuItem("Tools/KHTN 8/Tạo PhongAnScene")]
    public static void Create()
    {
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
        if (json == null) { EditorUtility.DisplayDialog("Phong Ấn", "Không thấy " + JsonPath, "OK"); return; }
        if (System.IO.File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Phong Ấn", "PhongAnScene đã tồn tại. Ghi đè?", "Ghi đè", "Huỷ")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) Object.DestroyImmediate(l.gameObject);
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.05f, .04f, .09f); }

        var go = new GameObject("PhongAnManager");
        var mgr = go.AddComponent<PhongAnManager>();
        var so = new SerializedObject(mgr);
        so.FindProperty("problemsJson").objectReferenceValue = json;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        Debug.Log("[PhongAn] Đã tạo " + ScenePath + ". Bấm Play để chơi thử.");
    }
}
#endif
