using System.Collections.Generic;
using UnityEngine;

public enum BoltStyle { Orb, Arrow, Rocket, Cannon, Vial, Thorn, Leaf, Fire, Shard, Spit }

/// <summary>Mô tả một viên đạn: tốc độ, sát thương, kiểu vẽ, nảy, xuyên, tự dẫn, uốn cong, nổ khi hết...</summary>
public class BoltSpec
{
    public Vector2 dir = Vector2.right; public float speed = 10f, dmg = 8f, radius = .2f, life = 3f, knock, homing, accel, curve, blast, size = 1f, gravity;
    public Element el = Element.None; public bool friendly; public int pierce, bounce; public BoltStyle style = BoltStyle.Orb; public Color col = Color.white;
    public bool lob; public Vector2 lobTo; public float lobTime = 1f, lobHeight = 2f;   // ném parabol: bay qua đầu, chỉ có tác dụng khi rơi xuống
    public System.Action<Vector2> onLand;                                                // gọi tại điểm rơi/nổ (đặt vũng độc, gai...)
    public bool silentTrail; public float slowPlayer;   // làm chậm người chơi khi trúng (axit)
}

/// <summary>Đạn trong trận (của người chơi hoặc của quái/boss). Va chạm tính bằng khoảng cách, không dùng Physics. Dừng khi Combat.Dt = 0.</summary>
public class CombatBolt : MonoBehaviour
{
    public static readonly List<CombatBolt> Enemy = new List<CombatBolt>();
    public BoltSpec s; public Vector2 vel; float t, trailT, spin, hp0; Vector2 start; float yOff;
    readonly HashSet<Foe> hit = new HashSet<Foe>(); Transform vis; bool dead;

    public static CombatBolt Fire(Vector2 pos, BoltSpec s)
    {
        var go = new GameObject(s.friendly ? "Bolt" : "EnemyBolt"); go.transform.position = pos;
        var b = go.AddComponent<CombatBolt>(); b.s = s; b.start = pos;
        b.vel = s.dir.normalized * s.speed; if (s.lob) b.vel = Vector2.zero;
        b.Build();
        if (!s.friendly) Enemy.Add(b);
        return b;
    }

    /// <summary>Tương thích kiểu cũ: đạn tròn phát sáng.</summary>
    public static CombatBolt Fire(Vector2 pos, Vector2 dir, float speed, float dmg, float radius, Color col, bool friendly, float life,
                                  Element el = Element.None, int pierce = 0, int bounce = 0)
    {
        return Fire(pos, new BoltSpec { dir = dir, speed = speed, dmg = dmg, radius = radius, col = col, friendly = friendly, life = life, el = el, pierce = pierce, bounce = bounce });
    }

