using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Màu nhấn theo độ khó, dùng chung cho màn Chọn độ khó và HUD trong phòng.</summary>
public static class DifficultyTheme
{
    public static Color Of(EssayDifficulty d)
    {
        switch (d)
        {
            case EssayDifficulty.Easy: return GameTheme.Moss;
            case EssayDifficulty.Normal: return GameTheme.Ember;
            case EssayDifficulty.Hard: return new Color(.92f, .27f, .33f);
            default: return GameTheme.Hex(0xB06BFF);
        }
    }

    public static Sprite Icon(EssayDifficulty d)
    {
        switch (d)
        {
            case EssayDifficulty.Easy: return RuneArt.Shield;
            case EssayDifficulty.Normal: return RuneArt.Sword;
            case EssayDifficulty.Hard: return RuneArt.Swords;
            default: return RuneArt.Skull;
        }
    }
}

/// <summary>
/// Màn CHỌN ĐỘ KHÓ của chế độ Tự luận (thay cho chọn môn Lý/Hóa/Sinh). Bốn thẻ: Dễ, Bình thường, Khó, Siêu khó.
/// Chọn xong là vào hầm ngục (RoomScene); môn Lý/Hóa/Sinh tự xen kẽ theo màn. Độ khó quyết định quái đông/nhanh/mạnh cỡ nào
/// và đề khó cỡ nào (xem RoomRules). Nền = rừng trăng của MainMenu, thẻ trồi lên lần lượt, rê chuột sáng lên, chọn thì ba thẻ kia tan đi.
/// Được SubjectPickerScreen (mode Essay) tự gắn vào, nên scene EssaySubjectSelectScene không cần sửa.
/// </summary>
public class EssayDifficultyScreen : MonoBehaviour
{
    [SerializeField] string backScene = "ModeSelectScene";
    [SerializeField] string targetScene = "RoomScene";

    public const float CardW = 400f, CardH = 640f, CardGap = 46f;

    RectTransform artRoot, scene, ui;
    MenuNatureFx nature;
    Vector2 lastRect;
    CanvasGroup header;
    Text titleText;
    float t0;
    bool locked;
    readonly List<DifficultyCard> cards = new List<DifficultyCard>();
    readonly List<Burst> bursts = new List<Burst>();

    class Burst { public RectTransform rt; public Image im; public Vector2 v; public float age, life; }

    void Start()
    {
        Time.timeScale = 1f;
        t0 = Time.unscaledTime;
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = GameTheme.Ink; }

        UIKit.EnsureEventSystem();
        SceneShell.Create(ShellMood.Hub, null, 1f, true, false);

        artRoot = UIKit.BuildCanvas(transform, "Difficulty Art", -90);
        scene = UIKit.CBox("Scene", artRoot, Color.clear, Vector2.zero, new Vector2(1920f, 1080f));
        nature = new MenuNatureFx();
        nature.BuildForest(scene, 200f, 24);

        ui = UIKit.BuildCanvas(transform, "Difficulty UI", 10);
        BuildHeader();
        for (int i = 0; i < 4; i++)
        {
            float x = (i - 1.5f) * (CardW + CardGap);
            cards.Add(DifficultyCard.Build(this, i, (EssayDifficulty)i, x, ui));
        }
        UIKit.MakeButton(ui, "Btn_Back", "QUAY LẠI", new Vector2(-750f, -456f), new Vector2(300f, 72f), Back, ButtonStyle.Stone, 32, .6f);
        MenuNatureFx.FitCover(artRoot, scene, ref lastRect);
    }

    void BuildHeader()
    {
        var h = UIKit.CBox("Header", ui, Color.clear, Vector2.zero, Vector2.zero);
        header = h.gameObject.AddComponent<CanvasGroup>();
        UIKit.Label("Kicker", h, "KHOA HỌC TỰ NHIÊN  •  TỰ LUẬN", 28, new Color(.67f, .75f, .9f, .92f), TextAnchor.MiddleCenter,
            new Vector2(0, 478), new Vector2(1200, 44), true, true);
        titleText = UIKit.Label("Title", h, "CHỌN ĐỘ KHÓ", 112, GameTheme.GoldHi, TextAnchor.MiddleCenter,
            new Vector2(0, 392), new Vector2(1200, 150), true, true, FontStyle.Bold);
        var shade = UIKit.CBox("SubShade", h, new Color(.02f, .04f, .10f, .65f), new Vector2(0, 292), new Vector2(1500, 150));
        shade.GetComponent<Image>().sprite = ThemeGfx.Glow;
        UIKit.Label("Sub", h, "Quái càng đông, càng nhanh, càng mạnh — đề càng hóc, thưởng càng lớn.", 34,
            new Color(.86f, .91f, 1f, .98f), TextAnchor.MiddleCenter, new Vector2(0, 292), new Vector2(1500, 50), true, true, FontStyle.Italic);
    }

    // ------------------------------------------------------------------ hành vi
    internal void Select(DifficultyCard card)
    {
        if (locked) return;
        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            card.Shake(); GameAudio.Play(Snd.CardLocked);
            UIKit.Toast(ui, "Không tìm thấy scene: " + targetScene);
            return;
        }
        locked = true;
        RunState.difficulty = card.diff;     // độ khó của cả lượt chơi
        RunState.ResetAll(true);             // lượt mới: màn 1-1, đầy máu, súng ngắn, Mực giữ nguyên
        GameAudio.Play(Snd.CardSelect);
        StartCoroutine(SelectRoutine(card));
    }

    IEnumerator SelectRoutine(DifficultyCard chosen)
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
        SceneRouter.Go(targetScene);
    }

    void Back()
    {
        if (locked) return;
        locked = true;
        GameAudio.Play(Snd.UiBack);
        SceneRouter.Go(backScene);
    }

    void SpawnBurst(DifficultyCard card)
    {
        var origin = card.Root.anchoredPosition + new Vector2(0f, 170f);
        for (int i = 0; i < 30; i++)
        {
            float a = Random.value * 6.283f, sp = Random.Range(220f, 660f);
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
        if (kb != null && !locked && cards.Count == 4)
        {
            if (kb.escapeKey.wasPressedThisFrame) Back();
            else if (kb.digit1Key.wasPressedThisFrame) Select(cards[0]);
            else if (kb.digit2Key.wasPressedThisFrame) Select(cards[1]);
            else if (kb.digit3Key.wasPressedThisFrame) Select(cards[2]);
            else if (kb.digit4Key.wasPressedThisFrame) Select(cards[3]);
        }
    }

    void OnDestroy() { if (nature != null) nature.Dispose(); }
}

