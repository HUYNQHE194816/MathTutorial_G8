using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Scene đăng nhập cho học sinh lớp 8. Toàn bộ UI + animation dựng bằng code
/// (không DOTween, không prefab) nên nhẹ và không cần chỉnh tay trong Editor.
/// </summary>
public class LoginManager : MonoBehaviour
{
    [SerializeField] private Sprite background, sparkle;
    [SerializeField] private string nextScene = "MainMenu";

    static readonly Color Navy = new(.13f, .2f, .45f), Blue = new(.25f, .55f, 1f), Sun = new(1f, .8f, .2f),
        Green = new(.3f, .78f, .45f), DGreen = new(.2f, .6f, .33f), Red = new(.92f, .3f, .3f),
        FieldCol = new(.92f, .96f, 1f), FocusCol = new(1f, .97f, .82f);

    struct Floater { public RectTransform rt; public float x, y0, speed, phase, amp, spin; public bool twinkle; }

    readonly List<Floater> floaters = new();
    readonly List<RectTransform> items = new();
    Sprite round;
    RectTransform card, mascot, face, eyeL, eyeR;
    CanvasGroup fade;
    TMP_InputField nameIn, classIn;
    TMP_Text msg;
    Button loginBtn;
    Press loginPress;
    bool busy;

    // ---------------------------------------------------------------- lifecycle
    private void Awake()
    {
        round = MakeRound();
        Build();
    }

    private void Start()
    {
        nameIn.text = PlayerPrefs.GetString("student_name", "");
        classIn.text = PlayerPrefs.GetString("student_class", "");
        StartCoroutine(Intro());
        StartCoroutine(Blink());
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        foreach (var f in floaters)
        {
            if (f.twinkle)
            {
                f.rt.localScale = Vector3.one * (.55f + .45f * Mathf.Sin(t * 2.5f + f.phase));
                f.rt.localEulerAngles = new Vector3(0, 0, t * f.spin);
            }
            else
            {
                f.rt.anchoredPosition = new Vector2(f.x + Mathf.Sin(t * .6f + f.phase) * f.amp,
                    Mathf.Repeat(f.y0 + f.speed * t + 650f, 1300f) - 650f);
                f.rt.localEulerAngles = new Vector3(0, 0, Mathf.Sin(t * .5f + f.phase) * f.spin * 2f);
            }
        }
        // linh vật lắc lư nhẹ
        face.localEulerAngles = new Vector3(0, 0, Mathf.Sin(t * 1.8f) * 4f);
        face.localScale = new Vector3(1f, 1f + .03f * Mathf.Sin(t * 3.2f), 1f);
    }

