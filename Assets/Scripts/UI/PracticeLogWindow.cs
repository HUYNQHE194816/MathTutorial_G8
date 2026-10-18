using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Nhật ký luyện tập. Mở ra chỉ hiện TỔNG HỢP; nút CHI TIẾT mới cho chọn môn, rồi chọn bài
/// để xem: số lần làm, điểm cao nhất, điểm lần cuối.
/// </summary>
public class PracticeLogWindow : MonoBehaviour
{
    const int PerPage = 6;

    Action onClose;
    RectTransform content, body;
    Text infoLabel;
    string subject = ProgressSaveSystem.SubjectLy;
    int page;

    public static PracticeLogWindow Open(Transform parent, Action onClose)
    {
        var dim = UIKit.Box("LogDim", parent, new Color(0, 0, 0, .7f), Vector2.zero, Vector2.one, UIKit.C, Vector2.zero, Vector2.zero, true);
        UIKit.Stretch(dim, 0, 0, 0, 0);
        var w = dim.gameObject.AddComponent<PracticeLogWindow>();
        w.onClose = onClose;
        w.Build(dim);
        return w;
    }

    void Build(Transform root)
    {
        UIKit.Panel(root, "LogPanel", Vector2.zero, new Vector2(1400, 820), PanelStyle.Dark, out content);
        UIKit.Label("T", content, "NHẬT KÝ LUYỆN TẬP", 52, GameTheme.GoldHi, TextAnchor.MiddleCenter,
            new Vector2(0, 330), new Vector2(900, 70), true, true, FontStyle.Bold);
        UIKit.Divider(content, new Vector2(0, 288), 560);
        UIKit.MakeButton(content, "Btn_Close", "ĐÓNG", new Vector2(0, -335), new Vector2(320, 72), Close, ButtonStyle.Bronze, 34, .1f);
        ShowSummary();
    }

    // Vùng nội dung ở giữa (body) được dựng lại mỗi khi đổi màn
    void Clear()
    {
        if (body != null) Destroy(body.gameObject);
        body = UIKit.CBox("Body", content, Color.clear, Vector2.zero, Vector2.zero);
        UIKit.Stretch(body, 0, 100, 0, 110);
    }

    // ------------------------------------------------------------ TỔNG HỢP
    void ShowSummary()
    {
        Clear();
        var d = ProgressSaveSystem.Current;

        UIKit.Label("All", body,
            "Tổng lượt chơi: " + ProgressSaveSystem.Attempts(d) + "     •     Thắng: " + ProgressSaveSystem.Wins(d) +
            "\nĐiểm cao nhất: " + ProgressSaveSystem.BestScore(d) + "     •     Tỉ lệ đúng: " + Mathf.RoundToInt(ProgressSaveSystem.Accuracy(d) * 100f) + "%",
            36, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 180), new Vector2(1200, 130));

        float y = 60;
        foreach (string sub in ProgressSaveSystem.Subjects)
        {
            var s = ProgressSaveSystem.FindSubject(d, sub);
            int att = s != null ? s.attempts : 0, best = s != null ? s.bestScore : 0;
            UIKit.Label("Sub_" + sub, body,
                sub + ":   " + att + " lượt   •   cao nhất " + best + "   •   đúng " + Mathf.RoundToInt(ProgressSaveSystem.Accuracy(s) * 100f) + "%",
                32, GameTheme.Rune, TextAnchor.MiddleCenter, new Vector2(0, y), new Vector2(1100, 46));
            y -= 56;
        }

        UIKit.MakeButton(body, "Btn_Detail", "CHI TIẾT", new Vector2(0, -190), new Vector2(420, 80),
            () => { page = 0; ShowDetail(); }, ButtonStyle.Stone, 36, .1f);
    }

    // ------------------------------------------------------------ CHI TIẾT THEO BÀI
    void ShowDetail()
    {
        Clear();
        var d = ProgressSaveSystem.Current;

        // chọn môn
        string[] subs = ProgressSaveSystem.Subjects;
        for (int i = 0; i < subs.Length; i++)
        {
            string s = subs[i];
            UIKit.MakeButton(body, "Sub" + i, s, new Vector2(-560 + i * 230, 220), new Vector2(210, 60),
                () => { subject = s; page = 0; ShowDetail(); },
                s == subject ? ButtonStyle.Bronze : ButtonStyle.Stone, 30, 0f);
        }

        // danh sách bài của môn đang chọn
        var list = d.lessons.FindAll(x => x.subject == subject);
        for (int i = 0; i < PerPage && page * PerPage + i < list.Count; i++)
        {
            var l = list[page * PerPage + i];
            UIKit.MakeButton(body, "L" + i, Short(l.lesson, 30), new Vector2(-340, 130 - i * 62), new Vector2(520, 54),
                () => ShowInfo(l), ButtonStyle.Stone, 26, 0f);
        }
        if (list.Count == 0)
            UIKit.Label("Empty", body, "Chưa có lượt làm bài nào\nở môn này.", 30, GameTheme.Fog, TextAnchor.MiddleCenter,
                new Vector2(-340, 60), new Vector2(520, 100));
        if (page > 0)
            UIKit.MakeButton(body, "Prev", "◀", new Vector2(-560, -245), new Vector2(80, 52), () => { page--; ShowDetail(); }, ButtonStyle.Stone, 28, 0f);
        if ((page + 1) * PerPage < list.Count)
            UIKit.MakeButton(body, "Next", "▶", new Vector2(-120, -245), new Vector2(80, 52), () => { page++; ShowDetail(); }, ButtonStyle.Stone, 28, 0f);

        // bảng thông tin bài đang chọn (cập nhật chữ, không dựng lại nút)
        infoLabel = UIKit.Label("Info", body, "Chọn một bài ở bên trái\nđể xem chi tiết.", 32, GameTheme.Fog,
            TextAnchor.MiddleLeft, new Vector2(360, 60), new Vector2(560, 400));

        UIKit.MakeButton(body, "Back", "TỔNG HỢP", new Vector2(360, -245), new Vector2(300, 52), ShowSummary, ButtonStyle.Stone, 28, 0f);
    }

    void ShowInfo(LessonStats l)
    {
        if (infoLabel == null) return;
        infoLabel.color = GameTheme.Bone;
        infoLabel.text = l.chapter + "\n" + l.lesson +
                         "\n\nSố lần làm: " + l.attempts +
                         "\nĐiểm cao nhất: " + l.bestScore +
                         "\nĐiểm lần cuối: " + l.lastScore +
                         "\nLần cuối: " + l.lastDate;
    }

    static string Short(string s, int max)
    {
        s = s ?? "";
        return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }

    // ------------------------------------------------------------ đóng
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    void Close()
    {
        var cb = onClose; onClose = null;
        Destroy(gameObject);
        if (cb != null) cb();
    }
}