using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Thùng gỗ trượt ngang sân khi trọng lực đổi chiều (pha 3 của boss 3).</summary>
public class SlidingCrate : MonoBehaviour
{
    Vector2 v; float dmg, hitCd; SpriteRenderer sr;
    public static void Spawn(Vector2 pos, Vector2 vel, float dmg) { var go = new GameObject("Crate"); go.transform.position = pos; var c = go.AddComponent<SlidingCrate>(); c.v = vel; c.dmg = dmg; c.sr = Combat.MakePx("Spr", go.transform, RogueArt.Crate, 0, null, 1.4f); }
    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f) return; hitCd -= dt; transform.position += (Vector3)(v * dt); sr.sortingOrder = Combat.Order(transform.position.y); transform.Rotate(0, 0, 90f * dt);
        var pl = Combat.Player; if (pl != null && !pl.Dead && hitCd <= 0f && Vector2.Distance(pl.transform.position, transform.position) < .95f) { pl.Hurt(dmg, Element.Nham, transform.position); hitCd = .7f; RFx.Burst(transform.position, new Color(.7f, .5f, .3f), 6, 3f, .4f, .16f, 6f, false); }
        if (Mathf.Abs(transform.position.x) > 11f || Mathf.Abs(transform.position.y) > 7f) Destroy(gameObject);
    }
}

/// <summary>Boss 3: Nhà Khoa Học Điên (màn 3-5, Lab Vật Lí) — cơ chế PHẢN ĐÒN: chuyển hướng tia hủy diệt về phía hắn.</summary>
public class BossScientist : BossBase
{
    protected override string Subject => "ly";
    protected override BossPillars.Style PillarStyle => BossPillars.Style.Transformer;
    protected override float BaseHp => 1600f;
    static readonly int[][] table = { new[] { 0, 1, 1 }, new[] { 2, 1, 0 }, new[] { 4, 1, 2 }, new[] { 5, 6, 7, 5 } };   // 0 lưới laze ngang · 1 loạt đạn điện · 2 sàn điện · 4 lưới laze dọc · 5 tên lửa · 6 đấm · 7 dậm
    protected override int AttackKinds(int p) => table[Mathf.Clamp(p, 0, 3)].Length;
    static readonly Color volt = new Color(.4f, .9f, 1f);

    int form; bool transforming; float magnetT = 3f, gravT = 4f, droneT, coreT; int magnetDir = 1; Vector2 gravDir; readonly List<BossMinion> drones = new List<BossMinion>();
    SpriteRenderer armL, armR, core; Sprite mech; bool coreOpen; Sprite[] droneFrames; float crateT;
    protected override float DamageTakenMul() => form == 1 ? (coreOpen ? 2.2f : .35f) : 1f;
    public override bool Targetable => base.Targetable && !transforming;

    protected override void BuildVisual()
    {
        bossName = "Nhà Khoa Học Điên"; bossTitle = "Kẻ gieo màn sương"; index = 2; innate = Element.Loi; resist[(int)Element.Loi] = .5f; radius = .9f; bloodCol = volt; metal = true;
        idleFrames = RogueArt.Scientist; mech = RogueArt.MechBody; MakeBody(idleFrames[0], 1.2f, 2.2f); home = new Vector2(0f, 1.7f); droneFrames = RogueArt.MonsterFrames(2, 5);
    }

    protected override Sprite CurrentSprite() => form == 1 ? mech : idleFrames[pose != 0 ? 2 : (int)(animT / .6f) % 2];

    protected override void EnterPhase(int p)
    {
        Combat.FieldStrength = 0f; Combat.Gravity = Vector2.zero; magnetT = 2.5f; gravT = 3.5f; gravDir = Vector2.zero;
        if (p == 0 || p == 1) SpawnDrones();
        if (p == 3) { hp = Mathf.Min(maxHp, hp + maxHp * .08f); StartCoroutine(BecomeMech()); }
        RoomManager.Instance?.ShowBanner(p == 0 ? "Pha 1 · drone hộ vệ và lưới laze" : p == 1 ? "Pha 2 · sàn điện và TỪ TRƯỜNG hút/đẩy đạn của bạn" : p == 2 ? "Pha 3 · ĐẢO TRỌNG LỰC, thùng hàng trượt khắp sân" : "Pha 4 · ROBOT KHỔNG LỒ · bắn vào LÕI khi nó lộ ra", 3.5f);
    }

