using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Đấu trường "Cược Tri Thức": hai hiệp sĩ hai bên, thanh máu, hiệu ứng trúng đòn và KO.
/// File này lo phần hình ảnh; luật chơi theo vòng nằm ở DuelArenaScreen.Game.cs (chặng 5D.2).
/// </summary>
public partial class DuelArenaScreen : MonoBehaviour
{
    const string ArenaScene = "DuelArenaScene";

    // Nếu hiệp sĩ quay lưng vào nhau thì đảo hai giá trị này
    const bool FlipMe = false, FlipOpp = true;
    const bool ShowDebugKeys = false;      // phím 1/2/R để thử hiệu ứng (tắt ở chặng sau)

    // ---- giao diện ----
    RectTransform artRoot, scene, ui;
    MenuNatureFx nature;
    Vector2 lastRect;
    Sprite[] knightFrames;
    Text roundText, timerText;

    class Fighter
    {
        public RectTransform rt; public Image img;
        public Vector2 home;
        public Color tint;
        public Image fill; public Text hpText, nameText;
        public float shown, target;      // máu đang hiển thị (chạy dần) và máu đích
        public float shake, flash, angle;
    }
    Fighter me = new Fighter(), opp = new Fighter();

    class Floaty { public RectTransform rt; public Text txt; public float age; }
    readonly List<Floaty> floaties = new List<Floaty>();

    partial void GameStart();             // chặng 5D.2 sẽ cài đặt
    partial void GameTick();

