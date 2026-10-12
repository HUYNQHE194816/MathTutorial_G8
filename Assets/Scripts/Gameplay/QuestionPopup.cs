using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Popup câu hỏi. Ngân hàng câu hỏi lấy theo thứ tự ưu tiên:
///   1) Google Sheet (link chia sẻ hoặc link "Xuất bản lên web" dạng CSV)  ->
///   2) file .csv gán trong Inspector  ->
///   3) 15 câu dự phòng viết sẵn trong code.
/// Mỗi ván chọn NGẪU NHIÊN "Questions Per Game" câu, không lặp câu trong ván.
/// Định dạng cột: STT, Câu hỏi, A, B, C, D, Đáp án (A/B/C/D hoặc 0-3).
/// </summary>
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

    [Header("Nguồn câu hỏi")]
    [Tooltip("Link Google Sheet (đã chia sẻ 'Bất kỳ ai có đường liên kết' hoặc Xuất bản lên web dạng CSV). Để trống = dùng file CSV.")]
    public string sheetUrl;
    [Tooltip("File CSV trong project (dự phòng khi Sheet trống hoặc tải lỗi).")]
    public TextAsset csvFile;
    [Min(1)] public int questionsPerGame = 15;
    [Tooltip("Quá số giây này mà chưa tải được Sheet thì chuyển sang file CSV.")]
    public float sheetTimeoutSeconds = 8f;

    [Header("Cấu hình")]
    [Tooltip("Số giây để trả lời mỗi câu hỏi")]
    public float secondsPerQuestion = 15f;

    /// <summary>true khi ngân hàng câu hỏi đã sẵn sàng (GameManager chờ cờ này mới cho chơi).</summary>
    public bool IsReady { get; private set; }

    private class Q
    {
        public string text; public string[] options; public int correct;
        public Q(string t, string[] o, int c) { text = t; options = o; correct = c; }
    }

    // Ngân hàng dự phòng (đáp án đúng ở vị trí 0, sẽ được xáo khi hiển thị)
    private static Q[] Fallback() => new[]
    {
        new Q("Công thức tính khối lượng riêng của một chất là:", new[] { "D = m / V", "D = m · V", "D = V / m", "D = m + V" }, 0),
        new Q("Khối lượng riêng của nước xấp xỉ bằng:", new[] { "1000 kg/m³", "100 kg/m³", "10 kg/m³", "10 000 kg/m³" }, 0),
        new Q("Đơn vị đo áp suất trong hệ SI là:", new[] { "Pascal (Pa)", "Newton (N)", "Jun (J)", "Oát (W)" }, 0),
        new Q("Áp suất được tính bằng công thức:", new[] { "p = F / S", "p = F · S", "p = S / F", "p = F + S" }, 0),
        new Q("Lực đẩy Archimedes tác dụng lên vật nhúng trong chất lỏng có phương và chiều:", new[] { "thẳng đứng, từ dưới lên", "thẳng đứng, từ trên xuống", "nằm ngang", "theo phương bất kì" }, 0),
        new Q("Vật nhúng trong chất lỏng sẽ chìm xuống khi:", new[] { "trọng lượng của vật lớn hơn lực đẩy Archimedes", "trọng lượng của vật nhỏ hơn lực đẩy Archimedes", "trọng lượng của vật bằng lực đẩy Archimedes", "thể tích của vật nhỏ hơn thể tích chất lỏng" }, 0),
        new Q("Moment của một lực đối với trục quay được tính bằng:", new[] { "tích của lực và cánh tay đòn", "thương của lực và cánh tay đòn", "tổng của lực và cánh tay đòn", "hiệu của lực và cánh tay đòn" }, 0),
        new Q("Hai vật nhiễm điện cùng loại khi đặt gần nhau sẽ:", new[] { "đẩy nhau", "hút nhau", "không tương tác", "lúc hút lúc đẩy" }, 0),
        new Q("Dòng điện là:", new[] { "dòng các điện tích dịch chuyển có hướng", "dòng các phân tử chuyển động hỗn loạn", "dòng các nguyên tử đứng yên", "dòng ánh sáng truyền trong dây dẫn" }, 0),
        new Q("Dụng cụ dùng để đo cường độ dòng điện là:", new[] { "ampe kế", "vôn kế", "nhiệt kế", "lực kế" }, 0),
        new Q("Đơn vị đo hiệu điện thế là:", new[] { "vôn (V)", "ampe (A)", "ôm (Ω)", "oát (W)" }, 0),
        new Q("Ampe kế phải được mắc như thế nào với thiết bị cần đo?", new[] { "nối tiếp", "song song", "nối tiếp hoặc song song đều được", "không cần mắc vào mạch" }, 0),
        new Q("Hình thức truyền nhiệt chủ yếu trong chất rắn là:", new[] { "dẫn nhiệt", "đối lưu", "bức xạ nhiệt", "truyền ánh sáng" }, 0),
        new Q("Nhiệt từ Mặt Trời truyền tới Trái Đất chủ yếu bằng hình thức:", new[] { "bức xạ nhiệt", "dẫn nhiệt", "đối lưu", "va chạm cơ học" }, 0),
        new Q("Khi nhiệt độ tăng, thể tích của đa số chất lỏng thường:", new[] { "tăng lên (nở ra)", "giảm đi (co lại)", "không thay đổi", "lúc tăng lúc giảm bất kì" }, 0)
    };

    private readonly Queue<Q> deck = new Queue<Q>();
    private List<Q> spare = new List<Q>();
    private List<Q> pool = new List<Q>();

    private Action<bool> onAnswered;
    private int correctIndex;
    private Coroutine countdownRoutine;
    private Coroutine popRoutine;
    private bool answered;
    private Image timerFill;   // thanh lửa đếm ngược (tự dựng trong BuildTheme)
    private Image[] answerFills;   // lớp nền đá của 4 nút (đổi màu khi hiện đáp án)

    // ------------------------------------------------------------------ tải câu hỏi
    IEnumerator Start()
    {
        BuildTheme();
        List<Q> loaded = null;

        // Vào từ luồng Chọn chương -> Chọn bài: dùng đúng câu hỏi của bài đã chọn.
        if (SubjectSession.TryGetCsv(SubjectId.Ly, out string sessionCsv)) loaded = ParseCsv(sessionCsv);

        if (loaded == null && !string.IsNullOrWhiteSpace(sheetUrl))
        {
            using (var req = UnityWebRequest.Get(NormalizeSheetUrl(sheetUrl.Trim())))
            {
                req.timeout = Mathf.CeilToInt(sheetTimeoutSeconds);
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                    Debug.LogWarning("[Question] Không tải được Google Sheet: " + req.error + " -> dùng file CSV.");
                else if (req.downloadHandler.text.TrimStart().StartsWith("<"))
                    Debug.LogWarning("[Question] Google Sheet trả về trang web thay vì CSV (chưa chia sẻ công khai?) -> dùng file CSV.");
                else
                    loaded = ParseCsv(req.downloadHandler.text);
            }
        }

        if ((loaded == null || loaded.Count == 0) && csvFile != null)
            loaded = ParseCsv(csvFile.text);

        if (loaded == null || loaded.Count == 0)
        {
            Debug.LogWarning("[Question] Không có dữ liệu từ Sheet/CSV -> dùng 15 câu dự phòng.");
            loaded = Fallback().ToList();
        }

        BuildDeck(loaded);
        IsReady = true;
    }

    // Chấp nhận cả link /edit thường: tự đổi sang link xuất CSV của đúng tab (gid).
    static string NormalizeSheetUrl(string url)
    {
        const string key = "/spreadsheets/d/";
        int i = url.IndexOf(key, StringComparison.Ordinal);
        if (i < 0 || url.Contains("output=csv") || url.Contains("format=csv") || url.Contains("/pub")) return url;

        int s = i + key.Length, e = url.IndexOfAny(new[] { '/', '?', '#' }, s);
        string id = e < 0 ? url.Substring(s) : url.Substring(s, e - s);
        var m = System.Text.RegularExpressions.Regex.Match(url, @"gid=(\d+)");
        return $"https://docs.google.com/spreadsheets/d/{id}/export?format=csv&gid={(m.Success ? m.Groups[1].Value : "0")}";
    }

    void BuildDeck(List<Q> all)
    {
        pool = all;
        var shuffled = all.OrderBy(_ => UnityEngine.Random.value).ToList();
        if (shuffled.Count < questionsPerGame)
            Debug.LogWarning($"[Question] Ngân hàng chỉ có {shuffled.Count} câu (< {questionsPerGame}); một số câu sẽ lặp lại trong ván.");

        deck.Clear();
        for (int i = 0; i < questionsPerGame; i++) deck.Enqueue(shuffled[i % shuffled.Count]);
        spare = shuffled.Skip(questionsPerGame).ToList(); // câu còn dư -> dành cho rương
        Debug.Log($"[Question] Đã nạp {all.Count} câu, chọn ngẫu nhiên {questionsPerGame} câu cho ván này.");
    }

    Q Draw(bool isFinal)
    {
        if (isFinal && spare.Count > 0) return spare[UnityEngine.Random.Range(0, spare.Count)];
        if (deck.Count == 0)
            foreach (var q in pool.OrderBy(_ => UnityEngine.Random.value)) deck.Enqueue(q);
        return deck.Dequeue();
    }

    private string currentQuestionText;
    private string[] currentDisplayedOptions = new string[4];

    // ------------------------------------------------------------------ hiển thị
    public void Show(Action<bool> callback, bool isFinal = false)
    {
        onAnswered = callback;
        answered = false;
        ResetAnswerVisuals();
        panel.SetActive(true);
        if (popRoutine != null) StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(PopIn());

        var data = Draw(isFinal);
        currentQuestionText = data.text;
        // xáo thứ tự đáp án để đáp án đúng không luôn nằm ở nút A
        var order = Enumerable.Range(0, 4).OrderBy(_ => UnityEngine.Random.value).ToArray();
        correctIndex = Array.IndexOf(order, data.correct);

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int idx = i;
            currentDisplayedOptions[i] = data.options[order[i]];
            answerLabels[i].text = currentDisplayedOptions[i];
            answerButtons[i].onClick.RemoveAllListeners();
            answerButtons[i].onClick.AddListener(() => Answer(idx));
        }

        if (countdownRoutine != null) StopCoroutine(countdownRoutine);
        countdownRoutine = StartCoroutine(CountdownRoutine(data.text));
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
            questionText.text = $"{questionLabel}\n\n⏳ {secs:00}s";
        }

        bool urgent = remaining <= 5f;
        if (timerText != null) timerText.color = urgent ? GameTheme.Blood : GameTheme.EmberDeep;
        if (timerFill != null)
        {
            UIKit.SetBar(timerFill, secondsPerQuestion > 0f ? remaining / secondsPerQuestion : 0f);
            Color want = urgent ? GameTheme.Blood : GameTheme.Ember;
            if (timerFill.color != want) timerFill.color = want;
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

        if (index != correctIndex)
        {
            ReviewSessionData.AddWrong(currentQuestionText, (string[])currentDisplayedOptions.Clone(), index, correctIndex);
        }

        StartCoroutine(RevealThenClose(index));
    }

    /// <summary>Tô xanh đáp án đúng, tô đỏ + lắc đáp án đã chọn sai, giữ ~0.5-1s rồi mới đóng popup.</summary>
    IEnumerator RevealThenClose(int chosen)
    {
        bool correct = chosen == correctIndex;
        if (answerFills != null)
        {
            if (correctIndex >= 0 && correctIndex < answerFills.Length && answerFills[correctIndex] != null)
                answerFills[correctIndex].color = Color.Lerp(GameTheme.Moss, GameTheme.Ink, .35f);
            if (!correct && chosen >= 0 && chosen < answerFills.Length && answerFills[chosen] != null)
                answerFills[chosen].color = Color.Lerp(GameTheme.Blood, GameTheme.Ink, .15f);
        }

        var good = (correctIndex >= 0 && correctIndex < answerButtons.Length) ? (RectTransform)answerButtons[correctIndex].transform : null;
        RectTransform bad = (!correct && chosen >= 0 && chosen < answerButtons.Length) ? (RectTransform)answerButtons[chosen].transform : null;
        Vector2 badHome = bad != null ? bad.anchoredPosition : Vector2.zero;

        float dur = correct ? .55f : 1f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur;
            if (good != null) good.localScale = Vector3.one * (1f + .06f * Mathf.Sin(Mathf.Clamp01(t / .3f) * Mathf.PI));
            if (bad != null) bad.anchoredPosition = badHome + new Vector2(Mathf.Sin(t * 70f) * 9f * (1f - k), 0f);
            yield return null;
        }
        if (good != null) good.localScale = Vector3.one;
        if (bad != null) bad.anchoredPosition = badHome;

        ResetAnswerVisuals();
        panel.SetActive(false);
        onAnswered?.Invoke(correct);
    }

    void ResetAnswerVisuals()
    {
        if (answerFills == null) return;
        for (int i = 0; i < answerFills.Length; i++)
        {
            if (answerFills[i] != null) answerFills[i].color = GameTheme.SlateHi;
            if (answerButtons != null && i < answerButtons.Length && answerButtons[i] != null)
                answerButtons[i].transform.localScale = Vector3.one;
        }
    }

    // ------------------------------------------------------------------ giao diện dark fantasy
    /// <summary>
    /// Phủ giao diện lên popup có sẵn trong scene (giữ nguyên mọi tham chiếu Inspector):
    /// giấy da phía sau, chữ mực đen, 4 nút đáp án kiểu đá phiến có rune A/B/C/D, thanh lửa đếm ngược.
    /// </summary>
    void BuildTheme()
    {
        if (panel == null) return;
        var prt = (RectTransform)panel.transform;

        // tấm giấy da nằm sau toàn bộ nội dung popup
        var card = UIKit.Panel(prt, "Card", Vector2.zero, new Vector2(100f, 100f), PanelStyle.Parchment, out _);
        card.anchorMin = new Vector2(.04f, .02f); card.anchorMax = new Vector2(.96f, .84f);
        card.offsetMin = Vector2.zero; card.offsetMax = Vector2.zero;
        card.GetComponent<Image>().raycastTarget = false;
        card.SetAsFirstSibling();

        // đề bài
        if (questionText != null)
        {
            questionText.color = GameTheme.PaperInk;
            questionText.fontStyle = FontStyles.Bold;
            questionText.alignment = TextAlignmentOptions.Center;
            questionText.enableAutoSizing = true; questionText.fontSizeMin = 26f; questionText.fontSizeMax = 44f;
            questionText.margin = new Vector4(24f, 12f, 24f, 12f);
        }

        // đồng hồ chữ + thanh lửa đếm ngược (nằm giữa đề bài và các nút)
        if (timerText != null) { timerText.color = GameTheme.EmberDeep; timerText.fontStyle = FontStyles.Bold; }
        var bar = UIKit.ProgressBar(prt, "TimerBar", Vector2.zero, new Vector2(100f, 14f), GameTheme.Ember, out timerFill);
        bar.anchorMin = new Vector2(.0822f, .298f); bar.anchorMax = new Vector2(.9178f, .312f);
        bar.offsetMin = Vector2.zero; bar.offsetMax = Vector2.zero;
        UIKit.SetBar(timerFill, 1f);

        // 4 nút đáp án
        string[] runes = { "A", "B", "C", "D" };
        answerFills = new Image[answerButtons.Length];
        for (int i = 0; i < answerButtons.Length; i++)
        {
            var b = answerButtons[i];
            if (b == null) continue;
            var brt = (RectTransform)b.transform;

            var frameImg = b.GetComponent<Image>();
            if (frameImg != null) { frameImg.sprite = null; frameImg.color = GameTheme.Bronze; }

            var fill = UIKit.CBox("Fill", brt, GameTheme.SlateHi, Vector2.zero, Vector2.zero);
            UIKit.Stretch(fill, 4f, 4f, 4f, 4f);
            fill.SetAsFirstSibling();   // nằm dưới chữ của nút
            answerFills[i] = fill.GetComponent<Image>();

            var rune = UIKit.CBox("Rune", brt, GameTheme.Gold, Vector2.zero, new Vector2(34f, 34f));
            UIKit.Place(rune, new Vector2(0f, .5f), new Vector2(0f, .5f), UIKit.C, new Vector2(40f, 0f), new Vector2(34f, 34f));
            rune.localEulerAngles = new Vector3(0f, 0f, 45f);
            var letter = UIKit.Label("Letter", brt, runes[i % 4], 28, GameTheme.PaperInk, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(40f, 40f), true, false, FontStyle.Bold);
            UIKit.Place(letter.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f), UIKit.C, new Vector2(40f, 0f), new Vector2(40f, 40f));

            // hiệu ứng rê chuột: sáng lên khi hover, tối đi khi bấm
            b.targetGraphic = fill.GetComponent<Image>();
            b.transition = Selectable.Transition.ColorTint;
            var cb = b.colors;
            cb.normalColor = new Color(.82f, .82f, .82f, 1f);
            cb.selectedColor = cb.normalColor;
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(.62f, .62f, .62f, 1f);
            cb.fadeDuration = .08f;
            b.colors = cb;

            var lbl = (answerLabels != null && i < answerLabels.Length) ? answerLabels[i] : null;
            if (lbl != null)
            {
                lbl.color = GameTheme.Bone;
                lbl.alignment = TextAlignmentOptions.MidlineLeft;
                lbl.enableAutoSizing = true; lbl.fontSizeMin = 20f; lbl.fontSizeMax = 30f;
                lbl.margin = new Vector4(80f, 6f, 16f, 6f);   // chừa chỗ cho rune bên trái
            }
        }
    }

    IEnumerator PopIn()
    {
        var rt = (RectTransform)panel.transform;
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = 0f; rt.localScale = Vector3.one * .94f;
        const float dur = .18f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            if (!panel.activeInHierarchy) break;
            float k = Mathf.Clamp01(t / dur); k = 1f - (1f - k) * (1f - k);
            cg.alpha = k; rt.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, k);
            yield return null;
        }
        cg.alpha = 1f; rt.localScale = Vector3.one;
        popRoutine = null;
    }

    // ------------------------------------------------------------------ CSV
    static List<Q> ParseCsv(string csv)
    {
        var result = new List<Q>();
        var lines = SplitRecords((csv ?? "").TrimStart('\uFEFF'));
        if (lines.Count < 2) return result;

        string[] head = ParseLine(lines[0]);
        int qc = Col(head, "cauhoi", "question", "noidungcauhoi");
        int ac = Col(head, "a", "answera", "dapana"), bc = Col(head, "b", "answerb", "dapanb");
        int cc = Col(head, "c", "answerc", "dapanc"), dc = Col(head, "d", "answerd", "dapand");
        int kc = Col(head, "dapan", "dapandung", "correctindex", "correct");
        if (qc < 0 || ac < 0 || bc < 0 || cc < 0 || dc < 0 || kc < 0) { qc = 1; ac = 2; bc = 3; cc = 4; dc = 5; kc = 6; }
        int need = Mathf.Max(qc, ac, bc, cc, dc, kc) + 1;

        for (int i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] f = ParseLine(lines[i]);
            if (f.Length < need) { Debug.LogWarning($"[Question] Dòng {i + 1} thiếu cột, bỏ qua."); continue; }

            int k = CorrectIndex(f[kc]);
            var opts = new[] { f[ac].Trim(), f[bc].Trim(), f[cc].Trim(), f[dc].Trim() };
            if (k < 0 || string.IsNullOrWhiteSpace(f[qc]) || opts.Any(string.IsNullOrEmpty))
            {
                Debug.LogWarning($"[Question] Dòng {i + 1} không hợp lệ (thiếu nội dung hoặc đáp án), bỏ qua.");
                continue;
            }
            result.Add(new Q(f[qc].Trim(), opts, k));
        }
        return result;
    }

    static int CorrectIndex(string raw)
    {
        raw = (raw ?? "").Trim();
        if (raw.Length == 1 && char.ToUpperInvariant(raw[0]) >= 'A' && char.ToUpperInvariant(raw[0]) <= 'D')
            return char.ToUpperInvariant(raw[0]) - 'A';
        return int.TryParse(raw, out int n) && n >= 0 && n <= 3 ? n : -1;
    }

    // Tách bản ghi, không cắt nhầm khi ô có xuống dòng trong ngoặc kép
    static List<string> SplitRecords(string csv)
    {
        var list = new List<string>(); var sb = new StringBuilder(); bool q = false;
        foreach (char c in csv)
        {
            if (c == '"') q = !q;
            if ((c == '\n' || c == '\r') && !q) { if (sb.Length > 0) { list.Add(sb.ToString()); sb.Clear(); } }
            else sb.Append(c);
        }
        if (sb.Length > 0) list.Add(sb.ToString());
        return list;
    }

    static string[] ParseLine(string line)
    {
        var r = new List<string>(); var f = new StringBuilder(); bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (q)
            {
                if (c == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { f.Append('"'); i++; } else q = false; }
                else f.Append(c);
            }
            else if (c == '"') q = true;
            else if (c == ',') { r.Add(f.ToString()); f.Clear(); }
            else f.Append(c);
        }
        r.Add(f.ToString());
        return r.ToArray();
    }

    // Tìm cột theo tên header, bỏ qua hoa/thường, dấu tiếng Việt, khoảng trắng
    static int Col(string[] head, params string[] names)
    {
        for (int i = 0; i < head.Length; i++)
        {
            string h = Norm(head[i]);
            if (Array.IndexOf(names, h) >= 0) return i;
        }
        return -1;
    }

    static string Norm(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace("đ", "d");
        var sb = new StringBuilder();
        foreach (char c in s.Normalize(NormalizationForm.FormD))
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }
}