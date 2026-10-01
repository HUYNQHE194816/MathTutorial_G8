#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Menu: Tools > KHTN 8 > Thêm nút Tiến độ vào MainMenu
/// Nhân bản nút Cài đặt thành "TIẾN ĐỘ" (đặt sau nút Bắt đầu), nối tới MainMenuManager.OnClickDashboard,
/// nới Panel_MenuButton xuống dưới (giữ nguyên mép trên) để các nút không bị co lại,
/// rồi lưu scene. Đồng thời sửa Build Settings: có MainMenu thật, bỏ dòng trỏ tới file không tồn tại.
/// Chạy lại nhiều lần vẫn an toàn (nếu nút đã có thì bỏ qua).
/// </summary>
public static class MainMenuDashboardButton
{
    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string ButtonName = "Btn_Dashboard";

    [MenuItem("Tools/KHTN 8/Thêm nút Tiến độ vào MainMenu")]
    public static void Add()
    {
        if (!File.Exists(MenuScenePath))
        {
            EditorUtility.DisplayDialog("Thêm nút Tiến độ", "Không tìm thấy " + MenuScenePath, "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

        var panel = GameObject.Find("Panel_MenuButton");
        var manager = Object.FindFirstObjectByType<MainMenuManager>();
        if (panel == null || manager == null)
        {
            EditorUtility.DisplayDialog("Thêm nút Tiến độ",
                "Không tìm thấy Panel_MenuButton hoặc MainMenuManager trong MainMenu.", "OK");
            return;
        }

        if (panel.transform.Find(ButtonName) != null)
        {
            Debug.Log("[MainMenu] Nút " + ButtonName + " đã có, bỏ qua.");
            EnsureBuildSettings();
            return;
        }

        var template = panel.transform.Find("Btn_Settings");
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        if (template == null || layout == null)
        {
            EditorUtility.DisplayDialog("Thêm nút Tiến độ",
                "Không tìm thấy Btn_Settings hoặc VerticalLayoutGroup trên Panel_MenuButton.", "OK");
            return;
        }

        // đo kích thước nút hiện tại trước khi thêm
        var panelRt = (RectTransform)panel.transform;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRt);
        float buttonHeight = ((RectTransform)panel.transform.GetChild(0)).rect.height;
        int count = panel.transform.childCount;

        // nhân bản nút Cài đặt
        var clone = Object.Instantiate(template.gameObject, panel.transform);
        clone.name = ButtonName;
        clone.transform.SetSiblingIndex(1);   // Bắt đầu > Tiến độ > Cài đặt > Thoát

        var label = clone.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "TIẾN ĐỘ";

        var button = clone.GetComponent<Button>();
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(manager.OnClickDashboard));

        // nới panel xuống, giữ nguyên mép trên để nút Bắt đầu không bị dịch
        float top = panelRt.anchoredPosition.y + panelRt.rect.height * (1f - panelRt.pivot.y);
        float newHeight = buttonHeight * (count + 1) + layout.spacing * count;
        panelRt.sizeDelta = new Vector2(panelRt.sizeDelta.x, newHeight);
        panelRt.anchoredPosition = new Vector2(panelRt.anchoredPosition.x, top - newHeight * (1f - panelRt.pivot.y));
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRt);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EnsureBuildSettings();

        Selection.activeGameObject = clone;
        Debug.Log("[MainMenu] Đã thêm nút TIẾN ĐỘ. Nhớ tạo DashboardScene (Tools > KHTN 8 > Tạo DashboardScene) nếu chưa có.");
    }

    /// <summary>Đảm bảo MainMenu thật có trong Build Settings và bỏ các dòng trỏ tới scene không tồn tại.</summary>
    private static void EnsureBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();

        // bỏ dòng lỗi thời trỏ tới file không có (ví dụ Assets/MainMenu.unity), trừ DashboardScene sắp được tạo
        scenes.RemoveAll(s => !File.Exists(s.path) && !s.path.EndsWith("DashboardScene.unity"));

        if (!scenes.Any(s => s.path == MenuScenePath))
            scenes.Add(new EditorBuildSettingsScene(MenuScenePath, true));

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
