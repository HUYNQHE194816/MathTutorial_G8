using UnityEngine;

public enum MonsterKind { Gunner, Charger, Mage, Robot, Summoner, Minion }

/// <summary>
/// Quái thường (mục 5.1 và 5.2). Sáu loại, mỗi loại có ba diện mạo theo khu vực (Phòng thí nghiệm hóa, Phòng sinh học, Lab trên mây).
/// Năm loại chính: Xạ thủ, Lao chém, Pháp sư, Lính robot (robot vỡ thì người cưỡi lộ ra), Triệu hồi; cộng Quân nhỏ và Tinh anh
/// (máu x4, sát thương x1,5, to hơn 40%, viền vàng). Hành vi theo từng khu vực dùng nguyên tố và loại đạn khác nhau.
/// Bộ đếm thời gian dùng Combat.Now (không chạy khi đang viết lời giải).
/// </summary>
public class Monster : Foe
{
    public MonsterKind kind; public bool elite; public int phase;       // phase 0: robot còn nguyên, 1: quái đã lộ diện
    public int biome;

    float dmgMul = 1f, hpMul = 1f, speedMul = 1f, rate = 1f, projMul = 1f, speed, atkT, stateT, strafeT, strafeSign = 1f, spawnT = .8f, burstT, step, animT, poseT, bobT, scale = 1f, footY;
    int state, burst, frame; Vector2 dashDir; bool dashHit, faceLeft; Sprite[] frames, riderFrames;
    SpriteRenderer barFill, barBg, shadow, marker, eliteRing, eliteGlow; float barW = 1f, baseFoot; Color tint = Color.white; bool flying;

    public static Monster Create(MonsterKind kind, Vector2 pos, bool elite, int biome = -1)
    {
        var go = new GameObject(kind + (elite ? "_Elite" : "")); go.transform.position = pos;
        var m = go.AddComponent<Monster>(); m.kind = kind; m.elite = elite; m.biome = biome < 0 ? RunState.Biome : biome; m.Setup();
        Combat.Foes.Add(m);
        return m;
    }

