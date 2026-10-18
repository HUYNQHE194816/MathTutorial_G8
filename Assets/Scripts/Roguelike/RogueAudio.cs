using System;
using System.Collections.Generic;
using UnityEngine;

public enum RSfx
{
    Pistol, Rifle, Laser, BowDraw, BowShot, Slash, Thrust, Orb, Cannon, Rocket, Boom, Staff, AxeSwing, Scythe,
    HitFlesh, HitMetal, Crit, Kill, Hurt, Dash, Pickup, Heal, Chest, Door, React, Fire, Ice, Thunder, Rock, Wind, Poison, Vine,
    Roar, BossHit, Warn, Shield, ShieldBreak, Rise, Select, Reflect, Spike, Slam, Teleport, Drone, Zap, Tick, Stinger, Rune, Swap, Glass
}

public enum RTrack { None, Intro, Lab, Bio, Sky, Boss, Rest, Altar, Ending, Credits }

/// <summary>Âm thanh của chế độ roguelike được tổng hợp ngay trong code (không cần file): mỗi hiệu ứng có vài biến thể, nhạc nền là vòng lặp ngắn theo từng khu vực.</summary>
public class RogueAudio : MonoBehaviour
{
    public static RogueAudio I { get; private set; }
    const int SR = 22050;

    static readonly Dictionary<RSfx, AudioClip[]> sfx = new Dictionary<RSfx, AudioClip[]>();
    static readonly Dictionary<RTrack, AudioClip> music = new Dictionary<RTrack, AudioClip>();
    AudioSource[] pool; int poolI; AudioSource mus; float musTarget = .5f; RTrack cur = RTrack.None; float masterSfx = .8f, masterMus = .55f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { sfx.Clear(); music.Clear(); I = null; }

    public static RogueAudio Ensure()
    {
        if (I != null) return I;
        var go = new GameObject("RogueAudio"); DontDestroyOnLoad(go); I = go.AddComponent<RogueAudio>();   // sống qua các lần nạp màn để nhạc nền không bị ngắt
        I.pool = new AudioSource[14];
        for (int i = 0; i < I.pool.Length; i++) { I.pool[i] = go.AddComponent<AudioSource>(); I.pool[i].playOnAwake = false; I.pool[i].spatialBlend = 0f; }
        I.StartCoroutine(I.Warm());
        I.mus = go.AddComponent<AudioSource>(); I.mus.loop = true; I.mus.playOnAwake = false; I.mus.volume = 0f; I.mus.spatialBlend = 0f;
        return I;
    }

    /// <summary>Tạo sẵn các hiệu ứng âm thanh, mỗi khung hình một cái để lần bắn đầu tiên không bị khựng.</summary>
    System.Collections.IEnumerator Warm()
    {
        foreach (RSfx id in System.Enum.GetValues(typeof(RSfx))) { Get(id); yield return null; }
    }

    void OnDestroy() { if (I == this) I = null; }

    public static void Play(RSfx id, float vol = 1f, float pitch = 1f)
    {
        var a = Ensure(); var clips = Get(id);
        var c = clips[UnityEngine.Random.Range(0, clips.Length)];
        var s = a.pool[a.poolI]; a.poolI = (a.poolI + 1) % a.pool.Length;
        s.pitch = pitch * UnityEngine.Random.Range(.96f, 1.04f); s.volume = Mathf.Clamp01(vol * a.masterSfx); s.clip = c; s.Play();
    }

    // ---------------------------------------------------------------------- nhạc nền
    public static void PlayTrack(RTrack t)
    {
        var a = Ensure(); if (a.cur == t) { if (t != RTrack.None) a.musTarget = a.masterMus * (t == RTrack.Boss ? .9f : 1f); return; } a.cur = t;
        if (t == RTrack.None) { a.musTarget = 0f; return; }
        if (!music.TryGetValue(t, out var c) || c == null) { c = BuildTrack(t); music[t] = c; }
        a.mus.clip = c; a.mus.loop = t != RTrack.Ending; a.mus.volume = 0f; a.mus.Play(); a.musTarget = a.masterMus * (t == RTrack.Boss ? .9f : 1f);
    }

