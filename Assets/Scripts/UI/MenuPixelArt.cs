using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Nạp pixel art của màn hình chính từ Assets/Resources/MenuPixel/*.bytes (thực chất là file PNG).
/// Dùng .bytes thay vì .png để Unity KHÔNG nén/làm mờ ảnh; texture luôn ở FilterMode.Point.
/// Quy ước lưới: 1 điểm ảnh pixel art = 4 điểm ảnh canvas (480x270 -> 1920x1080).
/// Vẽ lại / sửa art: chạy Tools/MenuPixelArt/gen_all.py (xem README.txt trong thư mục đó).
/// </summary>
public static class MenuPixelArt
{
    public const float PX = 4f;
    const string Dir = "MenuPixel/";

    static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Sprite> sprCache = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { texCache.Clear(); sprCache.Clear(); }

    public static Texture2D Tex(string name, bool repeat = false)
    {
        if (texCache.TryGetValue(name, out var cached) && cached != null) return cached;
        var ta = Resources.Load<TextAsset>(Dir + name);
        if (ta == null)
        {
            Debug.LogError("[MenuPixelArt] Thiếu Assets/Resources/" + Dir + name + ".bytes (chạy Tools/MenuPixelArt/gen_all.py --unity ...).");
            return null;
        }
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave
        };
        t.LoadImage(ta.bytes, false);
        t.filterMode = FilterMode.Point;
        t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        texCache[name] = t;
        return t;
    }

    public static Sprite Get(string name)
    {
        if (sprCache.TryGetValue(name, out var cached) && cached != null) return cached;
        var t = Tex(name);
        if (t == null) return null;
        var s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        s.name = name;
        s.hideFlags = HideFlags.HideAndDontSave;
        sprCache[name] = s;
        return s;
    }
}

/// <summary>
/// Nút pixel art: đổi sprite theo trạng thái (thường / rê chuột hoặc chọn bằng phím / nhấn),
/// nhãn lún xuống 1 điểm ảnh khi nhấn, hiện dần khi vào màn hình. Dùng unscaledTime.
/// </summary>
public class PixelMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    Image face; Sprite normal, hover, down;
    RectTransform label; Vector2 labelHome;
    bool hot, pressed, selected;

    public void Setup(Image face, Sprite normal, Sprite hover, Sprite down, RectTransform label, float introDelay)
    {
        this.face = face; this.normal = normal; this.hover = hover; this.down = down; this.label = label;
        labelHome = label != null ? label.anchoredPosition : Vector2.zero;
        var cg = gameObject.GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        StartCoroutine(Intro(cg, introDelay));
        Refresh();
    }

    IEnumerator Intro(CanvasGroup cg, float delay)
    {
        var rt = (RectTransform)transform;
        var home = rt.anchoredPosition;
        for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
        const float dur = .4f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - (1f - k) * (1f - k);
            cg.alpha = Mathf.Clamp01(k * 2.2f);
            // trượt lên theo bước 1 điểm ảnh để giữ lưới pixel
            rt.anchoredPosition = home + new Vector2(0f, -Mathf.Round((1f - e) * 5f) * MenuPixelArt.PX);
            yield return null;
        }
        cg.alpha = 1f; rt.anchoredPosition = home;
    }

    void Refresh()
    {
        if (face == null) return;
        face.sprite = pressed ? down : (hot || selected) ? hover : normal;
        if (label != null) label.anchoredPosition = labelHome + (pressed ? new Vector2(0f, -MenuPixelArt.PX) : Vector2.zero);
    }

    public void OnPointerEnter(PointerEventData e) { if (!hot && !selected) GameAudio.Play(Snd.UiHover); hot = true; Refresh(); }
    public void OnPointerExit(PointerEventData e) { hot = false; pressed = false; Refresh(); }
    public void OnPointerDown(PointerEventData e) { pressed = true; Refresh(); }
    public void OnPointerUp(PointerEventData e) { pressed = false; Refresh(); }
    public void OnSelect(BaseEventData e) { selected = true; Refresh(); }
    public void OnDeselect(BaseEventData e) { selected = false; Refresh(); }
    void OnDisable() { hot = pressed = false; }
}