    void Setup()
    {
        float hp0, rad; var diff = RunState.difficulty;
        hpMul = RoomRules.HpMul(diff) * (RunState.stage == 1 ? 1f : RunState.stage == 2 ? 1.3f : 1.6f);
        speedMul = RoomRules.SpeedMul(diff); dmgMul = RoomRules.DamageMul(diff); rate = RoomRules.RateMul(diff); projMul = 1f + (speedMul - 1f) * .7f;
        frames = RogueArt.MonsterFrames(biome, (int)kind);
        switch (kind)
        {
            case MonsterKind.Gunner: hp0 = 30f; speed = 2.6f; rad = .5f; break;
            case MonsterKind.Charger: hp0 = 40f; speed = 3.4f; rad = .62f; break;
            case MonsterKind.Mage: hp0 = 25f; speed = 2.2f; rad = .5f; break;
            case MonsterKind.Robot: hp0 = 60f; speed = 2.8f; rad = .8f; metal = true; break;
            case MonsterKind.Summoner: hp0 = 35f; speed = 2.4f; rad = .55f; break;
            default: hp0 = 8f; speed = 3.9f; rad = .3f; break;
        }
        flying = (biome == 2 && (kind == MonsterKind.Gunner || kind == MonsterKind.Minion));
        if (biome == 2 && kind != MonsterKind.Minion && kind != MonsterKind.Summoner) metal = true;
        // nguyên tố mang theo của từng khu vực
        innate = biome == 0 ? (kind == MonsterKind.Robot ? Element.Loi : kind == MonsterKind.Charger ? Element.None : Element.Doc)
               : biome == 1 ? (kind == MonsterKind.Mage || kind == MonsterKind.Charger ? Element.Doc : Element.Thao)
               : (kind == MonsterKind.Mage || kind == MonsterKind.Gunner ? Element.Loi : kind == MonsterKind.Charger ? Element.Phong : Element.Loi);
        if (innate != Element.None) resist[(int)innate] = .5f;
        bloodCol = biome == 0 ? new Color(.45f, .8f, .35f) : biome == 1 ? new Color(.5f, .8f, .3f) : new Color(.5f, .8f, 1f);
        if (metal) bloodCol = new Color(1f, .8f, .4f);

        speed *= speedMul;
        scale = elite ? 1.4f : 1f; if (elite) { hp0 *= 4f; dmgMul *= 1.5f; rad *= 1.4f; }
        hp0 *= hpMul; maxHp = hp = hp0; radius = rad;
        atkT = (kind == MonsterKind.Summoner ? 1.5f : kind == MonsterKind.Minion ? 0f : Random.Range(.6f, 1.6f)) / rate;

        float h = frames[0].bounds.size.y * scale; footY = -Mathf.Min(h * .5f, rad * 1.15f + (flying ? .15f : 0f)); if (flying) footY = -.4f * scale;
        baseFoot = footY;
        shadow = Combat.MakeSr("Shade", transform, RogueArt.Disc, new Color(0, 0, 0, .4f), new Vector2(rad * 2f, rad * .75f), 4, new Vector2(0, footY + (flying ? -.45f : .05f)));
        body = Combat.MakePx("Body", transform, frames[0], Combat.Order(transform.position.y), new Vector2(0, footY), scale);
        InitFlash(body);
        if (elite)
        {
            eliteGlow = Combat.MakeSr("EliteGlow", transform, RogueArt.Glow, new Color(1f, .8f, .3f, .45f), new Vector2(rad * 4.6f, rad * 3.6f), 5, new Vector2(0, footY + .4f)); eliteGlow.sharedMaterial = Gfx.Additive;
            eliteRing = Combat.MakeSr("EliteRing", transform, RogueArt.Ring, new Color(1f, .85f, .35f, .9f), new Vector2(rad * 2.8f, rad * 1.1f), 5, new Vector2(0, footY + .05f)); eliteRing.sharedMaterial = Gfx.Additive;
        }
        marker = Combat.MakeSr("Marker", transform, ThemeGfx.Pixel, new Color(1f, .3f, .3f, 0f), new Vector2(.1f, .1f), 3);
        Vector2 barPos = new Vector2(0, -footY * .0f + frames[0].bounds.size.y * scale * .5f + footY + .35f);
        barBg = Combat.MakeSr("BarBg", transform, ThemeGfx.Pixel, new Color(0, 0, 0, .0f), new Vector2(1.1f, .15f), Combat.OrdFx - 40, barPos);
        barFill = Combat.MakeSr("BarFill", transform, ThemeGfx.Pixel, new Color(.9f, .35f, .35f, 0f), new Vector2(1f, .08f), Combat.OrdFx - 39, barPos);
        barW = barFill.transform.localScale.x;
        body.color = new Color(1, 1, 1, 0f);
        RFx.Ring(transform.position, .9f * scale, new Color(.75f, .55f, 1f), .6f); RFx.Burst(transform.position, new Color(.75f, .55f, 1f), 8, 2f, .6f, .16f, -1f);
    }

    protected override float DamageTakenMul() => 1f;

    protected override void OnHurt(float d)
    {
        if (hp > 0f && elite == false) { }
    }

    protected override void Kill()
    {
        if (kind == MonsterKind.Robot && phase == 0) Unmount(); else Die();
    }

    void Unmount()
    {
        phase = 1; maxHp = hp = 20f * (elite ? 4f : 1f) * hpMul; speed = 2.6f * speedMul; atkT = .5f; burst = 0; state = 0; metal = false;
        riderFrames = RogueArt.RiderFrames(biome); radius *= .65f; frames = riderFrames; body.sprite = frames[0];
        stunUntil = freezeUntil = 0f; auraEl = Element.None; innate = Element.None; for (int i = 0; i < resist.Length; i++) resist[i] = 0f;
        footY = -radius * 1.1f; body.transform.localPosition = new Vector2(0, footY); shadow.transform.localScale *= .7f;
        RFx.Blast(transform.position, 1.6f, new Color(.9f, .8f, .5f), true); RFx.Burst(transform.position, new Color(.7f, .75f, .85f), 14, 5f, .7f, .16f, 8f, false);
        RFx.Text(transform.position + Vector3.up * 1.2f, "Quái lộ diện", GameTheme.EmberHi, .15f); RogueAudio.Play(RSfx.HitMetal, .8f); Combat.HitStop(.06f);
    }