    protected override void OnTransitionStart() { Combat.FieldStrength = 0f; Combat.Gravity = Vector2.zero; gravDir = Vector2.zero; foreach (var d in drones.ToArray()) if (d != null) d.TakeHit(999f, Element.None, d.Pos, 0f, false, true); drones.Clear(); }

    void SpawnDrones()
    {
        for (int i = 0; i < 3; i++)
        {
            var d = BossMinion.Create(droneFrames, Pos, 24f * (1f + (RoomRules.HpMul(RunState.difficulty) - 1f) * .6f), .4f, 1.4f, new Color(1f, 1f, 1f)); d.follow = transform; d.orbitAng = i * 120f; d.orbitR = 2.4f; d.orbitSpeed = 55f; d.dmgMul = dm; d.shotEl = Element.Loi; d.shotCol = volt; d.metal = true; d.life = 9999f; drones.Add(d);
        }
    }

    protected override void Tick(float dt)
    {
        airY = form == 0 ? .5f + Mathf.Sin(Time.time * 2f) * .1f : 0f;
        if (bstate != BState.Fight || transforming) return;
        drones.RemoveAll(d => d == null || d.Dead);
        if ((phaseIdx == 0 || phaseIdx == 1) && drones.Count == 0) { droneT += dt; if (droneT > 7f) { droneT = 0f; SpawnDrones(); } }
        if (phaseIdx == 1)
        {   // từ trường: đổi hút/đẩy mỗi 4,5 giây
            magnetT -= dt;
            if (magnetT <= 0f)
            {
                magnetT = 4.5f; magnetDir = -magnetDir; Combat.FieldPos = transform.position; Combat.FieldStrength = magnetDir;
                RoomManager.Instance?.ShowBanner(magnetDir > 0 ? "TỪ TRƯỜNG: HÚT · đạn của bạn bị kéo vào boss" : "TỪ TRƯỜNG: ĐẨY · đạn của bạn bị đẩy ra", 2f); RogueAudio.Play(RSfx.Zap, .5f, magnetDir > 0 ? .8f : 1.3f);
            }
            if (Combat.FieldStrength != 0f && Random.value < dt * 20f) { float a = Random.Range(0f, 360f); Vector2 p = (Vector2)transform.position + Combat.FromAng(a) * (magnetDir > 0 ? 6f : 1.5f); RFx.Burst(p, magnetDir > 0 ? new Color(.4f, .6f, 1f) : new Color(1f, .4f, .4f), 1, magnetDir > 0 ? -4f : 4f, .6f, .16f); }
            Combat.FieldPos = transform.position;
        }
        if (phaseIdx == 2)
        {   // trọng lực đảo chiều: báo trước 1,2 giây
            gravT -= dt;
            if (gravT <= 0f) { gravT = 7f; StartCoroutine(FlipGravity()); }
            crateT -= dt; if (crateT <= 0f && gravDir.sqrMagnitude > 0f) { crateT = 2.2f; SpawnCrates(); }
        }
        if (form == 1)
        {   // lõi năng lượng lộ ra theo chu kỳ 12 giây
            coreT += dt; float c = coreT % 12f; bool open = c > 7.5f;
            if (open != coreOpen) { coreOpen = open; if (open) { RoomManager.Instance?.ShowBanner("LÕI NĂNG LƯỢNG LỘ DIỆN! Bắn vào ngực robot", 2.5f); RogueAudio.Play(RSfx.Shield, .7f, 1.5f); RFx.Ring(transform.position, 2.4f, new Color(1f, .8f, .3f), .4f); } else RogueAudio.Play(RSfx.ShieldBreak, .5f, 1.4f); }
            if (core != null) { core.color = new Color(1f, 1f, 1f, coreOpen ? .85f + .15f * Mathf.Sin(Time.time * 12f) : 0f); core.transform.localScale = Vector3.one * Combat.PxScale * (coreOpen ? 2.1f + .2f * Mathf.Sin(Time.time * 10f) : 1.6f); }
            AnimateArms();
        }
    }