    // ------------------------------------------------------------------ tự gắn vào DuelArenaScene
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        if (s.name != ArenaScene || FindFirstObjectByType<DuelArenaScreen>() != null) return;
        new GameObject("DuelArenaScreen").AddComponent<DuelArenaScreen>();
    }

    // ------------------------------------------------------------------ khởi tạo
    void Start()
    {
        Time.timeScale = 1f;
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = GameTheme.Ink; }

        UIKit.EnsureEventSystem();
        SceneShell.Create(ShellMood.Hub, null, 1f, true, false);

        artRoot = UIKit.BuildCanvas(transform, "Arena Art", -90);
        scene = UIKit.CBox("Scene", artRoot, Color.clear, Vector2.zero, new Vector2(1920f, 1080f));
        nature = new MenuNatureFx();
        nature.BuildForest(scene, 250f, 20);
        BuildFighters();

        ui = UIKit.BuildCanvas(transform, "Arena UI", 10);
        BuildHud();
        MenuNatureFx.FitCover(artRoot, scene, ref lastRect);

        GameStart();
    }

    void OnDestroy() { if (nature != null) nature.Dispose(); }

    // ------------------------------------------------------------------ nhân vật
    void BuildFighters()
    {
        knightFrames = new Sprite[4];
        for (int i = 0; i < 4; i++) knightFrames[i] = MenuArt.Get("px_knight_" + i);

        MakeFighter(me, -600f, FlipMe, new Color(.78f, .92f, 1f));     // mình: ánh xanh lạnh
        MakeFighter(opp, 600f, FlipOpp, new Color(1f, .70f, .62f));    // đối thủ: ánh đỏ ấm
    }

    void MakeFighter(Fighter f, float x, bool flip, Color tint)
    {
        var shadow = UIKit.CBox("Shadow", scene, new Color(0, 0, 0, .6f), new Vector2(x, -410f), new Vector2(330f, 64f));
        shadow.GetComponent<Image>().sprite = ThemeGfx.Glow;

        f.home = new Vector2(x, -200f);
        f.rt = UIKit.CBox("Knight", scene, Color.white, f.home, new Vector2(240f, 420f));
        f.img = f.rt.GetComponent<Image>();
        f.tint = tint; f.img.color = tint;
        if (knightFrames[0] != null) f.img.sprite = knightFrames[0];
        if (flip) f.rt.localScale = new Vector3(-1f, 1f, 1f);
    }

    // ------------------------------------------------------------------ thanh máu + thông tin giữa
    void BuildHud()
    {
        BuildHpBar(me, -560f, false, GameTheme.Moss);
        BuildHpBar(opp, 560f, true, GameTheme.Ember);

        me.nameText.text = GameSettings.Nickname;
        string oppName = string.IsNullOrEmpty(DuelSession.OppNick) ? "Đối thủ thử" : DuelSession.OppNick;
        if (string.Equals(oppName, GameSettings.Nickname, System.StringComparison.OrdinalIgnoreCase)) oppName += " (2)";
        opp.nameText.text = oppName;

        roundText = UIKit.Label("Round", ui, "VÒNG 1 / 10", 34, GameTheme.GoldHi, TextAnchor.MiddleCenter,
            new Vector2(0, 505f), new Vector2(400f, 44f), true, true, FontStyle.Bold);
        timerText = UIKit.Label("Timer", ui, "20", 64, GameTheme.Bone, TextAnchor.MiddleCenter,
            new Vector2(0, 440f), new Vector2(300f, 76f), true, true, FontStyle.Bold);

        if (ShowDebugKeys)
            UIKit.Label("Debug", ui, "[1] mình trúng đòn   [2] đối thủ trúng đòn   [R] hồi máu", 24, GameTheme.Fog,
                TextAnchor.MiddleCenter, new Vector2(0, -490f), new Vector2(1200f, 36f));
    }

    void BuildHpBar(Fighter f, float x, bool right, Color col)
    {
        UIKit.ProgressBar(ui, right ? "HpOpp" : "HpMe", new Vector2(x, 470f), new Vector2(700f, 38f), col, out f.fill);
        if (right) f.fill.fillOrigin = 1;                      // thanh bên phải tụt về phía mép phải
        f.fill.fillAmount = 1f;

        f.nameText = UIKit.Label("Name", ui, "", 30, GameTheme.Bone, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft,
            new Vector2(x, 515f), new Vector2(700f, 40f), true, true, FontStyle.Bold);
        f.hpText = UIKit.Label("Hp", ui, "", 26, Color.white, TextAnchor.MiddleCenter,
            new Vector2(x, 470f), new Vector2(700f, 38f), false, true, FontStyle.Bold);
        f.shown = f.target = DuelRules.MaxHp;
    }

    // ------------------------------------------------------------------ giao tiếp với phần luật chơi (5D.2)
    void SetHp(Fighter f, int hp) { f.target = Mathf.Clamp(hp, 0, DuelRules.MaxHp); }

    /// <summary>Hiệu ứng trúng đòn: rung, nháy đỏ, số sát thương bay lên.</summary>
    void Hit(Fighter f, int damage)
    {
        if (damage <= 0) return;
        f.shake = .45f; f.flash = .35f;
        Floating(f, "-" + damage, new Color(1f, .35f, .3f));
    }

    void Floating(Fighter f, string text, Color col)
    {
        var lbl = UIKit.Label("Float", scene, text, 76, col, TextAnchor.MiddleCenter,
            f.home + new Vector2(0f, 250f), new Vector2(300f, 90f), true, true, FontStyle.Bold);
        floaties.Add(new Floaty { rt = lbl.rectTransform, txt = lbl });
    }

    // ------------------------------------------------------------------ cập nhật mỗi frame
    void Update()
    {
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        MenuNatureFx.FitCover(artRoot, scene, ref lastRect);
        if (nature != null) nature.Tick(t, dt);

        Animate(me, t, dt, 0, true);
        Animate(opp, t, dt, 2, false);
        UpdateFloaties(dt);

        if (ShowDebugKeys) RunDebugKeys();
        GameTick();
    }

    void Animate(Fighter f, float t, float dt, int phase, bool isMe)
    {
        // hít thở (4 khung hình, mỗi khung 0,7 giây)
        if (knightFrames != null && knightFrames[0] != null)
            f.img.sprite = knightFrames[(((int)(t / .7f)) + phase) % 4];

        // thanh máu chạy dần về đích
        f.shown = Mathf.MoveTowards(f.shown, f.target, 70f * dt);
        f.fill.fillAmount = f.shown / DuelRules.MaxHp;
        f.hpText.text = Mathf.RoundToInt(f.shown) + " / " + DuelRules.MaxHp;

        // KO: ngã ra sau khi hết máu
        bool ko = f.target <= 0f && f.shown <= .5f;
        float wantAngle = ko ? (isMe ? 85f : -85f) : 0f;
        f.angle = Mathf.MoveTowards(f.angle, wantAngle, 260f * dt);
        f.rt.localEulerAngles = new Vector3(0f, 0f, f.angle);

        // rung khi trúng đòn
        Vector2 off = Vector2.zero;
        if (f.shake > 0f)
        {
            f.shake -= dt;
            off.x = Mathf.Sin(t * 70f) * 14f * Mathf.Clamp01(f.shake / .45f);
        }
        off.y = -Mathf.Abs(f.angle) / 85f * 90f;               // ngã xuống thì hạ thấp để nằm trên mặt đất
        f.rt.anchoredPosition = f.home + off;

        // nháy đỏ + tối đi khi KO
        Color c = f.tint;
        if (f.flash > 0f)
        {
            f.flash -= dt;
            c = Color.Lerp(f.tint, new Color(1f, .15f, .15f), Mathf.Clamp01(f.flash / .35f));
        }
        if (ko) c = Color.Lerp(c, new Color(.35f, .35f, .4f), .6f);
        f.img.color = c;
    }

    void UpdateFloaties(float dt)
    {
        for (int i = floaties.Count - 1; i >= 0; i--)
        {
            var fl = floaties[i];
            fl.age += dt;
            float k = fl.age / 1.3f;
            if (k >= 1f) { Destroy(fl.rt.gameObject); floaties.RemoveAt(i); continue; }

            fl.rt.anchoredPosition += new Vector2(0f, 120f * dt);
            fl.rt.localScale = Vector3.one * (1f + .35f * Mathf.Max(0f, 1f - fl.age / .2f));
            var c = fl.txt.color; c.a = Mathf.Clamp01(2f * (1f - k)); fl.txt.color = c;
        }
    }

    // ------------------------------------------------------------------ phím thử (chỉ để xem hình)
    int dbgMe = DuelRules.MaxHp, dbgOpp = DuelRules.MaxHp;

    void RunDebugKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame) { dbgMe = Mathf.Max(0, dbgMe - 30); SetHp(me, dbgMe); Hit(me, 30); }
        if (kb.digit2Key.wasPressedThisFrame) { dbgOpp = Mathf.Max(0, dbgOpp - 30); SetHp(opp, dbgOpp); Hit(opp, 30); }
        if (kb.rKey.wasPressedThisFrame)
        {
            dbgMe = dbgOpp = DuelRules.MaxHp;
            SetHp(me, dbgMe); SetHp(opp, dbgOpp);
        }
    }
}