using System.Collections.Generic;
using UnityEngine;

// Lõi chiến đấu (mục 5 của kế hoạch): 8 nguyên tố + 11 phản ứng, 12 vũ khí, 9 buff, trạng thái lượt chơi và lưu tiến độ.
// Mọi con số là GIÁ TRỊ KHỞI ĐIỂM, sẽ cân bằng khi chơi thử.

public enum Element { None = 0, Thuy = 1, Hoa = 2, Bang = 3, Loi = 4, Nham = 5, Phong = 6, Doc = 7, Thao = 8 }

/// <summary>Tiện ích dùng chung cho mọi thứ trong trận. Đồng hồ trận không chạy khi RoomManager.Frozen (đang viết lời giải).</summary>
public static class Combat
{
    // Thứ tự vẽ: sàn/tường < nhân vật (theo y) < hiệu ứng < vignette < chữ nổi
    public const int OrdChar = 200, OrdFx = 400, OrdVignette = 600, OrdText = 700;

    public static float Now;                                   // giờ trận (giây), không tăng khi đóng băng
    static float stopUntil, slowUntil, slowScale = 1f;
    /// <summary>Delta time cho logic: 0 khi viết lời giải, khi khựng hình (hit-stop); chậm lại khi slow-mo.</summary>
    public static float Dt => RoomManager.Frozen || Time.unscaledTime < stopUntil ? 0f : Time.deltaTime * TimeScale;
    /// <summary>Delta time cho hiệu ứng thị giác: không khựng theo hit-stop nhưng vẫn dừng khi đóng băng.</summary>
    public static float VDt => RoomManager.Frozen ? 0f : Time.deltaTime * TimeScale;
    public static float TimeScale => Time.unscaledTime < slowUntil ? slowScale : 1f;
    public static void HitStop(float sec) { stopUntil = Mathf.Max(stopUntil, Time.unscaledTime + sec); }
    public static void SlowMo(float dur, float scale) { slowUntil = Time.unscaledTime + dur; slowScale = scale; }

    public static readonly List<Foe> Foes = new List<Foe>();
    public static PlayerCombat Player;

    // Trường lực của boss 3: từ trường hút/đẩy đạn người chơi, trọng lực kéo người chơi
    public static Vector2 FieldPos; public static float FieldStrength;
    public static Vector2 Gravity;

    public static void Reset() { Now = 0f; Foes.Clear(); Player = null; stopUntil = 0f; slowUntil = 0f; FieldStrength = 0f; Gravity = Vector2.zero; }

    public static int Order(float y) => OrdChar - Mathf.RoundToInt(y * 10f);

    public static Color ElementColor(Element e) => RogueArt.ElementCols[(int)e];
    public static string ElementName(Element e) => RogueArt.ElementNames[(int)e];

