using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Một thẻ môn học: idle (nhấp nhô), hover (phóng to + glow), intro, chọn (pop + xoay), dismiss, shake.
/// Toàn bộ dùng unscaledTime nên vẫn chạy khi Time.timeScale = 0.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class SubjectCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Data")]
    public string subjectName = "Lý";
    [Tooltip("Tên scene cần load (phải có trong Build Settings). Bỏ trống nếu chưa có.")]
    public string sceneToLoad = "GameplayScene";
    public bool available = true;

    [Header("Visual refs")]
    [SerializeField] private Image glow;
    [SerializeField] private RectTransform icon;

    [Header("Idle / Hover")]
    [SerializeField] private float floatAmplitude = 10f;
    [SerializeField] private float floatSpeed = 1.6f;
    [SerializeField] private float hoverScale = 1.08f;

    public static bool InputLocked;
    public event Action<SubjectCard> Clicked;

    private RectTransform rt;
    private CanvasGroup group;
    private Vector2 basePos;
    private float phase;
    private bool hovered;
    private bool busy = true;   // true = Update không điều khiển thẻ (đang intro / animation)

    public RectTransform Rect => rt;
    public Vector2 BasePosition => basePos;

    private void Awake()
    {
        rt = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        basePos = rt.anchoredPosition;
        phase = UnityEngine.Random.value * Mathf.PI * 2f;

        group.alpha = 0f;
        rt.localScale = Vector3.zero;
        SetGlow(0f);
    }

    private void Update()
    {
        if (busy) return;

        float t = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        bool isHot = hovered && !InputLocked;

        rt.anchoredPosition = basePos + new Vector2(0f, Mathf.Sin(t * floatSpeed + phase) * floatAmplitude);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * floatSpeed * 0.7f + phase) * 1.2f);

        float k = 1f - Mathf.Exp(-12f * dt);
        float target = isHot ? hoverScale : 1f;
        rt.localScale = Vector3.Lerp(rt.localScale, Vector3.one * target, k);

        if (glow != null)
        {
            float pulse = isHot ? 0.85f + 0.15f * Mathf.Sin(t * 5f) : 0f;
            SetGlow(Mathf.Lerp(glow.color.a, pulse, k));
        }
        if (icon != null)
            icon.localScale = Vector3.Lerp(icon.localScale, Vector3.one * (isHot ? 1.12f : 1f), k);
    }

    // ---------- Pointer ----------
    public void OnPointerEnter(PointerEventData e) { if (!hovered && !busy && !InputLocked) GameAudio.Play(Snd.UiHover); hovered = true; }
    public void OnPointerExit(PointerEventData e) { hovered = false; }
    public void OnPointerClick(PointerEventData e)
    {
        if (InputLocked || busy) return;
        Clicked?.Invoke(this);
    }

    // ---------- Animations ----------
    public void PlayIntro(float delay) => StartCoroutine(IntroRoutine(delay));

    private IEnumerator IntroRoutine(float delay)
    {
        busy = true;
        yield return WaitUnscaled(delay);
        Vector2 from = basePos + new Vector2(0f, -260f);
        yield return Tween(0.65f, k =>
        {
            float e = EaseOutBack(k);
            rt.anchoredPosition = Vector2.LerpUnclamped(from, basePos, e);
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.5f, 1f, e);
            group.alpha = Mathf.Clamp01(k * 2.5f);
        });
        busy = false;
    }

    /// <summary>Thẻ được chọn: nén lại -> bật ra giữa màn hình -> xoay 360° + glow.</summary>
    public IEnumerator PlaySelected(Vector2 centerPos)
    {
        busy = true;
        transform.SetAsLastSibling();
        Vector2 start = rt.anchoredPosition;
        Vector3 s0 = rt.localScale;
        float z0 = Mathf.DeltaAngle(0f, rt.localEulerAngles.z);

        yield return Tween(0.12f, k => rt.localScale = Vector3.Lerp(s0, Vector3.one * 0.9f, k));

        yield return Tween(0.4f, k =>
        {
            rt.anchoredPosition = Vector2.Lerp(start, centerPos, EaseOutCubic(k));
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.9f, 1.3f, EaseOutBack(k));
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(z0, 0f, k));
            SetGlow(k);
        });

        yield return Tween(0.7f, k =>
        {
            rt.localRotation = Quaternion.Euler(0f, 360f * EaseInOutCubic(k), 0f);
            if (icon != null) icon.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(Mathf.PI * k));
            SetGlow(0.8f + 0.2f * Mathf.Sin(k * Mathf.PI * 4f));
        });
        rt.localRotation = Quaternion.identity;
    }

    /// <summary>Thẻ không được chọn: mờ dần, thu nhỏ, trượt ra xa.</summary>
    public void PlayDismiss(Vector2 awayDir) => StartCoroutine(DismissRoutine(awayDir));

    private IEnumerator DismissRoutine(Vector2 awayDir)
    {
        busy = true;
        Vector2 start = rt.anchoredPosition;
        Vector3 s0 = rt.localScale;
        float a0 = group.alpha;
        yield return Tween(0.4f, k =>
        {
            float e = EaseOutCubic(k);
            rt.anchoredPosition = start + awayDir * 220f * e;
            rt.localScale = Vector3.Lerp(s0, Vector3.one * 0.6f, e);
            group.alpha = Mathf.Lerp(a0, 0f, k);
            SetGlow(0f);
        });
    }

    /// <summary>Rung nhẹ khi môn chưa có (Sinh).</summary>
    public void PlayShake() { if (!busy) StartCoroutine(ShakeRoutine()); }

    private IEnumerator ShakeRoutine()
    {
        busy = true;
        Vector2 start = rt.anchoredPosition;
        Vector3 s0 = rt.localScale;
        yield return Tween(0.45f, k =>
        {
            float x = Mathf.Sin(k * Mathf.PI * 8f) * (1f - k) * 22f;
            rt.anchoredPosition = start + new Vector2(x, 0f);
            rt.localScale = s0 * (1f - 0.05f * Mathf.Sin(k * Mathf.PI));
        });
        rt.anchoredPosition = start;
        busy = false;
    }

    // ---------- Helpers ----------
    private void SetGlow(float a)
    {
        if (glow == null) return;
        Color c = glow.color; c.a = a; glow.color = c;
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    private static IEnumerator Tween(float duration, Action<float> step)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            step(Mathf.Clamp01(t / duration));
            yield return null;
        }
        step(1f);
    }

    public static float EaseOutCubic(float k) => 1f - Mathf.Pow(1f - k, 3f);
    public static float EaseInOutCubic(float k) => k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;
    public static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
    }
}
