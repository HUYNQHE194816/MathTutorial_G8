using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hiệu ứng hình ảnh của trận đánh: hạt (tia lửa, khói, mảnh vỡ), vòng sóng, nổ, vệt chém, tia sét, vết máu quái, rung màn hình.
/// Mọi hạt dùng pool nên chém bắn dày đặc vẫn mượt. Hạt sáng dùng chất liệu cộng sáng (Gfx.Additive).
/// </summary>
public class RFxHost : MonoBehaviour
{
    class Pt
    {
        public Transform t; public SpriteRenderer sr; public Vector2 pos, vel; public float age, life, rot, spin, grav, drag, fadeIn;
        public Vector2 s0, s1; public Color c0, c1; public bool on, add;
    }

    public static RFxHost I { get; private set; }
    readonly List<Pt> live = new List<Pt>(); readonly Stack<Pt> pool = new Stack<Pt>();
    readonly Queue<GameObject> decals = new Queue<GameObject>();
    float trauma, shakeT; Vector3 camBase = new Vector3(0, 0, -10f); float baseSize = 5.4f, punch;
    Vector2 camOffset, camOffsetTarget; float camOffsetSpeed = 3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { I = null; }

    public static RFxHost Ensure(Transform parent)
    {
        if (I != null) return I;
        var go = new GameObject("RFx"); if (parent != null) go.transform.SetParent(parent, false);
        I = go.AddComponent<RFxHost>(); return I;
    }

    void OnDestroy() { if (I == this) I = null; }

    public void SetCamera(Vector3 basePos, float size) { camBase = basePos; baseSize = size; }

    public void Spawn(Sprite spr, Vector2 pos, Vector2 vel, float rot, float spin, Vector2 s0, Vector2 s1, Color c0, Color c1, float life, float grav, float drag, bool add, int order, float fadeIn = 0f)
    {
        Pt p;
        if (pool.Count > 0) p = pool.Pop();
        else
        {
            if (live.Count > 900) return;
            var go = new GameObject("p"); go.transform.SetParent(transform, false);
            p = new Pt { t = go.transform, sr = go.AddComponent<SpriteRenderer>() };
        }
        p.sr.sprite = spr; p.sr.sharedMaterial = add ? Gfx.Additive : Gfx.Alpha; p.sr.sortingOrder = order; p.sr.enabled = true;
        p.pos = pos; p.vel = vel; p.rot = rot; p.spin = spin; p.s0 = s0; p.s1 = s1; p.c0 = c0; p.c1 = c1; p.life = Mathf.Max(.02f, life); p.age = 0f; p.grav = grav; p.drag = drag; p.add = add; p.fadeIn = fadeIn; p.on = true;
        Apply(p, 0f); live.Add(p);
    }

    void Apply(Pt p, float k)
    {
        p.t.position = new Vector3(p.pos.x, p.pos.y, 0f); p.t.localEulerAngles = new Vector3(0f, 0f, p.rot);
        var s = Vector2.Lerp(p.s0, p.s1, k); p.t.localScale = new Vector3(s.x, s.y, 1f);
        var c = Color.Lerp(p.c0, p.c1, k); float a = c.a * (1f - k * k); if (p.fadeIn > 0f) a *= Mathf.Clamp01(p.age / p.fadeIn); c.a = a; p.sr.color = c;
    }

