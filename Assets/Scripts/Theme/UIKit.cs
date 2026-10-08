using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public enum PanelStyle { Parchment, Dark }
public enum ButtonStyle { Primary, Secondary, Danger }

/// <summary>
/// Bộ dựng UI bằng code DÙNG CHUNG cho toàn game (UnityEngine.UI, canvas tham chiếu 1920x1080).
/// Thay cho SinhMenuKit + các màu rải rác trong từng scene. Tất cả lấy màu/font từ GameTheme/GameFont.
/// Toạ độ pos/size tính theo pixel canvas, gốc ở TÂM của phần tử cha.
/// </summary>
public static class UIKit
{
    public static readonly Vector2 C = new Vector2(.5f, .5f);

    // ---------- Canvas / hệ thống ----------
    public static RectTransform BuildCanvas(Transform parent, string name = "UI Canvas", int sortingOrder = 10)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = sortingOrder;
        var sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(GameTheme.CanvasW, GameTheme.CanvasH);
        sc.matchWidthOrHeight = .5f;
        return (RectTransform)go.transform;
    }

    public static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
        var m = es.AddComponent<InputSystemUIInputModule>(); m.AssignDefaultActions();
    }

    // ---------- Phần tử cơ bản ----------
    public static RectTransform Place(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    { r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size; return r; }

    public static void Stretch(RectTransform r, float l, float t, float rr, float b)
    { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = C; r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rr, -t); }

    public static RectTransform Box(string n, Transform p, Color col, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, bool ray = false)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image)); go.transform.SetParent(p, false);
        var im = go.GetComponent<Image>(); im.sprite = ThemeGfx.Pixel; im.color = col; im.raycastTarget = ray;
        return Place((RectTransform)go.transform, aMin, aMax, pivot, pos, size);
    }

    /// <summary>Hộp đặt theo tâm cha.</summary>
    public static RectTransform CBox(string n, Transform p, Color col, Vector2 pos, Vector2 size, bool ray = false)
        => Box(n, p, col, C, C, C, pos, size, ray);

    public static Text Label(string n, Transform p, string s, int size, Color col, TextAnchor al, Vector2 pos, Vector2 box,
        bool display = false, bool outline = false, FontStyle st = FontStyle.Normal)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Text)); go.transform.SetParent(p, false);
        var t = go.GetComponent<Text>();
        t.font = display ? GameFont.Display : GameFont.Body;
        t.fontSize = size; t.fontStyle = st; t.alignment = al; t.color = col; t.text = s;
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.lineSpacing = 1.15f;
        if (outline)
        {
            var o = go.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, .6f); o.effectDistance = new Vector2(2, -2);
        }
        Place(t.rectTransform, C, C, C, pos, box);
        return t;
    }

    static void Outline4(Transform p, float inset, float th, Color c)
    {
        Box("T", p, c, new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), new Vector2(0, -inset), new Vector2(-2 * inset, th));
        Box("B", p, c, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), new Vector2(0, inset), new Vector2(-2 * inset, th));
        Box("L", p, c, Vector2.zero, new Vector2(0, 1), new Vector2(0, .5f), new Vector2(inset, 0), new Vector2(th, -2 * inset));
        Box("R", p, c, new Vector2(1, 0), Vector2.one, new Vector2(1, .5f), new Vector2(-inset, 0), new Vector2(th, -2 * inset));
    }

    static void Stud(Transform p, Vector2 corner, Color c)
    {
        float ox = corner.x < .5f ? 8f : -8f, oy = corner.y < .5f ? 8f : -8f;
        var s = Box("Stud", p, c, corner, corner, C, new Vector2(ox, oy), new Vector2(14, 14));
        s.localEulerAngles = new Vector3(0, 0, 45f);
    }

    // ---------- Panel ----------
    /// <summary>Khung panel: viền sắt, 4 đinh tán góc. Trả về root; vùng đặt nội dung ở content (đã chừa lề 28px).</summary>
    public static RectTransform Panel(Transform parent, string name, Vector2 pos, Vector2 size, PanelStyle style, out RectTransform content)
    {
        bool paper = style == PanelStyle.Parchment;
        var frame = CBox(name, parent, paper ? GameTheme.IronHi : GameTheme.Iron, pos, size, true);
        var sh = frame.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .55f); sh.effectDistance = new Vector2(0, -10);
        var fill = CBox("Fill", frame, paper ? GameTheme.Parchment : GameTheme.Night.WithAlpha(.97f), Vector2.zero, Vector2.zero);
        Stretch(fill, 6, 6, 6, 6);
        Outline4(fill, 8, 2, paper ? GameTheme.ParchmentDark : GameTheme.Ember.WithAlpha(.35f));
        Stud(frame, new Vector2(0, 1), GameTheme.EmberDeep); Stud(frame, new Vector2(1, 1), GameTheme.EmberDeep);
        Stud(frame, new Vector2(0, 0), GameTheme.EmberDeep); Stud(frame, new Vector2(1, 0), GameTheme.EmberDeep);
        content = CBox("Content", fill, Color.clear, Vector2.zero, Vector2.zero);
        Stretch(content, 28, 28, 28, 28);
        return frame;
    }

    // ---------- Nút ----------
    public static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Action onClick,
        ButtonStyle style = ButtonStyle.Primary, int fontSize = 34, float introDelay = 0f)
    {
        Color frameC, fillC, hoverC, textC;
        switch (style)
        {
            case ButtonStyle.Secondary: frameC = GameTheme.IronHi; fillC = GameTheme.Iron; hoverC = GameTheme.IronHi; textC = GameTheme.Bone; break;
            case ButtonStyle.Danger: frameC = Color.Lerp(GameTheme.Blood, GameTheme.Ink, .45f); fillC = GameTheme.Blood; hoverC = Color.Lerp(GameTheme.Blood, Color.white, .18f); textC = GameTheme.Bone; break;
            default: frameC = GameTheme.EmberDeep; fillC = GameTheme.Ember; hoverC = GameTheme.EmberHi; textC = GameTheme.PaperInk; break;
        }
        var frame = CBox(name, parent, frameC, pos, size, true);
        var sh = frame.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .5f); sh.effectDistance = new Vector2(0, -6);
        var fill = CBox("Fill", frame, fillC, Vector2.zero, Vector2.zero); Stretch(fill, 4, 4, 4, 4);
        Box("Shine", fill, new Color(1, 1, 1, .14f), new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 3));
        var l = Label("Label", frame, label, fontSize, textC, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, true, false, FontStyle.Bold);
        Stretch(l.rectTransform, 14, 0, 14, 0);

        var btn = frame.gameObject.AddComponent<Button>();
        btn.targetGraphic = frame.GetComponent<Image>(); btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => { GameAudio.Play(Snd.UiClick); if (onClick != null) onClick(); });
        var tb = frame.gameObject.AddComponent<ThemedButton>();
        tb.Setup(fill.GetComponent<Image>(), fillC, hoverC, l, textC, btn);
        tb.Intro(introDelay);
        return btn;
    }

    // ---------- Trang trí ----------
    public static RectTransform Divider(Transform p, Vector2 pos, float width, Color? col = null)
    {
        var c = col ?? GameTheme.Ember.WithAlpha(.7f);
        var root = CBox("Divider", p, Color.clear, pos, new Vector2(width, 14));
        Box("LineL", root, c, new Vector2(0, .5f), new Vector2(.5f, .5f), C, Vector2.zero, new Vector2(-24, 2));
        Box("LineR", root, c, new Vector2(.5f, .5f), new Vector2(1, .5f), C, Vector2.zero, new Vector2(-24, 2));
        var d = CBox("Gem", root, c, Vector2.zero, new Vector2(10, 10)); d.localEulerAngles = new Vector3(0, 0, 45f);
        return root;
    }

    public static Text Title(Transform p, string text, Vector2 pos, int size = 64)
    {
        var t = Label("Title", p, text, size, GameTheme.EmberHi, TextAnchor.MiddleCenter, pos, new Vector2(1700, size * 1.6f), true, true, FontStyle.Bold);
        Divider(p, pos + new Vector2(0, -size * .95f), 560);
        return t;
    }

    // ---------- Thanh tiến độ ----------
    public static RectTransform ProgressBar(Transform p, string name, Vector2 pos, Vector2 size, Color fillColor, out Image fill)
    {
        var frame = CBox(name, p, GameTheme.IronHi, pos, size);
        var back = CBox("Back", frame, GameTheme.Ink, Vector2.zero, Vector2.zero); Stretch(back, 3, 3, 3, 3);
        var f = CBox("Fill", back, fillColor, Vector2.zero, Vector2.zero); Stretch(f, 0, 0, 0, 0);
        fill = f.GetComponent<Image>();
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 0f;
        return frame;
    }

    public static void SetBar(Image fill, float v) { if (fill != null) fill.fillAmount = Mathf.Clamp01(v); }

    // ---------- Thông báo nhỏ ----------
    public static void Toast(Transform root, string msg, float seconds = 1.8f)
    {
        var box = CBox("Toast", root, GameTheme.Ember, new Vector2(0, -470), new Vector2(760, 80));
        var inner = CBox("Inner", box, GameTheme.Night, Vector2.zero, Vector2.zero); Stretch(inner, 3, 3, 3, 3);
        var t = Label("T", inner, msg, 34, GameTheme.Bone, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
        Stretch(t.rectTransform, 16, 0, 16, 0);
        UnityEngine.Object.Destroy(box.gameObject, seconds);
    }
}

