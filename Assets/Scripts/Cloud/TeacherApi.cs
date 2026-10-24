using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Gọi Supabase (REST) cho màn quản lý bài giảng của giáo viên: bài giảng (lessons) và câu hỏi (questions).
/// Mọi hàm là coroutine, kết quả trả qua Res. Quyền thật sự do RLS của Supabase kiểm tra.
/// </summary>
public static class TeacherApi
{
    public const string Draft = "draft", Published = "published";

    // ------------------------------------------------------------ dữ liệu
    [Serializable]
    public class Lesson
    {
        public string id, owner_id, subject, chapter, title, content, status;
        public int grade = 8, sort_order;
    }

    [Serializable]
    public class Question
    {
        public string id, lesson_id, text, option_a, option_b, option_c, option_d, answer, level, explanation;
        public int position;
    }

    [Serializable] class LessonRows { public List<Lesson> rows; }
    [Serializable] class QuestionRows { public List<Question> rows; }
    [Serializable] class PosRow { public string lesson_id; public int position; }
    [Serializable] class PosRows { public List<PosRow> rows; }
    [Serializable] class ErrBody { public string code, message, details, hint; }

    [Serializable]
    class LessonIn
    {
        public string owner_id, subject, chapter, title, content, status;
        public int grade, sort_order;
    }

    [Serializable]
    class LessonPatch
    {
        public string subject, chapter, title, content, status;
        public int grade, sort_order;
    }

    [Serializable]
    class QuestionIn
    {
        public string lesson_id, text, option_a, option_b, option_c, option_d, answer, level, explanation;
        public int position;
    }

    [Serializable]
    class QuestionPatch
    {
        public string text, option_a, option_b, option_c, option_d, answer, level, explanation;
    }

    public class Res { public bool ok; public string body, error; }

    // ------------------------------------------------------------ chạy coroutine không phụ thuộc cửa sổ
    class Runner : MonoBehaviour { }
    static Runner runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { runner = null; }

    /// <summary>Chạy trên một object sống xuyên scene: đóng cửa sổ giữa chừng thì việc đang lưu vẫn chạy nốt.</summary>
    public static Coroutine Run(IEnumerator routine)
    {
        if (runner == null)
        {
            var go = new GameObject("TeacherApi");
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }
        return runner.StartCoroutine(routine);
    }

