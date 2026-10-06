using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Hiệu ứng cho nút UI: hover phóng to, nhấn thì co lại, và (tuỳ chọn) đập nhịp nhẹ để gọi chú ý.
/// Dùng unscaledTime nên vẫn chạy khi Time.timeScale = 0.
/// </summary>
public class AnimatedButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float pressScale = 0.93f;
    [SerializeField] private float pulseAmount = 0.035f;
    [SerializeField] private float pulseSpeed = 4f;

    /// <summary>Bật để nút đập nhịp khi không hover/nhấn.</summary>
    public bool Pulse { get; set; }

    /// <summary>Tắt để nút đứng yên (ví dụ khi đang xử lý).</summary>
    public bool FxEnabled { get; set; } = true;

    private RectTransform rt;
    private bool hovered;
    private bool pressed;

    private void Awake()
    {
        rt = (RectTransform)transform;
    }

    private void OnDisable()
    {
        hovered = pressed = false;
        if (rt != null) rt.localScale = Vector3.one;
    }

    private void Update()
    {
        float target = 1f;
        if (FxEnabled)
        {
            if (pressed) target = pressScale;
            else if (hovered) target = hoverScale;
            else if (Pulse) target = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
        }

        float k = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
        rt.localScale = Vector3.Lerp(rt.localScale, Vector3.one * target, k);
    }

    public void OnPointerEnter(PointerEventData e) { if (!hovered && FxEnabled) GameAudio.Play(Snd.UiHover); hovered = true; }
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) { pressed = true; if (FxEnabled) GameAudio.Play(Snd.UiClick); }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
}
