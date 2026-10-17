using UnityEngine;

/// <summary>Sàn, tường, đạo cụ trong phòng, vật thể boss, lửa trại, bệ đá, cây, cú và cảnh cuối game. biome: 0 Hóa, 1 Sinh, 2 Vật lý.</summary>
public static partial class RogueArt
{
    static int Hash(int x, int y, int s) { unchecked { int h = x * 374761393 + y * 668265263 + s * 2147483647; h = (h ^ (h >> 13)) * 1274126177; return (h ^ (h >> 16)) & 0x7fffffff; } }
    static float Rnd(int x, int y, int s) => (Hash(x, y, s) % 1000) / 1000f;

    // ======================================================================== SÀN: 4 biến thể mỗi khu, ô 32x32
    public static Sprite[] FloorTiles(int biome) => GetN("floor" + biome, () =>
    {
        var r = new Sprite[4]; for (int v = 0; v < 4; v++) r[v] = DrawFloor(biome, v); return r;
    });

    static Sprite DrawFloor(int biome, int v)
    {
        var c = new Px(32, 32);
        Color32 a, a2, grout, spec;
        if (biome == 0) { a = K(0x363450); a2 = K(0x3C3A58); grout = K(0x1D1B2E); spec = K(0x504E72); }
        else if (biome == 1) { a = K(0x2C3A36); a2 = K(0x33433E); grout = K(0x161F1D); spec = K(0x45584F); }
        else { a = K(0x2E3550); a2 = K(0x353D5B); grout = K(0x171B2D); spec = K(0x4B567C); }
        c.Rect(0, 0, 32, 32, a);
        for (int qy = 0; qy < 2; qy++) for (int qx = 0; qx < 2; qx++) if ((qx + qy + v) % 2 == 0) c.Rect(qx * 16, qy * 16, 16, 16, a2);
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { float n = Rnd(x, y, v + biome * 7); if (n > .93f) c.Set(x, y, spec); else if (n < .04f) c.Set(x, y, Shade(a, .25f)); }
        c.Rect(0, 15, 32, 1, grout); c.Rect(15, 0, 1, 32, grout); c.Rect(0, 31, 32, 1, grout); c.Rect(31, 0, 1, 32, grout);
        c.Rect(0, 16, 32, 1, Lite(a, .08f)); c.Rect(16, 0, 1, 32, Lite(a, .05f)); c.Rect(0, 0, 32, 1, Lite(a, .06f));
        if (biome == 0)
        {   // vệt hóa chất và vết nứt
            if (v == 1) { c.Ellipse(9, 22, 5, 3, K(0x2F4A3A)); c.Ellipse(8, 22, 3.2f, 1.8f, K(0x3E6A4A)); c.Set(7, 23, K(0x7DFF5A)); }
            if (v == 2) { c.Line(20, 31, 24, 22, grout); c.Line(24, 22, 22, 15, grout); c.Line(24, 22, 29, 19, grout); }
            if (v == 3) { c.Rect(4, 4, 3, 1, K(0x6B5BA0)); c.Rect(6, 4, 1, 3, K(0x6B5BA0)); }
        }
        else if (biome == 1)
        {   // rêu, mầm phát sáng
            for (int i = 0; i < 20 + v * 6; i++) { int x = Hash(i, v, 3) % 32, y = Hash(i, v, 4) % 32; c.Set(x, y, i % 3 == 0 ? K(0x3F7A44) : K(0x2E5A36)); c.Set(x + 1, y, K(0x2E5A36)); }
            if (v == 2) { c.Rect(22, 8, 1, 3, K(0x6BD36A)); c.Set(22, 11, K(0xC8FF9A)); c.Set(20, 9, K(0x6BD36A)); }
            if (v == 3) { c.Line(4, 20, 14, 28, K(0x1F4A2C)); c.Line(14, 28, 24, 26, K(0x1F4A2C)); }
        }
        else
        {   // đinh tán, ô kính nhìn xuống mây
            for (int cx = 3; cx < 32; cx += 12) for (int cy = 3; cy < 32; cy += 12) c.Set(cx, cy, K(0x6F7BA8));
            if (v == 1 || v == 3) { c.Rect(20, 20, 8, 8, K(0x1D2A4A)); c.Rect(21, 21, 6, 6, K(0x3A5A92)); c.Rect(22, 24, 4, 1, K(0x9FC4F0)); c.Rect(21, 22, 2, 1, K(0x7FA8E0)); }
            if (v == 2) { c.Rect(5, 5, 6, 1, K(0x4DE0FF)); c.Rect(5, 6, 1, 4, K(0x4DE0FF)); }
        }
        return c.ToSprite(new Vector2(.5f, .5f));
    }

    // ======================================================================== TƯỜNG: băng 32x28, 2 biến thể
    public static Sprite[] WallTiles(int biome) => GetN("wall" + biome, () => new[] { DrawWall(biome, 0), DrawWall(biome, 1) });

