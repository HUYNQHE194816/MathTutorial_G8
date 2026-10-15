using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum BState { Intro, Fight, Transition, Dying }

/// <summary>
/// Khung chung của ba boss (mục 6): bốn pha theo đoạn máu 100-75-50-25-0; mỗi lần chuyển pha (ba lần mỗi trận) boss tung tuyệt kỹ báo hiệu,
/// ba trụ đá xuất hiện, người chơi chạy tới chọn một trụ rồi giải đề (trận đấu đóng băng khi viết). Kết quả chấm quyết định hiệu ứng (mục 6.1).
/// Mỗi boss chỉ cần viết các chiêu của mình dưới dạng coroutine.
/// </summary>
public abstract class BossBase : Foe
{
    public static BossBase Current;
    public int index; public string bossName = "", bossTitle = ""; public int phaseIdx; public BState bstate = BState.Intro; public bool vulnerable;
    public System.Action Defeated;
    public override bool Targetable => !Dead && vulnerable && bstate == BState.Fight;

    protected float dm, rate; protected Vector2 home = new Vector2(0, 1.6f); protected float footOff = 1.8f; protected SpriteRenderer shadow;
    protected int pose; protected float poseT; protected bool busy, pendingTransition; protected Sprite[] idleFrames; protected float animT; protected Color baseTint = Color.white;
    static readonly float[] thr = { .75f, .5f, .25f };
    readonly List<GameObject> cleanup = new List<GameObject>();

    protected abstract string Subject { get; }
    protected abstract BossPillars.Style PillarStyle { get; }
    protected abstract float BaseHp { get; }
    protected abstract int AttackKinds(int phase);
    protected abstract IEnumerator Attack(int phase, int kind);
    protected abstract IEnumerator Ultimate();                                  // hiệu ứng nạp tuyệt kỹ (trước khi hiện trụ)
    protected abstract IEnumerator Resolve(GradeBand band, EssayDifficulty d);   // tuyệt kỹ giáng xuống và hệ quả
    protected abstract void EnterPhase(int p);
    protected abstract void BuildVisual();
    protected virtual float CooldownFor(int phase) => Mathf.Lerp(2.3f, 1.4f, phase / 3f) / rate;
    public virtual float UltTime => 16f;
    protected virtual void Tick(float dt) { }
    protected virtual void OnTransitionStart() { }

    public static T Create<T>(Vector2 pos) where T : BossBase, new()
    {
        var go = new GameObject(typeof(T).Name); go.transform.position = pos; var b = go.AddComponent<T>(); b.boss = true; b.ccResist = .75f;
        float diffHp = 1f + (RoomRules.HpMul(RunState.difficulty) - 1f) * .6f;
        b.dm = Mathf.Lerp(1f, RoomRules.DamageMul(RunState.difficulty), .7f); b.rate = Mathf.Lerp(1f, RoomRules.RateMul(RunState.difficulty), .6f);
        b.maxHp = b.hp = b.BaseHp * diffHp; b.home = pos; Combat.Foes.Add(b); Current = b; b.BuildVisual();
        b.StartCoroutine(b.IntroRoutine()); return b;
    }

    // ------------------------------------------------------------------ nhịp trận
    protected IEnumerator Wait(float s) { float t = 0f; while (t < s) { t += Combat.Dt; yield return null; } }

    protected IEnumerator MoveTo(Vector2 target, float spd)
    {
        while (!Dead && Vector2.Distance(transform.position, target) > .12f)
        {
            float dt = Combat.Dt; if (dt > 0f && !IsStunned) transform.position = Vector2.MoveTowards(transform.position, target, spd * Mathf.Max(.3f, SpeedMulStatus) * dt);
            yield return null;
        }
    }

