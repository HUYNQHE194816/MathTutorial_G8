using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Giao diện riêng của từng môn cho màn Chọn chương / Chọn bài:
///   Lý   = bảng tối viền neon + góc ngoặc, nền có nguyên tử quay.
///   Hóa  = ống đong có vạch chia + chất lỏng gợn sóng, nền có bong bóng nổi lên.
///   Sinh = khung lá xanh viền vàng + kim cương ở góc, thẻ đung đưa, nền có lá rơi.
/// </summary>
public class FlowTheme
{
    public SubjectId Id; public string Name; public FlowFx.Mode Mode;
    public Color BgTint, Frame, Frame2, Paper, Accent, TextMain, TextSub, Muted;

    public static FlowTheme Get(SubjectId s)
    {
        switch (s)
        {
            case SubjectId.Ly:
                return new FlowTheme { Id = s, Name = "Vật lí", Mode = FlowFx.Mode.Atoms, BgTint = new Color(.82f, .78f, 1f),
                    Frame = new Color(.35f, .95f, 1f), Frame2 = new Color(1f, .35f, .85f), Paper = new Color(.1f, .09f, .27f),
                    Accent = new Color(.5f, .35f, .92f), TextMain = Color.white, TextSub = new Color(.78f, .85f, 1f), Muted = new Color(.55f, .58f, .75f) };
            case SubjectId.Hoa:
                return new FlowTheme { Id = s, Name = "Hóa học", Mode = FlowFx.Mode.Bubbles, BgTint = new Color(.7f, 1f, 1f),
                    Frame = new Color(.1f, .66f, .7f), Frame2 = new Color(.85f, .2f, .5f), Paper = new Color(.95f, 1f, 1f),
                    Accent = new Color(.1f, .66f, .7f), TextMain = new Color(.04f, .32f, .36f), TextSub = new Color(.15f, .3f, .35f), Muted = new Color(.45f, .55f, .58f) };
            default:
                return new FlowTheme { Id = SubjectId.Sinh, Name = "Sinh học", Mode = FlowFx.Mode.Leaves, BgTint = new Color(.78f, 1f, .82f),
                    Frame = new Color(.18f, .62f, .31f), Frame2 = SinhMenuKit.Gold, Paper = SinhMenuKit.Cream,
                    Accent = new Color(.18f, .62f, .31f), TextMain = new Color(.12f, .45f, .22f), TextSub = SinhMenuKit.Ink, Muted = new Color(.45f, .4f, .35f) };
        }
    }

    public Button Back(Transform root, string label, Action cb) =>
        SinhMenuKit.MakeButton("Back", root, new Vector2(-740, 462), new Vector2(400, 84), Paper, label, 32, TextMain, cb, Frame);

    // ------------------------------------------------------------------ khung thẻ
    /// <summary>Dựng khung thẻ đúng phong cách môn; trả về root (Image bấm được) và vùng content để đặt chữ.</summary>
    public RectTransform Decorate(Transform parent, string name, out RectTransform content)
    {
        var root = SinhMenuKit.CBox(name, parent, Frame, Vector2.zero, Vector2.zero, true);
        switch (Id)
        {
            case SubjectId.Ly: DecorateLy(root, out content); break;
            case SubjectId.Hoa: DecorateHoa(root, out content); break;
            default: DecorateSinh(root, out content); break;
        }
        return root;
    }

    static RectTransform Fill(string n, Transform p, Color c, float l, float t, float r, float b)
    { var x = SinhMenuKit.CBox(n, p, c, Vector2.zero, Vector2.zero); SinhMenuKit.Stretch(x, l, t, r, b); return x; }

