#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Kiểm tra luật trận Đối kháng bằng các tình huống có đáp án biết trước.</summary>
public static class DuelRulesTest
{
    static int pass, total;

    static DuelChoice C(int answer, int bet) { return new DuelChoice(answer, bet); }

    static void Check(string name, bool ok)
    {
        total++;
        if (ok) pass++; else Debug.LogError("[DuelRules] SAI: " + name);
    }

    [MenuItem("Tools/KHTN 8/Duel - Thử luật trận")]
    static void Run()
    {
        pass = total = 0;
        const int right = 2;     // đáp án đúng là C

        // ---- Từng vòng ----
        var r = DuelRules.Resolve(C(2, 20), C(2, 10), right);
        Check("1. Cùng đúng, cược 20 và 10: không ai mất máu", r.myLoss == 0 && r.oppLoss == 0);

        r = DuelRules.Resolve(C(2, 30), C(1, 20), right);
        Check("2. Mình đúng 30, đối thủ sai 20: mình 0, đối thủ 30 + 10 phạt = 40", r.myLoss == 0 && r.oppLoss == 40);

        r = DuelRules.Resolve(C(2, 30), C(1, 0), right);
        Check("3. Mình đúng 30, đối thủ thủ thế (trả lời sai, không phạt): nhận một nửa 30 = 15", r.myLoss == 0 && r.oppLoss == 15);

        r = DuelRules.Resolve(C(1, 30), C(2, 10), right);
        Check("4. Mình sai cược 30, đối thủ đúng cược 10: mức chung 30 => mất 30 + 15 phạt = 45", r.myLoss == 45 && r.oppLoss == 0);

        r = DuelRules.Resolve(C(2, 10), C(1, 30), right);
        Check("4b. Mình đúng cược 10, đối thủ sai cược 30: mức chung 30 => đối thủ mất 30 + 15 phạt = 45", r.myLoss == 0 && r.oppLoss == 45);

        r = DuelRules.Resolve(C(2, 30), C(1, 10), right);
        Check("4c. Mình đúng cược 30, đối thủ sai cược 10: đối thủ mất 30 + 5 phạt = 35", r.myLoss == 0 && r.oppLoss == 35);

        r = DuelRules.Resolve(C(2, 0), C(1, 30), right);
        Check("4d. Mình thủ thế mà đúng, đối thủ sai cược 30: không gây sát thương, đối thủ chỉ tự phạt 15", r.myLoss == 0 && r.oppLoss == 15);

        r = DuelRules.Resolve(C(-1, 30), C(2, 20), right);
        Check("5. Mình bỏ trống (coi như thủ thế): nhận một nửa 20 = 10, không phạt", r.myLoss == 10 && r.oppLoss == 0);

        r = DuelRules.Resolve(C(2, 0), C(2, 0), right);
        Check("6. Hai bên thủ thế: không ai mất máu", r.myLoss == 0 && r.oppLoss == 0);

        r = DuelRules.Resolve(C(1, 30), C(0, 30), right);
        Check("7. Cả hai sai cược 30: mỗi bên tự mất 15", r.myLoss == 15 && r.oppLoss == 15);

        r = DuelRules.Resolve(C(2, 30), C(2, 30), right);
        Check("8. Cả hai đúng cược 30: không ai mất máu", r.myLoss == 0 && r.oppLoss == 0);

        // ---- Tính đối xứng: đổi chỗ hai người thì kết quả phải đảo ngược ----
        bool sym = true;
        int[] answers = { -1, 0, 1, 2, 3 };
        foreach (int a1 in answers) foreach (int b1 in DuelRules.Bets)
                foreach (int a2 in answers) foreach (int b2 in DuelRules.Bets)
                    {
                        var x = DuelRules.Resolve(C(a1, b1), C(a2, b2), right);
                        var y = DuelRules.Resolve(C(a2, b2), C(a1, b1), right);
                        if (x.myLoss != y.oppLoss || x.oppLoss != y.myLoss) sym = false;
                    }
        Check("9. Đối xứng với mọi tổ hợp (hai máy ra cùng kết quả)", sym);

        var s = DuelRules.Sanitize(C(1, 15));
        var s2 = DuelRules.Sanitize(C(7, 30));
        Check("10. Mức cược lạ -> 0, đáp án ngoài 0..3 -> không chọn", s.bet == 0 && s.answer == 1 && s2.answer == -1 && s2.bet == 0);

        // ---- Cả trận ----
        var m = new DuelMatch(10);
        while (!m.Over) m.Play(C(1, 10), C(2, 30), right);   // mình sai, đối thủ đúng cược 30: mất 35 mỗi vòng
        Check("11. Mất 35/vòng: hết máu sau 5 vòng, đối thủ thắng",
              m.roundsDone == 5 && m.myHp == 0 && m.oppHp == DuelRules.MaxHp && m.Winner == -1);

        m = new DuelMatch(10);
        while (!m.Over) m.Play(C(2, 30), C(2, 30), right);   // cả hai đúng cược 30: không ai mất máu, chơi đủ 10 vòng
        Check("12. Cả hai luôn đúng: đủ 10 vòng, máu đầy, đúng bằng nhau: hòa",
              m.roundsDone == 10 && m.myHp == DuelRules.MaxHp && m.oppHp == DuelRules.MaxHp && m.Winner == 0);

        m = new DuelMatch(1);
        m.Play(C(2, 0), C(1, 0), right);                     // bằng máu, mình đúng 1 câu còn đối thủ 0
        Check("13. Bằng máu: ai đúng nhiều hơn thắng",
              m.myHp == m.oppHp && m.myCorrectCount == 1 && m.oppCorrectCount == 0 && m.Winner == 1);

        m = new DuelMatch(10);
        while (!m.Over) m.Play(DuelChoice.None, DuelChoice.None, right);
        Check("14. Bỏ trống cả 10 vòng: bằng máu, hòa",
              m.roundsDone == 10 && m.myHp == DuelRules.MaxHp && m.oppHp == DuelRules.MaxHp && m.Winner == 0);

        int before = m.roundsDone;
        m.Play(C(2, 30), C(1, 0), right);
        Check("15. Trận đã kết thúc thì không chơi thêm được", m.roundsDone == before);

        Debug.Log("[DuelRules] Đạt " + pass + "/" + total + " kiểm tra." + (pass == total ? " Luật trận ổn." : " CÓ LỖI, xem các dòng đỏ ở trên."));
    }
}
#endif