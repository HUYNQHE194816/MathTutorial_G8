using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Nhật ký luyện tập, phong cách dark fantasy ("sổ ký ức"). Mở ra chỉ hiện TỔNG HỢP (bốn tấm bia số liệu + ba hàng môn
/// có thanh đúng/sai); nút CHI TIẾT mới cho chọn môn, chọn bài, rồi lật "trang giấy da" bên phải để xem:
/// số lần làm, điểm cao nhất, điểm lần cuối. Trang trí: vòng rune xoay phía sau, tàn lửa bay, đuốc hai bên tiêu đề.
/// </summary>
public class PracticeLogWindow : MonoBehaviour
{
    const int PerPage = 6;
    static readonly Color Abyss = new Color(.03f, .02f, .05f);

    Action onClose;
    RectTransform content, body, infoBody, selMarker;
    string subject = ProgressSaveSystem.SubjectLy;
    int page;
    List<LessonStats> curList;

    // hiệu ứng
    CanvasGroup fade;
    float t0;
    RectTransform dashRing;
    readonly List<RectTransform> embers = new List<RectTransform>();
    readonly List<float> emberPhase = new List<float>(), emberX = new List<float>();
    readonly List<Image> torchGlow = new List<Image>();
    readonly List<Image> bars = new List<Image>();
    readonly List<float> barTargets = new List<float>();

    public static PracticeLogWindow Open(Transform parent, Action onClose)
    {
        var dim = UIKit.Box("LogDim", parent, new Color(0, 0, 0, .7f), Vector2.zero, Vector2.one, UIKit.C, Vector2.zero, Vector2.zero, true);
        UIKit.Stretch(dim, 0, 0, 0, 0);
        var w = dim.gameObject.AddComponent<PracticeLogWindow>();
        w.onClose = onClose;
        w.Build(dim);
        return w;
    }

    // ------------------------------------------------------------ khung + trang trí
    void Build(Transform root)
    {
        t0 = Time.unscaledTime;
        fade = gameObject.AddComponent<CanvasGroup>(); fade.alpha = 0f;

        UIKit.Panel(root, "LogPanel", Vector2.zero, new Vector2(1400, 820), PanelStyle.Dark, out content);

        // vòng rune khổng lồ mờ phía sau nội dung (xoay rất chậm)
        Img(content, "RuneWatermark", RuneArt.Ring, Vector2.zero, new Vector2(760, 760), GameTheme.Gold.WithAlpha(.07f));
        dashRing = Img(content, "RuneDash", RuneArt.DashRing, Vector2.zero, new Vector2(860, 860), GameTheme.Gold.WithAlpha(.06f)).rectTransform;

        // tàn lửa bay lên từ đáy sổ
        for (int i = 0; i < 16; i++)
        {
            var im = Img(content, "Ember", ThemeGfx.Glow, Vector2.zero, Vector2.one * UnityEngine.Random.Range(10f, 22f),
                Color.Lerp(GameTheme.Ember, GameTheme.EmberHi, UnityEngine.Random.value).WithAlpha(0f));
            embers.Add(im.rectTransform); emberPhase.Add(UnityEngine.Random.value); emberX.Add(UnityEngine.Random.Range(-610f, 610f));
        }

        // tiêu đề giữa hai ngọn đuốc, kèm đôi kiếm bắt chéo
        foreach (float sx in new[] { -1f, 1f })
        {
            torchGlow.Add(Img(content, "TorchGlow", ThemeGfx.Glow, new Vector2(sx * 392f, 330f), new Vector2(170, 170), GameTheme.Ember.WithAlpha(.4f)));
            Img(content, "TitleIcon", RuneArt.Swords, new Vector2(sx * 392f, 330f), new Vector2(58, 58), GameTheme.GoldHi);
        }
        UIKit.Label("T", content, "NHẬT KÝ LUYỆN TẬP", 52, GameTheme.GoldHi, TextAnchor.MiddleCenter,
            new Vector2(0, 330), new Vector2(760, 70), true, true, FontStyle.Bold);
        UIKit.Label("Sub", content, "Những trang ghi chép của Người Giữ Ký Ức", 24, GameTheme.Fog, TextAnchor.MiddleCenter,
            new Vector2(0, 288), new Vector2(900, 34), false, true, FontStyle.Italic);
        UIKit.Divider(content, new Vector2(0, 262), 640);

        UIKit.MakeButton(content, "Btn_Close", "ĐÓNG", new Vector2(0, -335), new Vector2(320, 72), Close, ButtonStyle.Bronze, 34, .1f);
        ShowSummary();
    }

    static Image Img(Transform p, string n, Sprite sp, Vector2 pos, Vector2 size, Color c)
    {
        var rt = UIKit.CBox(n, p, c, pos, size);
        var im = rt.GetComponent<Image>(); im.sprite = sp; im.raycastTarget = false;
        return im;
    }

    static RectTransform Gem(Transform p, Vector2 pos, float size, Color c)
    {
        var g = UIKit.CBox("Gem", p, c, pos, new Vector2(size, size));
        g.localEulerAngles = new Vector3(0, 0, 45f);
        return g;
    }

    /// <summary>Tấm bia đá: viền đen tím, vành đồng, mặt đá tối, ngọc hai bên.</summary>
    static RectTransform Plate(Transform p, string n, Vector2 pos, Vector2 size, Color rim, Color fill)
    {
        var outer = UIKit.CBox(n, p, Abyss, pos, size);
        var sh = outer.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .55f); sh.effectDistance = new Vector2(0, -5);
        var r = UIKit.CBox("Rim", outer, rim, Vector2.zero, Vector2.zero); UIKit.Stretch(r, 2, 2, 2, 2);
        var f = UIKit.CBox("Fill", outer, fill, Vector2.zero, Vector2.zero); UIKit.Stretch(f, 4, 4, 4, 4);
        UIKit.Box("Shade", f, new Color(0, 0, 0, .25f), Vector2.zero, new Vector2(1, .5f), UIKit.C, Vector2.zero, Vector2.zero);
        UIKit.Box("Shine", f, new Color(1, 1, 1, .10f), new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 2));
        Gem(outer, new Vector2(-size.x / 2f, 0), 13f, rim); Gem(outer, new Vector2(-size.x / 2f, 0), 5f, Abyss);
        Gem(outer, new Vector2(size.x / 2f, 0), 13f, rim); Gem(outer, new Vector2(size.x / 2f, 0), 5f, Abyss);
        return outer;
    }

    /// <summary>Đĩa rune tròn có biểu tượng bên trong.</summary>
    static void RuneDisc(Transform p, Vector2 pos, float size, Sprite icon, Color accent)
    {
        Img(p, "Glow", ThemeGfx.Glow, pos, Vector2.one * size * 1.7f, accent.WithAlpha(.30f));
        Img(p, "Disc", RuneArt.Disk, pos, Vector2.one * size, new Color(.05f, .04f, .10f, .92f));
        Img(p, "Ring", RuneArt.Ring, pos, Vector2.one * (size + 6f), GameTheme.Bronze);
        Img(p, "Icon", icon, pos, Vector2.one * size * .62f, accent);
    }

    static Color SubColor(string s)
    {
        if (s == ProgressSaveSystem.SubjectLy) return GameTheme.Ly;
        if (s == ProgressSaveSystem.SubjectHoa) return GameTheme.Hoa;
        return GameTheme.Sinh;
    }

    static Sprite SubIcon(string s)
    {
        if (s == ProgressSaveSystem.SubjectLy) return RuneArt.Orbits;
        if (s == ProgressSaveSystem.SubjectHoa) return RuneArt.Flask;
        return RuneArt.Leaf;
    }

    // Vùng nội dung ở giữa (body) được dựng lại mỗi khi đổi màn
    void Clear()
    {
        if (body != null) Destroy(body.gameObject);
        bars.Clear(); barTargets.Clear(); infoBody = null; selMarker = null;
        body = UIKit.CBox("Body", content, Color.clear, Vector2.zero, Vector2.zero);
        UIKit.Stretch(body, 0, 100, 0, 110);
    }

    // ------------------------------------------------------------ TỔNG HỢP
    void ShowSummary()
    {
        Clear();
        var d = ProgressSaveSystem.Current;

        // bốn tấm bia số liệu
        string[] keys = { "Tổng lượt chơi", "Thắng", "Điểm cao nhất", "Tỉ lệ đúng" };
        string[] vals =
        {
            ProgressSaveSystem.Attempts(d).ToString(), ProgressSaveSystem.Wins(d).ToString(),
            ProgressSaveSystem.BestScore(d).ToString(), Mathf.RoundToInt(ProgressSaveSystem.Accuracy(d) * 100f) + "%",
        };
        Sprite[] icons = { RuneArt.Sword, RuneArt.Shield, RuneArt.Swords, RuneArt.Orbits };
        Color[] acc = { GameTheme.Ember, GameTheme.Moss, GameTheme.GoldHi, GameTheme.Ly };
        for (int i = 0; i < 4; i++)
        {
            var pl = Plate(body, "Stat" + i, new Vector2((i - 1.5f) * 330f, 185f), new Vector2(300, 110), GameTheme.Bronze, GameTheme.Slate);
            RuneDisc(pl, new Vector2(-100, 0), 70f, icons[i], acc[i]);
            UIKit.Label("V", pl, vals[i], 46, GameTheme.GoldHi, TextAnchor.MiddleCenter, new Vector2(48, 14), new Vector2(190, 56), true, true, FontStyle.Bold);
            UIKit.Label("K", pl, keys[i], 22, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(48, -28), new Vector2(190, 30));
        }

        // ba hàng theo môn, mỗi hàng có thanh tỉ lệ đúng
        float y = 55f;
        foreach (string sub in ProgressSaveSystem.Subjects)
        {
            var s = ProgressSaveSystem.FindSubject(d, sub);
            int att = s != null ? s.attempts : 0, best = s != null ? s.bestScore : 0;
            float acr = ProgressSaveSystem.Accuracy(s);
            Color col = SubColor(sub);

            var row = Plate(body, "Row_" + sub, new Vector2(0, y), new Vector2(1180, 80), GameTheme.Bronze.WithAlpha(.9f), new Color(.10f, .08f, .16f));
            RuneDisc(row, new Vector2(-535, 0), 56f, SubIcon(sub), col);
            UIKit.Label("Name", row, sub, 40, col, TextAnchor.MiddleLeft, new Vector2(-420, 0), new Vector2(160, 56), true, true, FontStyle.Bold);
            Image fill;
            UIKit.ProgressBar(row, "Bar", new Vector2(-150, 0), new Vector2(340, 24), col, out fill);
            bars.Add(fill); barTargets.Add(acr);
            UIKit.Label("Stat", row, att + " lượt   •   cao nhất " + best + "   •   đúng " + Mathf.RoundToInt(acr * 100f) + "%",
                28, GameTheme.Bone, TextAnchor.MiddleRight, new Vector2(285, 0), new Vector2(560, 50));
            y -= 92f;
        }

        UIKit.MakeButton(body, "Btn_Detail", "CHI TIẾT", new Vector2(0, -222), new Vector2(420, 70),
            () => { page = 0; ShowDetail(); }, ButtonStyle.Stone, 34, .1f);
    }

    // ------------------------------------------------------------ CHI TIẾT THEO BÀI
    void ShowDetail()
    {
        Clear();
        var d = ProgressSaveSystem.Current;

        // chọn môn: nút + vạch màu môn dưới tab đang chọn
        string[] subs = ProgressSaveSystem.Subjects;
        for (int i = 0; i < subs.Length; i++)
        {
            string s = subs[i];
            float x = -560 + i * 230;
            UIKit.MakeButton(body, "Sub" + i, s, new Vector2(x, 225), new Vector2(210, 60),
                () => { subject = s; page = 0; ShowDetail(); },
                s == subject ? ButtonStyle.Bronze : ButtonStyle.Stone, 30, 0f);
            if (s == subject)
            {
                Img(body, "TabGlow", ThemeGfx.Glow, new Vector2(x, 192), new Vector2(240, 36), SubColor(s).WithAlpha(.55f));
                UIKit.CBox("TabBar", body, SubColor(s), new Vector2(x, 194), new Vector2(190, 4));
            }
        }

        // khung đá chứa danh sách bài
        var back = UIKit.CBox("ListBack", body, Abyss, new Vector2(-340, -18), new Vector2(580, 392));
        var br = UIKit.CBox("Rim", back, GameTheme.Bronze.WithAlpha(.8f), Vector2.zero, Vector2.zero); UIKit.Stretch(br, 2, 2, 2, 2);
        var bf = UIKit.CBox("Fill", back, new Color(.06f, .05f, .10f, .96f), Vector2.zero, Vector2.zero); UIKit.Stretch(bf, 4, 4, 4, 4);

        curList = d.lessons.FindAll(x => x.subject == subject);
        for (int i = 0; i < PerPage && page * PerPage + i < curList.Count; i++)
        {
            var l = curList[page * PerPage + i];
            int slot = i;
            UIKit.MakeButton(body, "L" + i, Short(l.lesson, 32), new Vector2(-340, 130 - i * 62), new Vector2(540, 54),
                () => ShowInfo(l, slot), ButtonStyle.Stone, 28, 0f);
        }
        if (curList.Count == 0)
        {
            Img(body, "EmptyIcon", RuneArt.Skull, new Vector2(-340, 70), new Vector2(90, 90), GameTheme.Fog.WithAlpha(.45f));
            UIKit.Label("Empty", body, "Chưa có lượt làm bài nào ở môn này.\nSương vẫn phủ kín trang sách.", 28, GameTheme.Fog,
                TextAnchor.MiddleCenter, new Vector2(-340, -30), new Vector2(520, 110), false, false, FontStyle.Italic);
        }

        // khung chọn bài (ẩn đến khi chọn)
        selMarker = UIKit.CBox("SelMarker", body, Color.clear, new Vector2(-340, 130), new Vector2(556, 66));
        Img(selMarker, "Glow", ThemeGfx.Glow, Vector2.zero, new Vector2(700, 130), GameTheme.Gold.WithAlpha(.22f));
        var gold = GameTheme.GoldHi;
        UIKit.Box("T", selMarker, gold, new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 3));
        UIKit.Box("B", selMarker, gold, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(0, 3));
        UIKit.Box("L", selMarker, gold, Vector2.zero, new Vector2(0, 1), new Vector2(0, .5f), Vector2.zero, new Vector2(3, 0));
        UIKit.Box("R", selMarker, gold, new Vector2(1, 0), Vector2.one, new Vector2(1, .5f), Vector2.zero, new Vector2(3, 0));
        selMarker.gameObject.SetActive(false);

        // lật trang
        int pages = Mathf.Max(1, Mathf.CeilToInt(curList.Count / (float)PerPage));
        if (page > 0)
            UIKit.MakeButton(body, "Prev", "◀", new Vector2(-560, -248), new Vector2(80, 52), () => { page--; ShowDetail(); }, ButtonStyle.Stone, 28, 0f);
        UIKit.Label("Page", body, "Trang " + (page + 1) + " / " + pages, 24, GameTheme.Fog, TextAnchor.MiddleCenter,
            new Vector2(-340, -248), new Vector2(200, 36), false, false, FontStyle.Italic);
        if ((page + 1) * PerPage < curList.Count)
            UIKit.MakeButton(body, "Next", "▶", new Vector2(-120, -248), new Vector2(80, 52), () => { page++; ShowDetail(); }, ButtonStyle.Stone, 28, 0f);

        // trang giấy da bên phải: nơi đọc chi tiết bài
        RectTransform paper;
        UIKit.Panel(body, "InfoCard", new Vector2(360, 5), new Vector2(560, 410), PanelStyle.Parchment, out paper);
        infoBody = UIKit.CBox("InfoBody", paper, Color.clear, Vector2.zero, Vector2.zero);
        UIKit.Stretch(infoBody, 0, 0, 0, 0);
        ShowInfoPlaceholder();

        UIKit.MakeButton(body, "Back", "TỔNG HỢP", new Vector2(360, -248), new Vector2(300, 52), ShowSummary, ButtonStyle.Stone, 28, 0f);
    }

    void ClearInfo()
    {
        if (infoBody == null) return;
        for (int i = infoBody.childCount - 1; i >= 0; i--) Destroy(infoBody.GetChild(i).gameObject);
    }

    void ShowInfoPlaceholder()
    {
        ClearInfo();
        if (infoBody == null) return;
        Img(infoBody, "Seal", RuneArt.Ring, new Vector2(0, 50), new Vector2(120, 120), GameTheme.EmberDeep.WithAlpha(.55f));
        Img(infoBody, "SealIcon", RuneArt.Orbits, new Vector2(0, 50), new Vector2(66, 66), GameTheme.EmberDeep.WithAlpha(.55f));
        UIKit.Label("Hint", infoBody, "Chọn một bài ở bên trái\nđể lật trang ký ức.", 30, GameTheme.PaperMuted,
            TextAnchor.MiddleCenter, new Vector2(0, -60), new Vector2(460, 100), false, false, FontStyle.Italic);
    }

    void ShowInfo(LessonStats l, int slot)
    {
        if (infoBody == null) return;
        if (selMarker != null)
        {
            selMarker.gameObject.SetActive(true);
            selMarker.anchoredPosition = new Vector2(-340, 130 - slot * 62);
        }
        ClearInfo();
        UIKit.Label("Chap", infoBody, l.chapter, 24, GameTheme.EmberDeep, TextAnchor.MiddleCenter,
            new Vector2(0, 140), new Vector2(480, 48), false, false, FontStyle.Italic);
        UIKit.Label("Lesson", infoBody, l.lesson, 28, GameTheme.PaperInk, TextAnchor.MiddleCenter,
            new Vector2(0, 84), new Vector2(480, 64), true, false, FontStyle.Bold);
        UIKit.Divider(infoBody, new Vector2(0, 40), 360, GameTheme.Bronze.WithAlpha(.8f));

        string[] k = { "Số lần làm", "Điểm cao nhất", "Điểm lần cuối", "Lần cuối" };
        string[] v = { l.attempts.ToString(), l.bestScore.ToString(), l.lastScore.ToString(), l.lastDate };
        for (int i = 0; i < k.Length; i++)
        {
            float y = -2f - i * 42f;
            UIKit.Label("K" + i, infoBody, k[i], 26, GameTheme.PaperMuted, TextAnchor.MiddleLeft, new Vector2(0, y), new Vector2(440, 38));
            UIKit.Label("V" + i, infoBody, v[i], 26, GameTheme.PaperInk, TextAnchor.MiddleRight, new Vector2(0, y), new Vector2(440, 38), false, false, FontStyle.Bold);
        }
        UIKit.Divider(infoBody, new Vector2(0, -160), 260, GameTheme.Bronze.WithAlpha(.5f));
    }

    static string Short(string s, int max)
    {
        s = s ?? "";
        return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }

    // ------------------------------------------------------------ hoạt ảnh + đóng
    void Update()
    {
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        if (fade != null) fade.alpha = Mathf.Clamp01((t - t0) / .25f);

        if (dashRing != null) dashRing.localRotation = Quaternion.Euler(0, 0, -t * 4f);

        for (int i = 0; i < embers.Count; i++)
        {
            if (embers[i] == null) continue;
            float u = Mathf.Repeat(t * (.04f + .015f * (i % 4)) + emberPhase[i], 1f);
            embers[i].anchoredPosition = new Vector2(emberX[i] + Mathf.Sin(u * 9f + i) * 18f, -300f + u * 600f);
            var im = embers[i].GetComponent<Image>(); var c = im.color; c.a = Mathf.Sin(u * Mathf.PI) * .55f; im.color = c;
        }

        for (int i = 0; i < torchGlow.Count; i++)
        {
            var c = torchGlow[i].color; c.a = .28f + .22f * Mathf.PerlinNoise(t * 3f, i * 5.3f); torchGlow[i].color = c;
        }

        for (int i = 0; i < bars.Count; i++)
            if (bars[i] != null) bars[i].fillAmount = Mathf.MoveTowards(bars[i].fillAmount, barTargets[i], dt * 1.1f);

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    void Close()
    {
        var cb = onClose; onClose = null;
        Destroy(gameObject);
        if (cb != null) cb();
    }
}