    void Build()
    {
        var root = new GameObject("Vis"); root.transform.SetParent(transform, false); vis = root.transform; int o = Combat.OrdFx - 20;
        Color c = s.col; Sprite spr = null; bool rot = false; float sc = s.size;
        switch (s.style)
        {
            case BoltStyle.Arrow: spr = RogueArt.Arrow; rot = true; break;
            case BoltStyle.Rocket: spr = RogueArt.Rocket; rot = true; break;
            case BoltStyle.Cannon: spr = RogueArt.Cannonball; sc *= 1.15f; break;
            case BoltStyle.Vial: spr = RogueArt.Vial; sc *= 1.1f; break;
            case BoltStyle.Thorn: spr = RogueArt.Thorn; rot = true; break;
            case BoltStyle.Leaf: spr = RogueArt.Leaf; break;
            case BoltStyle.Shard: spr = RogueArt.Shard; rot = true; break;
        }
        var glow = Combat.MakeSr("Glow", vis, RogueArt.Glow, new Color(c.r, c.g, c.b, s.style == BoltStyle.Cannon ? .0f : .55f), new Vector2(s.radius * 5f, s.radius * 5f), o);
        glow.sharedMaterial = Gfx.Additive;
        if (spr != null)
        {
            var sr = Combat.MakePx("Spr", vis, spr, o + 2, null, sc);
            if (s.style == BoltStyle.Shard || s.style == BoltStyle.Leaf) sr.color = c;
            if (s.style == BoltStyle.Arrow && s.el != Element.None) sr.color = Color.Lerp(Color.white, c, .35f);
        }
        else
        {
            var core = Combat.MakeSr("Core", vis, RogueArt.Disc, Color.Lerp(c, Color.white, .35f), new Vector2(s.radius * 2.4f, s.radius * 2.4f), o + 2);
            var rim = Combat.MakeSr("Rim", vis, RogueArt.Disc, c, new Vector2(s.radius * 3.4f, s.radius * 3.4f), o + 1);
            rim.color = new Color(c.r, c.g, c.b, .85f);
        }
        if (rot) vis.localEulerAngles = new Vector3(0, 0, Combat.Ang(s.dir) - (s.style == BoltStyle.Thorn ? 90f : 0f));
        hp0 = 0f;
    }

    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f || dead) return;
        t += dt; Vector2 p = transform.position;

        if (s.lob)
        {
            float k = Mathf.Clamp01(t / s.lobTime); Vector2 gp = Vector2.Lerp(start, s.lobTo, k); yOff = Mathf.Sin(k * Mathf.PI) * s.lobHeight;
            transform.position = gp; vis.localPosition = new Vector3(0, yOff, 0); vis.Rotate(0, 0, 420f * dt);
            if (k >= 1f) { Land(gp); return; }
            RFx.Burst(gp + Vector2.up * yOff, s.col, 1, .5f, .3f, .12f); return;
        }

        if (s.homing > 0f)
        {
            Vector2 target; bool has = false; target = Vector2.zero;
            if (s.friendly) { var f = Combat.Nearest(p, 11f); if (f != null) { target = f.Pos; has = true; } }
            else if (Combat.Player != null && !Combat.Player.Dead) { target = Combat.Player.transform.position; has = true; }
            if (has && t > .15f) { float cur = Combat.Ang(vel), want = Combat.Ang(target - p); float na = Mathf.MoveTowardsAngle(cur, want, s.homing * Mathf.Rad2Deg * dt); vel = Combat.FromAng(na) * vel.magnitude; }
        }
        if (s.accel != 0f) { float sp = Mathf.Max(1f, vel.magnitude + s.accel * dt); vel = vel.normalized * sp; }
        if (s.curve != 0f) vel = Combat.Rot(vel, s.curve * dt);
        if (s.gravity != 0f) vel.y -= s.gravity * dt;
        if (s.friendly && Combat.FieldStrength != 0f)
        {
            Vector2 to = Combat.FieldPos - p; float d = to.magnitude; if (d < 8f && d > .2f) vel += to / d * Combat.FieldStrength * dt * (1f - d / 8f) * 14f;
        }

        p += vel * dt;
        bool wall = false;
        if (p.x < RoomManager.MinX || p.x > RoomManager.MaxX) { wall = true; if (s.bounce > 0) { s.bounce--; vel.x = -vel.x; p.x = Mathf.Clamp(p.x, RoomManager.MinX, RoomManager.MaxX); Bounced(p); } else { transform.position = p; Die(true); return; } }
        if (p.y < RoomManager.MinY || p.y > RoomManager.WallY) { wall = true; if (s.bounce > 0) { s.bounce--; vel.y = -vel.y; p.y = Mathf.Clamp(p.y, RoomManager.MinY, RoomManager.WallY); Bounced(p); } else { transform.position = p; Die(true); return; } }
        transform.position = p; if (wall) { }
        if (s.style == BoltStyle.Arrow || s.style == BoltStyle.Rocket || s.style == BoltStyle.Thorn) vis.AimTo(vel, s.style == BoltStyle.Thorn);
        else if (s.style == BoltStyle.Leaf || s.style == BoltStyle.Cannon) vis.Rotate(0, 0, 360f * dt);
        if (t >= s.life) { Die(false); return; }
        Trail(dt, p);

        if (s.friendly)
        {
            var list = Combat.Foes;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;                       // TakeHit có thể làm quái chết và rời danh sách
                var f = list[i];
                if (f == null || f.Dead || !f.Targetable || hit.Contains(f)) continue;
                if (Vector2.Distance(p, f.Pos) > s.radius + f.radius) continue;
                hit.Add(f);
                float d = s.dmg; bool crit = Combat.Roll(ref d);
                f.TakeHit(d, s.el, p - vel.normalized * .5f, s.knock, crit);
                if (s.pierce <= 0) { Die(true); return; }
                s.pierce--; RFx.Sparks(p, vel, s.col, 3);
            }
        }
        else
        {
            var pl = Combat.Player;
            if (pl != null && !pl.Dead && Vector2.Distance(p, pl.transform.position) < s.radius + pl.radius * .8f)
            { pl.Hurt(s.dmg, s.el, p); if (s.slowPlayer > 0f) pl.Slow(2.2f, s.slowPlayer); Die(true); }
        }
    }

    void Bounced(Vector2 p) { RFx.Sparks(p, -vel, s.col, 4, 5f); RFx.Ring(p, .3f, s.col, .15f); }

    void Trail(float dt, Vector2 p)
    {
        if (s.silentTrail) return; trailT -= dt; if (trailT > 0f) return; trailT = .03f;
        switch (s.style)
        {
            case BoltStyle.Rocket: RFx.Burst(p - vel.normalized * .35f, new Color(1f, .6f, .2f), 1, .8f, .32f, .22f, -1f); RFx.Puff(p - vel.normalized * .45f, new Color(.3f, .28f, .34f), 1, .3f); break;
            case BoltStyle.Cannon: RFx.Puff(p, new Color(.32f, .3f, .36f), 1, .28f); break;
            case BoltStyle.Vial: break;
            case BoltStyle.Arrow: if (s.el != Element.None) RFx.Burst(p, s.col, 1, .4f, .25f, .1f); break;
            default: RFx.Burst(p, s.col, 1, .3f, .22f, s.radius * 1.2f); break;
        }
    }

    void Land(Vector2 p) { s.onLand?.Invoke(p); Die(true, true); }

    void Die(bool impact, bool landed = false)
    {
        if (dead) return; dead = true; Vector2 p = transform.position; Enemy.Remove(this);
        if (s.blast > 0f && !landed) Explode(p, s.blast, s.dmg * (s.friendly ? .6f : .8f), s.el, s.friendly, s.knock * 1.2f, s.col);
        else if (impact && !landed) { RFx.Burst(p, s.col, 5, 3f, .3f, s.radius * 1.2f); RFx.Flash(p, s.radius * 4f, s.col, .1f); if (s.el != Element.None && s.friendly) RFx.Ring(p, .5f, s.col, .18f); }
        if (s.lob && landed) { RFx.Burst(p, s.col, 8, 3f, .45f, .2f, 6f); RogueAudio.Play(RSfx.Glass, .6f); }
        Destroy(gameObject);
    }

    /// <summary>Vụ nổ có sát thương lan, hất lùi và hiệu ứng. friendly: gây sát thương lên quái; ngược lại lên người chơi.</summary>
    public static void Explode(Vector2 pos, float radius, float dmg, Element el, bool friendly, float knock, Color col)
    {
        radius *= friendly ? RunState.BlastMul : 1f;
        RFx.Blast(pos, radius, col, true); RogueAudio.Play(RSfx.Boom, Mathf.Clamp01(.45f + radius * .2f));
        Combat.HitStop(.05f);
        if (friendly)
        {
            foreach (var f in new List<Foe>(Combat.Foes))
            {
                if (f == null || f.Dead || !f.Targetable) continue; float d = Vector2.Distance(pos, f.Pos) - f.radius; if (d > radius) continue;
                float fall = Mathf.Lerp(1f, .5f, Mathf.Clamp01(d / radius)); float dd = dmg * fall; bool crit = Combat.Roll(ref dd, .05f);
                f.TakeHit(dd, el, pos, knock * fall, crit, true);
            }
        }
        else
        {
            var pl = Combat.Player; if (pl != null && !pl.Dead && Vector2.Distance(pos, pl.transform.position) < radius + pl.radius * .5f) pl.Hurt(dmg, el, pos);
        }
    }

    void OnDestroy() { Enemy.Remove(this); }
}

