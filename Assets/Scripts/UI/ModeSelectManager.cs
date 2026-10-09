using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Màn chọn chế độ sau nút BẮT ĐẦU của MainMenu: TRẮC NGHIỆM (-> SubjectSelectScene, 3 môn) hoặc TỰ LUẬN (chưa có scene).
/// Toàn bộ UI dựng bằng code, nền dùng lại tranh pixel + hiệu ứng thiên nhiên của MainMenu.
/// </summary>
public class ModeSelectManager : MonoBehaviour
{
    [SerializeField] string menuSceneName = "MainMenu";
    [SerializeField] string multipleChoiceScene = "SubjectSelectScene";
    [Tooltip("Tên scene Tự luận. Để trống = chưa có, nút hiện 'SẮP RA MẮT'. Khi có scene thì điền tên (vd PhongAnScene) và thêm vào Build Settings.")]
    [SerializeField] string essayScene = "";

    const float SceneW = 1920f, SceneH = 1080f;

    RectTransform artRoot, scene, ui;
    MenuNatureFx nature;
    Vector2 lastRect;
    bool leaving;

    void Start()
    {
        Time.timeScale = 1f;
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = GameTheme.Ink; }

        UIKit.EnsureEventSystem();
        SceneShell.Create(ShellMood.Hub, null, 1f, true, false);

        artRoot = UIKit.BuildCanvas(transform, "ModeSelect Art", -90);
        scene = UIKit.CBox("Scene", artRoot, Color.clear, Vector2.zero, new Vector2(SceneW, SceneH));
        BuildBackdrop();

        ui = UIKit.BuildCanvas(transform, "ModeSelect UI", 10);
        BuildUi();
        FitScene();
    }

    // ---------- nền ----------
    void BuildBackdrop()
    {
        nature = new MenuNatureFx();
        nature.BuildForest(scene, 250f, 20);
    }

    // ---------- giao diện ----------
    void BuildUi()
    {
        UIKit.Title(ui, "CHỌN CHẾ ĐỘ", new Vector2(0, 410), 76);

        BuildCard(-450f, "TRẮC NGHIỆM",
            "Chọn môn Lý – Hóa – Sinh\nvà vượt ải bằng những câu hỏi\nnhiều lựa chọn.",
            "VÀO CHƠI", ButtonStyle.Bronze, false, OpenMultipleChoice);

        bool hasEssay = !string.IsNullOrEmpty(essayScene);
        BuildCard(450f, "TỰ LUẬN",
            "Viết lời giải từng bước\nđể phá phong ấn của boss.",
            hasEssay ? "VÀO CHƠI" : "SẮP RA MẮT", hasEssay ? ButtonStyle.Bronze : ButtonStyle.Stone, !hasEssay, OpenEssay);

        UIKit.MakeButton(ui, "Btn_Back", "QUAY LẠI", new Vector2(-700, -450), new Vector2(320, 76), Back, ButtonStyle.Stone, 32, .3f);
    }

    void BuildCard(float x, string title, string desc, string btnLabel, ButtonStyle style, bool dim, System.Action onClick)
    {
        var panel = UIKit.Panel(ui, "Card_" + title, new Vector2(x, -10), new Vector2(700, 600), PanelStyle.Dark, out var c);
        UIKit.Label("T", c, title, 58, GameTheme.GoldHi, TextAnchor.MiddleCenter, new Vector2(0, 190), new Vector2(620, 80), true, true, FontStyle.Bold);
        UIKit.Divider(c, new Vector2(0, 135), 420);
        UIKit.Label("D", c, desc, 34, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 10), new Vector2(580, 220));
        UIKit.MakeButton(c, "Btn", btnLabel, new Vector2(0, -190), new Vector2(420, 88), onClick, style, 38, .1f);
        if (dim) panel.gameObject.AddComponent<CanvasGroup>().alpha = .72f;
    }

    // ---------- hành vi ----------
    void OpenMultipleChoice()
    {
        if (leaving) return;
        leaving = true;
        SceneRouter.Go(multipleChoiceScene);
    }

    void OpenEssay()
    {
        if (leaving) return;
        if (string.IsNullOrEmpty(essayScene) || !Application.CanStreamedLevelBeLoaded(essayScene))
        {
            UIKit.Toast(ui, "Chế độ Tự luận đang được xây dựng");
            return;
        }
        leaving = true;
        SceneRouter.Go(essayScene);
    }

    void Back()
    {
        if (leaving) return;
        leaving = true;
        SceneRouter.Go(menuSceneName);
    }

    // ---------- cập nhật ----------
    void FitScene() { MenuNatureFx.FitCover(artRoot, scene, ref lastRect); }

    void Update()
    {
        FitScene();
        if (nature != null) nature.Tick(Time.unscaledTime, Time.unscaledDeltaTime);
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
    }

    void OnDestroy() { if (nature != null) nature.Dispose(); }
}