    // ---------------------------------------------------------------- UI build
    void Build()
    {
        var cv = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cv.transform.SetParent(transform, false);
        cv.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var sc = cv.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = .5f;
        var root = (RectTransform)cv.transform;

        if (!FindFirstObjectByType<EventSystem>())
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        // nền
        var bg = Img("BG", root, Vector2.zero, Vector2.zero, background, background ? Color.white : new Color(.45f, .75f, 1f));
        if (background)
        {
            bg.rectTransform.sizeDelta = new Vector2(1920, 1080);
            var arf = bg.gameObject.AddComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            arf.aspectRatio = background.rect.width / background.rect.height;
        }
        else Stretch(bg.rectTransform);

        // ký hiệu toán học trôi lơ lửng
        string[] sy = { "+", "−", "×", "÷", "π", "√", "x²", "%", "=" };
        for (int i = 0; i < 16; i++)
        {
            var t = Txt("Sym", root, sy[i % sy.Length], Random.Range(70, 130), new Color(1, 1, 1, .35f), new Vector2(200, 200), Vector2.zero);
            floaters.Add(new Floater { rt = t.rectTransform, x = Random.Range(-900f, 900f), y0 = Random.Range(-650f, 650f), speed = Random.Range(20f, 50f), phase = Random.Range(0f, 6.28f), amp = Random.Range(20f, 60f), spin = Random.Range(-8f, 8f) });
        }
        // ngôi sao lấp lánh
        for (int i = 0; i < 10; i++)
        {
            var im = Img("Spark", root, new Vector2(Random.Range(40, 70), Random.Range(40, 70)), new Vector2(Random.Range(-900f, 900f), Random.Range(-500f, 500f)), sparkle ? sparkle : round, i % 2 == 0 ? Sun : Color.white);
            floaters.Add(new Floater { rt = im.rectTransform, phase = Random.Range(0f, 6.28f), spin = Random.Range(-30f, 30f), twinkle = true });
        }

        // thẻ đăng nhập
        card = RT("Card", root, new Vector2(760, 780), Vector2.zero);
        card.gameObject.AddComponent<CanvasGroup>();
        Img("Shadow", card, new Vector2(760, 780), new Vector2(0, -14), round, new Color(0, .1f, .3f, .25f));
        Img("Face", card, new Vector2(760, 780), Vector2.zero, round, Color.white);

        // linh vật
        mascot = RT("Mascot", card, new Vector2(170, 170), new Vector2(0, 390));
        face = Img("Head", mascot, new Vector2(170, 170), Vector2.zero, round, Sun).rectTransform;
        eyeL = Eye(face, -32); eyeR = Eye(face, 32);
        Img("Mouth", face, new Vector2(44, 20), new Vector2(0, -30), round, Navy);
        Img("CheekL", face, new Vector2(26, 26), new Vector2(-58, -14), round, new Color(1, .6f, .6f, .7f));
        Img("CheekR", face, new Vector2(26, 26), new Vector2(58, -14), round, new Color(1, .6f, .6f, .7f));

        var title = Txt("Title", card, "KHTN VUI LỚP 8", 58, Color.white, new Vector2(700, 80), new Vector2(0, 235));
        title.fontStyle = FontStyles.Bold;
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(Blue, Blue, Navy, Navy);
        var sub = Txt("Sub", card, "Đăng nhập để bắt đầu hành trình nhé!", 26, new Color(.4f, .45f, .6f), new Vector2(700, 40), new Vector2(0, 170));

        nameIn = Field("Tên của em là gì?", 60);
        classIn = Field("Em học lớp nào? (VD: 8A1)", -50);

        var btn = RT("Button", card, new Vector2(620, 108), new Vector2(0, -175));
        Img("Shadow", btn, new Vector2(620, 100), new Vector2(0, -8), round, DGreen);
        var bf = Img("Face", btn, new Vector2(620, 100), new Vector2(0, 4), round, Green);
        bf.raycastTarget = true;
        Txt("Label", btn, "BẮT ĐẦU HỌC", 42, Color.white, new Vector2(620, 100), new Vector2(0, 4)).fontStyle = FontStyles.Bold;
        loginBtn = btn.gameObject.AddComponent<Button>();
        loginBtn.targetGraphic = bf;
        loginBtn.transition = Selectable.Transition.None;
        loginBtn.onClick.AddListener(Login);
        loginPress = btn.gameObject.AddComponent<Press>();
        loginPress.pulse = true;

        msg = Txt("Msg", card, "", 28, Red, new Vector2(680, 44), new Vector2(0, -270));
        msg.fontStyle = FontStyles.Bold;
        var foot = Txt("Foot", card, "Học vui  •  Hiểu nhanh  •  Nhớ lâu", 24, new Color(.45f, .5f, .62f), new Vector2(700, 36), new Vector2(0, -335));

        items.AddRange(new[] { title.rectTransform, sub.rectTransform, (RectTransform)nameIn.transform, (RectTransform)classIn.transform, btn, msg.rectTransform, foot.rectTransform });

        // màn phủ đen để fade
        var fo = Img("Fade", root, Vector2.zero, Vector2.zero, null, Color.black);
        Stretch(fo.rectTransform);
        fade = fo.gameObject.AddComponent<CanvasGroup>();
        fade.alpha = 1f;
    }

