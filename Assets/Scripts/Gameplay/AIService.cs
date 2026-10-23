using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;

// ───────────────── Kịch bản dạy kèm (AI sinh ra, game diễn lại) ─────────────────
[Serializable]
public class CoachStep
{
    public string type;       // "hint" | "guide_question" | "check_question" | "recap"
    public string text;
    public string[] options;  // chỉ dùng cho guide_question / check_question
    public int correct;
    public string onWrong;
}

[Serializable]
public class CoachScript
{
    public string misconception;
    public CoachStep[] steps;

    public static bool IsQuiz(CoachStep s) => s.type == "guide_question" || s.type == "check_question";

    public bool IsValid()
    {
        if (steps == null || steps.Length == 0) return false;
        foreach (var s in steps)
        {
            if (s == null || string.IsNullOrEmpty(s.type) || string.IsNullOrEmpty(s.text)) return false;
            if (IsQuiz(s) && (s.options == null || s.options.Length < 2 || s.correct < 0 || s.correct >= s.options.Length))
                return false;
        }
        return true;
    }
}

public class AIService : MonoBehaviour
{
    public static AIService Instance;

    [Header("API Config")]
    [Tooltip("KHÔNG commit key lên Git. Tốt nhất gọi qua backend proxy thay vì để key trong client.")]
    [SerializeField] private string apiKey = "";

    [Tooltip("Thử lần lượt từng model. Nếu model đầu hết số lần retry (429/503) thì chuyển sang model tiếp theo.")]
    [SerializeField] private string[] models = { 
    "gemini-3.5-flash-lite", 
    "gemini-3.1-flash-lite", 
    "gemini-3.6-flash" 
};

    [SerializeField] private int maxRetriesPerModel = 3;

    [Tooltip("Thời gian chờ tối đa cho mỗi request (giây): văn bản / ảnh.")]
    [SerializeField] private int timeoutText = 30, timeoutImage = 60;

    /// <summary>Đã có khóa API để gọi Gemini chưa (tự luận dùng để quyết định bật AI hay rơi về bộ chấm tạm).</summary>
    public bool HasKey => CloudConfig.Configured;

    private const string URL_FORMAT = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";

    // Chỉ cho 1 request chạy tại một thời điểm để tránh bắn dồn gây 429
    private bool busy;

    // ───────────── Cấu trúc JSON theo chuẩn Gemini API ─────────────
    [Serializable] private class GenConfig { public string responseMimeType; }

    [Serializable]
    private class GeminiRequestBody
    {
        public ContentItem[] contents;
        public GenConfig generationConfig;
    }

