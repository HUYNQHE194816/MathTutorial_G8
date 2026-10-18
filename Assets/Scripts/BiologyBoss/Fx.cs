using System.Collections.Generic;
using UnityEngine;

/// <summary>Hiệu ứng: hạt, vòng sóng nổ, vệt chém, sét, rung camera.</summary>
public static class Fx
{
    public static readonly Color[] FireCols = { new Color(1f, .82f, .4f), new Color(1f, .48f, .23f), new Color(.9f, .28f, .3f), Color.white };
    public static readonly Color[] HitCols = { Color.white, new Color(1f, .6f, .6f) };
    public static readonly Color[] BuffCols = { new Color(.43f, .9f, 1f), new Color(.6f, .9f, .4f), Color.white, new Color(1f, .82f, .4f) };
    public static readonly Color[] BoltCols = { new Color(1f, .95f, .63f), new Color(.5f, .88f, 1f) };
    public static readonly Color[] IceCols = { new Color(.55f, .9f, 1f), new Color(.8f, .96f, 1f), Color.white };
    public static readonly Color[] HealCols = { new Color(.5f, 1f, .55f), new Color(.8f, 1f, .7f), Color.white };
    static readonly Stack<FxParticle> pool = new Stack<FxParticle>();

    public static FxParticle Spawn(Sprite s, Vector2 pos, Color c, float life, float s0, float s1, Vector2 vel,
        float grav = 0f, float rot = 0f, float spin = 0f, int order = 5000, bool flip = false, bool add = true, float stretch = 1f)
    {
        FxParticle p = null;
        while (pool.Count > 0) { p = pool.Pop(); if (p != null) break; p = null; }
        if (p == null) { var go = new GameObject("fx"); p = go.AddComponent<FxParticle>(); p.sr = go.AddComponent<SpriteRenderer>(); }
        p.gameObject.SetActive(true);
        p.Init(s, pos, c, life, s0, s1, vel, grav, rot, spin, order, flip, add, stretch);
        return p;
    }
    public static void Release(FxParticle p) { p.gameObject.SetActive(false); pool.Push(p); }

    public static void Burst(Vector2 pos, int n, Color[] cols, float vMin = 2f, float vMax = 9f, float size = 1f)
    {
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * 6.2832f, sp = Random.Range(vMin, vMax);
            Spawn(ProcSprites.Pixel, pos, cols[Random.Range(0, cols.Length)], Random.Range(.3f, .8f), Random.Range(.6f, 1.4f) * size, .2f,
                new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp + Vector2.up * 1.2f, 12f);
        }
    }

    public static void Ring(Vector2 pos, float radius, Color c, float t = .4f)
        => Spawn(ProcSprites.Ring, pos, c, t, .1f, radius / 2f, Vector2.zero, order: 4900);

    /// <summary>Vụ nổ: 2 vòng sóng + hạt + quầng sáng + rung camera + âm thanh. r tính theo đơn vị thế giới.</summary>
    public static void Boom(Vector2 pos, float r, Color[] cols)
    {
        Ring(pos, r * 1.6f, cols[0], .45f); Ring(pos, r, Color.white, .35f);
        Burst(pos, Mathf.Max(6, (int)(r * 14f)), cols, 2f, r * 7f + 3f); Sparks(pos, Mathf.Max(4, (int)(r * 8f)), cols[0]);
        Spawn(ProcSprites.Glow, pos, new Color(1f, .6f, .2f, .8f), .3f, r * .5f, r * .9f, Vector2.zero, order: 4800);
        CameraRig.Shake(r * 4f); Sfx.Boom();
    }

    /// <summary>Vệt chém hình lưỡi liềm quét quanh hiệp sĩ.</summary>
    public static void Slash(Vector2 pos, float angleDeg, float range, Color? tint = null)
    {
        Spawn(ProcSprites.Slash, pos, tint ?? new Color(.43f, .9f, 1f, .95f), .2f, range * .5f, range * .42f, Vector2.zero, 0f, angleDeg - 55f, 550f, 5100);
        Spawn(ProcSprites.Slash, pos, Color.white, .2f, range * .42f, range * .36f, Vector2.zero, 0f, angleDeg - 55f, 550f, 5101);
    }

    public static void Afterimage(Sprite s, Vector2 pos, bool flip, Color c) => Spawn(s, pos, c, .25f, 1f, 1f, Vector2.zero, order: 10, flip: flip, add: false);

    /// <summary>Tia lửa kéo dài bắn thẳng ra (trúng đòn / nổ).</summary>
    public static void Sparks(Vector2 pos, int n, Color c)
    {
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * 6.2832f, sp = Random.Range(6f, 16f);
            Spawn(ProcSprites.Pixel, pos, c, Random.Range(.12f, .3f), 1.3f, .4f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp, 0f, a * Mathf.Rad2Deg, 0f, 5300, false, true, 5f);
        }
    }

    /// <summary>Vệt cháy đen trên sàn sau vụ nổ, mờ dần sau vài giây.</summary>
    public static void Scorch(Vector2 pos, float r)
        => Spawn(ProcSprites.Circle, pos, new Color(0f, 0f, 0f, .5f), 3.5f, r * .3f, r * .5f, Vector2.zero, 0f, 0f, 0f, -880, false, false);

    public static void Lightning(Vector2 target)
    {
        float x = target.x;
        for (float y = 11f; y > target.y; y -= .5f)
        {
            x = Mathf.Lerp(x + Random.Range(-.6f, .6f), target.x, .15f);
            Spawn(ProcSprites.Pixel, new Vector2(x, y), Random.value < .5f ? Color.white : BoltCols[1], .25f, 2.2f, 1.2f, Vector2.zero, order: 5200);
        }
    }
}