    void Update()
    {
        float dt = Combat.VDt;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var p = live[i]; p.age += dt; float k = p.age / p.life;
            if (k >= 1f) { p.sr.enabled = false; p.on = false; pool.Push(p); live.RemoveAt(i); continue; }
            p.vel *= Mathf.Exp(-p.drag * dt); p.vel.y -= p.grav * dt; p.pos += p.vel * dt; p.rot += p.spin * dt; Apply(p, k);
        }
        // decal mờ dần chậm
        // (xử lý bởi chính decal, xem Decal())
    }

    Transform follow; Vector2 camPos; bool camInit;

    /// <summary>Camera đi theo mục tiêu (người chơi), bị giới hạn trong sân; sân nhỏ hơn khung nhìn thì căn giữa. Vị trí khớp lưới texel để pixel không rung.</summary>
    public void SetFollow(Transform t, bool snap = false) { follow = t; if (snap) camInit = false; }

    float PixelPerfectSize(Camera cam)
    {
        float h = Mathf.Max(360f, Screen.height); int k = Mathf.Max(2, Mathf.RoundToInt(h / 360f));       // mỗi texel (1/32 đơn vị) rộng k pixel màn hình
        float s = h / (2f * 32f * k); float aspect = cam.aspect > .1f ? cam.aspect : 1.78f;
        return Mathf.Max(s, 9.3f / aspect);                                                              // màn hình hẹp (dọc) thì nới ra cho đủ bề ngang
    }

    void LateUpdate()
    {
        var cam = Camera.main; if (cam == null) return;
        float dt = Time.unscaledDeltaTime; trauma = Mathf.Max(0f, trauma - dt * 1.6f); punch = Mathf.MoveTowards(punch, 0f, dt * 2.2f);
        camOffset = Vector2.Lerp(camOffset, camOffsetTarget, 1f - Mathf.Exp(-camOffsetSpeed * dt));
        float S = PixelPerfectSize(cam); float hh = S, hw = S * (cam.aspect > .1f ? cam.aspect : 1.78f);
        Vector2 want = follow != null ? (Vector2)follow.position : new Vector2(0f, RoomManager.CenterY);
        float mx = RoomManager.HalfW + 1.0f - hw, my = RoomManager.HalfH + 1.3f - hh;
        float tx = mx <= 0f ? 0f : Mathf.Clamp(want.x, -mx, mx), ty = my <= 0f ? RoomManager.CenterY : Mathf.Clamp(want.y, RoomManager.CenterY - my, RoomManager.CenterY + my);
        if (!camInit) { camPos = new Vector2(tx, ty); camInit = true; } else camPos = Vector2.Lerp(camPos, new Vector2(tx, ty), 1f - Mathf.Exp(-7f * dt));
        float sh = trauma * trauma; shakeT += dt * 60f;
        Vector2 off = new Vector2((Mathf.PerlinNoise(shakeT, 1f) - .5f), (Mathf.PerlinNoise(1f, shakeT) - .5f)) * 2f * sh * .55f;
        Vector2 p = camPos + camOffset + off;
        p.x = Mathf.Round(p.x * 32f) / 32f; p.y = Mathf.Round(p.y * 32f) / 32f;                          // khớp lưới texel
        cam.transform.position = new Vector3(p.x, p.y, -10f);
        cam.orthographicSize = S - punch * .25f;
    }

    public void AddTrauma(float t) { trauma = Mathf.Clamp01(Mathf.Max(trauma, trauma + t)); }
    public void Punch(float z) { punch = Mathf.Max(punch, z); }
    public void LookAt(Vector2 off, float speed = 3f) { camOffsetTarget = off; camOffsetSpeed = speed; }

    public void AddDecal(GameObject d) { decals.Enqueue(d); while (decals.Count > 70) { var o = decals.Dequeue(); if (o != null) Destroy(o); } }
}

public static class RFx
{
    static Sprite Disc => RogueArt.Disc;
    static Sprite Glow => RogueArt.Glow;
    static RFxHost H => RFxHost.I;
    static Color A(Color c, float a) { c.a = a; return c; }

    public static void Shake(float amount, float dur = 0f) { if (H != null) H.AddTrauma(amount); }
    public static void Punch(float z) { if (H != null) H.Punch(z); }

    /// <summary>Chùm hạt sáng tỏa ra (tia lửa, mảnh phép thuật).</summary>
    public static void Burst(Vector2 pos, Color col, int n, float speed, float life, float size, float grav = 0f, bool add = true, Sprite spr = null)
    {
        if (H == null) return; var s = spr ?? Disc;
        for (int i = 0; i < n; i++)
        {
            float a = Random.Range(0f, 360f); float sp = speed * Random.Range(.4f, 1f);
            var v = Combat.FromAng(a) * sp; float sz = size * Random.Range(.6f, 1.2f);
            H.Spawn(s, pos + Random.insideUnitCircle * .08f, v, a, 0f, new Vector2(sz, sz), new Vector2(sz * .2f, sz * .2f), col, A(col, 0f), life * Random.Range(.7f, 1.2f), grav, 2.2f, add, Combat.OrdFx);
        }
    }

