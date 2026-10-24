using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Tải bài giảng ĐÃ XUẤT BẢN và câu hỏi của chúng từ Supabase cho một môn.
/// Làm hết sức: mất mạng, chưa cấu hình hoặc bị từ chối quyền thì trả về danh sách rỗng (game dùng CSV như cũ).
/// </summary>
public static class CloudQuestionBank
{
    public class Question { public string text, a, b, c, d, answer, level, explanation; }
    public class Lesson { public string chapter, title; public List<Question> questions = new List<Question>(); }

    [Serializable] class CLesson { public string id, chapter, title; public int sort_order; }
    [Serializable] class CLessonRows { public List<CLesson> rows; }
    [Serializable]
    class CQuestion { public string lesson_id, text, option_a, option_b, option_c, option_d, answer, level, explanation; public int position; }
    [Serializable] class CQuestionRows { public List<CQuestion> rows; }
    class Box { public string text; }

    const int Page = 1000, IdsPerRequest = 40;

    /// <summary>subject: "Lý" / "Hóa" / "Sinh". Kết quả theo thứ tự bài giảng, bài không có câu hỏi bị bỏ.</summary>
    public static IEnumerator Load(string subject, List<Lesson> into)
    {
        into.Clear();
        if (!CloudConfig.Configured) yield break;

        var box = new Box();
        yield return Get("/rest/v1/lessons?select=id,chapter,title,sort_order&status=eq.published&subject=eq."
            + Uri.EscapeDataString(subject) + "&order=sort_order.asc,created_at.asc&limit=" + Page, box);
        if (box.text == null) yield break;

        List<CLesson> ls = ParseLessons(box.text);
        if (ls == null || ls.Count == 0) yield break;

        var ordered = new List<Lesson>();
        var byId = new Dictionary<string, Lesson>();
        foreach (var l in ls)
        {
            var item = new Lesson { chapter = (l.chapter ?? "").Trim(), title = (l.title ?? "").Trim() };
            if (item.title.Length == 0) continue;
            byId[l.id] = item; ordered.Add(item);
        }

        for (int i = 0; i < ls.Count; i += IdsPerRequest)
        {
            var ids = new StringBuilder();
            for (int j = i; j < Mathf.Min(i + IdsPerRequest, ls.Count); j++) { if (ids.Length > 0) ids.Append(','); ids.Append(Uri.EscapeDataString(ls[j].id)); }

            int offset = 0;
            while (true)
            {
                yield return Get("/rest/v1/questions?select=lesson_id,position,text,option_a,option_b,option_c,option_d,answer,level,explanation"
                    + "&lesson_id=in.(" + ids + ")&order=lesson_id.asc,position.asc,id.asc&limit=" + Page + "&offset=" + offset, box);
                if (box.text == null) yield break;                    // tải dở dang thì bỏ cả đợt, dùng CSV
                List<CQuestion> qs = ParseQuestions(box.text);
                if (qs == null) yield break;
                foreach (var q in qs)
                {
                    Lesson les;
                    if (!byId.TryGetValue(q.lesson_id, out les)) continue;
                    les.questions.Add(new Question
                    {
                        text = q.text, a = q.option_a, b = q.option_b, c = q.option_c, d = q.option_d,
                        answer = q.answer, level = q.level, explanation = q.explanation
                    });
                }
                if (qs.Count < Page) break;
                offset += Page;
            }
        }

        foreach (var l in ordered) if (l.questions.Count > 0) into.Add(l);
        Debug.Log("[CloudBank] " + subject + ": " + into.Count + " bài đã xuất bản từ Supabase.");
    }

    static List<CLesson> ParseLessons(string body)
    {
        try { var w = JsonUtility.FromJson<CLessonRows>("{\"rows\":" + body + "}"); return w != null ? w.rows : null; }
        catch (Exception e) { Debug.LogWarning("[CloudBank] Dữ liệu bài giảng lỗi: " + e.Message); return null; }
    }

    static List<CQuestion> ParseQuestions(string body)
    {
        try { var w = JsonUtility.FromJson<CQuestionRows>("{\"rows\":" + body + "}"); return w != null && w.rows != null ? w.rows : new List<CQuestion>(); }
        catch (Exception e) { Debug.LogWarning("[CloudBank] Dữ liệu câu hỏi lỗi: " + e.Message); return null; }
    }

    static IEnumerator Get(string path, Box box)
    {
        box.text = null;
        yield return SupabaseAuth.EnsureFresh();
        using (var req = UnityWebRequest.Get(CloudConfig.Url.TrimEnd('/') + path))
        {
            SupabaseAuth.ApplyHeaders(req);
            req.timeout = 8;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) box.text = req.downloadHandler.text;
            else Debug.LogWarning("[CloudBank] Không tải được (" + req.responseCode + "): " + req.error + " " + req.downloadHandler.text);
        }
    }
}
