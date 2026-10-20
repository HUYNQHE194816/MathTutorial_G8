using UnityEngine;

/// <summary>Lựa chọn của một người chơi trong một vòng.</summary>
public struct DuelChoice
{
    public int answer;   // 0..3; -1 = không chọn / hết giờ
    public int bet;      // 0 (thủ thế), 10, 20 hoặc 30

    public DuelChoice(int answer, int bet) { this.answer = answer; this.bet = bet; }
    public static DuelChoice None { get { return new DuelChoice(-1, 0); } }
}

/// <summary>Kết quả một vòng, nhìn từ phía "mình".</summary>
public struct DuelRoundResult
{
    public bool myCorrect, oppCorrect;   // trả lời đúng (kể cả khi cược 0)
    public int myLoss, oppLoss;          // máu mỗi bên mất trong vòng này
}

/// <summary>
/// Luật "Cược Tri Thức". Toàn bộ là số nguyên và không có ngẫu nhiên, nên hai máy tính cùng dữ kiện
/// sẽ ra cùng kết quả (đối xứng nhau).
/// </summary>
public static class DuelRules
{
    public const int MaxHp = 150;
    public static readonly int[] Bets = { 0, 10, 20, 30 };

    /// <summary>Đưa lựa chọn về dạng hợp lệ: đáp án ngoài 0..3 = không chọn (và cược 0); mức cược lạ = thủ thế.</summary>
    public static DuelChoice Sanitize(DuelChoice c)
    {
        if (c.answer < 0 || c.answer > 3) return DuelChoice.None;
        int bet = 0;
        foreach (int b in Bets) if (b == c.bet) bet = b;
        return new DuelChoice(c.answer, bet);
    }

    public static DuelRoundResult Resolve(DuelChoice mine, DuelChoice opp, int correctIndex)
    {
        mine = Sanitize(mine);
        opp = Sanitize(opp);

        bool myOk = mine.answer == correctIndex;
        bool oppOk = opp.answer == correctIndex;

        // Sát thương mỗi bên gây ra: đúng mới gây, bằng mức cược
        int myDealt = myOk ? mine.bet : 0;
        int oppDealt = oppOk ? opp.bet : 0;

        // Thủ thế (cược 0) nhận một nửa sát thương
        int myLoss = mine.bet == 0 ? oppDealt / 2 : oppDealt;
        int oppLoss = opp.bet == 0 ? myDealt / 2 : myDealt;

        // Đã trả lời mà sai thì tự mất nửa mức cược
        if (mine.answer >= 0 && !myOk) myLoss += mine.bet / 2;
        if (opp.answer >= 0 && !oppOk) oppLoss += opp.bet / 2;

        return new DuelRoundResult { myCorrect = myOk, oppCorrect = oppOk, myLoss = myLoss, oppLoss = oppLoss };
    }
}

/// <summary>Trạng thái một trận: máu hai bên, số vòng đã chơi, số câu đúng, người thắng.</summary>
public class DuelMatch
{
    public readonly int totalRounds;
    public int roundsDone;
    public int myHp = DuelRules.MaxHp, oppHp = DuelRules.MaxHp;
    public int myCorrectCount, oppCorrectCount;
    public DuelRoundResult last;

    public DuelMatch(int totalRounds) { this.totalRounds = totalRounds; }

    public bool Over { get { return myHp <= 0 || oppHp <= 0 || roundsDone >= totalRounds; } }

    /// <summary>1 = mình thắng, -1 = đối thủ thắng, 0 = hòa. Máu nhiều hơn thắng; bằng máu thì ai đúng nhiều câu hơn thắng.</summary>
    public int Winner
    {
        get
        {
            if (myHp != oppHp) return myHp > oppHp ? 1 : -1;
            if (myCorrectCount != oppCorrectCount) return myCorrectCount > oppCorrectCount ? 1 : -1;
            return 0;
        }
    }

    /// <summary>Chơi một vòng. Trận đã kết thúc thì không làm gì.</summary>
    public DuelRoundResult Play(DuelChoice mine, DuelChoice opp, int correctIndex)
    {
        if (Over) return last;
        last = DuelRules.Resolve(mine, opp, correctIndex);
        myHp = Mathf.Max(0, myHp - last.myLoss);
        oppHp = Mathf.Max(0, oppHp - last.oppLoss);
        if (last.myCorrect) myCorrectCount++;
        if (last.oppCorrect) oppCorrectCount++;
        roundsDone++;
        return last;
    }
}