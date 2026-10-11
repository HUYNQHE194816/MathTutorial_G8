using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu: Tools ▸ Math Tutorial ▸ Tạo Professor Review Scene
/// Tự dựng Assets/Scenes/ProfessorReviewScene.unity (UI + gắn ProfessorDialogueManager) và thêm vào Build Settings.
/// Cần có sprite ở Assets/Sprites/Professor/meomeo_neutral|happy|think.png.
/// </summary>
public static class ProfessorSceneCreator
{
    const string ScenePath = "Assets/Scenes/ProfessorReviewScene.unity";
    const string SpriteDir = "Assets/Sprites/Professor/";
    const string BgPath = "Assets/Sprites/SubjectSelect/bg_subject.png";
    const string FontGuid = "8f586378b4e144a9851e7b34d9b748ee"; // font TMP đang dùng trong các scene cũ

    static TMP_FontAsset font;

    [MenuItem("Tools/Math Tutorial/Tạo Professor Review Scene")]
    static void Create()
    {
        if (System.IO.File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Professor Review Scene",
                "Scene đã tồn tại. Tạo lại và ghi đè?", "Ghi đè", "Hủy"))
            return;

        Sprite neutral = LoadSprite(SpriteDir + "meomeo_neutral.png");
        Sprite happy = LoadSprite(SpriteDir + "meomeo_happy.png");
        Sprite think = LoadSprite(SpriteDir + "meomeo_think.png");
        if (neutral == null || happy == null || think == null)
        {
            EditorUtility.DisplayDialog("Thiếu sprite",
                "Hãy chép 3 file meomeo_neutral.png, meomeo_happy.png, meomeo_think.png vào " + SpriteDir, "OK");
            return;
        }

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        // ── Camera ──
        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
        var c = cam.GetComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(.06f, .08f, .16f);

