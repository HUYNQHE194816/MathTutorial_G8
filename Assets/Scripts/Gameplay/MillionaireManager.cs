using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Networking;
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

    // Bộ âm thanh theo mức độ căng thẳng của câu hỏi
    [System.Serializable]
    public class TierAudio
    {
        [Tooltip("SFX phát khi câu hỏi hiện ra (Question Music)")]
        public AudioClip question;
        [Tooltip("Nhạc nền lặp lại khi người chơi suy nghĩ (Thinking Music)")]
        public AudioClip thinking;
        [Tooltip("Nhạc hồi hộp sau khi khóa đáp án, trước khi MC công bố (Tension / Reveal)")]
        public AudioClip tension;
        [Tooltip("Số giây chờ hồi hộp trước khi công bố đúng/sai")]
        public float tensionSeconds = 1.5f;
    }

    [Header("Nguồn câu hỏi (ưu tiên: Csv File > Google Sheet > nhập tay)")]
    [Tooltip("Kéo file .csv (xuất từ Excel: CSV UTF-8) vào đây để chạy offline.")]
    public TextAsset csvFile;

    [Tooltip("Link CSV sau khi Publish to web. Dạng: https://docs.google.com/spreadsheets/d/e/XXXX/pub?output=csv")]
    public string sheetCsvUrl;
    [Tooltip("Cột theo file mẫu: STT, Câu hỏi, A, B, C, D, Đáp án (đáp án ghi A/B/C/D hoặc 0-3)")]
    public bool loadFromSheetOnStart = true;

    [Header("Câu hỏi (xếp từ dễ -> khó, tối đa 15) — dùng khi không tải từ CSV/Sheet")]
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
    public Color normalColor = new Color32(11, 26, 107, 255);
    public Color selectedColor = new Color32(242, 140, 30, 255);
    public Color correctColor = new Color32(46, 173, 75, 255);
    public Color wrongColor = new Color32(217, 58, 58, 255);
    public Color ladderNormal = Color.white;
    public Color ladderCurrent = new Color32(242, 140, 30, 255);
    public Color ladderSafe = new Color32(255, 214, 90, 255);

    // ================= ÂM THANH =================

    [Header("Audio Sources")]
    [Tooltip("Phát nhạc: theme, thinking, tension, victory... (Play On Awake = off)")]
    public AudioSource musicSource;
    [Tooltip("Phát hiệu ứng ngắn qua PlayOneShot (Play On Awake = off)")]
    public AudioSource sfxSource;

    [Header("1-4. Mở đầu chương trình (chạy 1 lần)")]
    public bool skipIntro = false;
    public AudioClip introTheme;          // 1. Intro / Main Theme
    public AudioClip contestantIntro;     // 2. Contestant Introduction
    public AudioClip hotSeatClip;         // 3. Hot Seat / Next Player
    public AudioClip letsPlayClip;        // 4. Let's Play / Start Game

    [Header("5-8. Âm thanh theo mức câu hỏi")]
    [Tooltip("Câu 1-5")] public TierAudio tierEasy = new TierAudio();
    [Tooltip("Câu 6-10")] public TierAudio tierMid = new TierAudio();
    [Tooltip("Câu 11-14")] public TierAudio tierHard = new TierAudio();
    [Tooltip("Câu 15")] public TierAudio tierFinal = new TierAudio { tensionSeconds = 3f };

    [Header("7. Khóa đáp án")]
    public AudioClip lockAnswerClip;      // Answer Lock / Final Answer

    [Header("9-10. Công bố kết quả")]
    public AudioClip correctClip;         // 9A. Correct Answer SFX
    public AudioClip correctMilestoneClip;// Câu 5 & 10: Correct SFX đặc biệt
    public AudioClip wrongClip;           // 9B. Wrong Answer SFX
    public AudioClip nextQuestionClip;    // 10. Next Question Music
    public float revealSeconds = 1.5f;    // thời gian giữ màn hình đáp án đúng/sai

    [Header("11-12. Quyền trợ giúp")]
    public AudioClip lifelineClip;        // 11. Lifeline SFX
    public AudioClip lifelineResultClip;  // 12. Lifeline Result SFX

    [Header("14-15. Kết thúc")]
    public AudioClip victoryClip;         // 14A. Winning / Victory Music
    public AudioClip endClip;             // 14B. Wrong Answer / End Music
    public AudioClip closingTheme;        // 15. Closing Theme (lặp lại)

    // ============================================

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
        StartCoroutine(InitRoutine());
    }

    IEnumerator InitRoutine()
    {
        endPanel.SetActive(false);

        if (SubjectSession.TryGetCsv(SubjectId.Hoa, out string sessionCsv))
            ParseQuestionsFromCsv(sessionCsv);   // vào từ luồng Chọn chương -> Chọn bài
        else if (csvFile != null)
            ParseQuestionsFromCsv(csvFile.text);
        else if (loadFromSheetOnStart && !string.IsNullOrWhiteSpace(sheetCsvUrl))
            yield return LoadQuestionsFromSheet(sheetCsvUrl);

        // Kiểm tra từng câu hỏi: mảng options phải luôn có đúng 4 phần tử.
        for (int qi = 0; qi < questions.Count; qi++)
        {
            var q = questions[qi];
            int have = q.options == null ? 0 : q.options.Length;
            if (have != 4)
            {
                string preview = string.IsNullOrEmpty(q.text) ? "(chưa có nội dung)" : q.text;
                Debug.LogError($"[MillionaireManager] Câu hỏi #{qi + 1} (\"{preview}\") chỉ có {have}/4 đáp án. " +
                    "Đã tự vá tạm bằng chuỗi rỗng — vào Inspector (List 'Questions') hoặc dòng tương ứng trong CSV/Sheet để sửa lại cho đủ 4 cột đáp án.");
                var fixedOptions = new string[4];
                if (q.options != null)
                    for (int k = 0; k < Mathf.Min(4, q.options.Length); k++) fixedOptions[k] = q.options[k];
                q.options = fixedOptions;
            }
            q.correctIndex = Mathf.Clamp(q.correctIndex, 0, 3);
        }

        if (questions.Count == 0)
        {
            Debug.LogError("[MillionaireManager] Danh sách câu hỏi rỗng. " +
                "Kiểm tra file CSV / link Google Sheet hoặc điền tay vào 'Questions' trong Inspector.");
            yield break;
        }
        if (answerButtons == null || answerButtons.Length < 4)
        {
            Debug.LogError("[MillionaireManager] 'Answer Buttons' phải có đúng 4 phần tử (A,B,C,D) trong Inspector.");
            yield break;
        }
        if (answerLabels == null || answerLabels.Length < 4)
        {
            Debug.LogError("[MillionaireManager] 'Answer Labels' phải có đúng 4 phần tử (A,B,C,D) trong Inspector.");
            yield break;
        }

        BuildLadder();
        fiftyFiftyButton.onClick.AddListener(UseFiftyFifty);
        for (int i = 0; i < answerButtons.Length; i++)
        {
            int idx = i;
            answerButtons[i].onClick.AddListener(() => OnAnswerClicked(idx));
        }

        // Mở đầu chương trình (bước 1-4), rồi mới vào câu 1
        yield return IntroRoutine();
        yield return ShowQuestionRoutine();
    }

    // ---------- 1-4: MỞ ĐẦU CHƯƠNG TRÌNH ----------

    IEnumerator IntroRoutine()
    {
        locked = true;

        // Ẩn nội dung câu hỏi trong lúc giới thiệu
        questionText.text = "";
        for (int i = 0; i < 4; i++)
        {
            answerButtons[i].interactable = false;
            answerButtons[i].image.color = normalColor;
            answerLabels[i].text = "";
        }
        fiftyFiftyButton.interactable = false;

        if (!skipIntro)
        {
            // 1. Intro / Main Theme
            PlayMusic(introTheme, false);
            yield return WaitClip(introTheme);

            // 2. Contestant Introduction
            PlayMusic(contestantIntro, false);
            yield return WaitClip(contestantIntro);

            // 3. Hot Seat / Next Player
            PlayMusic(hotSeatClip, false);
            yield return WaitClip(hotSeatClip);

            // 4. Let's Play / Start Game
            PlayMusic(letsPlayClip, false);
            yield return WaitClip(letsPlayClip);
        }

        fiftyFiftyButton.interactable = true;
    }

    // ---------- TẢI / PHÂN TÍCH CÂU HỎI ----------

    IEnumerator LoadQuestionsFromSheet(string url)
    {
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[MillionaireManager] Không tải được Google Sheet: " + req.error +
                    " — sẽ dùng danh sách 'Questions' nhập tay (nếu có).");
                yield break;
            }

            ParseQuestionsFromCsv(req.downloadHandler.text);
        }
    }

    void ParseQuestionsFromCsv(string csv)
    {
        csv = (csv ?? "").TrimStart('\uFEFF'); // bỏ BOM nếu Excel xuất CSV UTF-8 có BOM
        List<string> lines = SplitCsvRecords(csv);
        if (lines.Count < 2)
        {
            Debug.LogWarning("[MillionaireManager] CSV không có dữ liệu.");
            return;
        }

        // --- Nhận diện cột theo tên header (file mẫu: STT, Câu hỏi, A, B, C, D, Đáp án) ---
        string[] header = ParseCsvLine(lines[0]);
        int questionCol = FindColumn(header, "cauhoi", "question", "noidungcauhoi");
        int aCol = FindColumn(header, "a", "answera", "dapana");
        int bCol = FindColumn(header, "b", "answerb", "dapanb");
        int cCol = FindColumn(header, "c", "answerc", "dapanc");
        int dCol = FindColumn(header, "d", "answerd", "dapand");
        int correctCol = FindColumn(header, "dapan", "dapandung", "correctindex", "correct");

        if (questionCol < 0 || aCol < 0 || bCol < 0 || cCol < 0 || dCol < 0 || correctCol < 0)
        {
            // Không nhận diện được header -> dùng thứ tự của file mẫu
            questionCol = 1; aCol = 2; bCol = 3; cCol = 4; dCol = 5; correctCol = 6;
            Debug.LogWarning("[MillionaireManager] Không nhận diện được tên cột header, dùng thứ tự mặc định (STT, Câu hỏi, A, B, C, D, Đáp án).");
        }
        int neededCols = Mathf.Max(questionCol, aCol, bCol, cCol, dCol, correctCol) + 1;

        var loaded = new List<Question>();
        for (int i = 1; i < lines.Count; i++) // bỏ dòng header
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] cols = ParseCsvLine(line);
            if (cols.Length < neededCols)
            {
                Debug.LogWarning($"[MillionaireManager] Dòng {i + 1} thiếu cột ({cols.Length}/{neededCols}) — nội dung thô: \"{line}\"");
                continue;
            }

            int correct = ParseCorrectIndex(cols[correctCol]);
            if (correct < 0)
            {
                Debug.LogWarning($"[MillionaireManager] Dòng {i + 1}: giá trị đáp án đúng không hợp lệ ('{cols[correctCol]}'), bỏ qua.");
                continue;
            }

            loaded.Add(new Question
            {
                text = cols[questionCol].Trim(),
                options = new string[] { cols[aCol].Trim(), cols[bCol].Trim(), cols[cCol].Trim(), cols[dCol].Trim() },
                correctIndex = correct
            });
        }

        if (loaded.Count > 0)
        {
            questions = loaded;

            // Phát hiện câu hỏi bị TRÙNG nội dung y hệt nhau
            var seen = new Dictionary<string, List<int>>();
            for (int i = 0; i < loaded.Count; i++)
            {
                string t = loaded[i].text ?? "";
                if (!seen.ContainsKey(t)) seen[t] = new List<int>();
                seen[t].Add(i + 1);
            }
            foreach (var kv in seen)
            {
                if (kv.Value.Count > 1)
                    Debug.LogWarning($"[MillionaireManager] {kv.Value.Count} câu hỏi TRÙNG nội dung y hệt nhau: \"{kv.Key}\" — ở vị trí #{string.Join(", ", kv.Value)}. " +
                        "Có thể do copy dòng mẫu rồi quên sửa cột Câu hỏi.");
            }
        }
        else
            Debug.LogWarning("[MillionaireManager] Dữ liệu tải được nhưng không có dòng hợp lệ nào.");
    }

    // Tách CSV thành các bản ghi, không cắt nhầm khi ô có xuống dòng trong ngoặc kép
    List<string> SplitCsvRecords(string csv)
    {
        var records = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool inQuotes = false;
        foreach (char c in csv)
        {
            if (c == '"') inQuotes = !inQuotes;
            if ((c == '\n' || c == '\r') && !inQuotes)
            {
                if (sb.Length > 0) { records.Add(sb.ToString()); sb.Clear(); }
            }
            else sb.Append(c);
        }
        if (sb.Length > 0) records.Add(sb.ToString());
        return records;
    }

    // Tìm cột theo tên header, không phân biệt hoa/thường, dấu tiếng Việt, khoảng trắng.
    int FindColumn(string[] header, params string[] candidates)
    {
        for (int i = 0; i < header.Length; i++)
        {
            string h = NormalizeHeader(header[i]);
            foreach (var c in candidates)
                if (h == c) return i;
        }
        return -1;
    }

    string NormalizeHeader(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Trim().ToLowerInvariant().Replace(" ", "");
        string formD = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (char c in formD)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC).Replace("đ", "d");
    }

    // Chấp nhận đáp án đúng ghi dạng số (0-3) hoặc chữ cái (A-D / a-d)
    int ParseCorrectIndex(string raw)
    {
        raw = (raw ?? "").Trim();
        if (raw.Length == 1)
        {
            char c = char.ToUpperInvariant(raw[0]);
            if (c >= 'A' && c <= 'D') return c - 'A';
        }
        if (int.TryParse(raw, out int n) && n >= 0 && n <= 3) return n;
        return -1;
    }

    // Parser CSV đơn giản, xử lý đúng trường hợp có dấu phẩy nằm trong ngoặc kép "..."
    string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var field = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { result.Add(field.ToString()); field.Clear(); }
                else field.Append(c);
            }
        }
        result.Add(field.ToString());
        return result.ToArray();
    }

    // Đổi chỉ số dưới/trên Unicode (H₂O, 10²³) sang thẻ TMP để không bị ô vuông khi font thiếu glyph.
    static string FormatChem(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        const string sub = "₀₁₂₃₄₅₆₇₈₉";
        const string sup = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        var sb = new System.Text.StringBuilder();
        foreach (char c in s)
        {
            int i = sub.IndexOf(c);
            int j = sup.IndexOf(c);
            if (i >= 0) sb.Append("<sub>").Append(i).Append("</sub>");
            else if (j >= 0) sb.Append("<sup>").Append(j).Append("</sup>");
            else sb.Append(c);
        }
        // Nếu font vẫn hiện ô vuông cho các ký hiệu này, bỏ comment 2 dòng dưới:
        // sb.Replace("→", "->");
        // sb.Replace("×", "x");
        return sb.ToString();
    }

    // ---------- LOGIC GAME ----------

    void BuildLadder()
    {
        int n = Mathf.Min(questions.Count, Prizes.Length);
        ladder = new TMP_Text[n];
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

    // Index của câu cuối cùng thực sự được chơi (thường là 14 = câu 15)
    int LastIndex => Mathf.Min(questions.Count, Prizes.Length) - 1;

    // Chọn bộ âm thanh theo câu: 1-5 dễ, 6-10 vừa, 11-14 khó, 15 câu cuối
    TierAudio GetTier(int index)
    {
        if (index >= LastIndex) return tierFinal;
        if (index < 5) return tierEasy;
        if (index < 10) return tierMid;
        return tierHard;
    }

    // 5-6: Hiện câu hỏi -> Question SFX -> Thinking Music
    IEnumerator ShowQuestionRoutine()
    {
        locked = true;

        // 10. Next Question Music (từ câu 2 trở đi)
        if (current > 0)
        {
            PlaySfx(nextQuestionClip);
            yield return WaitClip(nextQuestionClip);
        }

        var q = questions[current];
        var tier = GetTier(current);
        questionText.text = FormatChem(q.text);
        string[] letters = { "A", "B", "C", "D" };

        for (int i = 0; i < 4; i++)
        {
            answerButtons[i].interactable = true;
            answerButtons[i].image.color = normalColor;
            answerLabels[i].text = $"{letters[i]}:  {FormatChem(q.options[i])}";
        }
        RefreshLadder();

        // 5. Question Music
        PlayMusic(tier.question, false);
        yield return WaitClip(tier.question);

        // 6. Thinking Music (lặp cho tới khi khóa đáp án)
        PlayMusic(tier.thinking, true);
        locked = false;
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
        var tier = GetTier(current);

        // 7. Answer Lock / Final Answer
        StopMusic();
        answerButtons[chosen].image.color = selectedColor; // "Đáp án cuối cùng?"
        PlaySfx(lockAnswerClip);

        // 8. Tension / Reveal (câu 15 chờ lâu hơn theo tierFinal.tensionSeconds)
        PlayMusic(tier.tension, false);
        yield return new WaitForSeconds(tier.tensionSeconds);
        StopMusic();

        bool isCorrect = chosen == q.correctIndex;
        bool isLast = current >= LastIndex;

        answerButtons[q.correctIndex].image.color = correctColor;
        if (!isCorrect)
            answerButtons[chosen].image.color = wrongColor;

        if (isCorrect)
        {
            // 9A. Đúng. Câu 5, 10 có SFX đặc biệt. Câu 15 để dành cho Victory.
            if (!isLast)
            {
                bool milestone = System.Array.IndexOf(Milestones, current) >= 0;
                PlaySfx(milestone && correctMilestoneClip != null ? correctMilestoneClip : correctClip);
            }
        }
        else
        {
            // 9B. Sai
            PlaySfx(wrongClip);
        }
        yield return new WaitForSeconds(revealSeconds);

        if (!isCorrect)
        {
            EndGame($"Rất tiếc! Đáp án đúng là {"ABCD"[q.correctIndex]}.\nBạn ra về với {SafePrize()} đồng.", false);
            yield break;
        }

        current++;
        if (current >= questions.Count || current >= Prizes.Length)
            EndGame($"CHÚC MỪNG! Bạn đã chiến thắng với {Prizes[current - 1]} đồng!", true);
        else
            yield return ShowQuestionRoutine();
    }

    string SafePrize()
    {
        for (int i = Milestones.Length - 1; i >= 0; i--)
            if (current > Milestones[i]) return Prizes[Milestones[i]];
        return "0";
    }

    void UseFiftyFifty()
    {
        if (locked) return;
        StartCoroutine(FiftyFiftyRoutine());
    }

    IEnumerator FiftyFiftyRoutine()
    {
        locked = true;
        fiftyFiftyButton.interactable = false;

        // 11. Lifeline SFX
        PlaySfx(lifelineClip);
        yield return WaitClip(lifelineClip);

        var q = questions[current];
        var wrong = new List<int>();
        for (int i = 0; i < 4; i++) if (i != q.correctIndex) wrong.Add(i);

        // 12. Lifeline Result SFX
        PlaySfx(lifelineResultClip);
        for (int k = 0; k < 2; k++)
        {
            int pick = Random.Range(0, wrong.Count);
            int idx = wrong[pick];
            wrong.RemoveAt(pick);
            answerButtons[idx].interactable = false;
            answerLabels[idx].text = "";
        }
        yield return WaitClip(lifelineResultClip);

        locked = false;
    }

    // 14-15: Victory / End Music -> Closing Theme
    void EndGame(string message, bool won)
    {
        // current = số câu đã trả lời đúng (thua: câu hiện tại là câu sai)
        ProgressSaveSystem.RecordSession(ProgressSaveSystem.SubjectHoa, current * 10, current, won ? 0 : 1, won, Time.timeSinceLevelLoad);

        endPanel.SetActive(true);
        endText.text = message;
        StartCoroutine(EndAudioRoutine(won));
    }

    IEnumerator EndAudioRoutine(bool won)
    {
        AudioClip clip = won ? victoryClip : endClip;   // 14A / 14B
        PlayMusic(clip, false);
        yield return WaitClip(clip);
        PlayMusic(closingTheme, true);                  // 15. Closing Theme
    }

    public void OnClickBackToMenu() => SceneManager.LoadScene(SubjectSession.BackSceneOr(SubjectId.Hoa, menuSceneName));

    // ---------- TIỆN ÍCH ÂM THANH ----------

    void PlayMusic(AudioClip clip, bool loop)
    {
        if (musicSource == null) return;
        musicSource.Stop();
        if (clip == null) return;
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }

    void PlaySfx(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }

    // Chờ hết độ dài clip (không có clip thì bỏ qua, không bị đứng game)
    IEnumerator WaitClip(AudioClip clip)
    {
        if (clip != null) yield return new WaitForSeconds(clip.length);
    }
}