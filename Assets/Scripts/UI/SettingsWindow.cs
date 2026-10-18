using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Cửa sổ CÀI ĐẶT dark fantasy (dựng bằng UIKit), mở từ MainMenu. Hai cột:
///  - Trái: ÂM THANH (bật/tắt, âm lượng tổng, nhạc nền, hiệu ứng) + HIỂN THỊ (độ sáng, font chữ, cỡ chữ).
///  - Phải: CẤU HÌNH GAME (chất lượng đồ họa, toàn màn hình, giới hạn FPS, VSync) + khung xem trước font.
/// Mọi thay đổi áp dụng ngay và lưu qua GameSettings / GameFont. Nút MẶC ĐỊNH khôi phục tất cả. Esc hoặc ĐÓNG để thoát.
/// Dựng mới mỗi lần mở, huỷ khi đóng (nút có coroutine xuất hiện dần nên không dùng SetActive).
/// </summary>
public class SettingsWindow : MonoBehaviour
{
    static readonly Color Abyss = new Color(.03f, .02f, .05f);

    Action onClose;
    RectTransform content;

    Slider master, music, sfx, bright;
    InputField nickIn;
    Text masterVal, musicVal, sfxVal, brightVal;
    Text soundBtnLabel, fullBtnLabel, vsyncBtnLabel;
    Image soundDot, fullDot, vsyncDot;
    Text fontLabel, scaleLabel, qualityLabel, fpsLabel, previewText;

    const int PreviewBase = 30;

    public static SettingsWindow Open(Transform parent, Action onClose)
    {
        var dim = UIKit.Box("SettingsDim", parent, new Color(0, 0, 0, .7f), Vector2.zero, Vector2.one, UIKit.C, Vector2.zero, Vector2.zero, true);
        UIKit.Stretch(dim, 0, 0, 0, 0);
        var w = dim.gameObject.AddComponent<SettingsWindow>();
        w.onClose = onClose;
        w.Build(dim);
        return w;
    }

