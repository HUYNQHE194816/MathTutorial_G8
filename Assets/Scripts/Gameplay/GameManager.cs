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
    public TMP_Text playerNameText;

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
        if (endGamePanel != null) endGamePanel.SetActive(false);
        UpdateScoreUI();
        if (playerNameText != null && !string.IsNullOrEmpty(StudentProfileData.studentName))
            playerNameText.text = StudentProfileData.studentName;
    }

    void Update()
    {
        if (isGameOver) return;
        if (questionPopup != null && !questionPopup.IsReady) return; // đang tải câu hỏi: chưa tính giờ
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
        if (questionPopup != null && !questionPopup.IsReady) return;
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
        }, cell.type == CellType.Goal);
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
        if (endGamePanel == null) BuildEndPanel();
        if (endGamePanel == null) return;
        endGamePanel.SetActive(true);
        if (endGameTitleText != null) endGameTitleText.text = title;
        if (endGameScoreText != null) endGameScoreText.text = $"Điểm: {score}";
    }

    // Scene chưa có bảng kết thúc -> tự dựng bằng code để trận đấu không kết thúc trong im lặng.
    void BuildEndPanel()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null || scoreText == null) return;

        var root = NewRect("EndGamePanel", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero);
        root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0.1f, 0.75f);
        endGamePanel = root.gameObject;

        var card = NewRect("Card", root, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(760, 520));
        card.gameObject.AddComponent<Image>().color = new Color(1f, .97f, .88f);
        endGameTitleText = NewText("Title", card, 44, new Vector2(700, 200), new Vector2(0, 110), new Color(.13f, .2f, .45f));
        endGameScoreText = NewText("Score", card, 40, new Vector2(700, 70), new Vector2(0, -40), new Color(.2f, .6f, .33f));
        NewButton(card, "CHƠI LẠI", new Vector2(-180, -180), new Color(.3f, .78f, .45f), OnClickPlayAgain);
        NewButton(card, "VỀ MENU", new Vector2(180, -180), new Color(.25f, .55f, 1f), OnClickBackToMenu);
    }

    RectTransform NewRect(string n, Transform p, Vector2 min, Vector2 max, Vector2 size)
    {
        var r = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(p, false);
        r.anchorMin = min; r.anchorMax = max; r.pivot = new Vector2(.5f, .5f);
        r.sizeDelta = size; r.anchoredPosition = Vector2.zero;
        return r;
    }

    TMP_Text NewText(string n, Transform p, float size, Vector2 box, Vector2 pos, Color c)
    {
        var r = NewRect(n, p, new Vector2(.5f, .5f), new Vector2(.5f, .5f), box);
        r.anchoredPosition = pos;
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = scoreText.font; t.fontSize = size; t.color = c; t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        return t;
    }

    void NewButton(Transform p, string label, Vector2 pos, Color c, UnityEngine.Events.UnityAction onClick)
    {
        var r = NewRect(label, p, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(300, 90));
        r.anchoredPosition = pos;
        var img = r.gameObject.AddComponent<Image>(); img.color = c;
        r.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
        NewText("Label", r, 34, new Vector2(300, 90), Vector2.zero, Color.white);
        r.GetComponentInChildren<TMP_Text>().text = label;
    }

    public void OnClickPlayAgain() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    public void OnClickBackToMenu() => SceneManager.LoadScene("MainMenu");

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