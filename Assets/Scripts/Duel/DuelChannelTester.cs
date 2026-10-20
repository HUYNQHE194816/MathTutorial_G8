#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEngine;

/// <summary>Kiểm tra kênh lựa chọn theo vòng với bot. Chỉ có trong Editor.</summary>
public class DuelChannelTester : MonoBehaviour
{
    [MenuItem("Tools/KHTN 8/Duel - Thử kênh bot (cần đang Play)")]
    static void Start3()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[DuelChannel] Hãy bấm Play trước rồi mới chọn menu này."); return; }
        if (FindFirstObjectByType<DuelChannelTester>() != null) return;
        new GameObject("DuelChannelTester").AddComponent<DuelChannelTester>();
    }

    void Start() { StartCoroutine(Run()); }

    IEnumerator Run()
    {
        var be = DuelBackend.Current;
        if (!(be is BotBackend))
        {
            Debug.LogWarning("[DuelChannel] Backend hiện không phải bot (đang bật DUEL_UGS?). Bỏ qua.");
            Destroy(gameObject); yield break;
        }

        be.Connect("Tester");
        float oldScale = BotBackend.DelayScale;
        BotBackend.DelayScale = 0.15f;                 // bot trả lời nhanh để thử cho gọn
        int[] correct = { 2, 0, 3 };
        int good = 0;

        for (int r = 0; r < correct.Length; r++)
        {
            be.BeginRound(r, correct[r]);

            DuelChoice c;
            bool early = be.TryGetPeerChoice(r, out c);          // vừa bắt đầu vòng: chưa thể có
            float t0 = Time.unscaledTime;
            while (!be.TryGetPeerChoice(r, out c) && Time.unscaledTime - t0 < 10f) yield return null;

            bool got = be.TryGetPeerChoice(r, out c);
            float waited = Time.unscaledTime - t0;

            DuelChoice again; be.TryGetPeerChoice(r, out again);
            bool valid = c.answer >= -1 && c.answer <= 3 && System.Array.IndexOf(DuelRules.Bets, c.bet) >= 0;
            bool stable = again.answer == c.answer && again.bet == c.bet;
            bool otherRoundEmpty = !be.TryGetPeerChoice(r + 5, out again);   // vòng chưa tồn tại: không được có dữ liệu

            bool ok = !early && got && valid && stable && otherRoundEmpty && !be.PeerLeft;
            if (ok) good++;
            Debug.Log("[DuelChannel] Vòng " + (r + 1) + ": đầu vòng " + (early ? "ĐÃ có (SAI)" : "chưa có (đúng)") +
                      ", nhận sau " + waited.ToString("0.0") + "s, đáp án " + c.answer + ", cược " + c.bet +
                      (ok ? "  ✔" : "  ✘ CÓ LỖI"));
        }

        BotBackend.DelayScale = oldScale;
        Debug.Log("[DuelChannel] Đạt " + good + "/" + correct.Length + " vòng." + (good == correct.Length ? " Kênh bot ổn." : " CÓ LỖI."));
        Destroy(gameObject);
    }
}
#endif