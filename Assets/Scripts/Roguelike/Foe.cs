using UnityEngine;

/// <summary>
/// Gốc chung của quái và boss: máu, kháng nguyên tố, trạng thái (ướt, cháy, độc, chậm, choáng, đóng băng, trói, hạ giáp, mù),
/// hất lùi, nháy trắng khi trúng đòn và hiệu ứng hạt báo trạng thái. Quái và boss chỉ cần viết AI riêng.
/// </summary>
public abstract class Foe : MonoBehaviour
{
    public static System.Action<Foe> Killed;               // RoomManager gán: tính Mực, rơi hồi máu...
    static float lastHitSfx;

    public float hp, maxHp, radius = .5f; public bool boss, decoy, metal;
    public bool Dead { get; protected set; }
    public virtual bool Targetable => !Dead;
    public Element innate = Element.None;                   // nguyên tố mang theo; kháng nguyên tố này 50%
    public float[] resist = new float[9];
    public float ccResist;                                  // 0: chịu đủ choáng/đóng băng; boss thì cao
    public Color bloodCol = new Color(.5f, .8f, .4f);

    // Trạng thái nguyên tố (Elements.Apply ghi vào)
    public Element auraEl; public float auraT, wetUntil, burnUntil, slowUntil, stunUntil, freezeUntil, rootUntil, poisonUntil, armorBreakUntil, blindUntil, exposedUntil, mudUntil;
    public int poison, frost; public float burnMul = 1f; public float exposedMul = 1.25f;

    protected Vector2 kb; protected SpriteRenderer body, flash; float burnAcc, poisonAcc, vfxT, flashUntil;
    public Vector2 Pos => transform.position;

    public bool IsStunned => Combat.Now < stunUntil || Combat.Now < freezeUntil;
    public bool IsRooted => Combat.Now < rootUntil;
    public bool IsFrozen => Combat.Now < freezeUntil;
    public bool IsBlind => Combat.Now < blindUntil;
    /// <summary>Hệ số tốc độ di chuyển: 0 khi choáng/đóng băng/trói, 0,5 khi chậm, 0,3 khi sa bùn.</summary>
    public float SpeedMulStatus => IsStunned || IsRooted ? 0f : (Combat.Now < mudUntil ? .3f : Combat.Now < slowUntil ? (boss ? .65f : .5f) : 1f);

    public void Stun(float d) { stunUntil = Mathf.Max(stunUntil, Combat.Now + d * (1f - ccResist)); }
    public void Freeze(float d) { freezeUntil = Mathf.Max(freezeUntil, Combat.Now + d * (1f - ccResist)); stunUntil = Mathf.Max(stunUntil, freezeUntil); }
    public void Root(float d) { rootUntil = Mathf.Max(rootUntil, Combat.Now + d * (1f - ccResist * .8f)); }
    public void Knock(Vector2 v) { kb += v * (1f - ccResist); }

    protected void InitFlash(SpriteRenderer b)
    {
        body = b;
        var go = new GameObject("Flash"); go.transform.SetParent(b.transform, false);
        flash = go.AddComponent<SpriteRenderer>(); flash.sortingOrder = b.sortingOrder + 1; flash.color = new Color(1, 1, 1, 0);
    }

    public void FlashNow(float d = .09f) { flashUntil = Combat.Now + d; }

    /// <summary>Gọi mỗi khung: đồng bộ sprite nháy, tint theo trạng thái và depth sorting theo y.</summary>
    protected void SyncVisual(Color baseCol)
    {
        if (body == null) return;
        if (flash != null)
        {
            flash.sprite = RogueArt.Silhouette(body.sprite); flash.flipX = body.flipX; flash.sortingOrder = body.sortingOrder + 1;
            float f = Combat.Now < flashUntil ? .85f : 0f; flash.color = new Color(1f, 1f, 1f, f);
        }
        Color c = baseCol;
        if (Combat.Now < freezeUntil) c = Color.Lerp(c, Combat.ElementColor(Element.Bang), .65f);
        else
        {
            if (Combat.Now < burnUntil) c = Color.Lerp(c, new Color(1f, .5f, .2f), .25f + .1f * Mathf.Sin(Time.time * 18f));
            if (poison > 0 && Combat.Now < poisonUntil) c = Color.Lerp(c, new Color(.6f, 1f, .4f), .22f);
            if (Combat.Now < wetUntil) c = Color.Lerp(c, new Color(.5f, .75f, 1f), .22f);
            if (Combat.Now < armorBreakUntil) c = Color.Lerp(c, new Color(1f, .7f, .35f), .18f);
            if (Combat.Now < stunUntil) { float s = Mathf.Sin(Time.time * 30f) * .5f + .5f; c = Color.Lerp(c, new Color(1f, .95f, .4f), .25f * s); }
        }
        body.color = c;
    }

