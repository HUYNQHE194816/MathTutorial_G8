using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI")]
    public TMP_Text scoreText;
    public TMP_Text timerText;

    [Header("End Game UI")]
    [Tooltip("Để trống = tự dựng màn Thắng/Thua dark fantasy bằng code. Chỉ gán nếu muốn dùng panel làm tay.")]
    public GameObject endGamePanel;
    public TMP_Text endGameTitleText;
    public TMP_Text endGameScoreText;

    [Header("Tham chiếu")]
    public MazeGridGenerator maze;
    public PlayerController player;
    public QuestionPopup questionPopup;

    [Header("Cấu hình")]
    public float startTimeSeconds = 20 * 60f;
    public int pointsPerCorrectAnswer = 10;

    private float timeRemaining;
    private int score;
    private int correctCount;
    private int wrongCount;
    private bool isGameOver;
    private bool isBusy; // true trong lúc popup câu hỏi đang mở, chặn không cho bấm ô khác
    private bool isPaused; // true khi đang hiện hộp xác nhận thoát: dừng đồng hồ, chặn bấm lưới

    // UI tự dựng bằng code (không cần sửa scene)
    private RectTransform ui;            // canvas phủ nằm trên cùng (order 50)
    private GameObject backButtonGo;
    private RectTransform confirmRoot;
    private RectTransform endRoot;
    private Color lastTimerColor = Color.clear;

    // Hiệu ứng: rung khi sai, hạt sáng khi đúng, nảy điểm
    private RectTransform fxLayer;
    private RectTransform shakeTarget;
    private Vector2 shakeHome;
    private Coroutine shakeRoutine;

    // Điều khiển: WASD / phím mũi tên + 4 nút trên màn hình
    private CanvasGroup dpadGroup;
    private Vector2Int heldDir;
    private float repeatTimer;

    void Awake() => Instance = this;

    void Start()
    {
        if (maze != null && maze.config != null)
        {
            startTimeSeconds = maze.config.timeLimitSeconds;
            pointsPerCorrectAnswer = maze.config.pointsPerCorrectAnswer;
        }

        timeRemaining = startTimeSeconds;

        // Phải lấy canvas của scene TRƯỚC khi SceneShell sinh thêm canvas nền.
        var sceneCanvas = scoreText != null ? scoreText.GetComponentInParent<Canvas>() : Object.FindFirstObjectByType<Canvas>();
        if (sceneCanvas != null) sceneCanvas = sceneCanvas.rootCanvas;

        UIKit.EnsureEventSystem();
        LyThemeSkin.Apply(sceneCanvas, scoreText, timerText);
        ui = UIKit.BuildCanvas(transform, "Ly UI", 50);
        fxLayer = (RectTransform)new GameObject("Fx", typeof(RectTransform)).transform;
        fxLayer.SetParent(ui, false);
        UIKit.Stretch(fxLayer, 0f, 0f, 0f, 0f);
        if (sceneCanvas != null)
        {
            shakeTarget = sceneCanvas.transform.Find("RightPanel") as RectTransform;   // rung cả khu vực lưới
            if (shakeTarget != null) shakeHome = shakeTarget.anchoredPosition;
        }
        BuildBackButton();
        BuildDpad();

        ReviewSessionData.Clear(); // Reset danh sách câu sai của lượt chơi trước
        if (endGamePanel != null) endGamePanel.SetActive(false);
        UpdateScoreUI();
    }

    void Update()
    {
        UpdateDpad();
        if (isGameOver || isPaused) return;
        ReadMoveKeys();
        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0)
        {
            timeRemaining = 0;
            UpdateTimerUI();
            LoseGame("Hết giờ rồi! Bạn chưa kịp giải cứu công chúa.");
            return;
        }
        UpdateTimerUI();
    }

    public void OnCellClicked(GridCellData cell)
    {
        if (isGameOver || isBusy || isPaused) return;
        player.TryMoveTo(cell);
    }

    public void OnPlayerEnteredQuestionCell(GridCellData cell, CellView view)
    {
        isBusy = true;
        GameAudio.Play(Snd.QuizOpen); questionPopup.Show(isCorrect =>
        {
            isBusy = false;
            if (isGameOver) return; // hết giờ trong lúc popup còn mở: ván đã kết thúc rồi

            if (isCorrect) correctCount++; else wrongCount++; GameAudio.Play(isCorrect ? Snd.QuizCorrect : Snd.QuizWrong);

            if (isCorrect)
            {
                cell.isVisited = true;
                score += pointsPerCorrectAnswer;
                UpdateScoreUI();
                Burst(view.transform.position, GameTheme.GoldHi, GameTheme.Ember, cell.type == CellType.Goal ? 36 : 14);
                if (scoreText != null) StartCoroutine(Punch((RectTransform)scoreText.transform));

                if (cell.type == CellType.Goal)
                {
                    view.MarkVisited();
                    WinGame();
                }
                else
                {
                    // Ô câu hỏi trả lời đúng -> biến thành đất, nhân vật đi tiếp bình thường
                    cell.type = CellType.Dirt;
                    view.Setup(cell);
                }
            }
            else
            {
                Burst(view.transform.position, GameTheme.Blood, GameTheme.EmberDeep, 10);
                if (cell.type != CellType.Goal)
                {
                    cell.type = CellType.Rock; // biến thành đá, chặn vĩnh viễn (logic chặn có hiệu lực ngay)
                    StartCoroutine(RockFallRoutine(view, cell));   // hình ảnh: tảng đá rơi từ trên xuống ô đó
                }
                else Shake();
                player.StepBack();   // hiệp sĩ nhảy lùi ra để né

                if (!maze.HasPathToGoal(player.CurrentPosition))
                    LoseGame("Hết đường đi! Không còn lối nào tới công chúa.", 1.1f);
            }
        });
    }

    void WinGame()
    {
        if (isGameOver) return;
        isGameOver = true;
        RecordProgress(true); GameAudio.StopMusic(); GameAudio.Play(Snd.Victory);
        if (backButtonGo != null) backButtonGo.SetActive(false);
        StartCoroutine(WinSequence());
    }

    void LoseGame(string reason, float delay = 0f)
    {
        if (isGameOver) return;
        isGameOver = true;
        RecordProgress(false); GameAudio.StopMusic(); GameAudio.Play(Snd.Defeat);
        if (delay > 0f) StartCoroutine(ShowEndPanelAfter(reason, false, delay));   // chờ tảng đá rơi xong
        else ShowEndPanel(reason, false);
    }

    IEnumerator ShowEndPanelAfter(string reason, bool won, float delay)
    {
        yield return Wait(delay);
        ShowEndPanel(reason, won);
    }

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    void RecordProgress(bool won)
    {
        ProgressSaveSystem.RecordSession(ProgressSaveSystem.SubjectLy, score, correctCount, wrongCount, won, Time.timeSinceLevelLoad);
    }

    // ------------------------------------------------------------ đá rơi
    /// <summary>Tảng đá rơi từ trên xuống ô trả lời sai: có bóng đổ lớn dần, chạm đất thì rung + bụi.</summary>
    IEnumerator RockFallRoutine(CellView view, GridCellData cell)
    {
        view.ShowFloorOnly(cell);
        if (fxLayer == null || ui == null) { view.LandRock(); yield break; }
        float sc = ui.lossyScale.x;
        Vector3 land = view.transform.position;

        var shadow = UIKit.CBox("shadow", fxLayer, Color.white, Vector2.zero, new Vector2(120f, 64f));
        var shImg = shadow.GetComponent<Image>();
        shImg.sprite = ThemeGfx.Glow; shImg.color = new Color(0f, 0f, 0f, 0f);
        shadow.position = land + Vector3.down * 18f * sc;

        var rock = UIKit.CBox("rock", fxLayer, Color.white, Vector2.zero, new Vector2(104f, 104f));
        var rkImg = rock.GetComponent<Image>();
        rkImg.sprite = LyGfx.Boulder; rkImg.preserveAspect = true;
        rock.gameObject.SetActive(false);

        yield return Wait(.15f);                       // để hiệp sĩ kịp nhảy lùi ra
        rock.gameObject.SetActive(true);

        const float dur = .5f;
        float startH = 700f * sc;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur, e = k * k;              // rơi nhanh dần
            rock.position = land + Vector3.up * ((1f - e) * startH);
            rock.localEulerAngles = new Vector3(0f, 0f, (1f - k) * 40f);
            shImg.color = new Color(0f, 0f, 0f, .55f * k);
            shadow.localScale = Vector3.one * Mathf.Lerp(.35f, 1f, k);
            yield return null;
        }
        Destroy(rock.gameObject);
        Destroy(shadow.gameObject);
        view.LandRock();
        Shake();
        Burst(land + Vector3.down * 24f * sc, GameTheme.IronHi, GameTheme.Fog, 16);   // bụi đá
    }

    // ------------------------------------------------------------ ăn mừng chiến thắng
    /// <summary>Hiệp sĩ và công chúa cùng nhảy cẫng, tim bay, mưa vàng ~3.5s rồi mới hiện bảng kết quả.</summary>
    IEnumerator WinSequence()
    {
        var goalView = maze.GetCellView(maze.goal.x, maze.goal.y);
        player.Celebrate(new Vector2(-40f, 0f));       // hiệp sĩ đứng cạnh công chúa
        if (goalView != null) goalView.Celebrate();
        StartCoroutine(GoldRain());

        float sc = ui != null ? ui.lossyScale.x : 1f;
        Vector3 center = goalView != null ? goalView.transform.position : new Vector3(Screen.width * .5f, Screen.height * .5f, 0f);
        Color pinkA = new Color(1f, .45f, .62f), pinkB = new Color(1f, .82f, .88f);
        for (int i = 0; i < 6; i++)
        {
            Burst(center + Vector3.up * 40f * sc, GameTheme.GoldHi, GameTheme.Ember, 10);
            Burst(center + Vector3.up * 70f * sc, pinkA, pinkB, 5, LyGfx.Heart, -90f, .45f);   // tim bay lên
            yield return Wait(.5f);
        }
        yield return Wait(.5f);
        ShowEndPanel("Chúc mừng! Bạn đã giải cứu được công chúa!", true);
    }

    // ------------------------------------------------------------ điều khiển: WASD / mũi tên / nút trên màn hình
    /// <summary>Đi 1 ô theo hướng dir (x = đổi hàng, y = đổi cột). Dùng chung luật với bấm chuột vào ô.</summary>
    void Step(Vector2Int dir)
    {
        if (player == null || maze == null) return;
        var p = player.CurrentPosition;
        int r = p.x + dir.x, c = p.y + dir.y;
        if (r < 0 || c < 0 || r >= maze.Rows || c >= maze.Columns) return;
        OnCellClicked(maze.GetCell(r, c));
    }

    static Vector2Int DirFrom(Keyboard k, bool pressedThisFrame)
    {
        bool up = pressedThisFrame ? (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) : (k.wKey.isPressed || k.upArrowKey.isPressed);
        bool down = pressedThisFrame ? (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame) : (k.sKey.isPressed || k.downArrowKey.isPressed);
        bool left = pressedThisFrame ? (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) : (k.aKey.isPressed || k.leftArrowKey.isPressed);
        bool right = pressedThisFrame ? (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) : (k.dKey.isPressed || k.rightArrowKey.isPressed);
        if (up) return new Vector2Int(-1, 0);
        if (down) return new Vector2Int(1, 0);
        if (left) return new Vector2Int(0, -1);
        if (right) return new Vector2Int(0, 1);
        return Vector2Int.zero;
    }

    void ReadMoveKeys()
    {
        var k = Keyboard.current;
        if (k == null || isBusy) return;

        Vector2Int pressed = DirFrom(k, true);
        if (pressed != Vector2Int.zero) { heldDir = pressed; repeatTimer = .3f; Step(pressed); return; }

        Vector2Int held = DirFrom(k, false);
        if (held == Vector2Int.zero || held != heldDir) { heldDir = held; repeatTimer = .3f; return; }

        repeatTimer -= Time.deltaTime;                 // giữ phím: đi liên tục
        if (repeatTimer <= 0f) { repeatTimer = .18f; Step(held); }
    }

    void BuildDpad()
    {
        if (ui == null) return;
        var root = (RectTransform)new GameObject("Dpad", typeof(RectTransform), typeof(CanvasGroup)).transform;
        root.SetParent(ui, false);
        UIKit.Stretch(root, 0f, 0f, 0f, 0f);
        dpadGroup = root.GetComponent<CanvasGroup>();

        // bố cục chữ T ngược giống phím WASD, đặt giữa nửa màn hình bên trái, sát đáy
        MakeDpadButton(root, "Up", new Vector2(0f, 162f), 0f, new Vector2Int(-1, 0));
        MakeDpadButton(root, "Left", new Vector2(-100f, 62f), 90f, new Vector2Int(0, -1));
        MakeDpadButton(root, "Down", new Vector2(0f, 62f), 180f, new Vector2Int(1, 0));
        MakeDpadButton(root, "Right", new Vector2(100f, 62f), -90f, new Vector2Int(0, 1));
    }

    void MakeDpadButton(RectTransform parent, string name, Vector2 pos, float rotZ, Vector2Int dir)
    {
        var btn = UIKit.MakeButton(parent, "Dpad_" + name, "", Vector2.zero, new Vector2(90f, 90f), () => Step(dir), ButtonStyle.Stone, 24, .2f);
        UIKit.Place((RectTransform)btn.transform, new Vector2(.25f, 0f), new Vector2(.25f, 0f), UIKit.C, pos, new Vector2(90f, 90f));
        var arrow = UIKit.CBox("Arrow", btn.transform, Color.white, Vector2.zero, new Vector2(46f, 46f));
        arrow.GetComponent<Image>().sprite = LyGfx.Arrow;
        arrow.localEulerAngles = new Vector3(0f, 0f, rotZ);
    }

    /// <summary>Ẩn 4 nút điều khiển khi đang trả lời câu hỏi / tạm dừng / hết ván.</summary>
    void UpdateDpad()
    {
        if (dpadGroup == null) return;
        bool show = !isBusy && !isPaused && !isGameOver;
        dpadGroup.alpha = Mathf.MoveTowards(dpadGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        dpadGroup.blocksRaycasts = show;
    }

    // ------------------------------------------------------------ hiệu ứng
    /// <summary>Rung khu vực lưới ~0.3s, giảm dần (trả lời sai).</summary>
    void Shake()
    {
        if (shakeTarget == null) return;
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    IEnumerator ShakeRoutine()
    {
        const float dur = .32f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / dur;
            shakeTarget.anchoredPosition = shakeHome + new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * 14f * k;
            yield return null;
        }
        shakeTarget.anchoredPosition = shakeHome;
        shakeRoutine = null;
    }

    /// <summary>Nảy chữ điểm lên một nhịp (trả lời đúng).</summary>
    IEnumerator Punch(RectTransform rt)
    {
        const float dur = .25f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            if (rt == null) yield break;
            rt.localScale = Vector3.one * (1f + .3f * Mathf.Sin(t / dur * Mathf.PI));
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    class Drop { public RectTransform rt; public Image img; public Vector2 v; public float age, life, phase; }

    /// <summary>Mưa vàng rơi từ trên xuống ~2.6s khi chiến thắng.</summary>
    IEnumerator GoldRain()
    {
        if (fxLayer == null || ui == null) yield break;
        float sc = ui.lossyScale.x;
        var drops = new System.Collections.Generic.List<Drop>();
        for (float t = 0f; t < 2.6f || drops.Count > 0; )
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            if (t < 2.6f)
                for (int n = 0; n < 2; n++)
                {
                    float size = Random.Range(10f, 22f);
                    var rt = UIKit.CBox("d", fxLayer, Color.white, Vector2.zero, new Vector2(size, size));
                    var img = rt.GetComponent<Image>();
                    img.sprite = ThemeGfx.Glow;
                    img.color = Color.Lerp(GameTheme.GoldHi, Random.value < .25f ? GameTheme.Bone : GameTheme.Ember, Random.value);
                    rt.position = new Vector3(Random.Range(0f, Screen.width), Screen.height + 20f, 0f);
                    drops.Add(new Drop { rt = rt, img = img, v = new Vector2(Random.Range(-40f, 40f), -Random.Range(220f, 460f)) * sc,
                                         life = Random.Range(1.6f, 2.4f), phase = Random.value * 6.28f });
                }
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var d = drops[i];
                d.age += dt;
                if (d.rt == null || d.age >= d.life) { if (d.rt != null) Destroy(d.rt.gameObject); drops.RemoveAt(i); continue; }
                d.rt.position += (Vector3)(d.v * dt) + new Vector3(Mathf.Sin(d.age * 5f + d.phase) * 30f * sc * dt, 0f, 0f);
                var c = d.img.color; c.a = Mathf.Clamp01((d.life - d.age) / (d.life * .35f)); d.img.color = c;
            }
            yield return null;
        }
    }

    /// <summary>Phun hạt sáng tại vị trí màn hình pos (toạ độ thế giới của ô lưới).</summary>
    void Burst(Vector3 pos, Color a, Color b, int count, Sprite spr = null, float gravity = 520f, float speed = 1f)
    {
        if (fxLayer != null) StartCoroutine(BurstRoutine(pos, a, b, count, spr, gravity, speed));
    }

    IEnumerator BurstRoutine(Vector3 pos, Color a, Color b, int n, Sprite spr, float gravity, float speed)
    {
        float sc = ui != null ? ui.lossyScale.x : 1f;   // đổi đơn vị canvas -> pixel
        var rts = new RectTransform[n];
        var imgs = new Image[n];
        var vel = new Vector2[n];
        var life = new float[n];
        float maxLife = 0f;

        for (int i = 0; i < n; i++)
        {
            float size = spr != null ? Random.Range(24f, 40f) : Random.Range(14f, 30f);
            rts[i] = UIKit.CBox("p", fxLayer, Color.white, Vector2.zero, new Vector2(size, size));
            imgs[i] = rts[i].GetComponent<Image>();
            imgs[i].sprite = spr != null ? spr : ThemeGfx.Glow;
            imgs[i].color = Color.Lerp(a, b, Random.value);
            rts[i].position = pos;
            float ang = gravity < 0f ? Random.Range(.5f, 2.64f) : Random.value * 6.2832f;   // trọng lực âm = bay lên (trái tim)
            float sp = Random.Range(120f, 420f) * sc * speed;
            vel[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp;
            life[i] = Random.Range(.45f, .85f);
            if (life[i] > maxLife) maxLife = life[i];
        }

        for (float t = 0f; t < maxLife; )
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            for (int i = 0; i < n; i++)
            {
                if (rts[i] == null) continue;
                float k = t / life[i];
                if (k >= 1f) { Destroy(rts[i].gameObject); rts[i] = null; continue; }
                vel[i].y -= gravity * sc * dt;                      // trọng lực nhẹ
                rts[i].position += (Vector3)(vel[i] * dt);
                var c = imgs[i].color; c.a = 1f - k; imgs[i].color = c;
                rts[i].localScale = Vector3.one * (1f - .5f * k);
            }
            yield return null;
        }
        for (int i = 0; i < n; i++) if (rts[i] != null) Destroy(rts[i].gameObject);
    }

    // ------------------------------------------------------------ UI dark fantasy (UIKit)
    /// <summary>Lớp phủ tối toàn màn hình, chặn bấm xuyên xuống lưới.</summary>
    RectTransform Dim(string name, float alpha)
    {
        var r = UIKit.Box(name, ui, GameTheme.Ink.WithAlpha(alpha), Vector2.zero, Vector2.one, UIKit.C, Vector2.zero, Vector2.zero, true);
        UIKit.Stretch(r, 0f, 0f, 0f, 0f);
        r.SetAsLastSibling();
        return r;
    }

    IEnumerator PopIn(RectTransform card)
    {
        if (card == null) yield break;
        var cg = card.GetComponent<CanvasGroup>();
        if (cg == null) cg = card.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f; card.localScale = Vector3.one * .92f;
        const float dur = .22f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            if (card == null) yield break;
            float k = Mathf.Clamp01(t / dur); k = 1f - (1f - k) * (1f - k);
            cg.alpha = k; card.localScale = Vector3.one * Mathf.Lerp(.92f, 1f, k);
            yield return null;
        }
        if (card == null) yield break;
        cg.alpha = 1f; card.localScale = Vector3.one;
    }

    void BuildBackButton()
    {
        if (ui == null || backButtonGo != null) return;
        var btn = UIKit.MakeButton(ui, "BackButton", "QUAY LẠI", Vector2.zero, new Vector2(230f, 64f), OnClickBack, ButtonStyle.Stone, 28, .2f);
        UIKit.Place((RectTransform)btn.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), UIKit.C,
            new Vector2(24f + 115f, -24f - 32f), new Vector2(230f, 64f));
        backButtonGo = btn.gameObject;
    }

    void OnClickBack()
    {
        if (isGameOver) { OnClickBackToMenu(); return; }
        ShowConfirmExit();
    }

    void ShowConfirmExit()
    {
        if (ui == null) { OnClickBackToMenu(); return; }
        if (confirmRoot != null) return;

        confirmRoot = Dim("ConfirmExit", .78f);
        var frame = UIKit.Panel(confirmRoot, "Card", Vector2.zero, new Vector2(780f, 420f), PanelStyle.Dark, out var c);

        UIKit.Label("Title", c, "THOÁT VÁN CHƠI?", 54, GameTheme.EmberHi, TextAnchor.MiddleCenter,
            new Vector2(0f, 125f), new Vector2(660f, 80f), true, true, FontStyle.Bold);
        UIKit.Divider(c, new Vector2(0f, 68f), 420f);
        UIKit.Label("Msg", c, "Ván này chưa kết thúc nên tiến độ sẽ không được lưu.\nBạn có chắc muốn quay lại không?", 30,
            GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0f, -5f), new Vector2(660f, 110f));
        UIKit.MakeButton(c, "Stay", "CHƠI TIẾP", new Vector2(-160f, -118f), new Vector2(290f, 74f), CancelExit, ButtonStyle.Bronze, 32, .1f);
        UIKit.MakeButton(c, "Leave", "THOÁT", new Vector2(160f, -118f), new Vector2(290f, 74f), OnClickBackToMenu, ButtonStyle.Danger, 32, .15f);

        StartCoroutine(PopIn(frame));
        isPaused = true;
    }

    void CancelExit()
    {
        if (confirmRoot != null) { Destroy(confirmRoot.gameObject); confirmRoot = null; }
        isPaused = false;
    }

    void ShowEndPanel(string reason, bool won)
    {
        if (backButtonGo != null) backButtonGo.SetActive(false);
        if (confirmRoot != null) { Destroy(confirmRoot.gameObject); confirmRoot = null; }
        if (timerText != null) timerText.rectTransform.localScale = Vector3.one;

        // Panel làm tay trong scene (hiếm khi dùng): chỉ điền 2 text cũ.
        if (endGamePanel != null)
        {
            endGamePanel.transform.SetAsLastSibling();
            endGamePanel.SetActive(true);
            if (endGameTitleText != null) endGameTitleText.text = reason;
            if (endGameScoreText != null) endGameScoreText.text = $"Điểm: {score}";
            return;
        }
        if (ui == null || endRoot != null) return;

        int secs = Mathf.RoundToInt(startTimeSeconds - timeRemaining);
        bool hasWrong = ReviewSessionData.wrongQuestions.Count > 0;
        string stats = $"Điểm: {score}\nTrả lời đúng: {correctCount}   •   Trả lời sai: {wrongCount}\nThời gian chơi: {secs / 60:00}:{secs % 60:00}";

        endRoot = Dim("EndGame", .82f);
        var frame = UIKit.Panel(endRoot, "Card", Vector2.zero, new Vector2(1000f, 640f), PanelStyle.Dark, out var c);
        frame.GetComponent<Image>().color = won ? GameTheme.Gold : Color.Lerp(GameTheme.Blood, GameTheme.Ink, .35f);

        Color titleColor = won ? GameTheme.GoldHi : Color.Lerp(GameTheme.Blood, Color.white, .2f);
        UIKit.Label("Title", c, won ? "CHIẾN THẮNG!" : "THUA CUỘC!", 78, titleColor, TextAnchor.MiddleCenter,
            new Vector2(0f, 215f), new Vector2(900f, 100f), true, true, FontStyle.Bold);
        UIKit.Divider(c, new Vector2(0f, 150f), 560f);
        UIKit.Label("Reason", c, reason, 32, GameTheme.Bone, TextAnchor.MiddleCenter,
            new Vector2(0f, 90f), new Vector2(860f, 90f));
        UIKit.Label("Stats", c, stats, 34, GameTheme.Rune, TextAnchor.MiddleCenter,
            new Vector2(0f, -35f), new Vector2(860f, 150f), false, false, FontStyle.Bold);

        // 3 nút (hoặc 2 nếu không có câu sai để AI giải thích), căn giữa
        var size = new Vector2(270f, 84f);
        float[] xs = hasWrong ? new[] { -300f, 0f, 300f } : new[] { -160f, 160f };
        int i = 0;
        UIKit.MakeButton(c, "PlayAgain", "CHƠI LẠI", new Vector2(xs[i++], -215f), size, OnClickPlayAgain, ButtonStyle.Bronze, 30, .25f);
        if (hasWrong)
            UIKit.MakeButton(c, "ReviewAI", "AI GIẢI THÍCH", new Vector2(xs[i++], -215f), size, OnClickReviewAI, ButtonStyle.Primary, 30, .35f);
        UIKit.MakeButton(c, "Menu", "VỀ MENU", new Vector2(xs[i], -215f), size, OnClickBackToMenu, ButtonStyle.Stone, 30, .45f);

        StartCoroutine(PopIn(frame));
        if (fxLayer != null) fxLayer.SetAsLastSibling();   // hạt sáng chiến thắng nằm trên lớp mờ
    }

    public void OnClickPlayAgain() => SceneRouter.Go(SceneManager.GetActiveScene().name);
    public void OnClickBackToMenu() => SceneRouter.Go(SubjectSession.BackSceneOr(SubjectId.Ly, "MainMenu"));
    public void OnClickReviewAI() => SceneRouter.Go("ProfessorReviewScene");

    void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = $"Điểm: {score}";
    }

    void UpdateTimerUI()
    {
        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);
        if (timerText == null) return;
        timerText.text = $"{minutes:00}:{seconds:00}";

        // Còn dưới 1 phút: đồng hồ đổi sang đỏ và đập một nhịp mỗi giây
        Color want = timeRemaining <= 60f ? LyThemeSkin.TimerDanger : LyThemeSkin.TimerNormal;
        if (want != lastTimerColor) { timerText.color = want; lastTimerColor = want; }

        if (timeRemaining <= 60f)
        {
            float since = 1f - (timeRemaining % 1f);   // giây kể từ lần đổi số gần nhất
            timerText.rectTransform.localScale = Vector3.one * (1f + .16f * Mathf.Exp(-since * 7f));
        }
        else timerText.rectTransform.localScale = Vector3.one;
    }
}
