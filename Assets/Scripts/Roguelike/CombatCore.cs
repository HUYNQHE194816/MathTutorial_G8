using System.Collections.Generic;
using UnityEngine;

// E3 – Lõi chiến đấu chặng 1 (mục 5 của kế hoạch). Mọi con số là GIÁ TRỊ KHỞI ĐIỂM, sẽ cân bằng khi chơi thử.
// Bản này chỉ có 4 nguyên tố đầu (Thủy, Hỏa, Băng, Lôi) và 5 vũ khí nhóm đầu; phần còn lại làm theo lô nhỏ ở các bước sau.

public enum Element { None = 0, Thuy = 1, Hoa = 2, Bang = 3, Loi = 4 }

/// <summary>Tiện ích dùng chung cho mọi thứ trong trận. Đồng hồ trận không chạy khi RoomManager.Frozen (đang viết lời giải).</summary>
public static class Combat
{
    public static float Now;                                   // giờ trận (giây), không tăng khi đóng băng
    public static float Dt => RoomManager.Frozen ? 0f : Time.deltaTime;
    public static readonly List<Monster> Monsters = new List<Monster>();
    public static PlayerCombat Player;

    public static void Reset() { Now = 0f; Monsters.Clear(); Player = null; }

    public static Color ElementColor(Element e)
    {
        switch (e)
        {
            case Element.Thuy: return new Color(.3f, .65f, 1f);
            case Element.Hoa: return new Color(1f, .45f, .15f);
            case Element.Bang: return new Color(.65f, .95f, 1f);
            case Element.Loi: return new Color(1f, .95f, .3f);
            default: return Color.white;
        }
    }

    public static string ElementName(Element e)
    {
        switch (e) { case Element.Thuy: return "Thủy"; case Element.Hoa: return "Hỏa"; case Element.Bang: return "Băng"; case Element.Loi: return "Lôi"; default: return "Không"; }
    }

    /// <summary>Tạo một SpriteRenderer có đường kính/kích thước thế giới cho trước (không phụ thuộc pixels-per-unit của sprite).</summary>
    public static SpriteRenderer MakeSr(string name, Transform parent, Sprite sprite, Color color, Vector2 size, int order, Vector2? localPos = null)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        if (localPos.HasValue) go.transform.localPosition = localPos.Value;
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = color; sr.sortingOrder = order;
        var b = sprite.bounds.size; go.transform.localScale = new Vector3(size.x / b.x, size.y / b.y, 1f);
        return sr;
    }

    public static Monster Nearest(Vector2 p, float maxR, Monster except = null)
    {
        Monster best = null; float bd = maxR;
        foreach (var m in Monsters)
        {
            if (m == null || m == except || m.Dead) continue;
            float d = Vector2.Distance(p, m.transform.position);
            if (d < bd) { bd = d; best = m; }
        }
        return best;
    }
}

// ====================================================================== nguyên tố và phản ứng (mục 5.4)
public static class Elements
{
    public const float ReactionWindow = 4f;       // hai nguyên tố khác nhau trong 4 giây thì phản ứng

    /// <summary>Gắn nguyên tố lên quái: tìm phản ứng với nguyên tố đang bám, nếu không có thì áp hiệu ứng riêng của nguyên tố.</summary>
    public static void Apply(Monster m, Element e)
    {
        if (e == Element.None || m == null || m.Dead) return;
        if (m.auraEl != Element.None && m.auraEl != e && Combat.Now - m.auraT <= ReactionWindow && TryReact(m, m.auraEl, e))
        { m.auraEl = Element.None; return; }

        m.auraEl = e; m.auraT = Combat.Now;
        switch (e)
        {
            case Element.Thuy: m.wetUntil = Combat.Now + 4f; break;
            case Element.Hoa: m.burnUntil = Combat.Now + 3f; break;
            case Element.Bang: m.slowUntil = Combat.Now + 3f; break;
            case Element.Loi:
                m.stunUntil = Mathf.Max(m.stunUntil, Combat.Now + (m.kind == MonsterKind.Robot && m.phase == 0 ? .9f : .3f));   // lôi làm robot choáng lâu hơn
                var hit = new List<Monster> { m };
                for (int i = 0; i < 2; i++)                               // giật nhảy sang tối đa 2 mục tiêu gần nhất
                {
                    Monster n = null; float bd = 3.5f;
                    foreach (var o in Combat.Monsters)
                    {
                        if (o == null || o.Dead || hit.Contains(o)) continue;
                        float d = Vector2.Distance(m.transform.position, o.transform.position);
                        if (d < bd) { bd = d; n = o; }
                    }
                    if (n == null) break;
                    hit.Add(n); n.Damage(5f, Element.None);
                }
                break;
        }
    }

