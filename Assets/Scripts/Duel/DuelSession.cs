using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;   // tránh nhập nhằng với System.Random

public enum DuelMode { None, Quiz, Essay }

/// <summary>Dữ liệu một trận đối kháng, sống xuyên các scene (giống SubjectSession).</summary>
/// <summary>Dữ liệu một trận đối kháng, sống xuyên các scene (giống SubjectSession).</summary>
public static class DuelSession
{
    public const float Duration = 180f;          // 3 phút

    // Các scene thuộc về một trận đang diễn ra. Nạp scene khác giữa lúc đang tính giờ = bỏ cuộc.
    static readonly string[] DuelScenes = { "GameplayScene", "MillionareScene", "BiologyBossScene", "RoomScene", "DuelResultScene" };

    public static bool Active;
    public static DuelMode Mode;
    public static string OppNick = "";
    public static float EndAt;                    // mốc hết giờ (realtimeSinceStartup); 0 = chưa tính giờ
    public static bool Finished;                  // mình đã xong (hết giờ hoặc ván kết thúc)
    public static int FinalScore;
    public static int EssayPoints;                // điểm tự luận cộng dồn (RoomManager ghi vào)
    public static Func<int> ScoreProbe;           // game trắc nghiệm đăng ký: () => điểm hiện tại

    public static int MyScore
    {
        get
        {
            if (Finished) return FinalScore;
            if (Mode == DuelMode.Essay) return EssayPoints;
            return ScoreProbe != null ? ScoreProbe() : 0;
        }
    }

    public static float Remaining { get { return EndAt <= 0f ? Duration : Mathf.Max(0f, EndAt - Time.realtimeSinceStartup); } }

    public static void Begin(DuelMode mode, string opp)
    {
        Reset();
        Active = true; Mode = mode; OppNick = opp ?? "";
    }

    /// <summary>Bắt đầu tính 3 phút (gọi lần đầu khi vào scene game; các lần sau không làm gì).</summary>
    public static void BeginClock()
    {
        if (!Active || EndAt > 0f) return;
        EndAt = Time.realtimeSinceStartup + Duration;
        DuelBackend.Current.RoundStarted();
    }

    /// <summary>Chốt điểm của mình. Gọi nhiều lần cũng chỉ tính lần đầu.</summary>
    public static void Finish(int score)
    {
        if (!Active || Finished) return;
        Finished = true; FinalScore = score; ScoreProbe = null;
        DuelBackend.Current.PushScore(score, true);
    }

    public static void Reset()
    {
        Active = false; Mode = DuelMode.None; OppNick = "";
        EndAt = 0f; Finished = false; FinalScore = 0; EssayPoints = 0; ScoreProbe = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        Reset();
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
    }

    static void OnLoaded(Scene s, LoadSceneMode m)
    {
        // Về menu / chọn chế độ = thoát trận
        if (s.name == "MainMenu" || s.name == "ModeSelectScene") { Reset(); return; }
        // Đang tính giờ mà sang scene lạ (vd bấm "Về menu" trong game) = bỏ cuộc
        if (Active && EndAt > 0f && Array.IndexOf(DuelScenes, s.name) < 0) Reset();
    }
}

// ============================================================================ backend
/// <summary>Điểm cắm cho dịch vụ online. Hiện có BotBackend (giả); sau này thay bằng UGS/Firebase.</summary>
public interface IDuelBackend
{
    void Connect(string myNick);
    void FindOpponent(Action<string> onFound);          // ghép ngẫu nhiên; gọi onFound(bí danh đối thủ)
    void CancelSearch();
    void Respond(bool accept, Action<bool> onPeerAnswer); // mình đồng ý/từ chối; onPeerAnswer(đối thủ có đồng ý không)
    void RoundStarted();                                 // đồng hồ 3 phút bắt đầu (phần B)
    void PushScore(int score, bool finished);            // gửi điểm của mình (phần B)
    int PeerScore { get; }
    bool PeerFinished { get; }
    void Tick();                                         // gọi mỗi frame từ UI
}

public static class DuelBackend
{
    public static IDuelBackend Current = new BotBackend();
}

/// <summary>Đối thủ giả để thử luồng khi chưa có mạng: tìm 2-4 giây, 85% đồng ý, mỗi 8-18 giây đúng một câu (10 điểm).</summary>
public class BotBackend : IDuelBackend
{
    static readonly string[] Names = { "Hiệp Sĩ Bóng Đêm", "Thỏ Ngọc", "Mèo Mun", "Rồng Con", "Cú Đêm", "Sói Xám", "Phù Thủy Nhỏ" };

    Action<string> onFound; float foundAt = -1f;
    Action<bool> onAnswer; float answerAt = -1f; bool botAccepts;
    readonly List<float> hits = new List<float>();
    float roundStart = -1f;

    public void Connect(string myNick) { CancelSearch(); roundStart = -1f; hits.Clear(); }

    public void FindOpponent(Action<string> cb)
    {
        onFound = cb; foundAt = Time.unscaledTime + Random.Range(2f, 4f);
    }

    public void CancelSearch()
    {
        onFound = null; foundAt = -1f; onAnswer = null; answerAt = -1f;
    }

    public void Respond(bool accept, Action<bool> cb)
    {
        if (!accept) { CancelSearch(); return; }
        onAnswer = cb; answerAt = Time.unscaledTime + Random.Range(1f, 2.5f);
        botAccepts = Random.value < .85f;
    }

    public void RoundStarted()
    {
        roundStart = Time.realtimeSinceStartup; hits.Clear();
        float t = 0f;
        while (true) { t += Random.Range(8f, 18f); if (t > DuelSession.Duration) break; hits.Add(t); }
    }

    public void PushScore(int score, bool finished) { }

    public int PeerScore
    {
        get
        {
            if (roundStart < 0f) return 0;
            float e = Time.realtimeSinceStartup - roundStart; int n = 0;
            foreach (var h in hits) if (h <= e) n++;
            return n * 10;
        }
    }

    public bool PeerFinished { get { return roundStart >= 0f && Time.realtimeSinceStartup - roundStart >= DuelSession.Duration; } }

    public void Tick()
    {
        float now = Time.unscaledTime;
        if (onFound != null && now >= foundAt)
        {
            var cb = onFound; onFound = null;
            cb(Names[Random.Range(0, Names.Length)]);
        }
        if (onAnswer != null && now >= answerAt)
        {
            var cb = onAnswer; onAnswer = null;
            cb(botAccepts);
        }
    }
}