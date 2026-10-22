using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Bảng xếp hạng + lịch sử điểm của TẤT CẢ người chơi (dữ liệu từ Supabase qua CloudSync).
/// Hai chế độ xem: XẾP HẠNG (điểm cao nhất mỗi người) và LỊCH SỬ (các lượt chơi mới nhất).
/// Lọc theo TỔNG / LÝ / HÓA / SINH / TỰ LUẬN. Chia trang (không cuộn). Tự xử lý Esc và đóng.
/// </summary>
public class LeaderboardWindow : MonoBehaviour
{
    const int PerPage = 8, FetchLimit = 100;
    static readonly string[] Tabs = { "Tổng", ProgressSaveSystem.SubjectLy, ProgressSaveSystem.SubjectHoa, ProgressSaveSystem.SubjectSinh, ProgressSaveSystem.SubjectTuLuan };
    static readonly string[] TabNames = { "TỔNG", "LÝ", "HÓA", "SINH", "TỰ LUẬN" };

    Action onClose;
    RectTransform content, controls, body;
    CanvasGroup fade; float t0; RectTransform ring;
    int tab, page, reqId;
    bool historyView;
    List<LeaderRow> curBoard; List<RecentRow> curHist;
    readonly Dictionary<string, List<LeaderRow>> boardCache = new Dictionary<string, List<LeaderRow>>();
    readonly Dictionary<string, List<RecentRow>> histCache = new Dictionary<string, List<RecentRow>>();

    public static LeaderboardWindow Open(Transform parent, Action onClose)
    {
        var dim = UIKit.Box("BoardDim", parent, new Color(0, 0, 0, .7f), Vector2.zero, Vector2.one, UIKit.C, Vector2.zero, Vector2.zero, true);
        UIKit.Stretch(dim, 0, 0, 0, 0);
        var w = dim.gameObject.AddComponent<LeaderboardWindow>();
        w.onClose = onClose;
        w.Build(dim);
        return w;
    }

    // ------------------------------------------------------------ khung
    void Build(Transform root)
    {
        t0 = Time.unscaledTime;
        fade = gameObject.AddComponent<CanvasGroup>(); fade.alpha = 0f;
        UIKit.Panel(root, "BoardPanel", Vector2.zero, new Vector2(1400, 820), PanelStyle.Dark, out content);

        var rc = UIKit.CBox("RuneWatermark", content, GameTheme.Gold.WithAlpha(.06f), Vector2.zero, new Vector2(800, 800));
        var im = rc.GetComponent<Image>(); im.sprite = RuneArt.Ring; im.raycastTarget = false; ring = rc;

        UIKit.Label("T", content, "BẢNG XẾP HẠNG", 52, GameTheme.GoldHi, TextAnchor.MiddleCenter,
            new Vector2(0, 330), new Vector2(760, 70), true, true, FontStyle.Bold);
        UIKit.Label("Sub", content, "Điểm của tất cả Người Giữ Ký Ức", 24, GameTheme.Fog, TextAnchor.MiddleCenter,
            new Vector2(0, 288), new Vector2(900, 34), false, true, FontStyle.Italic);
        UIKit.Divider(content, new Vector2(0, 262), 640);

        UIKit.MakeButton(content, "Btn_Close", "ĐÓNG", new Vector2(520, -335), new Vector2(260, 72), Close, ButtonStyle.Bronze, 34, 0f);
        BuildControls();
        Reload(false);
    }