    static Image[] Outline(Transform p, float inset, float th, Color c)
    {
        var top = SinhMenuKit.Box("Top", p, c, new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), new Vector2(0, -inset), new Vector2(-2 * inset, th));
        var bot = SinhMenuKit.Box("Bottom", p, c, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), new Vector2(0, inset), new Vector2(-2 * inset, th));
        var lef = SinhMenuKit.Box("Left", p, c, Vector2.zero, new Vector2(0, 1), new Vector2(0, .5f), new Vector2(inset, 0), new Vector2(th, -2 * inset));
        var rig = SinhMenuKit.Box("Right", p, c, new Vector2(1, 0), Vector2.one, new Vector2(1, .5f), new Vector2(-inset, 0), new Vector2(th, -2 * inset));
        return new[] { top.GetComponent<Image>(), bot.GetComponent<Image>(), lef.GetComponent<Image>(), rig.GetComponent<Image>() };
    }

    // Lý: ngoặc góc kiểu HUD
    static void Brackets(Transform p, float inset, float len, float th, Color c)
    {
        for (int i = 0; i < 4; i++)
        {
            float ax = i % 2, ay = i / 2; float sx = ax == 0 ? 1 : -1, sy = ay == 0 ? 1 : -1;
            var a = new Vector2(ax, ay);
            SinhMenuKit.Box("BrH" + i, p, c, a, a, a, new Vector2(sx * inset, sy * inset), new Vector2(len, th));
            SinhMenuKit.Box("BrV" + i, p, c, a, a, a, new Vector2(sx * inset, sy * inset), new Vector2(th, len));
        }
    }

    void DecorateLy(RectTransform root, out RectTransform content)
    {
        var plate = Fill("Plate", root, Paper, 4, 4, 4, 4);
        var line = Outline(plate, 12, 3, Frame2);
        Brackets(plate, 6, 40, 6, Frame);
        var pulse = root.gameObject.AddComponent<PulseGlow>(); pulse.imgs = line; pulse.speed = 2.4f; pulse.minAlpha = .3f; pulse.phase = UnityEngine.Random.value * 6f;
        content = Fill("Content", plate, Color.clear, 26, 26, 26, 26);
    }

    void DecorateHoa(RectTransform root, out RectTransform content)
    {
        var plate = Fill("Plate", root, Paper, 8, 8, 8, 8);
        var liquid = SinhMenuKit.Box("Liquid", plate, new Color(Accent.r, Accent.g, Accent.b, .2f), Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(0, 70));
        SinhMenuKit.Box("Surface", liquid, new Color(Accent.r, Accent.g, Accent.b, .55f), new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 4));
        var w = liquid.gameObject.AddComponent<Wobble>(); w.rt = liquid; w.baseH = 70f; w.amp = 9f; w.speed = 1.8f; w.phase = UnityEngine.Random.value * 6f;
        for (int i = 0; i < 9; i++)   // vạch chia ống đong
        {
            float y = (i + 1) / 10f; bool major = i % 2 == 0;
            SinhMenuKit.Box("Tick" + i, plate, Frame, new Vector2(0, y), new Vector2(0, y), new Vector2(0, .5f), new Vector2(14, 0), new Vector2(major ? 34 : 20, 4));
        }
        SinhMenuKit.Box("Lip", plate, Frame, new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 10));
        content = Fill("Content", plate, Color.clear, 60, 22, 22, 22);
    }

    void DecorateSinh(RectTransform root, out RectTransform content)
    {
        var plate = Fill("Plate", root, new Color(.6f, .84f, .52f), 7, 7, 7, 7);
        var paper = Fill("Paper", plate, Paper, 7, 7, 7, 7);
        for (int i = 0; i < 4; i++)   // kim cương vàng ở 4 góc
        {
            float ax = i % 2, ay = i / 2; float sx = ax == 0 ? 1 : -1, sy = ay == 0 ? 1 : -1;
            var a = new Vector2(ax, ay);
            var gem = SinhMenuKit.Box("Gem" + i, root, Frame2, a, a, SinhMenuKit.C, new Vector2(sx * 14, sy * 14), new Vector2(32, 32));
            gem.localEulerAngles = new Vector3(0, 0, 45);
        }
        for (int i = 0; i < 2; i++)   // hai chiếc lá nhô lên giữa cạnh trên
        {
            float s = i == 0 ? -1 : 1;
            var leaf = SinhMenuKit.Box("Leaf" + i, root, new Color(.22f, .7f, .34f), new Vector2(.5f, 1), new Vector2(.5f, 1), SinhMenuKit.C, new Vector2(s * 34, 2), new Vector2(66, 24));
            leaf.GetComponent<Image>().sprite = ProcSprites.Circle; leaf.localEulerAngles = new Vector3(0, 0, -s * 28);
        }
        var sway = root.gameObject.AddComponent<Sway>(); sway.amp = 1.1f; sway.speed = 1.3f; sway.phase = UnityEngine.Random.value * 6f;
        content = Fill("Content", paper, Color.clear, 22, 22, 22, 22);
    }
}