    /// <summary>Tia lửa bắn về một hướng (va chạm đạn, chém trúng).</summary>
    public static void Sparks(Vector2 pos, Vector2 dir, Color col, int n = 6, float speed = 7f, float spread = 55f)
    {
        if (H == null) return; float b = Combat.Ang(dir);
        for (int i = 0; i < n; i++)
        {
            float a = b + Random.Range(-spread, spread); float sp = speed * Random.Range(.5f, 1.1f);
            H.Spawn(RogueArt.Beam, pos, Combat.FromAng(a) * sp, a, 0f, new Vector2(.28f, .06f), new Vector2(.05f, .03f), Color.Lerp(col, Color.white, .5f), A(col, 0f), Random.Range(.14f, .3f), 0f, 3f, true, Combat.OrdFx + 2);
        }
    }

    public static void Puff(Vector2 pos, Color col, int n = 6, float size = .5f)
    {
        if (H == null) return;
        for (int i = 0; i < n; i++)
        {
            float a = Random.Range(0f, 360f); var v = Combat.FromAng(a) * Random.Range(.4f, 1.6f) + Vector2.up * .6f; float sz = size * Random.Range(.7f, 1.3f);
            H.Spawn(Disc, pos + Random.insideUnitCircle * .2f, v, 0f, 0f, new Vector2(sz * .5f, sz * .5f), new Vector2(sz * 1.6f, sz * 1.6f), A(col, .5f), A(col, 0f), Random.Range(.5f, .95f), -.3f, 1.4f, false, Combat.OrdFx - 5);
        }
    }

    public static void Ring(Vector2 pos, float radius, Color col, float dur = .35f, bool add = true)
    {
        if (H == null) return;
        H.Spawn(RogueArt.Ring, pos, Vector2.zero, 0f, 0f, new Vector2(.5f, .5f), new Vector2(radius * 2f, radius * 2f), A(col, .95f), A(col, 0f), dur, 0f, 0f, add, Combat.OrdFx - 2);
    }

    public static void Flash(Vector2 pos, float size, Color col, float dur = .14f)
    {
        if (H == null) return;
        H.Spawn(Glow, pos, Vector2.zero, 0f, 0f, new Vector2(size, size), new Vector2(size * .6f, size * .6f), A(col, .95f), A(col, 0f), dur, 0f, 0f, true, Combat.OrdFx + 1);
    }

    /// <summary>Cung chém: tỏa ra từ vị trí tay, quét ngang theo hướng dir; flip đổi chiều quét.</summary>
    public static void Slash(Vector2 pos, Vector2 dir, float length, Color col, float dur = .2f, bool flip = false, float width = 1f)
    {
        if (H == null) return; float a = Combat.Ang(dir); float sc = length / 4f;
        H.Spawn(RogueArt.SlashArc, pos, dir * 1.2f, a + (flip ? 35f : -35f), flip ? -170f / dur * .4f : 170f / dur * .4f, new Vector2(sc * .7f, sc * width * (flip ? -1f : 1f) * .8f), new Vector2(sc * 1.05f, sc * width * (flip ? -1f : 1f)),
            Color.Lerp(col, Color.white, .55f), A(col, 0f), dur, 0f, 3f, true, Combat.OrdFx + 3);
        H.Spawn(RogueArt.SlashArc, pos, dir * .8f, a + (flip ? 35f : -35f), flip ? -170f / dur * .4f : 170f / dur * .4f, new Vector2(sc * .8f, sc * width * (flip ? -1f : 1f) * .9f), new Vector2(sc * 1.2f, sc * width * (flip ? -1f : 1f) * 1.1f),
            A(col, .55f), A(col, 0f), dur * 1.4f, 0f, 3f, true, Combat.OrdFx + 2);
    }

