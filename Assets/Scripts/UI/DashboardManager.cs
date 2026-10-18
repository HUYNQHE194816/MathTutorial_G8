using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Bảng tiến độ học tập (DashboardScene), dựng hoàn toàn bằng code theo phong cách dark fantasy dùng chung:
/// SceneShell (sương + tàn lửa + fade) + nền rừng trăng của MenuNatureFx + UIKit (panel khung đồng, nút đá, thanh tiến độ).
/// Nội dung: 4 thẻ số liệu, thanh bài học + tiến độ từng môn, 5 lượt chơi gần nhất, bộ huy hiệu (rune ngọc).
/// Animation: các khối bật ra lần lượt, số đếm lên, thanh đầy dần, dòng trượt vào, huy hiệu mới nảy + tàn lửa bắn.
/// Dữ liệu lấy từ ProgressSaveSystem. Scene chỉ cần 1 GameObject gắn script này (không còn tham chiếu kéo thả).
/// </summary>
public class DashboardManager : MonoBehaviour
{
    [SerializeField] string backSceneName = "MainMenu";

    const float SceneW = 1920f, SceneH = 1080f;

    struct BarAnim { public Image fill; public float target; }
    struct CountAnim { public Text text; public int target; public string format; }

    class RowAnim { public RectTransform rt; public CanvasGroup cg; public Vector2 home; }

    class BadgeTile
    {
        public RectTransform tile, icon;
        public Image glow;
        public bool unlocked, isNew;
        public float phase;
    }

    RectTransform artRoot, scene, ui, fx;
    MenuNatureFx nature;
    Vector2 lastRect;
    bool leaving, introDone;
    ProgressData data;

    Image lessonFill;
    Text lessonText, badgeHeader, emptyText;

    RectTransform[] introItems;
    readonly List<BarAnim> bars = new List<BarAnim>();
    readonly List<CountAnim> counts = new List<CountAnim>();
    readonly List<RowAnim> subjectRows = new List<RowAnim>();
    readonly List<RowAnim> historyRows = new List<RowAnim>();
    readonly List<BadgeTile> badges = new List<BadgeTile>();

    // ---------- Easing / tiện ích ----------
    static float EaseOutCubic(float k) { k = 1f - k; return 1f - k * k * k; }