    public void Close()
    {
        if (nickIn != null) GameSettings.Nickname = nickIn.text;
        PlayerPrefs.Save();
        var cb = onClose; onClose = null;
        Destroy(gameObject);
        if (cb != null) cb();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    // ================================================================== dựng giao diện
    void Build(Transform root)
    {
        UIKit.Panel(root, "SettingsPanel", Vector2.zero, new Vector2(1560, 860), PanelStyle.Dark, out content);
        UIKit.Label("T", content, "CÀI ĐẶT", 52, GameTheme.GoldHi, TextAnchor.MiddleCenter, new Vector2(0, 350), new Vector2(660, 70), true, true, FontStyle.Bold);
        UIKit.Divider(content, new Vector2(0, 308), 560);

        const float L = -380f, R = 380f;

        // ---- Cột trái: ÂM THANH ----
        Header("ÂM THANH", L, 262);
        Row("Âm thanh", L, 200);
        Toggle(L, 200, ToggleSound, out soundBtnLabel, out soundDot);
        Row("Âm lượng tổng", L, 138);
        master = MakeSlider(L + 70, 138, 300, 0f, 1f, GameSettings.Master, GameTheme.Ember, v => { GameSettings.Master = v; RefreshValues(); });
        masterVal = Value(L, 138);
        Row("Nhạc nền", L, 76);
        music = MakeSlider(L + 70, 76, 300, 0f, 1f, GameSettings.Music, GameTheme.Ember, v => { GameSettings.Music = v; RefreshValues(); });
        musicVal = Value(L, 76);
        Row("Hiệu ứng", L, 14);
        sfx = MakeSlider(L + 70, 14, 300, 0f, 1f, GameSettings.Sfx, GameTheme.Ember, v => { GameSettings.Sfx = v; RefreshValues(); },
            () => GameAudio.Play(Snd.UiClick));   // thả chuột: nghe thử mức hiệu ứng
        sfxVal = Value(L, 14);

        // ---- Cột trái: HIỂN THỊ ----
        Header("HIỂN THỊ", L, -48);
        Row("Độ sáng", L, -120);
        bright = MakeSlider(L + 70, -120, 300, GameSettings.BrightMin, GameSettings.BrightMax, GameSettings.Brightness, GameTheme.GoldHi,
            v => { GameSettings.Brightness = v; RefreshValues(); });
        brightVal = Value(L, -120);
        Row("Font chữ", L, -182);
        Selector(L, -182, d => StepFont(d), out fontLabel);
        Row("Cỡ chữ", L, -244);
        Selector(L, -244, d => StepScale(d), out scaleLabel);

        // ---- Cột phải: CẤU HÌNH GAME ----
        Header("CẤU HÌNH GAME", R, 262);
        Row("Chất lượng", R, 200);
        Selector(R, 200, d => StepQuality(d), out qualityLabel);
        Row("Toàn màn hình", R, 138);
        Toggle(R, 138, ToggleFullscreen, out fullBtnLabel, out fullDot);
        Row("Giới hạn FPS", R, 76);
        Selector(R, 76, d => StepFps(d), out fpsLabel);
        Row("Đồng bộ dọc", R, 14);
        Toggle(R, 14, ToggleVsync, out vsyncBtnLabel, out vsyncDot);

        // ---- Cột phải: TÀI KHOẢN (bí danh) ----
        Header("TÀI KHOẢN", R, -48);
        Row("Bí danh", R, -110);
        nickIn = UIKit.MakeInput(content, "In_Nick", "Bí danh của bạn", new Vector2(R + 130, -110), 14, 360);
        nickIn.text = GameSettings.Nickname;
        nickIn.onEndEdit.AddListener(s => { GameSettings.Nickname = s; PlayerPrefs.Save(); });

        // ---- Cột phải: xem trước font (đã dời xuống và thu nhỏ) ----
        Header("XEM TRƯỚC FONT", R, -170);
        var frame = UIKit.CBox("PreviewFrame", content, Abyss, new Vector2(R, -262), new Vector2(640, 100));
        var rim = UIKit.CBox("Rim", frame, GameTheme.Bronze, Vector2.zero, Vector2.zero); UIKit.Stretch(rim, 2, 2, 2, 2);
        var inner = UIKit.CBox("In", frame, GameTheme.Night, Vector2.zero, Vector2.zero); UIKit.Stretch(inner, 4, 4, 4, 4);
        previewText = UIKit.Label("Txt", inner, "Mỗi điều bạn biết là một ngọn đèn giữa màn sương.\nAa Bb Cc – Lý · Hóa · Sinh – 0123456789",
            PreviewBase, GameTheme.Bone, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
        UIKit.Stretch(previewText.rectTransform, 20, 10, 20, 10);

        // ---- Nút dưới ----
        UIKit.MakeButton(content, "Btn_Default", "MẶC ĐỊNH", new Vector2(-200, -350), new Vector2(320, 72), ResetDefaults, ButtonStyle.Stone, 32, .1f);
        UIKit.MakeButton(content, "Btn_Close", "ĐÓNG", new Vector2(200, -350), new Vector2(320, 72), Close, ButtonStyle.Bronze, 34, .16f);

        RefreshValues();
    }

    // ---------- thành phần dựng sẵn ----------
    void Header(string text, float ox, float y)
    {
        UIKit.Label("Hd", content, text, 30, GameTheme.GoldHi, TextAnchor.MiddleCenter, new Vector2(ox, y), new Vector2(600, 40), true, true, FontStyle.Bold);
        UIKit.Divider(content, new Vector2(ox, y - 30), 600, GameTheme.Gold.WithAlpha(.5f));
    }

    void Row(string label, float ox, float y)
    {
        UIKit.Label("Lb", content, label, 28, GameTheme.Bone, TextAnchor.MiddleLeft, new Vector2(ox - 230, y), new Vector2(250, 44));
    }

    Text Value(float ox, float y)
    {
        return UIKit.Label("Val", content, "", 26, GameTheme.Rune, TextAnchor.MiddleLeft, new Vector2(ox + 285, y), new Vector2(110, 44));
    }

    /// <summary>Nút bật/tắt: chấm ngọc sáng (xanh) khi bật, tối khi tắt + nút chữ BẬT/TẮT.</summary>
    void Toggle(float ox, float y, Action onClick, out Text label, out Image dot)
    {
        var d = UIKit.CBox("Dot", content, GameTheme.Iron, new Vector2(ox - 40, y), new Vector2(16, 16));
        d.localEulerAngles = new Vector3(0, 0, 45f);
        dot = d.GetComponent<Image>();
        var b = UIKit.MakeButton(content, "Btn_Toggle", "", new Vector2(ox + 100, y), new Vector2(220, 50), onClick, ButtonStyle.Stone, 28, 0f);
        label = b.GetComponentInChildren<Text>();
    }

    /// <summary>Bộ chọn ◀ giá trị ▶ (vòng tròn qua các lựa chọn).</summary>
    void Selector(float ox, float y, Action<int> step, out Text valueLabel)
    {
        UIKit.MakeButton(content, "Btn_Prev", "<", new Vector2(ox - 20, y), new Vector2(60, 50), () => step(-1), ButtonStyle.Stone, 30, 0f);
        valueLabel = UIKit.Label("Sel", content, "", 24, GameTheme.GoldHi, TextAnchor.MiddleCenter, new Vector2(ox + 150, y), new Vector2(250, 44), true, false, FontStyle.Bold);
        UIKit.MakeButton(content, "Btn_Next", ">", new Vector2(ox + 320, y), new Vector2(60, 50), () => step(1), ButtonStyle.Stone, 30, 0f);
    }

    /// <summary>Thanh trượt khắc đồng: rãnh đen viền đồng, phần đầy màu lửa, núm ngọc hình thoi.</summary>
    Slider MakeSlider(float x, float y, float width, float min, float max, float val, Color fillCol, Action<float> onChange, Action onRelease = null)
    {
        var root = UIKit.CBox("Slider", content, Color.clear, new Vector2(x, y), new Vector2(width, 44), true);
        var frame = UIKit.CBox("Frame", root, Abyss, Vector2.zero, new Vector2(width, 20));
        var rim = UIKit.CBox("Rim", frame, Color.Lerp(GameTheme.Bronze, GameTheme.Ink, .1f), Vector2.zero, Vector2.zero); UIKit.Stretch(rim, 2, 2, 2, 2);
        var back = UIKit.CBox("Back", frame, GameTheme.Ink, Vector2.zero, Vector2.zero); UIKit.Stretch(back, 4, 4, 4, 4);
        var fillArea = UIKit.CBox("FillArea", back, Color.clear, Vector2.zero, Vector2.zero); UIKit.Stretch(fillArea, 0, 0, 0, 0);
        var fill = UIKit.CBox("Fill", fillArea, fillCol, Vector2.zero, Vector2.zero);

        var area = UIKit.CBox("HandleArea", root, Color.clear, Vector2.zero, Vector2.zero); UIKit.Stretch(area, 14, 0, 14, 0);
        var handle = UIKit.CBox("Handle", area, Color.clear, Vector2.zero, new Vector2(28, 0));
        Gem(handle, 28f, Abyss);
        Gem(handle, 22f, GameTheme.Gold);
        Gem(handle, 14f, GameTheme.GoldHi);
        Gem(handle, 6f, Abyss);

        var s = root.gameObject.AddComponent<Slider>();
        s.targetGraphic = root.GetComponent<Image>();
        s.transition = Selectable.Transition.None;
        s.navigation = new Navigation { mode = Navigation.Mode.None };
        s.fillRect = fill;
        s.handleRect = handle;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = min; s.maxValue = max; s.wholeNumbers = false;
        s.SetValueWithoutNotify(val);
        s.onValueChanged.AddListener(v => onChange(v));
        if (onRelease != null) root.gameObject.AddComponent<PointerUpHook>().up = onRelease;
        return s;
    }

    static void Gem(Transform p, float size, Color c)
    {
        var g = UIKit.CBox("Gem", p, c, Vector2.zero, new Vector2(size, size));
        g.localEulerAngles = new Vector3(0, 0, 45f);
    }

    // ================================================================== hành vi
    void RefreshValues()
    {
        if (masterVal != null) masterVal.text = Mathf.RoundToInt(GameSettings.Master * 100f) + "%";
        if (musicVal != null) musicVal.text = Mathf.RoundToInt(GameSettings.Music * 100f) + "%";
        if (sfxVal != null) sfxVal.text = Mathf.RoundToInt(GameSettings.Sfx * 100f) + "%";
        if (brightVal != null) brightVal.text = Mathf.RoundToInt(GameSettings.Brightness * 100f) + "%";

        SetToggle(soundBtnLabel, soundDot, !GameSettings.Muted);
        SetToggle(fullBtnLabel, fullDot, GameSettings.Fullscreen);
        SetToggle(vsyncBtnLabel, vsyncDot, GameSettings.Vsync);

        if (fontLabel != null) fontLabel.text = GameFont.Names[GameFont.Choice];
        if (scaleLabel != null) scaleLabel.text = GameFont.ScaleNames[GameFont.ScaleIndex];
        if (qualityLabel != null) qualityLabel.text = GameSettings.QualityName(GameSettings.QualityLevel);
        if (fpsLabel != null) fpsLabel.text = GameSettings.FpsNames[GameSettings.FpsIdx];
        if (previewText != null) previewText.fontSize = Mathf.RoundToInt(PreviewBase * GameFont.Scale);
    }

    static void SetToggle(Text label, Image dot, bool on)
    {
        if (label != null) label.text = on ? "BẬT" : "TẮT";
        if (dot != null) dot.color = on ? GameTheme.Moss : GameTheme.Iron;
    }

    static int Wrap(int v, int n) { return ((v % n) + n) % n; }

    void ToggleSound() { GameSettings.Muted = !GameSettings.Muted; RefreshValues(); }
    void ToggleFullscreen() { GameSettings.Fullscreen = !GameSettings.Fullscreen; RefreshValues(); }
    void ToggleVsync() { GameSettings.Vsync = !GameSettings.Vsync; RefreshValues(); }

    void StepQuality(int d)
    {
        int n = QualitySettings.names.Length;
        GameSettings.QualityLevel = Wrap(GameSettings.QualityLevel + d, n);
        RefreshValues();
    }

    void StepFps(int d)
    {
        GameSettings.FpsIdx = Wrap(GameSettings.FpsIdx + d, GameSettings.FpsOptions.Length);
        RefreshValues();
    }

    void StepFont(int d)
    {
        var old = GameFont.Body;
        GameFont.Choice = Wrap(GameFont.Choice + d, GameFont.Names.Length);
        ReplaceFont(old, GameFont.Body);
        RefreshValues();
    }

    void StepScale(int d)
    {
        float oldS = GameFont.Scale;
        GameFont.ScaleIndex = Wrap(GameFont.ScaleIndex + d, GameFont.ScaleNames.Length);
        Rescale(GameFont.Scale / oldS);
        RefreshValues();
    }

    void ResetDefaults()
    {
        var oldBody = GameFont.Body;
        float oldScale = GameFont.Scale;
        GameSettings.ResetAll();
        ReplaceFont(oldBody, GameFont.Body);
        Rescale(GameFont.Scale / oldScale);
        master.SetValueWithoutNotify(GameSettings.Master);
        music.SetValueWithoutNotify(GameSettings.Music);
        sfx.SetValueWithoutNotify(GameSettings.Sfx);
        bright.SetValueWithoutNotify(GameSettings.Brightness);
        RefreshValues();
    }

    /// <summary>Đổi font cho mọi chữ đang hiện dùng font thân cũ (tiêu đề dùng font Display nên không bị đổi).</summary>
    static void ReplaceFont(Font old, Font nw)
    {
        if (old == nw) return;
        foreach (var t in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.font == old) t.font = nw;
    }

    /// <summary>Đổi cỡ chữ cho phần giao diện phía sau; cửa sổ Cài đặt giữ nguyên cỡ để không vỡ bố cục (khung xem trước tự đổi).</summary>
    void Rescale(float ratio)
    {
        if (Mathf.Approximately(ratio, 1f)) return;
        foreach (var t in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!t.transform.IsChildOf(transform)) t.fontSize = Mathf.Max(8, Mathf.RoundToInt(t.fontSize * ratio));
    }
}

/// <summary>Gọi hành động khi thả chuột/ngón tay khỏi thanh trượt (nghe thử âm lượng hiệu ứng).</summary>
public class PointerUpHook : MonoBehaviour, IPointerUpHandler
{
    public Action up;
    public void OnPointerUp(PointerEventData e) { if (up != null) up(); }
}
