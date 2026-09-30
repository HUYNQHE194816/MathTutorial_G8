using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class AIService : MonoBehaviour
{
    public static AIService Instance;

    [Header("API Config")]
    [SerializeField] private string apiKey = "";
    private const string GEMINI_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent";

    // Cấu trúc Data JSON theo chuẩn Gemini API
    [Serializable]
    private class GeminiRequestBody
    {
        public ContentItem[] contents;
    }

    [Serializable]
    private class ContentItem
    {
        public PartItem[] parts;
    }

    [Serializable]
    private class PartItem
    {
        public string text;
    }

    [Serializable]
    private class GeminiResponse
    {
        public CandidateItem[] candidates;
    }

    [Serializable]
    private class CandidateItem
    {
        public ContentItem content;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void RequestExplanation(WrongQuestionItem item, Action<string> onComplete, Action<string> onError)
    {
        StartCoroutine(SendAIRoutine(item, onComplete, onError));
    }

    private IEnumerator SendAIRoutine(WrongQuestionItem item, Action<string> onComplete, Action<string> onError)
    {
        string chosenText = (item.chosenIndex >= 0 && item.options != null && item.chosenIndex < item.options.Length)
            ? $"{"ABCD"[item.chosenIndex]}. {item.options[item.chosenIndex]}"
            : "Hết thời gian (chưa chọn)";

        string correctText = (item.correctIndex >= 0 && item.options != null && item.correctIndex < item.options.Length)
            ? $"{"ABCD"[item.correctIndex]}. {item.options[item.correctIndex]}"
            : "N/A";

        string optA = (item.options != null && item.options.Length > 0) ? item.options[0] : "";
        string optB = (item.options != null && item.options.Length > 1) ? item.options[1] : "";
        string optC = (item.options != null && item.options.Length > 2) ? item.options[2] : "";
        string optD = (item.options != null && item.options.Length > 3) ? item.options[3] : "";

        string prompt = $@"Bạn là gia sư Hóa học/Toán học lớp 8 thân thiện và dễ hiểu.
Học sinh vừa làm SAI câu hỏi sau:
- Câu hỏi: {item.questionText}
- Các lựa chọn: A. {optA}, B. {optB}, C. {optC}, D. {optD}
- Lựa chọn của học sinh: {chosenText} (SAI)
- Đáp án đúng: {correctText}

Hãy phân tích ngắn gọn theo 3 mục:
1. Vì sao chọn như vậy là chưa đúng (Chỉ ra lỗi nhầm lẫn).
2. Cách suy luận và giải chi tiết để chọn đáp án đúng.
3. Mẹo nhớ nhanh hoặc công thức cần ghi nhớ.";

        GeminiRequestBody reqBody = new GeminiRequestBody
        {
            contents = new[]
            {
                new ContentItem
                {
                    parts = new[]
                    {
                        new PartItem { text = prompt }
                    }
                }
            }
        };

        string jsonBody = JsonUtility.ToJson(reqBody);

        using (UnityWebRequest request = new UnityWebRequest(GEMINI_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-goog-api-key", apiKey);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                string cleanText = ExtractGeminiContent(responseText);
                onComplete?.Invoke(cleanText);
            }
            else
            {
                string errDetail = request.downloadHandler != null ? request.downloadHandler.text : request.error;
                onError?.Invoke($"Lỗi ({request.responseCode}): {errDetail}");
            }
        }
    }

    private string ExtractGeminiContent(string rawJson)
    {
        try
        {
            GeminiResponse res = JsonUtility.FromJson<GeminiResponse>(rawJson);
            if (res != null && res.candidates != null && res.candidates.Length > 0
                && res.candidates[0].content != null && res.candidates[0].content.parts != null
                && res.candidates[0].content.parts.Length > 0)
            {
                return res.candidates[0].content.parts[0].text;
            }
        }
        catch (Exception)
        {
        }

        // Fallback string extraction
        int textIndex = rawJson.IndexOf("\"text\": \"");
        if (textIndex != -1)
        {
            int start = textIndex + 9;
            int end = rawJson.IndexOf("\"", start);
            if (end > start)
            {
                string res = rawJson.Substring(start, end - start);
                return System.Text.RegularExpressions.Regex.Unescape(res);
            }
        }

        return rawJson;
    }
}
