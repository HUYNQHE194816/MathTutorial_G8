using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Lớp áo" dark fantasy cho GameplayScene (game Vật lí). Không cần sửa scene bằng tay:
/// GameManager.Start() gọi Apply() một lần, hàm này
///   - dựng nền chung của game (SceneShell: gradient tối, sương, tàn lửa, vignette, fade),
///   - làm trong suốt 2 tấm nền cũ (LeftPanel / RightPanel), đặt khung sắt phía sau lưới,
///   - đổi màu thanh TopBar, điểm, đồng hồ, dòng hướng dẫn theo GameTheme,
///   - đổi nhân vật sang hiệp sĩ (LyGfx.KnightIdle; PlayerController tự đổi khung hình đi bộ / ăn mừng).
/// Popup câu hỏi do QuestionPopup tự phủ theme; ô lưới do CellView + LyGfx đảm nhiệm.
/// </summary>
public static class LyThemeSkin
{
    public static Color TimerNormal => GameTheme.EmberHi;
    public static Color TimerDanger => Color.Lerp(GameTheme.Blood, Color.white, .25f);

    public static void Apply(Canvas sceneCanvas, TMP_Text score, TMP_Text timer)
    {
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = GameTheme.Ink; }

        SceneShell.Create(ShellMood.Study, null, 1f, false, true);

        if (sceneCanvas == null) return;
        sceneCanvas.sortingOrder = 10;   // phải >= 10 để nằm trên vignette (5) của SceneShell
        var root = sceneCanvas.transform;

        // ---- 2 tấm nền cũ (xanh navy / gỗ) -> trong suốt, để lộ nền dark fantasy phía sau ----
        var left = root.Find("LeftPanel");
        var right = root.Find("RightPanel");
        MakeClear(left);
        MakeClear(right);

        // ---- khung sắt đặt SAU lưới (lưới và TopBar vẽ đè lên) ----
        if (right != null && right.Find("GridContainer") != null)
        {
            var frame = UIKit.Panel(right, "GridFrame", Vector2.zero, new Vector2(100f, 100f), PanelStyle.Dark, out _);
            UIKit.Stretch(frame, 0f, 116f, 0f, 0f);   // trùng vùng GridContainer (chừa 120px cho TopBar)
            frame.GetComponent<Image>().raycastTarget = false;
            frame.SetAsFirstSibling();
        }

        // ---- TopBar: tên học sinh + điểm ----
        var bar = right != null ? right.Find("TopBar") : null;
        if (bar != null)
        {
            var im = bar.GetComponent<Image>();
            if (im != null) { im.sprite = null; im.color = GameTheme.Night.WithAlpha(.96f); }
            UIKit.Box("EmberLine", bar, GameTheme.Ember.WithAlpha(.75f), new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(.5f, 0f), Vector2.zero, new Vector2(0f, 3f));
            Style(Tmp(bar.Find("PlayerNameText")), GameTheme.Bone, false);
        }
        Style(score, GameTheme.GoldHi, true);

        // ---- Nhân vật: thợ mỏ -> hiệp sĩ ----
        var tok = root.Find("PlayerToken");   // nằm trực tiếp dưới Canvas
        if (tok != null)
        {
            var ti = tok.GetComponent<Image>();
            if (ti != null) { ti.sprite = LyGfx.KnightIdle; ti.color = Color.white; ti.preserveAspect = true; ti.raycastTarget = false; }
        }

        // ---- Đồng hồ + dòng hướng dẫn ----
        Style(timer, TimerNormal, true);
        var hint = Tmp(left != null ? left.Find("Instruction") : null);
        if (hint != null)
        {
            hint.color = GameTheme.Fog; hint.fontStyle = FontStyles.Italic;
            hint.text = TouchControls.Active ? "Trả lời đúng để mở đường • Dùng 4 nút mũi tên để di chuyển" : "Trả lời đúng để mở đường • WASD / phím mũi tên để di chuyển";
        }
    }

    // ------------------------------------------------------------------ tiện ích
    static void MakeClear(Transform t)
    {
        if (t == null) return;
        var im = t.GetComponent<Image>();
        if (im == null) return;
        im.sprite = null;
        im.color = new Color(0f, 0f, 0f, 0f);
    }

    static TMP_Text Tmp(Transform t) => t != null ? t.GetComponent<TMP_Text>() : null;

    static void Style(TMP_Text t, Color c, bool outline)
    {
        if (t == null) return;
        t.color = c;
        t.fontStyle = FontStyles.Bold;
        if (outline) { t.outlineWidth = .22f; t.outlineColor = new Color32(14, 11, 20, 255); }
    }
}
