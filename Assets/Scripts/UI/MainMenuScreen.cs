using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Màn hình chính "Màn Sương Lãng Quên" v2: rừng khô + tàn tích dưới trăng xanh, hai đài đuốc gargoyle,
/// hiệp sĩ giáp xám (thở, có hơi thở), tiêu đề kim loại, menu trong khung khắc rune.
/// Toàn bộ tranh là pixel art nằm ở Assets/Resources/MainMenu (480x270 phóng 4 lần, Point filter).
/// Cách dùng: scene MainMenu -> Tools > KHTN 8 > "Áp MainMenu mới (dark fantasy)". Giao diện cũ chỉ bị ẩn lúc chạy.
/// </summary>
public class MainMenuScreen : MonoBehaviour
{
    const string MutedKey = "KHTN_Muted";
    // Đổi khối lớp hiển thị tại đây (dữ liệu bài học hiện tại của project là KHTN 8).
    const string GradeLabel = "LỚP 6";
    const string Kicker = "KHOA HỌC TỰ NHIÊN  •  " + GradeLabel;
    const string Tagline = "Mỗi điều bạn biết là một ngọn đèn giữa màn sương.";
    const float SceneW = 1920f, SceneH = 1080f;
    const float GroundY = -404f;            // chân tượng/hiệp sĩ trong toạ độ cảnh (gốc ở giữa màn hình)

    class Drift { public RectTransform rt; public float amp, speed, phase; }
    class Puff { public RectTransform rt; public Image im; public float age, dur, vx, vy; }

    RectTransform artRoot, scene, ui, settings;
    Text soundLabel;
    bool muted;

    readonly List<Drift> drifts = new List<Drift>();
    readonly List<Puff> puffs = new List<Puff>();
    Sprite[] knightFrames, flameFrames, puffSprites;
    Image knight, moonGlow, moonGlowTight, moonDisc;
    MenuNatureFx nature;
    RectTransform knightRt;
    readonly Image[] flame = new Image[2];
    readonly Image[] flameHalo = new Image[2];
    int lastKnight = -1;
    Vector2 lastRect;

    // ------------------------------------------------------------------ khởi tạo
    void Start()
    {
        Time.timeScale = 1f;
        HideLegacyUi();

        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = GameTheme.Ink; }

        UIKit.EnsureEventSystem();
        SceneShell.Create(ShellMood.Hub, null, 1f, true, false);   // vignette + tàn lửa phía trên tranh + fade

        artRoot = UIKit.BuildCanvas(transform, "MainMenu Art", -90);
        scene = UIKit.CBox("Scene", artRoot, Color.clear, Vector2.zero, new Vector2(SceneW, SceneH));
        BuildScene();

        ui = UIKit.BuildCanvas(transform, "MainMenu UI", 10);
        BuildTitle();
        BuildMenu();
        BuildFooter();

        muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
        ApplySound();
        FitScene();
    }

    static void HideLegacyUi()
    {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ cảnh nền (tranh pixel)
    Image Img(string n, Sprite sp, Vector2 pos, Vector2 size, Color c)
    {
        if (sp == null) return null;
        var rt = UIKit.CBox(n, scene, c, pos, size);
        var im = rt.GetComponent<Image>(); im.sprite = sp;
        return im;
    }

    RectTransform Layer(string n, string res, float amp, float speed)
    {
        var sp = MenuArt.Get(res); if (sp == null) return null;
        var rt = UIKit.CBox(n, scene, Color.white, Vector2.zero, Vector2.zero);
        UIKit.Stretch(rt, 0, 0, 0, 0);
        rt.GetComponent<Image>().sprite = sp;
        if (amp > 0f)
        {
            rt.localScale = Vector3.one * 1.04f;      // dư lề để trôi nhẹ mà không lộ mép
            drifts.Add(new Drift { rt = rt, amp = amp, speed = speed, phase = Random.value * 6f });
        }
        return rt;
    }

    void Fog(string n, float y, float alpha, float speed, float phase)
    {
        var im = Img(n, ThemeGfx.Glow, new Vector2(0, y), new Vector2(2600, 520), new Color(.6f, .75f, .95f, alpha));
        if (im != null) drifts.Add(new Drift { rt = im.rectTransform, amp = 180f, speed = speed, phase = phase });
    }

    Sprite[] Frames(string prefix, int n)
    {
        var a = new Sprite[n];
        for (int i = 0; i < n; i++) { a[i] = MenuArt.Get(prefix + i); if (a[i] == null) return null; }
        return a;
    }

    void BuildScene()
    {
        knightFrames = Frames("px_knight_", 4);
        flameFrames = Frames("px_flame_", 4);
        puffSprites = Frames("px_puff_", 3);

        nature = new MenuNatureFx();
        Layer("Sky", "px_sky", 0f, 0f);
        // vầng trăng + ánh xanh lạnh toả ra
        moonGlow = Img("MoonGlowWide", ThemeGfx.Glow, new Vector2(0, 148), new Vector2(1900, 1900), new Color(.35f, .6f, 1f, .38f));
        moonGlowTight = Img("MoonGlowTight", ThemeGfx.Glow, new Vector2(0, 148), new Vector2(1000, 1000), new Color(.7f, .85f, 1f, .30f));
        moonDisc = Img("Moon", MenuArt.Get("px_moon"), new Vector2(0, 148), new Vector2(624, 624), Color.white);

        // Cây KHÔNG trôi cả cây nữa: tán lay bằng uốn mesh + thêm cành con đung đưa riêng từng cành.
        var far = Layer("TreesFar", "px_trees_far", 0f, 0f);
        if (far != null) nature.Warp(far, 150f, 195f, 0f, 0f, 4f, 0f);
        Fog("FogA", 40, .10f, .05f, 0f);
        Layer("Ruins", "px_ruins", 10f, .07f);
        var mid = Layer("TreesMid", "px_trees_mid", 0f, 0f);
        if (mid != null) { nature.Warp(mid, 130f, 168f, 0f, 0f, 5f, 1.7f); nature.AddBranches(scene, false); }
        Fog("FogB", -170, .12f, .04f, 2f);
        var nearT = Layer("TreesNear", "px_trees_near", 0f, 0f);
        if (nearT != null) { nature.Warp(nearT, 70f, 140f, 45f, 100f, 8f, 3.1f); nature.AddBranches(scene, true); }
        nature.AddLeaves(scene);
        Layer("Ground", "px_ground", 0f, 0f);
        nature.AddGrass(scene, false);
        Fog("FogC", -400, .13f, .03f, 4f);

        BuildStatue(-560f, 0);
        BuildStatue(560f, 1);
        BuildKnight();
        nature.AddGrass(scene, true);
    }

    void BuildStatue(float x, int i)
    {
        Img("TorchGlow" + i, ThemeGfx.Glow, new Vector2(x, 60), new Vector2(900, 900), new Color(1f, .55f, .18f, .30f));
        Img("StatueShadow" + i, ThemeGfx.Glow, new Vector2(x, GroundY), new Vector2(380, 60), new Color(0, 0, 0, .6f));
        var st = Img("Statue" + i, MenuArt.Get("px_statue"), new Vector2(x, -156), new Vector2(256, 496), Color.white);
        if (st != null && i == 0) st.rectTransform.localScale = new Vector3(-1f, 1f, 1f);   // tượng trái lật để mặt sáng quay về phía trăng
        if (flameFrames == null) return;
        flameHalo[i] = Img("FlameHalo" + i, ThemeGfx.Glow, new Vector2(x, 70), new Vector2(360, 360), new Color(1f, .6f, .2f, .55f));
        flame[i] = Img("Flame" + i, flameFrames[0], new Vector2(x, 84), new Vector2(64, 112), Color.white);
    }

    void BuildKnight()
    {
        if (knightFrames == null) return;
        Img("KnightShadow", ThemeGfx.Glow, new Vector2(-790, GroundY - 8), new Vector2(260, 52), new Color(0, 0, 0, .6f));
        knight = Img("Knight", knightFrames[0], new Vector2(-790, -244), new Vector2(192, 336), Color.white);
        if (knight != null) knightRt = knight.rectTransform;
    }

    // ------------------------------------------------------------------ tiêu đề, menu, chân trang
    void BuildTitle()
    {
        UIKit.Label("Kicker", ui, Kicker, 28, new Color(.67f, .75f, .9f, .9f), TextAnchor.MiddleCenter,
            new Vector2(0, 478), new Vector2(1200, 44), true, true);

        var ts = MenuArt.Get("sm_title");
        if (ts != null)
        {
            const float w = 1380f;
            var rt = UIKit.CBox("TitleArt", ui, Color.white, new Vector2(0, 390), new Vector2(w, w * ts.rect.height / ts.rect.width));
            rt.GetComponent<Image>().sprite = ts;
        }
        else UIKit.Title(ui, "MÀN SƯƠNG LÃNG QUÊN", new Vector2(0, 390), 96);

        // lớp tối mờ sau khẩu hiệu để chữ luôn đọc được trên nền trăng sáng
        var shade = UIKit.CBox("TaglineShade", ui, new Color(.02f, .04f, .10f, .6f), new Vector2(0, 290), new Vector2(1500, 170));
        shade.GetComponent<Image>().sprite = ThemeGfx.Glow;
        UIKit.Label("Tagline", ui, Tagline, 34, new Color(.86f, .91f, 1f, .98f), TextAnchor.MiddleCenter,
            new Vector2(0, 290), new Vector2(1400, 50), true, true, FontStyle.Italic);
    }

    void BuildMenu()
    {
        var fs = MenuArt.Get("px_frame");
        if (fs != null)
        {
            var rt = UIKit.CBox("MenuFrame", ui, Color.white, new Vector2(0, -20), new Vector2(600, 520), true);
            rt.GetComponent<Image>().sprite = fs;
        }
        else UIKit.Panel(ui, "MenuFrame", new Vector2(0, -20), new Vector2(600, 520), PanelStyle.Dark, out _);

        UIKit.MakeButton(ui, "Btn_Start", "BẮT ĐẦU", new Vector2(0, 120), new Vector2(440, 88),
            () => SceneRouter.Go("ModeSelectScene"), ButtonStyle.Bronze, 42, 0f);
        UIKit.MakeButton(ui, "Btn_Dashboard", "TIẾN ĐỘ", new Vector2(0, 28), new Vector2(440, 72),
            OpenDashboard, ButtonStyle.Stone, 34, .08f);
        UIKit.MakeButton(ui, "Btn_Settings", "CÀI ĐẶT", new Vector2(0, -56), new Vector2(440, 72),
            OpenSettings, ButtonStyle.Stone, 34, .16f);
        UIKit.MakeButton(ui, "Btn_Exit", "THOÁT", new Vector2(0, -140), new Vector2(440, 72),
            Quit, ButtonStyle.Stone, 34, .24f);
    }

    void BuildFooter()
    {
        string n = StudentProfileData.studentName;
        if (string.IsNullOrWhiteSpace(n)) n = "Học sinh";
        UIKit.Label("Greeting", ui, "Người Giữ Ký Ức: " + n, 28, GameTheme.Bone, TextAnchor.MiddleLeft,
            new Vector2(-560, -490), new Vector2(700, 44), false, true);
        UIKit.Label("Version", ui, "Bản thử nghiệm · Giai đoạn 0", 24, GameTheme.Fog, TextAnchor.MiddleRight,
            new Vector2(560, -490), new Vector2(700, 44), false, true);
    }

    // ------------------------------------------------------------------ hành vi
    void OpenDashboard()
    {
        if (!Application.CanStreamedLevelBeLoaded("DashboardScene"))
        {
            UIKit.Toast(ui, "Chưa có DashboardScene trong Build Settings");
            return;
        }
        SceneRouter.Go("DashboardScene");
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Cửa sổ Cài đặt dựng mới mỗi lần mở và huỷ khi đóng (nút có coroutine xuất hiện dần nên không dùng SetActive).
    void OpenSettings()
    {
        if (settings != null) return;
        var dim = UIKit.Box("SettingsDim", ui, new Color(0, 0, 0, .7f), Vector2.zero, Vector2.one, UIKit.C, Vector2.zero, Vector2.zero, true);
        UIKit.Stretch(dim, 0, 0, 0, 0);
        settings = dim;

        UIKit.Panel(dim, "SettingsPanel", Vector2.zero, new Vector2(760, 460), PanelStyle.Dark, out var c);
        UIKit.Label("T", c, "Cài đặt", 52, GameTheme.GoldHi, TextAnchor.MiddleCenter, new Vector2(0, 150), new Vector2(660, 70), true, true, FontStyle.Bold);
        UIKit.Divider(c, new Vector2(0, 100), 420);
        var snd = UIKit.MakeButton(c, "Btn_Sound", "", new Vector2(0, 10), new Vector2(520, 84), ToggleSound, ButtonStyle.Stone, 32, 0f);
        soundLabel = snd.GetComponentInChildren<Text>();
        UIKit.MakeButton(c, "Btn_Close", "ĐÓNG", new Vector2(0, -110), new Vector2(320, 84), CloseSettings, ButtonStyle.Bronze, 34, .08f);
        ApplySound();
    }

    void CloseSettings()
    {
        if (settings == null) return;
        Destroy(settings.gameObject);
        settings = null; soundLabel = null;
    }

    void ToggleSound()
    {
        muted = !muted;
        PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
        PlayerPrefs.Save();
        ApplySound();
    }

    void ApplySound()
    {
        AudioListener.volume = muted ? 0f : 1f;
        if (soundLabel != null) soundLabel.text = muted ? "ÂM THANH: TẮT" : "ÂM THANH: BẬT";
    }

    // ------------------------------------------------------------------ hoạt ảnh
    /// <summary>Cảnh 1920x1080 phủ kín màn hình theo kiểu "cover" (giữ tỉ lệ, cắt phần thừa).</summary>
    void FitScene()
    {
        if (artRoot == null || scene == null) return;
        var r = artRoot.rect.size;
        if (r.x <= 1f || r == lastRect) return;
        lastRect = r;
        float s = Mathf.Max(r.x / SceneW, r.y / SceneH);
        scene.localScale = new Vector3(s, s, 1f);
    }

    void Update()
    {
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        FitScene();

        foreach (var d in drifts)
        {
            var p = d.rt.anchoredPosition; p.x = Mathf.Sin(t * d.speed * 6.283f + d.phase) * d.amp; d.rt.anchoredPosition = p;
        }
        // Ánh trăng nhấp nháy nhẹ: nhịp chậm + nhiễu nhanh, hai quầng lệch pha nhau.
        float fl = Mathf.PerlinNoise(t * 1.3f, 0.5f) - .5f, fl2 = Mathf.PerlinNoise(t * 1.9f, 8.1f) - .5f;
        if (moonGlow != null) { var c = moonGlow.color; c.a = .38f + .04f * Mathf.Sin(t * .5f) + .10f * fl; moonGlow.color = c; }
        if (moonGlowTight != null) { var c = moonGlowTight.color; c.a = .30f + .03f * Mathf.Sin(t * .8f + 1f) + .12f * fl2; moonGlowTight.color = c; }
        if (moonDisc != null) { float b = .94f + .10f * fl2; moonDisc.color = new Color(b, b, b, 1f); }
        if (nature != null) nature.Tick(t, dt);

        if (flameFrames != null)
            for (int i = 0; i < 2; i++)
            {
                if (flame[i] == null) continue;
                flame[i].sprite = flameFrames[((int)(t * 9f) + i * 2) % 4];
                if (flameHalo[i] != null)
                {
                    float f = .75f + .25f * Mathf.PerlinNoise(t * 3.2f, i * 7.7f);
                    flameHalo[i].color = new Color(1f, .6f, .2f, .55f * f);
                }
            }

        if (knight != null && knightFrames != null)
        {
            int idx = ((int)(t / .7f)) % 4;               // hít vào - giữ - thở ra - nghỉ
            if (idx != lastKnight)
            {
                knight.sprite = knightFrames[idx];
                if (idx == 2) SpawnPuff();                // hơi thở phả ra lúc thở ra
                lastKnight = idx;
            }
        }
        UpdatePuffs(dt);

        if (settings != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseSettings();
    }

    void OnDestroy() { if (nature != null) nature.Dispose(); }

    void SpawnPuff()
    {
        if (puffSprites == null || knightRt == null) return;
        var sp = puffSprites[Random.Range(0, puffSprites.Length)];
        var rt = UIKit.CBox("Puff", scene, Color.white, knightRt.anchoredPosition + new Vector2(2f, 44f),
            new Vector2(sp.rect.width * 4f, sp.rect.height * 4f));
        var im = rt.GetComponent<Image>(); im.sprite = sp;
        puffs.Add(new Puff { rt = rt, im = im, dur = 1.9f, vx = Random.Range(10f, 32f), vy = Random.Range(36f, 60f) });
    }

    void UpdatePuffs(float dt)
    {
        for (int i = puffs.Count - 1; i >= 0; i--)
        {
            var p = puffs[i]; p.age += dt;
            float k = p.age / p.dur;
            if (k >= 1f) { Destroy(p.rt.gameObject); puffs.RemoveAt(i); continue; }
            p.rt.anchoredPosition += new Vector2(p.vx, p.vy) * dt;
            p.rt.localScale = Vector3.one * (1f + 1.2f * k);
            var c = p.im.color; c.a = .9f * Mathf.Pow(1f - k, 1.3f); p.im.color = c;
        }
    }
}

/// <summary>Nạp sprite từ Assets/Resources/MainMenu, có cache và cảnh báo một lần nếu thiếu.</summary>
static class MenuArt
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    static bool warned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { cache.Clear(); warned = false; }

    public static Sprite Get(string name)
    {
        Sprite s;
        if (cache.TryGetValue(name, out s)) return s;
        s = Resources.Load<Sprite>("MainMenu/" + name);
        if (s == null && !warned)
        {
            warned = true;
            Debug.LogWarning("[MainMenu] Thiếu sprite Resources/MainMenu/" + name + " (và có thể các file khác). Hãy chép đủ thư mục Assets/Resources/MainMenu.");
        }
        cache[name] = s;
        return s;
    }
}
