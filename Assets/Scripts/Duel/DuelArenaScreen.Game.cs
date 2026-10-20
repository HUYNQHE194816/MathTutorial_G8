using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Luật chơi "Cược Tri Thức" theo vòng: câu hỏi, chọn đáp án + mức cược, chờ đối thủ, công bố, tổng kết.</summary>
public partial class DuelArenaScreen
{
    const float RoundSeconds = 20f;     // thời gian suy nghĩ mỗi vòng
    const float PeerTimeout = 25f;      // chờ đối thủ quá lâu sau khi mình khóa = coi như họ đã rời
    const float RevealSeconds = 4f;     // thời gian giữ màn công bố
    const bool PracticeFast = true;     // chế độ thử một mình: bot trả lời nhanh hơn cho đỡ chờ

    enum Phase { None, Question, Wait, Reveal, End }

    IDuelBackend be;
    List<DuelQuestion> questions;
    DuelMatch match;
    DuelChoice myChoice;
    Phase phase;
    float phaseT0;
    int round, selAnswer, selBet;
    bool practice;

    Text levelText, questionText, statusText, revealMe, revealOpp;
    readonly Button[] ansBtn = new Button[4];
    readonly Text[] ansTxt = new Text[4];
    readonly Image[] ansMark = new Image[4], betMark = new Image[4];
    Button lockBtn;
    GameObject endRoot;

    static readonly Color Gold = new Color(1f, .82f, .35f);
    static readonly Color GoodCol = new Color(.55f, 1f, .6f);
    static readonly Color BadCol = new Color(1f, .5f, .45f);

    // ------------------------------------------------------------------ khởi động trận
    partial void GameStart()
    {
        be = DuelBackend.Current;
        practice = !DuelSession.Active;          // mở thẳng scene này = chơi thử một mình với bot
        if (practice)
        {
            be.Connect(GameSettings.Nickname);
            if (be is BotBackend) BotBackend.DelayScale = PracticeFast ? .3f : 1f;
        }

        int seed = DuelSession.Seed != 0 ? DuelSession.Seed : Random.Range(1, int.MaxValue);
        questions = DuelQuestionBank.BuildMatch(seed, null, DuelQuestionBank.DefaultPlan);
        match = new DuelMatch(questions.Count);

        BuildGameUi();

        if (questions.Count == 0)
        {
            SetStatus("Không nạp được câu hỏi (kiểm tra Assets/Resources/Duel/DuelBank.csv).");
            phase = Phase.None;
            return;
        }
        StartRound(0);
    }

