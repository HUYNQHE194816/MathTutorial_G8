using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum PickerMode { MultipleChoice, Essay }

/// <summary>
/// Màn CHỌN MÔN dark fantasy dùng chung cho Trắc nghiệm (SubjectSelectScene) và Tự luận (EssaySubjectSelectScene).
/// Nền = rừng trăng của MainMenu (cây lay, cành, cỏ, lá rơi, trăng nhấp nháy); ba thẻ Lý/Hóa/Sinh có hoạt ảnh:
/// trồi lên lần lượt, lơ lửng, vòng rune xoay + tàn lửa, rê chuột sáng lên, chọn thì hai thẻ kia tan đi và thẻ chọn bùng sáng.
/// Mọi sprite biểu tượng sinh bằng code. Nếu scene còn giao diện cũ (SubjectSelectManager) thì chỉ ẩn lúc chạy.
/// </summary>
public class SubjectPickerScreen : MonoBehaviour
{
    /// <summary>Môn vừa chọn ở chế độ Tự luận (để scene Tự luận đọc).</summary>
    public static SubjectId EssaySubject;

    [SerializeField] PickerMode mode = PickerMode.MultipleChoice;
    [SerializeField] string backScene = "ModeSelectScene";
    [Tooltip("Chỉ dùng cho Tự luận: scene mở sau khi chọn môn. Để trống = chưa có, hiện thông báo 'đang xây dựng'.")]
    [SerializeField] string essayTargetScene = "";

    const float CardW = 440f, CardH = 630f, CardY = -93f, CardGap = 70f;

    RectTransform artRoot, scene, ui;
    MenuNatureFx nature;
    Vector2 lastRect;
    CanvasGroup header;
    Text titleText;
    float t0;
    bool locked;
    readonly List<PickerCard> cards = new List<PickerCard>();
    readonly List<Burst> bursts = new List<Burst>();

    class Burst { public RectTransform rt; public Image im; public Vector2 v; public float age, life; }

    // ------------------------------------------------------------------ khởi tạo
    void Awake()
    {
        // Giao diện cũ (nếu có) bị ẩn: tắt manager cũ trước khi nó kịp Start, rồi tắt các canvas cũ.
        var legacy = FindFirstObjectByType<SubjectSelectManager>();
        if (legacy != null) legacy.enabled = false;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
    }

    void Start()
    {
        Time.timeScale = 1f;
        t0 = Time.unscaledTime;
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = GameTheme.Ink; }

        UIKit.EnsureEventSystem();
        SceneShell.Create(ShellMood.Hub, null, 1f, true, false);

        artRoot = UIKit.BuildCanvas(transform, "Picker Art", -90);
        scene = UIKit.CBox("Scene", artRoot, Color.clear, Vector2.zero, new Vector2(1920f, 1080f));
        nature = new MenuNatureFx();
        nature.BuildForest(scene, 200f, 24);

