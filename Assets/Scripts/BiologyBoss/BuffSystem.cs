using System.Collections.Generic;
using UnityEngine;

public class BuffDef
{
    public string Id, Name, Desc; public Color Color; public System.Action<PlayerKnight> Apply;
    public BuffDef(string id, string n, string d, Color c, System.Action<PlayerKnight> a) { Id = id; Name = n; Desc = d; Color = c; Apply = a; }
    /// <summary>Icon pixel-art vẽ bằng code (xem ProcSprites.BuffIcon).</summary>
    public Sprite Icon => ProcSprites.BuffIcon(Id);
}

/// <summary>
/// Danh sách buff. Muốn thêm buff mới: thêm 1 dòng vào mảng All và 1 case vẽ icon trong ProcSprites.DrawIcon.
/// Mỗi buff chỉ chọn được 1 lần: buff đã có sẽ không xuất hiện lại và không cộng dồn hiệu ứng.
/// </summary>
public static class BuffSystem
{
    public static readonly BuffDef[] All =
    {
        new BuffDef("fire",    "Kiếm Lửa",    "Kiếm đỏ rực, chém gây đốt 3 giây", new Color(1f,.4f,.25f),    p => p.FireSword = true),
        new BuffDef("ice",     "Kiếm Băng",   "Làm rồng đánh chậm; mỗi 3s: 3 hàng băng đóng băng", new Color(.55f,.9f,1f), p => p.IceSword = true),
        new BuffDef("haste",   "Tốc Chém",    "Tốc độ chém +25%",            new Color(1f,.85f,.3f),   p => p.AtkInterval *= .8f),
        new BuffDef("heart",   "Tim Rồng",    "Máu tối đa +20, hồi 40 máu",  new Color(.9f,.3f,.4f),   p => { p.MaxHp += 20f; p.Heal(40f); }),
        new BuffDef("boots",   "Giày Gió",    "Tốc độ chạy +15%",            new Color(.5f,.85f,.9f),  p => p.Speed *= 1.15f),
        new BuffDef("vamp",    "Hút Máu",     "Hồi 15% sát thương gây ra",   new Color(.75f,.25f,.6f), p => p.Lifesteal = .15f),
        new BuffDef("shield",  "Khiên Sáng",  "Chặn 2 đòn tấn công",         new Color(.43f,.9f,1f),   p => p.Shield += 2),
        new BuffDef("crit",    "Chí Mạng",    "Tỉ lệ bạo kích +15%",         new Color(1f,.75f,.2f),   p => p.Crit += .15f),
        new BuffDef("long",    "Kiếm Dài",    "Tầm chém +20%",               new Color(.6f,.7f,1f),    p => p.Range *= 1.2f),
        new BuffDef("thunder", "Sấm Sét",     "Sét đánh rồng mỗi 3 giây",    new Color(.95f,.95f,.5f), p => p.Thunder = 1),
        new BuffDef("regen",   "Hồi Phục",    "Hồi 1 máu mỗi giây",          new Color(.4f,.85f,.5f),  p => p.Regen = 1f),
        new BuffDef("reflect", "Chém Phản",   "Chém hất đạn lửa bay ngược về phía rồng",        new Color(.5f,.9f,1f),   p => p.Reflect = true),
        new BuffDef("ally",    "Đệ Hiệp Sĩ",  "Đệ đi theo hiệp sĩ, tự bắn phép vào địch",  new Color(.45f,.9f,.65f), p => p.SpawnCompanion()),
    };

    /// <summary>Số buff tối đa người chơi có thể giữ, cứ sau bao nhiêu câu thì được chọn, và số lựa chọn mỗi lần.</summary>
    public const int MaxBuffs = 5, PickEvery = 3, Choices = 3;

    /// <summary>Còn buff nào chưa sở hữu không?</summary>
    public static bool HasAvailable(ICollection<BuffDef> owned)
    {
        foreach (var b in All) if (owned == null || !owned.Contains(b)) return true;
        return false;
    }

    /// <summary>Bốc n buff KHÁC NHAU và CHƯA SỞ HỮU để người chơi chọn 1.</summary>
    public static BuffDef[] PickChoices(ICollection<BuffDef> owned, int n = Choices)
    {
        var pool = new List<BuffDef>();
        foreach (var b in All) if (owned == null || !owned.Contains(b)) pool.Add(b);
        var res = new BuffDef[Mathf.Min(n, pool.Count)];
        for (int i = 0; i < res.Length; i++)
        {
            int k = UnityEngine.Random.Range(0, pool.Count);
            res[i] = pool[k]; pool.RemoveAt(k);
        }
        return res;
    }
}
