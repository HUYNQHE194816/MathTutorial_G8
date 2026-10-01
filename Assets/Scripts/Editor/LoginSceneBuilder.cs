#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Menu: Tools > KHTN 8 > Tạo LoginScene
/// Dựng toàn bộ scene đăng nhập (Canvas, nền, nhân vật, thẻ đăng nhập, ô nhập, nút, EventSystem),
/// gán sẵn mọi tham chiếu cho LoginManager, lưu ra Assets/Scenes/LoginScene.unity
/// và đưa vào đầu Build Settings.
/// </summary>
public static class LoginSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/LoginScene.unity";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    private static readonly Color Ink = new Color(0.16f, 0.14f, 0.38f);
    private static readonly Color Purple = new Color(0.38f, 0.29f, 0.80f);

    [MenuItem("Tools/KHTN 8/Tạo LoginScene")]
    public static void Build()
    {
        if (System.IO.File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Tạo LoginScene", "LoginScene đã tồn tại. Ghi đè?", "Ghi đè", "Huỷ"))
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

        // ---- tài nguyên ----
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var bgSprite = LoadSprite("Assets/Sprites/SubjectSelect/bg_subject.png");
        var glowSprite = LoadSprite("Assets/Sprites/SubjectSelect/glow.png");
        var sparkle = LoadSprite("Assets/Sprites/SubjectSelect/sparkle.png");
        var avatar = LoadSprite("Assets/Sprites/Gameplay/avatar_icon_128x128.png");
        var panel = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        var inputBg = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
        var btnBg = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var okClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AudioBank/Correct.mp3");
        var failClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AudioBank/Fail.mp3");

        // ---- Canvas ----
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasGo.transform;

        // ---- Nền ----
        var bg = NewUI("Background", root);
        Stretch(bg);
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.sprite = bgSprite;
        bgImg.color = bgSprite != null ? Color.white : new Color(0.44f, 0.4f, 0.85f);
        bgImg.raycastTarget = false;

        var glow = NewUI("BgGlow", root);
        Place(glow, new Vector2(0f, 0f), new Vector2(1500f, 1500f));
        var glowImg = glow.gameObject.AddComponent<Image>();
        glowImg.sprite = glowSprite;
        glowImg.color = new Color(1f, 1f, 1f, 0.4f);
        glowImg.raycastTarget = false;

        var symbols = NewUI("ScienceSymbols", root);
        Stretch(symbols);
        var fms = symbols.gameObject.AddComponent<FloatingScienceSymbols>();
        SetRef(fms, "font", font);

        // ---- Nhân vật + bong bóng thoại ----
        var mascot = NewUI("Mascot", root);
        Place(mascot, new Vector2(-560f, -200f), new Vector2(300f, 300f));
        var mascotImg = mascot.gameObject.AddComponent<Image>();
        mascotImg.sprite = avatar;
        mascotImg.preserveAspect = true;
        mascotImg.raycastTarget = false;

        var bubble = NewUI("Bubble", root);
        Place(bubble, new Vector2(-560f, 40f), new Vector2(430f, 150f));
        var bubbleImg = bubble.gameObject.AddComponent<Image>();
        bubbleImg.sprite = panel;
        bubbleImg.type = Image.Type.Sliced;
        bubbleImg.color = new Color(1f, 1f, 1f, 0.97f);
        bubbleImg.raycastTarget = false;
        var tail = NewUI("Tail", bubble);
        Place(tail, new Vector2(-70f, -75f), new Vector2(40f, 40f));
        tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var tailImg = tail.gameObject.AddComponent<Image>();
        tailImg.color = new Color(1f, 1f, 1f, 0.97f);
        tailImg.raycastTarget = false;
        var bubbleText = MakeText("Text", bubble, "", 32f, Ink, font, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(bubbleText.rectTransform, 22f, 14f, 22f, 14f);

        // ---- Tiêu đề ----
        var title = NewUI("TitleGroup", root);
        Place(title, new Vector2(0f, 390f), new Vector2(1400f, 260f));
        var titleShadow = MakeText("TitleShadow", title, "GIA SƯ KHTN 8", 120f, new Color(0.25f, 0.2f, 0.6f, 0.55f), font, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(titleShadow.rectTransform, new Vector2(6f, 28f), new Vector2(1400f, 150f));
        var titleText = MakeText("Title", title, "GIA SƯ KHTN 8", 120f, Color.white, font, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(titleText.rectTransform, new Vector2(0f, 35f), new Vector2(1400f, 150f));
        var sub = MakeText("Subtitle", title, "Lý  ·  Hóa  ·  Sinh  -  Lớp 8", 40f, new Color(1f, 1f, 1f, 0.95f), font, FontStyles.Normal, TextAlignmentOptions.Center);
        Place(sub.rectTransform, new Vector2(0f, -70f), new Vector2(1000f, 60f));

        // ---- Thẻ đăng nhập ----
        var card = NewUI("Card", root);
        Place(card, new Vector2(220f, -110f), new Vector2(700f, 560f));
        var cardGroup = card.gameObject.AddComponent<CanvasGroup>();

        var shadow = NewUI("Shadow", card);
        Place(shadow, new Vector2(8f, -14f), new Vector2(700f, 560f));
        var shadowImg = shadow.gameObject.AddComponent<Image>();
        shadowImg.sprite = panel; shadowImg.type = Image.Type.Sliced;
        shadowImg.color = new Color(0.1f, 0.05f, 0.3f, 0.28f);
        shadowImg.raycastTarget = false;

        var body = NewUI("Panel", card);
        Stretch(body);
        var bodyImg = body.gameObject.AddComponent<Image>();
        bodyImg.sprite = panel; bodyImg.type = Image.Type.Sliced;
        bodyImg.color = new Color(1f, 1f, 1f, 0.97f);

        var header = MakeText("Header", card, "ĐĂNG NHẬP", 46f, Purple, font, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(header.rectTransform, new Vector2(0f, 205f), new Vector2(600f, 70f));

        // nhóm Tên
        var nameGroup = NewGroup("NameGroup", card);
        var nameLabel = MakeText("Label", nameGroup.transform, "Tên của bạn", 30f, Ink, font, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Place(nameLabel.rectTransform, new Vector2(0f, 125f), new Vector2(600f, 40f));
        var nameInput = MakeInput("NameInput", nameGroup.transform, "Ví dụ: Nguyễn Minh Anh", new Vector2(0f, 65f), font, inputBg, out var nameFrame);

        // nhóm Lớp
        var classGroup = NewGroup("ClassGroup", card);
        var classLabel = MakeText("Label", classGroup.transform, "Lớp", 30f, Ink, font, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Place(classLabel.rectTransform, new Vector2(0f, -10f), new Vector2(600f, 40f));
        var classInput = MakeInput("ClassInput", classGroup.transform, "Ví dụ: 8A1", new Vector2(0f, -70f), font, inputBg, out var classFrame);
        classInput.characterLimit = 10;

        // thông báo
        var msgRt = NewUI("Message", card);
        Place(msgRt, new Vector2(0f, -140f), new Vector2(600f, 40f));
        var msgGroup = msgRt.gameObject.AddComponent<CanvasGroup>();
        var msgText = MakeText("Text", msgRt, "", 28f, Ink, font, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(msgText.rectTransform);

        // nhóm Nút
        var btnGroup = NewGroup("ButtonGroup", card);
        var btnRt = NewUI("LoginButton", btnGroup.transform);
        Place(btnRt, new Vector2(0f, -215f), new Vector2(560f, 100f));
        var btnImg = btnRt.gameObject.AddComponent<Image>();
        btnImg.sprite = btnBg; btnImg.type = Image.Type.Sliced;
        btnImg.color = new Color(1f, 0.74f, 0.2f);
        var btn = btnRt.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.transition = Selectable.Transition.None;
        var btnFx = btnRt.gameObject.AddComponent<AnimatedButton>();
        var btnLabel = MakeText("Label", btnRt, "VÀO HỌC NÀO!", 44f, Ink, font, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(btnLabel.rectTransform);
        btnLabel.raycastTarget = false;

        // ---- Hiệu ứng + Fade ----
        var fx = NewUI("Effects", root);
        Stretch(fx);

        var fade = NewUI("FadeOverlay", root);
        Stretch(fade);
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

        // ---- LoginManager ----
        var mgrGo = new GameObject("LoginManager", typeof(AudioSource));
        var audio = mgrGo.GetComponent<AudioSource>();
        audio.playOnAwake = false;
        var mgr = mgrGo.AddComponent<LoginManager>();

        SetRef(mgr, "titleGroup", title);
        SetRef(mgr, "mascot", mascot);
        SetRef(mgr, "bubble", bubble);
        SetRef(mgr, "bubbleText", bubbleText);
        SetRef(mgr, "card", card);
        SetRef(mgr, "cardGroup", cardGroup);
        SetRef(mgr, "bgGlow", glowImg);
        SetRef(mgr, "nameInput", nameInput);
        SetRef(mgr, "classInput", classInput);
        SetRef(mgr, "nameFrame", nameFrame);
        SetRef(mgr, "classFrame", classFrame);
        SetRef(mgr, "loginButton", btn);
        SetRef(mgr, "loginButtonFx", btnFx);
        SetRef(mgr, "loginButtonLabel", btnLabel);
        SetRef(mgr, "messageText", msgText);
        SetRef(mgr, "messageGroup", msgGroup);
        SetRef(mgr, "fadeOverlay", fadeGroup);
        SetRef(mgr, "effectsRoot", fx);
        SetRef(mgr, "sparkleSprite", sparkle);
        SetRef(mgr, "sfx", audio);
        SetRef(mgr, "successClip", okClip);
        SetRef(mgr, "errorClip", failClip);

        var so = new SerializedObject(mgr);
        var arr = so.FindProperty("fieldGroups");
        var groups = new[] { nameGroup, classGroup, btnGroup };
        arr.arraySize = groups.Length;
        for (int i = 0; i < groups.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = groups[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        // ---- Lưu + Build Settings ----
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettingsFirst();
        AssetDatabase.Refresh();
        Selection.activeGameObject = mgrGo;
        Debug.Log("[Login] Đã tạo " + ScenePath + " và đặt làm scene đầu trong Build Settings. Nhấn Play để xem.");
    }

    // ---------- Build Settings ----------
    private static void AddToBuildSettingsFirst()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------- Helpers dựng UI ----------
    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static CanvasGroup NewGroup(string name, Transform parent)
    {
        var rt = NewUI(name, parent);
        Stretch(rt);
        return rt.gameObject.AddComponent<CanvasGroup>();
    }

    private static void Stretch(RectTransform rt, float l = 0f, float b = 0f, float r = 0f, float t = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
    }

    private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Color color,
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

    private static TMP_InputField MakeInput(string name, Transform parent, string placeholder, Vector2 pos,
        TMP_FontAsset font, Sprite frameSprite, out Image frame)
    {
        var rt = NewUI(name, parent);
        Place(rt, pos, new Vector2(600f, 84f));
        frame = rt.gameObject.AddComponent<Image>();
        frame.sprite = frameSprite;
        frame.type = Image.Type.Sliced;
        frame.color = new Color(0.94f, 0.95f, 1f);

        var viewport = NewUI("Text Area", rt);
        Stretch(viewport, 24f, 8f, 24f, 8f);
        viewport.gameObject.AddComponent<RectMask2D>();

        var ph = MakeText("Placeholder", viewport, placeholder, 34f, new Color(Ink.r, Ink.g, Ink.b, 0.4f), font, FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
        Stretch(ph.rectTransform);
        var tx = MakeText("Text", viewport, "", 36f, Ink, font, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Stretch(tx.rectTransform);

        var input = rt.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = viewport;
        input.textComponent = tx;
        input.placeholder = ph;
        if (font != null) input.fontAsset = font;
        input.pointSize = 36f;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 30;
        input.targetGraphic = frame;
        input.transition = Selectable.Transition.None;
        input.customCaretColor = true;
        input.caretColor = Purple;
        input.caretWidth = 3;
        input.selectionColor = new Color(0.45f, 0.4f, 0.9f, 0.35f);
        return input;
    }

    private static Sprite LoadSprite(string path)
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

    private static void SetRef(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(property);
        if (p == null) { Debug.LogWarning("[Login] Không tìm thấy field: " + property); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
