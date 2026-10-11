using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene "LessonSelectScene" dùng chung cho 3 môn: chọn bài của chương vừa chọn (lấy từ cột "Bài"), hoặc ôn cả chương,
/// rồi vào game của môn đó với đúng câu hỏi đã chọn. Giao diện dark fantasy, rune accent đổi theo môn.
/// Mở trực tiếp scene này để thử (không đi qua Chọn chương) thì dùng Fallback Csv như môn Sinh.
/// </summary>
public class LessonSelectManager : MonoBehaviour
{
    [Tooltip("Không còn dùng (nền giờ là rừng trăng dùng chung). Giữ lại để không mất dữ liệu scene cũ.")]
    public Sprite background;
    [Header("Chỉ dùng khi mở scene này trực tiếp để thử (môn Sinh)")]
    public TextAsset fallbackCsv;
    public string fallbackTitle = "Chương VII";

    FlowScreen screen; FlowTheme theme;

    IEnumerator Start()
    {
        Time.timeScale = 1f;

        if (SubjectSession.Chapter == null)
        {
            SubjectSession.Begin(SubjectId.Sinh);
            yield return SubjectSession.LoadSources(SubjectId.Sinh, new[] { new SubjectSource { title = fallbackTitle, subtitle = "Sinh học 8", csvFile = fallbackCsv } });
            if (SubjectSession.Chapters.Count > 0 && !SubjectSession.Chapters[0].locked) SubjectSession.Choose(SubjectSession.Chapters[0]);
        }
        theme = FlowTheme.Get(SubjectSession.Subject);

        screen = FlowScreen.Create(transform, theme, "CHỌN BÀI HỌC", "VỀ CHỌN CHƯƠNG", SubjectSession.ChapterScene(SubjectSession.Subject));

        if (SubjectSession.Chapter == null || SubjectSession.Chapter.rows.Count == 0)
        {
            screen.SetStatus("Không đọc được câu hỏi của chương này (kiểm tra file CSV / link Google Sheet).");
            yield break;
        }
        BuildLessons();
    }

    void BuildLessons()
    {
        var ch = SubjectSession.Chapter; var lessons = SubjectSession.Lessons(); int total = ch.rows.Count;
        screen.SetStatus(theme.Name + " • " + ch.title + " • " + lessons.Count + " bài • " + total + " câu hỏi");

        var content = screen.Grid(new Vector2(0, -30), new Vector2(1700, 520), lessons.Count, 4,
            new Vector2(390, 150), new Vector2(36, 28), 30);

        for (int i = 0; i < lessons.Count; i++)
        {
            string key = lessons[i].Key; int count = lessons[i].Value;
            var card = FlowCard.Build(content, "Lesson" + i, theme.Accent, false, .05f + i * .05f, out RectTransform c);
            card.onClick = () => Play(key);

            var runePos = new Vector2(-118, 0);
            card.AddRune(c, runePos, 62f);
            UIKit.Label("Num", c, (i + 1).ToString(), 30, GameTheme.GoldHi, TextAnchor.MiddleCenter, runePos, new Vector2(60, 48), true, true, FontStyle.Bold);

            string label = key == SubjectSession.Other ? "Chưa phân bài" : key;
            var name = UIKit.Label("Name", c, label, 34, GameTheme.Bone, TextAnchor.MiddleLeft, new Vector2(40, 16), new Vector2(230, 56), true, true, FontStyle.Bold);
            FlowCard.Fit(name, 20, 34);
            UIKit.Label("Count", c, count + " câu hỏi", 24, GameTheme.Fog, TextAnchor.MiddleLeft, new Vector2(40, -24), new Vector2(230, 36));
        }

        UIKit.MakeButton(screen.Ui, "Btn_All", "ÔN TẬP CẢ CHƯƠNG  •  " + total + " câu", new Vector2(0, -385), new Vector2(820, 88),
            () => Play(""), ButtonStyle.Bronze, 36, .5f);
    }

    void Play(string lesson)
    {
        if (screen.Leaving) return;
        GameAudio.Play(Snd.LessonSelect);
        SubjectSession.Lesson = lesson;
        screen.Go(SubjectSession.GameScene(SubjectSession.Subject));
    }
}
