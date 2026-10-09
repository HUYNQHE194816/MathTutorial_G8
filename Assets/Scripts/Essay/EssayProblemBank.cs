using System.Collections.Generic;
using UnityEngine;

public static class EssayProblemBank
{
    /// <summary>Đọc từ nội dung JSON (kéo TextAsset essay_problems.json vào Inspector rồi truyền .text).</summary>
    public static List<EssayProblem> Parse(string json)
    {
        var set = JsonUtility.FromJson<EssayProblemSet>((json ?? "").TrimStart('\uFEFF'));
        return set != null && set.problems != null ? new List<EssayProblem>(set.problems) : new List<EssayProblem>();
    }
}
