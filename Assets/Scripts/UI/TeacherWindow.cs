using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Màn quản lý bài giảng của giáo viên (dữ liệu trên Supabase qua TeacherApi).
///  - Danh sách bài giảng: tạo / sửa / xóa, nhập CSV (tự tạo bài theo cột Môn, Chương, Bài).
///  - Trong mỗi bài: danh sách câu hỏi, thêm / sửa / xóa, nhập CSV vào bài (thêm hoặc thay thế).
/// Dựng hoàn toàn bằng code, cùng kiểu với LeaderboardWindow. Esc: quay lại / đóng.
/// </summary>
public class TeacherWindow : MonoBehaviour
{
    const int LessonsPerPage = 7, QuestionsPerPage = 6;
    static readonly string[] Subjects = { ProgressSaveSystem.SubjectLy, ProgressSaveSystem.SubjectHoa, ProgressSaveSystem.SubjectSinh };
    static readonly string[] SubjectNames = { "LÝ", "HÓA", "SINH" };
    static readonly string[] Levels = { "Nhận biết", "Thông hiểu", "Vận dụng" };
    static readonly string[] LevelNames = { "NHẬN BIẾT", "THÔNG HIỂU", "VẬN DỤNG" };
    static readonly string[] Letters = { "A", "B", "C", "D" };

    enum View { Lessons, LessonEdit, Questions, QuestionEdit }

    class ImportRow { public string subject, chapter, lesson; public TeacherApi.Question q; }

    Action onClose;
    RectTransform root, content, body, ring, confirmRt;
    Text titleText, subText;
    CanvasGroup fade; float t0;
    View view;
    bool busy;
    string pendingNotice; bool pendingOk;

    List<TeacherApi.Lesson> lessons = new List<TeacherApi.Lesson>();
    List<TeacherApi.Question> questions = new List<TeacherApi.Question>();
    TeacherApi.Lesson curLesson, draftL;
    TeacherApi.Question draftQ;
    bool newL, newQ;
    int lessonPage, questionPage;

    InputField fChapter, fTitle, fContent, fGrade;
    InputField qText, qA, qB, qC, qD, qExp;

    public static TeacherWindow Open(Transform parent, Action onClose)
    {
        var dim = UIKit.Box("TeacherDim", parent, new Color(0, 0, 0, .7f), Vector2.zero, Vector2.one, UIKit.C, Vector2.zero, Vector2.zero, true);
        UIKit.Stretch(dim, 0, 0, 0, 0);
        var w = dim.gameObject.AddComponent<TeacherWindow>();
        w.onClose = onClose;
        w.Build(dim);
        return w;
    }

    // ------------------------------------------------------------ khung
    void Build(RectTransform dim)
    {
        root = dim;
        t0 = Time.unscaledTime;
        fade = gameObject.AddComponent<CanvasGroup>(); fade.alpha = 0f;
        UIKit.Panel(dim, "TeacherPanel", Vector2.zero, new Vector2(1400, 820), PanelStyle.Dark, out content);

        var rc = UIKit.CBox("RuneWatermark", content, GameTheme.Gold.WithAlpha(.06f), Vector2.zero, new Vector2(800, 800));
        var im = rc.GetComponent<Image>(); im.sprite = RuneArt.Ring; im.raycastTarget = false; ring = rc;

        titleText = UIKit.Label("T", content, "", 52, GameTheme.GoldHi, TextAnchor.MiddleCenter,
            new Vector2(0, 330), new Vector2(1200, 70), true, true, FontStyle.Bold);
        subText = UIKit.Label("Sub", content, "", 24, GameTheme.Fog, TextAnchor.MiddleCenter,
            new Vector2(0, 288), new Vector2(1300, 34), false, true, FontStyle.Italic);
        UIKit.Divider(content, new Vector2(0, 262), 640);

        if (!SupabaseAuth.IsTeacher)
        {
            SetTitle("QUẢN LÝ BÀI GIẢNG", "", 52);
            ClearBody();
            L("Chỉ tài khoản giáo viên mới dùng được màn này.\nHãy đăng nhập lại bằng nút TÔI LÀ GIÁO VIÊN.", 30, GameTheme.Fog, TextAnchor.MiddleCenter, 0, 0, 1100, 160);
            Btn("ĐÓNG", 520, -335, 260, 72, Close, false, 34);
            return;
        }
        GoLessons(true);
    }

    void Update()
    {
        if (fade != null) fade.alpha = Mathf.Clamp01((Time.unscaledTime - t0) / .25f);
        if (ring != null) ring.localRotation = Quaternion.Euler(0, 0, -Time.unscaledTime * 4f);
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
    }

    void Back()
    {
        if (confirmRt != null) { CloseConfirm(); return; }
        if (busy) return;
        switch (view)
        {
            case View.Lessons: Close(); break;
            case View.Questions: RenderLessons(); break;
            // màn nhập liệu: không đóng bằng Esc để khỏi mất chữ đang gõ, dùng nút QUAY LẠI
        }
    }

    void Close()
    {
        var cb = onClose; onClose = null;
        Destroy(gameObject);
        if (cb != null) cb();
    }