static class VisExt
{
    public static void AimTo(this Transform t, Vector2 v, bool thorn) { t.localEulerAngles = new Vector3(0, 0, Combat.Ang(v) - (thorn ? 90f : 0f)); }
}

/// <summary>Vòng báo hiệu rồi nổ: đứng ngoài vòng khi hết giờ thì không bị thương.</summary>
public class CombatTelegraph : MonoBehaviour
{
    float t, delay, radius, dmg; Element el; SpriteRenderer fill, ring, rune; System.Action<Vector2> onFire; Color col; float spin;

    public static CombatTelegraph Spawn(Vector2 pos, float radius, float delay, float dmg, Element el, System.Action<Vector2> onFire = null)
    {
        var go = new GameObject("Telegraph"); go.transform.position = pos;
        var c = el == Element.None ? new Color(1f, .3f, .3f) : Combat.ElementColor(el);
        var t = go.AddComponent<CombatTelegraph>();
        t.fill = Combat.MakeSr("Fill", go.transform, RogueArt.Disc, new Color(c.r, c.g, c.b, .1f), new Vector2(radius * 2f, radius * 2f), -97);
        t.ring = Combat.MakeSr("Ring", go.transform, RogueArt.Ring, c, new Vector2(radius * 2f, radius * 2f), -96); t.ring.sharedMaterial = Gfx.Additive;
        t.rune = Combat.MakeSr("Rune", go.transform, RogueArt.Ring, new Color(c.r, c.g, c.b, .5f), new Vector2(radius * 1.3f, radius * 1.3f), -96); t.rune.sharedMaterial = Gfx.Additive;
        t.delay = delay; t.radius = radius; t.dmg = dmg; t.el = el; t.onFire = onFire; t.col = c;
        return t;
    }

    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f) return;
        t += dt; float k = Mathf.Clamp01(t / delay);
        var c = fill.color; c.a = .1f + .28f * k; fill.color = c;
        var rs = rune.transform.localScale; float sc = Mathf.Lerp(1.3f, .25f, k) * radius * 2f / rune.sprite.bounds.size.x; rune.transform.localScale = new Vector3(sc, sc, 1f);
        ring.color = new Color(col.r, col.g, col.b, .6f + .4f * Mathf.Sin(t * 22f));
        if (t < delay) return;
        var pl = Combat.Player;
        if (pl != null && !pl.Dead && Vector2.Distance(transform.position, pl.transform.position) <= radius + pl.radius * .5f) pl.Hurt(dmg, el, transform.position);
        RFx.Blast(transform.position, radius * .9f, col, true); RogueAudio.Play(el == Element.Hoa ? RSfx.Fire : RSfx.Boom, .6f);
        onFire?.Invoke(transform.position);
        Destroy(gameObject);
    }
}

