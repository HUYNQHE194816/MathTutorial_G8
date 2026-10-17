using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Hiệp sĩ trong trận. WASD đi, Shift lướt (hồi 3 giây, miễn sát thương 0,2 giây), Q đổi vũ khí, E tương tác.
/// TỰ NHẮM: mặc định tự khóa quái gần nhất và bắn/chém về phía nó; giữ chuột trái hoặc phím J để bắn. Không có quái thì ngắm bằng chuột
/// (để bắn thùng nổ, thùng độc). Phím T tắt/bật tự nhắm. Điện thoại: joystick trái đi, joystick phải giữ để bắn (TouchControls).
/// Mười bảy vũ khí, mỗi loại một cách đánh riêng (xem Attack). Máu, vũ khí, buff nằm trong RunState nên sống qua các màn.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    public static float MaxHp => 100f + 10f * Meta.PermHp + 20f * RunState.Count(BuffId.Heart);
    public const float Speed = 5.4f, DashCooldown = 3f;
    public static bool AutoAim = true;
    public float radius = .3f;
    public bool Dead { get; private set; }
    public System.Action Died;
    public float DashLeft => Mathf.Max(0f, dashCd);
    public float Hp => RunState.hp;
    public bool Controllable = true;
    public float HeatFrac => heat; public bool Overheated => overheat; public int Ammo => ammo; public bool Reloading => reload > 0f; public float Charge => charge;
    public int ShieldCharges => shieldCharges;
    public Foe Target => target;

    float cd, dashCd, dashT, invuln, hurtFlash, shield, shieldMax, shieldUntil; Vector2 dashDir, facing = Vector2.right, kb;
    SpriteRenderer body, weaponSr, flashSr, shadow, shieldSr; Transform weaponPivot; WeaponId shownWeapon = (WeaponId)(-1); int shownEl = -1, shownRar = -1;
    float animT, recoil, heat; bool overheat; float charge; bool charging; int ammo = -1; float reload; float rifleBloom; float spinT, thrustT; float afterT, stepDust;
    float slowUntil, slowMul = 1f, rootUntil, stunUntil, burnUntil, poisonUntil; int poison; float poisonAcc, burnAcc, statusVfx, swapT, sideFlip = 1f;
    readonly List<Transform> orbs = new List<Transform>(); float orbAng; WeaponId orbOwner = (WeaponId)(-1); float spinAngle;
    SpriteRenderer cross; Sprite[] idle, run; Foe target; int shieldCharges; float thunderT = 2f, iceT = 2f, regenAcc, vampAcc, landSquash;
    readonly List<SpiritCompanion> comps = new List<SpiritCompanion>(); readonly Dictionary<Foe, float> orbHit = new Dictionary<Foe, float>();

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
        shadow = Combat.MakeSr("Shade", transform, RogueArt.Disc, new Color(0, 0, 0, .45f), new Vector2(.85f, .3f), 4, new Vector2(0, -.6f));
        body = Combat.MakePx("Body", transform, idle[0], Combat.OrdChar, new Vector2(0, -.6f));
        var fg = new GameObject("Flash"); fg.transform.SetParent(body.transform, false); flashSr = fg.AddComponent<SpriteRenderer>(); flashSr.color = new Color(1, 1, 1, 0);
        weaponPivot = new GameObject("WeaponPivot").transform; weaponPivot.SetParent(transform, false); weaponPivot.localPosition = new Vector2(0, -.02f);
        weaponSr = Combat.MakePx("Weapon", weaponPivot, RogueArt.Weapon(0, 0), Combat.OrdChar + 3, new Vector2(.2f, 0));
        shieldSr = Combat.MakeSr("Shield", transform, RogueArt.Disc, new Color(.5f, .8f, 1f, 0f), new Vector2(1.6f, 1.6f), Combat.OrdChar + 6, new Vector2(0, -.05f)); shieldSr.sharedMaterial = Gfx.Additive;
        var cg = new GameObject("Crosshair"); cross = cg.AddComponent<SpriteRenderer>(); cross.sprite = RogueArt.Ring; cross.sortingOrder = Combat.OrdFx + 50; cg.transform.localScale = Vector3.one * .08f; cross.sharedMaterial = Gfx.Additive;
        shieldCharges = 2 * RunState.Count(BuffId.Shield); RefreshCompanions(); Combat.Dealt += OnDealt;
    }

    void OnDestroy()
    {
        Combat.Dealt -= OnDealt; if (cross != null) Destroy(cross.gameObject); Cursor.visible = true;
        foreach (var o in orbs) if (o != null) Destroy(o.gameObject); foreach (var c in comps) if (c != null) Destroy(c.gameObject);
    }

    public void RefreshCompanions() { int want = Mathf.Min(3, RunState.Count(BuffId.Ally)); comps.RemoveAll(c => c == null); while (comps.Count < want) comps.Add(SpiritCompanion.Create(comps.Count)); }

    /// <summary>Hút máu: hồi 15% sát thương gây ra, cộng dồn đến khi đủ 1 máu.</summary>
    void OnDealt(float d)
    {
        int n = RunState.Count(BuffId.Vamp); if (n <= 0 || Dead) return;
        vampAcc += d * .15f * n; if (vampAcc >= 1f) { int h = Mathf.Min(6, (int)vampAcc); vampAcc -= h; Heal(h); }
    }

    /// <summary>Nguyên tố phụ của Hỏa Ấn / Băng Ấn: gọi sau mỗi đòn trúng quái. Có cả hai thì mỗi đòn chỉ áp một loại ngẫu nhiên để không kích hoạt Tan băng liên tục.</summary>
    public static void Extra(Foe f, Vector2 src)
    {
        if (f == null || f.Dead || f.isProp) return; bool fire = RunState.Has(BuffId.Fire), ice = RunState.Has(BuffId.Ice);
        if (fire && ice) { if (Random.value < .5f) fire = false; else ice = false; }
        if (fire) Elements.Apply(f, Element.Hoa, src); else if (ice) Elements.Apply(f, Element.Bang, src);
    }

    // ------------------------------------------------------------------ sát thương, trạng thái, hồi máu
    public void AddShield(float amount, float seconds) { shield = amount; shieldMax = amount; shieldUntil = Combat.Now + seconds; }
    public float Shield => Combat.Now < shieldUntil ? shield : 0f;
    public bool Invulnerable => invuln > 0f;
    public void GiveInvuln(float t) { invuln = Mathf.Max(invuln, t); }

    public void Hurt(float d, Element e, Vector2 src = default(Vector2), bool dot = false, bool force = false)
    {
        if (Dead || d <= 0f) return;
        if (invuln > 0f && !dot && !force) return;
        d *= 1f - RunState.resist[(int)e];
        if (!dot && shieldCharges > 0 && !force)
        {   // Khiên Sáng: chặn trọn một đòn
            shieldCharges--; invuln = Mathf.Max(invuln, .4f); RFx.Ring(transform.position, 1f, new Color(.6f, .85f, 1f), .3f); RFx.Text(transform.position + Vector3.up * .8f, "Chặn! (" + shieldCharges + ")", new Color(.6f, .9f, 1f), .12f); RogueAudio.Play(RSfx.Shield, .7f, 1.3f); return;
        }
        if (Combat.Now < shieldUntil && shield > 0f)
        {
            float ab = Mathf.Min(shield, d); shield -= ab; d -= ab; RFx.Burst(transform.position, new Color(.6f, .85f, 1f), 6, 3f, .4f, .14f); RogueAudio.Play(RSfx.Shield, .5f, 1.4f);
            if (shield <= 0f) { RFx.Ring(transform.position, 1.2f, new Color(.6f, .85f, 1f), .3f); RogueAudio.Play(RSfx.ShieldBreak, .7f); }
            if (d <= .01f) { invuln = Mathf.Max(invuln, .25f); return; }
        }
        RunState.hp = Mathf.Max(0f, RunState.hp - d); if (!dot) { invuln = .6f; hurtFlash = .15f; Combat.HitStop(.06f); RFx.Shake(.28f); RoomManager.Instance?.HurtFlash(); }
        RFx.Text(transform.position + Vector3.up * .8f, "-" + Mathf.RoundToInt(d), GameTheme.Blood, .12f);
        RFx.Burst(transform.position, new Color(.9f, .2f, .3f), dot ? 2 : 8, 3f, .45f, .14f, 5f, false);
        RogueAudio.Play(RSfx.Hurt, dot ? .25f : .8f);
        if (!dot && src != default(Vector2)) kb += ((Vector2)transform.position - src).normalized * 4f;
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
    public void Root(float dur) { rootUntil = Mathf.Max(rootUntil, Combat.Now + dur); RFx.Burst(transform.position + Vector3.down * .5f, Combat.ElementColor(Element.Thao), 6, 1.6f, .5f, .14f); }

    public void Heal(float a)
    {
        if (Dead || a <= 0f) return;
        RunState.hp = Mathf.Min(MaxHp, RunState.hp + a);
        RFx.Text(transform.position + Vector3.up * .8f, "+" + Mathf.RoundToInt(a), GameTheme.Moss, .11f);
    }

    public void OnKill() { if (RunState.killHeal > 0) Heal(3f * RunState.killHeal); if (RunState.main != null && RunState.main.id == WeaponId.Scythe) Heal(2f); }

    // ------------------------------------------------------------------ đầu vào
    Vector2 MouseWorld()
    {
        var cam = Camera.main; if (cam == null || Mouse.current == null) return (Vector2)transform.position + facing * 4f;
        Vector3 m = Mouse.current.position.ReadValue(); m.z = -cam.transform.position.z; return cam.ScreenToWorldPoint(m);
    }

    Foe PickTarget()
    {
        Vector2 p = transform.position;
        if (target != null && (target.Dead || !target.Targetable || target.isProp || Vector2.Distance(p, target.Pos) > 22f)) target = null;
        var near = Combat.Nearest(p, 18f);
        if (target == null) target = near;
        else if (near != null && near != target && Vector2.Distance(p, near.Pos) + 1.3f < Vector2.Distance(p, target.Pos)) target = near;   // chỉ đổi mục tiêu khi có con gần hơn rõ rệt
        return target;
    }

    Vector2 AimDir(Vector2 mv)
    {
        Vector2 p = transform.position; var k = Keyboard.current;
        if (k != null && k.tKey.wasPressedThisFrame) { AutoAim = !AutoAim; RoomManager.Instance?.ShowBanner(AutoAim ? "Tự nhắm: BẬT" : "Tự nhắm: TẮT (ngắm bằng chuột)", 1.6f); }
        Vector2 want = facing; bool locked = false;
        if (AutoAim) { var t = PickTarget(); if (t != null) { want = t.Pos - p; locked = true; } } else target = null;
        if (!locked)
        {
            if (TouchControls.Active) { if (TouchControls.Aim.sqrMagnitude > .04f) want = TouchControls.Aim; else if (mv.sqrMagnitude > .04f) want = mv; }
            else want = MouseWorld() - p;
        }
        if (want.sqrMagnitude < .0004f) want = facing;
        // chấm ngắm: khóa mục tiêu (vòng quanh quái) hoặc theo chuột
        if (cross != null)
        {
            bool touch = TouchControls.Active; cross.enabled = !touch && !RoomManager.Frozen && Controllable && !Dead;
            if (locked) { cross.transform.position = target.Pos; cross.transform.localScale = Vector3.one * Mathf.Max(.05f, target.radius * .2f) * (1f + .06f * Mathf.Sin(Time.time * 8f)); cross.color = new Color(1f, .45f, .35f, .95f); }
            else { cross.transform.position = MouseWorld(); cross.transform.localScale = Vector3.one * .07f; cross.color = new Color(.9f, .95f, 1f, .85f); }
        }
        return want.normalized;
    }

    // ------------------------------------------------------------------ vòng lặp
    void Update()
    {
        float dt = Combat.Dt;
        bool playing = !RoomManager.Frozen && Controllable && !Dead;
        Cursor.visible = !playing || TouchControls.Active || cross == null;
        if (cross != null && !playing) cross.enabled = false;
        if (dt <= 0f || Dead) { AnimateBody(0f, Vector2.zero, facing); return; }
        var k = Keyboard.current; if (k == null) return;
        if (invuln > 0f) invuln -= dt; if (cd > 0f) cd -= dt; if (dashCd > 0f) dashCd -= dt; if (swapT > 0f) swapT -= dt; if (hurtFlash > 0f) hurtFlash -= dt; if (landSquash > 0f) landSquash -= dt;
        Status(dt); Passives(dt);

        Vector2 mv = Vector2.zero;
        if (Controllable)
        {
            float x = ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1f : 0f) - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1f : 0f);
            float y = ((k.wKey.isPressed || k.upArrowKey.isPressed) ? 1f : 0f) - ((k.sKey.isPressed || k.downArrowKey.isPressed) ? 1f : 0f);
            mv = new Vector2(x, y) + TouchControls.Move; if (mv.sqrMagnitude > 1f) mv.Normalize();
            if (Combat.Now < stunUntil) mv = Vector2.zero;
        }
        Vector2 aim = Controllable ? AimDir(mv) : facing; facing = aim;

        bool dashKey = k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame || TouchControls.DashPressed;
        if (Controllable && dashKey && dashCd <= 0f && dashT <= 0f && Combat.Now >= stunUntil)
        { dashT = .2f; dashCd = DashCooldown; invuln = Mathf.Max(invuln, .24f); dashDir = mv.sqrMagnitude > .01f ? mv : aim; RogueAudio.Play(RSfx.Dash, .6f); RFx.Puff(transform.position + Vector3.down * .5f, new Color(.7f, .68f, .8f), 4, .35f); }

        float spd = Speed * RunState.SpeedMul * (Combat.Now < slowUntil ? slowMul : 1f) * (Combat.Now < rootUntil ? 0f : 1f);
        if (dashT > 0f) { dashT -= dt; Step(dashDir * 15f * dt); afterT -= dt; if (afterT <= 0f) { afterT = .028f; Afterimage(); } if (dashT <= 0f) landSquash = .12f; } else Step(mv * spd * dt);
        if (kb.sqrMagnitude > .01f) { Step(kb * dt); kb *= Mathf.Exp(-10f * dt); }
        if (Combat.Gravity.sqrMagnitude > .01f) Step(Combat.Gravity * dt);

        bool swap = k.qKey.wasPressedThisFrame || TouchControls.SwapPressed;
        if (Controllable && swap && RunState.sub != null) { var t = RunState.main; RunState.main = RunState.sub; RunState.sub = t; swapT = .25f; charge = 0f; charging = false; ammo = -1; RogueAudio.Play(RSfx.Swap, .6f); RFx.Burst(transform.position, Color.white, 6, 2f, .3f, .12f); }

        RefreshWeaponVisual();
        bool overUi = EventSystem.current != null && Mouse.current != null && EventSystem.current.IsPointerOverGameObject();
        bool fire = Controllable && (k.jKey.isPressed || (Mouse.current != null && Mouse.current.leftButton.isPressed && !overUi) || TouchControls.AimHeld || TouchControls.AttackHeld);
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
            statusVfx = .2f; Vector2 p = (Vector2)transform.position + Random.insideUnitCircle * .3f;
            if (Combat.Now < burnUntil) RFx.Burst(p, Combat.ElementColor(Element.Hoa), 1, 1f, .4f, .14f, -3f);
            if (poison > 0) RFx.Burst(p, Combat.ElementColor(Element.Doc), 1, .7f, .5f, .12f, -1.5f);
            if (Combat.Now < slowUntil) RFx.Burst(p, Combat.ElementColor(Element.Bang), 1, .3f, .4f, .1f);
        }
        if (shieldSr != null) { bool on = (Combat.Now < shieldUntil && shield > 0f) || shieldCharges > 0; float a = on ? .18f + .08f * Mathf.Sin(Time.time * 6f) : 0f; shieldSr.color = new Color(.5f, .8f, 1f, a); }
    }

    /// <summary>Buff chạy ngầm của boss Rồng: Hồi Phục, Sấm Sét, Băng Ấn.</summary>
    void Passives(float dt)
    {
        int rg = RunState.Count(BuffId.Regen); if (rg > 0) { regenAcc += dt * rg; if (regenAcc >= 1f) { regenAcc -= 1f; if (RunState.hp < MaxHp) Heal(1f); } }
        if (RunState.Has(BuffId.Thunder)) { thunderT -= dt; if (thunderT <= 0f) { thunderT = 3f; var f = Combat.Nearest(transform.position, 14f); if (f != null) { RFx.Lightning(f.Pos + new Vector2(.4f, 6f), f.Pos, Combat.ElementColor(Element.Loi), .3f, .14f); f.TakeHit(14f, Element.Loi, f.Pos, 1f, false, true); RogueAudio.Play(RSfx.Thunder, .7f); RFx.Shake(.12f); } } }
        if (RunState.Has(BuffId.Ice)) { iceT -= dt; if (iceT <= 0f) { iceT = 3f; for (int i = 0; i < 3; i++) { var f = Combat.Nearest(transform.position, 12f); Vector2 d = f != null ? (f.Pos - (Vector2)transform.position).normalized : Combat.FromAng(i * 120f + Time.time * 30f); CombatBolt.Fire(transform.position, new BoltSpec { dir = Combat.Rot(d, (i - 1) * 18f), speed = 10f, dmg = 8f, radius = .24f, life = 1.5f, el = Element.Bang, friendly = true, col = Combat.ElementColor(Element.Bang), style = BoltStyle.Shard, homing = 1.6f, size = 1.1f }); } RogueAudio.Play(RSfx.Ice, .4f); } }
    }

    void Step(Vector2 d)
    {
        Vector2 p = (Vector2)transform.position + d;
        p.x = Mathf.Clamp(p.x, RoomManager.MinX, RoomManager.MaxX);
        bool inDoor = RoomManager.Instance != null && RoomManager.Instance.DoorOpen && Mathf.Abs(p.x) < RoomManager.DoorHalf - .2f;
        p.y = Mathf.Clamp(p.y, RoomManager.MinY, inDoor ? RoomManager.ExitY + .5f : RoomManager.WallY);
        transform.position = RoomProp.Resolve(p, radius);
    }

    void Afterimage()
    {
        var go = new GameObject("After"); go.transform.position = body.transform.position; go.transform.localScale = body.transform.localScale; go.transform.rotation = body.transform.rotation;
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = body.sprite; sr.flipX = body.flipX; sr.sortingOrder = Combat.OrdChar - 1; sr.color = new Color(.6f, .8f, 1f, .6f); sr.sharedMaterial = Gfx.Additive;
        go.AddComponent<AfterFade>();
    }

    // ------------------------------------------------------------------ hoạt họa thân và vũ khí (mô phỏng chuyển động kiểu Soul Knight: nhún, nghiêng, bụi chân)
    void AnimateBody(float dt, Vector2 mv, Vector2 aim)
    {
        bool moving = mv.sqrMagnitude > .01f || dashT > 0f; float adt = Combat.Dt > 0f ? Time.deltaTime : 0f; animT += adt;
        Sprite s;
        if (dashT > 0f) s = RogueArt.KnightDash;
        else if (moving) s = run[(int)(animT * (11f + 3f * Mathf.Min(1f, mv.magnitude))) % run.Length]; else s = idle[(int)(animT * 3.5f) % idle.Length];
        body.sprite = s; bool left = aim.x < 0f; if (dashT > 0f) left = dashDir.x < 0f; if (!moving && Mathf.Abs(aim.x) < .1f) left = body.flipX; body.flipX = left;
        float phase = animT * 13f;
        float bob = moving && dashT <= 0f ? Mathf.Abs(Mathf.Sin(phase)) * .035f : Mathf.Sin(animT * 2.2f) * .012f;
        float squash = moving ? 1f + .035f * Mathf.Sin(phase * 2f) : 1f + .02f * Mathf.Sin(animT * 4.4f);
        if (landSquash > 0f) squash *= 1f - .18f * (landSquash / .12f); if (dashT > 0f) squash = 1.08f;
        float lean = moving && dashT <= 0f ? Mathf.Clamp(mv.x, -1f, 1f) * -4f + Mathf.Sin(phase) * 2.2f : (dashT > 0f ? (dashDir.x >= 0f ? -10f : 10f) : 0f);
        body.transform.localPosition = new Vector2(0f, -.6f + bob); body.transform.localEulerAngles = new Vector3(0f, 0f, lean);
        body.transform.localScale = new Vector3(Combat.PxScale / squash * (dashT > 0f ? 1.1f : 1f), Combat.PxScale * squash, 1f);
        body.sortingOrder = Combat.Order(transform.position.y);
        flashSr.sprite = RogueArt.Silhouette(s); flashSr.flipX = body.flipX; flashSr.sortingOrder = body.sortingOrder + 1; flashSr.color = new Color(1f, .4f, .4f, hurtFlash > 0f ? .8f : 0f);
        var c = Color.white; c.a = invuln > 0f && dashT <= 0f && ((int)(Time.time * 20f) % 2 == 0) ? .35f : 1f; body.color = c;
        shadow.transform.localScale = new Vector3(.85f, .3f, 1f) * (dashT > 0f ? .8f : 1f) * (1f - bob * 2f);
        if (moving && dashT <= 0f && adt > 0f) { stepDust -= adt; if (stepDust <= 0f) { stepDust = .26f; RFx.Puff((Vector2)transform.position + new Vector2(0, -.55f) - mv.normalized * .15f, new Color(.55f, .52f, .62f), 1, .22f); } }

        // vũ khí xoay theo hướng ngắm, giật lùi khi bắn, lật theo trục y để luôn xuôi chiều
        float ang = Combat.Ang(aim); if (spinT > 0f) ang = spinAngle;
        weaponPivot.localEulerAngles = new Vector3(0, 0, ang); weaponPivot.localPosition = new Vector2(0, -.02f + bob);
        float rc = recoil; recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 8f);
        float thr = thrustT > 0f ? Mathf.Sin(Mathf.Clamp01(1f - thrustT / .2f) * Mathf.PI) * .8f : 0f;
        weaponSr.transform.localPosition = new Vector2(.28f - rc * .14f + thr, (moving ? Mathf.Sin(phase) * .03f : Mathf.Sin(animT * 2.2f) * .015f));
        float flipY = left ? -1f : 1f;
        weaponSr.transform.localScale = new Vector3(Combat.PxScale, Combat.PxScale * flipY, 1f);
        weaponSr.sortingOrder = aim.y > .35f ? body.sortingOrder - 1 : body.sortingOrder + 2;
        if (swapT > 0f) weaponSr.transform.localScale = new Vector3(Combat.PxScale, Combat.PxScale * flipY * (1f - swapT / .25f), 1f);
        if (thrustT > 0f) thrustT -= Time.deltaTime;
        var w = RunState.main;
        if (w != null && (w.id == WeaponId.Bow || w.id == WeaponId.Railgun)) weaponSr.transform.localPosition += new Vector3(-charge * .08f, 0, 0);
        if (w != null && w.id == WeaponId.Orb) { weaponPivot.localEulerAngles = Vector3.zero; weaponSr.transform.localPosition = new Vector2(0, .5f + Mathf.Sin(animT * 3f) * .08f); weaponSr.transform.localScale = Vector3.one * Combat.PxScale * (1f + .08f * Mathf.Sin(animT * 6f)); }
        if (w != null && w.id == WeaponId.Shuriken) weaponSr.transform.Rotate(0, 0, 360f * Time.deltaTime);
    }

    void RefreshWeaponVisual()
    {
        var w = RunState.main; if (w == null) return;
        if (w.id != shownWeapon || (int)w.element != shownEl || w.rarity != shownRar)
        {
            shownWeapon = w.id; shownEl = (int)w.element; shownRar = w.rarity; weaponSr.sprite = RogueArt.Weapon((int)w.id, w.rarity, (int)w.element);
            if (orbOwner != w.id) { foreach (var o in orbs) if (o != null) Destroy(o.gameObject); orbs.Clear(); orbOwner = w.id; }
        }
        if (w.rarity >= 2 && Random.value < Time.deltaTime * (w.rarity == 3 ? 10f : 5f)) RFx.Burst((Vector2)weaponSr.transform.position + Random.insideUnitCircle * .3f, RogueArt.RarityCols[w.rarity], 1, .6f, .5f, .1f, -1f);
    }

    // ------------------------------------------------------------------ tấn công
    float CdMul => 1f / (1f + .15f * RunState.atkSpeedStacks);
    Element Elem(WeaponInstance w) => w.element != Element.None ? w.element : RunState.infuse;
    Color Tint(Element el) => el == Element.None ? new Color(1f, .93f, .7f) : Combat.ElementColor(el);
    Vector2 Muzzle(Vector2 aim, float off = 1.1f) => (Vector2)transform.position + aim * off * .62f + new Vector2(0, -.03f);

    void UpdateWeapon(float dt, bool fire, Vector2 aim)
    {
        var w = RunState.main; if (w == null) return; var def = w.Def;
        rifleBloom = Mathf.MoveTowards(rifleBloom, 0f, dt * 14f);
        if (def.kind == WKind.Beam) { if (fire && !overheat) heat = Mathf.Min(1f, heat + dt * .42f); else heat = Mathf.Max(0f, heat - dt * (overheat ? .3f : .5f)); if (heat >= 1f) { overheat = true; RFx.Burst(transform.position, new Color(1f, .5f, .2f), 8, 2f, .5f, .14f, -2f); RogueAudio.Play(RSfx.ShieldBreak, .4f, 1.5f); } if (overheat && heat <= .4f) overheat = false; }
        else { heat = Mathf.Max(0f, heat - dt); overheat = false; }
        if (def.kind == WKind.Rocket) { if (ammo < 0) ammo = 4 + RunState.extraShots; if (reload > 0f) { reload -= dt; if (reload <= 0f) { ammo = 4 + RunState.extraShots; RogueAudio.Play(RSfx.Swap, .5f, 1.3f); } } }
        if (def.kind == WKind.Orbit) UpdateOrbs(dt, fire, aim, w);

        if (def.kind == WKind.Bow || def.kind == WKind.Charge)
        {   // vũ khí sạc: giữ để lên lực, thả để bắn
            bool rail = def.kind == WKind.Charge; float full = rail ? 1.1f : .75f;
            if (fire && cd <= 0f) { if (!charging) { charging = true; charge = 0f; RogueAudio.Play(RSfx.BowDraw, .5f, rail ? .7f : 1f); } charge = Mathf.Min(1f, charge + dt / full * (1f + .15f * RunState.atkSpeedStacks)); if (charge >= 1f && Random.value < .35f) RFx.Burst(Muzzle(aim, 1.3f), Tint(Elem(w)), 1, 1f, .25f, .12f); if (rail && Random.value < .5f) RFx.Burst(Muzzle(aim, 1.3f) + Random.insideUnitCircle * .4f, Tint(Elem(w)), 1, -2f, .25f, .1f); }
            else if (charging) { if (rail) ReleaseRail(w, aim); else ReleaseBow(w, aim); }
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
                        if (auto) ang += Random.Range(-1f, 1f) * (2f + rifleBloom);
                        var d = Combat.Rot(aim, ang);
                        CombatBolt.Fire(Muzzle(d), new BoltSpec { dir = d, speed = def.speed, dmg = dmg, radius = auto ? .16f : .2f, life = 1.4f, el = el, pierce = pierce, bounce = bnc, friendly = true, col = tint, style = BoltStyle.Orb, size = auto ? .8f : 1f, knock = auto ? 0f : 1f });
                    }
                    if (auto) rifleBloom = Mathf.Min(9f, rifleBloom + 1.3f);
                    RFx.Flash(Muzzle(aim, 1.5f), auto ? .6f : .8f, tint, .07f); RFx.Sparks(Muzzle(aim, 1.5f), aim, tint, 3, 5f, 30f); RFx.Shell(pos + aim * .3f, aim);
                    recoil = auto ? .6f : 1f; RFx.Shake(auto ? .03f : .06f); RogueAudio.Play(auto ? RSfx.Rifle : RSfx.Pistol, auto ? .45f : .6f);
                    break;
                }
            case WKind.Spread:
                {   // súng hoa cải: nhiều viên tỏa ra, mạnh ở tầm gần
                    int n = def.count + bonus * 2;
                    for (int i = 0; i < n; i++)
                    {
                        float ang = (i - (n - 1) * .5f) * (def.spreadDeg / Mathf.Max(1, n - 1)) + Random.Range(-3f, 3f); var d = Combat.Rot(aim, ang);
                        CombatBolt.Fire(Muzzle(d, 1.3f), new BoltSpec { dir = d, speed = def.speed * Random.Range(.85f, 1.15f), dmg = dmg, radius = .17f, life = Random.Range(.42f, .55f), el = el, pierce = pierce, bounce = bnc, friendly = true, col = tint, style = BoltStyle.Orb, size = .9f, knock = 1.2f });
                    }
                    RFx.Flash(Muzzle(aim, 1.7f), 1.2f, tint, .09f); RFx.Sparks(Muzzle(aim, 1.7f), aim, new Color(1f, .8f, .4f), 7, 7f, 45f); RFx.Puff(Muzzle(aim, 1.7f), new Color(.6f, .58f, .64f), 4, .35f); RFx.Shell(pos + aim * .3f, aim); RFx.Shell(pos + aim * .3f, aim);
                    kb += -aim * 3f; recoil = 1.6f; RFx.Shake(.14f); RogueAudio.Play(RSfx.Cannon, .55f, 1.5f); RogueAudio.Play(RSfx.Pistol, .5f, .7f);
                    break;
                }
            case WKind.Beam: FireBeam(w, aim, dmg, el, tint, pierce + 1, bnc, def.range, .22f, true); break;
            case WKind.Stab: MeleeArc(w, aim, def.range * RunState.RangeMul, 115f, dmg, el, tint, RSfx.Slash, 1.3f, 0f); break;
            case WKind.Reap: MeleeArc(w, aim, def.range * RunState.RangeMul, 165f, dmg, el, tint, RSfx.Scythe, 2.2f, 3f); break;
            case WKind.Thrust: ThrustAttack(w, aim, def.range * RunState.RangeMul + RunState.extraPierce * .3f, dmg, el, tint); break;
            case WKind.Spin: SpinAttack(w, def.range * RunState.RangeMul, dmg, el, tint); break;
            case WKind.Throw:
                {   // phi đao: ném liên tục, mỗi lần xoay tít
                    int n = 1 + bonus;
                    for (int i = 0; i < n; i++)
                    {
                        float ang = (n > 1 ? (i - (n - 1) * .5f) * 9f : 0f) + Random.Range(-def.spreadDeg, def.spreadDeg); var d = Combat.Rot(aim, ang);
                        CombatBolt.Fire(Muzzle(d, 1.2f), new BoltSpec { dir = d, speed = def.speed, dmg = dmg, radius = .2f, life = 1.1f, el = el, pierce = pierce, bounce = bnc, friendly = true, col = tint, style = BoltStyle.Knife, size = 1f, knock = .6f });
                    }
                    recoil = -.5f; sideFlip = -sideFlip; RFx.Sparks(Muzzle(aim, 1.2f), aim, tint, 2, 5f, 20f); RogueAudio.Play(RSfx.Slash, .4f, 1.5f);
                    break;
                }
            case WKind.Boomerang:
                {   // phi tiêu: bay đi rồi quay về
                    int n = 1 + (bonus > 0 ? 1 : 0) + (bonus > 1 ? 1 : 0);
                    for (int i = 0; i < n; i++)
                    {
                        var d = Combat.Rot(aim, n > 1 ? (i - (n - 1) * .5f) * 24f : 0f);
                        CombatBolt.Fire(Muzzle(d, 1.2f), new BoltSpec { dir = d, speed = def.speed, dmg = dmg, radius = .3f, life = 2.3f, el = el, pierce = 99, bounce = bnc, friendly = true, col = tint, style = BoltStyle.Star, size = 1.1f, returning = true, knock = 1f });
                    }
                    recoil = -.4f; RogueAudio.Play(RSfx.Slash, .5f, 1.2f); RogueAudio.Play(RSfx.Wind, .3f, 1.6f);
                    break;
                }
            case WKind.Grenade:
                {
                    Vector2 from = Muzzle(aim, 1f); Vector2 tgt = target != null && AutoAim ? target.Pos : (TouchControls.Active ? from + aim * 6f : MouseWorld());
                    float dist = Mathf.Min(9f, Vector2.Distance(from, tgt)); tgt = from + (tgt - from).normalized * Mathf.Max(2.5f, dist); tgt = RoomManager.Clamp(tgt);
                    int n = 1 + (bonus > 0 ? 1 : 0);
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 land = tgt + (n > 1 ? new Vector2((i - .5f) * 1.6f, 0f) : Vector2.zero); float r = 2.2f; float dd = dmg; var e2 = el; var tint2 = tint;
                        CombatBolt.Fire(from, new BoltSpec { lob = true, lobTo = land, lobTime = Mathf.Clamp(Vector2.Distance(from, land) / 9f, .45f, 1f), lobHeight = 1.8f, friendly = true, style = BoltStyle.Grenade, col = new Color(1f, .8f, .4f), size = 1f, radius = .3f, onLand = p => CombatBolt.Explode(p, r, dd, e2, true, 6f, tint2.a > 0 && e2 != Element.None ? tint2 : new Color(1f, .6f, .25f)) });
                    }
                    recoil = .4f; RogueAudio.Play(RSfx.Orb, .5f, .7f);
                    break;
                }
            case WKind.Blast:
                {
                    int n = 1 + (bonus > 0 ? 1 : 0);
                    for (int i = 0; i < n; i++)
                    {
                        var d = Combat.Rot(aim, n > 1 ? (i - .5f) * 10f : 0f);
                        CombatBolt.Fire(Muzzle(d, 1.4f), new BoltSpec { dir = d, speed = def.speed, dmg = dmg, radius = .3f, life = 1.5f, el = el, friendly = true, col = tint, style = BoltStyle.Cannon, blast = 2.4f, knock = 7f, bounce = bnc, size = 1f });
                    }
                    RFx.Blast(Muzzle(aim, 1.6f), .6f, new Color(1f, .7f, .3f), false); RFx.Puff(Muzzle(aim, 1.5f), new Color(.5f, .48f, .54f), 5, .5f); RFx.Shake(.4f);
                    kb += -aim * 6f; recoil = 2f; RogueAudio.Play(RSfx.Cannon, .9f); Combat.HitStop(.03f);
                    break;
                }
            case WKind.Rocket:
                {
                    if (ammo < 0) ammo = 4 + bonus; ammo--; if (ammo <= 0) { reload = 1.4f; cd = .45f; }
                    var d = Combat.Rot(aim, Random.Range(-4f, 4f));
                    CombatBolt.Fire(Muzzle(d, 1.3f), new BoltSpec { dir = d, speed = def.speed, accel = 9f, homing = 1.7f, dmg = dmg, radius = .24f, life = 3f, el = el, friendly = true, col = new Color(1f, .6f, .25f), style = BoltStyle.Rocket, blast = 1.6f, knock = 4f, bounce = 0 });
                    RFx.Puff(Muzzle(aim, 1.2f), new Color(.7f, .68f, .76f), 3, .35f); RFx.Flash(Muzzle(aim, 1.3f), .9f, new Color(1f, .7f, .3f), .08f); recoil = 1.1f; RFx.Shake(.1f); RogueAudio.Play(RSfx.Rocket, .7f);
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
                    RFx.Burst(Muzzle(aim, 1.6f), Combat.ElementColor(we), 5, 3f, .3f, .14f); RFx.Flash(Muzzle(aim, 1.5f), .8f, Combat.ElementColor(we), .08f); recoil = .5f; RogueAudio.Play(RSfx.Staff, .5f);
                    break;
                }
            case WKind.Orbit: OrbBarrage(w, aim, dmg, el, tint); break;
        }
    }

    void ReleaseBow(WeaponInstance w, Vector2 aim)
    {
        charging = false; float c = Mathf.Max(.2f, charge); bool full = charge >= 1f; charge = 0f; var def = w.Def; Element el = Elem(w); Color tint = Tint(el);
        cd = def.cooldown * CdMul * .6f; float dmg = w.Damage * Mathf.Lerp(.7f, 2.4f, c); int n = 1 + RunState.extraShots;
        for (int i = 0; i < n; i++)
        {
            var d = Combat.Rot(aim, n > 1 ? (i - (n - 1) * .5f) * 8f : 0f);
            CombatBolt.Fire(Muzzle(d, 1.2f), new BoltSpec { dir = d, speed = Mathf.Lerp(13f, 26f, c), dmg = dmg, radius = .2f, life = 1.6f, el = el, pierce = def.pierce + RunState.extraPierce + (full ? 1 : 0), bounce = RunState.bounces, friendly = true, col = tint, style = BoltStyle.Arrow, knock = full ? 3f : 0f, size = full ? 1.2f : 1f });
        }
        if (full) { RFx.Ring(Muzzle(aim, 1.2f), .8f, tint, .25f); RFx.Shake(.1f); }
        RFx.Sparks(Muzzle(aim, 1.2f), aim, tint, 3, 6f, 20f); recoil = 1f + c; RogueAudio.Play(RSfx.BowShot, .5f + c * .4f, Mathf.Lerp(.9f, 1.1f, c));
    }

    /// <summary>Súng điện từ: sạc càng lâu càng mạnh, nhả ra bắn tia xuyên mọi mục tiêu và bật tường.</summary>
    void ReleaseRail(WeaponInstance w, Vector2 aim)
    {
        charging = false; float c = charge; bool full = c >= 1f; charge = 0f; var def = w.Def; Element el = Elem(w); Color tint = Tint(el);
        if (c < .22f) { cd = .15f; RFx.Burst(Muzzle(aim, 1.2f), tint, 3, 2f, .2f, .1f); return; }        // sạc chưa đủ: chỉ xì hơi
        cd = def.cooldown * CdMul * .5f; float dmg = w.Damage * Mathf.Lerp(.7f, 3.2f, c);
        FireBeam(w, aim, dmg, el, tint, 99, RunState.bounces + (full ? 1 : 0), def.range, Mathf.Lerp(.25f, .55f, c), false);
        RFx.Shake(.18f + .3f * c); Combat.HitStop(.03f + .04f * c); kb += -aim * (2f + 4f * c); recoil = 1.5f + 2f * c; RFx.Ring(Muzzle(aim, 1.3f), .8f + c, tint, .3f);
        RogueAudio.Play(RSfx.Laser, .9f, .6f); RogueAudio.Play(RSfx.Boom, .35f + .3f * c, 1.6f);
    }

    // ---- laze và tia điện từ ----
    void FireBeam(WeaponInstance w, Vector2 aim, float dmg, Element el, Color tint, int pierceCount, int bounces, float range, float width, bool continuous)
    {
        Vector2 a = Muzzle(aim, 1.5f), dir = aim; float rem = range; var hitSet = new HashSet<Foe>(); Vector2 start = a; int segs = 0;
        while (rem > 0.1f && segs <= bounces)
        {
            float tmax = rem;
            if (dir.x != 0f) { float tx = dir.x > 0f ? (RoomManager.MaxX - start.x) / dir.x : (RoomManager.MinX - start.x) / dir.x; if (tx >= 0f) tmax = Mathf.Min(tmax, tx); }
            if (dir.y != 0f) { float ty = dir.y > 0f ? (RoomManager.WallY - start.y) / dir.y : (RoomManager.MinY - start.y) / dir.y; if (ty >= 0f) tmax = Mathf.Min(tmax, ty); }
            Vector2 end = start + dir * tmax;
            foreach (var f in new List<Foe>(Combat.Foes))
            {
                if (f == null || f.Dead || !f.Targetable || hitSet.Contains(f)) continue;
                Vector2 ab = end - start; float kk = Mathf.Clamp01(Vector2.Dot(f.Pos - start, ab) / Mathf.Max(.0001f, ab.sqrMagnitude)); float dd = Vector2.Distance(f.Pos, start + ab * kk);
                if (dd > f.radius + width * .6f) continue;
                hitSet.Add(f); float d2 = dmg; bool crit = Combat.Roll(ref d2, .06f); f.TakeHit(d2, el, start, continuous ? 0f : 3f, crit, continuous); Extra(f, start); RFx.Sparks(start + ab * kk, -dir, tint, 3, 5f, 70f);
            }
            RFx.BeamLine(start, end, tint, width, continuous ? .09f : .2f); segs++; rem -= tmax;
            if (segs <= bounces) { if (Mathf.Abs(end.x) >= RoomManager.MaxX - .01f) dir.x = -dir.x; else dir.y = -dir.y; start = end + dir * .02f; RFx.Sparks(end, dir, tint, 4); }
            else RFx.Burst(end, tint, 3, 3f, .25f, .12f);
        }
        RFx.Flash(a, .8f, tint, .06f); recoil = .35f; RFx.Shake(.025f); if (continuous && Time.frameCount % 3 == 0) RogueAudio.Play(RSfx.Laser, .22f, 1f + heat * .3f);
    }

    // ---- cận chiến ----
    void ReflectBolts(Vector2 pos, Vector2 aim, float range, float arcDeg)
    {
        if (!RunState.Has(BuffId.Reflect)) return;
        foreach (var b in CombatBolt.Enemy.ToArray())
        {
            if (b == null) continue; Vector2 d = (Vector2)b.transform.position - pos;
            if (d.magnitude > range + .5f || (arcDeg < 359f && Vector2.Angle(aim, d) > arcDeg * .5f)) continue;
            b.Reflect(d.normalized); RFx.Ring(b.transform.position, .5f, new Color(1f, .9f, .5f), .2f); RogueAudio.Play(RSfx.Reflect, .5f, 1.4f);
        }
    }

    void MeleeArc(WeaponInstance w, Vector2 aim, float range, float arcDeg, float dmg, Element el, Color tint, RSfx sfx, float knock, float lunge)
    {
        Vector2 pos = transform.position; sideFlip = -sideFlip; int hits = 0;
        foreach (var f in new List<Foe>(Combat.Foes))
        {
            if (f == null || f.Dead || !f.Targetable) continue;
            Vector2 d = f.Pos - pos; float dist = d.magnitude;
            if (dist > range + f.radius || (Vector2.Angle(aim, d) > arcDeg * .5f && dist > f.radius + .3f)) continue;
            float dd = dmg; bool crit = Combat.Roll(ref dd, .12f); f.TakeHit(dd, el, pos, knock, crit); Extra(f, pos); hits++;
            RFx.Sparks(f.Pos, d, tint, crit ? 8 : 5, 8f, 70f);
        }
        ReflectBolts(pos, aim, range, arcDeg);
        RFx.Slash(pos + aim * .5f, aim, range * 1.1f, tint, .2f, sideFlip > 0f, arcDeg > 140f ? 1.15f : .9f);
        if (RunState.wave) CombatBolt.Fire(pos + aim * .6f, new BoltSpec { dir = aim, speed = 12f, dmg = dmg * .5f, radius = .45f, life = 1f, el = el, pierce = 99, friendly = true, col = tint, style = BoltStyle.Shard, size = 2f });
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
            if (dd > f.radius + .3f) continue;
            float d2 = dmg; bool crit = Combat.Roll(ref d2, .1f); f.TakeHit(d2, el, pos, 2.5f, crit); Extra(f, pos); hits++; RFx.Sparks(f.Pos, aim, tint, 5, 8f, 40f);
        }
        ReflectBolts(pos + aim * length * .5f, aim, length * .6f, 80f);
        RFx.BeamLine(pos + aim * .5f, tip, Color.Lerp(tint, Color.white, .5f), .22f, .16f); RFx.Sparks(tip, aim, tint, 4, 7f, 25f);
        if (RunState.wave) CombatBolt.Fire(tip, new BoltSpec { dir = aim, speed = 14f, dmg = dmg * .5f, radius = .35f, life = 1f, el = el, pierce = 99, friendly = true, col = tint, style = BoltStyle.Shard, size = 1.8f });
        kb += aim * 3.5f; RogueAudio.Play(RSfx.Thrust, .6f); if (hits > 0) { Combat.HitStop(.04f); RFx.Shake(.08f); }
    }

    void SpinAttack(WeaponInstance w, float range, float dmg, Element el, Color tint)
    {
        Vector2 pos = transform.position; int hits = 0; spinT = .28f; spinAngle = Combat.Ang(facing); StartCoroutine(SpinRoutine());
        foreach (var f in new List<Foe>(Combat.Foes))
        {
            if (f == null || f.Dead || !f.Targetable) continue;
            Vector2 d = f.Pos - pos; if (d.magnitude > range + f.radius) continue;
            float dd = dmg; bool crit = Combat.Roll(ref dd, .12f); f.TakeHit(dd, el, pos, 7f, crit); Extra(f, pos); hits++; RFx.Sparks(f.Pos, d, tint, 7, 9f, 80f);
        }
        ReflectBolts(pos, facing, range, 360f);
        RFx.Spin(pos, range, tint, .3f);
        if (RunState.wave) for (int i = 0; i < 6; i++) CombatBolt.Fire(pos, new BoltSpec { dir = Combat.FromAng(i * 60f), speed = 11f, dmg = dmg * .35f, radius = .4f, life = .9f, el = el, pierce = 99, friendly = true, col = tint, style = BoltStyle.Shard, size = 1.6f });
        RFx.Shake(hits > 0 ? .28f : .14f); if (hits > 0) Combat.HitStop(.06f); RogueAudio.Play(RSfx.AxeSwing, .8f); RFx.Puff(pos + Vector2.down * .5f, new Color(.7f, .66f, .6f), 4, .4f);
    }

    System.Collections.IEnumerator SpinRoutine()
    {
        float t = 0f; float a0 = Combat.Ang(facing);
        while (t < .28f) { t += Time.deltaTime; spinAngle = a0 + 360f * (t / .28f); yield return null; }
        spinT = 0f;
    }

    // ---- pháp khí: quả cầu bay quanh người ----
    void UpdateOrbs(float dt, bool fire, Vector2 aim, WeaponInstance w)
    {
        int want = 3 + RunState.extraShots; Element el = Elem(w); Color tint = Tint(el);
        while (orbs.Count < want) { var o = Combat.MakePx("Orb", transform, RogueArt.OrbitOrb((int)el), Combat.OrdFx - 25, null, 1.4f); orbs.Add(o.transform); }
        while (orbs.Count > want) { Destroy(orbs[orbs.Count - 1].gameObject); orbs.RemoveAt(orbs.Count - 1); }
        orbAng += dt * (fire ? 260f : 140f); float rad = fire ? 1.35f : 1.1f;
        for (int i = 0; i < orbs.Count; i++)
        {
            float a = orbAng + i * 360f / orbs.Count; var t = orbs[i]; t.localPosition = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad) * .75f) * rad + new Vector2(0, -.05f);
            var osr = t.GetComponent<SpriteRenderer>(); osr.sprite = RogueArt.OrbitOrb((int)el); osr.sortingOrder = Combat.Order(transform.position.y + t.localPosition.y) + 2;
            Vector2 wp = t.position;
            foreach (var f in new List<Foe>(Combat.Foes))
            {
                if (f == null || f.Dead || !f.Targetable) continue;
                if (Vector2.Distance(wp, f.Pos) > f.radius + .28f) continue;
                if (!orbHit.TryGetValue(f, out float last) || Combat.Now - last > .45f) { orbHit[f] = Combat.Now; float dd = w.Damage * .9f; bool crit = Combat.Roll(ref dd, .06f); f.TakeHit(dd, el, wp, 3f, crit); Extra(f, wp); }
            }
            for (int b = CombatBolt.Enemy.Count - 1; b >= 0; b--) { var eb = CombatBolt.Enemy[b]; if (eb == null) continue; if (Vector2.Distance(eb.transform.position, wp) < .4f) { RFx.Burst(eb.transform.position, tint, 5, 3f, .3f, .12f); RogueAudio.Play(RSfx.HitMetal, .3f, 1.5f); Destroy(eb.gameObject); } }
            if (Random.value < dt * 6f) RFx.Burst(wp, tint, 1, .5f, .3f, .1f);
        }
    }

    void OrbBarrage(WeaponInstance w, Vector2 aim, float dmg, Element el, Color tint)
    {
        Color col = el == Element.None ? new Color(.75f, .6f, 1f) : tint;
        for (int i = 0; i < orbs.Count; i++)
        {
            Vector2 from = orbs[i].position; var tgt = Combat.Nearest(from, 12f); Vector2 d = tgt != null ? (tgt.Pos - from).normalized : aim;
            CombatBolt.Fire(from, new BoltSpec { dir = Combat.Rot(d, Random.Range(-8f, 8f)), speed = w.Def.speed, dmg = dmg, radius = .2f, life = 2f, el = el, homing = 2.6f, bounce = RunState.bounces, pierce = RunState.extraPierce, friendly = true, col = col, style = BoltStyle.Shard, size = 1.2f });
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
