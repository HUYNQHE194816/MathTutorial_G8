using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestionPopup : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text questionText;
    public Button[] answerButtons;
    public TMP_Text[] answerLabels;

    private Action<bool> onAnswered;
    private int correctIndex;

    public void Show(Action<bool> callback)
    {
        onAnswered = callback;
        panel.SetActive(true);

        // TODO: thay bằng ngân hàng câu hỏi thật thay vì dữ liệu mẫu này
        questionText.text = "12 x 8 = ?";
        string[] options = { "96", "88", "104", "80" };
        correctIndex = 0;

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int idx = i;
            answerLabels[i].text = options[i];
            answerButtons[i].onClick.RemoveAllListeners();
            answerButtons[i].onClick.AddListener(() => Answer(idx));
        }
    }

    void Answer(int index)
    {
        panel.SetActive(false);
        onAnswered?.Invoke(index == correctIndex);
    }
}
