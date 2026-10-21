#if DUEL_UGS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>Chạy Tick() của UgsBackend mỗi frame, sống xuyên các scene.</summary>
public class UgsPump : MonoBehaviour
{
    public Action tick;
    void Update() { if (tick != null) tick(); }
}

/// <summary>Đối kháng online bằng Unity Gaming Services (đăng nhập ẩn danh + Multiplayer Services Sessions).</summary>
public class UgsBackend : IDuelBackend
{
    const string KNick = "nick", KReady = "ready", KSubject = "sub", KSeed = "seed";
    const string KChoice = "c";                 // c0, c1, ... = "đáp án,mức cược"
    const int MaxRounds = 16;
    const float PollEvery = 1.5f, HostWaitMin = 7f, HostWaitMax = 12f, LeaveGrace = 3f, GhostWait = 5f;

    enum Phase { Idle, Searching, Hosting, Matched }

    // kết nối
    string myNick = "", error;
    bool initBusy, initDone;
    UgsPump pump;
    int lastFrame = -1;

    // phòng
    ISession session;
    Phase phase = Phase.Idle;
    bool busy, lost, matchStarted;
    int gen, fails;                              // gen tăng mỗi lần rời phòng để bỏ kết quả của lời gọi cũ
    float nextStep, hostDeadline, matchedAt, leaveAt = -1f;

    // luồng ghép cặp
    Action<string> onFound;
    Action<bool> onAnswer;
    bool foundFired, everHadPeer, peerPresent, wantReady, readySent, needReadyReset;

    // dữ liệu từ phòng
    string peerNick = "", peerReady = "0", peerSubject, wantSubject;
    int seed;
    readonly bool[] peerHas = new bool[MaxRounds];
    readonly DuelChoice[] peerChoice = new DuelChoice[MaxRounds];
    readonly Dictionary<int, string> outbox = new Dictionary<int, string>();   // lựa chọn của mình chờ gửi

    // ------------------------------------------------------------ thuộc tính
    public bool IsHost { get { return session != null && session.IsHost; } }
    public int Seed { get { return seed; } }
    public string Error { get { return error; } }
    public string PeerSubject { get { return peerSubject; } }
    public bool PeerLeft { get { return lost || (matchStarted && everHadPeer && !peerPresent); } }

    // phần của game cũ (tính điểm 3 phút), game mới không dùng
    public int PeerScore { get { return 0; } }
    public bool PeerFinished { get { return PeerLeft; } }
    public void RoundStarted() { }
    public void PushScore(int score, bool finished) { }

    // ------------------------------------------------------------ IDuelBackend
    public void Connect(string nick)
    {
        myNick = string.IsNullOrWhiteSpace(nick) ? "Người chơi" : nick;
        EnsurePump();
        LeaveNow();
        phase = Phase.Idle;
        ResetMatchData();
        StartInitIfNeeded();
    }

    public void FindOpponent(Action<string> cb)
    {
        LeaveNow();
        ResetMatchData();
        StartInitIfNeeded();
        onFound = cb;
        phase = Phase.Searching;
        nextStep = 0f;
    }

    public void CancelSearch()
    {
        LeaveNow();
        phase = Phase.Idle;
        onFound = null; onAnswer = null;
    }

    public void Respond(bool accept, Action<bool> cb)
    {
        if (!accept)
        {   // từ chối = rời phòng; bên kia sẽ thấy mình biến mất
            LeaveNow(); phase = Phase.Idle; onAnswer = null;
            return;
        }
        if (lost || session == null) { if (cb != null) cb(false); return; }
        onAnswer = cb; wantReady = true; nextStep = 0f;
    }

    public void SetSubject(string subject)
    {
        wantSubject = string.IsNullOrEmpty(subject) ? "all" : subject;
        nextStep = 0f;
    }

    public void BeginRound(int round, int correctIndex) { matchStarted = true; }   // bản online không cần đáp án đúng

    public void SendChoice(int round, DuelChoice choice)
    {
        if (round < 0 || round >= MaxRounds) return;
        outbox[round] = choice.answer + "," + choice.bet;
        nextStep = 0f;                                                              // gửi sớm nhất có thể
    }

    public bool TryGetPeerChoice(int round, out DuelChoice choice)
    {
        if (round >= 0 && round < MaxRounds && peerHas[round]) { choice = peerChoice[round]; return true; }
        choice = DuelChoice.None;
        return false;
    }

    public void Leave()
    {
        if (session == null) return;
        leaveAt = Time.unscaledTime + LeaveGrace;                                   // chờ vài giây để đối thủ kịp đọc lựa chọn cuối
    }

