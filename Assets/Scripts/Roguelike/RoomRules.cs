using System;
using System.Collections.Generic;

public enum EssayDifficulty { Easy = 0, Normal = 1, Hard = 2, Expert = 3 }
public enum GradeBand { Fail, Pass, Good }

/// <summary>
/// Luật của một màn (mục 4 và 15.1 của kế hoạch). Mọi con số ở đây là GIÁ TRỊ KHỞI ĐIỂM, sẽ cân bằng khi chơi thử.
/// Không dùng UnityEngine để dễ kiểm thử.
/// </summary>
public static class RoomRules
{
    // ---- Ngưỡng chấm (đã chốt, mục 4.1) ----
    public const float PassRatio = 0.5f, GoodRatio = 0.8f;
    public const int MaxAttempts = 3;              // làm lại tối đa 3 lần mỗi rương, rồi mới được phá khóa bằng Mực

    // ---- Con số khởi điểm (chưa chốt trong kế hoạch) ----
    public const int UnlockInkCost = 50;           // giá phá khóa
    public const int InkPerPoint = 10;             // Mực mỗi điểm barem đạt được (cùng quy ước với Phong Ấn bản cũ)
    public const float HintInkPenalty = 0.10f;     // mỗi gợi ý trả phí trừ 10% Mực của lượt đó (mục 4.4)
    public const int InkPerDummy = 5;

    public static readonly string[] RarityNames = { "Thường", "Hiếm", "Sử thi", "Huyền thoại" };

    public static string DifficultyName(EssayDifficulty d)
    {
        switch (d)
        {
            case EssayDifficulty.Easy: return "Dễ";
            case EssayDifficulty.Normal: return "Bình thường";
            case EssayDifficulty.Hard: return "Khó";
            default: return "Siêu khó";
        }
    }

    /// <summary>Mô tả ngắn hiện trên nút chọn độ khó (mục 15.1).</summary>
    public static string DifficultyBlurb(EssayDifficulty d)
    {
        switch (d)
        {
            case EssayDifficulty.Easy: return "2-3 bước · nhiều gợi ý · vũ khí thường";
            case EssayDifficulty.Normal: return "3-4 bước · gợi ý vừa · vũ khí thường đến hiếm";
            case EssayDifficulty.Hard: return "4-5 bước · ít gợi ý · vũ khí hiếm đến sử thi";
            default: return "5-6 bước · không gợi ý · vũ khí sử thi đến huyền thoại, nhiều Mực";
        }
    }

    public static float InkMultiplier(EssayDifficulty d)
    {
        switch (d)
        {
            case EssayDifficulty.Easy: return 1f;
            case EssayDifficulty.Normal: return 1.25f;
            case EssayDifficulty.Hard: return 1.5f;
            default: return 2f;
        }
    }

    // ---- Độ khó của cả lượt chơi (chọn một lần ở màn Chọn độ khó): quái đông hơn, nhanh hơn, trâu hơn, đau hơn; câu hỏi khó hơn ----
    // Chỉ số theo thứ tự Dễ, Bình thường, Khó, Siêu khó. Mọi con số là giá trị khởi điểm, cân bằng khi chơi thử.
    static readonly float[] CountMuls  = { 1f, 1.4f, 1.9f, 2.5f };      // số quái mỗi đợt
    static readonly float[] HpMuls     = { 1f, 1.3f, 1.7f, 2.3f };      // máu quái
    static readonly float[] SpeedMuls  = { 1f, 1.1f, 1.22f, 1.35f };    // tốc độ di chuyển và tốc độ đạn của quái
    static readonly float[] DamageMuls = { 1f, 1.2f, 1.5f, 1.9f };      // sát thương quái gây ra
    static readonly float[] RateMuls   = { 1f, 1.15f, 1.35f, 1.6f };    // tần suất ra đòn (hồi chiêu chia cho số này)
    static readonly float[] EliteBonus = { 0f, .05f, .12f, .22f };      // cộng thêm vào tỉ lệ tinh anh
    static readonly int[] PoolBonuses  = { 0, 0, 1, 1 };                // mở loại quái mạnh sớm hơn mấy màn
    static readonly int[] ExtraWaves   = { 0, 0, 0, 1 };                // thêm đợt quái

    public static float CountMul(EssayDifficulty d) => CountMuls[(int)d];
    public static float HpMul(EssayDifficulty d) => HpMuls[(int)d];
    public static float SpeedMul(EssayDifficulty d) => SpeedMuls[(int)d];
    public static float DamageMul(EssayDifficulty d) => DamageMuls[(int)d];
    public static float RateMul(EssayDifficulty d) => RateMuls[(int)d];
    public static float EliteRateBonus(EssayDifficulty d) => EliteBonus[(int)d];
    public static int PoolBonus(EssayDifficulty d) => PoolBonuses[(int)d];
    public static int ExtraWaveCount(EssayDifficulty d) => ExtraWaves[(int)d];