/// <summary>Hành vi nút: hiện dần khi bắt đầu, phóng to nhẹ khi rê chuột, nhún khi bấm, mờ khi bị khoá. Dùng unscaledTime.</summary>
public class ThemedButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    Image fill; Color normal, hover, textColor; Text label; Button btn;
    RectTransform rt; CanvasGroup cg; bool hot, down, ready;

    public void Setup(Image fill, Color normal, Color hover, Text label, Color textColor, Button btn)
    {
        this.fill = fill; this.normal = normal; this.hover = hover; this.label = label; this.textColor = textColor; this.btn = btn;
    }

    void Awake()
    {
        rt = (RectTransform)transform;
        cg = GetComponent<CanvasGroup>(); if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f; rt.localScale = Vector3.one * .9f;
    }

    public void Intro(float delay) { StartCoroutine(IntroRoutine(delay)); }

    IEnumerator IntroRoutine(float delay)
    {
        for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
        const float dur = .4f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / dur);
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(.9f, 1f, EaseOutBack(k));
            cg.alpha = Mathf.Clamp01(k * 2.5f);
            yield return null;
        }
        cg.alpha = 1f; rt.localScale = Vector3.one; ready = true;
    }

    static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f; float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    void Update()
    {
        if (!ready || fill == null) return;
        bool on = btn == null || btn.interactable;
        float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        float target = !on ? 1f : down ? .96f : hot ? 1.05f : 1f;
        rt.localScale = Vector3.Lerp(rt.localScale, Vector3.one * target, k);
        Color fc = !on ? Color.Lerp(normal, GameTheme.Ink, .6f) : (hot ? hover : normal);
        fill.color = Color.Lerp(fill.color, fc, k);
        if (label != null) label.color = on ? textColor : textColor.WithAlpha(.45f);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        bool on = btn == null || btn.interactable;
        if (ready && !hot && on) GameAudio.Play(Snd.UiHover);
        hot = true;
    }
    public void OnPointerExit(PointerEventData e) { hot = false; down = false; }
    public void OnPointerDown(PointerEventData e) { down = true; }
    public void OnPointerUp(PointerEventData e) { down = false; }
}
