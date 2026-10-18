using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Đồng hồ 3 phút + điểm hai bên, hiện trên cùng mọi scene game khi đang đối kháng.</summary>
public class DuelHud : MonoBehaviour
{
    const string ResultScene = "DuelResultScene";
    static readonly string[] GameScenes = { "GameplayScene", "MillionareScene", "BiologyBossScene", "RoomScene" };

    Text timeLabel, scoreLabel;
    float nextPush;
    bool done;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
    }

    static void OnLoaded(Scene s, LoadSceneMode m)
    {
        if (!DuelSession.Active || DuelSession.Finished) return;
        if (System.Array.IndexOf(GameScenes, s.name) < 0) return;
        if (FindFirstObjectByType<DuelHud>() != null) return;
        new GameObject("DuelHud").AddComponent<DuelHud>();
    }

    void Start()
    {
        DuelSession.BeginClock();
        var ui = UIKit.BuildCanvas(transform, "Duel HUD", 200);
        var plate = UIKit.CBox("Plate", ui, new Color(.02f, .02f, .05f, .78f), new Vector2(0, 490), new Vector2(560, 84));
        timeLabel = UIKit.Label("Time", plate, "3:00", 44, GameTheme.GoldHi, TextAnchor.MiddleCenter,
            new Vector2(0, 14), new Vector2(520, 50), true, true, FontStyle.Bold);
        scoreLabel = UIKit.Label("Score", plate, "", 24, GameTheme.Bone, TextAnchor.MiddleCenter,
            new Vector2(0, -26), new Vector2(540, 32));
    }

    void Update()
    {
        if (done || DuelSession.Finished) return;

        float r = DuelSession.Remaining;
        timeLabel.text = ((int)r / 60) + ":" + ((int)r % 60).ToString("00");
        timeLabel.color = r <= 30f ? GameTheme.Blood : GameTheme.GoldHi;
        scoreLabel.text = "Bạn " + DuelSession.MyScore + "   •   " + DuelSession.OppNick + " " + DuelBackend.Current.PeerScore;

        if (Time.unscaledTime >= nextPush)
        {
            nextPush = Time.unscaledTime + 1f;
            DuelBackend.Current.PushScore(DuelSession.MyScore, false);
        }

        if (r <= 0f)
        {
            done = true;
            DuelSession.Finish(DuelSession.MyScore);
            SceneRouter.Go(ResultScene);
        }
    }
}