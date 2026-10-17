using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Chùm đuôi của cáo, xòe hình quạt sau lưng và đung đưa. Gắn được cho cả cáo thật lẫn bóng giả để hai bên giống hệt nhau.</summary>
public class FoxTails : MonoBehaviour
{
    readonly List<SpriteRenderer> tails = new List<SpriteRenderer>(); SpriteRenderer bodyRef; public float alpha = 1f; float t; float baseY = .9f; float seed;

    public static FoxTails Attach(GameObject host, SpriteRenderer body, int n, float baseY = .9f)
    {
        var f = host.AddComponent<FoxTails>(); f.bodyRef = body; f.baseY = baseY; f.seed = Random.value * 6f; f.SetCount(n, false); return f;
    }

    public int Count => tails.Count;

    public void SetCount(int n, bool burst)
    {
        while (tails.Count > n) { var last = tails[tails.Count - 1]; if (burst) RFx.Burst(last.transform.position + last.transform.up * 1.6f, new Color(.5f, 1f, .9f), 12, 4f, .7f, .2f, 0f); Destroy(last.gameObject); tails.RemoveAt(tails.Count - 1); }
        while (tails.Count < n) { var sr = Combat.MakePx("Tail", transform, RogueArt.FoxTail, 0, null, 1.5f); tails.Add(sr); }
    }

    void LateUpdate()
    {
        t += Time.deltaTime; int n = tails.Count;
        for (int i = 0; i < n; i++)
        {
            float k = n == 1 ? .5f : i / (float)(n - 1); float ang = Mathf.Lerp(-82f, 82f, k) + Mathf.Sin(t * 1.7f + i * .8f + seed) * 5f;
            var tr = tails[i].transform; tr.localPosition = new Vector3(0, (bodyRef != null ? bodyRef.transform.localPosition.y : 0f) + baseY, 0); tr.localEulerAngles = new Vector3(0, 0, -ang);
            tails[i].sortingOrder = (bodyRef != null ? bodyRef.sortingOrder : 100) - 2 - Mathf.Abs(i - n / 2);
            tails[i].color = new Color(1f, 1f, 1f, alpha);
        }
    }
}

/// <summary>Boss 2: Cáo Chín Đuôi (màn 2-5, Phòng Sinh Học) — cơ chế TẤN CÔNG: bùa chỉ đúng bản thể giữa ảo cảnh.</summary>
public class BossFox : BossBase
{
    protected override string Subject => "sinh";
    protected override BossPillars.Style PillarStyle => BossPillars.Style.Lantern;
    protected override float BaseHp => 1300f;
    static readonly int[][] table = { new[] { 0, 1, 0 }, new[] { 2, 3, 0, 1 }, new[] { 4, 5, 0, 1 }, new[] { 6, 7, 2, 0 } };
    protected override int AttackKinds(int p) => table[Mathf.Clamp(p, 0, 3)].Length;
    static readonly Color ghost = new Color(.45f, 1f, .92f);

    FoxTails tails; bool hidden; float fade = 1f; readonly List<BossMinion> decoys = new List<BossMinion>(); readonly List<GameObject> shadows = new List<GameObject>(); float cloneT = 8f;
    public override bool Targetable => base.Targetable && !hidden;
    int[] tailsByPhase = { 9, 7, 5, 3 };

    protected override void BuildVisual()
    {
        bossName = "Cáo Chín Đuôi"; bossTitle = "Hồ ly giữa sương mù"; index = 1; innate = Element.Phong; resist[(int)Element.Phong] = .5f; radius = .95f; bloodCol = ghost;
        idleFrames = RogueArt.FoxBody; MakeBody(idleFrames[0], 1.35f, 2.3f); home = new Vector2(0f, 1.5f);
        tails = FoxTails.Attach(gameObject, body, 9, .6f);
    }

    protected override Sprite CurrentSprite() => idleFrames[pose != 0 ? 2 : (int)(animT / .6f) % 2];

