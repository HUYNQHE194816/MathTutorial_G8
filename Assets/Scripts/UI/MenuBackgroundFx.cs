using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Làm động ảnh nền MainMenu bằng các lớp hiệu ứng UI đặt lên đúng vị trí trong ảnh gốc 1024x576:
/// đèn nhấp nháy, tinh thể/lọ thuốc thở sáng, hơi nước + bọt khí bay lên, sao lấp lánh ngoài cửa sổ,
/// bụi sáng lơ lửng, robot chớp mắt, kim giây đồng hồ treo tường...
/// Gắn vào BG_MainMenu. Sprite glow/sparkle được tạo bằng code nên không cần thêm asset.
/// Mọi hiệu ứng nằm dưới nhóm nút (tự đặt làm sibling đầu tiên) và không chặn raycast.
/// Dùng unscaledTime nên vẫn chạy khi Time.timeScale = 0.
/// </summary>
public class MenuBackgroundFx : MonoBehaviour
{
    private const float SrcW = 1024f, SrcH = 576f;   // kích thước ảnh nền gốc
    private const float RefW = 1920f, RefH = 1080f;  // không gian canvas tham chiếu
    private const float K = RefW / SrcW;             // đổi px ảnh gốc -> px canvas

    [Range(0f, 2f)] [SerializeField] private float intensity = 1f;
    [SerializeField] private int dustCount = 34;
    [SerializeField] private bool showClockSecondHand = true;
    [SerializeField] private bool robotBlink = true;

    private RectTransform parentRt, root;
    private Sprite glowSprite, starSprite;

    // ---------- dữ liệu hiệu ứng ----------
    private class Lamp { public Image img; public RectTransform rt; public Vector2 size; public float a, speed, phase, flick; public bool flicker; }
    private class Twinkle { public Image img; public RectTransform rt; public float size, a, speed, phase, rot; }
    private class Particle { public Image img; public RectTransform rt; public float seed, prevAge, offX; }
    private class Emitter
    {
        public Vector2 origin; public float spread, rise, life, sway, s0, s1, a;
        public Color color; public List<Particle> items = new List<Particle>();
    }
    private class Mote { public Image img; public RectTransform rt; public Vector2 pos, vel; public float size, a, phase, speed; }

    private readonly List<Lamp> lamps = new List<Lamp>();
    private readonly List<Twinkle> twinkles = new List<Twinkle>();
    private readonly List<Emitter> emitters = new List<Emitter>();
    private readonly List<Mote> motes = new List<Mote>();

    private RectTransform secondHand;
    private Image[] eyeLids;
    private float nextBlink, blinkEnd;

    // ---------- khởi tạo ----------
    private void Awake()
    {
        parentRt = (RectTransform)transform;
        glowSprite = MakeGlow(128);
        starSprite = MakeStar(128);

        var go = new GameObject("FX_Background", typeof(RectTransform));
        root = (RectTransform)go.transform;
        root.SetParent(parentRt, false);
        root.SetAsFirstSibling();                       // nằm dưới Panel_MenuButton
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(RefW, RefH);

        BuildLamps();
        BuildTwinkles();
        BuildEmitters();
        BuildDust();
        if (showClockSecondHand) BuildClockHand();
        if (robotBlink) BuildRobotEyes();
        FitRoot();
    }

