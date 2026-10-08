using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene xem thử design system. Tạo scene trống, thêm 1 GameObject, gắn component này, bấm Play.
/// (Muốn thử nút "CHUYỂN CẢNH" thì thêm scene này vào Build Settings.)
/// Dùng để duyệt: màu, font tiếng Việt, panel, nút, thanh tiến độ, thông báo, nền sương + tàn lửa, fade.
/// </summary>
public class ThemePreview : MonoBehaviour
{
    Image barLy, barHoa, barSinh; float t;
    Transform root;

    void Start()
    {
        Time.timeScale = 1f;
        if (Camera.main == null)
        {
            var cg = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); cg.tag = "MainCamera";
            var cam = cg.GetComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = GameTheme.Ink;
        }
        UIKit.EnsureEventSystem();
        SceneShell.Create(ShellMood.Menu);

        var r = UIKit.BuildCanvas(transform, "Preview Canvas", 10); root = r;
        UIKit.Title(r, "MÀN SƯƠNG LÃNG QUÊN", new Vector2(0, 430), 76);
        UIKit.Label("Sub", r, "Thử font tiếng Việt: Ký ức · Hiệp sĩ · Ệ Ữ Ằ Ẳ Ỡ Ợ Ứ", 30, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, 335), new Vector2(1500, 50));

        // Panel giấy da
        UIKit.Panel(r, "PaperPanel", new Vector2(-470, 40), new Vector2(760, 480), PanelStyle.Parchment, out var paper);
        UIKit.Label("H", paper, "Trang Nhật Ký", 44, GameTheme.PaperInk, TextAnchor.UpperLeft, new Vector2(0, 150), new Vector2(660, 70), true, false, FontStyle.Bold);
        UIKit.Label("B", paper, "Sương Lãng Quên không đốt sách. Nó khiến người ta tin rằng mình không học nổi.\n\nThầy Cú: \"Sai không phải là hết. Sai là dữ liệu.\"",
            30, GameTheme.PaperMuted, TextAnchor.UpperLeft, new Vector2(0, 10), new Vector2(660, 240));

        // Panel tối + 3 thanh môn
        UIKit.Panel(r, "DarkPanel", new Vector2(470, 40), new Vector2(760, 480), PanelStyle.Dark, out var dark);
        UIKit.Label("H", dark, "Độ thuộc bài", 44, GameTheme.EmberHi, TextAnchor.UpperLeft, new Vector2(0, 150), new Vector2(660, 70), true, false, FontStyle.Bold);
        MakeRow(dark, "Vật lí", GameTheme.Ly, 50, out barLy);
        MakeRow(dark, "Hóa học", GameTheme.Hoa, -40, out barHoa);
        MakeRow(dark, "Sinh học", GameTheme.Sinh, -130, out barSinh);

        // Hàng nút
        UIKit.MakeButton(r, "BtnPrimary", "BẮT ĐẦU", new Vector2(-540, -330), new Vector2(320, 86), () => UIKit.Toast(r, "Nút chính"), ButtonStyle.Primary, 34, .0f);
        UIKit.MakeButton(r, "BtnScene", "CHUYỂN CẢNH", new Vector2(-180, -330), new Vector2(320, 86), () => SceneRouter.Go(SceneManager.GetActiveScene().name), ButtonStyle.Secondary, 30, .08f);
        UIKit.MakeButton(r, "BtnToast", "THÔNG BÁO", new Vector2(180, -330), new Vector2(320, 86), () => UIKit.Toast(r, "Sai không phải là hết."), ButtonStyle.Secondary, 30, .16f);
        UIKit.MakeButton(r, "BtnDanger", "THOÁT", new Vector2(540, -330), new Vector2(320, 86), () => UIKit.Toast(r, "Nút nguy hiểm"), ButtonStyle.Danger, 34, .24f);
    }

    void MakeRow(RectTransform parent, string label, Color accent, float y, out Image fill)
    {
        UIKit.Label("L", parent, label, 32, accent, TextAnchor.MiddleLeft, new Vector2(-250, y), new Vector2(180, 40), false, false, FontStyle.Bold);
        UIKit.ProgressBar(parent, "Bar_" + label, new Vector2(60, y), new Vector2(400, 28), accent, out fill);
    }

    void Update()
    {
        t += Time.deltaTime;
        UIKit.SetBar(barLy, .55f + .4f * Mathf.Sin(t * .9f));
        UIKit.SetBar(barHoa, .45f + .4f * Mathf.Sin(t * .7f + 1.3f));
        UIKit.SetBar(barSinh, .5f + .4f * Mathf.Sin(t * .6f + 2.6f));
    }
}