    /// <summary>Nhận sát thương. Trả về sát thương thực nhận.</summary>
    public virtual float TakeHit(float dmg, Element el, Vector2 src, float knock = 0f, bool crit = false, bool silent = false)
    {
        if (Dead || dmg <= 0f || !Targetable) return 0f;
        float m = 1f;
        if (el != Element.None) m *= 1f - resist[(int)el];
        if (Combat.Now < wetUntil && (el == Element.Loi || el == Element.Bang)) m *= 1.25f;      // ướt làm tăng sát thương lôi và băng
        if (Combat.Now < armorBreakUntil) m *= 1.25f;
        if (Combat.Now < exposedUntil) m *= exposedMul;
        float d = dmg * m * DamageTakenMul();
        hp -= d; FlashNow();
        if (knock > 0f) Knock(((Vector2)transform.position - src).normalized * knock);
        Vector2 dir = ((Vector2)transform.position - src).normalized; if (dir.sqrMagnitude < .01f) dir = Vector2.up;
        Color tc = crit ? new Color(1f, .9f, .3f) : el == Element.None ? Color.white : Combat.ElementColor(el);
        RFx.Text(transform.position + Vector3.up * (radius + .4f), (crit ? "" : "") + Mathf.RoundToInt(d) + (crit ? "!" : ""), tc, crit ? .22f : .14f);
        if (!silent)
        {
            RFx.Sparks(transform.position - (Vector3)dir * radius * .4f, dir, el == Element.None ? new Color(1f, .9f, .6f) : Combat.ElementColor(el), crit ? 8 : 4);
            if (Time.unscaledTime - lastHitSfx > .035f) { lastHitSfx = Time.unscaledTime; RogueAudio.Play(boss ? RSfx.BossHit : crit ? RSfx.Crit : metal ? RSfx.HitMetal : RSfx.HitFlesh, boss ? .7f : .5f); }
            if (crit) { Combat.HitStop(.045f); RFx.Shake(.1f); RFx.Ring(transform.position, .8f, new Color(1f, .9f, .3f), .2f); }
            else if (knock > 3f) Combat.HitStop(.03f);
        }
        OnHurt(d);
        if (el != Element.None) Elements.Apply(this, el, src);
        if (hp <= 0f && !Dead) Kill();
        return d;
    }

    protected virtual float DamageTakenMul() => 1f;
    protected virtual void OnHurt(float d) { }
    protected abstract void Kill();

    protected void Remove() { Combat.Foes.Remove(this); }

    /// <summary>Đốt, độc, hất lùi và hạt trạng thái. Trả về false nếu đã chết giữa chừng.</summary>
    protected bool TickStatus(float dt)
    {
        if (Combat.Now < burnUntil) { burnAcc += dt; if (burnAcc >= .5f) { burnAcc -= .5f; TakeHit(2f * burnMul, Element.Hoa, transform.position, 0f, false, true); if (Dead) return false; } }
        if (poison > 0)
        {
            if (Combat.Now >= poisonUntil) poison = 0;
            else { poisonAcc += dt; if (poisonAcc >= 1f) { poisonAcc -= 1f; TakeHit(2.2f * poison, Element.Doc, transform.position, 0f, false, true); if (Dead) return false; } }
        }
        if (Combat.Now >= wetUntil && Combat.Now >= slowUntil) frost = Mathf.Min(frost, 2);
        if (kb.sqrMagnitude > .01f) { transform.position = RoomManager.Clamp((Vector2)transform.position + kb * dt); kb *= Mathf.Exp(-9f * dt); } else kb = Vector2.zero;
        vfxT -= dt;
        if (vfxT <= 0f)
        {
            vfxT = .16f; Vector2 p = (Vector2)transform.position + Random.insideUnitCircle * radius * .6f;
            if (Combat.Now < burnUntil) RFx.Burst(p, Combat.ElementColor(Element.Hoa), 1, 1.2f, .4f, .16f, -3f);
            if (poison > 0) RFx.Burst(p, Combat.ElementColor(Element.Doc), 1, .8f, .55f, .13f, -1.5f);
            if (Combat.Now < wetUntil) RFx.Burst(p, Combat.ElementColor(Element.Thuy), 1, .5f, .4f, .1f, 5f);
            if (IsFrozen) RFx.Burst(p, Combat.ElementColor(Element.Bang), 1, .4f, .5f, .1f, 0f);
            if (IsRooted) RFx.Burst((Vector2)transform.position + Vector2.down * radius * .6f, Combat.ElementColor(Element.Thao), 1, .5f, .5f, .12f, 0f);
            if (Combat.Now < stunUntil && !IsFrozen) RFx.Burst((Vector2)transform.position + Vector2.up * radius, new Color(1f, .95f, .4f), 1, 1.2f, .3f, .1f, 0f);
        }
        return true;
    }

    void OnDestroy() { Combat.Foes.Remove(this); }
}
