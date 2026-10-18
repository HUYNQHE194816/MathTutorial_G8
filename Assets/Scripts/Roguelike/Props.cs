using System.Collections.Generic;
using UnityEngine;

public enum PropKind { Crate, Explosive, Poison, Stone }

/// <summary>
/// Vật cản trong phòng (như thùng gỗ và thùng nổ của Soul Knight): chặn đường đi và chặn đạn, có thể bắn vỡ.
/// Thùng gỗ vỡ ra thỉnh thoảng rơi hồi máu. Thùng nổ nổ lan làm đau cả quái lẫn người chơi và kích nổ thùng bên cạnh.
/// Thùng độc vỡ ra đám khí độc làm độc mọi thứ đứng trong đó. Khối đá không phá được.
/// Là Foe nên mọi loại đạn, vụ nổ, tia sét, lửa đều tác động được; chỉ khác là không bao giờ bị nhắm tự động và không tính vào số quái.
/// </summary>
public class RoomProp : Foe
{
    public static readonly List<RoomProp> All = new List<RoomProp>();
    public PropKind kind; float warnT, pulse; int biome; bool fuse; float fuseT;
    public override bool Targetable => !Dead;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }

    public static RoomProp Create(PropKind kind, Vector2 pos, int biome)
    {
        var go = new GameObject("Prop_" + kind); go.transform.position = pos;
        var p = go.AddComponent<RoomProp>(); p.kind = kind; p.biome = biome; p.Setup();
        Combat.Foes.Add(p); All.Add(p); return p;
    }

    void Setup()
    {
        isProp = true; metal = kind != PropKind.Crate; radius = kind == PropKind.Stone || kind == PropKind.Crate ? .5f : .42f;
        maxHp = hp = kind == PropKind.Crate ? 16f : kind == PropKind.Stone ? 9999f : 12f; bloodCol = kind == PropKind.Crate ? new Color(.75f, .55f, .3f) : kind == PropKind.Poison ? new Color(.5f, 1f, .4f) : new Color(1f, .6f, .3f);
        for (int i = 0; i < resist.Length; i++) resist[i] = 0f;
        var sh = Combat.MakeSr("Shade", transform, RogueArt.Disc, new Color(0, 0, 0, .4f), new Vector2(radius * 2.1f, radius * .8f), 4, new Vector2(0, -radius + .02f));
        body = Combat.MakePx("Body", transform, Sprite(false), Combat.Order(transform.position.y - radius), new Vector2(0, -radius), 1f);
        InitFlash(body);
    }

    Sprite Sprite(bool alt)
    {
        switch (kind)
        {
            case PropKind.Explosive: return RogueArt.Barrel(false, alt);
            case PropKind.Poison: return RogueArt.Barrel(true, alt);
            case PropKind.Stone: return RogueArt.StoneBlock(biome);
            default: return RogueArt.CrateProp(alt);
        }
    }

    public override float TakeHit(float dmg, Element el, Vector2 src, float knock = 0f, bool crit = false, bool silent = false)
    {
        if (Dead) return 0f;
        if (kind == PropKind.Stone) { RFx.Sparks((Vector2)transform.position, ((Vector2)transform.position - src).normalized, new Color(.8f, .8f, .9f), 3, 5f); FlashNow(.05f); return 0f; }
        float r = base.TakeHit(dmg, Element.None, src, 0f, false, true);          // vật cản không nhận nguyên tố, chỉ nhận sát thương
        if (el == Element.Hoa || el == Element.Loi) { if (kind == PropKind.Explosive && !fuse) { fuse = true; fuseT = .35f; } }
        return r;
    }

    protected override void Kill()
    {
        if (Dead) return; Dead = true; Remove(); All.Remove(this);
        Vector2 p = transform.position + Vector3.up * .3f;
        switch (kind)
        {
            case PropKind.Crate:
                RFx.Burst(p, new Color(.8f, .55f, .3f), 12, 4f, .7f, .18f, 9f, false); RFx.Puff(p, new Color(.6f, .5f, .4f), 3, .5f); RogueAudio.Play(RSfx.Rock, .5f, 1.3f);
                if (Random.value < .14f) CombatPickup.SpawnHeal(p, 12f);
                break;
            case PropKind.Explosive:
                RFx.Blast(p, 2.4f, new Color(1f, .6f, .25f), true); RogueAudio.Play(RSfx.Boom, 1f); Combat.HitStop(.07f);
                CombatBolt.Explode(p, 2.6f, 30f, Element.Hoa, true, 7f, new Color(1f, .6f, .25f));
                var pl = Combat.Player; if (pl != null && !pl.Dead && Vector2.Distance(pl.transform.position, p) < 2.6f + pl.radius) pl.Hurt(24f, Element.Hoa, p);
                break;
            case PropKind.Poison:
                RFx.Burst(p, new Color(.6f, 1f, .35f), 16, 4f, .8f, .2f, 3f); RFx.Puff(p, new Color(.5f, .9f, .35f), 8, .8f); RogueAudio.Play(RSfx.Glass, .7f); RogueAudio.Play(RSfx.Poison, .6f);
                var z = HazardFx.Puddle(p, 2.1f, 7f, new Color(.55f, 1f, .35f), Element.Doc, 3.5f, .4f); z.harmFoes = true;
                break;
        }
        Destroy(gameObject);
    }

    void Update()
    {
        if (Dead) return; float dt = Combat.Dt; if (dt <= 0f) return;
        if (!TickStatus(dt)) return;
        bool low = hp < maxHp * .5f || Combat.Now < burnUntil || fuse;
        pulse += dt;
        if (kind == PropKind.Crate) body.sprite = Sprite(hp < maxHp * .5f);
        else if (kind != PropKind.Stone) body.sprite = Sprite(low && ((int)(pulse * 8f) % 2 == 0));
        if (fuse) { fuseT -= dt; if (fuseT <= 0f) { hp = 0f; Kill(); return; } }
        body.sortingOrder = Combat.Order(transform.position.y - radius);
        SyncVisual(Color.white);
    }

    /// <summary>Đẩy một vòng tròn (người chơi, quái) ra khỏi các vật cản.</summary>
    public static Vector2 Resolve(Vector2 pos, float r)
    {
        for (int pass = 0; pass < 2; pass++)
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var p = All[i]; if (p == null || p.Dead) continue;
                Vector2 d = pos - p.Pos; float dist = d.magnitude, min = r + p.radius * .92f;
                if (dist < min) { if (dist < .001f) d = Vector2.up; else d /= dist; pos = p.Pos + d * min; }
            }
        return pos;
    }
}

