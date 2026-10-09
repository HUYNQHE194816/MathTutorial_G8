using System;
using System.Collections.Generic;

// Lược đồ bài tự luận (E1). Dùng JsonUtility nên chỉ có class/list/mảng, không có Dictionary.
// File này không dùng UnityEngine để server ASP.NET (Server/EssayServer) dùng chung.
// Dữ liệu: Assets/DataBank/Essay/essay_problems.json

[Serializable]
public class EssayParam
{
    public string name;
    public double[] values;      // game chọn 1 giá trị cho mỗi tham số
}

/// <summary>
/// Đại lượng do CODE tính (AI không tự tính). Hai loại:
/// - Số: formula, unit, decimals.
/// - Chọn chữ: có "above"/"below" – formula > threshold thì dùng "above", ngược lại dùng "below".
/// </summary>
[Serializable]
public class EssayDerived
{
    public string id;
    public string formula;       // vd "m/V"; dùng được tham số và đại lượng đã khai báo phía trên
    public string unit;
    public int decimals = 2;
    public double threshold;
    public string above;
    public string below;
    public bool IsChoice => !string.IsNullOrEmpty(above);
}

[Serializable]
public class EssayRubricStep
{
    public string id;            // s1, s2...
    public int points;
    public string label;         // tên ô nhập ở bậc 1 (Công thức, Thế số, Kết quả...)
    public string desc;          // có thể chứa {tham_số} và {đại_lượng}
    /// <summary>
    /// Dùng cho BỘ CHẤM GIẢ (E2), sẽ được AI thay ở E3. Mỗi phần tử là một "ý phải có";
    /// dùng | để liệt kê các cách viết tương đương. Khi so khớp bỏ dấu, hoa/thường và khoảng trắng.
    /// Tiền tố "#": so khớp theo giá trị số (vd "#{D}"). Tiền tố "~": đơn vị, phải đứng ngay sau một chữ số.
    /// Có thể chứa {tham_số} và {đại_lượng}.
    /// </summary>
    public string[] expect;
}

[Serializable]
public class EssayFinalAnswer
{
    public string derivedId;     // đại lượng dùng làm đáp số cuối
    public string unit;
    public double tolerance;
}

[Serializable]
public class EssayMisconception
{
    public string stepId;
    public string[] tags;        // thẻ lỗi quan niệm, nuôi hàng đợi Oan Hồn
}

[Serializable]
public class EssayProblem
{
    public string id;
    public string subject;       // "ly" | "hoa" | "sinh"
    public string concept;       // khái niệm để tính mastery
    public int level;            // 1 = Thông hiểu, 2 = Vận dụng, 3 = Vận dụng cao
    public int scaffold;         // bậc Phong Ấn mặc định: 1 điền rune, 2 viết từng bước, 3 giải tự do
    public string statement;     // có {tham_số}
    public EssayParam[] parameters;
    public EssayDerived[] derived;
    public EssayRubricStep[] rubric;
    public EssayFinalAnswer finalAnswer;   // bài lời (Sinh) không có: derivedId rỗng
    public EssayMisconception[] misconceptions;
    public string[] hints;       // gợi ý bậc 1-3 (bậc 4 là lời giải đầy đủ, chỉ khi học sinh xin)

    public int MaxScore { get { int t = 0; foreach (var r in rubric) t += r.points; return t; } }
    public bool HasNumericAnswer => finalAnswer != null && !string.IsNullOrEmpty(finalAnswer.derivedId);
}

[Serializable]
public class EssayProblemSet
{
    public EssayProblem[] problems;
}