    public static void Duck(float to) { if (I != null) I.musTarget = I.masterMus * to; }
    public static void Stop() { if (I != null) { I.cur = RTrack.None; I.musTarget = 0f; } }

    void Update() { if (mus != null) { mus.volume = Mathf.MoveTowards(mus.volume, musTarget, Time.unscaledDeltaTime * .6f); if (musTarget <= 0f && mus.volume <= 0f && mus.isPlaying) mus.Stop(); } }

    // ---------------------------------------------------------------------- bộ tổng hợp
    static float[] Buf(float dur) => new float[Mathf.CeilToInt(dur * SR)];
    static AudioClip Clip(string name, float[] d)
    {
        float mx = .0001f; foreach (var v in d) mx = Mathf.Max(mx, Mathf.Abs(v));
        float g = mx > .95f ? .95f / mx : 1f; if (g != 1f) for (int i = 0; i < d.Length; i++) d[i] *= g;
        var c = AudioClip.Create(name, d.Length, 1, SR, false); c.SetData(d, 0); return c;
    }

    static float Env(float t, float dur, float atk, float pow) { float a = atk > 0f ? Mathf.Clamp01(t / atk) : 1f; float r = Mathf.Clamp01(1f - t / dur); return a * Mathf.Pow(r, pow); }
    static float Osc(int kind, float ph) { ph -= Mathf.Floor(ph); switch (kind) { case 0: return Mathf.Sin(ph * 6.2832f); case 1: return ph < .5f ? 1f : -1f; case 2: return ph * 2f - 1f; default: return Mathf.Abs(ph * 4f - 2f) - 1f; } }

