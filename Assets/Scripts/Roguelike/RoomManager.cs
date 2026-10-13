using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// E2 + E3 – Một màn của chặng 1 (mục 3 và 4 của kế hoạch v3.2).
/// Mỗi màn là một lần nạp RoomScene; tiến trình (màn, máu, vũ khí, buff, Mực) nằm trong RunState nên sống qua các lần nạp.
/// Vòng lặp: vào phòng, đánh quái theo đợt (2-3 đợt), Rương Câu Đố hiện ra, E mở rương, viết lời giải, nộp, chấm giả.
/// Độ khó chọn MỘT LẦN ở màn Chọn độ khó (RunState.difficulty): quyết định quái đông/nhanh/mạnh cỡ nào và đề khó cỡ nào. Môn (Lý/Hóa/Sinh) tự xen kẽ theo màn.
/// Đạt thì nhận vũ khí (thay chính, thay phụ hoặc bỏ) rồi cửa mở; chưa đạt thì làm lại tối đa 3 lần rồi được phá khóa bằng Mực.
/// Sau màn 2 và 4 chọn 1 trong 3 buff. Hết máu thì về điểm lưu đầu chặng. Màn 5 (boss) làm ở E6, chưa có.
/// Phòng, HUD và mọi bảng đều dựng bằng code; scene chỉ cần một GameObject gắn script này (menu Tools > KHTN 8 > Tạo RoomScene).
/// </summary>
public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }
    /// <summary>true khi viết lời giải hoặc đang mở bảng chọn: mọi thứ trong trận (quái, đạn, người chơi, đồng hồ) đứng yên.</summary>
    public static bool Frozen { get; private set; }

    public const float MinX = -8f, MaxX = 8f, MinY = -3.9f, WallY = 3.6f, DoorHalf = 1.15f, ExitY = 4.4f, Reach = 2.3f;
    public static Vector2 Clamp(Vector2 p) => new Vector2(Mathf.Clamp(p.x, MinX, MaxX), Mathf.Clamp(p.y, MinY, WallY));

    static readonly HashSet<string> usedIds = new HashSet<string>();
    static int attemptCounter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Frozen = false; Instance = null; usedIds.Clear(); attemptCounter = 0; }

    [SerializeField] TextAsset problemsJson;                  // Assets/DataBank/Essay/essay_problems.json
    [SerializeField] string forceSubject = "";                // để trống: môn xen kẽ ngẫu nhiên theo màn (ly, hoa, sinh). Điền để thử riêng một môn
    [SerializeField] string menuSceneName = "EssaySubjectSelectScene";   // màn Chọn độ khó
    [SerializeField] int startInk = 100;                      // Mực khởi điểm của lượt chơi mới, để thử phá khóa; E5 lấy từ tiến độ đã lưu

    string Subject => string.IsNullOrEmpty(forceSubject) ? RunState.SubjectOfRoom(RunState.room) : forceSubject;
    string StageLabel => RunState.stage + "-" + RunState.room;

    // ---- kế hoạch quái: 3-5 quái mỗi màn chia 2-3 đợt; loại quái mở dần theo màn (mục 5.1, 5.2) ----
    static readonly int[][] WavePlan = { new[] { 2, 1 }, new[] { 2, 2 }, new[] { 2, 1, 1 }, new[] { 2, 2, 1 } };
    static readonly MonsterKind[][] Pools =
    {
        new[] { MonsterKind.Gunner, MonsterKind.Charger },
        new[] { MonsterKind.Gunner, MonsterKind.Charger, MonsterKind.Mage },
        new[] { MonsterKind.Gunner, MonsterKind.Charger, MonsterKind.Mage, MonsterKind.Robot },
        new[] { MonsterKind.Gunner, MonsterKind.Charger, MonsterKind.Mage, MonsterKind.Robot, MonsterKind.Summoner },
    };
    static readonly float[] EliteRate = { .10f, .15f, .20f };   // theo chặng

    enum State { Fighting, ChestReady, Solving, Panel, DoorOpen, Leaving }

    // ---- thế giới ----
    Transform world; PlayerCombat player; SpriteRenderer doorSr, doorGlow, chestSr;
    Vector2 chestPos = new Vector2(0, -0.2f);
    public bool DoorOpen { get; private set; }

    // ---- UI ----
    RectTransform hud, panelRoot; Text inkText, promptText, hpText, weaponText, buffText, reactionText; Image hpFill;
    EssaySolveUI solve; float hudT, reactionT;

    // ---- trạng thái ----
    List<EssayProblem> problems = new List<EssayProblem>();
    State state = State.Fighting;
    readonly System.Random rng = new System.Random();
    int waveIdx = -1; float clearT = .8f;
    bool unlockedByInk;

    // ---- một rương ----
    EssayProblem prob; EssayVariant variant; EssayDifficulty difficulty;
    int attemptsUsed, hintLevel, hintsPaid, hintCap;
    int[] missStreak;

    const string StudentCode = "local";                       // E4 sẽ thay bằng mã học sinh thật

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Frozen = false; Monster.Killed = null;
    }

    void Start()
    {
        Instance = this; Frozen = false;
        Combat.Reset();
        if (RunState.ink < 0) RunState.ink = startInk;
        if (RunState.main == null) RunState.ResetAll(true);
        if (RunState.stage == 1 && RunState.room == 1) usedIds.Clear();

        problems = problemsJson != null ? EssayProblemBank.Parse(problemsJson.text) : new List<EssayProblem>();
        BuildWorld();
        BuildHud();
        var go = new GameObject("EssaySolveUI"); go.transform.SetParent(transform, false);
        solve = go.AddComponent<EssaySolveUI>(); solve.Init(transform);

        player = PlayerCombat.Create(new Vector2(0, -3f));
        Combat.Player = player; player.Died = OnPlayerDied;
        Monster.Killed = OnMonsterKilled;

        if (problems.Count == 0) SetPrompt("Chưa có dữ liệu bài tự luận. Gắn essay_problems.json vào RoomManager.");
        NextWave();
        RefreshHud();
    }

    // ------------------------------------------------------------------ dựng phòng
    void BuildWorld()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true; cam.orthographicSize = 5.4f; cam.transform.position = new Vector3(0, 0, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = GameTheme.Ink;
        }
        world = new GameObject("Room").transform; world.SetParent(transform, false);
        Color tint = Subject == "ly" ? GameTheme.Ly : Subject == "hoa" ? GameTheme.Hoa : GameTheme.Sinh;
        var px = ThemeGfx.Pixel;
        var diffCol = DifficultyTheme.Of(RunState.difficulty);

        // Khung đá tối + viền đồng cổ
        Combat.MakeSr("Frame", world, px, new Color(.03f, .02f, .05f), new Vector2(17.5f, 9.2f), -102, new Vector2(0, -0.15f));
        Combat.MakeSr("FrameRim", world, px, Color.Lerp(GameTheme.Bronze, GameTheme.Ink, .35f), new Vector2(17.15f, 8.85f), -101, new Vector2(0, -0.15f));

        // Sàn gạch đá, ngả nhẹ màu môn đang thử
        const int cols = 8, rows = 4;
        float tw = 16.4f / cols, th = 8.1f / rows;
        var floorCol = Color.Lerp(new Color(.82f, .80f, .92f), tint, .16f);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                Combat.MakeSr("Tile", world, ProcSprites.FloorTile, floorCol, new Vector2(tw, th), -99, new Vector2(-8.2f + tw * (c + .5f), -0.15f - 4.05f + th * (r + .5f)));

        // Vòng ma pháp dưới sàn (màu theo độ khó) quanh vị trí rương
        Combat.MakeSr("RuneOuter", world, ProcSprites.Ring, diffCol.WithAlpha(.22f), new Vector2(6.4f, 6.4f), -98, chestPos);
        Combat.MakeSr("RuneInner", world, ProcSprites.Ring, GameTheme.Bronze.WithAlpha(.35f), new Vector2(4.4f, 4.4f), -98, chestPos);
        for (int i = 0; i < 8; i++)
        {
            float ang = i * Mathf.PI / 4f;
            var m = Combat.MakeSr("RuneMark", world, px, diffCol.WithAlpha(.28f), new Vector2(.22f, .22f), -98, chestPos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 3.2f);
            m.transform.localEulerAngles = new Vector3(0, 0, 45f);
        }

        // Tường trên: gạch đá, cửa là thanh rune đỏ (mở thì xanh)
        for (int i = 0; i < cols; i++)
            Combat.MakeSr("Wall" + i, world, ProcSprites.WallTile, Color.white, new Vector2(tw, .55f), -98, new Vector2(-8.2f + tw * (i + .5f), 4.05f));
        doorGlow = Combat.MakeSr("DoorGlow", world, ThemeGfx.Glow, GameTheme.Blood.WithAlpha(.5f), new Vector2(6f, 2.6f), -96, new Vector2(0, 3.7f));
        doorSr = Combat.MakeSr("Door", world, px, GameTheme.Blood, new Vector2(DoorHalf * 2f, .55f), -97, new Vector2(0, 4.05f));
        Combat.MakeSr("DoorFrameL", world, px, GameTheme.Gold, new Vector2(.12f, .7f), -96, new Vector2(-DoorHalf - .06f, 4.05f));
        Combat.MakeSr("DoorFrameR", world, px, GameTheme.Gold, new Vector2(.12f, .7f), -96, new Vector2(DoorHalf + .06f, 4.05f));

        // Đuốc ở bốn góc, lửa chập chờn
        Vector2[] torchAt = { new Vector2(-7.75f, 3.2f), new Vector2(7.75f, 3.2f), new Vector2(-7.75f, -3.6f), new Vector2(7.75f, -3.6f) };
        for (int i = 0; i < torchAt.Length; i++) RoomTorch.Create(world, torchAt[i], i * 3.7f);

        // Sương lững lờ trên sàn
        for (int i = 0; i < 5; i++) RoomMist.Create(world, i);

        // Vignette phủ cả màn hình cho bầu không khí u tối
        Combat.MakeSr("Vignette", world, ProcSprites.Vignette, Color.white, new Vector2(27f, 14.5f), 50, Vector2.zero);
    }

    // ------------------------------------------------------------------ HUD
    void BuildHud()
    {
        hud = UIKit.BuildCanvas(transform, "Room_HUD", 10);
        UIKit.EnsureEventSystem();
        var banner = UIKit.CBox("BannerShade", hud, new Color(.02f, .01f, .05f, .8f), new Vector2(0, 490), new Vector2(1250, 190));
        banner.GetComponent<Image>().sprite = ThemeGfx.Glow;
        UIKit.Label("Stage", hud, "Màn " + StageLabel + " · " + SubjectName(Subject), 40, GameTheme.Bone, TextAnchor.MiddleCenter,
            new Vector2(0, 508), new Vector2(900, 56), true, true, FontStyle.Bold);
        UIKit.Label("Diff", hud, "◆ Độ khó: " + RoomRules.DifficultyName(RunState.difficulty) + " ◆", 26, DifficultyTheme.Of(RunState.difficulty), TextAnchor.MiddleCenter,
            new Vector2(0, 470), new Vector2(900, 36), true, true, FontStyle.Bold);
        reactionText = UIKit.Label("Reaction", hud, "", 26, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, 430), new Vector2(1500, 40), false, true);
        inkText = UIKit.Label("Ink", hud, "", 34, GameTheme.EmberHi, TextAnchor.MiddleRight, new Vector2(780, 500), new Vector2(340, 50));
        UIKit.MakeButton(hud, "Back", "Về menu", new Vector2(-800, 500), new Vector2(220, 60), () => SceneRouter.Go(menuSceneName), ButtonStyle.Secondary, 26);

        UIKit.ProgressBar(hud, "HeroHp", new Vector2(-690, 440), new Vector2(420, 26), GameTheme.Blood, out hpFill);
        hpText = UIKit.Label("HpText", hud, "", 24, GameTheme.Bone, TextAnchor.MiddleLeft, new Vector2(-690, 408), new Vector2(420, 30));

        weaponText = UIKit.Label("Weapons", hud, "", 24, GameTheme.Bone, TextAnchor.LowerLeft, new Vector2(-480, -445), new Vector2(980, 80), false, true);
        buffText = UIKit.Label("Buffs", hud, "", 22, GameTheme.Fog, TextAnchor.LowerRight, new Vector2(500, -445), new Vector2(880, 80), false, true);
        promptText = UIKit.Label("Prompt", hud, "", 30, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, -510), new Vector2(1500, 44), false, true);
    }

    static string SubjectName(string s) => s == "ly" ? "Vật lí" : s == "hoa" ? "Hóa học" : "Sinh học";
    void SetPrompt(string s) { if (promptText != null) promptText.text = s; }

    void RefreshHud()
    {
        if (inkText == null) return;
        inkText.text = "Mực: " + RunState.ink;
        hpText.text = "Máu " + Mathf.CeilToInt(RunState.hp) + "/" + (int)PlayerCombat.MaxHp;
        UIKit.SetBar(hpFill, RunState.hp / PlayerCombat.MaxHp);
        string dash = player != null && player.DashLeft > 0f ? "Lướt " + player.DashLeft.ToString("0.0") + "s" : "Lướt sẵn sàng (Space)";
        weaponText.text = "Chính: " + RunState.main.Label + "\nPhụ: " + (RunState.sub != null ? RunState.sub.Label + " (Q đổi)" : "chưa có") + "   ·   " + dash;
        buffText.text = RunState.buffLog.Count == 0 ? "Chưa có buff" : "Buff: " + string.Join(", ", RunState.buffLog);
    }

    /// <summary>Elements.cs gọi khi xảy ra phản ứng: hiện tên và một dòng giải thích khoa học (mục 5.4).</summary>
    public void ShowReaction(string name, string note)
    {
        if (reactionText == null) return;
        reactionText.text = name + ": " + note; reactionT = 5f;
    }

    // ------------------------------------------------------------------ vòng lặp
    void Update()
    {
        Combat.Now += Combat.Dt;
        if (reactionT > 0f) { reactionT -= Time.deltaTime; if (reactionT <= 0f) reactionText.text = ""; }
        hudT -= Time.deltaTime; if (hudT <= 0f) { hudT = .15f; RefreshHud(); }
        if (Frozen) return;

        switch (state)
        {
            case State.Fighting:
                if (Combat.Monsters.Count == 0) { clearT -= Time.deltaTime; if (clearT <= 0f) NextWave(); }
                else { clearT = .8f; SetPrompt("Đợt " + (waveIdx + 1) + "/" + WaveCount + " · còn " + Combat.Monsters.Count + " quái"); }
                break;

            case State.ChestReady:
                var k = Keyboard.current;
                bool near = Vector2.Distance(player.transform.position, chestPos) <= Reach;
                SetPrompt(near ? "Nhấn E để mở Rương Câu Đố" : "Rương Câu Đố đã hiện ra giữa phòng. Lại gần và nhấn E.");
                if (near && k != null && k.eKey.wasPressedThisFrame) OpenChest();
                break;

            case State.DoorOpen:
                if (player.transform.position.y >= ExitY) LeaveRoom();
                break;
        }
    }

    int PlanIndex => Mathf.Clamp(RunState.room, 1, WavePlan.Length) - 1;
    int WaveCount => WavePlan[PlanIndex].Length + RoomRules.ExtraWaveCount(RunState.difficulty);       // Siêu khó: thêm một đợt
    int WaveSize(int idx) { var plan = WavePlan[PlanIndex]; return plan[Mathf.Min(idx, plan.Length - 1)]; }

    // ------------------------------------------------------------------ quái theo đợt
    void NextWave()
    {
        waveIdx++; clearT = .8f;
        if (waveIdx >= WaveCount) { SpawnChest(); return; }

        var diff = RunState.difficulty;
        var pool = Pools[Mathf.Min(Pools.Length - 1, PlanIndex + RoomRules.PoolBonus(diff))];                 // khó hơn: quái mạnh xuất hiện sớm hơn
        float eliteRate = EliteRate[Mathf.Clamp(RunState.stage, 1, 3) - 1] + RoomRules.EliteRateBonus(diff);
        int count = Mathf.CeilToInt(WaveSize(waveIdx) * RoomRules.CountMul(diff) - .01f);                    // khó hơn: quái đông hơn
        for (int i = 0; i < count; i++)
        {
            // Đợt đầu của mỗi màn luôn có ít nhất một con thuộc loại mới mở của màn đó.
            var kind = (waveIdx == 0 && i == 0 && RunState.room > 1) ? pool[pool.Length - 1] : pool[rng.Next(pool.Length)];
            Monster.Create(kind, SpawnPoint(), rng.NextDouble() < eliteRate);
        }
    }

    Vector2 SpawnPoint()
    {
        for (int t = 0; t < 30; t++)
        {
            var p = new Vector2((float)(rng.NextDouble() * 14.0 - 7.0), (float)(rng.NextDouble() * 5.5 - 2.4));
            if (Vector2.Distance(p, player.transform.position) > 4.5f) return p;
        }
        return new Vector2(0, 2.5f);
    }

    void OnMonsterKilled(Monster m)
    {
        RunState.ink += m.kind == MonsterKind.Minion ? 1 : m.elite ? 25 : 5;          // Mực hạ quái; tinh anh thưởng nhiều hơn (mục 5.2)
        if (m.kind != MonsterKind.Minion && (m.elite || Random.value < .15f)) CombatPickup.SpawnHeal(m.transform.position, 15f);
        if (player != null) player.OnKill();
    }

    void SpawnChest()
    {
        state = State.ChestReady;
        var px = ThemeGfx.Pixel;
        Combat.MakeSr("ChestGlow", world, ThemeGfx.Glow, GameTheme.Gold.WithAlpha(.38f), new Vector2(3.8f, 3.2f), 5, chestPos);
        chestSr = Combat.MakeSr("Chest", world, px, new Color(.30f, .17f, .10f), new Vector2(1.5f, 1.0f), 6, chestPos);
        Combat.MakeSr("ChestLid", world, px, GameTheme.Bronze, new Vector2(1.5f, .28f), 7, chestPos + new Vector2(0, .36f));
        Combat.MakeSr("StrapL", world, px, GameTheme.Gold, new Vector2(.12f, 1.0f), 7, chestPos + new Vector2(-.45f, 0));
        Combat.MakeSr("StrapR", world, px, GameTheme.Gold, new Vector2(.12f, 1.0f), 7, chestPos + new Vector2(.45f, 0));
        var gem = Combat.MakeSr("Lock", world, px, GameTheme.Blood, new Vector2(.26f, .26f), 8, chestPos + new Vector2(0, .08f));
        gem.transform.localEulerAngles = new Vector3(0, 0, 45f);
        CombatFx.Pulse(chestPos, 1.6f, GameTheme.Gold);
        GameAudio.Play(Snd.BuffOpen);
    }

    // ------------------------------------------------------------------ rương và giải bài (E2)
    void OpenChest()
    {
        if (problems.Count == 0) { UIKit.Toast(hud, "Chưa có dữ liệu bài tự luận."); return; }
        state = State.Solving; Frozen = true;
        OnPickDifficulty(RunState.difficulty);              // độ khó đã chọn từ đầu lượt chơi, không hỏi lại ở rương
    }

    void OnPickDifficulty(EssayDifficulty d)
    {
        var p = RoomRules.PickProblem(problems, Subject, d, usedIds, rng);
        if (p == null) { UIKit.Toast(hud, "Môn này chưa có bài."); Frozen = false; state = State.ChestReady; return; }
        difficulty = d; prob = p; usedIds.Add(p.id);
        attemptsUsed = 0; missStreak = new int[p.rubric.Length];
        Debug.Log("[Room " + StageLabel + "] bài " + p.id + " (mức bài: " + RoomRules.DifficultyOf(p) + ", người chơi chọn: " + d + ")");
        StartAttempt();
    }

    void NewVariant()
    {
        string prev = variant != null ? variant.statement : null;
        for (int t = 0; t < 12; t++)
        {
            variant = EssayVariantGenerator.Generate(prob, EssayVariantGenerator.SeedFor(StudentCode, prob.id, attemptCounter++));
            if (variant.statement != prev) break;                // bài lời (không tham số) không đổi đề được
        }
    }

    void StartAttempt()
    {
        NewVariant();
        int have = prob.hints != null ? prob.hints.Length : 0;
        hintCap = Mathf.Min(RoomRules.MaxHints(difficulty), have);
        hintLevel = 0; hintsPaid = 0;

        // Gợi ý đầu tự hiện (miễn phí): mức Dễ ngay từ đầu; hoặc khi sai cùng một bước hai lần liên tiếp (mục 4.4).
        bool streak = false; foreach (var m in missStreak) if (m >= 2) streak = true;
        if (hintCap > 0 && (RoomRules.AutoFirstHint(difficulty) || streak)) hintLevel = 1;

        ShowWriting("");
    }

    void ShowWriting(string draft)
    {
        state = State.Solving; Frozen = true;
        var a = new SolveWritingArgs
        {
            title = "Rương Câu Đố · Màn " + StageLabel + " · " + SubjectName(Subject) + " · " + RoomRules.DifficultyName(difficulty) +
                    " · Lần " + (attemptsUsed + 1),
            subjectId = Subject, statement = variant.statement, draft = draft,
            hintText = HintText(), showHintButton = hintCap > 0, hintButtonLabel = HintButtonLabel(), hintEnabled = hintLevel < hintCap,
            onSubmit = OnSubmit, onHint = OnHint, onExit = OnExitEssay,
        };
        solve.ShowWriting(a);
    }

    string HintText() => hintLevel > 0 && prob.hints != null && hintLevel <= prob.hints.Length ? "Gợi ý " + hintLevel + ": " + prob.hints[hintLevel - 1] : "";
    string HintButtonLabel() => hintLevel >= hintCap ? "Hết gợi ý" : "Gợi ý (" + hintLevel + "/" + hintCap + ")";

    void OnHint(string draft)
    {
        if (hintLevel >= hintCap) return;
        hintLevel++; hintsPaid++;
        solve.SetHint(HintText(), HintButtonLabel(), hintLevel < hintCap);
    }

    // Thoát giữa chừng: đề đang làm bị hủy và không tính lần làm (mục 3.2). Mở lại rương sẽ bốc đề mới.
    void OnExitEssay(string draft)
    {
        if (prob != null) usedIds.Remove(prob.id);
        solve.Hide(); Frozen = false; state = State.ChestReady;
        UIKit.Toast(hud, "Đã thoát đề, không tính lần làm.");
    }

    void OnSubmit(string raw)
    {
        var check = EssaySymbols.Prepare(raw, out var lines);
        if (check == SubmitCheck.Empty) { UIKit.Toast(solve.Root, "Em chưa viết lời giải."); return; }
        if (check == SubmitCheck.TooShort) { UIKit.Toast(solve.Root, "Lời giải còn quá ngắn, em viết thêm nhé."); return; }
        if (check == SubmitCheck.TooLong) { UIKit.Toast(solve.Root, "Lời giải dài quá " + EssaySymbols.MaxChars + " ký tự."); return; }

        var res = EssayFreeformGrader.Grade(prob, variant, lines);
        attemptsUsed++;
        for (int i = 0; i < missStreak.Length; i++)
            missStreak[i] = res.steps[i].status == EssayStepStatus.Missing ? missStreak[i] + 1 : 0;

        var band = RoomRules.Band(res.score, res.maxScore);
        int earned = RoomRules.InkFor(res.score, difficulty, hintsPaid);
        RunState.ink += earned; RefreshHud();
        Debug.Log("[Room " + StageLabel + "] " + prob.id + " lần " + attemptsUsed + ": " + res.score + "/" + res.maxScore + " (" + band + "), +" + earned + " Mực, gợi ý trả phí " + hintsPaid);

        var r = new SolveResultArgs { subjectId = Subject, problem = prob, variant = variant, grade = res, band = band, lines = lines };
        if (RoomRules.DoorOpens(band))
        {
            int rarity = RoomRules.RollRarity(difficulty, band, rng);
            r.title = band == GradeBand.Good ? "Đạt tốt" : "Đạt";
            r.revealDesc = true;
            r.footer = "Rương mở ra một vũ khí độ hiếm " + RoomRules.RarityNames[rarity] + (band == GradeBand.Pass ? " (giảm một bậc vì mới đạt)" : "") +
                       ".\n+" + earned + " Mực" + (hintsPaid > 0 ? " (đã trừ " + Mathf.RoundToInt(hintsPaid * RoomRules.HintInkPenalty * 100) + "% vì gợi ý)" : "");
            r.buttons.Add(new SolveResultButton { label = "Nhận vũ khí", onClick = () => OnSolvePassed(rarity) });
            GameAudio.Play(Snd.QuizCorrect);
        }
        else
        {
            r.title = "Chưa đạt";
            r.revealDesc = false;
            bool canUnlock = attemptsUsed >= RoomRules.MaxAttempts;
            int left = RoomRules.MaxAttempts - attemptsUsed;
            r.footer = (canUnlock ? "Em đã làm " + attemptsUsed + " lần. Có thể phá khóa bằng " + RoomRules.UnlockInkCost + " Mực (rương không cho vũ khí, bài này vào hàng ôn lại)."
                                  : "Còn " + left + " lần làm lại, mỗi lần có số liệu mới.") + "\n+" + earned + " Mực. Không mất máu, không mất vũ khí.";
            r.buttons.Add(new SolveResultButton { label = "Làm lại (số liệu mới)", onClick = StartAttempt, style = ButtonStyle.Primary });
            if (canUnlock)
            {
                if (RunState.ink >= RoomRules.UnlockInkCost)
                    r.buttons.Add(new SolveResultButton { label = "Phá khóa (-" + RoomRules.UnlockInkCost + " Mực)", onClick = OnUnlockWithInk, style = ButtonStyle.Danger });
                else r.footer += "\nChưa đủ Mực để phá khóa, em cứ làm lại để kiếm thêm.";
            }
            GameAudio.Play(Snd.QuizWrong);
        }
        solve.ShowResult(r);
    }

    void OnUnlockWithInk()
    {
        RunState.ink -= RoomRules.UnlockInkCost; unlockedByInk = true; RefreshHud();
        Debug.Log("[Room " + StageLabel + "] phá khóa bằng Mực, bài " + prob.id + " vào hàng ôn lại, không tính mastery.");
        solve.Hide();
        UIKit.Toast(hud, "Đã phá khóa: rương không có vũ khí.");
        OpenDoor();
    }

    // ------------------------------------------------------------------ phần thưởng và cửa
    void OnSolvePassed(int rarity)
    {
        solve.Hide();
        ShowWeaponPanel(Weapons.Roll(rarity, rng));
    }

    RectTransform OpenPanel(Vector2 size, out RectTransform content)
    {
        ClosePanel(); Frozen = true; state = State.Panel;
        panelRoot = UIKit.CBox("PanelRoot", hud, new Color(0, 0, 0, .72f), Vector2.zero, Vector2.zero, true);
        UIKit.Stretch(panelRoot, 0, 0, 0, 0);
        UIKit.Panel(panelRoot, "Panel", Vector2.zero, size, PanelStyle.Dark, out content);
        return panelRoot;
    }

    void ClosePanel() { if (panelRoot != null) { Destroy(panelRoot.gameObject); panelRoot = null; } }

    void ShowWeaponPanel(WeaponInstance w)
    {
        OpenPanel(new Vector2(1300, 640), out var c);
        var d = w.Def;
        UIKit.Label("T", c, "Rương mở ra", 60, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, 230), new Vector2(1100, 80), true, true, FontStyle.Bold);
        UIKit.Label("W", c, w.Label, 46, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 150), new Vector2(1200, 70), true);
        UIKit.Label("S", c, "Sát thương " + w.Damage.ToString("0.#") + "   ·   " + (1f / d.cooldown).ToString("0.0") + " đòn/giây   ·   " + (d.melee ? "cận chiến" : "tầm xa"),
            30, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, 90), new Vector2(1200, 44));
        UIKit.Label("Cur", c, "Đang cầm  ·  Chính: " + RunState.main.Label + "   ·   Phụ: " + (RunState.sub != null ? RunState.sub.Label : "chưa có"),
            26, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 30), new Vector2(1200, 60));
        UIKit.MakeButton(c, "Main", "Thay vũ khí chính", new Vector2(-400, -120), new Vector2(380, 80), () => { RunState.main = w; AfterWeapon(); }, ButtonStyle.Primary, 30);
        UIKit.MakeButton(c, "Sub", RunState.sub == null ? "Làm vũ khí phụ" : "Thay vũ khí phụ", new Vector2(0, -120), new Vector2(380, 80), () => { RunState.sub = w; AfterWeapon(); }, ButtonStyle.Bronze, 30, .1f);
        UIKit.MakeButton(c, "Skip", "Bỏ qua", new Vector2(400, -120), new Vector2(380, 80), AfterWeapon, ButtonStyle.Secondary, 30, .2f);
    }

    void AfterWeapon() { ClosePanel(); RefreshHud(); OpenDoor(); }

    void OpenDoor()
    {
        state = State.DoorOpen; Frozen = false; DoorOpen = true;
        doorSr.color = GameTheme.Moss;
        if (doorGlow != null) doorGlow.color = GameTheme.Moss.WithAlpha(.5f);
        if (chestSr != null) chestSr.color = GameTheme.Fog;
        GameAudio.Play(Snd.BuffPick);
        SetPrompt("Cửa đã mở. Đi lên cửa phía trên để sang màn kế tiếp.");
    }

    // ------------------------------------------------------------------ rời phòng, buff, sang màn kế
    void LeaveRoom()
    {
        state = State.Leaving; Frozen = true;
        if (RunState.room % 2 == 0) ShowBuffPanel(); else GoNext();      // buff mỗi 2 màn: sau màn 2, 4 (mục 3.1)
    }

    void ShowBuffPanel()
    {
        OpenPanel(new Vector2(1600, 640), out var c);
        UIKit.Label("T", c, "Chọn một buff", 60, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, 230), new Vector2(1300, 80), true, true, FontStyle.Bold);
        UIKit.Label("S", c, "Đã qua " + RunState.room + " màn. Ít nhất một buff hợp với vũ khí đang cầm (" + RunState.main.Def.name + ").",
            26, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, 170), new Vector2(1400, 40));
        var offer = Buffs.Offer(RunState.main.Def.melee, rng);
        for (int i = 0; i < offer.Count; i++)
        {
            var b = offer[i]; float x = (i - 1) * 490f;
            UIKit.MakeButton(c, "Buff" + i, b.name, new Vector2(x, 40), new Vector2(450, 120), () => { RunState.AddBuff(b, rng); ClosePanel(); GoNext(); },
                ButtonStyle.Bronze, 32, .1f * i);
            UIKit.Label("Desc" + i, c, b.desc, 26, GameTheme.Bone, TextAnchor.UpperCenter, new Vector2(x, -90), new Vector2(450, 110));
        }
    }

    void GoNext()
    {
        RunState.room++;
        if (RunState.room > RunState.RoomsPerStage) ShowChapterEnd(); else Reload();
    }

    void ShowChapterEnd()
    {
        GameAudio.Play(Snd.Victory);
        OpenPanel(new Vector2(1000, 600), out var c);
        UIKit.Label("T", c, "Xong 4 màn thường", 58, GameTheme.Moss, TextAnchor.MiddleCenter, new Vector2(0, 210), new Vector2(900, 90), true, true, FontStyle.Bold);
        UIKit.Label("B", c, "Em đã qua cả 4 màn thường của chặng 1.\nMực hiện có: " + RunState.ink +
                    (unlockedByInk ? "\nCó bài đã phá khóa bằng Mực, sẽ được ôn lại." : "") +
                    "\n\n(E3: màn 1-5 là boss Chuột Bạch Biến Dị, làm ở E6.)", 30, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 40), new Vector2(900, 280));
        UIKit.MakeButton(c, "Again", "Chơi lượt mới", new Vector2(-220, -200), new Vector2(380, 80), () => { RunState.ResetAll(true); Reload(); }, ButtonStyle.Primary, 32);
        UIKit.MakeButton(c, "Menu", "Về menu", new Vector2(220, -200), new Vector2(380, 80), () => SceneRouter.Go(menuSceneName), ButtonStyle.Secondary, 32);
    }

    void OnPlayerDied()
    {
        GameAudio.Play(Snd.Defeat);
        OpenPanel(new Vector2(1000, 560), out var c);
        UIKit.Label("T", c, "Hiệp sĩ gục ngã", 60, GameTheme.Blood, TextAnchor.MiddleCenter, new Vector2(0, 190), new Vector2(900, 90), true, true, FontStyle.Bold);
        UIKit.Label("B", c, "Bóng tối nuốt lấy em, nhưng tri thức thì không mất.\nEm quay về điểm lưu đầu chặng (màn 1-1). Mực vẫn giữ nguyên: " + RunState.ink + ".",
            30, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 30), new Vector2(900, 200));
        UIKit.MakeButton(c, "Back", "Về điểm lưu", new Vector2(-220, -190), new Vector2(380, 80), () => { RunState.ResetToStageStart(); Reload(); }, ButtonStyle.Primary, 32);
        UIKit.MakeButton(c, "Menu", "Về menu", new Vector2(220, -190), new Vector2(380, 80), () => SceneRouter.Go(menuSceneName), ButtonStyle.Secondary, 32);
    }

    void Reload() { SceneRouter.Go(SceneManager.GetActiveScene().name); }
}

