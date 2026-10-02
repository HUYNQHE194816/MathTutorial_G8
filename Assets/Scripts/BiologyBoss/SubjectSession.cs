using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public enum SubjectId { Ly = 0, Hoa = 1, Sinh = 2 }

/// <summary>Một nguồn dữ liệu (file CSV hoặc Google Sheet). Nếu có cột "Chương" thì tự tách thành nhiều chương, nếu không thì cả nguồn là 1 chương.</summary>
[Serializable]
public class SubjectSource
{
    public string title = "Chương VII";
    public string subtitle = "Sinh học 8";
    [Tooltip("File .csv (cột: STT, Câu hỏi, A, B, C, D, Đáp án, Chương, Bài). Cột Chương/Bài là tuỳ chọn.")]
    public TextAsset csvFile;
    [Tooltip("Link Google Sheet (tuỳ chọn). Tải lỗi thì dùng file CSV. Để trống cả hai = chương hiện 'Sắp ra mắt'.")]
    public string sheetUrl;
    public bool Available => csvFile != null || !string.IsNullOrWhiteSpace(sheetUrl);
}

public class FlowRow { public string lesson; public string[] cells; }

public class FlowChapter
{
    public string title, subtitle; public bool locked; public string[] header;
    public List<FlowRow> rows = new List<FlowRow>();
    public int LessonCount => rows.Select(r => r.lesson).Distinct(StringComparer.OrdinalIgnoreCase).Count();
}

/// <summary>
/// Luồng Chọn môn -> Chọn chương -> Chọn bài -> Vào trận, dùng chung cho Lý / Hóa / Sinh.
/// Game của từng môn chỉ cần hỏi TryGetCsv(môn, out csv): nhận về CSV đã lọc đúng bài (cùng định dạng cũ).
/// </summary>
public static class SubjectSession
{
    public const string LessonScene = "LessonSelectScene";
    /// <summary>Nhóm cho câu hỏi không có giá trị ở cột "Bài".</summary>
    public const string Other = "(chưa phân bài)";

    public static SubjectId Subject;
    /// <summary>true khi đã chọn xong chương (đang đi trong luồng). Game dùng cờ này để biết có lọc theo bài hay không.</summary>
    public static bool Active;
    public static float Timeout = 8f;
    public static List<FlowChapter> Chapters = new List<FlowChapter>();
    public static FlowChapter Chapter;
    /// <summary>Bài đang chọn; rỗng = ôn cả chương.</summary>
    public static string Lesson = "";

