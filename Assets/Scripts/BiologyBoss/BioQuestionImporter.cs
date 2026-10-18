using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Đọc câu hỏi môn Sinh từ file .csv hoặc Google Sheet.
/// Cột: STT, Câu hỏi, A, B, C, D, Đáp án (A/B/C/D hoặc 0-3), Bài (tuỳ chọn).
/// Tìm cột theo tên tiêu đề (không phân biệt hoa/thường, dấu); nếu không thấy tiêu đề thì dùng đúng thứ tự trên.
/// Tự nhận dấu phân cách , ; hoặc Tab (Excel tiếng Việt hay xuất bằng dấu ;).
/// </summary>
public static class BioQuestionImporter
{
    /// <summary>Chấp nhận link /edit thường: tự đổi sang link xuất CSV đúng tab (gid). Link "Xuất bản lên web" giữ nguyên.</summary>
    public static string NormalizeSheetUrl(string url)
    {
        url = (url ?? "").Trim();
        const string key = "/spreadsheets/d/";
        int i = url.IndexOf(key, StringComparison.Ordinal);
        if (i < 0 || url.Contains("output=csv") || url.Contains("format=csv") || url.Contains("/pub")) return url;

        int s = i + key.Length, e = url.IndexOfAny(new[] { '/', '?', '#' }, s);
        string id = e < 0 ? url.Substring(s) : url.Substring(s, e - s);
        var m = Regex.Match(url, @"gid=(\d+)");
        return $"https://docs.google.com/spreadsheets/d/{id}/export?format=csv&gid={(m.Success ? m.Groups[1].Value : "0")}";
    }

    /// <summary>Sheet chưa chia sẻ công khai thì Google trả về trang đăng nhập (HTML) thay vì CSV.</summary>
    public static bool LooksLikeHtml(string text) => (text ?? "").TrimStart('\uFEFF', ' ', '\r', '\n', '\t').StartsWith("<");

    /// <summary>Trả về danh sách câu hỏi hợp lệ; dòng lỗi bị bỏ qua và ghi Warning kèm số dòng.</summary>
    public static List<BioQuestion> ParseCsv(string csv)
    {
        var result = new List<BioQuestion>();
        csv = (csv ?? "").TrimStart('\uFEFF');
        var rows = ReadRows(csv, DetectDelimiter(csv));
        if (rows.Count == 0) return result;

        string[] head = rows[0];
        int qc = Col(head, "cauhoi", "question", "noidungcauhoi");
        int ac = Col(head, "a", "answera", "dapana"), bc = Col(head, "b", "answerb", "dapanb");
        int cc = Col(head, "c", "answerc", "dapanc"), dc = Col(head, "d", "answerd", "dapand");
        int kc = Col(head, "dapan", "dapandung", "correctindex", "correct", "answer");
        int lc = Col(head, "bai", "lesson", "baihoc");
        bool hasHeader = !(qc < 0 || ac < 0 || bc < 0 || cc < 0 || dc < 0 || kc < 0);
        if (!hasHeader) { qc = 1; ac = 2; bc = 3; cc = 4; dc = 5; kc = 6; lc = 7; }
        int need = Mathf.Max(qc, ac, bc, cc, dc, kc) + 1;

        // Không có tiêu đề: nếu dòng đầu đã là câu hỏi thật thì đọc luôn, ngược lại coi là tiêu đề.
        int start = (hasHeader || !(rows[0].Length >= need && CorrectIndex(rows[0][kc]) >= 0)) ? 1 : 0;
        int skipped = 0;

        for (int i = start; i < rows.Count; i++)
        {
            string[] f = rows[i];
            if (IsBlank(f)) continue;
            if (f.Length < need) { Debug.LogWarning($"[Sinh] Dòng {i + 1} thiếu cột, bỏ qua."); skipped++; continue; }

            int k = CorrectIndex(f[kc]);
            string[] opts = { f[ac].Trim(), f[bc].Trim(), f[cc].Trim(), f[dc].Trim() };
            bool bad = k < 0 || string.IsNullOrWhiteSpace(f[qc]);
            foreach (var o in opts) if (string.IsNullOrEmpty(o)) bad = true;
            if (bad) { Debug.LogWarning($"[Sinh] Dòng {i + 1} không hợp lệ (thiếu nội dung hoặc đáp án không phải A-D), bỏ qua."); skipped++; continue; }

            // Game quy ước answers[0] là đáp án đúng -> đưa đáp án đúng lên đầu, giữ thứ tự các đáp án sai.
            var answers = new List<string> { opts[k] };
            for (int j = 0; j < 4; j++) if (j != k) answers.Add(opts[j]);
            result.Add(new BioQuestion(f[qc].Trim(), answers.ToArray())
            { lesson = lc >= 0 && lc < f.Length ? f[lc].Trim() : "" });
        }
        if (skipped > 0) Debug.LogWarning($"[Sinh] Đã bỏ qua {skipped} dòng lỗi, nạp được {result.Count} câu.");
        return result;
    }