/// <summary>Đuốc tường: lửa chập chờn và quầng sáng cam hắt lên sàn.</summary>
public class RoomTorch : MonoBehaviour
{
    SpriteRenderer flame, light; Vector3 flameScale, lightScale; float seed;

    public static RoomTorch Create(Transform parent, Vector2 pos, float seed)
    {
        var go = new GameObject("Torch"); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
        var t = go.AddComponent<RoomTorch>(); t.seed = seed;
        Combat.MakeSr("Stick", go.transform, ProcSprites.Torch, Color.white, new Vector2(.34f, .68f), 3);
        t.light = Combat.MakeSr("Light", go.transform, ThemeGfx.Glow, GameTheme.Ember.WithAlpha(.12f), new Vector2(6.5f, 6.5f), -95, new Vector2(0, .3f));
        t.flame = Combat.MakeSr("Flame", go.transform, ThemeGfx.Glow, GameTheme.EmberHi, new Vector2(.8f, .8f), 4, new Vector2(0, .42f));
        t.flameScale = t.flame.transform.localScale; t.lightScale = t.light.transform.localScale;
        return t;
    }

    void Update()
    {
        float f = Mathf.PerlinNoise(Time.time * 7f, seed);
        flame.color = GameTheme.EmberHi.WithAlpha(.65f + .35f * f);
        flame.transform.localScale = flameScale * (.85f + .3f * f);
        light.color = GameTheme.Ember.WithAlpha(.07f + .09f * f);
        light.transform.localScale = lightScale * (.95f + .1f * f);
    }
}

/// <summary>Sương xanh tím trôi chậm ngang sàn phòng.</summary>
public class RoomMist : MonoBehaviour
{
    float baseX, amp, speed, phase;

    public static RoomMist Create(Transform parent, int i)
    {
        var rng = new System.Random(i * 7919 + 13);
        float y = -3.2f + (float)rng.NextDouble() * 6.4f, w = 5f + (float)rng.NextDouble() * 4f;
        var sr = Combat.MakeSr("Mist", parent, ThemeGfx.Glow, new Color(.55f, .6f, .95f, .05f + .03f * (float)rng.NextDouble()), new Vector2(w, w * .4f), -94, new Vector2(0, y));
        var m = sr.gameObject.AddComponent<RoomMist>();
        m.baseX = -6f + (float)rng.NextDouble() * 12f; m.amp = 1.5f + (float)rng.NextDouble() * 2f;
        m.speed = .12f + (float)rng.NextDouble() * .12f; m.phase = (float)rng.NextDouble() * 6.28f;
        return m;
    }

    void Update()
    {
        var p = transform.localPosition;
        p.x = baseX + Mathf.Sin(Time.time * speed + phase) * amp;
        transform.localPosition = p;
    }
}
