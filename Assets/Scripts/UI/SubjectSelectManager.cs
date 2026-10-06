using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene chọn môn: Lý -> GameplayScene, Hóa -> MillionareScene, Sinh -> chưa có (hiện thông báo).
/// Scene đích cấu hình trên từng SubjectCard (sceneToLoad / available).
/// </summary>
public class SubjectSelectManager : MonoBehaviour
{
    [SerializeField] private SubjectCard[] cards;
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private CanvasGroup toast;
    [SerializeField] private TMP_Text toastText;
    [SerializeField] private RectTransform effectsRoot;
    [SerializeField] private Sprite sparkleSprite;
    [SerializeField] private string menuSceneName = "MainMenu";
    [SerializeField] private float introStagger = 0.12f;

    private Coroutine toastRoutine;

    private void Awake()
    {
        SubjectCard.InputLocked = false;
        if (fadeOverlay != null) { fadeOverlay.alpha = 0f; fadeOverlay.blocksRaycasts = false; }
        if (toast != null) toast.alpha = 0f;
    }

    private void OnEnable()
    {
        foreach (var c in cards) if (c != null) c.Clicked += OnCardClicked;
    }

    private void OnDisable()
    {
        foreach (var c in cards) if (c != null) c.Clicked -= OnCardClicked;
    }

    private void Start()
    {
        for (int i = 0; i < cards.Length; i++)
            cards[i].PlayIntro(0.15f + i * introStagger);
    }

    private void OnCardClicked(SubjectCard card)
    {
        if (SubjectCard.InputLocked) return;

        if (!card.available || string.IsNullOrEmpty(card.sceneToLoad))
        {
            card.PlayShake(); GameAudio.Play(Snd.CardLocked);
            ShowToast($"Môn {card.subjectName} sắp ra mắt!");
            return;
        }

        StartCoroutine(SelectRoutine(card));
    }

    private IEnumerator SelectRoutine(SubjectCard selected)
    {
        SubjectCard.InputLocked = true;

        foreach (var c in cards)
        {
            if (c == selected) continue;
            float dir = Mathf.Sign(c.BasePosition.x - selected.BasePosition.x);
            c.PlayDismiss(new Vector2(dir == 0f ? 0f : dir, -0.3f));
        }

        GameAudio.Play(Snd.CardSelect); StartCoroutine(SparkleBurst(Vector2.zero));
        yield return selected.PlaySelected(Vector2.zero);
        yield return new WaitForSecondsRealtime(0.25f);

        yield return LoadSceneWithFade(selected.sceneToLoad, selected);
    }

    public void OnClickBack()
    {
        if (SubjectCard.InputLocked) return;
        SubjectCard.InputLocked = true;
        GameAudio.Play(Snd.UiBack); StartCoroutine(LoadSceneWithFade(menuSceneName, null));
    }

    private IEnumerator LoadSceneWithFade(string sceneName, SubjectCard selected)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SubjectSelect] Scene '{sceneName}' chưa có trong Build Settings.");
            ShowToast("Không tìm thấy scene: " + sceneName);
            SubjectCard.InputLocked = false;
            if (selected != null) selected.PlayShake();
            yield break;
        }

        if (fadeOverlay != null)
        {
            fadeOverlay.blocksRaycasts = true;
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                fadeOverlay.alpha = t / 0.35f;
                yield return null;
            }
            fadeOverlay.alpha = 1f;
        }

        var op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;
    }

    // ---------- Toast ----------
    private void ShowToast(string message)
    {
        if (toast == null) return;
        if (toastText != null) toastText.text = message;
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine());
    }

    private IEnumerator ToastRoutine()
    {
        RectTransform tr = (RectTransform)toast.transform;
        Vector2 basePos = new Vector2(0f, 90f);
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.25f;
            toast.alpha = k;
            tr.anchoredPosition = basePos + new Vector2(0f, -30f * (1f - SubjectCard.EaseOutCubic(k)));
            yield return null;
        }
        toast.alpha = 1f;
        tr.anchoredPosition = basePos;
        yield return new WaitForSecondsRealtime(1.6f);
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            toast.alpha = 1f - t / 0.3f;
            yield return null;
        }
        toast.alpha = 0f;
        toastRoutine = null;
    }

    // ---------- Sparkles ----------
    private IEnumerator SparkleBurst(Vector2 center)
    {
        if (sparkleSprite == null || effectsRoot == null) yield break;

        const int n = 18;
        var rts = new RectTransform[n];
        var imgs = new Image[n];
        var dirs = new Vector2[n];
        var speeds = new float[n];
        var sizes = new float[n];

        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("Sparkle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rts[i] = (RectTransform)go.transform;
            rts[i].SetParent(effectsRoot, false);
            imgs[i] = go.GetComponent<Image>();
            imgs[i].sprite = sparkleSprite;
            imgs[i].raycastTarget = false;
            imgs[i].color = Color.Lerp(new Color(1f, 0.88f, 0.4f), Color.white, Random.value);
            float a = (i / (float)n) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            speeds[i] = Random.Range(300f, 620f);
            sizes[i] = Random.Range(36f, 84f);
            rts[i].sizeDelta = Vector2.one * sizes[i];
            rts[i].anchoredPosition = center;
        }

        const float dur = 0.9f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur, e = SubjectCard.EaseOutCubic(k);
            for (int i = 0; i < n; i++)
            {
                rts[i].anchoredPosition = center + dirs[i] * speeds[i] * e;
                rts[i].localScale = Vector3.one * (1f - k * 0.7f);
                rts[i].localRotation = Quaternion.Euler(0f, 0f, k * 180f);
                var c = imgs[i].color; c.a = 1f - k; imgs[i].color = c;
            }
            yield return null;
        }
        for (int i = 0; i < n; i++) Destroy(rts[i].gameObject);
    }
}
