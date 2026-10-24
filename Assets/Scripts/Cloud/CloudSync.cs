using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Một lượt chơi gửi lên Supabase (tên trường trùng tên cột của bảng public.results).</summary>
[Serializable]
public class CloudResult
{
    public string client_uid;      // mã duy nhất của lượt chơi: gửi lại nhiều lần cũng không bị lưu trùng
    public string created_at;      // UTC, định dạng ISO 8601
    public string student_name, student_class, nickname;
    public string mode;            // "trac_nghiem" | "tu_luan"
    public string subject, chapter, lesson;
    public int score, correct, wrong, seconds;
    public bool won;
}

/// <summary>Một dòng bảng xếp hạng (view public.leaderboard / leaderboard_total).</summary>
[Serializable]
public class LeaderRow
{
    public string display_name, student_class, subject, last_played;
    public int best_score, attempts;
}

/// <summary>Một lượt chơi gần đây của mọi người chơi (view public.recent_results).</summary>
[Serializable]
public class RecentRow
{
    public string created_at, display_name, student_class, mode, subject, chapter, lesson;
    public int score, correct, wrong, seconds;
    public bool won;
}

[Serializable] class LeaderList { public List<LeaderRow> rows; }
[Serializable] class RecentList { public List<RecentRow> rows; }
[Serializable] class PendingFile { public List<CloudResult> items = new List<CloudResult>(); }

/// <summary>
/// Đồng bộ kết quả lên Supabase qua REST (không cần SDK). Tự khởi tạo khi mở game, sống xuyên mọi scene.
///  - Submit(): xếp kết quả vào hàng đợi lưu trên máy (cloud_pending.json) rồi gửi nền. Mất mạng thì giữ lại, 30 giây thử lại,
///    lần mở game sau vẫn còn. Không bao giờ làm đứng game.
///  - FetchLeaderboard() / FetchRecent(): lấy bảng xếp hạng và lịch sử của tất cả người chơi.
/// Chưa điền CloudConfig thì mọi hàm bỏ qua im lặng.
/// </summary>
public class CloudSync : MonoBehaviour
{
    /// <summary>Đặt true tạm thời để KHÔNG gửi lên mạng (dùng cho dữ liệu mẫu khi thử).</summary>
    public static bool Suppress;

    const int MaxPending = 500, BatchSize = 50;
    static CloudSync inst;
    static readonly List<CloudResult> pending = new List<CloudResult>();
    static bool flushing;
    static float nextTry;