    // ------------------------------------------------------------ gửi request
    public static IEnumerator Call(string method, string path, string json, string prefer, Res r)
    {
        r.ok = false; r.body = ""; r.error = null;
        if (!CloudConfig.Configured) { r.error = "Chưa cấu hình Supabase."; yield break; }
        if (!SupabaseAuth.IsTeacher) { r.error = "Cần đăng nhập bằng tài khoản giáo viên."; yield break; }

        yield return SupabaseAuth.EnsureFresh();
        if (!SupabaseAuth.IsTeacher) { r.error = "Phiên đăng nhập đã hết hạn, hãy đăng nhập lại."; yield break; }

        using (var req = new UnityWebRequest(CloudConfig.Url.TrimEnd('/') + path, method))
        {
            if (json != null) req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            SupabaseAuth.ApplyHeaders(req);
            if (json != null) req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(prefer)) req.SetRequestHeader("Prefer", prefer);
            req.timeout = 25;
            yield return req.SendWebRequest();

            string text = req.downloadHandler != null ? req.downloadHandler.text : "";
            if (req.result == UnityWebRequest.Result.Success) { r.ok = true; r.body = text; }
            else r.error = Explain(req.responseCode, text, req.error);
        }
    }

    static string Explain(long code, string text, string netError)
    {
        if (code == 0) return "Không kết nối được máy chủ.";
        string m = null, c = null;
        try
        {
            var e = JsonUtility.FromJson<ErrBody>(text);
            if (e != null) { m = e.message; c = e.code; if (!string.IsNullOrEmpty(e.details)) m += " (" + e.details + ")"; }
        }
        catch { }
        if (code == 401 || code == 403 || c == "42501") return "Không có quyền thực hiện (hãy đăng nhập lại bằng tài khoản giáo viên).";
        if (string.IsNullOrEmpty(m)) m = netError;
        return (m ?? "Lỗi không rõ") + " [" + code + (string.IsNullOrEmpty(c) ? "" : "/" + c) + "]";
    }

    static string Id(string s) { return Uri.EscapeDataString(s ?? ""); }

    // ------------------------------------------------------------ bài giảng
    public static IEnumerator LoadLessons(List<Lesson> into, Res r)
    {
        yield return Call("GET", "/rest/v1/lessons?select=*&owner_id=eq." + Id(SupabaseAuth.UserId)
            + "&order=subject.asc,sort_order.asc,created_at.asc&limit=1000", null, null, r);
        if (!r.ok) yield break;
        ParseLessons(r, into);
    }

    static void ParseLessons(Res r, List<Lesson> into)
    {
        try
        {
            var w = JsonUtility.FromJson<LessonRows>("{\"rows\":" + r.body + "}");
            into.Clear();
            if (w != null && w.rows != null) into.AddRange(w.rows);
        }
        catch (Exception e) { r.ok = false; r.error = "Dữ liệu trả về bị lỗi: " + e.Message; }
    }

    static LessonIn ToIn(Lesson l)
    {
        return new LessonIn
        {
            owner_id = SupabaseAuth.UserId, subject = l.subject, chapter = l.chapter ?? "", title = l.title ?? "",
            content = l.content ?? "", status = string.IsNullOrEmpty(l.status) ? Draft : l.status,
            grade = l.grade, sort_order = l.sort_order
        };
    }

    /// <summary>l.id rỗng: tạo mới (l.id được điền sau khi tạo). Có id: cập nhật.</summary>
    public static IEnumerator SaveLesson(Lesson l, Res r)
    {
        if (string.IsNullOrEmpty(l.id))
        {
            yield return Call("POST", "/rest/v1/lessons", "[" + JsonUtility.ToJson(ToIn(l)) + "]", "return=representation", r);
            if (!r.ok) yield break;
            var got = new List<Lesson>();
            ParseLessons(r, got);
            if (r.ok && got.Count > 0) l.id = got[0].id;
        }
        else
        {
            var p = new LessonPatch
            {
                subject = l.subject, chapter = l.chapter ?? "", title = l.title ?? "", content = l.content ?? "",
                status = string.IsNullOrEmpty(l.status) ? Draft : l.status, grade = l.grade, sort_order = l.sort_order
            };
            yield return Call("PATCH", "/rest/v1/lessons?id=eq." + Id(l.id), JsonUtility.ToJson(p), "return=minimal", r);
        }
    }

    /// <summary>Tạo nhiều bài một lần (dùng khi nhập CSV). created nhận lại các bài vừa tạo (có id).</summary>
    public static IEnumerator CreateLessons(List<Lesson> items, List<Lesson> created, Res r)
    {
        created.Clear();
        var sb = new StringBuilder("[");
        for (int i = 0; i < items.Count; i++) { if (i > 0) sb.Append(','); sb.Append(JsonUtility.ToJson(ToIn(items[i]))); }
        sb.Append(']');
        yield return Call("POST", "/rest/v1/lessons", sb.ToString(), "return=representation", r);
        if (!r.ok) yield break;
        ParseLessons(r, created);
    }

    /// <summary>Xóa câu hỏi của bài rồi xóa bài.</summary>
    public static IEnumerator DeleteLesson(string id, Res r)
    {
        yield return DeleteLessonQuestions(id, r);
        if (!r.ok) yield break;
        yield return Call("DELETE", "/rest/v1/lessons?id=eq." + Id(id), null, "return=minimal", r);
    }

    // ------------------------------------------------------------ câu hỏi
    public static IEnumerator LoadQuestions(string lessonId, List<Question> into, Res r)
    {
        yield return Call("GET", "/rest/v1/questions?select=*&lesson_id=eq." + Id(lessonId)
            + "&order=position.asc,created_at.asc&limit=1000", null, null, r);
        if (!r.ok) yield break;
        try
        {
            var w = JsonUtility.FromJson<QuestionRows>("{\"rows\":" + r.body + "}");
            into.Clear();
            if (w != null && w.rows != null) into.AddRange(w.rows);
        }
        catch (Exception e) { r.ok = false; r.error = "Dữ liệu trả về bị lỗi: " + e.Message; }
    }

    /// <summary>Vị trí lớn nhất hiện có của từng bài (để nối thêm câu hỏi vào cuối).</summary>
    public static IEnumerator MaxPositions(List<string> lessonIds, Dictionary<string, int> into, Res r)
    {
        into.Clear();
        r.ok = true; r.error = null;
        if (lessonIds.Count == 0) yield break;
        var ids = new StringBuilder();
        for (int i = 0; i < lessonIds.Count; i++) { if (i > 0) ids.Append(','); ids.Append(Id(lessonIds[i])); }
        yield return Call("GET", "/rest/v1/questions?select=lesson_id,position&lesson_id=in.(" + ids + ")&order=position.desc&limit=1000", null, null, r);
        if (!r.ok) yield break;
        try
        {
            var w = JsonUtility.FromJson<PosRows>("{\"rows\":" + r.body + "}");
            if (w != null && w.rows != null)
                foreach (var p in w.rows)
                {
                    int cur;
                    if (!into.TryGetValue(p.lesson_id, out cur) || p.position > cur) into[p.lesson_id] = p.position;
                }
        }
        catch (Exception e) { r.ok = false; r.error = "Dữ liệu trả về bị lỗi: " + e.Message; }
    }

    /// <summary>Thêm nhiều câu hỏi (mỗi câu cần lesson_id và position). Gửi từng đợt 100 câu.</summary>
    public static IEnumerator InsertQuestions(List<Question> qs, Res r)
    {
        r.ok = true; r.error = null;
        const int Batch = 100;
        for (int start = 0; start < qs.Count; start += Batch)
        {
            int end = Mathf.Min(start + Batch, qs.Count);
            var sb = new StringBuilder("[");
            for (int i = start; i < end; i++)
            {
                var q = qs[i];
                var b = new QuestionIn
                {
                    lesson_id = q.lesson_id, position = q.position, text = q.text, option_a = q.option_a, option_b = q.option_b,
                    option_c = q.option_c, option_d = q.option_d, answer = q.answer, level = q.level ?? "", explanation = q.explanation ?? ""
                };
                if (i > start) sb.Append(',');
                sb.Append(JsonUtility.ToJson(b));
            }
            sb.Append(']');
            yield return Call("POST", "/rest/v1/questions", sb.ToString(), "return=minimal", r);
            if (!r.ok) yield break;
        }
    }

    public static IEnumerator UpdateQuestion(Question q, Res r)
    {
        var p = new QuestionPatch
        {
            text = q.text, option_a = q.option_a, option_b = q.option_b, option_c = q.option_c, option_d = q.option_d,
            answer = q.answer, level = q.level ?? "", explanation = q.explanation ?? ""
        };
        yield return Call("PATCH", "/rest/v1/questions?id=eq." + Id(q.id), JsonUtility.ToJson(p), "return=minimal", r);
    }

    public static IEnumerator DeleteQuestion(string id, Res r)
    {
        yield return Call("DELETE", "/rest/v1/questions?id=eq." + Id(id), null, "return=minimal", r);
    }

    public static IEnumerator DeleteLessonQuestions(string lessonId, Res r)
    {
        yield return Call("DELETE", "/rest/v1/questions?lesson_id=eq." + Id(lessonId), null, "return=minimal", r);
    }
}