    float punchAim = float.NaN, punchExt;
    void AnimateArms()
    {
        if (armL == null) return; float sw = Mathf.Sin(Time.time * 1.6f) * 5f;
        armL.sortingOrder = body.sortingOrder + 1; armR.sortingOrder = body.sortingOrder + 1; if (core != null) core.sortingOrder = body.sortingOrder + 2;
        armL.transform.localEulerAngles = new Vector3(0, 0, sw + 8f); armL.transform.localPosition = new Vector3(-1.9f, .8f, 0);
        if (float.IsNaN(punchAim)) { armR.transform.localEulerAngles = new Vector3(0, 0, -sw - 8f); armR.transform.localPosition = new Vector3(1.9f, .8f, 0); }
        else { armR.transform.localEulerAngles = new Vector3(0, 0, punchAim + 90f); armR.transform.localPosition = new Vector3(1.9f, .8f, 0) + (Vector3)(Combat.FromAng(punchAim) * punchExt); }
    }

    IEnumerator BecomeMech()
    {
        transforming = true; baseTint = Color.white; pose = 2; poseT = 3f; RogueAudio.Play(RSfx.Roar, 1f, 1.1f); RFx.Shake(.8f);
        for (int i = 0; i < 6; i++) { RFx.Blast((Vector2)transform.position + Random.insideUnitCircle * 1.5f, 1.2f, volt, true); yield return Wait(.2f); }
        form = 1; bodyScale = 1.5f; footOff = 1.7f; radius = 1.5f; airY = 0f; shadow.transform.localScale = new Vector3(3.8f, 1.1f, 1f); pose = 0; poseT = 0f; metal = true;
        armL = Combat.MakePx("ArmL", transform, RogueArt.MechArm, 0, null, 1.5f); armR = Combat.MakePx("ArmR", transform, RogueArt.MechArm, 0, null, 1.5f); armR.flipX = true;
        core = Combat.MakePx("Core", transform, RogueArt.MechCore, 0, new Vector2(0, .2f), 1.6f); core.sharedMaterial = Gfx.Additive; core.color = new Color(1, 1, 1, 0);
        RFx.Flash(transform.position, 10f, Color.white, .35f); RogueAudio.Play(RSfx.Slam, 1f, .8f); coreT = 0f; transforming = false;
    }

    IEnumerator FlipGravity()
    {
        Vector2[] dirs = { Vector2.left, Vector2.right, Vector2.up, Vector2.down }; Vector2 nd; do { nd = dirs[Random.Range(0, 4)]; } while (nd == gravDir);
        RoomManager.Instance?.ShowBanner("TRỌNG LỰC ĐẢO CHIỀU! " + (nd == Vector2.left ? "← sang trái" : nd == Vector2.right ? "→ sang phải" : nd == Vector2.up ? "↑ lên trên" : "↓ xuống dưới"), 2.4f);
        RogueAudio.Play(RSfx.Warn, .6f, .8f); float t = 0f; while (t < 1.2f) { t += Combat.Dt; RFx.Burst(PlayerPos, volt, 1, 2f, .3f, .12f); yield return null; }
        gravDir = nd; Combat.Gravity = nd * 3.6f; RogueAudio.Play(RSfx.Rise, .7f, 1.2f); RFx.Shake(.3f);
    }

    void SpawnCrates()
    {
        for (int i = 0; i < 3; i++)
        {
            Vector2 from = gravDir.x != 0f ? new Vector2(-gravDir.x * 9.5f, Random.Range(-3.2f, 3f)) : new Vector2(Random.Range(-7f, 7f), -gravDir.y * 6.2f);
            SlidingCrate.Spawn(from, gravDir * 5.8f, 10f * dm);
        }
    }

    Vector2 Muzzle => (Vector2)transform.position + new Vector2(0, form == 1 ? .6f : .4f);

    protected override IEnumerator Attack(int phase, int kind)
    {
        while (transforming) yield return null;
        int a = table[Mathf.Clamp(phase, 0, 3)][kind];
        switch (a)
        {
            case 0: yield return LaserGrid(true); break;
            case 4: yield return LaserGrid(false); break;
            case 1:   // loạt đạn điện nhắm người chơi
                {
                    pose = 1; poseT = 1f; int waves = phase >= 2 ? 3 : 2;
                    for (int w = 0; w < waves; w++)
                    {
                        Vector2 d = ToPlayer(Muzzle); for (int i = 0; i < 5; i++) Shoot(Muzzle, Combat.Rot(d, (i - 2) * 13f), 8f, 8f, BoltStyle.Orb, volt, Element.Loi, .24f);
                        RFx.Flash(Muzzle, 1.2f, volt, .1f); RogueAudio.Play(RSfx.Zap, .5f, 1.2f); yield return Wait(.55f);
                    }
                    break;
                }
            case 2:   // sàn điện: các ô nhấp nháy rồi giật
                {
                    pose = 2; poseT = 1.2f; RogueAudio.Play(RSfx.Warn, .6f, 1.3f);
                    for (int r = 0; r < 2; r++)
                    {
                        var cells = new List<int>(); for (int i = 0; i < 12; i++) cells.Add(i);
                        for (int n = 0; n < 5; n++) { int ci = cells[Random.Range(0, cells.Count)]; cells.Remove(ci); int cx = ci % 4, cy = ci / 4; ElectricTile.Spawn(new Vector2(-6f + cx * 4f, -2.5f + cy * 2.6f), new Vector2(3.8f, 2.5f), 1.4f, 12f * dm); }
                        yield return Wait(1.9f);
                    }
                    break;
                }
            case 5: yield return RocketVolley(phase); break;
            case 6: yield return Punch(); break;
            default: yield return Stomp(); break;
        }
    }

