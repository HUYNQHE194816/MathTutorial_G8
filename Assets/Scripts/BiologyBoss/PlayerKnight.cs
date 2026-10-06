using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Hiệp sĩ: WASD/mũi tên di chuyển, Space/J/chuột trái chém (tự ngắm vào rồng), Shift lướt.</summary>
public class PlayerKnight : MonoBehaviour
{
    [Header("Chỉ số gốc (buff sẽ thay đổi các giá trị này)")]
    public float Hp = 100f, MaxHp = 100f, Damage = 12f, AtkInterval = .4f, Range = 3.1f, Speed = 5.9f, Crit = .1f, Lifesteal, Regen;
    public int Shield, Thunder;
    [Header("Buff đặc biệt (bật bởi BuffSystem)")]
    public bool FireSword, IceSword, Reflect;
    [HideInInspector] public float Invuln;
    public bool Alive = true;

    public Vector2 Position => transform.position;
    public Vector2 Center => (Vector2)transform.position + Vector2.up * .9f;
    public bool DashReady => dashCd <= 0f;
    public int Face => face;

    BossFightManager mgr; Transform vis, swordT, capeT; SpriteRenderer body, sword, shieldSr, cape, swordGlow; Companion ally;
    float cd, swing, dashT, dashCd, thunderT = 3f, iceT = 3f, swordFx; Vector2 dashDir; int face = 1;
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
        return p;
    }

    static Vector2 ReadMove()
    {
        var k = Keyboard.current; if (k == null) return Vector2.zero;
        float x = ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1f : 0f) - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1f : 0f);
        float y = ((k.wKey.isPressed || k.upArrowKey.isPressed) ? 1f : 0f) - ((k.sKey.isPressed || k.downArrowKey.isPressed) ? 1f : 0f);
        return new Vector2(x, y).normalized;
    }

    void Update()
    {
        if (!Alive || mgr == null) return;
        var boss = mgr.Boss;
        if (!mgr.Playing || boss == null) return;
        float dt = Time.deltaTime;
        cd -= dt; Invuln -= dt; swing -= dt; dashCd -= dt;
        Hp = Mathf.Min(MaxHp, Hp + Regen * dt);

        var k = Keyboard.current; Vector2 mv = ReadMove(); bool moving = mv.sqrMagnitude > 0f;
        if (k != null && k.leftShiftKey.wasPressedThisFrame && dashCd <= 0f)
        {
            dashT = .16f; dashCd = 1.1f; Invuln = Mathf.Max(Invuln, .3f); dashDir = moving ? mv : new Vector2(face, 0f);
            Sfx.Tone(200, .15f, 2, .2f, 300, snd: Snd.PlayerDash);
        }
        Vector2 vel = mv * Speed;
        if (dashT > 0f) { dashT -= dt; vel = dashDir * Speed * 3.4f; Fx.Afterimage(body.sprite, transform.position, face < 0, new Color(.5f, .7f, 1f, .6f)); }

        Vector3 pos = transform.position + (Vector3)(vel * dt);
        pos.x = Mathf.Clamp(pos.x, -XMax, XMax); pos.y = Mathf.Clamp(pos.y, YMin, YMax); transform.position = pos;

        face = boss.Position.x >= pos.x ? 1 : -1; vis.localScale = dashT > 0f ? new Vector3(face * 1.3f, .8f, 1f) : new Vector3(face, 1f, 1f);   // lướt: giãn ngang

        bool want = (k != null && (k.spaceKey.isPressed || k.jKey.isPressed)) || (Mouse.current != null && Mouse.current.leftButton.isPressed);
        if (want && cd <= 0f) Attack(boss);

        if (Thunder > 0) { thunderT -= dt; if (thunderT <= 0f) { thunderT = 3f; boss.Lightning(15f * Thunder); } }
        if (IceSword) { iceT -= dt; if (iceT <= 0f) { iceT = 3f; CastIce(boss); } }   // mỗi 3 giây: 3 hàng băng

        // animation
        var frames = moving ? ProcSprites.KnightWalk : ProcSprites.KnightIdle;
        body.sprite = frames[(int)(Time.time * (moving ? 11f : 3f)) % frames.Length];
        swordT.localRotation = Quaternion.Euler(0, 0, swing > 0f ? Mathf.Lerp(80f, -80f, 1f - swing / .18f) : -20f);
        bool vis_ = !(Invuln > 0f && ((int)(Time.time * 20f)) % 2 == 0);
        body.enabled = vis_; sword.enabled = vis_; cape.enabled = vis_;
        UpdateSwordLook(dt);
        capeT.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * (moving ? 14f : 4f)) * (moving ? 10f : 4f) - (moving ? 18f : 0f));   // áo choàng bay
        shieldSr.enabled = Shield > 0; if (Shield > 0) shieldSr.color = new Color(.43f, .9f, 1f, .6f + Mathf.Sin(Time.time * 8f) * .2f);
        int o = -Mathf.RoundToInt(pos.y * 20f); body.sortingOrder = o; sword.sortingOrder = o + 1; swordGlow.sortingOrder = o + 2; shieldSr.sortingOrder = o + 2; cape.sortingOrder = o - 1;
    }

    void Attack(DragonBoss boss)
    {
        cd = AtkInterval; swing = .18f;
        Vector2 c = Center; Vector2 to = boss.Position - c;
        float ang = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        Fx.Slash(c, ang, Range, FireSword || IceSword ? SwordColor : (Color?)null); Sfx.Tone(320, .12f, 1, .2f, -160, snd: Snd.PlayerSlash);
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
            if (IceSword) boss.Chill(2.5f);                                              // làm chậm đòn tấn công của rồng
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
    Color SwordColor => FireSword && IceSword ? new Color(.85f, .55f, 1f) : FireSword ? new Color(1f, .3f, .22f) : IceSword ? new Color(.55f, .92f, 1f) : Color.white;

    void UpdateSwordLook(float dt)
    {
        bool special = FireSword || IceSword;
        sword.color = SwordColor; swordGlow.enabled = special && sword.enabled;
        if (!special) return;
        var gc = SwordColor; swordGlow.color = new Color(gc.r, gc.g, gc.b, .35f + .1f * Mathf.Sin(Time.time * 12f));
        swordFx -= dt;
        if (swordFx <= 0f)
        {
            swordFx = .05f; Vector2 tip = swordT.TransformPoint(new Vector3(Random.Range(.4f, 1.3f), 0f, 0f));
            if (FireSword) Fx.Spawn(ProcSprites.Pixel, tip, Random.value < .5f ? new Color(1f, .55f, .15f) : new Color(1f, .85f, .3f), .4f, 1.5f, .3f, new Vector2(Random.Range(-.6f, .6f), Random.Range(1.5f, 3f)));
            if (IceSword) Fx.Spawn(ProcSprites.Pixel, tip, Random.value < .5f ? new Color(.7f, .95f, 1f) : Color.white, .5f, 1.3f, .3f, new Vector2(Random.Range(-.8f, .8f), Random.Range(-1f, .5f)), 4f);
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