    protected override void EnterPhase(int p)
    {
        tails.SetCount(tailsByPhase[Mathf.Clamp(p, 0, 3)], p > 0);
        if (p == 3) { baseTint = new Color(1f, .85f, .95f); RFx.Shake(.4f); RogueAudio.Play(RSfx.Roar, .8f, 1.3f); }
        RoomManager.Instance?.ShowBanner(p == 0 ? "Pha 1 · cầu lửa ma, lao chớp" : p == 1 ? "Pha 2 · phân thân: CHỈ MỘT LÀ THẬT" : p == 2 ? "Pha 3 · dây leo, bào tử và bóng tối" : "Pha 4 · CUỒNG NỘ · đuôi quạt tia, chớp nhoáng liên tiếp", 3.5f);
        if (p == 1) cloneT = 1f;
    }

    protected override void Tick(float dt)
    {
        tails.alpha = fade; baseTint = new Color(baseTint.r, baseTint.g, baseTint.b, fade); shadow.color = new Color(0, 0, 0, .45f * fade);
        airY = Mathf.Sin(Time.time * 2.2f) * .1f;
        decoys.RemoveAll(d => d == null || d.Dead);
    }

    IEnumerator Fade(float to, float dur) { float f0 = fade, t = 0f; while (t < dur) { t += Combat.Dt; fade = Mathf.Lerp(f0, to, t / dur); yield return null; } fade = to; }

    IEnumerator Vanish() { RogueAudio.Play(RSfx.Teleport, .7f); RFx.Burst(Pos, ghost, 14, 4f, .6f, .2f, 0f); hidden = true; yield return Fade(0f, .22f); }
    IEnumerator Appear(Vector2 at) { transform.position = ClampBoss(at); RFx.Burst(Pos, ghost, 14, 4f, .6f, .2f, 0f); RFx.Ring(Pos, 1.6f, ghost, .3f); yield return Fade(1f, .2f); hidden = false; }

    Vector2 Mouth => (Vector2)transform.position + new Vector2(0f, .35f);

    protected override IEnumerator Attack(int phase, int kind)
    {
        int a = table[Mathf.Clamp(phase, 0, 3)][kind];
        if (a == 2 && decoys.Count > 0) a = 0;
        switch (a)
        {
            case 0:   // cầu lửa ma xanh bắn quạt
                {
                    pose = 1; poseT = .8f; int n = phase == 0 ? 3 : 5; float spread = phase == 0 ? 22f : 17f; Vector2 d = ToPlayer(Mouth); RogueAudio.Play(RSfx.Fire, .7f, 1.3f);
                    for (int wave = 0; wave < (phase >= 3 ? 2 : 1); wave++)
                    {
                        d = ToPlayer(Mouth);
                        for (int i = 0; i < n; i++) Shoot(Mouth, Combat.Rot(d, (i - (n - 1) * .5f) * spread), 8.5f, 9f, BoltStyle.Orb, ghost, Element.Hoa, .3f, 0, (i - (n - 1) * .5f) * 7f, 0f, 1.2f);
                        RFx.Flash(Mouth, 1.4f, ghost, .1f); yield return Wait(.55f);
                    }
                    yield return Wait(.3f); break;
                }
            case 1: yield return BlinkDash(phase >= 3 ? 3 : 1, phase >= 3 ? .55f : .9f); break;
            case 2: yield return Clone(phase >= 3 ? 3 : 2); break;
            case 3: yield return TailSweep(); break;
            case 4:   // dây leo trồi lên và trói chân
                {
                    pose = 1; poseT = 1f; RogueAudio.Play(RSfx.Vine, .8f, .7f);
                    VineSnare.Spawn(PlayerPos, 1.15f, 1.35f, 8f * dm, .9f);
                    for (int i = 0; i < 3; i++) { yield return Wait(.3f); VineSnare.Spawn(ClampArena(PlayerPos + Random.insideUnitCircle * 2.8f), 1.1f, 1.2f, 7f * dm, .8f); }
                    yield return Wait(.9f); break;
                }
            case 5:   // bào tử độc: bãi độc và bóng tối bao phủ
                {
                    pose = 1; poseT = 1f; RogueAudio.Play(RSfx.Poison, .8f, .6f);
                    for (int i = 0; i < 4; i++) { var p = ClampArena(PlayerPos + Random.insideUnitCircle * 4f); CombatTelegraph.Spawn(p, 1.5f, 1.1f, 6f * dm, Element.Doc, q => HazardFx.Puddle(q, 1.5f, 8f, new Color(.75f, .45f, 1f), Element.Doc, 3f * dm, .3f)); yield return Wait(.25f); }
                    RoomManager.Instance?.PulseDarkness(.55f, 9f); yield return Wait(1.2f); break;
                }
            case 6: yield return RotatingBeams(); break;
            default: yield return BlinkDash(3, .5f); break;
        }
    }

