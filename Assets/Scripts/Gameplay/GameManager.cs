using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI")]
    public TMP_Text scoreText;
    public TMP_Text timerText;

    [Header("Tham chiếu")]
    public MazeGridGenerator maze;
    public PlayerController player;
    public QuestionPopup questionPopup;

    [Header("Cấu hình")]
    public float startTimeSeconds = 20 * 60f; // 20:00, khớp đồng hồ trong ảnh mẫu
    public int pointsPerCorrectAnswer = 10;

    private float timeRemaining;
    private int score;
    private bool isGameOver;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        timeRemaining = startTimeSeconds;
        UpdateScoreUI();
    }

    void Update()
    {
        if (isGameOver) return;

        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0)
        {
            timeRemaining = 0;
            EndGame();
        }
        UpdateTimerUI();
    }

    public void OnCellClicked(GridCellData cell)
    {
        if (isGameOver) return;
        player.TryMoveTo(cell);
    }

    public void OnPlayerEnteredQuestionCell(GridCellData cell, CellView view)
    {
        questionPopup.Show(isCorrect =>
        {
            if (isCorrect)
            {
                view.MarkVisited();
                score += pointsPerCorrectAnswer;
                UpdateScoreUI();
                CheckWin(cell);
            }
            else
            {
                player.StepBack();
            }
        });
    }

    void CheckWin(GridCellData cell)
    {
        if (cell.row == maze.Rows - 1 && cell.col == maze.Columns - 1)
            EndGame();
    }

    void EndGame()
    {
        isGameOver = true;
        // TODO: hiện panel kết thúc với điểm số cuối (score)
    }

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
