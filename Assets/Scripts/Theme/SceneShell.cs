using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum ShellMood { Menu, Hub, Study }

/// <summary>
/// "Vỏ" dùng chung cho mọi scene menu: nền gradient tối (hoặc ảnh nền được phủ tối), sương trôi, tàn lửa bay,
/// quầng sáng đuốc nhấp nháy, vignette, và hiệu ứng fade khi vào/ra scene.
/// Cách dùng: SceneShell.Create(ShellMood.Menu) trong Start() của scene (hoặc gắn component lên 1 GameObject).
/// Thứ tự canvas: BG = -100 (nền) | Vignette = 5 | UI của scene PHẢI có sortingOrder >= 10 | Fade = 1000.
/// </summary>
public class SceneShell : MonoBehaviour
{
    public static SceneShell Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Current = null; }

    [SerializeField] ShellMood mood = ShellMood.Menu;
    [SerializeField] Sprite backdrop;
    [SerializeField, Range(0f, 1.5f)] float intensity = 1f;
    [SerializeField] bool fadeInOnStart = true;

    class Ember { public RectTransform rt; public Image img; public float x0, speed, sway, phase, life, age, a; }
    class Drift { public RectTransform rt; public Image img; public float baseX, amp, speed, phase, a; }
    class Torch { public Image img; public float a, seed; }

    readonly List<Ember> embers = new List<Ember>();
    readonly List<Drift> fogs = new List<Drift>();
    readonly List<Torch> torches = new List<Torch>();
    RectTransform bgRoot; Image fade; Coroutine fadeCo; float clock;
    const float EmberStartY = -580f;

    public static SceneShell Create(ShellMood mood = ShellMood.Menu, Sprite backdrop = null, float intensity = 1f)
    {
        var go = new GameObject("SceneShell");
        var s = go.AddComponent<SceneShell>();   // Awake chạy ngay, Start (dựng nền) chạy sau khi gán xong tham số
        s.mood = mood; s.backdrop = backdrop; s.intensity = intensity;
        return s;
    }

    void Awake()
    {
        if (Current != null && Current != this) Destroy(Current.gameObject);
        Current = this;
    }

    void OnDestroy() { if (Current == this) Current = null; }

    void Start()
    {
        Build();
        if (fadeInOnStart) { SetFade(1f); FadeIn(); } else SetFade(0f);
    }

    void Build()
    {
        int emberCount; float fogA;
        switch (mood)
        {
            case ShellMood.Hub: emberCount = 60; fogA = .13f; break;
            case ShellMood.Study: emberCount = 24; fogA = .08f; break;
            default: emberCount = 48; fogA = .16f; break;
        }
        emberCount = Mathf.RoundToInt(emberCount * intensity);

        // ---- Canvas nền ----
        bgRoot = UIKit.BuildCanvas(transform, "Shell_BG", -100);
        var grad = UIKit.Box("Gradient", bgRoot, Color.white, Vector2.zero, Vector2.one, UIKit.C, Vector2.zero, Vector2.zero);
        UIKit.Stretch(grad, 0, 0, 0, 0);
        grad.GetComponent<Image>().sprite = ThemeGfx.Gradient;

        if (backdrop != null)
        {
            var bd = UIKit.Box("Backdrop", bgRoot, new Color(.42f, .36f, .55f, 1f), UIKit.C, UIKit.C, UIKit.C, Vector2.zero, Vector2.zero);
            var im = bd.GetComponent<Image>(); im.sprite = backdrop; im.preserveAspect = false;
            var fit = bd.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = backdrop.rect.width / Mathf.Max(1f, backdrop.rect.height);
            var dim = UIKit.Box("Dim", bgRoot, GameTheme.Ink.WithAlpha(.55f), Vector2.zero, Vector2.one, UIKit.C, Vector2.zero, Vector2.zero);
            UIKit.Stretch(dim, 0, 0, 0, 0);
        }

        // ---- Sương trôi ----
        for (int i = 0; i < 3; i++)
        {
            var r = UIKit.CBox("Fog" + i, bgRoot, GameTheme.Fog.WithAlpha(fogA * intensity), new Vector2(0, -280 + i * 240), new Vector2(2200, 760));
            var im = r.GetComponent<Image>(); im.sprite = ThemeGfx.Glow;
            fogs.Add(new Drift { rt = r, img = im, baseX = (i - 1) * 220f, amp = 140f + i * 40f, speed = .05f + .03f * i, phase = i * 2.1f, a = fogA });
        }

        // ---- Quầng sáng đuốc hai góc dưới ----
        for (int i = 0; i < 2; i++)
        {
            var r = UIKit.CBox("Torch" + i, bgRoot, GameTheme.Ember.WithAlpha(.22f * intensity), new Vector2(i == 0 ? -820 : 820, -420), new Vector2(1000, 700));
            var im = r.GetComponent<Image>(); im.sprite = ThemeGfx.Glow;
            torches.Add(new Torch { img = im, a = .22f, seed = i * 7.3f });
        }

        // ---- Tàn lửa ----
        for (int i = 0; i < emberCount; i++)
        {
            var r = UIKit.CBox("Ember", bgRoot, GameTheme.EmberHi, Vector2.zero, new Vector2(4, 4));
            var e = new Ember { rt = r, img = r.GetComponent<Image>() };
            Respawn(e, true); embers.Add(e);
        }

        // ---- Vignette (nằm dưới UI) ----
        var vc = UIKit.BuildCanvas(transform, "Shell_Vignette", 5);
        var v = UIKit.Box("Vignette", vc, Color.white, Vector2.zero, Vector2.one, UIKit.C, Vector2.zero, Vector2.zero);
        UIKit.Stretch(v, 0, 0, 0, 0);
        v.GetComponent<Image>().sprite = ThemeGfx.Vignette;

        // ---- Fade (trên cùng) ----
        var fc = UIKit.BuildCanvas(transform, "Shell_Fade", 1000);
        var f = UIKit.Box("Fade", fc, GameTheme.Ink, Vector2.zero, Vector2.one, UIKit.C, Vector2.zero, Vector2.zero);
        UIKit.Stretch(f, 0, 0, 0, 0);
        fade = f.GetComponent<Image>();
    }

    static void Respawn(Ember e, bool scatter)
    {
        e.x0 = UnityEngine.Random.Range(-980f, 980f);
        e.speed = UnityEngine.Random.Range(28f, 80f);
        e.sway = UnityEngine.Random.Range(.6f, 1.6f);
        e.phase = UnityEngine.Random.Range(0f, 6.28f);
        e.life = UnityEngine.Random.Range(7f, 14f);
        e.age = scatter ? UnityEngine.Random.Range(0f, e.life) : 0f;
        e.a = UnityEngine.Random.Range(.55f, 1f);
        float s = UnityEngine.Random.Range(4f, 9f);
        e.rt.sizeDelta = new Vector2(s, s);
    }

    static void SetAlpha(Image im, float a) { var c = im.color; c.a = a; im.color = c; }

    void Update()
    {
        if (bgRoot == null) return;
        float dt = Time.unscaledDeltaTime; clock += dt;

        foreach (var f in fogs)
        {
            var p = f.rt.anchoredPosition; p.x = f.baseX + Mathf.Sin(clock * f.speed * 6.28f + f.phase) * f.amp; f.rt.anchoredPosition = p;
            SetAlpha(f.img, f.a * intensity * (.75f + .25f * Mathf.Sin(clock * .3f + f.phase)));
        }
        foreach (var t in torches)
            SetAlpha(t.img, t.a * intensity * (.7f + .3f * Mathf.PerlinNoise(clock * 2.5f, t.seed)));

        foreach (var e in embers)
        {
            e.age += dt;
            if (e.age >= e.life) Respawn(e, false);
            float y = EmberStartY + e.speed * e.age;
            float x = e.x0 + Mathf.Sin(e.phase + e.age * e.sway) * 22f;
            e.rt.anchoredPosition = new Vector2(x, y);
            SetAlpha(e.img, e.a * Mathf.Sin(Mathf.PI * Mathf.Clamp01(e.age / e.life)));
        }
    }

    // ---------- Fade ----------
    void SetFade(float a) { if (fade != null) { SetAlpha(fade, a); fade.raycastTarget = a > .01f; } }

    public void FadeIn(float dur = .5f) { StartFade(1f, 0f, dur, null); }
    public void FadeOutThen(Action done, float dur = .35f) { StartFade(0f, 1f, dur, done); }

    void StartFade(float from, float to, float dur, Action done)
    {
        if (fadeCo != null) StopCoroutine(fadeCo);
        fadeCo = StartCoroutine(FadeRoutine(from, to, dur, done));
    }

    IEnumerator FadeRoutine(float from, float to, float dur, Action done)
    {
        if (fade != null) fade.raycastTarget = true;   // chặn bấm trong lúc chuyển cảnh
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            SetFade(Mathf.SmoothStep(from, to, t / dur));
            if (fade != null) fade.raycastTarget = true;
            yield return null;
        }
        SetFade(to);
        fadeCo = null;
        if (done != null) done();
    }
}

/// <summary>Chuyển scene có fade (nếu scene có SceneShell). Thay cho SceneManager.LoadScene rải rác.</summary>
public static class SceneRouter
{
    static bool busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { busy = false; }

    public static void Go(string sceneName)
    {
        if (busy) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("[SceneRouter] Scene '" + sceneName + "' chưa có trong Build Settings (File > Build Profiles > Scene List).");
            return;
        }
        var shell = SceneShell.Current;
        if (shell == null) { Load(sceneName); return; }
        busy = true;
        shell.FadeOutThen(() => { busy = false; Load(sceneName); });
    }

    static void Load(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
