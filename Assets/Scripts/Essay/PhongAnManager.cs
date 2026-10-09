using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// E2 – Cảnh Phong Ấn (bậc 1–2, gõ phím, bộ chấm giả). Toàn bộ UI dựng bằng code qua UIKit, giống các scene khác.
/// Luồng: chọn môn → boss có N lớp rune (mỗi bước barem một lớp) → viết từng bước → Chấm bài →
/// rune đúng thì vỡ (boss mất máu), đúng một phần thì nứt (50% sát thương), sai thì boss phản công nhẹ,
/// bước trống thì không có gì → làm lại các bước chưa phá với bản số liệu mới.
/// Chưa có pha chiến đấu (E5), chưa lưu tiến độ (E5), chưa dùng AI (E3).
/// </summary>
public class PhongAnManager : MonoBehaviour
{
    [SerializeField] TextAsset problemsJson;                    // Assets/DataBank/Essay/essay_problems.json
    [SerializeField] string menuSceneName = "SubjectSelectScene";

    enum Phase { Lobby, Writing, Graded, Ended }

    const int PlayerMaxHp = 5;
    const string StudentCode = "local";                         // E3 sẽ thay bằng mã học sinh thật

    RectTransform root, screen;
    List<EssayProblem> problems = new List<EssayProblem>();
    Phase phase;
    int sessionInk;                                             // Mực kiếm được trong phiên (chưa lưu)

    // ---- trạng thái một trận ----
    EssayProblem prob; EssayVariant variant; EssayGradeResult last;
    string subject, lastProblemId;
    int scaffold, bossHp, bossMax, playerHp, hintLevel, hintsPaid, attemptCounter, round;
    string[] answers; bool[] shattered, cracked; int[] dealt, lastDamage;
    string currentHint = "";
    bool victory, inkAwarded; int inkGained;

