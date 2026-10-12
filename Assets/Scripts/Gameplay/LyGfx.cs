using System;
using UnityEngine;

/// <summary>
/// Sprite dark fantasy cho game Vật lí, VẼ BẰNG CODE (không cần thêm ảnh vào project):
///   Tile (ô đá đi được), SolvedTile (ô đã giải, có dấu ✓ xanh), Rock (đá chắn nứt rỉ máu),
///   Rune (vòng rune vàng cho ô câu hỏi), Chest (rương cổ ở đích), Knight (nhân vật hiệp sĩ).
/// Mỗi sprite chỉ tạo 1 lần rồi dùng chung cho mọi ô. Màu lấy từ GameTheme.
/// </summary>
public static class LyGfx
{
    static Sprite _tile, _solved, _rock, _boulder, _rune, _knightIdle, _princess, _princessCheer, _heart, _arrow;
    static Sprite[] _walk, _cheer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _tile = _solved = _rock = _boulder = _rune = _knightIdle = _princess = _princessCheer = _heart = _arrow = null;
        _walk = _cheer = null;
    }

    public static Sprite Tile { get { if (_tile == null) _tile = Make(128, 128, PaintTile); return _tile; } }
    public static Sprite SolvedTile { get { if (_solved == null) _solved = Make(128, 128, PaintSolved); return _solved; } }
    public static Sprite Rock { get { if (_rock == null) _rock = Make(128, 128, PaintRock); return _rock; } }
    /// <summary>Tảng đá đứng riêng (nền trong suốt) dùng cho hoạt ảnh đá rơi.</summary>
    public static Sprite Boulder { get { if (_boulder == null) _boulder = Make(128, 128, r => DrawBoulder(r)); return _boulder; } }
    public static Sprite Rune { get { if (_rune == null) _rune = Make(128, 128, PaintRune); return _rune; } }
    public static Sprite Princess { get { if (_princess == null) _princess = Make(128, 128, r => PaintPrincess(r, false)); return _princess; } }
    public static Sprite PrincessCheer { get { if (_princessCheer == null) _princessCheer = Make(128, 128, r => PaintPrincess(r, true)); return _princessCheer; } }
    public static Sprite Heart { get { if (_heart == null) _heart = Make(64, 64, PaintHeart); return _heart; } }
    /// <summary>Mũi tên hướng LÊN (xoay Image để thành xuống/trái/phải) cho nút điều khiển.</summary>
    public static Sprite Arrow { get { if (_arrow == null) _arrow = Make(64, 64, PaintArrow); return _arrow; } }

    public static Sprite KnightIdle { get { if (_knightIdle == null) _knightIdle = Make(112, 144, r => PaintKnight(r, PoseIdle())); return _knightIdle; } }
    /// <summary>4 khung hình đi bộ (chu kỳ lặp).</summary>
    public static Sprite[] KnightWalk
    {
        get
        {
            if (_walk == null)
            {
                _walk = new Sprite[4];
                for (int i = 0; i < 4; i++) { int k = i; _walk[i] = Make(112, 144, r => PaintKnight(r, PoseWalk(k))); }
            }
            return _walk;
        }
    }
    /// <summary>2 khung hình ăn mừng: [0] cười tít mắt, [1] mắt long lanh co chân nhảy.</summary>
    public static Sprite[] KnightCheer
    {
        get
        {
            if (_cheer == null)
            {
                _cheer = new Sprite[2];
                for (int i = 0; i < 2; i++) { int k = i; _cheer[i] = Make(112, 144, r => PaintKnight(r, PoseCheer(k))); }
            }
            return _cheer;
        }
    }

    // =====================================================================================
    //  Bộ vẽ: raster nhỏ dùng khoảng cách có dấu (SDF), toạ độ pixel, gốc ở góc DƯỚI-TRÁI (y hướng lên)
    // =====================================================================================
    struct Col
    {
        public float r, g, b, a;
        public Col(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Col From(Color c) { return new Col(c.r, c.g, c.b, c.a); }
        public Col A(float alpha) { return new Col(r, g, b, alpha); }
        public Col Mul(float k) { return new Col(r * k, g * k, b * k, a); }
        public static Col Lerp(Col x, Col y, float t)
        {
            t = Mathf.Clamp01(t);
            return new Col(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);
        }
    }

    delegate float Sdf(float x, float y);
    delegate Col Shade(float x, float y);

    sealed class Raster
    {
        public readonly int W, H;
        readonly float[] px;   // r,g,b,a nhân sẵn alpha

        public Raster(int w, int h) { W = w; H = h; px = new float[w * h * 4]; }

        void Put(int i, Col c, float cov)
        {
            float sa = c.a * cov;
            if (sa <= 0f) return;
            int o = i * 4; float ia = 1f - sa;
            px[o] = c.r * sa + px[o] * ia;
            px[o + 1] = c.g * sa + px[o + 1] * ia;
            px[o + 2] = c.b * sa + px[o + 2] * ia;
            px[o + 3] = sa + px[o + 3] * ia;
        }

        public void Fill(Sdf f, Shade s)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float fx = x + .5f, fy = y + .5f;
                    float cov = Mathf.Clamp01(.5f - f(fx, fy));
                    if (cov <= 0f) continue;
                    Put(y * W + x, s(fx, fy), cov);
                }
        }
        public void Fill(Sdf f, Col c) { Fill(f, (x, y) => c); }

        public void Stroke(Sdf f, float w, Shade s) { Fill((x, y) => Mathf.Abs(f(x, y)) - w * .5f, s); }
        public void Stroke(Sdf f, float w, Col c) { Stroke(f, w, (x, y) => c); }

        /// <summary>Quầng sáng tròn mờ dần ra rìa.</summary>
        public void Glow(float cx, float cy, float rad, Col c)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float t = Len(x + .5f - cx, y + .5f - cy) / rad;
                    if (t >= 1f) continue;
                    float k = 1f - t;
                    Put(y * W + x, c, k * k);
                }
        }

        public Color32[] ToColor32()
        {
            var o = new Color32[W * H];
            for (int i = 0; i < o.Length; i++)
            {
                float a = px[i * 4 + 3];
                if (a <= 0.001f) { o[i] = new Color32(0, 0, 0, 0); continue; }
                o[i] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(px[i * 4] / a * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(px[i * 4 + 1] / a * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(px[i * 4 + 2] / a * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
            }
            return o;
        }
    }

    static Sprite Make(int w, int h, Action<Raster> paint)
    {
        var r = new Raster(w, h);
        paint(r);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.DontSave;
        tex.SetPixels32(r.ToColor32());
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f);
    }

    // ---------------------------------------------------------------- hình cơ bản (SDF, đơn vị pixel)
    static float Len(float x, float y) { return Mathf.Sqrt(x * x + y * y); }

    static float Box(float x, float y, float cx, float cy, float hw, float hh, float rad)
    {
        float qx = Mathf.Abs(x - cx) - (hw - rad), qy = Mathf.Abs(y - cy) - (hh - rad);
        return Len(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - rad;
    }

    static float Circ(float x, float y, float cx, float cy, float rad) { return Len(x - cx, y - cy) - rad; }

    // elip xấp xỉ (đủ cho bóng đổ)
    static float Ell(float x, float y, float cx, float cy, float rx, float ry)
    {
        return (Len((x - cx) / rx, (y - cy) / ry) - 1f) * Mathf.Min(rx, ry);
    }

    static float Seg(float x, float y, float ax, float ay, float bx, float by)
    {
        float px = x - ax, py = y - ay, ex = bx - ax, ey = by - ay;
        float h = Mathf.Clamp01((px * ex + py * ey) / (ex * ex + ey * ey));
        return Len(px - ex * h, py - ey * h);
    }

    /// <summary>Đường gấp khúc: v = x0,y0,x1,y1,... (khoảng cách tới đường, dùng với Stroke).</summary>
    static float Line(float x, float y, float[] v)
    {
        float d = 1e9f;
        for (int i = 0; i + 3 < v.Length; i += 2) d = Mathf.Min(d, Seg(x, y, v[i], v[i + 1], v[i + 2], v[i + 3]));
        return d;
    }

    /// <summary>Đa giác lồi/lõm: v = x0,y0,x1,y1,... SDF có dấu.</summary>
    static float Poly(float x, float y, float[] v)
    {
        int n = v.Length / 2;
        float dx0 = x - v[0], dy0 = y - v[1];
        float d = dx0 * dx0 + dy0 * dy0, s = 1f;
        for (int i = 0, j = n - 1; i < n; j = i, i++)
        {
            float vix = v[i * 2], viy = v[i * 2 + 1], vjx = v[j * 2], vjy = v[j * 2 + 1];
            float ex = vjx - vix, ey = vjy - viy, wx = x - vix, wy = y - viy;
            float h = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
            float bx = wx - ex * h, by = wy - ey * h;
            d = Mathf.Min(d, bx * bx + by * by);
            bool c1 = y >= viy, c2 = y < vjy, c3 = ex * wy > ey * wx;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
        }
        return s * Mathf.Sqrt(d);
    }

    /// <summary>Hạt nhiễu cố định theo pixel, 0..1 (tạo vân đá).</summary>
    static float Grain(float x, float y)
    {
        unchecked
        {
            uint h = (uint)((int)x * 374761393 + (int)y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }

    static Col K(Color c) { return Col.From(c); }

    /// <summary>Chuyển màu tuyến tính từ điểm (x0,y0) sang (x1,y1).</summary>
    static Shade Grad(Col a, Col b, float x0, float y0, float x1, float y1)
    {
        float dx = x1 - x0, dy = y1 - y0, dd = dx * dx + dy * dy;
        return (x, y) => Col.Lerp(a, b, ((x - x0) * dx + (y - y0) * dy) / dd);
    }

    /// <summary>Vẽ 1 khối có viền mực đen bao quanh (outline px).</summary>
    static void Part(Raster r, Sdf f, Shade s, float outline = 2.2f)
    {
        r.Fill((x, y) => f(x, y) - outline, K(GameTheme.Ink).A(.92f));
        r.Fill(f, s);
    }
    static void Part(Raster r, Sdf f, Col c, float outline = 2.2f) { Part(r, f, (x, y) => c, outline); }

    // =====================================================================================
    //  Ô đá
    // =====================================================================================
    static void Slab(Raster r, float dark)
    {
        Col lo = K(GameTheme.Slate), hi = K(GameTheme.SlateHi);
        r.Fill((x, y) => Box(x, y, 64, 64, 62, 62, 14), (x, y) =>
        {
            float t = Mathf.Clamp01(y / 128f * .8f + .1f);
            float n = .92f + .16f * Grain(x, y);
            return Col.Lerp(lo, hi, t).Mul(dark * n);
        });
        // cạnh vát: sáng ở trên-trái, tối ở dưới-phải
        r.Stroke((x, y) => Box(x, y, 64, 64, 59, 59, 12), 2.6f, (x, y) =>
        {
            float k = ((y - 64f) - (x - 64f)) / 128f;
            return k > 0f
                ? new Col(.72f, .69f, .85f, .34f * Mathf.Clamp01(k * 2.2f))
                : new Col(.04f, .03f, .07f, .45f * Mathf.Clamp01(-k * 2.2f));
        });
    }

    static void PaintTile(Raster r)
    {
        Slab(r, 1.12f);
        r.Stroke((x, y) => Box(x, y, 64, 64, 38, 38, 6), 2f, K(GameTheme.Ink).A(.36f));    // ô khắc chìm
        var c1 = new[] { 18f, 30f, 30f, 44f, 26f, 58f };
        var c2 = new[] { 106f, 100f, 96f, 88f, 102f, 76f };
        r.Stroke((x, y) => Line(x, y, c1), 1.4f, K(GameTheme.Ink).A(.30f));
        r.Stroke((x, y) => Line(x, y, c2), 1.4f, K(GameTheme.Ink).A(.30f));
    }

    static void PaintSolved(Raster r)
    {
        Slab(r, 1.12f);
        Col moss = K(GameTheme.Moss);
        r.Glow(64, 64, 52, moss.A(.22f));
        r.Stroke((x, y) => Circ(x, y, 64, 64, 30), 3f, moss.A(.78f));
        r.Stroke((x, y) => Circ(x, y, 64, 64, 21), 1.4f, moss.A(.40f));
        var tick = new[] { 49f, 62f, 59f, 51f, 80f, 78f };   // dấu ✓
        r.Stroke((x, y) => Line(x, y, tick), 6f, K(GameTheme.Ink).A(.55f));
        r.Stroke((x, y) => Line(x, y, tick), 4f, moss.A(.95f));
    }

    static void PaintRock(Raster r)
    {
        Slab(r, .78f);
        r.Fill((x, y) => Ell(x, y, 66, 22, 46, 9), K(GameTheme.Ink).A(.40f));   // bóng đổ dưới đá
        DrawBoulder(r);
    }

    /// <summary>Tảng đá nứt rỉ ánh đỏ (ý nghĩa: ô bị phong ấn, không đi được).</summary>
    static void DrawBoulder(Raster r)
    {
        float[] rad = { 40f, 44f, 37f, 43f, 39f, 45f, 38f, 42f };
        float[] jit = { .05f, -.08f, .10f, -.04f, .07f, -.09f, .04f, -.06f };
        var poly = new float[16];
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f + jit[i];
            poly[i * 2] = 64f + Mathf.Cos(a) * rad[i];
            poly[i * 2 + 1] = 62f + Mathf.Sin(a) * rad[i] * .95f;
        }

        Col dk = K(GameTheme.Slate), lt = K(GameTheme.IronHi).Mul(1.15f);
        Part(r, (x, y) => Poly(x, y, poly), (x, y) =>
        {
            float t = Mathf.Clamp01(.5f + ((y - 62f) - (x - 64f)) / 96f);
            return Col.Lerp(dk, lt, t).Mul(.9f + .2f * Grain(x, y));
        }, 2.4f);

        var f1 = new[] { 40f, 74f, 64f, 70f, 84f, 82f };
        var f2 = new[] { 64f, 70f, 60f, 44f };
        r.Stroke((x, y) => Line(x, y, f1), 1.6f, new Col(.85f, .82f, 1f, .16f));
        r.Stroke((x, y) => Line(x, y, f2), 1.6f, K(GameTheme.Ink).A(.30f));

        Col blood = K(GameTheme.Blood);
        var crack = new[] { 70f, 100f, 62f, 84f, 71f, 70f, 58f, 56f, 66f, 42f, 60f, 28f };
        r.Stroke((x, y) => Line(x, y, crack), 7f, blood.A(.22f));
        r.Stroke((x, y) => Line(x, y, crack), 3.2f, K(GameTheme.Ink).A(.85f));
        r.Stroke((x, y) => Line(x, y, crack), 1.6f, Col.Lerp(blood, new Col(1f, .75f, .7f), .35f).A(.95f));
    }

    // =====================================================================================
    //  Rune cho ô câu hỏi (chữ "?" do CellView vẽ đè lên bằng Text)
    // =====================================================================================
    static void PaintRune(Raster r)
    {
        Col gold = K(GameTheme.Gold), goldHi = K(GameTheme.GoldHi), bronze = K(GameTheme.Bronze);
        r.Glow(64, 64, 62, K(GameTheme.Ember).A(.40f));
        r.Fill((x, y) => Circ(x, y, 64, 64, 36), K(GameTheme.Ink).A(.62f));
        r.Stroke((x, y) => Circ(x, y, 64, 64, 46), 7f, K(GameTheme.Ink).A(.85f));
        r.Stroke((x, y) => Circ(x, y, 64, 64, 46), 4.6f, Grad(goldHi, bronze, 64, 110, 64, 18));
        r.Stroke((x, y) => Circ(x, y, 64, 64, 36), 1.6f, gold.A(.60f));

        // 4 viên kim cương ở các hướng
        float[][] dia =
        {
            new[] { 64f, 118f, 71f, 110f, 64f, 102f, 57f, 110f },
            new[] { 64f, 26f, 71f, 18f, 64f, 10f, 57f, 18f },
            new[] { 118f, 64f, 110f, 71f, 102f, 64f, 110f, 57f },
            new[] { 26f, 64f, 18f, 71f, 10f, 64f, 18f, 57f }
        };
        for (int i = 0; i < 4; i++)
        {
            var d = dia[i];
            Part(r, (x, y) => Poly(x, y, d), K(GameTheme.GoldHi), 1.6f);
        }
    }

    // =====================================================================================
    //  Hình phụ: ngôi sao, trái tim (SDF)
    // =====================================================================================
    static float[] Star(float cx, float cy, float s)
    {
        return new[] { cx, cy + s, cx + s * .25f, cy + s * .25f, cx + s, cy, cx + s * .25f, cy - s * .25f,
                       cx, cy - s, cx - s * .25f, cy - s * .25f, cx - s, cy, cx - s * .25f, cy + s * .25f };
    }

    static readonly float[] HeartTri = { -1.02f, .12f, 1.02f, .12f, 0f, -1.12f };

    static float HeartSdf(float x, float y, float cx, float cy, float s)
    {
        float u = (x - cx) / s, v = (y - cy) / s;
        float d1 = Len(u + .5f, v - .32f) - .56f;
        float d2 = Len(u - .5f, v - .32f) - .56f;
        float d3 = Poly(u, v, HeartTri);
        return Mathf.Min(d1, Mathf.Min(d2, d3)) * s;
    }

    static void PaintHeart(Raster r)
    {
        r.Fill((x, y) => HeartSdf(x, y, 32, 34, 22) - 2f, new Col(.35f, .1f, .2f, .55f));
        r.Fill((x, y) => HeartSdf(x, y, 32, 34, 22), Grad(new Col(1f, .97f, .98f), new Col(.96f, .78f, .86f), 32, 54, 32, 10));
        r.Fill((x, y) => Ell(x, y, 21, 44, 4.5f, 2.6f), new Col(1f, 1f, 1f, .75f));
    }

    static void PaintArrow(Raster r)
    {
        var tri = new[] { 32f, 58f, 56f, 18f, 32f, 29f, 8f, 18f };
        Part(r, (x, y) => Poly(x, y, tri), K(GameTheme.Rune), 2.4f);
    }

    // =====================================================================================
    //  Hiệp sĩ chibi (có mặt để thể hiện cảm xúc) - vẽ theo tư thế
    // =====================================================================================
    struct KPose
    {
        public bool walk, cheer, tuck;
        public float legPhase, dy, armL, armR;
        public int face;   // 0 bình thường, 1 cười tít mắt, 2 mắt long lanh
    }

    static KPose PoseIdle() { return new KPose { face = 0 }; }

    static KPose PoseWalk(int k)
    {
        float ph = k * Mathf.PI / 2f;
        return new KPose { walk = true, legPhase = ph, dy = (1f - Mathf.Abs(Mathf.Sin(ph))) * 2.5f, armL = -Mathf.Sin(ph), armR = Mathf.Sin(ph), face = 0 };
    }

    static KPose PoseCheer(int k)
    {
        return new KPose { cheer = true, tuck = k == 1, face = k == 0 ? 1 : 2, armL = k == 0 ? 0f : 1f, armR = k == 0 ? 1f : 0f, dy = k == 0 ? 0f : 3f };
    }

    static void PaintKnight(Raster r, KPose p)
    {
        Col steelHi = K(GameTheme.Hex(0xB4BCD2)), steelLo = K(GameTheme.Hex(0x4A5068)), steelDk = K(GameTheme.Hex(0x343850));
        Col gold = K(GameTheme.Gold), goldHi = K(GameTheme.GoldHi);
        Col ember = K(GameTheme.Ember), emberHi = K(GameTheme.EmberHi), blood = K(GameTheme.Blood);
        Col ink = K(GameTheme.Ink);
        Col skinHi = K(GameTheme.Hex(0xF8D6B0)), skinLo = K(GameTheme.Hex(0xE5AE85));
        Col boot = K(GameTheme.Hex(0x2A2638));
        float dy = p.dy;

        // bóng + quầng lửa dưới chân (khi nhảy thì bóng nhỏ lại)
        float sh = p.tuck ? .7f : 1f;
        r.Glow(56, 10, 44, ember.A(.26f));
        r.Fill((x, y) => Ell(x, y, 56, 8, 32 * sh, 6 * sh), ink.A(.5f));

        // áo choàng phía sau
        float sway = p.walk ? Mathf.Sin(p.legPhase) * 4f : 0f;
        float[] cape = p.cheer
            ? new[] { 36f, 74f + dy, 76f, 74f + dy, 98f, 42f, 14f, 42f }
            : new[] { 36f, 74f + dy, 76f, 74f + dy, 90f + sway, 30f, 22f + sway, 30f };
        Part(r, (x, y) => Poly(x, y, cape), Grad(Col.Lerp(blood, ink, .30f), Col.Lerp(blood, ink, .75f), 56, 74, 56, 30));

        // chân + giày
        float sL = p.walk ? Mathf.Sin(p.legPhase) : 0f, lL = p.walk ? Mathf.Max(0f, Mathf.Cos(p.legPhase)) : 0f;
        float sR = p.walk ? -Mathf.Sin(p.legPhase) : 0f, lR = p.walk ? Mathf.Max(0f, -Mathf.Cos(p.legPhase)) : 0f;
        float flx = 46f + sL * 5f, fly = 13f + lL * 8f, frx = 66f + sR * 5f, fry = 13f + lR * 8f;
        if (p.cheer)
        {
            flx = 40f; frx = 72f;
            fly = fry = p.tuck ? 26f : 13f;
        }
        float hly = 44f + dy;
        Part(r, (x, y) => Seg(x, y, 46f, hly, flx, fly + 4f) - 7.5f, Grad(steelLo, steelDk, 46, 48, 46, 14));
        Part(r, (x, y) => Seg(x, y, 66f, hly, frx, fry + 4f) - 7.5f, Grad(steelLo, steelDk, 66, 48, 66, 14));
        Part(r, (x, y) => Box(x, y, flx - 1f, fly, 10.5f, 5.5f, 3f), boot);
        Part(r, (x, y) => Box(x, y, frx + 1f, fry, 10.5f, 5.5f, 3f), boot);

        // thân giáp + đai + huy hiệu lửa
        Part(r, (x, y) => Box(x, y, 56, 56 + dy, 19, 16, 7), Grad(steelHi, steelLo, 40, 74, 72, 40));
        Part(r, (x, y) => Box(x, y, 56, 45 + dy, 19, 3.4f, 1.2f), Grad(goldHi, gold, 56, 49, 56, 42), 1.6f);
        var emblem = new[] { 56f, 66f + dy, 63f, 58f + dy, 56f, 50f + dy, 49f, 58f + dy };
        Part(r, (x, y) => Poly(x, y, emblem), Grad(emberHi, K(GameTheme.EmberDeep), 56, 66, 56, 50), 1.4f);

        // tay
        float shy = 64f + dy;
        float hlx, hly2, hrx, hry;
        if (p.cheer) { hlx = 18f; hly2 = 116f + p.armL * 6f; hrx = 94f; hry = 118f + p.armR * 6f; }
        else { hlx = 28f; hly2 = 47f + dy + p.armL * 6f; hrx = 86f; hry = 47f + dy + p.armR * 6f; }

        // kiếm (phía sau bàn tay phải)
        float tx = p.cheer ? hrx + 12f : hrx, ty = p.cheer ? hry + 24f : hry + 36f;
        Part(r, (x, y) => Seg(x, y, hrx, hry, tx, ty) - 3.4f, K(GameTheme.Hex(0xE6EAF5)), 1.8f);

        Part(r, (x, y) => Seg(x, y, 35f, shy, hlx, hly2) - 6f, Grad(steelLo, steelDk, 35, shy, hlx, hly2));
        Part(r, (x, y) => Seg(x, y, 77f, shy, hrx, hry) - 6f, Grad(steelLo, steelDk, 77, shy, hrx, hry));
        Part(r, (x, y) => Circ(x, y, 35, shy, 8.5f), Grad(steelHi, steelLo, 28, shy + 8, 42, shy - 8));
        Part(r, (x, y) => Circ(x, y, 77, shy, 8.5f), Grad(steelHi, steelLo, 70, shy + 8, 84, shy - 8));
        Part(r, (x, y) => Circ(x, y, hlx, hly2, 6.2f), steelHi, 1.8f);
        Part(r, (x, y) => Circ(x, y, hrx, hry, 6.2f), steelHi, 1.8f);
        // chắn kiếm
        Part(r, (x, y) => Seg(x, y, hrx - 8f, hry + 5f, hrx + 8f, hry + 5f) - 2.4f, Grad(goldHi, gold, hrx, hry + 8, hrx, hry + 2), 1.4f);

        // khiên (chỉ khi không ăn mừng)
        if (!p.cheer)
        {
            float sx = hlx - 3f, sy = hly2 + 1f;
            var shield = new[] { sx - 13f, sy + 17f, sx + 9f, sy + 17f, sx + 9f, sy - 5f, sx - 2f, sy - 23f, sx - 13f, sy - 5f };
            Part(r, (x, y) => Poly(x, y, shield), Grad(K(GameTheme.IronHi), K(GameTheme.Iron), sx, sy + 17, sx, sy - 23), 2.2f);
            r.Stroke((x, y) => Poly(x, y, shield) + 3f, 1.8f, gold.A(.95f));
            var sEm = new[] { sx - 2f, sy + 8f, sx + 3f, sy, sx - 2f, sy - 10f, sx - 7f, sy };
            r.Fill((x, y) => Poly(x, y, sEm), ember.A(.95f));
        }

        // lông mũ + mũ giáp
        float hy = 96f + dy;
        var plume = new[] { 50f, hy + 22f, 60f, hy + 27f, 74f, hy + 38f, 96f, hy + 26f, 88f, hy + 14f, 72f, hy + 22f, 60f, hy + 19f };
        Part(r, (x, y) => Poly(x, y, plume), Grad(Col.Lerp(blood, new Col(1f, .6f, .55f), .25f), blood, 92, hy + 36, 56, hy + 20));
        Part(r, (x, y) => Circ(x, y, 56, hy, 26), Grad(steelHi, steelLo, 38, hy + 22, 76, hy - 22), 2.4f);
        r.Fill((x, y) => Ell(x, y, 45, hy + 17, 10, 5), new Col(1f, 1f, 1f, .22f));

        // khuôn mặt (hở mặt để thấy biểu cảm)
        float fy = 90f + dy;
        r.Fill((x, y) => Box(x, y, 56, fy, 18.5f, 17.5f, 14.5f) - 1.8f, ink.A(.7f));
        r.Fill((x, y) => Box(x, y, 56, fy, 18.5f, 17.5f, 14.5f), Grad(skinHi, skinLo, 56, fy + 17, 56, fy - 17));
        Part(r, (x, y) => Box(x, y, 56, hy + 12f, 18f, 2.8f, 1.2f), Grad(goldHi, gold, 56, hy + 15, 56, hy + 9), 1.3f);   // dải vàng trên trán

        Col pink = new Col(1f, .56f, .64f);
        Col dark = K(GameTheme.Hex(0x2B1D12));
        float ey = fy + 3f;
        if (p.face == 0)
        {
            r.Fill((x, y) => Ell(x, y, 49, ey, 2.5f, 3.4f), dark);
            r.Fill((x, y) => Ell(x, y, 63, ey, 2.5f, 3.4f), dark);
            r.Fill((x, y) => Circ(x, y, 49.8f, ey + 1.4f, .9f), Col.Lerp(dark, new Col(1, 1, 1), 1f));
            r.Fill((x, y) => Circ(x, y, 63.8f, ey + 1.4f, .9f), new Col(1, 1, 1));
            r.Fill((x, y) => Circ(x, y, 44f, fy - 4f, 3.4f), pink.A(.38f));
            r.Fill((x, y) => Circ(x, y, 68f, fy - 4f, 3.4f), pink.A(.38f));
            var smile = new[] { 51f, fy - 6f, 54f, fy - 8f, 56f, fy - 8.6f, 58f, fy - 8f, 61f, fy - 6f };
            r.Stroke((x, y) => Line(x, y, smile), 1.9f, dark);
        }
        else
        {
            if (p.face == 1)
            {
                var al = new[] { 44f, ey - 1f, 49f, ey + 4.5f, 54f, ey - 1f };
                var ar = new[] { 58f, ey - 1f, 63f, ey + 4.5f, 68f, ey - 1f };
                r.Stroke((x, y) => Line(x, y, al), 2.6f, dark);
                r.Stroke((x, y) => Line(x, y, ar), 2.6f, dark);
            }
            else
            {
                r.Fill((x, y) => Circ(x, y, 49, ey, 4.6f), dark);
                r.Fill((x, y) => Circ(x, y, 63, ey, 4.6f), dark);
                r.Fill((x, y) => Circ(x, y, 50.6f, ey + 1.9f, 1.8f), new Col(1, 1, 1));
                r.Fill((x, y) => Circ(x, y, 64.6f, ey + 1.9f, 1.8f), new Col(1, 1, 1));
                r.Fill((x, y) => Circ(x, y, 47.8f, ey - 1.6f, .9f), new Col(1, 1, 1));
                r.Fill((x, y) => Circ(x, y, 61.8f, ey - 1.6f, .9f), new Col(1, 1, 1));
            }
            r.Fill((x, y) => Circ(x, y, 43.5f, fy - 4f, 4.4f), pink.A(.62f));
            r.Fill((x, y) => Circ(x, y, 68.5f, fy - 4f, 4.4f), pink.A(.62f));
            // miệng há to cười
            float my = fy - 5f;
            Sdf mouth = (x, y) => Mathf.Max(Ell(x, y, 56, my + 2f, 8.5f, 8f), y - (my + 2.2f));
            r.Fill(mouth, K(GameTheme.Hex(0x3A1620)));
            r.Fill((x, y) => Mathf.Max(Ell(x, y, 56, my - 5f, 5.5f, 3.4f), mouth(x, y)), K(GameTheme.Hex(0xE8707E)));
            r.Fill((x, y) => Mathf.Max(Box(x, y, 56, my + .8f, 6.4f, 1.7f, .6f), mouth(x, y)), new Col(1, 1, 1));
        }

        // tia sáng vui mừng quanh người
        if (p.cheer)
        {
            r.Fill((x, y) => Poly(x, y, Star(13, 138, 6)), goldHi.A(.95f));
            r.Fill((x, y) => Poly(x, y, Star(100, 100, 5)), new Col(1f, .96f, .8f, .95f));
            r.Fill((x, y) => Poly(x, y, Star(104, 60, 4)), goldHi.A(.9f));
            r.Fill((x, y) => Poly(x, y, Star(8, 84, 4)), new Col(1f, .96f, .8f, .9f));
        }
    }

    // =====================================================================================
    //  Công chúa (đích đến). cheer=false: mỉm cười chờ cứu, true: reo mừng giơ tay
    // =====================================================================================
    static void PaintPrincess(Raster r, bool cheer)
    {
        Col ink = K(GameTheme.Ink);
        Col goldHi = K(GameTheme.GoldHi), gold = K(GameTheme.Gold);
        Col skinHi = K(GameTheme.Hex(0xFBDCBA)), skinLo = K(GameTheme.Hex(0xEDB48C));
        Col hairHi = K(GameTheme.Hex(0xE2A646)), hairLo = K(GameTheme.Hex(0x8E4F1D));
        Col roseHi = K(GameTheme.Hex(0xF9B8DC)), roseLo = K(GameTheme.Hex(0xA25AD0));
        Col dark = K(GameTheme.Hex(0x3A2A5A));
        Col pink = new Col(1f, .5f, .62f);

        r.Glow(64, 70, 62, new Col(1f, .6f, .85f, .34f));
        r.Fill((x, y) => Ell(x, y, 64, 9, 36, 6), ink.A(.45f));

        // tóc dài phía sau
        Part(r, (x, y) => Box(x, y, 64, 78, 31, 37, 24), Grad(hairHi, hairLo, 64, 112, 64, 40), 2f);

        // váy xòe
        var skirt = new[] { 50f, 66f, 78f, 66f, 104f, 14f, 24f, 14f };
        Part(r, (x, y) => Poly(x, y, skirt), Grad(roseHi, roseLo, 64, 66, 64, 14));
        r.Stroke((x, y) => Seg(x, y, 58f, 62f, 40f, 16f), 1.4f, ink.A(.18f));
        r.Stroke((x, y) => Seg(x, y, 64f, 62f, 64f, 16f), 1.4f, ink.A(.18f));
        r.Stroke((x, y) => Seg(x, y, 70f, 62f, 88f, 16f), 1.4f, ink.A(.18f));
        Part(r, (x, y) => Box(x, y, 64, 15, 40, 2.6f, 1f), Grad(goldHi, gold, 64, 18, 64, 12), 1.4f);

        // thân áo + thắt lưng + ngọc
        Part(r, (x, y) => Box(x, y, 64, 70, 14, 11, 5), Grad(roseHi, roseLo, 64, 80, 64, 60));
        Part(r, (x, y) => Box(x, y, 64, 62, 15, 2.6f, 1f), Grad(goldHi, gold, 64, 65, 64, 60), 1.3f);
        r.Fill((x, y) => Circ(x, y, 64, 70, 3.2f), K(GameTheme.Ly));

        // tay
        if (!cheer)
        {
            Part(r, (x, y) => Seg(x, y, 48f, 74f, 61f, 58f) - 4.6f, Grad(skinHi, skinLo, 50, 74, 60, 58), 1.8f);
            Part(r, (x, y) => Seg(x, y, 80f, 74f, 67f, 58f) - 4.6f, Grad(skinHi, skinLo, 78, 74, 68, 58), 1.8f);
            Part(r, (x, y) => Circ(x, y, 64, 57, 5.4f), skinHi, 1.6f);
        }
        else
        {
            Part(r, (x, y) => Seg(x, y, 48f, 74f, 30f, 106f) - 4.6f, Grad(skinHi, skinLo, 48, 74, 30, 106), 1.8f);
            Part(r, (x, y) => Seg(x, y, 80f, 74f, 98f, 106f) - 4.6f, Grad(skinHi, skinLo, 80, 74, 98, 106), 1.8f);
            Part(r, (x, y) => Circ(x, y, 29, 108, 5.6f), skinHi, 1.6f);
            Part(r, (x, y) => Circ(x, y, 99, 108, 5.6f), skinHi, 1.6f);
        }
        Part(r, (x, y) => Circ(x, y, 47, 74, 7.5f), Grad(roseHi, roseLo, 47, 82, 47, 66), 1.8f);   // tay áo phồng
        Part(r, (x, y) => Circ(x, y, 81, 74, 7.5f), Grad(roseHi, roseLo, 81, 82, 81, 66), 1.8f);

        // đầu: tóc sau, mặt, mái, vương miện
        Part(r, (x, y) => Circ(x, y, 64, 99, 27), Grad(hairHi, hairLo, 64, 126, 64, 72), 2f);
        Part(r, (x, y) => Circ(x, y, 64, 92, 20.5f), Grad(skinHi, skinLo, 64, 112, 64, 72), 1.8f);
        var bangs = new[] { 42f, 98f, 64f, 108f, 86f, 98f, 84f, 116f, 64f, 121f, 44f, 116f };
        Part(r, (x, y) => Poly(x, y, bangs), Grad(hairHi, hairLo, 64, 121, 64, 98), 1.6f);
        var crown = new[] { 49f, 116f, 51f, 129f, 58f, 122f, 64f, 132f, 70f, 122f, 77f, 129f, 79f, 116f };
        Part(r, (x, y) => Poly(x, y, crown), Grad(goldHi, gold, 64, 132, 64, 116), 1.6f);
        r.Fill((x, y) => Circ(x, y, 64, 122, 2.6f), K(GameTheme.Ly));
        r.Fill((x, y) => Circ(x, y, 55, 118.5f, 1.6f), K(GameTheme.Hoa));
        r.Fill((x, y) => Circ(x, y, 73, 118.5f, 1.6f), K(GameTheme.Hoa));

        // nét mặt
        float ey = 92f;
        if (!cheer)
        {
            r.Fill((x, y) => Ell(x, y, 56, ey, 3f, 4.2f), dark);
            r.Fill((x, y) => Ell(x, y, 72, ey, 3f, 4.2f), dark);
            r.Fill((x, y) => Circ(x, y, 57.1f, ey + 1.8f, 1.1f), new Col(1, 1, 1));
            r.Fill((x, y) => Circ(x, y, 73.1f, ey + 1.8f, 1.1f), new Col(1, 1, 1));
            r.Stroke((x, y) => Seg(x, y, 52.5f, ey + 4f, 50.5f, ey + 5.8f), 1.5f, dark);
            r.Stroke((x, y) => Seg(x, y, 75.5f, ey + 4f, 77.5f, ey + 5.8f), 1.5f, dark);
            r.Fill((x, y) => Circ(x, y, 49f, 84f, 4.2f), pink.A(.42f));
            r.Fill((x, y) => Circ(x, y, 79f, 84f, 4.2f), pink.A(.42f));
            var smile = new[] { 60f, 80f, 62.5f, 78.2f, 64f, 77.8f, 65.5f, 78.2f, 68f, 80f };
            r.Stroke((x, y) => Line(x, y, smile), 1.8f, dark);
        }
        else
        {
            var al = new[] { 51f, ey - 1f, 56f, ey + 4.5f, 61f, ey - 1f };
            var ar = new[] { 67f, ey - 1f, 72f, ey + 4.5f, 77f, ey - 1f };
            r.Stroke((x, y) => Line(x, y, al), 2.4f, dark);
            r.Stroke((x, y) => Line(x, y, ar), 2.4f, dark);
            r.Fill((x, y) => Circ(x, y, 48.5f, 84f, 4.8f), pink.A(.62f));
            r.Fill((x, y) => Circ(x, y, 79.5f, 84f, 4.8f), pink.A(.62f));
            Sdf mouth = (x, y) => Mathf.Max(Ell(x, y, 64, 80.5f, 7f, 6.5f), y - 80.8f);
            r.Fill(mouth, K(GameTheme.Hex(0x3A1620)));
            r.Fill((x, y) => Mathf.Max(Ell(x, y, 64, 75.5f, 4.5f, 2.8f), mouth(x, y)), K(GameTheme.Hex(0xE8707E)));
            // trái tim bay quanh
            r.Fill((x, y) => HeartSdf(x, y, 16, 112, 7) - 1.5f, ink.A(.55f));
            r.Fill((x, y) => HeartSdf(x, y, 16, 112, 7), pink.A(.95f));
            r.Fill((x, y) => HeartSdf(x, y, 112, 104, 6) - 1.5f, ink.A(.55f));
            r.Fill((x, y) => HeartSdf(x, y, 112, 104, 6), new Col(1f, .66f, .74f, .95f));
            r.Fill((x, y) => Poly(x, y, Star(106, 126, 5)), goldHi.A(.95f));
        }
    }
}
