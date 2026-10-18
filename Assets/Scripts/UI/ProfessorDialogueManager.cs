using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Màn ôn câu sai: học sinh trò chuyện với Giáo sư Meomeo.
/// Nhánh hội thoại CỐ ĐỊNH (ProfessorLines). AI chỉ sinh nội dung kiến thức cho từng câu (CoachScript),
/// được tải trước (prefetch) và xếp hàng trong AIService. Nếu AI lỗi thì dùng lời thoại dự phòng, không gọi API nữa.
/// Cần: AIService.cs (bản có RequestScript / CoachScript) và ProfessorLines.cs.
/// </summary>
public class ProfessorDialogueManager : MonoBehaviour
{
    public enum Face { Neutral, Happy, Think }

    [Header("Header")]
    public TMP_Text counterText;
    public Button backButton;

    [Header("Thẻ câu hỏi")]
    public GameObject questionCard;
    public TMP_Text questionText;
    public Image[] optionBackgrounds;
    public TMP_Text[] optionLabels;

    [Header("Giáo sư")]
    public RectTransform portrait;
    public Image portraitImage;
    public Sprite faceNeutral;
    public Sprite faceHappy;
    public Sprite faceThink;

    [Header("Hộp thoại")]
    public TMP_Text speakerText;
    public TMP_Text dialogueText;
    public Button dialogueBox;          // bấm vào hộp thoại để bỏ qua hiệu ứng gõ chữ
    public Button[] choiceButtons;
    public TMP_Text[] choiceLabels;

    [Header("Nhập suy nghĩ của học sinh")]
    public GameObject inputContainer;
    public TMP_InputField thoughtInputField;
    public Button sendThoughtButton;

    [Header("Âm thanh")]
    public AudioSource audioSource;
    public AudioClip correctClip;
    public AudioClip wrongClip;

    [Header("Màu")]
    public Color normalColor = new Color32(40, 50, 70, 255);
    public Color wrongColor = new Color32(220, 60, 60, 255);
    public Color correctColor = new Color32(46, 173, 75, 255);

    [Header("Cài đặt")]
    public float charsPerSecond = 45f;
    public float scriptTimeoutSeconds = 75f;
    public string menuSceneName = "MainMenu";
    [Tooltip("Chỉ để test: nếu mở thẳng scene mà chưa có câu sai thì nạp 2 câu mẫu.")]
    public bool demoDataWhenEmpty = false;

    // Kịch bản AI của từng câu, tải trước để học sinh đỡ phải chờ
    private class ScriptSlot
    {
        public CoachScript script;
        public bool done;
        public bool failed;
    }

    private readonly Dictionary<WrongQuestionItem, ScriptSlot> slots = new Dictionary<WrongQuestionItem, ScriptSlot>();
    private int choice = -1;
    private bool skipTyping;
    private bool selfFixedThis;
    private string submittedThought = null;
    private bool thoughtSubmitted = false;

    // ───────────────────────── Khởi động ─────────────────────────

