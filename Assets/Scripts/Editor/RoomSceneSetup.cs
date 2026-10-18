#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Tools > KHTN 8 > Tạo RoomScene (E2)
/// Tạo scene Assets/Scenes/RoomScene.unity (Main Camera + 1 GameObject RoomManager đã gắn essay_problems.json)
/// và thêm vào Build Settings. Mở scene rồi bấm Play để chơi thử một màn.
/// Môn và số màn lấy từ RunState (chặng 1 = Hóa). Muốn thử riêng một môn: chọn RoomManager trong Hierarchy, điền Force Subject (hoa, sinh, ly).
/// </summary>
public static class RoomSceneSetup
{
    const string ScenePath = "Assets/Scenes/RoomScene.unity";
    const string JsonPath = "Assets/DataBank/Essay/essay_problems.json";

    [MenuItem("Tools/KHTN 8/Tạo RoomScene (E2)")]
    public static void Create()
    {
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
        if (json == null) { EditorUtility.DisplayDialog("RoomScene", "Không thấy " + JsonPath, "OK"); return; }
        if (System.IO.File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("RoomScene", "RoomScene đã tồn tại. Ghi đè?", "Ghi đè", "Huỷ")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) Object.DestroyImmediate(l.gameObject);
        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true; cam.orthographicSize = 5.4f; cam.transform.position = new Vector3(0, 0, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.05f, .04f, .09f);
        }

        var go = new GameObject("RoomManager");
        var mgr = go.AddComponent<RoomManager>();
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
        Debug.Log("[Room] Đã tạo " + ScenePath + ". Bấm Play để chơi thử một màn.");
    }
}
#endif