    /// <summary>Đọc thô CSV thành các dòng (tự nhận dấu phân cách; ô trong ngoặc kép được giữ nguyên).</summary>
    public static List<string[]> ReadRaw(string csv)
    {
        csv = (csv ?? "").TrimStart('\uFEFF');
        return ReadRows(csv, DetectDelimiter(csv));
    }

    static int CorrectIndex(string raw)
    {
        raw = (raw ?? "").Trim();
        if (raw.Length == 1 && char.ToUpperInvariant(raw[0]) >= 'A' && char.ToUpperInvariant(raw[0]) <= 'D')
            return char.ToUpperInvariant(raw[0]) - 'A';
        return int.TryParse(raw, out int n) && n >= 0 && n <= 3 ? n : -1;
    }

    static bool IsBlank(string[] f)
    {
        foreach (var c in f) if (!string.IsNullOrWhiteSpace(c)) return false;
        return true;
    }

    // Đếm , ; Tab ở dòng đầu (ngoài ngoặc kép), chọn dấu xuất hiện nhiều nhất.
    static char DetectDelimiter(string csv)
    {
        int comma = 0, semi = 0, tab = 0; bool q = false;
        foreach (char c in csv)
        {
            if (c == '"') q = !q;
            else if (!q && (c == '\n' || c == '\r')) break;
            else if (!q) { if (c == ',') comma++; else if (c == ';') semi++; else if (c == '\t') tab++; }
        }
        if (tab > comma && tab >= semi) return '\t';
        return semi > comma ? ';' : ',';
    }

    // Đọc toàn bộ bản ghi: ô trong ngoặc kép được phép chứa dấu phân cách, xuống dòng và "" (ngoặc kép thoát).
    static List<string[]> ReadRows(string text, char delim)
    {
        var rows = new List<string[]>(); var row = new List<string>(); var f = new StringBuilder(); bool q = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (q)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { f.Append('"'); i++; } else q = false; }
                else f.Append(c);
            }
            else if (c == '"') q = true;
            else if (c == delim) { row.Add(f.ToString()); f.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(f.ToString()); f.Clear(); rows.Add(row.ToArray()); row.Clear();
            }
            else f.Append(c);
        }
        if (f.Length > 0 || row.Count > 0) { row.Add(f.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }

    // Tìm cột theo tên tiêu đề, bỏ qua hoa/thường, dấu tiếng Việt, khoảng trắng
    public static int Col(string[] head, params string[] names)
    {
        for (int i = 0; i < head.Length; i++)
            if (Array.IndexOf(names, Norm(head[i])) >= 0) return i;
        return -1;
    }

    public static string Norm(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace("đ", "d");
        var sb = new StringBuilder();
        foreach (char c in s.Normalize(NormalizationForm.FormD))
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }
}