    TMP_InputField Field(string placeholder, float y)
    {
        var bg = Img("Field", card, new Vector2(620, 84), new Vector2(0, y), round, FieldCol);
        bg.raycastTarget = true;
        var vp = RT("Viewport", bg.transform, Vector2.zero, Vector2.zero);
        Stretch(vp); vp.offsetMin = new Vector2(28, 8); vp.offsetMax = new Vector2(-28, -8);
        vp.gameObject.AddComponent<RectMask2D>();
        var tx = Txt("Text", vp, "", 34, Navy, Vector2.zero, Vector2.zero);
        Stretch(tx.rectTransform); tx.alignment = TextAlignmentOptions.MidlineLeft;
        var pl = Txt("Placeholder", vp, placeholder, 34, new Color(.5f, .55f, .68f), Vector2.zero, Vector2.zero);
        Stretch(pl.rectTransform); pl.alignment = TextAlignmentOptions.MidlineLeft; pl.fontStyle = FontStyles.Italic;

        bg.gameObject.SetActive(false); // gán tham chiếu xong mới bật để TMP_InputField khởi tạo đúng
        var f = bg.gameObject.AddComponent<TMP_InputField>();
        f.textViewport = vp; f.textComponent = tx; f.placeholder = pl; f.targetGraphic = bg;
        f.transition = Selectable.Transition.None;
        f.characterLimit = 24; f.customCaretColor = true; f.caretColor = Navy; f.caretWidth = 3;
        f.selectionColor = new Color(.25f, .55f, 1f, .35f);
        f.onSelect.AddListener(_ => bg.color = FocusCol);
        f.onDeselect.AddListener(_ => bg.color = FieldCol);
        f.onSubmit.AddListener(_ => Login());
        bg.gameObject.SetActive(true);
        return f;
    }

    RectTransform Eye(Transform p, float x)
    {
        var e = Img("Eye", p, new Vector2(46, 46), new Vector2(x, 14), round, Color.white);
        Img("Pupil", e.transform, new Vector2(24, 24), new Vector2(0, -2), round, Navy);
        return e.rectTransform;
    }

    // ---------------------------------------------------------------- logic
    void Login()
    {
        if (busy) return;
        string n = nameIn.text.Trim();
        if (n.Length == 0)
        {
            Say("Em hãy nhập tên nhé!", Red);
            StartCoroutine(Shake());
            nameIn.Select();
            return;
        }
        busy = true;
        loginPress.pulse = false;
        StudentProfileData.studentName = n;
        PlayerPrefs.SetString("student_name", n);
        PlayerPrefs.SetString("student_class", classIn.text.Trim());
        StartCoroutine(Success(n));
    }

    void Say(string s, Color c)
    {
        msg.text = s; msg.color = c;
        StartCoroutine(Tween(.25f, t => msg.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(.8f, 1f, Back(t))));
    }

    // ---------------------------------------------------------------- animation
    IEnumerator Intro()
    {
        var cg = card.GetComponent<CanvasGroup>();
        cg.alpha = 0; card.localScale = Vector3.one * .6f;
        StartCoroutine(Tween(.5f, t => fade.alpha = 1f - t));
        StartCoroutine(Reveal(mascot, .05f, 260f));
        for (int i = 0; i < items.Count; i++) StartCoroutine(Reveal(items[i], .25f + i * .09f, -45f));
        yield return Tween(.55f, t =>
        {
            cg.alpha = Mathf.Clamp01(t * 2f);
            card.localScale = Vector3.one * Mathf.LerpUnclamped(.6f, 1f, Back(t));
        });
        if (!Application.isMobilePlatform) { nameIn.Select(); nameIn.ActivateInputField(); }
    }

    IEnumerator Reveal(RectTransform r, float delay, float dy)
    {
        var g = r.GetComponent<CanvasGroup>();
        if (!g) g = r.gameObject.AddComponent<CanvasGroup>();
        var end = r.anchoredPosition;
        g.alpha = 0; r.anchoredPosition = end + Vector2.up * dy;
        yield return new WaitForSecondsRealtime(delay);
        yield return Tween(.55f, t =>
        {
            g.alpha = Mathf.Clamp01(t * 2.5f);
            r.anchoredPosition = end + Vector2.up * dy * (1f - Back(t));
        });
    }