    [Serializable] private class ContentItem { public PartItem[] parts; }
    [Serializable] private class PartItem { public string text; }
    [Serializable] private class GeminiResponse { public CandidateItem[] candidates; }
    [Serializable] private class CandidateItem { public ContentItem content; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadKeyFromLocal();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>Nạp key từ ngoài Git: ai_key.txt cạnh thư mục Assets (Editor) hoặc cạnh file .exe (build Windows); nếu không có thì thử biến môi trường GEMINI_API_KEY.</summary>
    void LoadKeyFromLocal()
    {
        if (!string.IsNullOrWhiteSpace(apiKey)) return;   // đã gõ tay trong Inspector thì giữ nguyên
        string k = null;
        try
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ai_key.txt"));
            if (File.Exists(path)) k = File.ReadAllText(path);
        }
        catch (Exception e) { Debug.LogWarning("[AIService] Không đọc được ai_key.txt: " + e.Message); }
        if (string.IsNullOrWhiteSpace(k)) k = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrWhiteSpace(k)) apiKey = k.Trim();
    }

    // ───────────────────────── API công khai ─────────────────────────

    /// <summary>Giải thích dạng văn bản (chế độ cũ, dùng làm dự phòng).</summary>
    public void RequestExplanation(WrongQuestionItem item, Action<string> onComplete, Action<string> onError)
    {
        StartCoroutine(SendPrompt(BuildExplainPrompt(item), false, onComplete, onError));
    }

    /// <summary>Sinh kịch bản dạy kèm có cấu trúc để học sinh tự sửa lỗi (kèm theo suy nghĩ tự nhập của học sinh nếu có).</summary>
    public void RequestScript(WrongQuestionItem item, string studentThought, Action<CoachScript> onComplete, Action<string> onError)
    {
        StartCoroutine(SendPrompt(BuildScriptPrompt(item, studentThought), true,
            raw =>
            {
                CoachScript script = null;
                try { script = JsonUtility.FromJson<CoachScript>(CleanJson(raw)); }
                catch (Exception e) { Debug.LogWarning("Parse kịch bản lỗi: " + e.Message); }

                if (script != null && script.IsValid()) onComplete?.Invoke(script);
                else onError?.Invoke("AI trả về kịch bản không hợp lệ.");
            },
            onError));
    }

    public void RequestScript(WrongQuestionItem item, Action<CoachScript> onComplete, Action<string> onError)
    {
        RequestScript(item, null, onComplete, onError);
    }

    // ───────────────────────── Prompt ─────────────────────────

    /// <summary>Gửi một prompt tự do (tự luận: chấm bài, phân tích). json=true yêu cầu Gemini trả JSON.</summary>
    public void RequestRaw(string prompt, bool json, Action<string> onComplete, Action<string> onError, int attempts = 2)
    {
        StartCoroutine(SendPrompt(prompt, json, onComplete, onError, null, null, timeoutText, attempts));
    }

    /// <summary>Gửi prompt kèm một ảnh (đọc bài viết tay). Ảnh gửi thẳng cho Gemini, không lưu lại.</summary>
    public void RequestVision(string prompt, byte[] image, string mime, Action<string> onComplete, Action<string> onError, int attempts = 2)
    {
        StartCoroutine(SendPrompt(prompt, true, onComplete, onError, image, mime, timeoutImage, attempts));
    }

    /// <summary>Bỏ rào ```json nếu model lỡ thêm vào (dùng chung cho các lớp tự luận).</summary>
    public static string CleanJsonPublic(string raw) => CleanJson(raw);

    private static string JsonEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length + 16);
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private static string OptionText(WrongQuestionItem item, int index)
    {
        return (index >= 0 && item.options != null && index < item.options.Length)
            ? $"{"ABCD"[index]}. {item.options[index]}"
            : null;
    }

    private static string Opt(WrongQuestionItem item, int i)
        => (item.options != null && item.options.Length > i) ? item.options[i] : "";

    private string BuildExplainPrompt(WrongQuestionItem item)
    {
        string chosenText = OptionText(item, item.chosenIndex) ?? "Hết thời gian (chưa chọn)";
        string correctText = OptionText(item, item.correctIndex) ?? "N/A";

        // Giữ nguyên prompt giải thích cũ
        return $@"Bạn là gia sư lớp 8. Phân tích câu hỏi trắc nghiệm sau theo đúng khuôn mẫu.
LƯU Ý QUAN TRỌNG: Tuyệt đối không dùng ký hiệu LaTeX, không dùng \frac, \approx, \times hay dấu $. Hãy viết dạng chữ và số thông thường (dùng dấu / cho phép chia hoặc phân số).

[SAI] Nguyên nhân sai: (Chỉ ra lỗi học sinh chọn {chosenText} thay vì {correctText})
[GIAI] Hướng dẫn giải: (Trình bày các bước tính toán ngắn gọn bằng chữ/số thường)
[MEO] Mẹo ghi nhớ: (Công thức đơn giản)

Thông tin câu hỏi:
- Câu: {item.questionText}
- A. {Opt(item, 0)} | B. {Opt(item, 1)} | C. {Opt(item, 2)} | D. {Opt(item, 3)}
- Học sinh chọn: {chosenText} (SAI)
- Đáp án đúng: {correctText}";
    }

    // Dùng token <<...>> thay vì $@ để không phải escape dấu { } của JSON mẫu
    private const string SCRIPT_TEMPLATE = @"Bạn là gia sư lớp 8. Học sinh vừa làm sai một câu trắc nghiệm. Hãy soạn KỊCH BẢN dạy kèm để em TỰ sửa lỗi, KHÔNG đưa đáp án ngay từ đầu.