    void Start()
    {
        if (speakerText != null) speakerText.text = ProfessorLines.ProfessorName;

        if (backButton != null) backButton.onClick.AddListener(GoMenu);
        if (dialogueBox != null) dialogueBox.onClick.AddListener(() => skipTyping = true);

        if (choiceButtons != null)
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int k = i;
                choiceButtons[k].onClick.AddListener(() => choice = k);
            }
        }

        if (inputContainer == null && dialogueBox != null)
        {
            EnsureInputUI();
        }

        if (sendThoughtButton != null)
        {
            sendThoughtButton.onClick.AddListener(OnSubmitThought);
        }
        if (thoughtInputField != null)
        {
            thoughtInputField.onSubmit.AddListener(_ => OnSubmitThought());
        }

        if (demoDataWhenEmpty && (ReviewSessionData.wrongQuestions == null || ReviewSessionData.wrongQuestions.Count == 0))
            LoadDemoData();

        StartCoroutine(IdleBreathing());
        StartCoroutine(Run());
    }

    void OnSubmitThought()
    {
        if (thoughtInputField != null && !string.IsNullOrWhiteSpace(thoughtInputField.text))
        {
            submittedThought = thoughtInputField.text.Trim();
            thoughtSubmitted = true;
        }
    }

    void LoadDemoData()
    {
        ReviewSessionData.wrongQuestions = new List<WrongQuestionItem>
        {
            new WrongQuestionItem
            {
                questionText = "Công thức tính khối lượng riêng là:",
                options = new[] { "D = m·V", "D = V/m", "D = m/V", "D = m + V" },
                chosenIndex = 0, correctIndex = 2, aiExplanation = ""
            },
            new WrongQuestionItem
            {
                questionText = "Phân tử khối của H₂O là bao nhiêu? (H = 1, O = 16)",
                options = new[] { "16", "17", "18", "19" },
                chosenIndex = -1, correctIndex = 2, aiExplanation = ""
            }
        };
    }

    // ───────────────────────── Luồng chính ─────────────────────────

    IEnumerator Run()
    {
        var list = ReviewSessionData.wrongQuestions;
        HideChoices();
        HideInputField();
        if (questionCard != null) questionCard.SetActive(false);

        if (list == null || list.Count == 0)
        {
            SetCounter(0, 0);
            yield return Say(ProfessorLines.NoWrong(), Face.Happy);
            yield return Ask("Về menu");
            GoMenu();
            yield break;
        }

        yield return Say(ProfessorLines.Intro(list.Count), Face.Neutral);
        yield return Ask("Bắt đầu");

        int selfFixedTotal = 0;

        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            SetCounter(i + 1, list.Count);
            ShowQuestion(item);

            bool timedOut = item.chosenIndex < 0;
            selfFixedThis = false;

            // 1. Giáo sư hỏi: em tự nhập suy nghĩ/lý do lúc đó hoặc dùng ô nhập
            yield return Say(ProfessorLines.AskReason(i, item), Face.Think);

            // Cho học sinh tự nhập lý do
            yield return WaitForStudentInput();

            // Giáo sư phản hồi lại suy nghĩ của em
            yield return Say(ProfessorLines.ReactToCustomThought(), Face.Neutral);

            // Gửi suy nghĩ của học sinh cho Gemini để lấy kịch bản dạy kèm
            FetchScriptWithThought(item, submittedThought);

            // 2. Chờ kịch bản AI
            var slot = slots[item];
            if (!slot.done)
            {
                SetFace(Face.Think);
                HideChoices();
                HideInputField();
                SetTextInstant(ProfessorLines.Preparing());
                float waited = 0f;
                while (!slot.done && waited < scriptTimeoutSeconds)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
                if (!slot.done) { slot.failed = true; slot.done = true; }
            }

            // 3. Chạy kịch bản, hoặc dự phòng nếu AI lỗi
            if (!slot.failed) yield return RunScript(slot.script);
            else yield return RunFallback(item);

            // 4. Lộ đáp án đúng trên thẻ câu hỏi, nhận xét kết quả
            RevealCorrect(item);
            if (selfFixedThis) selfFixedTotal++;
            yield return Say(selfFixedThis ? ProfessorLines.OutcomeSelfFixed() : ProfessorLines.OutcomeNotYet(),
                             selfFixedThis ? Face.Happy : Face.Neutral);

            yield return Ask(i == list.Count - 1 ? "Xem tổng kết" : "Câu tiếp theo");
        }

        if (questionCard != null) questionCard.SetActive(false);
        yield return Say(ProfessorLines.Summary(selfFixedTotal, list.Count),
                         selfFixedTotal * 2 >= list.Count ? Face.Happy : Face.Neutral);
        yield return Ask("Về menu");
        GoMenu();
    }

    IEnumerator RunScript(CoachScript script)
    {
        if (!string.IsNullOrWhiteSpace(script.misconception))
        {
            yield return Say(ProfessorLines.Diagnosis(script.misconception), Face.Think);
            yield return Ask("Em hiểu chỗ nhầm rồi");
        }

        int hintCount = 0;
        foreach (var step in script.steps)
        {
            switch (step.type)
            {
                case "hint":
                    yield return Say(ProfessorLines.Hint(hintCount++, step.text), Face.Neutral);
                    yield return Ask("Ta đã hiểu");
                    break;

                case "guide_question":
                    yield return RunQuiz(step, false);
                    break;

                case "check_question":
                    yield return RunQuiz(step, true);
                    break;

                case "recap":
                    yield return Say(ProfessorLines.Recap(step.text), Face.Neutral);
                    yield return Ask("Em đã ghi nhớ");
                    break;

                default:
                    yield return Say(step.text, Face.Neutral);
                    yield return Ask("Tiếp tục");
                    break;
            }
        }
    }

    IEnumerator RunQuiz(CoachStep step, bool isCheck)
    {
        yield return Say(isCheck ? ProfessorLines.CheckIntro(step.text) : ProfessorLines.GuideIntro(step.text), Face.Think);

        int n = step.options.Length;
        var labels = new string[n];
        for (int i = 0; i < n; i++) labels[i] = $"{(char)('A' + i)}. {step.options[i]}";
        var disabled = new bool[n];
        int tries = 0;

        while (true)
        {
            yield return AskMany(labels, disabled);

            if (choice == step.correct)
            {
                Play(correctClip);
                if (isCheck && tries == 0) selfFixedThis = true;
                yield return Say(ProfessorLines.Correct(), Face.Happy);
                yield break;
            }

            tries++;
            Play(wrongClip);
            if (isCheck) selfFixedThis = false;
            if (choice >= 0 && choice < n) disabled[choice] = true;

            if (tries >= 2)
            {
                // Sai 2 lần: nói đáp án rồi đi tiếp
                yield return Say(ProfessorLines.Reveal(labels[step.correct], step.onWrong), Face.Neutral);
                yield break;
            }

            yield return Say(ProfessorLines.Wrong(step.onWrong), Face.Neutral);
        }
    }

    IEnumerator RunFallback(WrongQuestionItem item)
    {
        yield return Say(ProfessorLines.FallbackIntro(), Face.Think);

        string correct = "";
        if (item.options != null && item.correctIndex >= 0 && item.correctIndex < item.options.Length)
            correct = $"{(char)('A' + item.correctIndex)}. {item.options[item.correctIndex]}";

        RevealCorrect(item);
        yield return Say(ProfessorLines.FallbackReveal(correct), Face.Neutral);
        yield return Ask("Ta hiểu rồi");
    }

    // ───────────────────────── Tải kịch bản AI kèm suy nghĩ học sinh ─────────────────────────

    void FetchScriptWithThought(WrongQuestionItem item, string studentThought)
    {
        var slot = new ScriptSlot();
        slots[item] = slot;

        if (AIService.Instance == null)
            new GameObject("AIService").AddComponent<AIService>();

        AIService.Instance.RequestScript(
            item,
            studentThought,
            onComplete: s =>
            {
                if (slot.done) return;
                slot.script = s;
                slot.done = true;
            },
            onError: e =>
            {
                Debug.LogWarning("[Professor] Không lấy được kịch bản AI, dùng lời thoại dự phòng. " + e);
                if (slot.done) return;
                slot.failed = true;
                slot.done = true;
            });
    }

    // ───────────────────────── Nhập suy nghĩ của học sinh ─────────────────────────

    IEnumerator WaitForStudentInput()
    {
        HideChoices();
        thoughtSubmitted = false;
        submittedThought = null;

        if (thoughtInputField != null)
        {
            thoughtInputField.text = "";
        }

        if (inputContainer != null)
        {
            inputContainer.SetActive(true);
            if (thoughtInputField != null)
            {
                thoughtInputField.ActivateInputField();
                thoughtInputField.Select();
            }
        }

        while (!thoughtSubmitted)
        {
            yield return null;
        }

        HideInputField();
    }

    void HideInputField()
    {
        if (inputContainer != null)
            inputContainer.SetActive(false);
    }

    void EnsureInputUI()
    {
        if (dialogueBox == null) return;
        Transform parentBox = dialogueBox.transform;

        // Container
        var contGo = new GameObject("InputContainer", typeof(RectTransform));
        contGo.transform.SetParent(parentBox, false);
        var rtCont = contGo.GetComponent<RectTransform>();
        rtCont.anchorMin = new Vector2(0.03f, 0.03f);
        rtCont.anchorMax = new Vector2(0.97f, 0.36f);
        rtCont.offsetMin = Vector2.zero;
        rtCont.offsetMax = Vector2.zero;
        inputContainer = contGo;

        // InputField Box
        var inputGo = new GameObject("StudentThoughtInput", typeof(RectTransform), typeof(Image));
        inputGo.transform.SetParent(contGo.transform, false);
        var rtInput = inputGo.GetComponent<RectTransform>();
        rtInput.anchorMin = new Vector2(0f, 0.12f);
        rtInput.anchorMax = new Vector2(0.78f, 0.88f);
        rtInput.offsetMin = Vector2.zero;
        rtInput.offsetMax = Vector2.zero;
        var bg = inputGo.GetComponent<Image>();
        bg.color = new Color(0.14f, 0.18f, 0.30f, 1f);

        thoughtInputField = inputGo.AddComponent<TMP_InputField>();
        thoughtInputField.targetGraphic = bg;

        // TextArea
        var areaGo = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        areaGo.transform.SetParent(inputGo.transform, false);
        var rtArea = areaGo.GetComponent<RectTransform>();
        rtArea.anchorMin = Vector2.zero;
        rtArea.anchorMax = Vector2.one;
        rtArea.offsetMin = new Vector2(16, 6);
        rtArea.offsetMax = new Vector2(-16, -6);

        // Placeholder Text
        var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        phGo.transform.SetParent(areaGo.transform, false);
        var rtPh = phGo.GetComponent<RectTransform>();
        rtPh.anchorMin = Vector2.zero;
        rtPh.anchorMax = Vector2.one;
        rtPh.offsetMin = Vector2.zero;
        rtPh.offsetMax = Vector2.zero;
        var phText = phGo.GetComponent<TextMeshProUGUI>();
        if (dialogueText != null) phText.font = dialogueText.font;
        phText.text = "Nhập suy nghĩ hoặc lý do em chọn đáp án này...";
        phText.fontSize = 24;
        phText.fontStyle = FontStyles.Italic;
        phText.color = new Color(0.7f, 0.75f, 0.85f, 0.6f);
        phText.alignment = TextAlignmentOptions.MidlineLeft;

        // Main Text
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(areaGo.transform, false);
        var rtText = textGo.GetComponent<RectTransform>();
        rtText.anchorMin = Vector2.zero;
        rtText.anchorMax = Vector2.one;
        rtText.offsetMin = Vector2.zero;
        rtText.offsetMax = Vector2.zero;
        var txt = textGo.GetComponent<TextMeshProUGUI>();
        if (dialogueText != null) txt.font = dialogueText.font;
        txt.fontSize = 24;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.MidlineLeft;

        thoughtInputField.textViewport = rtArea;
        thoughtInputField.textComponent = txt;
        thoughtInputField.placeholder = phText;

        // Send Button
        var btnGo = new GameObject("SendButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(contGo.transform, false);
        var rtBtn = btnGo.GetComponent<RectTransform>();
        rtBtn.anchorMin = new Vector2(0.80f, 0.12f);
        rtBtn.anchorMax = new Vector2(1f, 0.88f);
        rtBtn.offsetMin = Vector2.zero;
        rtBtn.offsetMax = Vector2.zero;
        var btnImg = btnGo.GetComponent<Image>();
        btnImg.color = new Color(0.18f, 0.65f, 0.38f);
        sendThoughtButton = btnGo.GetComponent<Button>();
        sendThoughtButton.targetGraphic = btnImg;

        var lblGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        lblGo.transform.SetParent(btnGo.transform, false);
        var rtLbl = lblGo.GetComponent<RectTransform>();
        rtLbl.anchorMin = Vector2.zero;
        rtLbl.anchorMax = Vector2.one;
        rtLbl.offsetMin = Vector2.zero;
        rtLbl.offsetMax = Vector2.zero;
        var lbl = lblGo.GetComponent<TextMeshProUGUI>();
        if (dialogueText != null) lbl.font = dialogueText.font;
        lbl.text = "Gửi cho Thầy";
        lbl.fontSize = 24;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = Color.white;

        inputContainer.SetActive(false);
    }

    // ───────────────────────── Hội thoại: nói & hỏi ─────────────────────────

    /// <summary>Giáo sư nói một câu, chữ hiện dần. Bấm vào hộp thoại để hiện ngay.</summary>
    IEnumerator Say(string text, Face face)
    {
        SetFace(face);
        HideChoices();
        HideInputField();

        dialogueText.text = text;
        dialogueText.ForceMeshUpdate();
        int total = dialogueText.textInfo.characterCount;
        dialogueText.maxVisibleCharacters = 0;

        skipTyping = false;
        float shown = 0f;
        while (shown < total && !skipTyping)
        {
            shown += charsPerSecond * Time.deltaTime;
            dialogueText.maxVisibleCharacters = Mathf.Min(total, (int)shown);
            yield return null;
        }

        dialogueText.maxVisibleCharacters = int.MaxValue;
        skipTyping = false;
    }

    /// <summary>Hiện các nút lựa chọn, chờ học sinh bấm; kết quả nằm trong field choice.</summary>
    IEnumerator Ask(params string[] labels)
    {
        return AskMany(labels, null);
    }

    IEnumerator AskMany(string[] labels, bool[] disabled)
    {
        HideInputField();
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool has = i < labels.Length;
            choiceButtons[i].gameObject.SetActive(has);
            if (!has) continue;
            choiceLabels[i].text = labels[i];
            choiceButtons[i].interactable = disabled == null || i >= disabled.Length || !disabled[i];
        }

        choice = -1;
        while (choice < 0) yield return null;
        HideChoices();
    }

    void SetTextInstant(string text)
    {
        dialogueText.text = text;
        dialogueText.maxVisibleCharacters = int.MaxValue;
    }

    void HideChoices()
    {
        if (choiceButtons == null) return;
        foreach (var b in choiceButtons)
            if (b != null) b.gameObject.SetActive(false);
    }

    // ───────────────────────── Thẻ câu hỏi ─────────────────────────

    void ShowQuestion(WrongQuestionItem item)
    {
        if (questionCard != null) questionCard.SetActive(true);
        if (questionText != null) questionText.text = item.questionText;

        for (int i = 0; i < optionLabels.Length; i++)
        {
            bool has = item.options != null && i < item.options.Length;
            optionLabels[i].transform.parent.gameObject.SetActive(has);
            if (!has) continue;

            optionLabels[i].text = $"{(char)('A' + i)}. {item.options[i]}";
            // Chỉ tô đỏ lựa chọn sai của học sinh, CHƯA lộ đáp án đúng
            optionBackgrounds[i].color = (i == item.chosenIndex) ? wrongColor : normalColor;
        }
    }

    void RevealCorrect(WrongQuestionItem item)
    {
        if (item.options == null) return;
        if (item.correctIndex >= 0 && item.correctIndex < optionBackgrounds.Length)
            optionBackgrounds[item.correctIndex].color = correctColor;
    }

    void SetCounter(int current, int total)
    {
        if (counterText != null) counterText.text = $"Câu sai: {current} / {total}";
    }

    // ───────────────────────── Nhân vật & tiện ích ─────────────────────────

    void SetFace(Face face)
    {
        if (portraitImage == null) return;
        Sprite s = face == Face.Happy ? faceHappy : face == Face.Think ? faceThink : faceNeutral;
        if (s != null) portraitImage.sprite = s;
    }

    IEnumerator IdleBreathing()
    {
        if (portrait == null) yield break;
        while (true)
        {
            float s = 1f + 0.015f * Mathf.Sin(Time.unscaledTime * 2f);
            portrait.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    void GoMenu()
    {
        SceneManager.LoadScene(menuSceneName);
    }
}