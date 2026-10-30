using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Hiệp sĩ: WASD/mũi tên di chuyển, Space/J/chuột trái chém (tự ngắm vào rồng), Shift lướt.</summary>
public class PlayerKnight : MonoBehaviour
{
    [Header("Chỉ số gốc (buff sẽ thay đổi các giá trị này)")]
    public float Hp = 100f, MaxHp = 100f, Damage = 12f, AtkInterval = .4f, Range = 3.1f, Speed = 5.9f, Crit = .1f, Lifesteal, Regen;
    public int Shield, Thunder;
    [Header("Buff đặc biệt (bật bởi BuffSystem)")]
    public bool FireSword, IceSword, WindSword, WaterSword, LightningSword, Reflect;
    [HideInInspector] public float Invuln;
    public bool Alive = true;

    public Vector2 Position => transform.position;
    public Vector2 Center => (Vector2)transform.position + Vector2.up * .9f;
    public bool DashReady => dashCd <= 0f;
    public int Face => face;

    BossFightManager mgr; Transform vis, swordT, capeT; SpriteRenderer body, sword, shieldSr, cape, swordGlow; Companion ally;
    float cd, swing, dashT, dashCd, thunderT = 3f, iceT = 3f, swordFx; Vector2 dashDir; int face = 1;
    float swingDur = .18f, swStart, swSweep, lgReset; int swingKind, windN, lgStep;   // swingKind: 0 = chém thường, 3 = vung kiếm theo góc thế giới (Kiếm Sét)
    const float XMax = 13.75f, YMin = -8.25f, YMax = 5.25f;

    static SpriteRenderer NewSr(string n, Transform parent, Sprite s)
    {
        var go = new GameObject(n); go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; return sr;
    }

    public static PlayerKnight Create(BossFightManager m, Vector2 pos)
    {
        var go = new GameObject("Knight"); go.transform.position = pos;
        var p = go.AddComponent<PlayerKnight>(); p.mgr = m;
        var sh = NewSr("Shadow", go.transform, ProcSprites.Circle); sh.transform.localScale = new Vector3(.32f, .1f, 1f); sh.color = new Color(0, 0, 0, .4f); sh.sortingOrder = -900;
        p.vis = new GameObject("Visual").transform; p.vis.SetParent(go.transform, false);
        p.body = NewSr("Body", p.vis, ProcSprites.KnightIdle[0]);
        p.cape = NewSr("Cape", p.vis, ProcSprites.Cape); p.capeT = p.cape.transform; p.capeT.localPosition = new Vector3(-.5f, 1.1f, 0f);
        Gfx.Glow(go.transform, new Vector2(0, .4f), 3.6f, new Color(.5f, .7f, 1f, .14f), -896);
        p.sword = NewSr("Sword", p.vis, ProcSprites.Sword); p.swordT = p.sword.transform; p.swordT.localPosition = new Vector3(.45f, .85f, 0f);
        p.swordGlow = Gfx.Glow(p.swordT, new Vector2(.8f, 0f), 2.4f, new Color(1f, 1f, 1f, 0f), 0); p.swordGlow.enabled = false;   // quầng sáng quanh kiếm (Kiếm Lửa / Kiếm Băng)
        p.shieldSr = NewSr("Shield", go.transform, ProcSprites.Ring); p.shieldSr.transform.localPosition = new Vector3(0, .9f, 0); p.shieldSr.transform.localScale = Vector3.one * .6f;
        p.shieldSr.color = new Color(.43f, .9f, 1f, .7f); p.shieldSr.enabled = false;
        TouchControls.Create(p, TouchLayout.Melee, () => p != null && p.Alive && p.mgr != null && p.mgr.Playing);   // điện thoại: joystick + nút ĐÁNH / LƯỚT
        return p;
    }

