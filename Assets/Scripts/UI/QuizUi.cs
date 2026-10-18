using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Bộ dựng UI nhỏ bằng code (nút, hộp thoại, chữ) dùng chung cho game Lý và Hóa.
/// Mọi thứ sinh ra lúc chạy nên KHÔNG cần sửa scene bằng tay; font lấy từ 1 TMP_Text có sẵn
/// trong scene để hiển thị đúng tiếng Việt.
/// </summary>
public static class QuizUi
{
    public static readonly Color Navy   = new Color32(11, 26, 107, 255);
    public static readonly Color Deep   = new Color32(6, 14, 60, 255);
    public static readonly Color Gold   = new Color32(245, 184, 38, 255);
    public static readonly Color Green  = new Color32(46, 173, 75, 255);
    public static readonly Color Red    = new Color32(217, 58, 58, 255);
    public static readonly Color Orange = new Color32(242, 140, 30, 255);
    public static readonly Color Dim    = new Color(0f, 0f, 0.05f, 0.78f);

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    public static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    public static Image Box(string name, Transform parent, Color color, bool raycast = false)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    public static TMP_Text Label(string name, Transform parent, TMP_Text fontRef, string text, float size,
                                 Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Bold)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (fontRef != null)
        {
            t.font = fontRef.font;
            if (fontRef.fontSharedMaterial != null) t.fontSharedMaterial = fontRef.fontSharedMaterial;
        }
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Nút bấm có viền vàng, tự đổi màu khi rê chuột / nhấn / bị khoá.</summary>
    public static Button MakeButton(string name, Transform parent, TMP_Text fontRef, string label, Color bg,
                                    Color fg, float fontSize, UnityAction onClick)
    {
        var img = Box(name, parent, bg, true);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.75f);
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        var outline = img.gameObject.AddComponent<Outline>();
        outline.effectColor = Gold;
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = false;

        var txt = Label("Label", img.transform, fontRef, label, fontSize, fg);
        Stretch(txt.rectTransform);
        txt.rectTransform.offsetMin = new Vector2(8f, 4f);
        txt.rectTransform.offsetMax = new Vector2(-8f, -4f);
        txt.enableAutoSizing = true;
        txt.fontSizeMax = fontSize;
        txt.fontSizeMin = Mathf.Max(10f, fontSize * 0.45f);

        if (onClick != null) btn.onClick.AddListener(onClick);
        return btn;
    }

    public static void SetButtonLabel(Button b, string text)
    {
        var t = b != null ? b.GetComponentInChildren<TMP_Text>(true) : null;
        if (t != null) t.text = text;
    }

    /// <summary>Lớp phủ tối toàn màn hình (chặn bấm xuyên) – luôn nằm trên cùng canvas.</summary>
    public static RectTransform Overlay(string name, Transform canvas, Color dim)
    {
        var img = Box(name, canvas, dim, true);
        Stretch(img.rectTransform);
        img.transform.SetAsLastSibling();
        return img.rectTransform;
    }

    /// <summary>Thẻ giữa màn hình: viền vàng + nền xanh đậm. Trả về RectTransform của nền thẻ.</summary>
    public static RectTransform Card(Transform overlay, Vector2 size, Color border)
    {
        var b = Box("CardBorder", overlay, border);
        Anchor(b.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size + new Vector2(10f, 10f));
        var c = Box("Card", b.transform, Deep, true);
        Stretch(c.rectTransform);
        c.rectTransform.offsetMin = new Vector2(5f, 5f);
        c.rectTransform.offsetMax = new Vector2(-5f, -5f);
        return c.rectTransform;
    }

    public static Canvas RootCanvas(Component c)
    {
        var cv = c != null ? c.GetComponentInParent<Canvas>() : null;
        if (cv == null) cv = Object.FindFirstObjectByType<Canvas>();
        return cv != null ? cv.rootCanvas : null;
    }
}
