using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Toàn bộ hình ảnh (hiệp sĩ, rồng, đạn lửa, sàn hầm ngục...) được vẽ bằng code theo kiểu pixel art
/// nên KHÔNG cần file PNG. Muốn dùng art thật: thay Sprite trong PlayerKnight / DragonBoss.
/// </summary>
public static class ProcSprites
{
    public const float PPU = 16f;
    public static Color32 Hex(string h) { ColorUtility.TryParseHtmlString(h, out Color c); return c; }

    sealed class Cv
    {
        public readonly int w, h; public readonly Color32[] p;
        public Cv(int w, int h) { this.w = w; this.h = h; p = new Color32[w * h]; }
        public void Set(int x, int y, Color32 c) { if (x < 0 || y < 0 || x >= w || y >= h) return; p[y * w + x] = c; }
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
        public void Line(Vector2 a, Vector2 b, Color32 c)
        {
            int n = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y))) * 2 + 1;
            for (int i = 0; i <= n; i++) { var q = Vector2.Lerp(a, b, i / (float)n); Set((int)q.x, (int)q.y, c); Set((int)q.x + 1, (int)q.y, c); }
        }
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
        public Sprite ToSprite(Vector2 pivot, FilterMode fm = FilterMode.Point, float ppu = PPU, Vector4 border = default(Vector4))
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = fm, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            t.SetPixels32(p); t.Apply();
            var s = Sprite.Create(t, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect, border);
            s.hideFlags = HideFlags.HideAndDontSave; return s;
        }
    }

    static readonly Color32 Ink = new Color32(20, 16, 31, 255);
    static readonly Color32 White = new Color32(255, 255, 255, 255);

    // ---------- Hiệp sĩ ----------
    static Sprite[] _kIdle, _kWalk; static Sprite _sword;
    public static Sprite[] KnightIdle => _kIdle ??= new[] { Knight(0, 0), Knight(0, 1) };
    public static Sprite[] KnightWalk => _kWalk ??= new[] { Knight(-2, 1), Knight(0, 0), Knight(2, 1), Knight(0, 0) };

    static Sprite Knight(int leg, int bob)
    {
        var c = new Cv(28, 38); int ox = 4, oy = 2, b = bob;
        Color32 red = Hex("#c0392b"), dred = Hex("#8e2a20"), steel = Hex("#aeb9d6"), dsteel = Hex("#7d8bb0"), blue = Hex("#5a78c8"),
                dblue = Hex("#3b4a7a"), gold = Hex("#f5c542"), dark = Hex("#1b1b2e"), boot = Hex("#2b2f4a"), helm = Hex("#c8d2ea");
        c.Rect(ox + 5 + leg, oy, 4, 7, dblue); c.Rect(ox + 11 - leg, oy, 4, 7, dblue);                     // chân
        c.Rect(ox + 5 + leg, oy, 4, 2, boot); c.Rect(ox + 11 - leg, oy, 4, 2, boot);
        c.Rect(ox + 4, oy + 7 + b, 12, 11, steel); c.Rect(ox + 4, oy + 7 + b, 12, 2, dsteel);              // thân
        c.Rect(ox + 4, oy + 11 + b, 12, 2, blue); c.Rect(ox + 9, oy + 11 + b, 2, 2, gold);
        c.Rect(ox + 2, oy + 14 + b, 4, 4, dsteel); c.Rect(ox + 14, oy + 14 + b, 4, 4, dsteel);              // vai
        c.Rect(ox + 5, oy + 18 + b, 11, 10, helm); c.Rect(ox + 6, oy + 27 + b, 9, 1, White);                // mũ
        c.Rect(ox + 10, oy + 21 + b, 6, 2, dark);                                                           // khe nhìn
        c.Rect(ox + 8, oy + 28 + b, 4, 4, red); c.Rect(ox + 11, oy + 28 + b, 3, 2, dred);                  // chùm lông
        c.Outline(Ink);
        return c.ToSprite(new Vector2(.5f, 2f / 38f));
    }

    public static Sprite Sword
    {
        get
        {
            if (_sword != null) return _sword;
            var c = new Cv(26, 9);
            c.Rect(1, 3, 4, 3, Hex("#7a4f2e")); c.Rect(5, 1, 2, 7, Hex("#f5c542"));
            c.Rect(7, 3, 16, 3, Hex("#c9d6ff")); c.Rect(7, 4, 16, 1, White); c.Rect(23, 4, 1, 1, White);
            c.Outline(Ink); return _sword = c.ToSprite(new Vector2(.2f, .5f));
        }
    }


    // ---------- Đệ hiệp sĩ ----------
    static Sprite[] _ally;
    public static Sprite[] Ally => _ally ??= new[] { AllyFrame(0), AllyFrame(1) };

    static Sprite AllyFrame(int f)
    {
        var c = new Cv(22, 26); int b = f;
        Color32 steel = Hex("#cfe1f5"), dsteel = Hex("#8fb0d6"), green = Hex("#4cc38a"), dgreen = Hex("#2e8f64"), dark = Hex("#1b1b2e"), gold = Hex("#f5c542");
        c.Rect(5, 2 + b, 10, 2, dsteel);                                                          // đế lơ lửng
        c.Rect(4, 4 + b, 12, 7, steel); c.Rect(4, 4 + b, 12, 2, dsteel); c.Rect(9, 7 + b, 2, 2, gold);   // thân
        c.Rect(3, 11 + b, 14, 9, steel); c.Rect(3, 11 + b, 14, 2, dsteel);                         // mũ (đầu to kiểu chibi)
        c.Rect(10, 14 + b, 7, 2, dark);                                                           // khe nhìn
        c.Rect(8, 20 + b, 4, 3, green); c.Rect(11, 20 + b, 3, 1, dgreen);                         // chùm lông xanh
        c.Rect(17, 6 + b, 2, 7, Hex("#c9d6ff")); c.Rect(16, 5 + b, 4, 1, gold);                   // kiếm nhỏ
        c.Outline(Ink);
        return c.ToSprite(new Vector2(.5f, .5f));
    }

    // ---------- Gai băng (hàng băng của Kiếm Băng) ----------
    static Sprite _ice;
    public static Sprite IceSpike
    {
        get
        {
            if (_ice != null) return _ice;
            var c = new Cv(14, 24);
            c.Poly(new[] { new Vector2(2, 1), new Vector2(12, 1), new Vector2(10, 10), new Vector2(7, 22), new Vector2(4, 10) }, Hex("#8fe6ff"));
            c.Poly(new[] { new Vector2(7, 1), new Vector2(12, 1), new Vector2(10, 10), new Vector2(7, 22) }, Hex("#5fc8f0"));
            c.Rect(5, 3, 1, 8, White); c.Outline(Ink);
            return _ice = c.ToSprite(new Vector2(.5f, 1f / 24f));
        }
    }

    // ---------- Icon buff (pixel art 24x24, vẽ bằng code) ----------
    static readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();
    public static Sprite BuffIcon(string id)
    {
        if (_icons.TryGetValue(id, out var s) && s != null) return s;
        s = DrawIcon(id); _icons[id] = s; return s;
    }

    static Sprite DrawIcon(string id)
    {
        if (id == "ally") return Ally[0];
        var c = new Cv(24, 24);
        // toạ độ vẽ 0..19, tự dịch +2 để chừa chỗ cho viền
        void R(int x, int y, int w, int h, string col) => c.Rect(x + 2, y + 2, w, h, Hex(col));
        void E(float x, float y, float rx, float ry, string col) => c.Ellipse(x + 2f, y + 2f, rx, ry, Hex(col));
        void P(string col, params Vector2[] v) { for (int i = 0; i < v.Length; i++) v[i] += new Vector2(2f, 2f); c.Poly(v, Hex(col)); }
        void SwordIcon(string blade, string light)
        {
            R(8, 0, 4, 2, "#f5c542"); R(9, 2, 2, 3, "#7a4f2e"); R(5, 5, 10, 2, "#f5c542");   // chuôi, cán, đốc
            R(8, 7, 4, 10, blade); P(blade, new Vector2(8, 17), new Vector2(12, 17), new Vector2(10, 20)); R(9, 8, 1, 9, light);
        }
        switch (id)
        {
            case "fire":
                SwordIcon("#e5484d", "#ffb0a0");
                E(5.5f, 12f, 2.2f, 3.4f, "#ff7a2e"); E(14.5f, 13f, 2.2f, 3.6f, "#ff7a2e");
                E(5.5f, 11.5f, 1f, 1.8f, "#ffe14d"); E(14.5f, 12.5f, 1f, 2f, "#ffe14d");
                P("#ffb347", new Vector2(8.5f, 15), new Vector2(10, 19), new Vector2(11.5f, 15)); break;
            case "ice":
                SwordIcon("#8fe6ff", "#ffffff");
                P("#c8f4ff", new Vector2(3, 10), new Vector2(5, 15), new Vector2(7, 10)); P("#c8f4ff", new Vector2(13, 11), new Vector2(15, 17), new Vector2(17, 11));
                R(2, 16, 1, 1, "#ffffff"); R(17, 7, 1, 1, "#ffffff"); R(4, 18, 1, 1, "#ffffff"); break;
            case "haste":
                E(9f, 10f, 8.5f, 8.5f, "#ffd84d"); c.Ellipse(14.5f, 12f, 7.5f, 7.5f, default(Color32));
                R(14, 6, 5, 1, "#ffffff"); R(15, 10, 4, 1, "#ffe99a"); R(14, 14, 5, 1, "#ffffff"); break;
            case "heart":
                E(6.2f, 13f, 4.2f, 4.2f, "#e5484d"); E(13.8f, 13f, 4.2f, 4.2f, "#e5484d");
                P("#e5484d", new Vector2(2.2f, 12), new Vector2(17.8f, 12), new Vector2(10, 2)); R(4, 14, 2, 2, "#ff9aa8"); R(6, 16, 1, 1, "#ff9aa8"); break;
            case "boots":
                R(6, 9, 6, 9, "#4aa3c7"); R(6, 4, 12, 6, "#4aa3c7"); R(6, 3, 12, 2, "#2b4a66"); R(6, 16, 6, 2, "#dff4ff"); R(14, 7, 3, 2, "#8fd6f0");
                P("#ffffff", new Vector2(5, 13), new Vector2(0, 18), new Vector2(1, 13), new Vector2(0, 10), new Vector2(4, 9)); break;
            case "vamp":
                P("#b0246b", new Vector2(10, 19), new Vector2(4.2f, 9), new Vector2(15.8f, 9)); E(10f, 8f, 6f, 6f, "#b0246b"); R(6, 9, 2, 4, "#ff8fc0");
                P("#ffffff", new Vector2(6.5f, 4), new Vector2(9, 4), new Vector2(7.5f, 0)); P("#ffffff", new Vector2(11, 4), new Vector2(13.5f, 4), new Vector2(12.5f, 0)); break;
            case "shield":
                P("#6fe3ff", new Vector2(2, 17), new Vector2(18, 17), new Vector2(18, 9), new Vector2(10, 0), new Vector2(2, 9));
                P("#2f9fd4", new Vector2(4, 15), new Vector2(16, 15), new Vector2(16, 10), new Vector2(10, 3), new Vector2(4, 10));
                R(9, 5, 2, 10, "#dff8ff"); R(5, 10, 10, 2, "#dff8ff"); break;
            case "crit":
                P("#ffd84d", new Vector2(10, 19), new Vector2(12, 12), new Vector2(19, 10), new Vector2(12, 8), new Vector2(10, 1), new Vector2(8, 8), new Vector2(1, 10), new Vector2(8, 12));
                E(10f, 10f, 2f, 2f, "#ffffff"); break;
            case "long":
                R(9, 5, 2, 13, "#9fb4ff"); P("#9fb4ff", new Vector2(9, 18), new Vector2(11, 18), new Vector2(10, 19)); R(10, 6, 1, 12, "#ffffff");
                R(6, 3, 8, 2, "#f5c542"); R(9, 1, 2, 2, "#7a4f2e");
                P("#c9d6ff", new Vector2(15, 19), new Vector2(17.5f, 14), new Vector2(12.5f, 14)); P("#c9d6ff", new Vector2(15, 0), new Vector2(17.5f, 5), new Vector2(12.5f, 5)); R(15, 5, 1, 9, "#c9d6ff"); break;
            case "thunder":
                P("#ffe94d", new Vector2(12, 19), new Vector2(4, 8), new Vector2(9, 8), new Vector2(6, 1), new Vector2(16, 12), new Vector2(11, 12), new Vector2(15, 19));
                R(11, 15, 1, 3, "#ffffff"); break;
            case "regen":
                R(8, 2, 4, 16, "#4cd47a"); R(2, 8, 16, 4, "#4cd47a"); R(9, 3, 1, 14, "#a6f5c0"); R(3, 9, 14, 1, "#a6f5c0"); R(9, 9, 2, 2, "#ffffff"); break;
            case "reflect":
                R(3, 5, 3, 12, "#7fe3ff"); R(3, 14, 14, 3, "#7fe3ff"); R(14, 6, 3, 9, "#7fe3ff"); R(4, 15, 12, 1, "#e8fbff");
                P("#7fe3ff", new Vector2(11.5f, 8), new Vector2(19.5f, 8), new Vector2(15.5f, 1));
                E(4.5f, 3.2f, 3.2f, 3.2f, "#ff7a2e"); E(4.5f, 3.2f, 1.6f, 1.6f, "#ffe14d"); break;
            default:
                E(10f, 10f, 8f, 8f, "#c9d6ff"); break;
        }
        c.Outline(Ink);
        return c.ToSprite(Mid);
    }

    // ---------- Rồng ----------
    static Sprite[,] _dr, _drW;
    public static Sprite DragonFrame(int wing, bool mouth, bool white)
    {
        if (_dr == null) { _dr = new Sprite[3, 2]; _drW = new Sprite[3, 2]; }
        var arr = white ? _drW : _dr; int m = mouth ? 1 : 0;
        return arr[wing, m] ??= Dragon(wing, mouth, white);
    }

    static Sprite Dragon(int wing, bool mouth, bool white)
    {
        var c = new Cv(172, 150); const float OX = 86f, OY = 83f;
        Color32 K(string hx) => white ? White : Hex(hx);
        Vector2 V(float x, float y) => new Vector2(OX + x, OY - y);
        void R(float x, float yTop, int w, int h, string col) => c.Rect(Mathf.RoundToInt(OX + x), Mathf.RoundToInt(OY - yTop) - h, w, h, K(col));
        void E(float x, float y, float rx, float ry, string col) => c.Ellipse(OX + x, OY - y, rx, ry, K(col));
        float fp = wing == 0 ? 1f : (wing == 1 ? 0f : -1f);

        for (int si = 0; si < 2; si++)   // cánh
        {
            float s = si == 0 ? -1f : 1f;
            c.Poly(new[] { V(s * 12, -8), V(s * 52, -34 + fp * 14), V(s * 76, -10 + fp * 20), V(s * 58, -6 + fp * 10), V(s * 62, 12 + fp * 8), V(s * 40, 4), V(s * 30, 18), V(s * 14, 12) }, K("#5b2a86"));
            c.Line(V(s * 12, -8), V(s * 62, 12 + fp * 8), K("#8e4fc4")); c.Line(V(s * 12, -8), V(s * 58, -6 + fp * 10), K("#8e4fc4"));
        }
        E(0, 6, 27, 31, "#b3262e"); E(0, 11, 16, 24, "#f0c36a");                                                                                         // thân + bụng
        for (int i = -2; i < 4; i++) R(-13, 10 + i * 8, 26, 1, "#c99a3c");
        R(-35, 2, 11, 7, "#9c2029"); R(24, 2, 11, 7, "#9c2029");                                                                                         // tay
        for (int i = 0; i < 3; i++) { R(-36 + i * 3, 9, 2, 4, "#ffffff"); R(28 + i * 3, 9, 2, 4, "#ffffff"); }                                          // vuốt
        for (int i = -3; i <= 3; i++) c.Poly(new[] { V(i * 5 - 3, -44), V(i * 5, -52 + Mathf.Abs(i)), V(i * 5 + 3, -44) }, K("#e8e0d0"));              // gai
        E(0, -28, 17, 17, "#c42d38"); R(-12, -26, 24, 9, "#d9434e");                                                                                     // đầu
        int o = mouth ? 14 : 2;
        R(-10, -18, 20, o, "#2a0a10");
        if (mouth) { R(-7, -16, 14, o - 3, "#ff9b3a"); R(-4, -15, 8, o - 6, "#ffe14d"); }
        R(-11, -18 + o, 22, 5, "#a52a33"); R(-9, -18, 3, 3, "#ffffff"); R(6, -18, 3, 3, "#ffffff");
        R(-11, -37, 7, 5, "#ffe14d"); R(4, -37, 7, 5, "#ffe14d"); R(-7, -36, 3, 4, "#000000"); R(4, -36, 3, 4, "#000000");
        R(-12, -40, 9, 2, "#4a0d14"); R(3, -40, 9, 2, "#4a0d14");
        for (int si = 0; si < 2; si++) { float s = si == 0 ? -1f : 1f; c.Poly(new[] { V(s * 12, -40), V(s * 24, -62), V(s * 6, -44) }, K("#e8e0d0")); }  // sừng
        c.Outline(white ? White : Ink);
        return c.ToSprite(new Vector2(OX / 172f, OY / 150f));
    }

    // ---------- Quỷ nhỏ (đệ của rồng) ----------
    static Sprite[] _imp;
    public static Sprite[] Imp => _imp ??= new[] { ImpFrame(0), ImpFrame(1) };

    static Sprite ImpFrame(int f)
    {
        var c = new Cv(28, 26); int wy = f == 0 ? 4 : -1;
        Color32 body = Hex("#7b2fa8"), dbody = Hex("#5b2a86"), belly = Hex("#c78bf0"), wing = Hex("#3b1a5c"), eye = Hex("#ffe14d"), horn = Hex("#e8e0d0"), dark = Hex("#2a0a10");
        c.Poly(new[] { new Vector2(9, 13), new Vector2(1, 17 + wy), new Vector2(3, 11 + wy), new Vector2(9, 9) }, wing);       // cánh trái
        c.Poly(new[] { new Vector2(19, 13), new Vector2(27, 17 + wy), new Vector2(25, 11 + wy), new Vector2(19, 9) }, wing);   // cánh phải
        c.Rect(10, 2, 3, 4, dbody); c.Rect(15, 2, 3, 4, dbody);                                                               // chân
        c.Ellipse(14, 11, 7.5f, 8f, body); c.Ellipse(14, 9, 4.5f, 5.5f, belly);                                              // thân + bụng
        c.Poly(new[] { new Vector2(8, 18), new Vector2(7, 25), new Vector2(12, 20) }, horn);                                  // sừng
        c.Poly(new[] { new Vector2(20, 18), new Vector2(21, 25), new Vector2(16, 20) }, horn);
        c.Rect(9, 14, 4, 3, eye); c.Rect(15, 14, 4, 3, eye); c.Rect(11, 15, 1, 2, dark); c.Rect(16, 15, 1, 2, dark);        // mắt
        c.Rect(11, 11, 6, 1, dark); c.Rect(12, 10, 1, 1, White); c.Rect(15, 10, 1, 1, White);                                // miệng + răng
        c.Outline(Ink);
        return c.ToSprite(new Vector2(.5f, .5f));
    }

    // ---------- Áo choàng / khung UI / thanh máu ----------
    static Sprite _cape, _frame, _barGrad;
    public static Sprite Cape
    {
        get
        {
            if (_cape != null) return _cape;
            var c = new Cv(9, 20); c.Rect(2, 1, 5, 17, Hex("#c0392b")); c.Rect(2, 1, 5, 2, Hex("#8e2a20")); c.Rect(2, 14, 5, 4, Hex("#d4493a"));
            c.Rect(3, 3, 1, 12, Hex("#a8321f")); c.Outline(Ink); return _cape = c.ToSprite(new Vector2(.5f, .9f));
        }
    }

    /// <summary>Khung pixel 9-slice (viền đen, góc cắt, vát sáng/tối). Dùng với Image.Type.Sliced và tô màu bằng Image.color.</summary>
    public static Sprite Frame
    {
        get
        {
            if (_frame != null) return _frame;
            var c = new Cv(12, 12);
            c.Rect(0, 0, 12, 12, Ink); c.Rect(1, 1, 10, 10, new Color32(165, 165, 165, 255)); c.Rect(1, 2, 9, 9, White);
            c.Set(0, 0, default(Color32)); c.Set(11, 0, default(Color32)); c.Set(0, 11, default(Color32)); c.Set(11, 11, default(Color32));
            return _frame = c.ToSprite(Mid, FilterMode.Point, 50f, new Vector4(4, 4, 4, 4));
        }
    }

    /// <summary>Dải gradient dọc (sáng ở trên, tối ở dưới) để thanh máu có chiều sâu.</summary>
    public static Sprite BarGrad
    {
        get
        {
            if (_barGrad != null) return _barGrad;
            var c = new Cv(4, 16);
            for (int y = 0; y < 16; y++) { byte l = (byte)(y >= 14 ? 255 : 150 + y * 7); for (int x = 0; x < 4; x++) c.Set(x, y, new Color32(l, l, l, 255)); }
            return _barGrad = c.ToSprite(Mid, FilterMode.Bilinear);
        }
    }

    // ---------- Hiệu ứng / môi trường ----------
    static Sprite _fire, _circle, _ring, _glow, _pixel, _slash, _floor, _wall, _torch, _vig;
    static readonly Vector2 Mid = new Vector2(.5f, .5f);

    public static Sprite Pixel => _pixel ??= Fill(4, 4, (x, y) => White);
    public static Sprite Circle => _circle ??= Fill(64, 64, (x, y) => Dist(x, y) <= 31.5f ? White : default(Color32));
    public static Sprite Ring => _ring ??= Fill(64, 64, (x, y) => { float d = Dist(x, y); return d <= 31.5f && d >= 28.5f ? White : default(Color32); });
    public static Sprite Glow => _glow ??= Fill(64, 64, (x, y) => { float d = Mathf.Clamp01(1f - Dist(x, y) / 32f); return new Color32(255, 255, 255, (byte)(d * d * 255)); }, FilterMode.Bilinear);
    public static Sprite Slash => _slash ??= Fill(64, 64, (x, y) =>
    {
        float dx = x - 31.5f, dy = y - 31.5f, r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Abs(Mathf.Atan2(dy, dx));
        if (a > 1.15f || r > 30f) return default(Color32);
        float inner = 30f - 13f * Mathf.Pow(1f - a / 1.15f, .7f);
        return r >= inner ? White : default(Color32);
    });
    public static Sprite Vignette => _vig ??= Fill(64, 64, (x, y) =>
    {
        float d = Dist(x, y) / 32f, a = Mathf.Clamp01((d - .6f) / .8f); return new Color32(0, 0, 0, (byte)(a * a * .65f * 255));
    }, FilterMode.Bilinear);

    public static Sprite Fireball
    {
        get
        {
            if (_fire != null) return _fire;
            var c = new Cv(16, 16);
            c.Ellipse(8, 8, 6.5f, 6.5f, Hex("#e5484d")); c.Ellipse(8, 8, 4.8f, 4.8f, Hex("#ff9b3a")); c.Ellipse(8, 8, 2.6f, 2.6f, Hex("#fff3a0"));
            return _fire = c.ToSprite(Mid);
        }
    }

    public static Sprite FloorTile
    {
        get
        {
            if (_floor != null) return _floor;
            var c = new Cv(40, 40); var r = new System.Random(7);
            for (int ty = 0; ty < 2; ty++) for (int tx = 0; tx < 2; tx++)
                {
                    c.Rect(tx * 20, ty * 20, 20, 20, (tx + ty) % 2 == 0 ? Hex("#2a2540") : Hex("#262138"));
                    c.Rect(tx * 20, ty * 20, 20, 1, Hex("#1b1730")); c.Rect(tx * 20, ty * 20 + 19, 20, 1, Hex("#322b4d"));
                    if (r.NextDouble() < .6) c.Rect(tx * 20 + r.Next(2, 12), ty * 20 + r.Next(2, 12), 4, 2, Hex("#1e1a30"));
                }
            return _floor = c.ToSprite(Mid);
        }
    }

    public static Sprite WallTile
    {
        get
        {
            if (_wall != null) return _wall;
            var c = new Cv(48, 26); c.Rect(0, 0, 48, 26, Hex("#161225"));
            for (int row = 0; row < 2; row++)
            {
                int y = row * 13; int start = row == 0 ? -12 : 0;
                for (int x = start; x < 48; x += 24) { c.Rect(x + 1, y + 1, 22, 11, Hex("#211b36")); c.Rect(x + 1, y + 10, 22, 2, Hex("#2c2447")); }
            }
            return _wall = c.ToSprite(Mid);
        }
    }

    public static Sprite Torch
    {
        get
        {
            if (_torch != null) return _torch;
            var c = new Cv(8, 16); c.Rect(3, 0, 2, 13, Hex("#5a3a1e")); c.Rect(1, 10, 6, 2, Hex("#7a4f2e")); c.Outline(Ink);
            return _torch = c.ToSprite(Mid);
        }
    }

    static float Dist(int x, int y) { float dx = x - 31.5f, dy = y - 31.5f; return Mathf.Sqrt(dx * dx + dy * dy); }
    static Sprite Fill(int w, int h, System.Func<int, int, Color32> f, FilterMode fm = FilterMode.Point)
    {
        var c = new Cv(w, h); for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) c.p[y * w + x] = f(x, y);
        return c.ToSprite(Mid, fm);
    }
}