    void Die()
    {
        if (Dead) return;
        Dead = true; Remove(); Killed?.Invoke(this);
        Vector2 p = transform.position;
        RFx.Ichor(p, bloodCol, elite ? 1.6f : 1f); RFx.Burst(p, bloodCol, elite ? 22 : 10, 5f, .6f, .2f, 6f, false); RFx.Flash(p, 1.6f * scale, bloodCol, .16f);
        RFx.Wisp(p + Vector2.up * .3f, new Color(.7f, .85f, 1f)); if (elite) { RFx.Wisp(p, new Color(1f, .85f, .4f)); RFx.Ring(p, 1.8f, new Color(1f, .85f, .4f), .4f); RFx.Shake(.2f); Combat.SlowMo(.25f, .35f); }
        if (metal) { RFx.Burst(p, new Color(.7f, .75f, .85f), 10, 5f, .7f, .15f, 9f, false); }
        RogueAudio.Play(RSfx.Kill, elite ? .9f : .6f); Combat.HitStop(elite ? .09f : .04f);
        Destroy(gameObject);
    }

    // ------------------------------------------------------------------ vòng lặp
    void Update()
    {
        if (Dead) return;
        float dt = Combat.Dt; if (dt <= 0f) return;
        step = dt;

        if (!TickStatus(dt)) return;
        Animate(dt);

        if (spawnT > 0f) { spawnT -= dt; return; }
        if (IsStunned) return;
        var pl = Combat.Player; if (pl == null || pl.Dead) return;

        Vector2 pos = transform.position, to = (Vector2)pl.transform.position - pos;
        float dist = to.magnitude; Vector2 dir = dist > .001f ? to / dist : Vector2.right;
        atkT -= dt; float sp = speed * SpeedMulStatus;
        if (!IsRooted) { }
        if (dir.x != 0f) faceLeft = dir.x < 0f;

        switch (kind)
        {
            case MonsterKind.Gunner:
                strafeT -= dt; if (strafeT <= 0f) { strafeT = Random.Range(1.2f, 2.4f); strafeSign = Random.value < .5f ? -1f : 1f; }
                if (dist < 5f) Move(-dir * sp); else if (dist > 8f) Move(dir * sp); else Move(new Vector2(-dir.y, dir.x) * strafeSign * sp * .6f);
                if (atkT <= 0f) { atkT = 1.7f / rate; poseT = .28f; GunnerShot(pos, dir); }
                break;

            case MonsterKind.Charger:
                if (state == 0)
                {
                    if (dist > 1.5f) Move(dir * sp);
                    if (atkT <= 0f && dist < 6f && !IsRooted) { state = 1; stateT = .55f / Mathf.Sqrt(rate); dashDir = dir; RogueAudio.Play(RSfx.Warn, .35f, 1.4f); }
                }
                else if (state == 1)
                {
                    stateT -= dt; if (stateT <= 0f) { state = 2; stateT = .35f; dashHit = false; RFx.Burst(pos, new Color(.9f, .9f, 1f), 6, 3f, .3f, .14f); RogueAudio.Play(RSfx.Dash, .5f, 1.1f); }
                }
                else
                {
                    Move(dashDir * 12f * Mathf.Lerp(1f, speedMul, .6f) * SpeedMulStatus);
                    if (Random.value < .6f) RFx.Burst(pos, innate == Element.None ? new Color(.9f, .9f, 1f) : Combat.ElementColor(innate), 1, .6f, .3f, .2f);
                    if (!dashHit && dist < radius + pl.radius + .2f) { dashHit = true; pl.Hurt(14f * dmgMul, innate, pos); }
                    stateT -= dt; if (stateT <= 0f) { state = 0; atkT = 1.8f / rate; }
                }
                break;

            case MonsterKind.Mage:
                if (state == 1) { stateT -= dt; if (stateT <= 0f) { state = 0; atkT = 3f / rate; } break; }   // đứng yên khi đang niệm
                if (dist < 6f) Move(-dir * sp); else if (dist > 9f) Move(dir * sp);
                if (atkT <= 0f) { float warn = 1f / Mathf.Sqrt(rate); state = 1; stateT = warn; MageCast(pl, warn); }
                break;

            case MonsterKind.Robot:
                if (phase == 0)
                {
                    if (dist > 5f) Move(dir * sp); else if (dist < 3.5f) Move(-dir * sp * .6f);
                    if (burst > 0) { burstT -= dt; if (burstT <= 0f) { burstT = .14f; burst--; poseT = .16f; RobotShot(pos, dir); } }
                    else if (atkT <= 0f) { burst = 3; burstT = 0f; atkT = 2.2f / rate; }
                }
                else
                {
                    Move(dir * sp * .9f);
                    if (dist < radius + pl.radius + .1f && atkT <= 0f) { pl.Hurt(8f * dmgMul, Element.None, pos); atkT = 1f / rate; }
                }
                break;

            case MonsterKind.Summoner:
                if (dist < 6f) Move(-dir * sp);
                if (state == 1) { stateT -= dt; if (stateT <= 0f) { state = 0; Summon(pos); } break; }
                if (atkT <= 0f) { atkT = 6f / rate; state = 1; stateT = .55f; poseT = .6f; RFx.Ring(pos, 1.2f, Combat.ElementColor(innate == Element.None ? Element.Doc : innate), .55f); RogueAudio.Play(RSfx.Rise, .4f, 1.5f); }
                break;

            default:   // Minion
                Move(dir * sp);
                if (dist < radius + pl.radius && atkT <= 0f) { pl.Hurt(5f * dmgMul, innate, pos); atkT = .8f / rate; }
                break;
        }
    }

