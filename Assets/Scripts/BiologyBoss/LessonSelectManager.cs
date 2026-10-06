using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene "LessonSelectScene" dùng chung cho 3 môn: chọn bài của chương vừa chọn (lấy từ cột "Bài"), hoặc ôn cả chương,
/// rồi vào game của môn đó với đúng câu hỏi đã chọn. Phong cách khung đổi theo môn.
/// Mở trực tiếp scene này để thử (không đi qua Chọn chương) thì dùng Fallback Csv như môn Sinh.
/// </summary>
public class LessonSelectManager : MonoBehaviour
{
    public Sprite background;
    [Header("Chỉ dùng khi mở scene này trực tiếp để thử (môn Sinh)")]
    public TextAsset fallbackCsv;
    public string fallbackTitle = "Chương VII";

    RectTransform root; FlowTheme theme; Text status;

    IEnumerator Start()
    {
        Time.timeScale = 1f;
        SinhMenuKit.EnsureEventSystem();

        if (SubjectSession.Chapter == null)
        {
            SubjectSession.Begin(SubjectId.Sinh);
            yield return SubjectSession.LoadSources(SubjectId.Sinh, new[] { new SubjectSource { title = fallbackTitle, subtitle = "Sinh học 8", csvFile = fallbackCsv } });
            if (SubjectSession.Chapters.Count > 0 && !SubjectSession.Chapters[0].locked) SubjectSession.Choose(SubjectSession.Chapters[0]);
        }
        theme = FlowTheme.Get(SubjectSession.Subject);

        root = SinhMenuKit.BuildCanvas(transform, background);
        root.Find("Background").GetComponent<Image>().color = theme.BgTint;
        FlowFx.Create(root, theme.Mode);
        SinhMenuKit.Label("Title", root, "CHỌN BÀI HỌC", 96, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 410), new Vector2(1400, 130), true);
        status = SinhMenuKit.Label("Status", root, "", 38, new Color(1f, 1f, 1f, .94f), TextAnchor.MiddleCenter, new Vector2(0, 330), new Vector2(1500, 60), true, FontStyle.Normal);
        theme.Back(root, "← VỀ CHỌN CHƯƠNG", () => SinhMenuKit.LoadScene(SubjectSession.ChapterScene(SubjectSession.Subject)));

        if (SubjectSession.Chapter == null || SubjectSession.Chapter.rows.Count == 0)
        {
            status.text = "Không đọc được câu hỏi của chương này (kiểm tra file CSV / link Google Sheet).";
            yield break;
        }
        BuildLessons();
    }

    static void Fit(Text t, int min, int max) { t.resizeTextForBestFit = true; t.resizeTextMinSize = min; t.resizeTextMaxSize = max; t.verticalOverflow = VerticalWrapMode.Truncate; }

    void BuildLessons()
    {
        var ch = SubjectSession.Chapter; var lessons = SubjectSession.Lessons(); int total = ch.rows.Count;
        status.text = $"{theme.Name} • {ch.title} • {lessons.Count} bài • {total} câu hỏi";

        var cell = new Vector2(390, 170); var spacing = new Vector2(36, 30); const float viewH = 580f;
        var content = SinhMenuKit.MakeScroll(root, new Vector2(0, -5), new Vector2(1700, viewH));
        SinhMenuKit.SetupGrid(content, lessons.Count, 4, cell, spacing, viewH);

        for (int i = 0; i < lessons.Count; i++)
        {
            string key = lessons[i].Key; int count = lessons[i].Value;
            var card = theme.Decorate(content, "Lesson" + i, out RectTransform c);
            string label = key == SubjectSession.Other ? "Chưa phân bài" : key;
            var name = SinhMenuKit.Label("Name", c, label, 46, theme.TextMain, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            SinhMenuKit.Place(name.rectTransform, new Vector2(0, .42f), Vector2.one, SinhMenuKit.C, Vector2.zero, Vector2.zero); Fit(name, 24, 46);
            var cnt = SinhMenuKit.Label("Count", c, count + " câu hỏi", 30, theme.TextSub, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, false, FontStyle.Normal);
            SinhMenuKit.Place(cnt.rectTransform, Vector2.zero, new Vector2(1, .42f), SinhMenuKit.C, Vector2.zero, Vector2.zero);

            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card.GetComponent<Image>(); btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => Play(key));
            var sh = card.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .35f); sh.effectDistance = new Vector2(0, -6);
            var ui = card.gameObject.AddComponent<UiCard>(); ui.hoverScale = 1.05f; ui.Intro(.05f + i * .05f);
        }

        SinhMenuKit.MakeButton("AllLessons", root, new Vector2(0, -440), new Vector2(820, 100), theme.Accent,
            $"ÔN TẬP CẢ CHƯƠNG  •  {total} câu", 40, Color.white, () => Play(""), theme.Frame);
    }

    void Play(string lesson)
    {
        GameAudio.Play(Snd.LessonSelect); SubjectSession.Lesson = lesson;
        SinhMenuKit.LoadScene(SubjectSession.GameScene(SubjectSession.Subject));
    }
}
