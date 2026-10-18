#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Tools > KHTN 8 > Áp MainMenu mới (dark fantasy)
/// Thêm GameObject "MainMenuScreen" vào scene MainMenu đang mở. Không xoá/sửa giao diện cũ
/// (giao diện cũ chỉ bị ẩn lúc chạy). Muốn quay lại giao diện cũ: xoá GameObject này.
/// </summary>
public static class MainMenuScreenInstaller
{
    [MenuItem("Tools/KHTN 8/Áp MainMenu mới (dark fantasy)")]
    public static void Install()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "MainMenu")
        {
            EditorUtility.DisplayDialog("Áp MainMenu mới", "Hãy mở scene MainMenu (Assets/Scenes/MainMenu.unity) rồi chạy lại lệnh này.", "OK");
            return;
        }
        if (Object.FindFirstObjectByType<MainMenuScreen>() != null)
        {
            EditorUtility.DisplayDialog("Áp MainMenu mới", "Scene đã có MainMenuScreen rồi.", "OK");
            return;
        }

        var go = new GameObject("MainMenuScreen");
        go.AddComponent<MainMenuScreen>();
        Undo.RegisterCreatedObjectUndo(go, "Thêm MainMenuScreen");
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = go;
        Debug.Log("[MainMenu] Đã thêm MainMenuScreen. Lưu scene (Ctrl+S) rồi bấm Play để xem.");
    }
}
#endif
