using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>Một ô số học sinh phải điền lại trong công thức.</summary>
public class FormulaBlank
{
    public string name;      // tên tham số / đại lượng trong barem, vd "m", "D"
    public double value;     // giá trị đúng (đã tính bằng code)
    public double tol;       // sai số cho phép khi so
    public string shown;     // số đúng viết kiểu Việt (dấu phẩy), dùng để gợi ý khi sai nhiều lần
}

/// <summary>Một bước có công thức mà học sinh làm chưa đúng: câu công thức cắt thành chữ + ô số.</summary>
public class FormulaFixItem
{
    public int stepIndex;            // vị trí bước trong barem (bắt đầu từ 0)
    public string label;             // tên bước, vd "Thế số"
    public string[] parts;           // đoạn chữ xen giữa các ô; parts.Length == blanks.Length + 1
    public FormulaBlank[] blanks;

    /// <summary>Câu công thức với ô trống đánh số (1), (2)... để hiện lên màn hình.</summary>
    public string Template()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            sb.Append(parts[i]);
            if (i < blanks.Length) sb.Append('(').Append(i + 1).Append(')');
        }
        return sb.ToString();
    }

    /// <summary>Công thức đã điền đủ số đúng (dùng làm bản tham chiếu khi chép phạt).</summary>
    public string Filled()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            sb.Append(parts[i]);
            if (i < blanks.Length) sb.Append(blanks[i].shown);
        }
        return sb.ToString();
    }
}

/// <summary>Dữ liệu dựng màn "Sửa công thức".</summary>
public class FormulaFixArgs
{
    public string title, subjectId, statement;
    public int stepNo, stepCount;        // bước sửa thứ mấy / tổng số bước cần sửa
    public FormulaFixItem item;
    public Action onDone;                // gọi khi học sinh đã điền đúng hết
}

/// <summary>
/// Sau khi chấm tự luận: với mỗi bước CÓ CÔNG THỨC SỐ mà học sinh chưa đúng (Partial / Wrong / Missing),
/// bắt điền lại các số vào công thức của bước đó cho đúng. Không đổi điểm đã chấm.
/// Một bước được coi là "có công thức số" khi mô tả barem (desc) có dấu "=" và mọi {tên} trong đó là SỐ
/// (tham số hoặc đại lượng số). Bước có đại lượng chọn chữ (vd {verdict}) hoặc bước chỉ viết chữ thì bỏ qua.
/// </summary>
public static class EssayFormulaFix
{
    static readonly Regex Ph = new Regex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}");

    public static List<FormulaFixItem> Build(EssayProblem p, EssayVariant v, EssayGradeResult g)
    {
        var list = new List<FormulaFixItem>();
        if (p == null || v == null || g == null || g.steps == null || p.rubric == null) return list;
        for (int i = 0; i < p.rubric.Length && i < g.steps.Length; i++)
        {
            if (g.steps[i] == null || g.steps[i].status == EssayStepStatus.Correct) continue;
            var item = BuildItem(p, v, i);
            if (item != null) list.Add(item);
        }
        return list;
    }

    static FormulaFixItem BuildItem(EssayProblem p, EssayVariant v, int i)
    {
        string desc = p.rubric[i].desc;
        if (string.IsNullOrEmpty(desc) || desc.IndexOf('=') < 0) return null;

        var parts = new List<string>(); var blanks = new List<FormulaBlank>();
        int last = 0;
        foreach (Match m in Ph.Matches(desc))
        {
            string name = m.Groups[1].Value;
            if (!v.numbers.TryGetValue(name, out double val)) return null;   // đại lượng chọn chữ hoặc không rõ: không phải công thức số
            int dec = DecimalsOf(p, name);
            parts.Add(desc.Substring(last, m.Index - last));
            blanks.Add(new FormulaBlank
            {
                name = name, value = val, shown = Fmt(val, dec),
                tol = Math.Max(1e-9, Math.Max(0.005 * Math.Abs(val), 0.5 * Math.Pow(10, -dec)))
            });
            last = m.Index + m.Length;
        }
        if (blanks.Count == 0) return null;
        parts.Add(desc.Substring(last));
        parts[0] = TrimLead(parts[0]);
        parts[parts.Count - 1] = TrimTail(parts[parts.Count - 1]);
        return new FormulaFixItem { stepIndex = i, label = p.rubric[i].label, parts = parts.ToArray(), blanks = blanks.ToArray() };
    }

    /// <summary>Bỏ lời dẫn kiểu "Thế số đúng: " ở đầu (dấu ':' dính liền chữ phía trước, không phải tỉ lệ "Fe : FeCl₂").</summary>
    static string TrimLead(string s)
    {
        for (int i = 1; i + 1 < s.Length; i++)
            if (s[i] == ':' && s[i - 1] != ' ' && s[i + 1] == ' ') return s.Substring(i + 1).TrimStart();
        return s;
    }

    /// <summary>Sau ô cuối chỉ giữ đơn vị: cắt ở ", ..." hoặc " và ..." (vd " g/cm³ và ghi đơn vị" thành " g/cm³").</summary>
    static string TrimTail(string s)
    {
        int c = s.IndexOf(", ", StringComparison.Ordinal), v = s.IndexOf(" và ", StringComparison.Ordinal);
        int cut = c < 0 ? v : v < 0 ? c : Math.Min(c, v);
        return cut >= 0 ? s.Substring(0, cut) : s;
    }

    static int DecimalsOf(EssayProblem p, string name)
    {
        if (p.derived != null) foreach (var d in p.derived) if (d != null && d.id == name) return Math.Max(0, d.decimals);
        return 4;   // tham số của đề: giữ đến 4 chữ số như lúc hiển thị đề
    }

    static string Fmt(double x, int decimals)
        => Math.Round(x, decimals).ToString("0." + new string('#', Math.Max(decimals, 0)), CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>Đọc số học sinh gõ: chấp nhận dấu phẩy hoặc chấm, bỏ khoảng trắng.</summary>
    public static bool TryParse(string s, out double x)
    {
        x = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().Replace(" ", "").Replace(',', '.').Replace('−', '-').Replace('–', '-');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out x);
    }

    /// <summary>Chấm từng ô: true = số đúng (trong sai số cho phép).</summary>
    public static bool[] Check(FormulaFixItem it, string[] typed)
    {
        var ok = new bool[it.blanks.Length];
        for (int i = 0; i < ok.Length; i++)
        {
            string t = typed != null && i < typed.Length ? typed[i] : null;
            ok[i] = TryParse(t, out double x) && Math.Abs(x - it.blanks[i].value) <= it.blanks[i].tol;
        }
        return ok;
    }
}
