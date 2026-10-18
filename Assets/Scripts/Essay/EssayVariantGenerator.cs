using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>Một bản số liệu cụ thể của bài tự luận: đề, barem và đáp án đã tính bằng code.</summary>
public class EssayVariant
{
    public string problemId;
    public int seed;
    public string statement;                                  // đề đã thế số
    public Dictionary<string, double> numbers = new Dictionary<string, double>();   // tham số + đại lượng số
    public Dictionary<string, string> texts = new Dictionary<string, string>();     // đại lượng chọn chữ (vd chìm/nổi)
    public string[] rubricDesc;                               // mô tả barem đã thế số (cùng thứ tự rubric)
    public string[][] expect;                                 // "ý phải có" của từng bước đã thế số (cho bộ chấm giả)
    public double finalAnswer;                                // NaN nếu bài lời
    public string finalUnit;
    public double tolerance;

    public bool IsCorrectFinal(double value) =>
        !double.IsNaN(finalAnswer) && Math.Abs(value - finalAnswer) <= tolerance;
}

/// <summary>Sinh bản số liệu ổn định theo (mã học sinh, bài, lần) và tính đáp số bằng code.</summary>
public static class EssayVariantGenerator
{
    /// <summary>Hạt giống ổn định giữa các lần chạy (string.GetHashCode thì không).</summary>
    public static int SeedFor(string studentCode, string problemId, int attempt = 0)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in (studentCode ?? "") + "|" + (problemId ?? "") + "|" + attempt)
                h = (h ^ c) * 16777619;
            return (int)(h & 0x7FFFFFFF);
        }
    }

    public static EssayVariant Generate(EssayProblem p, int seed)
    {
        var rng = new System.Random(seed);
        var v = new EssayVariant { problemId = p.id, seed = seed, finalAnswer = double.NaN };

        if (p.parameters != null)
            foreach (var prm in p.parameters)
                if (prm.values != null && prm.values.Length > 0)
                    v.numbers[prm.name] = prm.values[rng.Next(prm.values.Length)];

        var shown = new Dictionary<string, string>();   // chữ hiển thị cho mỗi {tên}
        foreach (var kv in v.numbers) shown[kv.Key] = Fmt(kv.Value, 4);

        if (p.derived != null)
            foreach (var d in p.derived)
            {
                double val = Evaluate(d.formula, v.numbers);
                if (d.IsChoice)
                {
                    string t = val > d.threshold ? d.above : d.below;
                    v.texts[d.id] = t;
                    shown[d.id] = t;
                }
                else
                {
                    val = Math.Round(val, d.decimals);       // đáp số làm tròn đúng như hiển thị
                    v.numbers[d.id] = val;
                    shown[d.id] = Fmt(val, d.decimals);
                }
            }

        v.statement = Fill(p.statement, shown);
        v.rubricDesc = new string[p.rubric.Length];
        v.expect = new string[p.rubric.Length][];
        for (int i = 0; i < p.rubric.Length; i++)
        {
            v.rubricDesc[i] = Fill(p.rubric[i].desc, shown);
            var ex = p.rubric[i].expect ?? new string[0];
            v.expect[i] = new string[ex.Length];
            for (int j = 0; j < ex.Length; j++) v.expect[i][j] = Fill(ex[j], shown);
        }

        if (p.HasNumericAnswer)
        {
            v.finalAnswer = v.numbers[p.finalAnswer.derivedId];
            v.finalUnit = p.finalAnswer.unit;
            v.tolerance = p.finalAnswer.tolerance;
        }
        return v;
    }

    // Số hiển thị kiểu Việt: dấu phẩy thập phân, bỏ số 0 thừa ở cuối.
    static string Fmt(double x, int decimals)
    {
        string s = Math.Round(x, decimals).ToString("0." + new string('#', Math.Max(decimals, 0)), CultureInfo.InvariantCulture);
        return s.Replace('.', ',');
    }

    static string Fill(string template, Dictionary<string, string> shown)
    {
        if (string.IsNullOrEmpty(template)) return template;
        var sb = new StringBuilder(template);
        foreach (var kv in shown) sb.Replace("{" + kv.Key + "}", kv.Value);
        return sb.ToString();
    }

    // ---- Bộ tính biểu thức nhỏ: số, tên biến, + - * /, dấu ngoặc, dấu âm ----
    public static double Evaluate(string expr, Dictionary<string, double> vars)
    {
        var parser = new Parser(expr, vars);
        double r = parser.ParseExpr();
        parser.ExpectEnd();
        return r;
    }

    class Parser
    {
        readonly string s; readonly Dictionary<string, double> vars; int i;
        public Parser(string s, Dictionary<string, double> vars) { this.s = s ?? ""; this.vars = vars; }

        void Skip() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        public void ExpectEnd() { Skip(); if (i < s.Length) throw new FormatException("Biểu thức thừa ký tự: " + s); }

        public double ParseExpr()
        {
            double x = ParseTerm();
            while (true)
            {
                Skip();
                if (i < s.Length && s[i] == '+') { i++; x += ParseTerm(); }
                else if (i < s.Length && s[i] == '-') { i++; x -= ParseTerm(); }
                else return x;
            }
        }

        double ParseTerm()
        {
            double x = ParseFactor();
            while (true)
            {
                Skip();
                if (i < s.Length && s[i] == '*') { i++; x *= ParseFactor(); }
                else if (i < s.Length && s[i] == '/') { i++; x /= ParseFactor(); }
                else return x;
            }
        }

        double ParseFactor()
        {
            Skip();
            if (i >= s.Length) throw new FormatException("Biểu thức thiếu toán hạng: " + s);
            if (s[i] == '-') { i++; return -ParseFactor(); }
            if (s[i] == '(')
            {
                i++;
                double x = ParseExpr();
                Skip();
                if (i >= s.Length || s[i] != ')') throw new FormatException("Thiếu dấu ): " + s);
                i++;
                return x;
            }
            int start = i;
            if (char.IsDigit(s[i]) || s[i] == '.')
            {
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
            }
            if (char.IsLetter(s[i]) || s[i] == '_')
            {
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                string name = s.Substring(start, i - start);
                if (!vars.TryGetValue(name, out double val)) throw new FormatException("Chưa có biến '" + name + "' trong: " + s);
                return val;
            }
            throw new FormatException("Ký tự lạ '" + s[i] + "' trong: " + s);
        }
    }
}