    protected Vector2 PlayerPos => Combat.Player != null ? (Vector2)Combat.Player.transform.position : Vector2.zero;
    protected Vector2 ToPlayer(Vector2 from) { var d = PlayerPos - from; return d.sqrMagnitude < .01f ? Vector2.down : d.normalized; }
    protected Vector2 ClampBoss(Vector2 p) => new Vector2(Mathf.Clamp(p.x, -6.6f, 6.6f), Mathf.Clamp(p.y, -1.2f, 2.9f));

    protected CombatBolt Shoot(Vector2 from, Vector2 dir, float speed, float dmg, BoltStyle st, Color col, Element el, float radius = .26f, int bounce = 0, float curve = 0f, float homing = 0f, float size = 1f, float life = 4f, float slow = 0f)
    {
        var s = new BoltSpec { dir = dir, speed = speed, dmg = dmg * dm, radius = radius, life = life, el = el, col = col, style = st, bounce = bounce, curve = curve, homing = homing, size = size, slowPlayer = slow };
        return CombatBolt.Fire(from, s);
    }

    IEnumerator IntroRoutine()
    {
        bstate = BState.Intro; vulnerable = false; busy = true;
        RoomManager.Instance?.BossIntro(bossName, bossTitle); RogueAudio.Play(RSfx.Roar, 1f, index == 2 ? 1.2f : .9f); RFx.Shake(.6f); RFx.Punch(.6f);
        yield return Wait(2.6f);
        bstate = BState.Fight; vulnerable = true; busy = false; EnterPhase(0); RogueAudio.PlayTrack(RTrack.Boss);
        StartCoroutine(FightLoop());
    }

    IEnumerator FightLoop()
    {
        yield return Wait(.6f);
        int last = -1;
        while (!Dead)
        {
            if (pendingTransition && bstate == BState.Fight) { yield return Transition(); continue; }
            if (bstate != BState.Fight || IsStunned) { yield return null; continue; }
            int kinds = AttackKinds(phaseIdx), pick = Random.Range(0, kinds); if (kinds > 1 && pick == last) pick = (pick + 1) % kinds; last = pick;
            busy = true; yield return Attack(phaseIdx, pick); busy = false;
            float cd = CooldownFor(phaseIdx); float t = 0f; while (t < cd && !pendingTransition) { t += Combat.Dt; yield return null; }
        }
    }

    protected override void OnHurt(float d)
    {
        if (bstate != BState.Fight || phaseIdx >= 3) return;
        float floor = maxHp * thr[phaseIdx];
        if (hp <= floor) { hp = floor; pendingTransition = true; }
    }

    // ------------------------------------------------------------------ chuyển pha: tuyệt kỹ + trụ đá
    IEnumerator Transition()
    {
        pendingTransition = false; bstate = BState.Transition; vulnerable = false;
        foreach (var b in CombatBolt.Enemy.ToArray()) if (b != null) Destroy(b.gameObject);
        RoomManager.HazardsOn = false; OnTransitionStart();
        yield return MoveTo(home, 9f);
        RogueAudio.Play(RSfx.Roar, .9f); RFx.Shake(.45f); RFx.Punch(.5f); RFx.Ring(transform.position, 3f, Color.white, .5f);
        RoomManager.Instance?.ShowBanner("TUYỆT KỸ ĐANG NẠP · chọn một trụ đá và giải đề để chống đỡ", 4f);
        yield return Ultimate();

        var diffs = BossPillars.PickDifficulties(RunState.difficulty);
        int chosen = -1; bool timeout = false;
        var pillars = BossPillars.Spawn(PillarStyle, diffs, UltTime, i => chosen = i, () => timeout = true);
        while (chosen < 0 && !timeout) yield return null;

        GradeBand band = GradeBand.Fail; EssayDifficulty d = diffs[Mathf.Max(0, chosen)];
        if (chosen >= 0)
        {
            bool got = false; string note = null;
            RoomManager.Instance.StartBossEssay(d, Subject, (b, n) => { band = b; note = n; got = true; });
            while (!got) yield return null;
            if (!string.IsNullOrEmpty(note)) RunState.notes.Add(note);
        }
        else RoomManager.Instance?.ShowBanner("Hết giờ chọn trụ! Không có khiên.", 2.5f);
        pillars.Dismiss(band);
        yield return Wait(.5f);
        yield return Resolve(band, d);

        phaseIdx++; RoomManager.HazardsOn = true; bstate = BState.Fight; vulnerable = true; EnterPhase(phaseIdx); RogueAudio.PlayTrack(RTrack.Boss);
    }

