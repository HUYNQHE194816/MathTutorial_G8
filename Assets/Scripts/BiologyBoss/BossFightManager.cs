using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public enum BossState { Menu, Playing, Question, BuffPick, Dying, Won, Lost }

/// <summary>
/// Bộ điều khiển chính. Chỉ cần đặt component này lên 1 GameObject trong scene trống:
/// game tự dựng camera, hầm ngục, hiệp sĩ, rồng và toàn bộ UI khi bấm Play.
/// </summary>
public class BossFightManager : MonoBehaviour
{
    public static BossFightManager I;
    static bool autoStart;   // sau khi bấm "Chơi lại" thì vào thẳng trận

    [Header("Cài đặt trận đấu")]
    public int totalQuestions = 15;
    [Tooltip("Rồng mất bao nhiêu máu thì hiện 1 câu hỏi")] public float hpPerQuestion = 90f;
    public int wrongAnswerDamage = 25;
    [Tooltip("Cứ trả lời xong bấy nhiêu câu thì được chọn 1 trong 3 buff")] public int buffEvery = BuffSystem.PickEvery;
    [Tooltip("Số buff tối đa người chơi được giữ")] public int maxBuffs = BuffSystem.MaxBuffs;
    public string backSceneName = "SubjectSelectScene";
    [Header("Nguồn câu hỏi (ưu tiên: Google Sheet > file CSV > bộ câu hỏi có sẵn trong code)")]
    [Tooltip("Link Google Sheet (chia sẻ 'Bất kỳ ai có đường liên kết' hoặc Xuất bản lên web dạng CSV). Để trống = dùng file CSV.")]
    public string sheetUrl;
    [Tooltip("File .csv trong project (dự phòng khi Sheet trống / tải lỗi). Cột: STT, Câu hỏi, A, B, C, D, Đáp án, Bài")]
    public TextAsset csvFile;
    [Tooltip("Quá số giây này mà chưa tải được Sheet thì chuyển sang file CSV.")]
    public float sheetTimeoutSeconds = 8f;
    [Header("Âm thanh (tuỳ chọn, bỏ trống sẽ dùng âm tổng hợp)")]
    public AudioClip correctClip, wrongClip;

    public BossState State { get; private set; } = BossState.Menu;
    public bool Playing => State == BossState.Playing;
    public PlayerKnight Player; public DragonBoss Boss; public BossFightUI ui;
    public float Level => Mathf.Clamp01(answered / (float)Mathf.Max(1, totalQuestions));

    // Ngân hàng đã tải từ Sheet/CSV được giữ lại giữa các lần "Chơi lại" để khỏi tải lại.
    static List<BioQuestion> loadedBank; static string loadedKey;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { loadedBank = null; loadedKey = null; autoStart = false; }
    int configuredTotal; bool loading; RenderTexture worldRT; float emberT;

    List<BioQuestion> order; int answered, correct, wrong; float threshold, playTime, hitStop;
    bool lockAnswer, lockBuff; int[] map; BuffDef[] offered; readonly List<BuffDef> owned = new List<BuffDef>();   // buff đã chọn: mỗi buff chỉ 1 lần, không cộng dồn

