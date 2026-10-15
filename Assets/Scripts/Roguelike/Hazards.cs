using System.Collections.Generic;
using UnityEngine;

/// <summary>Cách dựng nhanh các vùng nguy hiểm/hiệu ứng vùng dùng ở nhiều nơi: phản ứng nguyên tố, chiêu boss, bẫy môi trường.</summary>
public static class HazardFx
{
    /// <summary>Đám mây/bùn tác động lên QUÁI (do người chơi tạo bằng phản ứng nguyên tố).</summary>
    public static void Cloud(Vector2 pos, float r, float dur, Color col, Element el, bool mud = false)
    {
        var go = new GameObject(mud ? "Mud" : "Cloud"); go.transform.position = pos;
        var z = go.AddComponent<HazardZone>(); z.r = r; z.life = dur; z.col = col; z.el = el; z.harmFoes = true; z.mud = mud; z.Build();
    }

    /// <summary>Vũng/đám độc tác động lên NGƯỜI CHƠI (chiêu boss, bẫy).</summary>
    public static HazardZone Puddle(Vector2 pos, float r, float dur, Color col, Element el, float dps, float slow, bool pulse = true)
    {
        var go = new GameObject("Puddle"); go.transform.position = pos;
        var z = go.AddComponent<HazardZone>(); z.r = r; z.life = dur; z.col = col; z.el = el; z.harmPlayer = true; z.dps = dps; z.slowPlayer = slow; z.Build(); return z;
    }

    public static void Vines(Vector2 pos, float r, bool poison = false)
    {
        var frames = RogueArt.VineFrames; int n = Mathf.CeilToInt(r * 2.2f);
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("VineFx"); go.transform.position = pos + Random.insideUnitCircle * r * .7f;
            var v = go.AddComponent<VineFx>(); v.poison = poison; v.delay = i * .05f;
        }
    }

    public static void Whirl(Vector2 pos, float r, Color col)
    {
        var go = new GameObject("Whirl"); go.transform.position = pos; var w = go.AddComponent<WhirlFx>(); w.r = r; w.col = col;
    }
}

/// <summary>Vùng tròn tồn tại một lúc: gây độc/làm chậm người chơi hoặc quái tùy cấu hình.</summary>
public class HazardZone : MonoBehaviour
{
    public float r, life, dps, slowPlayer; public Color col; public Element el; public bool harmFoes, harmPlayer, mud;
    SpriteRenderer disc, ring; float t, tick, pt;

    public void Build()
    {
        disc = Combat.MakeSr("Disc", transform, RogueArt.Disc, new Color(col.r, col.g, col.b, mud ? .5f : .28f), new Vector2(r * 2f, r * 1.5f), -95);
        ring = Combat.MakeSr("Ring", transform, RogueArt.Ring, new Color(col.r, col.g, col.b, .55f), new Vector2(r * 2f, r * 1.5f), -94);
        if (!mud) ring.sharedMaterial = Gfx.Additive;
    }

    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f) return;
        t += dt; if (t >= life) { Destroy(gameObject); return; }
        float fade = Mathf.Clamp01(Mathf.Min(t / .3f, (life - t) / .8f));
        disc.color = new Color(col.r, col.g, col.b, (mud ? .5f : .28f + .06f * Mathf.Sin(t * 5f)) * fade);
        ring.color = new Color(col.r, col.g, col.b, .55f * fade);
        pt -= dt; if (pt <= 0f) { pt = mud ? .35f : .12f; var p = (Vector2)transform.position + Random.insideUnitCircle * r * .8f; RFx.Burst(p, col, 1, mud ? .3f : .9f, .7f, mud ? .2f : .16f, mud ? 0f : -1.4f, !mud); }
        tick -= dt; if (tick > 0f) return; tick = .4f;
        Vector2 c = transform.position;
        if (harmFoes)
            foreach (var f in new List<Foe>(Combat.Foes))
            {
                if (f == null || f.Dead || !f.Targetable || Vector2.Distance(f.Pos, c) > r + f.radius * .5f) continue;
                if (mud) f.mudUntil = Combat.Now + .6f;
                else if (el == Element.Doc) { f.poison = Mathf.Min(5, Mathf.Max(f.poison, 1) + 0); f.poisonUntil = Combat.Now + 3f; f.TakeHit(3f, Element.None, c, 0f, false, true); }
            }
        var pl = Combat.Player;
        if (harmPlayer && pl != null && !pl.Dead && Vector2.Distance(pl.transform.position, c) <= r + pl.radius * .3f)
        {
            if (dps > 0f) pl.Hurt(dps * .4f, el, c, true);
            if (slowPlayer > 0f) pl.Slow(.7f, slowPlayer);
        }
    }
}

