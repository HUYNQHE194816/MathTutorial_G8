using System.Text.Json;

// Server trung gian cho tự luận (E3). Khóa Gemini chỉ nằm ở đây, game không giữ khóa.
// Chạy: set Gemini__ApiKey=... rồi dotnet run   (Development: có thêm GET /dev/variant cho bộ chạy thử)

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.IncludeFields = true;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.AddHttpClient<GeminiClient>();
builder.Services.AddSingleton<ProblemStore>();
builder.Services.AddSingleton<UsageLimiter>();
builder.Services.AddSingleton<AttemptLog>();
builder.Services.AddSingleton<GradingService>();

var app = builder.Build();
var cfg = app.Configuration;

// Khóa ứng dụng đơn giản. Đăng nhập học sinh thật làm ở E6.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/essay"))
    {
        string key = cfg["Essay:ClientKey"];
        if (string.IsNullOrEmpty(key) || ctx.Request.Headers["X-Client-Key"] != key)
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new { error = "unauthorized" });
            return;
        }
    }
    await next();
});

app.MapGet("/health", () => new { ok = true });

// Bản số liệu của một học sinh: chỉ trả đề, KHÔNG trả đáp án hay barem đã thế số.
app.MapGet("/essay/variant", (string problemId, string studentCode, int? attempt, ProblemStore store) =>
{
    var p = store.Find(problemId);
    if (p == null) return Results.NotFound(new { error = "problem_not_found" });
    int seed = EssayVariantGenerator.SeedFor(Sanitize.Code(studentCode), p.id, attempt ?? 0);
    var v = EssayVariantGenerator.Generate(p, seed);
    return Results.Ok(new
    {
        problemId = p.id, seed, statement = v.statement, maxScore = p.MaxScore, scaffold = p.scaffold,
        steps = p.rubric.Select(r => new { id = r.id, label = r.label, points = r.points }),
        hintCount = p.hints?.Length ?? 0
    });
});

app.MapPost("/essay/grade", async (GradeRequest req, GradingService svc, UsageLimiter limiter, ProblemStore store) =>
{
    var p = store.Find(req?.problemId);
    if (p == null) return Results.NotFound(new { error = "problem_not_found" });
    if (req.lines == null || req.lines.Length == 0 || req.lines.Length > 40 ||
        req.lines.All(string.IsNullOrWhiteSpace) || req.lines.Any(l => (l ?? "").Length > 400))
        return Results.BadRequest(new { error = "bad_lines", message = "Bài làm trống hoặc quá dài." });

    string code = Sanitize.Code(req.studentCode);
    if (!limiter.TryConsume(code, out string why))
        return Results.Json(new { error = "limit", message = why }, statusCode: 429);

    try { return Results.Ok(await svc.GradeAsync(p, req)); }
    catch (Exception e)
    {
        app.Logger.LogWarning("Chấm lỗi: {Msg}", e.Message);
        return Results.Json(new { error = "ai_unavailable", message = "Thầy Cú đang bận. Em thử lại hoặc chuyển sang bậc gõ phím." }, statusCode: 503);
    }
});

// Gợi ý bậc 1-3 lấy từ dữ liệu bài (miễn phí, đã duyệt). Bậc 4 = lời giải mẫu, chỉ khi học sinh chủ động xin.
app.MapPost("/essay/hint", (HintRequest req, ProblemStore store) =>
{
    var p = store.Find(req?.problemId);
    if (p == null) return Results.NotFound(new { error = "problem_not_found" });
    int n = p.hints?.Length ?? 0;
    if (req.level >= 1 && req.level <= n)
        return Results.Ok(new { level = req.level, text = p.hints[req.level - 1], countsForMastery = true });
    if (req.level == n + 1)
    {
        var v = EssayVariantGenerator.Generate(p, req.seed);
        return Results.Ok(new { level = req.level, steps = v.rubricDesc, countsForMastery = false });
    }
    return Results.BadRequest(new { error = "bad_level" });
});

app.MapPost("/essay/appeal", (AppealRequest req, AttemptLog log) =>
{
    if (string.IsNullOrWhiteSpace(req?.attemptId)) return Results.BadRequest(new { error = "bad_attempt" });
    log.Append("appeals.jsonl", new { time = DateTime.UtcNow, req.attemptId, reason = (req.reason ?? "").Trim().Truncate(300) });
    return Results.Ok(new { received = true });
});

// Chỉ Development: bộ chạy thử (run_cases.py) cần biết bài mẫu của một bản số liệu.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/dev/variant", (string problemId, int seed, ProblemStore store) =>
    {
        var p = store.Find(problemId);
        if (p == null) return Results.NotFound();
        var v = EssayVariantGenerator.Generate(p, seed);
        return Results.Ok(new { statement = v.statement, rubricDesc = v.rubricDesc, values = v.numbers, texts = v.texts });
    });
}

app.Run();

public class GradeRequest
{
    public string problemId { get; set; }
    public string studentCode { get; set; }     // chỉ để giới hạn lượt và sinh số liệu, KHÔNG gửi cho AI
    public int seed { get; set; }               // seed của bản số liệu (lấy từ /essay/variant)
    public string mode { get; set; } = "steps"; // "steps": lines[i] là bước i (bậc 1-2) | "free": các dòng bản chép (bậc 3)
    public string[] lines { get; set; }
}
public class HintRequest { public string problemId { get; set; } public int level { get; set; } public int seed { get; set; } }
public class AppealRequest { public string attemptId { get; set; } public string reason { get; set; } }

public static class Sanitize
{
    public static string Code(string s)
    {
        s = new string((s ?? "").Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
        return s.Length == 0 ? "anon" : Truncate(s, 40);
    }
    public static string Truncate(this string s, int n) => s != null && s.Length > n ? s.Substring(0, n) : s;
}
