using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene "Chọn chương" của từng môn (Lý / Hóa / Sinh): chọn môn bằng trường Subject, khai báo dữ liệu ở Sources.
/// Nếu CSV/Sheet có cột "Chương" thì tự tách thành nhiều chương; không có thì mỗi nguồn là 1 chương.
/// Nguồn chưa có dữ liệu hiện thẻ "Sắp ra mắt". UI dựng bằng code, phong cách khung theo môn (FlowTheme).
/// </summary>
public class ChapterSelectManager : MonoBehaviour
{
    public SubjectId subject = SubjectId.Sinh;
    public Sprite background, icon;
    public SubjectSource[] sources;
    public string backSceneName = "SubjectSelectScene";

    RectTransform root; FlowTheme theme; Text status;

    IEnumerator Start()
    {
        Time.timeScale = 1f;
        SinhMenuKit.EnsureEventSystem();
        theme = FlowTheme.Get(subject);
        SubjectSession.Begin(subject);

        root = SinhMenuKit.BuildCanvas(transform, background);
        root.Find("Background").GetComponent<Image>().color = theme.BgTint;
        FlowFx.Create(root, theme.Mode);

        SinhMenuKit.Label("Title", root, "CHỌN CHƯƠNG", 96, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 400), new Vector2(1400, 130), true);
        status = SinhMenuKit.Label("Sub", root, theme.Name + " • đang tải dữ liệu…", 38, new Color(1f, 1f, 1f, .94f), TextAnchor.MiddleCenter, new Vector2(0, 320), new Vector2(1400, 60), true, FontStyle.Normal);
        theme.Back(root, "← VỀ CHỌN MÔN", () => SinhMenuKit.LoadScene(backSceneName));

        yield return SubjectSession.LoadSources(subject, sources);

        var list = SubjectSession.Chapters; int n = list.Count;
        if (n == 0)
        {
            status.text = "Chưa có chương nào — kiểm tra Sources (file CSV / link Google Sheet) của ChapterSelectManager.";
            yield break;
        }
        status.text = theme.Name + " • chọn một chương để bắt đầu ôn tập";

        var cell = new Vector2(520, 620); var spacing = new Vector2(70, 50);
        var content = SinhMenuKit.MakeScroll(root, new Vector2(0, -60), new Vector2(1700, 700));
        SinhMenuKit.SetupGrid(content, n, Mathf.Min(n, 3), cell, spacing, 700f);
        for (int i = 0; i < n; i++) BuildCard(content, list[i], i);
    }

    static void Fit(Text t, int min, int max) { t.resizeTextForBestFit = true; t.resizeTextMinSize = min; t.resizeTextMaxSize = max; t.verticalOverflow = VerticalWrapMode.Truncate; }

    void BuildCard(Transform parent, FlowChapter ch, int index)
    {
        bool ok = !ch.locked;
        var card = theme.Decorate(parent, "Chapter" + index, out RectTransform c);
        Color accent = ok ? theme.Accent : theme.Muted;

        var disc = SinhMenuKit.CBox("Disc", c, accent, new Vector2(0, 150), new Vector2(230, 230));
        var ic = SinhMenuKit.CBox("Icon", disc, Color.white, Vector2.zero, new Vector2(200, 200));
        var im = ic.GetComponent<Image>(); im.preserveAspect = true;
        if (icon != null) { im.sprite = icon; im.color = ok ? Color.white : new Color(1f, 1f, 1f, .6f); } else im.color = Color.clear;

        var name = SinhMenuKit.Label("Name", c, ch.title, 60, ok ? theme.TextMain : theme.Muted, TextAnchor.MiddleCenter, new Vector2(0, -35), new Vector2(430, 120)); Fit(name, 30, 60);
        SinhMenuKit.Label("Sub", c, ch.subtitle ?? "", 34, theme.TextSub, TextAnchor.MiddleCenter, new Vector2(0, -112), new Vector2(430, 46), false, FontStyle.Normal);
        SinhMenuKit.Label("Info", c, ok ? $"{ch.LessonCount} bài • {ch.rows.Count} câu hỏi" : "", 28, theme.Muted, TextAnchor.MiddleCenter, new Vector2(0, -158), new Vector2(430, 40), false, FontStyle.Normal);

        var bar = SinhMenuKit.CBox("Go", c, accent, new Vector2(0, -232), new Vector2(360, 78));
        var l = SinhMenuKit.Label("L", bar, ok ? "VÀO HỌC  ▶" : "SẮP RA MẮT", 38, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
        SinhMenuKit.Stretch(l.rectTransform, 0, 0, 0, 0);

        var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card.GetComponent<Image>(); btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => Pick(ch));
        var sh = card.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .4f); sh.effectDistance = new Vector2(0, -8);
        var ui = card.gameObject.AddComponent<UiCard>(); ui.hoverScale = ok ? 1.05f : 1f; ui.Intro(.1f + index * .12f);
    }

    void Pick(FlowChapter ch)
    {
        if (ch.locked) { SinhMenuKit.Toast(root, "Chương này sắp ra mắt!"); return; }
        SubjectSession.Choose(ch);
        SinhMenuKit.LoadScene(SubjectSession.LessonScene);
    }
}