        ui = UIKit.BuildCanvas(transform, "Picker UI", 10);
        BuildHeader();
        BuildCards();
        UIKit.MakeButton(ui, "Btn_Back", "QUAY LẠI", new Vector2(-750f, -456f), new Vector2(300f, 72f), Back, ButtonStyle.Stone, 32, .6f);
        MenuNatureFx.FitCover(artRoot, scene, ref lastRect);
    }

    bool Essay { get { return mode == PickerMode.Essay; } }

    void BuildHeader()
    {
        var h = UIKit.CBox("Header", ui, Color.clear, Vector2.zero, Vector2.zero);
        header = h.gameObject.AddComponent<CanvasGroup>();
        UIKit.Label("Kicker", h, "KHOA HỌC TỰ NHIÊN  •  " + (Essay ? "TỰ LUẬN" : "TRẮC NGHIỆM"), 28,
            new Color(.67f, .75f, .9f, .92f), TextAnchor.MiddleCenter, new Vector2(0, 478), new Vector2(1200, 44), true, true);
        titleText = UIKit.Label("Title", h, "CHỌN MÔN", 112, GameTheme.GoldHi, TextAnchor.MiddleCenter,
            new Vector2(0, 392), new Vector2(1200, 150), true, true, FontStyle.Bold);
        var shade = UIKit.CBox("SubShade", h, new Color(.02f, .04f, .10f, .65f), new Vector2(0, 292), new Vector2(1500, 150));
        shade.GetComponent<Image>().sprite = ThemeGfx.Glow;
        UIKit.Label("Sub", h, Essay ? "Viết lời giải từng bước để phá các lớp rune của boss." : "Chọn một môn để bắt đầu hành trình.", 34,
            new Color(.86f, .91f, 1f, .98f), TextAnchor.MiddleCenter, new Vector2(0, 292), new Vector2(1400, 50), true, true, FontStyle.Italic);
    }

    void BuildCards()
    {
        string[] names = { "VẬT LÝ", "HÓA HỌC", "SINH HỌC" };
        string[] tagMc = { "Mê cung tri thức", "Ai là triệu phú", "Trận chiến Rồng Băng" };
        string[] tagEs = { "Phong ấn của\nTia Chớp", "Phong ấn của\nNgọn Lửa Tím", "Phong ấn của\nRồng Băng" };
        var ids = new[] { SubjectId.Ly, SubjectId.Hoa, SubjectId.Sinh };
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * (CardW + CardGap);
            var c = PickerCard.Build(this, i, ids[i], x, names[i], Essay ? tagEs[i] : tagMc[i], ui);
            cards.Add(c);
        }
    }

    // ------------------------------------------------------------------ hành vi
    internal void Select(PickerCard card)
    {
        if (locked) return;
        string target = Essay ? essayTargetScene : SubjectSession.ChapterScene(card.id);
        if (string.IsNullOrEmpty(target) || !Application.CanStreamedLevelBeLoaded(target))
        {
            card.Shake(); GameAudio.Play(Snd.CardLocked);
            UIKit.Toast(ui, Essay ? "Tự luận môn " + card.title + " đang được xây dựng" : "Không tìm thấy scene: " + target);
            return;
        }
        locked = true;
        if (Essay) EssaySubject = card.id;
        GameAudio.Play(Snd.CardSelect);
        StartCoroutine(SelectRoutine(card, target));
    }

    IEnumerator SelectRoutine(PickerCard chosen, string target)
    {
        SpawnBurst(chosen);
        for (float k = 0f; k < .55f; k += Time.unscaledDeltaTime)
        {
            float e = Mathf.SmoothStep(0f, 1f, k / .55f);
            foreach (var c in cards) { if (c == chosen) c.chosen = e; else c.dismiss = e; }
            yield return null;
        }
        chosen.chosen = 1f;
        yield return new WaitForSecondsRealtime(.15f);
        SceneRouter.Go(target);
    }

    void Back()
    {
        if (locked) return;
        locked = true;
        GameAudio.Play(Snd.UiBack);
        SceneRouter.Go(backScene);
    }

    void SpawnBurst(PickerCard card)
    {
        var origin = card.Root.anchoredPosition + new Vector2(0f, 160f);
        for (int i = 0; i < 26; i++)
        {
            float a = Random.value * 6.283f, sp = Random.Range(220f, 620f);
            var rt = UIKit.CBox("Burst", ui, Color.white, origin, Vector2.one * Random.Range(14f, 34f));
            var im = rt.GetComponent<Image>(); im.sprite = ThemeGfx.Glow; im.raycastTarget = false;
            im.color = Color.Lerp(card.color, Color.white, Random.Range(.2f, .8f));
            bursts.Add(new Burst { rt = rt, im = im, v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp, life = Random.Range(.7f, 1.3f) });
        }
    }

    // ------------------------------------------------------------------ hoạt ảnh
    void Update()
    {
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime, since = t - t0;
        MenuNatureFx.FitCover(artRoot, scene, ref lastRect);
        if (nature != null) nature.Tick(t, dt);

        if (header != null)
        {
            float k = Mathf.Clamp01(since / .7f);
            header.alpha = k;
            if (titleText != null)
            {
                float e = 1f + .12f * (1f - Mathf.SmoothStep(0f, 1f, k));
                titleText.rectTransform.localScale = new Vector3(e, e, 1f);
                titleText.color = Color.Lerp(GameTheme.GoldHi, new Color(1f, .93f, .68f), .5f + .5f * Mathf.Sin(t * 1.6f));
            }
        }

        foreach (var c in cards) c.Tick(t, dt, since);

        for (int i = bursts.Count - 1; i >= 0; i--)
        {
            var b = bursts[i]; b.age += dt;
            float k = b.age / b.life;
            if (k >= 1f) { Destroy(b.rt.gameObject); bursts.RemoveAt(i); continue; }
            b.v.y -= 380f * dt; b.v *= 1f - .9f * dt;
            b.rt.anchoredPosition += b.v * dt;
            var col = b.im.color; col.a = 1f - k * k; b.im.color = col;
        }

        var kb = Keyboard.current;
        if (kb != null && !locked)
        {
            if (kb.escapeKey.wasPressedThisFrame) Back();
            else if (kb.digit1Key.wasPressedThisFrame) Select(cards[0]);
            else if (kb.digit2Key.wasPressedThisFrame) Select(cards[1]);
            else if (kb.digit3Key.wasPressedThisFrame) Select(cards[2]);
        }
    }

    void OnDestroy() { if (nature != null) nature.Dispose(); }
}