    private void BuildLamps()
    {
        var warm = new Color(1f, 0.78f, 0.38f);
        // đèn tường / đèn bàn / đèn trần: nhấp nháy ấm
        AddLamp(334, 121, 90, 90, warm, 0.55f, 1.7f, 0.5f, true);
        AddLamp(760, 119, 90, 90, warm, 0.55f, 1.9f, 0.5f, true);
        AddLamp(585, 222, 120, 100, warm, 0.40f, 1.3f, 0.35f, true);
        AddLamp(186, 50, 230, 70, new Color(1f, 0.9f, 0.55f), 0.35f, 1.1f, 0.25f, true);
        AddLamp(146, 280, 60, 60, new Color(1f, 0.95f, 0.5f), 0.65f, 3.4f, 0.8f, false);   // bóng đèn trên bảng
        // lọ thuốc trên kệ
        AddLamp(378, 161, 62, 70, new Color(0.45f, 1f, 0.55f), 0.38f, 1.1f, 0.5f, false);
        AddLamp(413, 161, 62, 70, new Color(1f, 0.92f, 0.4f), 0.38f, 1.3f, 0.5f, false);
        AddLamp(447, 161, 58, 70, new Color(0.4f, 0.95f, 0.95f), 0.38f, 1.0f, 0.5f, false);
        AddLamp(481, 162, 62, 70, new Color(0.85f, 0.45f, 1f), 0.38f, 1.2f, 0.5f, false);
        // tinh thể cạnh cửa sổ
        AddLamp(895, 192, 90, 110, new Color(0.9f, 0.45f, 1f), 0.60f, 1.4f, 0.5f, false);
        AddLamp(945, 172, 105, 130, new Color(0.45f, 0.92f, 1f), 0.60f, 1.2f, 0.5f, false);
        AddLamp(987, 190, 90, 110, new Color(1f, 0.82f, 0.3f), 0.60f, 1.6f, 0.5f, false);
        // tinh thể trong máy
        AddLamp(884, 332, 70, 80, new Color(1f, 0.45f, 0.9f), 0.65f, 1.8f, 0.5f, false);
        AddLamp(909, 342, 60, 66, new Color(1f, 0.9f, 0.35f), 0.65f, 2.1f, 0.5f, false);
        AddLamp(934, 340, 70, 80, new Color(0.4f, 0.9f, 1f), 0.65f, 1.5f, 0.5f, false);
        // tinh thể góc trái
        AddLamp(27, 458, 70, 90, new Color(0.6f, 0.8f, 1f), 0.45f, 1.3f, 0.5f, false);
        AddLamp(50, 487, 40, 40, new Color(1f, 0.55f, 1f), 0.5f, 1.8f, 0.5f, false);
        // màn hình, bình cây, ăng-ten robot
        AddLamp(655, 336, 150, 110, new Color(0.35f, 0.8f, 1f), 0.20f, 1.1f, 0.5f, false);
        AddLamp(975, 420, 100, 100, new Color(0.5f, 1f, 0.6f), 0.28f, 0.9f, 0.5f, false);
        AddLamp(737, 166, 28, 28, new Color(0.4f, 0.95f, 1f), 0.75f, 3.0f, 0.5f, false);
        // dung dịch trong bình nhỏ
        AddLamp(70, 462, 70, 60, new Color(0.45f, 1f, 0.4f), 0.30f, 1.0f, 0.5f, false);
        AddLamp(117, 457, 60, 50, new Color(0.75f, 0.4f, 1f), 0.30f, 1.2f, 0.5f, false);
    }

    private void BuildTwinkles()
    {
        // sao ngoài cửa sổ
        var stars = new[] { new Vector2(842, 115), new Vector2(877, 100), new Vector2(868, 135),
                            new Vector2(838, 168), new Vector2(895, 190), new Vector2(851, 215), new Vector2(882, 232) };
        foreach (var p in stars) AddTwinkle(p.x, p.y, Random.Range(10f, 18f), new Color(0.85f, 0.92f, 1f), 0.95f);

        // lấp lánh trên tinh thể
        AddTwinkle(940, 150, 22, new Color(0.8f, 1f, 1f), 0.95f);
        AddTwinkle(893, 176, 18, new Color(1f, 0.8f, 1f), 0.95f);
        AddTwinkle(990, 172, 18, new Color(1f, 0.95f, 0.7f), 0.95f);
        AddTwinkle(884, 318, 16, new Color(1f, 0.85f, 1f), 0.95f);
        AddTwinkle(934, 325, 16, new Color(0.8f, 1f, 1f), 0.95f);
        AddTwinkle(24, 435, 18, new Color(0.85f, 0.95f, 1f), 0.95f);
        AddTwinkle(50, 482, 12, new Color(1f, 0.85f, 1f), 0.95f);

        // ngôi sao vàng trên logo
        var gold = new Color(1f, 0.95f, 0.6f);
        AddTwinkle(282, 80, 26, gold, 0.9f);
        AddTwinkle(62, 192, 22, gold, 0.9f);
        AddTwinkle(80, 209, 16, gold, 0.9f);
        AddTwinkle(283, 199, 22, gold, 0.9f);
    }

    private void BuildEmitters()
    {
        // hơi nước bay lên từ bình Z
        AddEmitter(new Vector2(553, 298), 5f, 85f, 3.4f, 7f, 16f, 46f, 0.32f, new Color(0.9f, 0.97f, 1f), 7);
        // bọt khí bình Y (hồng)
        AddEmitter(new Vector2(505, 322), 7f, 42f, 2.3f, 4f, 5f, 8f, 0.65f, new Color(1f, 0.7f, 0.95f), 5);
        // bọt khí bình lớn màu xanh lá và bình tím (góc trái)
        AddEmitter(new Vector2(68, 452), 14f, 55f, 2.6f, 4f, 4f, 8f, 0.65f, new Color(0.65f, 1f, 0.55f), 6);
        AddEmitter(new Vector2(117, 452), 10f, 45f, 2.4f, 3f, 4f, 7f, 0.65f, new Color(0.85f, 0.6f, 1f), 4);
        // tia lửa nhỏ bay lên từ máy tinh thể
        AddEmitter(new Vector2(909, 326), 28f, 36f, 2.8f, 3f, 3f, 6f, 0.7f, new Color(1f, 0.95f, 0.7f), 5);
    }