// ============================================================ hiệu ứng nền
public class FlowFx : MonoBehaviour
{
    public enum Mode { Atoms, Bubbles, Leaves }

    class P { public RectTransform rt; public Image img; public float a, b, c, d, e, f; }

    Mode mode; readonly List<P> ps = new List<P>(); readonly List<RectTransform> rings = new List<RectTransform>();
    readonly List<P> electrons = new List<P>(); P nucleus;
    static readonly Vector2 Center = new Vector2(0, -20);

    public static FlowFx Create(Transform canvasRoot, Mode m)
    {
        var go = new GameObject("FX", typeof(RectTransform)); go.transform.SetParent(canvasRoot, false);
        SinhMenuKit.Stretch((RectTransform)go.transform, 0, 0, 0, 0);
        go.transform.SetSiblingIndex(1);   // ngay trên nền, dưới mọi thẻ
        var fx = go.AddComponent<FlowFx>(); fx.mode = m; fx.Build();
        return fx;
    }

    static P Make(Transform parent, string n, Sprite sp, Color col, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var im = go.GetComponent<Image>(); im.sprite = sp; im.color = col; im.raycastTarget = false;
        var rt = (RectTransform)go.transform; SinhMenuKit.Place(rt, SinhMenuKit.C, SinhMenuKit.C, SinhMenuKit.C, Vector2.zero, size);
        return new P { rt = rt, img = im };
    }

    static float R(float a, float b) { return UnityEngine.Random.Range(a, b); }

    void Build()
    {
        if (mode == Mode.Bubbles)
            for (int i = 0; i < 20; i++)
            {
                float sz = R(18f, 74f);
                var p = Make(transform, "Bubble", ProcSprites.Ring, new Color(1, 1, 1, R(.35f, .65f)), new Vector2(sz, sz));
                var hl = Make(p.rt, "Hl", ProcSprites.Circle, new Color(1, 1, 1, .8f), new Vector2(sz * .16f, sz * .16f)); hl.rt.anchoredPosition = new Vector2(-sz * .22f, sz * .22f);
                p.a = R(-940f, 940f); p.b = R(-600f, 600f); p.c = R(40f, 120f); p.d = R(0f, 6.28f); p.e = R(10f, 40f);
                ps.Add(p);
            }
        else if (mode == Mode.Leaves)
            for (int i = 0; i < 16; i++)
            {
                var p = Make(transform, "Leaf", ProcSprites.Circle, Color.Lerp(new Color(.25f, .72f, .35f, .85f), new Color(.78f, .9f, .35f, .85f), R(0f, 1f)), new Vector2(R(34f, 58f), R(14f, 22f)));
                p.a = R(-940f, 940f); p.b = R(-600f, 600f); p.c = R(50f, 110f); p.d = R(0f, 6.28f); p.e = R(30f, 80f); p.f = R(0f, 360f);
                p.img.rectTransform.localEulerAngles = new Vector3(0, 0, p.f);
                ps.Add(p);
            }
        else
        {
            float[] angles = { 0f, 60f, 120f };
            for (int r = 0; r < 3; r++)
            {
                var ring = new GameObject("Orbit" + r, typeof(RectTransform)); ring.transform.SetParent(transform, false);
                var rrt = (RectTransform)ring.transform; SinhMenuKit.Place(rrt, SinhMenuKit.C, SinhMenuKit.C, SinhMenuKit.C, Center, Vector2.zero);
                rrt.localEulerAngles = new Vector3(0, 0, angles[r]); rings.Add(rrt);
                for (int k = 0; k < 72; k++)
                {
                    float t = k / 72f * Mathf.PI * 2f;
                    var d = Make(rrt, "d", ProcSprites.Circle, new Color(.6f, .85f, 1f, .28f), new Vector2(7, 7));
                    d.rt.anchoredPosition = new Vector2(Mathf.Cos(t) * 470f, Mathf.Sin(t) * 175f);
                }
                Color[] ec = { new Color(.4f, 1f, 1f, .95f), new Color(1f, .4f, .9f, .95f), new Color(1f, 1f, .7f, .95f) };
                var e = Make(transform, "Electron", ProcSprites.Glow, ec[r], new Vector2(56, 56)); e.a = r; e.b = r * 2.1f; e.c = R(.8f, 1.2f);
                electrons.Add(e);
            }
            nucleus = Make(transform, "Nucleus", ProcSprites.Glow, new Color(.6f, .9f, 1f, .6f), new Vector2(230, 230)); nucleus.rt.anchoredPosition = Center;
            for (int i = 0; i < 26; i++)
            {
                var s = Make(transform, "Star", ProcSprites.Circle, new Color(1, 1, 1, .4f), new Vector2(5, 5));
                s.rt.anchoredPosition = new Vector2(R(-940f, 940f), R(-520f, 520f)); s.d = R(0f, 6.28f); ps.Add(s);
            }
        }
    }