    void Start()
    {
        SceneShell.Create(ShellMood.Study);
        UIKit.EnsureEventSystem();
        root = UIKit.BuildCanvas(transform, "PhongAn_UI", 10);
        if (problemsJson != null) problems = EssayProblemBank.Parse(problemsJson.text);
        if (problems.Count == 0)
        {
            var s = NewScreen();
            UIKit.Label("Err", s, "Chưa có dữ liệu bài tự luận.\nGắn essay_problems.json vào PhongAnManager.", 40, GameTheme.Blood,
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1400, 200));
            return;
        }
        ShowLobby();
    }

    // ------------------------------------------------------------------ tiện ích
    RectTransform NewScreen()
    {
        if (screen != null) { screen.gameObject.SetActive(false); Destroy(screen.gameObject); }
        screen = UIKit.CBox("Screen", root, Color.clear, Vector2.zero, Vector2.zero);
        UIKit.Stretch(screen, 0, 0, 0, 0);
        return screen;
    }

    static string SubjectName(string s) => s == "ly" ? "Vật lí" : s == "hoa" ? "Hóa học" : "Sinh học";
    static Color SubjectColor(string s) => s == "ly" ? GameTheme.Ly : s == "hoa" ? GameTheme.Hoa : GameTheme.Sinh;

    static InputField MakeInput(Transform parent, string name, Vector2 pos, Vector2 size, string placeholder, bool multiline, Color border)
    {
        var frame = UIKit.CBox(name, parent, border, pos, size, true);
        var bg = UIKit.CBox("Bg", frame, new Color(1f, 1f, 1f, .6f), Vector2.zero, Vector2.zero, true);
        UIKit.Stretch(bg, 3, 3, 3, 3);
        var text = UIKit.Label("Text", bg, "", 26, GameTheme.PaperInk, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero);
        UIKit.Stretch(text.rectTransform, 16, 3, 16, 3); text.verticalOverflow = VerticalWrapMode.Truncate;
        var ph = UIKit.Label("Placeholder", bg, placeholder, 26, GameTheme.PaperMuted.WithAlpha(.55f), TextAnchor.MiddleLeft,
            Vector2.zero, Vector2.zero, false, false, FontStyle.Italic);
        UIKit.Stretch(ph.rectTransform, 16, 6, 16, 6);
        var input = bg.gameObject.AddComponent<InputField>();
        input.targetGraphic = bg.GetComponent<Image>();
        input.textComponent = text; input.placeholder = ph;
        input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
        input.characterLimit = 400;
        return input;
    }

    // ------------------------------------------------------------------ sảnh chọn môn
    void ShowLobby()
    {
        phase = Phase.Lobby;
        var s = NewScreen();
        UIKit.Title(s, "Phong Ấn", new Vector2(0, 330), 84);
        UIKit.Label("Sub", s, "Viết lời giải từng bước để phá các lớp rune của boss.", 34, GameTheme.Bone,
            TextAnchor.MiddleCenter, new Vector2(0, 200), new Vector2(1400, 60));
        string[] ids = { "ly", "hoa", "sinh" };
        for (int i = 0; i < ids.Length; i++)
        {
            string id = ids[i];
            UIKit.MakeButton(s, "Btn_" + id, SubjectName(id), new Vector2(-460 + i * 460, 20), new Vector2(400, 150),
                () => StartEncounter(id), ButtonStyle.Stone, 48, .1f * i);
        }
        UIKit.Label("Ink", s, "Mực trong phiên này: " + sessionInk, 32, GameTheme.EmberHi, TextAnchor.MiddleCenter,
            new Vector2(0, -150), new Vector2(900, 50));
        UIKit.MakeButton(s, "Back", "Về menu", new Vector2(0, -300), new Vector2(320, 80),
            () => SceneRouter.Go(menuSceneName), ButtonStyle.Secondary, 32, .35f);
    }

    // ------------------------------------------------------------------ bắt đầu / làm lại
    void StartEncounter(string subj)
    {
        subject = subj;
        var pool = problems.FindAll(p => p.subject == subj);
        if (pool.Count == 0) { UIKit.Toast(root, "Môn này chưa có bài."); return; }
        if (pool.Count > 1) pool.RemoveAll(p => p.id == lastProblemId);
        prob = pool[Random.Range(0, pool.Count)];
        lastProblemId = prob.id;

        scaffold = Mathf.Clamp(prob.scaffold, 1, 2);                 // E2 chỉ có bậc 1–2
        int n = prob.rubric.Length;
        answers = new string[n]; for (int i = 0; i < n; i++) answers[i] = "";
        shattered = new bool[n]; cracked = new bool[n]; dealt = new int[n]; lastDamage = new int[n];
        bossMax = bossHp = prob.MaxScore; playerHp = PlayerMaxHp;
        hintLevel = hintsPaid = 0; currentHint = ""; round = 1;
        victory = false; inkAwarded = false; inkGained = 0; last = null;
        NewVariant();
        phase = Phase.Writing;
        ShowPlay();
    }

    void NewVariant()
    {
        string prev = variant != null ? variant.statement : null;
        for (int t = 0; t < 12; t++)
        {
            variant = EssayVariantGenerator.Generate(prob, EssayVariantGenerator.SeedFor(StudentCode, prob.id, attemptCounter++));
            if (variant.statement != prev) break;                      // bài lời (Sinh) không đổi đề
        }
    }

    void OnRetry()
    {
        NewVariant();
        for (int i = 0; i < answers.Length; i++) if (!shattered[i]) answers[i] = "";
        last = null; round++;
        for (int i = 0; i < lastDamage.Length; i++) lastDamage[i] = 0;
        phase = Phase.Writing;
        ShowPlay();
    }

    // ------------------------------------------------------------------ chấm
    void OnGrade()
    {
        if (phase != Phase.Writing) return;
        bool any = false;
        for (int i = 0; i < answers.Length; i++) if (!shattered[i] && !string.IsNullOrWhiteSpace(answers[i])) any = true;
        if (!any) { UIKit.Toast(root, "Hãy viết ít nhất một bước."); return; }

        var sendAnswers = new string[answers.Length];
        for (int i = 0; i < answers.Length; i++) sendAnswers[i] = shattered[i] ? "" : answers[i];
        last = EssayFakeGrader.Grade(prob, variant, sendAnswers);

        bool anyWrong = false, anyCorrect = false; int run = 0, maxRun = 0;
        for (int i = 0; i < answers.Length; i++)
        {
            lastDamage[i] = 0;
            if (shattered[i]) continue;
            int pts = prob.rubric[i].points, dmg = 0;
            switch (last.steps[i].status)
            {
                case EssayStepStatus.Correct:
                    shattered[i] = true; cracked[i] = false; dmg = pts - dealt[i]; anyCorrect = true; run = 0; break;
                case EssayStepStatus.Partial:
                    cracked[i] = true; dmg = Mathf.Max(0, pts / 2 - dealt[i]); run = 0; break;
                case EssayStepStatus.Wrong:
                    anyWrong = true; run++; maxRun = Mathf.Max(maxRun, run); break;
                default: run = 0; break;                               // Missing: không phản công
            }
            dealt[i] += dmg; lastDamage[i] = dmg; bossHp -= dmg;
        }
        bossHp = Mathf.Max(0, bossHp);
        if (anyWrong) playerHp = Mathf.Max(0, playerHp - 1);          // phản công nhẹ: tối đa 1 máu mỗi lượt chấm

        // Sai hai bước liên tiếp: tự hiện gợi ý đầu tiên, không trừ Mực.
        if (maxRun >= 2 && hintLevel == 0 && prob.hints != null && prob.hints.Length > 0)
        { hintLevel = 1; currentHint = prob.hints[0]; }

        GameAudio.Play(anyCorrect ? Snd.QuizCorrect : Snd.QuizWrong);

        bool allBroken = true; foreach (var b in shattered) if (!b) allBroken = false;
        if (allBroken || playerHp <= 0) EndEncounter(allBroken);
        else phase = Phase.Graded;
        ShowPlay();
    }

    void EndEncounter(bool won)
    {
        victory = won; phase = Phase.Ended;
        if (!inkAwarded)
        {
            int earned = 0; foreach (var d in dealt) earned += d;
            // Mực = 10 mỗi điểm đã phá, trừ 10% cho mỗi gợi ý phải trả. Thua vẫn giữ phần các bước đã đúng.
            inkGained = Mathf.RoundToInt(earned * 10f * Mathf.Max(0f, 1f - .1f * hintsPaid));
            sessionInk += inkGained; inkAwarded = true;
        }
        GameAudio.Play(won ? Snd.Victory : Snd.Defeat);
    }

    void OnHint()
    {
        if (prob.hints == null || hintLevel >= prob.hints.Length) return;
        hintLevel++; hintsPaid++;
        currentHint = prob.hints[hintLevel - 1];
        ShowPlay();
    }

    void OnToggleScaffold()
    {
        scaffold = scaffold == 1 ? 2 : 1;
        ShowPlay();
    }

    // ------------------------------------------------------------------ màn chơi
    void ShowPlay()
    {
        var s = NewScreen();
        int n = prob.rubric.Length;
        Color sc = SubjectColor(subject);
        bool graded = phase == Phase.Graded || phase == Phase.Ended;

        UIKit.Title(s, "Phong Ấn · " + SubjectName(subject), new Vector2(0, 445), 54);
        UIKit.MakeButton(s, "Back", "Về menu", new Vector2(-800, 470), new Vector2(220, 60), ShowLobby, ButtonStyle.Secondary, 26);
        UIKit.Label("Ink", s, "Mực: " + sessionInk, 34, GameTheme.EmberHi, TextAnchor.MiddleRight, new Vector2(780, 470), new Vector2(300, 50));

        // ---- Panel boss (trái) ----
        UIKit.Panel(s, "BossPanel", new Vector2(-650, -40), new Vector2(520, 820), PanelStyle.Dark, out var bc);
        UIKit.Label("BossName", bc, "Kẻ Gác Phong Ấn", 40, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, 330), new Vector2(440, 60), true, true, FontStyle.Bold);
        UIKit.Label("Scaffold", bc, (scaffold == 1 ? "Bậc 1 · Điền rune" : "Bậc 2 · Viết từng bước") + "  ·  Lượt " + round,
            26, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, 280), new Vector2(440, 40));

        for (int i = 0; i < n; i++)
        {
            Color fill = shattered[i] ? GameTheme.Slate : cracked[i] ? GameTheme.Gold : GameTheme.EmberDeep;
            Color txt = shattered[i] ? GameTheme.Fog : cracked[i] ? GameTheme.PaperInk : GameTheme.Bone;
            var frame = UIKit.CBox("Rune" + i, bc, sc, new Vector2(0, 200 - i * 86), new Vector2(420, 70));
            var inner = UIKit.CBox("Fill", frame, fill, Vector2.zero, Vector2.zero); UIKit.Stretch(inner, 4, 4, 4, 4);
            var name = UIKit.Label("Name", inner, "Rune " + (i + 1) + " · " + prob.rubric[i].label, 26, txt, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, false, false, FontStyle.Bold);
            UIKit.Stretch(name.rectTransform, 14, 0, 150, 0);
            var st = UIKit.Label("State", inner, shattered[i] ? "Đã vỡ" : cracked[i] ? "Nứt" : "Nguyên", 26, txt, TextAnchor.MiddleRight, Vector2.zero, Vector2.zero);
            UIKit.Stretch(st.rectTransform, 0, 0, 14, 0);
        }

        UIKit.Label("BossHpText", bc, "Phong Ấn: " + bossHp + "/" + bossMax, 28, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, -180), new Vector2(440, 36));
        UIKit.ProgressBar(bc, "BossHp", new Vector2(0, -220), new Vector2(420, 30), GameTheme.Ember, out var bossFill);
        UIKit.SetBar(bossFill, bossMax > 0 ? (float)bossHp / bossMax : 0f);
        UIKit.Label("HpText", bc, "Máu hiệp sĩ: " + playerHp + "/" + PlayerMaxHp, 28, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, -275), new Vector2(440, 36));
        UIKit.ProgressBar(bc, "PlayerHp", new Vector2(0, -315), new Vector2(420, 30), GameTheme.Blood, out var hpFill);
        UIKit.SetBar(hpFill, (float)playerHp / PlayerMaxHp);
        UIKit.Label("HintInfo", bc, "Mỗi gợi ý trả phí trừ 10% Mực thưởng", 22, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, -362), new Vector2(440, 30));

        // ---- Giấy da (phải) ----
        UIKit.Panel(s, "PaperPanel", new Vector2(260, -40), new Vector2(1260, 820), PanelStyle.Parchment, out var pc);
        UIKit.Label("Statement", pc, variant.statement, 32, GameTheme.PaperInk, TextAnchor.UpperLeft, new Vector2(0, 300), new Vector2(1160, 150));

        InputField firstEmpty = null;
        for (int i = 0; i < n; i++)
        {
            int idx = i;
            float y = 150 - i * 106;
            var step = prob.rubric[i];
            UIKit.Label("StepLabel" + i, pc, scaffold == 1 ? (i + 1) + ". " + step.label : "Bước " + (i + 1), 28, GameTheme.PaperMuted,
                TextAnchor.MiddleLeft, new Vector2(-490, y), new Vector2(220, 76), false, false, FontStyle.Bold);

            // Bước đã vỡ từ lượt trước: khóa, hiện bước mẫu của bản số liệu mới.
            bool brokeBefore = shattered[i] && (phase == Phase.Writing || lastDamage[i] == 0);
            Color border = GameTheme.IronHi, fbColor = GameTheme.PaperMuted; string fb = "";
            if (brokeBefore) { border = GameTheme.Moss; fb = "Rune đã vỡ ở lượt trước. Đây là bước mẫu của bản số liệu mới."; }
            else if (graded && last != null)
            {
                switch (last.steps[i].status)
                {
                    case EssayStepStatus.Correct: border = fbColor = GameTheme.Moss; fb = "Đúng! Rune vỡ, boss mất " + lastDamage[i] + " máu."; break;
                    case EssayStepStatus.Partial: border = GameTheme.Gold; fbColor = GameTheme.Bronze; fb = "Đúng một phần, rune nứt. Mẫu: " + variant.rubricDesc[i]; break;
                    case EssayStepStatus.Wrong: border = fbColor = GameTheme.Blood; fb = "Chưa đúng, rune giữ nguyên. Mẫu: " + variant.rubricDesc[i]; break;
                    default: border = GameTheme.Fog; fb = "Em chưa viết bước này."; break;
                }
            }

            string ph = scaffold == 1 ? "Gõ " + step.label.ToLowerInvariant() + " tại đây" : "Viết bước " + (i + 1) + " của em…";
            var input = MakeInput(pc, "Input" + i, new Vector2(116, y), new Vector2(972, 70), ph, scaffold == 2, border);
            input.text = brokeBefore ? variant.rubricDesc[i] : answers[i];
            bool editable = phase == Phase.Writing && !shattered[i];
            input.interactable = editable;
            if (editable)
            {
                input.onValueChanged.AddListener(v => answers[idx] = v);
                if (firstEmpty == null && string.IsNullOrEmpty(answers[i])) firstEmpty = input;
            }
            UIKit.Label("Feedback" + i, pc, fb, 20, fbColor, TextAnchor.MiddleLeft, new Vector2(116, y - 55), new Vector2(972, 36));
        }

        UIKit.Label("Hint", pc, string.IsNullOrEmpty(currentHint) ? "" : "Gợi ý: " + currentHint, 28, GameTheme.EmberDeep,
            TextAnchor.MiddleLeft, new Vector2(0, -262), new Vector2(1160, 44), false, false, FontStyle.Italic);

        // ---- Nút ----
        if (phase == Phase.Writing)
        {
            int total = prob.hints != null ? prob.hints.Length : 0;
            var hintBtn = UIKit.MakeButton(pc, "HintBtn", hintLevel >= total ? "Hết gợi ý" : "Gợi ý (" + hintLevel + "/" + total + ")",
                new Vector2(-420, -330), new Vector2(300, 72), OnHint, ButtonStyle.Bronze, 30);
            hintBtn.interactable = hintLevel < total;
            UIKit.MakeButton(pc, "ScaffoldBtn", scaffold == 1 ? "Chuyển sang bậc 2" : "Chuyển sang bậc 1",
                new Vector2(-30, -330), new Vector2(380, 72), OnToggleScaffold, ButtonStyle.Secondary, 28);
            UIKit.MakeButton(pc, "GradeBtn", "Chấm bài", new Vector2(420, -330), new Vector2(340, 72), OnGrade, ButtonStyle.Primary, 34);
            if (firstEmpty != null) { firstEmpty.Select(); firstEmpty.ActivateInputField(); }
        }
        else if (phase == Phase.Graded)
        {
            UIKit.MakeButton(pc, "RetryBtn", "Làm lại các bước chưa phá (số liệu mới)", new Vector2(0, -330), new Vector2(760, 72), OnRetry, ButtonStyle.Primary, 30);
        }
        else ShowEndOverlay(s);
    }

    void ShowEndOverlay(RectTransform s)
    {
        var dim = UIKit.CBox("Dim", s, new Color(0, 0, 0, .65f), Vector2.zero, Vector2.zero, true);
        UIKit.Stretch(dim, 0, 0, 0, 0);
        UIKit.Panel(s, "EndPanel", Vector2.zero, new Vector2(900, 600), PanelStyle.Dark, out var ec);
        UIKit.Label("EndTitle", ec, victory ? "Phong ấn đã vỡ!" : "Hiệp sĩ gục ngã", 60,
            victory ? GameTheme.Moss : GameTheme.Blood, TextAnchor.MiddleCenter, new Vector2(0, 200), new Vector2(800, 90), true, true, FontStyle.Bold);
        int earned = 0; foreach (var d in dealt) earned += d;
        string body = "Điểm phá phong ấn: " + earned + "/" + bossMax + "\nMực nhận được: +" + inkGained +
                      (hintsPaid > 0 ? "  (đã trừ " + hintsPaid * 10 + "% vì gợi ý)" : "") +
                      "\nCác bước làm đúng vẫn được ghi, thua boss không mất tiến độ học.";
        UIKit.Label("EndBody", ec, body, 30, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 40), new Vector2(800, 200));
        UIKit.MakeButton(ec, "Again", "Chơi tiếp", new Vector2(-200, -190), new Vector2(340, 80), () => StartEncounter(subject), ButtonStyle.Primary, 34);
        UIKit.MakeButton(ec, "Lobby", "Chọn môn khác", new Vector2(200, -190), new Vector2(340, 80), ShowLobby, ButtonStyle.Secondary, 32);
    }
}
