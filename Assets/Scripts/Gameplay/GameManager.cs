using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

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
    private bool isGameOver;
    private bool isBusy; // true trong lúc popup câu hỏi đang mở, chặn không cho bấm ô khác

    void Awake() => Instance = this;

    void Start()
    {
        if (maze != null && maze.config != null)
        {
            startTimeSeconds = maze.config.timeLimitSeconds;
            pointsPerCorrectAnswer = maze.config.pointsPerCorrectAnswer;
        }

        timeRemaining = startTimeSeconds;
        ReviewSessionData.Clear(); // Reset danh sách câu sai của lượt chơi trước
        if (endGamePanel != null) endGamePanel.SetActive(false);
        UpdateScoreUI();
    }

    void Update()
    {
        if (isGameOver) return;
        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0)
        {
            timeRemaining = 0;
            UpdateTimerUI();
            LoseGame("⏰ Hết giờ!");
            return;
        }
        UpdateTimerUI();
    }

    public void OnCellClicked(GridCellData cell)
    {
        if (isGameOver || isBusy) return;
        player.TryMoveTo(cell);
    }

    public void OnPlayerEnteredQuestionCell(GridCellData cell, CellView view)
    {
        isBusy = true;
        questionPopup.Show(isCorrect =>
        {
            isBusy = false;

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
                    LoseGame("💥 Hết đường đi! Không còn lối tới rương.");
            }
        });
    }

    void WinGame()
    {
        isGameOver = true;
        ShowEndPanel("🎉 Chúc mừng! Bạn đã lấy được rương báu vật!");
    }

    void LoseGame(string reason)
    {
        isGameOver = true;
        ShowEndPanel(reason);
    }

    void ShowEndPanel(string title)
    {
        if (endGamePanel == null) return;
        endGamePanel.SetActive(true);
        if (endGameTitleText != null) endGameTitleText.text = title;
        if (endGameScoreText != null) endGameScoreText.text = $"Điểm: {score}";
    }

    public void OnClickPlayAgain() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    public void OnClickBackToMenu() => SceneManager.LoadScene("MainMenu");
    public void OnClickReviewAI() => SceneManager.LoadScene("AIReviewScene");

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