    /// <summary>Tính khiên theo kết quả chấm (mục 6.1): Đạt tốt chặn trọn, Đạt chặn 60%, Chưa đạt không khiên và sát thương có trần 30% máu tối đa.</summary>
    protected void HitWithUltimate(float baseDmg, Element el, GradeBand band, EssayDifficulty d, bool healOnExpert = true)
    {
        var pl = Combat.Player; if (pl == null || pl.Dead) return;
        float dmg = baseDmg * dm; string msg;
        if (band == GradeBand.Good) { pl.AddShield(dmg * 1.05f, 4f); msg = "Khiên chặn trọn đòn tuyệt kỹ!"; }
        else if (band == GradeBand.Pass) { pl.AddShield(dmg * .6f, 4f); msg = "Khiên chặn khoảng 60% sát thương."; }
        else { dmg = Mathf.Min(dmg, PlayerCombat.MaxHp * .3f); msg = "Không có khiên. Sát thương bị chặn trần ở 30% máu tối đa."; }
        RoomManager.Instance?.ShowBanner(msg, 3f);
        pl.Hurt(dmg, el, transform.position, false, true);
        if (band == GradeBand.Good && d == EssayDifficulty.Expert && healOnExpert) { pl.Heal(20f); RoomManager.Instance?.ShowBanner("Trụ Siêu khó: hồi thêm 20 máu!", 2.5f); }
    }

    // ------------------------------------------------------------------ hoạt họa và vòng lặp
    void Update()
    {
        if (Current != this) Current = this;
        float dt = Combat.Dt;
        if (!Dead || bstate == BState.Dying) AnimateBoss(dt);
        if (dt <= 0f || Dead) return;
        if (!TickStatus(dt)) return;
        if (bstate == BState.Fight && !busy && !IsStunned) Drift(dt);
        Tick(dt);
    }

    float driftT; Vector2 driftTo;
    void Drift(float dt)
    {
        driftT -= dt;
        if (driftT <= 0f) { driftT = Random.Range(1.5f, 3f); driftTo = ClampBoss(home + new Vector2(Random.Range(-4.5f, 4.5f), Random.Range(-1.4f, 1.2f))); }
        transform.position = Vector2.MoveTowards(transform.position, driftTo, 1.5f * Mathf.Max(.2f, SpeedMulStatus) * dt);
    }

    protected virtual Sprite CurrentSprite() => idleFrames[Mathf.Min(idleFrames.Length - 1, pose != 0 ? pose + 1 : (int)(animT / .55f) % 2)];

    protected virtual void AnimateBoss(float dt)
    {
        animT += Time.deltaTime; if (poseT > 0f) { poseT -= Time.deltaTime; if (poseT <= 0f) pose = 0; }
        if (body == null) return;
        body.sprite = CurrentSprite(); body.sortingOrder = Combat.Order(transform.position.y - footOff + .3f);
        if (bstate != BState.Dying) body.transform.localPosition = new Vector2(0, -footOff + airY);
        var pl = Combat.Player; if (pl != null) body.flipX = false;
        float breathe = 1f + .015f * Mathf.Sin(animT * 3f); body.transform.localScale = new Vector3(bodyScale / breathe, bodyScale * breathe, 1f);
        shadow.transform.localPosition = new Vector2(0, -footOff + .1f);
        SyncVisual(baseTint);
    }