    private void BuildDust()
    {
        for (int i = 0; i < dustCount; i++)
        {
            var m = new Mote();
            m.size = Random.Range(5f, 12f) * K;
            m.a = Random.Range(0.25f, 0.6f);
            m.phase = Random.Range(0f, 6.28f);
            m.speed = Random.Range(0.6f, 1.6f);
            m.pos = new Vector2(Random.Range(-RefW / 2f, RefW / 2f), Random.Range(-RefH / 2f, RefH / 2f));
            m.vel = new Vector2(Random.Range(4f, 14f), Random.Range(6f, 18f));
            m.img = NewImage("Dust", glowSprite, new Color(1f, 0.92f, 0.7f, 0f), out m.rt);
            m.rt.sizeDelta = Vector2.one * m.size;
            motes.Add(m);
        }
    }

    private void BuildClockHand()
    {
        var img = NewImage("ClockSecondHand", null, new Color(0.78f, 0.15f, 0.1f, 0.95f), out secondHand);
        secondHand.pivot = new Vector2(0.5f, 0f);
        secondHand.sizeDelta = new Vector2(1.8f * K, 17f * K);
        secondHand.anchoredPosition = Pos(718, 88);
        // tâm đồng hồ
        RectTransform hub;
        NewImage("ClockHub", glowSprite, new Color(0.35f, 0.1f, 0.05f, 1f), out hub);
        hub.sizeDelta = Vector2.one * 5f * K;
        hub.anchoredPosition = Pos(718, 88);
        secondHand.SetAsLastSibling();
    }

    private void BuildRobotEyes()
    {
        eyeLids = new Image[2];
        var xs = new[] { 712f, 733f };
        for (int i = 0; i < 2; i++)
        {
            RectTransform rt;
            eyeLids[i] = NewImage("RobotLid", null, new Color(0.11f, 0.16f, 0.23f, 0f), out rt);
            rt.sizeDelta = new Vector2(11f * K, 13f * K);
            rt.anchoredPosition = Pos(xs[i], 213);
        }
        nextBlink = Time.unscaledTime + Random.Range(2f, 4f);
    }

    // ---------- tạo phần tử ----------
    private void AddLamp(float x, float y, float w, float h, Color c, float a, float speed, float flick, bool flicker)
    {
        var l = new Lamp { size = new Vector2(w, h) * K, a = a, speed = speed, flick = flick, flicker = flicker, phase = Random.Range(0f, 100f) };
        l.img = NewImage("Glow", glowSprite, new Color(c.r, c.g, c.b, 0f), out l.rt);
        l.rt.anchoredPosition = Pos(x, y);
        l.rt.sizeDelta = l.size;
        lamps.Add(l);
    }

    private void AddTwinkle(float x, float y, float size, Color c, float a)
    {
        var t = new Twinkle { size = size * K, a = a, speed = Random.Range(1.2f, 2.6f), phase = Random.Range(0f, 6.28f), rot = Random.Range(-25f, 25f) };
        t.img = NewImage("Twinkle", starSprite, new Color(c.r, c.g, c.b, 0f), out t.rt);
        t.rt.anchoredPosition = Pos(x, y);
        t.rt.sizeDelta = Vector2.one * t.size;
        twinkles.Add(t);
    }

    private void AddEmitter(Vector2 origin, float spread, float rise, float life, float sway, float s0, float s1, float a, Color c, int count)
    {
        var e = new Emitter { origin = origin, spread = spread, rise = rise, life = life, sway = sway, s0 = s0 * K, s1 = s1 * K, a = a, color = c };
        for (int i = 0; i < count; i++)
        {
            var p = new Particle { seed = (float)i / count, offX = Random.Range(-spread, spread), prevAge = 0f };
            p.img = NewImage("Particle", glowSprite, new Color(c.r, c.g, c.b, 0f), out p.rt);
            e.items.Add(p);
        }
        emitters.Add(e);
    }