public class VineFx : MonoBehaviour
{
    public bool poison; public float delay; float t; SpriteRenderer sr; Sprite[] fr;
    void Start() { fr = RogueArt.VineFrames; sr = gameObject.AddComponent<SpriteRenderer>(); sr.sprite = fr[0]; sr.enabled = false; if (poison) sr.color = new Color(.8f, .6f, 1f); }
    void Update()
    {
        t += Combat.VDt; if (t < delay) return; float k = t - delay; sr.enabled = true; sr.sortingOrder = Combat.Order(transform.position.y);
        sr.sprite = fr[Mathf.Min(fr.Length - 1, (int)(k / .1f))];
        if (k > 1.4f) { var c = sr.color; c.a = Mathf.Clamp01(1.8f - k / .8f); sr.color = c; if (k > 2.2f) Destroy(gameObject); }
    }
}

public class WhirlFx : MonoBehaviour
{
    public float r; public Color col; float t;
    void Update()
    {
        t += Combat.VDt; if (t > .9f) { Destroy(gameObject); return; }
        for (int i = 0; i < 3; i++) { float a = t * 720f + i * 120f; float rr = r * (.2f + t * .9f); RFx.Burst((Vector2)transform.position + Combat.FromAng(a) * rr, col, 1, 1.2f, .35f, .2f, -2f); }
    }
}

/// <summary>Gai mọc từ đất: báo hiệu rồi trồi lên. Dùng cho "gai độc" của Chuột và gai của Thảo.</summary>
public class SpikeStrike : MonoBehaviour
{
    float t, delay, r, dmg; Element el; Sprite[] fr; SpriteRenderer sr, mark; bool hit; Color col;

    public static SpikeStrike Spawn(Vector2 pos, float delay, float r, float dmg, Element el = Element.Doc)
    {
        var go = new GameObject("Spike"); go.transform.position = pos; var s = go.AddComponent<SpikeStrike>();
        s.delay = delay; s.r = r; s.dmg = dmg; s.el = el; s.col = Combat.ElementColor(el); s.fr = RogueArt.SpikeFrames(el == Element.Thao ? 8 : 7);
        s.mark = Combat.MakeSr("Mark", go.transform, RogueArt.Ring, s.col, new Vector2(r * 2f, r * 1.6f), -96); s.mark.sharedMaterial = Gfx.Additive;
        s.sr = go.AddComponent<SpriteRenderer>(); s.sr.enabled = false; s.sr.sprite = s.fr[0]; s.sr.sortingOrder = Combat.Order(pos.y);
        return s;
    }

    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f) return; t += dt;
        if (t < delay) { mark.color = new Color(col.r, col.g, col.b, .35f + .4f * Mathf.Sin(t * 20f)); return; }
        float k = t - delay;
        if (k < .02f) { }
        mark.color = new Color(col.r, col.g, col.b, Mathf.Clamp01(.5f - k)); sr.enabled = true;
        float sc = r / .8f; transform.localScale = new Vector3(sc, sc, 1f);
        sr.sprite = fr[Mathf.Min(fr.Length - 1, (int)(k / .06f))];
        if (!hit)
        {
            hit = true; RFx.Burst(transform.position, col, 8, 3.5f, .4f, .16f, 6f, false); RFx.Shake(.08f); RogueAudio.Play(RSfx.Spike, .5f);
            var pl = Combat.Player; if (pl != null && !pl.Dead && Vector2.Distance(pl.transform.position, transform.position) <= r + pl.radius * .4f) pl.Hurt(dmg, el, transform.position);
        }
        if (k > .9f) { var c = sr.color; c.a = Mathf.Clamp01(1f - (k - .9f) / .3f); sr.color = c; if (k > 1.2f) Destroy(gameObject); }
    }
}

/// <summary>Dây leo trói chân: vòng báo hiệu, trồi lên, trói người chơi nếu còn trong vòng.</summary>
public class VineSnare : MonoBehaviour
{
    float t, delay, r, dmg, rootDur; Sprite[] fr; SpriteRenderer mark; bool fired; Color col; List<SpriteRenderer> vines = new List<SpriteRenderer>();

