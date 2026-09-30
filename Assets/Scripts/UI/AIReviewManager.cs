using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class AIReviewManager : MonoBehaviour
{
    [Header("Header")]
    public TMP_Text counterText;
    public Button backMenuButton;

    [Header("Left - Question & Options")]
    public TMP_Text questionText;
    public Image[] optionBackgrounds; // 4 Image nền của 4 đáp án
    public TMP_Text[] optionLabels;    // 4 Label text của 4 đáp án

    [Header("Right - AI Feedback")]
    public TMP_Text aiResponseText;
    public GameObject loadingIndicator;

    [Header("Navigation Buttons")]
    public Button prevButton;
    public Button nextButton;

    [Header("Colors")]
    public Color normalColor = new Color32(40, 50, 70, 255);
    public Color wrongColor = new Color32(220, 60, 60, 255);   // Màu đỏ: Đáp án học sinh chọn sai
    public Color correctColor = new Color32(46, 173, 75, 255); // Màu xanh: Đáp án đúng

    private int currentIndex = 0;

    void Start()
    {
        if (backMenuButton != null)
            backMenuButton.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));

        if (prevButton != null)
            prevButton.onClick.AddListener(OnPrevClicked);

        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextClicked);

        // Trường hợp không có câu sai nào (hoặc test trực tiếp Scene)
        if (ReviewSessionData.wrongQuestions == null || ReviewSessionData.wrongQuestions.Count == 0)
        {
            if (questionText != null) questionText.text = "Bạn không có câu sai nào! Xuất sắc 🎉";
            if (counterText != null) counterText.text = "0 / 0";
            if (aiResponseText != null) aiResponseText.text = "Hãy quay lại làm bài tiếp nhé!";
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
            if (prevButton != null) prevButton.interactable = false;
            if (nextButton != null) nextButton.interactable = false;
            return;
        }

        ShowQuestion(0);
    }

    public void ShowQuestion(int index)
    {
        currentIndex = index;
        var item = ReviewSessionData.wrongQuestions[currentIndex];

        // 1. Cập nhật số thứ tự câu
        if (counterText != null)
            counterText.text = $"Câu sai: {currentIndex + 1} / {ReviewSessionData.wrongQuestions.Count}";

        // 2. Cập nhật nội dung câu hỏi
        if (questionText != null)
            questionText.text = item.questionText;

        // 3. Hiển thị 4 lựa chọn và tô màu
        string[] letters = { "A", "B", "C", "D" };
        for (int i = 0; i < optionLabels.Length; i++)
        {
            if (optionLabels[i] == null) continue;

            if (item.options != null && i < item.options.Length)
            {
                optionLabels[i].gameObject.SetActive(true);
                optionLabels[i].text = $"{letters[i]}. {item.options[i]}";

                // Tô màu theo kết quả đúng/sai
                if (optionBackgrounds != null && i < optionBackgrounds.Length && optionBackgrounds[i] != null)
                {
                    if (i == item.chosenIndex)
                        optionBackgrounds[i].color = wrongColor; // Đỏ
                    else if (i == item.correctIndex)
                        optionBackgrounds[i].color = correctColor; // Xanh
                    else
                        optionBackgrounds[i].color = normalColor;
                }
            }
            else
            {
                optionLabels[i].gameObject.SetActive(false);
            }
        }

        // 4. Cập nhật trạng thái nút Trước / Sau
        if (prevButton != null) prevButton.interactable = (currentIndex > 0);
        if (nextButton != null) nextButton.interactable = (currentIndex < ReviewSessionData.wrongQuestions.Count - 1);

        // 5. Kiểm tra AI giải thích đã có sẵn trong cache chưa
        if (!string.IsNullOrEmpty(item.aiExplanation))
        {
            if (aiResponseText != null) aiResponseText.text = item.aiExplanation;
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
        }
        else
        {
            FetchAIExplanation(item);
        }
    }

    void FetchAIExplanation(WrongQuestionItem item)
    {
        if (aiResponseText != null) aiResponseText.text = "";
        if (loadingIndicator != null) loadingIndicator.SetActive(true);

        if (AIService.Instance == null)
        {
            GameObject aiObj = new GameObject("AIService");
            aiObj.AddComponent<AIService>();
        }

        AIService.Instance.RequestExplanation(
            item,
            onComplete: (explanation) =>
            {
                item.aiExplanation = explanation;
                if (aiResponseText != null) aiResponseText.text = explanation;
                if (loadingIndicator != null) loadingIndicator.SetActive(false);
            },
            onError: (error) =>
            {
                if (aiResponseText != null)
                    aiResponseText.text = $"<color=red>[Lỗi kết nối AI]:\n{error}</color>";
                if (loadingIndicator != null) loadingIndicator.SetActive(false);
            }
        );
    }

    void OnPrevClicked()
    {
        if (currentIndex > 0)
            ShowQuestion(currentIndex - 1);
    }

    void OnNextClicked()
    {
        if (currentIndex < ReviewSessionData.wrongQuestions.Count - 1)
            ShowQuestion(currentIndex + 1);
    }
}