    IEnumerator LaserGrid(bool horizontal)
    {
        pose = 2; poseT = 2.4f; RogueAudio.Play(RSfx.Warn, .6f, 1.1f); int lines = 6; int gap = Random.Range(1, lines - 1), gap2 = Random.value < .5f ? Mathf.Clamp(gap + 1, 0, lines - 1) : -1;
        for (int i = 0; i < lines; i++)
        {
            if (i == gap || i == gap2) continue; float k = (i + .5f) / lines;
            if (horizontal) { float y = Mathf.Lerp(-3.2f, 3.3f, k); TeleLine.Spawn(new Vector2(-8.2f, y), new Vector2(8.2f, y), .8f, 1.4f, 11f * dm, Element.Loi, volt); }
            else { float x = Mathf.Lerp(-7.4f, 7.4f, k); TeleLine.Spawn(new Vector2(x, -4f), new Vector2(x, 4.2f), .85f, 1.4f, 11f * dm, Element.Loi, volt); }
        }
        yield return Wait(1.8f);
    }

    IEnumerator RocketVolley(int phase)
    {
        pose = 1; poseT = 1.6f; RogueAudio.Play(RSfx.Rocket, .8f, .8f);
        for (int i = 0; i < 6; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f; Vector2 from = (Vector2)transform.position + new Vector2(side * 1.6f, 1.4f);
            var b = Shoot(from, Combat.Rot(Vector2.up, side * 35f), 5.2f, 14f, BoltStyle.Rocket, new Color(1f, .6f, .3f), Element.None, .28f, 0, 0f, 1.5f, 1f, 4.5f); b.s.blast = 1.3f; b.s.accel = 1.5f; RFx.Burst(from, new Color(1f, .6f, .3f), 4, 3f, .3f, .16f);
            yield return Wait(.22f);
        }
        yield return Wait(.9f);
    }

    IEnumerator Punch()
    {
        Vector2 d = ToPlayer(transform.position); float a = Combat.Ang(d); Vector2 from = (Vector2)transform.position + new Vector2(0, .3f); Vector2 end = from + d * 9.5f; punchAim = a; punchExt = 0f;
        RogueAudio.Play(RSfx.Warn, .6f, 1.2f); TeleLine.Spawn(from, end, 1.7f, .95f, 22f * dm, Element.Nham, new Color(1f, .6f, .3f));
        float t = 0f; while (t < .95f) { t += Combat.Dt; punchExt = Mathf.Lerp(0f, -.4f, t / .95f); yield return null; }
        t = 0f; while (t < .15f) { t += Combat.Dt; punchExt = Mathf.Lerp(-.4f, 2.3f, t / .15f); yield return null; }
        RFx.Shake(.45f); RogueAudio.Play(RSfx.Slam, .8f, 1.1f); yield return Wait(.5f); punchAim = float.NaN; yield return Wait(.6f);
    }

    IEnumerator Stomp()
    {
        pose = 2; poseT = 1.6f; Vector2 p = (Vector2)transform.position + Vector2.down * 1.2f; RogueAudio.Play(RSfx.Warn, .6f, .7f);
        CombatTelegraph.Spawn(p, 3.4f, 1.1f, 20f * dm, Element.Loi); yield return Wait(1.1f);
        RFx.Shake(.7f); RogueAudio.Play(RSfx.Slam, 1f, 1.1f); RogueShockwave.Spawn(p, 9f, 16f, 1f, 14f * dm, Element.Loi, volt); yield return Wait(1.4f);
    }