    // ------------------------------------------------------------ tiện ích dựng giao diện
    void SetTitle(string t, string sub, int size)
    {
        titleText.text = t; titleText.fontSize = size;
        if (pendingNotice != null) { subText.text = pendingNotice; subText.color = pendingOk ? GameTheme.Moss : GameTheme.Blood; pendingNotice = null; }
        else { subText.text = sub; subText.color = GameTheme.Fog; }
    }

    /// <summary>Thông báo ngay (dòng phụ dưới tiêu đề).</summary>
    void Notice(string msg, bool ok)
    {
        subText.text = Trunc(msg, 120); subText.color = ok ? GameTheme.Moss : GameTheme.Blood;
    }

    /// <summary>Thông báo sẽ hiện ở lần dựng màn hình kế tiếp.</summary>
    void Later(string msg, bool ok) { pendingNotice = Trunc(msg, 120); pendingOk = ok; }

    void ClearBody()
    {
        if (body != null) Destroy(body.gameObject);
        body = UIKit.CBox("Body", content, Color.clear, Vector2.zero, Vector2.zero);
        UIKit.Stretch(body, 0, 0, 0, 0);
    }

    Text L(string s, int size, Color c, TextAnchor a, float x, float y, float w, float h, FontStyle st = FontStyle.Normal)
    {
        return UIKit.Label("L", body, s, size, c, a, new Vector2(x, y), new Vector2(w, h), false, false, st);
    }

    Button Btn(string label, float x, float y, float w, float h, Action a, bool on, int fs = 28)
    {
        return UIKit.MakeButton(body, "B_" + label, label, new Vector2(x, y), new Vector2(w, h), a, on ? ButtonStyle.Bronze : ButtonStyle.Stone, fs, 0f);
    }

    Button BtnDanger(string label, float x, float y, float w, float h, Action a, int fs = 24)
    {
        return UIKit.MakeButton(body, "B_" + label, label, new Vector2(x, y), new Vector2(w, h), a, ButtonStyle.Danger, fs, 0f);
    }

    void ShowMessage(string msg)
    {
        ClearBody();
        L(msg, 30, GameTheme.Fog, TextAnchor.MiddleCenter, 0, -20, 1100, 200);
    }

    void ShowError(string err, Action retry, Action back)
    {
        ClearBody();
        L("Có lỗi:\n" + Trunc(err, 260), 28, GameTheme.Blood, TextAnchor.MiddleCenter, 0, 30, 1100, 220);
        if (retry != null) Btn("THỬ LẠI", -200, -150, 300, 72, retry, true, 32);
        Btn("QUAY LẠI", 200, -150, 300, 72, back ?? (Action)Close, false, 32);
    }

    InputField MakeMulti(string n, string hint, Vector2 pos, Vector2 size, int limit)
    {
        var fr = UIKit.CBox(n, body, GameTheme.Iron, pos, size, true);
        var frame = fr.GetComponent<Image>();
        var fill = UIKit.CBox("Fill", fr, GameTheme.Ink, Vector2.zero, Vector2.zero); UIKit.Stretch(fill, 4, 4, 4, 4);
        var ph = UIKit.Label("Placeholder", fill, hint, 26, GameTheme.Fog.WithAlpha(.6f), TextAnchor.UpperLeft, Vector2.zero, Vector2.zero, false, false, FontStyle.Italic);
        var tx = UIKit.Label("Text", fill, "", 28, GameTheme.Bone, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero);
        UIKit.Stretch(ph.rectTransform, 16, 10, 16, 10); UIKit.Stretch(tx.rectTransform, 16, 10, 16, 10);
        ph.horizontalOverflow = tx.horizontalOverflow = HorizontalWrapMode.Wrap;
        ph.verticalOverflow = tx.verticalOverflow = VerticalWrapMode.Truncate;
        tx.supportRichText = false;
        var input = fr.gameObject.AddComponent<InputField>();
        input.targetGraphic = frame; input.transition = Selectable.Transition.None;
        input.textComponent = tx; input.placeholder = ph; input.characterLimit = limit;
        input.lineType = InputField.LineType.MultiLineNewline;
        input.customCaretColor = true; input.caretColor = GameTheme.EmberHi;
        input.selectionColor = GameTheme.Ember.WithAlpha(.35f);
        return input;
    }

    void Pager(int page, int pages, Action<int> go, float y)
    {
        if (pages <= 1) return;
        Btn("< TRƯỚC", -190, y, 180, 50, () => { if (page > 0) go(page - 1); }, false, 24);
        L("Trang " + (page + 1) + "/" + pages, 26, GameTheme.Bone, TextAnchor.MiddleCenter, 0, y, 200, 40);
        Btn("SAU >", 190, y, 180, 50, () => { if (page < pages - 1) go(page + 1); }, false, 24);
    }

