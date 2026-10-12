using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Image))]
public class CellView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Sprites (không còn dùng - giao diện dark fantasy vẽ bằng LyGfx)")]
    public Sprite rockSprite;
    public Sprite dirtSprite;

    [Header("Icon con")]
    public GameObject questionMarkIcon; // vòng rune + chữ "?" (ô câu hỏi)
    public GameObject goalIcon;         // công chúa ở ô đích

    private Image background;
    private GridCellData data;
    private bool themed;
    private CanvasGroup cg;
    private Coroutine anim;
    private CellType lastType;
    private bool lastVisited;

    void Awake()
    {
        background = GetComponent<Image>();
    }

    public void Setup(GridCellData cellData)
    {
        data = cellData;
        bool first = !themed;
        EnsureTheme();

        Sprite s = LyGfx.Tile;
        if (data.type == CellType.Rock) s = LyGfx.Rock;
        else if (data.type == CellType.Dirt && data.isVisited) s = LyGfx.SolvedTile;   // ô câu hỏi đã giải

        background.sprite = s;
        background.color = Color.white;

        if (questionMarkIcon != null)
            questionMarkIcon.SetActive(data.type == CellType.Question && !data.isVisited);

        if (goalIcon != null)
            goalIcon.SetActive(data.type == CellType.Goal);

        if (first)
        {
            lastType = data.type; lastVisited = data.isVisited;
            PlayIntro();
        }
        else if (lastType != data.type || lastVisited != data.isVisited)
        {
            bool slam = data.type == CellType.Rock;   // hoá đá: đập mạnh; giải xong: nảy nhẹ
            lastType = data.type; lastVisited = data.isVisited;
            if (cg != null) cg.alpha = 1f;
            Run(PopRoutine(slam));
        }
    }

    // ------------------------------------------------------------------ hoạt ảnh ô
    void Run(IEnumerator r)
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(r);
    }

    /// <summary>Lúc vào màn: các ô bật lên lần lượt theo đường chéo từ ô xuất phát (có độ nảy).</summary>
    void PlayIntro()
    {
        cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        transform.localScale = Vector3.zero;
        Run(IntroRoutine((data.row + data.col) * .045f));
    }

    IEnumerator IntroRoutine(float delay)
    {
        for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
        const float dur = .38f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur, c1 = 1.70158f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);   // easeOutBack
            transform.localScale = Vector3.one * e;
            cg.alpha = Mathf.Clamp01(k * 2.5f);
            yield return null;
        }
        transform.localScale = Vector3.one;
        cg.alpha = 1f;
        anim = null;
    }

    IEnumerator PopRoutine(bool slam)
    {
        float from = slam ? 1.45f : 1.22f, dur = slam ? .28f : .35f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur, e = 1f - Mathf.Pow(1f - k, 3f);
            transform.localScale = Vector3.one * Mathf.Lerp(from, 1f, e);
            yield return null;
        }
        transform.localScale = Vector3.one;
        anim = null;
    }

    /// <summary>Đổi icon của prefab sang rune + rương dark fantasy (làm 1 lần cho mỗi ô).</summary>
    void EnsureTheme()
    {
        if (themed) return;
        themed = true;

        if (questionMarkIcon != null)
        {
            var im = questionMarkIcon.GetComponent<Image>();
            if (im != null) { im.sprite = LyGfx.Rune; im.color = Color.white; im.preserveAspect = true; im.raycastTarget = false; }

            var mark = UIKit.Label("Mark", questionMarkIcon.transform, "?", 56, GameTheme.GoldHi, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(100f, 100f), true, true, FontStyle.Bold);
            UIKit.Stretch(mark.rectTransform, 0f, 0f, 0f, 0f);

            questionMarkIcon.AddComponent<RunePulse>().Init(.06f, 2.2f);
        }

        if (goalIcon != null)
        {
            var im = goalIcon.GetComponent<Image>();
            if (im != null) { im.sprite = LyGfx.Princess; im.color = Color.white; im.preserveAspect = true; im.raycastTarget = false; }
            goalIcon.AddComponent<IdleSway>();
        }
    }

    // ------------------------------------------------------------------ đá rơi xuống ô
    /// <summary>Hiện ô là nền đá trống (chưa có đá) để GameManager cho tảng đá rơi xuống rồi gọi LandRock().</summary>
    public void ShowFloorOnly(GridCellData cellData)
    {
        data = cellData;
        EnsureTheme();
        if (cg != null) cg.alpha = 1f;
        background.sprite = LyGfx.Tile;
        background.color = Color.white;
        if (questionMarkIcon != null) questionMarkIcon.SetActive(false);
        if (goalIcon != null) goalIcon.SetActive(false);
    }

    /// <summary>Tảng đá chạm đất: ô thành đá chắn, nảy nhẹ một nhịp.</summary>
    public void LandRock()
    {
        background.sprite = LyGfx.Rock;
        lastType = CellType.Rock;
        lastVisited = data != null && data.isVisited;
        Run(LandRoutine());
    }

    IEnumerator LandRoutine()
    {
        const float dur = .22f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur;
            transform.localScale = new Vector3(1f + .10f * (1f - k), 1f - .12f * (1f - k), 1f);   // bẹp xuống rồi bật lại
            yield return null;
        }
        transform.localScale = Vector3.one;
        anim = null;
    }

    // ------------------------------------------------------------------ công chúa ăn mừng
    /// <summary>Công chúa reo mừng: giơ tay, nhảy cẫng, lắc lư (chạy tới hết scene).</summary>
    public void Celebrate()
    {
        if (goalIcon == null || !goalIcon.activeSelf) return;
        var sway = goalIcon.GetComponent<IdleSway>();
        if (sway != null) sway.enabled = false;
        StartCoroutine(CelebrateRoutine());
    }

    IEnumerator CelebrateRoutine()
    {
        var rt = (RectTransform)goalIcon.transform;
        var im = goalIcon.GetComponent<Image>();
        Vector2 home = rt.anchoredPosition;
        if (im != null) im.sprite = LyGfx.PrincessCheer;
        float t = 0f;
        while (rt != null)
        {
            t += Time.unscaledDeltaTime;
            float s = Mathf.Sin(t * 6.5f);
            float up = Mathf.Abs(s);
            float grow = Mathf.Min(1f, t * 4f);   // phóng to dần lúc bắt đầu để thấy rõ nét mặt
            rt.anchoredPosition = home + new Vector2(0f, up * 26f);
            float sq = up < .08f ? .93f : 1.04f;   // chạm đất thì bẹp nhẹ
            rt.localScale = new Vector3(Mathf.Lerp(1f, 1.28f, grow) / sq, Mathf.Lerp(1f, 1.28f, grow) * sq, 1f);
            rt.localEulerAngles = new Vector3(0f, 0f, s * 6f);
            yield return null;
        }
    }

    public void MarkVisited()
    {
        data.isVisited = true;
        if (questionMarkIcon != null)
            questionMarkIcon.SetActive(false);
    }

    public GridCellData Data => data;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (background != null) background.color = Color.white;
        GameManager.Instance?.OnCellClicked(data);
    }

    // Rê chuột vào ô kề nhân vật (đi được) -> ô ánh lên màu lửa, gợi ý nước đi hợp lệ.
    public void OnPointerEnter(PointerEventData eventData)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.player == null || data == null || background == null) return;
        if (data.type == CellType.Rock) return;
        var p = gm.player.CurrentPosition;
        if (Mathf.Abs(p.x - data.row) + Mathf.Abs(p.y - data.col) != 1) return;
        background.color = Color.Lerp(Color.white, GameTheme.EmberHi, .55f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (background != null) background.color = Color.white;
    }
}

