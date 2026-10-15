using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Boss 1: Chuột Bạch Biến Dị (màn 1-5, Phòng Thí Nghiệm, Hóa học) — cơ chế PHÒNG THỦ: khiên chặn gai độc.</summary>
public class BossRat : BossBase
{
    protected override string Subject => "hoa";
    protected override BossPillars.Style PillarStyle => BossPillars.Style.Stone;
    protected override float BaseHp => 1100f;
    static readonly int[][] table = { new[] { 0, 1, 2 }, new[] { 3, 4, 0 }, new[] { 5, 6, 1 }, new[] { 7, 5, 3, 1, 6 } };   // 0 cầu · 1 tỏa đạn · 2 đạn nảy · 3 bình độc · 4 axit · 5 gai · 6 chuột con · 7 dậm đất
    protected override int AttackKinds(int p) => table[Mathf.Clamp(p, 0, 3)].Length;
    readonly List<GameObject> marks = new List<GameObject>();
    static readonly Color poison = new Color(.55f, 1f, .35f);

    protected override void BuildVisual()
    {
        bossName = "Chuột Bạch Biến Dị"; bossTitle = "Vật thí nghiệm số 7"; index = 0; innate = Element.Doc; resist[(int)Element.Doc] = .5f; radius = 1.7f; bloodCol = new Color(.5f, 1f, .4f);
        idleFrames = RogueArt.RatBoss; MakeBody(idleFrames[0], 1.8f, 4.2f);
    }

    Vector2 Mouth => (Vector2)transform.position + new Vector2(0f, .4f);
    protected override void EnterPhase(int p)
    {
        if (p == 3)
        {   // hóa to, hồi máu, ra chiêu nhanh hơn
            StartCoroutine(Grow()); hp = Mathf.Min(maxHp, hp + maxHp * .12f); RFx.Text(transform.position + Vector3.up * 3f, "+12% máu", GameTheme.Moss, .2f);
        }
        RoomManager.Instance?.ShowBanner(p == 0 ? "Pha 1 · bắn cầu, tỏa đạn, đạn nảy" : p == 1 ? "Pha 2 · bình thuốc độc, khạc axit" : p == 2 ? "Pha 3 · gai độc, đàn chuột con" : "Pha 4 · HÓA TO! Cẩn thận cú dậm đất", 3f);
    }

    IEnumerator Grow()
    {
        RogueAudio.Play(RSfx.Roar, 1f, .7f); RFx.Shake(.6f); RFx.Punch(.6f); baseTint = new Color(1f, .82f, .82f); float t = 0f, s0 = bodyScale, f0 = footOff;
        while (t < 1.2f) { t += Combat.Dt; float k = Mathf.SmoothStep(0f, 1f, t / 1.2f); bodyScale = Mathf.Lerp(s0, 1.3f, k); footOff = Mathf.Lerp(f0, f0 * 1.3f, k); RFx.Burst(Pos + Random.insideUnitCircle * 2f, poison, 1, 2f, .5f, .2f); yield return null; }
        radius = 2.2f;
    }