    void Summon(Vector2 pos)
    {
        int alive = 0; foreach (var o in Combat.Foes) { var m = o as Monster; if (m != null && !m.Dead && m.kind == MonsterKind.Minion) alive++; }
        int perCast = 2 + ((int)RunState.difficulty >= 2 ? 1 : 0), maxMinions = 4 + (int)RunState.difficulty;
        for (int i = 0; i < perCast && alive < maxMinions; i++, alive++) Create(MonsterKind.Minion, RoomManager.Clamp(pos + Random.insideUnitCircle * 1.3f), false, biome);
        RFx.Blast(pos, 1.2f, Combat.ElementColor(innate == Element.None ? Element.Doc : innate), false); RogueAudio.Play(RSfx.Chest, .35f, 1.6f);
    }

    // ---- đòn đánh theo khu vực ----
    Vector2 Aim(Vector2 dir) { if (!IsBlind) return dir; return Combat.Rot(dir, Random.Range(-40f, 40f)); }

    void GunnerShot(Vector2 pos, Vector2 dir)
    {
        dir = Aim(dir); var s = new BoltSpec { dir = dir, speed = 7f * projMul, dmg = 8f * dmgMul, radius = .24f, life = 3.2f, el = innate };
        if (biome == 0) { s.style = BoltStyle.Vial; s.col = new Color(.5f, 1f, .35f); s.size = .8f; s.silentTrail = false; }
        else if (biome == 1) { s.style = BoltStyle.Leaf; s.col = new Color(.5f, .9f, .4f); s.size = 1f; s.radius = .26f; }
        else { s.style = BoltStyle.Orb; s.col = new Color(.5f, .9f, 1f); s.radius = .22f; s.speed *= 1.15f; }
        Vector2 from = pos + dir * (radius + .1f);
        CombatBolt.Fire(from, s);
        if (biome == 2) { var s2 = new BoltSpec { dir = Combat.Rot(dir, 10f), speed = s.speed, dmg = s.dmg, radius = s.radius, life = 3f, el = innate, col = s.col }; CombatBolt.Fire(from, s2); }
        RFx.Flash(from, .7f, s.col, .1f); RogueAudio.Play(biome == 2 ? RSfx.Zap : RSfx.Poison, .3f, 1.2f);
    }

    void RobotShot(Vector2 pos, Vector2 dir)
    {
        dir = Aim(dir);
        var s = new BoltSpec { dir = dir, speed = 10f * projMul, dmg = 6f * dmgMul, radius = .2f, life = 3f, el = innate, col = Combat.ElementColor(innate == Element.None ? Element.Loi : innate) };
        if (biome == 1) { s.style = BoltStyle.Thorn; s.col = new Color(.7f, .45f, 1f); s.el = Element.Doc; }
        else if (biome == 2) { s.style = BoltStyle.Rocket; s.speed = 6.5f * projMul; s.homing = 1.4f; s.dmg = 8f * dmgMul; s.blast = 1.0f; s.col = new Color(1f, .6f, .3f); s.el = Element.None; s.life = 3.5f; }
        else { s.style = BoltStyle.Orb; }
        CombatBolt.Fire(pos + dir * (radius + .1f), s); RogueAudio.Play(biome == 2 ? RSfx.Rocket : RSfx.Zap, .3f, 1.3f);
    }