    void Update()
    {
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        if (mode == Mode.Bubbles)
            foreach (var p in ps)
            {
                p.b += p.c * dt;
                if (p.b > 620f) { p.b = -620f; p.a = UnityEngine.Random.Range(-940f, 940f); }
                p.rt.anchoredPosition = new Vector2(p.a + Mathf.Sin(t * .9f + p.d) * p.e, p.b);
            }
        else if (mode == Mode.Leaves)
            foreach (var p in ps)
            {
                p.b -= p.c * dt; p.f += p.e * dt * (Mathf.Sin(t + p.d) > 0 ? 1f : -1f);
                if (p.b < -600f) { p.b = 620f; p.a = UnityEngine.Random.Range(-940f, 940f); }
                p.rt.anchoredPosition = new Vector2(p.a + Mathf.Sin(t * .8f + p.d) * 70f, p.b);
                p.rt.localEulerAngles = new Vector3(0, 0, p.f);
            }
        else
        {
            float[] baseA = { 0f, 60f, 120f };
            for (int r = 0; r < rings.Count; r++) rings[r].localEulerAngles = new Vector3(0, 0, baseA[r] + t * 7f);
            foreach (var e in electrons)
            {
                int r = (int)e.a; float ang = (baseA[r] + t * 7f) * Mathf.Deg2Rad, a = t * e.c * 1.1f + e.b;
                var local = new Vector2(Mathf.Cos(a) * 470f, Mathf.Sin(a) * 175f);
                e.rt.anchoredPosition = Center + new Vector2(local.x * Mathf.Cos(ang) - local.y * Mathf.Sin(ang), local.x * Mathf.Sin(ang) + local.y * Mathf.Cos(ang));
            }
            float pulse = 1f + .08f * Mathf.Sin(t * 2.2f); nucleus.rt.localScale = new Vector3(pulse, pulse, 1);
            foreach (var s in ps) { var c = s.img.color; c.a = .12f + .55f * (Mathf.Sin(t * 1.6f + s.d) + 1f) / 2f; s.img.color = c; }
        }
    }
}

/// <summary>Làm viền nhấp nháy (neon).</summary>
public class PulseGlow : MonoBehaviour
{
    public Image[] imgs; public float speed = 2f, minAlpha = .3f, phase; Color[] baseCol;
    void Start() { if (imgs == null) return; baseCol = new Color[imgs.Length]; for (int i = 0; i < imgs.Length; i++) baseCol[i] = imgs[i].color; }
    void Update()
    {
        if (baseCol == null) return;
        float k = Mathf.Lerp(minAlpha, 1f, (Mathf.Sin(Time.unscaledTime * speed + phase) + 1f) / 2f);
        for (int i = 0; i < imgs.Length; i++) { var c = baseCol[i]; c.a = k; imgs[i].color = c; }
    }
}

/// <summary>Mặt chất lỏng gợn lên xuống.</summary>
public class Wobble : MonoBehaviour
{
    public RectTransform rt; public float baseH = 70f, amp = 8f, speed = 1.8f, phase;
    void Update() { if (rt != null) rt.sizeDelta = new Vector2(rt.sizeDelta.x, baseH + Mathf.Sin(Time.unscaledTime * speed + phase) * amp); }
}

/// <summary>Thẻ đung đưa nhẹ như lá.</summary>
public class Sway : MonoBehaviour
{
    public float amp = 1f, speed = 1.2f, phase;
    void Update() { transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * speed + phase) * amp); }
}
