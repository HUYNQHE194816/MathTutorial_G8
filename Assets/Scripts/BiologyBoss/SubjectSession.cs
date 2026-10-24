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
    /// <summary>true khi đang ôn TỔNG HỢP MÔN (gộp câu hỏi của mọi chương, không qua màn Chọn bài).</summary>
    public static bool Combined;
    public static float Timeout = 8f;
    public static List<FlowChapter> Chapters = new List<FlowChapter>();
    public static FlowChapter Chapter;
    /// <summary>Bài đang chọn; rỗng = ôn cả chương.</summary>
    public static string Lesson = "";

    static readonly Dictionary<SubjectId, List<FlowChapter>> cache = new Dictionary<SubjectId, List<FlowChapter>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Active = false; Combined = false; Chapter = null; Lesson = ""; Chapters = new List<FlowChapter>(); cache.Clear(); }

    public static string ChapterScene(SubjectId s) => s == SubjectId.Ly ? "ChapterSelectScene_Ly" : s == SubjectId.Hoa ? "ChapterSelectScene_Hoa" : "ChapterSelectScene";
    public static string GameScene(SubjectId s) => s == SubjectId.Ly ? "GameplayScene" : s == SubjectId.Hoa ? "MillionareScene" : "BiologyBossScene";

    public static void Begin(SubjectId s) { Subject = s; Active = false; Combined = false; Chapter = null; Lesson = ""; Chapters = new List<FlowChapter>(); }

    public static void Choose(FlowChapter c) { Chapter = c; Lesson = ""; Active = true; Combined = false; }

    /// <summary>Chọn chế độ ôn tổng hợp môn: <paramref name="all"/> lấy từ BuildCombined().</summary>
    public static void ChooseCombined(FlowChapter all) { Chapter = all; Lesson = ""; Active = true; Combined = true; }

    static readonly string[] StdHeader = { "STT", "Câu hỏi", "A", "B", "C", "D", "Đáp án", "Mức độ", "Chương", "Bài" };

    static string Cell(string[] f, int i) { return i >= 0 && i < f.Length ? (f[i] ?? "") : ""; }

    /// <summary>
    /// Gộp câu hỏi của MỌI chương đã mở của môn hiện tại thành một "chương ảo" (chuẩn hoá về cùng một header, bỏ câu trùng nội dung).
    /// Trả về null nếu môn chưa có dữ liệu hoặc chỉ có 1 chương (không cần nút tổng hợp).
    /// </summary>
    public static FlowChapter BuildCombined()
    {
        var all = new FlowChapter { title = "Tổng hợp môn", subtitle = "Toàn bộ chương", header = (string[])StdHeader.Clone() };
        var seen = new HashSet<string>(); bool anyLevel = false; int chapters = 0;
        foreach (var ch in Chapters)
        {
            if (ch == null || ch.locked || ch.rows.Count == 0 || ch.header == null) continue;
            chapters++;
            string[] h = ch.header;
            int q = BioQuestionImporter.Col(h, "cauhoi", "question", "noidungcauhoi");
            int a = BioQuestionImporter.Col(h, "a", "answera", "dapana"), b = BioQuestionImporter.Col(h, "b", "answerb", "dapanb");
            int c = BioQuestionImporter.Col(h, "c", "answerc", "dapanc"), d = BioQuestionImporter.Col(h, "d", "answerd", "dapand");
            int k = BioQuestionImporter.Col(h, "dapan", "dapandung", "correctindex", "correct", "answer");
            int lv = BioQuestionImporter.Col(h, "mucdo", "level", "difficulty");
            if (q < 0 || a < 0 || b < 0 || c < 0 || d < 0 || k < 0) { q = 1; a = 2; b = 3; c = 4; d = 5; k = 6; }   // thứ tự của file mẫu
            if (lv >= 0) anyLevel = true;

            foreach (var r in ch.rows)
            {
                string question = Cell(r.cells, q).Trim();
                if (question.Length == 0 || !seen.Add(BioQuestionImporter.Norm(question))) continue;
                all.rows.Add(new FlowRow
                {
                    lesson = r.lesson,
                    cells = new[] { (all.rows.Count + 1).ToString(), question, Cell(r.cells, a), Cell(r.cells, b), Cell(r.cells, c), Cell(r.cells, d),
                                    Cell(r.cells, k), Cell(r.cells, lv), ch.title, r.lesson == Other ? "" : r.lesson }
                });
            }
        }
        if (chapters < 2 || all.rows.Count == 0) return null;

        if (!anyLevel)   // không nguồn nào có cột "Mức độ" -> bỏ cột này để game không bật chế độ bốc theo mức độ
        {
            var hl = new List<string>(all.header); hl.RemoveAt(7); all.header = hl.ToArray();
            foreach (var r in all.rows) { var cl = new List<string>(r.cells); cl.RemoveAt(7); r.cells = cl.ToArray(); }
        }
        return all;
    }

    // ---------------------------------------------------------------- nạp dữ liệu
    /// <summary>
    /// Nạp chương/bài của môn: CSV/Google Sheet như cũ, rồi trộn thêm bài giảng đã XUẤT BẢN từ Supabase (giáo viên tạo).
    /// Bài của Supabase thắng bài CSV cùng chương + cùng tên bài. Không tải được Supabase thì chỉ dùng CSV.
    /// </summary>
    public static IEnumerator LoadSources(SubjectId s, SubjectSource[] sources)
    {
        yield return LoadCsvSources(s, sources);
        var csvList = Chapters;
        var cloud = new List<CloudQuestionBank.Lesson>();
        yield return CloudQuestionBank.Load(SubjectName(s), cloud);
        if (cloud.Count > 0) Chapters = Merge(csvList, cloud);
    }

    static string SubjectName(SubjectId s) => s == SubjectId.Ly ? ProgressSaveSystem.SubjectLy : s == SubjectId.Hoa ? ProgressSaveSystem.SubjectHoa : ProgressSaveSystem.SubjectSinh;

    /// <summary>Không sửa danh sách CSV (đang nằm trong cache): chương bị ảnh hưởng được tạo bản mới, chương khác dùng lại nguyên.</summary>
    static List<FlowChapter> Merge(List<FlowChapter> csv, List<CloudQuestionBank.Lesson> cloud)
    {
        var result = new List<FlowChapter>(csv);
        var order = new List<string>();
        var byChapter = new Dictionary<string, List<CloudQuestionBank.Lesson>>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in cloud)
        {
            string t = string.IsNullOrWhiteSpace(l.chapter) ? "(chưa phân chương)" : l.chapter.Trim();
            if (!byChapter.ContainsKey(t)) { byChapter[t] = new List<CloudQuestionBank.Lesson>(); order.Add(t); }
            byChapter[t].Add(l);
        }

        string subject = SubjectName(Subject);
        foreach (var title in order)
        {
            var lessons = byChapter[title];
            int idx = result.FindIndex(c => string.Equals(c.title, title, StringComparison.OrdinalIgnoreCase));
            FlowChapter target;
            if (idx >= 0 && !result[idx].locked)
            {
                var old = result[idx];
                target = new FlowChapter { title = old.title, subtitle = old.subtitle, header = old.header };
                var cloudTitles = new HashSet<string>(lessons.Select(l => l.title), StringComparer.OrdinalIgnoreCase);
                foreach (var r in old.rows) if (!cloudTitles.Contains(r.lesson)) target.rows.Add(r);   // bài CSV trùng tên với bài Supabase thì nhường
                result[idx] = target;
            }
            else
            {
                bool multiLevel = lessons.SelectMany(l => l.questions).Select(q => q.level ?? "").Distinct().Count() > 1;
                var h = new List<string>(StdHeader);
                if (!multiLevel) h.RemoveAt(7);   // chỉ có 1 mức độ: bỏ cột để game không bật chế độ bốc theo mức độ
                target = new FlowChapter { title = title, subtitle = "Bài giảng giáo viên", header = h.ToArray() };
                if (idx >= 0) result[idx] = target; else result.Add(target);   // thay thẻ "Sắp ra mắt" cùng tên nếu có
            }
            foreach (var l in lessons)
                foreach (var q in l.questions)
                    target.rows.Add(new FlowRow { lesson = l.title, cells = MapRow(target.header, q, target.rows.Count + 1, subject, title, l.title) });
        }
        return result;
    }

    /// <summary>Xếp một câu hỏi từ Supabase vào đúng thứ tự cột của header chương.</summary>
    static string[] MapRow(string[] h, CloudQuestionBank.Question q, int stt, string subject, string chapter, string lesson)
    {
        var cells = new string[h.Length];
        for (int i = 0; i < cells.Length; i++) cells[i] = "";
        int cq = BioQuestionImporter.Col(h, "cauhoi", "question", "noidungcauhoi");
        int a = BioQuestionImporter.Col(h, "a", "answera", "dapana"), b = BioQuestionImporter.Col(h, "b", "answerb", "dapanb");
        int c = BioQuestionImporter.Col(h, "c", "answerc", "dapanc"), d = BioQuestionImporter.Col(h, "d", "answerd", "dapand");
        int k = BioQuestionImporter.Col(h, "dapan", "dapandung", "correctindex", "correct", "answer");
        if (cq < 0 || a < 0 || b < 0 || c < 0 || d < 0 || k < 0) { cq = 1; a = 2; b = 3; c = 4; d = 5; k = 6; }   // thứ tự của file mẫu
        Put(cells, BioQuestionImporter.Col(h, "stt"), stt.ToString());
        Put(cells, cq, q.text); Put(cells, a, q.a); Put(cells, b, q.b); Put(cells, c, q.c); Put(cells, d, q.d); Put(cells, k, q.answer);
        Put(cells, BioQuestionImporter.Col(h, "mucdo", "level", "difficulty"), q.level);
        Put(cells, BioQuestionImporter.Col(h, "giaithich", "explanation", "loigiai"), q.explanation);
        Put(cells, BioQuestionImporter.Col(h, "mon", "subject"), subject);
        Put(cells, BioQuestionImporter.Col(h, "chuong", "chapter"), chapter);
        Put(cells, BioQuestionImporter.Col(h, "bai", "lesson", "baihoc"), lesson);
        return cells;
    }

    static void Put(string[] cells, int i, string v) { if (i >= 0 && i < cells.Length) cells[i] = v ?? ""; }

    static IEnumerator LoadCsvSources(SubjectId s, SubjectSource[] sources)
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
                list.AddRange(BuildChapters(s, src, text));
            }
        Chapters = list;
        if (list.Any(c => !c.locked)) cache[s] = list;
    }

    static List<FlowChapter> BuildChapters(SubjectId subject, SubjectSource src, string text)
    {
        var result = new List<FlowChapter>();
        var rows = BioQuestionImporter.ReadRaw(text);
        if (rows.Count < 2) return result;

        string[] head = rows[0];
        int qc = BioQuestionImporter.Col(head, "cauhoi", "question", "noidungcauhoi"); if (qc < 0) qc = 1;
        int cc = BioQuestionImporter.Col(head, "chuong", "chapter");
        int lc = BioQuestionImporter.Col(head, "bai", "lesson", "baihoc");
        int mc = BioQuestionImporter.Col(head, "mon", "subject");   // cột "Môn" (tuỳ chọn): 1 file CSV chứa cả Lý/Hóa/Sinh
        var byTitle = new Dictionary<string, FlowChapter>(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i < rows.Count; i++)
        {
            string[] f = rows[i];
            if (f.Length <= qc || string.IsNullOrWhiteSpace(f[qc])) continue;
            if (mc >= 0 && mc < f.Length && !string.IsNullOrWhiteSpace(f[mc]) && !SubjectMatches(f[mc], subject)) continue;
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

    /// <summary>Giá trị cột "Môn" (Lý / Hóa / Sinh, có hoặc không dấu, "Vật lí", "Hóa học"...) có thuộc môn <paramref name="s"/> không.</summary>
    static bool SubjectMatches(string cell, SubjectId s)
    {
        string n = BioQuestionImporter.Norm(cell);
        switch (s)
        {
            case SubjectId.Ly: return n == "ly" || n == "vatly" || n == "vatli" || n == "physics";
            case SubjectId.Hoa: return n == "hoa" || n == "hoahoc" || n == "chemistry";
            default: return n == "sinh" || n == "sinhhoc" || n == "biology";
        }
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
    public static string BackSceneOr(SubjectId s, string fallback) => Active && Subject == s && Chapter != null ? (Combined ? ChapterScene(s) : LessonScene) : fallback;

    public static string Describe(int questionCount) => Combined
        ? $"Ôn tổng hợp môn • {questionCount} câu"
        : $"{(Chapter != null ? Chapter.title : "")} • {(string.IsNullOrEmpty(Lesson) ? "Ôn cả chương" : Lesson)} • {questionCount} câu";
}
