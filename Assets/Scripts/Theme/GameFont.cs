using UnityEngine;

/// <summary>
/// Font dùng chung cho UnityEngine.UI.Text.
/// Display: Resources/Fonts/Display.ttf (Philosopher) cho tiêu đề/nút; nếu không load được thì dùng Body.
/// Body: font hệ thống có hỗ trợ tiếng Việt (giống SinhMenuKit), fallback LegacyRuntime.
/// </summary>
public static class GameFont
{
    static Font _body, _display;

    public static Font Body
    {
        get
        {
            if (_body != null) return _body;
            _body = Font.CreateDynamicFontFromOSFont(
                new[] { "Segoe UI", "Arial", "Roboto", "Noto Sans", "Helvetica Neue", "Helvetica" }, 48);
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
            if (_display == null) _display = Body;
            return _display;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _body = null; _display = null; }
}