    protected float bodyScale = 1f, airY;

    protected void MakeBody(Sprite s, float footOffset, float shadowW)
    {
        footOff = footOffset;
        shadow = Combat.MakeSr("Shade", transform, RogueArt.Disc, new Color(0, 0, 0, .45f), new Vector2(shadowW, shadowW * .3f), 4, new Vector2(0, -footOffset + .1f));
        body = Combat.MakePx("Body", transform, s, Combat.Order(transform.position.y), new Vector2(0, -footOffset)); InitFlash(body);
    }

    // ------------------------------------------------------------------ chết
    protected override void Kill()
    {
        if (Dead) return; Dead = true; bstate = BState.Dying; vulnerable = false; StopAllCoroutines();
        foreach (var b in CombatBolt.Enemy.ToArray()) if (b != null) Destroy(b.gameObject);
        foreach (var o in new List<Foe>(Combat.Foes)) if (o != null && o != this && !o.Dead) { RFx.Burst(o.Pos, Color.white, 6, 3f, .4f, .15f); o.TakeHit(9999f, Element.None, o.Pos, 0f, false, true); }
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        RogueAudio.Play(RSfx.Roar, 1f, .7f); Combat.SlowMo(2.8f, .3f); RFx.Shake(.8f); RogueAudio.Duck(.2f); RoomManager.Instance?.BossBarHide();
        float t = 0f;
        while (t < 2.6f)
        {
            t += Time.unscaledDeltaTime; Vector2 p = (Vector2)transform.position + Random.insideUnitCircle * radius * .9f;
            if (Random.value < .35f) { RFx.Blast(p, Random.Range(.8f, 1.6f), new Color(1f, .6f, .3f), true); RogueAudio.Play(RSfx.Boom, .6f); }
            var c = body.color; body.transform.localPosition = new Vector2(Random.Range(-.1f, .1f), -footOff + Random.Range(-.05f, .05f)); body.color = c;
            if (flash != null) flash.color = new Color(1, 1, 1, Random.value < .4f ? .8f : 0f);
            yield return null;
        }
        RFx.Flash(transform.position, 14f, Color.white, .5f); RFx.Blast(transform.position, 4f, new Color(1f, .8f, .5f), true); RogueAudio.Play(RSfx.Boom, 1f);
        for (int i = 0; i < 4; i++) RFx.Wisp(transform.position + (Vector3)Random.insideUnitCircle, new Color(.8f, .9f, 1f));
        body.enabled = false; if (flash != null) flash.enabled = false; shadow.enabled = false;
        Remove(); Current = null; yield return new WaitForSecondsRealtime(.9f);
        Defeated?.Invoke(); Destroy(gameObject, .1f);
    }
}

/// <summary>Quân giữa trận do boss triệu hồi: bóng giả của cáo, drone của nhà khoa học. Nhận sát thương như quái nhưng không tính vào Mực.</summary>
public class BossMinion : Foe
{
    public System.Action<BossMinion> OnDie; public bool explodeOnHit; public float explodeDmg = 10f; public float life = 99f; public Color tint = Color.white; public float orbit; public Vector2 center; float t, shootT = 1.5f, fade;
    public Element shotEl = Element.None; public float dmgMul = 1f; public BoltStyle shotStyle = BoltStyle.Orb; public Color shotCol = new Color(.5f, .9f, 1f); public Transform follow; public float orbitR = 3f, orbitSpeed = 70f, orbitAng; public bool shoots = true; public Vector2 drift;
    Sprite[] frames; int fi;

