using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene đăng nhập cho học sinh: nhập Tên + Lớp -> lưu vào StudentProfileData -> vào MainMenu.
/// Animation: intro (tiêu đề nảy, nhân vật + bong bóng thoại, thẻ trượt lên), idle (lơ lửng),
/// focus ô nhập (viền vàng), sai (rung + đỏ), thành công (pháo hoa + nhân vật nhảy + fade).
/// Toàn bộ dùng unscaledTime giống SubjectSelectManager.
/// </summary>
public class LoginManager : MonoBehaviour
{
    private const string PrefName = "login.name";
    private const string PrefClass = "login.class";

    private static readonly Color NormalColor = new Color(0.94f, 0.95f, 1f, 1f);
    private static readonly Color FocusColor = new Color(1f, 0.94f, 0.68f, 1f);
    private static readonly Color ErrorColor = new Color(1f, 0.76f, 0.76f, 1f);
    private static readonly Color ErrorTextColor = new Color(0.86f, 0.2f, 0.25f, 1f);
    private static readonly Color SuccessTextColor = new Color(0.1f, 0.6f, 0.35f, 1f);

    [Header("Bố cục")]
    [SerializeField] private RectTransform titleGroup;
    [SerializeField] private RectTransform mascot;
    [SerializeField] private RectTransform bubble;
    [SerializeField] private TMP_Text bubbleText;
    [SerializeField] private RectTransform card;
    [SerializeField] private CanvasGroup cardGroup;
    [Tooltip("Các nhóm hiện lần lượt trong thẻ: Tên, Lớp, Nút")]
    [SerializeField] private CanvasGroup[] fieldGroups;
    [SerializeField] private Image bgGlow;