    public static VineSnare Spawn(Vector2 pos, float delay, float r, float dmg, float rootDur)
    {
        var go = new GameObject("Snare"); go.transform.position = pos; var s = go.AddComponent<VineSnare>();
        s.delay = delay; s.r = r; s.dmg = dmg; s.rootDur = rootDur; s.fr = RogueArt.VineFrames; s.col = Combat.ElementColor(Element.Thao);
        s.mark = Combat.MakeSr("Mark", go.transform, RogueArt.Ring, s.col, new Vector2(r * 2f, r * 1.6f), -96); s.mark.sharedMaterial = Gfx.Additive;
        return s;
    }

    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f) return; t += dt;
        if (!fired)
        {
            mark.color = new Color(col.r, col.g, col.b, .3f + .5f * Mathf.Sin(t * 18f)); if (t < delay) return;
            fired = true; RogueAudio.Play(RSfx.Vine, .6f);
            for (int i = 0; i < 7; i++) { var v = Combat.MakePx("v", transform, fr[0], Combat.Order(transform.position.y) + 1, Random.insideUnitCircle * r * .8f, 1f); vines.Add(v); }
            var pl = Combat.Player; if (pl != null && !pl.Dead && Vector2.Distance(pl.transform.position, transform.position) <= r + pl.radius * .4f) { pl.Hurt(dmg, Element.Thao, transform.position); pl.Root(rootDur); }
        }
        float k = t - delay; foreach (var v in vines) v.sprite = fr[Mathf.Min(fr.Length - 1, (int)(k / .1f))];
        mark.color = new Color(col.r, col.g, col.b, Mathf.Clamp01(.4f - k));
        if (k > 1.2f) { foreach (var v in vines) { var c = v.color; c.a = Mathf.Clamp01(1f - (k - 1.2f) / .4f); v.color = c; } if (k > 1.6f) Destroy(gameObject); }
    }
}

/// <summary>Sóng xung kích lan từ tâm: né bằng cách lướt qua vành sóng (Space).</summary>
public class RogueShockwave : MonoBehaviour
{
    float r, speed, maxR, thick, dmg; Element el; bool hit; Color col; SpriteRenderer ring, glow; float t;

    public static RogueShockwave Spawn(Vector2 pos, float speed, float maxR, float thick, float dmg, Element el, Color col)
    {
        var go = new GameObject("Shockwave"); go.transform.position = pos; var s = go.AddComponent<RogueShockwave>(); s.speed = speed; s.maxR = maxR; s.thick = thick; s.dmg = dmg; s.el = el; s.col = col;
        s.ring = Combat.MakeSr("Ring", go.transform, RogueArt.Ring, col, new Vector2(1f, .8f), Combat.OrdFx - 10); s.ring.sharedMaterial = Gfx.Additive;
        s.glow = Combat.MakeSr("Glow", go.transform, RogueArt.Ring, new Color(col.r, col.g, col.b, .35f), new Vector2(1f, .8f), Combat.OrdFx - 11); s.glow.sharedMaterial = Gfx.Additive;
        return s;
    }

    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f) return; t += dt;
        r += speed * dt; if (r > maxR) { Destroy(gameObject); return; }
        float w = r * 2f; ring.transform.localScale = new Vector3(w / ring.sprite.bounds.size.x, w * .8f / ring.sprite.bounds.size.y, 1f);
        glow.transform.localScale = ring.transform.localScale * 1.06f; ring.color = new Color(col.r, col.g, col.b, Mathf.Clamp01(1.3f - r / maxR));
        var pl = Combat.Player; if (hit || pl == null || pl.Dead) return;
        Vector2 d = (Vector2)pl.transform.position - (Vector2)transform.position; d.y /= .8f; float dist = d.magnitude;
        if (Mathf.Abs(dist - r) < thick * .5f + pl.radius * .3f) { hit = true; pl.Hurt(dmg, el, transform.position); }
    }
}

/// <summary>Ô sàn nhiễm điện: nhấp nháy rồi giật. Dùng cho pha điện từ của boss 3 và bẫy khu 3.</summary>
public class ElectricTile : MonoBehaviour
{
    float t, delay, dmg, fadeT; Vector2 size; SpriteRenderer sr; bool fired; static Color Col => Combat.ElementColor(Element.Loi);

    public static ElectricTile Spawn(Vector2 pos, Vector2 size, float delay, float dmg)
    {
        var go = new GameObject("ElecTile"); go.transform.position = pos; var e = go.AddComponent<ElectricTile>();
        e.delay = delay; e.dmg = dmg; e.size = size; var c = Col;
        e.sr = Combat.MakeSr("Fill", go.transform, ThemeGfx.Pixel, new Color(c.r, c.g, c.b, .15f), size, -96); e.sr.sharedMaterial = Gfx.Additive;
        return e;
    }

    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f) return; var col = Col;
        if (!fired)
        {
            t += dt; float f = Mathf.Sin(t * Mathf.Lerp(8f, 40f, t / delay)) * .5f + .5f; sr.color = new Color(col.r, col.g, col.b, .1f + .3f * f);
            if (t < delay) return; fired = true;
            var pl = Combat.Player; Vector2 p = pl != null ? (Vector2)pl.transform.position - (Vector2)transform.position : new Vector2(99, 99);
            if (pl != null && !pl.Dead && Mathf.Abs(p.x) <= size.x * .5f + .2f && Mathf.Abs(p.y) <= size.y * .5f + .2f) pl.Hurt(dmg, Element.Loi, transform.position);
            for (int i = 0; i < 3; i++) RFx.Lightning((Vector2)transform.position + new Vector2(Random.Range(-1f, 1f) * size.x * .5f, -size.y * .5f), (Vector2)transform.position + new Vector2(Random.Range(-1f, 1f) * size.x * .5f, size.y * .5f), col, .25f, .12f);
            RogueAudio.Play(RSfx.Zap, .6f); RFx.Shake(.08f);
        }
        fadeT += dt; sr.color = new Color(col.r, col.g, col.b, Mathf.Clamp01(.6f - fadeT * 1.2f)); if (fadeT > .5f) Destroy(gameObject);
    }
}

