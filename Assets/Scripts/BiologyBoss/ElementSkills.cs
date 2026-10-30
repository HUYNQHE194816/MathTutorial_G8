using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Màu + hàm dùng chung cho 3 buff nguyên tố: Kiếm Gió, Kiếm Nước, Kiếm Sét.</summary>
public static class ElementFx
{
    public static readonly Color[] WindCols = { new Color(.75f, 1f, 1f), new Color(.6f, .9f, .35f), Color.white };
    public static readonly Color[] WaterCols = { new Color(.43f, .78f, 1f), new Color(.75f, .93f, 1f), Color.white };
    public static readonly Color[] SparkCols = { new Color(1f, .88f, .3f), Color.white, new Color(.5f, .88f, 1f) };

    /// <summary>Khoảng cách từ điểm p tới đoạn thẳng a-b.</summary>
    public static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a; float l = ab.sqrMagnitude;
        float t = l > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>Xoá đạn lửa của rồng trong bán kính r quanh p.</summary>
    public static void ClearBullets(Vector2 p, float r)
    {
        for (int i = Projectile.All.Count - 1; i >= 0; i--)
        {
            var b = Projectile.All[i];
            if (b == null || b.Friendly) continue;
            if (Vector2.Distance(b.transform.position, p) < r) b.Deflect();
        }
    }
}

/// <summary>
/// Kiếm Gió: cứ 3 đòn chém tạo 1 lốc xoáy bay đuổi theo rồng trong 3 giây.
/// Lốc xoáy gây sát thương liên tục cho rồng + đệ tử và xoá đạn lửa chạm vào nó.
/// </summary>
public class Tornado : MonoBehaviour
{
    const float Life = 3f, Speed = 6f, BossR = 3f, MinionR = 1.7f;
    Vector2 vel; float t, tick, dmg;
    readonly Transform[] rings = new Transform[9]; readonly SpriteRenderer[] rsr = new SpriteRenderer[9];

    public static Tornado Spawn(Vector2 from, float angleDeg, float damage)
    {
        var go = new GameObject("Tornado"); go.transform.position = from;
        var tw = go.AddComponent<Tornado>(); float a = angleDeg * Mathf.Deg2Rad;
        tw.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 8.5f; tw.dmg = damage; tw.Build();
        Fx.Ring(from, 2.6f, ElementFx.WindCols[0], .35f); Fx.Burst(from, 12, ElementFx.WindCols, 2f, 6f);
        Sfx.Tone(180, .45f, 1, .12f, 260); return tw;
    }

    void Build()
    {
        var sh = new GameObject("Shadow"); sh.transform.SetParent(transform, false); sh.transform.localScale = new Vector3(.3f, .1f, 1f);
        var ss = sh.AddComponent<SpriteRenderer>(); ss.sprite = ProcSprites.Circle; ss.color = new Color(0, 0, 0, .3f); ss.sortingOrder = -900;
        for (int i = 0; i < 9; i++)
        {
            var o = new GameObject("Ring" + i); o.transform.SetParent(transform, false); o.transform.localPosition = new Vector3(0f, i * .34f, 0f);
            var sr = o.AddComponent<SpriteRenderer>(); sr.sprite = ProcSprites.Ring; Gfx.SetAdd(sr, true);
            rings[i] = o.transform; rsr[i] = sr;
        }
    }

