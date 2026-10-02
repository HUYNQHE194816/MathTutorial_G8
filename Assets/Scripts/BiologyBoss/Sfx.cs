using System.Collections.Generic;
using UnityEngine;

/// <summary>Âm thanh tổng hợp bằng code (không cần file). Có thể gán clip riêng cho đúng/sai.</summary>
public static class Sfx
{
    static AudioSource src;
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    public static AudioClip CorrectClip, WrongClip;

    static void Ensure()
    {
        if (src != null) return;
        var go = new GameObject("Sfx"); src = go.AddComponent<AudioSource>(); src.spatialBlend = 0f;
    }

    /// <param name="wave">0 vuông, 1 răng cưa, 2 tam giác, 3 sin, 4 nhiễu</param>
    public static void Tone(float f, float d = .1f, int wave = 0, float vol = .25f, float slide = 0f)
    {
        Ensure();
        string k = f + "|" + d + "|" + wave + "|" + slide;
        if (!cache.TryGetValue(k, out var c) || c == null)
        {
            int sr = 22050, n = Mathf.Max(1, (int)(sr * d)); var s = new float[n]; float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n; ph += Mathf.Max(30f, f + slide * t) / sr; float p = ph % 1f, v;
                switch (wave) { case 0: v = p < .5f ? 1f : -1f; break; case 1: v = p * 2f - 1f; break; case 2: v = Mathf.Abs(p * 4f - 2f) - 1f; break; case 3: v = Mathf.Sin(p * 6.2832f); break; default: v = Random.value * 2f - 1f; break; }
                s[i] = v * (1f - t) * (1f - t) * .5f;
            }
            c = AudioClip.Create(k, n, 1, sr, false); c.SetData(s, 0); cache[k] = c;
        }
        src.PlayOneShot(c, vol);
    }

    public static void Boom() { Tone(90, .4f, 4, .3f, -50); Tone(70, .35f, 1, .2f, -30); }
    public static void Correct() { if (CorrectClip != null) { Ensure(); src.PlayOneShot(CorrectClip); } else { Tone(700, .3f, 2, .3f, 400); } }
    public static void Wrong() { if (WrongClip != null) { Ensure(); src.PlayOneShot(WrongClip); } else { Tone(90, .35f, 1, .3f, -40); } }
}