    void Confirm(string msg, string yesLabel, Action yes)
    {
        if (confirmRt != null) Destroy(confirmRt.gameObject);
        confirmRt = UIKit.CBox("Confirm", content, new Color(0, 0, 0, .78f), Vector2.zero, Vector2.zero, true);
        UIKit.Stretch(confirmRt, 0, 0, 0, 0);
        RectTransform cc;
        UIKit.Panel(confirmRt, "ConfirmPanel", Vector2.zero, new Vector2(820, 340), PanelStyle.Dark, out cc);
        UIKit.Label("Msg", cc, msg, 30, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 50), new Vector2(700, 160));
        UIKit.MakeButton(cc, "Yes", yesLabel, new Vector2(-170, -100), new Vector2(280, 72), () => { CloseConfirm(); yes(); }, ButtonStyle.Danger, 32, 0f);
        UIKit.MakeButton(cc, "No", "HỦY", new Vector2(170, -100), new Vector2(280, 72), CloseConfirm, ButtonStyle.Stone, 32, 0f);
    }

    void CloseConfirm()
    {
        if (confirmRt != null) Destroy(confirmRt.gameObject);
        confirmRt = null;
    }

    static string Trunc(string s, int n)
    {
        s = (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length <= n ? s : s.Substring(0, Mathf.Max(1, n - 3)) + "...";
    }

    static Color SubjectColor(string s)
    {
        if (s == ProgressSaveSystem.SubjectLy) return GameTheme.Ly;
        if (s == ProgressSaveSystem.SubjectHoa) return GameTheme.Hoa;
        if (s == ProgressSaveSystem.SubjectSinh) return GameTheme.Sinh;
        return GameTheme.EmberHi;
    }

    static string SubjectShort(string s)
    {
        for (int i = 0; i < Subjects.Length; i++) if (Subjects[i] == s) return SubjectNames[i];
        return (s ?? "?").ToUpperInvariant();
    }

    static string TeacherName() { return string.IsNullOrEmpty(SupabaseAuth.DisplayName) ? "Giáo viên" : SupabaseAuth.DisplayName; }

    static TeacherApi.Lesson Clone(TeacherApi.Lesson l) { return JsonUtility.FromJson<TeacherApi.Lesson>(JsonUtility.ToJson(l)); }
    static TeacherApi.Question Clone(TeacherApi.Question q) { return JsonUtility.FromJson<TeacherApi.Question>(JsonUtility.ToJson(q)); }

    // ============================================================ DANH SÁCH BÀI GIẢNG
    void GoLessons(bool reload)
    {
        if (!reload) { RenderLessons(); return; }
        busy = true;
        view = View.Lessons;
        ShowMessage("Đang tải danh sách bài giảng...");
        TeacherApi.Run(LoadLessonsRoutine());
    }

    IEnumerator LoadLessonsRoutine()
    {
        var r = new TeacherApi.Res();
        var list = new List<TeacherApi.Lesson>();
        yield return TeacherApi.LoadLessons(list, r);
        if (this == null) yield break;
        busy = false;
        if (!r.ok) { ShowError(r.error, () => GoLessons(true), Close); yield break; }
        lessons = list;
        RenderLessons();
    }

    void RenderLessons()
    {
        view = View.Lessons;
        fChapter = fTitle = fContent = fGrade = null;
        SetTitle("QUẢN LÝ BÀI GIẢNG", TeacherName() + " · " + lessons.Count + " bài giảng", 52);
        ClearBody();

        int pages = Mathf.Max(1, Mathf.CeilToInt(lessons.Count / (float)LessonsPerPage));
        lessonPage = Mathf.Clamp(lessonPage, 0, pages - 1);
        if (lessons.Count == 0)
            L("Chưa có bài giảng nào.\nBấm TẠO BÀI MỚI hoặc NHẬP CSV để bắt đầu.", 30, GameTheme.Fog, TextAnchor.MiddleCenter, 0, 60, 1100, 160);

        for (int i = 0; i < LessonsPerPage; i++)
        {
            int idx = lessonPage * LessonsPerPage + i;
            if (idx >= lessons.Count) break;
            var l = lessons[idx];
            float y = 200 - i * 62f;
            UIKit.CBox("RowBg", body, new Color(1, 1, 1, .05f), new Vector2(0, y), new Vector2(1260, 56));
            L(SubjectShort(l.subject), 26, SubjectColor(l.subject), TextAnchor.MiddleCenter, -585, y, 90, 50, FontStyle.Bold);
            L(Trunc(l.title, 48), 26, GameTheme.Bone, TextAnchor.MiddleLeft, -230, y + 11, 620, 30);
            L(Trunc(l.chapter, 70), 20, GameTheme.Fog, TextAnchor.MiddleLeft, -230, y - 14, 620, 26);
            bool pub = l.status == TeacherApi.Published;
            L(pub ? "XUẤT BẢN" : "NHÁP", 22, pub ? GameTheme.Moss : GameTheme.Fog, TextAnchor.MiddleCenter, 150, y, 140, 40);
            Btn("CÂU HỎI", 315, y, 160, 50, () => OpenQuestions(l), false, 24);
            Btn("SỬA", 455, y, 100, 50, () => EditLesson(l), false, 24);
            BtnDanger("XÓA", 565, y, 100, 50, () => AskDeleteLesson(l), 24);
        }

        Pager(lessonPage, pages, p => { lessonPage = p; RenderLessons(); }, -240);
        Btn("TẠO BÀI MỚI", -500, -335, 300, 72, NewLesson, true, 30);
        Btn("NHẬP CSV", -170, -335, 300, 72, ImportGrouped, false, 30);
        Btn("ĐÓNG", 520, -335, 260, 72, Close, false, 34);
    }

    void AskDeleteLesson(TeacherApi.Lesson l)
    {
        if (busy) return;
        Confirm("Xóa bài \"" + Trunc(l.title, 40) + "\"\nvà toàn bộ câu hỏi của bài?", "XÓA", () =>
        {
            busy = true;
            ShowMessage("Đang xóa...");
            TeacherApi.Run(DeleteLessonRoutine(l));
        });
    }

    IEnumerator DeleteLessonRoutine(TeacherApi.Lesson l)
    {
        var r = new TeacherApi.Res();
        yield return TeacherApi.DeleteLesson(l.id, r);
        if (this == null) yield break;
        busy = false;
        Later(r.ok ? "Đã xóa bài giảng." : "Không xóa được: " + r.error, r.ok);
        GoLessons(true);
    }

    // ============================================================ SỬA / TẠO BÀI GIẢNG
    void NewLesson()
    {
        if (busy) return;
        draftL = new TeacherApi.Lesson { subject = Subjects[0], chapter = "", title = "", content = "", status = TeacherApi.Draft, grade = 8 };
        newL = true;
        ShowLessonEditor();
    }

    void EditLesson(TeacherApi.Lesson l)
    {
        if (busy) return;
        draftL = Clone(l);
        if (draftL.content == null) draftL.content = "";
        newL = false;
        ShowLessonEditor();
    }

    void SyncLesson()
    {
        if (fTitle == null) return;
        draftL.chapter = fChapter.text.Trim();
        draftL.title = fTitle.text.Trim();
        draftL.content = fContent.text;
        int g;
        draftL.grade = int.TryParse(fGrade.text, out g) && g >= 1 && g <= 12 ? g : 8;
    }

    void ShowLessonEditor()
    {
        view = View.LessonEdit;
        SetTitle(newL ? "TẠO BÀI GIẢNG" : "SỬA BÀI GIẢNG", "Học sinh chỉ thấy bài ở trạng thái XUẤT BẢN", 52);
        ClearBody();

        L("Môn", 28, GameTheme.Fog, TextAnchor.MiddleRight, -560, 215, 200, 50);
        for (int i = 0; i < Subjects.Length; i++)
        {
            string s = Subjects[i];
            Btn(SubjectNames[i], -300 + i * 200, 215, 180, 56, () => { SyncLesson(); draftL.subject = s; ShowLessonEditor(); }, draftL.subject == s, 28);
        }
        L("Lớp", 28, GameTheme.Fog, TextAnchor.MiddleRight, 330, 215, 100, 50);
        fGrade = UIKit.MakeInput(body, "In_Grade", "8", new Vector2(470, 215), 2, 130);
        fGrade.contentType = InputField.ContentType.IntegerNumber;
        fGrade.text = draftL.grade.ToString();

        L("Trạng thái", 28, GameTheme.Fog, TextAnchor.MiddleRight, -560, 145, 200, 50);
        Btn("NHÁP", -300, 145, 200, 56, () => { SyncLesson(); draftL.status = TeacherApi.Draft; ShowLessonEditor(); }, draftL.status != TeacherApi.Published, 28);
        Btn("XUẤT BẢN", -70, 145, 240, 56, () => { SyncLesson(); draftL.status = TeacherApi.Published; ShowLessonEditor(); }, draftL.status == TeacherApi.Published, 28);

        L("Chương", 28, GameTheme.Fog, TextAnchor.MiddleRight, -560, 75, 200, 50);
        fChapter = UIKit.MakeInput(body, "In_Chapter", "Ví dụ: Chương III. Khối lượng riêng và áp suất", new Vector2(90, 75), 200, 1000);
        fChapter.text = draftL.chapter ?? "";

        L("Tên bài", 28, GameTheme.Fog, TextAnchor.MiddleRight, -560, 5, 200, 50);
        fTitle = UIKit.MakeInput(body, "In_Title", "Ví dụ: Bài 13. Khối lượng riêng", new Vector2(90, 5), 200, 1000);
        fTitle.text = draftL.title ?? "";

        L("Nội dung\nbài giảng", 28, GameTheme.Fog, TextAnchor.MiddleRight, -560, -70, 200, 80);
        fContent = MakeMulti("In_Content", "Gõ hoặc dán nội dung bài giảng (không bắt buộc)", new Vector2(90, -120), new Vector2(1000, 200), 20000);
        fContent.text = draftL.content ?? "";

        Btn("LƯU", -300, -335, 300, 72, SaveLesson, true, 34);
        Btn("QUAY LẠI", 520, -335, 260, 72, () => { if (!busy) RenderLessons(); }, false, 34);
    }

    void SaveLesson()
    {
        if (busy) return;
        SyncLesson();
        if (string.IsNullOrWhiteSpace(draftL.title)) { Notice("Hãy nhập tên bài.", false); return; }
        busy = true;
        ShowMessage("Đang lưu...");
        TeacherApi.Run(SaveLessonRoutine());
    }

    IEnumerator SaveLessonRoutine()
    {
        var r = new TeacherApi.Res();
        if (newL) draftL.sort_order = lessons.Count + 1;
        yield return TeacherApi.SaveLesson(draftL, r);
        if (this == null) yield break;
        busy = false;
        if (!r.ok) { Later("Không lưu được: " + r.error, false); ShowLessonEditor(); yield break; }
        Later("Đã lưu bài giảng.", true);
        GoLessons(true);
    }

    // ============================================================ DANH SÁCH CÂU HỎI
    void OpenQuestions(TeacherApi.Lesson l)
    {
        if (busy) return;
        curLesson = l; questionPage = 0;
        GoQuestions(true);
    }

    void GoQuestions(bool reload)
    {
        if (!reload) { RenderQuestions(); return; }
        busy = true;
        view = View.Questions;
        ShowMessage("Đang tải câu hỏi...");
        TeacherApi.Run(LoadQuestionsRoutine());
    }

    IEnumerator LoadQuestionsRoutine()
    {
        var r = new TeacherApi.Res();
        var list = new List<TeacherApi.Question>();
        yield return TeacherApi.LoadQuestions(curLesson.id, list, r);
        if (this == null) yield break;
        busy = false;
        if (!r.ok) { ShowError(r.error, () => GoQuestions(true), () => RenderLessons()); yield break; }
        questions = list;
        RenderQuestions();
    }

    void RenderQuestions()
    {
        view = View.Questions;
        qText = qA = qB = qC = qD = qExp = null;
        SetTitle("CÂU HỎI", "[" + SubjectShort(curLesson.subject) + "] " + Trunc(curLesson.title, 60) + " · " + questions.Count + " câu", 52);
        ClearBody();

        int pages = Mathf.Max(1, Mathf.CeilToInt(questions.Count / (float)QuestionsPerPage));
        questionPage = Mathf.Clamp(questionPage, 0, pages - 1);
        if (questions.Count == 0)
            L("Bài này chưa có câu hỏi.\nBấm THÊM CÂU hoặc NHẬP CSV.", 30, GameTheme.Fog, TextAnchor.MiddleCenter, 0, 60, 1100, 160);

        for (int i = 0; i < QuestionsPerPage; i++)
        {
            int idx = questionPage * QuestionsPerPage + i;
            if (idx >= questions.Count) break;
            var q = questions[idx];
            float y = 200 - i * 66f;
            UIKit.CBox("RowBg", body, new Color(1, 1, 1, .05f), new Vector2(0, y), new Vector2(1260, 60));
            L((idx + 1) + ". " + Trunc(q.text, 62), 26, GameTheme.Bone, TextAnchor.MiddleLeft, -190, y + 12, 880, 30);
            L("Đáp án " + q.answer + (string.IsNullOrEmpty(q.level) ? "" : " · " + q.level), 20, GameTheme.Fog, TextAnchor.MiddleLeft, -190, y - 15, 880, 26);
            Btn("SỬA", 400, y, 110, 50, () => EditQuestion(q), false, 24);
            BtnDanger("XÓA", 530, y, 110, 50, () => AskDeleteQuestion(q), 24);
        }

        Pager(questionPage, pages, p => { questionPage = p; RenderQuestions(); }, -240);
        Btn("THÊM CÂU", -520, -335, 220, 72, NewQuestion, true, 28);
        Btn("CSV: THÊM VÀO", -285, -335, 230, 72, () => ImportToLesson(false), false, 24);
        Btn("CSV: THAY THẾ", -40, -335, 250, 72, () => ImportToLesson(true), false, 24);
        Btn("QUAY LẠI", 520, -335, 260, 72, () => { if (!busy) RenderLessons(); }, false, 34);
    }

    void AskDeleteQuestion(TeacherApi.Question q)
    {
        if (busy) return;
        Confirm("Xóa câu hỏi này?\n" + Trunc(q.text, 80), "XÓA", () =>
        {
            busy = true;
            ShowMessage("Đang xóa...");
            TeacherApi.Run(DeleteQuestionRoutine(q));
        });
    }

    IEnumerator DeleteQuestionRoutine(TeacherApi.Question q)
    {
        var r = new TeacherApi.Res();
        yield return TeacherApi.DeleteQuestion(q.id, r);
        if (this == null) yield break;
        busy = false;
        Later(r.ok ? "Đã xóa câu hỏi." : "Không xóa được: " + r.error, r.ok);
        GoQuestions(true);
    }

    // ============================================================ SỬA / THÊM CÂU HỎI
    void NewQuestion()
    {
        if (busy) return;
        draftQ = new TeacherApi.Question { lesson_id = curLesson.id, text = "", option_a = "", option_b = "", option_c = "", option_d = "", answer = "A", level = Levels[0], explanation = "" };
        newQ = true;
        ShowQuestionEditor();
    }

    void EditQuestion(TeacherApi.Question q)
    {
        if (busy) return;
        draftQ = Clone(q);
        if (string.IsNullOrEmpty(draftQ.answer)) draftQ.answer = "A";
        newQ = false;
        ShowQuestionEditor();
    }

    void SyncQuestion()
    {
        if (qText == null) return;
        draftQ.text = qText.text.Trim();
        draftQ.option_a = qA.text.Trim(); draftQ.option_b = qB.text.Trim();
        draftQ.option_c = qC.text.Trim(); draftQ.option_d = qD.text.Trim();
        draftQ.explanation = qExp.text.Trim();
    }

    void ShowQuestionEditor()
    {
        view = View.QuestionEdit;
        SetTitle(newQ ? "THÊM CÂU HỎI" : "SỬA CÂU HỎI", Trunc(curLesson.title, 80), 52);
        ClearBody();

        qText = MakeMulti("In_Q", "Nội dung câu hỏi", new Vector2(0, 175), new Vector2(1160, 100), 1000);
        qText.text = draftQ.text ?? "";
        qA = UIKit.MakeInput(body, "In_A", "Đáp án A", new Vector2(-300, 85), 400, 560); qA.text = draftQ.option_a ?? "";
        qB = UIKit.MakeInput(body, "In_B", "Đáp án B", new Vector2(300, 85), 400, 560); qB.text = draftQ.option_b ?? "";
        qC = UIKit.MakeInput(body, "In_C", "Đáp án C", new Vector2(-300, 15), 400, 560); qC.text = draftQ.option_c ?? "";
        qD = UIKit.MakeInput(body, "In_D", "Đáp án D", new Vector2(300, 15), 400, 560); qD.text = draftQ.option_d ?? "";
        qExp = MakeMulti("In_Exp", "Giải thích (không bắt buộc)", new Vector2(0, -72), new Vector2(1160, 90), 2000);
        qExp.text = draftQ.explanation ?? "";

        L("Đáp án đúng", 26, GameTheme.Fog, TextAnchor.MiddleRight, -560, -165, 200, 50);
        for (int i = 0; i < 4; i++)
        {
            string a = Letters[i];
            Btn(a, -400 + i * 130, -165, 110, 56, () => { SyncQuestion(); draftQ.answer = a; ShowQuestionEditor(); }, draftQ.answer == a, 30);
        }
        L("Mức độ", 26, GameTheme.Fog, TextAnchor.MiddleRight, -560, -232, 200, 50);
        float[] lx = { -330, -90, 150 }; float[] lw = { 220, 230, 220 };
        for (int i = 0; i < 3; i++)
        {
            string lv = Levels[i];
            Btn(LevelNames[i], lx[i], -232, lw[i], 56, () => { SyncQuestion(); draftQ.level = lv; ShowQuestionEditor(); }, draftQ.level == lv, 24);
        }

        Btn("LƯU", -300, -335, 300, 72, SaveQuestion, true, 34);
        Btn("QUAY LẠI", 520, -335, 260, 72, () => { if (!busy) RenderQuestions(); }, false, 34);
    }

    void SaveQuestion()
    {
        if (busy) return;
        SyncQuestion();
        if (draftQ.text.Length == 0) { Notice("Hãy nhập nội dung câu hỏi.", false); return; }
        if (draftQ.option_a.Length == 0 || draftQ.option_b.Length == 0 || draftQ.option_c.Length == 0 || draftQ.option_d.Length == 0)
        { Notice("Cần nhập đủ 4 đáp án A, B, C, D.", false); return; }
        busy = true;
        ShowMessage("Đang lưu...");
        TeacherApi.Run(SaveQuestionRoutine());
    }

    IEnumerator SaveQuestionRoutine()
    {
        var r = new TeacherApi.Res();
        if (newQ)
        {
            int max = 0;
            foreach (var q in questions) if (q.position > max) max = q.position;
            draftQ.position = max + 1;
            yield return TeacherApi.InsertQuestions(new List<TeacherApi.Question> { draftQ }, r);
        }
        else yield return TeacherApi.UpdateQuestion(draftQ, r);
        if (this == null) yield break;
        busy = false;
        if (!r.ok) { Later("Không lưu được: " + r.error, false); ShowQuestionEditor(); yield break; }
        Later("Đã lưu câu hỏi.", true);
        GoQuestions(true);
    }

    // ============================================================ NHẬP CSV
    /// <summary>Hiện hộp chọn file và đọc nội dung. Trả về null nếu hủy hoặc lỗi (đã báo ở dòng phụ).</summary>
    string ReadCsvFile()
    {
        if (!FilePicker.Supported) { Notice("Nền tảng này chưa hỗ trợ chọn file (chỉ Windows và Unity Editor).", false); return null; }
        string path = FilePicker.PickCsv();
        if (string.IsNullOrEmpty(path)) return null;   // người dùng hủy
        try { return File.ReadAllText(path, Encoding.UTF8); }
        catch (Exception e) { Notice("Không đọc được file: " + e.Message, false); return null; }
    }

    static string Cell(string[] f, int i) { return i >= 0 && i < f.Length ? (f[i] ?? "").Trim() : ""; }

    static string MapSubject(string raw)
    {
        string n = BioQuestionImporter.Norm(raw);
        if (n == "li" || n.StartsWith("ly") || n.StartsWith("vatly") || n.StartsWith("vatli")) return ProgressSaveSystem.SubjectLy;
        if (n.StartsWith("hoa")) return ProgressSaveSystem.SubjectHoa;
        if (n.StartsWith("sinh")) return ProgressSaveSystem.SubjectSinh;
        return null;
    }

    static string MapLevel(string raw)
    {
        string n = BioQuestionImporter.Norm(raw);
        if (n.StartsWith("thonghieu")) return Levels[1];
        if (n.StartsWith("vandung")) return Levels[2];
        return Levels[0];
    }

    /// <summary>
    /// Đọc CSV. Cột bắt buộc: Câu hỏi, A, B, C, D, Đáp án (A/B/C/D). Tùy chọn: Mức độ, Giải thích.
    /// grouped = true: cần thêm cột Môn và Bài (Chương là tùy chọn) để biết câu thuộc bài nào.
    /// </summary>
    static bool ParseCsv(string text, bool grouped, out List<ImportRow> rows, out int skipped, out string error)
    {
        rows = new List<ImportRow>(); skipped = 0; error = null;
        var raw = BioQuestionImporter.ReadRaw(text);
        if (raw.Count < 2) { error = "File CSV trống hoặc chỉ có dòng tiêu đề."; return false; }

        var h = raw[0];
        int cQ = BioQuestionImporter.Col(h, "cauhoi", "question", "noidung");
        int cA = BioQuestionImporter.Col(h, "a", "dapana", "luachona");
        int cB = BioQuestionImporter.Col(h, "b", "dapanb", "luachonb");
        int cC = BioQuestionImporter.Col(h, "c", "dapanc", "luachonc");
        int cD = BioQuestionImporter.Col(h, "d", "dapand", "luachond");
        int cAns = BioQuestionImporter.Col(h, "dapan", "dapandung", "answer", "dapanchinhxac");
        int cLv = BioQuestionImporter.Col(h, "mucdo", "level", "dokho");
        int cEx = BioQuestionImporter.Col(h, "giaithich", "explanation", "loigiai");
        int cSub = BioQuestionImporter.Col(h, "mon", "monhoc", "subject");
        int cCh = BioQuestionImporter.Col(h, "chuong");
        int cLe = BioQuestionImporter.Col(h, "bai", "tenbai", "baihoc");

        var missing = new List<string>();
        if (cQ < 0) missing.Add("Câu hỏi");
        if (cA < 0) missing.Add("A");
        if (cB < 0) missing.Add("B");
        if (cC < 0) missing.Add("C");
        if (cD < 0) missing.Add("D");
        if (cAns < 0) missing.Add("Đáp án");
        if (grouped && cSub < 0) missing.Add("Môn");
        if (grouped && cLe < 0) missing.Add("Bài");
        if (missing.Count > 0)
        {
            error = "File CSV thiếu cột: " + string.Join(", ", missing.ToArray()) + (grouped ? ". (File không có cột Môn/Bài thì hãy mở một bài và dùng CSV trong bài đó.)" : ".");
            return false;
        }

        for (int i = 1; i < raw.Count; i++)
        {
            var f = raw[i];
            bool blank = true;
            foreach (var c in f) if (!string.IsNullOrWhiteSpace(c)) { blank = false; break; }
            if (blank) continue;

            string ans = Cell(f, cAns).ToUpperInvariant();
            if (ans.Length > 0) ans = ans.Substring(0, 1);
            var q = new TeacherApi.Question
            {
                text = Cell(f, cQ), option_a = Cell(f, cA), option_b = Cell(f, cB), option_c = Cell(f, cC), option_d = Cell(f, cD),
                answer = ans, level = MapLevel(Cell(f, cLv)), explanation = Cell(f, cEx)
            };
            bool bad = q.text.Length == 0 || q.option_a.Length == 0 || q.option_b.Length == 0 || q.option_c.Length == 0
                || q.option_d.Length == 0 || Array.IndexOf(Letters, ans) < 0;

            var row = new ImportRow { q = q, chapter = Cell(f, cCh), lesson = Cell(f, cLe) };
            if (grouped)
            {
                row.subject = MapSubject(Cell(f, cSub));
                if (row.subject == null || row.lesson.Length == 0) bad = true;
            }
            if (bad) { skipped++; continue; }
            rows.Add(row);
        }
        if (rows.Count == 0) { error = "Không có dòng hợp lệ nào trong file (đã bỏ qua " + skipped + " dòng lỗi)."; return false; }
        return true;
    }

    static string LessonKey(string subject, string chapter, string title)
    {
        return subject + "|" + (chapter ?? "").Trim().ToLowerInvariant() + "|" + (title ?? "").Trim().ToLowerInvariant();
    }

    // ---- nhập CSV vào MỘT bài đang mở
    void ImportToLesson(bool replace)
    {
        if (busy || curLesson == null) return;
        string text = ReadCsvFile();
        if (text == null) return;
        List<ImportRow> rows; int skipped; string err;
        if (!ParseCsv(text, false, out rows, out skipped, out err)) { Notice(err, false); return; }

        Action go = () =>
        {
            busy = true;
            ShowMessage("Đang nhập " + rows.Count + " câu hỏi...");
            TeacherApi.Run(ImportToLessonRoutine(rows, skipped, replace));
        };
        if (replace) Confirm("Xóa hết " + questions.Count + " câu hiện có của bài\nvà thay bằng " + rows.Count + " câu từ file?", "THAY THẾ", go);
        else go();
    }

    IEnumerator ImportToLessonRoutine(List<ImportRow> rows, int skipped, bool replace)
    {
        var r = new TeacherApi.Res();
        string id = curLesson.id;
        int start = 0;
        if (replace)
        {
            yield return TeacherApi.DeleteLessonQuestions(id, r);
        }
        else
        {
            var mp = new Dictionary<string, int>();
            yield return TeacherApi.MaxPositions(new List<string> { id }, mp, r);
            if (r.ok) mp.TryGetValue(id, out start);
        }
        if (r.ok)
        {
            var qs = new List<TeacherApi.Question>();
            for (int i = 0; i < rows.Count; i++) { rows[i].q.lesson_id = id; rows[i].q.position = start + i + 1; qs.Add(rows[i].q); }
            yield return TeacherApi.InsertQuestions(qs, r);
        }
        if (this == null) yield break;
        busy = false;
        Later(r.ok ? "Đã nhập " + rows.Count + " câu hỏi" + (skipped > 0 ? " · bỏ qua " + skipped + " dòng lỗi" : "") + "."
                   : "Nhập CSV lỗi: " + r.error, r.ok);
        GoQuestions(true);
    }

    // ---- nhập CSV nhiều bài (theo cột Môn, Chương, Bài)
    void ImportGrouped()
    {
        if (busy) return;
        string text = ReadCsvFile();
        if (text == null) return;
        List<ImportRow> rows; int skipped; string err;
        if (!ParseCsv(text, true, out rows, out skipped, out err)) { Notice(err, false); return; }
        busy = true;
        ShowMessage("Đang nhập " + rows.Count + " câu hỏi...");
        TeacherApi.Run(ImportGroupedRoutine(rows, skipped));
    }

    IEnumerator ImportGroupedRoutine(List<ImportRow> rows, int skipped)
    {
        var r = new TeacherApi.Res();
        int newLessons = 0, usedLessons = 0;

        // 1. gom câu hỏi theo (môn, chương, bài), giữ thứ tự xuất hiện
        var keys = new List<string>();
        var groups = new Dictionary<string, List<ImportRow>>();
        foreach (var row in rows)
        {
            string k = LessonKey(row.subject, row.chapter, row.lesson);
            if (!groups.ContainsKey(k)) { groups[k] = new List<ImportRow>(); keys.Add(k); }
            groups[k].Add(row);
        }
        usedLessons = keys.Count;

        // 2. bài đã có thì dùng lại, chưa có thì tạo (trạng thái NHÁP)
        var idOf = new Dictionary<string, string>();
        foreach (var l in lessons) idOf[LessonKey(l.subject, l.chapter, l.title)] = l.id;
        var existingIds = new List<string>();
        var toCreate = new List<TeacherApi.Lesson>();
        foreach (var k in keys)
        {
            string id;
            if (idOf.TryGetValue(k, out id)) { existingIds.Add(id); continue; }
            var first = groups[k][0];
            toCreate.Add(new TeacherApi.Lesson
            {
                subject = first.subject, chapter = first.chapter, title = first.lesson, content = "",
                status = TeacherApi.Draft, grade = 8, sort_order = lessons.Count + toCreate.Count + 1
            });
        }
        newLessons = toCreate.Count;
        if (toCreate.Count > 0)
        {
            var created = new List<TeacherApi.Lesson>();
            yield return TeacherApi.CreateLessons(toCreate, created, r);
            if (r.ok) foreach (var c in created) idOf[LessonKey(c.subject, c.chapter, c.title)] = c.id;
        }

        // 3. vị trí nối tiếp cho các bài đã có
        var maxPos = new Dictionary<string, int>();
        if (r.ok) yield return TeacherApi.MaxPositions(existingIds, maxPos, r);

        // 4. thêm câu hỏi
        if (r.ok)
        {
            var qs = new List<TeacherApi.Question>();
            foreach (var k in keys)
            {
                string id;
                if (!idOf.TryGetValue(k, out id)) { r.ok = false; r.error = "Không lấy được mã bài vừa tạo."; break; }
                int pos; maxPos.TryGetValue(id, out pos);
                foreach (var row in groups[k]) { row.q.lesson_id = id; row.q.position = ++pos; qs.Add(row.q); }
            }
            if (r.ok) yield return TeacherApi.InsertQuestions(qs, r);
        }

        if (this == null) yield break;
        busy = false;
        Later(r.ok ? "Đã nhập " + rows.Count + " câu vào " + usedLessons + " bài (" + newLessons + " bài mới, trạng thái NHÁP)"
                     + (skipped > 0 ? " · bỏ qua " + skipped + " dòng lỗi" : "") + "."
                   : "Nhập CSV lỗi: " + r.error, r.ok);
        GoLessons(true);
    }
}