/// <summary>Bẫy môi trường theo khu vực (không phải boss): đều có báo hiệu rõ ràng và sát thương nhẹ.</summary>
public class RoomHazards : MonoBehaviour
{
    int biome; float t1, t2, t3; float intensity;

    public static RoomHazards Create(Transform parent, int biome)
    {
        var go = new GameObject("Hazards"); go.transform.SetParent(parent, false);
        var h = go.AddComponent<RoomHazards>(); h.biome = biome; h.intensity = 1f + .25f * (int)RunState.difficulty;
        h.t1 = 4f; h.t2 = 7f; h.t3 = 11f;
        // vài vũng cố định cho đúng không khí của khu vực
        if (biome == 0) { HazardFx.Puddle(new Vector2(-4.8f, -1.8f), 1.2f, 9999f, new Color(.45f, .9f, .3f), Element.Doc, 3f, .35f); HazardFx.Puddle(new Vector2(4.6f, 1.6f), 1.0f, 9999f, new Color(.45f, .9f, .3f), Element.Doc, 3f, .35f); }
        if (biome == 1) { HazardFx.Puddle(new Vector2(-5.2f, 1.4f), 1.3f, 9999f, new Color(.75f, .45f, 1f), Element.Doc, 3f, .3f); HazardFx.Puddle(new Vector2(5.2f, -1.9f), 1.1f, 9999f, new Color(.75f, .45f, 1f), Element.Doc, 3f, .3f); }
        return h;
    }

    Vector2 RandPos() { var pl = Combat.Player; for (int i = 0; i < 12; i++) { var p = new Vector2(Random.Range(-6.8f, 6.8f), Random.Range(-3f, 2.8f)); if (pl == null || Vector2.Distance(p, pl.transform.position) > 1.4f) return p; } return Vector2.zero; }

    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f || !RoomManager.HazardsOn) return;
        t1 -= dt; t2 -= dt; t3 -= dt;
        if (biome == 0)
        {   // ống xì khí độc: vòng báo hiệu rồi phụt khí
            if (t1 <= 0f) { t1 = Random.Range(5.5f, 8f) / intensity; var p = RandPos(); CombatTelegraph.Spawn(p, 1.3f, 1.2f, 7f * intensity, Element.Doc, q => HazardFx.Puddle(q, 1.1f, 3f, new Color(.55f, 1f, .35f), Element.Doc, 3f, .35f)); }
            if (t2 <= 0f) { t2 = Random.Range(10f, 14f) / intensity; var p = RandPos(); CombatTelegraph.Spawn(p, 1.5f, 1.4f, 10f * intensity, Element.Hoa); }
        }
        else if (biome == 1)
        {   // dây leo trồi lên, bào tử bay
            if (t1 <= 0f) { t1 = Random.Range(5f, 7.5f) / intensity; VineSnare.Spawn(RandPos(), 1.2f, 1.2f, 5f * intensity, .8f); }
            if (t2 <= 0f) { t2 = Random.Range(9f, 12f) / intensity; var p = RandPos(); CombatTelegraph.Spawn(p, 1.4f, 1.3f, 8f * intensity, Element.Doc, q => HazardFx.Puddle(q, 1.2f, 4f, new Color(.75f, .45f, 1f), Element.Doc, 3f, .3f)); }
        }
        else
        {   // sàn nhiễm điện theo hàng, tia laze quét
            if (t1 <= 0f) { t1 = Random.Range(6f, 8.5f) / intensity; float x = Random.Range(-6f, 6f); ElectricTile.Spawn(new Vector2(x, -.3f), new Vector2(2.2f, 7f), 1.5f, 9f * intensity); }
            if (t3 <= 0f) { t3 = Random.Range(11f, 15f) / intensity; float y = Random.Range(-2.6f, 2.4f); TeleLine.Spawn(new Vector2(-8f, y), new Vector2(8f, y), .7f, 1.4f, 9f * intensity, Element.Loi, Combat.ElementColor(Element.Loi)); }
        }
    }
}