    void Update()
    {
        var mgr = BossFightManager.I;
        if (mgr == null) { Destroy(gameObject); return; }
        if (!mgr.Playing) return;
        float dt = Time.deltaTime; t += dt; tick -= dt;
        if (t >= Life) { Fx.Burst((Vector2)transform.position + Vector2.up * .8f, 8, ElementFx.WindCols, 1f, 4f); Destroy(gameObject); return; }

        var boss = mgr.Boss; Vector2 pos = transform.position;
        if (boss != null && !boss.Dead)
        {
            Vector2 want = (boss.Position + Vector2.down * 1.7f - pos).normalized * Speed;   // bay đuổi theo rồng
            vel = Vector2.Lerp(vel, want, 1f - Mathf.Exp(-2.5f * dt));
        }
        pos += vel * dt; pos.x = Mathf.Clamp(pos.x, -14.5f, 14.5f); pos.y = Mathf.Clamp(pos.y, -8f, 6f); transform.position = pos;

        float k = Mathf.Min(1f, t * 3f, (Life - t) * 4f); int order = -Mathf.RoundToInt(pos.y * 20f) + 6;
        for (int i = 0; i < 9; i++)
        {
            float r = (.3f + i * .17f) * (1f + .08f * Mathf.Sin(t * 9f + i)), sw = Mathf.Sin(t * 7f + i * .7f) * .12f;
            rings[i].localPosition = new Vector3(sw, i * .34f, 0f); rings[i].localScale = new Vector3(r / 2f, r / 2f * .3f, 1f);
            rsr[i].color = i % 2 == 1 ? new Color(.75f, 1f, 1f, .9f * k) : new Color(.45f, .9f, .4f, .9f * k); rsr[i].sortingOrder = order + i;
        }
        if (Random.value < dt * 40f)
        {
            float h = Random.Range(0f, 3f), w = .4f + h * .25f, a = Random.value * 6.2832f;
            Fx.Spawn(ProcSprites.Pixel, pos + new Vector2(Mathf.Cos(a) * w, h), Random.value < .5f ? ElementFx.WindCols[0] : ElementFx.WindCols[1], .25f, 1.2f, .3f, new Vector2(Mathf.Cos(a + 1.57f) * 3.5f, Random.Range(-1f, 0f)));
        }

        ElementFx.ClearBullets(pos + Vector2.up * .8f, 1.6f);
        if (tick > 0f) return;
        bool hit = false;
        if (boss != null && !boss.Dead && Vector2.Distance(boss.Position + Vector2.down * .6f, pos) < BossR)
        {
            hit = true; boss.TakeDamage(Mathf.Max(1f, Mathf.Round(dmg * Random.Range(.9f, 1.1f))), false);
            if (!mgr.Playing) return;
        }
        for (int i = Minion.All.Count - 1; i >= 0; i--)
        {
            var m = Minion.All[i];
            if (m == null || m.Spawning || Vector2.Distance(m.transform.position, pos) > MinionR) continue;
            hit = true; m.TakeDamage(Mathf.Max(1f, Mathf.Round(dmg)), false);
        }
        if (hit) tick = .25f;
    }
}

/// <summary>
/// Kiếm Nước: sóng nước hình lưỡi liềm bay theo hướng chém. Trúng rồng thì gây sát thương và ĐẨY LÙI rồng, quét sạch đạn lửa,
/// đẩy lùi + gây sát thương đệ tử trên đường đi.
/// </summary>
public class WaterWave : MonoBehaviour
{
    const float Speed = 14f, Life = 1.2f, Reach = 1.6f;
    Vector2 dir; float t, dmg; bool hitBoss; readonly HashSet<Minion> hit = new HashSet<Minion>();

    public static WaterWave Spawn(Vector2 from, float angleDeg, float damage)
    {
        var go = new GameObject("WaterWave"); go.transform.position = from; go.transform.rotation = Quaternion.Euler(0, 0, angleDeg);
        var w = go.AddComponent<WaterWave>(); float a = angleDeg * Mathf.Deg2Rad; w.dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); w.dmg = damage;
        Color[] cols = { new Color(.43f, .78f, 1f, .9f), new Color(.18f, .5f, .82f, .8f), new Color(.9f, .97f, 1f, .9f) };
        for (int i = 0; i < 3; i++)
        {
            var o = new GameObject("Layer" + i); o.transform.SetParent(go.transform, false); o.transform.localPosition = new Vector3(-i * .55f, 0f, 0f);
            o.transform.localScale = Vector3.one * (1f + i * .15f);
            var sr = o.AddComponent<SpriteRenderer>(); sr.sprite = ProcSprites.Slash; sr.color = cols[i]; sr.sortingOrder = 4950 - i; Gfx.SetAdd(sr, true);
        }
        Fx.Ring(from, 2.2f, ElementFx.WaterCols[0], .3f); Sfx.Tone(300, .4f, 3, .1f, -150); return w;
    }

    void Update()
    {
        var mgr = BossFightManager.I;
        if (mgr == null) { Destroy(gameObject); return; }
        if (!mgr.Playing) return;
        float dt = Time.deltaTime; t += dt;
        Vector2 pos = (Vector2)transform.position + dir * Speed * dt; transform.position = pos;
        if (t >= Life || Mathf.Abs(pos.x) > 20f || pos.y > 10f || pos.y < -11f) { Destroy(gameObject); return; }
        if (Random.value < dt * 55f) Fx.Spawn(ProcSprites.Pixel, pos + Random.insideUnitCircle * 1.2f, Random.value < .5f ? ElementFx.WaterCols[0] : Color.white, .3f, 1.3f, .3f, Random.insideUnitCircle * 1.5f);

        ElementFx.ClearBullets(pos, Reach + .6f);
        for (int i = Minion.All.Count - 1; i >= 0; i--)
        {
            var m = Minion.All[i];
            if (m == null || m.Spawning || hit.Contains(m) || Vector2.Distance(m.transform.position, pos) > Reach + .5f) continue;
            hit.Add(m); m.TakeDamage(Mathf.Max(1f, Mathf.Round(dmg)), false);
        }
        var boss = mgr.Boss;
        if (!hitBoss && boss != null && !boss.Dead && Vector2.Distance(boss.Position, pos) < Reach + 2.1f)
        {
            hitBoss = true; Fx.Ring(boss.Position, 4f, ElementFx.WaterCols[0], .3f); Fx.Burst(boss.Position, 12, ElementFx.WaterCols);
            boss.TakeDamage(Mathf.Max(1f, Mathf.Round(dmg)), false);
            if (mgr.Playing && !boss.Dead) boss.StartCoroutine(Push(boss, dir));
        }
    }

    /// <summary>Đẩy rồng lùi theo hướng sóng khoảng .3 giây (rồng tự bay về vị trí cũ sau đó).</summary>
    static IEnumerator Push(DragonBoss b, Vector2 d)
    {
        for (float e = 0f; e < .3f; e += Time.deltaTime)
        {
            if (b == null || b.Dead) yield break;
            Vector3 p = b.transform.position + (Vector3)(d * 20f * (1f - e / .3f) * Time.deltaTime);
            p.x = Mathf.Clamp(p.x, -12.5f, 12.5f); p.y = Mathf.Clamp(p.y, -6.25f, 5f); b.transform.position = p;
            yield return null;
        }
    }
}

