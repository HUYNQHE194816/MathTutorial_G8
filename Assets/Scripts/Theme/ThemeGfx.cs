using UnityEngine;

/// <summary>
/// Sprite dựng bằng code dùng chung cho UIKit / SceneShell / MainMenuScreen.
/// Pixel / Glow / Vignette tái dùng từ ProcSprites; Gradient là nền dọc tím -> đen (nên Image dùng nó phải để màu trắng).
/// </summary>
public static class ThemeGfx
{
    static Sprite _gradient;

    /// <summary>Sprite 1x1 trắng (ô màu phẳng).</summary>
    public static Sprite Pixel => ProcSprites.Pixel;

    /// <summary>Quầng sáng tròn mờ dần ra ngoài (trắng, alpha giảm dần).</summary>
    public static Sprite Glow => ProcSprites.Glow;

    /// <summary>Viền tối bo quanh màn hình.</summary>
    public static Sprite Vignette => ProcSprites.Vignette;

    /// <summary>Gradient dọc: đỉnh GameTheme.Violet -> đáy GameTheme.Ink.</summary>
    public static Sprite Gradient
    {
        get
        {
            if (_gradient != null) return _gradient;
            const int h = 128;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (int y = 0; y < h; y++)
            {
                float t = y / (h - 1f);                       // y=0 là đáy
                Color c = Color.Lerp(GameTheme.Ink, GameTheme.Violet, t * t);
                tex.SetPixel(0, y, c); tex.SetPixel(1, y, c);
            }
            tex.Apply();
            _gradient = Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(.5f, .5f), 100f);
            _gradient.hideFlags = HideFlags.HideAndDontSave;
            return _gradient;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _gradient = null; }
}
