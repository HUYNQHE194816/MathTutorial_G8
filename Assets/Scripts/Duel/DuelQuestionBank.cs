using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Một câu trắc nghiệm của game Đối kháng.</summary>
public class DuelQuestion
{
    public string text;
    public string[] options = new string[4];
    public int correct;        // 0..3
    public int level;          // 0 = Nhận biết, 1 = Thông hiểu, 2 = Vận dụng
    public string subject;     // "Lý" / "Hóa" / "Sinh"
    public string lesson;
}

/// <summary>
/// Ngân hàng câu hỏi cho Đối kháng, đọc từ Assets/Resources/Duel/DuelBank.csv
/// (cột: STT, Câu hỏi, A, B, C, D, Đáp án, Mức độ, Môn, Chương, Bài).
/// BuildMatch dựng bộ câu hỏi theo hạt giống: cùng seed + cùng bộ lọc = cùng kết quả trên mọi máy.
/// </summary>
public static class DuelQuestionBank
{
    const string ResourcePath = "Duel/DuelBank";

    // Số vòng và mức độ từng vòng: 3 Nhận biết, 4 Thông hiểu, 3 Vận dụng.
    public static readonly int[] DefaultPlan = { 0, 0, 0, 1, 1, 1, 1, 2, 2, 2 };

    static List<DuelQuestion> all;

    public static int Count { get { Load(); return all.Count; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { all = null; }

    // ------------------------------------------------------------ nạp file
    static void Load()
    {
        if (all != null && all.Count > 0) return;   // lần trước nạp được 0 câu thì thử nạp lại
        all = new List<DuelQuestion>();

        var ta = Resources.Load<TextAsset>(ResourcePath);
        if (ta == null)
        {
            Debug.LogError("[DuelBank] Không thấy Assets/Resources/" + ResourcePath + ".csv. Hãy chép file ngân hàng câu hỏi vào đó.");
            return;
        }

        var rows = ParseCsv((ta.text ?? "").TrimStart('\uFEFF'));
        for (int i = 1; i < rows.Count; i++)              // bỏ dòng tiêu đề
        {
            var c = rows[i];
            if (c.Count < 9) continue;

            int correct = AnswerIndex(c[6]);
            if (correct < 0) continue;

            all.Add(new DuelQuestion
            {
                text = c[1].Trim(),
                options = new[] { c[2].Trim(), c[3].Trim(), c[4].Trim(), c[5].Trim() },
                correct = correct,
                level = LevelOf(c[7]),
                subject = c[8].Trim(),
                lesson = c.Count > 10 ? c[10].Trim() : ""
            });
        }
        Debug.Log("[DuelBank] Đã nạp " + all.Count + " câu.");
    }

    static int AnswerIndex(string s)
    {
        s = (s ?? "").Trim().ToUpperInvariant();
        if (s.Length == 0) return -1;
        int i = s[0] - 'A';
        return i >= 0 && i <= 3 ? i : -1;
    }

    // Nhận biết = N, Thông hiểu = T, Vận dụng = V (chữ cái đầu là ASCII nên không lo lệch mã hóa dấu)
    static int LevelOf(string s)
    {
        s = (s ?? "").Trim();
        if (s.Length == 0) return 1;
        char ch = char.ToUpperInvariant(s[0]);
        return ch == 'N' ? 0 : ch == 'V' ? 2 : 1;
    }

    // ------------------------------------------------------------ dựng bộ câu hỏi theo seed
    /// <summary>
    /// Chọn một câu cho mỗi phần tử của levelPlan (0/1/2). subject = null thì lấy cả 3 môn; hoặc "Lý"/"Hóa"/"Sinh".
    /// Không trùng câu trong cùng một trận.
    /// </summary>
    public static List<DuelQuestion> BuildMatch(int seed, string subject, int[] levelPlan)
    {
        Load();
        var rng = new Rng(seed);
        var result = new List<DuelQuestion>();
        var used = new HashSet<DuelQuestion>();

        foreach (int level in levelPlan)
        {
            // thứ tự gốc trong file giống nhau trên mọi máy; xáo bằng bộ ngẫu nhiên có seed
            var pool = new List<DuelQuestion>();
            foreach (var q in all)
                if (q.level == level && !used.Contains(q) && SubjectMatches(q, subject)) pool.Add(q);

            if (pool.Count == 0) continue;
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
            }
            used.Add(pool[0]);
            result.Add(pool[0]);
        }
        return result;
    }

    // So sánh môn bằng chữ cái đầu (L/H/S)
    static bool SubjectMatches(DuelQuestion q, string subject)
    {
        if (string.IsNullOrEmpty(subject)) return true;
        if (string.IsNullOrEmpty(q.subject)) return false;
        return char.ToUpperInvariant(q.subject[0]) == char.ToUpperInvariant(subject[0]);
    }

    // Bộ ngẫu nhiên xorshift tự viết: kết quả giống hệt nhau trên Windows, Android, Mac...
    sealed class Rng
    {
        uint s;
        public Rng(int seed)
        {
            s = unchecked((uint)seed * 2654435761u + 1u);
            if (s == 0) s = 1;
        }
        public int Next(int n)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return (int)(s % (uint)n);
        }
    }

    // ------------------------------------------------------------ bộ đọc CSV (hỗ trợ "ngoặc kép", dấu phẩy và xuống dòng trong ô)
    static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else quoted = false;
                }
                else cell.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { row.Add(cell.ToString()); cell.Length = 0; }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Length = 0;
                if (row.Count > 1 || row[0].Length > 0) rows.Add(row);
                row = new List<string>();
            }
            else cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }
        return rows;
    }
}