    private Image NewImage(string name, Sprite sprite, Color color, out RectTransform rt)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static Vector2 Pos(float x, float y)
    {
        return new Vector2((x / SrcW - 0.5f) * RefW, (0.5f - y / SrcH) * RefH);
    }

    // ---------- cập nhật ----------
    private void FitRoot()
    {
        // BG đã được AspectRatioFitter(Envelope) giữ tỉ lệ 16:9 -> chỉ cần scale đều theo chiều rộng
        float s = parentRt.rect.width / RefW;
        if (s > 0.001f) root.localScale = new Vector3(s, s, 1f);
    }

    private void LateUpdate()
    {
        FitRoot();
        float t = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        foreach (var l in lamps)
        {
            float k;
            if (l.flicker)
            {
                float n = Mathf.PerlinNoise(t * l.speed * 2.2f, l.phase);
                k = 1f - l.flick + l.flick * 2f * n;
            }
            else
            {
                float s = 0.5f + 0.5f * Mathf.Sin(t * l.speed + l.phase);
                k = 0.4f + 0.6f * s;
            }
            SetAlpha(l.img, l.a * k * intensity);
            l.rt.sizeDelta = l.size * (0.92f + 0.12f * k);
        }

        foreach (var w in twinkles)
        {
            float s = Mathf.Max(0f, Mathf.Sin(t * w.speed + w.phase));
            s = s * s * s;
            SetAlpha(w.img, w.a * s * intensity);
            w.rt.localScale = Vector3.one * (0.5f + 0.7f * s);
            w.rt.localRotation = Quaternion.Euler(0f, 0f, t * w.rot);
        }

        foreach (var e in emitters)
        {
            foreach (var p in e.items)
            {
                float age = ((t / e.life) + p.seed) % 1f;
                if (age < p.prevAge) p.offX = Random.Range(-e.spread, e.spread);   // sang chu kỳ mới -> đổi vị trí xuất phát
                p.prevAge = age;

                float x = e.origin.x + p.offX + Mathf.Sin(age * 6.28f * 1.5f + p.seed * 10f) * e.sway;
                float y = e.origin.y - e.rise * age;
                p.rt.anchoredPosition = Pos(x, y);
                p.rt.sizeDelta = Vector2.one * Mathf.Lerp(e.s0, e.s1, age);
                float fade = Mathf.Sin(age * Mathf.PI);
                SetAlpha(p.img, e.a * fade * intensity);
            }
        }

        float hx = RefW / 2f + 60f, hy = RefH / 2f + 60f;
        foreach (var m in motes)
        {
            m.pos += (m.vel + new Vector2(Mathf.Sin(t * 0.7f + m.phase) * 10f, 0f)) * dt;
            if (m.pos.x > hx) m.pos.x = -hx;
            if (m.pos.y > hy) m.pos.y = -hy;
            m.rt.anchoredPosition = m.pos;
            float tw = 0.5f + 0.5f * Mathf.Sin(t * m.speed + m.phase);
            SetAlpha(m.img, m.a * tw * intensity);
        }

        if (secondHand != null)
        {
            // kim giây nhảy từng nấc 6 độ, có chút nảy
            float sec = t % 60f;
            float step = Mathf.Floor(sec);
            float frac = sec - step;
            float bounce = frac < 0.12f ? Mathf.Sin(frac / 0.12f * Mathf.PI) * 1.5f : 0f;
            secondHand.localRotation = Quaternion.Euler(0f, 0f, -(step * 6f + bounce));
        }

        if (eyeLids != null)
        {
            if (t >= nextBlink) { blinkEnd = t + 0.14f; nextBlink = t + Random.Range(2.5f, 5.5f); }
            float a = t < blinkEnd ? 1f : 0f;
            foreach (var lid in eyeLids) SetAlpha(lid, a);
        }
    }

    private static void SetAlpha(Image img, float a)
    {
        var c = img.color; c.a = Mathf.Clamp01(a); img.color = c;
    }

    private void OnDestroy()
    {
        if (glowSprite != null) Destroy(glowSprite.texture);
        if (starSprite != null) Destroy(starSprite.texture);
    }

    // ---------- sprite sinh bằng code ----------
    private static Sprite MakeGlow(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);              // smoothstep: viền mềm
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite MakeStar(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) / n * 2f - 1f), dy = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                float d = Mathf.Sqrt(dx) + Mathf.Sqrt(dy);                 // hình sao 4 cánh (astroid)
                float arms = Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float core = Mathf.Pow(Mathf.Clamp01(1f - r * 2.2f), 2f);   // lõi sáng ở giữa
                float a = Mathf.Clamp01(arms + core);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
