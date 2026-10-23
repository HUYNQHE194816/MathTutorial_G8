// Danh sách model phải khớp với mảng "models" trong AIService.cs
const ALLOWED_MODELS = [
  "gemini-3.5-flash-lite",
  "gemini-3.1-flash-lite",
  "gemini-3.6-flash",
];
const MAX_BODY_CHARS = 8_000_000; // đủ cho request có ảnh base64

Deno.serve(async (req) => {
  if (req.method !== "POST") {
    return new Response("Method not allowed", { status: 405 });
  }

  // Chỉ nhận request kèm đúng publishable key
  const appKey = Deno.env.get("APP_APIKEY");
  if (appKey && req.headers.get("apikey") !== appKey) {
    return new Response("Unauthorized", { status: 401 });
  }

  const model = new URL(req.url).searchParams.get("model") ?? "";
  if (!ALLOWED_MODELS.includes(model)) {
    return new Response("Bad model", { status: 400 });
  }

  const body = await req.text();
  if (body.length > MAX_BODY_CHARS) {
    return new Response("Body too large", { status: 413 });
  }

  const r = await fetch(
    `https://generativelanguage.googleapis.com/v1beta/models/${model}:generateContent`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "x-goog-api-key": Deno.env.get("GEMINI_API_KEY")!,
      },
      body,
    },
  );

  // Trả nguyên status của Gemini để logic retry 429/503 trong Unity vẫn chạy
  return new Response(await r.text(), {
    status: r.status,
    headers: { "Content-Type": "application/json" },
  });
});