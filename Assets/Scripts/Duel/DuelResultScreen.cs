using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Màn kết quả đối kháng: điểm hai bên, thắng/thua/hòa. Chờ đối thủ nếu họ chưa xong.</summary>
public class DuelResultScreen : MonoBehaviour
{
    const string ResultScene = "DuelResultScene";

    RectTransform artRoot, scene, ui, content;
    MenuNatureFx nature;
    Vector2 lastRect;
    Text oppScoreLabel, verdictLabel, subLabel;
    Button againBtn;
    bool decided, noMatch;
    int peerScore;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        if (s.name != ResultScene || FindFirstObjectByType<DuelResultScreen>() != null) return;
        new GameObject("DuelResultScreen").AddComponent<DuelResultScreen>();
    }

    void Start()
    {
        Time.timeScale = 1f;
        noMatch = !DuelSession.Active;
        GameAudio.StopMusic(); RogueAudio.Stop();

        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = GameTheme.Ink; }

        UIKit.EnsureEventSystem();
        SceneShell.Create(ShellMood.Hub, null, 1f, true, false);

        artRoot = UIKit.BuildCanvas(transform, "Result Art", -90);
        scene = UIKit.CBox("Scene", artRoot, Color.clear, Vector2.zero, new Vector2(1920f, 1080f));
        nature = new MenuNatureFx();
        nature.BuildForest(scene, 250f, 20);

        ui = UIKit.BuildCanvas(transform, "Result UI", 10);
        UIKit.Title(ui, "KẾT QUẢ ĐỐI KHÁNG", new Vector2(0, 410), 72);
        UIKit.Panel(ui, "ResultPanel", new Vector2(0, -20), new Vector2(1100, 640), PanelStyle.Dark, out content);

        string me = GameSettings.Nickname;
        string opp = string.IsNullOrEmpty(DuelSession.OppNick) ? "Đối thủ" : DuelSession.OppNick;

        UIKit.Label("MeName", content, me, 38, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(-300, 160), new Vector2(420, 54));
        UIKit.Label("MeScore", content, DuelSession.FinalScore.ToString(), 110, GameTheme.GoldHi, TextAnchor.MiddleCenter,
            new Vector2(-300, 50), new Vector2(420, 130), true, true, FontStyle.Bold);
        UIKit.Label("Vs", content, "VS", 46, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, 100), new Vector2(160, 60), true, true, FontStyle.Bold);
        UIKit.Label("OppName", content, opp, 38, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(300, 160), new Vector2(420, 54));
        oppScoreLabel = UIKit.Label("OppScore", content, "0", 110, GameTheme.GoldHi, TextAnchor.MiddleCenter,
            new Vector2(300, 50), new Vector2(420, 130), true, true, FontStyle.Bold);

        verdictLabel = UIKit.Label("Verdict", content, "", 64, GameTheme.GoldHi, TextAnchor.MiddleCenter,
            new Vector2(0, -90), new Vector2(980, 90), true, true, FontStyle.Bold);
        subLabel = UIKit.Label("Sub", content, "", 28, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, -160), new Vector2(980, 44));

        againBtn = UIKit.MakeButton(content, "Btn_Again", "ĐẤU LẠI", new Vector2(-230, -245), new Vector2(380, 76), Again, ButtonStyle.Bronze, 36, .1f);
        UIKit.MakeButton(content, "Btn_Menu", "VỀ MENU", new Vector2(230, -245), new Vector2(380, 76), Menu, ButtonStyle.Stone, 36, .16f);
        againBtn.interactable = false;

        MenuNatureFx.FitCover(artRoot, scene, ref lastRect);

        if (noMatch) { verdictLabel.text = "KHÔNG CÓ TRẬN ĐẤU"; againBtn.interactable = true; decided = true; }
    }

    void OnDestroy() { if (nature != null) nature.Dispose(); }

    void Update()
    {
        MenuNatureFx.FitCover(artRoot, scene, ref lastRect);
        if (nature != null) nature.Tick(Time.unscaledTime, Time.unscaledDeltaTime);
        if (decided) return;

        var be = DuelBackend.Current;
        peerScore = be.PeerScore;
        oppScoreLabel.text = peerScore.ToString();

        bool over = be.PeerFinished || DuelSession.Remaining <= 0f;
        if (!over)
        {
            int r = Mathf.CeilToInt(DuelSession.Remaining);
            verdictLabel.text = "ĐANG CHỜ ĐỐI THỦ…";
            subLabel.text = "Đối thủ vẫn đang làm bài, còn " + (r / 60) + ":" + (r % 60).ToString("00");
            return;
        }
        Decide();
    }

    void Decide()
    {
        decided = true;
        int mine = DuelSession.FinalScore;
        oppScoreLabel.text = peerScore.ToString();
        subLabel.text = "";
        if (mine > peerScore) { verdictLabel.text = "BẠN THẮNG!"; verdictLabel.color = GameTheme.Moss; GameAudio.Play(Snd.Victory); }
        else if (mine < peerScore) { verdictLabel.text = "BẠN THUA"; verdictLabel.color = GameTheme.Blood; GameAudio.Play(Snd.Defeat); }
        else { verdictLabel.text = "HÒA"; verdictLabel.color = GameTheme.Gold; }
        againBtn.interactable = true;
    }

    void Again() { DuelSession.Reset(); SceneRouter.Go("DuelLobbyScene"); }
    void Menu() { SceneRouter.Go("MainMenu"); }
}