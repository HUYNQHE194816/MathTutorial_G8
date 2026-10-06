using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI")]
    public TMP_Text scoreText;
    public TMP_Text timerText;

    [Header("End Game UI")]
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
    private TMP_Text fontRef;
    private Transform canvasRoot;
    private GameObject backButtonGo;
    private GameObject confirmPanel;
    private TMP_Text endReasonText;
    private TMP_Text endStatsText;
    private Image endBorder;
    private Button endAgainButton;
    private Button endMenuButton;

    void Awake() => Instance = this;

    void Start()
    {
        if (maze != null && maze.config != null)
        {
            startTimeSeconds = maze.config.timeLimitSeconds;
            pointsPerCorrectAnswer = maze.config.pointsPerCorrectAnswer;
        }

        timeRemaining = startTimeSeconds;

        fontRef = scoreText != null ? scoreText : timerText;
        var cv = QuizUi.RootCanvas(scoreText != null ? (Component)scoreText : this);
        canvasRoot = cv != null ? cv.transform : null;
        BuildBackButton();
        if (endGamePanel == null) BuildEndPanel();

        if (endGamePanel != null) endGamePanel.SetActive(false);
        UpdateScoreUI();
    }

    void Update()
    {
        if (isGameOver || isPaused) return;
        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0)
        {
            timeRemaining = 0;
            UpdateTimerUI();
            LoseGame("Hết giờ rồi! Bạn chưa kịp lấy rương báu vật.");
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
                if (cell.type != CellType.Goal)
                {
                    cell.type = CellType.Rock; // biến thành đá, chặn vĩnh viễn
                    view.Setup(cell);
                }
                player.StepBack();

                if (!maze.HasPathToGoal(player.CurrentPosition))
                    LoseGame("Hết đường đi! Không còn lối nào tới rương báu vật.");
            }
        });
    }

    void WinGame()
    {
        if (isGameOver) return;
        isGameOver = true;
        RecordProgress(true); GameAudio.StopMusic(); GameAudio.Play(Snd.Victory);
        ShowEndPanel("Chúc mừng! Bạn đã lấy được rương báu vật!", true);
    }

    void LoseGame(string reason)
    {
        if (isGameOver) return;
        isGameOver = true;
        RecordProgress(false); GameAudio.StopMusic(); GameAudio.Play(Snd.Defeat);
        ShowEndPanel(reason, false);
    }

    void RecordProgress(bool won)
    {
        ProgressSaveSystem.RecordSession(ProgressSaveSystem.SubjectLy, score, correctCount, wrongCount, won, Time.timeSinceLevelLoad);
    }

    void ShowEndPanel(string reason, bool won)
    {
        if (backButtonGo != null) backButtonGo.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (endGamePanel == null) return;

        endGamePanel.transform.SetAsLastSibling();
        endGamePanel.SetActive(true);

        // Nếu dùng panel tự dựng: tiêu đề + lý do + thống kê. Nếu dùng panel làm tay trong scene: chỉ điền 2 text cũ.
        if (endGameTitleText != null)
            endGameTitleText.text = endReasonText != null ? (won ? "CHIẾN THẮNG!" : "THUA CUỘC!") : reason;
        if (endGameTitleText != null && endReasonText != null)
            endGameTitleText.color = won ? QuizUi.Green : QuizUi.Red;
        if (endReasonText != null) endReasonText.text = reason;
        if (endBorder != null) endBorder.color = won ? QuizUi.Green : QuizUi.Red;

        int secs = Mathf.RoundToInt(startTimeSeconds - timeRemaining);
        string stats = $"Điểm: {score}\nTrả lời đúng: {correctCount}   •   Trả lời sai: {wrongCount}\nThời gian chơi: {secs / 60:00}:{secs % 60:00}";
        if (endStatsText != null) endStatsText.text = stats;
        else if (endGameScoreText != null) endGameScoreText.text = $"Điểm: {score}";
    }

    // ------------------------------------------------------------ UI tự dựng
    void BuildBackButton()
    {
        if (canvasRoot == null || backButtonGo != null) return;

        var btn = QuizUi.MakeButton("BackButton", canvasRoot, fontRef, "◄  QUAY LẠI", QuizUi.Navy, Color.white, 30f, OnClickBack);
        var rt = (RectTransform)btn.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24f, -24f);
        rt.sizeDelta = new Vector2(230f, 70f);
        backButtonGo = btn.gameObject;
        backButtonGo.transform.SetAsLastSibling();
    }

    void OnClickBack()
    {
        if (isGameOver) { OnClickBackToMenu(); return; }
        ShowConfirmExit();
    }

    void ShowConfirmExit()
    {
        if (canvasRoot == null) { OnClickBackToMenu(); return; }
        if (confirmPanel == null)
        {
            var overlay = QuizUi.Overlay("ConfirmExitPanel", canvasRoot, QuizUi.Dim);
            var card = QuizUi.Card(overlay, new Vector2(760f, 380f), QuizUi.Gold);

            var title = QuizUi.Label("Title", card, fontRef, "THOÁT VÁN CHƠI?", 52f, QuizUi.Gold);
            QuizUi.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(700f, 80f));

            var msg = QuizUi.Label("Message", card, fontRef, "Ván này chưa kết thúc nên tiến độ sẽ không được lưu.\nBạn có chắc muốn quay lại không?", 32f, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
            QuizUi.Anchor(msg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(680f, 140f));

            var stay = QuizUi.MakeButton("StayButton", card, fontRef, "CHƠI TIẾP", QuizUi.Green, Color.white, 34f, CancelExit);
            QuizUi.Anchor((RectTransform)stay.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-170f, 65f), new Vector2(300f, 80f));

            var leave = QuizUi.MakeButton("LeaveButton", card, fontRef, "THOÁT", QuizUi.Red, Color.white, 34f, OnClickBackToMenu);
            QuizUi.Anchor((RectTransform)leave.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(170f, 65f), new Vector2(300f, 80f));

            confirmPanel = overlay.gameObject;
        }
        confirmPanel.transform.SetAsLastSibling();
        confirmPanel.SetActive(true);
        isPaused = true;
    }

    void CancelExit()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        isPaused = false;
    }

    void BuildEndPanel()
    {
        if (canvasRoot == null) return;

        var overlay = QuizUi.Overlay("EndGamePanel", canvasRoot, QuizUi.Dim);
        var card = QuizUi.Card(overlay, new Vector2(900f, 560f), QuizUi.Gold);
        endBorder = card.parent.GetComponent<Image>();

        endGameTitleText = QuizUi.Label("Title", card, fontRef, "", 76f, QuizUi.Gold);
        QuizUi.Anchor(endGameTitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(840f, 110f));

        endReasonText = QuizUi.Label("Reason", card, fontRef, "", 34f, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
        QuizUi.Anchor(endReasonText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(820f, 90f));

        endStatsText = QuizUi.Label("Stats", card, fontRef, "", 34f, QuizUi.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        QuizUi.Anchor(endStatsText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(820f, 160f));

        endAgainButton = QuizUi.MakeButton("PlayAgainButton", card, fontRef, "CHƠI LẠI", QuizUi.Green, Color.white, 36f, OnClickPlayAgain);
        QuizUi.Anchor((RectTransform)endAgainButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-200f, 70f), new Vector2(340f, 88f));

        endMenuButton = QuizUi.MakeButton("MenuButton", card, fontRef, "VỀ MENU", QuizUi.Orange, Color.white, 36f, OnClickBackToMenu);
        QuizUi.Anchor((RectTransform)endMenuButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(200f, 70f), new Vector2(340f, 88f));

        endGamePanel = overlay.gameObject;
        endGamePanel.SetActive(false);
    }

    public void OnClickPlayAgain() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    public void OnClickBackToMenu() => SceneManager.LoadScene(SubjectSession.BackSceneOr(SubjectId.Ly, "MainMenu"));

    void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = $"Điểm: {score}";
    }

    void UpdateTimerUI()
    {
        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);
        if (timerText != null) timerText.text = $"{minutes:00}:{seconds:00}";
    }
}