/// <summary>Một thẻ môn: dựng giao diện + toàn bộ hoạt ảnh của thẻ.</summary>
public class PickerCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public SubjectId id;
    public string title;
    public Color color;
    public float dismiss, chosen;                       // 0..1, do SubjectPickerScreen điều khiển

    SubjectPickerScreen owner;
    int index;
    Vector2 basePos;
    CanvasGroup cg;
    Image aura, glowIcon;
    RectTransform dash, iconRt, orbits;
    RectTransform[] bubbles; float[] bubblePhase;
    RectTransform[] embers; float[] emberPhase, emberX;
    float hover; bool hovered, introSfx;
    float shakeLeft;

    public RectTransform Root { get; private set; }

    public static PickerCard Build(SubjectPickerScreen owner, int index, SubjectId id, float x, string name, string tagline, RectTransform ui)
    {
        var col = GameTheme.Subject(id);
        var root = UIKit.CBox("Card_" + name, ui, Color.clear, new Vector2(x, -93f), new Vector2(440f, 630f));
        var card = root.gameObject.AddComponent<PickerCard>();
        card.owner = owner; card.index = index; card.id = id; card.title = name; card.color = col;
        card.Root = root; card.basePos = root.anchoredPosition;
        card.cg = root.gameObject.AddComponent<CanvasGroup>(); card.cg.alpha = 0f;

        Img(root, "Shadow", ThemeGfx.Glow, new Color(0, 0, 0, .6f), new Vector2(0, -318), new Vector2(560, 80));
        card.aura = Img(root, "Aura", ThemeGfx.Glow, col.WithAlpha(0f), new Vector2(0, 20), new Vector2(800, 980));

        var frame = UIKit.CBox("Frame", root, Color.white, Vector2.zero, new Vector2(440f, 630f), true).GetComponent<Image>();
        var fs = MenuArt.Get("px_frame");
        if (fs != null) { frame.sprite = fs; frame.type = Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 1f / 3f; }
        else frame.color = GameTheme.Night;

        // ---- vòng rune
        var ringPos = new Vector2(0, 160);
        Img(root, "RuneGlow", ThemeGfx.Glow, col.WithAlpha(.40f), ringPos, new Vector2(250, 250));
        Img(root, "RuneDisc", RuneArt.Disk, new Color(.05f, .04f, .10f, .88f), ringPos, new Vector2(156, 156));
        Img(root, "RuneRing", RuneArt.Ring, GameTheme.Bronze, ringPos, new Vector2(164, 164));
        card.dash = Img(root, "RuneDash", RuneArt.DashRing, col.WithAlpha(.75f), ringPos, new Vector2(196, 196)).rectTransform;
        card.glowIcon = Img(root, "IconGlow", ThemeGfx.Glow, col.WithAlpha(.45f), ringPos, new Vector2(150, 150));

        var icon = UIKit.CBox("Icon", root, Color.clear, ringPos, new Vector2(110, 110));
        card.iconRt = icon;
        switch (id)
        {
            case SubjectId.Ly:
                card.orbits = Img(icon, "Orbits", RuneArt.Orbits, col, Vector2.zero, new Vector2(110, 110)).rectTransform;
                Img(icon, "Nucleus", RuneArt.Disk, Color.Lerp(col, Color.white, .4f), Vector2.zero, new Vector2(15, 15));
                break;
            case SubjectId.Hoa:
                card.bubbles = new RectTransform[3]; card.bubblePhase = new float[3];
                for (int i = 0; i < 3; i++)
                {
                    card.bubbles[i] = Img(icon, "Bubble", RuneArt.Ring, Color.Lerp(col, Color.white, .5f), Vector2.zero, new Vector2(15, 15)).rectTransform;
                    card.bubblePhase[i] = i / 3f;
                }
                Img(icon, "Flask", RuneArt.Flask, col, Vector2.zero, new Vector2(110, 110));
                break;
            default:
                Img(icon, "Leaf", RuneArt.Leaf, col, Vector2.zero, new Vector2(110, 110));
                break;
        }

        // ---- chữ + nút
        UIKit.Label("Name", root, name, 56, GameTheme.GoldHi, TextAnchor.MiddleCenter, new Vector2(0, 36), new Vector2(340, 80), true, true, FontStyle.Bold);
        UIKit.Divider(root, new Vector2(0, -14), 300);
        UIKit.Label("Tag", root, tagline, 28, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, -86), new Vector2(330, 96), false, false, FontStyle.Italic);
        UIKit.MakeButton(root, "Btn_Choose", "CHỌN", new Vector2(0, -222), new Vector2(240, 70), () => owner.Select(card), ButtonStyle.Bronze, 32, .5f + index * .14f);

        // ---- tàn lửa bay lên từ vòng rune
        card.embers = new RectTransform[6]; card.emberPhase = new float[6]; card.emberX = new float[6];
        for (int i = 0; i < 6; i++)
        {
            card.embers[i] = Img(root, "Ember", ThemeGfx.Glow, Color.Lerp(GameTheme.Ember, col, .5f), ringPos, new Vector2(14, 14)).rectTransform;
            card.emberPhase[i] = Random.value; card.emberX[i] = Random.Range(-60f, 60f);
        }
        return card;
    }

    static Image Img(Transform parent, string n, Sprite sp, Color c, Vector2 pos, Vector2 size)
    {
        var rt = UIKit.CBox(n, parent, c, pos, size);
        var im = rt.GetComponent<Image>(); im.sprite = sp; im.raycastTarget = false;
        return im;
    }

    public void Shake() { shakeLeft = .4f; }

    public void OnPointerEnter(PointerEventData e)
    {
        if (hovered) return;
        hovered = true;
        if (dismiss <= 0f && chosen <= 0f) GameAudio.Play(Snd.UiHover);
    }
    public void OnPointerExit(PointerEventData e)
    {
        // rời sang nút con vẫn tính là đang rê trong thẻ
        var go = e.pointerCurrentRaycast.gameObject;
        if (go != null && go.transform.IsChildOf(transform)) return;
        hovered = false;
    }
    public void OnPointerClick(PointerEventData e) { owner.Select(this); }

    static float EaseOutBack(float k) { const float c1 = 1.70158f, c3 = c1 + 1f; float m = k - 1f; return 1f + c3 * m * m * m + c1 * m * m; }

    public void Tick(float t, float dt, float since)
    {
        float k = Mathf.Clamp01((since - .25f - index * .14f) / .75f);
        float e = EaseOutBack(k);
        hover = Mathf.MoveTowards(hover, hovered && dismiss <= 0f ? 1f : 0f, dt * 6f);
        if (shakeLeft > 0f) shakeLeft -= dt;

        float bob = k >= 1f ? Mathf.Sin(t * 1.4f + index * 1.3f) * 8f : 0f;
        float shakeX = shakeLeft > 0f ? Mathf.Sin(t * 70f) * 10f * (shakeLeft / .4f) : 0f;
        var p = basePos + new Vector2(shakeX, -(1f - e) * 300f + bob * (1f - dismiss) - dismiss * 120f + chosen * 14f);
        Root.anchoredPosition = p;
        float sc = Mathf.Lerp(.82f, 1f, e) * (1f + .06f * hover + .10f * chosen) * (1f - .18f * dismiss);
        Root.localScale = new Vector3(sc, sc, 1f);
        cg.alpha = Mathf.Clamp01(k * 1.4f) * (1f - dismiss);
        cg.blocksRaycasts = k > .6f && dismiss <= 0f;

        float pulse = .06f * Mathf.Sin(t * 2f + index);
        var ac = color; ac.a = Mathf.Clamp01(.10f + pulse + .32f * hover + .55f * chosen) * k; aura.color = ac;

        dash.localRotation = Quaternion.Euler(0, 0, -t * (14f + 70f * hover + 340f * chosen));
        var gc = glowIcon.color; gc.a = .40f + .12f * Mathf.Sin(t * 2.2f + index) + .25f * hover + .3f * chosen; glowIcon.color = gc;
        float breathe = 1f + .04f * Mathf.Sin(t * 2f + index) + .08f * hover + .12f * chosen;
        iconRt.localScale = new Vector3(breathe, breathe, 1f);

        // riêng từng môn
        if (orbits != null) orbits.localRotation = Quaternion.Euler(0, 0, t * (24f + 90f * hover + 300f * chosen));
        if (id == SubjectId.Sinh) iconRt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.3f) * 6f + Mathf.Sin(t * 2.9f) * 2f);
        if (bubbles != null)
            for (int i = 0; i < bubbles.Length; i++)
            {
                float u = Mathf.Repeat(t * (.35f + .25f * hover) + bubblePhase[i], 1f);
                bubbles[i].anchoredPosition = new Vector2((i - 1) * 16f + Mathf.Sin(u * 9f + i) * 4f, Mathf.Lerp(-34f, 14f, u));
                var im = bubbles[i].GetComponent<Image>(); var bc = im.color; bc.a = Mathf.Sin(u * 3.1416f); im.color = bc;
                bubbles[i].localScale = Vector3.one * Mathf.Lerp(.5f, 1.1f, u);
            }

        for (int i = 0; i < embers.Length; i++)
        {
            float u = Mathf.Repeat(t * (.28f + .3f * hover) + emberPhase[i], 1f);
            embers[i].anchoredPosition = new Vector2(emberX[i] + Mathf.Sin(u * 7f + i * 2f) * 14f, 90f + u * 190f);
            var im = embers[i].GetComponent<Image>(); var ec = im.color; ec.a = Mathf.Sin(u * 3.1416f) * (.55f + .4f * hover) * k; im.color = ec;
        }
    }
}