    public static BossMinion Create(Sprite[] frames, Vector2 pos, float hp, float rad, float scale, Color tint)
    {
        var go = new GameObject("BossMinion"); go.transform.position = pos; var m = go.AddComponent<BossMinion>();
        m.frames = frames; m.maxHp = m.hp = hp; m.radius = rad; m.tint = tint; m.bloodCol = new Color(.6f, .9f, 1f);
        m.body = Combat.MakePx("Body", go.transform, frames[0], Combat.Order(pos.y), new Vector2(0, -frames[0].bounds.size.y * scale * .35f), scale); m.InitFlash(m.body);
        Combat.MakeSr("Shade", go.transform, RogueArt.Disc, new Color(0, 0, 0, .35f), new Vector2(rad * 1.8f, rad * .6f), 4, new Vector2(0, -frames[0].bounds.size.y * scale * .35f + .1f));
        Combat.Foes.Add(m); RFx.Ring(pos, 1f, tint, .4f); return m;
    }

    void Update()
    {
        if (Dead) return; float dt = Combat.Dt; if (dt <= 0f) return;
        t += dt; fade = Mathf.Min(1f, fade + dt * 2f); if (!TickStatus(dt)) return;
        if (follow != null) { orbitAng += orbitSpeed * dt; transform.position = (Vector2)follow.position + Vector2.Scale(Combat.FromAng(orbitAng) * orbitR, new Vector2(1f, .7f)); }
        else if (drift.sqrMagnitude > 0f) transform.position = RoomManager.Clamp((Vector2)transform.position + drift * SpeedMulStatus * dt);
        if (t > life) { Fade(); return; }
        if (shoots && !IsStunned && t > .8f)
        {
            shootT -= dt; if (shootT <= 0f && Combat.Player != null && !Combat.Player.Dead)
            { shootT = Random.Range(2f, 3.2f); var d = ((Vector2)Combat.Player.transform.position - Pos).normalized; CombatBolt.Fire(Pos, new BoltSpec { dir = d, speed = 6.5f, dmg = 7f * dmgMul, radius = .22f, el = shotEl, col = shotCol, style = shotStyle, life = 3.5f }); RogueAudio.Play(RSfx.Drone, .25f, 1.4f); }
        }
        body.sprite = frames[(int)(Time.time * 6f) % frames.Length]; body.sortingOrder = Combat.Order(transform.position.y);
        SyncVisual(new Color(tint.r, tint.g, tint.b, tint.a * fade));
    }

    void Fade() { if (Dead) return; Dead = true; Remove(); RFx.Burst(Pos, tint, 8, 2.5f, .5f, .16f); OnDie?.Invoke(this); Destroy(gameObject); }

    public override float TakeHit(float dmg, Element el, Vector2 src, float knock = 0f, bool crit = false, bool silent = false)
    {
        if (Dead || fade < .5f) return 0f;
        if (explodeOnHit) { Dead = true; Remove(); CombatBolt.Explode(Pos, 1.9f, explodeDmg * dmgMul, Element.Phong, false, 0f, tint); RFx.Burst(Pos, tint, 10, 4f, .5f, .18f); OnDie?.Invoke(this); Destroy(gameObject); return dmg; }
        return base.TakeHit(dmg, el, src, knock, crit, silent);
    }

    protected override void Kill()
    {
        if (Dead) return; Dead = true; Remove(); RFx.Blast(Pos, 1f, tint, false); RogueAudio.Play(RSfx.Kill, .4f); OnDie?.Invoke(this); Destroy(gameObject);
    }
}

/// <summary>Ba trụ đá (hoặc đèn lồng bùa, máy biến áp) mỗi trụ gắn một đề, hiện sẵn độ khó. Chạy tới và nhấn E để chọn; hai trụ còn lại biến mất.</summary>
public class BossPillars : MonoBehaviour
{
    public enum Style { Stone, Lantern, Transformer }
    class P { public Transform t; public SpriteRenderer sr, rune, glow; public EssayDifficulty d; public TextMesh label; public Vector2 pos; public Sprite[] frames; }
    readonly List<P> ps = new List<P>(); Style style; float timeLeft, total; System.Action<int> onChosen; System.Action onTimeout; int chosen = -1; bool done; TextMesh prompt, timer; float t; int nearIdx = -1;

