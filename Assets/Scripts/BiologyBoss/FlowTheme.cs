using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Giao diện dark fantasy dùng chung cho màn Chọn chương / Chọn bài (đồng bộ MainMenu + Chọn môn):
/// nền rừng trăng, tiêu đề vàng, thẻ khung đá khắc rune. Màu theo môn chỉ là "rune accent" (GameTheme.Subject).
/// </summary>
public class FlowTheme
{
    public SubjectId Id; public string Name; public Color Accent, Muted;

    public static FlowTheme Get(SubjectId s)
    {
        string n = s == SubjectId.Ly ? "Vật lí" : s == SubjectId.Hoa ? "Hóa học" : "Sinh học";
        return new FlowTheme { Id = s, Name = n, Accent = GameTheme.Subject(s), Muted = GameTheme.Fog };
    }
}

/// <summary>
/// Khung màn hình dùng chung: nền rừng trăng (cây lay, lá rơi, tàn lửa), tiêu đề + dòng trạng thái, nút quay lại, phím Esc.
/// Các manager chỉ việc gọi FlowScreen.Create rồi thêm thẻ vào Ui / Grid.
/// </summary>
public class FlowScreen : MonoBehaviour
{
    public RectTransform Ui { get; private set; }
    public bool Leaving { get; private set; }

    RectTransform artRoot, scene;
    MenuNatureFx nature;
    Vector2 lastRect;
    CanvasGroup header;
    Text titleText, status;
    string backScene;
    float t0;

    public static FlowScreen Create(Transform parent, FlowTheme th, string title, string backLabel, string backScene)
    {
        var go = new GameObject("FlowScreen"); go.transform.SetParent(parent, false);
        var s = go.AddComponent<FlowScreen>();
        s.Init(th, title, backLabel, backScene);
        return s;
    }

    void Init(FlowTheme th, string title, string backLabel, string backScene)
    {
        this.backScene = backScene; t0 = Time.unscaledTime;
        Time.timeScale = 1f;
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = GameTheme.Ink; }

        UIKit.EnsureEventSystem();
        SceneShell.Create(ShellMood.Hub, null, 1f, true, false);

        artRoot = UIKit.BuildCanvas(transform, "Flow Art", -90);
        scene = UIKit.CBox("Scene", artRoot, Color.clear, Vector2.zero, new Vector2(1920f, 1080f));
        nature = new MenuNatureFx();
        nature.BuildForest(scene, 200f, 24);

        Ui = UIKit.BuildCanvas(transform, "Flow UI", 10);
        var h = UIKit.CBox("Header", Ui, Color.clear, Vector2.zero, Vector2.zero);
        header = h.gameObject.AddComponent<CanvasGroup>();
        UIKit.Label("Kicker", h, "KHOA HỌC TỰ NHIÊN  •  " + th.Name.ToUpperInvariant(), 28, new Color(.67f, .75f, .9f, .92f),
            TextAnchor.MiddleCenter, new Vector2(0, 478), new Vector2(1200, 44), true, true);
        titleText = UIKit.Label("Title", h, title, 100, GameTheme.GoldHi, TextAnchor.MiddleCenter,
            new Vector2(0, 402), new Vector2(1400, 140), true, true, FontStyle.Bold);
        var shade = UIKit.CBox("SubShade", h, new Color(.02f, .04f, .10f, .65f), new Vector2(0, 296), new Vector2(1600, 130));
        shade.GetComponent<Image>().sprite = ThemeGfx.Glow;
        status = UIKit.Label("Status", h, "", 34, new Color(.86f, .91f, 1f, .98f), TextAnchor.MiddleCenter,
            new Vector2(0, 296), new Vector2(1500, 50), true, true, FontStyle.Italic);