/// <summary>
/// Kiếm Sét: một tia sét bắn từ hiệp sĩ theo góc cho trước, sau khoảng trễ delay (để các tia nổ lần lượt theo nhịp vung kiếm).
/// Gây sát thương rồng + đệ tử nằm trên đường tia, xoá đạn lửa chạm tia.
/// </summary>
public class LightningBolt : MonoBehaviour
{
    Vector2 origin, dir; float len, dmg, delay; bool sound;

    public static void Spawn(Vector2 origin, float angleDeg, float len, float dmg, float delay, bool sound)
    {
        var go = new GameObject("LightningBolt"); float a = angleDeg * Mathf.Deg2Rad;
        var b = go.AddComponent<LightningBolt>(); b.origin = origin; b.dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); b.len = len; b.dmg = dmg; b.delay = delay; b.sound = sound;
    }

    void Update()
    {
        delay -= Time.deltaTime; if (delay > 0f) return;
        Fire(); Destroy(gameObject);
    }

    void Fire()
    {
        var mgr = BossFightManager.I; if (mgr == null || !mgr.Playing) return;
        Vector2 end = origin + dir * len, perp = new Vector2(-dir.y, dir.x);
        for (float d = 0f; d <= len; d += .5f)   // đường sét ngoằn ngoèo vẽ bằng hạt
        {
            Vector2 p = origin + dir * d + perp * (d > 0f && d < len - .5f ? Random.Range(-.45f, .45f) : 0f);
            Fx.Spawn(ProcSprites.Pixel, p, new Color(1f, .88f, .3f), .22f, 3.6f, 1.2f, Vector2.zero, order: 5199);
            Fx.Spawn(ProcSprites.Pixel, p, Color.white, .18f, 2.2f, .8f, Vector2.zero, order: 5200);
        }
        Fx.Burst(end, 5, ElementFx.SparkCols, 2f, 6f, .8f);
        if (sound) Sfx.Tone(800 + Random.Range(0, 300), .12f, 0, .1f, -500);

        for (int i = Projectile.All.Count - 1; i >= 0; i--)
        {
            var b = Projectile.All[i];
            if (b == null || b.Friendly || ElementFx.SegDist(b.transform.position, origin, end) > .9f) continue;
            b.Deflect();
        }
        for (int i = Minion.All.Count - 1; i >= 0; i--)
        {
            var m = Minion.All[i];
            if (m == null || m.Spawning || ElementFx.SegDist(m.transform.position, origin, end) > 1f) continue;
            m.TakeDamage(dmg, false);
        }
        var boss = mgr.Boss;
        if (boss != null && !boss.Dead && ElementFx.SegDist(boss.Position, origin, end) < 2.2f)
        {
            Fx.Burst(boss.Position + Random.insideUnitCircle * .6f, 8, ElementFx.SparkCols);
            boss.TakeDamage(dmg, false);
        }
    }
}