    void Awake()
    {
        I = this; Time.timeScale = 1f; Application.targetFrameRate = 60;
        Sfx.CorrectClip = correctClip; Sfx.WrongClip = wrongClip;
        configuredTotal = totalQuestions;
        string key = (sheetUrl ?? "").Trim() + "|" + (csvFile != null ? csvFile.name : "");
        // Vào từ luồng Chọn chương -> Chọn bài: dùng đúng câu hỏi của bài đã chọn (đã nạp ở màn Chọn bài).
        List<BioQuestion> sessionBank = null;
        if (SubjectSession.TryGetCsv(SubjectId.Sinh, out string sessionCsv)) sessionBank = BioQuestionImporter.ParseCsv(sessionCsv);
        bool session = sessionBank != null && sessionBank.Count > 0;
        bool external = !session && (!string.IsNullOrEmpty((sheetUrl ?? "").Trim()) || csvFile != null);
        bool reuse = external && loadedKey == key && (loadedBank != null || autoStart);
        SetBank(session ? sessionBank : (reuse && loadedBank != null ? loadedBank : BioQuestionBank.Build()));
        BuildWorld();
        Player = PlayerKnight.Create(this, new Vector2(0f, -5f));
        Boss = DragonBoss.Create(this, Player, new Vector2(0f, 3.44f), totalQuestions * hpPerQuestion);
        Boss.Damaged += OnBossDamaged; threshold = Boss.MaxHp - hpPerQuestion;
        ui = gameObject.AddComponent<BossFightUI>();
        ui.Build(StartGame, Choose, ChooseBuff, Retry, Back); ui.SetWorldView(worldRT);
        EnsureEventSystem();
        bool needLoad = external && !reuse;
        string info = session ? SubjectSession.Describe(sessionBank.Count) : (reuse && loadedBank != null ? $"Nguồn: Sheet/CSV • {loadedBank.Count} câu" : "");
        loading = needLoad; ui.SetMenuInfo(totalQuestions, info, needLoad);
        ui.SetBackLabel(session ? "VỀ CHỌN BÀI" : "VỀ CHỌN MÔN");
        if (needLoad) StartCoroutine(LoadBank(key));
        if (autoStart) { autoStart = false; ui.ShowMenu(false); BeginFight(); } else ui.ShowMenu(true);
    }

    /// <summary>Đặt ngân hàng câu hỏi, xáo trộn, và cập nhật số câu / máu rồng nếu rồng đã được tạo.</summary>
    void SetBank(List<BioQuestion> bank)
    {
        order = new List<BioQuestion>(bank); Shuffle(order);
        totalQuestions = Mathf.Min(configuredTotal, order.Count);
        if (Boss != null) { Boss.MaxHp = Boss.Hp = totalQuestions * hpPerQuestion; threshold = Boss.MaxHp - hpPerQuestion; }
    }

    IEnumerator LoadBank(string key)
    {
        List<BioQuestion> loaded = null; string source = "";
        if (!string.IsNullOrWhiteSpace(sheetUrl))
        {
            using (var req = UnityWebRequest.Get(BioQuestionImporter.NormalizeSheetUrl(sheetUrl)))
            {
                req.timeout = Mathf.Max(1, Mathf.CeilToInt(sheetTimeoutSeconds));
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                    Debug.LogWarning("[Sinh] Không tải được Google Sheet: " + req.error + " -> dùng file CSV.");
                else
                {
                    string txt = Encoding.UTF8.GetString(req.downloadHandler.data);
                    if (BioQuestionImporter.LooksLikeHtml(txt))
                        Debug.LogWarning("[Sinh] Google Sheet trả về trang web thay vì CSV (chưa chia sẻ công khai?) -> dùng file CSV.");
                    else { loaded = BioQuestionImporter.ParseCsv(txt); source = "Google Sheet"; }
                }
            }
        }
        if ((loaded == null || loaded.Count == 0) && csvFile != null) { loaded = BioQuestionImporter.ParseCsv(csvFile.text); source = "file CSV"; }

        loading = false;
        loadedKey = key;
        if (loaded == null || loaded.Count == 0)
        {
            Debug.LogWarning("[Sinh] Không có dữ liệu từ Sheet/CSV -> dùng bộ câu hỏi có sẵn.");
            loadedBank = null;
            ui.SetMenuInfo(totalQuestions, "Không đọc được Sheet/CSV — dùng bộ câu hỏi có sẵn", false);
            yield break;
        }
        loadedBank = loaded;
        if (State == BossState.Menu) SetBank(loaded);
        ui.SetMenuInfo(totalQuestions, $"Nguồn: {source} • {loaded.Count} câu (mỗi trận {totalQuestions} câu)", false);
    }

    void OnDestroy() { Time.timeScale = 1f; if (I == this) I = null; if (worldRT != null) { worldRT.Release(); Destroy(worldRT); } }

