using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rồng lửa. Chiêu cơ bản: quạt lửa, vòng lửa, mưa thiên thạch, lao tới.
/// Pha 2 (máu ≤ 70%): thêm laze từ miệng + sóng chấn từ cánh.
/// Pha 3 (máu ≤ 50%): tốc độ ra chiêu và phạm vi chiêu tăng dần theo máu đã mất.
/// Pha 4 (máu ≤ 25%): nổi giận - lưới laze toàn màn hình + gọi đệ tử, rồi từ đó TẤT CẢ các chiêu
///   (kể cả lưới laze và gọi đệ) được tung ra liên tục, gần như không nghỉ.
/// Trả lời sai: rồng hồi lại phần máu đã mất của câu đó và gây sát thương mạnh hơn (DamageMult).
/// </summary>
public class DragonBoss : MonoBehaviour
{
    [Header("Ngưỡng máu của các pha (0-1)")]
    public float phase2At = .70f, phase3At = .50f, enrageAt = .25f;
    [Tooltip("Thời gian lưới laze toàn màn hình (giây)")] public float gridSeconds = 5f;
    public int minionCount = 4;
    [Header("Phạt khi trả lời sai")]
    [Tooltip("Mỗi lần sai, sát thương của rồng tăng thêm bấy nhiêu (0.2 = +20%)")] public float damageUpPerWrong = .2f;
    [Tooltip("Hệ số sát thương tối đa của rồng")] public float maxDamageMult = 2.5f;
    [Header("Pha cuối: tần suất lưới laze / gọi đệ (giây)")]
    public float gridEvery = 11f, summonEvery = 13f;

    public float Hp, MaxHp; public bool Dead, Invulnerable;
    /// <summary>Hệ số sát thương của rồng, tăng mỗi khi người chơi trả lời sai.</summary>
    public float DamageMult = 1f;
    public bool Frozen => frozenT > 0f;
    public event System.Action Damaged;
    public Vector2 Position => transform.position;

    BossFightManager mgr; PlayerKnight pl; SpriteRenderer sr, shadowSr, aura, mouthGlow; Transform vis; SpriteRenderer[] tail, tailOut;
    float t, flashT, mouthT, flapT, immuneMsgT; bool hover = true, enraged, toast2, toast3; GameObject telegraph; int lastAtk = -1;
    float burnT, burnDmg, burnTick, chillT, frozenT, freezeCd, lastGridT, lastSummonT;   // trạng thái từ Kiếm Lửa / Kiếm Băng; mốc thời gian chiêu pha cuối
    static readonly int[] WingSeq = { 0, 1, 2, 1 };

    float Hp01 => Mathf.Clamp01(Hp / Mathf.Max(1f, MaxHp));
    /// <summary>0 khi máu ≥ 50%, tăng dần tới 1 khi máu về 0.</summary>
    float P3 => Mathf.Clamp01((phase3At - Hp01) / phase3At);
    /// <summary>Hệ số tốc độ ra chiêu (1 → 1.6, thêm 20% khi nổi giận, giảm 30% khi bị Kiếm Băng làm chậm).</summary>
    float Haste => (1f + .6f * P3) * (enraged ? 1.2f : 1f) * (chillT > 0f ? .7f : 1f);   // chillT: Kiếm Băng làm rồng ra đòn chậm 30%
    /// <summary>Hệ số phạm vi chiêu: số đạn, độ rộng laze/sóng, bán kính nổ (1 → 1.5, thêm 10% khi nổi giận).</summary>
    float Reach => (1f + .5f * P3) * (enraged ? 1.1f : 1f);
    WaitForSeconds W(float s) => new WaitForSeconds(s / Haste);