/// <summary>Dải báo hiệu hình chữ nhật rồi quét tia: tia laze, cú lao của cáo, vệt tên lửa.</summary>
public class TeleLine : MonoBehaviour
{
    Vector2 a, b; float delay, width, dmg, t; Element el; Color col; SpriteRenderer fill; System.Action onFire; bool fired; float fireT;

    public static TeleLine Spawn(Vector2 from, Vector2 to, float width, float delay, float dmg, Element el, Color col, System.Action onFire = null)
    {
        var go = new GameObject("TeleLine"); var l = go.AddComponent<TeleLine>();
        l.a = from; l.b = to; l.width = width; l.delay = delay; l.dmg = dmg; l.el = el; l.col = col; l.onFire = onFire;
        Vector2 d = to - from; float len = d.magnitude;
        go.transform.position = from; go.transform.eulerAngles = new Vector3(0, 0, Combat.Ang(d));
        l.fill = Combat.MakeSr("Fill", go.transform, ThemeGfx.Pixel, new Color(col.r, col.g, col.b, .12f), new Vector2(len, width), -96, new Vector2(len * .5f, 0f));
        Combat.MakeSr("EdgeA", go.transform, ThemeGfx.Pixel, new Color(col.r, col.g, col.b, .8f), new Vector2(len, .05f), -95, new Vector2(len * .5f, width * .5f));
        Combat.MakeSr("EdgeB", go.transform, ThemeGfx.Pixel, new Color(col.r, col.g, col.b, .8f), new Vector2(len, .05f), -95, new Vector2(len * .5f, -width * .5f));
        return l;
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b) { Vector2 ab = b - a; float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(.0001f, ab.sqrMagnitude)); return Vector2.Distance(p, a + ab * k); }

    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f) return;
        t += dt;
        if (!fired)
        {
            var c = fill.color; c.a = .1f + .3f * Mathf.Clamp01(t / delay) * (.7f + .3f * Mathf.Sin(t * 25f)); fill.color = c;
            if (t < delay) return;
            fired = true; fireT = .18f; RFx.BeamLine(a, b, col, width, .22f); RFx.Shake(.12f); RogueAudio.Play(el == Element.Loi ? RSfx.Thunder : RSfx.Laser, .7f);
            var pl = Combat.Player; if (pl != null && !pl.Dead && SegDist(pl.transform.position, a, b) <= width * .5f + pl.radius * .4f) pl.Hurt(dmg, el, a);
            onFire?.Invoke();
            fill.color = new Color(col.r, col.g, col.b, .0f);
        }
        fireT -= dt; if (fireT <= 0f) Destroy(gameObject);
    }
}