    static Vector2 ClampArena(Vector2 p) => new Vector2(Mathf.Clamp(p.x, -7.2f, 7.2f), Mathf.Clamp(p.y, -3.4f, 3.2f));

    IEnumerator BlinkDash(int count, float warn)
    {
        for (int c = 0; c < count; c++)
        {
            yield return Vanish();
            Vector2 pp = PlayerPos; Vector2 dir = Combat.FromAng(Random.Range(0f, 360f)); Vector2 at = ClampBoss(pp - dir * 5f);
            // chọn điểm xuất hiện sao cho đường lao đi xuyên qua người chơi
            dir = (pp - at).normalized; yield return Appear(at); pose = 1; poseT = warn + .5f;
            Vector2 end = ClampArena(at + dir * 16f); bool onEdge = false;
            float tmax = 16f; for (float s = 1f; s < 16f; s += .5f) { var q = at + dir * s; if (Mathf.Abs(q.x) > 7.6f || q.y < -3.5f || q.y > 3.3f) { tmax = s; onEdge = true; break; } }
            end = at + dir * tmax;
            TeleLine.Spawn(at, end, 1.2f, warn, 15f * dm, Element.Phong, ghost); RogueAudio.Play(RSfx.Warn, .45f, 1.5f);
            yield return Wait(warn);
            float t = 0f; Vector2 s0 = at; while (t < .22f) { t += Combat.Dt; transform.position = Vector2.Lerp(s0, ClampBoss(end), t / .22f); RFx.Burst(Pos, ghost, 1, 1f, .35f, .3f); yield return null; }
            yield return Wait(count > 1 ? .12f : .5f);
        }
    }

    IEnumerator Clone(int total)
    {
        yield return Vanish(); RogueAudio.Play(RSfx.Rise, .6f, 1.4f);
        var slots = new List<Vector2>(); for (int i = 0; i < total; i++) slots.Add(new Vector2(Mathf.Lerp(-5.6f, 5.6f, (i + .5f) / total) + Random.Range(-.6f, .6f), Random.Range(.4f, 2.4f)));
        for (int i = slots.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var t = slots[i]; slots[i] = slots[j]; slots[j] = t; }
        for (int i = 1; i < total; i++) SpawnDecoy(slots[i]);
        yield return Appear(slots[0]); RoomManager.Instance?.ShowBanner("Phân thân! Chỉ MỘT là thật: bóng giả sẽ nổ khi trúng đòn", 3.5f); yield return Wait(.8f);
    }

    void SpawnDecoy(Vector2 pos)
    {
        var d = BossMinion.Create(idleFrames, pos, 1f, .95f, 1.5f, Color.white);
        d.explodeOnHit = true; d.explodeDmg = 12f; d.dmgMul = dm; d.life = 14f; d.shotEl = Element.Hoa; d.shotCol = ghost; d.shotStyle = BoltStyle.Orb; d.drift = Random.insideUnitCircle.normalized * .6f; d.bloodCol = ghost;
        var host = d.gameObject; var tl = FoxTails.Attach(host, d.GetComponentInChildren<SpriteRenderer>(), tails.Count, .6f);
        decoys.Add(d);
    }