    static Sprite DrawWall(int biome, int v)
    {
        var c = new Px(32, 28);
        if (biome == 0)
        {
            Color32 b = K(0x2E2B45), bh = K(0x403C60), bd = K(0x1B1930);
            c.Rect(0, 0, 32, 28, b); for (int y = 0; y < 28; y += 7) c.Rect(0, y, 32, 1, bd); for (int y = 0; y < 28; y += 7) for (int x = (y / 7 % 2) * 8; x < 32; x += 16) c.Rect(x, y, 1, 7, bd);
            c.Rect(0, 27, 32, 1, bh); c.Rect(0, 20, 32, 1, Lite(b, .08f));
            if (v == 1) { c.Rect(4, 8, 24, 4, K(0x5A6288)); c.Rect(4, 11, 24, 1, K(0x8E97B8)); c.Rect(4, 8, 24, 1, K(0x363B52)); c.Rect(6, 6, 2, 8, K(0x363B52)); c.Rect(22, 6, 2, 8, K(0x363B52)); c.Rect(14, 9, 3, 2, K(0x7DFF5A)); }
            else { c.Rect(11, 9, 10, 12, K(0x1B1930)); c.Rect(12, 10, 8, 10, K(0x3A5A4A)); c.Rect(12, 10, 8, 3, K(0x6BD36A)); c.Set(14, 15, K(0xC8FF9A)); c.Set(17, 17, K(0xC8FF9A)); }
        }
        else if (biome == 1)
        {
            Color32 b = K(0x20302C), bd = K(0x121C1A), glass = K(0x2E5A4A);
            c.Rect(0, 0, 32, 28, b); c.Rect(0, 0, 32, 3, bd); c.Rect(0, 26, 32, 2, Lite(b, .12f));
            c.Rect(3, 4, 26, 20, bd); c.Rect(4, 5, 24, 18, v == 0 ? glass : K(0x3A6A4A)); c.Rect(4, 5, 24, 5, Lite(glass, .14f));
            for (int i = 0; i < 6; i++) { int x = 6 + i * 4; c.Line(x, 5, x + (i % 2) * 2 - 1, 13 + (i * 3) % 6, K(0x2F7A3E)); c.Set(x, 13 + (i * 3) % 6, K(0x6BD36A)); }
            c.Rect(8, 7, 1, 1, K(0xC8FF9A)); c.Rect(20, 11, 1, 1, K(0xC8FF9A)); c.Rect(14, 17, 1, 1, K(0xC8FF9A)); c.Rect(4, 5, 1, 18, Lite(glass, .3f)); c.Rect(16, 4, 1, 20, bd);
        }
        else
        {
            Color32 b = K(0x2A3050), bd = K(0x171B2D);
            c.Rect(0, 0, 32, 28, b); c.Rect(0, 0, 32, 3, bd); c.Rect(0, 26, 32, 2, Lite(b, .12f));
            c.Rect(3, 4, 26, 20, bd); for (int y = 0; y < 18; y++) c.Rect(4, 5 + y, 24, 1, Mix(K(0x2A3F7A), K(0xE59A8A), y / 18f * .8f));
            c.Ellipse(10, 14, 6, 2, K(0xC8D6F5)); c.Ellipse(22, 10, 5, 1.6f, K(0xB8C8EE)); c.Ellipse(16, 8, 4, 1.2f, K(0xC8D6F5)); c.Rect(15, 4, 2, 20, bd); c.Rect(3, 13, 26, 1, bd);
            if (v == 1) { c.Rect(5, 6, 8, 1, K(0x4DE0FF)); }
        }
        return c.ToSprite(new Vector2(.5f, .5f));
    }

