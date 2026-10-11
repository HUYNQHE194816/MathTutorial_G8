using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene "Chọn chương" của từng môn (Lý / Hóa / Sinh): chọn môn bằng trường Subject, khai báo dữ liệu ở Sources.
/// Nếu CSV/Sheet có cột "Chương" thì tự tách thành nhiều chương; không có thì mỗi nguồn là 1 chương.
/// Nguồn chưa có dữ liệu hiện thẻ "Sắp ra mắt". Giao diện dark fantasy dựng bằng code (FlowScreen / FlowCard).
/// </summary>
public class ChapterSelectManager : MonoBehaviour
{
    public SubjectId subject = SubjectId.Sinh;
    [Tooltip("Không còn dùng (nền giờ là rừng trăng dùng chung). Giữ lại để không mất dữ liệu scene cũ.")]
    public Sprite background;
    [Tooltip("Biểu tượng giữa vòng rune. Để trống = dùng biểu tượng rune của môn.")]
    public Sprite icon;
    public SubjectSource[] sources;
    public string backSceneName = "SubjectSelectScene";

    FlowScreen screen; FlowTheme theme;

    IEnumerator Start()
    {
        theme = FlowTheme.Get(subject);
        SubjectSession.Begin(subject);

        screen = FlowScreen.Create(transform, theme, "CHỌN CHƯƠNG", "VỀ CHỌN MÔN", backSceneName);
        screen.SetStatus(theme.Name + " • đang tải dữ liệu…");

        yield return SubjectSession.LoadSources(subject, sources);

        var list = SubjectSession.Chapters; int n = list.Count;
        if (n == 0)
        {
            screen.SetStatus("Chưa có chương nào — kiểm tra Sources (file CSV / link Google Sheet) của ChapterSelectManager.");
            yield break;
        }
        screen.SetStatus(theme.Name + " • chọn một chương để bắt đầu ôn tập");

        var content = screen.Grid(new Vector2(0, -95), new Vector2(1700, 660), n, Mathf.Min(n, 3),
            new Vector2(440, 590), new Vector2(70, 40), 30);
        for (int i = 0; i < n; i++) BuildCard(content, list[i], i);
    }

    void BuildCard(Transform parent, FlowChapter ch, int index)
    {
        bool ok = !ch.locked;
        var card = FlowCard.Build(parent, "Chapter" + index, theme.Accent, !ok, .1f + index * .12f, out RectTransform c);
        card.onClick = () => Pick(ch);

        var ringPos = new Vector2(0, 170);
        card.AddRune(c, ringPos, 156f);
        if (icon != null)
        {
            var im = FlowCard.Spr(c, "Icon", icon, ok ? Color.white : new Color(1f, 1f, 1f, .5f), ringPos, 110f);
            im.preserveAspect = true;
        }
        else card.Glyph(c, subject, ringPos, 110f);

        var name = UIKit.Label("Name", c, ch.title, 50, ok ? GameTheme.GoldHi : GameTheme.Fog, TextAnchor.MiddleCenter,
            new Vector2(0, 40), new Vector2(360, 112), true, true, FontStyle.Bold);
        FlowCard.Fit(name, 26, 50);
        UIKit.Divider(c, new Vector2(0, -28), 300);
        UIKit.Label("Sub", c, ch.subtitle ?? "", 28, GameTheme.Bone, TextAnchor.MiddleCenter,
            new Vector2(0, -66), new Vector2(360, 44), false, false, FontStyle.Italic);
        UIKit.Label("Info", c, ok ? ch.LessonCount + " bài • " + ch.rows.Count + " câu hỏi" : "", 26, GameTheme.Fog,
            TextAnchor.MiddleCenter, new Vector2(0, -112), new Vector2(360, 40));
        UIKit.MakeButton(c, "Btn_Go", ok ? "VÀO HỌC" : "SẮP RA MẮT", new Vector2(0, -212), new Vector2(280, 70),
            () => Pick(ch), ok ? ButtonStyle.Bronze : ButtonStyle.Stone, 30, .3f + index * .12f);
    }

    void Pick(FlowChapter ch)
    {
        if (screen.Leaving) return;
        if (ch.locked) { GameAudio.Play(Snd.CardLocked); UIKit.Toast(screen.Ui, "Chương này sắp ra mắt!"); return; }
        GameAudio.Play(Snd.ChapterSelect);
        SubjectSession.Choose(ch);
        screen.Go(SubjectSession.LessonScene);
    }
}