    IEnumerator TailSweep()
    {
        pose = 1; poseT = 3.4f; RogueAudio.Play(RSfx.Warn, .5f, 1.2f); Vector2 c = transform.position;
        var ring = Combat.MakeSr("SweepMark", transform, RogueArt.Ring, ghost, new Vector2(7f, 5f), -96); ring.sharedMaterial = Gfx.Additive;
        float t = 0f; while (t < .85f) { t += Combat.Dt; ring.color = new Color(ghost.r, ghost.g, ghost.b, .4f + .4f * Mathf.Sin(t * 22f)); yield return null; }
        Destroy(ring.gameObject);
        var blades = new List<SpriteRenderer>(); for (int i = 0; i < 4; i++) { var b = Combat.MakePx("Blade", transform, RogueArt.FoxTail, Combat.OrdFx - 12, new Vector2(0, 0), 2.0f); b.sharedMaterial = Gfx.Additive; blades.Add(b); }
        float ang = Random.Range(0f, 360f), dir = Random.value < .5f ? 1f : -1f; float hitCd = 0f; RogueAudio.Play(RSfx.Wind, .9f, .8f);
        t = 0f;
        while (t < 2.3f)
        {
            float dt = Combat.Dt; t += dt; ang += 190f * dir * dt; hitCd -= dt;
            for (int i = 0; i < 4; i++)
            {
                float a = ang + i * 90f; blades[i].transform.localPosition = (Vector3)(Combat.FromAng(a) * .4f) + new Vector3(0, -.1f, 0); blades[i].transform.localEulerAngles = new Vector3(0, 0, a - 90f); blades[i].color = new Color(.8f, 1f, 1f, .85f);
                if (Random.value < .4f) RFx.Burst((Vector2)transform.position + Combat.FromAng(a) * Random.Range(1.2f, 3.2f), ghost, 1, .5f, .25f, .16f);
            }
            var pl = Combat.Player;
            if (pl != null && !pl.Dead && hitCd <= 0f)
            {
                Vector2 rel = (Vector2)pl.transform.position - (Vector2)transform.position; float dist = rel.magnitude;
                if (dist > .8f && dist < 3.6f) for (int i = 0; i < 4; i++) { if (Mathf.Abs(Mathf.DeltaAngle(Combat.Ang(rel), ang + i * 90f)) < 15f) { pl.Hurt(13f * dm, Element.Phong, transform.position); hitCd = .6f; break; } }
            }
            yield return null;
        }
        foreach (var b in blades) Destroy(b.gameObject); yield return Wait(.4f);
    }

