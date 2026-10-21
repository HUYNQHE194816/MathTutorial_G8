using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cài đặt chung của game, lưu bằng PlayerPrefs và tự áp dụng khi mở game (mọi scene dùng chung).
/// Âm lượng tổng / nhạc / hiệu ứng, độ sáng, và cấu hình: chất lượng đồ họa, toàn màn hình, giới hạn FPS, VSync.
/// Font chữ + cỡ chữ nằm ở GameFont. Giao diện chỉnh: SettingsWindow (mở từ MainMenu > CÀI ĐẶT).
/// Cấu hình (chất lượng/FPS/VSync/toàn màn hình) chỉ ghi đè khi người chơi đã chỉnh; chưa chỉnh thì giữ mặc định của project.
/// </summary>
public static class GameSettings
{
    const string KMuted = "KHTN_Muted";            // cùng khoá cũ của nút Âm thanh ở MainMenu
    const string KMaster = "KHTN_VolMaster", KMusic = "KHTN_VolMusic", KSfx = "KHTN_VolSfx";
    const string KBright = "KHTN_Brightness";
    const string KFull = "KHTN_Fullscreen", KQuality = "KHTN_Quality", KFps = "KHTN_FpsIdx", KVsync = "KHTN_Vsync", KRes = "KHTN_ResIdx";

    public const float BrightMin = .5f, BrightMax = 1.25f;
    public static readonly int[] FpsOptions = { 30, 60, 120, -1 };
    public static readonly string[] FpsNames = { "30 FPS", "60 FPS", "120 FPS", "KHÔNG GIỚI HẠN" };

    // ---------- Độ phân giải (danh sách giống Genshin Impact) ----------
    public struct ResOption { public int w, h; public bool window; public string name; }
    /// <summary>Mục 0 = "Không viền" (theo độ phân giải màn hình máy); các mục sau là chế độ cửa sổ. Chỉ giữ mục nhỏ hơn hoặc bằng màn hình.</summary>
    public static readonly List<ResOption> Resolutions = new List<ResOption>();
    static readonly int[] WinW = { 1680, 1600, 1440, 1400, 1366, 1360, 1280, 1280, 1280, 1280, 1280, 1280, 1152, 1024, 800, 640 };
    static readonly int[] WinH = { 1050, 900, 900, 1050, 768, 768, 1024, 960, 800, 768, 720, 600, 864, 768, 600, 480 };

    static void BuildResolutions()
    {
        Resolutions.Clear();
        int sw = Mathf.Max(640, Display.main.systemWidth), sh = Mathf.Max(480, Display.main.systemHeight);
        Resolutions.Add(new ResOption { w = sw, h = sh, window = false, name = sw + "×" + sh + " Không viền" });
        for (int i = 0; i < WinW.Length; i++)
            if (WinW[i] <= sw && WinH[i] <= sh)
                Resolutions.Add(new ResOption { w = WinW[i], h = WinH[i], window = true, name = WinW[i] + "×" + WinH[i] + " Cửa Sổ" });
    }

    static float master = 1f, music = 1f, sfx = 1f, brightness = 1f;
    static bool muted;
    static int bootQuality, bootFpsIdx, bootFull, bootVsync, bootRes;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        master = PlayerPrefs.GetFloat(KMaster, 1f);
        music = PlayerPrefs.GetFloat(KMusic, 1f);
        sfx = PlayerPrefs.GetFloat(KSfx, 1f);
        brightness = Mathf.Clamp(PlayerPrefs.GetFloat(KBright, 1f), BrightMin, BrightMax);
        muted = PlayerPrefs.GetInt(KMuted, 0) == 1;

        // ghi nhớ mặc định của project để nút "MẶC ĐỊNH" khôi phục đúng
        bootQuality = QualitySettings.GetQualityLevel();
        bootFpsIdx = FpsIndexOf(Application.targetFrameRate);
        bootFull = Screen.fullScreen ? 1 : 0;
        bootVsync = QualitySettings.vSyncCount > 0 ? 1 : 0;
        BuildResolutions();
        bootRes = Screen.fullScreenMode != FullScreenMode.Windowed ? 0 : ResIndexOfCurrent();

