using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

public enum EssayStepStatus { Correct, Partial, Wrong, Missing }

public class EssayStepResult
{
    public string id;
    public EssayStepStatus status;
    public int points;       // điểm bước này đạt được
    public int maxPoints;
}

public class EssayGradeResult
{
    public EssayStepResult[] steps;
    public int score, maxScore;
}

/// <summary>
/// BỘ CHẤM GIẢ (E2): chấm bằng so khớp từ khóa/số theo trường "expect" của barem, KHÔNG dùng AI.
/// Chỉ để thử cảm giác chơi Phong Ấn; ở E3 sẽ thay bằng server + AI nhưng giữ nguyên kiểu kết quả này.
/// Quy tắc: bước trống = Missing; đủ mọi ý = Correct; có ít nhất một ý = Partial; còn lại = Wrong.
/// </summary>
public static class EssayFakeGrader
{
    public static EssayGradeResult Grade(EssayProblem p, EssayVariant v, string[] answers)
    {
        var res = new EssayGradeResult { steps = new EssayStepResult[p.rubric.Length], maxScore = p.MaxScore };
        for (int i = 0; i < p.rubric.Length; i++)
        {
            var st = p.rubric[i];
            string a = answers != null && i < answers.Length ? answers[i] : null;
            var status = GradeStep(a, v.expect[i]);
            int pts = status == EssayStepStatus.Correct ? st.points : status == EssayStepStatus.Partial ? st.points / 2 : 0;
            res.steps[i] = new EssayStepResult { id = st.id, status = status, points = pts, maxPoints = st.points };
            res.score += pts;
        }
        return res;
    }

    public static EssayStepStatus GradeStep(string answer, string[] expect)
    {
        if (string.IsNullOrWhiteSpace(answer)) return EssayStepStatus.Missing;
        if (expect == null || expect.Length == 0) return EssayStepStatus.Wrong;   // bước chưa cấu hình "expect"

        string text = Normalize(answer);
        var numbers = ExtractNumbers(text);
        int matched = 0;
        foreach (var g in expect) if (GroupMatches(g, text, numbers)) matched++;
        return matched == expect.Length ? EssayStepStatus.Correct : matched > 0 ? EssayStepStatus.Partial : EssayStepStatus.Wrong;
    }

    /// <summary>Kiểm một "ý phải có" (cú pháp expect) với văn bản thô của học sinh. Server dùng để kiểm số sau khi AI chấm.</summary>
    public static bool Matches(string group, string rawText)
    {
        string t = Normalize(rawText);
        return GroupMatches(group, t, ExtractNumbers(t));
    }

    /// <summary>Hạ chữ thường, bỏ dấu tiếng Việt, đổi chỉ số trên/dưới thành chữ số thường, bỏ khoảng trắng, ',' thành '.'.</summary>
    public static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.ToLowerInvariant().Replace('đ', 'd');
        string d = s.Normalize(NormalizationForm.FormKD);          // ₂ → 2, ³ → 3, tách dấu khỏi chữ
        var sb = new StringBuilder(d.Length);
        foreach (char c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(c)) continue;
            switch (c)
            {
                case '×': case '·': case '⋅': case '∙': case '•': sb.Append('*'); break;
                case '÷': sb.Append('/'); break;
                case '−': case '–': sb.Append('-'); break;
                case ',': sb.Append('.'); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    static System.Collections.Generic.List<double> ExtractNumbers(string text)
    {
        var list = new System.Collections.Generic.List<double>();
        foreach (Match m in Regex.Matches(text, @"\d+(?:\.\d+)?"))
            if (double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double x)) list.Add(x);
        return list;
    }

    static bool GroupMatches(string group, string text, System.Collections.Generic.List<double> numbers)
    {
        if (string.IsNullOrEmpty(group)) return true;
        char mode = group[0] == '#' || group[0] == '~' ? group[0] : ' ';
        if (mode != ' ') group = group.Substring(1);

        foreach (var raw in group.Split('|'))
        {
            string alt = Normalize(raw);
            if (alt.Length == 0) continue;

            if (mode == '#')
            {
                if (double.TryParse(alt, NumberStyles.Float, CultureInfo.InvariantCulture, out double target))
                {
                    double tol = Math.Max(1e-9, 0.005 * Math.Abs(target));
                    foreach (var n in numbers) if (Math.Abs(n - target) <= tol) return true;
                }
                else if (text.Contains(alt)) return true;
            }
            else if (mode == '~')   // đơn vị: phải đứng ngay sau chữ số (hoặc dấu ngoặc đóng)
            {
                int idx = text.IndexOf(alt, StringComparison.Ordinal);
                while (idx >= 0)
                {
                    if (idx > 0 && (char.IsDigit(text[idx - 1]) || text[idx - 1] == ')')) return true;
                    idx = text.IndexOf(alt, idx + 1, StringComparison.Ordinal);
                }
            }
            else if (text.Contains(alt)) return true;
        }
        return false;
    }
}
