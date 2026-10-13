using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>Một ký hiệu trong bảng: ký tự, tên hiển thị khi rê chuột, các cách gõ tắt (có thể rỗng).</summary>
public class SymbolDef
{
    public string symbol, name;
    public string[] keys;
    public SymbolDef(string symbol, string name, string keys)
    {
        this.symbol = symbol; this.name = name;
        this.keys = string.IsNullOrEmpty(keys) ? new string[0] : keys.Split(',');
    }
    public string KeysText => keys.Length == 0 ? "" : string.Join(" hoặc ", keys);
}

public class SymbolTab
{
    public string name;
    public string[] symbols;
}

/// <summary>Kết quả kiểm tra lời giải trước khi gửi chấm.</summary>
public enum SubmitCheck { Ok, Empty, TooShort, TooLong }

/// <summary>
/// Mục 8 của kế hoạch: bảng ký hiệu, gõ tắt gợi ý, chuẩn hóa ký hiệu trước khi chấm, đánh số dòng.
/// Không dùng UnityEngine để dễ kiểm thử.
/// </summary>
public static class EssaySymbols
{
    public const int MaxChars = 2000;
    public const int MinChars = 6;       // dưới mức này coi như trống/quá ngắn, không gọi chấm

    // ------------------------------------------------------------------ dữ liệu bảng (mục 8.2)
    static readonly SymbolDef[] all =
    {
        // Toán và đơn vị
        new SymbolDef("±", "cộng trừ", "+-"), new SymbolDef("×", "nhân", ""), new SymbolDef("÷", "chia", ""),
        new SymbolDef("≈", "xấp xỉ", "~="), new SymbolDef("≠", "khác", "!="), new SymbolDef("≤", "nhỏ hơn hoặc bằng", "<="),
        new SymbolDef("≥", "lớn hơn hoặc bằng", ">="), new SymbolDef("√", "căn bậc hai", "sqrt"), new SymbolDef("∞", "vô cực", "inf"),
        new SymbolDef("½", "một phần hai", "1/2"), new SymbolDef("¼", "một phần tư", "1/4"), new SymbolDef("π", "pi", "pi"),
        new SymbolDef("%", "phần trăm", ""), new SymbolDef("°", "độ", ""),
        // Số mũ và chỉ số
        new SymbolDef("⁰", "mũ 0", "^0"), new SymbolDef("¹", "mũ 1", "^1"), new SymbolDef("²", "mũ 2 (bình phương)", "^2"),
        new SymbolDef("³", "mũ 3 (lập phương)", "^3"), new SymbolDef("⁴", "mũ 4", "^4"), new SymbolDef("⁵", "mũ 5", "^5"),
        new SymbolDef("⁶", "mũ 6", "^6"), new SymbolDef("⁷", "mũ 7", "^7"), new SymbolDef("⁸", "mũ 8", "^8"), new SymbolDef("⁹", "mũ 9", "^9"),
        new SymbolDef("⁺", "mũ cộng (điện tích dương)", "^+"), new SymbolDef("⁻", "mũ trừ (điện tích âm)", "^-"),
        new SymbolDef("₀", "chỉ số dưới 0", "_0"), new SymbolDef("₁", "chỉ số dưới 1", "_1"), new SymbolDef("₂", "chỉ số dưới 2", "_2"),
        new SymbolDef("₃", "chỉ số dưới 3", "_3"), new SymbolDef("₄", "chỉ số dưới 4", "_4"), new SymbolDef("₅", "chỉ số dưới 5", "_5"),
        new SymbolDef("₆", "chỉ số dưới 6", "_6"), new SymbolDef("₇", "chỉ số dưới 7", "_7"), new SymbolDef("₈", "chỉ số dưới 8", "_8"),
        new SymbolDef("₉", "chỉ số dưới 9", "_9"),
        // Hóa học
        new SymbolDef("→", "phản ứng tạo ra", "->"), new SymbolDef("⇌", "phản ứng thuận nghịch", "<=>"),
        new SymbolDef("↑", "chất khí bay lên", "(khí),(khi)"), new SymbolDef("↓", "chất kết tủa", "(kết tủa),(ket tua)"),
        new SymbolDef("Δ", "delta / đun nóng", "delta,Delta"), new SymbolDef("·", "dấu chấm giữa", ""),
        // Vật lí
        new SymbolDef("Ω", "ôm", "Omega,ohm"), new SymbolDef("µ", "micro", "mu"), new SymbolDef("λ", "lambda", "lambda"),
        new SymbolDef("ρ", "rô (khối lượng riêng)", "rho"), new SymbolDef("θ", "theta", "theta"), new SymbolDef("ω", "omega", "omega"),
        new SymbolDef("α", "alpha", "alpha"), new SymbolDef("β", "beta", "beta"), new SymbolDef("τ", "tau", "tau"),
        // Sinh học
        new SymbolDef("♂", "giống đực / nam", "nam"), new SymbolDef("♀", "giống cái / nữ", "nu"), new SymbolDef("↔", "hai chiều", "<->"),
        new SymbolDef("↓", "giảm", ""), new SymbolDef("‰", "phần nghìn", ""),
    };

