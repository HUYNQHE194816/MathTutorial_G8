using UnityEngine;

/// <summary>Sprite sinh bằng code (không cần asset): điểm ảnh, quầng sáng, vignette, gradient nền. Được cache.</summary>
public static class ThemeGfx
{
    static Sprite pixel, glow, vignette, gradient;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { pixel = glow = vignette = gradient = null; }

    static float Sm(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static Sprite Make(Texture2D t, bool point = false)
    {
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), 100f);
    }

    public static Sprite Pixel
    {
        get
        {
            if (pixel == null)
            {
                var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                t.SetPixel(0, 0, Color.white);
                pixel = Make(t, true);
            }
            return pixel;
        }
    }

    /// <summary>Quầng sáng tròn mềm (trắng, alpha giảm dần ra rìa). Nhuộm màu bằng Image.color.</summary>
    public static Sprite Glow
    {
        get
        {
            if (glow == null)
            {
                const int n = 128;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x / (n - 1f) - .5f) * 2f, dy = (y / (n - 1f) - .5f) * 2f;
                        float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                        float a = Mathf.Pow(1f - d, 2f);
                        t.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                glow = Make(t);
            }
            return glow;
        }
    }

    /// <summary>Viền tối bốn góc (đen, alpha tăng ra rìa). Phủ toàn màn hình sau UI.</summary>
    public static Sprite Vignette
    {
        get
        {
            if (vignette == null)
            {
                const int n = 256;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x / (n - 1f) - .5f) * 2f, dy = (y / (n - 1f) - .5f) * 2f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                        float a = Sm(.4f, 1f, d) * .78f;
                        t.SetPixel(x, y, new Color(GameTheme.Ink.r, GameTheme.Ink.g, GameTheme.Ink.b, a));
                    }
                vignette = Make(t);
            }
            return vignette;
        }
    }

    /// <summary>Gradient dọc: đỉnh tím tối -> đáy đen tím.</summary>
    public static Sprite Gradient
    {
        get
        {
            if (gradient == null)
            {
                const int h = 256;
                var t = new Texture2D(2, h, TextureFormat.RGBA32, false);
                for (int y = 0; y < h; y++)
                {
                    float k = y / (h - 1f);   // 0 = đáy, 1 = đỉnh
                    var c = Color.Lerp(GameTheme.Ink, GameTheme.Violet, Mathf.Pow(k, 1.4f));
                    t.SetPixel(0, y, c); t.SetPixel(1, y, c);
                }
                gradient = Make(t);
            }
            return gradient;
        }
    }
}