    public static bool Enabled => CloudConfig.Configured;
    public static int PendingCount => pending.Count;
    static string PendingPath => Path.Combine(Application.persistentDataPath, "cloud_pending.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { inst = null; pending.Clear(); flushing = false; nextTry = 0f; Suppress = false; warnedConfig = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot() { if (Enabled) Ensure(); }

    static void Ensure()
    {
        if (inst != null) return;
        var go = new GameObject("CloudSync");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<CloudSync>();
        LoadPending();
    }

    // ------------------------------------------------------------ gửi kết quả
    static bool warnedConfig;

    public static void Submit(CloudResult r)
    {
        if (Suppress || r == null) return;
        if (!Enabled)
        {
            if (!warnedConfig) { warnedConfig = true; Debug.LogWarning("[Cloud] Chưa điền Url/AnonKey trong CloudConfig.cs nên kết quả chưa được gửi lên Supabase."); }
            return;
        }
        Ensure();
        r.client_uid = Guid.NewGuid().ToString();
        r.created_at = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        pending.Add(r);
        if (pending.Count > MaxPending) pending.RemoveRange(0, pending.Count - MaxPending);
        SavePending();
        nextTry = 0f;   // gửi ngay ở frame sau
    }

    void Update()
    {
        if (flushing || pending.Count == 0 || Time.realtimeSinceStartup < nextTry) return;
        flushing = true;
        StartCoroutine(FlushRoutine());
    }

    IEnumerator FlushRoutine()
    {
        int take = BatchSize;
        while (pending.Count > 0)
        {
            yield return SupabaseAuth.EnsureFresh();   // token sắp hết hạn thì làm mới trước khi gửi
            var batch = pending.GetRange(0, Mathf.Min(take, pending.Count));
            var sb = new StringBuilder("[");
            for (int i = 0; i < batch.Count; i++) { if (i > 0) sb.Append(','); sb.Append(JsonUtility.ToJson(batch[i])); }
            sb.Append(']');

            using (var req = new UnityWebRequest(CloudConfig.Url.TrimEnd('/') + "/rest/v1/results", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(sb.ToString()));
                req.downloadHandler = new DownloadHandlerBuffer();
                Headers(req);
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Prefer", "return=minimal");
                req.timeout = 15;
                yield return req.SendWebRequest();

                long code = req.responseCode;
                bool ok = req.result == UnityWebRequest.Result.Success;
                if (ok) Debug.Log("[Cloud] Đã gửi " + batch.Count + " kết quả lên Supabase.");
                else Debug.LogWarning("[Cloud] Gửi kết quả lỗi " + code + ": " + req.error + " " + req.downloadHandler.text);

                if (!ok && code == 409 && batch.Count > 1) { take = 1; continue; }   // có lượt đã tồn tại: gửi từng lượt một để lượt mới không bị vạ lây
                bool drop = !ok && (code == 400 || code == 409 || code == 422);       // 409 = đã có rồi, 400/422 = dữ liệu bị từ chối: bỏ để không kẹt hàng đợi
                if (ok || drop) { pending.RemoveRange(0, batch.Count); SavePending(); }
                else { nextTry = Time.realtimeSinceStartup + 30f; flushing = false; yield break; }
            }
        }
        flushing = false;
    }

    // ------------------------------------------------------------ lấy dữ liệu
    /// <summary>subject: "Tổng" (cộng điểm cao nhất mỗi môn), "Lý", "Hóa", "Sinh" hoặc "Tự luận". done(rows, lỗi): lỗi = null nếu thành công.</summary>
    public static void FetchLeaderboard(string subject, int limit, Action<List<LeaderRow>, string> done)
    {
        if (!Enabled) { if (done != null) done(null, "Chưa cấu hình Supabase (file CloudConfig.cs)."); return; }
        Ensure();
        string path = subject == "Tổng"
            ? "/rest/v1/leaderboard_total?select=*"
            : "/rest/v1/leaderboard?select=*&subject=eq." + Uri.EscapeDataString(subject);
        path += "&order=best_score.desc,last_played.asc&limit=" + Mathf.Clamp(limit, 1, 200);
        inst.StartCoroutine(Get(path, (body, err) =>
        {
            if (err != null) { if (done != null) done(null, err); return; }
            try
            {
                var w = JsonUtility.FromJson<LeaderList>("{\"rows\":" + body + "}");
                if (done != null) done(w != null && w.rows != null ? w.rows : new List<LeaderRow>(), null);
            }
            catch (Exception e) { if (done != null) done(null, "Dữ liệu trả về bị lỗi: " + e.Message); }
        }));
    }

    /// <summary>Lịch sử các lượt chơi mới nhất của tất cả người chơi. subject = null/rỗng: lấy mọi môn.</summary>
    public static void FetchRecent(string subject, int limit, Action<List<RecentRow>, string> done)
    {
        if (!Enabled) { if (done != null) done(null, "Chưa cấu hình Supabase (file CloudConfig.cs)."); return; }
        Ensure();
        string path = "/rest/v1/recent_results?select=*";
        if (!string.IsNullOrEmpty(subject)) path += "&subject=eq." + Uri.EscapeDataString(subject);
        path += "&order=created_at.desc&limit=" + Mathf.Clamp(limit, 1, 200);
        inst.StartCoroutine(Get(path, (body, err) =>
        {
            if (err != null) { if (done != null) done(null, err); return; }
            try
            {
                var w = JsonUtility.FromJson<RecentList>("{\"rows\":" + body + "}");
                if (done != null) done(w != null && w.rows != null ? w.rows : new List<RecentRow>(), null);
            }
            catch (Exception e) { if (done != null) done(null, "Dữ liệu trả về bị lỗi: " + e.Message); }
        }));
    }

    static IEnumerator Get(string path, Action<string, string> done)
    {
        yield return SupabaseAuth.EnsureFresh();
        using (var req = UnityWebRequest.Get(CloudConfig.Url + path))
        {
            Headers(req);
            req.timeout = 10;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
                done(null, "Không kết nối được máy chủ (" + req.responseCode + " " + req.error + ")");
            else done(req.downloadHandler.text, null);
        }
    }

    static void Headers(UnityWebRequest r)
    {
        SupabaseAuth.ApplyHeaders(r);   // có đăng nhập thì gửi kèm token của người dùng, chưa thì dùng khóa anon
    }

    // ------------------------------------------------------------ hàng đợi lưu trên máy
    static void LoadPending()
    {
        try
        {
            if (!File.Exists(PendingPath)) return;
            var f = JsonUtility.FromJson<PendingFile>(File.ReadAllText(PendingPath, Encoding.UTF8));
            if (f != null && f.items != null) pending.AddRange(f.items);
        }
        catch (Exception e) { Debug.LogWarning("[Cloud] Không đọc được hàng đợi: " + e.Message); }
    }

    static void SavePending()
    {
        try { File.WriteAllText(PendingPath, JsonUtility.ToJson(new PendingFile { items = pending }), Encoding.UTF8); }
        catch (Exception e) { Debug.LogWarning("[Cloud] Không lưu được hàng đợi: " + e.Message); }
    }
}
