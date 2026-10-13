using UnityEngine;

public enum MonsterKind { Gunner, Charger, Mage, Robot, Summoner, Minion }

/// <summary>
/// E3 – Quái chặng 1 (mục 5.1 và 5.2). Năm loại: Xạ thủ, Lao chém, Phù thuật sư, Lính robot (robot vỡ thì quái lăn ra, phase 1),
/// Triệu hồi sư (đệ tử là Minion), cộng Tinh anh (máu x4, sát thương x1,5, to hơn 40%, có viền vàng).
/// Hành vi viết gọn bằng switch; mọi con số là giá trị khởi điểm. Hình dạng tạm là hình tròn màu, chờ đồ họa thật.
/// Bộ đếm thời gian dùng Combat.Now (không chạy khi đang viết lời giải).
/// </summary>
public class Monster : MonoBehaviour
{
    public static System.Action<Monster> Killed;      // RoomManager gán: tính Mực, rơi hồi máu...

    public MonsterKind kind; public bool elite; public int phase;       // phase 0: robot còn nguyên, 1: quái đã lộ diện
    public float hp, maxHp, radius = .5f;
    public bool Dead { get; private set; }

    // Trạng thái nguyên tố: Elements.Apply (CombatCore.cs) ghi vào các trường này.
    public Element auraEl; public float auraT, wetUntil, burnUntil, slowUntil, stunUntil, freezeUntil;

    float dmgMul = 1f, hpMul = 1f, speedMul = 1f, rate = 1f, projMul = 1f, speed, atkT, stateT, strafeT, strafeSign = 1f, burnAcc, spawnT = .8f, burstT, flashUntil, step, barScaleX;
    int state, burst; Vector2 dashDir; bool dashHit;
    SpriteRenderer body, barFill; Color baseColor;

    public static Monster Create(MonsterKind kind, Vector2 pos, bool elite)
    {
        var go = new GameObject(kind + (elite ? "_Elite" : "")); go.transform.position = pos;
        var m = go.AddComponent<Monster>(); m.kind = kind; m.elite = elite; m.Setup();
        Combat.Monsters.Add(m);
        return m;
    }

    void Setup()
    {
        float hp0, sz; Color col;
        // Độ khó của lượt chơi: quái trâu hơn, nhanh hơn, đau hơn, ra đòn dày hơn (RoomRules).
        var diff = RunState.difficulty;
        hpMul = RoomRules.HpMul(diff); speedMul = RoomRules.SpeedMul(diff); dmgMul = RoomRules.DamageMul(diff); rate = RoomRules.RateMul(diff);
        projMul = 1f + (speedMul - 1f) * .7f;
        switch (kind)
        {
            case MonsterKind.Gunner:   hp0 = 30f; speed = 2.6f; sz = .9f;  col = new Color(1f, .62f, .3f); break;
            case MonsterKind.Charger:  hp0 = 40f; speed = 3.4f; sz = 1.0f; col = new Color(.92f, .25f, .3f); break;
            case MonsterKind.Mage:     hp0 = 25f; speed = 2.2f; sz = .9f;  col = new Color(.72f, .42f, 1f); break;
            case MonsterKind.Robot:    hp0 = 60f; speed = 2.8f; sz = 1.3f; col = new Color(.6f, .66f, .78f); break;
            case MonsterKind.Summoner: hp0 = 35f; speed = 2.4f; sz = 1.0f; col = new Color(.4f, .85f, .5f); break;
            default:                   hp0 = 8f;  speed = 3.9f; sz = .5f;  col = new Color(.65f, 1f, .7f); break;
        }
        speed *= speedMul; col = Color.Lerp(col, GameTheme.Blood, .13f * (int)diff);      // khó hơn thì quái ngả đỏ máu
        if (elite) { hp0 *= 4f; dmgMul *= 1.5f; sz *= 1.4f; }
        hp0 *= hpMul;
        maxHp = hp = hp0; radius = sz * .5f; baseColor = col;
        atkT = (kind == MonsterKind.Summoner ? 1.5f : kind == MonsterKind.Minion ? 0f : Random.Range(.6f, 1.6f)) / rate;

        Combat.MakeSr("Shade", transform, ProcSprites.Circle, new Color(0, 0, 0, .35f), new Vector2(sz * 1.1f, sz * .35f), 4, new Vector2(0, -sz * .45f));
        body = Combat.MakeSr("Body", transform, ProcSprites.Circle, col, new Vector2(sz, sz), 5);
        if (elite) Combat.MakeSr("EliteRing", transform, ProcSprites.Ring, GameTheme.Gold, new Vector2(sz * 1.35f, sz * 1.35f), 6);
        if (kind == MonsterKind.Robot) Combat.MakeSr("Visor", transform, ThemeGfx.Pixel, new Color(1f, .3f, .3f), new Vector2(sz * .6f, sz * .12f), 7, new Vector2(0, sz * .1f));
        Vector2 barPos = new Vector2(0, sz * .5f + .25f);
        Combat.MakeSr("BarBg", transform, ThemeGfx.Pixel, new Color(0, 0, 0, .65f), new Vector2(1f, .13f), 20, barPos);
        barFill = Combat.MakeSr("BarFill", transform, ThemeGfx.Pixel, new Color(.45f, .9f, .5f), new Vector2(1f, .08f), 21, barPos);
        barScaleX = barFill.transform.localScale.x;
    }

