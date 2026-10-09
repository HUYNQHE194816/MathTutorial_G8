using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hiệu ứng thiên nhiên cho MainMenu / ModeSelect (toàn bộ sprite sinh bằng code, không cần thêm asset):
///  - Warp: uốn mesh tán cây của ảnh pixel -> cành lá lay theo gió, thân gần như đứng yên.
///  - AddBranches: thêm nhiều cành con mọc ra từ tán cây, mỗi cành là một khớp riêng nên đung đưa độc lập.
///  - AddGrass: cỏ lay (xoay quanh gốc lá cỏ, lan theo sóng gió).
///  - AddLeaves: lá rơi, nhào lộn và trôi theo gió.
/// Toạ độ theo "cảnh" 1920x1080, gốc ở giữa (ảnh pixel 480x270 phóng 4 lần).
/// Gọi Tick(t, dt) mỗi frame và Dispose() khi huỷ.
/// </summary>
public class MenuNatureFx
{
    const float Px = 4f;                       // 1 pixel ảnh gốc = 4 đơn vị cảnh

    // ---------- gió dùng chung ----------
    public static float Wind(float t)
    {
        float w = .70f + .28f * Mathf.Sin(t * .23f + 1f) + .30f * (Mathf.PerlinNoise(t * .18f, 3.7f) - .5f);
        return Mathf.Clamp(w, .25f, 1.3f);
    }

    // ---------- sprite sinh bằng code ----------
    Sprite branchSprite, bladeSprite, leafSprite;
    readonly List<Texture2D> textures = new List<Texture2D>();