    public static DragonBoss Create(BossFightManager m, PlayerKnight p, Vector2 pos, float maxHp)
    {
        var go = new GameObject("Dragon"); go.transform.position = pos;
        var b = go.AddComponent<DragonBoss>(); b.mgr = m; b.pl = p; b.Hp = b.MaxHp = maxHp;
        var sh = new GameObject("Shadow"); sh.transform.SetParent(go.transform, false); sh.transform.localPosition = new Vector3(0, -3.25f, 0);
        sh.transform.localScale = new Vector3(1.12f, .25f, 1f);
        b.shadowSr = sh.AddComponent<SpriteRenderer>(); b.shadowSr.sprite = ProcSprites.Circle; b.shadowSr.color = new Color(0, 0, 0, .35f); b.shadowSr.sortingOrder = -900;
        var v = new GameObject("Visual"); v.transform.SetParent(go.transform, false); b.vis = v.transform;
        b.sr = v.AddComponent<SpriteRenderer>(); b.sr.sprite = ProcSprites.DragonFrame(0, false, false);
        b.tail = new SpriteRenderer[9]; b.tailOut = new SpriteRenderer[9];   // đuôi: 9 đốt tròn lắc lư mỗi frame
        for (int i = 0; i < 9; i++)
        {
            float sc = (10f - i * .8f) / 32f;
            var o = new GameObject("TailO" + i); o.transform.SetParent(v.transform, false); o.transform.localScale = Vector3.one * (sc + .032f);
            b.tailOut[i] = o.AddComponent<SpriteRenderer>(); b.tailOut[i].sprite = ProcSprites.Circle; b.tailOut[i].color = new Color(.08f, .06f, .12f);
            var tg = new GameObject("Tail" + i); tg.transform.SetParent(v.transform, false); tg.transform.localScale = Vector3.one * sc;
            b.tail[i] = tg.AddComponent<SpriteRenderer>(); b.tail[i].sprite = ProcSprites.Circle;
        }
        b.aura = Gfx.Glow(go.transform, new Vector2(0, .5f), 12f, new Color(1f, .3f, .12f, .11f), -897);        // quầng sáng đỏ quanh rồng
        b.mouthGlow = Gfx.Glow(go.transform, new Vector2(0, 1.1f), 4f, new Color(1f, .6f, .2f, 0f), 4700);       // lửa trong miệng khi ra chiêu
        return b;
    }