/// <summary>Một thẻ độ khó: khung đá, vòng rune xoay, biểu tượng, các chỉ số quái, nút CHỌN. Cùng bộ hoạt ảnh với thẻ chọn môn.</summary>
public class DifficultyCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public EssayDifficulty diff;
    public Color color;
    public float dismiss, chosen;                       // 0..1, do EssayDifficultyScreen điều khiển

    EssayDifficultyScreen owner;
    int index;
    Vector2 basePos;
    CanvasGroup cg;
    Image aura, glowIcon;
    RectTransform dash, iconRt;
    RectTransform[] embers; float[] emberPhase, emberX;
    float hover, shakeLeft; bool hovered;

    public RectTransform Root { get; private set; }

    static readonly string[] StepText = { "2-3 bước", "3-4 bước", "4-5 bước", "5-6 bước" };

    static string Mul(float v) { return "×" + v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ','); }

    public static DifficultyCard Build(EssayDifficultyScreen owner, int index, EssayDifficulty d, float x, RectTransform ui)
    {
        var col = DifficultyTheme.Of(d);
        string name = RoomRules.DifficultyName(d).ToUpper();
        var root = UIKit.CBox("Card_" + d, ui, Color.clear, new Vector2(x, -93f), new Vector2(EssayDifficultyScreen.CardW, EssayDifficultyScreen.CardH));
        var card = root.gameObject.AddComponent<DifficultyCard>();
        card.owner = owner; card.index = index; card.diff = d; card.color = col;
        card.Root = root; card.basePos = root.anchoredPosition;
        card.cg = root.gameObject.AddComponent<CanvasGroup>(); card.cg.alpha = 0f;

        Img(root, "Shadow", ThemeGfx.Glow, new Color(0, 0, 0, .6f), new Vector2(0, -328), new Vector2(540, 80));
        card.aura = Img(root, "Aura", ThemeGfx.Glow, col.WithAlpha(0f), new Vector2(0, 20), new Vector2(740, 1000));

        var frame = UIKit.CBox("Frame", root, Color.white, Vector2.zero, new Vector2(EssayDifficultyScreen.CardW, EssayDifficultyScreen.CardH), true).GetComponent<Image>();
        var fs = MenuArt.Get("px_frame");
        if (fs != null) { frame.sprite = fs; frame.type = Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 1f / 3f; }
        else frame.color = GameTheme.Night;

        // ---- vòng rune + biểu tượng
        var ringPos = new Vector2(0, 205);
        Img(root, "RuneGlow", ThemeGfx.Glow, col.WithAlpha(.40f), ringPos, new Vector2(250, 250));
        Img(root, "RuneDisc", RuneArt.Disk, new Color(.05f, .04f, .10f, .88f), ringPos, new Vector2(150, 150));
        Img(root, "RuneRing", RuneArt.Ring, GameTheme.Bronze, ringPos, new Vector2(158, 158));
        card.dash = Img(root, "RuneDash", RuneArt.DashRing, col.WithAlpha(.75f), ringPos, new Vector2(190, 190)).rectTransform;
        card.glowIcon = Img(root, "IconGlow", ThemeGfx.Glow, col.WithAlpha(.45f), ringPos, new Vector2(145, 145));
        var icon = UIKit.CBox("Icon", root, Color.clear, ringPos, new Vector2(100, 100));
        card.iconRt = icon;
        Img(icon, "Glyph", DifficultyTheme.Icon(d), col, Vector2.zero, new Vector2(100, 100));

        // ---- bậc: bốn viên ngọc, sáng bấy nhiêu viên
        for (int i = 0; i < 4; i++)
        {
            var g = UIKit.CBox("Pip" + i, root, i <= index ? col : GameTheme.Iron.WithAlpha(.7f), new Vector2((i - 1.5f) * 28f, 118f), new Vector2(12, 12));
            g.localEulerAngles = new Vector3(0, 0, 45f);
        }

        // ---- tên + chỉ số
        UIKit.Label("Name", root, name, 48, GameTheme.GoldHi, TextAnchor.MiddleCenter, new Vector2(0, 70), new Vector2(360, 70), true, true, FontStyle.Bold);
        UIKit.Divider(root, new Vector2(0, 26), 300);
        string[] names = { "Số quái", "Tốc độ quái", "Sát thương", "Đề bài", "Mực thưởng" };
        string[] vals =
        {
            Mul(RoomRules.CountMul(d)), Mul(RoomRules.SpeedMul(d)), Mul(RoomRules.DamageMul(d)),
            StepText[(int)d], Mul(RoomRules.InkMultiplier(d)),
        };
        for (int i = 0; i < names.Length; i++)
        {
            float y = -4f - i * 34f;
            UIKit.Label("K" + i, root, names[i], 24, GameTheme.Fog, TextAnchor.MiddleLeft, new Vector2(0, y), new Vector2(320, 32));
            UIKit.Label("V" + i, root, vals[i], 24, GameTheme.Bone, TextAnchor.MiddleRight, new Vector2(0, y), new Vector2(320, 32), false, false, FontStyle.Bold);
        }
        UIKit.Label("Tag", root, RoomRules.DifficultyTag(d), 22, col, TextAnchor.MiddleCenter, new Vector2(0, -190), new Vector2(330, 64), false, false, FontStyle.Italic);
        UIKit.MakeButton(root, "Btn_Choose", "CHỌN", new Vector2(0, -264), new Vector2(240, 66), () => owner.Select(card),
            d == EssayDifficulty.Expert ? ButtonStyle.Danger : ButtonStyle.Bronze, 32, .5f + index * .14f);

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
        var go = e.pointerCurrentRaycast.gameObject;
        if (go != null && go.transform.IsChildOf(transform)) return;      // rời sang nút con vẫn tính là đang rê trong thẻ
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
        Root.anchoredPosition = basePos + new Vector2(shakeX, -(1f - e) * 300f + bob * (1f - dismiss) - dismiss * 120f + chosen * 14f);
        float sc = Mathf.Lerp(.82f, 1f, e) * (1f + .06f * hover + .10f * chosen) * (1f - .18f * dismiss);
        Root.localScale = new Vector3(sc, sc, 1f);
        cg.alpha = Mathf.Clamp01(k * 1.4f) * (1f - dismiss);
        cg.blocksRaycasts = k > .6f && dismiss <= 0f;

        float pulse = .06f * Mathf.Sin(t * 2f + index);
        var ac = color; ac.a = Mathf.Clamp01(.10f + pulse + .32f * hover + .55f * chosen) * k; aura.color = ac;

        dash.localRotation = Quaternion.Euler(0, 0, -t * (14f + 12f * index + 70f * hover + 340f * chosen));     // càng khó vòng rune quay càng gấp
        var gc = glowIcon.color; gc.a = .40f + .12f * Mathf.Sin(t * 2.2f + index) + .25f * hover + .3f * chosen; glowIcon.color = gc;
        float breathe = 1f + .04f * Mathf.Sin(t * 2f + index) + .08f * hover + .12f * chosen;
        iconRt.localScale = new Vector3(breathe, breathe, 1f);

        for (int i = 0; i < embers.Length; i++)
        {
            float u = Mathf.Repeat(t * (.28f + .06f * index + .3f * hover) + emberPhase[i], 1f);
            embers[i].anchoredPosition = new Vector2(emberX[i] + Mathf.Sin(u * 7f + i * 2f) * 14f, 135f + u * 190f);
            var im = embers[i].GetComponent<Image>(); var ec = im.color; ec.a = Mathf.Sin(u * 3.1416f) * (.55f + .4f * hover) * k; im.color = ec;
        }
    }
}