/// <summary>Công chúa đứng chờ: lắc lư nhẹ và nhấp nhô lên xuống.</summary>
public class IdleSway : MonoBehaviour
{
    private RectTransform rt;
    private Vector2 home;
    private float phase;

    void Awake()
    {
        rt = (RectTransform)transform;
        phase = Random.value * 6.283f;
    }

    void Start() { home = rt.anchoredPosition; }

    void Update()
    {
        float t = Time.unscaledTime * 1.8f + phase;
        rt.anchoredPosition = home + new Vector2(0f, Mathf.Sin(t) * 2.5f);
        rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * .8f) * 3f);
    }
}

/// <summary>Nhịp "thở" nhẹ cho icon (phóng to thu nhỏ + sáng tối), dùng cho rune ô câu hỏi và rương.</summary>
public class RunePulse : MonoBehaviour
{
    private RectTransform rt;
    private Image img;
    private float amp, speed, phase;

    public void Init(float amplitude, float spd)
    {
        rt = (RectTransform)transform;
        img = GetComponent<Image>();
        amp = amplitude; speed = spd;
        phase = Random.value * 6.283f;
    }

    void Update()
    {
        if (rt == null) return;
        float k = .5f + .5f * Mathf.Sin(Time.unscaledTime * speed + phase);
        rt.localScale = Vector3.one * (1f + amp * k);
        if (img != null) { var c = img.color; c.a = .78f + .22f * k; img.color = c; }
    }
}