    // ======================================================================== TRỤ ĐÁ / ĐÈN LỒNG / MÁY BIẾN ÁP (cơ chế boss)
    public static Sprite StonePillar => Get("pillar", () =>
    {
        var c = new Px(22, 44); Color32 s = K(0x7A7690), sh = K(0xA6A2BC), sl = K(0x4C4864), sd = K(0x2E2B42);
        c.Slab(2, 0, 18, 6, s, sh, sl); c.Rect(5, 6, 12, 32, s); c.Rect(5, 6, 3, 32, sh); c.Rect(14, 6, 3, 32, sl); c.Rect(5, 12, 12, 1, sd); c.Rect(5, 30, 12, 1, sd);
        c.Slab(1, 38, 20, 6, s, sh, sl); c.Rect(3, 36, 16, 2, sl);
        c.Rect(7, 17, 8, 10, sd); c.Rect(8, 18, 6, 8, sl);   // khung rune
        c.Set(6, 8, sd); c.Set(13, 33, sd); c.Set(9, 10, sl);
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, 1f / 44f));
    });

    /// <summary>Ký hiệu phát sáng đặt lên khung rune của trụ đá (tô màu theo độ khó bằng SpriteRenderer.color).</summary>
    public static Sprite PillarRune => Get("prune", () =>
    {
        var c = new Px(8, 10); c.Rect(3, 0, 2, 10, White); c.Rect(0, 3, 8, 2, White); c.Rect(1, 7, 6, 1, White); c.Set(0, 8, White); c.Set(7, 8, White);
        return c.ToSprite(new Vector2(.5f, .5f));
    });

    public static Sprite[] Lantern => GetN("lantern", () =>
    {
        var r = new Sprite[2];
        for (int f = 0; f < 2; f++)
        {
            var c = new Px(22, 38); Color32 red = K(0xD9455A), redL = K(0xFF8A9A), redD = K(0x8E2438), gold = K(0xD4A33F);
            c.Rect(10, 33, 2, 5, K(0x4C4864)); c.Rect(6, 32, 10, 2, gold); c.Ellipse(11, 18, 9, 13, redD); c.Ellipse(10.5f, 18.5f, 8.2f, 12.2f, red); c.Ellipse(8, 24, 3.4f, 5, redL);
            for (int i = 0; i < 4; i++) c.Rect(3, 10 + i * 5, 16, 1, redD); c.Rect(6, 4, 10, 2, gold); c.Rect(8, 2, 6, 2, gold);
            c.Rect(8, 10, 6, 14, K(0xFFE9B0)); c.Rect(9, 12, 4, 10, K(0xFFF8D8)); c.Rect(10, 14, 2, 1, redD); c.Rect(10, 17, 2, 1, redD); c.Rect(10, 20, 2, 1, redD);
            c.Rect(10 - f, 0, 2, 3 + f, gold); c.Rect(9 + f, 0, 4, 1, K(0xFF8A9A));
            c.Outline(Ink); r[f] = c.ToSprite(new Vector2(.5f, 1f / 38f));
        }
        return r;
    });

    public static Sprite[] Transformer => GetN("trafo", () =>
    {
        var r = new Sprite[2];
        for (int f = 0; f < 2; f++)
        {
            var c = new Px(28, 40); Color32 hull = K(0x7F89AC), hullL = K(0xC3CCE8), hullD = K(0x444A68), cy = K(0x4DE0FF), cu = K(0xC87A3A);
            c.Slab(2, 0, 24, 6, hullD, hull, K(0x2B2F46)); c.Slab(4, 6, 20, 18, hull, hullL, hullD); c.Rect(7, 9, 14, 8, K(0x2B2F46)); c.Rect(8, 10, 12, 6, f == 0 ? cy : Lite(cy, .4f));
            c.Rect(8, 12, 12, 1, White); c.Rect(6, 19, 16, 2, hullD);
            c.Rect(11, 24, 6, 4, hullD); for (int i = 0; i < 6; i++) c.Rect(10, 28 + i, 8, 1, i % 2 == 0 ? cu : K(0xE8A060));
            c.Ellipse(14, 36, 4, 3, hullD); c.Ellipse(14, 36, 2.6f, 2f, f == 0 ? cy : White);
            c.Set(8 + f * 3, 34, cy); c.Set(20 - f * 3, 33, cy);
            c.Outline(Ink); r[f] = c.ToSprite(new Vector2(.5f, 1f / 40f));
        }
        return r;
    });

    // ======================================================================== CHÔNG GAI / DÂY LEO (nguy hiểm môi trường và chiêu boss)
    public static Sprite[] SpikeFrames(int el) => GetN("spike" + el, () =>
    {
        var r = new Sprite[4]; Color32 col = el == 8 ? K(0x4FB04A) : K(0xB050E0), hi = Lite(col, .55f), lo = Shade(col, .5f);
        for (int f = 0; f < 4; f++)
        {
            var c = new Px(20, 28); int hgt = new[] { 4, 12, 24, 20 }[f];
            for (int i = 0; i < 3; i++) { int x = 3 + i * 6; int h2 = i == 1 ? hgt : (int)(hgt * .7f); c.Poly(V(x, 0, x + 6, 0, x + 3, h2), col); c.Poly(V(x + 3, 0, x + 6, 0, x + 3, h2), lo); c.Line(x + 2, 2, x + 3, h2 - 3, hi); }
            c.Outline(Ink); r[f] = c.ToSprite(new Vector2(.5f, 0f));
        }
        return r;
    });

    public static Sprite[] VineFrames => GetN("vine", () =>
    {
        var r = new Sprite[3];
        for (int f = 0; f < 3; f++)
        {
            var c = new Px(18, 30); Color32 g = K(0x4FB04A), gl = K(0x8FE06A), gd = K(0x276A32); int hgt = new[] { 8, 18, 28 }[f];
            for (int i = 0; i < hgt; i++) { float x = 9 + Mathf.Sin(i * .5f + f) * 3.5f; c.Rect(Mathf.RoundToInt(x) - 1, i, 3, 1, g); c.Set(Mathf.RoundToInt(x) - 1, i, gl); c.Set(Mathf.RoundToInt(x) + 1, i, gd); if (i % 7 == 5) c.Poly(V(x, i, x + 4, i + 2, x, i + 3), gl); }
            c.Poly(V(8, hgt, 12, hgt, 10, hgt + 2), K(0xF078B0)); c.Outline(Ink); r[f] = c.ToSprite(new Vector2(.5f, 0f));
        }
        return r;
    });

    public static Sprite Crate => Get("crate", () =>
    {
        var c = new Px(18, 18); c.Slab(1, 1, 16, 16, K(0x8A6A44), K(0xB8905E), K(0x5A4028)); c.Line(1, 1, 16, 16, K(0x5A4028)); c.Line(1, 16, 16, 1, K(0x5A4028)); c.Rect(1, 1, 16, 1, K(0x3A2818));
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, .5f));
    });

    // ======================================================================== PHÒNG NGHỈ: LỬA TRẠI
    public static Sprite[] Campfire => GetN("campfire", () =>
    {
        var r = new Sprite[4];
        for (int f = 0; f < 4; f++)
        {
            var c = new Px(26, 30);
            c.Line(3, 3, 22, 7, K(0x6B4423), 3); c.Line(22, 3, 4, 7, K(0x7A4F2E), 3); c.Rect(4, 1, 18, 2, K(0x2B2D44)); for (int i = 0; i < 6; i++) c.Rect(3 + i * 4, 0, 3, 2, K(0x4A4E66));
            int h = 14 + (f % 2) * 3 + (f == 3 ? 2 : 0), sw = new[] { 0, 1, 0, -1 }[f];
            c.Poly(V(13, 4, 5, 8, 7 + sw, 6 + h / 2, 13 + sw, 6 + h, 19 + sw, 6 + h / 2, 21, 8), K(0xE5484D)); c.Poly(V(13, 4, 8, 7, 10 + sw, 4 + h / 2, 13 + sw, 4 + h - 2, 17 + sw, 4 + h / 2, 18, 7), K(0xFF9A3C));
            c.Poly(V(13, 4, 10, 6, 13 + sw, 2 + h / 2, 16, 6), K(0xFFE14D)); c.Poly(V(13, 4, 12, 5, 13 + sw, 2 + h / 3, 14, 5), K(0xFFF8D0));
            c.Set(5 + f * 5, 18 + f, K(0xFFD86B)); c.Set(20 - f * 3, 21 - f, K(0xFF9A3C));
            c.Outline(Ink); r[f] = c.ToSprite(new Vector2(.5f, 0f));
        }
        return r;
    });

    // ======================================================================== PHÒNG 16: BỆ ĐÁ CỔ + ĐÁ RUNE
    public static Sprite Altar => Get("altar", () =>
    {
        var c = new Px(88, 60); Color32 s = K(0x6E6A88), sh = K(0x9894B4), sl = K(0x47435E), sd = K(0x2B2840);
        c.Slab(0, 0, 88, 10, s, sh, sl); c.Slab(8, 10, 72, 10, s, sh, sl); c.Slab(16, 20, 56, 10, s, sh, sl);
        for (int x = 4; x < 88; x += 12) c.Rect(x, 2, 1, 6, sd); for (int x = 12; x < 80; x += 14) c.Rect(x, 12, 1, 6, sd); c.Rect(28, 22, 1, 6, sd); c.Rect(44, 22, 1, 6, sd); c.Rect(60, 22, 1, 6, sd);
        // lõi bệ: vòng rune khắc
        c.Ellipse(44, 40, 20, 8, sd); c.Ellipse(44, 41, 18.5f, 7, sl); c.Ellipse(44, 41, 13, 4.6f, sd);
        for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6f; c.Set(44 + Mathf.RoundToInt(Mathf.Cos(a) * 16), 41 + Mathf.RoundToInt(Mathf.Sin(a) * 5.6f), sh); }
        // vết nứt của phong ấn
        c.Line(44, 36, 36, 30, sd); c.Line(44, 36, 54, 31, sd); c.Line(36, 30, 30, 24, sd); c.Line(54, 31, 60, 24, sd);
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, 0f));
    });

    /// <summary>Đá rune đứng: 0 Lý (xanh), 1 Hóa (tím), 2 Sinh (lục). Vẽ xám; phần phát sáng là sprite RuneGlyph tô màu riêng.</summary>
    public static Sprite RuneStone => Get("rstone", () =>
    {
        var c = new Px(16, 34); Color32 s = K(0x6E6A88), sh = K(0x9894B4), sl = K(0x47435E), sd = K(0x2B2840);
        c.Poly(V(2, 0, 14, 0, 15, 26, 8, 34, 1, 27), s); c.Poly(V(2, 0, 6, 0, 6, 30, 1, 27), sh); c.Poly(V(10, 0, 14, 0, 15, 26, 10, 30), sl); c.Rect(5, 12, 6, 12, sd);
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, 0f));
    });

    public static Sprite RuneGlyph(int kind) => Get("glyph" + kind, () =>
    {
        var c = new Px(8, 12);
        if (kind == 0) { c.Poly(V(5, 12, 1, 5, 4, 5, 3, 0, 7, 7, 4, 7), White); }                                      // Lý: tia chớp
        else if (kind == 1) { c.Rect(3, 0, 2, 4, White); c.Ellipse(4, 6, 3.4f, 3.4f, White); c.Rect(2, 9, 4, 2, White); c.Ellipse(4, 6, 1.6f, 1.6f, Color.clear); }   // Hóa: bình
        else { c.Rect(3, 0, 2, 12, White); c.Poly(V(4, 6, 8, 11, 8, 6), White); c.Poly(V(4, 4, 0, 9, 0, 4), White); }    // Sinh: nhánh lá
        return c.ToSprite(new Vector2(.5f, .5f));
    });

    // ======================================================================== CÂY CỔ THỤ (cảnh kết) & THẦY CÚ
    public static Sprite AncientTree => Get("atree", () =>
    {
        var c = new Px(140, 150); Color32 bark = K(0x5A3F34), barkL = K(0x7C5A48), barkD = K(0x372420), leaf = K(0x4FB04A), leafL = K(0x9FE070), leafD = K(0x276A32);
        c.Poly(V(58, 0, 82, 0, 78, 50, 74, 80, 66, 80, 62, 50), barkD); c.Poly(V(60, 0, 80, 0, 76, 50, 72, 78, 68, 78, 64, 50), bark); c.Poly(V(60, 0, 66, 0, 66, 50, 64, 76), barkL);
        c.Poly(V(50, 0, 62, 20, 58, 0), barkD); c.Poly(V(90, 0, 78, 20, 82, 0), barkD);
        c.Line(70, 76, 40, 100, barkD, 4); c.Line(70, 76, 100, 102, barkD, 4); c.Line(70, 82, 70, 110, barkD, 4); c.Line(70, 76, 40, 100, bark, 2); c.Line(70, 76, 100, 102, bark, 2);
        foreach (var p in new[] { new Vector3(70, 124, 40), new Vector3(36, 106, 28), new Vector3(104, 108, 28), new Vector3(52, 130, 26), new Vector3(90, 132, 26), new Vector3(22, 92, 18), new Vector3(120, 94, 18) })
        { c.Blob(p.x, p.y, p.z, p.z * .8f, leaf, leafL, leafD); }
        for (int i = 0; i < 60; i++) { int x = 10 + Hash(i, 1, 9) % 120, y = 80 + Hash(i, 2, 9) % 66; if (c.Solid(x, y)) c.Set(x, y, i % 2 == 0 ? leafL : leafD); }
        for (int i = 0; i < 9; i++) { int x = 20 + Hash(i, 3, 9) % 100, y = 85 + Hash(i, 4, 9) % 55; if (c.Solid(x, y)) { c.Set(x, y, K(0xFFE14D)); c.Set(x + 1, y, K(0xFFB84D)); } }
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, 0f));
    });

    public static Sprite[] Owl => GetN("owl", () =>
    {
        var r = new Sprite[3];
        for (int f = 0; f < 3; f++)
        {
            var c = new Px(26, 32); Color32 br = K(0x8A6A4C), brL = K(0xB8946A), brD = K(0x54402E), cr = K(0xF0E2C0);
            int b = f == 1 ? 1 : 0;
            c.Rect(8, 0, 3, 4, K(0xE8A33C)); c.Rect(15, 0, 3, 4, K(0xE8A33C));
            c.Ellipse(13, 12, 9, 11, brD); c.Ellipse(13, 12.6f, 8.2f, 10.2f, br); c.Ellipse(13, 9, 5, 6.6f, cr); for (int i = 0; i < 3; i++) c.Rect(10 + i * 2, 7 + i % 2 * 3, 1, 2, brL);
            c.Poly(V(2, 14, 7, 20, 6, 4), brD); c.Poly(V(24, 14, 19, 20, 20, 4), brD); c.Poly(V(3, 13, 6, 17, 6, 6), br); c.Poly(V(23, 13, 20, 17, 20, 6), br);
            if (f == 2) { c.Poly(V(1, 20, 8, 22, 5, 12), brD); c.Poly(V(2, 20, 7, 21, 5, 14), br); }
            c.Ellipse(13, 24 + b, 9, 6.6f, brD); c.Ellipse(13, 24.6f + b, 8.2f, 6f, br); c.Ellipse(13, 26 + b, 6, 2.6f, brL);
            c.Poly(V(5, 29 + b, 9, 31 + b, 8, 27 + b), brD); c.Poly(V(21, 29 + b, 17, 31 + b, 18, 27 + b), brD);
            c.Ellipse(9, 24 + b, 3.6f, 3.6f, K(0xFFF0C8)); c.Ellipse(17, 24 + b, 3.6f, 3.6f, K(0xFFF0C8)); c.Rect(8, 23 + b, 2, 2, K(0x1B1633)); c.Rect(16, 23 + b, 2, 2, K(0x1B1633));
            c.Poly(V(11, 22 + b, 15, 22 + b, 13, 19 + b), K(0xE8A33C));
            c.Rect(5, 25 + b, 16, 1, Color.clear); c.Rect(8, 20 + b, 4, 1, K(0x3B405C)); c.Rect(14, 20 + b, 4, 1, K(0x3B405C));   // kính lão
            c.Outline(Ink); r[f] = c.ToSprite(new Vector2(.5f, 0f));
        }
        return r;
    });

    // ======================================================================== CẢNH TOÀN CẢNH KẾT: mảnh đất nhìn từ trên cao (bản xám và bản màu)
    public static Sprite Landscape(bool colored) => Get("land" + colored, () =>
    {
        int W = 192, H = 108; var c = new Px(W, H);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float n1 = Mathf.PerlinNoise(x * .045f + 3f, y * .06f + 1f), n2 = Mathf.PerlinNoise(x * .13f, y * .13f + 7f);
                float river = Mathf.Abs(y - (H * .55f + Mathf.Sin(x * .05f) * 12f + Mathf.Sin(x * .021f) * 10f));
                Color32 col;
                if (river < 4.5f) col = colored ? Mix(K(0x3A86C8), K(0x7FC4F0), n2) : Mix(K(0x4A4E5C), K(0x6A6E7C), n2);
                else if (n1 > .66f) col = colored ? Mix(K(0x2F7A3E), K(0x4FA050), n2) : Mix(K(0x3E4048), K(0x555860), n2);                // rừng
                else if (n1 < .36f) col = colored ? Mix(K(0x9A8A5A), K(0xC8B070), n2) : Mix(K(0x585660), K(0x6E6C76), n2);                // đồi khô / ruộng
                else col = colored ? Mix(K(0x5FA84C), K(0x8CCB62), n2) : Mix(K(0x4A4C54), K(0x666870), n2);
                if (((x / 12 + y / 9) % 2 == 0) && n1 > .4f && n1 < .6f && river > 8) col = colored ? Mix(col, K(0xE8C85A), .35f) : Mix(col, K(0x7A7A82), .3f);   // ô ruộng
                c.Set(x, y, col);
            }
        for (int i = 0; i < 6; i++)
        {   // vài ngôi nhà nhỏ
            int hx = 20 + Hash(i, 1, 21) % 150, hy = 10 + Hash(i, 2, 21) % 40; c.Rect(hx, hy, 6, 4, colored ? K(0xD9B48A) : K(0x7A7A82)); c.Rect(hx - 1, hy + 4, 8, 2, colored ? K(0xC2453C) : K(0x5A5A62));
        }
        return c.ToSprite(new Vector2(.5f, .5f), 12f, FilterMode.Point);
    });

    /// <summary>Dải sương lăn ở rìa cảnh (đốm xám mềm).</summary>
    public static Sprite FogPuff => Get("fog", () =>
    {
        int n = 64; var c = new Px(n, n);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - n / 2f) * (x - n / 2f) + (y - n / 2f) * (y - n / 2f)) / (n / 2f); float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                a *= .6f + .4f * Mathf.PerlinNoise(x * .1f, y * .1f); c.p[y * n + x] = new Color32(210, 214, 232, (byte)(a * 150));
            }
        return c.ToSprite(new Vector2(.5f, .5f), 16f, FilterMode.Bilinear);
    });

    /// <summary>Quầng sáng mềm dùng cho mọi hiệu ứng phát sáng (tô màu bằng SpriteRenderer.color).</summary>
    public static Sprite Glow => Get("glow", () =>
    {
        int n = 64; var c = new Px(n, n);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - n / 2f + .5f) * (x - n / 2f + .5f) + (y - n / 2f + .5f) * (y - n / 2f + .5f)) / (n / 2f); float a = Mathf.Clamp01(1f - d); a *= a; c.p[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        return c.ToSprite(new Vector2(.5f, .5f), 16f, FilterMode.Bilinear);
    });

    /// <summary>Hình tròn đặc có viền mềm 1 px, dùng cho vòng báo hiệu, bóng, hạt.</summary>
    public static Sprite Disc => Get("disc", () =>
    {
        int n = 64; var c = new Px(n, n);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - n / 2f + .5f) * (x - n / 2f + .5f) + (y - n / 2f + .5f) * (y - n / 2f + .5f)); float a = Mathf.Clamp01(n / 2f - d); c.p[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        return c.ToSprite(new Vector2(.5f, .5f), 16f, FilterMode.Bilinear);
    });

    public static Sprite Ring => Get("ring", () =>
    {
        int n = 128; var c = new Px(n, n);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - n / 2f + .5f) * (x - n / 2f + .5f) + (y - n / 2f + .5f) * (y - n / 2f + .5f)) / (n / 2f); float a = Mathf.Clamp01(1f - Mathf.Abs(d - .93f) / .07f); c.p[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        return c.ToSprite(new Vector2(.5f, .5f), 16f, FilterMode.Bilinear);
    });

    /// <summary>Cung chém hình lưỡi liềm (mở về phía +x), dùng cho cận chiến.</summary>
    public static Sprite SlashArc => Get("slasharc", () =>
    {
        int W = 64, H = 64; var c = new Px(W, H);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float dx = x + .5f - 8f, dy = y + .5f - 32f; float r = Mathf.Sqrt(dx * dx + dy * dy); float ang = Mathf.Atan2(dy, dx);
                float outer = 54f, inner = 54f - 20f * (1f - Mathf.Abs(ang) / 1.25f) - 2f; if (Mathf.Abs(ang) > 1.25f) continue;
                if (r <= outer && r >= inner) { float t = (r - inner) / Mathf.Max(1f, outer - inner); float a = Mathf.Clamp01(t * 1.4f) * Mathf.Clamp01(1.4f - Mathf.Abs(ang)); c.p[y * W + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(.25f + a))); }
            }
        return c.ToSprite(new Vector2(8f / W, .5f), 16f, FilterMode.Bilinear);
    });

    /// <summary>Tia nhọn dùng cho đường đạn laze, tia sét, vệt chém.</summary>
    public static Sprite Beam => Get("beam", () =>
    {
        int W = 8, H = 16; var c = new Px(W, H);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { float d = Mathf.Abs(y + .5f - H / 2f) / (H / 2f); float a = Mathf.Clamp01(1f - d); a = a * a; c.p[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255)); }
        return c.ToSprite(new Vector2(0f, .5f), 16f, FilterMode.Bilinear);
    });
    /// <summary>Mặt nạ bóng tối: giữa trong suốt, mép đen đặc. Đi theo người chơi để làm tối tầm nhìn (pha 3 của Cáo, Ảo Cảnh).</summary>
    public static Sprite DarkMask => Get("darkmask", () =>
    {
        int n = 128; var c = new Px(n, n);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - n / 2f + .5f) * (x - n / 2f + .5f) + (y - n / 2f + .5f) * (y - n / 2f + .5f)) / (n / 2f);
                float a = Mathf.Clamp01((d - .18f) / .45f); a = a * a * (3f - 2f * a); c.p[y * n + x] = new Color32(6, 4, 14, (byte)(a * 255));
            }
        return c.ToSprite(new Vector2(.5f, .5f), 16f, FilterMode.Bilinear);
    });
    /// <summary>Rương Câu Đố: 0 đóng, 1 mở (có ánh vàng bên trong).</summary>
    public static Sprite Chest(bool open) => Get("chest" + open, () =>
    {
        var c = new Px(34, 30); Color32 w = K(0x7A4F2E), wh = K(0xA8703F), wl = K(0x4C2F1B), g = K(0xD4A33F), g2 = K(0xF6D57A);
        c.Slab(3, 0, 28, 15, w, wh, wl); c.Rect(3, 0, 28, 3, wl);
        c.Rect(6, 0, 3, 15, g); c.Rect(25, 0, 3, 15, g); c.Rect(6, 14, 3, 1, g2); c.Rect(25, 14, 3, 1, g2);
        if (!open)
        {
            c.Poly(V(3, 15, 31, 15, 30, 22, 26, 25, 8, 25, 4, 22), w); c.Poly(V(4, 16, 30, 16, 29, 21, 25, 24, 9, 24, 5, 21), wh); c.Rect(3, 15, 28, 2, wl);
            c.Rect(6, 15, 3, 9, g); c.Rect(25, 15, 3, 9, g); c.Rect(15, 11, 4, 7, g); c.Rect(16, 13, 2, 3, K(0xC23B4E)); c.Set(16, 15, White);
        }
        else
        {
            c.Rect(5, 15, 24, 2, K(0xFFE9A0)); c.Ellipse(17, 17, 11, 4, K(0xFFD25A)); c.Ellipse(17, 18, 8, 2.6f, K(0xFFF3C0));
            c.Poly(V(4, 22, 30, 22, 28, 29, 6, 29), wl); c.Poly(V(5, 23, 29, 23, 27, 28, 7, 28), w); c.Rect(6, 22, 3, 7, g); c.Rect(25, 22, 3, 7, g);
        }
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, 0f));
    });
    // ======================================================================== VẬT CẢN TRONG PHÒNG
    /// <summary>Thùng gỗ: 0 nguyên vẹn, 1 nứt (khi còn dưới nửa máu).</summary>
    public static Sprite CrateProp(bool damaged) => Get("crateprop" + damaged, () =>
    {
        var c = new Px(30, 30); Color32 w = K(0xB07A44), wh = K(0xD9A468), wl = K(0x6E4624), band = K(0x8A5A30);
        c.Slab(1, 1, 28, 28, w, wh, wl); c.Rect(1, 14, 28, 2, band); c.Rect(1, 1, 3, 28, band); c.Rect(26, 1, 3, 28, band); c.Rect(1, 1, 28, 2, wl);
        for (int i = 0; i < 4; i++) { c.Set(2, 4 + i * 7, K(0x3A2410)); c.Set(27, 4 + i * 7, K(0x3A2410)); }
        c.Line(4, 4, 25, 25, wl); c.Line(4, 25, 25, 4, wl); c.Line(5, 4, 25, 24, wh);
        if (damaged) { c.Line(8, 26, 13, 17, Ink); c.Line(13, 17, 11, 11, Ink); c.Line(21, 3, 17, 12, Ink); }
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, 0f));
    });

    /// <summary>Thùng nổ (đỏ, ký hiệu lửa) hoặc thùng độc (xanh lá, đầu lâu). Khung 1 nhấp nháy khi sắp nổ.</summary>
    public static Sprite Barrel(bool poison, bool warn) => Get("barrel" + poison + warn, () =>
    {
        var c = new Px(24, 30); Color32 b = poison ? K(0x3F9A3A) : K(0xC23B2E), bh = poison ? K(0x7BD65A) : K(0xF0705C), bl = poison ? K(0x1F5A22) : K(0x7A1F1A); Color32 band = K(0x2B2D44), gold = K(0xF6D57A);
        if (warn) { b = Lite(b, .35f); bh = Lite(bh, .5f); }
        c.Ellipse(12, 26, 11, 4, bl); c.Rect(1, 4, 22, 22, b); c.Rect(1, 4, 4, 22, bh); c.Rect(19, 4, 4, 22, bl); c.Ellipse(12, 4, 11, 4, bl); c.Ellipse(12, 26, 10, 3.4f, bh);
        c.Rect(1, 8, 22, 2, band); c.Rect(1, 20, 22, 2, band);
        if (poison)
        {   // đầu lâu
            c.Ellipse(12, 15, 4.4f, 4f, K(0xEFE8D8)); c.Rect(9, 10, 6, 3, K(0xEFE8D8)); c.Rect(9, 14, 2, 3, band); c.Rect(13, 14, 2, 3, band); c.Rect(11, 11, 1, 2, band); c.Rect(13, 11, 1, 2, band);
            c.Rect(10, 24, 4, 2, K(0x9BE05A));
        }
        else
        {   // biểu tượng lửa vàng và ngòi
            c.Poly(V(12, 21, 17, 15, 14, 11, 10, 11, 7, 15), K(0xFFD25A)); c.Poly(V(12, 18, 15, 15, 13, 12, 11, 12, 9, 15), K(0xFF8A2A)); c.Rect(11, 26, 2, 3, K(0x2B2D44)); c.Rect(12, 29, 1, 1, warn ? K(0xFFF3A0) : K(0xFFA14D));
        }
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, 0f));
    });

    /// <summary>Khối đá không thể phá, dựng thành tường chắn trong sân lớn.</summary>
    public static Sprite StoneBlock(int biome) => Get("block" + biome, () =>
    {
        var c = new Px(30, 30); Color32 a = biome == 1 ? K(0x4F6A5A) : biome == 2 ? K(0x59638A) : K(0x6A6688), ah = Lite(a, .25f), al = Shade(a, .45f);
        c.Slab(1, 1, 28, 24, a, ah, al); c.Rect(1, 25, 28, 4, ah); c.Rect(2, 26, 26, 2, Lite(ah, .2f)); c.Rect(1, 1, 28, 3, al);
        c.Line(9, 5, 12, 20, al); c.Line(20, 6, 17, 22, al); c.Rect(6, 13, 6, 1, al); c.Rect(17, 9, 7, 1, al); if (biome == 1) { c.Rect(3, 22, 8, 3, K(0x3F8A44)); c.Rect(18, 24, 8, 3, K(0x56B25A)); }
        if (biome == 2) { c.Rect(12, 12, 6, 2, K(0x4DE0FF)); }
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, 0f));
    });
}