        // ── EventSystem (Input System mới nếu có) ──
        var es = new GameObject("EventSystem", typeof(EventSystem));
        var inputModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModuleType != null)
        {
            var module = es.AddComponent(inputModuleType);
            var assign = inputModuleType.GetMethod("AssignDefaultActions");
            if (assign != null) assign.Invoke(module, null);
        }
        else es.AddComponent<StandaloneInputModule>();

        // ── Canvas ──
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = canvasGo.transform;

        // Nền
        var bg = NewImage("Background", root, Vector2.zero, Vector2.one, new Color(.55f, .6f, .75f));
        bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
        bg.raycastTarget = false;

        // ── Header ──
        var backBtn = NewButton("BackButton", root, new Vector2(.015f, .935f), new Vector2(.12f, .99f),
            new Color(.2f, .3f, .55f), "← Menu", 30, out _);
        NewText("Title", root, new Vector2(.30f, .935f), new Vector2(.70f, .99f),
            "LỚP HỌC GIÁO SƯ MEOMEO", 40, Color.white, TextAlignmentOptions.Center);
        var counter = NewText("CounterText", root, new Vector2(.74f, .935f), new Vector2(.985f, .99f),
            "Câu sai: 0 / 0", 32, Color.white, TextAlignmentOptions.Right);

        // ── Thẻ câu hỏi ──
        var card = NewImage("QuestionCard", root, new Vector2(.03f, .52f), new Vector2(.97f, .92f),
            new Color(.06f, .09f, .18f, .9f));
        card.raycastTarget = false;
        var qText = NewText("QText", card.transform, new Vector2(.03f, .55f), new Vector2(.97f, .96f),
            "Câu hỏi", 40, Color.white, TextAlignmentOptions.TopLeft, true, 26, 42);

        var optContainer = NewRect("Options", card.transform, new Vector2(.03f, .05f), new Vector2(.97f, .52f));
        var optGrid = optContainer.gameObject.AddComponent<GridLayoutGroup>();
        optGrid.cellSize = new Vector2(810, 90);
        optGrid.spacing = new Vector2(20, 14);
        optGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        optGrid.constraintCount = 2;

        var optBgs = new Image[4];
        var optLabels = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            var o = NewImage("Option_" + i, optContainer, Vector2.zero, Vector2.one, new Color32(40, 50, 70, 255));
            o.raycastTarget = false;
            var l = NewText("OptionLabel_" + i, o.transform, Vector2.zero, Vector2.one,
                "", 30, Color.white, TextAlignmentOptions.Left, true, 20, 32);
            l.rectTransform.offsetMin = new Vector2(24, 0);
            l.rectTransform.offsetMax = new Vector2(-24, 0);
            optBgs[i] = o;
            optLabels[i] = l;
        }

        // ── Giáo sư ──
        var portraitImg = NewImage("Professor", root, new Vector2(.02f, .02f), new Vector2(.30f, .50f), Color.white);
        portraitImg.sprite = neutral;
        portraitImg.preserveAspect = true;
        portraitImg.raycastTarget = false;

        // ── Hộp thoại ──
        var box = NewImage("DialogueBox", root, new Vector2(.31f, .02f), new Vector2(.98f, .50f),
            new Color(.07f, .09f, .17f, .95f));
        var boxBtn = box.gameObject.AddComponent<Button>();
        boxBtn.targetGraphic = box;
        boxBtn.transition = Selectable.Transition.None;

        var tag = NewImage("NameTag", box.transform, new Vector2(.03f, .84f), new Vector2(.36f, .97f),
            new Color(.88f, .68f, .22f));
        tag.raycastTarget = false;
        var speaker = NewText("SpeakerText", tag.transform, Vector2.zero, Vector2.one,
            ProfessorLines.ProfessorName, 30, new Color(.12f, .08f, .02f), TextAlignmentOptions.Center);

        var dialogue = NewText("DialogueText", box.transform, new Vector2(.03f, .38f), new Vector2(.97f, .82f),
            "", 34, Color.white, TextAlignmentOptions.TopLeft, true, 22, 36);

        var choiceContainer = NewRect("Choices", box.transform, new Vector2(.03f, .03f), new Vector2(.97f, .36f));
        var grid = choiceContainer.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(560, 70);
        grid.spacing = new Vector2(16, 12);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;

        var choiceBtns = new Button[4];
        var choiceLbls = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            choiceBtns[i] = NewButton("Choice_" + i, choiceContainer, Vector2.zero, Vector2.one,
                new Color(.28f, .45f, .85f), "", 28, out choiceLbls[i], true);
        }

        // ── Manager ──
        var mgrGo = new GameObject("ProfessorDialogueManager");
        var audio = mgrGo.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        var mgr = mgrGo.AddComponent<ProfessorDialogueManager>();

        var so = new SerializedObject(mgr);
        so.FindProperty("counterText").objectReferenceValue = counter;
        so.FindProperty("backButton").objectReferenceValue = backBtn;
        so.FindProperty("questionCard").objectReferenceValue = card.gameObject;
        so.FindProperty("questionText").objectReferenceValue = qText;
        SetArray(so.FindProperty("optionBackgrounds"), optBgs);
        SetArray(so.FindProperty("optionLabels"), optLabels);
        so.FindProperty("portrait").objectReferenceValue = portraitImg.rectTransform;
        so.FindProperty("portraitImage").objectReferenceValue = portraitImg;
        so.FindProperty("faceNeutral").objectReferenceValue = neutral;
        so.FindProperty("faceHappy").objectReferenceValue = happy;
        so.FindProperty("faceThink").objectReferenceValue = think;
        so.FindProperty("speakerText").objectReferenceValue = speaker;
        so.FindProperty("dialogueText").objectReferenceValue = dialogue;
        so.FindProperty("dialogueBox").objectReferenceValue = boxBtn;
        SetArray(so.FindProperty("choiceButtons"), choiceBtns);
        SetArray(so.FindProperty("choiceLabels"), choiceLbls);
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.FindProperty("correctClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AudioBank/Correct.mp3");
        so.FindProperty("wrongClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AudioBank/Fail.mp3");
        so.ApplyModifiedPropertiesWithoutUndo();

        // Chuyển các root object sang scene mới rồi lưu
        foreach (var go in new[] { cam, es, canvasGo, mgrGo })
            SceneManager.MoveGameObjectToScene(go, scene);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        Register();

        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Debug.Log("[Professor] Đã tạo " + ScenePath + ". Nhớ đổi tên scene review trong GameManager / MillionaireManager.");
    }

    // ───────────────────────── Helpers ─────────────────────────

    static void SetArray(SerializedProperty prop, UnityEngine.Object[] items)
    {
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    static Sprite LoadSprite(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return null;
        if (imp.textureType != TextureImporterType.Sprite)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static Image NewImage(string name, Transform parent, Vector2 aMin, Vector2 aMax, Color color)
    {
        var rt = NewRect(name, parent, aMin, aMax);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static TMP_Text NewText(string name, Transform parent, Vector2 aMin, Vector2 aMax, string text,
        float size, Color color, TextAlignmentOptions align, bool autoSize = false, float minSize = 18, float maxSize = 36)
    {
        var rt = NewRect(name, parent, aMin, aMax);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        if (autoSize)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = minSize;
            t.fontSizeMax = maxSize;
        }
        return t;
    }

    static Button NewButton(string name, Transform parent, Vector2 aMin, Vector2 aMax, Color color,
        string label, float fontSize, out TMP_Text labelText, bool autoSize = false)
    {
        var img = NewImage(name, parent, aMin, aMax, color);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;

        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(.88f, .93f, 1f);
        cb.pressedColor = new Color(.7f, .75f, .88f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(.45f, .45f, .45f, .75f);
        btn.colors = cb;

        labelText = NewText("Label", img.transform, Vector2.zero, Vector2.one, label, fontSize,
            Color.white, TextAlignmentOptions.Center, autoSize, 18, fontSize);
        labelText.rectTransform.offsetMin = new Vector2(12, 0);
        labelText.rectTransform.offsetMax = new Vector2(-12, 0);
        return btn;
    }

    static void Register()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}