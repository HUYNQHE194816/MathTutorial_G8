using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Hiệp sĩ trong trận (mục 5.6): WASD đi, chuột ngắm và bắn (giữ chuột trái hoặc phím J), Space lướt (hồi 3 giây, miễn sát thương 0,2 giây),
/// Q đổi vũ khí chính/phụ. Mười hai vũ khí, mỗi loại một cách đánh riêng (xem Attack). Máu, vũ khí và buff nằm trong RunState nên sống qua các màn.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    public static float MaxHp => 100f + 10f * Meta.PermHp;
    public const float Speed = 6.2f, DashCooldown = 3f;
    public float radius = .45f;
    public bool Dead { get; private set; }
    public System.Action Died;
    public float DashLeft => Mathf.Max(0f, dashCd);
    public float Hp => RunState.hp;
    public bool Controllable = true;                                // tắt khi đang chiếu cảnh phim
    public float HeatFrac => heat; public bool Overheated => overheat; public int Ammo => ammo; public bool Reloading => reload > 0f; public float Charge => charge;

    float cd, dashCd, dashT, invuln, hurtFlash, shield, shieldMax; Vector2 dashDir, facing = Vector2.up, kb;
    SpriteRenderer body, weaponSr, flashSr, shadow, shieldSr; Transform weaponPivot; WeaponId shownWeapon = (WeaponId)(-1); int shownEl = -1, shownRar = -1;
    float animT, recoil, heat; bool overheat; float charge; bool charging; int ammo = -1; float reload; float rifleBloom; float spinT, thrustT; Vector2 thrustDir; float afterT;
    float slowUntil, slowMul = 1f, rootUntil, stunUntil, burnUntil, poisonUntil; int poison; float poisonAcc, burnAcc, statusVfx, swapT, sideFlip = 1f;
    float beamUntil; readonly List<Transform> orbs = new List<Transform>(); float orbAng, orbShootT; WeaponId orbOwner = (WeaponId)(-1);
    SpriteRenderer cross; Sprite[] idle, run;

    public static PlayerCombat Create(Vector2 pos)
    {
        var go = new GameObject("Player"); go.transform.position = pos;
        var p = go.AddComponent<PlayerCombat>(); p.Build();
        if (RunState.hp <= 0f) RunState.hp = MaxHp;
        RunState.hp = Mathf.Min(RunState.hp, MaxHp);
        return p;
    }

    void Build()
    {
        idle = RogueArt.KnightIdle; run = RogueArt.KnightRun;
        shadow = Combat.MakeSr("Shade", transform, RogueArt.Disc, new Color(0, 0, 0, .45f), new Vector2(1.5f, .5f), 4, new Vector2(0, -1.0f));
        body = Combat.MakePx("Body", transform, idle[0], Combat.OrdChar, new Vector2(0, -1.0f));
        var fg = new GameObject("Flash"); fg.transform.SetParent(body.transform, false); flashSr = fg.AddComponent<SpriteRenderer>(); flashSr.color = new Color(1, 1, 1, 0);
        weaponPivot = new GameObject("WeaponPivot").transform; weaponPivot.SetParent(transform, false); weaponPivot.localPosition = new Vector2(0, -.05f);
        weaponSr = Combat.MakePx("Weapon", weaponPivot, RogueArt.Weapon(0, 0), Combat.OrdChar + 3, new Vector2(.35f, 0));
        shieldSr = Combat.MakeSr("Shield", transform, RogueArt.Disc, new Color(.5f, .8f, 1f, 0f), new Vector2(2.6f, 2.6f), Combat.OrdChar + 6, new Vector2(0, -.1f)); shieldSr.sharedMaterial = Gfx.Additive;
        var bl = new GameObject("Beam"); bl.transform.SetParent(transform, false);
        // chấm ngắm (thay con trỏ chuột trong trận)
        var cg = new GameObject("Crosshair"); cross = cg.AddComponent<SpriteRenderer>(); cross.sprite = RogueArt.Ring; cross.sortingOrder = Combat.OrdFx + 50; cg.transform.localScale = Vector3.one * .075f; cross.sharedMaterial = Gfx.Additive;
    }

    void OnDestroy() { if (cross != null) Destroy(cross.gameObject); Cursor.visible = true; foreach (var o in orbs) if (o != null) Destroy(o.gameObject); }

    // ------------------------------------------------------------------ sát thương, trạng thái, hồi máu
    public void AddShield(float amount, float seconds) { shield = amount; shieldMax = amount; shieldUntil = Combat.Now + seconds; }
    float shieldUntil;
    public float Shield => Combat.Now < shieldUntil ? shield : 0f;
    public bool Invulnerable => invuln > 0f;
    public void GiveInvuln(float t) { invuln = Mathf.Max(invuln, t); }

    public void Hurt(float d, Element e, Vector2 src = default(Vector2), bool dot = false, bool force = false)
    {
        if (Dead || d <= 0f) return;
        if (invuln > 0f && !dot && !force) return;
        d *= 1f - RunState.resist[(int)e];                           // buff Kháng nguyên tố
        if (Combat.Now < shieldUntil && shield > 0f)
        {
            float ab = Mathf.Min(shield, d); shield -= ab; d -= ab; RFx.Burst(transform.position, new Color(.6f, .85f, 1f), 6, 3f, .4f, .14f); RogueAudio.Play(RSfx.Shield, .5f, 1.4f);
            if (shield <= 0f) { RFx.Ring(transform.position, 1.6f, new Color(.6f, .85f, 1f), .3f); RogueAudio.Play(RSfx.ShieldBreak, .7f); }
            if (d <= .01f) { invuln = Mathf.Max(invuln, .25f); return; }
        }
        RunState.hp = Mathf.Max(0f, RunState.hp - d); if (!dot) { invuln = .6f; hurtFlash = .15f; Combat.HitStop(.06f); RFx.Shake(.28f); RoomManager.Instance?.HurtFlash(); }
        RFx.Text(transform.position + Vector3.up * 1.3f, "-" + Mathf.RoundToInt(d), GameTheme.Blood, .16f);
        RFx.Burst(transform.position, new Color(.9f, .2f, .3f), dot ? 2 : 8, 3f, .45f, .14f, 5f, false);
        RogueAudio.Play(RSfx.Hurt, dot ? .25f : .8f);
        if (!dot && src != default(Vector2)) kb += ((Vector2)transform.position - src).normalized * 4f;
        // hiệu ứng nguyên tố của đòn đánh lên người chơi
        float k = 1f - RunState.resist[(int)e];
        if (!dot && k > .01f) switch (e)
            {
                case Element.Doc: poison = Mathf.Min(4, poison + 1); poisonUntil = Combat.Now + 4f * k; break;
                case Element.Hoa: burnUntil = Combat.Now + 2.5f * k; break;
                case Element.Bang: Slow(2f * k, .5f); break;
                case Element.Loi: stunUntil = Combat.Now + .25f * k; break;
                case Element.Nham: case Element.Phong: kb += ((Vector2)transform.position - src).normalized * 6f; break;
                case Element.Thao: Root(.7f * k); break;
            }
        if (RunState.hp <= 0f) Die();
    }

    void Die()
    {
        Dead = true; RFx.Blast(transform.position, 2f, new Color(.9f, .2f, .3f), true); RFx.Wisp(transform.position, new Color(.8f, .9f, 1f)); Combat.SlowMo(.8f, .25f);
        body.enabled = false; weaponSr.enabled = false; shadow.enabled = false; Died?.Invoke();
    }

    public void Slow(float dur, float mul) { if (Combat.Now >= slowUntil) slowMul = mul; else slowMul = Mathf.Min(slowMul, mul); slowUntil = Mathf.Max(slowUntil, Combat.Now + dur); }
    public void Root(float dur) { rootUntil = Mathf.Max(rootUntil, Combat.Now + dur); RFx.Burst(transform.position + Vector3.down * .9f, Combat.ElementColor(Element.Thao), 6, 1.6f, .5f, .14f); }

    public void Heal(float a)
    {
        if (Dead || a <= 0f) return;
        RunState.hp = Mathf.Min(MaxHp, RunState.hp + a);
        RFx.Text(transform.position + Vector3.up * 1.3f, "+" + Mathf.RoundToInt(a), GameTheme.Moss, .15f);
    }

    public void OnKill() { if (RunState.killHeal > 0) Heal(3f * RunState.killHeal); if (RunState.main != null && RunState.main.id == WeaponId.Scythe) Heal(2f); }

    // ------------------------------------------------------------------ vòng lặp
    Vector2 AimDir()
    {
        var cam = Camera.main;
        if (cam == null || Mouse.current == null) return facing;
        Vector3 m = Mouse.current.position.ReadValue(); m.z = -cam.transform.position.z;
        Vector2 w = cam.ScreenToWorldPoint(m); if (cross != null) cross.transform.position = w;
        Vector2 d = w - (Vector2)transform.position;
        return d.sqrMagnitude < .01f ? facing : d.normalized;
    }

    void Update()
    {
        float dt = Combat.Dt;
        bool playing = !RoomManager.Frozen && Controllable && !Dead; Cursor.visible = !playing || (cross == null);
        if (cross != null) { cross.enabled = playing; Cursor.visible = !playing; }
        if (dt <= 0f || Dead) { AnimateBody(0f, Vector2.zero, facing); return; }
        var k = Keyboard.current; if (k == null) return;
        if (invuln > 0f) invuln -= dt; if (cd > 0f) cd -= dt; if (dashCd > 0f) dashCd -= dt; if (swapT > 0f) swapT -= dt; if (hurtFlash > 0f) hurtFlash -= dt;
        Status(dt);

        Vector2 mv = Vector2.zero; Vector2 aim = AimDir(); facing = aim;
        if (Controllable)
        {
            float x = ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1f : 0f) - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1f : 0f);
            float y = ((k.wKey.isPressed || k.upArrowKey.isPressed) ? 1f : 0f) - ((k.sKey.isPressed || k.downArrowKey.isPressed) ? 1f : 0f);
            mv = new Vector2(x, y); if (mv.sqrMagnitude > 1f) mv.Normalize();
            if (Combat.Now < stunUntil) mv = Vector2.zero;
        }

        if (Controllable && k.spaceKey.wasPressedThisFrame && dashCd <= 0f && dashT <= 0f && Combat.Now >= stunUntil)
        { dashT = .2f; dashCd = DashCooldown; invuln = Mathf.Max(invuln, .22f); dashDir = mv.sqrMagnitude > .01f ? mv : aim; RogueAudio.Play(RSfx.Dash, .6f); RFx.Puff(transform.position + Vector3.down * .9f, new Color(.7f, .68f, .8f), 4, .4f); }

        float spd = Speed * (Combat.Now < slowUntil ? slowMul : 1f) * (Combat.Now < rootUntil ? 0f : 1f);
        if (dashT > 0f) { dashT -= dt; Step(dashDir * 18f * dt); afterT -= dt; if (afterT <= 0f) { afterT = .03f; Afterimage(); } } else Step(mv * spd * dt);
        if (kb.sqrMagnitude > .01f) { Step(kb * dt); kb *= Mathf.Exp(-10f * dt); }
        if (Combat.Gravity.sqrMagnitude > .01f) Step(Combat.Gravity * dt);

        if (Controllable && k.qKey.wasPressedThisFrame && RunState.sub != null) { var t = RunState.main; RunState.main = RunState.sub; RunState.sub = t; swapT = .25f; charge = 0f; charging = false; ammo = -1; RogueAudio.Play(RSfx.Swap, .6f); RFx.Burst(transform.position, Color.white, 6, 2f, .3f, .12f); }

        RefreshWeaponVisual();
        bool fire = Controllable && (k.jKey.isPressed || (Mouse.current != null && Mouse.current.leftButton.isPressed && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())));
        UpdateWeapon(dt, fire, aim);
        AnimateBody(dt, mv, aim);
    }

    void Status(float dt)
    {
        if (Combat.Now < burnUntil) { burnAcc += dt; if (burnAcc >= .5f) { burnAcc -= .5f; Hurt(1.5f, Element.None, default(Vector2), true); } }
        if (poison > 0) { if (Combat.Now >= poisonUntil) poison = 0; else { poisonAcc += dt; if (poisonAcc >= 1f) { poisonAcc -= 1f; Hurt(1.8f * poison, Element.None, default(Vector2), true); } } }
        statusVfx -= dt;
        if (statusVfx <= 0f)
        {
            statusVfx = .2f; Vector2 p = (Vector2)transform.position + Random.insideUnitCircle * .5f;
            if (Combat.Now < burnUntil) RFx.Burst(p, Combat.ElementColor(Element.Hoa), 1, 1f, .4f, .14f, -3f);
            if (poison > 0) RFx.Burst(p, Combat.ElementColor(Element.Doc), 1, .7f, .5f, .12f, -1.5f);
            if (Combat.Now < slowUntil) RFx.Burst(p, Combat.ElementColor(Element.Bang), 1, .3f, .4f, .1f);
        }
        if (shieldSr != null)
        {
            bool on = Combat.Now < shieldUntil && shield > 0f; float a = on ? .22f + .1f * Mathf.Sin(Time.time * 6f) : 0f; shieldSr.color = new Color(.5f, .8f, 1f, a);
        }
    }

    void Step(Vector2 d)
    {
        Vector2 p = (Vector2)transform.position + d;
        p.x = Mathf.Clamp(p.x, RoomManager.MinX, RoomManager.MaxX);
        bool inDoor = RoomManager.Instance != null && RoomManager.Instance.DoorOpen && Mathf.Abs(p.x) < RoomManager.DoorHalf - .2f;
        p.y = Mathf.Clamp(p.y, RoomManager.MinY, inDoor ? RoomManager.ExitY + .5f : RoomManager.WallY);
        transform.position = p;
    }

    void Afterimage()
    {
        var go = new GameObject("After"); go.transform.position = body.transform.position; go.transform.localScale = body.transform.localScale;
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = body.sprite; sr.flipX = body.flipX; sr.sortingOrder = Combat.OrdChar - 1; sr.color = new Color(.6f, .8f, 1f, .6f); sr.sharedMaterial = Gfx.Additive;
        go.AddComponent<AfterFade>();
    }

    // ------------------------------------------------------------------ hoạt họa thân và vũ khí
    void AnimateBody(float dt, Vector2 mv, Vector2 aim)
    {
        bool moving = mv.sqrMagnitude > .01f || dashT > 0f; animT += Time.deltaTime * (Combat.Dt > 0f ? 1f : 0f);
        Sprite s;
        if (dashT > 0f) s = RogueArt.KnightDash;
        else if (moving) s = run[(int)(animT * 12f) % run.Length]; else s = idle[(int)(animT * 4f) % idle.Length];
        body.sprite = s; bool left = aim.x < 0f; if (dashT > 0f) left = dashDir.x < 0f; body.flipX = left;
        float squash = moving ? 1f + .04f * Mathf.Sin(animT * 24f) : 1f + .015f * Mathf.Sin(animT * 6f);
        body.transform.localScale = new Vector3(1f / squash, squash, 1f);
        body.sortingOrder = Combat.Order(transform.position.y);
        flashSr.sprite = RogueArt.Silhouette(s); flashSr.flipX = body.flipX; flashSr.sortingOrder = body.sortingOrder + 1; flashSr.color = new Color(1f, .4f, .4f, hurtFlash > 0f ? .8f : 0f);
        var c = Color.white; c.a = invuln > 0f && dashT <= 0f && ((int)(Time.time * 20f) % 2 == 0) ? .35f : 1f; body.color = c;
        shadow.transform.localScale = new Vector3(1.5f, .5f, 1f) * (dashT > 0f ? .8f : 1f);

        // vũ khí xoay theo hướng ngắm, giật lùi khi bắn, lật theo trục y để luôn xuôi chiều
        float ang = Combat.Ang(aim); if (spinT > 0f) ang = spinAngle;
        weaponPivot.localEulerAngles = new Vector3(0, 0, ang);
        float rc = recoil; recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 8f);
        float thr = thrustT > 0f ? Mathf.Sin(Mathf.Clamp01(1f - thrustT / .2f) * Mathf.PI) * 1.2f : 0f;
        weaponSr.transform.localPosition = new Vector2(.4f - rc * .25f + thr, Mathf.Sin(animT * 5f) * .02f);
        weaponSr.transform.localScale = new Vector3(1f, left ? -1f : 1f, 1f);
        weaponSr.sortingOrder = aim.y > .35f ? body.sortingOrder - 1 : body.sortingOrder + 2;
        if (swapT > 0f) weaponSr.transform.localScale = new Vector3(1f, (left ? -1f : 1f) * (1f - swapT / .25f), 1f);
        if (thrustT > 0f) thrustT -= Time.deltaTime;
        if (RunState.main != null && RunState.main.id == WeaponId.Bow) { float ch = charge; weaponSr.transform.localPosition += new Vector3(-ch * .12f, 0, 0); }
        if (RunState.main != null && (RunState.main.id == WeaponId.Orb)) weaponSr.transform.localPosition = new Vector2(0, .6f + Mathf.Sin(animT * 3f) * .12f);
        if (RunState.main != null && RunState.main.id == WeaponId.Orb) weaponPivot.localEulerAngles = Vector3.zero;
        if (RunState.main != null && RunState.main.id == WeaponId.Orb) weaponSr.transform.localScale = Vector3.one * (1f + .08f * Mathf.Sin(animT * 6f));
    }

    float spinAngle;

    void RefreshWeaponVisual()
    {
        var w = RunState.main; if (w == null) return;
        if (w.id != shownWeapon || (int)w.element != shownEl || w.rarity != shownRar)
        {
            shownWeapon = w.id; shownEl = (int)w.element; shownRar = w.rarity; weaponSr.sprite = RogueArt.Weapon((int)w.id, w.rarity, (int)w.element);
            if (orbOwner != w.id) { foreach (var o in orbs) if (o != null) Destroy(o.gameObject); orbs.Clear(); orbOwner = w.id; }
        }
        // huyền thoại lấp lánh
        if (w.rarity >= 2 && Random.value < Time.deltaTime * (w.rarity == 3 ? 10f : 5f)) RFx.Burst((Vector2)weaponSr.transform.position + Random.insideUnitCircle * .5f, RogueArt.RarityCols[w.rarity], 1, .6f, .5f, .1f, -1f);
    }

    // ------------------------------------------------------------------ tấn công
    float CdMul => 1f / (1f + .15f * RunState.atkSpeedStacks);
    Element Elem(WeaponInstance w) => w.element != Element.None ? w.element : RunState.infuse;      // buff Nhiễm nguyên tố chỉ cho vũ khí chưa có nguyên tố
    Color Tint(Element el) => el == Element.None ? new Color(1f, .93f, .7f) : Combat.ElementColor(el);
    Vector2 Muzzle(Vector2 aim, float off = 1.1f) => (Vector2)transform.position + aim * off + new Vector2(0, -.05f);

    void UpdateWeapon(float dt, bool fire, Vector2 aim)
    {
        var w = RunState.main; if (w == null) return; var def = w.Def;
        rifleBloom = Mathf.MoveTowards(rifleBloom, 0f, dt * 14f);
        // quá nhiệt của súng laze, nạp đạn của tên lửa
        if (def.kind == WKind.Beam) { if (fire && !overheat) heat = Mathf.Min(1f, heat + dt * .42f); else heat = Mathf.Max(0f, heat - dt * (overheat ? .3f : .5f)); if (heat >= 1f) { overheat = true; RFx.Burst(transform.position, new Color(1f, .5f, .2f), 8, 2f, .5f, .14f, -2f); RogueAudio.Play(RSfx.ShieldBreak, .4f, 1.5f); } if (overheat && heat <= .4f) overheat = false; }
        else { heat = Mathf.Max(0f, heat - dt); overheat = false; }
        if (def.kind == WKind.Rocket) { if (ammo < 0) ammo = 4 + RunState.extraShots; if (reload > 0f) { reload -= dt; if (reload <= 0f) { ammo = 4 + RunState.extraShots; RogueAudio.Play(RSfx.Swap, .5f, 1.3f); } } }
        if (def.kind == WKind.Beam) { if (beamUntil > Time.time) { } }
        if (def.kind == WKind.Orbit) UpdateOrbs(dt, fire, aim, w);

        // cung: giữ để lên lực, thả để bắn
        if (def.kind == WKind.Bow)
        {
            if (fire && cd <= 0f) { if (!charging) { charging = true; charge = 0f; RogueAudio.Play(RSfx.BowDraw, .5f); } charge = Mathf.Min(1f, charge + dt / .75f); if (charge >= 1f && Random.value < .3f) RFx.Burst(Muzzle(aim, 1.3f), Tint(Elem(w)), 1, 1f, .25f, .12f); }
            else if (charging) { ReleaseBow(w, aim); }
            return;
        }
        if (!fire || cd > 0f) return;
        if (def.kind == WKind.Rocket && reload > 0f) return;
        Attack(w, aim);
    }

    void Attack(WeaponInstance w, Vector2 aim)
    {
        var def = w.Def; Element el = Elem(w); float dmg = w.Damage; Color tint = Tint(el);
        Vector2 pos = transform.position;
        cd = def.cooldown * CdMul;
        int bonus = RunState.extraShots; int pierce = def.pierce + RunState.extraPierce; int bnc = RunState.bounces;
        switch (def.kind)
        {
            case WKind.Gun: case WKind.Auto:
                {
                    bool auto = def.kind == WKind.Auto; int n = def.count + bonus;
                    for (int i = 0; i < n; i++)
                    {
                        float ang = n > 1 ? (i - (n - 1) * .5f) * 9f : 0f;
                        if (auto) { ang += Random.Range(-1f, 1f) * (2f + rifleBloom); }
                        var d = Combat.Rot(aim, ang);
                        CombatBolt.Fire(Muzzle(d), new BoltSpec { dir = d, speed = def.speed, dmg = dmg, radius = auto ? .16f : .2f, life = 1.4f, el = el, pierce = pierce, bounce = bnc, friendly = true, col = tint, style = BoltStyle.Orb, size = auto ? .8f : 1f, knock = auto ? 0f : 1f });
                    }
                    if (auto) rifleBloom = Mathf.Min(9f, rifleBloom + 1.3f);
                    RFx.Flash(Muzzle(aim, 1.5f), auto ? .8f : 1.1f, tint, .07f); RFx.Sparks(Muzzle(aim, 1.5f), aim, tint, 3, 5f, 30f); RFx.Shell(pos + aim * .5f, aim);
                    recoil = auto ? .6f : 1f; RFx.Shake(auto ? .03f : .06f); RogueAudio.Play(auto ? RSfx.Rifle : RSfx.Pistol, auto ? .45f : .6f);
                    break;
                }
            case WKind.Beam: FireBeam(w, aim, dmg, el, tint, pierce + 1, bnc); break;
            case WKind.Stab: MeleeArc(w, aim, def.range, 115f, dmg, el, tint, RSfx.Slash, 1.3f, 0f); break;
            case WKind.Reap: MeleeArc(w, aim, def.range, 165f, dmg, el, tint, RSfx.Scythe, 2.2f, 3f); break;
            case WKind.Thrust: ThrustAttack(w, aim, def.range + RunState.extraPierce * .3f, dmg, el, tint); break;
            case WKind.Spin: SpinAttack(w, def.range, dmg, el, tint); break;
            case WKind.Blast:
                {
                    int n = 1 + (bonus > 0 ? 1 : 0);
                    for (int i = 0; i < n; i++)
                    {
                        var d = Combat.Rot(aim, n > 1 ? (i - .5f) * 10f : 0f);
                        CombatBolt.Fire(Muzzle(d, 1.4f), new BoltSpec { dir = d, speed = def.speed, dmg = dmg, radius = .3f, life = 1.5f, el = el, friendly = true, col = tint, style = BoltStyle.Cannon, blast = 2.4f, knock = 7f, bounce = bnc, size = 1f });
                    }
                    RFx.Blast(Muzzle(aim, 1.6f), .7f, new Color(1f, .7f, .3f), false); RFx.Puff(Muzzle(aim, 1.5f), new Color(.5f, .48f, .54f), 5, .6f); RFx.Shake(.4f);
                    kb += -aim * 6f; recoil = 2f; RogueAudio.Play(RSfx.Cannon, .9f); Combat.HitStop(.03f);
                    break;
                }
            case WKind.Rocket:
                {
                    if (ammo < 0) ammo = 4 + bonus; ammo--; if (ammo <= 0) { reload = 1.4f; cd = .45f; }
                    int n = 1 + (bonus > 0 && ammo >= 0 ? 0 : 0);
                    var d = Combat.Rot(aim, Random.Range(-4f, 4f));
                    CombatBolt.Fire(Muzzle(d, 1.3f), new BoltSpec { dir = d, speed = def.speed, accel = 9f, homing = 1.7f, dmg = dmg, radius = .24f, life = 3f, el = el, friendly = true, col = new Color(1f, .6f, .25f), style = BoltStyle.Rocket, blast = 1.6f, knock = 4f, bounce = 0 });
                    RFx.Puff(Muzzle(aim, 1.2f), new Color(.7f, .68f, .76f), 3, .45f); RFx.Flash(Muzzle(aim, 1.3f), 1.1f, new Color(1f, .7f, .3f), .08f); recoil = 1.1f; RFx.Shake(.1f); RogueAudio.Play(RSfx.Rocket, .7f);
                    break;
                }
            case WKind.Wand:
                {
                    int n = def.count + bonus; Element we = el == Element.None ? Weapons.Elems[(int)(Time.time) % Weapons.Elems.Length] : el;
                    for (int i = 0; i < n; i++)
                    {
                        float ang = n > 1 ? (i - (n - 1) * .5f) * 11f : 0f; var d = Combat.Rot(aim, ang);
                        CombatBolt.Fire(Muzzle(d, 1.5f), new BoltSpec { dir = d, speed = def.speed, dmg = dmg, radius = .24f, life = 1.7f, el = we, pierce = pierce, bounce = bnc, friendly = true, col = Combat.ElementColor(we), style = BoltStyle.Orb, size = 1.2f, curve = i % 2 == 0 ? 25f : -25f });
                    }
                    RFx.Burst(Muzzle(aim, 1.6f), Combat.ElementColor(we), 5, 3f, .3f, .14f); RFx.Flash(Muzzle(aim, 1.5f), 1.0f, Combat.ElementColor(we), .08f); recoil = .5f; RogueAudio.Play(RSfx.Staff, .5f);
                    break;
                }
            case WKind.Orbit: OrbBarrage(w, aim, dmg, el, tint); break;
        }
    }

    // ---- cung ----
    void ReleaseBow(WeaponInstance w, Vector2 aim)
    {
        charging = false; float c = Mathf.Max(.2f, charge); bool full = charge >= 1f; charge = 0f; var def = w.Def; Element el = Elem(w); Color tint = Tint(el);
        cd = def.cooldown * CdMul * .6f; float dmg = w.Damage * Mathf.Lerp(.7f, 2.4f, c); int n = 1 + RunState.extraShots;
        for (int i = 0; i < n; i++)
        {
            var d = Combat.Rot(aim, n > 1 ? (i - (n - 1) * .5f) * 8f : 0f);
            CombatBolt.Fire(Muzzle(d, 1.2f), new BoltSpec { dir = d, speed = Mathf.Lerp(13f, 26f, c), dmg = dmg, radius = .2f, life = 1.6f, el = el, pierce = def.pierce + RunState.extraPierce + (full ? 1 : 0), bounce = RunState.bounces, friendly = true, col = tint, style = BoltStyle.Arrow, knock = full ? 3f : 0f, size = full ? 1.2f : 1f });
        }
        if (full) { RFx.Ring(Muzzle(aim, 1.2f), 1f, tint, .25f); RFx.Shake(.1f); }
        RFx.Sparks(Muzzle(aim, 1.2f), aim, tint, 3, 6f, 20f); recoil = 1f + c; RogueAudio.Play(RSfx.BowShot, .5f + c * .4f, Mathf.Lerp(.9f, 1.1f, c));
    }

    // ---- laze ----
    void FireBeam(WeaponInstance w, Vector2 aim, float dmg, Element el, Color tint, int pierceCount, int bounces)
    {
        var def = w.Def; Vector2 a = Muzzle(aim, 1.5f), dir = aim; float rem = def.range; var hitSet = new HashSet<Foe>(); Vector2 start = a; int segs = 0;
        while (rem > 0.1f && segs <= bounces)
        {
            Vector2 end = start + dir * rem;
            // cắt tại tường
            float tmax = rem; if (dir.x != 0f) { float tx = dir.x > 0f ? (RoomManager.MaxX - start.x) / dir.x : (RoomManager.MinX - start.x) / dir.x; if (tx >= 0f) tmax = Mathf.Min(tmax, tx); }
            if (dir.y != 0f) { float ty = dir.y > 0f ? (RoomManager.WallY - start.y) / dir.y : (RoomManager.MinY - start.y) / dir.y; if (ty >= 0f) tmax = Mathf.Min(tmax, ty); }
            end = start + dir * tmax;
            foreach (var f in new List<Foe>(Combat.Foes))
            {
                if (f == null || f.Dead || !f.Targetable || hitSet.Contains(f)) continue;
                Vector2 ab = end - start; float kk = Mathf.Clamp01(Vector2.Dot(f.Pos - start, ab) / Mathf.Max(.0001f, ab.sqrMagnitude)); float dd = Vector2.Distance(f.Pos, start + ab * kk);
                if (dd > f.radius + .18f) continue;
                hitSet.Add(f); float d2 = dmg; bool crit = Combat.Roll(ref d2, .06f); f.TakeHit(d2, el, start, 0f, crit, true); RFx.Sparks(start + ab * kk, -dir, tint, 3, 5f, 70f);
                if (hitSet.Count > pierceCount + 12) break;
            }
            RFx.BeamLine(start, end, tint, .22f, .09f); segs++; rem -= tmax; if (segs <= bounces) { var wallN = Vector2.zero; if (Mathf.Abs(end.x) >= RoomManager.MaxX - .01f) dir.x = -dir.x; else dir.y = -dir.y; start = end + dir * .02f; RFx.Sparks(end, dir, tint, 4); } else { RFx.Burst(end, tint, 3, 3f, .25f, .12f); }
        }
        RFx.Flash(a, 1f, tint, .06f); recoil = .35f; RFx.Shake(.025f); if (Time.frameCount % 3 == 0) RogueAudio.Play(RSfx.Laser, .22f, 1f + heat * .3f);
    }

    // ---- cận chiến ----
    void MeleeArc(WeaponInstance w, Vector2 aim, float range, float arcDeg, float dmg, Element el, Color tint, RSfx sfx, float knock, float lunge)
    {
        Vector2 pos = transform.position; sideFlip = -sideFlip;
        int hits = 0;
        foreach (var f in new List<Foe>(Combat.Foes))
        {
            if (f == null || f.Dead || !f.Targetable) continue;
            Vector2 d = f.Pos - pos; float dist = d.magnitude;
            if (dist > range + f.radius || (Vector2.Angle(aim, d) > arcDeg * .5f && dist > f.radius + .3f)) continue;
            float dd = dmg; bool crit = Combat.Roll(ref dd, .12f); f.TakeHit(dd, el, pos, knock, crit); hits++;
            RFx.Sparks(f.Pos, d, tint, crit ? 8 : 5, 8f, 70f);
        }
        RFx.Slash(pos + aim * .8f, aim, range * 1.1f, tint, .2f, sideFlip > 0f, arcDeg > 140f ? 1.15f : .9f);
        if (RunState.wave) CombatBolt.Fire(pos + aim * .9f, new BoltSpec { dir = aim, speed = 12f, dmg = dmg * .5f, radius = .45f, life = 1f, el = el, pierce = 99, friendly = true, col = tint, style = BoltStyle.Shard, size = 2f });
        if (lunge > 0f) kb += aim * lunge; else if (hits == 0) kb += aim * 2f;
        recoil = -.6f; RogueAudio.Play(sfx, .6f); if (hits > 0) { Combat.HitStop(.035f); RFx.Shake(.07f); }
    }

    void ThrustAttack(WeaponInstance w, Vector2 aim, float length, float dmg, Element el, Color tint)
    {
        Vector2 pos = transform.position; Vector2 tip = pos + aim * length; thrustT = .2f; int hits = 0;
        foreach (var f in new List<Foe>(Combat.Foes))
        {
            if (f == null || f.Dead || !f.Targetable) continue;
            Vector2 ab = tip - pos; float k = Mathf.Clamp01(Vector2.Dot(f.Pos - pos, ab) / ab.sqrMagnitude); float dd = Vector2.Distance(f.Pos, pos + ab * k);
            if (dd > f.radius + .4f) continue;
            float d2 = dmg; bool crit = Combat.Roll(ref d2, .1f); f.TakeHit(d2, el, pos, 2.5f, crit); hits++; RFx.Sparks(f.Pos, aim, tint, 5, 8f, 40f);
        }
        RFx.BeamLine(pos + aim * .8f, tip, Color.Lerp(tint, Color.white, .5f), .3f, .16f); RFx.Sparks(tip, aim, tint, 4, 7f, 25f);
        if (RunState.wave) CombatBolt.Fire(tip, new BoltSpec { dir = aim, speed = 14f, dmg = dmg * .5f, radius = .35f, life = 1f, el = el, pierce = 99, friendly = true, col = tint, style = BoltStyle.Shard, size = 1.8f });
        kb += aim * 3.5f; RogueAudio.Play(RSfx.Thrust, .6f); if (hits > 0) { Combat.HitStop(.04f); RFx.Shake(.08f); }
    }

    void SpinAttack(WeaponInstance w, float range, float dmg, Element el, Color tint)
    {
        Vector2 pos = transform.position; int hits = 0; spinT = .28f; spinAngle = Combat.Ang(facing); StartCoroutineSpin();
        foreach (var f in new List<Foe>(Combat.Foes))
        {
            if (f == null || f.Dead || !f.Targetable) continue;
            Vector2 d = f.Pos - pos; if (d.magnitude > range + f.radius) continue;
            float dd = dmg; bool crit = Combat.Roll(ref dd, .12f); f.TakeHit(dd, el, pos, 7f, crit); hits++; RFx.Sparks(f.Pos, d, tint, 7, 9f, 80f);
        }
        RFx.Spin(pos, range, tint, .3f);
        if (RunState.wave) for (int i = 0; i < 6; i++) CombatBolt.Fire(pos, new BoltSpec { dir = Combat.FromAng(i * 60f), speed = 11f, dmg = dmg * .35f, radius = .4f, life = .9f, el = el, pierce = 99, friendly = true, col = tint, style = BoltStyle.Shard, size = 1.6f });
        RFx.Shake(hits > 0 ? .28f : .14f); if (hits > 0) Combat.HitStop(.06f); RogueAudio.Play(RSfx.AxeSwing, .8f); RFx.Puff(pos + Vector2.down * .9f, new Color(.7f, .66f, .6f), 4, .5f);
    }

    void StartCoroutineSpin() { StartCoroutine(SpinRoutine()); }
    System.Collections.IEnumerator SpinRoutine()
    {
        float t = 0f; float a0 = Combat.Ang(facing);
        while (t < .28f) { t += Time.deltaTime; spinAngle = a0 + 360f * (t / .28f) * (sideFlip > 0 ? 1f : 1f); yield return null; }
        spinT = 0f;
    }

    // ---- pháp khí: quả cầu bay quanh người ----
    void UpdateOrbs(float dt, bool fire, Vector2 aim, WeaponInstance w)
    {
        int want = 3 + RunState.extraShots; Element el = Elem(w); Color tint = Tint(el);
        while (orbs.Count < want) { var o = Combat.MakePx("Orb", transform, RogueArt.OrbitOrb((int)(el == Element.None ? Element.None : el)), Combat.OrdFx - 25); orbs.Add(o.transform); }
        while (orbs.Count > want) { Destroy(orbs[orbs.Count - 1].gameObject); orbs.RemoveAt(orbs.Count - 1); }
        orbAng += dt * (fire ? 260f : 140f); float rad = fire ? 2.0f : 1.7f;
        for (int i = 0; i < orbs.Count; i++)
        {
            float a = orbAng + i * 360f / orbs.Count; var t = orbs[i]; t.localPosition = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad) * .75f) * rad + new Vector2(0, -.1f);
            t.GetComponent<SpriteRenderer>().sprite = RogueArt.OrbitOrb((int)el); t.GetComponent<SpriteRenderer>().sortingOrder = Combat.Order(transform.position.y + t.localPosition.y) + 2;
            // chạm quái gây sát thương, chặn đạn địch
            Vector2 wp = t.position;
            foreach (var f in new List<Foe>(Combat.Foes))
            {
                if (f == null || f.Dead || !f.Targetable) continue;
                if (Vector2.Distance(wp, f.Pos) > f.radius + .4f) continue;
                if (!orbHit.TryGetValue(f, out float last) || Combat.Now - last > .45f) { orbHit[f] = Combat.Now; float dd = w.Damage * .9f; bool crit = Combat.Roll(ref dd, .06f); f.TakeHit(dd, el, wp, 3f, crit); }
            }
            for (int b = CombatBolt.Enemy.Count - 1; b >= 0; b--) { var eb = CombatBolt.Enemy[b]; if (eb == null) continue; if (Vector2.Distance(eb.transform.position, wp) < .55f) { RFx.Burst(eb.transform.position, tint, 5, 3f, .3f, .12f); RogueAudio.Play(RSfx.HitMetal, .3f, 1.5f); Destroy(eb.gameObject); } }
            if (Random.value < dt * 6f) RFx.Burst(wp, tint, 1, .5f, .3f, .1f);
        }
    }
    readonly Dictionary<Foe, float> orbHit = new Dictionary<Foe, float>();

    void OrbBarrage(WeaponInstance w, Vector2 aim, float dmg, Element el, Color tint)
    {
        Element we = el == Element.None ? Element.None : el; Color col = el == Element.None ? new Color(.75f, .6f, 1f) : tint;
        for (int i = 0; i < orbs.Count; i++)
        {
            Vector2 from = orbs[i].position; var tgt = Combat.Nearest(from, 12f); Vector2 d = tgt != null ? (tgt.Pos - from).normalized : aim;
            CombatBolt.Fire(from, new BoltSpec { dir = Combat.Rot(d, Random.Range(-8f, 8f)), speed = w.Def.speed, dmg = dmg, radius = .2f, life = 2f, el = we, homing = 2.6f, bounce = RunState.bounces, pierce = RunState.extraPierce, friendly = true, col = col, style = BoltStyle.Shard, size = 1.2f });
            RFx.Burst(from, col, 3, 2f, .25f, .1f);
        }
        RogueAudio.Play(RSfx.Orb, .5f);
    }
}

class AfterFade : MonoBehaviour
{
    float t; SpriteRenderer sr;
    void Start() { sr = GetComponent<SpriteRenderer>(); }
    void Update() { t += Time.deltaTime; var c = sr.color; c.a = Mathf.Clamp01(.6f - t * 3f); sr.color = c; if (t > .2f) Destroy(gameObject); }
}
