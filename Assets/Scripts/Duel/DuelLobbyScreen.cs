using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Sảnh Đối kháng: tìm đối thủ ngẫu nhiên, chờ hai bên đồng ý, chọn Trắc nghiệm / Tự luận.</summary>
public class DuelLobbyScreen : MonoBehaviour
{
    const string LobbyScene = "DuelLobbyScene";
    const string BackScene = "ModeSelectScene";
    const string QuizScene = "SubjectSelectScene";
    const string EssayScene = "EssaySubjectSelectScene";
    const float AcceptSeconds = 15f;

    enum State { Searching, Found, Waiting, Choose, Declined }

    RectTransform artRoot, scene, ui, content, body;
    MenuNatureFx nature;
    Vector2 lastRect;
    State state;
    float stateT0;
    Text statusLabel, timerLabel;
    string oppNick = "";
    bool leaving;

    // ------------------------------------------------------------------ tự gắn vào DuelLobbyScene
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        if (s.name != LobbyScene || FindFirstObjectByType<DuelLobbyScreen>() != null) return;
        new GameObject("DuelLobbyScreen").AddComponent<DuelLobbyScreen>();
    }

    // ------------------------------------------------------------------ khởi tạo
    void Start()
    {
        Time.timeScale = 1f;
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = GameTheme.Ink; }

        UIKit.EnsureEventSystem();
        SceneShell.Create(ShellMood.Hub, null, 1f, true, false);

        artRoot = UIKit.BuildCanvas(transform, "Duel Art", -90);
        scene = UIKit.CBox("Scene", artRoot, Color.clear, Vector2.zero, new Vector2(1920f, 1080f));
        nature = new MenuNatureFx();
        nature.BuildForest(scene, 250f, 20);

        ui = UIKit.BuildCanvas(transform, "Duel UI", 10);
        UIKit.Title(ui, "ĐỐI KHÁNG", new Vector2(0, 410), 76);
        UIKit.Panel(ui, "DuelPanel", new Vector2(0, -20), new Vector2(1000, 600), PanelStyle.Dark, out content);
        MenuNatureFx.FitCover(artRoot, scene, ref lastRect);

        DuelSession.Reset();
        DuelBackend.Current.Connect(GameSettings.Nickname);
        GoSearching();
    }

    void OnDestroy() { if (nature != null) nature.Dispose(); }

    // ------------------------------------------------------------------ tiện ích dựng
    void Enter(State s)
    {
        state = s; stateT0 = Time.unscaledTime; statusLabel = timerLabel = null;
        if (body != null) Destroy(body.gameObject);
        body = UIKit.CBox("Body", content, Color.clear, Vector2.zero, Vector2.zero);
        UIKit.Stretch(body, 0, 0, 0, 0);
    }

    Text Lbl(string text, int size, Color col, float y, bool display = false)
    {
        return UIKit.Label("L", body, text, size, col, TextAnchor.MiddleCenter, new Vector2(0, y), new Vector2(880, size * 1.5f),
            display, display, display ? FontStyle.Bold : FontStyle.Normal);
    }

    // ------------------------------------------------------------------ các trạng thái
    void GoSearching()
    {
        Enter(State.Searching);
        statusLabel = Lbl("ĐANG TÌM ĐỐI THỦ", 48, GameTheme.GoldHi, 150, true);
        Lbl("Bạn: " + GameSettings.Nickname, 34, GameTheme.Bone, 50);
        Lbl("Đang ghép cặp ngẫu nhiên với người chơi khác", 28, GameTheme.Fog, -30);
        UIKit.MakeButton(body, "Btn_Cancel", "HỦY", new Vector2(0, -170), new Vector2(360, 76), Back, ButtonStyle.Stone, 34, .1f);

        DuelBackend.Current.FindOpponent(nick =>
        {
            if (state != State.Searching) return;
            oppNick = nick; GoFound();
        });
    }

    void GoFound()
    {
        Enter(State.Found);
        Lbl("ĐÃ TÌM THẤY ĐỐI THỦ!", 46, GameTheme.GoldHi, 170, true);
        Lbl(GameSettings.Nickname + "   VS   " + oppNick, 44, GameTheme.Bone, 70);
        timerLabel = Lbl("", 28, GameTheme.Fog, -20);
        UIKit.MakeButton(body, "Btn_Accept", "ĐỒNG Ý", new Vector2(-220, -150), new Vector2(380, 84), Accept, ButtonStyle.Bronze, 38, .1f);
        UIKit.MakeButton(body, "Btn_Decline", "TỪ CHỐI", new Vector2(220, -150), new Vector2(380, 84), Decline, ButtonStyle.Danger, 38, .16f);
    }

    void Accept()
    {
        if (state != State.Found) return;
        Enter(State.Waiting);
        Lbl("ĐANG CHỜ ĐỐI THỦ ĐỒNG Ý…", 44, GameTheme.GoldHi, 120, true);
        Lbl("Đối thủ: " + oppNick, 34, GameTheme.Bone, 30);
        UIKit.MakeButton(body, "Btn_Cancel", "HỦY", new Vector2(0, -150), new Vector2(360, 76), Back, ButtonStyle.Stone, 34, .1f);

        DuelBackend.Current.Respond(true, ok =>
        {
            if (state != State.Waiting) return;
            if (ok) GoChoose(); else GoDeclined("Đối thủ đã từ chối trận đấu.");
        });
    }

    void Decline()
    {
        if (state != State.Found) return;
        DuelBackend.Current.Respond(false, null);
        GoSearching();                      // từ chối thì tìm đối thủ khác
    }

    void GoDeclined(string msg)
    {
        Enter(State.Declined);
        Lbl(msg, 40, GameTheme.Blood, 100, true);
        UIKit.MakeButton(body, "Btn_Again", "TÌM LẠI", new Vector2(-220, -100), new Vector2(380, 80), GoSearching, ButtonStyle.Bronze, 36, .1f);
        UIKit.MakeButton(body, "Btn_Back", "QUAY LẠI", new Vector2(220, -100), new Vector2(380, 80), Back, ButtonStyle.Stone, 36, .16f);
    }

    void GoChoose()
    {
        Enter(State.Choose);
        Lbl("CHỌN THỂ THỨC THI ĐẤU", 46, GameTheme.GoldHi, 190, true);
        Lbl("Đối thủ: " + oppNick, 34, GameTheme.Bone, 120);
        Lbl("Thời gian 3 phút. Đồng hồ bắt đầu khi bạn vào màn chơi.", 26, GameTheme.Fog, 60);
        UIKit.MakeButton(body, "Btn_Quiz", "TRẮC NGHIỆM", new Vector2(-230, -50), new Vector2(420, 96), () => Choose(DuelMode.Quiz), ButtonStyle.Bronze, 36, .1f);
        UIKit.MakeButton(body, "Btn_Essay", "TỰ LUẬN", new Vector2(230, -50), new Vector2(420, 96), () => Choose(DuelMode.Essay), ButtonStyle.Bronze, 36, .16f);
        UIKit.MakeButton(body, "Btn_Leave", "THOÁT TRẬN", new Vector2(0, -200), new Vector2(360, 70), Back, ButtonStyle.Stone, 32, .22f);
    }

    // ------------------------------------------------------------------ hành vi
    void Choose(DuelMode mode)
    {
        if (leaving) return;
        leaving = true;
        DuelSession.Begin(mode, oppNick);
        SceneRouter.Go(mode == DuelMode.Quiz ? QuizScene : EssayScene);
    }

    void Back()
    {
        if (leaving) return;
        leaving = true;
        DuelBackend.Current.CancelSearch();
        DuelSession.Reset();
        SceneRouter.Go(BackScene);
    }

    // ------------------------------------------------------------------ cập nhật
    void Update()
    {
        MenuNatureFx.FitCover(artRoot, scene, ref lastRect);
        if (nature != null) nature.Tick(Time.unscaledTime, Time.unscaledDeltaTime);
        DuelBackend.Current.Tick();

        float t = Time.unscaledTime - stateT0;
        if (state == State.Searching && statusLabel != null)
            statusLabel.text = "ĐANG TÌM ĐỐI THỦ" + new string('.', ((int)(t * 2f)) % 4);

        if (state == State.Found)
        {
            float left = AcceptSeconds - t;
            if (timerLabel != null) timerLabel.text = "Còn " + Mathf.CeilToInt(Mathf.Max(0f, left)) + " giây để trả lời";
            if (left <= 0f) Decline();
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
    }
}