    Sprite Make(int w, int h, System.Func<int, int, float> alpha)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x, y)) * 255f));
        tex.SetPixels32(px); tex.Apply(false, true);
        textures.Add(tex);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, 0f), 100f);
    }

    void EnsureSprites()
    {
        if (branchSprite != null) return;
        // cành: thon dần từ gốc (rộng) lên ngọn (nhọn)
        branchSprite = Make(16, 64, (x, y) =>
        {
            float half = Mathf.Lerp(7.5f, 1.0f, y / 63f);
            return half - Mathf.Abs(x - 7.5f) + .5f;
        });
        // lá cỏ: thon nhọn
        bladeSprite = Make(8, 48, (x, y) =>
        {
            float half = 3.6f * (1f - Mathf.Pow(y / 47f, .85f));
            return half - Mathf.Abs(x - 3.5f) + .5f;
        });
        // lá cây: hình trám dài
        leafSprite = Make(16, 10, (x, y) =>
        {
            float u = (x - 7.5f) / 8f, v = (y - 4.5f) / 5f;
            return (1f - Mathf.Abs(u) * 1.0f - Mathf.Abs(v) * Mathf.Abs(v) * 1.4f) * 3f;
        });
        leafSprite = Sprite.Create(textures[textures.Count - 1], new Rect(0, 0, 16, 10), new Vector2(.5f, .5f), 100f);
    }

    public void Dispose()
    {
        foreach (var t in textures) if (t != null) Object.Destroy(t);
        textures.Clear();
        warps.Clear(); nodes.Clear(); blades.Clear(); leaves.Clear();
    }

    // ================================================================== 1) Warp tán cây
    readonly List<TreeWarp> warps = new List<TreeWarp>();

    /// <summary>
    /// Gắn hiệu ứng uốn mesh cho lớp cây. yFull/yZero: hàng pixel (tính từ trên xuống) mà trên yFull lay 100%, dưới yZero đứng yên.
    /// xLo/xHi: (tuỳ chọn) vùng sát hai mép trái/phải - nơi có thân cây - lay ít, tính bằng pixel.
    /// </summary>
    public void Warp(RectTransform layer, float yFull, float yZero, float xLo, float xHi, float amp, float phase)
    {
        var w = layer.gameObject.AddComponent<TreeWarp>();
        w.yFull = yFull; w.yZero = yZero; w.xLo = xLo; w.xHi = xHi; w.amp = amp; w.phase = phase;
        warps.Add(w);
    }

    // ================================================================== 2) Cành thêm
    class Node { public RectTransform rt; public float baseAngle, amp, freq, phase; }
    readonly List<Node> nodes = new List<Node>();

    // Điểm gốc cành lấy sẵn trên ảnh px_trees_near / px_trees_mid: (x, y) pixel từ góc trên trái, cx = tâm tán.
    static readonly int[][] NearAnchors =
    {
        new[]{31,140,80}, new[]{180,94,80}, new[]{341,113,400}, new[]{356,141,400}, new[]{123,18,80}, new[]{137,69,80},
        new[]{449,122,400}, new[]{402,35,400}, new[]{346,79,400}, new[]{385,14,400}, new[]{80,12,80}, new[]{142,43,80},
        new[]{325,14,400}, new[]{13,61,80}, new[]{112,117,80}, new[]{35,93,80},
    };
    static readonly int[][] MidAnchors =
    {
        new[]{25,102,85}, new[]{40,72,85}, new[]{123,115,85}, new[]{387,137,395}, new[]{138,80,85},
        new[]{444,127,395}, new[]{352,91,395}, new[]{83,63,85}, new[]{429,85,395}, new[]{86,137,395},
    };

    /// <summary>Thêm cụm cành đung đưa lên tán cây. near = cây lớn phía trước, ngược lại = cây giữa.</summary>
    public void AddBranches(RectTransform parent, bool near)
    {
        EnsureSprites();
        var root = Group("Branches_" + (near ? "Near" : "Mid"), parent);
        var anchors = near ? NearAnchors : MidAnchors;
        Color col = near ? new Color32(5, 8, 16, 255) : new Color32(8, 17, 31, 255);
        float len0 = near ? 100f : 70f, th0 = near ? 7f : 5f;
        var rnd = new System.Random(near ? 11 : 23);

        foreach (var a in anchors)
        {
            float sx = (a[0] + .5f - 240f) * Px, sy = (135f - (a[1] + .5f)) * Px;
            int side = a[0] >= a[2] ? 1 : -1;
            float elev = Range(rnd, 15f, 65f);
            float abs = side > 0 ? elev : 180f - elev;
            Branch(root, new Vector2(sx, sy), true, 0f, abs, Range(rnd, .75f, 1.1f) * len0, th0, 0, col, rnd, sx);
        }
    }

    // parentAbs: góc tuyệt đối của cành cha; abs: góc tuyệt đối của cành này (độ, 0 = sang phải, 90 = lên).
    void Branch(RectTransform parent, Vector2 pos, bool isRoot, float parentAbs, float abs, float len, float th, int depth,
                Color col, System.Random rnd, float worldX)
    {
        var go = new GameObject("B" + depth, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        var im = go.GetComponent<Image>();
        im.sprite = branchSprite; im.color = col; im.raycastTarget = false;
        rt.anchorMin = rt.anchorMax = isRoot ? new Vector2(.5f, .5f) : new Vector2(.5f, 0f);
        rt.pivot = new Vector2(.5f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(th, len);
        float rel = (abs - 90f) - (isRoot ? 0f : (parentAbs - 90f));
        rt.localRotation = Quaternion.Euler(0, 0, rel);

        nodes.Add(new Node
        {
            rt = rt, baseAngle = rel,
            amp = 1.3f + depth * 1.5f,                    // ngọn lay mạnh hơn gốc
            freq = Range(rnd, .8f, 1.4f) + depth * .35f,
            phase = worldX * .004f + (float)rnd.NextDouble() * 6.283f,
        });

        if (depth >= 3) return;
        int n = (depth == 0 && rnd.NextDouble() < .5) ? 3 : 2;
        for (int i = 0; i < n; i++)
        {
            float t = Range(rnd, .45f, 1f);
            float ang = abs + (rnd.NextDouble() < .5 ? -1f : 1f) * Range(rnd, 20f, 55f);
            Branch(rt, new Vector2(0, t * len), false, abs, ang, len * Range(rnd, .55f, .72f), th * .62f, depth + 1, col, rnd, worldX);
        }
    }

    // ================================================================== 3) Cỏ
    class Blade { public RectTransform rt; public float baseRot, amp, speed, phase, x; }
    readonly List<Blade> blades = new List<Blade>();

    /// <summary>front=false: cỏ nằm sau tượng/hiệp sĩ (y cao); front=true: cỏ che chân (y thấp, to hơn).</summary>
    public void AddGrass(RectTransform parent, bool front)
    {
        EnsureSprites();
        var root = Group(front ? "Grass_Front" : "Grass_Back", parent);
        int count = front ? 90 : 120;
        float yHi = front ? -404f : -296f, yLo = front ? -545f : -404f;
        var rnd = new System.Random(front ? 5 : 9);
        var pal = new[] { new Color32(15, 40, 28, 255), new Color32(21, 58, 40, 255), new Color32(30, 84, 66, 255), new Color32(47, 115, 96, 255) };

        var list = new List<Blade>();
        for (int i = 0; i < count; i++)
        {
            float x = Range(rnd, -1000f, 1000f);
            float y = Range(rnd, yLo, yHi);
            float near01 = Mathf.InverseLerp(-296f, -545f, y);              // 0 = xa, 1 = gần người xem
            float h = Mathf.Lerp(30f, 78f, near01) * Range(rnd, .8f, 1.25f);
            float w = Mathf.Lerp(7f, 15f, near01);
            var c = pal[rnd.Next(front ? 2 : 0, front ? 4 : 3)];
            c.a = 255;
            Color col = (Color)c;
            var go = new GameObject("Blade", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            var im = go.GetComponent<Image>(); im.sprite = bladeSprite; im.color = col; im.raycastTarget = false;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, 0f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            list.Add(new Blade
            {
                rt = rt, x = x, baseRot = Range(rnd, -12f, 12f),
                amp = Range(rnd, 4f, 9f) * Mathf.Lerp(.8f, 1.3f, near01),
                speed = Range(rnd, 1.1f, 1.9f), phase = (float)rnd.NextDouble() * 6.283f,
            });
        }
        // vẽ lá ở xa trước, lá gần sau
        list.Sort((a, b) => b.rt.anchoredPosition.y.CompareTo(a.rt.anchoredPosition.y));
        foreach (var b in list) { b.rt.SetAsLastSibling(); blades.Add(b); }
    }

    // ================================================================== 4) Lá rơi
    class Leaf { public RectTransform rt; public Image im; public Color col; public float x, y, vy, swayAmp, swayFreq, phase, spin, rot, size, drift; }
    readonly List<Leaf> leaves = new List<Leaf>();
    const float LeafTop = 520f, LeafBottom = -330f;

    public void AddLeaves(RectTransform parent, int count = 26)
    {
        EnsureSprites();
        var root = Group("FallingLeaves", parent);
        var pal = new[]
        {
            new Color(.62f, .42f, .22f), new Color(.70f, .54f, .28f), new Color(.45f, .50f, .28f),
            new Color(.36f, .43f, .56f), new Color(.52f, .33f, .20f),
        };
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Leaf", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            var im = go.GetComponent<Image>(); im.sprite = leafSprite; im.raycastTarget = false;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f); rt.pivot = new Vector2(.5f, .5f);
            var l = new Leaf { rt = rt, im = im, col = pal[Random.Range(0, pal.Length)] };
            Respawn(l, true);
            leaves.Add(l);
        }
    }

    static void Respawn(Leaf l, bool anywhere)
    {
        bool canopy = Random.value < .7f;
        l.x = canopy ? (Random.value < .5f ? Random.Range(-920f, -260f) : Random.Range(300f, 940f)) : Random.Range(-950f, 950f);
        l.y = anywhere ? Random.Range(LeafBottom, LeafTop) : Random.Range(300f, LeafTop);
        l.vy = Random.Range(38f, 85f);
        l.swayAmp = Random.Range(30f, 90f); l.swayFreq = Random.Range(.6f, 1.3f);
        l.phase = Random.value * 6.283f;
        l.spin = Random.Range(-70f, 70f); l.rot = Random.value * 360f;
        l.size = Random.Range(22f, 40f);
        l.drift = Random.Range(8f, 26f);
        l.rt.sizeDelta = new Vector2(l.size, l.size * .62f);
    }

    // ================================================================== Rừng trăng dùng chung cho các màn menu
    Image forestGlow, forestGlowTight, forestMoon;

    static RectTransform ArtLayer(RectTransform scene, string n, string res)
    {
        var sp = MenuArt.Get(res); if (sp == null) return null;
        var rt = UIKit.CBox(n, scene, Color.white, Vector2.zero, Vector2.zero);
        UIKit.Stretch(rt, 0, 0, 0, 0);
        rt.GetComponent<Image>().sprite = sp;
        return rt;
    }

    /// <summary>Dựng nền: trời, trăng nhấp nháy, cây lay + cành, lá rơi, mặt đất, cỏ. Dùng cho ModeSelect và SubjectPicker.</summary>
    public void BuildForest(RectTransform scene, float moonY, int leafCount)
    {
        ArtLayer(scene, "Sky", "px_sky");
        var g1 = UIKit.CBox("MoonGlow", scene, new Color(.35f, .6f, 1f, .38f), new Vector2(0, moonY), new Vector2(1800, 1800));
        forestGlow = g1.GetComponent<Image>(); forestGlow.sprite = ThemeGfx.Glow;
        var g2 = UIKit.CBox("MoonGlowTight", scene, new Color(.7f, .85f, 1f, .30f), new Vector2(0, moonY), new Vector2(1000, 1000));
        forestGlowTight = g2.GetComponent<Image>(); forestGlowTight.sprite = ThemeGfx.Glow;
        var moon = MenuArt.Get("px_moon");
        if (moon != null)
        {
            var m = UIKit.CBox("Moon", scene, Color.white, new Vector2(0, moonY), new Vector2(560, 560));
            forestMoon = m.GetComponent<Image>(); forestMoon.sprite = moon;
        }

        var far = ArtLayer(scene, "TreesFar", "px_trees_far");
        if (far != null) Warp(far, 150f, 195f, 0f, 0f, 4f, 0f);
        ArtLayer(scene, "Ruins", "px_ruins");
        var mid = ArtLayer(scene, "TreesMid", "px_trees_mid");
        if (mid != null) { Warp(mid, 130f, 168f, 0f, 0f, 5f, 1.7f); AddBranches(scene, false); }
        var near = ArtLayer(scene, "TreesNear", "px_trees_near");
        if (near != null) { Warp(near, 70f, 140f, 45f, 100f, 8f, 3.1f); AddBranches(scene, true); }
        AddLeaves(scene, leafCount);
        ArtLayer(scene, "Ground", "px_ground");
        AddGrass(scene, false);
        AddGrass(scene, true);
    }

    /// <summary>Cảnh 1920x1080 phủ kín màn hình kiểu "cover".</summary>
    public static void FitCover(RectTransform artRoot, RectTransform scene, ref Vector2 last)
    {
        if (artRoot == null || scene == null) return;
        var r = artRoot.rect.size;
        if (r.x <= 1f || r == last) return;
        last = r;
        float s = Mathf.Max(r.x / 1920f, r.y / 1080f);
        scene.localScale = new Vector3(s, s, 1f);
    }

    // ================================================================== cập nhật mỗi frame
    public void Tick(float t, float dt)
    {
        float wind = Wind(t);

        if (forestGlow != null)
        {
            float fl = Mathf.PerlinNoise(t * 1.3f, .5f) - .5f, fl2 = Mathf.PerlinNoise(t * 1.9f, 8.1f) - .5f;
            var c = forestGlow.color; c.a = .38f + .04f * Mathf.Sin(t * .5f) + .10f * fl; forestGlow.color = c;
            if (forestGlowTight != null) { c = forestGlowTight.color; c.a = .30f + .03f * Mathf.Sin(t * .8f + 1f) + .12f * fl2; forestGlowTight.color = c; }
            if (forestMoon != null) { float b = .94f + .10f * fl2; forestMoon.color = new Color(b, b, b, 1f); }
        }

        foreach (var w in warps) if (w != null) w.Refresh();

        foreach (var n in nodes)
        {
            float a = n.baseAngle + n.amp * wind * (Mathf.Sin(t * n.freq + n.phase) + .35f * Mathf.Sin(t * n.freq * 2.3f + n.phase * 1.7f));
            n.rt.localRotation = Quaternion.Euler(0, 0, a);
        }

        foreach (var b in blades)
        {
            float a = b.baseRot * .5f + wind * b.amp * Mathf.Sin(t * b.speed + b.x * .006f + b.phase)
                      + wind * 3f * Mathf.Sin(t * .6f + b.x * .002f);                 // đợt gió dài
            b.rt.localRotation = Quaternion.Euler(0, 0, a);
        }

        foreach (var l in leaves)
        {
            l.y -= l.vy * dt;
            l.x += (l.drift * wind + Mathf.Cos(t * l.swayFreq + l.phase) * l.swayAmp * l.swayFreq * .5f) * dt;
            l.rot += l.spin * dt;
            if (l.y < LeafBottom || l.x > 1000f || l.x < -1000f) Respawn(l, false);
            float fadeIn = Mathf.Clamp01((LeafTop - l.y) / 60f);
            float fadeOut = Mathf.Clamp01((l.y - LeafBottom) / 90f);
            var c = l.col; c.a = .9f * fadeIn * fadeOut; l.im.color = c;
            l.rt.anchoredPosition = new Vector2(l.x, l.y);
            float flip = Mathf.Cos(t * 2.4f + l.phase);                                // nhào lộn 3D
            l.rt.localScale = new Vector3(Mathf.Sign(flip) * Mathf.Max(.25f, Mathf.Abs(flip)), 1f, 1f);
            l.rt.localRotation = Quaternion.Euler(0, 0, l.rot + Mathf.Sin(t * l.swayFreq + l.phase) * 35f);
        }
    }

    // ---------- tiện ích ----------
    static RectTransform Group(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f); rt.pivot = new Vector2(.5f, .5f);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = Vector2.zero;
        return rt;
    }

    static float Range(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }
}

