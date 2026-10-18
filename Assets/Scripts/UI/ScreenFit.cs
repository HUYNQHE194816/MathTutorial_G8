using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Giai đoạn 1 của bản responsive cho điện thoại. KHÔNG cần gắn vào scene: tự khởi tạo khi mở game
/// và sống xuyên mọi scene (DontDestroyOnLoad).
///
/// Việc làm:
///  1. Cho phép xoay ngang lẫn dọc (đặt AllowPortrait = false nếu muốn khóa chỉ ngang).
///  2. Đổi mọi CanvasScaler (ScaleWithScreenSize) sang chế độ Expand: vùng thiết kế 1920x1080 luôn nằm TRỌN trong màn hình,
///     ở cả ngang lẫn dọc, màn dài 20:9, tablet 4:3 và cửa sổ PC bị kéo méo. Tỉ lệ 16:9 thì giống hệt trước đây.
///  3. Camera của RoomScene (tự luận) tự nới orthographicSize khi màn hình hẹp hơn thiết kế (màn dọc) để thấy hết phòng.
///     Scene Sinh (boss) đã tự letterbox RenderTexture trong BossFightUI.FitWorld nên không cần xử lý thêm.
///  4. Trên thiết bị di động: khóa 60 FPS, không cho tắt màn hình khi đang chơi.
///
/// Các script khác có thể dùng ScreenFit.IsPortrait và sự kiện ScreenFit.Changed (bắn khi kích thước màn hình đổi).
/// </summary>
public class ScreenFit : MonoBehaviour
{
    /// <summary>false: chỉ cho xoay ngang.</summary>
    public static bool AllowPortrait = true;

    const float RoomBaseSize = 5.4f;        // orthographicSize gốc của RoomManager
    const float RoomHalfWidth = 9.3f;       // nửa bề rộng cần nhìn thấy (khung phòng rộng 17.5 + lề)
    const float ScanInterval = .25f;        // quét canvas mới được dựng bằng code mỗi 0.25s

    public static bool IsPortrait => Screen.height > Screen.width;
    public static event Action Changed;

    static ScreenFit instance;
    int lastW, lastH;
    float scanT;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("ScreenFit");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ScreenFit>();
    }

    void Awake()
    {
        ApplyOrientation();
        if (Application.isMobilePlatform)
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }

    static void ApplyOrientation()
    {
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortrait = AllowPortrait;
        Screen.autorotateToPortraitUpsideDown = AllowPortrait;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }

    void LateUpdate()
    {
        bool resized = Screen.width != lastW || Screen.height != lastH;
        if (resized)
        {
            lastW = Screen.width; lastH = Screen.height;
            ApplyScalers();
            Changed?.Invoke();
        }

        scanT -= Time.unscaledDeltaTime;
        if (scanT <= 0f) { scanT = ScanInterval; ApplyScalers(); }

        FitRoomCamera();
    }

    static void ApplyScalers()
    {
        var all = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var s = all[i];
            if (s == null || s.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
            if (s.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand) s.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }
    }

    static void FitRoomCamera()
    {
        // RoomManager dùng camera theo người chơi (RFxHost) và tự tính kích thước pixel-perfect có bù màn hình hẹp,
        // nên không ghi orthographicSize ở đây nữa để hai nơi không giành quyền điều khiển camera.
        return;
    }
}
