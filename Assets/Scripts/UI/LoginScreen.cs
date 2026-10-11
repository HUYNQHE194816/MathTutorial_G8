using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Màn ĐĂNG NHẬP dark fantasy (đồng bộ MainMenu / Chọn môn): rừng trăng, hiệp sĩ giáp xám đứng canh và "hỏi tên",
/// khung đá khắc rune chứa ô Tên + Lớp. Ô nhập viền cam khi chọn, đỏ + rung khi sai, thành công thì tàn lửa bùng lên rồi vào MainMenu.
/// Tự gắn vào scene "LoginScene" lúc chạy (không cần chỉnh scene); giao diện cũ (LoginManager) chỉ bị ẩn.
/// Muốn quay lại giao diện cũ: xoá hàm Hook() bên dưới.
/// </summary>
public class LoginScreen : MonoBehaviour
{
    const string PrefName = "login.name", PrefClass = "login.class";
    [SerializeField] string nextScene = "MainMenu";

    RectTransform artRoot, scene, ui, panel, plate, knightRt;
    CanvasGroup headCg, panelCg, plateCg;
    InputField nameIn, classIn;
    Image nameFrame, classFrame;
    Text message, plateText, btnLabel;
    Button loginBtn;
    MenuNatureFx nature;
    Sprite[] knightFrames;
    Image knight;
    Vector2 lastRect, panelBase, knightBase;
    readonly List<Burst> bursts = new List<Burst>();
    float t0, shake, punch, jump = -1f;
    bool nameErr, classErr, locked;
    int lastKnight = -1;

    class Burst { public RectTransform rt; public Image im; public Vector2 v; public float age, life; }