    /// <summary>Hàng tab môn + nút đổi chế độ xem + nút tải lại (dựng lại để tô sáng mục đang chọn).</summary>
    void BuildControls()
    {
        if (controls != null) Destroy(controls.gameObject);
        controls = UIKit.CBox("Controls", content, Color.clear, Vector2.zero, Vector2.zero);
        UIKit.Stretch(controls, 0, 0, 0, 0);

        for (int i = 0; i < Tabs.Length; i++)
        {
            int idx = i;
            UIKit.MakeButton(controls, "Tab" + i, TabNames[i], new Vector2((i - 2) * 215f, 205f), new Vector2(200, 56),
                () => { if (tab == idx) return; tab = idx; BuildControls(); Reload(false); },
                tab == i ? ButtonStyle.Bronze : ButtonStyle.Stone, 28, 0f);
        }
        UIKit.MakeButton(controls, "ViewBoard", "XẾP HẠNG", new Vector2(-150, 140), new Vector2(260, 56),
            () => { if (!historyView) return; historyView = false; BuildControls(); Reload(false); },
            !historyView ? ButtonStyle.Bronze : ButtonStyle.Stone, 28, 0f);
        UIKit.MakeButton(controls, "ViewHistory", "LỊCH SỬ", new Vector2(150, 140), new Vector2(260, 56),
            () => { if (historyView) return; historyView = true; BuildControls(); Reload(false); },
            historyView ? ButtonStyle.Bronze : ButtonStyle.Stone, 28, 0f);
        UIKit.MakeButton(controls, "Refresh", "TẢI LẠI", new Vector2(540, 140), new Vector2(200, 56),
            () => Reload(true), ButtonStyle.Stone, 26, 0f);
    }

    // ------------------------------------------------------------ nạp dữ liệu
    void Reload(bool force)
    {
        string key = Tabs[tab];
        if (force) { boardCache.Remove(key); histCache.Remove(key); }
        page = 0;

        List<LeaderRow> lb; List<RecentRow> hs;
        if (!historyView && boardCache.TryGetValue(key, out lb)) { curBoard = lb; Render(); return; }
        if (historyView && histCache.TryGetValue(key, out hs)) { curHist = hs; Render(); return; }

        ShowMessage("Đang tải dữ liệu...");
        int id = ++reqId;
        if (!historyView)
            CloudSync.FetchLeaderboard(key, FetchLimit, (rows, err) =>
            {
                if (this == null || id != reqId) return;
                if (err != null) { ShowMessage("Không tải được bảng xếp hạng.\n" + err); return; }
                boardCache[key] = rows; curBoard = rows; Render();
            });
        else
            CloudSync.FetchRecent(key == "Tổng" ? null : key, FetchLimit, (rows, err) =>
            {
                if (this == null || id != reqId) return;
                if (err != null) { ShowMessage("Không tải được lịch sử.\n" + err); return; }
                histCache[key] = rows; curHist = rows; Render();
            });
    }

    // ------------------------------------------------------------ dựng nội dung
    void ClearBody()
    {
        if (body != null) Destroy(body.gameObject);
        body = UIKit.CBox("Body", content, Color.clear, Vector2.zero, Vector2.zero);
        UIKit.Stretch(body, 0, 0, 0, 0);
    }