    IEnumerator Blink()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(Random.Range(2f, 4f));
            yield return Tween(.14f, t =>
            {
                var s = new Vector3(1f, 1f - .9f * Mathf.Sin(t * Mathf.PI), 1f);
                eyeL.localScale = s; eyeR.localScale = s;
            });
        }
    }

    IEnumerator Shake()
    {
        var p0 = card.anchoredPosition;
        yield return Tween(.45f, t => card.anchoredPosition = p0 + Vector2.right * Mathf.Sin(t * 40f) * (1f - t) * 24f);
        card.anchoredPosition = p0;
    }

    IEnumerator Success(string n)
    {
        loginBtn.interactable = false;
        Say($"Chào {n}! Cùng khám phá Toán học nào!", Green);
        StartCoroutine(Burst());
        var p0 = mascot.anchoredPosition;
        yield return Tween(.5f, t => mascot.anchoredPosition = p0 + Vector2.up * Mathf.Sin(t * Mathf.PI) * 70f);
        yield return new WaitForSecondsRealtime(.5f);
        yield return Tween(.4f, t => fade.alpha = t);

        if (Application.CanStreamedLevelBeLoaded(nextScene)) SceneManager.LoadScene(nextScene);
        else
        {
            Debug.LogWarning($"[Login] Scene '{nextScene}' chưa có trong Build Settings.");
            fade.alpha = 0; busy = false; loginBtn.interactable = true; loginPress.pulse = true;
        }
    }

    IEnumerator Burst()
    {
        const int n = 18;
        var rts = new RectTransform[n]; var imgs = new Image[n]; var dirs = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            imgs[i] = Img("Burst", card, new Vector2(46, 46), new Vector2(0, 60), sparkle ? sparkle : round, i % 2 == 0 ? Sun : Blue);
            rts[i] = imgs[i].rectTransform;
            dirs[i] = Random.insideUnitCircle.normalized * Random.Range(250f, 450f);
        }
        yield return Tween(.9f, t =>
        {
            float e = 1f - Mathf.Pow(1f - t, 3f);
            for (int i = 0; i < n; i++)
            {
                rts[i].anchoredPosition = new Vector2(0, 60) + dirs[i] * e;
                rts[i].localEulerAngles = new Vector3(0, 0, t * 360f);
                rts[i].localScale = Vector3.one * (1f - t * .5f);
                var c = imgs[i].color; c.a = 1f - t; imgs[i].color = c;
            }
        });
        foreach (var r in rts) Destroy(r.gameObject);
    }

    static IEnumerator Tween(float d, System.Action<float> f)
    {
        for (float t = 0; t < d; t += Time.unscaledDeltaTime) { f(t / d); yield return null; }
        f(1f);
    }

    static float Back(float t) { t -= 1f; return 1f + 2.70158f * t * t * t + 1.70158f * t * t; }

    // ---------------------------------------------------------------- helpers
    static RectTransform RT(string n, Transform p, Vector2 size, Vector2 pos)
    {
        var r = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(p, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.sizeDelta = size; r.anchoredPosition = pos;
        return r;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
    }

    Image Img(string n, Transform p, Vector2 size, Vector2 pos, Sprite s, Color c)
    {
        var i = RT(n, p, size, pos).gameObject.AddComponent<Image>();
        i.sprite = s; i.color = c; i.raycastTarget = false;
        i.type = s == round && s != null ? Image.Type.Sliced : Image.Type.Simple;
        return i;
    }

    static TMP_Text Txt(string n, Transform p, string s, float size, Color col, Vector2 box, Vector2 pos)
    {
        var t = RT(n, p, box, pos).gameObject.AddComponent<TextMeshProUGUI>();
        t.text = s; t.fontSize = size; t.color = col;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Sprite bo tròn 9-slice tạo bằng code (thẻ, ô nhập, nút, hình tròn).</summary>
    static Sprite MakeRound()
    {
        const int N = 64;
        var tx = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x + .5f - N / 2f) - 0f), dy = Mathf.Abs(y + .5f - N / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(N / 2f - d + .5f) * 255));
            }
        tx.SetPixels32(px); tx.Apply();
        return Sprite.Create(tx, new Rect(0, 0, N, N), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(31, 31, 31, 31));
    }

    /// <summary>Hiệu ứng phóng to khi rê chuột / thu nhỏ khi nhấn, và nhịp "thở" cho nút chính.</summary>
    class Press : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public bool pulse;
        float target = 1f, cur = 1f;
        bool over;

        public void OnPointerEnter(PointerEventData e) { over = true; target = 1.06f; }
        public void OnPointerExit(PointerEventData e) { over = false; target = 1f; }
        public void OnPointerDown(PointerEventData e) { target = .94f; }
        public void OnPointerUp(PointerEventData e) { target = over ? 1.06f : 1f; }

        void Update()
        {
            cur = Mathf.Lerp(cur, target, Time.unscaledDeltaTime * 14f);
            float p = pulse && Mathf.Approximately(target, 1f) ? 1f + .025f * Mathf.Sin(Time.unscaledTime * 3f) : 1f;
            transform.localScale = Vector3.one * cur * p;
        }
    }
}