    // ------------------------------------------------------------------ tự gắn vào LoginScene
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        if (s.name != "LoginScene" || FindFirstObjectByType<LoginScreen>() != null) return;
        new GameObject("LoginScreen").AddComponent<LoginScreen>();
    }

    // ------------------------------------------------------------------ khởi tạo
    void Awake()
    {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
    }

    void Start()
    {
        Time.timeScale = 1f; t0 = Time.unscaledTime;
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = GameTheme.Ink; }

        UIKit.EnsureEventSystem();
        SceneShell.Create(ShellMood.Hub, null, 1f, true, false);

        artRoot = UIKit.BuildCanvas(transform, "Login Art", -90);
        scene = UIKit.CBox("Scene", artRoot, Color.clear, Vector2.zero, new Vector2(1920f, 1080f));
        nature = new MenuNatureFx();
        nature.BuildForest(scene, 200f, 24);
        BuildKnight();

        ui = UIKit.BuildCanvas(transform, "Login UI", 10);
        BuildTitle();
        BuildPlate();
        BuildPanel();

        nameIn.text = PlayerPrefs.GetString(PrefName, "");
        classIn.text = PlayerPrefs.GetString(PrefClass, "");
        MenuNatureFx.FitCover(artRoot, scene, ref lastRect);
    }

    void BuildKnight()
    {
        knightFrames = new Sprite[4];
        for (int i = 0; i < 4; i++) { knightFrames[i] = MenuArt.Get("px_knight_" + i); if (knightFrames[i] == null) { knightFrames = null; return; } }
        var sh = UIKit.CBox("KnightShadow", scene, new Color(0, 0, 0, .6f), new Vector2(-640, -412), new Vector2(300, 56));
        sh.GetComponent<Image>().sprite = ThemeGfx.Glow;
        knightBase = new Vector2(-640, -236);
        knightRt = UIKit.CBox("Knight", scene, Color.white, knightBase, new Vector2(216, 378));
        knight = knightRt.GetComponent<Image>(); knight.sprite = knightFrames[0];
    }

    void BuildTitle()
    {
        var h = UIKit.CBox("Head", ui, Color.clear, Vector2.zero, Vector2.zero);
        headCg = h.gameObject.AddComponent<CanvasGroup>();
        var ts = MenuArt.Get("sm_title");
        if (ts != null)
        {
            const float w = 1000f;
            var rt = UIKit.CBox("TitleArt", h, Color.white, new Vector2(0, 420), new Vector2(w, w * ts.rect.height / ts.rect.width));
            rt.GetComponent<Image>().sprite = ts;
        }
        else UIKit.Label("Title", h, "MÀN SƯƠNG LÃNG QUÊN", 84, GameTheme.GoldHi, TextAnchor.MiddleCenter, new Vector2(0, 420), new Vector2(1400, 120), true, true, FontStyle.Bold);
        UIKit.Label("Kicker", h, "KHOA HỌC TỰ NHIÊN  •  ĐĂNG NHẬP", 26, new Color(.67f, .75f, .9f, .92f), TextAnchor.MiddleCenter,
            new Vector2(0, 336), new Vector2(1200, 40), true, true);
    }

    // bong bóng thoại của hiệp sĩ
    void BuildPlate()
    {
        var p = UIKit.Panel(ui, "Plate", new Vector2(-640, 70), new Vector2(520, 190), PanelStyle.Dark, out var c);
        plate = p; plateCg = p.gameObject.AddComponent<CanvasGroup>();
        var tail = UIKit.CBox("Tail", p, GameTheme.Iron, new Vector2(0, -95), new Vector2(28, 28));
        tail.localEulerAngles = new Vector3(0, 0, 45f); tail.SetAsFirstSibling();
        plateText = UIKit.Label("Text", c, "Lữ khách, hãy cho ta biết tên ngươi.", 30, GameTheme.Bone, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(440, 130), false, false, FontStyle.Italic);
    }

    void BuildPanel()
    {
        panelBase = new Vector2(300, -50);
        panel = UIKit.Panel(ui, "LoginPanel", panelBase, new Vector2(780, 660), PanelStyle.Dark, out var c);
        panelCg = panel.gameObject.AddComponent<CanvasGroup>();

        UIKit.Label("T", c, "Sổ Ký Ức", 56, GameTheme.GoldHi, TextAnchor.MiddleCenter, new Vector2(0, 245), new Vector2(660, 76), true, true, FontStyle.Bold);
        UIKit.Divider(c, new Vector2(0, 196), 420);

        UIKit.Label("L1", c, "TÊN CỦA BẠN", 24, GameTheme.Fog, TextAnchor.MiddleLeft, new Vector2(0, 140), new Vector2(580, 34), false, true);
        nameIn = MakeInput(c, "In_Name", "Ví dụ: Nguyễn Văn An", new Vector2(0, 80), 28, out nameFrame);
        UIKit.Label("L2", c, "LỚP", 24, GameTheme.Fog, TextAnchor.MiddleLeft, new Vector2(0, 10), new Vector2(580, 34), false, true);
        classIn = MakeInput(c, "In_Class", "Ví dụ: 8A1", new Vector2(0, -50), 8, out classFrame);

        message = UIKit.Label("Msg", c, "", 28, GameTheme.Blood, TextAnchor.MiddleCenter, new Vector2(0, -125), new Vector2(600, 40));
        loginBtn = UIKit.MakeButton(c, "Btn_Login", "BƯỚC VÀO MÀN SƯƠNG", new Vector2(0, -205), new Vector2(580, 92), TrySubmit, ButtonStyle.Bronze, 36, .5f);
        btnLabel = loginBtn.GetComponentInChildren<Text>();

        nameIn.onEndEdit.AddListener(s => { if (EnterPressed() && s.Trim().Length > 0) Focus(classIn); });
        classIn.onEndEdit.AddListener(s => { if (EnterPressed()) TrySubmit(); });
        nameIn.onValueChanged.AddListener(s => { nameErr = false; ClearMsg(); });
        classIn.onValueChanged.AddListener(s => { classErr = false; ClearMsg(); });
    }

    InputField MakeInput(Transform p, string n, string hint, Vector2 pos, int limit, out Image frame)
    {
        var fr = UIKit.CBox(n, p, GameTheme.Iron, pos, new Vector2(580, 76), true);
        frame = fr.GetComponent<Image>();
        var fill = UIKit.CBox("Fill", fr, GameTheme.Ink, Vector2.zero, Vector2.zero); UIKit.Stretch(fill, 4, 4, 4, 4);
        var ph = UIKit.Label("Placeholder", fill, hint, 32, GameTheme.Fog.WithAlpha(.6f), TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, false, false, FontStyle.Italic);
        var tx = UIKit.Label("Text", fill, "", 34, GameTheme.Bone, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero);
        UIKit.Stretch(ph.rectTransform, 20, 0, 20, 0); UIKit.Stretch(tx.rectTransform, 20, 0, 20, 0);
        ph.horizontalOverflow = tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        tx.supportRichText = false;
        var input = fr.gameObject.AddComponent<InputField>();
        input.targetGraphic = frame; input.transition = Selectable.Transition.None;
        input.textComponent = tx; input.placeholder = ph; input.characterLimit = limit;
        input.lineType = InputField.LineType.SingleLine;
        input.customCaretColor = true; input.caretColor = GameTheme.EmberHi;
        input.selectionColor = GameTheme.Ember.WithAlpha(.35f);
        return input;
    }

    static bool EnterPressed()
    {
        var k = Keyboard.current;
        return k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
    }

    static void Focus(InputField f) { f.Select(); f.ActivateInputField(); }

    // ------------------------------------------------------------------ đăng nhập
    void TrySubmit()
    {
        if (locked || Time.unscaledTime - t0 < 1.2f) return;
        string n = nameIn.text.Trim(), c = classIn.text.Trim().ToUpperInvariant();
        if (n.Length < 2) { Fail(true, "Bạn chưa khắc tên vào Sổ Ký Ức!"); return; }
        if (c.Length < 1) { Fail(false, "Hãy nhập lớp của bạn (ví dụ 8A1)."); return; }
        Success(n, c);
    }

    void Fail(bool nameField, string msg)
    {
        if (nameField) nameErr = true; else classErr = true;
        message.text = msg; message.color = GameTheme.Blood;
        Say("Hmm… kiểm tra lại giúp ta nhé!");
        shake = .42f;
        GameAudio.Play(Snd.CardLocked);
        Focus(nameField ? nameIn : classIn);
    }

    void Success(string n, string c)
    {
        locked = true;
        StudentProfileData.studentName = n; StudentProfileData.studentClass = c;
        ProgressSaveSystem.Load(n, c);
        PlayerPrefs.SetString(PrefName, n); PlayerPrefs.SetString(PrefClass, c); PlayerPrefs.Save();

        nameIn.interactable = false; classIn.interactable = false; loginBtn.interactable = false;
        btnLabel.text = "ĐANG VÀO...";
        message.text = "Cánh cổng sương đã mở!"; message.color = GameTheme.Moss;
        Say("Chào " + n + "! Màn sương đang chờ ngươi.");
        GameAudio.Play(Snd.CardSelect);
        SpawnBurst(panelBase + new Vector2(0, -205));
        jump = 0f;
        Invoke(nameof(Leave), .95f);
    }

    void Leave() { SceneRouter.Go(nextScene); }

    void Say(string s) { if (plateText.text == s) return; plateText.text = s; punch = .3f; }

    void ClearMsg() { if (!locked) message.text = ""; }

    void SpawnBurst(Vector2 origin)
    {
        for (int i = 0; i < 30; i++)
        {
            float a = Random.value * 6.283f, sp = Random.Range(240f, 680f);
            var rt = UIKit.CBox("Burst", ui, Color.white, origin, Vector2.one * Random.Range(14f, 34f));
            var im = rt.GetComponent<Image>(); im.sprite = ThemeGfx.Glow; im.raycastTarget = false;
            im.color = Color.Lerp(GameTheme.Ember, GameTheme.GoldHi, Random.value);
            bursts.Add(new Burst { rt = rt, im = im, v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp, life = Random.Range(.7f, 1.3f) });
        }
    }

    // ------------------------------------------------------------------ hoạt ảnh
    static float EaseOutCubic(float k) { k = 1f - k; return 1f - k * k * k; }

    void Update()
    {
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime, since = t - t0;
        MenuNatureFx.FitCover(artRoot, scene, ref lastRect);
        if (nature != null) nature.Tick(t, dt);
        if (panel == null) return;

        // vào cảnh: tiêu đề hiện dần, bong bóng, rồi khung đăng nhập trồi lên
        headCg.alpha = Mathf.Clamp01(since / .7f);
        plateCg.alpha = Mathf.Clamp01((since - .5f) / .5f);
        float k = Mathf.Clamp01((since - .3f) / .7f), e = EaseOutCubic(k);
        panelCg.alpha = Mathf.Clamp01(k * 1.6f);
        float sx = 0f;
        if (shake > 0f) { shake -= dt; sx = Mathf.Sin(t * 70f) * 18f * Mathf.Clamp01(shake / .42f); }
        panel.anchoredPosition = panelBase + new Vector2(sx, -(1f - e) * 160f);

        if (punch > 0f) { punch -= dt; float s = 1f + .06f * Mathf.Sin(Mathf.Clamp01(punch / .3f) * Mathf.PI); plate.localScale = new Vector3(s, s, 1f); }

        // ô nhập: viền cam khi đang gõ, đỏ khi sai
        float lk = 1f - Mathf.Exp(-14f * dt);
        Tint(nameFrame, nameErr, nameIn.isFocused, lk);
        Tint(classFrame, classErr, classIn.isFocused, lk);

        // hiệp sĩ thở; nhảy lên khi đăng nhập thành công
        if (knight != null)
        {
            int idx = ((int)(t / .7f)) % 4;
            if (idx != lastKnight) { knight.sprite = knightFrames[idx]; lastKnight = idx; }
            float y = 0f;
            if (jump >= 0f) { jump += dt / .6f; y = Mathf.Sin(Mathf.Clamp01(jump) * Mathf.PI) * 90f; }
            knightRt.anchoredPosition = knightBase + new Vector2(0f, y);
        }

        for (int i = bursts.Count - 1; i >= 0; i--)
        {
            var b = bursts[i]; b.age += dt;
            float u = b.age / b.life;
            if (u >= 1f) { Destroy(b.rt.gameObject); bursts.RemoveAt(i); continue; }
            b.v.y -= 380f * dt; b.v *= 1f - .9f * dt;
            b.rt.anchoredPosition += b.v * dt;
            var col = b.im.color; col.a = 1f - u * u; b.im.color = col;
        }
    }

    static void Tint(Image frame, bool err, bool focus, float k)
    {
        Color target = err ? GameTheme.Blood : focus ? GameTheme.Ember : GameTheme.Iron;
        frame.color = Color.Lerp(frame.color, target, k);
    }

    void OnDestroy() { if (nature != null) nature.Dispose(); }
}