    static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    static IEnumerator Tween(float duration, Action<float> step)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            step(t / duration);
            yield return null;
        }
        step(1f);
    }

    static IEnumerator Delayed(float delay, IEnumerator inner)
    {
        yield return new WaitForSecondsRealtime(delay);
        yield return inner;
    }

    static Color SubjectColor(string subject)
    {
        if (subject == ProgressSaveSystem.SubjectLy) return GameTheme.Ly;
        if (subject == ProgressSaveSystem.SubjectHoa) return GameTheme.Hoa;
        return GameTheme.Sinh;
    }

    // ---------- Vòng đời ----------
    void Start()
    {
        Time.timeScale = 1f;
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = GameTheme.Ink; }

        UIKit.EnsureEventSystem();
        SceneShell.Create(ShellMood.Hub, null, 1f, true, false);

        artRoot = UIKit.BuildCanvas(transform, "Dashboard Art", -90);
        scene = UIKit.CBox("Scene", artRoot, Color.clear, Vector2.zero, new Vector2(SceneW, SceneH));
        nature = new MenuNatureFx();
        nature.BuildForest(scene, 250f, 20);

        ui = UIKit.BuildCanvas(transform, "Dashboard UI", 10);
        data = ProgressSaveSystem.Current;
        BuildUi();
        FitScene();
        StartCoroutine(IntroRoutine());
    }

    void OnDestroy() { if (nature != null) nature.Dispose(); }

    void FitScene() { MenuNatureFx.FitCover(artRoot, scene, ref lastRect); }

    void Update()
    {
        FitScene();
        if (nature != null) nature.Tick(Time.unscaledTime, Time.unscaledDeltaTime);
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();

        if (!introDone) return;
        float t = Time.unscaledTime;
        foreach (var b in badges)
        {
            if (!b.unlocked) continue;
            b.icon.anchoredPosition = new Vector2(0f, 24f + Mathf.Sin(t * 2.2f + b.phase) * 4f);
            if (b.glow != null) b.glow.color = b.glow.color.WithAlpha(.26f + .12f * Mathf.Sin(t * 2.2f + b.phase));
        }
    }

    // ---------- Dựng giao diện ----------
    void BuildUi()
    {
        // lớp tối nhẹ phủ nền rừng để panel nổi bật
        var dim = UIKit.Box("Dim", ui, GameTheme.Ink.WithAlpha(.38f), Vector2.zero, Vector2.one, UIKit.C, Vector2.zero, Vector2.zero);
        UIKit.Stretch(dim, 0, 0, 0, 0);

        var intro = new List<RectTransform>();

        // ---- Tiêu đề ----
        var titleRoot = UIKit.CBox("TitleGroup", ui, Color.clear, new Vector2(0, 420), new Vector2(1700, 160));
        UIKit.Title(titleRoot, "BẢNG TIẾN ĐỘ", new Vector2(0, 20), 72);
        intro.Add(titleRoot);

        // ---- Học sinh (góc phải trên) ----
        var student = UIKit.CBox("StudentGroup", ui, Color.clear, new Vector2(650, 445), new Vector2(560, 100));
        UIKit.Label("Name", student, "Xin chào, " + StudentProfileData.studentName + "!", 38, GameTheme.Bone,
            TextAnchor.MiddleRight, new Vector2(0, 20), new Vector2(560, 50), true, true, FontStyle.Bold);
        UIKit.Label("Class", student, string.IsNullOrEmpty(StudentProfileData.studentClass) ? "Lớp 8" : "Lớp " + StudentProfileData.studentClass,
            28, GameTheme.Ember, TextAnchor.MiddleRight, new Vector2(0, -24), new Vector2(560, 40), true);
        intro.Add(student);

        // ---- Nút quay lại (góc trái trên) ----
        UIKit.MakeButton(ui, "Btn_Back", "QUAY LẠI", new Vector2(-740, 450), new Vector2(300, 72), Back, ButtonStyle.Stone, 30, .3f);

        BuildStatCards(intro);
        BuildSubjectPanel(intro);
        BuildHistoryPanel(intro);
        BuildBadgePanel(intro);

        // lớp hiệu ứng nằm trên cùng
        fx = UIKit.CBox("Effects", ui, Color.clear, Vector2.zero, Vector2.zero);
        UIKit.Stretch(fx, 0, 0, 0, 0);

        introItems = intro.ToArray();
        foreach (var rt in introItems) rt.localScale = Vector3.zero;
    }

    // ----- 4 thẻ số liệu -----
    void BuildStatCards(List<RectTransform> intro)
    {
        string[] labels = { "HUY HIỆU", "ĐIỂM CAO NHẤT", "LƯỢT CHƠI", "ĐỘ CHÍNH XÁC" };
        Color[] cols = { GameTheme.GoldHi, GameTheme.Ember, GameTheme.Ly, GameTheme.Moss };

        int attempts = ProgressSaveSystem.Attempts(data);
        int best = ProgressSaveSystem.BestScore(data);
        int accuracy = Mathf.RoundToInt(ProgressSaveSystem.Accuracy(data) * 100f);
        int[] targets = { data.badges.Count, best, attempts, accuracy };
        string[] formats = { "{0}/" + ProgressSaveSystem.Badges.Length, "{0}", "{0}", "{0}%" };

        for (int i = 0; i < 4; i++)
        {
            var p = UIKit.Panel(ui, "Stat_" + i, new Vector2(-690 + i * 460, 245), new Vector2(420, 150), PanelStyle.Dark, out var c);
            UIKit.Label("L", c, labels[i], 24, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, 32), new Vector2(360, 30), true);
            var v = UIKit.Label("V", c, string.Format(formats[i], 0), 58, cols[i], TextAnchor.MiddleCenter,
                new Vector2(0, -16), new Vector2(360, 64), true, true, FontStyle.Bold);
            counts.Add(new CountAnim { text = v, target = targets[i], format = formats[i] });
            intro.Add(p);
        }
    }

    // ----- Tiến độ học tập + từng môn -----
    void BuildSubjectPanel(List<RectTransform> intro)
    {
        var panel = UIKit.Panel(ui, "SubjectPanel", new Vector2(-450, -50), new Vector2(860, 400), PanelStyle.Dark, out var c);
        UIKit.Label("H", c, "TIẾN ĐỘ HỌC TẬP", 38, GameTheme.GoldHi, TextAnchor.MiddleCenter, new Vector2(0, 146), new Vector2(700, 50), true, true, FontStyle.Bold);
        UIKit.Divider(c, new Vector2(0, 116), 420);

        int done = ProgressSaveSystem.CompletedLessons(data);
        int total = Mathf.Max(1, StudentProfileData.totalLessons);
        lessonText = UIKit.Label("Lesson", c, "Bài học hoàn thành: " + done + "/" + total, 26, GameTheme.Bone,
            TextAnchor.MiddleLeft, new Vector2(0, 76), new Vector2(780, 34));
        UIKit.ProgressBar(c, "LessonBar", new Vector2(0, 42), new Vector2(780, 28), GameTheme.Ember, out lessonFill);
        bars.Add(new BarAnim { fill = lessonFill, target = (float)done / total });

        for (int i = 0; i < ProgressSaveSystem.Subjects.Length; i++)
        {
            string subject = ProgressSaveSystem.Subjects[i];
            var stats = ProgressSaveSystem.FindSubject(data, subject);
            Color col = SubjectColor(subject);

            var row = UIKit.CBox("Row_" + subject, c, Color.clear, new Vector2(0, -10 - i * 62), new Vector2(780, 52));
            var rune = UIKit.CBox("Rune", row, col, new Vector2(-362, 0), new Vector2(16, 16));
            rune.localEulerAngles = new Vector3(0, 0, 45f);
            UIKit.Label("N", row, subject.ToUpperInvariant(), 34, GameTheme.Bone, TextAnchor.MiddleLeft, new Vector2(-290, 0), new Vector2(120, 50), true, true, FontStyle.Bold);

            UIKit.ProgressBar(row, "Bar", new Vector2(-20, 0), new Vector2(340, 26), col, out var fill);

            string info = "Chưa chơi";
            float target = 0f;
            if (stats != null && stats.attempts > 0)
            {
                target = ProgressSaveSystem.Accuracy(stats);
                info = stats.attempts + " lượt · " + Mathf.RoundToInt(target * 100f) + "% đúng";
            }
            bars.Add(new BarAnim { fill = fill, target = target });
            UIKit.Label("Info", row, info, 22, stats != null && stats.attempts > 0 ? GameTheme.Rune : GameTheme.Fog,
                TextAnchor.MiddleRight, new Vector2(272, 0), new Vector2(220, 50));

            subjectRows.Add(MakeRow(row));
        }
        intro.Add(panel);
    }

    // ----- Lịch sử gần đây -----
    void BuildHistoryPanel(List<RectTransform> intro)
    {
        var panel = UIKit.Panel(ui, "HistoryPanel", new Vector2(450, -50), new Vector2(860, 400), PanelStyle.Dark, out var c);
        UIKit.Label("H", c, "LƯỢT CHƠI GẦN ĐÂY", 38, GameTheme.GoldHi, TextAnchor.MiddleCenter, new Vector2(0, 146), new Vector2(700, 50), true, true, FontStyle.Bold);
        UIKit.Divider(c, new Vector2(0, 116), 420);

        int shown = Mathf.Min(5, data.sessions.Count);
        emptyText = UIKit.Label("Empty", c, "Chưa có lượt chơi nào.\nHãy thử một màn để bắt đầu nhé!", 30, GameTheme.Fog,
            TextAnchor.MiddleCenter, new Vector2(0, -20), new Vector2(640, 120), true);
        emptyText.gameObject.SetActive(shown == 0);

        for (int i = 0; i < shown; i++)
        {
            var s = data.sessions[data.sessions.Count - 1 - i];
            Color sc = SubjectColor(s.subject);
            Color res = s.won ? GameTheme.Moss : GameTheme.Blood;

            var row = UIKit.CBox("Session_" + i, c, i % 2 == 0 ? GameTheme.Iron.WithAlpha(.32f) : Color.clear, new Vector2(0, 76 - i * 52), new Vector2(780, 46));

            string dateStr = s.date;
            if (DateTime.TryParseExact(s.date, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                dateStr = dt.ToString("dd/MM  HH:mm", CultureInfo.InvariantCulture);

            UIKit.Label("Date", row, dateStr, 22, GameTheme.Fog, TextAnchor.MiddleLeft, new Vector2(-285, 0), new Vector2(210, 44));
            UIKit.Label("Subject", row, s.subject.ToUpperInvariant(), 28, sc, TextAnchor.MiddleCenter, new Vector2(-140, 0), new Vector2(80, 44), true, true, FontStyle.Bold);
            UIKit.Label("Score", row, s.score + " điểm", 26, GameTheme.Bone, TextAnchor.MiddleLeft, new Vector2(-5, 0), new Vector2(160, 44), true);
            UIKit.Label("Acc", row, s.correct + "/" + (s.correct + s.wrong) + " đúng", 22, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(150, 0), new Vector2(150, 44));

            // nhãn kết quả: viền màu + nền tối pha màu
            var pill = UIKit.CBox("Pill", row, res, new Vector2(310, 0), new Vector2(130, 36));
            var inner = UIKit.CBox("Inner", pill, Color.Lerp(res, GameTheme.Ink, .7f), Vector2.zero, Vector2.zero);
            UIKit.Stretch(inner, 2, 2, 2, 2);
            var pt = UIKit.Label("T", inner, s.won ? "THẮNG" : "THUA", 22, Color.Lerp(res, Color.white, .35f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, true, false, FontStyle.Bold);
            UIKit.Stretch(pt.rectTransform, 0, 0, 0, 0);

            historyRows.Add(MakeRow(row));
        }
        intro.Add(panel);
    }

    // ----- Huy hiệu -----
    void BuildBadgePanel(List<RectTransform> intro)
    {
        var defs = ProgressSaveSystem.Badges;
        var panel = UIKit.Panel(ui, "BadgePanel", new Vector2(0, -385), new Vector2(1780, 230), PanelStyle.Dark, out var c);
        badgeHeader = UIKit.Label("H", c, "HUY HIỆU  " + data.badges.Count + "/" + defs.Length, 30, GameTheme.GoldHi,
            TextAnchor.MiddleCenter, new Vector2(0, 62), new Vector2(600, 40), true, true, FontStyle.Bold);

        for (int i = 0; i < defs.Length; i++)
        {
            var def = defs[i];
            bool unlocked = data.badges.Contains(def.id);
            bool isNew = unlocked && !data.seenBadges.Contains(def.id);
            float x = (i - (defs.Length - 1) * .5f) * 240f;

            var tile = UIKit.CBox("Badge_" + def.id, c, Color.clear, new Vector2(x, -14), new Vector2(230, 130));
            var bt = new BadgeTile { tile = tile, unlocked = unlocked, isNew = isNew, phase = i * .9f };

            if (unlocked)
            {
                var g = UIKit.CBox("Glow", tile, def.color.WithAlpha(.3f), new Vector2(0, 24), new Vector2(150, 150));
                bt.glow = g.GetComponent<Image>(); bt.glow.sprite = ThemeGfx.Glow;
            }

            // rune ngọc: hình thoi viền kim loại, lõi màu huy hiệu (khoá = đá phiến tối)
            var icon = UIKit.CBox("Icon", tile, Color.clear, new Vector2(0, 24), new Vector2(64, 64));
            Color rim = unlocked ? GameTheme.Gold : GameTheme.Iron;
            Color core = unlocked ? def.color : GameTheme.Slate;
            Diamond(icon, 54f, new Color(.03f, .02f, .05f));
            Diamond(icon, 48f, rim);
            Diamond(icon, 40f, core);
            Diamond(icon, 12f, unlocked ? new Color(1, 1, 1, .55f) : GameTheme.IronHi.WithAlpha(.5f));
            bt.icon = icon;

            UIKit.Label("Title", tile, def.title, 24, unlocked ? GameTheme.GoldHi : GameTheme.Fog, TextAnchor.MiddleCenter,
                new Vector2(0, -32), new Vector2(230, 30), true, unlocked, FontStyle.Bold);
            UIKit.Label("Desc", tile, def.desc, 17, unlocked ? GameTheme.Bone : GameTheme.Fog.WithAlpha(.65f), TextAnchor.MiddleCenter,
                new Vector2(0, -58), new Vector2(230, 26));

            if (isNew)
            {
                var tag = UIKit.CBox("NewTag", tile, GameTheme.Gold, new Vector2(60, 52), new Vector2(72, 30));
                var inner = UIKit.CBox("Inner", tag, GameTheme.Blood, Vector2.zero, Vector2.zero);
                UIKit.Stretch(inner, 2, 2, 2, 2);
                var tt = UIKit.Label("T", inner, "MỚI!", 19, GameTheme.Bone, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, true, false, FontStyle.Bold);
                UIKit.Stretch(tt.rectTransform, 0, 0, 0, 0);
            }

            tile.localScale = Vector3.zero;
            badges.Add(bt);
        }
        intro.Add(panel);
    }

    static RectTransform Diamond(Transform parent, float size, Color col)
    {
        var d = UIKit.CBox("Dia", parent, col, Vector2.zero, new Vector2(size, size));
        d.localEulerAngles = new Vector3(0, 0, 45f);
        return d;
    }

    RowAnim MakeRow(RectTransform row)
    {
        var cg = row.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        return new RowAnim { rt = row, cg = cg, home = row.anchoredPosition };
    }

    // ---------- Animation ----------
    IEnumerator IntroRoutine()
    {
        const float start = .2f, step = .09f;
        for (int i = 0; i < introItems.Length; i++)
            StartCoroutine(Delayed(start + i * step, Pop(introItems[i], .45f)));

        yield return new WaitForSecondsRealtime(start + introItems.Length * step + .3f);

        foreach (var cnt in counts) StartCoroutine(CountUp(cnt));
        foreach (var b in bars) StartCoroutine(FillBar(b));
        StartCoroutine(RevealRows(subjectRows, 0f));
        StartCoroutine(RevealRows(historyRows, .1f));

        for (int i = 0; i < badges.Count; i++)
            StartCoroutine(Delayed(.2f + i * .08f, Pop(badges[i].tile, .45f)));

        yield return new WaitForSecondsRealtime(.2f + badges.Count * .08f + .5f);

        bool anyNew = false;
        foreach (var b in badges)
            if (b.isNew) { anyNew = true; StartCoroutine(EmberBurst(ToFx(b.tile))); }
        if (anyNew) GameAudio.Play(Snd.BuffPick);

        // đánh dấu đã xem để lần sau không hiện "MỚI!" nữa
        bool changed = false;
        foreach (var id in data.badges)
            if (!data.seenBadges.Contains(id)) { data.seenBadges.Add(id); changed = true; }
        if (changed) ProgressSaveSystem.Save();

        introDone = true;
    }

    static IEnumerator Pop(RectTransform rt, float duration)
    {
        yield return Tween(duration, k => rt.localScale = Vector3.one * EaseOutBack(k));
        rt.localScale = Vector3.one;
    }

    IEnumerator RevealRows(List<RowAnim> rows, float initialDelay)
    {
        yield return new WaitForSecondsRealtime(initialDelay);
        for (int i = 0; i < rows.Count; i++)
        {
            StartCoroutine(RowSlide(rows[i]));
            yield return new WaitForSecondsRealtime(.07f);
        }
    }

    static IEnumerator RowSlide(RowAnim r)
    {
        yield return Tween(.35f, k =>
        {
            float e = EaseOutCubic(k);
            r.cg.alpha = e;
            r.rt.anchoredPosition = r.home + new Vector2(-60f * (1f - e), 0f);
        });
        r.cg.alpha = 1f;
        r.rt.anchoredPosition = r.home;
    }

    static IEnumerator CountUp(CountAnim c)
    {
        yield return Tween(.9f, k => c.text.text = string.Format(c.format, Mathf.RoundToInt(c.target * EaseOutCubic(k))));
        c.text.text = string.Format(c.format, c.target);
    }

    static IEnumerator FillBar(BarAnim b)
    {
        yield return Tween(.9f, k => UIKit.SetBar(b.fill, b.target * EaseOutCubic(k)));
        UIKit.SetBar(b.fill, b.target);
    }

    Vector2 ToFx(RectTransform target)
    {
        Vector3 local = fx.InverseTransformPoint(target.position);
        return new Vector2(local.x, local.y + 24f);
    }

    /// <summary>Tàn lửa vàng-cam bắn toả ra quanh huy hiệu mới (thay cho pháo sáng sprite cũ).</summary>
    IEnumerator EmberBurst(Vector2 center)
    {
        const int n = 18;
        var rts = new RectTransform[n];
        var imgs = new Image[n];
        var dirs = new Vector2[n];
        var speeds = new float[n];

        for (int i = 0; i < n; i++)
        {
            float s = UnityEngine.Random.Range(6f, 13f);
            rts[i] = UIKit.CBox("Spark", fx, Color.Lerp(GameTheme.Ember, GameTheme.GoldHi, UnityEngine.Random.value), center, new Vector2(s, s));
            imgs[i] = rts[i].GetComponent<Image>();
            float a = i / (float)n * Mathf.PI * 2f + UnityEngine.Random.Range(-.2f, .2f);
            dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            speeds[i] = UnityEngine.Random.Range(90f, 230f);
        }

        const float dur = .9f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = t / dur, e = EaseOutCubic(k);
            for (int i = 0; i < n; i++)
            {
                if (rts[i] == null) continue;
                rts[i].anchoredPosition = center + dirs[i] * speeds[i] * e + new Vector2(0f, -40f * k * k);   // rơi nhẹ như tàn lửa
                rts[i].localScale = Vector3.one * (1f - k * .7f);
                rts[i].localRotation = Quaternion.Euler(0f, 0f, k * 180f);
                imgs[i].color = imgs[i].color.WithAlpha(1f - k);
            }
            yield return null;
        }
        for (int i = 0; i < n; i++) if (rts[i] != null) Destroy(rts[i].gameObject);
    }

    // ---------- Điều hướng ----------
    void Back()
    {
        if (leaving) return;
        if (!Application.CanStreamedLevelBeLoaded(backSceneName))
        {
            Debug.LogError("[Dashboard] Scene '" + backSceneName + "' chưa có trong Build Settings.");
            return;
        }
        leaving = true;
        SceneRouter.Go(backSceneName);
    }

    // ---------- Thử nghiệm trong Editor ----------
    [ContextMenu("Thêm dữ liệu mẫu")]
    void DebugAddDemo()
    {
        ProgressSaveSystem.DebugAddDemoData();
        if (Application.isPlaying) UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    [ContextMenu("Xoá tiến độ học sinh hiện tại")]
    void DebugReset()
    {
        ProgressSaveSystem.ResetCurrent();
        if (Application.isPlaying) UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}
