using System.Collections.Generic;

/// <summary>
/// BỘ CHẤM GIẢ cho lời giải gõ tự do một ô (E2, không dùng AI). Khác EssayFakeGrader (mỗi bước một ô nhập):
/// ở đây học sinh viết cả bài, bộ chấm tìm trong TOÀN BỘ bài các "ý phải có" (trường expect của barem) của từng bước,
/// rồi báo số dòng khớp. Ở E4 sẽ thay bằng AI chấm; kiểu kết quả EssayGradeResult giữ nguyên.
/// Quy tắc: đủ mọi ý = Correct (đủ điểm); có ít nhất một ý = Partial (nửa điểm, làm tròn xuống); không thấy ý nào = Missing.
/// Chấm giả không phân biệt được "viết sai" với "chưa viết", nên chưa dùng trạng thái Wrong.
/// </summary>
public static class EssayFreeformGrader
{
    public static EssayGradeResult Grade(EssayProblem p, EssayVariant v, string[] lines)
    {
        var res = new EssayGradeResult { steps = new EssayStepResult[p.rubric.Length], maxScore = p.MaxScore };
        // Nối các dòng bằng ';' để số ở hai dòng khác nhau không dính thành một số khi bỏ khoảng trắng.
        string whole = string.Join(" ; ", lines ?? new string[0]);

        for (int i = 0; i < p.rubric.Length; i++)
        {
            var expect = v.expect[i];
            int matched = 0;
            if (expect != null) foreach (var g in expect) if (EssayFakeGrader.Matches(g, whole)) matched++;

            EssayStepStatus status;
            if (expect == null || expect.Length == 0 || matched == 0) status = EssayStepStatus.Missing;
            else status = matched == expect.Length ? EssayStepStatus.Correct : EssayStepStatus.Partial;

            int max = p.rubric[i].points;
            int pts = status == EssayStepStatus.Correct ? max : status == EssayStepStatus.Partial ? max / 2 : 0;
            res.steps[i] = new EssayStepResult { id = p.rubric[i].id, status = status, points = pts, maxPoints = max, lines = FindLines(expect, lines) };
            res.score += pts;
        }
        return res;
    }

    // Các dòng (đánh số từ 1) có chứa ít nhất một "ý phải có" của bước.
    static int[] FindLines(string[] expect, string[] lines)
    {
        var found = new List<int>();
        if (expect != null && lines != null)
            for (int li = 0; li < lines.Length; li++)
                foreach (var g in expect)
                    if (EssayFakeGrader.Matches(g, lines[li])) { found.Add(li + 1); break; }
        return found.ToArray();
    }
}
