using UnityEngine;

/// <summary>
/// Toàn bộ lời thoại CỐ ĐỊNH của Giáo sư Meomeo (giọng nghiêm túc, kiểu thầy giáo).
/// Chỉnh giọng điệu ở đây, không cần đụng vào logic hội thoại.
/// Phần kiến thức theo từng câu (gợi ý, câu hỏi dẫn dắt...) do AI sinh qua CoachScript.
/// </summary>
public static class ProfessorLines
{
    public const string ProfessorName = "Giáo sư Meomeo";

    static string Pick(params string[] a) => a[UnityEngine.Random.Range(0, a.Length)];

    // ───────────── Mở đầu / kết thúc ─────────────

    public static string Intro(int count) =>
        $"Chào em, ta là Giáo sư Meomeo. Hôm nay chúng ta sẽ xem lại {count} câu em chưa làm đúng. " +
        "Sai không đáng sợ, điều đáng tiếc là không tìm hiểu vì sao mình sai. Chúng ta bắt đầu thôi.";

    public static string NoWrong() =>
        "Em không có câu nào sai. Rất tốt! Hãy tiếp tục giữ vững phong độ và quay lại thử thách tiếp theo.";

    public static string Summary(int fixedCount, int total)
    {
        string tail = fixedCount == total ? "Xuất sắc."
                    : fixedCount * 2 >= total ? "Khá tốt, em hãy tiếp tục luyện tập."
                    : "Hãy ôn lại những câu chưa sửa được. Ta tin em làm được.";
        return $"Buổi học kết thúc. Em đã tự sửa được {fixedCount}/{total} câu. {tail}";
    }

    // ───────────── Chẩn đoán nguyên nhân sai ─────────────

    public static string AskReason(int index, WrongQuestionItem item)
    {
        if (item.chosenIndex >= 0)
            return $"Câu {index + 1}: em đã chọn đáp án {(char)('A' + item.chosenIndex)}. " +
                   "Trước khi ta chỉ ra điều gì, hãy tự nhìn lại: lúc đó em nghĩ thế nào?";
        return $"Câu {index + 1}: em đã không kịp trả lời. Hãy nhìn lại xem nguyên nhân là gì?";
    }

    public static string[] ReasonChoices(bool timedOut) => new[]
    {
        "Em nhầm kiến thức hoặc công thức",
        "Em chưa nhớ rõ bài này",
        timedOut ? "Em hết thời gian" : "Em đọc đề chưa kỹ",
        "Em đoán bừa"
    };

    public static string ReactToReason(int reason, bool timedOut)
    {
        switch (reason)
        {
            case 0:
                return Pick("Nhầm lẫn xảy ra khi kiến thức chưa được hệ thống lại. Ta sẽ cùng tìm đúng chỗ em nhầm.",
                            "Nhầm công thức rất phổ biến. Quan trọng là hiểu bản chất để không nhầm nữa.");
            case 1:
                return Pick("Chưa nhớ thì ôn lại là được. Ta sẽ nhắc lại phần kiến thức cần thiết, em theo dõi nhé.",
                            "Trung thực với bản thân là bước đầu tiên của việc học. Ta ôn lại ngay bây giờ.");
            case 2:
                return timedOut
                    ? "Quản lý thời gian cũng là một kỹ năng. Nhưng trước hết, hãy kiểm tra xem em đã nắm kiến thức này chưa."
                    : "Đọc kỹ đề là kỹ năng quan trọng. Ta sẽ vừa ôn kiến thức vừa chỉ em cách đọc đề.";
            default:
                return Pick("Đoán bừa thì chỉ là may rủi, hiểu bài mới là chắc chắn. Ta sẽ giúp em hiểu thật sự.",
                            "Có thể em đã đoán, nhưng bây giờ ta sẽ biến nó thành hiểu biết thật sự.");
        }
    }

    public static string Preparing() =>
        "Hmm... để ta soạn bài giảng cho câu này. Em chờ ta một chút.";

    // ───────────── Các bước trong kịch bản AI ─────────────

    public static string Diagnosis(string misconception) => $"Theo ta quan sát, chỗ em nhầm là: {misconception}";

    public static string Hint(int n, string text) => n == 0 ? $"Gợi ý: {text}" : $"Gợi ý thêm: {text}";

    public static string GuideIntro(string text) => $"Hãy tự suy nghĩ câu hỏi nhỏ này: {text}";

    public static string CheckIntro(string text) => $"Bây giờ ta kiểm tra xem em đã hiểu chưa. {text}";

    public static string Recap(string text) => $"Điều cần ghi nhớ: {text}";

    public static string Correct() =>
        Pick("Chính xác.", "Đúng rồi. Em suy luận tốt.", "Tốt lắm, đúng như ta mong đợi.");

    public static string Wrong(string onWrong) =>
        string.IsNullOrWhiteSpace(onWrong)
            ? "Chưa chính xác. Hãy thử lại, suy nghĩ chậm hơn một chút."
            : $"Chưa chính xác. {onWrong} Hãy thử lại.";

    public static string Reveal(string correctLabel, string onWrong) =>
        $"Chưa được. Đáp án đúng là {correctLabel}. {onWrong}".Trim();

    // ───────────── Dự phòng khi AI không phản hồi ─────────────

    public static string FallbackIntro() =>
        "Hôm nay ta chưa kịp soạn bài giảng riêng cho câu này. Ta sẽ cho em xem đáp án và cách tự kiểm tra.";

    public static string FallbackReveal(string correctLabel) =>
        $"Đáp án đúng là {correctLabel}. Em hãy đọc lại đề và so sánh với lựa chọn của mình để tìm ra điểm khác biệt.";

    // ───────────── Kết quả từng câu ─────────────

    public static string OutcomeSelfFixed() =>
        Pick("Tốt! Em đã tự sửa được lỗi sai này.", "Rất tốt. Em đã hiểu bản chất của câu này.");

    public static string OutcomeNotYet() =>
        Pick("Không sao. Em hãy xem lại phần ghi nhớ, lần sau sẽ tốt hơn.",
             "Chưa hoàn hảo, nhưng em đã biết chỗ cần sửa. Đó là bước tiến.");
}