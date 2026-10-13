using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public enum PanelStyle { Parchment, Dark }
public enum ButtonStyle { Primary, Secondary, Danger, Bronze, Stone }

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

    static readonly Color Abyss = new Color(.03f, .02f, .05f);   // đen tím sâu nhất, dùng cho viền ngoài các khung

    /// <summary>Ngoặc góc hình chữ L đặt sát một góc khung (corner là (0|1, 0|1)).</summary>
    static void Bracket(Transform p, Vector2 corner, Color c, float arm, float th, float inset)
    {
        float sx = corner.x < .5f ? 1f : -1f, sy = corner.y < .5f ? 1f : -1f;
        Box("BrH", p, c, corner, corner, corner, new Vector2(sx * inset, sy * inset), new Vector2(arm, th));
        Box("BrV", p, c, corner, corner, corner, new Vector2(sx * inset, sy * inset), new Vector2(th, arm));
    }

    /// <summary>Viên ngọc hình thoi (ô vuông xoay 45 độ) đặt tại điểm neo.</summary>
    static RectTransform Gem(Transform p, Vector2 anchor, Vector2 pos, float size, Color c)
    {
        var g = Box("Gem", p, c, anchor, anchor, C, pos, new Vector2(size, size));
        g.localEulerAngles = new Vector3(0, 0, 45f);
        return g;
    }

    // ---------- Panel ----------
    /// <summary>
    /// Khung panel dark fantasy: viền đen tím, vành đồng cổ, nền sương tím tối (Dark) hoặc giấy da ố mép (Parchment),
    /// ngoặc vàng và ngọc máu ở bốn góc, đá chốt giữa cạnh trên, quầng than hồng dưới đáy. Trả về root; vùng đặt nội dung ở content (đã chừa lề 28px).
    /// </summary>
    public static RectTransform Panel(Transform parent, string name, Vector2 pos, Vector2 size, PanelStyle style, out RectTransform content)
    {
        bool paper = style == PanelStyle.Parchment;
        Color rim = Color.Lerp(GameTheme.Bronze, GameTheme.Ink, paper ? .25f : .45f);
        Color metal = paper ? GameTheme.Gold : GameTheme.Gold.WithAlpha(.9f);

        var frame = CBox(name, parent, Abyss, pos, size, true);
        var sh = frame.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .65f); sh.effectDistance = new Vector2(0, -12);
        var edge = CBox("Rim", frame, rim, Vector2.zero, Vector2.zero); Stretch(edge, 2, 2, 2, 2);

        var fill = CBox("Fill", frame, Color.white, Vector2.zero, Vector2.zero); Stretch(fill, 6, 6, 6, 6);
        var fi = fill.GetComponent<Image>();
        if (paper) fi.color = GameTheme.Parchment;
        else { fi.sprite = ThemeGfx.Gradient; fi.color = new Color(.92f, .9f, 1f, 1f); }

        Outline4(fill, 8, 2, paper ? GameTheme.Bronze.WithAlpha(.55f) : GameTheme.Gold.WithAlpha(.28f));
        Outline4(fill, 14, 1, paper ? GameTheme.ParchmentDark : GameTheme.Ember.WithAlpha(.18f));
        if (!paper && size.x > 300f && size.y > 200f)
        {
            var glow = CBox("EmberGlow", fill, GameTheme.EmberDeep.WithAlpha(.14f), new Vector2(0, -size.y / 2f + 76f), new Vector2(size.x - 80f, 140f));
            glow.GetComponent<Image>().sprite = ThemeGfx.Glow;
        }
        var vg = CBox("Vignette", fill, Color.white, Vector2.zero, Vector2.zero); Stretch(vg, 0, 0, 0, 0);
        var vi = vg.GetComponent<Image>(); vi.sprite = ThemeGfx.Vignette; vi.color = new Color(1, 1, 1, paper ? .4f : .9f);

        content = CBox("Content", fill, Color.clear, Vector2.zero, Vector2.zero);
        Stretch(content, 28, 28, 28, 28);

        // ngoặc vàng + ngọc máu ở bốn góc
        float arm = Mathf.Min(40f, Mathf.Min(size.x, size.y) * .2f);
        Color gem = paper ? GameTheme.EmberDeep : GameTheme.Blood;
        foreach (var cn in new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0) })
        {
            Bracket(frame, cn, metal, arm, 3f, 5f);
            Gem(frame, cn, new Vector2(cn.x < .5f ? 17f : -17f, cn.y < .5f ? 17f : -17f), 11f, gem);
        }
        // đá chốt giữa cạnh trên và đáy
        if (size.x >= 420f)
        {
            Box("KeyL", frame, metal, new Vector2(.5f, 1), new Vector2(.5f, 1), C, new Vector2(-46, 0), new Vector2(58, 3));
            Box("KeyR", frame, metal, new Vector2(.5f, 1), new Vector2(.5f, 1), C, new Vector2(46, 0), new Vector2(58, 3));
            Gem(frame, new Vector2(.5f, 1), Vector2.zero, 22f, metal);
            Gem(frame, new Vector2(.5f, 1), Vector2.zero, 9f, gem);
            Gem(frame, new Vector2(.5f, 0), Vector2.zero, 14f, metal);
        }
        return frame;
    }

    // ---------- Nút ----------
    /// <summary>
    /// Nút dark fantasy: viền sắt đen, vành kim loại, mặt đá/than tối vát sáng trên và tối dưới, đường khắc chìm,
    /// hai cánh nhọn hai bên, quầng sáng bùng lên khi rê chuột. Chữ sáng, có viền đen cho dễ đọc.
    /// </summary>
    public static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Action onClick,
        ButtonStyle style = ButtonStyle.Primary, int fontSize = 34, float introDelay = 0f)
    {
        Color frameC, fillC, hoverC, textC, glowC;
        switch (style)
        {
            case ButtonStyle.Secondary: frameC = GameTheme.IronHi; fillC = Color.Lerp(GameTheme.Iron, GameTheme.Ink, .4f); hoverC = GameTheme.Iron; textC = GameTheme.Bone; glowC = GameTheme.Fog; break;
            case ButtonStyle.Danger: frameC = GameTheme.Blood; fillC = Color.Lerp(GameTheme.Blood, GameTheme.Ink, .6f); hoverC = Color.Lerp(GameTheme.Blood, GameTheme.Ink, .25f); textC = new Color(1f, .86f, .84f); glowC = GameTheme.Blood; break;
            case ButtonStyle.Bronze: frameC = GameTheme.Gold; fillC = Color.Lerp(GameTheme.Bronze, GameTheme.Ink, .5f); hoverC = Color.Lerp(GameTheme.Bronze, GameTheme.Ink, .15f); textC = GameTheme.GoldHi; glowC = GameTheme.Gold; break;
            case ButtonStyle.Stone: frameC = GameTheme.Bronze; fillC = GameTheme.Slate; hoverC = GameTheme.SlateHi; textC = GameTheme.Rune; glowC = GameTheme.Rune; break;
            default: frameC = GameTheme.Ember; fillC = Color.Lerp(GameTheme.EmberDeep, GameTheme.Ink, .6f); hoverC = Color.Lerp(GameTheme.EmberDeep, GameTheme.Ink, .25f); textC = GameTheme.EmberHi; glowC = GameTheme.Ember; break;
        }
        var frame = CBox(name, parent, Abyss, pos, size, true);
        var sh = frame.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .6f); sh.effectDistance = new Vector2(0, -6);
        var glow = CBox("Glow", frame, glowC.WithAlpha(0f), Vector2.zero, new Vector2(size.x * 1.3f, size.y * 2.1f));
        var glowImg = glow.GetComponent<Image>(); glowImg.sprite = ThemeGfx.Glow; glowImg.raycastTarget = false;
        var border = CBox("Border", frame, frameC, Vector2.zero, Vector2.zero); Stretch(border, 2, 2, 2, 2);
        var fill = CBox("Fill", frame, fillC, Vector2.zero, Vector2.zero); Stretch(fill, 5, 5, 5, 5);
        Box("Shade", fill, new Color(0, 0, 0, .28f), Vector2.zero, new Vector2(1, .5f), C, Vector2.zero, Vector2.zero);
        Box("Shine", fill, new Color(1, 1, 1, .12f), new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 2));
        Outline4(fill, 4, 1, frameC.WithAlpha(.45f));
        foreach (var side in new[] { new Vector2(0, .5f), new Vector2(1, .5f) })       // cánh nhọn hai bên
        {
            Gem(frame, side, Vector2.zero, 15f, frameC);
            Gem(frame, side, Vector2.zero, 6f, Abyss);
        }
        var l = Label("Label", frame, label, fontSize, textC, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, true, true, FontStyle.Bold);
        Stretch(l.rectTransform, 18, 0, 18, 0);

        var btn = frame.gameObject.AddComponent<Button>();
        btn.targetGraphic = frame.GetComponent<Image>(); btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => { GameAudio.Play(Snd.UiClick); if (onClick != null) onClick(); });
        var tb = frame.gameObject.AddComponent<ThemedButton>();
        tb.Setup(fill.GetComponent<Image>(), fillC, hoverC, l, textC, btn);
        tb.glow = glowImg; tb.glowColor = glowC;
        tb.Intro(introDelay);
        return btn;
    }

    // ---------- Trang trí ----------
    public static RectTransform Divider(Transform p, Vector2 pos, float width, Color? col = null)
    {
        var c = col ?? GameTheme.Gold.WithAlpha(.75f);
        var root = CBox("Divider", p, Color.clear, pos, new Vector2(width, 14));
        Box("LineL", root, c, new Vector2(0, .5f), new Vector2(.5f, .5f), C, Vector2.zero, new Vector2(-34, 2));
        Box("LineR", root, c, new Vector2(.5f, .5f), new Vector2(1, .5f), C, Vector2.zero, new Vector2(-34, 2));
        Gem(root, C, Vector2.zero, 12f, c);
        Gem(root, C, new Vector2(-18, 0), 5f, c); Gem(root, C, new Vector2(18, 0), 5f, c);
        Gem(root, new Vector2(0, .5f), Vector2.zero, 6f, c); Gem(root, new Vector2(1, .5f), Vector2.zero, 6f, c);
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
        var frame = CBox(name, p, Abyss, pos, size);
        var rim = CBox("Rim", frame, Color.Lerp(GameTheme.Bronze, GameTheme.Ink, .1f), Vector2.zero, Vector2.zero); Stretch(rim, 2, 2, 2, 2);
        var back = CBox("Back", frame, GameTheme.Ink, Vector2.zero, Vector2.zero); Stretch(back, 4, 4, 4, 4);
        var f = CBox("Fill", back, fillColor, Vector2.zero, Vector2.zero); Stretch(f, 0, 0, 0, 0);
        fill = f.GetComponent<Image>();
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 0f;
        Gem(frame, new Vector2(0, .5f), Vector2.zero, 9f, GameTheme.Gold); Gem(frame, new Vector2(1, .5f), Vector2.zero, 9f, GameTheme.Gold);
        return frame;
    }

    public static void SetBar(Image fill, float v) { if (fill != null) fill.fillAmount = Mathf.Clamp01(v); }

    // ---------- Thông báo nhỏ ----------
    public static void Toast(Transform root, string msg, float seconds = 1.8f)
    {
        var box = CBox("Toast", root, Abyss, new Vector2(0, -470), new Vector2(760, 80));
        var border = CBox("Border", box, GameTheme.Gold.WithAlpha(.85f), Vector2.zero, Vector2.zero); Stretch(border, 2, 2, 2, 2);
        var inner = CBox("Inner", box, GameTheme.Night, Vector2.zero, Vector2.zero); Stretch(inner, 4, 4, 4, 4);
        Outline4(inner, 5, 1, GameTheme.Gold.WithAlpha(.3f));
        Gem(box, new Vector2(0, .5f), Vector2.zero, 14f, GameTheme.Gold); Gem(box, new Vector2(1, .5f), Vector2.zero, 14f, GameTheme.Gold);
        var t = Label("T", inner, msg, 34, GameTheme.Bone, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
        Stretch(t.rectTransform, 24, 0, 24, 0);
        UnityEngine.Object.Destroy(box.gameObject, seconds);
    }
}

/// <summary>Hành vi nút: hiện dần khi bắt đầu, phóng to nhẹ khi rê chuột, nhún khi bấm, mờ khi bị khoá. Dùng unscaledTime.</summary>
public class ThemedButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    Image fill; Color normal, hover, textColor; Text label; Button btn;
    public Image glow; public Color glowColor;       // quầng sáng sau nút: sáng lên khi rê chuột/bấm
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
        if (glow != null)
        {
            float ga = !on ? 0f : down ? .7f : hot ? .55f : .10f + .05f * Mathf.Sin(Time.unscaledTime * 2f + (GetInstanceID() & 15));
            var gc = glowColor; gc.a = Mathf.Lerp(glow.color.a, ga, k); glow.color = gc;
        }
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