    static bool Pair(Element a, Element b, Element x, Element y) => (a == x && b == y) || (a == y && b == x);

    static bool TryReact(Monster m, Element a, Element b)
    {
        Vector2 pos = m.transform.position;
        if (Pair(a, b, Element.Thuy, Element.Hoa))
        {
            foreach (var o in new List<Monster>(Combat.Monsters))
                if (o != null && !o.Dead && Vector2.Distance(pos, o.transform.position) <= 2.2f) o.Damage(20f, Element.None);
            RoomManager.Instance?.ShowReaction("Hơi nước", "nước gặp nhiệt thì bay hơi, thể tích nở ra rất nhanh nên gây nổ.");
            m.burnUntil = 0f; m.wetUntil = 0f; return true;
        }
        if (Pair(a, b, Element.Thuy, Element.Bang))
        {
            m.freezeUntil = Combat.Now + 2f; m.wetUntil = 0f; m.slowUntil = 0f;
            RoomManager.Instance?.ShowReaction("Đóng băng", "nước ướt gặp lạnh thì đông lại thành đá, quái bị giữ yên 2 giây.");
            return true;
        }
        if (Pair(a, b, Element.Thuy, Element.Loi))
        {
            foreach (var o in new List<Monster>(Combat.Monsters))
                if (o != null && !o.Dead && (o == m || o.wetUntil > Combat.Now) && Vector2.Distance(pos, o.transform.position) <= 4f) o.Damage(12f, Element.None);
            RoomManager.Instance?.ShowReaction("Dẫn điện", "nước có lẫn tạp chất dẫn điện, dòng điện lan sang mọi vật đang ướt.");
            return true;
        }
        if (Pair(a, b, Element.Hoa, Element.Bang))
        {
            m.Damage(30f, Element.None); m.slowUntil = 0f; m.burnUntil = 0f; m.freezeUntil = 0f;
            RoomManager.Instance?.ShowReaction("Tan băng", "nhiệt làm băng tan, lớp nước đá giãn nở đột ngột làm vỡ giáp, gây một đòn lớn.");
            return true;
        }
        return false;
    }
}

// ====================================================================== vũ khí (mục 5.3)
public enum WeaponId { Pistol, Rifle, Bow, Dagger, Staff }

public class WeaponDef
{
    public WeaponId id; public string name; public bool melee;
    public float damage, cooldown, speed, spreadDeg, range; public int count, pierce;
    public WeaponDef(WeaponId id, string name, bool melee, float damage, float cooldown, float speed, int count, float spreadDeg, int pierce, float range = 0f)
    { this.id = id; this.name = name; this.melee = melee; this.damage = damage; this.cooldown = cooldown; this.speed = speed; this.count = count; this.spreadDeg = spreadDeg; this.pierce = pierce; this.range = range; }
}

public class WeaponInstance
{
    public WeaponId id; public int rarity; public Element element;
    public WeaponDef Def => Weapons.Get(id);
    public float Damage => Def.damage * Weapons.RarityMult[Mathf.Clamp(rarity, 0, 3)];
    public string Label => Def.name + " [" + RoomRules.RarityNames[Mathf.Clamp(rarity, 0, 3)] + "]" + (element != Element.None ? " · " + Combat.ElementName(element) : "");
    public WeaponInstance(WeaponId id, int rarity, Element el = Element.None) { this.id = id; this.rarity = rarity; element = el; }
}