    void MageCast(PlayerCombat pl, float warn)
    {
        poseT = warn + .3f; Vector2 at = pl.transform.position;
        if (IsBlind) at += Random.insideUnitCircle * 2.2f;
        Element el = biome == 0 ? Element.Doc : biome == 1 ? Element.Doc : Element.Loi; float dmg = 16f * dmgMul;
        System.Action<Vector2> fire = null;
        if (biome == 0) fire = p => HazardFx.Puddle(p, 1.0f, 3f, new Color(.55f, 1f, .35f), Element.Doc, 3f, .3f);
        else if (biome == 1) fire = p => HazardFx.Puddle(p, 1.1f, 3.5f, new Color(.75f, .45f, 1f), Element.Doc, 3f, .3f);
        else fire = p => RFx.Lightning(p + new Vector2(.4f, 6f), p, Combat.ElementColor(Element.Loi), .3f, .16f);
        CombatTelegraph.Spawn(at, 1.8f, warn, dmg, el, fire);
    }

    void Move(Vector2 v) { if (IsRooted) return; transform.position = RoomManager.Clamp((Vector2)transform.position + v * step); }

    // ------------------------------------------------------------------ hoạt họa
    void Animate(float dt)
    {
        bool moving = state != 1 && !IsStunned && !IsRooted && spawnT <= 0f;
        animT += dt; bobT += dt; poseT -= dt;
        int f;
        if (poseT > 0f || (kind == MonsterKind.Charger && state == 1) || (kind == MonsterKind.Mage && state == 1) || (kind == MonsterKind.Summoner && state == 1)) f = 2 < frames.Length && phase == 0 ? 2 : 0;
        else f = moving ? ((int)(animT / .2f) % 2) : 0;
        if (phase == 1 && frames.Length == 2 && f == 2) f = 0;
        body.sprite = frames[Mathf.Min(f, frames.Length - 1)];
        body.flipX = faceLeft;
        float bob = flying ? Mathf.Sin(bobT * 5f) * .12f : (moving ? Mathf.Abs(Mathf.Sin(animT * 12f)) * .05f : 0f);
        body.transform.localPosition = new Vector2(0f, baseFootY() + bob + (kind == MonsterKind.Charger && state == 1 ? Mathf.Sin(animT * 60f) * .03f : 0f));
        float sx = scale * (kind == MonsterKind.Charger && state == 1 ? 1.06f : 1f), sy = scale * (kind == MonsterKind.Charger && state == 1 ? .94f : 1f);
        body.transform.localScale = new Vector3(sx, sy, 1f);
        body.sortingOrder = Combat.Order(transform.position.y);
        if (eliteRing != null) { eliteRing.color = new Color(1f, .85f, .35f, .6f + .35f * Mathf.Sin(Time.time * 6f)); }
        float a = spawnT > 0f ? .35f + .3f * Mathf.Sin(Combat.Now * 25f) : 1f;
        Color baseC = new Color(1f, 1f, 1f, a);
        // đỏ dần khi gồng lao
        if (kind == MonsterKind.Charger && state == 1) baseC = Color.Lerp(baseC, new Color(1f, .45f, .45f, a), .5f + .5f * Mathf.Sin(Combat.Now * 30f));
        SyncVisual(baseC);
        // vạch báo hướng lao khi đang gồng
        if (kind == MonsterKind.Charger && state == 1)
        {
            float len = 4.2f; marker.transform.position = (Vector2)transform.position + dashDir * len * .5f; marker.transform.eulerAngles = new Vector3(0, 0, Combat.Ang(dashDir));
            marker.transform.localScale = new Vector3(len / marker.sprite.bounds.size.x, .55f / marker.sprite.bounds.size.y, 1f); marker.color = new Color(1f, .25f, .25f, .12f + .18f * Mathf.Sin(Combat.Now * 30f));
        }
        else marker.color = new Color(1, 0, 0, 0);
        if (hp < maxHp)
        {
            barBg.color = new Color(0, 0, 0, .7f); barFill.color = elite ? new Color(1f, .78f, .3f) : new Color(.9f, .35f, .35f);
            float r = Mathf.Clamp01(hp / Mathf.Max(1f, maxHp)); var t = barFill.transform; t.localScale = new Vector3(barW * r, t.localScale.y, 1f); t.localPosition = new Vector3(-(1f - r) * .5f, t.localPosition.y, 0f);
        }
    }

    float baseFootY() => footY;
}