    protected override IEnumerator Attack(int phase, int kind)
    {
        int a = table[Mathf.Clamp(phase, 0, 3)][kind]; float spd = 1f + .12f * phase;
        switch (a)
        {
            case 0:
                {   // bắn cầu: 3 cầu độc to chậm
                    pose = 1; poseT = .8f; RogueAudio.Play(RSfx.Poison, .8f, .7f);
                    for (int i = 0; i < 3; i++) { Vector2 d = ToPlayer(Mouth); var dd = Combat.Rot(d, (i - 1) * 14f); Shoot(Mouth, dd, 6f * spd, 11f, BoltStyle.Orb, poison, Element.Doc, .42f, 0, 0f, 0f, 1f); RFx.Flash(Mouth, 1.4f, poison, .1f); yield return Wait(.28f / spd); }
                    break;
                }
            case 1:
                {   // tỏa đạn theo đường thẳng
                    pose = 1; poseT = .6f; Vector2 d = ToPlayer(Mouth); int n = phase == 3 ? 11 : 9; RogueAudio.Play(RSfx.Roar, .35f, 1.4f);
                    for (int i = 0; i < n; i++) Shoot(Mouth, Combat.Rot(d, (i - (n - 1) * .5f) * 11f), 8.2f * spd, 7f, BoltStyle.Orb, new Color(.7f, 1f, .5f), Element.Doc, .22f);
                    RFx.Burst(Mouth, poison, 10, 4f, .4f, .16f);
                    if (phase == 3) { yield return Wait(.45f); Vector2 d2 = ToPlayer(Mouth); for (int i = 0; i < n - 1; i++) Shoot(Mouth, Combat.Rot(d2, (i - (n - 2) * .5f) * 11f), 8.2f * spd, 7f, BoltStyle.Orb, new Color(.7f, 1f, .5f), Element.Doc, .22f); }
                    yield return Wait(.4f); break;
                }
            case 2:
                {   // đạn nảy vào tường rồi bắn ngược lại
                    pose = 1; poseT = .8f; Vector2 d = ToPlayer(Mouth);
                    for (int i = 0; i < 4; i++) { Shoot(Mouth, Combat.Rot(d, (i - 1.5f) * 9f), 9.5f * spd, 8f, BoltStyle.Orb, new Color(1f, .9f, .4f), Element.Doc, .26f, 2, 0f, 0f, 1f, 5f); yield return Wait(.12f); }
                    RogueAudio.Play(RSfx.Zap, .5f, .8f); yield return Wait(.5f); break;
                }
            case 3:
                {   // ném bình thuốc độc tạo bãi độc 10 giây
                    int n = phase >= 3 ? 3 : 2; poseT = .9f; pose = 2;
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 target = ClampArena(PlayerPos + Random.insideUnitCircle * (i == 0 ? .3f : 2.2f));
                        CombatBolt.Fire(Mouth, new BoltSpec { lob = true, lobTo = target, lobTime = 1f, lobHeight = 2.6f, friendly = false, style = BoltStyle.Vial, col = poison, size = 1.3f, radius = .3f, onLand = p => { HazardFx.Puddle(p, phase >= 3 ? 2.2f : 1.8f, 10f, poison, Element.Doc, 4f * dm, .45f); RFx.Blast(p, 1f, poison, true); } });
                        RogueAudio.Play(RSfx.Glass, .5f, .8f); yield return Wait(.5f);
                    }
                    yield return Wait(.5f); break;
                }
            case 4:
                {   // khạc axit: chùm axit hình nón, gây độc và làm chậm
                    pose = 1; poseT = .9f; Vector2 d = ToPlayer(Mouth); RogueAudio.Play(RSfx.Poison, .9f, .6f);
                    for (int w = 0; w < 2; w++) { for (int i = 0; i < 7; i++) Shoot(Mouth, Combat.Rot(d, (i - 3f) * 10f + Random.Range(-3f, 3f)), 8f * spd + Random.Range(-1f, 1f), 8f, BoltStyle.Orb, new Color(.8f, 1f, .2f), Element.Doc, .28f, 0, 0f, 0f, 1.2f, 4f, .5f); RFx.Burst(Mouth, new Color(.8f, 1f, .2f), 8, 4f, .4f, .2f); yield return Wait(.45f); }
                    break;
                }
            case 5:
                {   // gai độc mọc dưới chân và đuổi theo
                    int n = phase == 3 ? 9 : 7; float r = phase == 3 ? 1.25f : 1.0f; pose = 2; poseT = .6f; RogueAudio.Play(RSfx.Vine, .6f, .6f);
                    for (int i = 0; i < n; i++) { SpikeStrike.Spawn(PlayerPos, .8f, r, 13f * dm, Element.Doc); yield return Wait(.36f / spd); }
                    yield return Wait(.5f); break;
                }
            case 6:
                {   // đàn chuột con tấn công từ hai hướng
                    pose = 2; poseT = 1f; RogueAudio.Play(RSfx.Roar, .6f, 1.5f); RFx.Shake(.2f); int per = phase == 3 ? 4 : 3;
                    for (int i = 0; i < per; i++)
                    {
                        Monster.Create(MonsterKind.Minion, new Vector2(-7.4f, Random.Range(-3f, 3f)), false, 0); Monster.Create(MonsterKind.Minion, new Vector2(7.4f, Random.Range(-3f, 3f)), false, 0); yield return Wait(.28f);
                    }
                    yield return Wait(.9f); break;
                }
            default:
                {   // nhảy lên rồi dậm xuống: sóng xung kích toàn map (né bằng lướt)
                    Vector2 target = ClampBoss(PlayerPos); RogueAudio.Play(RSfx.Warn, .8f, .7f); pose = 2; poseT = 2.2f;
                    float t = 0f; Vector2 from = transform.position;
                    CombatTelegraph.Spawn(target, 3f, 1.25f, 20f * dm, Element.Doc);
                    while (t < .55f) { t += Combat.Dt; airY = Mathf.Sin(t / .55f * Mathf.PI * .5f) * 5.5f; shadow.color = new Color(0, 0, 0, .45f * (1f - t / .55f)); yield return null; }
                    t = 0f; while (t < .7f) { t += Combat.Dt; transform.position = Vector2.Lerp(from, target, t / .7f); yield return null; }
                    t = 0f; while (t < .2f) { t += Combat.Dt; airY = Mathf.Lerp(5.5f, 0f, t / .2f); yield return null; }
                    airY = 0f; shadow.color = new Color(0, 0, 0, .45f); RogueAudio.Play(RSfx.Slam, 1f); RFx.Shake(.7f); RFx.Blast(transform.position + Vector3.down * 1.5f, 3f, poison, true); Combat.HitStop(.08f);
                    RogueShockwave.Spawn(transform.position + Vector3.down * 1.5f, 8f, 17f, 1.1f, 15f * dm, Element.Doc, poison); yield return Wait(.55f);
                    RogueShockwave.Spawn(transform.position + Vector3.down * 1.5f, 8f, 17f, 1.1f, 15f * dm, Element.Doc, poison); yield return Wait(1.6f); break;
                }
        }
    }

    static Vector2 ClampArena(Vector2 p) => new Vector2(Mathf.Clamp(p.x, -7.2f, 7.2f), Mathf.Clamp(p.y, -3.4f, 3.2f));

    protected override IEnumerator Ultimate()
    {
        pose = 1; poseT = 4f; RoomManager.Instance?.SetOverlay(new Color(.35f, 1f, .2f, .14f));
        for (int i = 0; i < 15; i++)
        {
            var p = new Vector2(Random.Range(-7f, 7f), Random.Range(-3.3f, 3f)); var t = CombatTelegraph.Spawn(p, 1.15f, 999f, 0f, Element.Doc); marks.Add(t.gameObject); RFx.Burst(p, poison, 4, 2f, .5f, .14f); yield return Wait(.07f);
        }
        RogueAudio.Play(RSfx.Poison, 1f, .5f);
    }

    protected override IEnumerator Resolve(GradeBand band, EssayDifficulty d)
    {
        foreach (var m in marks) if (m != null) Destroy(m); marks.Clear(); pose = 2; poseT = 1.5f;
        for (int i = 0; i < 34; i++) { SpikeStrike.Spawn(new Vector2(Random.Range(-7.4f, 7.4f), Random.Range(-3.6f, 3.4f)), Random.Range(.05f, .7f), 1.1f, 0f, Element.Doc); }
        RogueAudio.Play(RSfx.Slam, .9f, .8f); RFx.Shake(.6f); yield return Wait(.62f);
        HitWithUltimate(55f, Element.Doc, band, d); RoomManager.Instance?.SetOverlay(new Color(0, 0, 0, 0));
        yield return Wait(1.6f);
    }
}
