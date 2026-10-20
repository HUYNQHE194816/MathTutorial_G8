#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Kiểm tra ngân hàng câu hỏi Đối kháng: số câu, tính "cùng seed = cùng bộ câu".</summary>
public static class DuelBankTest
{
    [MenuItem("Tools/KHTN 8/Duel - Thử ngân hàng câu hỏi")]
    static void Run()
    {
        Debug.Log("[DuelBank] Tổng số câu: " + DuelQuestionBank.Count);

        var plan = DuelQuestionBank.DefaultPlan;
        var a = DuelQuestionBank.BuildMatch(12345, null, plan);
        var b = DuelQuestionBank.BuildMatch(12345, null, plan);

        bool same = a.Count == b.Count;
        for (int i = 0; same && i < a.Count; i++) same = a[i].text == b[i].text;
        Debug.Log("[DuelBank] Seed 12345: " + a.Count + " câu, hai lần dựng " + (same ? "GIỐNG nhau (đúng)" : "KHÁC nhau (SAI!)"));

        for (int i = 0; i < a.Count; i++)
            Debug.Log("   Vòng " + (i + 1) + " [" + a[i].subject + ", mức " + a[i].level + ", đáp án " + (char)('A' + a[i].correct) + "] " + a[i].text);

        var other = DuelQuestionBank.BuildMatch(999, null, plan);
        Debug.Log("[DuelBank] Seed 999, câu đầu: " + (other.Count > 0 ? other[0].text : "(trống)"));

        var ly = DuelQuestionBank.BuildMatch(12345, "Lý", plan);
        Debug.Log("[DuelBank] Chỉ môn Lý: " + ly.Count + " câu, câu đầu thuộc môn " + (ly.Count > 0 ? ly[0].subject : "?"));
    }
}
#endif