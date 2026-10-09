using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

public class ProblemStore
{
    readonly Dictionary<string, EssayProblem> byId;
    public ProblemStore(IConfiguration cfg, IHostEnvironment env)
    {
        string path = Path.Combine(AppContext.BaseDirectory, cfg["Essay:ProblemsPath"] ?? "essay_problems.json");
        var opt = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true };
        var set = JsonSerializer.Deserialize<EssayProblemSet>(File.ReadAllText(path), opt);
        byId = set.problems.ToDictionary(p => p.id);
    }
    public EssayProblem Find(string id) => id != null && byId.TryGetValue(id, out var p) ? p : null;
}

/// <summary>Giới hạn lượt chấm mỗi ngày theo học sinh và toàn hệ thống (đòn bẩy chi phí, mục 11.3).</summary>
public class UsageLimiter
{
    readonly int perStudent, global;
    readonly object gate = new object();
    DateOnly day = DateOnly.FromDateTime(DateTime.UtcNow);
    int total; readonly Dictionary<string, int> perCode = new Dictionary<string, int>();

    public UsageLimiter(IConfiguration cfg)
    {
        perStudent = cfg.GetValue("Essay:DailyGradePerStudent", 30);
        global = cfg.GetValue("Essay:DailyGradeGlobal", 2000);
    }

    public bool TryConsume(string code, out string why)
    {
        lock (gate)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (today != day) { day = today; total = 0; perCode.Clear(); }
            perCode.TryGetValue(code, out int n);
            if (n >= perStudent) { why = "Hôm nay em đã dùng hết lượt chấm bằng Thầy Cú. Mai quay lại nhé."; return false; }
            if (total >= global) { why = "Thầy Cú đang quá tải hôm nay. Em thử lại sau."; return false; }
            perCode[code] = n + 1; total++; why = null; return true;
        }
    }
}

/// <summary>Ghi JSONL: không lưu tên hay lớp. Thời hạn giữ nhật ký sẽ chốt ở E6.</summary>
public class AttemptLog
{
    readonly string dir; readonly object gate = new object();
    public AttemptLog(IConfiguration cfg)
    {
        dir = Path.Combine(AppContext.BaseDirectory, cfg["Essay:LogDir"] ?? "logs");
        Directory.CreateDirectory(dir);
    }
    public void Append(string file, object row)
    {
        string line = JsonSerializer.Serialize(row, new JsonSerializerOptions { IncludeFields = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        lock (gate) File.AppendAllText(Path.Combine(dir, file), line + "\n", Encoding.UTF8);
    }
}

public class GeminiResult { public string Text; public int InputTokens, OutputTokens; }

/// <summary>Gọi Gemini generateContent, ép trả JSON theo schema. Model đặt trong appsettings (Gemini:Model).</summary>
public class GeminiClient
{
    readonly HttpClient http; readonly string apiKey, model, endpoint;

    public GeminiClient(HttpClient http, IConfiguration cfg)
    {
        this.http = http;
        apiKey = cfg["Gemini:ApiKey"];
        model = cfg["Gemini:Model"] ?? "gemini-3.8-flash";
        endpoint = (cfg["Gemini:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta").TrimEnd('/');
        http.Timeout = TimeSpan.FromSeconds(cfg.GetValue("Gemini:TimeoutSeconds", 25));
    }

    public async Task<GeminiResult> GenerateJsonAsync(string system, string user, JsonNode schema)
    {
        if (string.IsNullOrEmpty(apiKey)) throw new InvalidOperationException("Chưa cấu hình Gemini:ApiKey.");
        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = system }) },
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = user })
            }),
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0.2,
                ["responseMimeType"] = "application/json",
                ["responseJsonSchema"] = schema
            }
        };

        for (int attempt = 0; ; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/models/{model}:generateContent")
            { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
            req.Headers.Add("x-goog-api-key", apiKey);
            using var resp = await http.SendAsync(req);
            string raw = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                var root = JsonNode.Parse(raw);
                var parts = root?["candidates"]?[0]?["content"]?["parts"]?.AsArray();
                if (parts == null) throw new InvalidOperationException("Gemini không trả nội dung (có thể bị chặn an toàn).");
                var sb = new StringBuilder();
                foreach (var part in parts) sb.Append((string)part?["text"]);
                return new GeminiResult
                {
                    Text = sb.ToString(),
                    InputTokens = (int?)root["usageMetadata"]?["promptTokenCount"] ?? 0,
                    OutputTokens = (int?)root["usageMetadata"]?["candidatesTokenCount"] ?? 0
                };
            }
            bool retry = (int)resp.StatusCode == 429 || (int)resp.StatusCode >= 500;
            if (!retry || attempt >= 1) throw new HttpRequestException($"Gemini {(int)resp.StatusCode}: {raw.Truncate(300)}");
            await Task.Delay(1200);
        }
    }
}
