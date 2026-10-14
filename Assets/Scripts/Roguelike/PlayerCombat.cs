using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// E3 – Hiệp sĩ trong trận (mục 5.6): WASD đi, chuột ngắm và bắn (giữ chuột trái hoặc phím J), Space lướt (hồi 3 giây, miễn sát thương 0,2 giây),
/// Q đổi vũ khí chính/phụ. Máu, vũ khí và buff nằm trong RunState nên sống qua các màn.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    public const float MaxHp = 100f, Speed = 6.2f, DashCooldown = 3f;
    public float radius = .45f;
    public bool Dead { get; private set; }
    public System.Action Died;
    public float DashLeft => Mathf.Max(0f, dashCd);
    public float Hp => RunState.hp;

    float cd, dashCd, dashT, invuln; Vector2 dashDir, facing = Vector2.up; SpriteRenderer body;

    public static PlayerCombat Create(Vector2 pos)
    {
        var go = new GameObject("Player"); go.transform.position = pos;
        var p = go.AddComponent<PlayerCombat>();
        Combat.MakeSr("Shade", go.transform, ProcSprites.Circle, new Color(0, 0, 0, .4f), new Vector2(.9f, .3f), 9, new Vector2(0, -.7f));
        var knight = Resources.Load<Sprite>("MainMenu/px_knight_0");
        if (knight != null)
        {
            p.body = Combat.MakeSr("Body", go.transform, knight, Color.white, new Vector2(knight.bounds.size.x / knight.bounds.size.y * 1.6f, 1.6f), 10);
        }
        else p.body = Combat.MakeSr("Body", go.transform, ProcSprites.Circle, GameTheme.Gold, new Vector2(.9f, .9f), 10);
        if (RunState.hp <= 0f) RunState.hp = MaxHp;
        TouchControls.Create(p, TouchLayout.Shooter, () => p != null && !p.Dead && !RoomManager.Frozen, () => RunState.sub != null);   // điện thoại: joystick + nút
        return p;
    }

    // ------------------------------------------------------------------ sát thương và hồi máu
    public void Hurt(float d, Element e)
    {
        if (Dead || invuln > 0f || d <= 0f) return;
        d *= 1f - RunState.resist[(int)e];                         // buff Kháng nguyên tố
        RunState.hp = Mathf.Max(0f, RunState.hp - d); invuln = .6f;
        FloatText.Spawn(transform.position + Vector3.up * 1.1f, "-" + Mathf.RoundToInt(d), GameTheme.Blood, .15f);
        GameAudio.Play(Snd.PlayerHurt);
        if (RunState.hp <= 0f) { Dead = true; Died?.Invoke(); }
    }

    public void Heal(float a)
    {
        if (Dead || a <= 0f) return;
        RunState.hp = Mathf.Min(MaxHp, RunState.hp + a);
        FloatText.Spawn(transform.position + Vector3.up * 1.1f, "+" + Mathf.RoundToInt(a), GameTheme.Moss, .15f);
    }

    public void OnKill() { if (RunState.killHeal > 0) Heal(3f * RunState.killHeal); }

    // ------------------------------------------------------------------ vòng lặp
    Vector2 AimDir()
    {
        if (TouchControls.Aim.sqrMagnitude > .01f) return TouchControls.Aim;          // joystick ngắm bên phải
        var cam = Camera.main;
        if (cam == null || Mouse.current == null) return facing;
        Vector3 m = Mouse.current.position.ReadValue(); m.z = -cam.transform.position.z;
        Vector2 d = (Vector2)cam.ScreenToWorldPoint(m) - (Vector2)transform.position;
        return d.sqrMagnitude < .01f ? facing : d.normalized;
    }

    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f || Dead) return;
        var k = Keyboard.current; if (k == null && !TouchControls.Active) return;
        if (invuln > 0f) invuln -= dt; if (cd > 0f) cd -= dt; if (dashCd > 0f) dashCd -= dt;
        TouchControls.DashCooldown01 = Mathf.Clamp01(dashCd / DashCooldown);

        float x = 0f, y = 0f;
        if (k != null)
        {
            x = ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1f : 0f) - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1f : 0f);
            y = ((k.wKey.isPressed || k.upArrowKey.isPressed) ? 1f : 0f) - ((k.sKey.isPressed || k.downArrowKey.isPressed) ? 1f : 0f);
        }
        var mv = new Vector2(x, y) + TouchControls.Move; if (mv.sqrMagnitude > 1f) mv.Normalize();
        Vector2 aim = AimDir();
        if (TouchControls.Active && !TouchControls.AimHeld && mv.sqrMagnitude > .01f) aim = mv.normalized;   // cảm ứng: không ngắm thì quay mặt theo hướng đi
        facing = aim;

        if (((k != null && k.spaceKey.wasPressedThisFrame) || TouchControls.DashPressed) && dashCd <= 0f && dashT <= 0f)
        { dashT = .2f; dashCd = DashCooldown; invuln = Mathf.Max(invuln, .2f); dashDir = mv.sqrMagnitude > .01f ? mv : aim; GameAudio.Play(Snd.PlayerDash); }

        if (dashT > 0f) { dashT -= dt; Step(dashDir * 18f * dt); } else Step(mv * Speed * dt);

        if (((k != null && k.qKey.wasPressedThisFrame) || TouchControls.SwapPressed) && RunState.sub != null) { var t = RunState.main; RunState.main = RunState.sub; RunState.sub = t; GameAudio.Play(Snd.UiClick); }

        bool fire = (k != null && k.jKey.isPressed) || TouchControls.AimHeld || (Mouse.current != null && Mouse.current.leftButton.isPressed &&
                                         (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()));
        if (fire && cd <= 0f && RunState.main != null) Attack(aim);

        // thân quay theo hướng ngắm; nhấp nháy khi đang miễn sát thương
        var s = body.transform.localScale; s.x = Mathf.Abs(s.x) * (aim.x < 0f ? -1f : 1f); body.transform.localScale = s;
        var c = body.color; c.a = invuln > 0f && dashT <= 0f && ((int)(Combat.Now * 20f) % 2 == 0) ? .35f : 1f; body.color = c;
    }

    void Step(Vector2 d)
    {
        Vector2 p = (Vector2)transform.position + d;
        p.x = Mathf.Clamp(p.x, RoomManager.MinX, RoomManager.MaxX);
        bool inDoor = RoomManager.Instance != null && RoomManager.Instance.DoorOpen && Mathf.Abs(p.x) < RoomManager.DoorHalf - .2f;
        p.y = Mathf.Clamp(p.y, RoomManager.MinY, inDoor ? RoomManager.ExitY + .5f : RoomManager.WallY);
        transform.position = p;
    }

    // ------------------------------------------------------------------ tấn công
    void Attack(Vector2 aim)
    {
        var w = RunState.main; var def = w.Def;
        cd = def.cooldown / (1f + .15f * RunState.atkSpeedStacks);                 // buff Tốc đánh
        Element el = w.element != Element.None ? w.element : RunState.infuse;      // buff Nhiễm nguyên tố chỉ cho vũ khí chưa có nguyên tố
        float dmg = w.Damage; Vector2 pos = transform.position;
        Color tint = el == Element.None ? new Color(1f, .95f, .75f) : Combat.ElementColor(el);

        if (def.melee)
        {
            foreach (var m in new System.Collections.Generic.List<Monster>(Combat.Monsters))
            {
                if (m == null || m.Dead) continue;
                Vector2 d = (Vector2)m.transform.position - pos;
                if (d.magnitude > def.range + m.radius || Vector2.Angle(aim, d) > 75f) continue;
                m.Damage(dmg, el);
                if (el != Element.None) Elements.Apply(m, el);
            }
            CombatFx.Pulse(pos + aim * 1.0f, def.range * .6f, tint);
            if (RunState.wave) CombatBolt.Fire(pos + aim * .8f, aim, 12f, dmg * .5f, .3f, tint, true, 1f, Element.None, 3, 0);   // buff Sóng chém
            GameAudio.Play(Snd.PlayerSlash);
            return;
        }

        int n = def.count + RunState.extraShots;                                   // buff Thêm đạn
        for (int i = 0; i < n; i++)
        {
            float ang = n > 1 ? (i - (n - 1) * .5f) * Mathf.Max(def.spreadDeg, 9f) : 0f;
            if (def.spreadDeg > 0f) ang += Random.Range(-def.spreadDeg * .5f, def.spreadDeg * .5f);
            Vector2 d = (Vector2)(Quaternion.Euler(0f, 0f, ang) * (Vector3)aim);
            CombatBolt.Fire(pos + d * .7f, d, def.speed, dmg, .2f, tint, true, 9.5f / def.speed + .4f, el, def.pierce + RunState.extraPierce, RunState.bounces);
        }
        GameAudio.Play(Snd.PlayerSlash);
    }
}