    public static EssayDifficulty[] PickDifficulties(EssayDifficulty run)
    {
        int lo = Mathf.Clamp((int)run - 1, 0, 1); return new[] { (EssayDifficulty)lo, (EssayDifficulty)(lo + 1), (EssayDifficulty)(lo + 2) };
    }

    public static BossPillars Spawn(Style style, EssayDifficulty[] diffs, float time, System.Action<int> onChosen, System.Action onTimeout)
    {
        var go = new GameObject("Pillars"); var b = go.AddComponent<BossPillars>(); b.style = style; b.timeLeft = b.total = time; b.onChosen = onChosen; b.onTimeout = onTimeout;
        Vector2[] pos = { new Vector2(-5.4f, -1.9f), new Vector2(0f, -2.5f), new Vector2(5.4f, -1.9f) };
        for (int i = 0; i < 3; i++) b.Add(pos[i], diffs[i], i);
        b.prompt = MakeText("PillarPrompt", "", .12f, new Color(1f, .95f, .8f), 150); b.timer = MakeText("PillarTimer", "", .16f, new Color(1f, .5f, .4f), 150);
        RogueAudio.Play(RSfx.Rise, .9f); RFx.Shake(.3f);
        return b;
    }

    static TextMesh MakeText(string n, string s, float size, Color c, int order)
    {
        var go = new GameObject(n); var tm = go.AddComponent<TextMesh>(); tm.font = GameFont.Body; tm.text = s; tm.fontSize = 64; tm.characterSize = size; tm.anchor = TextAnchor.MiddleCenter; tm.color = c; tm.fontStyle = FontStyle.Bold;
        var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = tm.font.material; mr.sortingOrder = Combat.OrdText - 10 + order - 150; return tm;
    }

    void Add(Vector2 pos, EssayDifficulty d, int i)
    {
        var p = new P { pos = pos, d = d }; var go = new GameObject("Pillar" + i); go.transform.SetParent(transform, false); go.transform.position = pos; p.t = go.transform;
        Color dc = DifficultyTheme.Of(d);
        p.glow = Combat.MakeSr("Glow", go.transform, RogueArt.Glow, new Color(dc.r, dc.g, dc.b, .55f), new Vector2(3.4f, 3.4f), Combat.Order(pos.y) - 2, new Vector2(0, 1.1f)); p.glow.sharedMaterial = Gfx.Additive;
        Combat.MakeSr("Base", go.transform, RogueArt.Disc, new Color(0, 0, 0, .45f), new Vector2(1.8f, .6f), 4, new Vector2(0, .05f));
        Vector2 off = Vector2.zero;
        if (style == Style.Stone) { p.sr = Combat.MakePx("Spr", go.transform, RogueArt.StonePillar, Combat.Order(pos.y)); p.rune = Combat.MakePx("Rune", go.transform, RogueArt.PillarRune, Combat.Order(pos.y) + 1, new Vector2(0, 1.35f)); p.rune.color = dc; p.rune.sharedMaterial = Gfx.Additive; }
        else if (style == Style.Lantern) { p.frames = RogueArt.Lantern; p.sr = Combat.MakePx("Spr", go.transform, p.frames[0], Combat.Order(pos.y), new Vector2(0, 1.0f)); p.sr.color = Color.Lerp(Color.white, dc, .3f); }
        else { p.frames = RogueArt.Transformer; p.sr = Combat.MakePx("Spr", go.transform, p.frames[0], Combat.Order(pos.y)); p.sr.color = Color.Lerp(Color.white, dc, .25f); }
        var tmGo = new GameObject("Label"); tmGo.transform.SetParent(go.transform, false); tmGo.transform.localPosition = new Vector3(0, 3.3f, 0);
        var tm = tmGo.AddComponent<TextMesh>(); tm.font = GameFont.Body; tm.text = RoomRules.DifficultyName(d); tm.fontSize = 64; tm.characterSize = .14f; tm.anchor = TextAnchor.MiddleCenter; tm.color = dc; tm.fontStyle = FontStyle.Bold;
        var mr = tmGo.GetComponent<MeshRenderer>(); mr.sharedMaterial = tm.font.material; mr.sortingOrder = Combat.OrdText - 20; p.label = tm;
        ps.Add(p); RFx.Burst(pos + Vector2.up * .5f, dc, 10, 3f, .6f, .16f, 3f, false); RFx.Ring(pos, 1.2f, dc, .5f);
    }

