using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Bộ dựng UI bằng code dùng chung cho các scene Chọn chương / Chọn bài (UnityEngine.UI, 1920x1080).</summary>
public static class SinhMenuKit
{
    public static readonly Vector2 C = new Vector2(.5f, .5f);
    public static readonly Color Gold = new Color(.96f, .77f, .26f), GoldHi = new Color(1f, .87f, .45f);
    public static readonly Color Cream = new Color(.99f, .94f, .84f), Green = new Color(.18f, .62f, .31f), GreenHi = new Color(.25f, .72f, .38f);
    public static readonly Color Ink = new Color(.17f, .1f, .05f), Muted = new Color(.45f, .4f, .35f);
    static Font font;

    public static Font GetFont()
    {
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Roboto", "Noto Sans", "Helvetica Neue", "Helvetica" }, 48);
        return font;
    }

    // ---------- Canvas / nền ----------
    public static RectTransform BuildCanvas(Transform parent, Sprite bg)
    {
        var cgo = new GameObject("UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(parent, false);
        var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = .5f;
        var root = (RectTransform)cgo.transform;

        var bgRt = Box("Background", root, Color.white, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        Stretch(bgRt, 0, 0, 0, 0);
        var img = bgRt.GetComponent<Image>();
        if (bg != null) img.sprite = bg; else img.color = new Color(.36f, .3f, .72f);
        return root;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
        var m = es.AddComponent<InputSystemUIInputModule>(); m.AssignDefaultActions();
    }

    public static void LoadScene(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[Sinh] Scene '{sceneName}' chưa có trong Build Settings (File > Build Profiles > Scene List).");
            return;
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    // ---------- Phần tử cơ bản ----------
    public static RectTransform Place(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    { r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size; return r; }

    public static void Stretch(RectTransform r, float l, float t, float rr, float b)
    { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = C; r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rr, -t); }

    public static RectTransform Box(string n, Transform p, Color col, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, bool ray = false)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image)); go.transform.SetParent(p, false);
        var im = go.GetComponent<Image>(); im.sprite = ProcSprites.Pixel; im.color = col; im.raycastTarget = ray;
        return Place((RectTransform)go.transform, aMin, aMax, pivot, pos, size);
    }

    /// <summary>Hộp đặt theo tâm cha: pos/size tính theo pixel canvas.</summary>
    public static RectTransform CBox(string n, Transform p, Color col, Vector2 pos, Vector2 size, bool ray = false) => Box(n, p, col, C, C, C, pos, size, ray);

    public static Text Label(string n, Transform p, string s, int size, Color col, TextAnchor al, Vector2 pos, Vector2 box, bool outline = false, FontStyle st = FontStyle.Bold)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Text)); go.transform.SetParent(p, false);
        var t = go.GetComponent<Text>(); t.font = GetFont(); t.fontSize = size; t.fontStyle = st; t.alignment = al; t.color = col; t.text = s;
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.lineSpacing = 1.1f;
        if (outline) { var o = go.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, .55f); o.effectDistance = new Vector2(2, -2); }
        Place(t.rectTransform, C, C, C, pos, box);
        return t;
    }

    /// <summary>Nút chữ nhật có viền vàng. Màu hover do UiCard xử lý (phóng to nhẹ).</summary>
    public static Button MakeButton(string n, Transform p, Vector2 pos, Vector2 size, Color fill, string label, int fs, Color txt, System.Action cb, Color? frameColor = null)
    {
        var frame = CBox(n, p, frameColor ?? Gold, pos, size, true);
        var inner = CBox("Fill", frame, fill, Vector2.zero, Vector2.zero); Stretch(inner, 5, 5, 5, 5);
        var b = frame.gameObject.AddComponent<Button>(); b.targetGraphic = frame.GetComponent<Image>(); b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => cb());
        var sh = frame.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .45f); sh.effectDistance = new Vector2(0, -6);
        var l = Label("Label", frame, label, fs, txt, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
        Stretch(l.rectTransform, 12, 0, 12, 0);
        var card = frame.gameObject.AddComponent<UiCard>(); card.hoverScale = 1.06f; card.Intro(0.05f);
        return b;
    }

    public static Button BackButton(Transform root, string label, System.Action cb)
        => MakeButton("Back", root, new Vector2(-740, 462), new Vector2(360, 84), Cream, label, 34, Ink, cb);

    // ---------- Cuộn + lưới ----------
    public static RectTransform MakeScroll(Transform p, Vector2 pos, Vector2 size)
    {
        var sv = CBox("Scroll", p, Color.clear, pos, size, true);
        var vp = CBox("Viewport", sv, Color.clear, Vector2.zero, Vector2.zero, true); Stretch(vp, 0, 0, 0, 0);
        vp.gameObject.AddComponent<RectMask2D>();
        var go = new GameObject("Content", typeof(RectTransform)); var content = (RectTransform)go.transform; content.SetParent(vp, false);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0, size.y);
        var sr = sv.gameObject.AddComponent<ScrollRect>();
        sr.viewport = vp; sr.content = content; sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 40f;
        return content;
    }

    /// <summary>Gắn lưới vào content và đặt chiều cao content (>= chiều cao khung nhìn để lưới nằm giữa khi ít phần tử).</summary>
    public static void SetupGrid(RectTransform content, int count, int cols, Vector2 cell, Vector2 spacing, float viewHeight)
    {
        var g = content.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = cell; g.spacing = spacing; g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = Mathf.Max(1, cols);
        g.childAlignment = TextAnchor.MiddleCenter; g.padding = new RectOffset(0, 0, 20, 20);
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)Mathf.Max(1, cols)));
        float h = rows * cell.y + (rows - 1) * spacing.y + 40f;
        content.sizeDelta = new Vector2(0, Mathf.Max(viewHeight, h));
    }

    // ---------- Thông báo nhỏ ----------
    public static void Toast(Transform root, string msg)
    {
        var box = CBox("Toast", root, new Color(.1f, .08f, .2f, .92f), new Vector2(0, -470), new Vector2(760, 80));
        Label("T", box, msg, 36, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero); Stretch(box.Find("T") as RectTransform, 10, 0, 10, 0);
        Object.Destroy(box.gameObject, 1.8f);
    }
}

/// <summary>Thẻ / nút: hiện dần khi bắt đầu, phóng to nhẹ khi rê chuột. Dùng unscaledTime.</summary>
public class UiCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float hoverScale = 1.05f;
    RectTransform rt; CanvasGroup cg; bool hot, ready;

    void Awake()
    {
        rt = (RectTransform)transform;
        cg = GetComponent<CanvasGroup>(); if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f; rt.localScale = Vector3.one * .85f;
    }

    public void Intro(float delay) => StartCoroutine(IntroRoutine(delay));

    IEnumerator IntroRoutine(float delay)
    {
        for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
        const float dur = .45f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / dur);
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(.85f, 1f, SubjectCard.EaseOutBack(k));
            cg.alpha = Mathf.Clamp01(k * 2.5f);
            yield return null;
        }
        cg.alpha = 1f; rt.localScale = Vector3.one; ready = true;
    }

    void Update()
    {
        if (!ready) return;
        float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
        rt.localScale = Vector3.Lerp(rt.localScale, Vector3.one * (hot ? hoverScale : 1f), k);
    }

    public void OnPointerEnter(PointerEventData e) { hot = true; }
    public void OnPointerExit(PointerEventData e) { hot = false; }
}