/// <summary>Sprite biểu tượng + vòng rune vẽ bằng code (khử răng cưa theo khoảng cách), có cache.</summary>
static class RuneArt
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { cache.Clear(); }

    public static Sprite Disk { get { return Get("disk", 128, (b, n) => Fill(b, n, .5f, .5f, .48f)); } }
    public static Sprite Ring { get { return Get("ring", 256, (b, n) => Arc(b, n, .5f, .5f, .46f, 0f, 360f, .022f)); } }
    public static Sprite DashRing
    {
        get
        {
            return Get("dash", 256, (b, n) =>
            {
                for (int i = 0; i < 28; i++) Arc(b, n, .5f, .5f, .46f, i * 360f / 28f, i * 360f / 28f + 7f, .02f);
            });
        }
    }
    public static Sprite Orbits
    {
        get
        {
            return Get("orbits", 256, (b, n) =>
            {
                for (int i = 0; i < 3; i++) Ellipse(b, n, .5f, .5f, .43f, .16f, i * 60f, .028f);
            });
        }
    }
    public static Sprite Flask
    {
        get
        {
            return Get("flask", 256, (b, n) =>
            {
                float hw = .03f;
                Poly(b, n, hw, .38f, .92f, .62f, .92f);
                Poly(b, n, hw, .43f, .92f, .43f, .58f, .16f, .14f, .20f, .07f, .80f, .07f, .84f, .14f, .57f, .58f, .57f, .92f);
                Poly(b, n, hw * .8f, .25f, .30f, .75f, .30f);
            });
        }
    }
    public static Sprite Leaf
    {
        get
        {
            return Get("leaf", 256, (b, n) =>
            {
                float hw = .03f;
                Bezier(b, n, hw, .16f, .14f, .06f, .78f, .88f, .92f);
                Bezier(b, n, hw, .16f, .14f, .84f, .20f, .88f, .92f);
                Poly(b, n, hw * .8f, .16f, .14f, .72f, .72f);
                Poly(b, n, hw * .7f, .34f, .33f, .28f, .52f);
                Poly(b, n, hw * .7f, .46f, .45f, .66f, .38f);
                Poly(b, n, hw * .7f, .56f, .57f, .50f, .74f);
                Poly(b, n, hw, .16f, .14f, .07f, .05f);
            });
        }
    }

    static Sprite Get(string key, int n, System.Action<float[], int> paint)
    {
        Sprite s;
        if (cache.TryGetValue(key, out s) && s != null) return s;
        var buf = new float[n * n];
        paint(buf, n);
        var px = new Color32[n * n];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(buf[i]) * 255f));
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        tex.SetPixels32(px); tex.Apply(true);
        s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f);
        s.hideFlags = HideFlags.DontSave;
        cache[key] = s;
        return s;
    }

    // Toạ độ 0..1 (gốc dưới-trái), hw = nửa bề dày nét theo tỉ lệ ảnh.
    static void Seg(float[] b, int n, float ax, float ay, float bx, float by, float hw)
    {
        float x0 = ax * n, y0 = ay * n, x1 = bx * n, y1 = by * n, h = hw * n;
        int xmin = Mathf.Max(0, (int)(Mathf.Min(x0, x1) - h - 2)), xmax = Mathf.Min(n - 1, (int)(Mathf.Max(x0, x1) + h + 2));
        int ymin = Mathf.Max(0, (int)(Mathf.Min(y0, y1) - h - 2)), ymax = Mathf.Min(n - 1, (int)(Mathf.Max(y0, y1) + h + 2));
        float dx = x1 - x0, dy = y1 - y0, l2 = dx * dx + dy * dy;
        for (int y = ymin; y <= ymax; y++)
            for (int x = xmin; x <= xmax; x++)
            {
                float px = x + .5f, py = y + .5f;
                float t = l2 > 0f ? Mathf.Clamp01(((px - x0) * dx + (py - y0) * dy) / l2) : 0f;
                float ex = px - (x0 + t * dx), ey = py - (y0 + t * dy);
                float a = Mathf.Clamp01(h + .5f - Mathf.Sqrt(ex * ex + ey * ey));
                if (a > b[y * n + x]) b[y * n + x] = a;
            }
    }

    static void Poly(float[] b, int n, float hw, params float[] p)
    {
        for (int i = 0; i + 3 < p.Length; i += 2) Seg(b, n, p[i], p[i + 1], p[i + 2], p[i + 3], hw);
    }

    static void Bezier(float[] b, int n, float hw, float x0, float y0, float cx, float cy, float x1, float y1)
    {
        float px = x0, py = y0;
        for (int i = 1; i <= 40; i++)
        {
            float t = i / 40f, u = 1f - t;
            float x = u * u * x0 + 2f * u * t * cx + t * t * x1, y = u * u * y0 + 2f * u * t * cy + t * t * y1;
            Seg(b, n, px, py, x, y, hw); px = x; py = y;
        }
    }

    static void Ellipse(float[] b, int n, float cx, float cy, float rx, float ry, float rotDeg, float hw)
    {
        float cr = Mathf.Cos(rotDeg * Mathf.Deg2Rad), sr = Mathf.Sin(rotDeg * Mathf.Deg2Rad);
        float px = 0, py = 0;
        for (int i = 0; i <= 90; i++)
        {
            float a = i / 90f * 6.2832f;
            float lx = Mathf.Cos(a) * rx, ly = Mathf.Sin(a) * ry;
            float x = cx + lx * cr - ly * sr, y = cy + lx * sr + ly * cr;
            if (i > 0) Seg(b, n, px, py, x, y, hw);
            px = x; py = y;
        }
    }

    static void Arc(float[] b, int n, float cx, float cy, float r, float a0, float a1, float hw)
    {
        int steps = Mathf.Max(2, (int)((a1 - a0) / 4f));
        float px = 0, py = 0;
        for (int i = 0; i <= steps; i++)
        {
            float a = Mathf.Lerp(a0, a1, i / (float)steps) * Mathf.Deg2Rad;
            float x = cx + Mathf.Cos(a) * r, y = cy + Mathf.Sin(a) * r;
            if (i > 0) Seg(b, n, px, py, x, y, hw);
            px = x; py = y;
        }
    }

    static void Fill(float[] b, int n, float cx, float cy, float r)
    {
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + .5f - cx * n, dy = y + .5f - cy * n;
                b[y * n + x] = Mathf.Clamp01(r * n + .5f - Mathf.Sqrt(dx * dx + dy * dy));
            }
    }
}