    IEnumerator RotatingBeams()
    {
        pose = 1; poseT = 5.5f; Vector2 c = transform.position + new Vector3(0, .3f); float a0 = Random.Range(0f, 360f); int n = 6; RogueAudio.Play(RSfx.Warn, .6f, 1.3f);
        float t = 0f; while (t < 1.1f) { t += Combat.Dt; for (int i = 0; i < n; i++) RFx.BeamLine(c, c + Combat.FromAng(a0 + i * 60f) * 16f, ghost, .08f + .06f * Mathf.Sin(t * 30f), .05f); yield return null; }
        RogueAudio.Play(RSfx.Laser, .8f, .7f); RFx.Shake(.2f); float rot = 0f, hitCd = 0f; float dir = Random.value < .5f ? 1f : -1f; t = 0f;
        while (t < 3.6f)
        {
            float dt = Combat.Dt; t += dt; rot += 48f * dir * dt; hitCd -= dt; var pl = Combat.Player;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = Combat.FromAng(a0 + rot + i * 60f); RFx.BeamLine(c, c + d * 16f, ghost, .55f, .05f);
                if (pl != null && !pl.Dead && hitCd <= 0f)
                {
                    Vector2 rel = (Vector2)pl.transform.position - c; float along = Vector2.Dot(rel, d); if (along > .6f && Mathf.Abs(rel.x * d.y - rel.y * d.x) < .55f + pl.radius * .4f) { pl.Hurt(10f * dm, Element.Phong, c); hitCd = .35f; }
                }
            }
            if (Time.frameCount % 4 == 0) RogueAudio.Play(RSfx.Laser, .12f, 1.5f);
            yield return null;
        }
        yield return Wait(.5f);
    }

    // ------------------------------------------------------------------ tuyệt kỹ Ảo Cảnh
    protected override IEnumerator Ultimate()
    {
        pose = 1; poseT = 5f; RoomManager.Instance?.SetOverlay(new Color(.35f, .15f, .55f, .22f)); RoomManager.Instance?.PulseDarkness(.45f, 40f);
        foreach (var d in decoys.ToArray()) if (d != null) d.TakeHit(999f, Element.None, d.Pos, 0f, false, true); decoys.Clear();
        RogueAudio.Play(RSfx.Rise, .9f, .6f); yield return Fade(.0f, .3f); hidden = true;
        for (int i = 0; i < 12; i++)
        {   // bóng ảo trôi khắp sân (chỉ để nhìn)
            var go = new GameObject("Shadow"); go.transform.position = new Vector2(Random.Range(-7f, 7f), Random.Range(-1.5f, 3f)); var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = idleFrames[i % 2]; sr.color = new Color(.5f, .35f, .8f, .0f); sr.sortingOrder = Combat.Order(go.transform.position.y - 1.35f);
            go.transform.localScale = Vector3.one * Combat.PxScale * 1.5f; go.AddComponent<ShadowDrift>(); shadows.Add(go); RFx.Burst(go.transform.position, new Color(.6f, .4f, 1f), 6, 2f, .5f, .18f); yield return Wait(.1f);
        }
    }

    protected override IEnumerator Resolve(GradeBand band, EssayDifficulty d)
    {
        RoomManager.Instance?.SetOverlay(new Color(0, 0, 0, 0)); RoomManager.Instance?.PulseDarkness(0f, 1f);
        foreach (var s in shadows) if (s != null) Destroy(s); shadows.Clear();
        Vector2 at = new Vector2(Random.Range(-4f, 4f), Random.Range(.6f, 2.4f)); hidden = true; transform.position = at;
        if (band == GradeBand.Fail)
        {   // không có bùa: bản thể ẩn giữa 7 bóng giả
            RoomManager.Instance?.ShowBanner("Không có bùa chỉ đường. Hãy tự tìm bản thể!", 3f);
            for (int i = 0; i < 7; i++) SpawnDecoy(new Vector2(Random.Range(-6.6f, 6.6f), Random.Range(-.6f, 2.6f)));
            yield return Appear(at); busy = false; yield return Wait(.6f);
            yield break;
        }
        yield return Appear(at);
        stunUntil = Combat.Now + 6f; if (band == GradeBand.Good) { exposedUntil = Combat.Now + 6f; exposedMul = 1.3f; }
        RoomManager.Instance?.ShowBanner(band == GradeBand.Good ? "Bùa hiện ra! Cáo choáng 6 giây và chịu thêm 30% sát thương" : "Bùa hiện ra! Cáo choáng 6 giây", 3f);
        RogueAudio.Play(RSfx.Select, .8f, 1.2f);
        StartCoroutine(Talisman(6f));
        yield return Wait(.3f);
    }

    IEnumerator Talisman(float dur)
    {
        var go = new GameObject("Talisman"); var ring = Combat.MakeSr("Ring", go.transform, RogueArt.Ring, new Color(1f, .85f, .3f), new Vector2(3.6f, 3.6f), Combat.OrdFx); ring.sharedMaterial = Gfx.Additive;
        var tm = go.AddComponent<TextMesh>(); tm.font = GameFont.Body; tm.text = "BẢN THỂ"; tm.fontSize = 64; tm.characterSize = .13f; tm.anchor = TextAnchor.MiddleCenter; tm.color = new Color(1f, .85f, .3f); tm.fontStyle = FontStyle.Bold; go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material; go.GetComponent<MeshRenderer>().sortingOrder = Combat.OrdText;
        float t = 0f; while (t < dur && !Dead) { t += Combat.Dt; go.transform.position = (Vector2)transform.position + new Vector2(0, 2.2f + Mathf.Sin(t * 5f) * .12f); ring.transform.position = (Vector2)transform.position + Vector2.up * .2f; ring.transform.localScale = Vector3.one * (1f + .08f * Mathf.Sin(t * 8f)) * 3.6f / ring.sprite.bounds.size.x; yield return null; }
        Destroy(go);
    }

    protected override float CooldownFor(int phase) => base.CooldownFor(phase) * .9f;
}

public class ShadowDrift : MonoBehaviour
{
    SpriteRenderer sr; Vector2 v; float t;
    void Start() { sr = GetComponent<SpriteRenderer>(); v = Random.insideUnitCircle.normalized * Random.Range(.4f, 1f); }
    void Update() { t += Combat.VDt; transform.position += (Vector3)(v * Combat.VDt); var c = sr.color; c.a = Mathf.Min(.4f, t * .5f); sr.color = c; sr.sortingOrder = Combat.Order(transform.position.y - 1.35f); if (Mathf.Abs(transform.position.x) > 8f) v.x = -v.x; }
}