    static void Shuffle<T>(IList<T> l) { for (int i = l.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (l[i], l[j]) = (l[j], l[i]); } }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
        var m = es.AddComponent<InputSystemUIInputModule>(); m.AssignDefaultActions();
    }

    // ---------- Dựng thế giới ----------
    void BuildWorld()
    {
        var cam = Camera.main;
        if (cam == null) { var cg = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); cg.tag = "MainCamera"; cam = cg.GetComponent<Camera>(); }
        cam.orthographic = true; cam.orthographicSize = 9.375f; cam.transform.position = new Vector3(0, 0, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.05f, .04f, .09f);
        if (cam.GetComponent<CameraRig>() == null) cam.gameObject.AddComponent<CameraRig>();
        // Render pixel-perfect: thế giới vẽ vào RenderTexture 480x300 (1 texel = 1 pixel-art), UI hiển thị lại bằng Point filter.
        worldRT = Gfx.CreateWorldRT(); cam.targetTexture = worldRT;
        var scGo = new GameObject("ScreenCam", typeof(Camera)); var scam = scGo.GetComponent<Camera>();
        scam.clearFlags = CameraClearFlags.SolidColor; scam.backgroundColor = new Color(.02f, .015f, .04f); scam.cullingMask = 0; scam.depth = -100; scam.orthographic = true;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.gameObject.SetActive(false);

        var floor = Spr("Floor", ProcSprites.FloorTile, new Vector2(0, -1.875f), -1000); floor.drawMode = SpriteDrawMode.Tiled; floor.size = new Vector2(46f, 16f);
        var wall = Spr("Wall", ProcSprites.WallTile, new Vector2(0, 8.625f), -990); wall.drawMode = SpriteDrawMode.Tiled; wall.size = new Vector2(46f, 5f);
        Rect("WallEdge", new Vector2(0, 6.05f), new Vector2(200f, 3f), new Color(.05f, .04f, .09f), -985);
        Rect("FrameL", new Vector2(-14.8125f, 0), new Vector2(1.5f, 80f), new Color(.05f, .04f, .09f), -940);
        Rect("FrameR", new Vector2(14.8125f, 0), new Vector2(1.5f, 80f), new Color(.05f, .04f, .09f), -940); Rect("FrameB", new Vector2(0, -9.1875f), new Vector2(128f, 1.5f), new Color(.05f, .04f, .09f), -940);

        foreach (float x in new[] { -11.25f, -3.75f, 3.75f, 11.25f })
        {
            Spr("Torch", ProcSprites.Torch, new Vector2(x, 6.9f), -980);
            var fl = Spr("Flame", ProcSprites.Circle, new Vector2(x, 7.95f), -975); fl.color = new Color(1f, .6f, .2f); fl.transform.localScale = Vector3.one * .13f; Gfx.SetAdd(fl, true); fl.gameObject.AddComponent<FxFlicker>().speed = 12f;
            var gl = Spr("Glow", ProcSprites.Glow, new Vector2(x, 7.4f), -970); gl.color = new Color(1f, .6f, .2f, .5f); gl.transform.localScale = Vector3.one * 2.2f; Gfx.SetAdd(gl, true); gl.gameObject.AddComponent<FxFlicker>();
            var fp = Spr("FloorLight", ProcSprites.Glow, new Vector2(x, 2.2f), -960); fp.color = new Color(1f, .55f, .2f, .16f); fp.transform.localScale = new Vector3(4.2f, 3f, 1f); Gfx.SetAdd(fp, true); fp.gameObject.AddComponent<FxFlicker>().amount = .08f;
        }
    }

    static SpriteRenderer Spr(string n, Sprite s, Vector2 pos, int order)
    {
        var go = new GameObject(n); go.transform.position = pos; var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sortingOrder = order; return sr;
    }
    static void Rect(string n, Vector2 pos, Vector2 scale, Color c, int order)
    { var sr = Spr(n, ProcSprites.Pixel, pos, order); sr.color = c; sr.transform.localScale = new Vector3(scale.x, scale.y, 1f); }

    // ---------- Luồng game ----------
    public void StartGame() { if (State != BossState.Menu || loading) return; ui.ShowMenu(false); BeginFight(); }

    void BeginFight() { State = BossState.Playing; Boss.Begin(); Sfx.Tone(300, .3f, 2, .25f, 400, snd: Snd.FightStart); GameAudio.PlayMusic(Mus.BossFight); }

    void Update()
    {
        if (hitStop > 0f)
        {
            hitStop -= Time.unscaledDeltaTime;
            Time.timeScale = hitStop > 0f ? .03f : ((State == BossState.Question || State == BossState.BuffPick) ? 0f : 1f);
        }
        emberT -= Time.deltaTime;   // tàn lửa bay lơ lửng trong hầm
        if (emberT <= 0f) { emberT = .12f; Fx.Spawn(ProcSprites.Pixel, new Vector2(Random.Range(-14f, 14f), Random.Range(-8f, 4f)), new Color(1f, .55f, .2f, .8f), Random.Range(2f, 3.5f), Random.Range(.6f, 1.1f), .2f, new Vector2(Random.Range(-.4f, .4f), Random.Range(.7f, 1.5f)), 0f, 0f, 0f, 4000); }
        if (State == BossState.Playing) playTime += Time.deltaTime;
        if (State == BossState.Question && !lockAnswer) ReadAnswerKeys();
        if (State == BossState.BuffPick && !lockBuff) ReadBuffKeys();
        ui.SetHud(Boss.Hp / Boss.MaxHp, Player.Hp, Player.MaxHp, Mathf.Min(answered + 1, totalQuestions), totalQuestions, Player.DashReady);
    }

    void ReadAnswerKeys()
    {
        var k = Keyboard.current; if (k == null) return;
        if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) Choose(0);
        else if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) Choose(1);
        else if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) Choose(2);
        else if (k.digit4Key.wasPressedThisFrame || k.numpad4Key.wasPressedThisFrame) Choose(3);
    }

    void ReadBuffKeys()
    {
        var k = Keyboard.current; if (k == null) return;
        if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) ChooseBuff(0);
        else if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) ChooseBuff(1);
        else if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) ChooseBuff(2);
    }

    public void HitStop(float s) { if (State == BossState.Playing || State == BossState.Dying) hitStop = Mathf.Max(hitStop, s); }

    void OnBossDamaged()
    {
        if (State != BossState.Playing || Boss.Hp > threshold) return;
        Boss.Hp = threshold; AskQuestion();
    }

    void AskQuestion()
    {
        State = BossState.Question; hitStop = 0f; Time.timeScale = 0f; lockAnswer = false;
        var q = order[answered]; map = new[] { 0, 1, 2, 3 }; Shuffle(map);
        var shown = new string[4]; for (int i = 0; i < 4; i++) shown[i] = q.answers[map[i]];
        ui.ShowQuestion(answered + 1, totalQuestions, q.text, shown);
        Sfx.Tone(500, .3f, 2, .25f, 300, snd: Snd.QuizOpen);
    }

    void Choose(int i)
    {
        if (State != BossState.Question || lockAnswer) return;
        lockAnswer = true; bool ok = map[i] == 0;
        ui.MarkAnswer(i, System.Array.IndexOf(map, 0), ok);
        if (ok) Sfx.Correct(); else Sfx.Wrong();
        StartCoroutine(AfterAnswer(ok));
    }

    IEnumerator AfterAnswer(bool ok)
    {
        yield return new WaitForSecondsRealtime(1.3f);
        ui.HideQuestion();
        if (ok) { answered++; correct++; threshold -= hpPerQuestion; }
        else { wrong++; RequeueCurrentQuestion(); }   // sai: câu này chưa qua, phải đánh lại phần máu rồng vừa hồi
        Projectile.ClearAll(); Meteor.ClearAll(); Hazards.ClearAll(); Boss.ResetAfterQuestion();
        State = BossState.Playing; Time.timeScale = 1f; Player.Invuln = 0f;

        if (ok) ui.Float(Player.Center + Vector2.up * 2.6f, "CHÍNH XÁC!", new Color(.6f, 1f, .6f), true);
        else
        {
            Boss.PunishWrong(hpPerQuestion);   // rồng hồi lại máu đã mất cho câu này + sát thương tăng (áp dụng ngay cho đòn phạt bên dưới)
            Fx.Boom(Player.Center, 1.6f, Fx.FireCols); Player.Hurt(DragonBoss.Scale(wrongAnswerDamage), true);
            ui.Float(Player.Center + Vector2.up * 2.6f, "LỬA RỒNG!", new Color(1f, .48f, .23f), true);
        }
        if (Player.Alive)
        {
            Player.Invuln = Mathf.Max(Player.Invuln, 1.5f);
            if (ok && answered >= totalQuestions) { State = BossState.Dying; Boss.Die(); }
            else if (ok && answered % Mathf.Max(1, buffEvery) == 0 && owned.Count < maxBuffs && BuffSystem.HasAvailable(owned)) OpenBuffPick();
        }
    }

    /// <summary>Trả lời sai: đưa câu vừa sai xuống sau 2 câu nữa để người chơi gặp câu khác trước, rồi vẫn phải trả lời lại.</summary>
    void RequeueCurrentQuestion()
    {
        if (answered < 0 || answered >= order.Count) return;
        var q = order[answered]; order.RemoveAt(answered);
        order.Insert(Mathf.Min(order.Count, answered + 2), q);
    }

    /// <summary>Dừng game, cho người chơi chọn 1 trong 3 buff khác nhau (chưa sở hữu).</summary>
    void OpenBuffPick()
    {
        offered = BuffSystem.PickChoices(owned);
        if (offered.Length == 0) return;
        State = BossState.BuffPick; hitStop = 0f; Time.timeScale = 0f; lockBuff = false;
        ui.ShowBuffChoice(offered, owned.Count, maxBuffs);
        Sfx.Tone(600, .3f, 2, .25f, 300, snd: Snd.BuffOpen);
    }

    void ChooseBuff(int i)
    {
        if (State != BossState.BuffPick || lockBuff || offered == null || i < 0 || i >= offered.Length) return;
        lockBuff = true; ui.HideBuffChoice();
        State = BossState.Playing; Time.timeScale = 1f; Player.Invuln = Mathf.Max(Player.Invuln, 1.5f);
        ApplyBuff(offered[i]);
    }

    void ApplyBuff(BuffDef b)
    {
        if (owned.Contains(b)) return;   // phòng hờ: không bao giờ nhận 2 lần
        owned.Add(b); b.Apply(Player);
        ui.SetBuffs(owned);
        ui.ShowToast(b.Name, b.Desc, b.Color);
        Fx.Boom(Player.Center, 1f, Fx.BuffCols); Sfx.Play(Snd.BuffPick);
    }

    // ---------- Kết thúc ----------
    public void OnPlayerDied()
    {
        if (State == BossState.Lost || State == BossState.Won) return;
        State = BossState.Lost; hitStop = 0f; Time.timeScale = 1f;
        Fx.Boom(Player.Center, 1.9f, new[] { new Color(.5f, .7f, 1f), Color.white, new Color(.9f, .28f, .3f) });
        Player.gameObject.SetActive(false); Projectile.ClearAll(); Meteor.ClearAll(); Hazards.ClearAll(); Minion.ClearAll();
        StartCoroutine(EndRoutine(false));
    }

    public void OnBossDefeated() { if (State == BossState.Won) return; State = BossState.Won; StartCoroutine(EndRoutine(true)); }

    IEnumerator EndRoutine(bool win)
    {
        Record(win); GameAudio.StopMusic(); Sfx.Play(win ? Snd.Victory : Snd.Defeat);
        if (win && !Sfx.Has(Snd.Victory)) { Sfx.Tone(523, .2f, 2, .3f); yield return new WaitForSeconds(.2f); Sfx.Tone(659, .2f, 2, .3f); yield return new WaitForSeconds(.2f); Sfx.Tone(784, .4f, 2, .3f); }
        else yield return new WaitForSeconds(1.3f);
        string sub = win
            ? $"Bạn đã vượt qua {totalQuestions} câu hỏi Sinh học!\nTrả lời sai {wrong} lần, còn {Mathf.CeilToInt(Player.Hp)} máu."
            : $"Bạn đã vượt qua {answered}/{totalQuestions} câu (trả lời sai {wrong} lần).\nÔn lại bài rồi quay lại đánh bại rồng nhé!";
        ui.ShowEnd(win, sub);
    }

    /// <summary>Lưu kết quả vào hệ thống tiến độ sẵn có của project (hiện trên Dashboard).</summary>
    void Record(bool won)
    {
        try { ProgressSaveSystem.RecordSession(ProgressSaveSystem.SubjectSinh, correct * 10, correct, wrong, won, playTime); }
        catch (System.Exception e) { Debug.LogWarning("[BiologyBoss] Không lưu được tiến độ: " + e.Message); }
    }

    void Retry() { autoStart = true; Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    void Back() { Time.timeScale = 1f; SceneManager.LoadScene(SubjectSession.BackSceneOr(SubjectId.Sinh, backSceneName)); }
}