/// <summary>Một hạt / sprite hiệu ứng tạm thời (có pool).</summary>
public class FxParticle : MonoBehaviour
{
    public SpriteRenderer sr;
    Vector2 vel; float g, life, max, s0, s1, spin, sx = 1f; Color col;

    public void Init(Sprite s, Vector2 pos, Color c, float l, float a, float b, Vector2 v, float grav, float rot, float sp, int order, bool flip, bool add, float stretch)
    {
        sr.sprite = s; sr.sharedMaterial = add ? Gfx.Additive : Gfx.Alpha; sx = stretch; sr.sortingOrder = order; sr.flipX = flip; col = c; life = max = l; s0 = a; s1 = b; vel = v; g = grav; spin = sp;
        transform.position = pos; transform.rotation = Quaternion.Euler(0, 0, rot); transform.localScale = new Vector3(a * stretch, a, 1f); sr.color = c;
    }

    void Update()
    {
        float dt = Time.deltaTime; life -= dt;
        if (life <= 0f) { Fx.Release(this); return; }
        float t = 1f - life / max;
        transform.position += (Vector3)(vel * dt); vel.y -= g * dt;
        float sc = Mathf.Lerp(s0, s1, t); transform.localScale = new Vector3(sc * sx, sc, 1f);
        if (spin != 0f) transform.Rotate(0, 0, spin * dt);
        var c = col; c.a *= 1f - t; sr.color = c;
    }
}

/// <summary>Gắn vào Camera: rung màn hình khi trúng đòn / nổ.</summary>
public class CameraRig : MonoBehaviour
{
    static CameraRig I; float shake; Vector3 basePos;
    void Awake() { I = this; basePos = transform.position; }
    public static void Shake(float a) { if (I != null) I.shake = Mathf.Max(I.shake, a); }
    void LateUpdate()
    {
        shake *= Mathf.Pow(.88f, Time.unscaledDeltaTime * 60f);
        var sp = basePos + (Vector3)(Random.insideUnitCircle * shake * .09f); sp.x = Mathf.Round(sp.x * 16f) / 16f; sp.y = Mathf.Round(sp.y * 16f) / 16f; transform.position = sp;   // bám lưới pixel
    }
}

/// <summary>Nhấp nháy ngọn đuốc.</summary>
public class FxFlicker : MonoBehaviour
{
    public float speed = 9f, amount = .15f, phase; Vector3 baseScale;
    void Start() { baseScale = transform.localScale; phase = Random.value * 10f; }
    void Update() { transform.localScale = baseScale * (1f + Mathf.Sin(Time.time * speed + phase) * amount); }
}
