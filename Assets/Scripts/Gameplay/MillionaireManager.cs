using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MillionaireManager : MonoBehaviour
{
    [System.Serializable]
    public class Question
    {
        [TextArea] public string text;
        public string[] options = new string[4];
        [Range(0, 3)] public int correctIndex;
    }

    [Header("Câu hỏi (xếp từ dễ -> khó, tối đa 15)")]
    public List<Question> questions = new List<Question>();

    [Header("UI câu hỏi")]
    public TMP_Text questionText;
    public Button[] answerButtons;      // 4 nút A B C D
    public TMP_Text[] answerLabels;     // 4 label tương ứng (vd "A: 96")

    [Header("UI thang tiền")]
    public Transform ladderContainer;   // có VerticalLayoutGroup
    public TMP_Text ladderItemPrefab;   // 1 TMP_Text làm mẫu

    [Header("Quyền trợ giúp")]
    public Button fiftyFiftyButton;

    [Header("Màn kết thúc")]
    public GameObject endPanel;
    public TMP_Text endText;
    public string menuSceneName = "MainMenu";

    [Header("Màu")]
    public Color normalColor   = new Color32(11, 26, 107, 255);
    public Color selectedColor = new Color32(242, 140, 30, 255);
    public Color correctColor  = new Color32(46, 173, 75, 255);
    public Color wrongColor    = new Color32(217, 58, 58, 255);
    public Color ladderNormal  = Color.white;
    public Color ladderCurrent = new Color32(242, 140, 30, 255);
    public Color ladderSafe    = new Color32(255, 214, 90, 255);

    // Thang tiền chương trình Ai là triệu phú VN, mốc an toàn: câu 5, 10, 15
    static readonly string[] Prizes =
    {
        "200.000", "400.000", "600.000", "1.000.000", "2.000.000",
        "3.000.000", "6.000.000", "10.000.000", "14.000.000", "22.000.000",
        "30.000.000", "40.000.000", "60.000.000", "85.000.000", "150.000.000"
    };
    static readonly int[] Milestones = { 4, 9, 14 }; // index 0-based

    int current;            // index câu đang chơi
    bool locked;            // đang chờ hiệu ứng, không cho bấm
    TMP_Text[] ladder;

    void Start()
    {
        endPanel.SetActive(false);
        BuildLadder();
        fiftyFiftyButton.onClick.AddListener(UseFiftyFifty);
        for (int i = 0; i < answerButtons.Length; i++)
        {
            int idx = i;
            answerButtons[i].onClick.AddListener(() => OnAnswerClicked(idx));
        }
        ShowQuestion();
    }

    void BuildLadder()
    {
        int n = Mathf.Min(questions.Count, Prizes.Length);
        ladder = new TMP_Text[n];
        // Tạo từ câu cao nhất xuống thấp nhất để câu 15 nằm trên cùng
        for (int i = n - 1; i >= 0; i--)
        {
            var item = Instantiate(ladderItemPrefab, ladderContainer);
            item.gameObject.SetActive(true);
            item.text = $"{i + 1,2}   ◆   {Prizes[i]}";
            ladder[i] = item;
        }
        RefreshLadder();
    }

    void RefreshLadder()
    {
        for (int i = 0; i < ladder.Length; i++)
        {
            bool safe = System.Array.IndexOf(Milestones, i) >= 0;
            ladder[i].color = i == current ? ladderCurrent : (safe ? ladderSafe : ladderNormal);
        }
    }

    void ShowQuestion()
    {
        locked = false;
        var q = questions[current];
        questionText.text = q.text;
        string[] letters = { "A", "B", "C", "D" };

        for (int i = 0; i < 4; i++)
        {
            answerButtons[i].interactable = true;
            answerButtons[i].image.color = normalColor;
            answerLabels[i].text = $"{letters[i]}:  {q.options[i]}";
        }
        RefreshLadder();
    }

    void OnAnswerClicked(int idx)
    {
        if (locked) return;
        locked = true;
        StartCoroutine(ResolveRoutine(idx));
    }

    IEnumerator ResolveRoutine(int chosen)
    {
        var q = questions[current];
        answerButtons[chosen].image.color = selectedColor; // "Đáp án cuối cùng?"
        yield return new WaitForSeconds(1.5f);

        answerButtons[q.correctIndex].image.color = correctColor;
        if (chosen != q.correctIndex)
            answerButtons[chosen].image.color = wrongColor;
        yield return new WaitForSeconds(1.5f);

        if (chosen != q.correctIndex)
        {
            EndGame($"Rất tiếc! Đáp án đúng là {"ABCD"[q.correctIndex]}.\nBạn ra về với {SafePrize()} đồng.");
            yield break;
        }

        current++;
        if (current >= questions.Count || current >= Prizes.Length)
            EndGame($"CHÚC MỪNG! Bạn đã chiến thắng với {Prizes[current - 1]} đồng!");
        else
            ShowQuestion();
    }

    string SafePrize()
    {
        // Mốc an toàn cao nhất đã vượt qua (current là câu vừa sai, chưa qua)
        for (int i = Milestones.Length - 1; i >= 0; i--)
            if (current > Milestones[i]) return Prizes[Milestones[i]];
        return "0";
    }

    void UseFiftyFifty()
    {
        if (locked) return;
        fiftyFiftyButton.interactable = false;

        var q = questions[current];
        var wrong = new List<int>();
        for (int i = 0; i < 4; i++) if (i != q.correctIndex) wrong.Add(i);

        // Loại ngẫu nhiên 2 đáp án sai
        for (int k = 0; k < 2; k++)
        {
            int pick = Random.Range(0, wrong.Count);
            int idx = wrong[pick];
            wrong.RemoveAt(pick);
            answerButtons[idx].interactable = false;
            answerLabels[idx].text = "";
        }
    }

    void EndGame(string message)
    {
        endPanel.SetActive(true);
        endText.text = message;
    }

    // Gắn vào nút "Về menu" trên EndPanel
    public void OnClickBackToMenu() => SceneManager.LoadScene(menuSceneName);
}