    void ShowMessage(string msg)
    {
        ClearBody();
        UIKit.Label("Msg", body, msg, 30, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, -60), new Vector2(1100, 200));
    }

    void Render()
    {
        ClearBody();
        int total = historyView ? (curHist != null ? curHist.Count : 0) : (curBoard != null ? curBoard.Count : 0);
        if (total == 0)
        {
            UIKit.Label("Msg", body, historyView ? "Chưa có lượt chơi nào được ghi nhận." : "Chưa có ai trên bảng xếp hạng này. Hãy là người đầu tiên!",
                30, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, -60), new Vector2(1100, 120));
            return;
        }

        int pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)PerPage));
        page = Mathf.Clamp(page, 0, pages - 1);
        if (historyView) RenderHistory(); else RenderBoard();

        // chuyển trang
        var prev = UIKit.MakeButton(body, "Btn_Prev", "<", new Vector2(-150, -335), new Vector2(100, 72), () => Turn(-1), ButtonStyle.Stone, 40, 0f);
        var next = UIKit.MakeButton(body, "Btn_Next", ">", new Vector2(150, -335), new Vector2(100, 72), () => Turn(1), ButtonStyle.Stone, 40, 0f);
        prev.interactable = page > 0; next.interactable = page < pages - 1;
        UIKit.Label("PageLbl", body, "TRANG " + (page + 1) + " / " + pages, 28, GameTheme.GoldHi, TextAnchor.MiddleCenter,
            new Vector2(0, -335), new Vector2(200, 50), true, true, FontStyle.Bold);
    }

    void Turn(int d)
    {
        int total = historyView ? (curHist != null ? curHist.Count : 0) : (curBoard != null ? curBoard.Count : 0);
        int pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)PerPage));
        int np = page + d;
        if (np < 0 || np >= pages) return;
        page = np; Render();
    }

    static string MyName()
    {
        string n = GameSettings.Nickname;
        return string.IsNullOrWhiteSpace(n) ? StudentProfileData.studentName : n;
    }

    void RenderBoard()
    {
        // cột: hạng · người chơi · lớp · điểm cao nhất · số lần · lần cuối
        Header("HẠNG", -620, 100, TextAnchor.MiddleCenter); Header("NGƯỜI CHƠI", -505, 330, TextAnchor.MiddleLeft);
        Header("LỚP", -170, 130, TextAnchor.MiddleCenter); Header("ĐIỂM CAO NHẤT", -35, 200, TextAnchor.MiddleCenter);
        Header("SỐ LẦN", 170, 150, TextAnchor.MiddleCenter); Header("LẦN CUỐI", 325, 295, TextAnchor.MiddleCenter);

        string me = MyName(), myClass = StudentProfileData.studentClass ?? "";
        for (int k = 0; k < PerPage; k++)
        {
            int i = page * PerPage + k; if (i >= curBoard.Count) break;
            var r = curBoard[i]; float y = 38f - k * 42f;
            bool mine = r.display_name == me && (r.student_class ?? "") == myClass;
            Stripe(y, k, mine);
            Color rc = i == 0 ? GameTheme.GoldHi : i == 1 ? new Color(.82f, .86f, .92f) : i == 2 ? new Color(.88f, .58f, .32f) : GameTheme.Bone;
            Cell((i + 1).ToString(), -620, 100, y, TextAnchor.MiddleCenter, rc, 30, true);
            Cell(r.display_name, -505, 330, y, TextAnchor.MiddleLeft, mine ? GameTheme.GoldHi : GameTheme.Bone, 28, mine);
            Cell(string.IsNullOrEmpty(r.student_class) ? "—" : r.student_class, -170, 130, y, TextAnchor.MiddleCenter, GameTheme.Fog, 26, false);
            Cell(r.best_score.ToString(), -35, 200, y, TextAnchor.MiddleCenter, rc, 30, true);
            Cell(r.attempts.ToString(), 170, 150, y, TextAnchor.MiddleCenter, GameTheme.Fog, 26, false);
            Cell(When(r.last_played, "dd/MM/yyyy"), 325, 295, y, TextAnchor.MiddleCenter, GameTheme.Fog, 24, false);
        }
    }

    void RenderHistory()
    {
        // cột: thời gian · người chơi · lớp · môn · nội dung · điểm · kết quả · thời lượng
        Header("THỜI GIAN", -620, 150, TextAnchor.MiddleCenter); Header("NGƯỜI CHƠI", -465, 210, TextAnchor.MiddleLeft);
        Header("LỚP", -250, 90, TextAnchor.MiddleCenter); Header("MÔN", -155, 100, TextAnchor.MiddleCenter);
        Header("NỘI DUNG", -50, 330, TextAnchor.MiddleLeft); Header("ĐIỂM", 290, 110, TextAnchor.MiddleCenter);
        Header("KẾT QUẢ", 405, 110, TextAnchor.MiddleCenter); Header("THỜI GIAN CHƠI", 520, 100, TextAnchor.MiddleCenter);

        string me = MyName(), myClass = StudentProfileData.studentClass ?? "";
        for (int k = 0; k < PerPage; k++)
        {
            int i = page * PerPage + k; if (i >= curHist.Count) break;
            var r = curHist[i]; float y = 38f - k * 42f;
            bool mine = r.display_name == me && (r.student_class ?? "") == myClass;
            Stripe(y, k, mine);
            string detail = r.chapter ?? "";
            if (!string.IsNullOrEmpty(r.lesson)) detail += (detail.Length > 0 ? " • " : "") + r.lesson;
            Cell(When(r.created_at, "dd/MM HH:mm"), -620, 150, y, TextAnchor.MiddleCenter, GameTheme.Fog, 24, false);
            Cell(r.display_name, -465, 210, y, TextAnchor.MiddleLeft, mine ? GameTheme.GoldHi : GameTheme.Bone, 26, mine);
            Cell(string.IsNullOrEmpty(r.student_class) ? "—" : r.student_class, -250, 90, y, TextAnchor.MiddleCenter, GameTheme.Fog, 24, false);
            Cell(r.subject, -155, 100, y, TextAnchor.MiddleCenter, SubjectColor(r.subject), 26, true);
            Cell(detail.Length == 0 ? "—" : detail, -50, 330, y, TextAnchor.MiddleLeft, GameTheme.Fog, 24, false);
            Cell(r.score.ToString(), 290, 110, y, TextAnchor.MiddleCenter, GameTheme.GoldHi, 30, true);
            Cell(r.won ? "Thắng" : "Thua", 405, 110, y, TextAnchor.MiddleCenter, r.won ? new Color(.55f, .9f, .55f) : new Color(.95f, .5f, .5f), 26, true);
            Cell(r.seconds > 0 ? (r.seconds / 60) + ":" + (r.seconds % 60).ToString("00") : "—", 520, 100, y, TextAnchor.MiddleCenter, GameTheme.Fog, 24, false);
        }
    }

    // ------------------------------------------------------------ tiện ích dựng ô
    void Header(string text, float left, float w, TextAnchor al)
    {
        Cell(text, left, w, 82f, al, GameTheme.Gold, 22, true);
        if (left == -620) UIKit.Divider(body, new Vector2(0, 62), 1240);
    }

    void Stripe(float y, int k, bool mine)
    {
        Color c = mine ? GameTheme.Gold.WithAlpha(.20f) : (k % 2 == 0 ? new Color(1, 1, 1, .05f) : new Color(0, 0, 0, .12f));
        UIKit.CBox("Row", body, c, new Vector2(0, y), new Vector2(1260, 38));
    }

    void Cell(string text, float left, float w, float y, TextAnchor al, Color col, int size, bool bold)
    {
        var t = UIKit.Label("C", body, text ?? "", size, col, al, new Vector2(left + w / 2f, y), new Vector2(w, 38f), false, false,
            bold ? FontStyle.Bold : FontStyle.Normal);
        t.resizeTextForBestFit = true; t.resizeTextMinSize = 14; t.resizeTextMaxSize = size;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
    }

    static Color SubjectColor(string s)
    {
        if (s == ProgressSaveSystem.SubjectLy) return GameTheme.Ly;
        if (s == ProgressSaveSystem.SubjectHoa) return GameTheme.Hoa;
        if (s == ProgressSaveSystem.SubjectSinh) return GameTheme.Sinh;
        return GameTheme.EmberHi;
    }

    /// <summary>Đổi chuỗi thời gian UTC của Supabase sang giờ máy.</summary>
    static string When(string iso, string fmt)
    {
        DateTime dt;
        if (string.IsNullOrEmpty(iso) || !DateTime.TryParse(iso, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out dt)) return "—";
        return dt.ToLocalTime().ToString(fmt, CultureInfo.InvariantCulture);
    }

    // ------------------------------------------------------------ hoạt ảnh + đóng
    void Update()
    {
        if (fade != null) fade.alpha = Mathf.Clamp01((Time.unscaledTime - t0) / .25f);
        if (ring != null) ring.localRotation = Quaternion.Euler(0, 0, -Time.unscaledTime * 4f);
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    void Close()
    {
        var cb = onClose; onClose = null;
        Destroy(gameObject);
        if (cb != null) cb();
    }
}