/// <summary>
/// Chia ảnh thành lưới và dịch đỉnh theo gió. Trọng số theo độ cao (tán cây lay, gốc đứng yên)
/// và (tuỳ chọn) theo khoảng cách tới mép ảnh (thân cây nằm sát mép).
/// </summary>
[RequireComponent(typeof(Image))]
public class TreeWarp : BaseMeshEffect
{
    public float yFull = 100f, yZero = 160f, xLo = 0f, xHi = 0f, amp = 6f, phase;
    const int Nx = 60, Ny = 34;
    const float SrcW = 480f, SrcH = 270f;
    static readonly List<UIVertex> tmp = new List<UIVertex>();

    public void Refresh() { if (graphic != null) graphic.SetVerticesDirty(); }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount < 4) return;
        tmp.Clear(); vh.GetUIVertexStream(tmp);
        Vector2 pMin = tmp[0].position, pMax = pMin, uMin = tmp[0].uv0, uMax = uMin;
        Color32 col = tmp[0].color;
        for (int i = 1; i < tmp.Count; i++)
        {
            Vector2 p = tmp[i].position, u = tmp[i].uv0;
            pMin = Vector2.Min(pMin, p); pMax = Vector2.Max(pMax, p);
            uMin = Vector2.Min(uMin, u); uMax = Vector2.Max(uMax, u);
        }
        float t = Time.unscaledTime, wind = MenuNatureFx.Wind(t);
        vh.Clear();
        for (int j = 0; j <= Ny; j++)
            for (int i = 0; i <= Nx; i++)
            {
                float u = i / (float)Nx, v = j / (float)Ny;                 // v = 0 ở đáy
                float ypx = (1f - v) * SrcH, xpx = Mathf.Min(u, 1f - u) * SrcW;
                float wy = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(yFull, yZero, ypx));
                float wx = xHi > xLo ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(xLo, xHi, xpx)) : 1f;
                float wgt = wy * wx;
                float s = .6f * Mathf.Sin(t * 1.1f + u * 9f + v * 4f + phase) + .4f * Mathf.Sin(t * 2.3f + u * 40f - v * 30f + phase * 2f);
                float dx = amp * wgt * wind * s;
                float dy = .3f * amp * wgt * wind * Mathf.Sin(t * 1.7f + u * 14f + v * 7f + phase);
                var vert = UIVertex.simpleVert;
                vert.position = new Vector3(Mathf.Lerp(pMin.x, pMax.x, u) + dx, Mathf.Lerp(pMin.y, pMax.y, v) + dy, 0f);
                vert.uv0 = new Vector2(Mathf.Lerp(uMin.x, uMax.x, u), Mathf.Lerp(uMin.y, uMax.y, v));
                vert.color = col;
                vh.AddVert(vert);
            }
        int stride = Nx + 1;
        for (int j = 0; j < Ny; j++)
            for (int i = 0; i < Nx; i++)
            {
                int a = j * stride + i;
                vh.AddTriangle(a, a + stride, a + stride + 1);
                vh.AddTriangle(a, a + stride + 1, a + 1);
            }
    }
}
