using UnityEngine;

/// <summary>
/// Vật liệu + render pixel-perfect. Thế giới game được render vào RenderTexture 480x300
/// (đúng 1 texel = 1 pixel-art) rồi phóng to bằng Point filter, giống bản HTML.
/// Material Additive dùng cho mọi thứ phát sáng (lửa, laze, nổ, đuốc).
/// </summary>
public static class Gfx
{
    public const int W = 480, H = 300;
    static Material add, alpha;

    public static Material Additive
    {
        get
        {
            if (add != null) return add;
            var sh = Resources.Load<Shader>("BioSpriteAdditive");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh == null) return Alpha;
            add = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            return add;
        }
    }

    public static Material Alpha
    {
        get
        {
            if (alpha != null) return alpha;
            var go = new GameObject("tmp"); var sr = go.AddComponent<SpriteRenderer>(); alpha = sr.sharedMaterial;
            Object.Destroy(go); return alpha;
        }
    }

    public static void SetAdd(SpriteRenderer sr, bool on) { if (sr != null) sr.sharedMaterial = on ? Additive : Alpha; }

    /// <summary>Sprite phát sáng (cộng màu) gắn vào object cha.</summary>
    public static SpriteRenderer Glow(Transform parent, Vector2 localPos, float diameter, Color c, int order, bool flicker = false)
    {
        var go = new GameObject("Glow"); go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * (diameter / 4f);          // sprite Glow = 4 đơn vị
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = ProcSprites.Glow; sr.color = c; sr.sortingOrder = order; SetAdd(sr, true);
        if (flicker) go.AddComponent<FxFlicker>();
        return sr;
    }

    public static RenderTexture CreateWorldRT()
    {
        var rt = new RenderTexture(W, H, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, antiAliasing = 1, wrapMode = TextureWrapMode.Clamp, name = "BioWorldRT" };
        rt.Create(); return rt;
    }
}
