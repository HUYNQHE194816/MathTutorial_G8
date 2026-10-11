using System.Collections.Generic;
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

    [Header("Right - AI Coach")]
    public TMP_Text aiResponseText;
    public GameObject loadingIndicator;

    [Header("Right - Coach UI (để trống = chế độ giải thích văn bản như cũ)")]
    public Button[] coachOptionButtons;  // 4 nút lựa chọn cho câu hỏi dẫn dắt / kiểm tra
    public TMP_Text[] coachOptionLabels; // label tương ứng từng nút
    public Button continueButton;        // nút "Tiếp"
    public TMP_Text continueLabel;       // text trên nút Tiếp

    [Header("Navigation Buttons")]
    public Button prevButton;
    public Button nextButton;

    [Header("Colors")]
    public Color normalColor = new Color32(40, 50, 70, 255);
    public Color wrongColor = new Color32(220, 60, 60, 255);   // Màu đỏ: Đáp án học sinh chọn sai
    public Color correctColor = new Color32(46, 173, 75, 255); // Màu xanh: Đáp án đúng

    // Trạng thái dạy kèm của từng câu sai (giữ riêng để không phải sửa WrongQuestionItem)
    private class CoachState
    {
        public CoachScript script;
        public int step;
        public int wrongTries;
        public bool selfCorrected;
        public bool finished;
        public bool loading;
        public bool failed;
    }

    private readonly Dictionary<WrongQuestionItem, CoachState> states = new Dictionary<WrongQuestionItem, CoachState>();
    private int currentIndex = 0;

    bool CoachUiReady =>
        coachOptionButtons != null && coachOptionButtons.Length > 0 &&
        coachOptionLabels != null && coachOptionLabels.Length >= coachOptionButtons.Length &&
        continueButton != null && continueLabel != null;

    WrongQuestionItem Current => ReviewSessionData.wrongQuestions[currentIndex];

    bool IsCurrent(WrongQuestionItem item) =>
        ReviewSessionData.wrongQuestions != null &&
        currentIndex >= 0 && currentIndex < ReviewSessionData.wrongQuestions.Count &&
        ReviewSessionData.wrongQuestions[currentIndex] == item;

    void Start()
    {
        if (backMenuButton != null)
            backMenuButton.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));

        if (prevButton != null)
            prevButton.onClick.AddListener(OnPrevClicked);

        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextClicked);

        if (CoachUiReady)
        {
            for (int i = 0; i < coachOptionButtons.Length; i++)
            {
                int k = i;
                coachOptionButtons[k].onClick.AddListener(() => OnCoachOption(k));
            }
            continueButton.onClick.AddListener(OnContinue);
        }

        // Trường hợp không có câu sai nào (hoặc test trực tiếp Scene)
        if (ReviewSessionData.wrongQuestions == null || ReviewSessionData.wrongQuestions.Count == 0)
        {
            if (questionText != null) questionText.text = "Bạn không có câu sai nào! Xuất sắc 🎉";
            if (counterText != null) counterText.text = "0 / 0";
            if (aiResponseText != null) aiResponseText.text = "Hãy quay lại làm bài tiếp nhé!";
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
            if (prevButton != null) prevButton.interactable = false;
            if (nextButton != null) nextButton.interactable = false;
            HideCoachControls();
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

        // 5. AI dạy kèm (hoặc giải thích văn bản nếu chưa dựng UI coach)
        ShowCoach(item);
    }

    // ───────────────────────── AI Coach ─────────────────────────

    void ShowCoach(WrongQuestionItem item)
    {
        HideCoachControls();

        if (!CoachUiReady)
        {
            ShowExplanation(item);
            return;
        }

        if (!states.TryGetValue(item, out var st))
        {
            st = new CoachState();
            states[item] = st;
        }

        if (st.script != null)
        {
            if (st.finished) ShowFinished(st);
            else RenderStep(item, st);
            return;
        }

        if (st.failed)
        {
            ShowExplanation(item); // dự phòng: giải thích văn bản
            return;
        }

        if (st.loading)
        {
            // Đang tải từ lần trước: chỉ hiện loading, callback sẽ tự render
            if (aiResponseText != null) aiResponseText.text = "";
            if (loadingIndicator != null) loadingIndicator.SetActive(true);
            return;
        }

        st.loading = true;
        if (aiResponseText != null) aiResponseText.text = "";
        if (loadingIndicator != null) loadingIndicator.SetActive(true);
        EnsureService();

        AIService.Instance.RequestScript(
            item,
            onComplete: script =>
            {
                st.loading = false;
                st.script = script;
                st.step = 0;
                if (IsCurrent(item)) RenderStep(item, st);
            },
            onError: err =>
            {
                st.loading = false;
                st.failed = true;
                Debug.LogWarning("[AIReview] Không lấy được kịch bản, dùng giải thích văn bản. " + err);
                if (IsCurrent(item)) ShowExplanation(item);
            });
    }

    void RenderStep(WrongQuestionItem item, CoachState st)
    {
        if (loadingIndicator != null) loadingIndicator.SetActive(false);

        var s = st.script.steps[st.step];
        st.wrongTries = 0;

        string head;
        switch (s.type)
        {
            case "hint": head = "💡 Gợi ý\n"; break;
            case "guide_question": head = "🧭 Thử nghĩ nhé\n"; break;
            case "check_question": head = "✅ Kiểm tra lại\n"; break;
            default: head = "📌 Cần nhớ\n"; break;
        }
        if (aiResponseText != null) aiResponseText.text = head + s.text;

        if (CoachScript.IsQuiz(s))
        {
            for (int i = 0; i < coachOptionButtons.Length; i++)
            {
                bool has = s.options != null && i < s.options.Length;
                coachOptionButtons[i].gameObject.SetActive(has);
                coachOptionButtons[i].interactable = true;
                if (has) coachOptionLabels[i].text = $"{"ABCD"[i]}. {s.options[i]}";
            }
            continueButton.gameObject.SetActive(false);
        }
        else
        {
            SetCoachControls(false, true);
            bool last = st.step >= st.script.steps.Length - 1;
            continueLabel.text = last ? "Xong câu này" : "Tiếp";
        }
    }

    void OnCoachOption(int i)
    {
        var item = Current;
        if (!states.TryGetValue(item, out var st) || st.script == null || st.finished) return;

        var s = st.script.steps[st.step];
        if (!CoachScript.IsQuiz(s)) return;
        bool isCheck = s.type == "check_question";

        if (i == s.correct)
        {
            if (isCheck && st.wrongTries == 0) st.selfCorrected = true;
            aiResponseText.text += "\n\n<color=#2EAD4B>Đúng rồi!</color>";
            SetCoachControls(false, true);
            continueLabel.text = st.step >= st.script.steps.Length - 1 ? "Xong câu này" : "Tiếp";
            return;
        }

        st.wrongTries++;
        if (isCheck) st.selfCorrected = false;
        coachOptionButtons[i].interactable = false;

        if (st.wrongTries >= 2)
        {
            // Sai 2 lần: tiết lộ đáp án rồi đi tiếp
            aiResponseText.text += $"\n\n<color=#DC3C3C>Đáp án đúng là {"ABCD"[s.correct]}.</color> {s.onWrong}";
            SetCoachControls(false, true);
            continueLabel.text = st.step >= st.script.steps.Length - 1 ? "Xong câu này" : "Tiếp";
        }
        else
        {
            aiResponseText.text += $"\n\n<color=#DC3C3C>Chưa đúng.</color> {s.onWrong}";
        }
    }

    void OnContinue()
    {
        var item = Current;
        if (!states.TryGetValue(item, out var st) || st.script == null) return;

        if (st.step >= st.script.steps.Length - 1)
        {
            st.finished = true;
            ShowFinished(st);
            return;
        }

        st.step++;
        RenderStep(item, st);
    }

    void ShowFinished(CoachState st)
    {
        if (loadingIndicator != null) loadingIndicator.SetActive(false);
        HideCoachControls();
        if (aiResponseText != null)
        {
            aiResponseText.text = st.selfCorrected
                ? "Tuyệt vời! Em đã tự sửa được câu này 🎉"
                : "Xong rồi! Lần sau mình cố gắng hơn nhé 💪";
        }
    }

    // ───────────────────────── Dự phòng: giải thích văn bản ─────────────────────────

    void ShowExplanation(WrongQuestionItem item)
    {
        HideCoachControls();

        // Đã có sẵn trong cache
        if (!string.IsNullOrEmpty(item.aiExplanation))
        {
            if (aiResponseText != null) aiResponseText.text = item.aiExplanation;
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
            return;
        }

        if (aiResponseText != null) aiResponseText.text = "";
        if (loadingIndicator != null) loadingIndicator.SetActive(true);
        EnsureService();

        AIService.Instance.RequestExplanation(
            item,
            onComplete: explanation =>
            {
                item.aiExplanation = explanation;
                if (!IsCurrent(item)) return;
                if (aiResponseText != null) aiResponseText.text = explanation;
                if (loadingIndicator != null) loadingIndicator.SetActive(false);
            },
            onError: error =>
            {
                if (!IsCurrent(item)) return;
                if (aiResponseText != null)
                    aiResponseText.text = $"<color=red>[Lỗi kết nối AI]:\n{error}</color>";
                if (loadingIndicator != null) loadingIndicator.SetActive(false);
            });
    }

    // ───────────────────────── Tiện ích ─────────────────────────

    void EnsureService()
    {
        if (AIService.Instance == null)
        {
            GameObject aiObj = new GameObject("AIService");
            aiObj.AddComponent<AIService>();
        }
    }

    void HideCoachControls() => SetCoachControls(false, false);

    void SetCoachControls(bool options, bool cont)
    {
        if (coachOptionButtons != null)
            foreach (var b in coachOptionButtons)
                if (b != null) b.gameObject.SetActive(options);
        if (continueButton != null) continueButton.gameObject.SetActive(cont);
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