Quy tắc: tiếng Việt đơn giản, thân thiện; tuyệt đối không dùng LaTeX hay dấu $, viết công thức bằng chữ và số thông thường (dùng / cho phép chia); mỗi text tối đa 2 câu.
Chỉ trả về JSON đúng mẫu, không thêm chữ nào khác. Thứ tự steps:
1) hint mức nhẹ (chỉ nhắc lại khái niệm liên quan),
2) hint mức rõ hơn (gợi công thức hoặc ví dụ, vẫn chưa nói đáp án),
3) một guide_question dễ hơn, đúng 3 lựa chọn, giúp em tự suy ra chỗ mình nhầm,
4) một check_question tương tự câu gốc nhưng đổi số hoặc đổi tình huống, đúng 4 lựa chọn,
5) recap: một câu 'cần nhớ' ngắn gọn.
Với hint và recap để options là mảng rỗng, correct là 0, onWrong là chuỗi rỗng. Với guide_question và check_question, correct là chỉ số (bắt đầu từ 0) của đáp án đúng, onWrong là gợi ý thêm khi em chọn sai (không tiết lộ đáp án).
Mẫu JSON:
{""misconception"":""em đã nhầm ở đâu (dựa trên giải thích của học sinh)"",""steps"":[{""type"":""hint"",""text"":""..."",""options"":[],""correct"":0,""onWrong"":""""},{""type"":""guide_question"",""text"":""..."",""options"":[""..."",""..."",""...""],""correct"":0,""onWrong"":""...""}]}

Thông tin câu hỏi gốc:
- Câu: <<Q>>
- A. <<A>> | B. <<B>> | C. <<C>> | D. <<D>>
- Học sinh chọn: <<CH>> (SAI)
- Đáp án đúng: <<CO>>
- Học sinh giải thích suy nghĩ/lý do lúc chọn: <<THOUGHT>>";

    private string BuildScriptPrompt(WrongQuestionItem item, string studentThought = null)
    {
        string thought = string.IsNullOrWhiteSpace(studentThought) ? "Không có (học sinh chưa nói rõ)" : studentThought.Trim();
        return SCRIPT_TEMPLATE
            .Replace("<<Q>>", item.questionText ?? "")
            .Replace("<<A>>", Opt(item, 0))
            .Replace("<<B>>", Opt(item, 1))
            .Replace("<<C>>", Opt(item, 2))
            .Replace("<<D>>", Opt(item, 3))
            .Replace("<<CH>>", OptionText(item, item.chosenIndex) ?? "Hết thời gian (chưa chọn)")
            .Replace("<<CO>>", OptionText(item, item.correctIndex) ?? "N/A")
            .Replace("<<THOUGHT>>", thought);
    }

    // ───────────────────────── Gửi request + retry + fallback model ─────────────────────────

    private IEnumerator SendPrompt(string prompt, bool json, Action<string> onComplete, Action<string> onError,
                                   byte[] imageBytes = null, string imageMime = null, int timeoutSec = 0, int attemptsOverride = 0)
    {
        // Xếp hàng: chờ request trước xong
        while (busy) yield return null;
        busy = true;

        if (!CloudConfig.Configured)
        {
            busy = false;
            onError?.Invoke("Chưa cấu hình API key cho AIService.");
            yield break;
        }

        byte[] bodyRaw;
        if (imageBytes == null)
        {
            GeminiRequestBody reqBody = new GeminiRequestBody
            {
                contents = new[] { new ContentItem { parts = new[] { new PartItem { text = prompt } } } },
                generationConfig = new GenConfig { responseMimeType = json ? "application/json" : "text/plain" }
            };
            bodyRaw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(reqBody));
        }
        else
        {
            // JsonUtility luôn ghi mọi trường nên không dựng được phần inlineData; dựng JSON thủ công cho request có ảnh.
            var sbBody = new StringBuilder(imageBytes.Length * 4 / 3 + prompt.Length + 256);
            sbBody.Append("{\"contents\":[{\"parts\":[{\"text\":\"").Append(JsonEscape(prompt)).Append("\"},{\"inlineData\":{\"mimeType\":\"")
                  .Append(string.IsNullOrEmpty(imageMime) ? "image/jpeg" : imageMime).Append("\",\"data\":\"").Append(Convert.ToBase64String(imageBytes))
                  .Append("\"}}]}],\"generationConfig\":{\"responseMimeType\":\"").Append(json ? "application/json" : "text/plain").Append("\"}}");
            bodyRaw = Encoding.UTF8.GetBytes(sbBody.ToString());
        }
        int attemptsPerModel = attemptsOverride > 0 ? Mathf.Min(maxRetriesPerModel, attemptsOverride) : maxRetriesPerModel;
        int timeout = timeoutSec > 0 ? timeoutSec : timeoutText;

        string lastError = "";

        foreach (string model in models)
        {
            if (string.IsNullOrWhiteSpace(model)) continue;
            string url = CloudConfig.Url.TrimEnd('/') + "/functions/v1/ai-proxy?model=" + Uri.EscapeDataString(model.Trim());

            for (int attempt = 0; attempt < attemptsPerModel; attempt++)
            {
                using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("apikey", CloudConfig.AnonKey);
                    request.timeout = timeout;      // tránh kẹt hàng đợi khi mạng treo

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string text = ExtractGeminiContent(request.downloadHandler.text);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            busy = false;
                            onComplete?.Invoke(text);
                            yield break;
                        }
                        lastError = "AI trả về nội dung rỗng.";
                    }
                    else
                    {
                        long code = request.responseCode;
                        string body = request.downloadHandler != null ? request.downloadHandler.text : "";
                        lastError = $"[{model}] HTTP {code}: {(string.IsNullOrEmpty(body) ? request.error : body)}";

                        bool retriable = code == 503 || code == 429 || code == 500 || code == 504
                                         || request.result == UnityWebRequest.Result.ConnectionError;
                        if (!retriable)
                        {
                            // Lỗi không thể khắc phục bằng retry (key sai, request sai...)
                            busy = false;
                            onError?.Invoke(lastError);
                            yield break;
                        }
                    }
                }

                if (attempt < attemptsPerModel - 1)
                {
                    // Exponential backoff + jitter: ~2s, ~4s... để nhiều máy không retry cùng lúc
                    float wait = Mathf.Pow(2, attempt + 1) + UnityEngine.Random.Range(0f, 1f);
                    Debug.LogWarning($"AI quá tải ({model}). Thử lại lần {attempt + 1} sau {wait:0.0}s...");
                    yield return new WaitForSeconds(wait);
                }
            }

            Debug.LogWarning($"Model {model} hết lượt thử, chuyển model tiếp theo (nếu có).");
        }

        busy = false;
        onError?.Invoke($"Lỗi kết nối AI (đã thử {maxRetriesPerModel} lần/model):\n{lastError}");
    }

    // ───────────────────────── Đọc kết quả ─────────────────────────

    private string ExtractGeminiContent(string rawJson)
    {
        try
        {
            GeminiResponse res = JsonUtility.FromJson<GeminiResponse>(rawJson);
            if (res != null && res.candidates != null && res.candidates.Length > 0
                && res.candidates[0].content != null && res.candidates[0].content.parts != null)
            {
                // Nối tất cả các part (một số model trả nhiều part)
                var sb = new StringBuilder();
                foreach (var p in res.candidates[0].content.parts)
                    if (p != null && !string.IsNullOrEmpty(p.text)) sb.Append(p.text);
                if (sb.Length > 0) return sb.ToString();
            }
        }
        catch (Exception)
        {
        }

        // Fallback: tách chuỗi thủ công
        int textIndex = rawJson.IndexOf("\"text\": \"", StringComparison.Ordinal);
        if (textIndex != -1)
        {
            int start = textIndex + 9;
            int end = rawJson.IndexOf("\"", start, StringComparison.Ordinal);
            if (end > start)
                return Regex.Unescape(rawJson.Substring(start, end - start));
        }

        return "";
    }

    // Bỏ rào ```json ... ``` nếu model lỡ thêm vào
    private static string CleanJson(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        raw = raw.Trim();
        if (raw.StartsWith("```"))
        {
            int firstNewline = raw.IndexOf('\n');
            if (firstNewline >= 0) raw = raw.Substring(firstNewline + 1);
            int fence = raw.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0) raw = raw.Substring(0, fence);
        }
        int a = raw.IndexOf('{'), b = raw.LastIndexOf('}');
        return (a >= 0 && b > a) ? raw.Substring(a, b - a + 1) : raw;
    }
}
