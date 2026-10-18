using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Điều phối toàn bộ chế độ roguelike trong một scene (RoomScene), mỗi lần chuyển màn là một lần nạp lại scene; tiến trình nằm trong RunState.
/// Các pha (RunState.phase): Room (màn thường: đánh quái theo đợt, Rương Câu Đố, cửa), Boss (màn 5 của mỗi chặng: boss 4 pha + trụ đá),
/// Rest (phòng nghỉ sau boss: lửa trại, Thầy Cú), Final (màn 16: bệ đá cổ, chọn phần thưởng, nghi lễ), Ending (cảnh kết + credit).
/// Phòng, HUD và mọi bảng đều dựng bằng code; scene chỉ cần một GameObject gắn script này.
/// </summary>
public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }
    /// <summary>true khi viết lời giải hoặc đang mở bảng: mọi thứ trong trận (quái, đạn, người chơi, đồng hồ) đứng yên.</summary>
    public static bool Frozen { get; private set; }
    public static bool HazardsOn = true;

    public const float DoorHalf = .95f, Reach = 1.9f, CenterY = -.15f;
    /// <summary>Nửa kích thước sân hiện tại: sân to hay bé theo số quái của màn (quái đông thì sân rộng, quái ít thì sân nhỏ gọn), camera đi theo người chơi.</summary>
    public static float HalfW = 8.2f, HalfH = 4.05f;
    public static float MinX => -HalfW + .2f;
    public static float MaxX => HalfW - .2f;
    public static float MinY => CenterY - HalfH + .3f;
    public static float WallY => CenterY + HalfH - .3f;
    public static float ExitY => CenterY + HalfH + .55f;
    public static Vector2 Clamp(Vector2 p) => new Vector2(Mathf.Clamp(p.x, MinX, MaxX), Mathf.Clamp(p.y, MinY, WallY));

    static readonly HashSet<string> usedIds = new HashSet<string>();
    static int attemptCounter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Frozen = false; Instance = null; usedIds.Clear(); attemptCounter = 0; HazardsOn = true; }

    [SerializeField] TextAsset problemsJson;                  // Assets/DataBank/Essay/essay_problems.json
    [SerializeField] string forceSubject = "";                // để trống: môn xen kẽ ngẫu nhiên theo màn. Điền để thử riêng một môn
    [SerializeField] string menuSceneName = "EssaySubjectSelectScene";
    [SerializeField] int startInk = 100;
    /// <summary>Hệ số kích thước sân theo từng loại màn (xem ArenaFactor).</summary>
    float arenaF = 1f;

    string Subject => string.IsNullOrEmpty(forceSubject) ? RunState.SubjectOfRoom(RunState.room) : forceSubject;
    string StageLabel => RunState.stage + "-" + RunState.room;
    int biome;

    // ---- kế hoạch quái theo chặng (mục 5.1): chặng 1 có 3-5 quái, chặng 2 có 4-6, chặng 3 có 5-8 ----
    static readonly int[][][] WavePlans =
    {
        new[] { new[] { 2, 1 }, new[] { 2, 2 }, new[] { 2, 1, 1 }, new[] { 2, 2, 1 } },
        new[] { new[] { 2, 2 }, new[] { 3, 2 }, new[] { 2, 2, 2 }, new[] { 3, 2, 1 } },
        new[] { new[] { 3, 2 }, new[] { 3, 3 }, new[] { 3, 2, 2 }, new[] { 4, 2, 2 } },
    };
    static readonly MonsterKind[][] Pools =
    {
        new[] { MonsterKind.Gunner, MonsterKind.Charger },
        new[] { MonsterKind.Gunner, MonsterKind.Charger, MonsterKind.Mage },
        new[] { MonsterKind.Gunner, MonsterKind.Charger, MonsterKind.Mage, MonsterKind.Robot },
        new[] { MonsterKind.Gunner, MonsterKind.Charger, MonsterKind.Mage, MonsterKind.Robot, MonsterKind.Summoner },
    };
    static readonly float[] EliteRate = { .10f, .15f, .20f };

    enum State { Fighting, ChestReady, Solving, Panel, DoorOpen, Leaving, Boss, Rest, Final, Cutscene }

    // ---- thế giới ----
    Transform world; PlayerCombat player; SpriteRenderer doorSr, doorGlow, chestSr, darkSr, vignetteSr, weaponBarBg, weaponBarFill; SpriteRenderer[] pips = new SpriteRenderer[8];
    Vector2 chestPos = new Vector2(0, CenterY);
    public bool DoorOpen { get; private set; }

    // ---- UI ----
    RectTransform hud, panelRoot, bossRoot; Text inkText, promptText, hpText, weaponText, buffText, reactionText, bannerText, bossName, stageText, shardText;
    Image hpFill, bossFill, overlayImg, hurtImg, mainIcon, subIcon, mainEl, dashFill, topBar, botBar; Image[] buffIcons = new Image[16];
    Text introName, introTitle;
    EssaySolveUI solve; float hudT, reactionT, bannerT, introT, hurtA; Color overlayTarget; float darkCur, darkTarget, darkUntil;
    StoryDirector story; TouchControls touch;

    // ---- trạng thái ----
    List<EssayProblem> problems = new List<EssayProblem>();
    State state = State.Fighting;
    readonly System.Random rng = new System.Random();
    int waveIdx = -1; float clearT = .8f; bool unlockedByInk;

    // ---- một rương / một trụ đá ----
    EssayProblem prob; EssayVariant variant; EssayDifficulty difficulty;
    int attemptsUsed, hintLevel, hintsPaid, hintCap; int[] missStreak;
    System.Action<GradeBand, string> bossDone; bool inBossEssay; string essaySubject; string essayTitle;
    const string StudentCode = "local";

    void OnDestroy() { if (Instance == this) Instance = null; Frozen = false; Foe.Killed = null; Cursor.visible = true; Combat.FieldStrength = 0f; Combat.Gravity = Vector2.zero; }

    void Start()
    {
        Instance = this; Frozen = false; HazardsOn = true; inBossEssay = false;
        Combat.Reset(); CombatBolt.Enemy.Clear();
        if (RunState.ink < 0) RunState.ink = startInk;
        if (RunState.main == null) RunState.ResetAll(true);
        if (RunState.stage == 1 && RunState.room == 1 && RunState.phase == RunPhase.Room) usedIds.Clear();
        biome = RunState.phase == RunPhase.Final || RunState.phase == RunPhase.Ending ? 2 : RunState.Biome;

        problems = problemsJson != null ? EssayProblemBank.Parse(problemsJson.text) : new List<EssayProblem>();
        GameAudio.StopMusic(); RogueAudio.Ensure(); RFxHost.Ensure(transform);
        BuildWorld();
        BuildHud();
        var go = new GameObject("EssaySolveUI"); go.transform.SetParent(transform, false);
        solve = go.AddComponent<EssaySolveUI>(); solve.Init(transform);
        var sg = new GameObject("Story"); sg.transform.SetParent(transform, false); story = sg.AddComponent<StoryDirector>(); story.Init(transform);

        player = PlayerCombat.Create(new Vector2(0, MinY + 1f));
        Combat.Player = player; player.Died = OnPlayerDied; RFxHost.I.SetFollow(player.transform, true);
        touch = TouchControls.Create(this, TouchLayout.Shooter, () => !Frozen && state != State.Cutscene && state != State.Panel, () => RunState.sub != null);
        Foe.Killed = OnFoeKilled;
        if (problems.Count == 0) SetPrompt("Chưa có dữ liệu bài tự luận. Gắn essay_problems.json vào RoomManager.");

        if (RunState.introPending)
        {
            RunState.introPending = false; Meta.SeenIntro = true; state = State.Cutscene; player.Controllable = false;
            story.PlayIntro(() => { player.Controllable = true; BeginPhase(); });
        }
        else BeginPhase();
        RefreshHud();
    }

    void BeginPhase()
    {
        switch (RunState.phase)
        {
            case RunPhase.Room:
                RogueAudio.PlayTrack(biome == 0 ? RTrack.Lab : biome == 1 ? RTrack.Bio : RTrack.Sky);
                RoomHazards.Create(transform, biome); state = State.Fighting;
                if (RunState.room == 1) ShowBanner("CHẶNG " + RunState.stage + " · " + RunState.StageName(RunState.stage), 3.5f);
                RunSave.Save(); NextWave(); break;
            case RunPhase.Boss: BeginBoss(); break;
            case RunPhase.Rest: BeginRest(); break;
            case RunPhase.Final: BeginFinal(); break;
            default: BeginEnding(); break;
        }
    }

    // ------------------------------------------------------------------ dựng phòng
    /// <summary>Hệ số to nhỏ của sân: màn thường theo tổng số quái, boss và màn 16 rộng cố định, phòng nghỉ vừa phải.</summary>
    float ArenaFactor()
    {
        switch (RunState.phase)
        {
            case RunPhase.Boss: return 1.3f;
            case RunPhase.Rest: return 1.0f;
            case RunPhase.Final: return 1.2f;
            case RunPhase.Ending: return 1.0f;
        }
        int total = 0; int waves = WaveCount; for (int i = 0; i < waves; i++) total += Mathf.CeilToInt(WaveSize(i) * RoomRules.CountMul(RunState.difficulty) - .01f);
        return Mathf.Clamp(.66f + .15f * total, 1.0f, 2.0f);
    }

    void BuildWorld()
    {
        arenaF = ArenaFactor(); HalfW = 8.2f * arenaF; HalfH = 4.05f * (1f + (arenaF - 1f) * .85f);
        var cam = Camera.main;
        if (cam != null) { cam.orthographic = true; cam.transform.position = new Vector3(0, CenterY, -10f); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = GameTheme.Ink; }
        RFxHost.I.SetCamera(new Vector3(0, CenterY, -10f), 5.6f);
        world = new GameObject("Room").transform; world.SetParent(transform, false);
        var px = ThemeGfx.Pixel; var diffCol = DifficultyTheme.Of(RunState.difficulty);
        bool room = RunState.phase == RunPhase.Room;

        float fw = HalfW * 2f + 1.6f, fh = HalfH * 2f + 1.3f;
        Combat.MakeSr("Frame", world, px, new Color(.03f, .02f, .05f), new Vector2(fw + 30f, fh + 20f), -102, new Vector2(0, CenterY));
        Combat.MakeSr("FrameRim", world, px, Color.Lerp(GameTheme.Bronze, GameTheme.Ink, .45f), new Vector2(fw, fh), -101, new Vector2(0, CenterY));

        // Sàn: ô 32x32 pixel nguyên kích thước (1 ô = 1 đơn vị), bốn biến thể trộn ngẫu nhiên theo khu vực
        int cols = Mathf.CeilToInt(HalfW * 2f), rows = Mathf.CeilToInt(HalfH * 2f); var floors = RogueArt.FloorTiles(biome); var fr = new System.Random(RunState.stage * 31 + RunState.room * 7 + (int)RunState.phase);
        Color floorCol = RunState.phase == RunPhase.Rest ? new Color(1f, .9f, .82f) : Color.white; float x0 = -cols * .5f + .5f, y0 = CenterY - rows * .5f + .5f;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            { var t = Combat.MakePx("Tile", world, floors[fr.Next(4)], -99, new Vector2(x0 + c, y0 + r)); t.color = floorCol; }

        if (room)
        {   // vòng rune quanh vị trí rương (màu theo độ khó)
            Combat.MakeSr("RuneOuter", world, ProcSprites.Ring, diffCol.WithAlpha(.22f), new Vector2(5f, 5f), -98, chestPos);
            Combat.MakeSr("RuneInner", world, ProcSprites.Ring, GameTheme.Bronze.WithAlpha(.35f), new Vector2(3.4f, 3.4f), -98, chestPos);
        }

        // Tường bao: hai hàng ở trên (cao 1,75), một hàng quanh trái, phải và dưới
        var walls = RogueArt.WallTiles(biome); float topY = CenterY + rows * .5f;
        for (int i = 0; i < cols; i++)
        {
            Combat.MakePx("WallA" + i, world, walls[i % 2], -98, new Vector2(x0 + i, topY + .22f)); Combat.MakePx("WallB" + i, world, walls[(i + 1) % 2], -98, new Vector2(x0 + i, topY + .66f));
            var bt = Combat.MakePx("WallBot" + i, world, walls[(i + 1) % 2], -98, new Vector2(x0 + i, CenterY - rows * .5f - .22f)); bt.color = new Color(.75f, .72f, .85f);
        }
        for (int j = -1; j < rows + 1; j++)
        {
            var l = Combat.MakePx("WallL" + j, world, walls[j & 1], -98, new Vector2(x0 - 1f, y0 + j)); l.color = new Color(.8f, .78f, .9f);
            var rr = Combat.MakePx("WallR" + j, world, walls[(j + 1) & 1], -98, new Vector2(-x0 + 1f, y0 + j)); rr.color = new Color(.8f, .78f, .9f);
        }
        bool door = RunState.phase == RunPhase.Room || RunState.phase == RunPhase.Rest; float doorY = topY + .25f;
        doorGlow = Combat.MakeSr("DoorGlow", world, ThemeGfx.Glow, GameTheme.Blood.WithAlpha(door ? .5f : 0f), new Vector2(4.4f, 2.2f), -96, new Vector2(0, doorY - .3f));
        doorSr = Combat.MakeSr("Door", world, px, GameTheme.Blood, new Vector2(DoorHalf * 2f, .5f), -97, new Vector2(0, doorY));
        Combat.MakeSr("DoorFrameL", world, px, GameTheme.Gold, new Vector2(.1f, .64f), -96, new Vector2(-DoorHalf - .05f, doorY));
        Combat.MakeSr("DoorFrameR", world, px, GameTheme.Gold, new Vector2(.1f, .64f), -96, new Vector2(DoorHalf + .05f, doorY));
        if (!door) doorSr.color = new Color(.1f, .08f, .14f);

        float tx = HalfW - .5f, tyTop = CenterY + HalfH - .9f, tyBot = CenterY - HalfH + .6f;
        Vector2[] torchAt = { new Vector2(-tx, tyTop), new Vector2(tx, tyTop), new Vector2(-tx, tyBot), new Vector2(tx, tyBot), new Vector2(-tx * .5f, tyTop + .2f), new Vector2(tx * .5f, tyTop + .2f) };
        int tc = arenaF > 1.35f ? 6 : 4; for (int i = 0; i < tc; i++) RoomTorch.Create(world, torchAt[i], i * 3.7f);
        int mist = RunState.phase == RunPhase.Final ? 9 : 5; for (int i = 0; i < mist; i++) RoomMist.Create(world, i);

        vignetteSr = Combat.MakeSr("Vignette", world, ProcSprites.Vignette, Color.white, new Vector2(27f, 14.5f), Combat.OrdVignette, Vector2.zero);
        darkSr = Combat.MakeSr("Dark", world, RogueArt.DarkMask, new Color(1, 1, 1, 0), new Vector2(26f, 26f), Combat.OrdVignette - 5, Vector2.zero);
        PlaceProps();
    }

    // ------------------------------------------------------------------ vật cản
    void PlaceProps()
    {
        if (RunState.phase != RunPhase.Room && RunState.phase != RunPhase.Boss) return;
        var rnd = new System.Random(RunState.stage * 977 + RunState.room * 131 + (int)RunState.difficulty);
        bool bossRoom = RunState.phase == RunPhase.Boss; int groups = bossRoom ? 5 : Mathf.Clamp(Mathf.RoundToInt(HalfW * .45f), 3, 8);
        var spots = new List<Vector2>();
        Vector2 spawn = new Vector2(0f, MinY + 1f);
        for (int g = 0; g < groups; g++)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                float x = Mathf.Round(Mathf.Lerp(-HalfW + 2f, HalfW - 2f, (float)rnd.NextDouble())), y = Mathf.Round(Mathf.Lerp(MinY + 1.2f, WallY - 1.6f, (float)rnd.NextDouble()));
                var p = new Vector2(x, y);
                if (Vector2.Distance(p, chestPos) < 3f || Vector2.Distance(p, spawn) < 3.2f || Mathf.Abs(x) < 2.2f && y > WallY - 2.2f) continue;      // chừa chỗ rương, chỗ xuất phát, cửa
                if (bossRoom && (y < -.4f || Vector2.Distance(p, new Vector2(0f, 1.5f)) < 3.4f)) continue;                                        // boss: vật cản ở nửa trên, chừa chỗ trụ đá phía dưới
                bool near = false; foreach (var q in spots) if (Vector2.Distance(p, q) < 2.6f) { near = true; break; }
                if (near) continue;
                spots.Add(p); double roll = rnd.NextDouble(); bool horiz = rnd.NextDouble() < .5; Vector2 step = horiz ? Vector2.right : Vector2.up;
                if (roll < .5)
                {   // một dãy thùng gỗ làm bức che chắn, đôi khi kèm một thùng nổ ở đầu dãy
                    int n = 2 + rnd.Next(3); for (int i = 0; i < n; i++) RoomProp.Create(PropKind.Crate, Clamp(p + step * i), biome);
                    if (rnd.NextDouble() < .35) RoomProp.Create(PropKind.Explosive, Clamp(p + step * n), biome);
                }
                else if (roll < .72) { int n = 1 + rnd.Next(2); for (int i = 0; i < n; i++) RoomProp.Create(PropKind.Explosive, Clamp(p + step * i), biome); }
                else if (roll < .9) { int n = 1 + rnd.Next(2); for (int i = 0; i < n; i++) RoomProp.Create(PropKind.Poison, Clamp(p + step * i), biome); }
                else if (arenaF >= 1.3f || bossRoom) { int n = 2 + rnd.Next(2); for (int i = 0; i < n; i++) RoomProp.Create(PropKind.Stone, Clamp(p + step * i), biome); }
                else RoomProp.Create(PropKind.Crate, p, biome);
                break;
            }
        }
    }

    // ------------------------------------------------------------------ HUD
    void BuildHud()
    {
        hud = UIKit.BuildCanvas(transform, "Room_HUD", 10);
        UIKit.EnsureEventSystem();
        overlayImg = UIKit.CBox("Overlay", hud, new Color(0, 0, 0, 0), Vector2.zero, Vector2.zero).GetComponent<Image>(); UIKit.Stretch(overlayImg.rectTransform, 0, 0, 0, 0); overlayImg.raycastTarget = false;
        hurtImg = UIKit.CBox("Hurt", hud, new Color(.8f, .05f, .1f, 0f), Vector2.zero, Vector2.zero).GetComponent<Image>(); UIKit.Stretch(hurtImg.rectTransform, 0, 0, 0, 0); hurtImg.sprite = ThemeGfx.Vignette; hurtImg.raycastTarget = false;

        var banner = UIKit.CBox("BannerShade", hud, new Color(.02f, .01f, .05f, .8f), new Vector2(0, 490), new Vector2(1250, 190));
        banner.GetComponent<Image>().sprite = ThemeGfx.Glow;
        string stg = RunState.phase == RunPhase.Room ? "Màn " + StageLabel + " · " + SubjectName(Subject)
                   : RunState.phase == RunPhase.Boss ? "Màn " + RunState.stage + "-5 · Boss"
                   : RunState.phase == RunPhase.Rest ? "Phòng nghỉ" : RunState.phase == RunPhase.Final ? "Màn 16 · Bệ Đá Cổ" : "";
        stageText = UIKit.Label("Stage", hud, stg, 40, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 508), new Vector2(900, 56), true, true, FontStyle.Bold);
        UIKit.Label("Diff", hud, "◆ Độ khó: " + RoomRules.DifficultyName(RunState.difficulty) + " ◆", 26, DifficultyTheme.Of(RunState.difficulty), TextAnchor.MiddleCenter, new Vector2(0, 470), new Vector2(900, 36), true, true, FontStyle.Bold);
        reactionText = UIKit.Label("Reaction", hud, "", 26, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, -395), new Vector2(1700, 40), false, true);
        bannerText = UIKit.Label("BannerText", hud, "", 34, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 360), new Vector2(1700, 60), true, true, FontStyle.Bold);
        inkText = UIKit.Label("Ink", hud, "", 34, GameTheme.EmberHi, TextAnchor.MiddleRight, new Vector2(780, 500), new Vector2(340, 50));
        shardText = UIKit.Label("Shards", hud, "", 26, new Color(.55f, .9f, 1f), TextAnchor.MiddleRight, new Vector2(780, 455), new Vector2(340, 40));
        UIKit.MakeButton(hud, "Back", "Về menu", new Vector2(-800, 500), new Vector2(220, 60), () => { RunSave.Save(); RogueAudio.Stop(); SceneRouter.Go(menuSceneName); }, ButtonStyle.Secondary, 26);

        UIKit.ProgressBar(hud, "HeroHp", new Vector2(-690, 440), new Vector2(420, 26), GameTheme.Blood, out hpFill);
        hpText = UIKit.Label("HpText", hud, "", 24, GameTheme.Bone, TextAnchor.MiddleLeft, new Vector2(-690, 408), new Vector2(420, 30));
        var dashBg = UIKit.CBox("DashBg", hud, new Color(0, 0, 0, .6f), new Vector2(-940, 440), new Vector2(46, 46)); dashBg.GetComponent<Image>().sprite = RogueArt.Disc;
        var df = UIKit.CBox("DashFill", hud, new Color(.55f, .85f, 1f, .9f), new Vector2(-940, 440), new Vector2(40, 40)); dashFill = df.GetComponent<Image>(); dashFill.sprite = RogueArt.Disc; dashFill.type = Image.Type.Filled; dashFill.fillMethod = Image.FillMethod.Radial360;

        // ô vũ khí: chính (to) và phụ (nhỏ), nguyên tố hiện kèm
        var slot = UIKit.CBox("SlotMain", hud, new Color(.05f, .04f, .09f, .85f), new Vector2(-850, -440), new Vector2(112, 112)); slot.GetComponent<Image>().sprite = ThemeGfx.Glow;
        mainIcon = Icon("MainIcon", hud, null, new Vector2(-850, -440), new Vector2(104, 104));
        mainEl = Icon("MainEl", hud, null, new Vector2(-805, -485), new Vector2(34, 34));
        subIcon = Icon("SubIcon", hud, null, new Vector2(-735, -460), new Vector2(66, 66)); subIcon.color = new Color(1, 1, 1, .6f);
        weaponText = UIKit.Label("Weapons", hud, "", 24, GameTheme.Bone, TextAnchor.LowerLeft, new Vector2(-230, -445), new Vector2(980, 80), false, true);
        buffText = UIKit.Label("Buffs", hud, "", 20, GameTheme.Fog, TextAnchor.LowerRight, new Vector2(500, -500), new Vector2(880, 36), false, true);
        for (int i = 0; i < buffIcons.Length; i++) { buffIcons[i] = Icon("Buff" + i, hud, null, new Vector2(920 - i * 44, -440), new Vector2(40, 40)); buffIcons[i].enabled = false; }
        promptText = UIKit.Label("Prompt", hud, "", 30, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, -510), new Vector2(1500, 44), false, true);

        // thanh máu boss với ba vạch pha (75/50/25)
        bossRoot = UIKit.CBox("BossBar", hud, new Color(0, 0, 0, 0), new Vector2(0, 425), new Vector2(1000, 80));
        bossName = UIKit.Label("BossName", bossRoot, "", 34, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 26), new Vector2(1000, 44), true, true, FontStyle.Bold);
        UIKit.ProgressBar(bossRoot, "BossHp", new Vector2(0, -12), new Vector2(900, 28), new Color(.8f, .2f, .3f), out bossFill);
        for (int i = 1; i <= 3; i++) UIKit.CBox("Tick" + i, bossRoot, new Color(1, 1, 1, .85f), new Vector2(-450 + 900 * (i * .25f), -12), new Vector2(4, 36));
        bossRoot.gameObject.SetActive(false);

        // dải đen điện ảnh cho giới thiệu boss
        topBar = UIKit.CBox("BarTop", hud, Color.black, new Vector2(0, 640), new Vector2(2100, 180)).GetComponent<Image>(); botBar = UIKit.CBox("BarBot", hud, Color.black, new Vector2(0, -640), new Vector2(2100, 180)).GetComponent<Image>();
        topBar.raycastTarget = false; botBar.raycastTarget = false;
        introName = UIKit.Label("IntroName", hud, "", 96, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 90), new Vector2(1800, 130), true, true, FontStyle.Bold);
        introTitle = UIKit.Label("IntroTitle", hud, "", 40, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, -10), new Vector2(1800, 60), true, true, FontStyle.Italic);
    }

    Image Icon(string name, Transform p, Sprite s, Vector2 pos, Vector2 size)
    {
        var r = UIKit.CBox(name, p, Color.white, pos, size); var im = r.GetComponent<Image>(); im.sprite = s; im.preserveAspect = true; im.raycastTarget = false; return im;
    }

    static string SubjectName(string s) => s == "ly" ? "Vật lí" : s == "hoa" ? "Hóa học" : "Sinh học";
    void SetPrompt(string s) { if (promptText != null) promptText.text = s; }

    public void ShowBanner(string s, float dur) { if (bannerText == null) return; bannerText.text = s; bannerT = dur; }
    public void ShowReaction(string name, string note) { if (reactionText == null) return; reactionText.text = name + ": " + note; reactionT = 5f; }
    public void HurtFlash() { hurtA = .75f; }
    public void SetOverlay(Color c) { overlayTarget = c; }
    public void PulseDarkness(float alpha, float dur) { darkTarget = alpha; darkUntil = Time.time + dur; }
    public void BossBarHide() { if (bossRoot != null) bossRoot.gameObject.SetActive(false); }

    public void BossIntro(string name, string title)
    {
        introT = 2.6f; introName.text = name; introTitle.text = title; bossRoot.gameObject.SetActive(true); bossName.text = name;
    }

    void RefreshHud()
    {
        if (inkText == null) return;
        inkText.text = "Mực: " + RunState.ink;
        shardText.text = RunState.shards > 0 ? "Mảnh Ký Ức: " + new string('◆', RunState.shards) + new string('◇', 3 - RunState.shards) : "";
        hpText.text = "Máu " + Mathf.CeilToInt(RunState.hp) + "/" + (int)PlayerCombat.MaxHp + (player != null && player.Shield > 0f ? "  (+" + Mathf.CeilToInt(player.Shield) + " khiên)" : "") + (player != null && player.ShieldCharges > 0 ? "  ◈×" + player.ShieldCharges : "");
        UIKit.SetBar(hpFill, RunState.hp / PlayerCombat.MaxHp);
        if (player != null) { dashFill.fillAmount = 1f - Mathf.Clamp01(player.DashLeft / PlayerCombat.DashCooldown); dashFill.color = player.DashLeft > 0f ? new Color(.5f, .6f, .75f, .8f) : new Color(.6f, 1f, 1f, 1f); }
        var m = RunState.main; var s = RunState.sub;
        mainIcon.sprite = m.Icon; mainIcon.enabled = true; mainEl.enabled = m.element != Element.None; if (mainEl.enabled) mainEl.sprite = RogueArt.ElementIcon((int)m.element);
        subIcon.sprite = s != null ? s.Icon : null; subIcon.enabled = s != null;
        weaponText.text = m.Label + "\n" + (s != null ? "Phụ: " + s.Label + "  (Q đổi)" : "Chưa có vũ khí phụ") + (RunState.infuse != Element.None ? "   ·   Nhiễm " + Combat.ElementName(RunState.infuse) : "");
        buffText.text = RunState.buffLog.Count == 0 ? "Chưa có buff" : "";
        for (int i = 0; i < buffIcons.Length; i++)
        {
            if (i < RunState.buffLog.Count) { buffIcons[i].enabled = true; buffIcons[i].sprite = BuffSprite(RunState.buffLog[i]); } else buffIcons[i].enabled = false;
        }
    }

    /// <summary>Icon buff: buff lấy từ boss Rồng dùng đúng icon của trận Sinh học, buff còn lại dùng icon của chế độ này.</summary>
    public static Sprite BuffSprite(string log) { for (int i = 0; i < Buffs.All.Length; i++) if (log.StartsWith(Buffs.All[i].name)) return Buffs.All[i].icon != null ? ProcSprites.BuffIcon(Buffs.All[i].icon) : RogueArt.BuffIcon(i); return RogueArt.BuffIcon(0); }

    // ------------------------------------------------------------------ vòng lặp
    void Update()
    {
        Combat.Now += Combat.Dt;
        float rdt = Time.deltaTime;
        if (reactionT > 0f) { reactionT -= rdt; if (reactionT <= 0f) reactionText.text = ""; }
        if (bannerT > 0f) { bannerT -= rdt; var c = bannerText.color; c.a = Mathf.Clamp01(bannerT / .5f); bannerText.color = new Color(GameTheme.Bone.r, GameTheme.Bone.g, GameTheme.Bone.b, c.a); if (bannerT <= 0f) bannerText.text = ""; }
        hudT -= rdt; if (hudT <= 0f) { hudT = .1f; RefreshHud(); }
        TouchControls.InteractVisible = false; TouchControls.DashCooldown01 = player != null ? Mathf.Clamp01(player.DashLeft / PlayerCombat.DashCooldown) : 0f;
        UpdateEffectsUi(rdt);
        UpdateBossBar();
        UpdateWeaponBar();
        if (Frozen) return;

        switch (state)
        {
            case State.Fighting:
                if (Combat.EnemyCount() == 0) { clearT -= rdt; if (clearT <= 0f) NextWave(); }
                else { clearT = .8f; SetPrompt("Đợt " + (waveIdx + 1) + "/" + WaveCount + " · còn " + Combat.EnemyCount() + " quái"); }
                break;
            case State.ChestReady:
                var k = Keyboard.current;
                bool near = Vector2.Distance(player.transform.position, chestPos) <= Reach;
                SetPrompt(near ? (TouchControls.Active ? "Chạm nút MỞ để mở Rương Câu Đố" : "Nhấn E để mở Rương Câu Đố") : "Rương Câu Đố đã hiện ra giữa phòng. Lại gần để mở.");
                TouchControls.InteractVisible = near;
                if (near && Interact()) OpenChest();
                break;
            case State.DoorOpen: if (player.transform.position.y >= ExitY) LeaveRoom(); break;
            case State.Boss: SetPrompt(BossBase.Current != null && BossBase.Current.bstate == BState.Fight ? TouchControls.Active ? "" : "WASD đi · giữ chuột/J bắn (tự nhắm) · Shift lướt · Q đổi vũ khí · T tắt tự nhắm" : ""); break;
            case State.Rest: RestUpdate(); break;
            case State.Final: FinalUpdate(); break;
        }
    }

    bool Interact() { var k = Keyboard.current; return (k != null && k.eKey.wasPressedThisFrame) || TouchControls.InteractPressed; }

    void UpdateEffectsUi(float dt)
    {
        if (vignetteSr != null && Camera.main != null)
        {   // vignette luôn phủ đúng khung nhìn dù camera di chuyển
            var cam = Camera.main; float hh = cam.orthographicSize * 1.5f, hw = hh * Mathf.Max(1.2f, cam.aspect);
            vignetteSr.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
            var b = vignetteSr.sprite.bounds.size; vignetteSr.transform.localScale = new Vector3(hw * 2f / b.x, hh * 2f / b.y, 1f);
        }
        overlayImg.color = Color.Lerp(overlayImg.color, overlayTarget, 1f - Mathf.Exp(-3f * dt));
        hurtA = Mathf.MoveTowards(hurtA, 0f, dt * 1.6f); float low = RunState.hp / PlayerCombat.MaxHp < .3f ? .18f + .12f * Mathf.Sin(Time.time * 5f) : 0f;
        hurtImg.color = new Color(.8f, .05f, .1f, Mathf.Max(hurtA * .6f, low));
        if (introT > 0f)
        {
            introT -= dt; float k = Mathf.Clamp01(Mathf.Min((2.6f - introT) / .4f, introT / .5f));
            topBar.rectTransform.anchoredPosition = new Vector2(0, Mathf.Lerp(640f, 458f, k)); botBar.rectTransform.anchoredPosition = new Vector2(0, Mathf.Lerp(-640f, -458f, k));
            introName.color = new Color(GameTheme.Bone.r, GameTheme.Bone.g, GameTheme.Bone.b, k); introTitle.color = new Color(GameTheme.EmberHi.r, GameTheme.EmberHi.g, GameTheme.EmberHi.b, k);
            introName.transform.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, k);
            if (introT <= 0f) { introName.text = ""; introTitle.text = ""; }
        }
        else { topBar.rectTransform.anchoredPosition = new Vector2(0, 640); botBar.rectTransform.anchoredPosition = new Vector2(0, -640); }
        // bóng tối của Ảo Cảnh đi theo người chơi
        darkCur = Mathf.MoveTowards(darkCur, Time.time < darkUntil ? darkTarget : 0f, dt * .6f);
        if (darkSr != null) { darkSr.color = new Color(1, 1, 1, darkCur); if (player != null) darkSr.transform.position = player.transform.position; }
    }

    void UpdateBossBar()
    {
        var b = BossBase.Current; if (b == null || bossRoot == null || !bossRoot.gameObject.activeSelf) return;
        float v = b.hp / Mathf.Max(1f, b.maxHp); bossFill.fillAmount = Mathf.MoveTowards(bossFill.fillAmount, v, Time.deltaTime * 1.5f);
        bossFill.color = !b.vulnerable ? new Color(.6f, .6f, .7f) : Color.Lerp(new Color(.9f, .25f, .3f), new Color(1f, .55f, .3f), Mathf.PingPong(Time.time * 2f, 1f) * (v < .26f ? 1f : 0f));
        bossName.text = b.bossName + "  ·  pha " + Mathf.Min(4, b.phaseIdx + 1) + "/4" + (!b.vulnerable && b.bstate == BState.Transition ? "  ·  ĐANG NẠP TUYỆT KỸ" : "");
    }

    void UpdateWeaponBar()
    {   // thanh nhiệt (laze), lực kéo (cung), đạn (tên lửa) bám dưới chân người chơi
        if (player == null || weaponBarBg == null && !MakeWeaponBar()) return;
        var w = RunState.main; float v = 0f; Color c = Color.white; bool show = false; int pipN = 0;
        if (w.id == WeaponId.Laser && (player.HeatFrac > .01f || player.Overheated)) { v = player.HeatFrac; c = player.Overheated ? new Color(1f, .3f, .2f) : Color.Lerp(new Color(.4f, .9f, 1f), new Color(1f, .5f, .2f), v); show = true; }
        else if (w.id == WeaponId.Bow && player.Charge > .02f) { v = player.Charge; c = Color.Lerp(new Color(1f, .9f, .5f), Color.white, v); show = true; }
        else if (w.id == WeaponId.Rocket) { pipN = player.Reloading ? 0 : Mathf.Max(0, player.Ammo); if (player.Ammo < 0) pipN = 4 + RunState.extraShots; show = true; v = player.Reloading ? .5f + .5f * Mathf.PingPong(Time.time * 3f, 1f) : 0f; c = new Color(1f, .6f, .3f); if (!player.Reloading) v = 0f; }
        var pos = (Vector2)player.transform.position + new Vector2(0, -.95f);
        weaponBarBg.enabled = show && w.id != WeaponId.Rocket || (w.id == WeaponId.Rocket && player.Reloading); weaponBarFill.enabled = weaponBarBg.enabled; weaponBarBg.transform.position = pos; weaponBarFill.transform.position = pos - new Vector2((1f - v) * .6f, 0f);
        weaponBarFill.transform.localScale = new Vector3(Mathf.Max(.001f, 1.2f * v / weaponBarFill.sprite.bounds.size.x), .1f / weaponBarFill.sprite.bounds.size.y, 1f); weaponBarFill.color = c;
        for (int i = 0; i < pips.Length; i++) { pips[i].enabled = show && w.id == WeaponId.Rocket && !player.Reloading && i < pipN; pips[i].transform.position = pos + new Vector2((i - (pipN - 1) * .5f) * .22f, 0f); }
    }

    bool MakeWeaponBar()
    {
        weaponBarBg = Combat.MakeSr("WBarBg", world, ThemeGfx.Pixel, new Color(0, 0, 0, .7f), new Vector2(1.3f, .16f), Combat.OrdFx + 30); weaponBarFill = Combat.MakeSr("WBarFill", world, ThemeGfx.Pixel, Color.white, new Vector2(1.2f, .1f), Combat.OrdFx + 31);
        for (int i = 0; i < pips.Length; i++) pips[i] = Combat.MakeSr("Pip", world, RogueArt.Rocket, Color.white, new Vector2(.28f, .14f), Combat.OrdFx + 32);
        return true;
    }

    // ------------------------------------------------------------------ màn thường
    int PlanIndex => Mathf.Clamp(RunState.room, 1, RunState.RoomsPerStage) - 1;
    int[] CurPlan => WavePlans[Mathf.Clamp(RunState.stage, 1, 3) - 1][PlanIndex];
    int WaveCount => CurPlan.Length + RoomRules.ExtraWaveCount(RunState.difficulty);
    int WaveSize(int idx) { var plan = CurPlan; return plan[Mathf.Min(idx, plan.Length - 1)]; }

    void NextWave()
    {
        waveIdx++; clearT = .8f;
        if (waveIdx >= WaveCount) { SpawnChest(); return; }
        var diff = RunState.difficulty;
        int poolIdx = Mathf.Min(Pools.Length - 1, (RunState.stage - 1) + PlanIndex + RoomRules.PoolBonus(diff)); var pool = Pools[poolIdx];
        float eliteRate = EliteRate[Mathf.Clamp(RunState.stage, 1, 3) - 1] + RoomRules.EliteRateBonus(diff);
        int count = Mathf.CeilToInt(WaveSize(waveIdx) * RoomRules.CountMul(diff) - .01f);
        for (int i = 0; i < count; i++)
        {
            var kind = (waveIdx == 0 && i == 0 && (RunState.room > 1 || RunState.stage > 1)) ? pool[pool.Length - 1] : pool[rng.Next(pool.Length)];
            Monster.Create(kind, SpawnPoint(), rng.NextDouble() < eliteRate);
        }
        if (waveIdx > 0) RogueAudio.Play(RSfx.Warn, .4f, 1.5f);
    }

    Vector2 SpawnPoint()
    {
        for (int t = 0; t < 30; t++)
        {
            var p = new Vector2(Mathf.Lerp(-HalfW + 1.2f, HalfW - 1.2f, (float)rng.NextDouble()), Mathf.Lerp(MinY + 1f, WallY - .7f, (float)rng.NextDouble()));
            if (Vector2.Distance(p, player.transform.position) < 5f) continue;
            bool blocked = false; foreach (var pr in RoomProp.All) if (pr != null && Vector2.Distance(p, pr.Pos) < 1.3f) { blocked = true; break; }
            if (!blocked) return p;
        }
        return new Vector2(0, WallY - 1f);
    }

    void OnFoeKilled(Foe f)
    {
        var m = f as Monster; if (m == null) { if (f is BossBase) return; if (player != null) player.OnKill(); return; }
        RunState.ink += m.kind == MonsterKind.Minion ? 1 : m.elite ? 25 : 5;
        if (m.kind != MonsterKind.Minion && (m.elite || Random.value < .15f)) CombatPickup.SpawnHeal(m.transform.position, 15f);
        if (player != null) player.OnKill();
    }

    GameObject chestRoot; SpriteRenderer chestSpr;
    void SpawnChest()
    {
        state = State.ChestReady;
        chestRoot = new GameObject("Chest"); chestRoot.transform.SetParent(world, false); chestRoot.transform.localPosition = chestPos + new Vector2(0, -.45f);
        var glow = Combat.MakeSr("ChestGlow", chestRoot.transform, ThemeGfx.Glow, GameTheme.Gold.WithAlpha(.4f), new Vector2(3f, 2.4f), 5, new Vector2(0, .4f)); glow.sharedMaterial = Gfx.Additive;
        chestSpr = Combat.MakePx("Spr", chestRoot.transform, RogueArt.Chest(false), 6, null, 1.15f);
        RFx.Ring(chestPos, 1.8f, GameTheme.Gold, .5f); RFx.Burst(chestPos, GameTheme.GoldHi, 14, 4f, .7f, .16f, 1f); RogueAudio.Play(RSfx.Chest, .8f); GameAudio.Play(Snd.BuffOpen);
    }

    void OpenChest()
    {
        if (problems.Count == 0) { UIKit.Toast(hud, "Chưa có dữ liệu bài tự luận."); return; }
        state = State.Solving; Frozen = true; RogueAudio.Play(RSfx.Select, .7f);
        var p = RoomRules.PickProblem(problems, Subject, RunState.difficulty, usedIds, rng);
        if (p == null) { UIKit.Toast(hud, "Môn này chưa có bài."); Frozen = false; state = State.ChestReady; return; }
        difficulty = RunState.difficulty; prob = p; usedIds.Add(p.id); attemptsUsed = 0; missStreak = new int[p.rubric.Length]; inBossEssay = false; essaySubject = Subject;
        essayTitle = "Rương Câu Đố · Màn " + StageLabel + " · " + SubjectName(Subject) + " · " + RoomRules.DifficultyName(difficulty);
        StartAttempt();
    }

    void NewVariant()
    {
        string prev = variant != null ? variant.statement : null;
        for (int t = 0; t < 12; t++)
        {
            variant = EssayVariantGenerator.Generate(prob, EssayVariantGenerator.SeedFor(StudentCode, prob.id, attemptCounter++));
            if (variant.statement != prev) break;
        }
    }

    void StartAttempt()
    {
        NewVariant();
        int have = prob.hints != null ? prob.hints.Length : 0; hintCap = Mathf.Min(RoomRules.MaxHints(difficulty), have); hintLevel = 0; hintsPaid = 0;
        bool streak = false; foreach (var m in missStreak) if (m >= 2) streak = true;
        if (hintCap > 0 && (RoomRules.AutoFirstHint(difficulty) || streak)) hintLevel = 1;
        ShowWriting("");
    }

    void ShowWriting(string draft)
    {
        Frozen = true; if (!inBossEssay) state = State.Solving;
        var a = new SolveWritingArgs
        {
            title = essayTitle + (inBossEssay ? " · một lần duy nhất" : " · Lần " + (attemptsUsed + 1)), subjectId = essaySubject, statement = variant.statement, draft = draft,
            hintText = HintText(), showHintButton = hintCap > 0, hintButtonLabel = HintButtonLabel(), hintEnabled = hintLevel < hintCap,
            onSubmit = OnSubmit, onHint = OnHint, onExit = OnExitEssay,
            onPickImage = ImageAllowed ? (System.Action<string>)OnPickImage : null,
        };
        solve.ShowWriting(a);
    }

    string HintText() => hintLevel > 0 && prob.hints != null && hintLevel <= prob.hints.Length ? "Gợi ý " + hintLevel + ": " + prob.hints[hintLevel - 1] : "";
    string HintButtonLabel() => hintLevel >= hintCap ? "Hết gợi ý" : "Gợi ý (" + hintLevel + "/" + hintCap + ")";

    void OnHint(string draft)
    {
        if (hintLevel >= hintCap) return;
        hintLevel++; hintsPaid++; solve.SetHint(HintText(), HintButtonLabel(), hintLevel < hintCap);
    }

    void OnExitEssay(string draft)
    {
        gradeToken++; grading = false; visionToken++; DropTex();
        if (inBossEssay) { solve.Hide(); Frozen = false; inBossEssay = false; var d = bossDone; bossDone = null; UIKit.Toast(hud, "Bỏ trụ đá: tính là chưa đạt."); d?.Invoke(GradeBand.Fail, null); return; }
        if (prob != null) usedIds.Remove(prob.id);
        solve.Hide(); Frozen = false; state = State.ChestReady; UIKit.Toast(hud, "Đã thoát đề, không tính lần làm.");
    }

    // ------------------------------------------------------------------ chấm bài: AI (Gemini) hoặc bộ chấm tạm, và đọc ảnh bài viết tay
    bool grading; int gradeToken, visionToken; Texture2D lastTex; string lastDraft = "";
    /// <summary>Nút tải ảnh bài viết tay hiện ở độ khó Khó trở lên trên máy tính; có bấm được hay không phụ thuộc AI đã kết nối chưa (xem OnPickImage).</summary>
    bool ImageAllowed => NativeFilePicker.Supported;

    void DropTex() { if (lastTex != null) { Destroy(lastTex); lastTex = null; } }

    void OnPickImage(string draft)
    {
        lastDraft = draft ?? "";
        if (!EssayAI.Available)
        {
            UIKit.Toast(solve.Root, AIService.Instance == null ? "Chưa có AIService trong scene. Hãy vào game từ LoginScene, hoặc thêm một GameObject gắn AIService (điền API key) vào RoomScene." : "AIService chưa có API key nên chưa đọc được ảnh.", 4f);
            return;
        }
        string path = NativeFilePicker.PickImage(); if (string.IsNullOrEmpty(path)) return;
        if (!EssayImageTools.Load(path, out var jpg, out var tex, out var err)) { UIKit.Toast(solve.Root, err); return; }
        DropTex(); lastTex = tex; SendImage(jpg);
    }

    void SendImage(byte[] jpg)
    {
        int token = ++visionToken;
        solve.ShowBusy("Thầy Cú đang đọc ảnh", "Thầy đọc từng dòng chữ viết tay của em", () => { visionToken++; DropTex(); ShowWriting(lastDraft); }, "Quay lại gõ tay");
        EssayVision.Read(jpg, (list, err) =>
        {
            if (token != visionToken) return;
            if (list == null) { DropTex(); ShowWriting(lastDraft); UIKit.Toast(solve.Root, err ?? "Không đọc được ảnh."); return; }
            solve.ShowTranscript(new SolveTranscriptArgs
            {
                title = essayTitle, subjectId = essaySubject, preview = lastTex, lines = list,
                onConfirm = text => { DropTex(); OnSubmit(text); },
                onOtherImage = () => { OnPickImage(lastDraft); },
                onCancel = () => { DropTex(); ShowWriting(lastDraft); },
            });
        });
    }

    void OnSubmit(string raw)
    {
        var check = EssaySymbols.Prepare(raw, out var lines);
        if (check == SubmitCheck.Empty) { UIKit.Toast(solve.Root, "Em chưa viết lời giải."); return; }
        if (check == SubmitCheck.TooShort) { UIKit.Toast(solve.Root, "Lời giải còn quá ngắn, em viết thêm nhé."); return; }
        if (check == SubmitCheck.TooLong) { UIKit.Toast(solve.Root, "Lời giải dài quá " + EssaySymbols.MaxChars + " ký tự."); return; }
        lastDraft = raw; BeginGrade(lines);
    }

    /// <summary>Có AI thì nhờ Gemini chấm kèm phân tích; AI chậm hoặc lỗi thì rơi về bộ chấm tạm để người chơi không bị kẹt.</summary>
    void BeginGrade(string[] lines)
    {
        if (grading) return;
        if (!EssayAI.Available) { FinishGrade(lines, EssayFreeformGrader.Grade(prob, variant, lines), "Chưa kết nối AI nên đang dùng bộ chấm tạm (không có phân tích)."); return; }
        grading = true; int token = ++gradeToken; bool done = false;
        solve.ShowBusy("Thầy Cú đang chấm bài", "Thầy đang đọc kỹ từng dòng lời giải của em", () => { if (done) return; done = true; grading = false; gradeToken++; FinishGrade(lines, EssayFreeformGrader.Grade(prob, variant, lines), "Em chọn dùng bộ chấm tạm."); });
        EssayAIGrader.Grade(prob, variant, lines, (res, err) =>
        {
            if (done || token != gradeToken) return; done = true; grading = false;
            if (res == null) { Debug.LogWarning("AI chấm lỗi: " + err); FinishGrade(lines, EssayFreeformGrader.Grade(prob, variant, lines), "AI chưa phản hồi nên tạm dùng bộ chấm đơn giản."); }
            else FinishGrade(lines, res, null);
        });
    }

    void FinishGrade(string[] lines, EssayGradeResult res, string note)
    {
        attemptsUsed++;
        for (int i = 0; i < missStreak.Length; i++) missStreak[i] = res.steps[i].status == EssayStepStatus.Missing || res.steps[i].status == EssayStepStatus.Wrong ? missStreak[i] + 1 : 0;
        var band = RoomRules.Band(res.score, res.maxScore);
        int earned = RoomRules.InkFor(res.score, difficulty, hintsPaid); RunState.ink += earned; RefreshHud();
        var r = new SolveResultArgs { subjectId = essaySubject, problem = prob, variant = variant, grade = res, band = band, lines = lines, note = note };
        var ai = res as EssayAIResult; if (ai != null) { r.analysis = ai.AnalysisText(); r.stepWhy = ai.why; }

        if (inBossEssay)
        {   // trụ đá của boss: một lần duy nhất, kết quả đi thẳng vào hiệu ứng tuyệt kỹ (mục 6.1)
            r.revealDesc = true; r.title = band == GradeBand.Good ? "Đạt tốt" : band == GradeBand.Pass ? "Đạt" : "Chưa đạt";
            r.footer = BossEffectText(band) + "\n+" + earned + " Mực";
            string bnote = band == GradeBand.Pass ? MissingNoteFor(res) : null;
            r.buttons.Add(new SolveResultButton { label = "Tiếp tục trận đấu", onClick = () => { solve.Hide(); Frozen = false; inBossEssay = false; var d = bossDone; bossDone = null; d?.Invoke(band, bnote); } });
            GameAudio.Play(band == GradeBand.Fail ? Snd.QuizWrong : Snd.QuizCorrect); solve.ShowResult(r); return;
        }

        if (RoomRules.DoorOpens(band))
        {
            int rarity = RoomRules.RollRarity(difficulty, band, rng);
            r.title = band == GradeBand.Good ? "Đạt tốt" : "Đạt"; r.revealDesc = true;
            r.footer = "Rương mở ra một vũ khí độ hiếm " + RoomRules.RarityNames[rarity] + (band == GradeBand.Pass ? " (giảm một bậc vì mới đạt)" : "") + ".\n+" + earned + " Mực" + (hintsPaid > 0 ? " (đã trừ " + Mathf.RoundToInt(hintsPaid * RoomRules.HintInkPenalty * 100) + "% vì gợi ý)" : "");
            r.buttons.Add(new SolveResultButton { label = "Nhận vũ khí", onClick = () => OnSolvePassed(rarity) });
            GameAudio.Play(Snd.QuizCorrect);
        }
        else
        {
            r.title = "Chưa đạt"; r.revealDesc = false; bool canUnlock = attemptsUsed >= RoomRules.MaxAttempts; int left = RoomRules.MaxAttempts - attemptsUsed;
            r.footer = (canUnlock ? "Em đã làm " + attemptsUsed + " lần. Có thể phá khóa bằng " + RoomRules.UnlockInkCost + " Mực (rương không cho vũ khí, bài này vào hàng ôn lại)." : "Còn " + left + " lần làm lại, mỗi lần có số liệu mới.") + "\n+" + earned + " Mực. Không mất máu, không mất vũ khí.";
            r.buttons.Add(new SolveResultButton { label = "Làm lại (số liệu mới)", onClick = StartAttempt, style = ButtonStyle.Primary });
            if (canUnlock)
            {
                if (RunState.ink >= RoomRules.UnlockInkCost) r.buttons.Add(new SolveResultButton { label = "Phá khóa (-" + RoomRules.UnlockInkCost + " Mực)", onClick = OnUnlockWithInk, style = ButtonStyle.Danger });
                else r.footer += "\nChưa đủ Mực để phá khóa, em cứ làm lại để kiếm thêm.";
            }
            GameAudio.Play(Snd.QuizWrong);
        }
        solve.ShowResult(r);
    }

    string BossEffectText(GradeBand band)
    {
        int bi = RunState.stage;
        if (bi == 1) return band == GradeBand.Good ? "Khiên chặn trọn tuyệt kỹ gai độc." : band == GradeBand.Pass ? "Khiên chặn khoảng 60% sát thương gai độc." : "Không có khiên. Sát thương của tuyệt kỹ bị chặn trần ở 30% máu tối đa.";
        if (bi == 2) return band == GradeBand.Fail ? "Không có bùa chỉ đường: bản thể ẩn giữa các bóng giả." : band == GradeBand.Good ? "Bùa hiện ra chỉ đúng bản thể, Cáo choáng 6 giây và chịu thêm sát thương." : "Bùa hiện ra chỉ đúng bản thể, Cáo choáng 6 giây.";
        return band == GradeBand.Good ? "Máy biến áp phản đòn trọn vẹn: tia hủy diệt dội ngược về boss." : band == GradeBand.Pass ? "Tia bị chuyển hướng, boss chịu sát thương vừa." : "Không phản đòn được: tia quét ngang, sát thương bị chặn trần ở 30% máu tối đa.";
    }

    string MissingNoteFor(EssayGradeResult res)
    {
        var aiRes = res as EssayAIResult; if (aiRes != null && !string.IsNullOrEmpty(aiRes.nextHint)) return SubjectName(essaySubject) + (string.IsNullOrEmpty(prob.concept) ? "" : " · " + prob.concept) + ": " + aiRes.nextHint;
        var miss = new List<string>();
        for (int i = 0; i < res.steps.Length; i++) if (res.steps[i].status != EssayStepStatus.Correct) miss.Add(prob.rubric[i].label);
        if (miss.Count == 0) return null;
        return SubjectName(essaySubject) + (string.IsNullOrEmpty(prob.concept) ? "" : " · " + prob.concept) + ": em còn thiếu phần " + string.Join(", ", miss) + ".";
    }

    void OnUnlockWithInk()
    {
        RunState.ink -= RoomRules.UnlockInkCost; unlockedByInk = true; RefreshHud(); solve.Hide(); UIKit.Toast(hud, "Đã phá khóa: rương không có vũ khí."); OpenDoor();
    }

    // ------------------------------------------------------------------ Trụ đá của boss: giải đề một lần
    public void StartBossEssay(EssayDifficulty d, string subject, System.Action<GradeBand, string> done)
    {
        bossDone = done; inBossEssay = true; essaySubject = subject; difficulty = d; Frozen = true;
        var p = RoomRules.PickProblem(problems, subject, d, usedIds, rng);
        if (p == null) { foreach (var s in new[] { "ly", "hoa", "sinh" }) { p = RoomRules.PickProblem(problems, s, d, usedIds, rng); if (p != null) { essaySubject = s; break; } } }
        if (p == null) { Frozen = false; inBossEssay = false; UIKit.Toast(hud, "Chưa có đề cho trụ đá."); done?.Invoke(GradeBand.Pass, null); return; }
        prob = p; usedIds.Add(p.id); attemptsUsed = 0; missStreak = new int[p.rubric.Length];
        essayTitle = (BossBase.Current != null ? BossBase.Current.bossName : "Boss") + " · Trụ đá · " + SubjectName(essaySubject) + " · " + RoomRules.DifficultyName(d);
        NewVariant(); int have = prob.hints != null ? prob.hints.Length : 0; hintCap = Mathf.Min(RoomRules.MaxHints(d), have); hintLevel = 0; hintsPaid = 0;
        if (hintCap > 0 && RoomRules.AutoFirstHint(d)) hintLevel = 1;
        ShowWriting("");
    }

    // ------------------------------------------------------------------ phần thưởng và cửa
    void OnSolvePassed(int rarity) { solve.Hide(); ShowWeaponPanel(Weapons.Roll(rarity, rng)); }

    RectTransform OpenPanel(Vector2 size, out RectTransform content)
    {
        ClosePanel(); Frozen = true; state = State.Panel;
        panelRoot = UIKit.CBox("PanelRoot", hud, new Color(0, 0, 0, .72f), Vector2.zero, Vector2.zero, true); UIKit.Stretch(panelRoot, 0, 0, 0, 0);
        UIKit.Panel(panelRoot, "Panel", Vector2.zero, size, PanelStyle.Dark, out content); return panelRoot;
    }

    void ClosePanel() { if (panelRoot != null) { Destroy(panelRoot.gameObject); panelRoot = null; } }

    void ShowWeaponPanel(WeaponInstance w)
    {
        OpenPanel(new Vector2(1300, 680), out var c); var d = w.Def;
        UIKit.Label("T", c, "Rương mở ra", 60, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, 260), new Vector2(1100, 80), true, true, FontStyle.Bold);
        var ic = Icon("WIcon", c, w.Icon, new Vector2(-480, 120), new Vector2(240, 150)); ic.color = Color.white;
        UIKit.Label("W", c, w.Label, 46, RogueArt.RarityCols[w.rarity], TextAnchor.MiddleLeft, new Vector2(120, 160), new Vector2(900, 70), true);
        UIKit.Label("S", c, "Sát thương " + w.Damage.ToString("0.#") + "   ·   " + (1f / d.cooldown).ToString("0.0") + " đòn/giây   ·   " + d.group, 28, GameTheme.Fog, TextAnchor.MiddleLeft, new Vector2(120, 100), new Vector2(900, 44));
        UIKit.Label("D", c, d.desc, 26, GameTheme.Bone, TextAnchor.MiddleLeft, new Vector2(120, 50), new Vector2(900, 44));
        UIKit.Label("Cur", c, "Đang cầm  ·  Chính: " + RunState.main.Label + "   ·   Phụ: " + (RunState.sub != null ? RunState.sub.Label : "chưa có"), 26, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, -40), new Vector2(1200, 60));
        UIKit.MakeButton(c, "Main", "Thay vũ khí chính", new Vector2(-400, -170), new Vector2(380, 80), () => { RunState.main = w; AfterWeapon(); }, ButtonStyle.Primary, 30);
        UIKit.MakeButton(c, "Sub", RunState.sub == null ? "Làm vũ khí phụ" : "Thay vũ khí phụ", new Vector2(0, -170), new Vector2(380, 80), () => { RunState.sub = w; AfterWeapon(); }, ButtonStyle.Bronze, 30, .1f);
        UIKit.MakeButton(c, "Skip", "Bỏ qua", new Vector2(400, -170), new Vector2(380, 80), AfterWeapon, ButtonStyle.Secondary, 30, .2f);
    }

    void AfterWeapon() { ClosePanel(); RefreshHud(); OpenDoor(); }

    void OpenDoor()
    {
        state = State.DoorOpen; Frozen = false; DoorOpen = true; doorSr.color = GameTheme.Moss;
        if (doorGlow != null) doorGlow.color = GameTheme.Moss.WithAlpha(.5f);
        if (chestSpr != null) { chestSpr.sprite = RogueArt.Chest(true); }
        RogueAudio.Play(RSfx.Door, .8f); GameAudio.Play(Snd.BuffPick);
        SetPrompt("Cửa đã mở. Đi lên cửa phía trên để sang màn kế tiếp.");
    }

    void LeaveRoom() { state = State.Leaving; Frozen = true; if (RunState.room % 2 == 0) ShowBuffPanel(); else GoNext(); }

    void ShowBuffPanel()
    {
        OpenPanel(new Vector2(1600, 700), out var c);
        UIKit.Label("T", c, "Chọn một buff", 60, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, 260), new Vector2(1300, 80), true, true, FontStyle.Bold);
        UIKit.Label("S", c, "Đã qua " + RunState.room + " màn. Ít nhất một buff hợp với vũ khí đang cầm (" + RunState.main.Def.name + ").", 26, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, 200), new Vector2(1400, 40));
        var offer = Buffs.Offer(RunState.main.Def, rng);
        for (int i = 0; i < offer.Count; i++)
        {
            var b = offer[i]; float x = (i - 1) * 490f;
            UIKit.MakeButton(c, "Buff" + i, b.name, new Vector2(x, 20), new Vector2(450, 120), () => { RunState.AddBuff(b, rng); ClosePanel(); GoNext(); }, ButtonStyle.Bronze, 32, .1f * i);
            Icon("BI" + i, c, b.icon != null ? ProcSprites.BuffIcon(b.icon) : RogueArt.BuffIcon((int)b.id), new Vector2(x, 130), new Vector2(72, 72));
            UIKit.Label("Desc" + i, c, b.desc + (Buffs.Suits(b, RunState.main.Def) ? "" : ""), 26, GameTheme.Bone, TextAnchor.UpperCenter, new Vector2(x, -110), new Vector2(450, 110));
        }
    }

    void GoNext()
    {
        RunState.room++;
        if (RunState.room > RunState.RoomsPerStage) { RunState.room = 5; RunState.phase = RunPhase.Boss; }
        RunSave.Save(); Reload();
    }

    // ------------------------------------------------------------------ BOSS
    BossBase boss;
    void BeginBoss()
    {
        state = State.Boss; HazardsOn = true; RogueAudio.PlayTrack(RTrack.Altar);
        Vector2 at = new Vector2(0, 1.5f);
        if (RunState.stage == 1) boss = BossBase.Create<BossRat>(at); else if (RunState.stage == 2) boss = BossBase.Create<BossFox>(at); else boss = BossBase.Create<BossScientist>(at);
        boss.Defeated = OnBossDefeated; RunSave.Save();
    }

    void OnBossDefeated()
    {
        RunState.bossesDown++; RunState.shards = Mathf.Min(3, RunState.shards + 1); RunState.ink += 120; RefreshHud(); RogueAudio.Duck(.5f);
        state = State.Cutscene; Frozen = false; player.Controllable = false; RogueAudio.PlayTrack(RTrack.None);
        story.PlayFlashback(RunState.stage, () =>
        {
            player.Controllable = true; OpenBossReward();
        });
    }

    void OpenBossReward()
    {
        OpenPanel(new Vector2(1800, 760), out var c);
        UIKit.Label("T", c, "Chiến lợi phẩm của " + (RunState.stage == 1 ? "Chuột Bạch" : RunState.stage == 2 ? "Cáo Chín Đuôi" : "Nhà Khoa Học Điên"), 56, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, 310), new Vector2(1600, 80), true, true, FontStyle.Bold);
        UIKit.Label("S", c, "Chọn một trong ba vũ khí (độ hiếm cao, thường kèm nguyên tố). Em nhặt thêm Mảnh Ký Ức " + RunState.shards + "/3 và 120 Mực.", 26, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, 250), new Vector2(1600, 40));
        int lo = RunState.stage == 1 ? 1 : 2, hi = RunState.stage == 3 ? 3 : 2; var offers = Weapons.RollChoices(lo, hi, rng);
        for (int i = 0; i < 3; i++)
        {
            var w = offers[i]; float x = (i - 1) * 560f; var d = w.Def;
            var card = UIKit.CBox("Card" + i, c, new Color(.07f, .05f, .12f, .9f), new Vector2(x, 20), new Vector2(520, 440)); card.GetComponent<Image>().sprite = ThemeGfx.Glow;
            var ic = Icon("WI" + i, c, w.Icon, new Vector2(x, 150), new Vector2(300, 150));
            UIKit.Label("N" + i, c, w.Label, 34, RogueArt.RarityCols[w.rarity], TextAnchor.MiddleCenter, new Vector2(x, 50), new Vector2(500, 60), true);
            UIKit.Label("D" + i, c, "Sát thương " + w.Damage.ToString("0.#") + " · " + (1f / d.cooldown).ToString("0.0") + "/s\n" + d.desc, 24, GameTheme.Bone, TextAnchor.UpperCenter, new Vector2(x, -40), new Vector2(480, 110));
            UIKit.MakeButton(c, "Main" + i, "Làm vũ khí chính", new Vector2(x, -170), new Vector2(430, 64), () => { RunState.main = w; ClaimBoss(); }, ButtonStyle.Primary, 26, .1f * i);
            UIKit.MakeButton(c, "Sub" + i, RunState.sub == null ? "Làm vũ khí phụ" : "Thay vũ khí phụ", new Vector2(x, -245), new Vector2(430, 54), () => { RunState.sub = w; ClaimBoss(); }, ButtonStyle.Bronze, 24, .1f * i + .05f);
        }
    }

    void ClaimBoss()
    {
        ClosePanel(); RogueAudio.Play(RSfx.Pickup, .9f);
        RunState.phase = RunPhase.Rest; RunState.room = 1; RunState.SaveCheckpoint(); RunSave.Save(); Reload();
    }

    // ------------------------------------------------------------------ PHÒNG NGHỈ
    SpriteRenderer fireSr, owlSr; Vector2 firePos = new Vector2(1.4f, -.6f), owlPos = new Vector2(-1.8f, -.5f); bool rested; Sprite[] fireFr, owlFr; float restT;
    void BeginRest()
    {
        state = State.Rest; HazardsOn = false; DoorOpen = true; rested = false; RogueAudio.PlayTrack(RTrack.Rest);
        doorSr.color = GameTheme.Moss; doorGlow.color = GameTheme.Moss.WithAlpha(.5f);
        fireFr = RogueArt.Campfire; owlFr = RogueArt.Owl;
        var fg = Combat.MakeSr("FireGlow", world, ThemeGfx.Glow, GameTheme.Ember.WithAlpha(.3f), new Vector2(8f, 6f), -95, firePos + new Vector2(0, .6f)); fg.sharedMaterial = Gfx.Additive;
        fireSr = Combat.MakePx("Fire", world, fireFr[0], Combat.Order(firePos.y), firePos, 1.25f);
        owlSr = Combat.MakePx("Owl", world, owlFr[0], Combat.Order(owlPos.y), owlPos, 1.2f);
        ShowBanner("Phòng nghỉ · đến bên lửa trại (E) để hồi sức, trò chuyện với Thầy Cú rồi đi tiếp", 5f);
        RunSave.Save();
    }

    void RestUpdate()
    {
        restT += Time.deltaTime; fireSr.sprite = fireFr[(int)(restT * 8f) % fireFr.Length]; owlSr.sprite = owlFr[(restT % 4f) > 3.6f ? 2 : (int)(restT * 1.5f) % 2]; owlSr.sortingOrder = Combat.Order(owlPos.y);
        if (Random.value < Time.deltaTime * 8f) RFx.Burst(firePos + new Vector2(Random.Range(-.3f, .3f), .8f), GameTheme.EmberHi, 1, 1.5f, .8f, .1f, -2f);
        var k = Keyboard.current; var pp = (Vector2)player.transform.position;
        bool nearFire = Vector2.Distance(pp, firePos + Vector2.up * .5f) < 1.8f, nearOwl = Vector2.Distance(pp, owlPos + Vector2.up * .5f) < 1.7f;
        TouchControls.InteractVisible = (nearFire && !rested) || nearOwl;
        if (nearFire && !rested) SetPrompt("Nhấn E · nghỉ bên lửa trại (hồi 50% máu)");
        else if (nearFire) SetPrompt("Em đã nghỉ ngơi. Đi lên cửa phía trên để sang chặng sau.");
        else if (nearOwl) SetPrompt("Nhấn E · trò chuyện với Thầy Cú");
        else SetPrompt(rested ? "Cửa đã mở. Đi lên cửa phía trên để sang chặng sau." : "Lại gần lửa trại hoặc Thầy Cú. Cửa phía trên đã mở.");
        if (Interact())
        {
            if (nearFire && !rested) { rested = true; float h = PlayerCombat.MaxHp * .5f; player.Heal(h); RFx.Burst(player.transform.position, GameTheme.Moss, 14, 3f, .8f, .16f, -2f); RFx.Ring(player.transform.position, 1.2f, GameTheme.Moss, .5f); RogueAudio.Play(RSfx.Heal, .9f); RunState.hp = Mathf.Min(PlayerCombat.MaxHp, RunState.hp); RunSave.Save(); }
            else if (nearOwl) ShowOwlTalk();
        }
        if (pp.y >= ExitY) LeaveRest();
    }

    void ShowOwlTalk()
    {
        OpenPanel(new Vector2(1500, 720), out var c);
        var oi = Icon("Owl", c, RogueArt.Owl[0], new Vector2(-560, 60), new Vector2(240, 300));
        UIKit.Label("T", c, "Thầy Cú", 52, GameTheme.EmberHi, TextAnchor.MiddleLeft, new Vector2(-380, 270), new Vector2(900, 70), true, true, FontStyle.Bold);
        string line;
        if (RunState.stage == 1) line = "Giỏi lắm! Chuột Bạch từng là bạn của một cô trợ giảng hóa học. Em vừa lấy lại được một mảnh ký ức. Đừng quên rằng mỗi bài giải em viết ra đều là một ký ức được gọi trở về.";
        else if (RunState.stage == 2) line = "Cáo Chín Đuôi đã quên mất mình là ai. Khi em đặt tên cho từng cơ quan, từng quá trình sống, sương mù lùi lại một chút. Chặng cuối sẽ cần tới cả sức mạnh vật lí của em.";
        else line = "Em đã đi qua cả ba nỗi nhớ. Phía trước là bệ đá cổ, nơi màn sương được phong ấn. Hãy mang theo cả ba mảnh ký ức.";
        UIKit.Label("L", c, "\"" + line + "\"", 30, GameTheme.Bone, TextAnchor.UpperLeft, new Vector2(110, 140), new Vector2(920, 220));
        string note = RunState.notes.Count > 0 ? "Thầy ghi chú cho em:\n• " + string.Join("\n• ", RunState.notes.GetRange(Mathf.Max(0, RunState.notes.Count - 3), Mathf.Min(3, RunState.notes.Count))) : "Em chưa có phần nào cần ôn lại ở các trụ đá. Rất tốt!";
        UIKit.Label("N", c, note, 26, GameTheme.Fog, TextAnchor.UpperLeft, new Vector2(110, -90), new Vector2(920, 220));
        UIKit.MakeButton(c, "Close", "Cảm ơn Thầy", new Vector2(0, -290), new Vector2(380, 76), () => { ClosePanel(); Frozen = false; state = State.Rest; }, ButtonStyle.Primary, 30);
    }

    void LeaveRest()
    {
        state = State.Leaving;
        if (RunState.stage < 3) { RunState.stage++; RunState.room = 1; RunState.phase = RunPhase.Room; RunState.SaveCheckpoint(); }
        else { RunState.phase = RunPhase.Final; RunState.room = 1; RunState.SaveCheckpoint(); }
        RunSave.Save(); RogueAudio.Play(RSfx.Door, .8f); Reload();
    }

    // ------------------------------------------------------------------ MÀN 16: BỆ ĐÁ CỔ
    SpriteRenderer altarSr; SpriteRenderer[] runeGlow = new SpriteRenderer[3]; bool ritualStarted; bool rewardChosen; Vector2 altarPos = new Vector2(0, -.6f);
    void BeginFinal()
    {
        state = State.Final; HazardsOn = false; DoorOpen = false; RogueAudio.PlayTrack(RTrack.Altar); ritualStarted = false; rewardChosen = false;
        var ag = Combat.MakeSr("AltarGlow", world, ThemeGfx.Glow, new Color(.6f, .8f, 1f, .18f), new Vector2(11f, 7f), -95, altarPos + new Vector2(0, 1f)); ag.sharedMaterial = Gfx.Additive;
        altarSr = Combat.MakePx("Altar", world, RogueArt.Altar, Combat.Order(altarPos.y) - 5, altarPos, 1.3f);
        Color[] cols = { GameTheme.Ly, GameTheme.Hoa, GameTheme.Sinh }; Vector2[] sp = { new Vector2(-3.8f, 1.1f), new Vector2(0f, 2.3f), new Vector2(3.8f, 1.1f) };
        for (int i = 0; i < 3; i++)
        {
            var st = Combat.MakePx("Stone" + i, world, RogueArt.RuneStone, Combat.Order(sp[i].y), sp[i], 1.4f);
            var gl = Combat.MakePx("Glyph" + i, world, RogueArt.RuneGlyph(i), Combat.Order(sp[i].y) + 1, sp[i] + new Vector2(0, .85f), 1.5f); gl.color = new Color(cols[i].r, cols[i].g, cols[i].b, .25f); gl.sharedMaterial = Gfx.Additive; runeGlow[i] = gl;
        }
        owlFr = RogueArt.Owl; owlSr = Combat.MakePx("Owl", world, owlFr[0], Combat.Order(-1.5f), new Vector2(-6.2f, -1.8f), 1.2f);
        ShowBanner("Màn 16 · Bệ Đá Cổ — hãy đặt tay lên bệ đá (E)", 6f); RunSave.Save();
    }

    void FinalUpdate()
    {
        restT += Time.deltaTime; owlSr.sprite = owlFr[(int)(restT * 1.5f) % 2];
        for (int i = 0; i < 3; i++) { var c = runeGlow[i].color; if (!ritualStarted) { c.a = .3f + .2f * Mathf.Sin(restT * 2f + i); runeGlow[i].color = c; } }
        if (ritualStarted) return;
        var k = Keyboard.current; bool near = Vector2.Distance(player.transform.position, altarPos + Vector2.up * 1.0f) < 2.4f;
        SetPrompt(near ? "Nhấn E · đặt tay lên bệ đá" : "Đến bên bệ đá cổ ở giữa phòng."); TouchControls.InteractVisible = near;
        if (near && Interact()) ShowFinalReward();
    }

    void ShowFinalReward()
    {
        OpenPanel(new Vector2(1700, 720), out var c);
        UIKit.Label("T", c, "Phần thưởng cuối hành trình", 56, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, 290), new Vector2(1500, 80), true, true, FontStyle.Bold);
        UIKit.Label("S", c, "Chọn một thứ để mang theo cho những chuyến đi sau.", 28, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, 232), new Vector2(1500, 44));
        int legendId = rng.Next(Weapons.Count); var legend = new WeaponInstance((WeaponId)legendId, 3, Weapons.Elems[legendId % Weapons.Elems.Length]);
        int title = 1 + rng.Next(3);
        var opts = new[]
        {
            new { name = "Vũ khí Huyền thoại", desc = legend.Label + "\nLượt sau, em xuất phát với nó làm vũ khí phụ.", icon = legend.Icon, act = (System.Action)(() => Meta.LegendId = legendId) },
            new { name = "Danh hiệu", desc = "\"" + Meta.Titles[title] + "\"\nHiện bên cạnh tên em ở các lượt sau.", icon = RogueArt.MemoryShard, act = (System.Action)(() => Meta.TitleIdx = title) },
            new { name = "Cường hóa vĩnh viễn", desc = "+10 máu tối đa mỗi lượt chơi\n(cộng dồn, tối đa +50).", icon = RogueArt.Heart, act = (System.Action)(() => Meta.PermHp = Mathf.Min(5, Meta.PermHp + 1)) },
        };
        for (int i = 0; i < 3; i++)
        {
            var o = opts[i]; float x = (i - 1) * 540f;
            var card = UIKit.CBox("Card" + i, c, new Color(.07f, .05f, .12f, .9f), new Vector2(x, 20), new Vector2(500, 420)); card.GetComponent<Image>().sprite = ThemeGfx.Glow;
            Icon("RI" + i, c, o.icon, new Vector2(x, 140), new Vector2(240, 140));
            UIKit.Label("N" + i, c, o.name, 34, GameTheme.GoldHi, TextAnchor.MiddleCenter, new Vector2(x, 40), new Vector2(480, 50), true);
            UIKit.Label("D" + i, c, o.desc, 25, GameTheme.Bone, TextAnchor.UpperCenter, new Vector2(x, -30), new Vector2(460, 120));
            UIKit.MakeButton(c, "Pick" + i, "Chọn", new Vector2(x, -170), new Vector2(300, 70), () => { o.act(); ClosePanel(); StartCoroutine(Ritual()); }, ButtonStyle.Primary, 30, .1f * i);
        }
    }

    IEnumerator Ritual()
    {
        ritualStarted = true; Frozen = false; player.Controllable = false; state = State.Cutscene; RogueAudio.Duck(.35f);
        Color[] cols = { GameTheme.Ly, GameTheme.Hoa, GameTheme.Sinh };
        yield return new WaitForSeconds(.6f);
        for (int i = 0; i < 3; i++)
        {
            RogueAudio.Play(RSfx.Rune, 1f, 1f); runeGlow[i].color = new Color(cols[i].r, cols[i].g, cols[i].b, 1f); RFx.Ring(runeGlow[i].transform.position, 2.4f, cols[i], .7f); RFx.Burst(runeGlow[i].transform.position, cols[i], 20, 5f, 1f, .2f, -1f); RFx.Shake(.25f);
            ShowBanner(i == 0 ? "Mảnh ký ức thứ nhất — Vật lí" : i == 1 ? "Mảnh ký ức thứ hai — Hóa học" : "Mảnh ký ức thứ ba — Sinh học", 1.8f);
            yield return new WaitForSeconds(1.5f);
        }
        RogueAudio.Play(RSfx.Stinger, 1f); RFx.Flash(altarPos + Vector2.up * 1.5f, 16f, Color.white, .8f); RFx.Shake(.8f);
        for (int i = 0; i < 12; i++) { RFx.Lightning(altarPos + Vector2.up * 1f, altarPos + Vector2.up * 1f + Random.insideUnitCircle.normalized * Random.Range(3f, 6f), Color.white, .4f, .14f); yield return new WaitForSeconds(.08f); }
        yield return new WaitForSeconds(.8f);
        RunState.phase = RunPhase.Ending; RunSave.Save(); BeginEnding();
    }

    // ------------------------------------------------------------------ KẾT THÚC + CREDIT
    void BeginEnding()
    {
        state = State.Cutscene; HazardsOn = false; player.Controllable = false; if (stageText != null) stageText.text = "";
        story.PlayEnding(FinishRun);
    }

    void FinishRun()
    {
        Meta.Clears = Meta.Clears + 1; RunSave.Clear(); RunState.ResetAll(true); RogueAudio.Stop(); SceneRouter.Go(menuSceneName);
    }

    // ------------------------------------------------------------------ chết
    void OnPlayerDied()
    {
        GameAudio.Play(Snd.Defeat); RogueAudio.Duck(.2f);
        OpenPanel(new Vector2(1000, 600), out var c);
        string where = RunState.checkpoint.phase == RunPhase.Rest ? "phòng nghỉ sau boss của chặng " + (RunState.checkpoint.stage - 1) : "màn " + RunState.checkpoint.stage + "-1";
        UIKit.Label("T", c, "Hiệp sĩ gục ngã", 60, GameTheme.Blood, TextAnchor.MiddleCenter, new Vector2(0, 210), new Vector2(900, 90), true, true, FontStyle.Bold);
        UIKit.Label("B", c, "Bóng tối nuốt lấy em, nhưng tri thức thì không mất.\nEm quay về điểm lưu đầu chặng (" + where + "), giữ vũ khí và buff lúc đó, mất các vật nhặt trong chặng.\nMực và Mảnh Ký Ức vẫn giữ nguyên: " + RunState.ink + " Mực.", 28, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 30), new Vector2(900, 240));
        UIKit.MakeButton(c, "Back", "Về điểm lưu", new Vector2(-220, -210), new Vector2(380, 80), () => { RunState.ResetToStageStart(); Reload(); }, ButtonStyle.Primary, 32);
        UIKit.MakeButton(c, "Menu", "Về menu", new Vector2(220, -210), new Vector2(380, 80), () => { RunState.ResetToStageStart(); RogueAudio.Stop(); SceneRouter.Go(menuSceneName); }, ButtonStyle.Secondary, 32);
    }

    void Reload() { Frozen = false; SceneRouter.Go(SceneManager.GetActiveScene().name); }
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