    float Mul => (Combat.Now < freezeUntil || Combat.Now < stunUntil) ? 0f : Combat.Now < slowUntil ? .5f : 1f;

    // ------------------------------------------------------------------ nhận sát thương
    public void Damage(float d, Element e)
    {
        if (Dead || d <= 0f) return;
        hp -= d; flashUntil = Combat.Now + .08f;
        FloatText.Spawn(transform.position + Vector3.up * (radius + .45f), Mathf.RoundToInt(d).ToString(), e == Element.None ? Color.white : Combat.ElementColor(e));
        if (hp > 0f) return;
        if (kind == MonsterKind.Robot && phase == 0) Unmount(); else Die();
    }

    void Unmount()
    {
        phase = 1; maxHp = hp = 20f * (elite ? 4f : 1f) * hpMul; speed = 2.6f * speedMul; radius *= .7f; atkT = .5f; burst = 0; state = 0;
        body.transform.localScale *= .7f; baseColor = new Color(.95f, .9f, .75f);
        stunUntil = freezeUntil = 0f; auraEl = Element.None;
        FloatText.Spawn(transform.position + Vector3.up * 1f, "Quái lộ diện", GameTheme.EmberHi);
        CombatFx.Pulse(transform.position, 1.6f, new Color(.8f, .85f, 1f));
    }

    void Die()
    {
        if (Dead) return;
        Dead = true; Combat.Monsters.Remove(this); Killed?.Invoke(this);
        Destroy(gameObject);
    }

