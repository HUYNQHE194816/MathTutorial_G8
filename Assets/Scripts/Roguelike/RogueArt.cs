using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Canvas pixel dùng để vẽ MỌI hình ảnh của chế độ roguelike bằng code (hiệp sĩ, vũ khí, quái, boss, sàn, đạn, icon...).
/// Quy ước thống nhất cho cả game: 1 texel = 1/16 đơn vị thế giới (PPU 16), lọc Point, viền tối cùng một tông,
/// ánh sáng từ trên-trái. Nhờ vậy mọi thứ đồng đều về độ hạt pixel và phong cách.
/// Tọa độ vẽ: gốc ở góc dưới-trái, trục y hướng lên.
/// </summary>
public sealed class Px
{
    public readonly int w, h; public readonly Color32[] p;
    public Px(int w, int h) { this.w = w; this.h = h; p = new Color32[w * h]; }

    public void Set(int x, int y, Color32 c) { if (x < 0 || y < 0 || x >= w || y >= h) return; p[y * w + x] = c; }
    public Color32 Get(int x, int y) { if (x < 0 || y < 0 || x >= w || y >= h) return default(Color32); return p[y * w + x]; }
    public bool Solid(int x, int y) => Get(x, y).a != 0;

    public void Rect(int x, int y, int rw, int rh, Color32 c) { for (int j = y; j < y + rh; j++) for (int i = x; i < x + rw; i++) Set(i, j, c); }

    public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
    {
        for (int j = Mathf.FloorToInt(cy - ry); j <= Mathf.CeilToInt(cy + ry); j++)
            for (int i = Mathf.FloorToInt(cx - rx); i <= Mathf.CeilToInt(cx + rx); i++)
            {
                float dx = (i + .5f - cx) / rx, dy = (j + .5f - cy) / ry;
                if (dx * dx + dy * dy <= 1f) Set(i, j, c);
            }
    }

    public void Poly(Vector2[] v, Color32 c)
    {
        float minx = 1e9f, maxx = -1e9f, miny = 1e9f, maxy = -1e9f;
        foreach (var q in v) { minx = Mathf.Min(minx, q.x); maxx = Mathf.Max(maxx, q.x); miny = Mathf.Min(miny, q.y); maxy = Mathf.Max(maxy, q.y); }
        for (int j = Mathf.FloorToInt(miny); j <= Mathf.CeilToInt(maxy); j++)
            for (int i = Mathf.FloorToInt(minx); i <= Mathf.CeilToInt(maxx); i++)
            {
                float x = i + .5f, y = j + .5f; bool inside = false;
                for (int a = 0, b = v.Length - 1; a < v.Length; b = a++)
                    if (((v[a].y > y) != (v[b].y > y)) && (x < (v[b].x - v[a].x) * (y - v[a].y) / (v[b].y - v[a].y) + v[a].x)) inside = !inside;
                if (inside) Set(i, j, c);
            }
    }

    public void Line(float x0, float y0, float x1, float y1, Color32 c, int th = 1)
    {
        int n = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) * 2 + 1;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n; int x = Mathf.FloorToInt(Mathf.Lerp(x0, x1, t)), y = Mathf.FloorToInt(Mathf.Lerp(y0, y1, t));
            for (int a = 0; a < th; a++) for (int b = 0; b < th; b++) Set(x + a, y + b, c);
        }
    }

    /// <summary>Viền 4 hướng quanh phần đã vẽ.</summary>
    public void Outline(Color32 oc)
    {
        var src = (Color32[])p.Clone();
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                if (src[y * w + x].a != 0) continue;
                bool n = (x > 0 && src[y * w + x - 1].a != 0) || (x < w - 1 && src[y * w + x + 1].a != 0) ||
                         (y > 0 && src[(y - 1) * w + x].a != 0) || (y < h - 1 && src[(y + 1) * w + x].a != 0);
                if (n) p[y * w + x] = oc;
            }
    }

    /// <summary>Khối tròn có sáng (trên-trái) và tối (dưới-phải): đủ để thành hình khối mà vẫn giữ nét pixel.</summary>
    public void Blob(float cx, float cy, float rx, float ry, Color32 baseC, Color32 lightC, Color32 darkC)
    {
        Ellipse(cx, cy, rx, ry, darkC);
        Ellipse(cx - .5f, cy + .5f, Mathf.Max(.6f, rx - .9f), Mathf.Max(.6f, ry - .9f), baseC);
        Ellipse(cx - rx * .3f, cy + ry * .38f, Mathf.Max(.5f, rx * .42f), Mathf.Max(.5f, ry * .36f), lightC);
    }

    /// <summary>Hình chữ nhật có vạch sáng ở cạnh trên và vạch tối ở cạnh dưới.</summary>
    public void Slab(int x, int y, int rw, int rh, Color32 baseC, Color32 lightC, Color32 darkC)
    {
        Rect(x, y, rw, rh, baseC);
        if (rh >= 3) { Rect(x, y + rh - 1, rw, 1, lightC); Rect(x, y, rw, 1, darkC); }
        else if (rh == 2) Rect(x, y + 1, rw, 1, lightC);
    }

    public void Blit(Px s, int ox, int oy)
    {
        for (int y = 0; y < s.h; y++) for (int x = 0; x < s.w; x++) { var c = s.p[y * s.w + x]; if (c.a != 0) Set(ox + x, oy + y, c); }
    }

    public Px Mirror()
    {
        var m = new Px(w, h);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) m.p[y * w + (w - 1 - x)] = p[y * w + x];
        return m;
    }

    /// <summary>Ghi đè màu của mọi pixel đang có chữ (dùng làm bóng trắng để nháy trúng đòn).</summary>
    public Px Silhouette(Color32 c)
    {
        var m = new Px(w, h);
        for (int i = 0; i < p.Length; i++) if (p[i].a != 0) m.p[i] = c;
        return m;
    }

    public Sprite ToSprite(Vector2 pivot, float ppu = 16f, FilterMode fm = FilterMode.Point)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = fm, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        t.SetPixels32(p); t.Apply();
        var s = Sprite.Create(t, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect, Vector4.zero);
        s.hideFlags = HideFlags.HideAndDontSave; return s;
    }
}

