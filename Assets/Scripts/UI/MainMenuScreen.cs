using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Màn hình chính "Màn Sương Lãng Quên" vẽ hoàn toàn bằng PIXEL ART (xem MenuPixelArt.cs).
/// Cách dùng: mở scene MainMenu, menu Tools > KHTN 8 > "Áp MainMenu mới (dark fantasy)" (hoặc tự thêm component này
/// vào 1 GameObject Ở GỐC scene). Giao diện cũ chỉ bị ẩn lúc chạy (không xoá), muốn quay lại: xoá GameObject MainMenuScreen.
/// Giữ nguyên hành vi cũ: Bắt đầu -> SubjectSelectScene, Tiến độ -> DashboardScene, âm thanh lưu ở PlayerPrefs "KHTN_Muted".
///
/// Bố cục dùng lưới 480x270 điểm ảnh pixel art, mỗi điểm ảnh = 4 điểm ảnh canvas (canvas tham chiếu 1920x1080).
/// Toạ độ "tex" bên dưới là toạ độ pixel art, gốc ở GÓC TRÊN-TRÁI ảnh, y hướng xuống (khớp với script vẽ art);
/// toạ độ "canvas" có gốc ở TÂM màn hình, y hướng lên.
/// </summary>
public class MainMenuScreen : MonoBehaviour
{
    const string MutedKey = "KHTN_Muted";
    const float PX = MenuPixelArt.PX;
    const float TexW = 480f, TexH = 270f;

    RectTransform sceneRoot, fxRoot, root, settings;
    Image soundLabel;
    bool muted;

    // ---- hoạt ảnh ----
    Image knight; Sprite[] knightFrames;
    readonly Image[] flame = new Image[2];
    readonly Image[] warmGlow = new Image[2];
    Sprite[] flameFrames;
    Image moonGlow;
    readonly List<RawImage> mists = new List<RawImage>();
    readonly List<float> mistSpeed = new List<float>();

    class Spark
    {
        public RectTransform rt; public Image im; public bool ember;
        public Vector2 origin; public float age, life, speed, sway, phase, drift, a; public Color col;
    }
    readonly List<Spark> sparks = new List<Spark>();
    // đáy ngọn lửa (toạ độ canvas) - nơi tàn lửa bay lên
    static readonly Vector2[] FlameTop = { new Vector2(-472f, 20f), new Vector2(472f, 20f) };

    void Start()
    {
        Time.timeScale = 1f;
        HideLegacyUi();

        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = GameTheme.Ink; }

        UIKit.EnsureEventSystem();
        SceneShell.Create(ShellMood.Hub, null, 0f);   // chỉ lấy hiệu ứng fade + vignette; nền/sương/tàn lửa do pixel art đảm nhiệm

        sceneRoot = UIKit.BuildCanvas(transform, "MainMenu Scene (pixel art)", 0);
        BuildScene();
        root = UIKit.BuildCanvas(transform, "MainMenu Canvas", 10);
        BuildHeader();
        BuildMenu();
        BuildFooter();

        muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
        ApplySound();
    }

    /// <summary>Ẩn mọi Canvas có sẵn của scene cũ (gọi TRƯỚC khi tạo canvas mới).</summary>
    static void HideLegacyUi()
    {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
    }

    // ---------- Tiện ích dựng ảnh pixel ----------
    /// <summary>Đổi toạ độ góc trên-trái (pixel art) của một ảnh -> toạ độ canvas của TÂM ảnh.</summary>
    static Vector2 TexToCanvas(float texX, float texY, Vector2 texSize)
        => new Vector2((texX + texSize.x * .5f - TexW * .5f) * PX, (TexH * .5f - (texY + texSize.y * .5f)) * PX);

    /// <summary>Ảnh pixel đặt theo tâm canvas, kích thước = số điểm ảnh x 4.</summary>
    static Image Pic(Transform parent, string objName, string sprite, Vector2 center, bool flipX = false, float alpha = 1f, bool ray = false)
    {
        var s = MenuPixelArt.Get(sprite);
        var go = new GameObject(objName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var im = go.GetComponent<Image>();
        im.sprite = s; im.color = new Color(1f, 1f, 1f, alpha); im.raycastTarget = ray;
        var size = s != null ? s.rect.size * PX : Vector2.zero;
        UIKit.Place(im.rectTransform, UIKit.C, UIKit.C, UIKit.C, center, size);
        if (flipX) im.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        return im;
    }

    /// <summary>Ảnh pixel đặt theo góc trên-trái trong toạ độ pixel art.</summary>
    static Image PicTL(Transform parent, string objName, string sprite, float texX, float texY, bool flipX = false, float alpha = 1f)
    {
        var s = MenuPixelArt.Get(sprite);
        var size = s != null ? s.rect.size : Vector2.zero;
        return Pic(parent, objName, sprite, TexToCanvas(texX, texY, size), flipX, alpha);
    }

    RawImage Mist(string objName, string tex, float canvasY, float alpha, float speed)
    {
        var t = MenuPixelArt.Tex(tex, true);
        var go = new GameObject(objName, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(sceneRoot, false);
        var ri = go.GetComponent<RawImage>();
        ri.texture = t; ri.color = new Color(1f, 1f, 1f, alpha); ri.raycastTarget = false;
        float h = t != null ? t.height * PX : 200f;
        // trải ngang hết chiều rộng màn hình (kể cả màn rộng hơn 16:9)
        UIKit.Place(ri.rectTransform, new Vector2(0f, .5f), new Vector2(1f, .5f), UIKit.C, new Vector2(0f, canvasY), new Vector2(0f, h));
        mists.Add(ri); mistSpeed.Add(speed);
        return ri;
    }

    // ---------- Cảnh nền ----------
    void BuildScene()
    {
        // 1) Nền: rừng chết + trăng + tàn tích + cỏ xen gạch đá (480x270 pixel art)
        var bg = Pic(sceneRoot, "Backdrop", "bg", Vector2.zero);
        bg.rectTransform.sizeDelta = Vector2.zero;
        var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = TexW / TexH;

        // 2) Ánh trăng + sương mù xa
        moonGlow = Pic(sceneRoot, "MoonGlow", "glow_cold", new Vector2(0f, 292f), false, .5f);
        Mist("MistHigh", "mist_high", 250f, .5f, -.004f);
        Mist("MistFar", "mist_far", -244f, .9f, .006f);

        // 3) Hai đài đuốc: quầng sáng ấm, trụ đá + tượng gargoyle, ngọn lửa
        flameFrames = new[] { MenuPixelArt.Get("flame_0"), MenuPixelArt.Get("flame_1"), MenuPixelArt.Get("flame_2"), MenuPixelArt.Get("flame_3") };
        float[] torchX = { 100f, 336f };
        for (int i = 0; i < 2; i++)
        {
            warmGlow[i] = PicTL(sceneRoot, "TorchGlow" + i, "glow_warm", torchX[i] + 22f - 48f, 148f - 48f, false, .9f);
            PicTL(sceneRoot, "TorchStatue" + i, "torch", torchX[i], 132f, i == 1);
            flame[i] = PicTL(sceneRoot, "TorchFlame" + i, "flame_0", torchX[i] + 22f - 12f, 130f);
        }

        // 4) Hiệp sĩ (đang thở nhẹ), đứng bên trái khung menu, quay mặt về phía người chơi
        knightFrames = new[] { MenuPixelArt.Get("knight_0"), MenuPixelArt.Get("knight_1"), MenuPixelArt.Get("knight_2"), MenuPixelArt.Get("knight_3") };
        knight = PicTL(sceneRoot, "Knight", "knight_0", 32f, 175f);

        // 5) Sương mù cận cảnh + tàn lửa + đốm sáng ma mị
        Mist("MistNear", "mist_near", -412f, .55f, .012f);
        fxRoot = new GameObject("FX", typeof(RectTransform)).GetComponent<RectTransform>();
        fxRoot.SetParent(sceneRoot, false);
        UIKit.Place(fxRoot, UIKit.C, UIKit.C, UIKit.C, Vector2.zero, Vector2.zero);
        for (int i = 0; i < 2; i++) for (int k = 0; k < 14; k++) AddSpark(true, i);
        for (int k = 0; k < 16; k++) AddSpark(false, 0);
    }

    void AddSpark(bool ember, int side)
    {
        var go = new GameObject(ember ? "Ember" : "Wisp", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(fxRoot, false);
        var im = go.GetComponent<Image>();
        im.sprite = ThemeGfx.Pixel; im.raycastTarget = false;
        var s = new Spark { rt = im.rectTransform, im = im, ember = ember };
        s.origin = ember ? FlameTop[side] : Vector2.zero;
        UIKit.Place(s.rt, UIKit.C, UIKit.C, UIKit.C, Vector2.zero, new Vector2(PX, PX));
        Respawn(s, true, side);
        sparks.Add(s);
    }

    static void Respawn(Spark s, bool scatter, int side)
    {
        s.phase = UnityEngine.Random.Range(0f, 6.28f);
        s.a = UnityEngine.Random.Range(.65f, 1f);
        if (s.ember)
        {
            s.origin = FlameTop[side] + new Vector2(UnityEngine.Random.Range(-20f, 20f), UnityEngine.Random.Range(0f, 24f));
            s.speed = UnityEngine.Random.Range(40f, 110f);
            s.life = UnityEngine.Random.Range(1.6f, 3.4f);
            s.sway = UnityEngine.Random.Range(.8f, 2f);
            s.col = UnityEngine.Random.value < .5f ? new Color(1f, .75f, .35f) : new Color(1f, .47f, .16f);
            float px = UnityEngine.Random.value < .8f ? PX : PX * 2f;
            s.rt.sizeDelta = new Vector2(px, px);
        }
        else
        {
            s.origin = new Vector2(UnityEngine.Random.Range(-900f, 900f), UnityEngine.Random.Range(-340f, 480f));
            s.speed = UnityEngine.Random.Range(6f, 16f);
            s.drift = UnityEngine.Random.Range(-14f, 14f);
            s.life = UnityEngine.Random.Range(6f, 12f);
            s.sway = UnityEngine.Random.Range(.4f, 1.1f);
            s.col = new Color(.68f, .84f, 1f);
        }
        s.age = scatter ? UnityEngine.Random.Range(0f, s.life) : 0f;
    }

    static float Snap(float v) { return Mathf.Round(v / PX) * PX; }

    // ---------- Tiêu đề, khẩu hiệu ----------
    void BuildHeader()
    {
        Pic(root, "Kicker", "kicker", new Vector2(0f, 505f), false, .9f);           // KHOA HỌC TỰ NHIÊN • LỚP 8
        Pic(root, "Title", "title", new Vector2(0f, 335f));                          // MÀN SƯƠNG LÃNG QUÊN (chữ đồng khắc nổi)
        Pic(root, "Tagline", "tagline", new Vector2(0f, 235f), false, .95f);         // Mỗi điều bạn biết là một ngọn đèn giữa màn sương.
    }

    // ---------- Khung menu + 4 nút ----------
    void BuildMenu()
    {
        Pic(root, "MenuFrame", "panel", new Vector2(0f, -80f));

        PixelButton(root, "Btn_Start", "start", "lbl_start", new Vector2(0f, 82f), () => SceneRouter.Go("SubjectSelectScene"), 0f);
        PixelButton(root, "Btn_Dashboard", "menu", "lbl_progress", new Vector2(0f, -30f), OpenDashboard, .08f);
        PixelButton(root, "Btn_Settings", "menu", "lbl_settings", new Vector2(0f, -130f), OpenSettings, .16f);
        PixelButton(root, "Btn_Exit", "menu", "lbl_exit", new Vector2(0f, -230f), Quit, .24f);
    }

    /// <summary>kind: "start" (nút vàng nổi bật) | "menu" (nút sắt) | "wide" (nút sắt rộng). Trả về ảnh nhãn.</summary>
    Image PixelButton(Transform parent, string objName, string kind, string labelSprite, Vector2 center, Action onClick, float delay)
    {
        var n = MenuPixelArt.Get("btn_" + kind + "_n");
        var size = n != null ? n.rect.size * PX : new Vector2(480f, 84f);

        var go = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var hit = go.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f); hit.raycastTarget = true;     // vùng bấm trong suốt
        UIKit.Place(hit.rectTransform, UIKit.C, UIKit.C, UIKit.C, center, size);

        var face = Pic(go.transform, "Face", "btn_" + kind + "_n", Vector2.zero);
        var label = Pic(go.transform, "Label", labelSprite, Vector2.zero);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = hit; btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => { GameAudio.Play(Snd.UiClick); if (onClick != null) onClick(); });

        var fx = go.AddComponent<PixelMenuButton>();
        fx.Setup(face, n, MenuPixelArt.Get("btn_" + kind + "_h"), MenuPixelArt.Get("btn_" + kind + "_d"), label.rectTransform, delay);
        return label;
    }

    void BuildFooter()
    {
        string n = StudentProfileData.studentName;
        if (string.IsNullOrWhiteSpace(n)) n = "Học sinh";
        UIKit.Label("Greeting", root, "Người Giữ Ký Ức: " + n, 28, GameTheme.Bone, TextAnchor.MiddleLeft, new Vector2(-560, -490), new Vector2(700, 44), false, true);
        UIKit.Label("Version", root, "Bản thử nghiệm · Giai đoạn 0", 24, GameTheme.Fog, TextAnchor.MiddleRight, new Vector2(560, -490), new Vector2(700, 44), false, true);
    }

    // ---------- Hành vi ----------
    void OpenDashboard()
    {
        if (!Application.CanStreamedLevelBeLoaded("DashboardScene"))
        {
            UIKit.Toast(root, "Chưa có DashboardScene trong Build Settings");
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

    // Cửa sổ Cài đặt được dựng mới mỗi lần mở và huỷ khi đóng (cùng phong cách pixel art).
    void OpenSettings()
    {
        if (settings != null) return;
        var dim = UIKit.Box("SettingsDim", root, new Color(0, 0, 0, .7f), Vector2.zero, Vector2.one, UIKit.C, Vector2.zero, Vector2.zero, true);
        UIKit.Stretch(dim, 0, 0, 0, 0);
        settings = dim;

        Pic(dim, "SettingsPanel", "panel_small", Vector2.zero, false, 1f, true);
        Pic(dim, "SettingsTitle", "lbl_settings", new Vector2(0f, 104f));
        soundLabel = PixelButton(dim, "Btn_Sound", "wide", "lbl_sound_on", new Vector2(0f, 10f), ToggleSound, 0f);
        PixelButton(dim, "Btn_Close", "menu", "lbl_close", new Vector2(0f, -90f), CloseSettings, .08f);
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
        if (soundLabel != null)
        {
            var s = MenuPixelArt.Get(muted ? "lbl_sound_off" : "lbl_sound_on");
            if (s != null) { soundLabel.sprite = s; soundLabel.rectTransform.sizeDelta = s.rect.size * PX; }
        }
    }

    // ---------- Hoạt ảnh ----------
    void Update()
    {
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;

        // hiệp sĩ thở nhẹ (4 khung: ngực nhấp nhô + chùm lông đỏ đung đưa)
        if (knight != null && knightFrames != null && knightFrames.Length > 0)
            knight.sprite = knightFrames[(int)(t * 1.6f) % knightFrames.Length];

        // lửa đuốc nhấp nháy + quầng sáng ấm
        for (int i = 0; i < 2; i++)
        {
            if (flame[i] == null) continue;
            flame[i].sprite = flameFrames[(int)(t * 9f + i * 2) % flameFrames.Length];
            float f = .72f + .28f * Mathf.PerlinNoise(t * 3f, i * 9.1f);
            warmGlow[i].color = new Color(1f, 1f, 1f, .9f * f);
        }

        // ánh trăng thở chậm
        if (moonGlow != null) moonGlow.color = new Color(1f, 1f, 1f, .42f + .14f * Mathf.Sin(t * .7f));

        // sương trôi (texture lặp, lấy mẫu Point nên chuyển động theo từng điểm ảnh)
        for (int i = 0; i < mists.Count; i++)
        {
            var m = mists[i]; if (m == null || m.texture == null) continue;
            float w = m.rectTransform.rect.width / (m.texture.width * PX);
            m.uvRect = new Rect(Mathf.Repeat(t * mistSpeed[i], 1f), 0f, Mathf.Max(1f, w), 1f);
        }

        // tàn lửa + đốm sáng
        foreach (var s in sparks)
        {
            s.age += dt;
            if (s.age >= s.life) Respawn(s, false, s.origin.x < 0f ? 0 : 1);
            float k = Mathf.Clamp01(s.age / s.life);
            float env = Mathf.Sin(Mathf.PI * k);
            if (s.ember)
            {
                float x = s.origin.x + Mathf.Sin(s.phase + s.age * s.sway) * 16f;
                float y = s.origin.y + s.speed * s.age;
                s.rt.anchoredPosition = new Vector2(Snap(x), Snap(y));
                s.im.color = new Color(s.col.r, s.col.g, s.col.b, s.a * env);
            }
            else
            {
                float x = s.origin.x + s.drift * s.age + Mathf.Sin(s.phase + s.age * s.sway) * 24f;
                float y = s.origin.y + s.speed * s.age;
                s.rt.anchoredPosition = new Vector2(Snap(x), Snap(y));
                float tw = Mathf.Round((.35f + .65f * Mathf.Abs(Mathf.Sin(t * 1.3f + s.phase))) * 4f) / 4f;   // nhấp nháy theo nấc
                s.im.color = new Color(s.col.r, s.col.g, s.col.b, s.a * env * tw * .8f);
            }
        }

        if (settings != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseSettings();
    }
}
