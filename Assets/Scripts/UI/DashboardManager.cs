using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dashboard tiến độ học tập: 4 thẻ số liệu, thanh bài học + tiến độ từng môn,
/// lịch sử 5 lượt gần nhất, bộ huy hiệu. Dữ liệu lấy từ ProgressSaveSystem.
/// Animation: các khối bật ra lần lượt, số đếm lên, thanh chạy đầy, dòng trượt vào,
/// huy hiệu mới nảy + pháo sáng. Dùng unscaledTime giống các scene UI khác.
/// </summary>
public class DashboardManager : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.16f, 0.14f, 0.38f);
    private static readonly Color Muted = new Color(0.16f, 0.14f, 0.38f, 0.55f);
    private static readonly Color BarFrame = new Color(0.88f, 0.9f, 0.98f);
    private static readonly Color WinColor = new Color(0.25f, 0.75f, 0.45f);
    private static readonly Color LoseColor = new Color(0.9f, 0.42f, 0.42f);
    private static readonly Color LockedColor = new Color(0.45f, 0.45f, 0.55f, 0.45f);

    [Header("Font & sprite")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite chestSprite;
    [SerializeField] private Sprite sparkleSprite;

    [Header("Header")]
    [SerializeField] private RectTransform titleGroup;
    [SerializeField] private RectTransform headerGroup;
    [SerializeField] private RectTransform avatar;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text classText;
    [SerializeField] private Button backButton;

    [Header("Thẻ số liệu (Huy hiệu, Điểm cao, Lượt chơi, Chính xác)")]
    [SerializeField] private RectTransform[] statCards;
    [SerializeField] private TMP_Text[] statValues;

    [Header("Khối nội dung")]
    [SerializeField] private RectTransform subjectPanel;
    [SerializeField] private RectTransform historyPanel;
    [SerializeField] private RectTransform badgePanel;
    [SerializeField] private RectTransform lessonBarFill;
    [SerializeField] private TMP_Text lessonText;
    [SerializeField] private RectTransform subjectList;
    [SerializeField] private RectTransform historyList;
    [SerializeField] private RectTransform badgeList;
    [SerializeField] private TMP_Text badgeHeader;
    [SerializeField] private TMP_Text emptyText;

    [Header("Hiệu ứng")]
    [SerializeField] private Image bgGlow;
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private RectTransform effectsRoot;

    [Header("Điều hướng")]
    [SerializeField] private string backSceneName = "MainMenu";

    private struct BarAnim { public RectTransform fill; public float target; }
    private struct CountAnim { public TMP_Text text; public int target; public string format; }

    private readonly List<BarAnim> bars = new List<BarAnim>();
    private readonly List<CountAnim> counts = new List<CountAnim>();
    private readonly List<RectTransform> subjectRows = new List<RectTransform>();
    private readonly List<RectTransform> historyRows = new List<RectTransform>();
    private readonly List<RectTransform> badgeTiles = new List<RectTransform>();
    private readonly List<RectTransform> bobChests = new List<RectTransform>();
    private readonly List<float> bobPhase = new List<float>();
    private readonly List<int> newBadgeTileIndex = new List<int>();

    private RectTransform[] introItems;
    private Vector2 titleBase, avatarBase;
    private bool introDone, locked;
    private ProgressData data;

    // ---------- Easing / tiện ích ----------
    private static float EaseOutCubic(float k) { k = 1f - k; return 1f - k * k * k; }

    private static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private static IEnumerator Tween(float duration, Action<float> step)
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
        avatarBase = avatar.anchoredPosition;

        var list = new List<RectTransform> { titleGroup, headerGroup };
        list.AddRange(statCards);
        list.Add(subjectPanel);
        list.Add(historyPanel);
        list.Add(badgePanel);
        introItems = list.ToArray();
        foreach (var rt in introItems) rt.localScale = Vector3.zero;

        fadeOverlay.alpha = 1f;
        fadeOverlay.blocksRaycasts = true;
    }

    private void OnEnable() { if (backButton != null) backButton.onClick.AddListener(OnClickBack); }
    private void OnDisable() { if (backButton != null) backButton.onClick.RemoveListener(OnClickBack); }

    private void Start()
    {
        data = ProgressSaveSystem.Current;
        Populate();
        StartCoroutine(IntroRoutine());
    }

    private void Update()
    {
        float t = Time.unscaledTime;

        if (bgGlow != null)
        {
            var c = bgGlow.color;
            c.a = 0.38f + 0.10f * Mathf.Sin(t * 0.8f);
            bgGlow.color = c;
        }

        if (!introDone) return;

        titleGroup.anchoredPosition = titleBase + new Vector2(0f, Mathf.Sin(t * 1.4f) * 6f);
        avatar.anchoredPosition = avatarBase + new Vector2(0f, Mathf.Sin(t * 2f) * 6f);
        avatar.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f) * 3f);

        for (int i = 0; i < bobChests.Count; i++)
        {
            var rt = bobChests[i];
            rt.anchoredPosition = new Vector2(0f, 20f + Mathf.Sin(t * 2.2f + bobPhase[i]) * 4f);
        }
    }

    // ---------- Đổ dữ liệu ----------
    private void Populate()
    {
        nameText.text = "Xin chào, " + StudentProfileData.studentName + "!";
        classText.text = string.IsNullOrEmpty(StudentProfileData.studentClass)
            ? "Lớp 8"
            : "Lớp " + StudentProfileData.studentClass;

        int attempts = ProgressSaveSystem.Attempts(data);
        int best = ProgressSaveSystem.BestScore(data);
        int accuracy = Mathf.RoundToInt(ProgressSaveSystem.Accuracy(data) * 100f);

        AddCount(statValues[0], data.badges.Count, "{0}/" + ProgressSaveSystem.Badges.Length);
        AddCount(statValues[1], best, "{0}");
        AddCount(statValues[2], attempts, "{0}");
        AddCount(statValues[3], accuracy, "{0}%");

        // Thanh bài học
        int done = ProgressSaveSystem.CompletedLessons(data);
        int total = Mathf.Max(1, StudentProfileData.totalLessons);
        lessonText.text = "Bài học hoàn thành: " + done + "/" + total;
        SetBar(lessonBarFill, 0f);
        bars.Add(new BarAnim { fill = lessonBarFill, target = (float)done / total });

        BuildSubjectRows();
        BuildHistoryRows();
        BuildBadgeTiles();
    }

    private void AddCount(TMP_Text text, int target, string format)
    {
        text.text = string.Format(format, 0);
        counts.Add(new CountAnim { text = text, target = target, format = format });
    }

    private static void SetBar(RectTransform fill, float v)
    {
        v = Mathf.Clamp01(v);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(v, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fill.gameObject.SetActive(v > 0.02f);
    }

    // ----- Tiến độ theo môn -----
    private void BuildSubjectRows()
    {
        Color[] colors =
        {
            new Color(0.35f, 0.6f, 1f), new Color(0.3f, 0.8f, 0.55f), new Color(0.6f, 0.6f, 0.72f)
        };

        for (int i = 0; i < ProgressSaveSystem.Subjects.Length; i++)
        {
            string subject = ProgressSaveSystem.Subjects[i];
            var stats = ProgressSaveSystem.FindSubject(data, subject);

            var row = NewRect("Row_" + subject, subjectList);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.sizeDelta = new Vector2(800f, 56f);
            row.anchoredPosition = new Vector2(0f, -i * 72f - 28f);
            row.localScale = new Vector3(1f, 0f, 1f);

            var label = MakeText("Label", row, subject.ToUpperInvariant(), 34f, Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Place(label.rectTransform, new Vector2(-325f, 0f), new Vector2(120f, 50f));

            var frame = NewRect("BarFrame", row);
            Place(frame, new Vector2(-70f, 0f), new Vector2(330f, 28f));
            var frameImg = frame.gameObject.AddComponent<Image>();
            frameImg.sprite = panelSprite; frameImg.type = Image.Type.Sliced;
            frameImg.color = BarFrame; frameImg.raycastTarget = false;

            var fill = NewRect("Fill", frame);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = panelSprite; fillImg.type = Image.Type.Sliced;
            fillImg.color = colors[i]; fillImg.raycastTarget = false;
            SetBar(fill, 0f);

            string info;
            float target = 0f;
            if (stats == null || stats.attempts == 0) info = "Chưa chơi";
            else
            {
                target = ProgressSaveSystem.Accuracy(stats);
                info = stats.attempts + " lượt · " + Mathf.RoundToInt(target * 100f) + "% đúng";
            }
            bars.Add(new BarAnim { fill = fill, target = target });

            var infoText = MakeText("Info", row, info, 24f, Ink, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            Place(infoText.rectTransform, new Vector2(240f, 0f), new Vector2(290f, 50f));

            subjectRows.Add(row);
        }
    }

    // ----- Lịch sử gần đây -----
    private void BuildHistoryRows()
    {
        int shown = Mathf.Min(5, data.sessions.Count);
        emptyText.gameObject.SetActive(shown == 0);

        for (int i = 0; i < shown; i++)
        {
            var s = data.sessions[data.sessions.Count - 1 - i];

            var row = NewRect("Session_" + i, historyList);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.sizeDelta = new Vector2(800f, 54f);
            row.anchoredPosition = new Vector2(0f, -i * 62f - 27f);
            row.localScale = new Vector3(1f, 0f, 1f);

            var stripe = NewRect("Stripe", row);
            Stretch(stripe);
            var stripeImg = stripe.gameObject.AddComponent<Image>();
            stripeImg.sprite = panelSprite; stripeImg.type = Image.Type.Sliced;
            stripeImg.color = i % 2 == 0 ? new Color(0.93f, 0.94f, 1f) : new Color(1f, 1f, 1f, 0f);
            stripeImg.raycastTarget = false;

            string dateStr = s.date;
            if (DateTime.TryParse(s.date, out var dt)) dateStr = dt.ToString("dd/MM  HH:mm");

            var date = MakeText("Date", row, dateStr, 24f, Muted, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            Place(date.rectTransform, new Vector2(-290f, 0f), new Vector2(220f, 50f));

            var subject = MakeText("Subject", row, s.subject, 28f, Ink, FontStyles.Bold, TextAlignmentOptions.Midline);
            Place(subject.rectTransform, new Vector2(-120f, 0f), new Vector2(90f, 50f));

            var score = MakeText("Score", row, s.score + " điểm", 28f, Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Place(score.rectTransform, new Vector2(40f, 0f), new Vector2(170f, 50f));

            var pill = NewRect("Pill", row);
            Place(pill, new Vector2(290f, 0f), new Vector2(150f, 40f));
            var pillImg = pill.gameObject.AddComponent<Image>();
            pillImg.sprite = panelSprite; pillImg.type = Image.Type.Sliced;
            pillImg.color = s.won ? WinColor : LoseColor;
            pillImg.raycastTarget = false;
            var pillText = MakeText("Text", pill, s.won ? "THẮNG" : "THUA", 24f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(pillText.rectTransform);

            historyRows.Add(row);
        }
    }

    // ----- Huy hiệu -----
    private void BuildBadgeTiles()
    {
        var defs = ProgressSaveSystem.Badges;
        badgeHeader.text = "HUY HIỆU  " + data.badges.Count + "/" + defs.Length;

        for (int i = 0; i < defs.Length; i++)
        {
            var def = defs[i];
            bool unlocked = data.badges.Contains(def.id);
            bool isNew = unlocked && !data.seenBadges.Contains(def.id);

            var tile = NewRect("Badge_" + def.id, badgeList);
            Place(tile, new Vector2(-720f + i * 240f, 0f), new Vector2(230f, 130f));
            tile.localScale = Vector3.zero;

            var chest = NewRect("Chest", tile);
            Place(chest, new Vector2(0f, 20f), new Vector2(72f, 72f));
            var chestImg = chest.gameObject.AddComponent<Image>();
            chestImg.sprite = chestSprite;
            chestImg.preserveAspect = true;
            chestImg.color = unlocked ? Color.Lerp(Color.white, def.color, 0.55f) : LockedColor;
            chestImg.raycastTarget = false;

            var title = MakeText("Title", tile, def.title, 23f, unlocked ? Ink : Muted, FontStyles.Bold, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0f, -32f), new Vector2(230f, 30f));

            var desc = MakeText("Desc", tile, def.desc, 17f, Muted, FontStyles.Normal, TextAlignmentOptions.Center);
            Place(desc.rectTransform, new Vector2(0f, -58f), new Vector2(230f, 26f));

            if (unlocked)
            {
                bobChests.Add(chest);
                bobPhase.Add(i * 0.9f);
            }

            if (isNew)
            {
                var tag = NewRect("NewTag", tile);
                Place(tag, new Vector2(46f, 52f), new Vector2(70f, 30f));
                var tagImg = tag.gameObject.AddComponent<Image>();
                tagImg.sprite = panelSprite; tagImg.type = Image.Type.Sliced;
                tagImg.color = new Color(1f, 0.45f, 0.35f); tagImg.raycastTarget = false;
                var tagText = MakeText("Text", tag, "MỚI!", 20f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(tagText.rectTransform);
                newBadgeTileIndex.Add(i);
            }

            badgeTiles.Add(tile);
        }
    }

    // ---------- Animation ----------
    private IEnumerator IntroRoutine()
    {
        StartCoroutine(FadeOverlay(1f, 0f, 0.5f));

        const float start = 0.15f, step = 0.09f;
        for (int i = 0; i < introItems.Length; i++)
            StartCoroutine(Delayed(start + i * step, Pop(introItems[i], 0.5f)));

        yield return new WaitForSecondsRealtime(start + introItems.Length * step + 0.3f);

        foreach (var c in counts) StartCoroutine(CountUp(c));
        foreach (var b in bars) StartCoroutine(FillBar(b));
        StartCoroutine(RevealRows(subjectRows, 0f));
        StartCoroutine(RevealRows(historyRows, 0.1f));

        for (int i = 0; i < badgeTiles.Count; i++)
            StartCoroutine(Delayed(0.2f + i * 0.08f, Pop(badgeTiles[i], 0.45f)));

        yield return new WaitForSecondsRealtime(0.2f + badgeTiles.Count * 0.08f + 0.5f);

        foreach (int idx in newBadgeTileIndex)
            StartCoroutine(SparkleBurst(ToEffectsSpace(badgeTiles[idx])));

        // đánh dấu đã xem để lần sau không hiện "MỚI!" nữa
        bool changed = false;
        foreach (var id in data.badges)
            if (!data.seenBadges.Contains(id)) { data.seenBadges.Add(id); changed = true; }
        if (changed) ProgressSaveSystem.Save();

        introDone = true;
    }

    private static IEnumerator Pop(RectTransform rt, float duration)
    {
        yield return Tween(duration, k => rt.localScale = Vector3.one * EaseOutBack(k));
        rt.localScale = Vector3.one;
    }

    private IEnumerator RevealRows(List<RectTransform> rows, float initialDelay)
    {
        yield return new WaitForSecondsRealtime(initialDelay);
        for (int i = 0; i < rows.Count; i++)
        {
            StartCoroutine(RowGrow(rows[i]));   // chạy song song, cách nhau một nhịp ngắn
            yield return new WaitForSecondsRealtime(0.07f);
        }
    }

    private static IEnumerator RowGrow(RectTransform rt)
    {
        yield return Tween(0.3f, k => rt.localScale = new Vector3(1f, EaseOutBack(k), 1f));
        rt.localScale = Vector3.one;
    }

    private static IEnumerator CountUp(CountAnim c)
    {
        yield return Tween(0.9f, k =>
            c.text.text = string.Format(c.format, Mathf.RoundToInt(c.target * EaseOutCubic(k))));
        c.text.text = string.Format(c.format, c.target);
    }

    private static IEnumerator FillBar(BarAnim b)
    {
        yield return Tween(0.9f, k => SetBar(b.fill, b.target * EaseOutCubic(k)));
        SetBar(b.fill, b.target);
    }

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        fadeOverlay.blocksRaycasts = true;
        yield return Tween(duration, k => fadeOverlay.alpha = Mathf.Lerp(from, to, k));
        fadeOverlay.blocksRaycasts = to > 0.5f;
    }

    private Vector2 ToEffectsSpace(RectTransform target)
    {
        Vector3 local = effectsRoot.InverseTransformPoint(target.position);
        return new Vector2(local.x, local.y + 20f);
    }

    private IEnumerator SparkleBurst(Vector2 center)
    {
        if (sparkleSprite == null || effectsRoot == null) yield break;

        const int n = 16;
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
            imgs[i].color = Color.Lerp(new Color(1f, 0.88f, 0.4f), Color.white, UnityEngine.Random.value);
            float a = (i / (float)n) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.2f, 0.2f);
            dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            speeds[i] = UnityEngine.Random.Range(90f, 230f);
            rts[i].sizeDelta = Vector2.one * UnityEngine.Random.Range(26f, 56f);
            rts[i].anchoredPosition = center;
        }

        const float dur = 0.85f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur, e = EaseOutCubic(k);
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

    // ---------- Điều hướng ----------
    public void OnClickBack()
    {
        if (locked) return;
        locked = true;
        StartCoroutine(LoadSceneWithFade(backSceneName));
    }

    private IEnumerator LoadSceneWithFade(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("[Dashboard] Scene '" + sceneName + "' chưa có trong Build Settings.");
            locked = false;
            yield break;
        }
        yield return FadeOverlay(0f, 1f, 0.35f);
        var op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;
    }

    // ---------- Dựng UI nhỏ ----------
    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Color color,
        FontStyles style, TextAlignmentOptions align)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }

    // ---------- Thử nghiệm trong Editor ----------
    [ContextMenu("Thêm dữ liệu mẫu")]
    private void DebugAddDemo()
    {
        ProgressSaveSystem.DebugAddDemoData();
        if (Application.isPlaying) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    [ContextMenu("Xoá tiến độ học sinh hiện tại")]
    private void DebugReset()
    {
        ProgressSaveSystem.ResetCurrent();
        if (Application.isPlaying) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