    /// <summary>Quét tròn 360° của rìu.</summary>
    public static void Spin(Vector2 pos, float radius, Color col, float dur = .28f)
    {
        if (H == null) return;
        for (int k = 0; k < 3; k++)
            H.Spawn(RogueArt.SlashArc, pos, Vector2.zero, k * 120f, 900f, new Vector2(radius * .22f, radius * .22f), new Vector2(radius * .27f, radius * .27f), Color.Lerp(col, Color.white, .5f), A(col, 0f), dur, 0f, 0f, true, Combat.OrdFx + 3);
        Ring(pos, radius, col, dur);
    }

    /// <summary>Vụ nổ hoàn chỉnh: chớp sáng, vòng sóng, hạt lửa, khói, rung màn hình.</summary>
    public static void Blast(Vector2 pos, float radius, Color col, bool shake = true)
    {
        if (H == null) return;
        Flash(pos, radius * 2.6f, Color.Lerp(col, Color.white, .5f), .2f);
        Ring(pos, radius, col, .35f);
        Burst(pos, col, Mathf.RoundToInt(10 + radius * 6f), 4f + radius * 2.5f, .55f, .2f, 2f);
        Burst(pos, Color.white, 6, 3f + radius, .3f, .16f);
        Puff(pos, new Color(.2f, .18f, .24f), Mathf.RoundToInt(4 + radius * 2f), radius * .5f);
        if (shake) { Shake(Mathf.Clamp01(.18f + radius * .12f)); }
        Scorch(pos, radius * .8f);
    }

    public static void Lightning(Vector2 a, Vector2 b, Color col, float dur = .2f, float width = .1f)
    {
        if (H == null) return;
        Vector2 d = b - a; float len = d.magnitude; if (len < .05f) return; Vector2 dir = d / len, nrm = new Vector2(-dir.y, dir.x);
        int seg = Mathf.Max(3, Mathf.CeilToInt(len / .7f)); Vector2 prev = a;
        for (int i = 1; i <= seg; i++)
        {
            Vector2 p = a + dir * (len * i / seg) + (i < seg ? nrm * Random.Range(-.35f, .35f) : Vector2.zero);
            Vector2 sd = p - prev; float sl = sd.magnitude;
            H.Spawn(RogueArt.Beam, prev, Vector2.zero, Combat.Ang(sd), 0f, new Vector2(sl / .5f, width), new Vector2(sl / .5f, width * .3f), Color.Lerp(col, Color.white, .6f), A(col, 0f), dur, 0f, 0f, true, Combat.OrdFx + 4);
            H.Spawn(RogueArt.Beam, prev, Vector2.zero, Combat.Ang(sd), 0f, new Vector2(sl / .5f, width * 3.2f), new Vector2(sl / .5f, width), A(col, .45f), A(col, 0f), dur * 1.3f, 0f, 0f, true, Combat.OrdFx + 3);
            prev = p;
        }
        Flash(b, .9f, col, dur);
    }

    public static void BeamLine(Vector2 a, Vector2 b, Color col, float width, float dur)
    {
        if (H == null) return; Vector2 d = b - a; float len = d.magnitude; if (len < .05f) return;
        float ang = Combat.Ang(d);
        H.Spawn(RogueArt.Beam, a, Vector2.zero, ang, 0f, new Vector2(len / .5f, width), new Vector2(len / .5f, width * .15f), Color.Lerp(col, Color.white, .75f), A(col, 0f), dur, 0f, 0f, true, Combat.OrdFx + 4);
        H.Spawn(RogueArt.Beam, a, Vector2.zero, ang, 0f, new Vector2(len / .5f, width * 3f), new Vector2(len / .5f, width * .6f), A(col, .5f), A(col, 0f), dur * 1.4f, 0f, 0f, true, Combat.OrdFx + 3);
    }

    public static void Shell(Vector2 pos, Vector2 aimDir)
    {
        if (H == null) return; Vector2 side = new Vector2(-aimDir.y, aimDir.x) * (Random.value < .5f ? 1f : -1f);
        H.Spawn(ThemeGfx.Pixel, pos, side * Random.Range(1.6f, 3f) + Vector2.up * Random.Range(1.5f, 3f), Random.Range(0f, 360f), Random.Range(-720f, 720f), new Vector2(.09f, .06f), new Vector2(.07f, .05f), new Color(1f, .85f, .4f), new Color(1f, .85f, .4f, 0f), .6f, 11f, .2f, false, Combat.OrdFx - 6);
    }

