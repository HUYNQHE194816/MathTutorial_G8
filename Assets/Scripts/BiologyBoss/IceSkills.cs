using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff "Kiếm Băng": một hàng băng - các gai băng trồi lên nối tiếp nhau dọc theo một tia xuất phát từ hiệp sĩ.
/// Kẻ địch (rồng / đệ tử) bị quét qua sẽ nhận sát thương và bị ĐÓNG BĂNG.
/// </summary>
public class IceRow : MonoBehaviour
{
    const float Speed = 17f, MaxDist = 28f, SpikeGap = .7f;
    Vector2 origin, dir; float dist, nextSpike, dmg, freezeFor; bool hitBoss;
    readonly HashSet<Minion> hitMinions = new HashSet<Minion>();

    public static IceRow Spawn(Vector2 origin, Vector2 dir, float damage, float freezeSeconds)
    {
        var go = new GameObject("IceRow"); go.transform.position = origin;
        var r = go.AddComponent<IceRow>(); r.origin = origin; r.dir = dir.normalized; r.dmg = damage; r.freezeFor = freezeSeconds; return r;
    }

    void Update()
    {
        var mgr = BossFightManager.I;
        if (mgr == null || !mgr.Playing) { if (mgr == null) Destroy(gameObject); return; }
        dist += Speed * Time.deltaTime;
        while (nextSpike <= dist) { IceSpike.Spawn(origin + dir * nextSpike); nextSpike += SpikeGap; }
        Vector2 head = origin + dir * dist;

        var boss = mgr.Boss;
        if (!hitBoss && boss != null && !boss.Dead && Vector2.Distance(head, boss.Position) < 2.4f)
        {
            hitBoss = true; Fx.Burst(boss.Position, 12, Fx.IceCols);
            boss.TakeDamage(dmg, false); if (mgr.Playing) boss.Freeze(freezeFor);
        }
        for (int i = Minion.All.Count - 1; i >= 0; i--)
        {
            var m = Minion.All[i];
            if (m == null || m.Spawning || hitMinions.Contains(m) || Vector2.Distance(head, m.transform.position) > 1.2f) continue;
            hitMinions.Add(m); m.TakeDamage(dmg, false); if (m != null) m.Freeze(freezeFor + .8f);
        }
        if (dist > MaxDist || Mathf.Abs(head.x) > 20f || head.y > 10f || head.y < -11f) Destroy(gameObject);
    }
}

/// <summary>Một gai băng: trồi lên rất nhanh, đứng yên một lát rồi mờ dần.</summary>
public class IceSpike : MonoBehaviour
{
    SpriteRenderer sr; float t, scale;
    const float Grow = .1f, Life = .75f;

    public static void Spawn(Vector2 pos)
    {
        var go = new GameObject("IceSpike"); go.transform.position = pos;
        var s = go.AddComponent<IceSpike>(); s.scale = Random.Range(.9f, 1.3f);
        s.sr = go.AddComponent<SpriteRenderer>(); s.sr.sprite = ProcSprites.IceSpike; s.sr.sortingOrder = -Mathf.RoundToInt(pos.y * 20f) + 3;
        go.transform.localScale = new Vector3(s.scale, 0f, 1f);
        Fx.Spawn(ProcSprites.Pixel, pos + Vector2.up * .3f, Fx.IceCols[Random.Range(0, Fx.IceCols.Length)], .35f, 1.4f, .2f, new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(1f, 3f)), 6f);
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / Grow);
        transform.localScale = new Vector3(scale, scale * (1f - (1f - k) * (1f - k)), 1f);
        float a = t > Life - .25f ? Mathf.Clamp01((Life - t) / .25f) : 1f;
        sr.color = new Color(1f, 1f, 1f, a);
        if (t >= Life) Destroy(gameObject);
    }
}
