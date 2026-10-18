using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quả cầu lửa của rồng. Bị chém trúng thì tan biến.
/// Friendly = true: đạn của phe hiệp sĩ (đạn bị "Chém Phản" hất ngược lại, hoặc phép của Đệ Hiệp Sĩ) - bay tới đâu gây sát thương rồng / đệ tử tới đó.
/// </summary>
public class Projectile : MonoBehaviour
{
    public static readonly List<Projectile> All = new List<Projectile>();
    public bool Friendly { get; private set; }
    Vector2 vel; PlayerKnight pl; float trail, dmg; SpriteRenderer sr, glow; Color tint = new Color(1f, .48f, .23f);

    public static Projectile Spawn(Vector2 pos, Vector2 vel, PlayerKnight p)
    {
        var go = new GameObject("Fireball"); go.transform.position = pos; go.transform.localScale = Vector3.one * 1.2f;
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = ProcSprites.Fireball; sr.sortingOrder = 4000;
        var gl = Gfx.Glow(go.transform, Vector2.zero, 3.4f, new Color(1f, .5f, .2f, .6f), 3999);
        var pr = go.AddComponent<Projectile>(); pr.vel = vel; pr.pl = p; pr.sr = sr; pr.glow = gl; return pr;
    }

    /// <summary>Phép của phe hiệp sĩ (Đệ Hiệp Sĩ bắn).</summary>
    public static Projectile SpawnFriendly(Vector2 pos, Vector2 vel, float damage, Color color)
    {
        var pr = Spawn(pos, vel, null); pr.MakeFriendly(damage, color); pr.transform.localScale = Vector3.one * .9f; return pr;
    }

    void MakeFriendly(float damage, Color color)
    {
        Friendly = true; dmg = damage; tint = color; sr.color = color; glow.color = new Color(color.r, color.g, color.b, .6f);
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Update()
    {
        float dt = Time.deltaTime;
        transform.position += (Vector3)(vel * dt); transform.Rotate(0, 0, 360f * dt);
        trail -= dt;
        if (trail <= 0f) { trail = .04f; Fx.Spawn(ProcSprites.Pixel, transform.position, tint, .25f, 1.4f, .2f, Random.insideUnitCircle * .6f); }
        Vector2 p = transform.position;
        if (Friendly) { if (HitEnemies(p)) return; }
        else if (pl != null && pl.Alive && Vector2.Distance(p, pl.Center) < .8f)
        {
            Fx.Burst(pl.Center, 6, Fx.FireCols); pl.Hurt(DragonBoss.Scale(10)); Destroy(gameObject); return;
        }
        if (Mathf.Abs(p.x) > 24f || p.y > 12f || p.y < -12f) Destroy(gameObject);
    }

    /// <summary>Đạn phe ta trúng rồng hoặc đệ tử thì nổ. Trả về true nếu đã trúng.</summary>
    bool HitEnemies(Vector2 p)
    {
        var mgr = BossFightManager.I; if (mgr == null) return false;
        var boss = mgr.Boss;
        if (boss != null && !boss.Dead && Vector2.Distance(p, boss.Position) < 2.3f)
        {
            Fx.Burst(p, 8, new[] { tint, Color.white }); boss.TakeDamage(dmg, false); Destroy(gameObject); return true;
        }
        for (int i = Minion.All.Count - 1; i >= 0; i--)
        {
            var m = Minion.All[i];
            if (m == null || m.Spawning || Vector2.Distance(p, m.transform.position) > 1.1f) continue;
            Fx.Burst(p, 8, new[] { tint, Color.white }); m.TakeDamage(dmg, false); Destroy(gameObject); return true;
        }
        return false;
    }

    public void Deflect() { Fx.Burst(transform.position, 6, new[] { new Color(1f, .82f, .4f), Color.white }); Destroy(gameObject); }

    /// <summary>Buff "Chém Phản": hất đạn bay ngược về phía rồng, gây d sát thương khi trúng.</summary>
    public void Reflect(DragonBoss boss, float d)
    {
        if (Friendly) return;
        Vector2 here = transform.position, to = (boss != null ? boss.Position : here + Vector2.up) - here;
        vel = to.normalized * Mathf.Max(9f, vel.magnitude * 1.5f);
        MakeFriendly(d, new Color(.7f, 1f, 1f));
        Fx.Ring(here, 1.8f, new Color(.5f, .9f, 1f), .25f); Fx.Burst(here, 8, new[] { new Color(.5f, .9f, 1f), Color.white });
        Sfx.Tone(520, .1f, 2, .15f, 200, snd: Snd.SwordReflect);
    }

    public static void ClearAll() { for (int i = All.Count - 1; i >= 0; i--) if (All[i] != null) Destroy(All[i].gameObject); }
}
