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
        new Q("(x + 3)² = ?", new[] { "x² + 6x + 9", "x² + 9", "x² + 3x + 9", "x² - 6x + 9" }, 0),
        new Q("Phân tích x² - 16 thành nhân tử:", new[] { "(x - 4)(x + 4)", "(x - 4)²", "(x + 4)²", "(x - 8)(x + 2)" }, 0),
        new Q("Giải phương trình 2x + 6 = 0", new[] { "x = -3", "x = 3", "x = -6", "x = 6" }, 0),
        new Q("Tổng các góc của một tứ giác bằng:", new[] { "360°", "180°", "270°", "540°" }, 0),
        new Q("Tam giác vuông có hai cạnh góc vuông 3 cm và 4 cm. Cạnh huyền dài:", new[] { "5 cm", "6 cm", "7 cm", "25 cm" }, 0),
        new Q("Diện tích hình chữ nhật dài 8 cm, rộng 5 cm là:", new[] { "40 cm²", "26 cm²", "13 cm²", "80 cm²" }, 0),
        new Q("(a - b)² = ?", new[] { "a² - 2ab + b²", "a² - b²", "a² + 2ab + b²", "a² - 2ab - b²" }, 0),
        new Q("Trong hình bình hành, hai đường chéo:", new[] { "cắt nhau tại trung điểm mỗi đường", "luôn bằng nhau", "luôn vuông góc", "song song với nhau" }, 0),
        new Q("Phân tích 2x + 2y thành nhân tử:", new[] { "2(x + y)", "2xy", "x(2 + y)", "4(x + y)" }, 0),
        new Q("Phương trình 3x - 9 = 0 có nghiệm:", new[] { "x = 3", "x = -3", "x = 9", "x = 6" }, 0),
        new Q("Tứ giác có ba góc vuông là hình gì?", new[] { "Hình chữ nhật", "Hình thoi", "Hình thang cân", "Hình bình hành" }, 0),
        new Q("Mỗi góc của lục giác đều bằng:", new[] { "120°", "60°", "108°", "135°" }, 0),
        new Q("Diện tích tam giác có đáy 10 cm, chiều cao 6 cm là:", new[] { "30 cm²", "60 cm²", "16 cm²", "15 cm²" }, 0),
        new Q("Phân thức 1/(x - 2) xác định khi:", new[] { "x khác 2", "x khác -2", "x khác 0", "x khác 1" }, 0),
        new Q("Giá trị của x² - 2x + 1 tại x = 3 là:", new[] { "4", "2", "10", "16" }, 0)
    };

    private readonly Queue<Q> deck = new Queue<Q>();
    private List<Q> spare = new List<Q>();
    private List<Q> pool = new List<Q>();

    private Action<bool> onAnswered;
    private int correctIndex;
    private Coroutine countdownRoutine;
    private bool answered;

    // ------------------------------------------------------------------ tải câu hỏi
    IEnumerator Start()
    {
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

    // ------------------------------------------------------------------ hiển thị
    public void Show(Action<bool> callback, bool isFinal = false)
    {
        onAnswered = callback;
        answered = false;
        panel.SetActive(true);

        var data = Draw(isFinal);
        // xáo thứ tự đáp án để đáp án đúng không luôn nằm ở nút A
        var order = Enumerable.Range(0, 4).OrderBy(_ => UnityEngine.Random.value).ToArray();
        correctIndex = Array.IndexOf(order, data.correct);

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int idx = i;
            answerLabels[i].text = data.options[order[i]];
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