/// <summary>Nhóm tiện ích màu và các sprite dùng chung: hiệp sĩ, vũ khí, icon, đạn. Quái/boss ở RogueArtMonsters.cs, môi trường ở RogueArtWorld.cs.</summary>
public static partial class RogueArt
{
    public const float PPU = 16f;
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, Sprite[]> cacheN = new Dictionary<string, Sprite[]>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { cache.Clear(); cacheN.Clear(); silCache.Clear(); }

    public static Color32 K(int rgb, int a = 255) => new Color32((byte)((rgb >> 16) & 255), (byte)((rgb >> 8) & 255), (byte)(rgb & 255), (byte)a);
    public static readonly Color32 Ink = new Color32(18, 14, 28, 255);
    public static readonly Color32 White = new Color32(255, 255, 255, 255);
    public static Vector2[] V(params float[] xy) { var v = new Vector2[xy.Length / 2]; for (int i = 0; i < v.Length; i++) v[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]); return v; }

    static Sprite Get(string key, System.Func<Sprite> make) { if (cache.TryGetValue(key, out var s) && s != null) return s; s = make(); cache[key] = s; return s; }
    static Sprite[] GetN(string key, System.Func<Sprite[]> make) { if (cacheN.TryGetValue(key, out var s) && s != null && s[0] != null) return s; s = make(); cacheN[key] = s; return s; }

    // ---- màu nguyên tố: 0 không, 1 Thủy, 2 Hỏa, 3 Băng, 4 Lôi, 5 Nham, 6 Phong, 7 Độc, 8 Thảo ----
    public static readonly Color[] ElementCols =
    {
        new Color(1f, 1f, 1f), new Color(.30f, .65f, 1f), new Color(1f, .42f, .18f), new Color(.62f, .94f, 1f), new Color(1f, .93f, .30f),
        new Color(.80f, .58f, .30f), new Color(.66f, .96f, .86f), new Color(.72f, .32f, .92f), new Color(.38f, .83f, .36f)
    };
    public static readonly string[] ElementNames = { "Không", "Thủy", "Hỏa", "Băng", "Lôi", "Nham", "Phong", "Độc", "Thảo" };

    /// <summary>Màu theo độ hiếm: 0 Thường (thép), 1 Hiếm (xanh), 2 Sử thi (tím), 3 Huyền thoại (vàng).</summary>
    public static readonly Color[] RarityCols = { new Color(.72f, .76f, .86f), new Color(.40f, .70f, 1f), new Color(.78f, .48f, 1f), new Color(1f, .82f, .35f) };

    static Color32 C32(Color c) => (Color32)c;
    static Color32 Mix(Color32 a, Color32 b, float t) { return (Color32)Color.Lerp((Color)a, (Color)b, t); }
    static Color32 Lite(Color32 a, float t = .35f) => Mix(a, White, t);
    static Color32 Shade(Color32 a, float t = .4f) => Mix(a, Ink, t);

    // ======================================================================== HIỆP SĨ (giáp xám)
    // Hướng sang phải. 0..3 đứng thở, 0..5 chạy. Pivot ở chân. Vũ khí là sprite riêng xoay quanh tay.
    public static Sprite[] KnightIdle => GetN("kidle", () => new[] { Knight(0, 0, 0, false), Knight(0, 0, 1, false), Knight(1, 0, 2, false), Knight(1, 0, 1, false) });
    public static Sprite[] KnightRun => GetN("krun", () => new[]
    {
        Knight(1, 3, 0, true), Knight(0, 2, 1, true), Knight(0, 0, 2, true), Knight(1, -3, 1, true), Knight(0, -2, 0, true), Knight(0, 0, 1, true)
    });
    public static Sprite KnightDash => Get("kdash", () => Knight(0, 4, 3, true, true));

    static Sprite Knight(int bob, int stride, int cape, bool run, bool dash = false)
    {
        var c = new Px(30, 38); int b = bob;
        Color32 steel = K(0xA9B1C6), hi = K(0xE2E7F4), lo = K(0x6A7392), deep = K(0x3B405C), dark = K(0x23253A), cloak = K(0x41528C), cloakD = K(0x283258),
                red = K(0xC23B4E), redD = K(0x862737), gold = K(0xD4A33F), gold2 = K(0xF6D57A), eye = K(0xFFC36B);
        int lean = dash ? 3 : 0;

        // áo choàng phía sau, gợn theo nhịp chạy
        float sw = cape * 1.6f + (run ? 2.5f : 0f) + (dash ? 5f : 0f);
        c.Poly(V(11 + lean, 28 + b, 17 + lean, 28 + b, 17 + lean, 12, 7 - sw + lean, 6 + cape, 5 - sw + lean, 12 + cape, 8 + lean, 22 + b), cloakD);
        c.Poly(V(12 + lean, 27 + b, 16 + lean, 27 + b, 16 + lean, 13, 9 - sw + lean, 9 + cape), cloak);
        c.Line(13 + lean, 25 + b, 9 - sw * .5f + lean, 10 + cape, Shade(cloak, .3f));

        // chân (chạy thì bước xen kẽ)
        int s1 = stride, s2 = -stride; int up1 = run && stride > 1 ? 1 : 0, up2 = run && stride < -1 ? 1 : 0;
        c.Rect(11 + s1 + lean, 3 + up1, 4, 10 - up1, deep); c.Rect(11 + s1 + lean, 6 + up1, 2, 7 - up1, lo);
        c.Rect(10 + s1 + lean, 2 + up1, 6, 3, dark); c.Rect(10 + s1 + lean, 4 + up1, 6, 1, steel);
        c.Rect(15 + s2 + lean, 3 + up2, 4, 10 - up2, deep); c.Rect(15 + s2 + lean, 6 + up2, 2, 7 - up2, lo);
        c.Rect(14 + s2 + lean, 2 + up2, 6, 3, dark); c.Rect(14 + s2 + lean, 4 + up2, 6, 1, steel);

        // thân giáp
        c.Slab(10 + lean, 13 + b, 11, 12, steel, hi, lo);
        c.Rect(10 + lean, 13 + b, 11, 2, deep);                                    // đai
        c.Rect(14 + lean, 13 + b, 3, 2, gold); c.Set(15 + lean, 14 + b, gold2);     // khóa đai
        c.Rect(12 + lean, 18 + b, 7, 1, lo); c.Rect(13 + lean, 21 + b, 5, 1, lo);   // đường chia giáp ngực
        c.Rect(15 + lean, 16 + b, 1, 8, lo);
        c.Blob(9 + lean, 24 + b, 3.2f, 2.8f, steel, hi, lo); c.Blob(21 + lean, 24 + b, 3.2f, 2.8f, steel, hi, lo);   // giáp vai
        c.Rect(7 + lean, 23 + b, 2, 1, gold); c.Rect(21 + lean, 23 + b, 2, 1, gold);

        // đầu và mũ giáp
        c.Rect(11 + lean, 25 + b, 9, 9, steel); c.Rect(12 + lean, 33 + b, 7, 1, steel);
        c.Rect(11 + lean, 33 + b, 9, 1, hi); c.Rect(11 + lean, 25 + b, 9, 1, lo);
        c.Rect(11 + lean, 26 + b, 1, 7, lo); c.Rect(19 + lean, 26 + b, 1, 7, hi);
        c.Rect(13 + lean, 29 + b, 7, 2, dark); c.Set(18 + lean, 29 + b, eye); c.Set(19 + lean, 30 + b, eye); c.Set(17 + lean, 30 + b, eye);   // khe nhìn, mắt lửa
        c.Rect(15 + lean, 31 + b, 1, 3, lo);                                        // gờ mũ
        // chùm lông đỏ
        int pw = cape;
        c.Poly(V(13 + lean, 34 + b, 17 + lean, 34 + b, 14 - pw - 2 + lean, 38, 10 - pw - 3 + lean, 36), red);
        c.Poly(V(14 + lean, 34 + b, 16 + lean, 34 + b, 14 - pw - 1 + lean, 37), redD);
        c.Outline(Ink);
        return c.ToSprite(new Vector2(.5f, 2f / 38f));
    }

    // ======================================================================== VŨ KHÍ
    // Hướng sang phải, pivot ở tay cầm. id theo thứ tự WeaponId: 0 Súng ngắn, 1 Súng trường, 2 Súng laze, 3 Cung, 4 Dao, 5 Thương,
    // 6 Pháp khí, 7 Đại bác, 8 Tên lửa, 9 Gậy phép, 10 Rìu chiến, 11 Lưỡi hái. rar 0..3; el = nguyên tố (chỉ gậy phép dùng để đổi màu pha lê).
    public static Sprite Weapon(int id, int rar, int el = 0)
    {
        string key = "w" + id + "_" + rar + "_" + (id == 9 || id == 6 ? el : 0);
        return Get(key, () => DrawWeapon(id, rar, el));
    }

    static Sprite DrawWeapon(int id, int rar, int el)
    {
        Color32 acc = rar == 0 ? K(0x8FA0C4) : C32(RarityCols[rar]);
        if ((id == 9 || id == 6) && el > 0) acc = C32(ElementCols[el]);
        Color32 steel = K(0xB9C1D8), steelHi = K(0xEEF2FF), steelLo = K(0x6B7392), dark = K(0x2B2D44), wood = K(0x7A4F2E), woodHi = K(0xA8703F), woodLo = K(0x4C2F1B),
                gold = K(0xD4A33F), gold2 = K(0xF6D57A), bronze = K(0xA9703A), bronzeHi = K(0xD9A15E), bronzeLo = K(0x6B4423);
        Px c; Vector2 pv;
        switch (id)
        {
            case 0: // Súng ngắn ma thuật
                c = new Px(18, 11);
                c.Rect(3, 0, 4, 6, wood); c.Rect(3, 0, 1, 6, woodHi); c.Rect(6, 0, 1, 6, woodLo); c.Rect(4, 4, 2, 1, gold);
                c.Slab(3, 5, 12, 4, steel, steelHi, steelLo); c.Rect(14, 6, 4, 2, dark); c.Rect(14, 7, 4, 1, steelLo);
                c.Rect(5, 4, 3, 1, dark); c.Rect(2, 8, 2, 2, steelLo);
                c.Rect(7, 6, 3, 2, acc); c.Set(8, 7, White); c.Rect(10, 5, 3, 1, gold);
                pv = new Vector2(5f / 18f, 3f / 11f); break;
            case 1: // Súng trường
                c = new Px(30, 11);
                c.Poly(V(0, 3, 8, 5, 8, 8, 0, 9), wood); c.Rect(1, 4, 7, 1, woodHi); c.Rect(0, 3, 2, 6, woodLo);
                c.Slab(8, 4, 10, 5, steel, steelHi, steelLo); c.Rect(18, 6, 12, 2, steelLo); c.Rect(18, 7, 12, 1, steel); c.Rect(28, 5, 2, 4, dark);
                c.Rect(10, 9, 7, 2, dark); c.Rect(11, 10, 5, 1, acc); c.Set(10, 10, White);   // ống ngắm
                c.Rect(11, 0, 3, 4, dark); c.Rect(11, 1, 1, 3, steelLo);                      // băng đạn
                c.Rect(8, 2, 2, 3, wood); c.Rect(14, 5, 3, 2, acc);
                pv = new Vector2(9f / 30f, 6f / 11f); break;
            case 2: // Súng laze
                c = new Px(24, 12);
                c.Rect(2, 0, 4, 5, dark); c.Slab(3, 4, 14, 6, K(0xDDE4F4), White, K(0x8D96B4)); c.Rect(16, 5, 6, 4, dark);
                c.Rect(21, 4, 3, 6, steelLo); c.Rect(22, 5, 2, 4, acc);
                c.Rect(6, 5, 7, 4, acc); c.Rect(7, 6, 5, 2, Lite(acc, .6f)); c.Rect(5, 5, 1, 4, dark); c.Rect(13, 5, 1, 4, dark);   // lõi năng lượng sáng
                c.Rect(8, 10, 5, 2, steelLo); c.Rect(10, 1, 3, 3, dark);
                pv = new Vector2(5f / 24f, 4f / 12f); break;
            case 3: // Cung
                c = new Px(16, 32);
                for (int i = 0; i < 28; i++)
                {
                    float t = (i - 13.5f) / 13.5f, x = 12f - 9f * t * t; int y = 2 + i;
                    c.Rect(Mathf.RoundToInt(x), y, 2, 1, wood); c.Set(Mathf.RoundToInt(x) + 1, y, woodHi);
                    if (i % 7 == 3) c.Rect(Mathf.RoundToInt(x) - 1, y, 4, 1, gold);
                }
                c.Line(3, 3, 3, 28, K(0xE8E0D0)); c.Rect(10, 14, 4, 5, dark); c.Rect(11, 15, 2, 3, acc);   // dây và tay cầm
                c.Poly(V(3, 2, 6, 5, 3, 8), woodHi); c.Poly(V(3, 29, 6, 26, 3, 23), woodHi);
                pv = new Vector2(12f / 16f, 16.5f / 32f); break;
            case 4: // Dao găm cong
                c = new Px(20, 9);
                c.Poly(V(6, 3, 18, 6, 19, 5, 14, 2, 6, 1), steel); c.Poly(V(6, 4, 17, 6, 11, 4), steelHi); c.Line(6, 2, 14, 2, steelLo); c.Set(19, 5, White);
                c.Rect(5, 0, 2, 7, gold); c.Rect(5, 6, 2, 1, gold2); c.Rect(1, 2, 4, 3, dark); c.Rect(1, 3, 4, 1, wood);
                c.Rect(0, 2, 1, 3, acc); c.Set(0, 3, Lite(acc, .6f));
                pv = new Vector2(3f / 20f, 3.5f / 9f); break;
            case 5: // Thương
                c = new Px(42, 9);
                c.Rect(0, 3, 32, 3, wood); c.Rect(0, 4, 32, 1, woodHi); c.Rect(0, 3, 32, 1, woodLo); c.Rect(2, 3, 2, 3, gold); c.Rect(14, 3, 2, 3, gold);
                c.Poly(V(31, 1, 42, 4, 31, 8, 28, 4), steel); c.Poly(V(31, 4, 41, 4, 31, 7), steelHi); c.Line(30, 4, 40, 4, steelLo);
                c.Rect(27, 2, 2, 5, acc); c.Poly(V(27, 6, 27, 9, 23, 7), K(0xC23B4E)); c.Poly(V(27, 2, 27, 0, 23, 2), K(0x8E2A38));
                pv = new Vector2(8f / 42f, 4.5f / 9f); break;
            case 6: // Pháp khí: quả cầu rune trôi lơ lửng
                c = new Px(16, 16);
                c.Blob(8, 8, 5.6f, 5.6f, K(0x2E2850), K(0x5A4D96), K(0x1B1633));
                c.Ellipse(8, 8, 3f, 3f, acc); c.Ellipse(7.5f, 8.5f, 1.7f, 1.7f, Lite(acc, .65f));
                for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; c.Set(8 + Mathf.RoundToInt(Mathf.Cos(a) * 7f), 8 + Mathf.RoundToInt(Mathf.Sin(a) * 7f), (i % 2 == 0) ? gold2 : gold); }
                c.Set(8, 15, gold2); c.Set(8, 0, gold2); c.Set(0, 8, gold2); c.Set(15, 8, gold2);
                pv = new Vector2(.5f, .5f); break;
            case 7: // Đại bác cầm tay
                c = new Px(28, 16);
                c.Poly(V(4, 4, 20, 2, 26, 3, 26, 13, 20, 14, 4, 12), bronze);
                c.Poly(V(5, 10, 20, 12, 26, 12, 26, 11, 5, 8), bronzeHi); c.Poly(V(5, 5, 20, 3, 26, 4, 26, 3, 20, 2, 5, 4), bronzeLo);
                c.Rect(24, 2, 4, 12, dark); c.Rect(25, 4, 3, 8, K(0x0C0A14)); c.Rect(9, 2, 2, 12, gold); c.Rect(16, 3, 2, 10, gold); c.Rect(9, 12, 2, 1, gold2);
                c.Ellipse(3, 8, 3.5f, 4.2f, bronzeLo); c.Ellipse(2.5f, 8.5f, 2f, 2.6f, bronze); c.Rect(1, 12, 2, 3, K(0xFFA33A)); c.Set(1, 14, K(0xFFEFA0));
                c.Rect(12, 0, 4, 3, wood); c.Rect(12, 1, 4, 1, woodHi); c.Rect(12, 8, 3, 3, acc); c.Set(13, 9, White);
                pv = new Vector2(12f / 28f, 3f / 16f); break;
            case 8: // Bệ phóng tên lửa
                c = new Px(32, 14);
                c.Slab(4, 4, 24, 7, K(0x59607A), K(0x8E97B8), K(0x363B52)); c.Rect(26, 3, 4, 9, K(0x2D3046)); c.Rect(2, 3, 4, 9, K(0x2D3046)); c.Rect(2, 5, 2, 5, K(0xFF9A3C));
                c.Poly(V(28, 5, 32, 7, 28, 9), K(0xD9453C)); c.Poly(V(28, 6, 31, 7, 28, 8), K(0xFFC9B0)); c.Rect(12, 4, 2, 7, K(0xD9453C)); c.Rect(17, 4, 2, 7, K(0xD9453C));
                c.Rect(9, 0, 4, 5, dark); c.Rect(10, 1, 2, 3, steelLo); c.Rect(14, 11, 8, 3, dark); c.Rect(15, 12, 6, 1, acc); c.Set(15, 12, White);
                pv = new Vector2(11f / 32f, 3f / 14f); break;
            case 9: // Gậy phép
                c = new Px(36, 14);
                c.Rect(0, 5, 26, 3, wood); c.Rect(0, 6, 26, 1, woodHi); c.Rect(0, 5, 26, 1, woodLo); c.Rect(2, 5, 2, 3, gold); c.Rect(12, 5, 2, 3, gold);
                c.Poly(V(24, 4, 27, 7, 24, 11, 22, 7), gold); c.Poly(V(26, 1, 36, 7, 26, 13, 29, 7), dark);
                c.Poly(V(27, 3, 35, 7, 27, 11, 30, 7), acc); c.Poly(V(28, 7, 33, 7, 28, 10), Lite(acc, .6f)); c.Set(29, 9, White); c.Set(30, 8, White);
                c.Poly(V(24, 3, 28, 1, 26, 4), gold2); c.Poly(V(24, 11, 28, 13, 26, 10), gold2);
                pv = new Vector2(6f / 36f, 6.5f / 14f); break;
            case 10: // Rìu chiến
                c = new Px(30, 26);
                c.Rect(0, 11, 26, 3, wood); c.Rect(0, 12, 26, 1, woodHi); c.Rect(0, 11, 26, 1, woodLo); c.Rect(3, 11, 2, 3, gold); c.Rect(9, 11, 2, 3, gold);
                c.Poly(V(18, 12, 20, 25, 26, 24, 30, 13, 26, 2, 20, 1), steel);
                c.Poly(V(19, 13, 21, 24, 25, 23, 28, 13), steelHi); c.Poly(V(19, 12, 21, 2, 25, 3, 28, 12), steelLo);
                c.Line(29, 13, 29, 13, White); c.Rect(21, 11, 4, 4, acc); c.Set(22, 13, White); c.Rect(16, 9, 5, 7, dark);
                pv = new Vector2(5f / 30f, 12.5f / 26f); break;
            default: // Lưỡi hái
                c = new Px(34, 28);
                c.Rect(0, 4, 28, 3, wood); c.Rect(0, 5, 28, 1, woodHi); c.Rect(0, 4, 28, 1, woodLo); c.Rect(4, 4, 2, 3, gold); c.Rect(14, 4, 2, 3, gold);
                c.Poly(V(25, 7, 25, 14, 28, 21, 34, 27, 32, 22, 31, 15, 30, 10, 30, 7), dark);
                c.Poly(V(26, 8, 26, 14, 28, 20, 32, 25, 30, 19, 29, 13, 29, 8), steel); c.Poly(V(27, 9, 27, 14, 29, 20, 31, 24), steelHi);
                c.Line(26, 7, 33, 26, acc); c.Rect(24, 3, 5, 5, gold); c.Set(26, 5, acc);
                pv = new Vector2(5f / 34f, 5.5f / 28f); break;
        }
        if (rar == 3) { foreach (var q in new[] { new Vector2(2, 2), new Vector2(c.w - 3, c.h - 2) }) c.Set((int)q.x, (int)q.y, gold2); }
        c.Outline(rar == 3 ? Shade(gold, .55f) : rar == 2 ? Shade(acc, .6f) : Ink);
        return c.ToSprite(pv);
    }

    /// <summary>Quả cầu nhỏ bay quanh người của Pháp khí.</summary>
    public static Sprite OrbitOrb(int el) => Get("orb" + el, () =>
    {
        var c = new Px(10, 10); Color32 col = el > 0 ? C32(ElementCols[el]) : K(0xB89BFF);
        c.Blob(5, 5, 3.6f, 3.6f, col, Lite(col, .6f), Shade(col, .45f)); c.Set(3, 7, White); c.Outline(Shade(col, .65f));
        return c.ToSprite(new Vector2(.5f, .5f));
    });

    // ======================================================================== ĐẠN
    public static Sprite Arrow => Get("arrow", () =>
    {
        var c = new Px(18, 7);
        c.Rect(2, 3, 13, 1, K(0xD9C8A0)); c.Rect(2, 2, 13, 1, K(0x8A6A40)); c.Poly(V(14, 1, 18, 3, 14, 6), K(0xDDE4F4)); c.Poly(V(14, 3, 17, 3, 14, 5), White);
        c.Poly(V(0, 0, 4, 3, 0, 3), K(0xC23B4E)); c.Poly(V(0, 6, 4, 3, 0, 3), K(0xE5637A)); c.Outline(Ink);
        return c.ToSprite(new Vector2(.85f, .5f));
    });

    public static Sprite Rocket => Get("rocket", () =>
    {
        var c = new Px(16, 8);
        c.Slab(3, 1, 9, 6, K(0x9AA3C2), K(0xDDE4F4), K(0x555C7A)); c.Poly(V(11, 1, 16, 4, 11, 7), K(0xD9453C)); c.Poly(V(11, 4, 15, 4, 11, 6), K(0xFFC9B0));
        c.Rect(5, 1, 2, 6, K(0xD9453C)); c.Poly(V(3, 0, 0, 0, 3, 3), K(0x555C7A)); c.Poly(V(3, 8, 0, 8, 3, 5), K(0x555C7A)); c.Outline(Ink);
        return c.ToSprite(new Vector2(.7f, .5f));
    });

    public static Sprite Cannonball => Get("cball", () =>
    {
        var c = new Px(10, 10); c.Blob(5, 5, 4.4f, 4.4f, K(0x4A4E66), K(0xB9C1D8), K(0x20222F)); c.Set(3, 7, White); c.Outline(Ink);
        return c.ToSprite(new Vector2(.5f, .5f));
    });

    public static Sprite Shard => Get("shard", () =>
    {
        var c = new Px(9, 9); c.Poly(V(4.5f, 0, 9, 4.5f, 4.5f, 9, 0, 4.5f), White); c.Poly(V(4.5f, 4.5f, 9, 4.5f, 4.5f, 9), K(0xC8C8D8)); c.Set(3, 5, White);
        return c.ToSprite(new Vector2(.5f, .5f));
    });

    public static Sprite Orb => Get("orbw", () =>
    {
        var c = new Px(12, 12); c.Ellipse(6, 6, 5.6f, 5.6f, K(0xB0B0B0)); c.Ellipse(6, 6, 4.2f, 4.2f, K(0xE0E0E0)); c.Ellipse(5.4f, 6.6f, 2.4f, 2.4f, White);
        return c.ToSprite(new Vector2(.5f, .5f));
    });

    public static Sprite Vial => Get("vial", () =>
    {
        var c = new Px(10, 12);
        c.Ellipse(5, 4.5f, 4f, 4f, K(0xBFF6FF)); c.Ellipse(5, 4f, 3.2f, 3f, K(0x7DE05A)); c.Rect(4, 8, 2, 3, K(0xBFF6FF)); c.Rect(3, 11, 4, 1, K(0x7A4F2E));
        c.Set(3, 5, White); c.Set(7, 3, K(0xC8FF9A)); c.Outline(Ink);
        return c.ToSprite(new Vector2(.5f, .5f));
    });

    public static Sprite Thorn => Get("thorn", () =>
    {
        var c = new Px(8, 14); c.Poly(V(4, 14, 8, 0, 0, 0), K(0x9B5CFF)); c.Poly(V(4, 14, 8, 0, 4, 0), K(0x6A33C2)); c.Rect(3, 4, 1, 6, K(0xE0C8FF)); c.Outline(Ink);
        return c.ToSprite(new Vector2(.5f, 0f));
    });

    public static Sprite Leaf => Get("leaf", () =>
    {
        var c = new Px(10, 8); c.Poly(V(0, 4, 5, 0, 10, 4, 5, 8), K(0x5FD35A)); c.Poly(V(0, 4, 10, 4, 5, 0), K(0x3A9A3E)); c.Line(1, 4, 9, 4, K(0xB8F5A0)); c.Outline(Ink);
        return c.ToSprite(new Vector2(.5f, .5f));
    });

    public static Sprite Heart => Get("heart", () =>
    {
        var c = new Px(13, 12);
        c.Ellipse(3.8f, 8.2f, 3.6f, 3.4f, K(0xE5484D)); c.Ellipse(9.2f, 8.2f, 3.6f, 3.4f, K(0xE5484D));
        c.Poly(V(.2f, 7.6f, 12.8f, 7.6f, 6.5f, .2f), K(0xE5484D)); c.Rect(2, 9, 2, 2, K(0xFFB0BA)); c.Set(4, 8, K(0xFFB0BA)); c.Poly(V(7, 1, 12, 7, 8, 6), K(0xA8283A));
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, .5f));
    });

    public static Sprite InkDrop => Get("ink", () =>
    {
        var c = new Px(10, 13);
        c.Poly(V(5, 13, 9, 5, 5, 0, 1, 5), K(0x6B5BFF)); c.Ellipse(5, 4.6f, 4f, 4f, K(0x6B5BFF)); c.Poly(V(5, 12, 7, 6, 5, 3), K(0xA79BFF)); c.Set(3, 5, White);
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, .5f));
    });

    public static Sprite MemoryShard => GetN("shard5", () => new[] { Crystal(0), Crystal(1) })[0];
    public static Sprite[] MemoryShardFrames => GetN("shard5", () => new[] { Crystal(0), Crystal(1) });

    static Sprite Crystal(int f)
    {
        var c = new Px(16, 26); int s = f;
        c.Poly(V(8, 25, 14, 14, 8, 1, 2, 14), K(0x6FE3FF)); c.Poly(V(8, 25, 14, 14, 8, 1), K(0x3FA6D9)); c.Poly(V(8, 24, 5, 14, 8, 6), K(0xCFF8FF));
        c.Poly(V(8, 25, 8, 1, 6, 14), K(0x9FF0FF)); c.Set(6, 18, White); c.Set(6, 17, White); c.Set(5, 14 + s, White);
        c.Outline(K(0x1B3B6A)); return c.ToSprite(new Vector2(.5f, .5f));
    }

    // ======================================================================== ICON NGUYÊN TỐ VÀ BUFF (16x16)
    public static Sprite ElementIcon(int el) => Get("eico" + el, () =>
    {
        var c = new Px(16, 16); Color32 col = el > 0 ? C32(ElementCols[el]) : K(0x8E86B8), hi = Lite(col, .6f), lo = Shade(col, .45f);
        switch (el)
        {
            case 1: c.Poly(V(8, 15, 13, 6, 8, 1, 3, 6), col); c.Ellipse(8, 6, 5.2f, 5f, col); c.Poly(V(8, 14, 10, 7, 8, 3), hi); c.Set(5, 5, White); break;
            case 2: c.Poly(V(8, 15, 13, 8, 10, 1, 6, 1, 3, 8), K(0xE5484D)); c.Poly(V(8, 12, 11, 7, 9, 2, 7, 2, 5, 7), col); c.Poly(V(8, 9, 10, 5, 8, 2, 6, 5), K(0xFFE14D)); break;
            case 3: c.Line(8, 1, 8, 14, col, 2); c.Line(2, 4, 14, 11, col, 2); c.Line(2, 11, 14, 4, col, 2); c.Rect(7, 7, 2, 2, White); c.Set(8, 15, hi); c.Set(8, 0, hi); break;
            case 4: c.Poly(V(10, 15, 3, 7, 7, 7, 5, 1, 13, 9, 9, 9, 11, 15), col); c.Poly(V(9, 13, 6, 8, 9, 8, 7, 4), hi); break;
            case 5: c.Poly(V(2, 2, 14, 2, 15, 8, 11, 14, 4, 13, 1, 7), K(0x8C6B3E)); c.Poly(V(3, 4, 13, 4, 14, 8, 10, 12, 5, 12, 3, 8), col); c.Rect(5, 8, 3, 1, lo); c.Rect(9, 6, 3, 1, hi); break;
            case 6: c.Line(2, 11, 12, 11, col, 2); c.Line(4, 7, 14, 7, col, 2); c.Line(2, 3, 10, 3, col, 2); c.Ellipse(12, 10.5f, 2.4f, 2.2f, Color.clear); c.Rect(12, 12, 2, 2, col); c.Rect(14, 6, 1, 2, hi); break;
            case 7: c.Ellipse(8, 7, 5.2f, 5.2f, K(0xE8E0D0)); c.Rect(5, 1, 6, 4, K(0xE8E0D0)); c.Rect(5, 6, 2, 3, K(0x1B1633)); c.Rect(9, 6, 2, 3, K(0x1B1633)); c.Rect(7, 3, 1, 2, K(0x1B1633)); c.Rect(9, 3, 1, 2, K(0x1B1633)); c.Rect(6, 2, 4, 1, col); break;
            case 8: c.Poly(V(2, 2, 14, 14, 14, 6, 9, 1), col); c.Poly(V(3, 3, 13, 13, 8, 4), lo); c.Line(3, 3, 12, 12, hi); break;
            default: c.Ellipse(8, 8, 5f, 5f, col); break;
        }
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, .5f));
    });

    /// <summary>Icon buff: 0 Thêm đạn, 1 Đạn nảy, 2 Đạn xuyên, 3 Kháng nguyên tố, 4 Nhiễm nguyên tố, 5 Sóng chém, 6 Tốc đánh, 7 Hồi máu khi hạ quái, 8 Nổ lan rộng.</summary>
    public static Sprite BuffIcon(int id) => Get("bico" + id, () =>
    {
        var c = new Px(16, 16); Color32 g = K(0xF1CB6E), s = K(0xDCE2F0), r = K(0xE5484D), b = K(0x6FB3FF), o = K(0xFF9A3C);
        switch (id)
        {
            case 0: for (int i = 0; i < 3; i++) { c.Rect(2 + i * 4, 3, 3, 8, g); c.Poly(V(2 + i * 4, 11, 5 + i * 4, 11, 3.5f + i * 4, 14), o); c.Rect(2 + i * 4, 3, 1, 8, Lite(g)); } break;
            case 1: c.Line(2, 3, 7, 12, s, 2); c.Line(7, 12, 12, 4, s, 2); c.Poly(V(12, 2, 15, 7, 9, 6), g); break;
            case 2: c.Rect(1, 7, 11, 2, s); c.Poly(V(11, 4, 16, 8, 11, 12), g); c.Rect(6, 3, 2, 10, K(0x6A7392)); break;
            case 3: c.Poly(V(8, 15, 14, 12, 13, 5, 8, 1, 3, 5, 2, 12), b); c.Poly(V(8, 13, 12, 11, 11, 6, 8, 3, 5, 6, 4, 11), K(0x2F6FC2)); c.Rect(7, 4, 2, 8, s); c.Rect(5, 8, 6, 2, s); break;
            case 4: c.Poly(V(8, 15, 12, 8, 8, 2, 4, 8), K(0x8F6BFF)); c.Ellipse(8, 7, 4.4f, 4.4f, K(0x8F6BFF)); c.Rect(6, 6, 2, 3, K(0xD9C8FF)); c.Set(12, 13, White); c.Set(13, 12, White); c.Set(3, 12, White); break;
            case 5: c.Poly(V(1, 3, 15, 8, 1, 13, 5, 8), s); c.Poly(V(2, 5, 12, 8, 2, 11, 5, 8), White); c.Poly(V(4, 8, 14, 8, 6, 11), g); break;
            case 6: c.Poly(V(1, 2, 7, 8, 1, 14, 4, 8), g); c.Poly(V(8, 2, 14, 8, 8, 14, 11, 8), o); break;
            case 7: c.Ellipse(5, 10, 3.2f, 3f, r); c.Ellipse(11, 10, 3.2f, 3f, r); c.Poly(V(1.8f, 9.4f, 14.2f, 9.4f, 8, 2), r); c.Rect(7, 5, 2, 6, K(0xFFE8EC)); c.Rect(5, 7, 6, 2, K(0xFFE8EC)); break;
            default: c.Ellipse(8, 8, 3f, 3f, K(0xFFE14D)); for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; c.Line(8 + Mathf.Cos(a) * 3.5f, 8 + Mathf.Sin(a) * 3.5f, 8 + Mathf.Cos(a) * 7f, 8 + Mathf.Sin(a) * 7f, i % 2 == 0 ? o : r); } break;
        }
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, .5f));
    });

    // ======================================================================== BÓNG TRẮNG (nháy trúng đòn)
    static readonly Dictionary<Sprite, Sprite> silCache = new Dictionary<Sprite, Sprite>();

    /// <summary>Bản trắng toàn phần của một sprite, dùng để nháy sáng khi trúng đòn (SpriteRenderer chỉ nhân màu nên không thể làm trắng hơn).</summary>
    public static Sprite Silhouette(Sprite s)
    {
        if (s == null) return null;
        if (silCache.TryGetValue(s, out var r) && r != null) return r;
        var t = s.texture; var px = new Px((int)s.rect.width, (int)s.rect.height);
        var all = t.GetPixels32();
        for (int y = 0; y < px.h; y++) for (int x = 0; x < px.w; x++)
            {
                var q = all[((int)s.rect.y + y) * t.width + (int)s.rect.x + x];
                if (q.a != 0) px.p[y * px.w + x] = White;
            }
        r = px.ToSprite(new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height), s.pixelsPerUnit); silCache[s] = r; return r;
    }
}