    // ------------------------------------------------------------------ vòng lặp
    void Update()
    {
        if (Dead) return;
        float dt = Combat.Dt; if (dt <= 0f) return;
        step = dt;

        if (Combat.Now < burnUntil) { burnAcc += dt; if (burnAcc >= 1f) { burnAcc -= 1f; Damage(4f, Element.Hoa); if (Dead) return; } }
        UpdateVisual();

        if (spawnT > 0f) { spawnT -= dt; return; }
        if (Mul <= 0f) return;
        var pl = Combat.Player; if (pl == null || pl.Dead) return;

        Vector2 pos = transform.position, to = (Vector2)pl.transform.position - pos;
        float dist = to.magnitude; Vector2 dir = dist > .001f ? to / dist : Vector2.right;
        atkT -= dt; float sp = speed * Mul;

        switch (kind)
        {
            case MonsterKind.Gunner:
                strafeT -= dt; if (strafeT <= 0f) { strafeT = Random.Range(1.2f, 2.4f); strafeSign = Random.value < .5f ? -1f : 1f; }
                if (dist < 5f) Move(-dir * sp); else if (dist > 8f) Move(dir * sp); else Move(new Vector2(-dir.y, dir.x) * strafeSign * sp * .6f);
                if (atkT <= 0f) { atkT = 1.7f / rate; Shoot(pos, dir, 7f, 8f * dmgMul, Element.None); }
                break;

            case MonsterKind.Charger:
                if (state == 0)
                {
                    if (dist > 1.5f) Move(dir * sp);
                    if (atkT <= 0f && dist < 6f) { state = 1; stateT = .55f / Mathf.Sqrt(rate); dashDir = dir; }
                }
                else if (state == 1) { stateT -= dt; if (stateT <= 0f) { state = 2; stateT = .35f; dashHit = false; } }   // gồng 0,55 giây rồi mới lao
                else
                {
                    Move(dashDir * 12f * Mathf.Lerp(1f, speedMul, .6f) * Mul);
                    if (!dashHit && dist < radius + pl.radius + .2f) { dashHit = true; pl.Hurt(14f * dmgMul, Element.None); }
                    stateT -= dt; if (stateT <= 0f) { state = 0; atkT = 1.8f / rate; }
                }
                break;

            case MonsterKind.Mage:
                if (state == 1) { stateT -= dt; if (stateT <= 0f) { state = 0; atkT = 3f / rate; } break; }   // đứng yên khi đang niệm
                if (dist < 6f) Move(-dir * sp); else if (dist > 9f) Move(dir * sp);
                if (atkT <= 0f) { float warn = 1f / Mathf.Sqrt(rate); state = 1; stateT = warn; CombatTelegraph.Spawn(pl.transform.position, 1.8f, warn, 16f * dmgMul, Element.Hoa); }
                break;

            case MonsterKind.Robot:
                if (phase == 0)
                {
                    if (dist > 5f) Move(dir * sp); else if (dist < 3.5f) Move(-dir * sp * .6f);
                    if (burst > 0) { burstT -= dt; if (burstT <= 0f) { burstT = .14f; burst--; Shoot(pos, dir, 10f, 6f * dmgMul, Element.Loi); } }
                    else if (atkT <= 0f) { burst = 3; burstT = 0f; atkT = 2.2f / rate; }
                }
                else
                {
                    Move(dir * sp * .9f);
                    if (dist < radius + pl.radius + .1f && atkT <= 0f) { pl.Hurt(8f * dmgMul, Element.None); atkT = 1f / rate; }
                }
                break;

            case MonsterKind.Summoner:
                if (dist < 6f) Move(-dir * sp);
                if (atkT <= 0f)
                {
                    atkT = 6f / rate;
                    int alive = 0; foreach (var m in Combat.Monsters) if (m != null && !m.Dead && m.kind == MonsterKind.Minion) alive++;
                    int perCast = 2 + ((int)RunState.difficulty >= 2 ? 1 : 0), maxMinions = 4 + (int)RunState.difficulty;   // khó hơn: triệu hồi nhiều đệ tử hơn
                    for (int i = 0; i < perCast && alive < maxMinions; i++, alive++) Create(MonsterKind.Minion, RoomManager.Clamp(pos + Random.insideUnitCircle * 1.3f), false);
                    CombatFx.Pulse(pos, 1.4f, baseColor);
                }
                break;

            default:   // Minion
                Move(dir * sp);
                if (dist < radius + pl.radius && atkT <= 0f) { pl.Hurt(5f * dmgMul, Element.None); atkT = .8f / rate; }
                break;
        }
    }

    void Move(Vector2 v) { transform.position = RoomManager.Clamp((Vector2)transform.position + v * step); }

    void Shoot(Vector2 from, Vector2 dir, float spd, float dmg, Element el)
    {
        CombatBolt.Fire(from + dir * (radius + .1f), dir, spd * projMul, dmg, .22f, el == Element.None ? new Color(1f, .75f, .4f) : Combat.ElementColor(el), false, 3f, el);
    }

    void UpdateVisual()
    {
        Color c = baseColor;
        if (spawnT > 0f) c.a = .35f + .25f * Mathf.Sin(Combat.Now * 25f);
        else if (Combat.Now < freezeUntil) c = Color.Lerp(c, Combat.ElementColor(Element.Bang), .7f);
        else if (Combat.Now < burnUntil) c = Color.Lerp(c, Combat.ElementColor(Element.Hoa), .4f);
        else if (Combat.Now < wetUntil) c = Color.Lerp(c, Combat.ElementColor(Element.Thuy), .4f);
        else if (state == 1 && kind == MonsterKind.Charger) c = Color.Lerp(c, Color.white, .5f + .5f * Mathf.Sin(Combat.Now * 30f));   // đang gồng
        if (Combat.Now < flashUntil) c = Color.white;
        body.color = c;

        float r = Mathf.Clamp01(hp / Mathf.Max(1f, maxHp));
        var t = barFill.transform; t.localScale = new Vector3(barScaleX * r, t.localScale.y, 1f);
        t.localPosition = new Vector3(-(1f - r) * .5f, t.localPosition.y, 0f);
    }

    void OnDestroy() { Combat.Monsters.Remove(this); }
}