    /// <summary>Danh hiệu ngắn hiện trên thẻ chọn độ khó.</summary>
    public static string DifficultyTag(EssayDifficulty d)
    {
        switch (d)
        {
            case EssayDifficulty.Easy: return "Màn sương còn mỏng.\nHầm ngục chưa lộ nanh vuốt.";
            case EssayDifficulty.Normal: return "Bầy quái bắt đầu\nbiết cắn trả.";
            case EssayDifficulty.Hard: return "Đàn quỷ nhe nanh,\nsai một bước là trả giá.";
            default: return "Vực thẳm gọi tên em.\nChỉ kẻ vững tri thức mới sống sót.";
        }
    }

    /// <summary>Số gợi ý tối đa được xin: Dễ nhiều, Siêu khó không có (bậc 4 làm ở E4).</summary>
    public static int MaxHints(EssayDifficulty d)
    {
        switch (d)
        {
            case EssayDifficulty.Easy: return 3;
            case EssayDifficulty.Normal: return 3;
            case EssayDifficulty.Hard: return 1;
            default: return 0;
        }
    }

    /// <summary>Mức Dễ tự hiện gợi ý đầu ngay từ đầu (mục 15.1: "hiện gợi ý đầu sớm"), không trừ Mực.</summary>
    public static bool AutoFirstHint(EssayDifficulty d) => d == EssayDifficulty.Easy;

    /// <summary>Lấy độ khó của bài: ưu tiên trường difficulty, nếu trống thì suy ra từ level cũ (1 Dễ, 2 Bình thường, 3 Khó).</summary>
    public static EssayDifficulty DifficultyOf(EssayProblem p)
    {
        switch ((p.difficulty ?? "").Trim().ToLowerInvariant())
        {
            case "easy": return EssayDifficulty.Easy;
            case "normal": return EssayDifficulty.Normal;
            case "hard": return EssayDifficulty.Hard;
            case "expert": return EssayDifficulty.Expert;
        }
        return p.level <= 1 ? EssayDifficulty.Easy : p.level == 2 ? EssayDifficulty.Normal : EssayDifficulty.Hard;
    }

    public static GradeBand Band(int score, int maxScore)
    {
        if (maxScore <= 0) return GradeBand.Fail;
        float r = (float)score / maxScore;
        return r >= GoodRatio ? GradeBand.Good : r >= PassRatio ? GradeBand.Pass : GradeBand.Fail;
    }

    public static bool DoorOpens(GradeBand b) => b != GradeBand.Fail;

    /// <summary>Độ hiếm vũ khí (0 Thường .. 3 Huyền thoại): rút trong khoảng của mức khó; Đạt thì giảm một bậc.</summary>
    public static int RollRarity(EssayDifficulty d, GradeBand band, Random rng)
    {
        int lo, hi;
        switch (d)
        {
            case EssayDifficulty.Easy: lo = 0; hi = 0; break;
            case EssayDifficulty.Normal: lo = 0; hi = 1; break;
            case EssayDifficulty.Hard: lo = 1; hi = 2; break;
            default: lo = 2; hi = 3; break;
        }
        int r = rng.Next(lo, hi + 1);
        if (band == GradeBand.Pass) r = Math.Max(0, r - 1);
        return r;
    }

    public static int InkFor(int score, EssayDifficulty d, int hintsPaid)
    {
        float k = Math.Max(0f, 1f - HintInkPenalty * hintsPaid);
        return (int)Math.Round(score * InkPerPoint * InkMultiplier(d) * k);
    }

    /// <summary>
    /// Chọn bài: đúng môn và đúng mức, chưa gặp trong lượt chơi. Nếu không có thì lấy mức gần nhất của cùng môn
    /// (kho đề mẫu còn mỏng), sau cùng mới cho gặp lại. Trả về null nếu môn chưa có bài nào.
    /// </summary>
    public static EssayProblem PickProblem(List<EssayProblem> all, string subject, EssayDifficulty want, HashSet<string> used, Random rng)
    {
        var pool = all.FindAll(p => p.subject == subject);
        if (pool.Count == 0) return null;
        var fresh = pool.FindAll(p => !used.Contains(p.id));
        var from = fresh.Count > 0 ? fresh : pool;
        int bestDist = int.MaxValue;
        foreach (var p in from) bestDist = Math.Min(bestDist, Math.Abs((int)DifficultyOf(p) - (int)want));
        var best = from.FindAll(p => Math.Abs((int)DifficultyOf(p) - (int)want) == bestDist);
        return best[rng.Next(best.Count)];
    }
}
