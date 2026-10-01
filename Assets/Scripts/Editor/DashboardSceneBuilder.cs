#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Menu: Tools > KHTN 8 > Tạo DashboardScene
/// Dựng scene Dashboard tiến độ học tập, gán sẵn mọi tham chiếu cho DashboardManager,
/// lưu Assets/Scenes/DashboardScene.unity và thêm vào Build Settings.
/// Kèm menu thêm dữ liệu mẫu / xoá tiến độ (dùng khi Play để xem thử).
/// </summary>
public static class DashboardSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/DashboardScene.unity";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    private static readonly Color Ink = new Color(0.16f, 0.14f, 0.38f);
    private static readonly Color Purple = new Color(0.38f, 0.29f, 0.80f);
    private static readonly Color PanelWhite = new Color(1f, 1f, 1f, 0.97f);

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
            cam.backgroundColor = new Color(0.44f, 0.4f, 0.85f);
        }

        // ---- Tài nguyên ----
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var bgSprite = SceneBuildUtil.LoadSprite("Assets/Sprites/SubjectSelect/bg_subject.png");
        var glowSprite = SceneBuildUtil.LoadSprite("Assets/Sprites/SubjectSelect/glow.png");
        var sparkle = SceneBuildUtil.LoadSprite("Assets/Sprites/SubjectSelect/sparkle.png");
        var avatarSprite = SceneBuildUtil.LoadSprite("Assets/Sprites/Gameplay/avatar_icon_128x128.png");
        var chestSprite = SceneBuildUtil.LoadSprite("Assets/Sprites/Gameplay/treasure_chest_icon_128x128.png");
        var panel = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        var btnBg = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // ---- Canvas ----
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasGo.transform;

        // ---- Nền ----
        var bg = SceneBuildUtil.NewUI("Background", root);
        SceneBuildUtil.Stretch(bg);
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.sprite = bgSprite;
        bgImg.color = bgSprite != null ? Color.white : new Color(0.44f, 0.4f, 0.85f);
        bgImg.raycastTarget = false;

        var glow = SceneBuildUtil.NewUI("BgGlow", root);
        SceneBuildUtil.Place(glow, Vector2.zero, new Vector2(1500f, 1500f));
        var glowImg = glow.gameObject.AddComponent<Image>();
        glowImg.sprite = glowSprite;
        glowImg.color = new Color(1f, 1f, 1f, 0.4f);
        glowImg.raycastTarget = false;

        var symbols = SceneBuildUtil.NewUI("ScienceSymbols", root);
        SceneBuildUtil.Stretch(symbols);
        var fms = symbols.gameObject.AddComponent<FloatingScienceSymbols>();
        SceneBuildUtil.SetRef(fms, "font", font);

        // ---- Tiêu đề ----
        var titleGroup = SceneBuildUtil.NewUI("TitleGroup", root);
        SceneBuildUtil.Place(titleGroup, new Vector2(420f, 415f), new Vector2(900f, 130f));
        var titleShadow = SceneBuildUtil.MakeText("Shadow", titleGroup, "BẢNG TIẾN ĐỘ", 84f, new Color(0.25f, 0.2f, 0.6f, 0.55f), font, FontStyles.Bold, TextAlignmentOptions.Center);
        SceneBuildUtil.Place(titleShadow.rectTransform, new Vector2(5f, -5f), new Vector2(900f, 120f));
        var title = SceneBuildUtil.MakeText("Title", titleGroup, "BẢNG TIẾN ĐỘ", 84f, Color.white, font, FontStyles.Bold, TextAlignmentOptions.Center);
        SceneBuildUtil.Place(title.rectTransform, Vector2.zero, new Vector2(900f, 120f));

        // ---- Header học sinh ----
        var headerGroup = SceneBuildUtil.NewUI("HeaderGroup", root);
        SceneBuildUtil.Place(headerGroup, new Vector2(-500f, 385f), new Vector2(760f, 150f));
        var avatar = SceneBuildUtil.NewUI("Avatar", headerGroup);
        SceneBuildUtil.Place(avatar, new Vector2(-290f, 0f), new Vector2(130f, 130f));
        var avatarImg = avatar.gameObject.AddComponent<Image>();
        avatarImg.sprite = avatarSprite; avatarImg.preserveAspect = true; avatarImg.raycastTarget = false;
        var nameText = SceneBuildUtil.MakeText("Name", headerGroup, "Xin chào!", 50f, Color.white, font, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SceneBuildUtil.Place(nameText.rectTransform, new Vector2(60f, 25f), new Vector2(560f, 70f));
        var classText = SceneBuildUtil.MakeText("Class", headerGroup, "Lớp 8", 34f, new Color(1f, 1f, 1f, 0.9f), font, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SceneBuildUtil.Place(classText.rectTransform, new Vector2(60f, -30f), new Vector2(560f, 50f));

        // ---- Nút quay lại ----
        var backRt = SceneBuildUtil.NewUI("BackButton", root);
        SceneBuildUtil.Place(backRt, new Vector2(-830f, 495f), new Vector2(180f, 64f));
        var backImg = backRt.gameObject.AddComponent<Image>();
        backImg.sprite = btnBg; backImg.type = Image.Type.Sliced;
        backImg.color = new Color(1f, 1f, 1f, 0.92f);
        var backBtn = backRt.gameObject.AddComponent<Button>();
        backBtn.targetGraphic = backImg;
        backBtn.transition = Selectable.Transition.None;
        backRt.gameObject.AddComponent<AnimatedButton>();
        var backLabel = SceneBuildUtil.MakeText("Label", backRt, "‹ Quay lại", 30f, Ink, font, FontStyles.Bold, TextAlignmentOptions.Center);
        SceneBuildUtil.Stretch(backLabel.rectTransform);

        // ---- 4 thẻ số liệu ----
        string[] statLabels = { "HUY HIỆU", "ĐIỂM CAO NHẤT", "LƯỢT CHƠI", "ĐỘ CHÍNH XÁC" };
        Color[] statColors =
        {
            new Color(1f, 0.78f, 0.25f), new Color(1f, 0.55f, 0.3f), new Color(0.4f, 0.65f, 1f), new Color(0.35f, 0.85f, 0.55f)
        };
        var statCards = new RectTransform[4];
        var statValues = new TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
        {
            var card = MakePanel("StatCard_" + i, root, new Vector2(-690f + i * 460f, 195f), new Vector2(420f, 170f), panel);
            statCards[i] = card;

            var accent = SceneBuildUtil.NewUI("Accent", card);
            SceneBuildUtil.Place(accent, new Vector2(-190f, 0f), new Vector2(12f, 110f));
            var accentImg = accent.gameObject.AddComponent<Image>();
            accentImg.sprite = panel; accentImg.type = Image.Type.Sliced;
            accentImg.color = statColors[i]; accentImg.raycastTarget = false;

            var label = SceneBuildUtil.MakeText("Label", card, statLabels[i], 26f, new Color(Ink.r, Ink.g, Ink.b, 0.6f), font, FontStyles.Bold, TextAlignmentOptions.Center);
            SceneBuildUtil.Place(label.rectTransform, new Vector2(10f, 48f), new Vector2(360f, 40f));
            var value = SceneBuildUtil.MakeText("Value", card, "0", 80f, Purple, font, FontStyles.Bold, TextAlignmentOptions.Center);
            SceneBuildUtil.Place(value.rectTransform, new Vector2(10f, -18f), new Vector2(360f, 100f));
            statValues[i] = value;
        }

        // ---- Khối Tiến độ học tập ----
        var subjectPanel = MakePanel("SubjectPanel", root, new Vector2(-450f, -100f), new Vector2(860f, 400f), panel);
        var subjHeader = SceneBuildUtil.MakeText("Header", subjectPanel, "TIẾN ĐỘ HỌC TẬP", 36f, Purple, font, FontStyles.Bold, TextAlignmentOptions.Center);
        SceneBuildUtil.Place(subjHeader.rectTransform, new Vector2(0f, 168f), new Vector2(700f, 50f));

        var lessonText = SceneBuildUtil.MakeText("LessonText", subjectPanel, "Bài học hoàn thành: 0/20", 26f, Ink, font, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SceneBuildUtil.Place(lessonText.rectTransform, new Vector2(0f, 122f), new Vector2(780f, 36f));
        var lessonFrame = SceneBuildUtil.NewUI("LessonBar", subjectPanel);
        SceneBuildUtil.Place(lessonFrame, new Vector2(0f, 88f), new Vector2(780f, 30f));
        var lessonFrameImg = lessonFrame.gameObject.AddComponent<Image>();
        lessonFrameImg.sprite = panel; lessonFrameImg.type = Image.Type.Sliced;
        lessonFrameImg.color = new Color(0.88f, 0.9f, 0.98f); lessonFrameImg.raycastTarget = false;
        var lessonFill = SceneBuildUtil.NewUI("Fill", lessonFrame);
        SceneBuildUtil.Stretch(lessonFill);
        var lessonFillImg = lessonFill.gameObject.AddComponent<Image>();
        lessonFillImg.sprite = panel; lessonFillImg.type = Image.Type.Sliced;
        lessonFillImg.color = new Color(1f, 0.74f, 0.2f); lessonFillImg.raycastTarget = false;

        var subjectList = SceneBuildUtil.NewUI("SubjectList", subjectPanel);
        SceneBuildUtil.Place(subjectList, new Vector2(0f, 35f), new Vector2(800f, 10f));
        subjectList.pivot = new Vector2(0.5f, 1f);

        // ---- Khối Lịch sử ----
        var historyPanel = MakePanel("HistoryPanel", root, new Vector2(450f, -100f), new Vector2(860f, 400f), panel);
        var histHeader = SceneBuildUtil.MakeText("Header", historyPanel, "LƯỢT CHƠI GẦN ĐÂY", 36f, Purple, font, FontStyles.Bold, TextAlignmentOptions.Center);
        SceneBuildUtil.Place(histHeader.rectTransform, new Vector2(0f, 168f), new Vector2(700f, 50f));
        var historyList = SceneBuildUtil.NewUI("HistoryList", historyPanel);
        SceneBuildUtil.Place(historyList, new Vector2(0f, 135f), new Vector2(800f, 10f));
        historyList.pivot = new Vector2(0.5f, 1f);
        var emptyText = SceneBuildUtil.MakeText("Empty", historyPanel, "Chưa có lượt chơi nào.\nHãy thử một màn để bắt đầu nhé!", 30f,
            new Color(Ink.r, Ink.g, Ink.b, 0.55f), font, FontStyles.Bold, TextAlignmentOptions.Center);
        SceneBuildUtil.Place(emptyText.rectTransform, new Vector2(0f, -10f), new Vector2(700f, 140f));

        // ---- Khối Huy hiệu ----
        var badgePanel = MakePanel("BadgePanel", root, new Vector2(0f, -415f), new Vector2(1780f, 220f), panel);
        var badgeHeader = SceneBuildUtil.MakeText("Header", badgePanel, "HUY HIỆU", 32f, Purple, font, FontStyles.Bold, TextAlignmentOptions.Center);
        SceneBuildUtil.Place(badgeHeader.rectTransform, new Vector2(0f, 88f), new Vector2(700f, 44f));
        var badgeList = SceneBuildUtil.NewUI("BadgeList", badgePanel);
        SceneBuildUtil.Place(badgeList, new Vector2(0f, -22f), new Vector2(1700f, 140f));

        // ---- Hiệu ứng + Fade ----
        var fx = SceneBuildUtil.NewUI("Effects", root);
        SceneBuildUtil.Stretch(fx);

        var fade = SceneBuildUtil.NewUI("FadeOverlay", root);
        SceneBuildUtil.Stretch(fade);
        var fadeImg = fade.gameObject.AddComponent<Image>();
        fadeImg.color = Color.black;
        var fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;

        // ---- EventSystem ----
        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
        es.AddComponent<StandaloneInputModule>();
#endif

        // ---- DashboardManager ----
        var mgrGo = new GameObject("DashboardManager");
        var mgr = mgrGo.AddComponent<DashboardManager>();
        SceneBuildUtil.SetRef(mgr, "font", font);
        SceneBuildUtil.SetRef(mgr, "panelSprite", panel);
        SceneBuildUtil.SetRef(mgr, "chestSprite", chestSprite);
        SceneBuildUtil.SetRef(mgr, "sparkleSprite", sparkle);
        SceneBuildUtil.SetRef(mgr, "titleGroup", titleGroup);
        SceneBuildUtil.SetRef(mgr, "headerGroup", headerGroup);
        SceneBuildUtil.SetRef(mgr, "avatar", avatar);
        SceneBuildUtil.SetRef(mgr, "nameText", nameText);
        SceneBuildUtil.SetRef(mgr, "classText", classText);
        SceneBuildUtil.SetRef(mgr, "backButton", backBtn);
        SceneBuildUtil.SetRef(mgr, "subjectPanel", subjectPanel);
        SceneBuildUtil.SetRef(mgr, "historyPanel", historyPanel);
        SceneBuildUtil.SetRef(mgr, "badgePanel", badgePanel);
        SceneBuildUtil.SetRef(mgr, "lessonBarFill", lessonFill);
        SceneBuildUtil.SetRef(mgr, "lessonText", lessonText);
        SceneBuildUtil.SetRef(mgr, "subjectList", subjectList);
        SceneBuildUtil.SetRef(mgr, "historyList", historyList);
        SceneBuildUtil.SetRef(mgr, "badgeList", badgeList);
        SceneBuildUtil.SetRef(mgr, "badgeHeader", badgeHeader);
        SceneBuildUtil.SetRef(mgr, "emptyText", emptyText);
        SceneBuildUtil.SetRef(mgr, "bgGlow", glowImg);
        SceneBuildUtil.SetRef(mgr, "fadeOverlay", fadeGroup);
        SceneBuildUtil.SetRef(mgr, "effectsRoot", fx);

        var so = new SerializedObject(mgr);
        SetArray(so.FindProperty("statCards"), statCards);
        SetArray(so.FindProperty("statValues"), statValues);
        so.ApplyModifiedPropertiesWithoutUndo();

        // ---- Lưu + Build Settings ----
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

    private static RectTransform MakePanel(string name, Transform parent, Vector2 pos, Vector2 size, Sprite panelSprite)
    {
        var card = SceneBuildUtil.NewUI(name, parent);
        SceneBuildUtil.Place(card, pos, size);

        var shadow = SceneBuildUtil.NewUI("Shadow", card);
        SceneBuildUtil.Place(shadow, new Vector2(8f, -12f), size);
        var shadowImg = shadow.gameObject.AddComponent<Image>();
        shadowImg.sprite = panelSprite; shadowImg.type = Image.Type.Sliced;
        shadowImg.color = new Color(0.1f, 0.05f, 0.3f, 0.26f);
        shadowImg.raycastTarget = false;

        var body = SceneBuildUtil.NewUI("Body", card);
        SceneBuildUtil.Stretch(body);
        var bodyImg = body.gameObject.AddComponent<Image>();
        bodyImg.sprite = panelSprite; bodyImg.type = Image.Type.Sliced;
        bodyImg.color = PanelWhite;
        bodyImg.raycastTarget = false;
        return card;
    }

    private static void SetArray(SerializedProperty prop, Object[] items)
    {
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}

/// <summary>Hàm dựng UI dùng chung cho các builder scene.</summary>
internal static class SceneBuildUtil
{
    public static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    public static TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Color color,
        TMP_FontAsset font, FontStyles style, TextAlignmentOptions align)
    {
        var rt = NewUI(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    public static Sprite LoadSprite(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.Sprite)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static void SetRef(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(property);
        if (p == null) { Debug.LogWarning("[Builder] Không tìm thấy field: " + property); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
