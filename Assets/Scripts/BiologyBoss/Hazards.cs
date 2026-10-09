using System.Collections.Generic;
using UnityEngine;

/// <summary>Dọn toàn bộ laze / sóng chấn đang có trên màn hình.</summary>
public static class Hazards
{
    public static void ClearAll() { LaserBeam.ClearAll(); Shockwave.ClearAll(); }

    /// <summary>Khoảng cách từ điểm p tới đoạn thẳng a-b.</summary>
    public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a; float l2 = ab.sqrMagnitude;
        if (l2 < 1e-5f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
        return Vector2.Distance(p, a + ab * t);
    }
}

/// <summary>
/// Tia laze: Deadly = false thì chỉ là vệt cảnh báo, Deadly = true thì gây sát thương khi hiệp sĩ chạm vào.
/// Gọi Set(...) mỗi frame để di chuyển / quét tia.
/// </summary>
public class LaserBeam : MonoBehaviour
{
    public static readonly List<LaserBeam> All = new List<LaserBeam>();
    public bool Deadly; public int Damage = 12; public float Width = .8f;
    Vector2 a, b; SpriteRenderer outer, core; PlayerKnight pl; Color col; float spark;

    public static LaserBeam Spawn(PlayerKnight p, Vector2 a, Vector2 b, float width, Color c, bool deadly, int dmg = 12)
    {
        var go = new GameObject("Laser");
        var l = go.AddComponent<LaserBeam>(); l.pl = p; l.Damage = dmg;
        l.outer = go.AddComponent<SpriteRenderer>(); l.outer.sprite = ProcSprites.Pixel; l.outer.sortingOrder = 4500;
        var cg = new GameObject("Core"); cg.transform.SetParent(go.transform, false);
        l.core = cg.AddComponent<SpriteRenderer>(); l.core.sprite = ProcSprites.Pixel; l.core.sortingOrder = 4501;
        l.core.transform.localScale = new Vector3(1f, .4f, 1f); Gfx.SetAdd(l.outer, true); Gfx.SetAdd(l.core, true);
        l.Set(a, b, width, c, deadly); return l;
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public void Set(Vector2 from, Vector2 to, float width, Color c, bool deadly)
    {
        a = from; b = to; Width = width; col = c; Deadly = deadly;
        Vector2 d = to - from;
        transform.position = (from + to) * .5f;
        transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        transform.localScale = new Vector3(Mathf.Max(.01f, d.magnitude) * 4f, Mathf.Max(.01f, width) * 4f, 1f);   // sprite Pixel = 0.25 đơn vị
        outer.color = c; core.color = deadly ? new Color(1f, 1f, 1f, .95f) : new Color(1f, 1f, 1f, 0f);
    }

    void Update()
    {
        if (!Deadly) return;
        float k = .85f + .15f * Mathf.Sin(Time.time * 50f);
        outer.color = new Color(col.r, col.g, col.b, col.a * k);
        spark -= Time.deltaTime;
        if (spark <= 0f)
        {
            spark = .04f; Vector2 pt = Vector2.Lerp(a, b, Random.value);
            Fx.Spawn(ProcSprites.Pixel, pt + Random.insideUnitCircle * Width * .4f, new Color(1f, .8f, .5f), .3f, 1.6f, .2f, Random.insideUnitCircle * 3f);
        }
        if (pl != null && pl.Alive && Hazards.DistToSegment(pl.Center, a, b) < Width * .5f + .3f) pl.Hurt(DragonBoss.Scale(Damage));
    }

    public static void ClearAll() { for (int i = All.Count - 1; i >= 0; i--) if (All[i] != null) Destroy(All[i].gameObject); }
}

/// <summary>Sóng chấn từ cánh rồng: một dải năng lượng quét dọc mặt đất về phía người chơi, rộng dần theo khoảng cách.</summary>
public class Shockwave : MonoBehaviour
{
    public static readonly List<Shockwave> All = new List<Shockwave>();
    Vector2 origin, dir, perp; float speed, halfW, grow, dist, life, dust; int damage; bool hit; PlayerKnight pl; SpriteRenderer sr, core;

    public static Shockwave Spawn(PlayerKnight p, Vector2 origin, Vector2 dir, float speed, float halfWidth, float grow, int dmg)
    {
        var go = new GameObject("Shockwave"); go.transform.position = origin;
        var s = go.AddComponent<Shockwave>(); s.pl = p; s.origin = origin; s.dir = dir.normalized; s.perp = new Vector2(-s.dir.y, s.dir.x);
        s.speed = speed; s.halfW = halfWidth; s.grow = grow; s.damage = dmg;
        go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(s.dir.y, s.dir.x) * Mathf.Rad2Deg);
        s.sr = go.AddComponent<SpriteRenderer>(); s.sr.sprite = ProcSprites.Pixel; s.sr.sortingOrder = -830; s.sr.color = new Color(1f, .62f, .25f, .6f);
        var cg = new GameObject("Core"); cg.transform.SetParent(go.transform, false);
        s.core = cg.AddComponent<SpriteRenderer>(); s.core.sprite = ProcSprites.Pixel; s.core.sortingOrder = -829; s.core.color = new Color(1f, .95f, .75f, .9f);
        s.core.transform.localScale = new Vector3(.35f, 1f, 1f); Gfx.SetAdd(s.sr, true); Gfx.SetAdd(s.core, true);
        s.Apply(); return s;
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Apply()
    {
        transform.position = origin + dir * dist;
        transform.localScale = new Vector3(1.1f * 4f, halfW * 2f * 4f, 1f);   // x: độ dày theo hướng đi, y: bề ngang
    }

    void Update()
    {
        float dt = Time.deltaTime; dist += speed * dt; halfW += grow * dt; life += dt; Apply();
        Vector2 pos = transform.position;
        dust -= dt;
        if (dust <= 0f)
        {
            dust = .04f;
            for (int i = 0; i < 3; i++)
                Fx.Spawn(ProcSprites.Pixel, pos + perp * Random.Range(-halfW, halfW), new Color(.85f, .6f, .35f, .8f), .5f, Random.Range(.8f, 1.6f), .2f, new Vector2(Random.Range(-1f, 1f), Random.Range(1f, 3.5f)), 4f);
        }
        if (!hit && pl != null && pl.Alive)
        {
            Vector2 rel = pl.Center - pos;
            if (Mathf.Abs(Vector2.Dot(rel, dir)) < .9f && Mathf.Abs(Vector2.Dot(rel, perp)) < halfW) { hit = true; pl.Hurt(DragonBoss.Scale(damage)); }
        }
        if (life > 4f || Mathf.Abs(pos.x) > 26f || Mathf.Abs(pos.y) > 14f) Destroy(gameObject);
    }

    public static void ClearAll() { for (int i = All.Count - 1; i >= 0; i--) if (All[i] != null) Destroy(All[i].gameObject); }
}