/// <summary>Vật hồi máu rơi từ quái (mục 5.6).</summary>
public class CombatPickup : MonoBehaviour
{
    float heal, bob, life = 14f; Transform vis;

    public static void SpawnHeal(Vector2 pos, float heal)
    {
        var go = new GameObject("HealPickup"); go.transform.position = pos;
        var g = Combat.MakeSr("Glow", go.transform, RogueArt.Glow, new Color(1f, .35f, .4f, .6f), new Vector2(1.5f, 1.5f), Combat.OrdFx - 30); g.sharedMaterial = Gfx.Additive;
        var h = Combat.MakePx("Heart", go.transform, RogueArt.Heart, Combat.OrdFx - 29, null, 1f);
        go.AddComponent<CombatPickup>().heal = heal;
        RFx.Burst(pos, new Color(1f, .4f, .5f), 6, 2f, .4f, .12f);
    }

    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f) return;
        bob += dt; life -= dt; transform.GetChild(1).localPosition = new Vector3(0, .12f * Mathf.Sin(bob * 5f), 0);
        if (life < 3f) transform.localScale = Vector3.one * (Mathf.Sin(bob * 22f) > 0 ? 1f : .6f);
        var pl = Combat.Player;
        if (pl != null && !pl.Dead)
        {
            float d = Vector2.Distance(transform.position, pl.transform.position);
            if (d < 2.4f) transform.position = Vector2.MoveTowards(transform.position, pl.transform.position, 7f * dt);        // hút về phía người chơi
            if (d < .8f) { pl.Heal(heal); RogueAudio.Play(RSfx.Heal, .6f); RFx.Burst(pl.transform.position, new Color(1f, .4f, .5f), 8, 2.5f, .5f, .13f); Destroy(gameObject); return; }
        }
        if (life <= 0f) Destroy(gameObject);
    }
}

/// <summary>Vòng sáng nở ra rồi mờ đi (tương thích mã cũ): dùng RFx.Ring.</summary>
public static class CombatFx
{
    public static void Pulse(Vector2 pos, float radius, Color c) { RFx.Ring(pos, radius, c, .35f); }
}