    /// <summary>Tạo một SpriteRenderer có kích thước thế giới cho trước (không phụ thuộc pixels-per-unit của sprite).</summary>
    public static SpriteRenderer MakeSr(string name, Transform parent, Sprite sprite, Color color, Vector2 size, int order, Vector2? localPos = null)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        if (localPos.HasValue) go.transform.localPosition = localPos.Value;
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = color; sr.sortingOrder = order;
        var b = sprite.bounds.size; go.transform.localScale = new Vector3(size.x / b.x, size.y / b.y, 1f);
        return sr;
    }

    /// <summary>Sprite pixel art ở kích thước tự nhiên (1 texel = 1/16 đơn vị) nhân với scale.</summary>
    public static SpriteRenderer MakePx(string name, Transform parent, Sprite sprite, int order, Vector2? localPos = null, float scale = 1f)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        if (localPos.HasValue) go.transform.localPosition = localPos.Value;
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sortingOrder = order; return sr;
    }

    public static Foe Nearest(Vector2 p, float maxR, Foe except = null)
    {
        Foe best = null; float bd = maxR;
        foreach (var m in Foes)
        {
            if (m == null || m == except || m.Dead || !m.Targetable) continue;
            float d = Vector2.Distance(p, m.transform.position) - m.radius;
            if (d < bd) { bd = d; best = m; }
        }
        return best;
    }

    public static bool Roll(ref float dmg, float chance = .08f)
    {
        if (Random.value >= chance) return false;
        dmg *= 2f; return true;
    }

    public static Vector2 Rot(Vector2 v, float deg) => (Vector2)(Quaternion.Euler(0f, 0f, deg) * (Vector3)v);
    public static float Ang(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
    public static Vector2 FromAng(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
}

// ====================================================================== nguyên tố và phản ứng (mục 5.4)
public static class Elements
{
    public const float ReactionWindow = 4f;       // hai nguyên tố khác nhau trong 4 giây thì phản ứng

    /// <summary>Gắn nguyên tố lên mục tiêu: tìm phản ứng với nguyên tố đang bám, nếu không có thì áp hiệu ứng riêng của nguyên tố.</summary>
    public static void Apply(Foe m, Element e, Vector2 src)
    {
        if (e == Element.None || m == null || m.Dead) return;
        if (m.auraEl != Element.None && m.auraEl != e && Combat.Now - m.auraT <= ReactionWindow && TryReact(m, m.auraEl, e))
        { m.auraEl = Element.None; return; }

        m.auraEl = e; m.auraT = Combat.Now;
        Vector2 pos = m.Pos;
        switch (e)
        {
            case Element.Thuy: m.wetUntil = Combat.Now + 4f; RFx.Burst(pos, Combat.ElementColor(e), 5, 2f, .5f, .12f, 4f); break;
            case Element.Hoa: m.burnUntil = Combat.Now + 3f; m.burnMul = Mathf.Max(1f, m.burnMul); RFx.Burst(pos, Combat.ElementColor(e), 5, 2.4f, .45f, .14f, -2f); break;
            case Element.Bang:
                m.slowUntil = Combat.Now + 3f; m.frost++; RFx.Burst(pos, Combat.ElementColor(e), 5, 2f, .5f, .1f, 0f);
                if (m.frost >= 3) { m.frost = 0; m.Freeze(1.4f); RFx.Ring(pos, 1f, Combat.ElementColor(e), .3f); RogueAudio.Play(RSfx.Ice, .7f); }
                break;
            case Element.Loi:
                m.Stun(.3f);
                var hit = new List<Foe> { m };
                for (int i = 0; i < 2; i++)                               // giật nhảy sang tối đa 2 mục tiêu gần nhất
                {
                    Foe n = null; float bd = 3.8f;
                    foreach (var o in Combat.Foes)
                    {
                        if (o == null || o.Dead || !o.Targetable || hit.Contains(o)) continue;
                        float d = Vector2.Distance(m.Pos, o.Pos);
                        if (d < bd) { bd = d; n = o; }
                    }
                    if (n == null) break;
                    RFx.Lightning(hit[hit.Count - 1].Pos, n.Pos, Combat.ElementColor(e), .18f);
                    hit.Add(n); n.TakeHit(n.wetUntil > Combat.Now ? 8f : 5f, Element.None, m.Pos, 0f, false, true);
                }
                RogueAudio.Play(RSfx.Zap, .6f);
                break;
            case Element.Nham:
                m.armorBreakUntil = Combat.Now + 4f; m.Knock((m.Pos - src).normalized * 4f);
                if (Random.value < .3f) m.Stun(.35f);
                RFx.Burst(pos, Combat.ElementColor(e), 6, 3f, .5f, .13f, 7f, false);
                break;
            case Element.Phong:
                m.Knock((m.Pos - src).normalized * 2.5f);
                foreach (var o in Combat.Foes)                              // kéo quái xung quanh gom về mục tiêu
                    if (o != null && o != m && !o.Dead && !o.boss && Vector2.Distance(o.Pos, pos) < 3.6f) o.Knock((pos - o.Pos).normalized * 6f);
                RFx.Ring(pos, 1.8f, Combat.ElementColor(e), .3f); RogueAudio.Play(RSfx.Wind, .5f);
                break;
            case Element.Doc:
                m.poison = Mathf.Min(5, m.poison + 1); m.poisonUntil = Combat.Now + 5f;
                RFx.Burst(pos, Combat.ElementColor(e), 5, 1.4f, .6f, .13f, -1f); break;
            case Element.Thao:
                m.Root(1.5f); RFx.Burst(pos, Combat.ElementColor(e), 6, 1.6f, .5f, .13f, 0f);
                HazardFx.Vines(pos, 1.5f);
                break;
        }
    }

    static bool Pair(Element a, Element b, Element x, Element y) => (a == x && b == y) || (a == y && b == x);

    static void Note(string name, string note) { if (RoomManager.Instance != null) RoomManager.Instance.ShowReaction(name, note); RogueAudio.Play(RSfx.React, .8f); }

    static void Area(Vector2 pos, float r, System.Action<Foe> f)
    {
        foreach (var o in new List<Foe>(Combat.Foes)) if (o != null && !o.Dead && o.Targetable && Vector2.Distance(pos, o.Pos) <= r + o.radius) f(o);
    }

    static bool TryReact(Foe m, Element a, Element b)
    {
        Vector2 pos = m.Pos;
        Color ca = Combat.ElementColor(a), cb = Combat.ElementColor(b), mix = Color.Lerp(ca, cb, .5f);
        if (Pair(a, b, Element.Thuy, Element.Hoa))
        {
            Area(pos, 2.4f, o => { o.TakeHit(20f, Element.None, pos, 3f, false, true); o.blindUntil = Combat.Now + 3.5f; });
            RFx.Blast(pos, 2.4f, new Color(.85f, .9f, 1f), false); RFx.Puff(pos, new Color(.9f, .94f, 1f), 14, .9f);
            m.burnUntil = 0f; m.wetUntil = 0f;
            Note("Hơi nước", "nước gặp nhiệt thì bay hơi, thể tích nở ra rất nhanh nên gây nổ và che mờ tầm nhìn."); return true;
        }
        if (Pair(a, b, Element.Thuy, Element.Bang))
        {
            m.Freeze(2f); m.wetUntil = 0f; m.slowUntil = 0f; RFx.Ring(pos, 1.4f, cb, .4f); RFx.Burst(pos, cb, 12, 3f, .6f, .12f, 0f);
            Note("Đóng băng", "nước ướt gặp lạnh thì đông lại thành đá, mục tiêu bị giữ yên 2 giây."); return true;
        }
        if (Pair(a, b, Element.Thuy, Element.Loi))
        {
            Area(pos, 4.2f, o => { if (o == m || o.wetUntil > Combat.Now) { o.TakeHit(12f, Element.None, pos, 0f, false, true); if (o != m) RFx.Lightning(pos, o.Pos, Combat.ElementColor(Element.Loi), .22f); } });
            RFx.Ring(pos, 2f, Combat.ElementColor(Element.Loi), .35f);
            Note("Dẫn điện", "nước có lẫn tạp chất nên dẫn điện, dòng điện lan sang mọi vật đang ướt."); return true;
        }
        if (Pair(a, b, Element.Hoa, Element.Bang))
        {
            m.TakeHit(30f, Element.None, pos, 2f, true, true); m.slowUntil = 0f; m.burnUntil = 0f; m.freezeUntil = 0f;
            RFx.Burst(pos, Combat.ElementColor(Element.Bang), 16, 5f, .6f, .14f, 5f); RFx.Blast(pos, 1.4f, mix, false);
            Note("Tan băng", "nhiệt làm băng tan, lớp nước đá giãn nở đột ngột làm vỡ giáp, gây một đòn lớn."); return true;
        }
        if (Pair(a, b, Element.Hoa, Element.Phong))
        {
            Area(pos, 3.6f, o => { o.TakeHit(10f, Element.None, pos, 0f, false, true); o.burnUntil = Combat.Now + 3f; if (!o.boss) o.Knock((pos - o.Pos).normalized * 7f); });
            HazardFx.Whirl(pos, 3.2f, Combat.ElementColor(Element.Hoa));
            Note("Bão lửa", "gió cấp thêm oxi làm lửa bùng mạnh, cuốn thành lốc và hút mọi thứ vào giữa."); return true;
        }
        if (Pair(a, b, Element.Loi, Element.Phong))
        {
            var hit = new List<Foe> { m }; Foe last = m;
            for (int i = 0; i < 5; i++)
            {
                Foe n = null; float bd = 6f;
                foreach (var o in Combat.Foes) { if (o == null || o.Dead || !o.Targetable || hit.Contains(o)) continue; float d = Vector2.Distance(last.Pos, o.Pos); if (d < bd) { bd = d; n = o; } }
                if (n == null) break;
                RFx.Lightning(last.Pos, n.Pos, Combat.ElementColor(Element.Loi), .25f); hit.Add(n); n.TakeHit(10f, Element.None, last.Pos, 0f, false, true); last = n;
            }
            m.TakeHit(10f, Element.None, pos, 0f, false, true); RFx.Ring(pos, 1.6f, Combat.ElementColor(Element.Loi), .3f);
            Note("Bão điện", "gió cuốn các hạt tích điện đi xa nên tia sét lan theo gió tới nhiều mục tiêu."); return true;
        }
        if (Pair(a, b, Element.Hoa, Element.Thao))
        {
            Area(pos, 2.6f, o => { o.burnUntil = Combat.Now + 4f; o.burnMul = 2f; });
            RFx.Blast(pos, 2f, Combat.ElementColor(Element.Hoa), false); RFx.Burst(pos, Combat.ElementColor(Element.Thao), 10, 3f, .6f, .14f, -3f);
            Note("Cháy rừng", "cây cỏ khô là nhiên liệu tốt, lửa lan nhanh sang các mục tiêu gần và cháy mạnh hơn."); return true;
        }
        if (Pair(a, b, Element.Doc, Element.Phong))
        {
            HazardFx.Cloud(pos, 2.4f, 5f, Combat.ElementColor(Element.Doc), Element.Doc);
            Note("Khí độc", "khí độc khuếch tán theo gió, tạo thành đám mây lan rộng tồn tại vài giây."); return true;
        }
        if (Pair(a, b, Element.Thuy, Element.Nham))
        {
            HazardFx.Cloud(pos, 2.6f, 5f, new Color(.45f, .32f, .2f), Element.Nham, true);
            m.slowUntil = Combat.Now + 4f;
            Note("Bùn lầy", "đất hút nước thành bùn nhão, bước chân sa lầy nên chậm hẳn đi trên cả vùng."); return true;
        }
        if (Pair(a, b, Element.Nham, Element.Bang))
        {
            m.armorBreakUntil = Combat.Now + 5f; m.TakeHit(35f, Element.None, pos, 3f, true, true);
            RFx.Burst(pos, Combat.ElementColor(Element.Nham), 14, 5f, .6f, .14f, 8f, false); RFx.Ring(pos, 1.6f, Combat.ElementColor(Element.Bang), .3f);
            Note("Nứt đá", "đá thấm nước gặp lạnh thì đông cứng nở ra làm nứt vỡ, gây sát thương xuyên giáp."); return true;
        }
        if (Pair(a, b, Element.Thao, Element.Doc))
        {
            m.poison = Mathf.Min(8, m.poison * 2 + 1); m.poisonUntil = Combat.Now + 6f; m.TakeHit(8f, Element.None, pos, 0f, false, true);
            HazardFx.Vines(pos, 2f, true);
            Note("Mầm độc", "cây độc mọc lên và tiết chất độc, lượng độc trong mục tiêu tăng gấp đôi."); return true;
        }
        return false;
    }
}

// ====================================================================== vũ khí (mục 5.3)
public enum WeaponId { Pistol, Rifle, Laser, Bow, Dagger, Spear, Orb, Cannon, Rocket, Staff, Axe, Scythe }
public enum WKind { Gun, Auto, Beam, Bow, Stab, Thrust, Orbit, Blast, Rocket, Wand, Spin, Reap }

public class WeaponDef
{
    public WeaponId id; public string name, group, desc; public WKind kind; public bool melee;
    public float damage, cooldown, speed, spreadDeg, range; public int count, pierce;
    public WeaponDef(WeaponId id, string name, string group, WKind kind, bool melee, float damage, float cooldown, float speed, int count, float spreadDeg, int pierce, float range, string desc)
    { this.id = id; this.name = name; this.group = group; this.kind = kind; this.melee = melee; this.damage = damage; this.cooldown = cooldown; this.speed = speed; this.count = count; this.spreadDeg = spreadDeg; this.pierce = pierce; this.range = range; this.desc = desc; }
}

public class WeaponInstance
{
    public WeaponId id; public int rarity; public Element element;
    public WeaponDef Def => Weapons.Get(id);
    public float Damage => Def.damage * Weapons.RarityMult[Mathf.Clamp(rarity, 0, 3)];
    public string Label => Def.name + " [" + RoomRules.RarityNames[Mathf.Clamp(rarity, 0, 3)] + "]" + (element != Element.None ? " · " + Combat.ElementName(element) : "");
    public WeaponInstance(WeaponId id, int rarity, Element el = Element.None) { this.id = id; this.rarity = rarity; element = el; }
    public WeaponInstance Clone() => new WeaponInstance(id, rarity, element);
    public Sprite Icon => RogueArt.Weapon((int)id, rarity, (int)element);
}

public static class Weapons
{
    public static readonly float[] RarityMult = { 1f, 1.25f, 1.55f, 2f };
    static readonly WeaponDef[] defs =
    {
        new WeaponDef(WeaponId.Pistol, "Súng ngắn", "Tầm xa", WKind.Gun, false, 10f, .32f, 15f, 1, 0f, 0, 0f, "Đạn thẳng, dễ dùng, hợp buff đạn nảy."),
        new WeaponDef(WeaponId.Rifle, "Súng trường", "Tầm xa", WKind.Auto, false, 6f, .1f, 18f, 1, 2f, 0, 0f, "Liên thanh, giữ cò càng lâu đạn càng tản."),
        new WeaponDef(WeaponId.Laser, "Súng laze", "Tầm xa", WKind.Beam, false, 7f, .1f, 0f, 1, 0f, 99, 14f, "Tia xuyên mọi mục tiêu, quá nhiệt phải nghỉ."),
        new WeaponDef(WeaponId.Bow, "Cung", "Tầm xa", WKind.Bow, false, 12f, .45f, 15f, 1, 0f, 2, 0f, "Giữ chuột để lên lực, mũi tên xuyên."),
        new WeaponDef(WeaponId.Dagger, "Dao găm", "Cận chiến", WKind.Stab, true, 12f, .2f, 0f, 1, 0f, 0, 2.0f, "Chém cực nhanh, tầm ngắn, cơ động."),
        new WeaponDef(WeaponId.Spear, "Thương", "Cận chiến", WKind.Thrust, true, 16f, .45f, 0f, 1, 0f, 0, 3.8f, "Đâm xa, xuyên cả hàng quái theo đường thẳng."),
        new WeaponDef(WeaponId.Orb, "Pháp khí", "Phép", WKind.Orbit, false, 7f, .55f, 10f, 3, 0f, 0, 0f, "Quả cầu bay quanh người, nhả đạn tự tìm."),
        new WeaponDef(WeaponId.Cannon, "Đại bác", "Hạng nặng", WKind.Blast, false, 36f, 1.05f, 11f, 1, 0f, 0, 0f, "Bắn chậm, nổ lan, hất lùi mạnh."),
        new WeaponDef(WeaponId.Rocket, "Tên lửa", "Hạng nặng", WKind.Rocket, false, 20f, .32f, 8f, 1, 0f, 0, 0f, "Băng 4 quả tự dẫn, nổ lan nhỏ, nạp đạn lâu."),
        new WeaponDef(WeaponId.Staff, "Gậy phép", "Phép", WKind.Wand, false, 9f, .36f, 12f, 1, 0f, 0, 0f, "Cầu phép mang nguyên tố, nhịp đánh nhanh."),
        new WeaponDef(WeaponId.Axe, "Rìu chiến", "Cận chiến", WKind.Spin, true, 28f, .85f, 0f, 1, 0f, 0, 2.3f, "Chém vòng quanh người, chậm, hất lùi mạnh."),
        new WeaponDef(WeaponId.Scythe, "Lưỡi hái", "Cận chiến", WKind.Reap, true, 17f, .5f, 0f, 1, 0f, 0, 3.0f, "Chém cung rộng, hồi máu khi hạ quái."),
    };
    public static WeaponDef Get(WeaponId id) => defs[(int)id];
    public static int Count => defs.Length;
    public static readonly Element[] Elems = { Element.Thuy, Element.Hoa, Element.Bang, Element.Loi, Element.Nham, Element.Phong, Element.Doc, Element.Thao };

    public static WeaponInstance RollId(WeaponId id, int rarity, System.Random rng)
    {
        bool alwaysEl = id == WeaponId.Staff || id == WeaponId.Orb;
        bool el = alwaysEl || rarity >= 3 || (rarity == 2 && rng.NextDouble() < .6) || (rarity == 1 && rng.NextDouble() < .15);
        return new WeaponInstance(id, rarity, el ? Elems[rng.Next(Elems.Length)] : Element.None);
    }

    /// <summary>Vũ khí ngẫu nhiên từ Rương Câu Đố. Độ hiếm cao thì dễ mang nguyên tố.</summary>
    public static WeaponInstance Roll(int rarity, System.Random rng) => RollId((WeaponId)rng.Next(defs.Length), rarity, rng);

    /// <summary>Ba vũ khí khác loại cho phần thưởng boss.</summary>
    public static List<WeaponInstance> RollChoices(int rarityLo, int rarityHi, System.Random rng)
    {
        var ids = new List<int>(); for (int i = 0; i < defs.Length; i++) ids.Add(i);
        var res = new List<WeaponInstance>();
        for (int i = 0; i < 3; i++) { int k = rng.Next(ids.Count); res.Add(RollId((WeaponId)ids[k], rng.Next(rarityLo, rarityHi + 1), rng)); ids.RemoveAt(k); }
        return res;
    }
}

// ====================================================================== buff (mục 5.5)
public enum BuffId { ExtraShot, Bounce, Pierce, Resist, Infuse, Wave, AttackSpeed, KillHeal, Blast }

public class RunBuffDef
{
    public BuffId id; public string name, desc;
    public RunBuffDef(BuffId id, string name, string desc) { this.id = id; this.name = name; this.desc = desc; }
}

public static class Buffs
{
    public static readonly RunBuffDef[] All =
    {
        new RunBuffDef(BuffId.ExtraShot, "Thêm đạn", "Mỗi loạt bắn thêm 1 viên"),
        new RunBuffDef(BuffId.Bounce, "Đạn nảy", "Đạn nảy vào tường thêm 1 lần"),
        new RunBuffDef(BuffId.Pierce, "Đạn xuyên", "Đạn xuyên thêm 1 mục tiêu"),
        new RunBuffDef(BuffId.Resist, "Kháng nguyên tố", "Giảm 25% sát thương từ một nguyên tố (ngẫu nhiên trong 8)"),
        new RunBuffDef(BuffId.Infuse, "Nhiễm nguyên tố", "Mỗi đòn đánh thêm một nguyên tố (ngẫu nhiên trong 8)"),
        new RunBuffDef(BuffId.Wave, "Sóng chém", "Đòn chém bắn ra một đường sóng bay xa"),
        new RunBuffDef(BuffId.AttackSpeed, "Tốc đánh", "Tăng 15% tốc độ bắn hoặc chém"),
        new RunBuffDef(BuffId.KillHeal, "Hồi máu khi hạ quái", "Hồi 3 máu mỗi khi hạ một con quái"),
        new RunBuffDef(BuffId.Blast, "Nổ lan rộng", "Tăng 35% bán kính nổ của đại bác, tên lửa và mọi vụ nổ"),
    };

    public static RunBuffDef Of(BuffId id) => All[(int)id];

    /// <summary>Buff có tác dụng rõ với vũ khí này (dùng để bảo đảm ít nhất 1 buff hợp vũ khí đang cầm, mục 5.5).</summary>
    public static bool Suits(RunBuffDef b, WeaponDef w)
    {
        switch (b.id)
        {
            case BuffId.Blast: return w.kind == WKind.Blast || w.kind == WKind.Rocket;
            case BuffId.Wave: return w.melee;
            case BuffId.Bounce: return !w.melee && w.kind != WKind.Orbit || w.kind == WKind.Orbit;
            case BuffId.Pierce: return w.kind == WKind.Bow || w.kind == WKind.Beam || w.kind == WKind.Thrust || w.kind == WKind.Gun || w.kind == WKind.Auto;
            case BuffId.ExtraShot: return !w.melee;
            default: return true;
        }
    }

    /// <summary>Rút 3 buff ngẫu nhiên, bảo đảm ít nhất 1 buff hợp với vũ khí đang cầm.</summary>
    public static List<RunBuffDef> Offer(WeaponDef main, System.Random rng)
    {
        var pool = new List<RunBuffDef>(All);
        var suit = pool.FindAll(b => Suits(b, main) && b.id != BuffId.Resist && b.id != BuffId.Infuse && b.id != BuffId.AttackSpeed && b.id != BuffId.KillHeal);
        if (suit.Count == 0) suit = pool;
        var res = new List<RunBuffDef>();
        var first = suit[rng.Next(suit.Count)]; res.Add(first); pool.Remove(first);
        while (res.Count < 3) { var b = pool[rng.Next(pool.Count)]; res.Add(b); pool.Remove(b); }
        for (int i = res.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = res[i]; res[i] = res[j]; res[j] = t; }
        return res;
    }
}

// ====================================================================== trạng thái lượt chơi, sống qua các lần nạp scene
public enum RunPhase { Room, Boss, Rest, Final, Ending }

[System.Serializable] public class WeaponSave { public int id = -1, rarity, element; }

/// <summary>Ảnh chụp tình trạng tại điểm lưu (đầu chặng): chết thì quay lại đây, mất vật nhặt và buff trong chặng, giữ Mực và Mảnh Ký Ức.</summary>
[System.Serializable]
public class RunSnap
{
    public int stage = 1; public RunPhase phase = RunPhase.Room; public float hp = 100f;
    public WeaponSave main = new WeaponSave(), sub = new WeaponSave();
    public int extraShots, bounces, extraPierce, killHeal, atkSpeedStacks, blastStacks; public bool wave; public int infuse;
    public float[] resist = new float[9]; public List<string> buffLog = new List<string>();
}

[System.Serializable]
public class RunSaveData
{
    public int version = 3; public int stage = 1, room = 1; public RunPhase phase = RunPhase.Room; public int ink, shards, difficulty, bossesDown;
    public RunSnap now = new RunSnap(), checkpoint = new RunSnap();
    public string[] subjects; public List<string> notes = new List<string>();
}

public static class RunState
{
    public const int RoomsPerStage = 4;                       // 4 màn thường mỗi chặng; màn 5 là boss
    public const int Stages = 3;
    public static int stage = 1, room = 1; public static RunPhase phase = RunPhase.Room;
    public static float hp = 100f; public static int ink = -1, shards, bossesDown;
    public static bool introPending;
    /// <summary>Độ khó của lượt chơi, chọn một lần ở màn Chọn độ khó. KHÔNG bị ResetAll xóa.</summary>
    public static EssayDifficulty difficulty = EssayDifficulty.Easy;
    static string[] roomSubjects;
    public static WeaponInstance main, sub;
    public static int extraShots, bounces, extraPierce, killHeal, atkSpeedStacks, blastStacks;
    public static bool wave; public static Element infuse = Element.None;
    public static readonly float[] resist = new float[9];
    public static readonly List<string> buffLog = new List<string>();
    /// <summary>Ghi chú của Thầy Cú về phần còn thiếu trong các lần giải trụ đá "Đạt" (hiện ở phòng nghỉ).</summary>
    public static readonly List<string> notes = new List<string>();
    public static RunSnap checkpoint = new RunSnap();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Boot()
    {   // chỉ đặt giá trị mặc định, không đụng PlayerPrefs ở giai đoạn khởi động sớm
        RollSubjects(); stage = 1; room = 1; phase = RunPhase.Room; hp = 100f; ink = -1; shards = 0; bossesDown = 0; introPending = false;
        main = new WeaponInstance(WeaponId.Pistol, 0); sub = null; checkpoint = new RunSnap();
    }

    public static string SubjectOf(int st) => st == 1 ? "hoa" : st == 2 ? "sinh" : "ly";

    /// <summary>Môn của một màn thường: xen kẽ ngẫu nhiên (Lý/Hóa/Sinh), không có hai màn liền nhau cùng môn.</summary>
    public static string SubjectOfRoom(int r)
    {
        if (roomSubjects == null) RollSubjects();
        int idx = (Mathf.Clamp(stage, 1, Stages) - 1) * RoomsPerStage + Mathf.Clamp(r, 1, RoomsPerStage) - 1;
        return roomSubjects[idx];
    }

    static void RollSubjects()
    {
        var rng = new System.Random();
        var s = new[] { "ly", "hoa", "sinh" };
        roomSubjects = new string[Stages * RoomsPerStage];
        for (int i = 0; i < roomSubjects.Length; i++)
        {
            string prev = i > 0 ? roomSubjects[i - 1] : null, pick;
            do { pick = s[rng.Next(s.Length)]; } while (pick == prev);
            roomSubjects[i] = pick;
        }
    }

    /// <summary>Số thứ tự màn toàn hành trình (1..15, 16 là bệ đá).</summary>
    public static int GlobalRoom => phase == RunPhase.Final || phase == RunPhase.Ending ? 16 : (stage - 1) * 5 + Mathf.Min(room, 5);

    public static string StageName(int st) => st == 1 ? "Phòng Thí Nghiệm Hóa Học" : st == 2 ? "Phòng Nghiên Cứu Sinh Học" : "Lab Vật Lí Trên Mây";
    public static int Biome => Mathf.Clamp(stage, 1, 3) - 1;

    /// <summary>Bắt đầu lượt mới. Mực giữ nguyên nếu keepInk.</summary>
    public static void ResetAll(bool keepInk = false)
    {
        int keep = ink;
        RollSubjects();
        stage = 1; room = 1; phase = RunPhase.Room; hp = PlayerCombat.MaxHp; shards = 0; bossesDown = 0;
        main = new WeaponInstance(WeaponId.Pistol, 0); sub = Meta.LegendWeapon();           // vũ khí huyền thoại nhận ở cuối lượt trước làm vũ khí phụ
        extraShots = bounces = extraPierce = killHeal = atkSpeedStacks = blastStacks = 0; wave = false; infuse = Element.None;
        for (int i = 0; i < resist.Length; i++) resist[i] = 0f;
        buffLog.Clear(); notes.Clear();
        ink = keepInk ? keep : -1;
        SaveCheckpoint();
    }

    /// <summary>Gọi từ màn Chọn độ khó khi bắt đầu lượt mới.</summary>
    public static void NewRun() { ResetAll(true); introPending = !Meta.SeenIntro; RunSave.Save(); }

    // ---- điểm lưu ----
    public static RunSnap Capture()
    {
        var s = new RunSnap { stage = stage, phase = phase, hp = hp, extraShots = extraShots, bounces = bounces, extraPierce = extraPierce, killHeal = killHeal, atkSpeedStacks = atkSpeedStacks, blastStacks = blastStacks, wave = wave, infuse = (int)infuse };
        s.main = ToSave(main); s.sub = ToSave(sub); System.Array.Copy(resist, s.resist, resist.Length); s.buffLog = new List<string>(buffLog); return s;
    }

    static WeaponSave ToSave(WeaponInstance w) => w == null ? new WeaponSave() : new WeaponSave { id = (int)w.id, rarity = w.rarity, element = (int)w.element };
    static WeaponInstance FromSave(WeaponSave w) => w == null || w.id < 0 ? null : new WeaponInstance((WeaponId)w.id, w.rarity, (Element)w.element);

    public static void Apply(RunSnap s)
    {
        hp = s.hp; extraShots = s.extraShots; bounces = s.bounces; extraPierce = s.extraPierce; killHeal = s.killHeal; atkSpeedStacks = s.atkSpeedStacks; blastStacks = s.blastStacks; wave = s.wave; infuse = (Element)s.infuse;
        main = FromSave(s.main) ?? new WeaponInstance(WeaponId.Pistol, 0); sub = FromSave(s.sub);
        for (int i = 0; i < resist.Length; i++) resist[i] = i < s.resist.Length ? s.resist[i] : 0f;
        buffLog.Clear(); buffLog.AddRange(s.buffLog);
    }

    /// <summary>Gọi lúc bắt đầu một chặng (kể cả vào phòng nghỉ sau boss).</summary>
    public static void SaveCheckpoint() { checkpoint = Capture(); }

    /// <summary>Hết máu: về điểm lưu đầu chặng (1-1 với chặng 1; phòng nghỉ sau boss với chặng 2 và 3). Mất vật nhặt và buff trong chặng; Mực và Mảnh Ký Ức giữ nguyên.</summary>
    public static void ResetToStageStart()
    {
        Apply(checkpoint); stage = checkpoint.stage; phase = checkpoint.phase; room = 1;
        hp = phase == RunPhase.Rest ? Mathf.Max(hp, PlayerCombat.MaxHp * .6f) : PlayerCombat.MaxHp;
        RunSave.Save();
    }

    public static void AddBuff(RunBuffDef b, System.Random rng)
    {
        string log = b.name;
        switch (b.id)
        {
            case BuffId.ExtraShot: extraShots++; break;
            case BuffId.Bounce: bounces++; break;
            case BuffId.Pierce: extraPierce++; break;
            case BuffId.Resist: { var e = Weapons.Elems[rng.Next(Weapons.Elems.Length)]; resist[(int)e] = Mathf.Min(.75f, resist[(int)e] + .25f); log += " " + Combat.ElementName(e); break; }
            case BuffId.Infuse: { infuse = Weapons.Elems[rng.Next(Weapons.Elems.Length)]; log += " " + Combat.ElementName(infuse); break; }
            case BuffId.Wave: wave = true; break;
            case BuffId.AttackSpeed: atkSpeedStacks++; break;
            case BuffId.KillHeal: killHeal++; break;
            case BuffId.Blast: blastStacks++; break;
        }
        buffLog.Add(log);
    }

    /// <summary>Hệ số bán kính nổ từ buff "Nổ lan rộng".</summary>
    public static float BlastMul => 1f + .35f * blastStacks;

    // ---- ghi và đọc file lưu ----
    public static RunSaveData ToData()
    {
        var d = new RunSaveData { stage = stage, room = room, phase = phase, ink = ink, shards = shards, difficulty = (int)difficulty, bossesDown = bossesDown, subjects = roomSubjects, notes = new List<string>(notes) };
        d.now = Capture(); d.checkpoint = checkpoint; return d;
    }

    public static void FromData(RunSaveData d)
    {
        stage = d.stage; room = d.room; phase = d.phase; ink = d.ink; shards = d.shards; difficulty = (EssayDifficulty)Mathf.Clamp(d.difficulty, 0, 3); bossesDown = d.bossesDown;
        if (d.subjects != null && d.subjects.Length == Stages * RoomsPerStage) roomSubjects = d.subjects; else RollSubjects();
        Apply(d.now); checkpoint = d.checkpoint ?? Capture(); notes.Clear(); if (d.notes != null) notes.AddRange(d.notes);
    }
}

/// <summary>Lưu lượt chơi dở vào PlayerPrefs để có nút "Tiếp tục". Xóa khi thắng hoặc chơi mới.</summary>
public static class RunSave
{
    const string Key = "KHTN_RunSave_v3";
    public static bool Exists => PlayerPrefs.HasKey(Key);
    public static void Save() { try { PlayerPrefs.SetString(Key, JsonUtility.ToJson(RunState.ToData())); PlayerPrefs.Save(); } catch (System.Exception e) { Debug.LogWarning("Không lưu được lượt chơi: " + e.Message); } }
    public static bool Load()
    {
        if (!Exists) return false;
        try { var d = JsonUtility.FromJson<RunSaveData>(PlayerPrefs.GetString(Key)); if (d == null || d.version != 3) return false; RunState.FromData(d); return true; }
        catch (System.Exception e) { Debug.LogWarning("Không đọc được lượt chơi đã lưu: " + e.Message); return false; }
    }
    public static void Clear() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
}

/// <summary>Thành tựu giữ qua các lượt: phần thưởng cuối hành trình ở màn 16.</summary>
public static class Meta
{
    const string KLegend = "KHTN_LegendWeapon", KTitle = "KHTN_Title", KHp = "KHTN_PermHp", KClears = "KHTN_Clears", KIntro = "KHTN_SeenIntro";
    public static readonly string[] Titles = { "", "Người Gỡ Sương", "Kẻ Nhớ Lại", "Học Trò Của Thầy Cú" };
    public static int LegendId { get => PlayerPrefs.GetInt(KLegend, -1); set { PlayerPrefs.SetInt(KLegend, value); PlayerPrefs.Save(); } }
    public static int TitleIdx { get => PlayerPrefs.GetInt(KTitle, 0); set { PlayerPrefs.SetInt(KTitle, value); PlayerPrefs.Save(); } }
    public static int PermHp { get => PlayerPrefs.GetInt(KHp, 0); set { PlayerPrefs.SetInt(KHp, value); PlayerPrefs.Save(); } }
    public static int Clears { get => PlayerPrefs.GetInt(KClears, 0); set { PlayerPrefs.SetInt(KClears, value); PlayerPrefs.Save(); } }
    public static bool SeenIntro { get => PlayerPrefs.GetInt(KIntro, 0) == 1; set { PlayerPrefs.SetInt(KIntro, value ? 1 : 0); PlayerPrefs.Save(); } }
    public static string Title => TitleIdx > 0 && TitleIdx < Titles.Length ? Titles[TitleIdx] : "";
    public static WeaponInstance LegendWeapon() => LegendId < 0 ? null : new WeaponInstance((WeaponId)LegendId, 3, Weapons.Elems[LegendId % Weapons.Elems.Length]);
}