    [Header("Form")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField classInput;
    [SerializeField] private Image nameFrame;
    [SerializeField] private Image classFrame;
    [SerializeField] private Button loginButton;
    [SerializeField] private AnimatedButton loginButtonFx;
    [SerializeField] private TMP_Text loginButtonLabel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private CanvasGroup messageGroup;

    [Header("Hiệu ứng")]
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private RectTransform effectsRoot;
    [SerializeField] private Sprite sparkleSprite;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private AudioClip successClip;
    [SerializeField] private AudioClip errorClip;

    [Header("Điều hướng")]
    [SerializeField] private string nextSceneName = "MainMenu";

    private Vector2 titleBase, mascotBase, cardBase;
    private Vector2[] fieldBase;
    private bool introDone, locked, mascotBusy;
    private bool nameError, classError;
    private TMP_InputField focused;
    private Coroutine bubbleRoutine, messageRoutine;

    // ---------- Easing ----------
    private static float EaseOutCubic(float k) { k = 1f - k; return 1f - k * k * k; }

    private static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private static float EaseOutBounce(float k)
    {
        const float n1 = 7.5625f, d1 = 2.75f;
        if (k < 1f / d1) return n1 * k * k;
        if (k < 2f / d1) { k -= 1.5f / d1; return n1 * k * k + 0.75f; }
        if (k < 2.5f / d1) { k -= 2.25f / d1; return n1 * k * k + 0.9375f; }
        k -= 2.625f / d1;
        return n1 * k * k + 0.984375f;
    }

    private static IEnumerator Tween(float duration, System.Action<float> step)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            step(t / duration);
            yield return null;
        }
        step(1f);
    }

    private static IEnumerator Delayed(float delay, IEnumerator inner)
    {
        yield return new WaitForSecondsRealtime(delay);
        yield return inner;
    }

    // ---------- Vòng đời ----------
    private void Awake()
    {
        titleBase = titleGroup.anchoredPosition;
        mascotBase = mascot.anchoredPosition;
        cardBase = card.anchoredPosition;

        fieldBase = new Vector2[fieldGroups.Length];
        for (int i = 0; i < fieldGroups.Length; i++)
        {
            var g = fieldGroups[i];
            fieldBase[i] = ((RectTransform)g.transform).anchoredPosition;
            g.alpha = 0f;
        }

        titleGroup.localScale = Vector3.zero;
        mascot.localScale = Vector3.zero;
        bubble.localScale = Vector3.zero;
        cardGroup.alpha = 0f;
        card.anchoredPosition = cardBase + new Vector2(0f, -200f);
        messageGroup.alpha = 0f;

        fadeOverlay.alpha = 1f;
        fadeOverlay.blocksRaycasts = true;

        nameInput.text = PlayerPrefs.GetString(PrefName, "");
        classInput.text = PlayerPrefs.GetString(PrefClass, "");
        bubbleText.text = "Xin chào! Mình là gia sư Khoa học của bạn. Bạn tên là gì nhỉ?";
    }

    private void OnEnable()
    {
        loginButton.onClick.AddListener(TrySubmit);
        nameInput.onSelect.AddListener(OnNameSelect);
        classInput.onSelect.AddListener(OnClassSelect);
        nameInput.onDeselect.AddListener(OnAnyDeselect);
        classInput.onDeselect.AddListener(OnAnyDeselect);
        nameInput.onValueChanged.AddListener(OnNameChanged);
        classInput.onValueChanged.AddListener(OnClassChanged);
        nameInput.onSubmit.AddListener(OnNameSubmit);
        classInput.onSubmit.AddListener(OnClassSubmit);
    }

    private void OnDisable()
    {
        loginButton.onClick.RemoveListener(TrySubmit);
        nameInput.onSelect.RemoveListener(OnNameSelect);
        classInput.onSelect.RemoveListener(OnClassSelect);
        nameInput.onDeselect.RemoveListener(OnAnyDeselect);
        classInput.onDeselect.RemoveListener(OnAnyDeselect);
        nameInput.onValueChanged.RemoveListener(OnNameChanged);
        classInput.onValueChanged.RemoveListener(OnClassChanged);
        nameInput.onSubmit.RemoveListener(OnNameSubmit);
        classInput.onSubmit.RemoveListener(OnClassSubmit);
    }

    private void Start()
    {
        StartCoroutine(IntroRoutine());
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;
        float k = 1f - Mathf.Exp(-14f * dt);

        if (bgGlow != null)
        {
            var c = bgGlow.color;
            c.a = 0.38f + 0.10f * Mathf.Sin(t * 0.8f);
            bgGlow.color = c;
        }

        if (!introDone) return;

        titleGroup.anchoredPosition = titleBase + new Vector2(0f, Mathf.Sin(t * 1.4f) * 8f);
        titleGroup.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.9f) * 1.2f);

        if (!mascotBusy)
        {
            mascot.anchoredPosition = mascotBase + new Vector2(0f, Mathf.Sin(t * 2f) * 10f);
            mascot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f) * 3f);
        }

        AnimateFrame(nameFrame, focused == nameInput, nameError, k);
        AnimateFrame(classFrame, focused == classInput, classError, k);

        bool ready = nameInput.text.Trim().Length >= 2 && classInput.text.Trim().Length >= 1;
        loginButtonFx.Pulse = ready && !locked;
    }

    private static void AnimateFrame(Image frame, bool isFocused, bool isError, float k)
    {
        if (frame == null) return;
        Color target = isError ? ErrorColor : isFocused ? FocusColor : NormalColor;
        frame.color = Color.Lerp(frame.color, target, k);
        var rt = frame.rectTransform;
        rt.localScale = Vector3.Lerp(rt.localScale, Vector3.one * (isFocused ? 1.025f : 1f), k);
    }

    // ---------- Intro ----------
    private IEnumerator IntroRoutine()
    {
        StartCoroutine(FadeOverlay(1f, 0f, 0.5f));
        StartCoroutine(Delayed(0.2f, DropIn(titleGroup, titleBase + new Vector2(0f, 420f), titleBase, 0.9f)));
        StartCoroutine(Delayed(0.6f, PopIn(mascot, 0.6f)));
        StartCoroutine(Delayed(0.95f, PopIn(bubble, 0.45f)));
        StartCoroutine(Delayed(0.8f, CardIn()));

        yield return new WaitForSecondsRealtime(2.1f);
        introDone = true;
        nameInput.Select();
        nameInput.ActivateInputField();
    }

    private IEnumerator DropIn(RectTransform rt, Vector2 from, Vector2 to, float duration)
    {
        rt.localScale = Vector3.one;
        rt.anchoredPosition = from;
        yield return Tween(duration, k => rt.anchoredPosition = Vector2.LerpUnclamped(from, to, EaseOutBounce(k)));
    }

    private IEnumerator PopIn(RectTransform rt, float duration)
    {
        yield return Tween(duration, k => rt.localScale = Vector3.one * EaseOutBack(k));
        rt.localScale = Vector3.one;
    }

    private IEnumerator CardIn()
    {
        for (int i = 0; i < fieldGroups.Length; i++)
            StartCoroutine(Delayed(0.3f + i * 0.1f, FieldIn(i)));

        Vector2 from = cardBase + new Vector2(0f, -200f);
        yield return Tween(0.6f, k =>
        {
            card.anchoredPosition = Vector2.LerpUnclamped(from, cardBase, EaseOutBack(k));
            cardGroup.alpha = Mathf.Clamp01(k * 2.2f);
        });
        card.anchoredPosition = cardBase;
    }

    private IEnumerator FieldIn(int i)
    {
        var g = fieldGroups[i];
        var rt = (RectTransform)g.transform;
        Vector2 to = fieldBase[i];
        Vector2 from = to + new Vector2(0f, -36f);
        yield return Tween(0.4f, k =>
        {
            float e = EaseOutCubic(k);
            rt.anchoredPosition = Vector2.Lerp(from, to, e);
            g.alpha = e;
        });
    }

    // ---------- Sự kiện ô nhập ----------
    private void OnNameSelect(string _) { focused = nameInput; SetBubble("Bạn tên là gì nhỉ?"); }
    private void OnClassSelect(string _) { focused = classInput; SetBubble("Bạn học lớp nào vậy? Ví dụ: 8A1"); }

    private void OnAnyDeselect(string _)
    {
        if (focused != null && !focused.isFocused) focused = null;
    }

    private void OnNameChanged(string _) { if (nameError) { nameError = false; HideMessage(); } }
    private void OnClassChanged(string _) { if (classError) { classError = false; HideMessage(); } }

    private void OnNameSubmit(string _)
    {
        if (string.IsNullOrWhiteSpace(nameInput.text)) return;
        classInput.Select();
        classInput.ActivateInputField();
    }

    private void OnClassSubmit(string _) { TrySubmit(); }

    // ---------- Đăng nhập ----------
    private void TrySubmit()
    {
        if (locked || !introDone) return;

        string n = nameInput.text.Trim();
        string c = classInput.text.Trim().ToUpperInvariant();

        if (n.Length < 2)
        {
            Fail(true, "Bạn quên nhập tên rồi nè!");
            return;
        }
        if (c.Length < 1)
        {
            Fail(false, "Nhập lớp của bạn nhé (ví dụ 8A1).");
            return;
        }

        StartCoroutine(SuccessRoutine(n, c));
    }

    private void Fail(bool nameField, string message)
    {
        if (nameField) nameError = true; else classError = true;

        ShowMessage(message, ErrorTextColor);
        SetBubble("Hmm, kiểm tra lại giúp mình nhé!");
        Play(errorClip);
        StartCoroutine(ShakeCard());
        StartCoroutine(MascotShake());

        var input = nameField ? nameInput : classInput;
        input.Select();
        input.ActivateInputField();
    }

    private IEnumerator SuccessRoutine(string n, string c)
    {
        locked = true;

        StudentProfileData.studentName = n;
        StudentProfileData.studentClass = c;
        ProgressSaveSystem.Load(n, c);
        PlayerPrefs.SetString(PrefName, n);
        PlayerPrefs.SetString(PrefClass, c);
        PlayerPrefs.Save();

        nameInput.interactable = false;
        classInput.interactable = false;
        loginButton.interactable = false;
        loginButtonFx.Pulse = false;
        loginButtonLabel.text = "ĐANG VÀO...";

        Play(successClip);
        SetBubble("Chào " + n + "! Cùng khám phá Khoa học 8 nào!");
        ShowMessage("Đăng nhập thành công!", SuccessTextColor);

        StartCoroutine(SparkleBurst(mascot.anchoredPosition));
        yield return MascotJump();
        yield return new WaitForSecondsRealtime(0.35f);

        yield return Tween(0.3f, k =>
        {
            card.localScale = Vector3.one * (1f - 0.08f * k);
            cardGroup.alpha = 1f - k;
        });

        yield return LoadSceneWithFade(nextSceneName);
    }

    private IEnumerator LoadSceneWithFade(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("[Login] Scene '" + sceneName + "' chưa có trong Build Settings.");
            // trả form về trạng thái nhập lại
            card.localScale = Vector3.one;
            cardGroup.alpha = 1f;
            nameInput.interactable = true;
            classInput.interactable = true;
            loginButton.interactable = true;
            loginButtonLabel.text = "VÀO HỌC NÀO!";
            ShowMessage("Không tìm thấy scene: " + sceneName, ErrorTextColor);
            locked = false;
            yield break;
        }

        yield return FadeOverlay(0f, 1f, 0.35f);
        var op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;
    }

    // ---------- Thông báo & bong bóng ----------
    private void ShowMessage(string message, Color color)
    {
        messageText.text = message;
        messageText.color = color;
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(FadeGroup(messageGroup, 1f, 0.2f));
    }

    private void HideMessage()
    {
        if (messageGroup.alpha <= 0.01f) return;
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(FadeGroup(messageGroup, 0f, 0.2f));
    }

    private static IEnumerator FadeGroup(CanvasGroup g, float to, float duration)
    {
        float from = g.alpha;
        yield return Tween(duration, k => g.alpha = Mathf.Lerp(from, to, k));
    }

    private void SetBubble(string text)
    {
        if (bubbleText.text == text) return;
        bubbleText.text = text;
        if (!introDone) return;
        if (bubbleRoutine != null) StopCoroutine(bubbleRoutine);
        bubbleRoutine = StartCoroutine(BubblePunch());
    }

    private IEnumerator BubblePunch()
    {
        yield return Tween(0.3f, k => bubble.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.1f));
        bubble.localScale = Vector3.one;
    }

    // ---------- Hoạt ảnh ----------
    private IEnumerator ShakeCard()
    {
        yield return Tween(0.42f, k =>
        {
            float x = Mathf.Sin(k * 32f) * (1f - k) * 22f;
            card.anchoredPosition = cardBase + new Vector2(x, 0f);
        });
        card.anchoredPosition = cardBase;
    }

    private IEnumerator MascotShake()
    {
        mascotBusy = true;
        yield return Tween(0.42f, k =>
        {
            mascot.anchoredPosition = mascotBase;
            mascot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * 28f) * (1f - k) * 10f);
        });
        mascotBusy = false;
    }

    private IEnumerator MascotJump()
    {
        mascotBusy = true;
        yield return Tween(0.6f, k =>
        {
            float h = Mathf.Sin(k * Mathf.PI);
            mascot.anchoredPosition = mascotBase + new Vector2(0f, h * 110f);
            mascot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 2f) * 8f);
            float squash = k < 0.15f ? 1f - k * 0.5f : 1f + h * 0.08f;
            mascot.localScale = new Vector3(1f / squash, squash, 1f);
        });
        mascot.localScale = Vector3.one;
        mascot.anchoredPosition = mascotBase;
        mascot.localRotation = Quaternion.identity;
        // giữ trạng thái busy: sau khi thành công nhân vật đứng yên chờ chuyển cảnh
    }

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        fadeOverlay.blocksRaycasts = true;
        yield return Tween(duration, k => fadeOverlay.alpha = Mathf.Lerp(from, to, k));
        fadeOverlay.blocksRaycasts = to > 0.5f;
    }

    private void Play(AudioClip clip)
    {
        if (sfx != null && clip != null) sfx.PlayOneShot(clip);
    }

    private IEnumerator SparkleBurst(Vector2 center)
    {
        if (sparkleSprite == null || effectsRoot == null) yield break;

        const int n = 28;
        Color[] palette =
        {
            new Color(1f, 0.88f, 0.4f), Color.white, new Color(0.6f, 1f, 0.85f), new Color(1f, 0.7f, 0.85f)
        };

        var rts = new RectTransform[n];
        var imgs = new Image[n];
        var dirs = new Vector2[n];
        var speeds = new float[n];

        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("Sparkle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rts[i] = (RectTransform)go.transform;
            rts[i].SetParent(effectsRoot, false);
            imgs[i] = go.GetComponent<Image>();
            imgs[i].sprite = sparkleSprite;
            imgs[i].raycastTarget = false;
            imgs[i].color = palette[Random.Range(0, palette.Length)];
            float a = (i / (float)n) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            speeds[i] = Random.Range(280f, 640f);
            rts[i].sizeDelta = Vector2.one * Random.Range(34f, 84f);
            rts[i].anchoredPosition = center;
        }

        const float dur = 1f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur, e = EaseOutCubic(k);
            for (int i = 0; i < n; i++)
            {
                rts[i].anchoredPosition = center + dirs[i] * speeds[i] * e + new Vector2(0f, -120f * k * k);
                rts[i].localScale = Vector3.one * (1f - k * 0.7f);
                rts[i].localRotation = Quaternion.Euler(0f, 0f, k * 200f);
                var c = imgs[i].color; c.a = 1f - k; imgs[i].color = c;
            }
            yield return null;
        }
        for (int i = 0; i < n; i++) Destroy(rts[i].gameObject);
    }
}