    static readonly Dictionary<SubjectId, List<FlowChapter>> cache = new Dictionary<SubjectId, List<FlowChapter>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Active = false; Chapter = null; Lesson = ""; Chapters = new List<FlowChapter>(); cache.Clear(); }

    public static string ChapterScene(SubjectId s) => s == SubjectId.Ly ? "ChapterSelectScene_Ly" : s == SubjectId.Hoa ? "ChapterSelectScene_Hoa" : "ChapterSelectScene";
    public static string GameScene(SubjectId s) => s == SubjectId.Ly ? "GameplayScene" : s == SubjectId.Hoa ? "MillionareScene" : "BiologyBossScene";

    public static void Begin(SubjectId s) { Subject = s; Active = false; Chapter = null; Lesson = ""; Chapters = new List<FlowChapter>(); }

    public static void Choose(FlowChapter c) { Chapter = c; Lesson = ""; Active = true; }

    // ---------------------------------------------------------------- nạp dữ liệu
    public static IEnumerator LoadSources(SubjectId s, SubjectSource[] sources)
    {
        if (cache.TryGetValue(s, out var cached)) { Chapters = cached; yield break; }
        var list = new List<FlowChapter>();
        if (sources != null)
            foreach (var src in sources)
            {
                if (src == null) continue;
                if (!src.Available) { list.Add(new FlowChapter { title = src.title, subtitle = src.subtitle, locked = true }); continue; }

                string text = null;
                if (!string.IsNullOrWhiteSpace(src.sheetUrl))
                {
                    using (var req = UnityWebRequest.Get(BioQuestionImporter.NormalizeSheetUrl(src.sheetUrl)))
                    {
                        req.timeout = Mathf.Max(1, Mathf.CeilToInt(Timeout));
                        yield return req.SendWebRequest();
                        if (req.result != UnityWebRequest.Result.Success)
                            Debug.LogWarning("[Flow] Không tải được Google Sheet: " + req.error + " -> dùng file CSV.");
                        else
                        {
                            string t = Encoding.UTF8.GetString(req.downloadHandler.data);
                            if (BioQuestionImporter.LooksLikeHtml(t)) Debug.LogWarning("[Flow] Google Sheet trả về trang web thay vì CSV (chưa chia sẻ công khai?) -> dùng file CSV.");
                            else text = t;
                        }
                    }
                }
                if (string.IsNullOrEmpty(text) && src.csvFile != null) text = src.csvFile.text;
                if (string.IsNullOrEmpty(text)) { Debug.LogWarning("[Flow] Không có dữ liệu cho '" + src.title + "'."); continue; }
                list.AddRange(BuildChapters(src, text));
            }
        Chapters = list;
        if (list.Any(c => !c.locked)) cache[s] = list;
    }

    static List<FlowChapter> BuildChapters(SubjectSource src, string text)
    {
        var result = new List<FlowChapter>();
        var rows = BioQuestionImporter.ReadRaw(text);
        if (rows.Count < 2) return result;

        string[] head = rows[0];
        int qc = BioQuestionImporter.Col(head, "cauhoi", "question", "noidungcauhoi"); if (qc < 0) qc = 1;
        int cc = BioQuestionImporter.Col(head, "chuong", "chapter");
        int lc = BioQuestionImporter.Col(head, "bai", "lesson", "baihoc");
        var byTitle = new Dictionary<string, FlowChapter>(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i < rows.Count; i++)
        {
            string[] f = rows[i];
            if (f.Length <= qc || string.IsNullOrWhiteSpace(f[qc])) continue;
            bool hasChapter = cc >= 0 && cc < f.Length && !string.IsNullOrWhiteSpace(f[cc]);
            string title = hasChapter ? f[cc].Trim() : src.title;
            if (!byTitle.TryGetValue(title, out var ch))
            {
                ch = new FlowChapter { title = title, subtitle = hasChapter ? src.title : src.subtitle, header = head };
                byTitle[title] = ch; result.Add(ch);
            }
            string lesson = lc >= 0 && lc < f.Length && !string.IsNullOrWhiteSpace(f[lc]) ? f[lc].Trim() : Other;
            ch.rows.Add(new FlowRow { lesson = lesson, cells = f });
        }
        return result;
    }

    // ---------------------------------------------------------------- bài / lọc
    /// <summary>Danh sách bài + số câu: bài có số (Bài 30, 31...) xếp theo số, bài đặt tên xếp theo thứ tự xuất hiện, "chưa phân bài" xếp cuối.</summary>
    public static List<KeyValuePair<string, int>> Lessons()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); var names = new List<string>();
        if (Chapter != null)
            foreach (var r in Chapter.rows)
            {
                if (counts.ContainsKey(r.lesson)) counts[r.lesson]++; else { counts[r.lesson] = 1; names.Add(r.lesson); }
            }
        var ordered = names.OrderBy(n => n == Other ? 2 : (Num(n) == int.MaxValue ? 1 : 0)).ThenBy(n => Num(n)).ToList();
        return ordered.Select(n => new KeyValuePair<string, int>(n, counts[n])).ToList();
    }

    static int Num(string s)
    {
        var m = Regex.Match(s ?? "", @"\d+");
        return m.Success && int.TryParse(m.Value, out int n) ? n : int.MaxValue;
    }

    public static List<FlowRow> SelectedRows()
    {
        if (Chapter == null) return new List<FlowRow>();
        if (string.IsNullOrEmpty(Lesson)) return Chapter.rows;
        return Chapter.rows.Where(r => string.Equals(r.lesson, Lesson, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>Game của môn <paramref name="s"/> gọi hàm này: nếu đang đi trong luồng thì trả về CSV (cùng cột với file gốc) chỉ gồm câu của bài đã chọn.</summary>
    public static bool TryGetCsv(SubjectId s, out string csv)
    {
        csv = null;
        if (!Active || Subject != s || Chapter == null || Chapter.locked || Chapter.header == null) return false;
        var sb = new StringBuilder();
        AppendRow(sb, Chapter.header);
        foreach (var r in SelectedRows()) AppendRow(sb, r.cells);
        csv = sb.ToString();
        return true;
    }

    static void AppendRow(StringBuilder sb, string[] cells)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append((cells[i] ?? "").Replace("\"", "\"\"")).Append('"');
        }
        sb.Append('\n');
    }

    /// <summary>Scene để quay về từ game: màn Chọn bài nếu đang đi trong luồng, ngược lại <paramref name="fallback"/>.</summary>
    public static string BackSceneOr(SubjectId s, string fallback) => Active && Subject == s && Chapter != null ? LessonScene : fallback;

    public static string Describe(int questionCount) =>
        $"{(Chapter != null ? Chapter.title : "")} • {(string.IsNullOrEmpty(Lesson) ? "Ôn cả chương" : Lesson)} • {questionCount} câu";
}