public static class Weapons
{
    public static readonly float[] RarityMult = { 1f, 1.25f, 1.5f, 2f };
    static readonly WeaponDef[] defs =
    {
        new WeaponDef(WeaponId.Pistol, "Súng ngắn", false, 10f, .35f, 14f, 1, 0f, 0),
        new WeaponDef(WeaponId.Rifle, "Súng trường", false, 6f, .12f, 17f, 1, 6f, 0),
        new WeaponDef(WeaponId.Bow, "Cung", false, 18f, .7f, 20f, 1, 0f, 2),
        new WeaponDef(WeaponId.Dagger, "Dao", true, 14f, .22f, 0f, 1, 0f, 0, 1.9f),
        new WeaponDef(WeaponId.Staff, "Gậy phép", false, 9f, .45f, 11f, 1, 0f, 0),
    };
    public static WeaponDef Get(WeaponId id) => defs[(int)id];
    public static readonly Element[] Elems = { Element.Thuy, Element.Hoa, Element.Bang, Element.Loi };

    /// <summary>Vũ khí ngẫu nhiên từ Rương Câu Đố; gậy phép luôn mang một nguyên tố ngẫu nhiên.</summary>
    public static WeaponInstance Roll(int rarity, System.Random rng)
    {
        var id = (WeaponId)rng.Next(defs.Length);
        return new WeaponInstance(id, rarity, id == WeaponId.Staff ? Elems[rng.Next(Elems.Length)] : Element.None);
    }
}

// ====================================================================== buff (mục 5.5)
public enum BuffId { ExtraShot, Bounce, Pierce, Resist, Infuse, Wave, AttackSpeed, KillHeal }

public class RunBuffDef
{
    public BuffId id; public string name, desc; public bool ranged, melee;
    public RunBuffDef(BuffId id, string name, string desc, bool ranged, bool melee) { this.id = id; this.name = name; this.desc = desc; this.ranged = ranged; this.melee = melee; }
}

public static class Buffs
{
    public static readonly RunBuffDef[] All =
    {
        new RunBuffDef(BuffId.ExtraShot, "Thêm đạn", "Mỗi loạt bắn thêm 1 viên", true, false),
        new RunBuffDef(BuffId.Bounce, "Đạn nảy", "Đạn nảy vào tường thêm 1 lần", true, false),
        new RunBuffDef(BuffId.Pierce, "Đạn xuyên", "Đạn xuyên thêm 1 mục tiêu", true, false),
        new RunBuffDef(BuffId.Resist, "Kháng nguyên tố", "Giảm 25% sát thương từ một nguyên tố (ngẫu nhiên)", true, true),
        new RunBuffDef(BuffId.Infuse, "Nhiễm nguyên tố", "Vũ khí chưa có nguyên tố được thêm một nguyên tố (ngẫu nhiên)", true, true),
        new RunBuffDef(BuffId.Wave, "Sóng chém", "Đòn chém bắn ra một đường sóng bay xa", false, true),
        new RunBuffDef(BuffId.AttackSpeed, "Tốc đánh", "Tăng 15% tốc độ bắn hoặc chém", true, true),
        new RunBuffDef(BuffId.KillHeal, "Hồi máu khi hạ quái", "Hồi 3 máu mỗi khi hạ một con quái", true, true),
    };

    /// <summary>Rút 3 buff ngẫu nhiên, bảo đảm ít nhất 1 buff hợp với vũ khí đang cầm.</summary>
    public static List<RunBuffDef> Offer(bool mainIsMelee, System.Random rng)
    {
        var pool = new List<RunBuffDef>(All);
        var suit = pool.FindAll(b => mainIsMelee ? b.melee : b.ranged);
        var res = new List<RunBuffDef>();
        var first = suit[rng.Next(suit.Count)]; res.Add(first); pool.Remove(first);
        while (res.Count < 3) { var b = pool[rng.Next(pool.Count)]; res.Add(b); pool.Remove(b); }
        for (int i = res.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = res[i]; res[i] = res[j]; res[j] = t; }
        return res;
    }
}