    static readonly Dictionary<string, SymbolDef> bySymbol = BuildIndex();

    static Dictionary<string, SymbolDef> BuildIndex()
    {
        var d = new Dictionary<string, SymbolDef>();
        foreach (var s in all) if (!d.ContainsKey(s.symbol)) d[s.symbol] = s;   // ký hiệu trùng: giữ định nghĩa đầu tiên
        return d;
    }

    static SymbolTab Tab(string name, string symbols)
    {
        var list = new List<string>();
        foreach (var ch in symbols.Split(' ')) if (ch.Length > 0) list.Add(ch);
        return new SymbolTab { name = name, symbols = list.ToArray() };
    }

    /// <summary>Chia tab theo nhóm: Hay dùng, Toán và đơn vị, Hóa học, Vật lí, Sinh học.</summary>
    public static readonly SymbolTab[] Tabs =
    {
        Tab("Hay dùng", "² ³ ₂ ₃ → ⇌ ↑ ↓ Δ ° √ × ÷ ± ≈ ≤ ≥ π %"),
        Tab("Toán và đơn vị", "± × ÷ ≈ ≠ ≤ ≥ √ ∞ ½ ¼ π % ° ⁰ ¹ ² ³ ⁴ ⁵ ⁶ ⁷ ⁸ ⁹ ⁺ ⁻ ₀ ₁ ₂ ₃ ₄ ₅ ₆ ₇ ₈ ₉"),
        Tab("Hóa học", "→ ⇌ ↑ ↓ Δ ⁺ ⁻ ° · ₂ ₃ ₄ ₅ ₆ ₇ ₈ ₉"),
        Tab("Vật lí", "Ω µ λ ρ θ ω α β τ Δ ° ± ² ³"),
        Tab("Sinh học", "♂ ♀ → ↔ ↑ ↓ × % ‰ °"),
    };

    /// <summary>Tab mặc định theo môn của chặng đang chơi.</summary>
    public static int DefaultTab(string subject) => subject == "hoa" ? 2 : subject == "ly" ? 3 : subject == "sinh" ? 4 : 0;

    public static SymbolDef Get(string symbol) => bySymbol.TryGetValue(symbol, out var d) ? d : new SymbolDef(symbol, symbol, "");

    /// <summary>Mọi ký tự trong bảng (để kiểm font có đủ glyph không).</summary>
    public static IEnumerable<string> AllSymbols() { foreach (var kv in bySymbol) yield return kv.Key; }

    // ------------------------------------------------------------------ gợi ý khi gõ tắt
    /// <summary>
    /// Nhìn phần văn bản ngay trước con trỏ; nếu kết thúc bằng một gõ tắt thì trả về ký hiệu gợi ý và độ dài gõ tắt.
    /// Gõ tắt dạng chữ (sqrt, pi, delta...) chỉ nhận khi đứng đầu từ, tránh bắt nhầm giữa chữ thường.
    /// </summary>
    public static bool Suggest(string text, int caret, out SymbolDef def, out int keyLen)
    {
        def = null; keyLen = 0;
        if (string.IsNullOrEmpty(text)) return false;
        if (caret < 0 || caret > text.Length) caret = text.Length;
        SymbolDef best = null; int bestLen = 0;
        foreach (var s in all)
            foreach (var key in s.keys)
            {
                if (key.Length > caret || key.Length <= bestLen) continue;
                if (string.CompareOrdinal(text, caret - key.Length, key, 0, key.Length) != 0) continue;
                if (char.IsLetterOrDigit(key[0]) && caret - key.Length > 0)
                {
                    char before = text[caret - key.Length - 1];
                    if (char.IsLetterOrDigit(before)) continue;     // "api" không gợi ý π, "11/2" không gợi ý ½
                }
                best = s; bestLen = key.Length;
            }
        if (best == null) return false;
        def = best; keyLen = bestLen;
        return true;
    }

