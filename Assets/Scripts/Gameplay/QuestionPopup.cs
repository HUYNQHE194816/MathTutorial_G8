using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestionPopup : MonoBehaviour
{
    [Header("UI bắt buộc")]
    public GameObject panel;
    public TMP_Text questionText;
    public Button[] answerButtons;
    public TMP_Text[] answerLabels;

    [Header("UI tuỳ chọn - đếm ngược")]
    [Tooltip("Nếu để trống, thời gian còn lại sẽ được ghép vào questionText.")]
    public TMP_Text timerText;

    [Header("Cấu hình")]
    [Tooltip("Số giây để trả lời mỗi câu hỏi")]
    public float secondsPerQuestion = 15f;

    private struct QuestionData
    {
        public string question;
        public string[] options;
        public int correctIndex;
        public QuestionData(string q, string[] o, int c) { question = q; options = o; correctIndex = c; }
    }

    // TODO: thay bằng ngân hàng câu hỏi thật (ScriptableObject / JSON) theo chương trình lớp 8
    private static readonly QuestionData[] Bank = new[]
    {
        new QuestionData("12 x 8 = ?", new[] { "96", "88", "104", "80" }, 0),
        new QuestionData("15 + 27 = ?", new[] { "42", "32", "45", "52" }, 0),
        new QuestionData("9 x 9 = ?", new[] { "81", "72", "99", "89" }, 0),
        new QuestionData("144 : 12 = ?", new[] { "12", "10", "14", "11" }, 0),
        new QuestionData("7 x 6 = ?", new[] { "42", "36", "48", "35" }, 0),
    };

    private Action<bool> onAnswered;
    private int correctIndex;
    private Coroutine countdownRoutine;
    private bool answered;

    public void Show(Action<bool> callback)
    {
        onAnswered = callback;
        answered = false;
        panel.SetActive(true);

        var data = Bank[UnityEngine.Random.Range(0, Bank.Length)];
        correctIndex = data.correctIndex;

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int idx = i;
            answerLabels[i].text = data.options[i];
            answerButtons[i].onClick.RemoveAllListeners();
            answerButtons[i].onClick.AddListener(() => Answer(idx));
        }

        if (countdownRoutine != null) StopCoroutine(countdownRoutine);
        countdownRoutine = StartCoroutine(CountdownRoutine(data.question));
    }

    IEnumerator CountdownRoutine(string questionLabel)
    {
        float remaining = secondsPerQuestion;
        while (remaining > 0f)
        {
            UpdateQuestionAndTimer(questionLabel, remaining);
            yield return null;
            remaining -= Time.deltaTime;
        }
        UpdateQuestionAndTimer(questionLabel, 0f);

        if (!answered)
            Answer(-1); // hết giờ = coi như trả lời sai
    }

    void UpdateQuestionAndTimer(string questionLabel, float remaining)
    {
        int secs = Mathf.CeilToInt(Mathf.Max(remaining, 0f));
        if (timerText != null)
        {
            questionText.text = questionLabel;
            timerText.text = $"{secs:00}s";
        }
        else
        {
            // Không có ô riêng cho đồng hồ -> ghép chung vào câu hỏi
            questionText.text = $"{questionLabel}\n\n⏳ {secs:00}s";
        }
    }

    void Answer(int index)
    {
        if (answered) return;
        answered = true;

        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        panel.SetActive(false);
        onAnswered?.Invoke(index == correctIndex);
    }
}