// ====================================================================== trạng thái lượt chơi, sống qua các lần nạp scene
public static class RunState
{
    public const int RoomsPerStage = 4;                       // 4 màn thường mỗi chặng; màn 5 là boss (E6)
    public static int stage = 1, room = 1;
    public static float hp = 100f; public static int ink = -1;
    /// <summary>Độ khó của lượt chơi, chọn ở màn Chọn độ khó. KHÔNG bị ResetAll xóa (chết hay chơi lại vẫn giữ mức đã chọn).</summary>
    public static EssayDifficulty difficulty = EssayDifficulty.Easy;
    static string[] roomSubjects;
    public static WeaponInstance main, sub;
    public static int extraShots, bounces, extraPierce, killHeal, atkSpeedStacks;
    public static bool wave; public static Element infuse = Element.None;
    public static readonly float[] resist = new float[5];     // chỉ số 1..4 theo Element
    public static readonly List<string> buffLog = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Boot() { ResetAll(); }

    public static string SubjectOf(int stage) => stage == 1 ? "hoa" : stage == 2 ? "sinh" : "ly";

    /// <summary>Môn của một màn. Không còn chọn môn: mỗi lượt chơi bốc ngẫu nhiên, đủ cả Lý, Hóa, Sinh và không có hai màn liền nhau cùng môn.</summary>
    public static string SubjectOfRoom(int r)
    {
        if (roomSubjects == null) RollSubjects();
        return roomSubjects[Mathf.Clamp(r, 1, roomSubjects.Length) - 1];
    }

    static void RollSubjects()
    {
        var rng = new System.Random();
        var s = new[] { "ly", "hoa", "sinh" };
        for (int i = s.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = s[i]; s[i] = s[j]; s[j] = t; }
        roomSubjects = new string[RoomsPerStage];
        for (int i = 0; i < roomSubjects.Length; i++)
        {
            if (i < s.Length) { roomSubjects[i] = s[i]; continue; }
            string prev = roomSubjects[i - 1], pick;
            do { pick = s[rng.Next(s.Length)]; } while (pick == prev);
            roomSubjects[i] = pick;
        }
    }

    /// <summary>Bắt đầu lượt mới. Mực giữ nguyên nếu keepInk.</summary>
    public static void ResetAll(bool keepInk = false)
    {
        int keep = ink;
        RollSubjects();
        stage = 1; room = 1; hp = 100f;
        main = new WeaponInstance(WeaponId.Pistol, 0); sub = null;     // mỗi lượt chơi khởi đầu bằng súng ngắn (mục 5.6)
        extraShots = bounces = extraPierce = killHeal = atkSpeedStacks = 0; wave = false; infuse = Element.None;
        for (int i = 0; i < resist.Length; i++) resist[i] = 0f;
        buffLog.Clear();
        ink = keepInk ? keep : -1;
    }

    /// <summary>Hết máu: quay về điểm lưu đầu chặng (mục 3.2): mất vật nhặt và buff trong chặng, giữ Mực.</summary>
    public static void ResetToStageStart() { int keep = ink; ResetAll(true); ink = keep; }

    public static void AddBuff(RunBuffDef b, System.Random rng)
    {
        string log = b.name;
        switch (b.id)
        {
            case BuffId.ExtraShot: extraShots++; break;
            case BuffId.Bounce: bounces++; break;
            case BuffId.Pierce: extraPierce++; break;
            case BuffId.Resist: { var e = Weapons.Elems[rng.Next(4)]; resist[(int)e] = Mathf.Min(.75f, resist[(int)e] + .25f); log += " " + Combat.ElementName(e); break; }
            case BuffId.Infuse: { infuse = Weapons.Elems[rng.Next(4)]; log += " " + Combat.ElementName(infuse); break; }
            case BuffId.Wave: wave = true; break;
            case BuffId.AttackSpeed: atkSpeedStacks++; break;
            case BuffId.KillHeal: killHeal++; break;
        }
        buffLog.Add(log);
    }
}
