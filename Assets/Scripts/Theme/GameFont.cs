using UnityEngine;

/// <summary>
/// Font dùng chung. Ưu tiên file font trong Assets/Resources/Fonts/Display và Fonts/Body
/// (nên dùng font OFL có đủ dấu tiếng Việt), nếu chưa có thì lấy font hệ điều hành.
/// Tự cảnh báo trong Console nếu font không có glyph tiếng Việt (ệ ữ ẳ ỡ).
/// </summary>
public static class GameFont
{
    static Font display, body;

    static readonly string[] DisplayOs = { "Georgia", "Palatino Linotype", "Cambria", "Times New Roman", "Noto Serif", "Serif" };
    static readonly string[] BodyOs = { "Segoe UI", "Roboto", "Noto Sans", "Arial", "Helvetica Neue", "Helvetica" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { display = body = null; }

    public static Font Display
    {
        get { if (display == null) display = Load("Fonts/Display", DisplayOs); return display; }
    }

    public static Font Body
    {
        get { if (body == null) body = Load("Fonts/Body", BodyOs); return body; }
    }

    static Font Load(string resPath, string[] osNames)
    {
        var f = Resources.Load<Font>(resPath);
        string source = "Resources/" + resPath;
        if (f == null)
        {
            f = Font.CreateDynamicFontFromOSFont(osNames, 48);
            source = "font hệ điều hành";
        }
        if (f != null)
        {
            f.RequestCharactersInTexture("\u1EC7\u1EEF\u1EB3\u1EE1", 48, FontStyle.Normal);
            if (!f.HasCharacter('\u1EC7') || !f.HasCharacter('\u1EEF'))
                Debug.LogWarning("[GameFont] " + resPath + " (" + source + ") thiếu glyph tiếng Việt. Hãy thêm font có đủ dấu vào Assets/Resources/" + resPath + ".ttf");
        }
        return f;
    }
}