        UIKit.MakeButton(Ui, "Btn_Back", backLabel, new Vector2(-690f, -462f), new Vector2(420f, 72f), Back, ButtonStyle.Stone, 30, .6f);
        MenuNatureFx.FitCover(artRoot, scene, ref lastRect);
    }

    public void SetStatus(string s) { if (status != null) status.text = s; }

    /// <summary>Chuyển scene có fade; trả về false nếu scene chưa có trong Build Settings.</summary>
    public bool Go(string sceneName)
    {
        if (Leaving) return false;
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            UIKit.Toast(Ui, "Không tìm thấy scene: " + sceneName);
            return false;
        }
        Leaving = true;
        SceneRouter.Go(sceneName);
        return true;
    }

    void Back()
    {
        if (Leaving) return;
        GameAudio.Play(Snd.UiBack);
        Go(backScene);
    }

    /// <summary>Vùng cuộn + lưới thẻ. Số hàng ít hơn khung nhìn thì lưới nằm giữa, không cuộn.</summary>
    public RectTransform Grid(Vector2 pos, Vector2 view, int count, int cols, Vector2 cell, Vector2 spacing, int padV)
    {
        var content = SinhMenuKit.MakeScroll(Ui, pos, view);
        var g = content.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = cell; g.spacing = spacing;
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = Mathf.Max(1, cols);
        g.childAlignment = TextAnchor.MiddleCenter; g.padding = new RectOffset(0, 0, padV, padV);
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)Mathf.Max(1, cols)));
        float h = rows * cell.y + (rows - 1) * spacing.y + 2 * padV;
        content.sizeDelta = new Vector2(0, Mathf.Max(view.y, h));
        return content;
    }

    void Update()
    {
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime, since = t - t0;
        MenuNatureFx.FitCover(artRoot, scene, ref lastRect);
        if (nature != null) nature.Tick(t, dt);

        float k = Mathf.Clamp01(since / .7f);
        header.alpha = k;
        float e = 1f + .12f * (1f - Mathf.SmoothStep(0f, 1f, k));
        titleText.rectTransform.localScale = new Vector3(e, e, 1f);
        titleText.color = Color.Lerp(GameTheme.GoldHi, new Color(1f, .93f, .68f), .5f + .5f * Mathf.Sin(t * 1.6f));

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
    }

    void OnDestroy() { if (nature != null) nature.Dispose(); }
}