    void Update()
    {
        if (done) return; float dt = Combat.Dt; if (dt <= 0f) return; t += dt; timeLeft -= dt;
        var pl = Combat.Player; var k = Keyboard.current; nearIdx = -1; float best = RoomManager.Reach;
        for (int i = 0; i < ps.Count; i++)
        {
            var p = ps[i]; float a = Mathf.Sin(t * 3f + i) * .5f + .5f; p.glow.color = new Color(p.glow.color.r, p.glow.color.g, p.glow.color.b, .35f + .3f * a);
            if (p.frames != null) p.sr.sprite = p.frames[(int)(t * 4f) % p.frames.Length];
            if (p.rune != null) p.rune.color = new Color(p.rune.color.r, p.rune.color.g, p.rune.color.b, .5f + .5f * a);
            if (pl != null && !pl.Dead) { float d = Vector2.Distance(pl.transform.position, p.pos + Vector2.up * .6f); if (d < best) { best = d; nearIdx = i; } }
        }
        timer.transform.position = new Vector3(0, 4.7f, 0); timer.text = "Tuyệt kỹ giáng xuống sau " + Mathf.Max(0, Mathf.CeilToInt(timeLeft)) + "s";
        timer.color = timeLeft < 5f ? new Color(1f, .35f + .3f * Mathf.Sin(t * 14f), .3f) : new Color(1f, .8f, .5f);
        if (nearIdx >= 0)
        {
            prompt.transform.position = ps[nearIdx].pos + new Vector2(0, 4.0f); prompt.text = "Nhấn E · chọn trụ " + RoomRules.DifficultyName(ps[nearIdx].d); prompt.color = DifficultyTheme.Of(ps[nearIdx].d);
            if (k != null && k.eKey.wasPressedThisFrame) { Choose(nearIdx); return; }
        }
        else { prompt.text = ""; }
        if (timeLeft <= 0f) { done = true; onTimeout?.Invoke(); }
    }

    void Choose(int i)
    {
        chosen = i; done = true; RogueAudio.Play(RSfx.Select, .9f); RFx.Shake(.25f);
        for (int k = 0; k < ps.Count; k++) if (k != i) { var p = ps[k]; RFx.Burst(p.pos + Vector2.up, new Color(.5f, .5f, .5f), 14, 4f, .8f, .2f, 7f, false); RFx.Puff(p.pos + Vector2.up, new Color(.4f, .38f, .46f), 6, .8f); Destroy(p.t.gameObject); }
        var c = ps[i]; RFx.Ring(c.pos + Vector2.up, 2f, DifficultyTheme.Of(c.d), .5f); prompt.text = ""; timer.text = "";
        onChosen?.Invoke(i);
    }

    public void Dismiss(GradeBand band)
    {
        foreach (var p in ps) if (p.t != null) { RFx.Burst(p.pos + Vector2.up, band == GradeBand.Fail ? new Color(.5f, .5f, .5f) : DifficultyTheme.Of(p.d), 12, 3f, .7f, .18f, 2f, band != GradeBand.Fail); Destroy(p.t.gameObject); }
        if (prompt != null) Destroy(prompt.gameObject); if (timer != null) Destroy(timer.gameObject); Destroy(gameObject);
    }
}
