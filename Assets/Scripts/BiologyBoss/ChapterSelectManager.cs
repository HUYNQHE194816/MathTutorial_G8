using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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

    // ---- phân trang: mỗi trang tối đa 3 thẻ, không cuộn ----
    const int PerPage = 3;
    readonly List<System.Action<Transform, int>> items = new List<System.Action<Transform, int>>();
    int page, pages = 1; GameObject pageRoot; Text pageLabel; Button btnPrev, btnNext;

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

        var all = SubjectSession.BuildCombined();   // null nếu môn chỉ có < 2 chương
        if (all != null) items.Add((parent, slot) => BuildCombinedCard(parent, all, list));
        for (int i = 0; i < n; i++) { var chapter = list[i]; items.Add((parent, slot) => BuildCard(parent, chapter, slot)); }
        pages = Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)PerPage));

        if (pages > 1)
        {
            btnPrev = UIKit.MakeButton(screen.Ui, "Btn_PrevPage", "<", new Vector2(-190f, -462f), new Vector2(110f, 72f), () => Turn(-1), ButtonStyle.Bronze, 40, .6f);
            btnNext = UIKit.MakeButton(screen.Ui, "Btn_NextPage", ">", new Vector2(190f, -462f), new Vector2(110f, 72f), () => Turn(1), ButtonStyle.Bronze, 40, .6f);
            pageLabel = UIKit.Label("PageLbl", screen.Ui, "", 32, GameTheme.GoldHi, TextAnchor.MiddleCenter,
                new Vector2(0f, -462f), new Vector2(260f, 50f), true, true, FontStyle.Bold);
        }
        ShowPage(0);
    }

    /// <summary>Dựng lại lưới cho trang <paramref name="p"/> (tối đa PerPage thẻ, vừa khung nhìn nên không cần cuộn).</summary>
    void ShowPage(int p)
    {
        page = Mathf.Clamp(p, 0, pages - 1);
        if (pageRoot != null) Destroy(pageRoot);
        int from = page * PerPage, cnt = Mathf.Min(PerPage, items.Count - from);
        var content = screen.Grid(new Vector2(0, -95), new Vector2(1700, 660), cnt, cnt,
            new Vector2(440, 590), new Vector2(70, 40), 30);
        var sr = content.GetComponentInParent<ScrollRect>();
        if (sr != null) { sr.enabled = false; pageRoot = sr.gameObject; } else pageRoot = content.gameObject;
        for (int k = 0; k < cnt; k++) items[from + k](content, k);

        if (pageLabel != null) pageLabel.text = "TRANG " + (page + 1) + " / " + pages;
        if (btnPrev != null) btnPrev.interactable = page > 0;
        if (btnNext != null) btnNext.interactable = page < pages - 1;
    }

    void Turn(int d)
    {
        if (screen == null || screen.Leaving) return;
        int np = page + d;
        if (np < 0 || np >= pages) return;
        GameAudio.Play(Snd.UiClick);
        ShowPage(np);
    }

    void Update()
    {
        if (pages < 2) return;
        var kb = Keyboard.current; if (kb == null) return;
        if (kb.rightArrowKey.wasPressedThisFrame) Turn(1);
        else if (kb.leftArrowKey.wasPressedThisFrame) Turn(-1);
    }

    /// <summary>Thẻ "TỔNG HỢP MÔN": ôn toàn bộ câu hỏi của mọi chương, vào thẳng game (bỏ qua chọn bài).</summary>
    void BuildCombinedCard(Transform parent, FlowChapter all, System.Collections.Generic.List<FlowChapter> list)
    {
        var card = FlowCard.Build(parent, "ChapterAll", GameTheme.GoldHi, false, .1f, out RectTransform c);
        card.onClick = () => PickAll(all);

        var ringPos = new Vector2(0, 170);
        card.AddRune(c, ringPos, 156f);
        if (icon != null)
        {
            var im = FlowCard.Spr(c, "Icon", icon, Color.white, ringPos, 110f);
            im.preserveAspect = true;
        }
        else card.Glyph(c, subject, ringPos, 110f);

        int chapters = 0; foreach (var ch in list) if (!ch.locked) chapters++;
        var name = UIKit.Label("Name", c, "TỔNG HỢP MÔN", 50, GameTheme.GoldHi, TextAnchor.MiddleCenter,
            new Vector2(0, 40), new Vector2(360, 112), true, true, FontStyle.Bold);
        FlowCard.Fit(name, 26, 50);
        UIKit.Divider(c, new Vector2(0, -28), 300);
        UIKit.Label("Sub", c, "Ôn toàn bộ kiến thức", 28, GameTheme.Bone, TextAnchor.MiddleCenter,
            new Vector2(0, -66), new Vector2(360, 44), false, false, FontStyle.Italic);
        UIKit.Label("Info", c, chapters + " chương • " + all.rows.Count + " câu hỏi", 26, GameTheme.Fog,
            TextAnchor.MiddleCenter, new Vector2(0, -112), new Vector2(360, 40));
        UIKit.MakeButton(c, "Btn_Go", "ÔN TỔNG HỢP", new Vector2(0, -212), new Vector2(280, 70),
            () => PickAll(all), ButtonStyle.Bronze, 30, .3f);
    }

    void PickAll(FlowChapter all)
    {
        if (screen.Leaving) return;
        GameAudio.Play(Snd.ChapterSelect);
        SubjectSession.ChooseCombined(all);
        screen.Go(SubjectSession.GameScene(subject));
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