/// <summary>Thẻ khung đá khắc rune: hiện dần, rê chuột sáng + phóng to, vòng rune xoay. Hoạt động bên trong GridLayoutGroup (chỉ đổi scale/alpha).</summary>
public class FlowCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Action onClick;

    RectTransform rt; CanvasGroup cg;
    Image aura, dash, glow;
    Color accent; bool locked, hovered;
    float hover, born, delay, phase;

    public static FlowCard Build(Transform parent, string name, Color accent, bool locked, float delay, out RectTransform content)
    {
        var root = UIKit.CBox(name, parent, Color.clear, Vector2.zero, Vector2.zero, true);
        var c = root.gameObject.AddComponent<FlowCard>();
        c.rt = root; c.accent = accent; c.locked = locked; c.delay = delay; c.phase = UnityEngine.Random.value * 6f;
        c.cg = root.gameObject.AddComponent<CanvasGroup>(); c.cg.alpha = 0f;

        var au = UIKit.CBox("Aura", root, accent.WithAlpha(0f), Vector2.zero, Vector2.zero);
        UIKit.Stretch(au, -60, -60, -60, -60);
        c.aura = au.GetComponent<Image>(); c.aura.sprite = ThemeGfx.Glow;

        var fr = UIKit.CBox("Frame", root, Color.white, Vector2.zero, Vector2.zero);
        UIKit.Stretch(fr, 0, 0, 0, 0);
        var fi = fr.GetComponent<Image>(); var fs = MenuArt.Get("px_frame");
        if (fs != null) { fi.sprite = fs; fi.type = Image.Type.Sliced; fi.pixelsPerUnitMultiplier = 1f / 3f; }
        else
        {
            fi.color = GameTheme.Iron;
            var inner = UIKit.CBox("Fill", fr, GameTheme.Night, Vector2.zero, Vector2.zero); UIKit.Stretch(inner, 5, 5, 5, 5);
        }
        if (locked) fi.color = new Color(.6f, .6f, .68f, 1f);

        content = UIKit.CBox("Content", root, Color.clear, Vector2.zero, Vector2.zero);
        UIKit.Stretch(content, 28, 28, 28, 28);
        return c;
    }

    /// <summary>Vòng rune (đĩa tối + viền đồng + vòng đứt nét xoay + quầng sáng). Trả về tâm vòng để đặt biểu tượng lên trên.</summary>
    public void AddRune(Transform p, Vector2 pos, float d)
    {
        Color col = locked ? GameTheme.Fog.WithAlpha(.5f) : accent;
        Spr(p, "RuneGlow", ThemeGfx.Glow, col.WithAlpha(locked ? .12f : .40f), pos, d * 1.6f);
        Spr(p, "RuneDisc", RuneArt.Disk, new Color(.05f, .04f, .10f, .9f), pos, d);
        Spr(p, "RuneRing", RuneArt.Ring, locked ? GameTheme.Iron : GameTheme.Bronze, pos, d * 1.05f);
        dash = Spr(p, "RuneDash", RuneArt.DashRing, col.WithAlpha(locked ? .35f : .75f), pos, d * 1.26f);
        glow = Spr(p, "IconGlow", ThemeGfx.Glow, col.WithAlpha(locked ? 0f : .45f), pos, d * .96f);
    }

    /// <summary>Biểu tượng rune của môn (Lý: quỹ đạo, Hóa: bình, Sinh: lá).</summary>
    public Image Glyph(Transform p, SubjectId id, Vector2 pos, float size)
    {
        Sprite sp = id == SubjectId.Ly ? RuneArt.Orbits : id == SubjectId.Hoa ? RuneArt.Flask : RuneArt.Leaf;
        return Spr(p, "Glyph", sp, locked ? GameTheme.Fog.WithAlpha(.5f) : accent, pos, size);
    }

    public static Image Spr(Transform p, string n, Sprite sp, Color c, Vector2 pos, float size)
    {
        var r = UIKit.CBox(n, p, c, pos, new Vector2(size, size));
        var im = r.GetComponent<Image>(); im.sprite = sp; im.raycastTarget = false;
        return im;
    }

    public static Text Fit(Text t, int min, int max)
    { t.resizeTextForBestFit = true; t.resizeTextMinSize = min; t.resizeTextMaxSize = max; t.verticalOverflow = VerticalWrapMode.Truncate; return t; }

    void Awake() { born = Time.unscaledTime; }

    static float EaseOutBack(float k) { const float c1 = 1.70158f, c3 = c1 + 1f; float m = k - 1f; return 1f + c3 * m * m * m + c1 * m * m; }

    void Update()
    {
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        float k = Mathf.Clamp01((t - born - delay) / .6f);
        float e = EaseOutBack(k);
        hover = Mathf.MoveTowards(hover, hovered && !locked ? 1f : 0f, dt * 6f);

        rt.localScale = Vector3.one * Mathf.LerpUnclamped(.85f, 1f, e) * (1f + .05f * hover);
        cg.alpha = Mathf.Clamp01(k * 2f);
        cg.blocksRaycasts = k > .5f;

        if (!locked)
        {
            aura.color = accent.WithAlpha(Mathf.Clamp01(.08f + .05f * Mathf.Sin(t * 2f + phase) + .32f * hover) * k);
            if (glow != null) glow.color = accent.WithAlpha(.40f + .12f * Mathf.Sin(t * 2.2f + phase) + .25f * hover);
        }
        if (dash != null) dash.rectTransform.localRotation = Quaternion.Euler(0, 0, -t * (locked ? 4f : 14f + 70f * hover));
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (hovered) return;
        hovered = true;
        if (!locked) GameAudio.Play(Snd.UiHover);
    }

    public void OnPointerExit(PointerEventData e)
    {
        var go = e.pointerCurrentRaycast.gameObject;
        if (go != null && go.transform.IsChildOf(transform)) return;   // rời sang nút con vẫn tính là đang rê trong thẻ
        hovered = false;
    }

    public void OnPointerClick(PointerEventData e) { if (onClick != null) onClick(); }
}
