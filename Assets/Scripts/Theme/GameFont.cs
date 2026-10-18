using UnityEngine;

/// <summary>
/// Font dùng chung cho UnityEngine.UI.Text.
/// Display: Resources/Fonts/Display.ttf (Philosopher) cho tiêu đề/nút; nếu không load được thì dùng Body.
/// Body: font hệ thống có hỗ trợ tiếng Việt, người chơi chọn trong Cài đặt (Choice) + cỡ chữ (ScaleIndex), lưu PlayerPrefs.
/// </summary>
public static class GameFont
{
    const string KFont = "KHTN_Font", KScale = "KHTN_FontScale";

    /// <summary>Tên hiển thị trong Cài đặt. Thứ tự khớp với Os[].</summary>
    public static readonly string[] Names = { "MẶC ĐỊNH", "ARIAL", "TIMES NEW ROMAN", "VERDANA", "GEORGIA" };
    static readonly string[][] Os =
    {
        new[] { "Segoe UI", "Arial", "Roboto", "Noto Sans", "Helvetica Neue", "Helvetica" },
        new[] { "Arial", "Helvetica", "Roboto", "Noto Sans" },
        new[] { "Times New Roman", "Georgia", "Noto Serif", "Times" },
        new[] { "Verdana", "Tahoma", "Segoe UI", "DejaVu Sans" },
        new[] { "Georgia", "Times New Roman", "Noto Serif", "Times" }
    };

    public static readonly string[] ScaleNames = { "NHỎ", "VỪA", "LỚN" };
    static readonly float[] Scales = { .9f, 1f, 1.15f };

    static Font _body, _display;

    public static int Choice
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(KFont, 0), 0, Names.Length - 1); }
        set { PlayerPrefs.SetInt(KFont, Mathf.Clamp(value, 0, Names.Length - 1)); _body = null; }
    }

    public static int ScaleIndex
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(KScale, 1), 0, Scales.Length - 1); }
        set { PlayerPrefs.SetInt(KScale, Mathf.Clamp(value, 0, Scales.Length - 1)); }
    }

    /// <summary>Hệ số nhân cỡ chữ (UIKit.Label nhân vào mọi nhãn mới dựng).</summary>
    public static float Scale { get { return Scales[ScaleIndex]; } }

    public static float ScaleOf(int index) { return Scales[Mathf.Clamp(index, 0, Scales.Length - 1)]; }

    public static Font Body
    {
        get
        {
            if (_body != null) return _body;
            _body = Font.CreateDynamicFontFromOSFont(Os[Choice], 48);
            if (_body == null) _body = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _body;
        }
    }

    public static Font Display
    {
        get
        {
            if (_display != null) return _display;
            _display = Resources.Load<Font>("Fonts/Display");
            if (_display == null)
            {
                _display = Font.CreateDynamicFontFromOSFont(Os[0], 48);
                if (_display == null) _display = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return _display;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _body = null; _display = null; }
}