    public static void Wisp(Vector2 pos, Color col)
    {
        if (H == null) return;
        H.Spawn(Glow, pos, new Vector2(Random.Range(-.3f, .3f), 1.5f), 0f, 0f, new Vector2(.5f, .5f), new Vector2(.15f, .15f), A(col, .8f), A(col, 0f), 1.1f, -.2f, .5f, true, Combat.OrdFx);
    }

    /// <summary>Vết ichor (máu quái) và cháy xém trên sàn.</summary>
    public static void Ichor(Vector2 pos, Color col, float size = 1f)
    {
        if (H == null) return;
        Burst(pos, col, 7, 3.2f * size, .5f, .16f * size, 8f, false);
        Decal(pos, new Color(col.r * .55f, col.g * .45f, col.b * .6f, .55f), size);
    }

    public static void Scorch(Vector2 pos, float r) { Decal(pos, new Color(.04f, .03f, .06f, .5f), r * 1.6f); }

    static void Decal(Vector2 pos, Color col, float size)
    {
        if (H == null) return;
        var go = new GameObject("Decal"); go.transform.SetParent(H.transform, false); go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = Disc; sr.color = col; sr.sortingOrder = -97;
        float w = Random.Range(.55f, .9f) * size, h = w * Random.Range(.45f, .7f); go.transform.localScale = new Vector3(w * 2f / 4f, h * 2f / 4f, 1f); go.transform.localEulerAngles = new Vector3(0, 0, Random.Range(0f, 180f));
        var f = go.AddComponent<DecalFade>(); f.sr = sr; f.a0 = col.a; H.AddDecal(go);
    }

    public static void Text(Vector2 pos, string text, Color col, float size = .13f) { FloatText.Spawn(pos, text, col, size); }
}

class DecalFade : MonoBehaviour
{
    public SpriteRenderer sr; public float a0, t;
    void Update() { t += Combat.VDt; float k = Mathf.Clamp01((t - 18f) / 10f); if (sr != null) { var c = sr.color; c.a = a0 * (1f - k); sr.color = c; } if (t > 28f) Destroy(gameObject); }
}

/// <summary>Số sát thương / chữ nổi bay lên rồi mờ dần (TextMesh, không cần Canvas).</summary>
public class FloatText : MonoBehaviour
{
    TextMesh tm; float t; Color col; float life = .8f; Vector3 vel; float pop;

    public static void Spawn(Vector3 pos, string text, Color col, float size = .13f)
    {
        var go = new GameObject("Float"); go.transform.position = new Vector3(pos.x + Random.Range(-.15f, .15f), pos.y, 0f);
        var tm = go.AddComponent<TextMesh>();
        tm.font = GameFont.Body; tm.text = text; tm.fontSize = 64; tm.characterSize = size; tm.anchor = TextAnchor.MiddleCenter; tm.color = col; tm.fontStyle = FontStyle.Bold;
        var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = tm.font.material; mr.sortingOrder = Combat.OrdText;
        var f = go.AddComponent<FloatText>(); f.tm = tm; f.col = col; f.vel = new Vector3(Random.Range(-.4f, .4f), 1.6f, 0f); f.pop = size > .16f ? 1.5f : 1f; if (size > .2f) f.life = 1.1f;
    }

    void Update()
    {
        t += Time.deltaTime; vel.y = Mathf.MoveTowards(vel.y, .5f, Time.deltaTime * 3f);
        transform.position += vel * Time.deltaTime;
        float s = t < .12f ? Mathf.Lerp(.4f, pop, t / .12f) : Mathf.Lerp(pop, 1f, Mathf.Clamp01((t - .12f) / .15f)); transform.localScale = Vector3.one * s;
        var c = col; c.a = Mathf.Clamp01(1.4f - t / life * 1.4f + .0f); c.a = Mathf.Clamp01((life - t) / (life * .45f)); tm.color = c;
        if (t >= life) Destroy(gameObject);
    }
}