    /// <summary>Nốt có tần số trượt từ f0 xuống f1 (tiếng bắn, tiếng laze, tiếng đổ).</summary>
    static float[] Sweep(float dur, float f0, float f1, int kind, float vol, float pow = 2f, float noise = 0f, int seed = 1, float vib = 0f)
    {
        var d = Buf(dur); var r = new System.Random(seed); float ph = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)SR, k = t / dur; float f = f0 * Mathf.Pow(f1 / f0, k) * (1f + vib * Mathf.Sin(t * 40f));
            ph += f / SR; float n = (float)(r.NextDouble() * 2.0 - 1.0);
            d[i] = (Osc(kind, ph) * (1f - noise) + n * noise) * Env(t, dur, .004f, pow) * vol;
        }
        return d;
    }

    static float[] Noise(float dur, float vol, float pow, float lp, int seed = 2, float atk = .002f)
    {
        var d = Buf(dur); var r = new System.Random(seed); float y = 0f;
        for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; float n = (float)(r.NextDouble() * 2.0 - 1.0); y += (n - y) * lp; d[i] = y * Env(t, dur, atk, pow) * vol; }
        return d;
    }

    static float[] Mixd(params float[][] parts)
    {
        int n = 0; foreach (var p in parts) n = Mathf.Max(n, p.Length); var d = new float[n];
        foreach (var p in parts) for (int i = 0; i < p.Length; i++) d[i] += p[i]; return d;
    }

    static float[] Delay(float[] a, float sec) { var d = new float[a.Length + Mathf.CeilToInt(sec * SR)]; Array.Copy(a, 0, d, d.Length - a.Length, a.Length); return d; }

    static AudioClip[] Get(RSfx id)
    {
        if (sfx.TryGetValue(id, out var c) && c != null && c[0] != null) return c;
        var list = new List<AudioClip>(); int vars = 3;
        for (int v = 0; v < vars; v++) list.Add(Clip(id + "_" + v, Make(id, v)));
        sfx[id] = list.ToArray(); return sfx[id];
    }

    static float[] Make(RSfx id, int v)
    {
        float j = 1f + v * .07f; int s = 11 + v * 17 + (int)id * 5;
        switch (id)
        {
            case RSfx.Pistol: return Mixd(Sweep(.16f, 900f * j, 160f, 1, .55f, 3f, .25f, s), Noise(.09f, .5f, 3f, .6f, s + 1), Sweep(.22f, 180f, 60f, 0, .5f, 2f));
            case RSfx.Rifle: return Mixd(Sweep(.1f, 1200f * j, 220f, 2, .45f, 3f, .3f, s), Noise(.06f, .45f, 3f, .7f, s + 1));
            case RSfx.Laser: return Mixd(Sweep(.22f, 2400f * j, 500f, 2, .35f, 1.6f, .05f, s, .03f), Sweep(.22f, 1200f * j, 250f, 0, .4f, 1.4f));
            case RSfx.BowDraw: return Mixd(Sweep(.35f, 160f, 340f * j, 3, .18f, .6f, .1f, s), Noise(.35f, .08f, .8f, .05f, s));
            case RSfx.BowShot: return Mixd(Sweep(.12f, 700f * j, 180f, 3, .45f, 2f, .15f, s), Noise(.18f, .28f, 2.4f, .25f, s + 2), Delay(Sweep(.2f, 520f, 300f, 0, .12f, 2f), .02f));
            case RSfx.Slash: return Mixd(Noise(.2f, .55f, 2.2f, .22f + v * .04f, s, .02f), Sweep(.18f, 1500f * j, 400f, 2, .12f, 2f, .4f, s));
            case RSfx.Thrust: return Mixd(Noise(.16f, .5f, 2f, .3f, s, .01f), Sweep(.16f, 700f * j, 280f, 2, .2f, 2f, .3f, s));
            case RSfx.Orb: return Mixd(Sweep(.25f, 600f * j, 1100f, 0, .35f, 1.5f), Delay(Sweep(.25f, 900f * j, 1500f, 0, .22f, 1.5f), .03f));
            case RSfx.Cannon: return Mixd(Sweep(.5f, 140f * j, 34f, 0, .9f, 2.2f, .12f, s), Noise(.35f, .8f, 2f, .09f, s + 3), Sweep(.12f, 800f, 120f, 1, .4f, 3f, .5f));
            case RSfx.Rocket: return Mixd(Noise(.5f, .5f, 1.2f, .18f, s, .04f), Sweep(.5f, 220f * j, 700f, 2, .25f, 1.2f, .3f, s));
            case RSfx.Boom: return Mixd(Noise(.7f, .95f, 1.6f, .06f + v * .02f, s), Sweep(.6f, 110f * j, 28f, 0, .95f, 2f), Noise(.25f, .5f, 3f, .5f, s + 4));
            case RSfx.Staff: return Mixd(Sweep(.3f, 500f * j, 1300f, 0, .35f, 1.4f, 0f, s, .02f), Delay(Sweep(.3f, 750f * j, 1950f, 3, .2f, 1.6f), .02f), Noise(.12f, .1f, 2f, .5f, s));
            case RSfx.AxeSwing: return Mixd(Noise(.34f, .65f, 1.6f, .12f, s, .06f), Sweep(.3f, 300f * j, 90f, 2, .2f, 1.6f, .3f, s));
            case RSfx.Scythe: return Mixd(Noise(.32f, .5f, 1.5f, .3f, s, .05f), Sweep(.32f, 1300f * j, 350f, 3, .18f, 1.5f, .2f, s, .02f));
            case RSfx.HitFlesh: return Mixd(Noise(.1f, .7f, 3f, .22f, s), Sweep(.1f, 260f * j, 80f, 0, .6f, 3f));
            case RSfx.HitMetal: return Mixd(Sweep(.18f, 1700f * j, 1300f, 1, .3f, 3f, .2f, s), Sweep(.22f, 2600f * j, 2100f, 0, .2f, 3f), Noise(.04f, .4f, 3f, .8f, s));
            case RSfx.Crit: return Mixd(Sweep(.2f, 1100f * j, 1800f, 0, .35f, 2.4f), Sweep(.14f, 300f, 90f, 0, .6f, 3f), Noise(.08f, .5f, 3f, .6f, s));
            case RSfx.Kill: return Mixd(Sweep(.3f, 380f * j, 70f, 2, .45f, 2f, .3f, s), Noise(.18f, .45f, 2.2f, .2f, s));
            case RSfx.Hurt: return Mixd(Sweep(.28f, 240f * j, 70f, 2, .6f, 2.4f, .2f, s), Noise(.12f, .5f, 3f, .25f, s));
            case RSfx.Dash: return Mixd(Noise(.22f, .5f, 1.4f, .14f, s, .05f), Sweep(.2f, 220f, 900f * j, 3, .1f, 1.6f));
            case RSfx.Pickup: return Mixd(Sweep(.14f, 700f * j, 1400f, 0, .4f, 1.6f), Delay(Sweep(.18f, 1050f * j, 2100f, 0, .35f, 2f), .07f));
            case RSfx.Heal: return Mixd(Sweep(.4f, 500f * j, 1000f, 0, .3f, 1.2f), Delay(Sweep(.4f, 750f * j, 1500f, 0, .25f, 1.2f), .08f), Delay(Sweep(.4f, 1000f * j, 2000f, 0, .2f, 1.2f), .16f));
            case RSfx.Chest: return Mixd(Sweep(.5f, 300f, 600f * j, 3, .25f, 1.2f), Delay(Sweep(.5f, 450f, 900f, 0, .3f, 1.4f), .08f), Delay(Sweep(.5f, 600f, 1200f, 0, .3f, 1.4f), .16f), Noise(.3f, .2f, 2f, .4f, s));
            case RSfx.Door: return Mixd(Noise(.7f, .5f, 1f, .05f, s, .05f), Sweep(.7f, 90f, 60f, 2, .45f, 1.2f, .3f, s));
            case RSfx.React: return Mixd(Sweep(.35f, 400f * j, 1600f, 0, .35f, 1.6f), Delay(Sweep(.3f, 700f * j, 2200f, 3, .25f, 1.8f), .05f), Noise(.2f, .3f, 2.5f, .4f, s));
            case RSfx.Fire: return Mixd(Noise(.4f, .6f, 1.4f, .1f, s, .03f), Sweep(.3f, 200f, 100f, 2, .2f, 1.5f, .5f));
            case RSfx.Ice: return Mixd(Sweep(.3f, 2800f * j, 1500f, 0, .25f, 2.6f), Delay(Sweep(.3f, 3400f * j, 2000f, 0, .18f, 2.8f), .04f), Noise(.1f, .3f, 3f, .8f, s));
            case RSfx.Thunder: return Mixd(Noise(.5f, .8f, 2f, .35f, s, .001f), Sweep(.45f, 1800f * j, 120f, 2, .45f, 2.2f, .4f, s), Sweep(.6f, 90f, 40f, 0, .6f, 1.6f));
            case RSfx.Rock: return Mixd(Noise(.3f, .7f, 2.5f, .08f, s), Sweep(.25f, 120f * j, 50f, 0, .8f, 2.6f));
            case RSfx.Wind: return Mixd(Noise(.6f, .45f, 1f, .06f + v * .02f, s, .15f), Sweep(.6f, 300f, 500f * j, 0, .08f, 1f, .5f, s));
            case RSfx.Poison: return Mixd(Sweep(.3f, 300f * j, 120f, 0, .35f, 1.8f, .3f, s, .1f), Noise(.3f, .25f, 1.5f, .1f, s));
            case RSfx.Vine: return Mixd(Noise(.35f, .45f, 1.5f, .08f, s, .08f), Sweep(.35f, 150f * j, 320f, 3, .3f, 1.5f, .1f, s));
            case RSfx.Roar: return Mixd(Sweep(1.1f, 130f * j, 55f, 2, .7f, .9f, .35f, s, .06f), Noise(1f, .4f, .8f, .05f, s, .1f), Sweep(1.1f, 260f, 90f, 1, .25f, .9f, .3f));
            case RSfx.BossHit: return Mixd(Noise(.14f, .75f, 3f, .12f, s), Sweep(.2f, 180f * j, 55f, 0, .8f, 3f), Sweep(.12f, 1100f, 300f, 1, .2f, 3f, .3f));
            case RSfx.Warn: return Mixd(Sweep(.35f, 520f, 520f * j, 1, .25f, .5f), Delay(Sweep(.35f, 520f, 520f * j, 1, .25f, .5f), .4f));
            case RSfx.Shield: return Mixd(Sweep(.6f, 400f * j, 900f, 0, .35f, 1f, 0f, s, .03f), Delay(Sweep(.6f, 800f, 1800f, 3, .18f, 1.2f), .05f));
            case RSfx.ShieldBreak: return Mixd(Noise(.4f, .6f, 2f, .5f, s), Sweep(.4f, 2000f, 300f, 1, .3f, 2f, .3f));
            case RSfx.Rise: return Mixd(Noise(.8f, .5f, 1f, .05f, s, .2f), Sweep(.8f, 70f, 140f * j, 2, .45f, 1f, .2f, s));
            case RSfx.Select: return Mixd(Sweep(.25f, 200f, 800f * j, 0, .4f, 1.4f), Delay(Sweep(.4f, 800f, 1200f, 3, .3f, 1.6f), .1f), Noise(.2f, .2f, 2f, .3f, s));
            case RSfx.Reflect: return Mixd(Sweep(.5f, 2000f * j, 400f, 3, .35f, 1.6f, .1f, s), Sweep(.4f, 300f, 1200f, 0, .3f, 1.4f), Noise(.3f, .4f, 2f, .5f, s));
            case RSfx.Spike: return Mixd(Noise(.18f, .6f, 2.4f, .3f, s), Sweep(.18f, 400f * j, 120f, 2, .45f, 2.4f, .2f, s));
            case RSfx.Slam: return Mixd(Sweep(.8f, 100f * j, 30f, 0, 1f, 1.8f), Noise(.6f, .9f, 1.8f, .05f, s), Noise(.3f, .5f, 2.6f, .4f, s + 1));
            case RSfx.Teleport: return Mixd(Sweep(.3f, 1800f, 300f * j, 0, .3f, 1.4f, .1f, s, .06f), Noise(.3f, .3f, 1.6f, .3f, s, .01f));
            case RSfx.Drone: return Mixd(Sweep(.4f, 150f * j, 160f, 2, .25f, 1f, .1f, s, .05f), Sweep(.4f, 300f, 320f, 1, .1f, 1f));
            case RSfx.Zap: return Mixd(Noise(.22f, .6f, 1.8f, .6f, s, .001f), Sweep(.2f, 1600f * j, 300f, 1, .3f, 2f, .4f, s));
            case RSfx.Tick: return Mixd(Sweep(.05f, 900f * j, 700f, 0, .35f, 2f));
            case RSfx.Stinger: return Mixd(Sweep(1.4f, 220f, 220f * j, 0, .3f, .8f), Sweep(1.4f, 330f, 330f * j, 0, .25f, .8f), Sweep(1.4f, 440f, 440f * j, 3, .2f, .8f), Sweep(1.4f, 660f, 660f * j, 0, .15f, .8f));
            case RSfx.Rune: return Mixd(Sweep(.9f, 440f * (1f + v * .335f), 440f * (1f + v * .335f), 0, .4f, 1.1f, 0f, s, .01f), Sweep(.9f, 880f * (1f + v * .335f), 880f * (1f + v * .335f), 0, .2f, 1.4f), Noise(.3f, .15f, 2f, .3f, s));
            case RSfx.Swap: return Mixd(Noise(.12f, .4f, 2f, .4f, s), Sweep(.1f, 600f, 300f * j, 1, .2f, 2f));
            default: return Mixd(Sweep(.4f, 3000f * j, 800f, 0, .2f, 2.6f), Noise(.15f, .3f, 3f, .8f, s));
        }
    }

    // ---------------------------------------------------------------------- nhạc: synth pad + bass + arp + trống
    static readonly float[][] Prog =
    {
        new[] { 0f, 8f, 3f, 10f }, new[] { 0f, 5f, 8f, 7f }, new[] { 0f, 3f, 8f, 5f }, new[] { 0f, 7f, 5f, 3f }
    };

    static float Hz(float semi) => 55f * Mathf.Pow(2f, semi / 12f);

    static AudioClip BuildTrack(RTrack t)
    {
        float bpm = 80f; float root = 0f; int prog = 0; bool minor = true; int bars = 8; float arpVol = .22f, padVol = .3f, bassVol = .32f, drums = 0f; bool sparse = false;
        switch (t)
        {
            case RTrack.Intro: bpm = 60f; root = 0f; prog = 1; arpVol = .12f; padVol = .38f; bassVol = .18f; sparse = true; break;
            case RTrack.Lab: bpm = 84f; root = 0f; prog = 0; arpVol = .2f; padVol = .26f; drums = .25f; break;
            case RTrack.Bio: bpm = 78f; root = 5f; prog = 1; arpVol = .22f; padVol = .3f; drums = .2f; break;
            case RTrack.Sky: bpm = 92f; root = 7f; prog = 2; arpVol = .24f; padVol = .24f; drums = .3f; break;
            case RTrack.Boss: bpm = 132f; root = 2f; prog = 3; arpVol = .26f; padVol = .24f; bassVol = .42f; drums = .9f; break;
            case RTrack.Rest: bpm = 66f; root = 7f; prog = 1; minor = false; arpVol = .22f; padVol = .3f; bassVol = .2f; sparse = true; break;
            case RTrack.Altar: bpm = 58f; root = 0f; prog = 2; arpVol = .16f; padVol = .42f; bassVol = .22f; sparse = true; break;
            case RTrack.Ending: bpm = 70f; root = 3f; prog = 1; minor = false; bars = 10; arpVol = .3f; padVol = .4f; bassVol = .22f; break;
            case RTrack.Credits: bpm = 72f; root = 7f; prog = 0; minor = false; bars = 8; arpVol = .26f; padVol = .34f; bassVol = .2f; sparse = true; break;
        }
        float beat = 60f / bpm, barLen = beat * 4f; int n = Mathf.CeilToInt(barLen * bars * SR); var d = new float[n]; var rng = new System.Random((int)t * 77);
        var prg = Prog[prog];
        // thang âm: thứ (hoặc trưởng cho cảnh sáng)
        float[] scale = minor ? new[] { 0f, 2f, 3f, 5f, 7f, 8f, 10f } : new[] { 0f, 2f, 4f, 5f, 7f, 9f, 11f };
        for (int bar = 0; bar < bars; bar++)
        {
            float chordRoot = root + (minor ? prg[bar % 4] : MajorMap(prg[bar % 4]));
            float t0 = bar * barLen;
            // pad: 3 nốt hợp âm, vào chậm
            float[] chord = { 0f, minor ? 3f : 4f, 7f };
            for (int k = 0; k < 3; k++) AddTone(d, t0, barLen * 1.05f, Hz(chordRoot + 24f + chord[k]), padVol * .5f, 0, barLen * .35f, 1.3f, .003f * (k + 1));
            // bass
            if (!sparse || bar % 2 == 0) for (int b = 0; b < 4; b++) if (t != RTrack.Boss ? (b % 2 == 0) : true) AddTone(d, t0 + b * beat, beat * (t == RTrack.Boss ? .45f : .9f), Hz(chordRoot + 12f), bassVol, 0, .01f, t == RTrack.Boss ? 5f : 2.4f, 0f, .35f);
            // arp
            int steps = sparse ? 8 : 16; float st = barLen / steps;
            for (int i = 0; i < steps; i++)
            {
                if (sparse && rng.NextDouble() < .35) continue;
                int deg = (i * 2 + (bar % 2) * 3 + (int)(rng.NextDouble() * 2)) % 7; float semi = chordRoot + 36f + scale[deg] + ((i / 4) % 2) * 0f;
                if (i % 4 == 0) semi = chordRoot + 36f + chord[(i / 4) % 3];
                AddTone(d, t0 + i * st, st * 3f, Hz(semi), arpVol, 3, .002f, 5f, 0f);
            }
            // trống
            if (drums > 0f)
            {
                for (int b = 0; b < 4; b++)
                {
                    float tt = t0 + b * beat; if (b % 2 == 0 || t == RTrack.Boss) AddKick(d, tt, drums);
                    if (t == RTrack.Boss || b % 2 == 1) AddHat(d, tt + beat * .5f, drums * .35f, rng);
                    if (t == RTrack.Boss && b % 2 == 1) AddSnare(d, tt, drums * .6f, rng);
                }
            }
        }
        // vang: hai tap trễ
        var o = new float[n]; int d1 = (int)(beat * .75f * SR), d2 = (int)(beat * 1.5f * SR);
        for (int i = 0; i < n; i++) { float v = d[i]; if (i >= d1) v += o[i - d1] * .32f; if (i >= d2) v += d[i - d2] * .18f; o[i] = v; }
        // làm trơn mép để vòng lặp không nghe tiếng "tách"
        int fade = (int)(.05f * SR); for (int i = 0; i < fade; i++) { float k = i / (float)fade; o[i] *= k; o[n - 1 - i] *= k; }
        float mx = .001f; foreach (var v in o) mx = Mathf.Max(mx, Mathf.Abs(v)); for (int i = 0; i < n; i++) o[i] *= .8f / mx;
        var c = AudioClip.Create("mus_" + t, n, 1, SR, false); c.SetData(o, 0); return c;
    }

    static float MajorMap(float minorSemi) { return minorSemi == 3f ? 4f : minorSemi == 8f ? 9f : minorSemi == 10f ? 11f : minorSemi; }

    static void AddTone(float[] d, float start, float dur, float f, float vol, int kind, float atk, float decayPow, float detune, float sub = 0f)
    {
        int s0 = (int)(start * SR), len = (int)(dur * SR); float ph = 0f, ph2 = 0f;
        for (int i = 0; i < len && s0 + i < d.Length; i++)
        {
            float tt = i / (float)SR, k = i / (float)len; ph += f * (1f + detune) / SR; ph2 += f * .5f / SR;
            float a = Mathf.Clamp01(tt / Mathf.Max(.0005f, atk)) * Mathf.Pow(Mathf.Clamp01(1f - k), decayPow);
            float o = Osc(kind, ph); if (kind == 0) o = o * .75f + Osc(2, ph * 1.003f) * .08f; if (sub > 0f) o += Osc(0, ph2) * sub;
            d[s0 + i] += o * a * vol;
        }
    }

    static void AddKick(float[] d, float start, float vol)
    {
        int s0 = (int)(start * SR), len = (int)(.22f * SR); float ph = 0f;
        for (int i = 0; i < len && s0 + i < d.Length; i++) { float k = i / (float)len; ph += (120f * Mathf.Pow(.3f, k)) / SR * 1f; d[s0 + i] += Mathf.Sin(ph * 6.2832f * 3.3f) * Mathf.Pow(1f - k, 2.4f) * vol * .75f; }
    }

    static void AddHat(float[] d, float start, float vol, System.Random r)
    {
        int s0 = (int)(start * SR), len = (int)(.05f * SR); float y = 0f;
        for (int i = 0; i < len && s0 + i < d.Length; i++) { float n = (float)(r.NextDouble() * 2.0 - 1.0); y = n - y * .6f; d[s0 + i] += y * Mathf.Pow(1f - i / (float)len, 2f) * vol * .3f; }
    }

    static void AddSnare(float[] d, float start, float vol, System.Random r)
    {
        int s0 = (int)(start * SR), len = (int)(.14f * SR);
        for (int i = 0; i < len && s0 + i < d.Length; i++) { float n = (float)(r.NextDouble() * 2.0 - 1.0); d[s0 + i] += (n * .6f + Mathf.Sin(i / (float)SR * 6.2832f * 190f) * .4f) * Mathf.Pow(1f - i / (float)len, 2f) * vol * .5f; }
    }
}