/// <summary>Linh hồn hộ vệ (buff Linh Hộ Vệ của boss Rồng): bay theo hiệp sĩ và tự bắn vào kẻ địch gần nhất.</summary>
public class SpiritCompanion : MonoBehaviour
{
    public int index; float t, shootT = .8f; SpriteRenderer spr, glow;

    public static SpiritCompanion Create(int index)
    {
        var go = new GameObject("SpiritCompanion" + index); var c = go.AddComponent<SpiritCompanion>(); c.index = index;
        c.glow = Combat.MakeSr("Glow", go.transform, RogueArt.Glow, new Color(.6f, 1f, .75f, .6f), new Vector2(1.4f, 1.4f), Combat.OrdFx - 5); c.glow.sharedMaterial = Gfx.Additive;
        c.spr = Combat.MakePx("Spr", go.transform, RogueArt.OrbitOrb((int)Element.Thao), Combat.OrdFx - 4, null, 1.4f);
        if (Combat.Player != null) go.transform.position = Combat.Player.transform.position;
        return c;
    }

    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f) return; var pl = Combat.Player; if (pl == null || pl.Dead) return;
        t += dt; float a = t * 1.3f + index * 2.1f; Vector2 want = (Vector2)pl.transform.position + new Vector2(Mathf.Cos(a) * (1.1f + index * .25f), .7f + Mathf.Sin(a * 1.7f) * .35f);
        transform.position = Vector2.Lerp(transform.position, want, 1f - Mathf.Exp(-6f * dt));
        spr.sortingOrder = Combat.Order(transform.position.y - 1f) + 3; glow.color = new Color(.6f, 1f, .75f, .45f + .15f * Mathf.Sin(t * 6f));
        shootT -= dt;
        if (shootT <= 0f)
        {
            var f = Combat.Nearest(transform.position, 12f);
            if (f != null)
            {
                shootT = 1.15f; Vector2 d = (f.Pos - (Vector2)transform.position).normalized;
                CombatBolt.Fire(transform.position, new BoltSpec { dir = d, speed = 11f, dmg = 7f, radius = .2f, life = 1.6f, friendly = true, col = new Color(.6f, 1f, .75f), style = BoltStyle.Shard, homing = 2f, size = 1f, el = Element.None });
                RFx.Burst(transform.position, new Color(.6f, 1f, .75f), 3, 2f, .25f, .1f);
            }
            else shootT = .3f;
        }
    }
}