    // ------------------------------------------------------------------ tuyệt kỹ Tia Hủy Diệt
    GameObject orb;
    protected override IEnumerator Ultimate()
    {
        pose = 2; poseT = 6f; RoomManager.Instance?.SetOverlay(new Color(1f, .55f, .2f, .12f));
        orb = new GameObject("Satellite"); var g = Combat.MakeSr("G", orb.transform, RogueArt.Glow, new Color(1f, .8f, .4f, .9f), new Vector2(1f, 1f), Combat.OrdFx + 20); g.sharedMaterial = Gfx.Additive;
        orb.transform.position = new Vector2(0f, 4.9f); RogueAudio.Play(RSfx.Rise, 1f, .7f);
        float t = 0f; while (t < 2.2f) { t += Combat.Dt; float k = t / 2.2f; g.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 6f, k) / g.sprite.bounds.size.x; RFx.Burst((Vector2)orb.transform.position + Random.insideUnitCircle * 2f, new Color(1f, .8f, .4f), 1, -3f, .5f, .2f); yield return null; }
    }

    protected override IEnumerator Resolve(GradeBand band, EssayDifficulty d)
    {
        RoomManager.Instance?.SetOverlay(new Color(0, 0, 0, 0)); var pl = Combat.Player; Vector2 src = orb != null ? (Vector2)orb.transform.position : new Vector2(0, 4.9f);
        RogueAudio.Play(RSfx.Laser, 1f, .6f); RFx.Shake(.6f);
        if (band != GradeBand.Fail)
        {   // chuyển hướng tia: bắn xuống máy biến áp ở người chơi rồi phản ngược về boss
            Vector2 mid = pl != null ? (Vector2)pl.transform.position : Vector2.zero; mid.y = Mathf.Min(mid.y, 1f);
            for (int i = 0; i < 6; i++) { RFx.BeamLine(src, mid, new Color(1f, .8f, .4f), 1.2f, .12f); yield return Wait(.05f); }
            RogueAudio.Play(RSfx.Reflect, 1f); RFx.Ring(mid, 2.4f, volt, .5f); RFx.Flash(mid, 4f, Color.white, .3f);
            for (int i = 0; i < 8; i++) { RFx.BeamLine(mid, (Vector2)transform.position + Vector2.up * .5f, volt, 1.5f, .14f); yield return Wait(.05f); }
            float pct = band == GradeBand.Good ? .14f : .09f; float dmg = maxHp * pct; float floor = maxHp * (phaseIdx < 3 ? new float[] { .75f, .5f, .25f }[phaseIdx] : 0f);
            hp = Mathf.Max(floor + 1f, hp - dmg); FlashNow(.4f); RFx.Blast(transform.position, 3f, volt, true); RFx.Text(transform.position + Vector3.up * 3f, "-" + Mathf.RoundToInt(dmg), new Color(1f, .9f, .4f), .24f); RogueAudio.Play(RSfx.BossHit, 1f);
            RoomManager.Instance?.ShowBanner(band == GradeBand.Good ? "Phản đòn hoàn hảo! Tia hủy diệt dội ngược về boss" : "Tia bị chuyển hướng! Boss chịu sát thương", 3f);
            if (band == GradeBand.Good && d == EssayDifficulty.Expert && pl != null) pl.Heal(20f);
        }
        else
        {   // không có máy biến áp: tia quét ngang cả sân, sát thương có trần 30% máu tối đa
            RoomManager.Instance?.ShowBanner("Không phản đòn được! Lướt qua tia quét bằng Shift", 2.5f);
            float x = -9f; float dmg = Mathf.Min(60f * dm, PlayerCombat.MaxHp * .3f); bool hit = false;
            TeleLine.Spawn(new Vector2(-8f, -4f), new Vector2(-8f, 4.2f), .5f, .6f, 0f, Element.Loi, new Color(1f, .6f, .3f)); yield return Wait(.7f);
            while (x < 9.5f)
            {
                float dt = Combat.Dt; x += 9f * dt; RFx.BeamLine(new Vector2(x, 5f), new Vector2(x, -4f), new Color(1f, .75f, .35f), 1.6f, .06f);
                if (!hit && pl != null && !pl.Dead && Mathf.Abs(pl.transform.position.x - x) < .9f) { hit = true; pl.Hurt(dmg, Element.Loi, new Vector2(x, 0)); }
                yield return null;
            }
        }
        if (orb != null) { RFx.Burst(orb.transform.position, new Color(1f, .8f, .4f), 16, 5f, .6f, .2f); Destroy(orb); }
        yield return Wait(1.2f);
    }
}