    // ------------------------------------------------------------------ chuẩn hóa trước khi chấm
    const string SupDigits = "⁰¹²³⁴⁵⁶⁷⁸⁹", SubDigits = "₀₁₂₃₄₅₆₇₈₉";

    static string Map(string digits, string table)
    {
        var sb = new StringBuilder(digits.Length);
        foreach (char c in digits) sb.Append(c >= '0' && c <= '9' ? table[c - '0'] : c);
        return sb.ToString();
    }

    static readonly Regex ChemSubscript = new Regex(@"(?<=[A-Z][a-z]?|\))(\d+)", RegexOptions.Compiled);
    static readonly Regex UnitPower = new Regex(@"(?<![A-Za-z])(mm|cm|dm|km|m)([23])(?![\w])", RegexOptions.Compiled);
    static readonly Regex IonCharge = new Regex(@"(?<=[A-Z][a-z]?\d*)\^(\d*)([+-])", RegexOptions.Compiled);
    static readonly Regex Power = new Regex(@"\^(\d+)", RegexOptions.Compiled);
    static readonly Regex Subscript = new Regex(@"_(\d+)", RegexOptions.Compiled);

    /// <summary>
    /// Chuẩn hóa ký hiệu: H2O thành H₂O, x^2 thành x², m3 thành m³, Fe^3+ thành Fe³⁺, -&gt; thành →, &lt;= thành ≤...
    /// Gõ sẵn ký hiệu đúng thì giữ nguyên. Đây là tiện ích, không phải điều kiện để được điểm.
    /// </summary>
    public static string NormalizeSymbols(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\r\n", "\n").Replace('\r', '\n');
        s = s.Replace("<=>", "⇌").Replace("<->", "↔").Replace("->", "→");
        s = s.Replace("<=", "≤").Replace(">=", "≥").Replace("!=", "≠").Replace("~=", "≈").Replace("+-", "±");
        s = IonCharge.Replace(s, m => Map(m.Groups[1].Value, SupDigits) + (m.Groups[2].Value == "+" ? "⁺" : "⁻"));
        s = Power.Replace(s, m => Map(m.Groups[1].Value, SupDigits));
        s = Subscript.Replace(s, m => Map(m.Groups[1].Value, SubDigits));
        s = UnitPower.Replace(s, m => m.Groups[1].Value + (m.Groups[2].Value == "2" ? "²" : "³"));
        // Chỉ số hóa học: H2O → H₂O. Bỏ qua số đứng ngay sau một chữ cái hoa đơn lẻ trong từ thường (vd "Bài 13" không ảnh hưởng vì "i" là chữ thường).
        s = ChemSubscript.Replace(s, m => Map(m.Groups[1].Value, SubDigits));
        return s;
    }

    /// <summary>
    /// Chuẩn bị lời giải gõ tay: kiểm độ dài, chuẩn hóa ký hiệu, bỏ khoảng trắng thừa, bỏ dòng trống rồi đánh số từng dòng.
    /// lines[i] là dòng thứ i+1 (đã bỏ khoảng trắng đầu và cuối).
    /// </summary>
    public static SubmitCheck Prepare(string raw, out string[] lines)
    {
        lines = new string[0];
        if (string.IsNullOrWhiteSpace(raw)) return SubmitCheck.Empty;
        if (raw.Length > MaxChars) return SubmitCheck.TooLong;
        var list = new List<string>();
        foreach (var l in NormalizeSymbols(raw).Split('\n'))
        {
            var t = l.Trim();
            if (t.Length > 0) list.Add(t);
        }
        lines = list.ToArray();
        int chars = 0; foreach (var l in lines) chars += l.Length;
        return chars < MinChars ? SubmitCheck.TooShort : SubmitCheck.Ok;
    }

    /// <summary>Văn bản đánh số dòng ("1 ...\n2 ...") để đưa vào prompt chấm (E4) hoặc hiển thị.</summary>
    public static string Numbered(string[] lines)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Length; i++) sb.Append(i + 1).Append(' ').Append(lines[i]).Append('\n');
        return sb.ToString().TrimEnd('\n');
    }
}