    public void Tick()
    {
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        float now = Time.unscaledTime;

        if (leaveAt >= 0f && now >= leaveAt) { LeaveNow(); phase = Phase.Idle; return; }
        if (phase == Phase.Idle || busy || !initDone || now < nextStep) return;
        _ = StepAsync();
    }

    // ------------------------------------------------------------ khởi tạo + đăng nhập ẩn danh
    void EnsurePump()
    {
        if (pump != null) return;
        var go = new GameObject("UgsPump");
        UnityEngine.Object.DontDestroyOnLoad(go);
        pump = go.AddComponent<UgsPump>();
        pump.tick = Tick;
    }

    void StartInitIfNeeded()
    {
        if (initDone || initBusy) return;
        error = null;
        _ = InitAsync();
    }

    async Task InitAsync()
    {
        initBusy = true;
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
            initDone = true; error = null;
            Debug.Log("[Duel/UGS] Đã đăng nhập ẩn danh, PlayerId = " + AuthenticationService.Instance.PlayerId);
        }
        catch (Exception e)
        {
            error = e.Message;
            Debug.LogWarning("[Duel/UGS] Khởi tạo lỗi: " + e.Message);
        }
        initBusy = false;
    }

    // ------------------------------------------------------------ vòng lặp chính (mỗi lần chỉ một lời gọi mạng)
    async Task StepAsync()
    {
        busy = true;
        int g = gen;
        try
        {
            if (phase == Phase.Searching) await DoSearch(g);
            else if (phase == Phase.Hosting || phase == Phase.Matched) await DoSync(g);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Duel/UGS] " + e.Message);
            nextStep = Time.unscaledTime + 2f;
            if ((phase == Phase.Hosting || phase == Phase.Matched) && ++fails >= 3) OnSessionLost();
        }
        busy = false;
    }

    async Task DoSearch(int g)
    {
        // 1) thử vào phòng đang chờ (chưa tự tạo phòng ở bước này)
        ISession s = null;
        try
        {
            var qj = new QuickJoinOptions
            {
                Filters = new List<FilterOption> { new FilterOption(FilterField.AvailableSlots, "1", FilterOperation.GreaterOrEqual) },
                Timeout = TimeSpan.FromSeconds(2),
                CreateSession = false
            };
            s = await MultiplayerService.Instance.MatchmakeSessionAsync(qj, new SessionOptions { MaxPlayers = 2 });
        }
        catch (Exception) { s = null; }          // chưa có phòng nào đang chờ

        if (g != gen) { if (s != null) _ = SafeLeave(s); return; }

        if (s != null)
        {
            session = s; phase = Phase.Matched; fails = 0; matchedAt = Time.unscaledTime;
            await PublishMyData(s);
            nextStep = Time.unscaledTime + 0.3f;
            return;
        }

        // 2) không có phòng: tự mở phòng 2 người và đợi
        var host = await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions { MaxPlayers = 2 });
        if (g != gen) { _ = SafeLeave(host); return; }

        session = host; phase = Phase.Hosting; fails = 0;
        hostDeadline = Time.unscaledTime + Random.Range(HostWaitMin, HostWaitMax);
        seed = Random.Range(1, int.MaxValue);
        await PublishMyData(host);
        host.SetProperty(KSeed, new SessionProperty(seed.ToString(), VisibilityPropertyOptions.Member));
        await host.SavePropertiesAsync();
        nextStep = Time.unscaledTime + PollEvery;
    }

    async Task DoSync(int g)
    {
        var s = session;
        if (s == null) return;

        await s.RefreshAsync();
        if (g != gen) return;
        fails = 0;
        ReadRoom(s);

        float now = Time.unscaledTime;
        if (phase == Phase.Hosting)
        {
            if (peerPresent) phase = Phase.Matched;
            else if (now >= hostDeadline)
            {   // chờ lâu mà chưa ai vào: bỏ phòng và tìm lại, để hai người mở phòng cùng lúc vẫn gặp nhau
                LeaveNow(); phase = Phase.Searching; nextStep = now + 0.5f;
                return;
            }
        }
        else if (phase == Phase.Matched && !peerPresent && !everHadPeer && now - matchedAt > GhostWait)
        {   // vào phải phòng "ma" (chủ phòng đã rời): rời và tìm lại
            LeaveNow(); phase = Phase.Searching; nextStep = now + 1f;
            return;
        }

        DispatchEvents(s);
        if (g != gen) return;                    // callback của UI có thể đã đổi trạng thái

        // ghi dữ liệu của mình lên phòng
        var me = s.CurrentPlayer;
        bool dirty = false;
        if (needReadyReset) { me.SetProperty(KReady, P("0")); needReadyReset = false; dirty = true; }
        if (wantReady && !readySent) { me.SetProperty(KReady, P("1")); readySent = true; dirty = true; }

        List<int> sending = null;
        if (outbox.Count > 0)
        {
            sending = new List<int>(outbox.Keys);
            foreach (int r in sending) me.SetProperty(KChoice + r, P(outbox[r]));
            dirty = true;
        }
        if (dirty)
        {
            await s.SaveCurrentPlayerDataAsync();
            if (g != gen) return;
            if (sending != null) foreach (int r in sending) outbox.Remove(r);
        }

        // host ghi môn đã chọn
        if (wantSubject != null && s.IsHost)
        {
            var h = s.AsHost();
            h.SetProperty(KSubject, new SessionProperty(wantSubject, VisibilityPropertyOptions.Member));
            await h.SavePropertiesAsync();
            if (g != gen) return;
            wantSubject = null;
        }

        nextStep = Time.unscaledTime + PollEvery;
    }

    // ------------------------------------------------------------ đọc phòng + phát sự kiện cho UI
    void ReadRoom(ISession s)
    {
        string myId = AuthenticationService.Instance.PlayerId;
        IReadOnlyPlayer peer = null;
        foreach (var p in s.Players) if (p.Id != myId) { peer = p; break; }

        peerPresent = peer != null;
        if (peer != null)
        {
            everHadPeer = true;
            peerNick = Prop(peer, KNick);
            peerReady = Prop(peer, KReady, "0");

            for (int r = 0; r < MaxRounds; r++)
            {
                string v = Prop(peer, KChoice + r);
                if (v == "") continue;
                int comma = v.IndexOf(',');
                int a, b;
                if (comma > 0 && int.TryParse(v.Substring(0, comma), out a) && int.TryParse(v.Substring(comma + 1), out b))
                {
                    peerHas[r] = true;
                    peerChoice[r] = new DuelChoice(a, b);
                }
            }
        }

        SessionProperty sp;
        if (s.Properties != null)
        {
            if (s.Properties.TryGetValue(KSubject, out sp)) peerSubject = sp.Value;
            if (!s.IsHost && s.Properties.TryGetValue(KSeed, out sp))
            {
                int sd; if (int.TryParse(sp.Value, out sd)) seed = sd;
            }
        }
    }

    void DispatchEvents(ISession s)
    {
        // tìm thấy đối thủ (đã có bí danh)
        if (peerPresent && !foundFired && peerNick != "" && onFound != null)
        {
            foundFired = true;
            onFound(peerNick);
            return;
        }

        // đối thủ rời đi trước khi trận bắt đầu
        if (!peerPresent && everHadPeer && !matchStarted)
        {
            var cb = onAnswer; onAnswer = null;
            wantReady = readySent = false; needReadyReset = true;
            ResetPeerState();
            if (s.IsHost && phase == Phase.Matched)
            {   // mình còn trong phòng một mình: tiếp tục chờ người khác
                phase = Phase.Hosting;
                hostDeadline = Time.unscaledTime + Random.Range(HostWaitMin, HostWaitMax);
            }
            if (cb != null) cb(false);
            return;
        }

        // đối thủ đã đồng ý
        if (foundFired && onAnswer != null && peerReady == "1")
        {
            var cb = onAnswer; onAnswer = null;
            cb(true);
        }
    }

    void OnSessionLost()
    {
        fails = 0;
        LeaveNow();
        lost = true; phase = Phase.Idle;
        if (onAnswer != null) { var cb = onAnswer; onAnswer = null; cb(false); }
    }

    // ------------------------------------------------------------ tiện ích
    async Task PublishMyData(ISession s)
    {
        var me = s.CurrentPlayer;
        me.SetProperty(KNick, P(myNick));
        me.SetProperty(KReady, P("0"));
        await s.SaveCurrentPlayerDataAsync();
    }

    void LeaveNow()
    {
        gen++;
        leaveAt = -1f;
        var s = session; session = null;
        if (s != null) _ = SafeLeave(s);
    }

    static async Task SafeLeave(ISession s)
    {
        try { await s.LeaveAsync(); } catch (Exception) { }
    }

    void ResetMatchData()
    {
        ResetPeerState();
        wantReady = readySent = needReadyReset = false;
        matchStarted = lost = false;
        wantSubject = null; peerSubject = null; seed = 0; fails = 0;
        outbox.Clear(); onAnswer = null;
    }

    void ResetPeerState()
    {
        foundFired = everHadPeer = peerPresent = false;
        peerNick = ""; peerReady = "0";
        for (int i = 0; i < MaxRounds; i++) peerHas[i] = false;
    }

    static string Prop(IReadOnlyPlayer p, string key, string def = "")
    {
        PlayerProperty v;
        return p.Properties != null && p.Properties.TryGetValue(key, out v) && v != null ? v.Value : def;
    }

    static PlayerProperty P(string v) { return new PlayerProperty(v, VisibilityPropertyOptions.Member); }
}
#endif