    // ------------------------------------------------------------------ giao diện chơi
    void BuildGameUi()
    {
        // bảng câu hỏi
        RectTransform qc;
        UIKit.Panel(ui, "QPanel", new Vector2(0f, 215f), new Vector2(1060f, 230f), PanelStyle.Dark, out qc);
        levelText = UIKit.Label("Level", qc, "", 24, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0f, 66f), new Vector2(960f, 32f));
        questionText = UIKit.Label("Question", qc, "", 30, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0f, -12f), new Vector2(960f, 130f));

        // bốn đáp án
        Vector2[] ap = { new Vector2(-245f, 45f), new Vector2(245f, 45f), new Vector2(-245f, -55f), new Vector2(245f, -55f) };
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            ansMark[i] = Marker(ap[i], new Vector2(470f, 84f));
            ansBtn[i] = UIKit.MakeButton(ui, "Ans" + i, "", ap[i], new Vector2(470f, 84f), () => PickAnswer(idx), ButtonStyle.Stone, 26, .02f * i);
            ansTxt[i] = ansBtn[i].GetComponentInChildren<Text>();
            ansTxt[i].resizeTextForBestFit = true;
            ansTxt[i].resizeTextMinSize = 14;
            ansTxt[i].resizeTextMaxSize = Mathf.RoundToInt(26 * GameFont.Scale);
        }

        // bốn mức cược
        UIKit.Label("BetHint", ui, "MỨC CƯỢC  (đúng: gây sát thương bằng mức cược  ·  sai: tự mất một nửa  ·  thủ thế: nhận một nửa sát thương)",
            22, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0f, -114f), new Vector2(1300f, 30f));
        string[] names = { "THỦ THẾ", "CƯỢC 10", "CƯỢC 20", "CƯỢC 30" };
        for (int i = 0; i < 4; i++)
        {
            int val = DuelRules.Bets[i];
            Vector2 pos = new Vector2(-345f + i * 230f, -165f);
            betMark[i] = Marker(pos, new Vector2(220f, 66f));
            UIKit.MakeButton(ui, "Bet" + val, names[i], pos, new Vector2(220f, 66f), () => PickBet(val),
                i == 0 ? ButtonStyle.Secondary : ButtonStyle.Bronze, 28, .1f + .03f * i);
        }

        lockBtn = UIKit.MakeButton(ui, "Btn_Lock", "KHÓA ĐÁP ÁN", new Vector2(0f, -270f), new Vector2(420f, 84f), Lock, ButtonStyle.Primary, 36, .2f);
        statusText = UIKit.Label("Status", ui, "", 30, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0f, -360f), new Vector2(1300f, 80f));

        // chữ công bố phía trên mỗi hiệp sĩ (cùng hệ toạ độ với hiệp sĩ)
        revealMe = UIKit.Label("RevealMe", scene, "", 30, GameTheme.Bone, TextAnchor.MiddleCenter,
            me.home + new Vector2(50f, 320f), new Vector2(260f, 90f), false, true, FontStyle.Bold);
        revealOpp = UIKit.Label("RevealOpp", scene, "", 30, GameTheme.Bone, TextAnchor.MiddleCenter,
            opp.home + new Vector2(-50f, 320f), new Vector2(260f, 90f), false, true, FontStyle.Bold);
    }

    // Viền sáng nằm sau nút (tạo trước nút nên được vẽ phía sau); bật/tắt để đánh dấu lựa chọn.
    Image Marker(Vector2 pos, Vector2 size)
    {
        var rt = UIKit.CBox("Sel", ui, Gold, pos, size + new Vector2(16f, 16f));
        rt.gameObject.SetActive(false);
        return rt.GetComponent<Image>();
    }

    void SetStatus(string s) { if (statusText != null) statusText.text = s; }

    // ------------------------------------------------------------------ một vòng
    void StartRound(int r)
    {
        round = r;
        var q = questions[r];
        phase = Phase.Question;
        phaseT0 = Time.unscaledTime;
        selAnswer = -1;
        selBet = 10;

        roundText.text = "VÒNG " + (r + 1) + " / " + questions.Count;
        levelText.text = LevelName(q.level) + "   ·   " + (q.subject ?? "").ToUpperInvariant();
        questionText.text = q.text;
        for (int i = 0; i < 4; i++) ansTxt[i].text = (char)('A' + i) + ".  " + q.options[i];

        revealMe.text = revealOpp.text = "";
        lockBtn.interactable = true;
        RefreshSelection();
        SetStatus("Chọn đáp án và mức cược, rồi bấm KHÓA.");

        be.BeginRound(r, q.correct);
    }

    static string LevelName(int level) { return level == 0 ? "NHẬN BIẾT" : level == 1 ? "THÔNG HIỂU" : "VẬN DỤNG"; }

    void PickAnswer(int i)
    {
        if (phase != Phase.Question) return;
        selAnswer = i;
        RefreshSelection();
    }

    void PickBet(int value)
    {
        if (phase != Phase.Question) return;
        selBet = value;
        RefreshSelection();
    }

    void RefreshSelection()
    {
        for (int i = 0; i < 4; i++)
        {
            ansMark[i].color = Gold;
            ansMark[i].gameObject.SetActive(i == selAnswer);
            betMark[i].color = Gold;
            betMark[i].gameObject.SetActive(DuelRules.Bets[i] == selBet);
        }
    }

    void Lock() { LockChoice(false); }

    void LockChoice(bool timedOut)
    {
        if (phase != Phase.Question) return;
        if (!timedOut && selBet > 0 && selAnswer < 0)
        {
            SetStatus("Hãy chọn một đáp án trước khi cược (hoặc chọn THỦ THẾ).");
            return;
        }

        // Hết giờ: tự khóa theo lựa chọn hiện tại; chưa chọn đáp án thì thành bỏ trống
        myChoice = DuelRules.Sanitize(new DuelChoice(selAnswer, selBet));
        be.SendChoice(round, myChoice);

        phase = Phase.Wait;
        phaseT0 = Time.unscaledTime;
        lockBtn.interactable = false;
        SetStatus(timedOut ? "Hết giờ! Đang chờ đối thủ…" : "Đã khóa. Đang chờ đối thủ…");
    }

    void Reveal(DuelChoice peerRaw)
    {
        var q = questions[round];
        var peer = DuelRules.Sanitize(peerRaw);
        var res = match.Play(myChoice, peer, q.correct);

        phase = Phase.Reveal;
        phaseT0 = Time.unscaledTime;
        timerText.text = "";

        SetHp(me, match.myHp);
        SetHp(opp, match.oppHp);
        Hit(me, res.myLoss);
        Hit(opp, res.oppLoss);

        revealMe.text = Describe(myChoice, res.myCorrect);
        revealMe.color = myChoice.answer < 0 ? GameTheme.Fog : res.myCorrect ? GoodCol : BadCol;
        revealOpp.text = Describe(peer, res.oppCorrect);
        revealOpp.color = peer.answer < 0 ? GameTheme.Fog : res.oppCorrect ? GoodCol : BadCol;

        // đáp án đúng sáng xanh; đáp án sai của mình sáng đỏ
        for (int i = 0; i < 4; i++)
        {
            bool isRight = i == q.correct;
            bool myWrong = i == myChoice.answer && !isRight;
            ansMark[i].gameObject.SetActive(isRight || myWrong);
            ansMark[i].color = isRight ? GoodCol : BadCol;
        }
        SetStatus("Đáp án đúng: " + (char)('A' + q.correct) + ".  " + q.options[q.correct]);
    }

    static string Describe(DuelChoice c, bool correct)
    {
        if (c.answer < 0) return "Bỏ trống\n(thủ thế)";
        string bet = c.bet == 0 ? "thủ thế" : "cược " + c.bet;
        return "Chọn " + (char)('A' + c.answer) + " · " + bet + "\n" + (correct ? "ĐÚNG" : "SAI");
    }

    // ------------------------------------------------------------------ vòng lặp theo trạng thái
    partial void GameTick()
    {
        if (phase == Phase.None || be == null) return;
        float now = Time.unscaledTime;
        be.Tick();

        switch (phase)
        {
            case Phase.Question:
                {
                    float left = RoundSeconds - (now - phaseT0);
                    timerText.text = Mathf.CeilToInt(Mathf.Max(0f, left)).ToString();
                    timerText.color = left <= 5f ? new Color(1f, .4f, .35f) : GameTheme.Bone;
                    ReadShortcuts();
                    if (left <= 0f) LockChoice(true);
                    break;
                }
            case Phase.Wait:
                {
                    timerText.text = "";
                    DuelChoice peer;
                    if (be.TryGetPeerChoice(round, out peer)) Reveal(peer);
                    else if (be.PeerLeft || now - phaseT0 > PeerTimeout) EndMatch(true);
                    break;
                }
            case Phase.Reveal:
                if (now - phaseT0 > RevealSeconds)
                {
                    if (match.Over) EndMatch(false);
                    else StartRound(round + 1);
                }
                break;
        }
    }

    // Phím tắt: 1-4 chọn đáp án A-D, Enter khóa
    void ReadShortcuts()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame) PickAnswer(0);
        if (kb.digit2Key.wasPressedThisFrame) PickAnswer(1);
        if (kb.digit3Key.wasPressedThisFrame) PickAnswer(2);
        if (kb.digit4Key.wasPressedThisFrame) PickAnswer(3);
        if (kb.enterKey.wasPressedThisFrame) Lock();
    }

    // ------------------------------------------------------------------ tổng kết
    void EndMatch(bool opponentGone)
    {
        phase = Phase.End;
        int w = opponentGone ? 1 : match.Winner;
        string title = w > 0 ? "BẠN THẮNG!" : w < 0 ? "BẠN THUA" : "HÒA";
        string detail = "Máu: " + match.myHp + " – " + match.oppHp +
                        "          Câu đúng: " + match.myCorrectCount + " – " + match.oppCorrectCount +
                        (opponentGone ? "\nĐối thủ đã rời trận." : "");

        BuildEndPanel(title, detail, w);
        if (w > 0) GameAudio.Play(Snd.Victory);
        else if (w < 0) GameAudio.Play(Snd.Defeat);
    }

    void BuildEndPanel(string title, string detail, int winner)
    {
        var dim = UIKit.Box("EndDim", ui, new Color(0f, 0f, 0f, .6f), Vector2.zero, Vector2.one, UIKit.C, Vector2.zero, Vector2.zero, true);
        UIKit.Stretch(dim, 0, 0, 0, 0);
        endRoot = dim.gameObject;

        RectTransform c;
        UIKit.Panel(dim, "EndPanel", Vector2.zero, new Vector2(900f, 520f), PanelStyle.Dark, out c);
        Color tc = winner > 0 ? GoodCol : winner < 0 ? BadCol : GameTheme.GoldHi;
        UIKit.Label("T", c, title, 84, tc, TextAnchor.MiddleCenter, new Vector2(0f, 150f), new Vector2(800f, 110f), true, true, FontStyle.Bold);
        UIKit.Label("D", c, detail, 34, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0f, 20f), new Vector2(800f, 130f));
        UIKit.MakeButton(c, "Btn_Again", "ĐẤU LẠI", new Vector2(-210f, -170f), new Vector2(360f, 76f), RestartMatch, ButtonStyle.Bronze, 36, .1f);
        UIKit.MakeButton(c, "Btn_Menu", "VỀ MENU", new Vector2(210f, -170f), new Vector2(360f, 76f), GoMenu, ButtonStyle.Stone, 36, .16f);
    }

    // Đấu lại ngay trong scene này (chỉ đúng cho chế độ thử một mình; bước nối với sảnh sẽ đổi lại)
    void RestartMatch()
    {
        if (endRoot != null) Destroy(endRoot);
        questions = DuelQuestionBank.BuildMatch(Random.Range(1, int.MaxValue), null, DuelQuestionBank.DefaultPlan);
        match = new DuelMatch(questions.Count);
        SetHp(me, DuelRules.MaxHp);
        SetHp(opp, DuelRules.MaxHp);
        if (practice) be.Connect(GameSettings.Nickname);
        StartRound(0);
    }

    void GoMenu()
    {
        if (!Application.CanStreamedLevelBeLoaded("MainMenu")) { UIKit.Toast(ui, "Chưa có MainMenu trong Build Settings"); return; }
        SceneRouter.Go("MainMenu");
    }
}