        ApplyAudio();
        if (PlayerPrefs.HasKey(KQuality)) QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(KQuality), 0, QualitySettings.names.Length - 1), true);
        if (PlayerPrefs.HasKey(KVsync)) QualitySettings.vSyncCount = PlayerPrefs.GetInt(KVsync) == 1 ? 1 : 0;
        if (PlayerPrefs.HasKey(KFps)) Application.targetFrameRate = FpsOptions[Mathf.Clamp(PlayerPrefs.GetInt(KFps), 0, FpsOptions.Length - 1)];
        if (PlayerPrefs.HasKey(KRes)) ApplyResolution(ResIdx);
        else if (PlayerPrefs.HasKey(KFull)) ApplyFullscreen(PlayerPrefs.GetInt(KFull) == 1);   // tương thích bản cũ
        BrightnessOverlay.Ensure();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (sc, mode) => BrightnessOverlay.Ensure();   // đảm bảo lớp phủ độ sáng luôn còn sau mỗi lần đổi scene
    }

    static int ResIndexOfCurrent()
    {
        for (int i = 1; i < Resolutions.Count; i++)
            if (Resolutions[i].w == Screen.width && Resolutions[i].h == Screen.height) return i;
        return 0;
    }

    static void ApplyResolution(int idx)
    {
        if (Resolutions.Count == 0) BuildResolutions();
        var r = Resolutions[Mathf.Clamp(idx, 0, Resolutions.Count - 1)];
        // Lưu ý: trong Unity Editor lệnh này không đổi cửa sổ Game view, chỉ có tác dụng trong bản Build.
        Screen.SetResolution(r.w, r.h, r.window ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
    }

    /// <summary>Đặt lại giới hạn FPS + VSync. Khi VSync bật thì Unity BỎ QUA Application.targetFrameRate, nên 2 thứ này phải áp dụng cùng nhau.</summary>
    static void ApplyFrameRate()
    {
        if (PlayerPrefs.HasKey(KVsync)) QualitySettings.vSyncCount = PlayerPrefs.GetInt(KVsync) == 1 ? 1 : 0;
        if (PlayerPrefs.HasKey(KFps)) Application.targetFrameRate = FpsOptions[Mathf.Clamp(PlayerPrefs.GetInt(KFps), 0, FpsOptions.Length - 1)];
    }

    static int FpsIndexOf(int target)
    {
        for (int i = 0; i < FpsOptions.Length; i++) if (FpsOptions[i] == target) return i;
        return FpsOptions.Length - 1;
    }

    static void ApplyFullscreen(bool on)
    {
        Screen.fullScreenMode = on ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    // ---------- Âm thanh ----------
    public static float Master { get { return master; } set { master = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KMaster, master); ApplyAudio(); } }
    public static float Music { get { return music; } set { music = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KMusic, music); } }
    public static float Sfx { get { return sfx; } set { sfx = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KSfx, sfx); } }
    public static bool Muted { get { return muted; } set { muted = value; PlayerPrefs.SetInt(KMuted, value ? 1 : 0); ApplyAudio(); } }

    public static void ApplyAudio() { AudioListener.volume = muted ? 0f : master; }

    // ---------- Độ sáng ----------
    public static float Brightness
    {
        get { return brightness; }
        set { brightness = Mathf.Clamp(value, BrightMin, BrightMax); PlayerPrefs.SetFloat(KBright, brightness); BrightnessOverlay.Ensure(); }
    }

    // ---------- Bí danh (lưu riêng theo từng tài khoản Tên + Lớp) ----------
    static string NickKey
    {
        get { return "KHTN_Nick_" + StudentProfileData.studentName + "_" + StudentProfileData.studentClass; }
    }

    /// <summary>Bí danh hiển thị. Chưa đặt thì dùng tên đăng nhập.</summary>
    public static string Nickname
    {
        get
        {
            string n = PlayerPrefs.GetString(NickKey, "");
            return string.IsNullOrWhiteSpace(n) ? StudentProfileData.studentName : n;
        }
        set { PlayerPrefs.SetString(NickKey, (value ?? "").Trim()); }
    }

    // ---------- Cấu hình ----------
    public static int QualityLevel
    {
        get { return PlayerPrefs.HasKey(KQuality) ? Mathf.Clamp(PlayerPrefs.GetInt(KQuality), 0, QualitySettings.names.Length - 1) : QualitySettings.GetQualityLevel(); }
        set { value = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1); PlayerPrefs.SetInt(KQuality, value); QualitySettings.SetQualityLevel(value, true); ApplyFrameRate(); }
    }

    /// <summary>Chỉ số trong Resolutions (0 = Không viền). Chưa chỉnh thì suy ra từ trạng thái cửa sổ hiện tại.</summary>
    public static int ResIdx
    {
        get
        {
            if (Resolutions.Count == 0) BuildResolutions();
            if (!PlayerPrefs.HasKey(KRes)) return Screen.fullScreenMode != FullScreenMode.Windowed ? 0 : ResIndexOfCurrent();
            return Mathf.Clamp(PlayerPrefs.GetInt(KRes), 0, Resolutions.Count - 1);
        }
        set
        {
            if (Resolutions.Count == 0) BuildResolutions();
            value = Mathf.Clamp(value, 0, Resolutions.Count - 1);
            PlayerPrefs.SetInt(KRes, value);
            ApplyResolution(value);
        }
    }

    public static string ResName(int idx)
    {
        if (Resolutions.Count == 0) BuildResolutions();
        return Resolutions[Mathf.Clamp(idx, 0, Resolutions.Count - 1)].name;
    }

    public static int FpsIdx
    {
        get { return PlayerPrefs.HasKey(KFps) ? Mathf.Clamp(PlayerPrefs.GetInt(KFps), 0, FpsOptions.Length - 1) : FpsIndexOf(Application.targetFrameRate); }
        set
        {
            value = Mathf.Clamp(value, 0, FpsOptions.Length - 1);
            PlayerPrefs.SetInt(KFps, value);
            if (FpsOptions[value] > 0) PlayerPrefs.SetInt(KVsync, 0);   // VSync bật sẽ bỏ qua giới hạn FPS -> tự tắt để giới hạn có tác dụng
            ApplyFrameRate();
        }
    }

    public static bool Vsync
    {
        get { return PlayerPrefs.HasKey(KVsync) ? PlayerPrefs.GetInt(KVsync) == 1 : QualitySettings.vSyncCount > 0; }
        set { PlayerPrefs.SetInt(KVsync, value ? 1 : 0); ApplyFrameRate(); }
    }

    /// <summary>Tên mức chất lượng hiển thị (Việt hoá các tên Unity thường gặp, tên khác giữ nguyên).</summary>
    public static string QualityName(int level)
    {
        var names = QualitySettings.names;
        string n = names[Mathf.Clamp(level, 0, names.Length - 1)];
        switch (n.Trim().ToLowerInvariant())
        {
            case "very low": return "RẤT THẤP";
            case "low": return "THẤP";
            case "medium": return "VỪA";
            case "high": return "CAO";
            case "very high": return "RẤT CAO";
            case "ultra": return "TỐI ĐA";
            default: return n.ToUpperInvariant();
        }
    }

    /// <summary>Đưa toàn bộ về mặc định (âm lượng, độ sáng, font, cấu hình theo mặc định của project).</summary>
    public static void ResetAll()
    {
        Master = 1f; Music = 1f; Sfx = 1f; Muted = false; Brightness = 1f;
        GameFont.Choice = 0; GameFont.ScaleIndex = 1;
        QualityLevel = bootQuality; ResIdx = bootRes; Vsync = bootVsync == 1; FpsIdx = bootFpsIdx;
        if (bootVsync == 1) Vsync = true;   // FpsIdx có thể vừa tắt VSync, trả lại đúng mặc định
        PlayerPrefs.Save();
    }
}

/// <summary>Lớp phủ độ sáng toàn game: tối hơn = phủ đen, sáng hơn = phủ trắng nhạt. Sống xuyên các scene, nằm trên mọi canvas, không chặn chuột.</summary>
public class BrightnessOverlay : MonoBehaviour
{
    static BrightnessOverlay inst;
    Image dark, light;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { inst = null; }

    public static void Ensure()
    {
        if (inst == null)
        {
            var go = new GameObject("BrightnessOverlay");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<BrightnessOverlay>();
            inst.Build();
        }
        inst.Refresh();
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        dark = Layer("Dark", Color.black);
        light = Layer("Light", Color.white);
    }

    Image Layer(string n, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var im = go.GetComponent<Image>();
        im.sprite = ThemeGfx.Pixel; im.color = c.WithAlpha(0f); im.raycastTarget = false;
        return im;
    }

    void Refresh()
    {
        float b = GameSettings.Brightness;
        dark.color = Color.black.WithAlpha(b < 1f ? (1f - b) : 0f);
        light.color = Color.white.WithAlpha(b > 1f ? (b - 1f) * 1.2f : 0f);   // 125% -> phủ trắng 30% (trước đây chỉ ~12% nên gần như không thấy)
    }
}