    static Vector2 ReadMove()
    {
        var k = Keyboard.current; Vector2 v = TouchControls.Move;
        if (k != null)
        {
            float x = ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1f : 0f) - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1f : 0f);
            float y = ((k.wKey.isPressed || k.upArrowKey.isPressed) ? 1f : 0f) - ((k.sKey.isPressed || k.downArrowKey.isPressed) ? 1f : 0f);
            v += new Vector2(x, y);
        }
        if (v.sqrMagnitude <= 0f) return Vector2.zero;
        return TouchControls.Move == Vector2.zero || v.sqrMagnitude > 1f ? v.normalized : v;   // bàn phím: tốc độ cố định; joystick: nhanh chậm theo độ kéo
    }

    void Update()
    {
        if (!Alive || mgr == null) return;
        var boss = mgr.Boss;
        if (!mgr.Playing || boss == null) return;
        float dt = Time.deltaTime;
        cd -= dt; Invuln -= dt; swing -= dt; dashCd -= dt; lgReset -= dt; if (lgReset <= 0f) lgStep = 0;
        TouchControls.DashCooldown01 = Mathf.Clamp01(dashCd / 1.1f);
        Hp = Mathf.Min(MaxHp, Hp + Regen * dt);

        var k = Keyboard.current; Vector2 mv = ReadMove(); bool moving = mv.sqrMagnitude > 0f;
        if (((k != null && k.leftShiftKey.wasPressedThisFrame) || TouchControls.DashPressed) && dashCd <= 0f)
        {
            dashT = .16f; dashCd = 1.1f; Invuln = Mathf.Max(Invuln, .3f); dashDir = moving ? mv : new Vector2(face, 0f);
            Sfx.Tone(200, .15f, 2, .2f, 300, snd: Snd.PlayerDash);
        }
        Vector2 vel = mv * Speed;
        if (dashT > 0f) { dashT -= dt; vel = dashDir * Speed * 3.4f; Fx.Afterimage(body.sprite, transform.position, face < 0, new Color(.5f, .7f, 1f, .6f)); }

        Vector3 pos = transform.position + (Vector3)(vel * dt);
        pos.x = Mathf.Clamp(pos.x, -XMax, XMax); pos.y = Mathf.Clamp(pos.y, YMin, YMax); transform.position = pos;

        face = boss.Position.x >= pos.x ? 1 : -1; vis.localScale = dashT > 0f ? new Vector3(face * 1.3f, .8f, 1f) : new Vector3(face, 1f, 1f);   // lướt: giãn ngang

        bool want = TouchControls.AttackHeld || (k != null && (k.spaceKey.isPressed || k.jKey.isPressed)) || (Mouse.current != null && Mouse.current.leftButton.isPressed);
        if (want && cd <= 0f) Attack(boss);

        if (Thunder > 0) { thunderT -= dt; if (thunderT <= 0f) { thunderT = 3f; boss.Lightning(15f * Thunder); } }
        if (IceSword) { iceT -= dt; if (iceT <= 0f) { iceT = 3f; CastIce(boss); } }   // mỗi 3 giây: 3 hàng băng

        // animation
        var frames = moving ? ProcSprites.KnightWalk : ProcSprites.KnightIdle;
        body.sprite = frames[(int)(Time.time * (moving ? 11f : 3f)) % frames.Length];
        float rot = -20f;
        if (swing > 0f)
        {
            if (swingKind == 3) { float wa = swStart + swSweep * Mathf.Clamp01((swingDur - swing) / Mathf.Max(.01f, swingDur - .08f)); rot = face > 0 ? wa : 180f - wa; }   // Kiếm Sét: kiếm quét đúng hướng các tia sét
            else rot = Mathf.Lerp(80f, -80f, 1f - swing / swingDur);
        }
        swordT.localRotation = Quaternion.Euler(0, 0, rot);
        bool vis_ = !(Invuln > 0f && ((int)(Time.time * 20f)) % 2 == 0);
        body.enabled = vis_; sword.enabled = vis_; cape.enabled = vis_;
        UpdateSwordLook(dt);
        capeT.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * (moving ? 14f : 4f)) * (moving ? 10f : 4f) - (moving ? 18f : 0f));   // áo choàng bay
        shieldSr.enabled = Shield > 0; if (Shield > 0) shieldSr.color = new Color(.43f, .9f, 1f, .6f + Mathf.Sin(Time.time * 8f) * .2f);
        int o = -Mathf.RoundToInt(pos.y * 20f); body.sortingOrder = o; sword.sortingOrder = o + 1; swordGlow.sortingOrder = o + 2; shieldSr.sortingOrder = o + 2; cape.sortingOrder = o - 1;
    }

    void Attack(DragonBoss boss)
    {
        cd = AtkInterval; swing = swingDur = .18f; swingKind = 0;
        Vector2 c = Center; Vector2 to = boss.Position - c;
        float ang = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        Fx.Slash(c, ang, Range, AnySpecial ? SwordColor : (Color?)null);
        if (LightningSword) LightningCombo(c, ang);
        if (WindSword && ++windN >= 3) { windN = 0; Tornado.Spawn(c + to.normalized * 1.1f, ang, Mathf.Max(3f, Mathf.Round(Damage * .35f))); } Sfx.Tone(320, .12f, 1, .2f, -160, snd: Snd.PlayerSlash);
        for (int i = Projectile.All.Count - 1; i >= 0; i--)
        {
            var b = Projectile.All[i];
            if (b == null || b.Friendly || Vector2.Distance(b.transform.position, c) >= Range + .4f) continue;
            if (Reflect) b.Reflect(boss, Mathf.Round(Damage * 1.2f)); else b.Deflect();   // Chém Phản: hất đạn ngược lại về phía rồng
        }
        if (!boss.Dead && Vector2.Distance(boss.Position, transform.position) < Range + 2.4f)
        {
            bool crit = Random.value < Crit;
            float d = Mathf.Round(Damage * Random.Range(.9f, 1.1f) * (crit ? 2f : 1f));
            boss.TakeDamage(d, crit); Heal(d * Lifesteal);
            if (FireSword) boss.Ignite(3f, Mathf.Max(1f, Mathf.Round(Damage * .25f)));   // đốt 3 giây, chém tiếp thì làm mới (không cộng dồn)
            if (IceSword) boss.Chill(2.5f);
            if (WaterSword) WaterHit(boss, ang);                                              // làm chậm đòn tấn công của rồng
        }
        for (int i = Minion.All.Count - 1; i >= 0; i--)   // chém trúng đệ tử của rồng
        {
            var m = Minion.All[i];
            if (m == null || m.Spawning || Vector2.Distance(m.transform.position, c) > Range + 1f) continue;
            bool mc = Random.value < Crit; float md = Mathf.Round(Damage * Random.Range(.9f, 1.1f) * (mc ? 2f : 1f));
            m.TakeDamage(md, mc); Heal(md * Lifesteal);
            if (m != null) { if (FireSword) m.Ignite(3f, Mathf.Max(1f, Mathf.Round(Damage * .25f))); if (IceSword) m.Chill(2.5f); }
        }
    }

    /// <summary>Màu kiếm theo buff: Lửa = đỏ, Băng = xanh băng, có cả hai = tím.</summary>
    bool AnySpecial => FireSword || IceSword || WindSword || WaterSword || LightningSword;
    Color SwordColor
    {
        get
        {
            if (FireSword && IceSword && !WindSword && !WaterSword && !LightningSword) return new Color(.85f, .55f, 1f);
            Color sum = Color.black; int n = 0;
            if (FireSword) { sum += new Color(1f, .3f, .22f); n++; }
            if (IceSword) { sum += new Color(.55f, .92f, 1f); n++; }
            if (WindSword) { sum += new Color(.6f, .95f, .55f); n++; }
            if (WaterSword) { sum += new Color(.3f, .6f, 1f); n++; }
            if (LightningSword) { sum += new Color(1f, .9f, .3f); n++; }
            return n == 0 ? Color.white : new Color(sum.r / n, sum.g / n, sum.b / n, 1f);
        }
    }

    void UpdateSwordLook(float dt)
    {
        bool special = AnySpecial;
        sword.color = SwordColor; swordGlow.enabled = special && sword.enabled;
        if (!special) return;
        var gc = SwordColor; swordGlow.color = new Color(gc.r, gc.g, gc.b, .35f + .1f * Mathf.Sin(Time.time * 12f));
        swordFx -= dt;
        if (swordFx <= 0f)
        {
            swordFx = .05f; Vector2 tip = swordT.TransformPoint(new Vector3(Random.Range(.4f, 1.3f), 0f, 0f));
            if (FireSword) Fx.Spawn(ProcSprites.Pixel, tip, Random.value < .5f ? new Color(1f, .55f, .15f) : new Color(1f, .85f, .3f), .4f, 1.5f, .3f, new Vector2(Random.Range(-.6f, .6f), Random.Range(1.5f, 3f)));
            if (IceSword) Fx.Spawn(ProcSprites.Pixel, tip, Random.value < .5f ? new Color(.7f, .95f, 1f) : Color.white, .5f, 1.3f, .3f, new Vector2(Random.Range(-.8f, .8f), Random.Range(-1f, .5f)), 4f);
            if (WindSword) Fx.Spawn(ProcSprites.Pixel, tip, Random.value < .5f ? new Color(.75f, 1f, .7f) : Color.white, .45f, 1.3f, .3f, new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(-.5f, 1f)));
            if (WaterSword) Fx.Spawn(ProcSprites.Pixel, tip, Random.value < .5f ? new Color(.4f, .75f, 1f) : new Color(.8f, .95f, 1f), .5f, 1.4f, .3f, new Vector2(Random.Range(-.6f, .6f), Random.Range(.2f, 1.2f)), 9f);
            if (LightningSword) Fx.Spawn(ProcSprites.Pixel, tip, Random.value < .5f ? new Color(1f, .95f, .5f) : Color.white, .15f, 1.8f, .3f, Random.insideUnitCircle * 5f);
        }
    }

    /// <summary>Kiếm Băng: 3 hàng băng toả ra hình quạt về phía rồng, đóng băng kẻ địch bị quét trúng.</summary>
    void CastIce(DragonBoss boss)
    {
        if (boss == null || boss.Dead) return;
        Vector2 o = Position + Vector2.up * .3f; Vector2 to = boss.Position - o; float a = Mathf.Atan2(to.y, to.x);
        float dmg = Mathf.Max(6f, Mathf.Round(Damage * .8f));
        for (int i = -1; i <= 1; i++) { float an = a + i * .3f; IceRow.Spawn(o, new Vector2(Mathf.Cos(an), Mathf.Sin(an)), dmg, 1.2f); }
        Fx.Ring(Center, 2.4f, new Color(.55f, .9f, 1f), .35f); Fx.Burst(Center, 10, Fx.IceCols); Sfx.Tone(900, .25f, 2, .2f, -400, snd: Snd.IceCast);
    }

    /// <summary>Kiếm Sét: 3 đòn lặp lại - quẹt trái (5 tia), quẹt phải (5 tia), xoay tròn (8 tia). Ngừng chém 1.2 giây thì về đòn đầu.</summary>
    void LightningCombo(Vector2 c, float aimDeg)
    {
        int k = lgStep; bool spin = k == 2; int cnt = spin ? 8 : 5;
        float arc = 200f, sign = k == 1 ? -1f : 1f, gap = spin ? .035f : .045f;
        float start = spin ? aimDeg : aimDeg - sign * arc / 2f, sweep = spin ? 360f * (cnt - 1) / cnt : arc;
        float dmg = Mathf.Max(3f, Mathf.Round(Damage * (spin ? .8f : .5f))), len = Range + 7f;
        for (int i = 0; i < cnt; i++) LightningBolt.Spawn(c, start + sign * sweep * i / (cnt - 1), len, dmg, i * gap, i == 0);
        swingKind = 3; swStart = start; swSweep = sign * sweep; swingDur = (cnt - 1) * gap + .08f; swing = swingDur;
        lgStep = (k + 1) % 3; lgReset = 1.2f;
        if (spin) Fx.Ring(c, 3.4f, new Color(1f, .9f, .3f), .3f);
    }

    /// <summary>Kiếm Nước: mỗi lần chém trúng rồng gây thêm sát thương nước, 30% tạo sóng nước đẩy lùi.</summary>
    void WaterHit(DragonBoss boss, float aimDeg)
    {
        if (boss == null || boss.Dead || !mgr.Playing) return;
        Fx.Burst(boss.Position + Random.insideUnitCircle * .8f, 8, ElementFx.WaterCols);
        boss.TakeDamage(Mathf.Max(1f, Mathf.Round(Damage * .3f * Random.Range(.9f, 1.1f))), false);
        if (mgr.Playing && Random.value < .3f) WaterWave.Spawn(Center, aimDeg, Mathf.Max(3f, Mathf.Round(Damage * .8f)));
    }

    /// <summary>Buff Đệ Hiệp Sĩ: gọi 1 đệ đi theo (không cộng dồn).</summary>
    public void SpawnCompanion() { if (ally == null) ally = Companion.Create(mgr, this); }

    public void Heal(float v) { Hp = Mathf.Min(MaxHp, Hp + v); }

    /// <param name="force">true = bỏ qua thời gian bất tử (dùng khi trả lời sai)</param>
    public void Hurt(int d, bool force = false)
    {
        if (!Alive || !mgr.Playing) return;
        if (Invuln > 0f && !force) return;
        if (Shield > 0)
        {
            Shield--; Invuln = .5f; Fx.Ring(Center, 1.5f, new Color(.43f, .9f, 1f)); Sfx.Tone(600, .12f, 2, .2f, snd: Snd.ShieldBlock); return;
        }
        Hp -= d; Invuln = .9f; CameraRig.Shake(6f); mgr.HitStop(.06f); mgr.ui.FlashRed();
        mgr.ui.Float(Center + Vector2.up * 1.2f, "-" + d, new Color(1f, .36f, .36f), true);
        Fx.Burst(Center, 12, new[] { new Color(1f, .36f, .36f), Color.white }); Sfx.Tone(110, .2f, 1, .3f, -60, snd: Snd.PlayerHurt);
        if (Hp <= 0f) { Hp = 0f; Alive = false; mgr.OnPlayerDied(); }
    }
}