    /// <summary>Nhân sát thương rồng gây ra cho người chơi với hệ số hiện tại (tăng khi trả lời sai).</summary>
    public static int Scale(int baseDamage)
    {
        var m = BossFightManager.I; float k = (m != null && m.Boss != null) ? m.Boss.DamageMult : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * k));
    }

    public void Begin() { StopAllCoroutines(); StartCoroutine(Brain(2f)); }

    public void ResetAfterQuestion()
    {
        StopAllCoroutines(); if (telegraph != null) Destroy(telegraph);
        burnT = chillT = frozenT = 0f; mouthT = flapT = 0f;
        Invulnerable = false; hover = true; if (!Dead) StartCoroutine(Brain(1.5f));
    }

    /// <summary>Người chơi trả lời sai: rồng hồi lại phần máu đã mất cho câu này và gây sát thương mạnh hơn.</summary>
    public void PunishWrong(float healAmount)
    {
        if (Dead) return;
        float before = Hp; Hp = Mathf.Min(MaxHp, Hp + healAmount); float gained = Hp - before;
        DamageMult = Mathf.Min(maxDamageMult, DamageMult + damageUpPerWrong);
        Fx.Ring(Position, 6f, new Color(.5f, 1f, .55f), .5f); Fx.Burst(Position, 26, Fx.HealCols, 2f, 9f); Sfx.Tone(330, .5f, 2, .25f, 400);
        mgr.ui.Float(Position + new Vector2(0f, 2.9f), "+" + Mathf.RoundToInt(gained) + " MÁU", new Color(.5f, 1f, .55f), true);
        mgr.ui.SetRage(DamageMult);
        mgr.ui.ShowToast("RỒNG HỒI MÁU!", "Sát thương rồng +" + Mathf.RoundToInt(damageUpPerWrong * 100f) + "%  (tổng ×" + DamageMult.ToString("0.0") + ")", new Color(1f, .45f, .35f));
    }

    // ---------- Trạng thái từ buff ----------
    /// <summary>Kiếm Lửa: đốt dur giây, mỗi 0.5 giây mất perTick máu. Chém tiếp chỉ làm mới thời gian, không cộng dồn.</summary>
    public void Ignite(float dur, float perTick) { if (Dead) return; if (burnT <= 0f) burnTick = .5f; burnT = dur; burnDmg = perTick; }

    /// <summary>Kiếm Băng: làm chậm tốc độ ra đòn của rồng trong dur giây.</summary>
    public void Chill(float dur) { if (Dead) return; chillT = Mathf.Max(chillT, dur); }

    /// <summary>Kiếm Băng: đóng băng rồng - huỷ chiêu đang ra, đứng yên s giây (có thời gian hồi để không bị khoá cứng). Miễn nhiễm khi đang nổi giận lần đầu.</summary>
    public void Freeze(float s)
    {
        if (Dead || Invulnerable || freezeCd > 0f || frozenT > 0f) return;
        freezeCd = s + 4f; frozenT = s; hover = false; mouthT = 0f; flapT = 0f;
        StopAllCoroutines(); if (telegraph != null) Destroy(telegraph); Hazards.ClearAll();
        Fx.Ring(Position, 6f, new Color(.55f, .9f, 1f), .45f); Fx.Burst(Position, 22, Fx.IceCols, 2f, 8f); Sfx.Tone(900, .3f, 2, .2f, -300);
        mgr.ui.Float(Position + new Vector2(0f, 3f), "ĐÓNG BĂNG!", new Color(.6f, .92f, 1f), true);
        StartCoroutine(FreezeRoutine());
    }

    IEnumerator FreezeRoutine()
    {
        while (frozenT > 0f) yield return null;
        Fx.Burst(Position, 16, Fx.IceCols, 2f, 8f); Sfx.Tone(500, .15f, 0, .15f, 200);
        if (!Dead) StartCoroutine(Brain(.4f));
    }

    void UpdateStatus(float dt)
    {
        freezeCd -= dt;
        if (burnT > 0f)
        {
            burnT -= dt; burnTick -= dt;
            if (Random.value < dt * 25f) Fx.Spawn(ProcSprites.Pixel, Position + new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(-1.8f, 1.2f)), Random.value < .5f ? new Color(1f, .55f, .15f) : new Color(1f, .85f, .3f), .5f, 1.6f, .3f, new Vector2(Random.Range(-.5f, .5f), Random.Range(1.5f, 3f)));
            if (burnTick <= 0f) { burnTick = .5f; TakeDamage(burnDmg, false, true); if (Dead || !mgr.Playing) return; }
        }
        if (chillT > 0f)
        {
            chillT -= dt;
            if (Random.value < dt * 12f) Fx.Spawn(ProcSprites.Pixel, Position + new Vector2(Random.Range(-1.8f, 1.8f), Random.Range(-1.5f, 1.5f)), new Color(.7f, .95f, 1f), .6f, 1.4f, .3f, new Vector2(0f, -.8f), 3f);
        }
        if (frozenT > 0f) frozenT -= dt;
    }

    void Update()
    {
        float dt = Time.deltaTime; t += dt; flashT -= dt; mouthT -= dt; flapT -= dt; immuneMsgT -= dt;
        if (!Dead && mgr.Playing) UpdateStatus(dt);
        if (hover && !Dead)
        {
            Vector3 p = transform.position;
            float tx = Mathf.Sin(t * .7f) * 6.9f, ty = 3.44f + Mathf.Sin(t * 1.3f) * .75f;
            p.x += (tx - p.x) * dt * .9f; p.y += (ty - p.y) * dt * 3f; transform.position = p;
        }
        float wingRate = frozenT > 0f ? 0f : (flapT > 0f ? 22f : (hover ? 8f : 12f));
        int w = WingSeq[(int)(t * wingRate) % 4];
        sr.sprite = ProcSprites.DragonFrame(w, mouthT > 0f, flashT > 0f);
        Vector3 bob = frozenT > 0f ? Vector3.zero : new Vector3(0, Mathf.Sin(t * 3f) * .19f, 0);
        if (flapT > 0f) bob.y += .35f * Mathf.Sin(t * 22f);
        if (Dead) bob += (Vector3)(Random.insideUnitCircle * .2f);
        vis.localPosition = bob;
        sr.sortingOrder = -Mathf.RoundToInt((transform.position.y - 3.25f) * 20f);
        vis.localScale = Vector3.one * (1f + Mathf.Max(0f, flashT) * 1.2f);   // nảy nhẹ khi trúng đòn
        for (int i = 0; i < 9; i++)
        {
            float tx_ = Mathf.Sin(t * 3f + i * .7f) * i * 2.6f / 16f * (flapT > 0f ? 1.6f : 1f), ty_ = -(24f + i * 5.5f) / 16f; var tp = new Vector3(tx_, ty_, 0f);
            tail[i].transform.localPosition = tp; tailOut[i].transform.localPosition = tp; tail[i].sortingOrder = sr.sortingOrder - 2; tailOut[i].sortingOrder = sr.sortingOrder - 3;
        }
        aura.color = new Color(1f, enraged ? .15f : .3f, .12f, (enraged ? .22f : .11f) + .03f * Mathf.Sin(t * 3f));
        mouthGlow.color = new Color(1f, .6f, .2f, mouthT > 0f ? .55f : 0f); mouthGlow.transform.position = Mouth;
        // màu: nổi giận = đỏ rực nhấp nháy, miễn nhiễm = ánh xanh
        if (flashT > 0f) sr.color = Color.white;
        else if (frozenT > 0f) sr.color = Color.Lerp(new Color(.55f, .85f, 1f), Color.white, .2f + .15f * Mathf.Sin(t * 10f));
        else if (Invulnerable) sr.color = Color.Lerp(new Color(1f, .5f, .45f), new Color(1f, .9f, .6f), .5f + .5f * Mathf.Sin(t * 14f));
        else if (enraged) sr.color = Color.Lerp(Color.white, new Color(1f, .5f, .45f), .55f + .25f * Mathf.Sin(t * 8f));
        else if (chillT > 0f) sr.color = new Color(.75f, .9f, 1f);
        else if (burnT > 0f) sr.color = new Color(1f, .75f, .6f);
        else sr.color = Color.white;
        Color tint = flashT > 0f ? Color.white : sr.color;
        for (int i = 0; i < 9; i++) { var bc = i % 2 == 1 ? new Color(.65f, .165f, .2f) : new Color(.7f, .15f, .18f); tail[i].color = flashT > 0f ? Color.white : bc * tint; }
    }

    // ---------- Sát thương ----------
    /// <param name="dot">true = sát thương theo thời gian (đốt): không rung màn hình / không khựng hình, bị bỏ qua khi rồng miễn nhiễm.</param>
    public void TakeDamage(float d, bool crit, bool dot = false)
    {
        if (Dead) return;
        if (Invulnerable)
        {
            if (dot) return;
            if (immuneMsgT <= 0f) { immuneMsgT = .6f; mgr.ui.Float(Position + new Vector2(0, 2.6f), "MIỄN NHIỄM", new Color(.6f, .9f, 1f), false); Sfx.Tone(700, .08f, 2, .15f); }
            Fx.Burst(Position, 4, Fx.BoltCols); return;
        }
        Hp -= d;
        if (dot)
        {
            mgr.ui.Float(Position + new Vector2(Random.Range(-.9f, .9f), 2.4f), Mathf.RoundToInt(d).ToString(), new Color(1f, .55f, .2f), false);
            Damaged?.Invoke(); return;
        }
        flashT = .1f; CameraRig.Shake(crit ? 5f : 2.5f); mgr.HitStop(crit ? .07f : .035f);
        Vector2 rnd = new Vector2(Random.Range(-.9f, .9f), Random.Range(-.6f, .6f));
        mgr.ui.Float(Position + new Vector2(rnd.x, 2.4f), Mathf.RoundToInt(d).ToString(), crit ? new Color(1f, .82f, .4f) : Color.white, crit);
        Fx.Burst(Position + rnd, crit ? 14 : 8, crit ? Fx.FireCols : Fx.HitCols); Fx.Sparks(Position + rnd, crit ? 10 : 5, crit ? new Color(1f, .85f, .4f) : Color.white);
        Sfx.Tone(crit ? 170 : 120, .1f, 0, .25f, -60);
        Damaged?.Invoke();
    }

    public void Lightning(float dmg)
    {
        if (Invulnerable) return;
        Fx.Lightning(Position); Fx.Boom(Position, .8f, Fx.BoltCols); TakeDamage(dmg, false);
    }

    // ---------- AI ----------
    IEnumerator Brain(float firstDelay)
    {
        hover = true; yield return new WaitForSeconds(firstDelay);
        while (!Dead)
        {
            if (!enraged && Hp01 <= enrageAt) yield return Enrage();
            PhaseToasts();
            hover = false; mouthT = .5f;
            switch (PickAttack())
            {
                case 0: yield return Fan(); break;
                case 1: yield return RingAttack(); break;
                case 2: yield return Rain(); break;
                case 3: yield return Charge(); break;
                case 4: yield return MouthLaser(); break;
                case 5: yield return WingQuake(); break;
                case 6: yield return GridAttack(); break;     // chỉ ở pha cuối
                default: yield return SummonAttack(); break;  // chỉ ở pha cuối
            }
            // Pha cuối: gần như không nghỉ giữa các chiêu -> tần suất liên tục
            hover = true; yield return W(enraged ? .2f : Mathf.Max(.7f, 1.8f - mgr.Level));
        }
    }

    /// <summary>Chọn chiêu theo pha, không lặp lại chiêu vừa dùng. Pha cuối: dùng cả 8 chiêu, lưới laze / gọi đệ quay lại đều đặn.</summary>
    int PickAttack()
    {
        if (enraged)   // lưới laze và gọi đệ có lịch riêng để chắc chắn lặp lại
        {
            if (t - lastGridT >= gridEvery && lastAtk != 6) return lastAtk = 6;
            if (t - lastSummonT >= summonEvery && Minion.All.Count < minionCount && lastAtk != 7) return lastAtk = 7;
        }
        var pool = new List<int> { 0, 1, 2, 3 };
        if (Hp01 <= phase2At || enraged) { pool.Add(4); pool.Add(5); pool.Add(4); pool.Add(5); }   // chiêu mới được ưu tiên
        if (enraged) { pool.Add(6); if (Minion.All.Count < minionCount) pool.Add(7); }
        pool.RemoveAll(x => x == lastAtk);
        lastAtk = pool[Random.Range(0, pool.Count)]; return lastAtk;
    }

    void PhaseToasts()
    {
        if (!toast2 && Hp01 <= phase2At) { toast2 = true; mgr.ui.ShowToast("RỒNG THỨC TỈNH!", "Laze từ miệng + sóng chấn từ cánh", new Color(1f, .6f, .3f)); mgr.ui.FlashRed(); Sfx.Tone(90, .6f, 1, .3f, -30); }
        if (!toast3 && Hp01 <= phase3At) { toast3 = true; mgr.ui.ShowToast("RỒNG CUỒNG NỘ!", "Ra chiêu nhanh và rộng dần", new Color(1f, .45f, .35f)); mgr.ui.FlashRed(); Sfx.Tone(80, .6f, 1, .3f, -30); }
    }

    Vector2 Mouth => Position + Vector2.up * 1.1f;
    void Shoot(float rad, float speed) => Projectile.Spawn(Mouth, new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed, pl);
    float Aim() { Vector2 d = pl.Center - Mouth; return Mathf.Atan2(d.y, d.x); }

    IEnumerator Fan()   // quạt đạn nhắm vào người chơi, 3 đợt
    {
        float lv = mgr.Level, rc = Reach; yield return W(.5f);
        for (int n = 0; n < 3; n++)
        {
            mouthT = .4f; int k = 3 + Mathf.RoundToInt(lv * 4f + (rc - 1f) * 5f); float aim = Aim();
            for (int i = 0; i < k; i++) Shoot(aim + (i - (k - 1) / 2f) * .22f, (95f + lv * 40f) / 16f * (1f + (Haste - 1f) * .5f));
            Sfx.Tone(220, .2f, 1, .2f, -120);
            if (n < 2) yield return W(.45f);
        }
        yield return W(.6f);
    }

    IEnumerator RingAttack()   // vòng lửa toả ra mọi hướng
    {
        float lv = mgr.Level, rc = Reach; int vol = 2 + (lv > .5f ? 1 : 0) + (Haste > 1.3f ? 1 : 0); int cnt = 18 + Mathf.RoundToInt((rc - 1f) * 16f);
        yield return W(.5f);
        for (int n = 1; n <= vol; n++)
        {
            mouthT = .4f;
            for (int i = 0; i < cnt; i++) Shoot(i / (float)cnt * 6.2832f + n * .17f, (70f + lv * 30f) / 16f * (1f + (Haste - 1f) * .5f));
            Sfx.Tone(150, .3f, 1, .2f, -80);
            yield return W(.6f);
        }
        yield return W(.3f);
    }

    IEnumerator Rain()   // mưa thiên thạch: vòng đỏ cảnh báo rồi nổ
    {
        float lv = mgr.Level, rc = Reach, hs = Mathf.Sqrt(Haste); int k = 4 + Mathf.RoundToInt(lv * 5f + (rc - 1f) * 6f);
        for (int i = 0; i < k; i++)
        {
            Vector2 p = pl.Position;
            if (i > 0) p += new Vector2(Random.Range(-6.25f, 6.25f), Random.Range(-3.75f, 3.1f));
            p.x = Mathf.Clamp(p.x, -13f, 13f); p.y = Mathf.Clamp(p.y, -7.8f, 5f);
            Meteor.Spawn(p, (.9f + i * .14f) / hs, pl, 1.6f * rc);
        }
        yield return W(1.8f);
    }

    IEnumerator Charge()   // vệt đỏ báo trước rồi lao thẳng vào người chơi
    {
        float hs = Mathf.Sqrt(Haste), hit = 2.4f * (1f + (Reach - 1f) * .5f);
        Vector2 d = (pl.Center - Position).normalized; Sfx.Tone(130, .6f, 1, .2f, 60);
        telegraph = new GameObject("ChargeTelegraph"); var tsr = telegraph.AddComponent<SpriteRenderer>();
        tsr.sprite = ProcSprites.Pixel; tsr.sortingOrder = -850; telegraph.transform.localScale = new Vector3(80f, 6f * (1f + (Reach - 1f) * .5f), 1f);
        telegraph.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        for (float e = 0f; e < Mathf.Max(.5f, .8f / hs); e += Time.deltaTime)
        {
            telegraph.transform.position = Position + d * 10f; mouthT = .1f;
            tsr.color = new Color(.9f, .28f, .3f, .2f + .2f * Mathf.Sin(Time.time * 25f)); yield return null;
        }
        Destroy(telegraph);
        for (float e = 0f; e < .5f; e += Time.deltaTime)
        {
            Vector3 p = transform.position + (Vector3)(d * 18.75f * hs * Time.deltaTime);
            p.x = Mathf.Clamp(p.x, -12.5f, 12.5f); p.y = Mathf.Clamp(p.y, -6.25f, 5f); transform.position = p; mouthT = .1f;
            Fx.Spawn(ProcSprites.Pixel, p, new Color(1f, .48f, .23f), .4f, 4f, .5f, Random.insideUnitCircle);
            if (Vector2.Distance(p, pl.Center) < hit) pl.Hurt(Scale(18));
            yield return null;
        }
        yield return W(.3f);
    }

    // ---------- Pha 2: laze từ miệng ----------
    IEnumerator MouthLaser()
    {
        int shots = 1 + (Reach > 1.25f ? 1 : 0); float width = 1.1f * Reach;
        for (int s = 0; s < shots; s++)
        {
            float charge = Mathf.Max(.55f, 1.05f / Haste), ang = Aim();
            var beam = LaserBeam.Spawn(pl, Mouth, Mouth + Dir(ang) * 40f, .22f, new Color(1f, .3f, .3f, .55f), false);
            Sfx.Tone(300, charge, 2, .2f, 500);
            for (float e = 0f; e < charge; e += Time.deltaTime)   // gom năng lượng: tia cảnh báo bám theo, 35% cuối thì khoá hướng
            {
                mouthT = .1f; if (e < charge * .65f) ang = Aim();
                beam.Set(Mouth, Mouth + Dir(ang) * 40f, .18f + .12f * Mathf.Sin(e * 30f), new Color(1f, .3f, .3f, .55f), false);
                Fx.Spawn(ProcSprites.Glow, Mouth, new Color(1f, .5f, .3f, .7f), .15f, 1.8f * (e / charge) + .3f, .2f, Vector2.zero, order: 4800);
                yield return null;
            }
            Sfx.Tone(160, .7f, 1, .35f, -80); CameraRig.Shake(5f); mgr.ui.FlashRed();
            float dur = .75f, sweep = (Random.value < .5f ? -1f : 1f) * .35f * Reach;   // tia quét ngang qua người chơi
            for (float e = 0f; e < dur; e += Time.deltaTime)
            {
                mouthT = .1f; float a = ang + sweep * (e / dur - .5f);
                beam.Set(Mouth, Mouth + Dir(a) * 40f, width * (e < .08f ? e / .08f : 1f), new Color(1f, .22f, .22f, .75f), true);
                yield return null;
            }
            Destroy(beam.gameObject);
            if (s < shots - 1) yield return W(.35f);
        }
        yield return W(.5f);
    }

    static Vector2 Dir(float rad) => new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

    // ---------- Pha 2: sóng chấn từ cánh ----------
    IEnumerator WingQuake()
    {
        int n = 2 + (Reach > 1.2f ? 1 : 0) + (Reach > 1.4f ? 1 : 0); float halfW = 3.2f * Reach;
        for (int i = 0; i < n; i++)
        {
            Vector2 o = Position + Vector2.down * 2.4f, d = (pl.Center - o).normalized;
            flapT = .7f; mouthT = .3f; Sfx.Tone(200, .35f, 4, .2f, -100);
            var warn = LaserBeam.Spawn(pl, o, o + d * 40f, halfW * 2f, new Color(1f, .7f, .3f, .14f), false);
            for (float e = 0f; e < Mathf.Max(.4f, .6f / Haste); e += Time.deltaTime) { flapT = .2f; yield return null; }
            Destroy(warn.gameObject);
            Sfx.Tone(80, .5f, 1, .35f, -30); CameraRig.Shake(6f * Reach);
            Fx.Ring(o, 3.5f, new Color(1f, .75f, .4f), .35f);
            Shockwave.Spawn(pl, o, d, 9f * Mathf.Sqrt(Haste), halfW, 1.2f * Reach, 14);
            yield return W(.55f);
        }
        yield return W(.8f);
    }

    // ---------- Pha 4: nổi giận ----------
    /// <summary>Chạy 1 lần duy nhất khi rồng xuống ≤ enrageAt máu: gầm thét, lưới laze đầu tiên rồi gọi đệ. Sau đó Brain tung mọi chiêu liên tục.</summary>
    IEnumerator Enrage()
    {
        enraged = true; Invulnerable = true; hover = false;
        Projectile.ClearAll(); Meteor.ClearAll(); Hazards.ClearAll();
        mgr.ui.FlashRed(); mgr.ui.ShowToast("RỒNG NỔI GIẬN!", "Mọi chiêu thức tung ra liên tục - né lưới laze!", new Color(1f, .35f, .3f));
        Sfx.Tone(60, 1.2f, 1, .4f, -20); CameraRig.Shake(10f);

        yield return FlyToCenter(.8f);
        yield return LaserGrid(gridSeconds, 1.4f);
        yield return Summon(true);
        lastGridT = lastSummonT = t;
        Invulnerable = false;
    }

    /// <summary>Bay về giữa màn hình, gầm thét.</summary>
    IEnumerator FlyToCenter(float dur)
    {
        Vector3 startPos = transform.position, endPos = new Vector3(0f, 3.9f, 0f);
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(startPos, endPos, e / dur); mouthT = .1f; flapT = .1f;
            if (Random.value < .4f) Fx.Burst(Position + Random.insideUnitCircle * 2f, 3, Fx.FireCols);
            yield return null;
        }
    }

    /// <summary>Chiêu lưới laze lặp lại ở pha cuối (ngắn hơn lần đầu một chút, rồng không miễn nhiễm).</summary>
    IEnumerator GridAttack()
    {
        lastGridT = t; mgr.ui.Float(Position + new Vector2(0f, 3f), "LƯỚI LAZE!", new Color(1f, .4f, .35f), true);
        yield return FlyToCenter(.5f);
        yield return LaserGrid(gridSeconds * .8f, 1.1f);
    }

    /// <summary>Chiêu gọi đệ lặp lại ở pha cuối: chỉ gọi bù cho đủ minionCount.</summary>
    IEnumerator SummonAttack() { yield return Summon(false); }

    /// <summary>Lưới laze: 5 tia dọc + 3 tia ngang, trôi dần để người chơi phải di chuyển. warn = thời gian cảnh báo, active = thời gian tia hoạt động.</summary>
    IEnumerator LaserGrid(float active, float warnTime)
    {
        float[] vx = { -12f, -6f, 0f, 6f, 12f }, hy = { -4.5f, 0f, 4.5f };
        float[] vph = new float[vx.Length], hph = new float[hy.Length];
        var vb = new LaserBeam[vx.Length]; var hb = new LaserBeam[hy.Length];
        Color warn = new Color(1f, .3f, .3f, .5f), fire = new Color(1f, .2f, .25f, .8f);
        for (int i = 0; i < vx.Length; i++) { vph[i] = Random.value * 6.28f; vb[i] = LaserBeam.Spawn(pl, new Vector2(vx[i], -12f), new Vector2(vx[i], 12f), .2f, warn, false); }
        for (int i = 0; i < hy.Length; i++) { hph[i] = Random.value * 6.28f; hb[i] = LaserBeam.Spawn(pl, new Vector2(-22f, hy[i]), new Vector2(22f, hy[i]), .2f, warn, false); }
        Sfx.Tone(250, warnTime, 2, .25f, 400);
        for (float e = 0f; e < warnTime; e += Time.deltaTime)   // cảnh báo
        {
            mouthT = .1f; float wd = .14f + .08f * Mathf.Sin(e * 25f);
            for (int i = 0; i < vb.Length; i++) vb[i].Set(new Vector2(vx[i], -12f), new Vector2(vx[i], 12f), wd, warn, false);
            for (int i = 0; i < hb.Length; i++) hb[i].Set(new Vector2(-22f, hy[i]), new Vector2(22f, hy[i]), wd, warn, false);
            yield return null;
        }
        Sfx.Tone(170, .9f, 1, .4f, -70); CameraRig.Shake(8f); mgr.ui.FlashRed();
        for (float e = 0f; e < active; e += Time.deltaTime)   // lưới hoạt động đúng "active" giây
        {
            mouthT = .1f; float wd = .9f * (e < .15f ? e / .15f : 1f) * (e > active - .3f ? Mathf.Max(.05f, (active - e) / .3f) : 1f);
            for (int i = 0; i < vb.Length; i++)
            {
                float x = vx[i] + (Mathf.Sin(e * 1.2f + vph[i]) - Mathf.Sin(vph[i])) * 1.1f;   // lệch = 0 lúc bắt đầu nên khớp vệt cảnh báo
                vb[i].Set(new Vector2(x, -12f), new Vector2(x, 12f), Mathf.Max(.05f, wd), fire, wd > .3f);
            }
            for (int i = 0; i < hb.Length; i++)
            {
                float y = hy[i] + (Mathf.Sin(e * 1.0f + hph[i]) - Mathf.Sin(hph[i])) * .8f;
                hb[i].Set(new Vector2(-22f, y), new Vector2(22f, y), Mathf.Max(.05f, wd), fire, wd > .3f);
            }
            if (Random.value < .15f) CameraRig.Shake(2f);
            yield return null;
        }
        foreach (var b in vb) if (b != null) Destroy(b.gameObject);
        foreach (var b in hb) if (b != null) Destroy(b.gameObject);
        Fx.Boom(Position, 1.6f, Fx.FireCols);
    }

    /// <summary>Gọi đệ tử. first = lần đầu (toast to, chờ lâu hơn). Chỉ gọi thêm cho đủ minionCount.</summary>
    IEnumerator Summon(bool first)
    {
        lastSummonT = t;
        int need = minionCount - Minion.All.Count; if (need <= 0) yield break;
        if (first) mgr.ui.ShowToast("ĐỆ TỬ XUẤT HIỆN!", "Hạ gục đàn quỷ nhỏ!", new Color(.8f, .5f, 1f));
        else mgr.ui.Float(Position + new Vector2(0f, 3f), "GỌI ĐỆ!", new Color(.8f, .5f, 1f), true);
        Sfx.Tone(120, .6f, 1, .3f, 60);
        var sp = new List<Vector2> { new Vector2(-10f, -1f), new Vector2(10f, -1f), new Vector2(-6f, 4.5f), new Vector2(6f, 4.5f), new Vector2(0f, -6f), new Vector2(0f, 5f) };
        if (!first) for (int i = sp.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var tmp = sp[i]; sp[i] = sp[j]; sp[j] = tmp; }   // lần sau: vị trí ngẫu nhiên
        for (int i = 0; i < Mathf.Min(need, sp.Count); i++) { Minion.Spawn(sp[i], pl); yield return new WaitForSeconds(.15f); }
        yield return new WaitForSeconds(first ? .8f : .3f);
    }

    // ---------- Chết ----------
    public void Die()
    {
        if (Dead) return;
        Dead = true; StopAllCoroutines(); if (telegraph != null) Destroy(telegraph); hover = false; Invulnerable = false; sr.color = Color.white;
        Hazards.ClearAll(); Minion.ClearAll(true); StartCoroutine(DieRoutine());
    }

    IEnumerator DieRoutine()
    {
        Sfx.Tone(60, 1.5f, 1, .3f, -20); float e = 0f, nb = 0f;
        while (e < 2.4f)
        {
            e += Time.deltaTime; nb -= Time.deltaTime;
            if (nb <= 0f) { nb = .12f; Fx.Boom(Position + new Vector2(Random.Range(-2.1f, 2.1f), Random.Range(-1.9f, 1.9f)), .55f, Fx.FireCols); }
            yield return null;
        }
        Fx.Boom(Position, 2.8f, Fx.FireCols); Fx.Boom(Position + Vector2.up, 1.8f, Fx.BoltCols);
        sr.enabled = false; shadowSr.enabled = false; foreach (var q in tail) q.enabled = false; foreach (var q in tailOut) q.enabled = false; aura.enabled = false; mouthGlow.enabled = false; mgr.ui.FlashWhite();
        yield return new WaitForSeconds(1.2f);
        mgr.OnBossDefeated();
    }
}
