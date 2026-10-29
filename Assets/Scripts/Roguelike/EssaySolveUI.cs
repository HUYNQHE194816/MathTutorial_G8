using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Dữ liệu để dựng màn viết lời giải.</summary>
public class SolveWritingArgs
{
    public string title, subjectId, statement, draft = "";
    public string hintText = "";
    public bool showHintButton; public string hintButtonLabel = ""; public bool hintEnabled;
    public Action<string> onSubmit;      // tham số: văn bản thô trong ô nhập
    public Action<string> onHint;        // tham số: văn bản thô (để giữ bản nháp)
    public Action<string> onExit;        // thoát đề: bản nháp bị hủy, không tính lần làm
    public Action<string> onPickImage;   // E5: bấm "Tải ảnh" (null = ẩn nút); tham số: bản nháp đang gõ
    public string referenceTitle, referenceText;   // chép phạt: khung tham chiếu (công thức đã sửa) thay cho khung mẹo bên phải; để trống = hiện mẹo gõ ký hiệu
    public string submitLabel = "Nộp bài", exitLabel = "Thoát đề";
}

/// <summary>Dữ liệu dựng màn xác nhận bản chép từ ảnh (E5).</summary>
public class SolveTranscriptArgs
{
    public string title, subjectId; public Texture2D preview; public System.Collections.Generic.List<TranscriptLine> lines;
    public Action<string> onConfirm;     // văn bản đã xác nhận/sửa
    public Action onOtherImage, onCancel;
}

public class SolveResultButton
{
    public string label; public Action onClick; public ButtonStyle style = ButtonStyle.Primary;
}

/// <summary>Dữ liệu để dựng màn kết quả chấm.</summary>
public class SolveResultArgs
{
    public string title, footer, subjectId;
    public EssayProblem problem; public EssayVariant variant; public EssayGradeResult grade; public GradeBand band;
    public string[] lines;                 // lời giải đã chuẩn hóa, mỗi phần tử là một dòng (đánh số từ 1)
    public string analysis;                // E4: phần "Thầy Cú nhận xét" do AI viết (null = chấm bằng bộ chấm tạm)
    public string[] stepWhy;               // E4: lời giải thích từng bước (theo thứ tự barem), có thể null
    public string note;                    // dòng nhắn nhỏ trên tiêu đề (ví dụ: AI chưa phản hồi nên dùng bộ chấm tạm)
    public bool revealDesc;                // true: hiện mô tả barem đã thế số cho mọi bước (khi đã qua cửa hoặc đã phá khóa)
    public List<SolveResultButton> buttons = new List<SolveResultButton>();
}

/// <summary>
/// E2 – Giao diện giải Rương Câu Đố: chọn độ khó, ô nhập lời giải nhiều dòng, bảng ký hiệu (mục 8.2), màn kết quả (mục 4.1).
/// Dựng toàn bộ bằng code qua UIKit. Không giữ logic chơi: mọi hành động trả về qua callback cho RoomManager.
/// </summary>
public class EssaySolveUI : MonoBehaviour
{
    static readonly string[] TabLabels = { "Hay dùng", "Toán", "Hóa", "Lí", "Sinh" };
    static readonly List<string> recent = new List<string>();   // 12 ký hiệu vừa dùng (giữ trong phiên chơi)
    static bool fontChecked;

    RectTransform root, screen;
    InputField input; RectTransform paletteHost, tipHost;
    Text hintLabel, infoLabel, sugLabel; Button hintBtn; Text hintBtnLabel;
    GameObject sugBox;
    bool paletteOpen; int tabIndex; string subjectId = "";
    int lastAnchor, lastFocus;

    public RectTransform Root => root;
    public bool IsWriting => input != null;

    public void Init(Transform parent)
    {
        UIKit.EnsureEventSystem();
        root = UIKit.BuildCanvas(parent, "Solve_UI", 30);
    }

    public void Hide()
    {
        if (screen != null) { screen.gameObject.SetActive(false); Destroy(screen.gameObject); screen = null; }
        input = null; paletteHost = null; tipHost = null; sugBox = null; busyLabel = null;
    }

    RectTransform NewScreen()
    {
        Hide();
        screen = UIKit.CBox("Screen", root, Color.clear, Vector2.zero, Vector2.zero);
        UIKit.Stretch(screen, 0, 0, 0, 0);
        return screen;
    }

    static string SubjectName(string s) => s == "ly" ? "Vật lí" : s == "hoa" ? "Hóa học" : s == "sinh" ? "Sinh học" : "";
    static Color SubjectColor(string s) => s == "ly" ? GameTheme.Ly : s == "hoa" ? GameTheme.Hoa : GameTheme.Sinh;

    void Dim(RectTransform s)
    {
        var dim = UIKit.CBox("Dim", s, new Color(0, 0, 0, .72f), Vector2.zero, Vector2.zero, true);
        UIKit.Stretch(dim, 0, 0, 0, 0);
    }

    // ================================================================== chọn độ khó
    public void ShowDifficulty(string subjectId, string stageLabel, Action<EssayDifficulty> onPick, Action onCancel)
    {
        var s = NewScreen(); Dim(s);
        UIKit.Panel(s, "DiffPanel", Vector2.zero, new Vector2(1500, 720), PanelStyle.Dark, out var c);
        UIKit.Label("T", c, "Rương Câu Đố", 64, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, 265), new Vector2(1300, 90), true, true, FontStyle.Bold);
        UIKit.Label("S", c, "Màn " + stageLabel + " · " + SubjectName(subjectId) + ". Chọn mức thử thách, mức càng cao thưởng càng lớn.",
            30, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 195), new Vector2(1300, 50));
        for (int i = 0; i < 4; i++)
        {
            var d = (EssayDifficulty)i;
            float x = i % 2 == 0 ? -345 : 345, y = i < 2 ? 85 : -105;
            UIKit.MakeButton(c, "Diff" + i, RoomRules.DifficultyName(d), new Vector2(x, y), new Vector2(640, 100),
                () => onPick?.Invoke(d), i == 3 ? ButtonStyle.Danger : i == 2 ? ButtonStyle.Bronze : i == 1 ? ButtonStyle.Primary : ButtonStyle.Stone, 44, .06f * i);
            UIKit.Label("Blurb" + i, c, RoomRules.DifficultyBlurb(d), 22, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(x, y - 68), new Vector2(640, 36));
        }
        UIKit.MakeButton(c, "Cancel", "Để sau", new Vector2(0, -262), new Vector2(300, 70), () => onCancel?.Invoke(), ButtonStyle.Secondary, 30, .3f);
    }

    // ================================================================== viết lời giải
    public void ShowWriting(SolveWritingArgs a)
    {
        subjectId = a.subjectId; paletteOpen = false; tabIndex = EssaySymbols.DefaultTab(a.subjectId);
        var s = NewScreen(); Dim(s);
        UIKit.Label("Title", s, a.title, 46, SubjectColor(a.subjectId), TextAnchor.MiddleCenter, new Vector2(0, 492), new Vector2(1700, 60), true, true, FontStyle.Bold);
        UIKit.Panel(s, "Paper", new Vector2(0, -30), new Vector2(1780, 900), PanelStyle.Parchment, out var pc);

        UIKit.Label("Statement", pc, a.statement, 30, GameTheme.PaperInk, TextAnchor.UpperLeft, new Vector2(0, 345), new Vector2(1640, 150));

        // ---- ô nhập nhiều dòng ----
        var frame = UIKit.CBox("InputFrame", pc, GameTheme.Bronze, new Vector2(-250, 20), new Vector2(1080, 470), true);
        var bg = UIKit.CBox("Bg", frame, new Color(1f, 1f, 1f, .65f), Vector2.zero, Vector2.zero, true);
        UIKit.Stretch(bg, 3, 3, 3, 3);
        var text = UIKit.Label("Text", bg, "", 30, GameTheme.PaperInk, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero);
        UIKit.Stretch(text.rectTransform, 16, 12, 16, 12);
        text.verticalOverflow = VerticalWrapMode.Truncate;     // InputField cuộn theo con trỏ khi Truncate
        var ph = UIKit.Label("Placeholder", bg, "Viết lời giải của em ở đây. Mỗi bước một dòng cho dễ đọc, trình bày tự do.", 28,
            GameTheme.PaperMuted.WithAlpha(.55f), TextAnchor.UpperLeft, Vector2.zero, Vector2.zero, false, false, FontStyle.Italic);
        UIKit.Stretch(ph.rectTransform, 16, 14, 16, 12);
        input = bg.gameObject.AddComponent<InputField>();
        input.targetGraphic = bg.GetComponent<Image>();
        input.textComponent = text; input.placeholder = ph;
        input.lineType = InputField.LineType.MultiLineNewline;
        input.caretBlinkRate = 0f;   // THÊM: dấu nháy luôn hiện, không nhấp nháy
        input.caretWidth = 3;        // THÊM (tuỳ chọn)
        input.characterLimit = EssaySymbols.MaxChars + 200;
        input.onValidateInput = (string t, int i, char ch) => ch == '\t' ? '\0' : ch;   // Tab dành cho nhận gợi ý, không chèn ký tự Tab
        input.text = a.draft ?? "";
        lastAnchor = lastFocus = input.text.Length;

        // ---- thanh gợi ý gõ tắt ----
        var sb = UIKit.CBox("Suggest", pc, GameTheme.ParchmentDark, new Vector2(-250, -250), new Vector2(1080, 44), true);
        sugBox = sb.gameObject;
        sugLabel = UIKit.Label("L", sb, "", 26, GameTheme.PaperInk, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero);
        UIKit.Stretch(sugLabel.rectTransform, 14, 0, 14, 0);
        var sbtn = sb.gameObject.AddComponent<Button>(); sbtn.targetGraphic = sb.GetComponent<Image>(); sbtn.transition = Selectable.Transition.None;
        sbtn.onClick.AddListener(AcceptSuggestion);
        sugBox.SetActive(false);

        // ---- cột phải: bảng ký hiệu hoặc mẹo ----
        paletteHost = UIKit.CBox("Palette", pc, Color.clear, new Vector2(545, 20), new Vector2(520, 470));
        tipHost = UIKit.CBox("Tip", pc, Color.clear, new Vector2(545, 20), new Vector2(520, 470));
        BuildTip(a.referenceTitle, a.referenceText);
        paletteHost.gameObject.SetActive(false);

        hintLabel = UIKit.Label("Hint", pc, a.hintText, 28, GameTheme.EmberDeep, TextAnchor.MiddleLeft, new Vector2(0, -302), new Vector2(1640, 44), false, false, FontStyle.Italic);

        // ---- nút ----
        if (a.showHintButton)
        {
            hintBtn = UIKit.MakeButton(pc, "HintBtn", a.hintButtonLabel, new Vector2(-670, -372), new Vector2(280, 70),
                () => a.onHint?.Invoke(input != null ? input.text : ""), ButtonStyle.Bronze, 28);
            hintBtn.interactable = a.hintEnabled;
            hintBtnLabel = hintBtn.GetComponentInChildren<Text>();
        }
        else { hintBtn = null; hintBtnLabel = null; }
        UIKit.MakeButton(pc, "SymBtn", "Ký hiệu (F2)", new Vector2(-400, -372), new Vector2(220, 70), TogglePalette, ButtonStyle.Secondary, 26);
        UIKit.MakeButton(pc, "ExitBtn", a.exitLabel, new Vector2(-130, -372), new Vector2(260, 70),
            () => a.onExit?.Invoke(input != null ? input.text : ""), ButtonStyle.Secondary, 28);
        if (a.onPickImage != null)
            UIKit.MakeButton(pc, "ImgBtn", "Tải ảnh bài viết tay", new Vector2(240, -372), new Vector2(390, 70),
                () => a.onPickImage?.Invoke(input != null ? input.text : ""), ButtonStyle.Bronze, 26);
        UIKit.MakeButton(pc, "SubmitBtn", a.submitLabel, new Vector2(620, -372), new Vector2(340, 70),
            () => a.onSubmit?.Invoke(input != null ? input.text : ""), ButtonStyle.Primary, 34);

        input.Select(); input.ActivateInputField();
        CheckFontOnce();
    }

    /// <summary>Cập nhật dòng gợi ý và nút gợi ý tại chỗ (không dựng lại màn, giữ nguyên bản nháp).</summary>
    public void SetHint(string text, string buttonLabel, bool enabled)
    {
        if (hintLabel != null) hintLabel.text = text;
        if (hintBtn != null) { hintBtn.interactable = enabled; if (hintBtnLabel != null) hintBtnLabel.text = buttonLabel; }
    }

    void BuildTip(string refTitle = null, string refText = null)
    {
        var f = UIKit.CBox("TipFrame", tipHost, GameTheme.ParchmentDark.WithAlpha(.55f), Vector2.zero, Vector2.zero);
        UIKit.Stretch(f, 0, 0, 0, 0);
        if (!string.IsNullOrEmpty(refText))
        {   // chép phạt: hiện công thức đã sửa để em chép lại vào lời giải (cuộn được khi dài)
            UIKit.Label("RefTitle", f, string.IsNullOrEmpty(refTitle) ? "Công thức đã sửa" : refTitle, 30, GameTheme.PaperInk, TextAnchor.UpperLeft, new Vector2(0, 190), new Vector2(480, 44), false, false, FontStyle.Bold);
            var rc = SinhMenuKit.MakeScroll(f, new Vector2(0, -30), new Vector2(480, 370));
            var rv = rc.gameObject.AddComponent<VerticalLayoutGroup>();
            rv.childControlWidth = rv.childControlHeight = true; rv.childForceExpandWidth = true; rv.childForceExpandHeight = false;
            rv.padding = new RectOffset(0, 12, 0, 12);
            rc.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rt = UIKit.Label("RefBody", rc, refText, 24, GameTheme.PaperInk, TextAnchor.UpperLeft, Vector2.zero, new Vector2(470, 0));
            rt.verticalOverflow = VerticalWrapMode.Overflow;
            return;
        }
        UIKit.Label("TipTitle", f, "Mẹo gõ ký hiệu", 30, GameTheme.PaperInk, TextAnchor.UpperLeft, new Vector2(0, 190), new Vector2(480, 44), false, false, FontStyle.Bold);
        UIKit.Label("TipBody", f,
            "Bấm nút Ký hiệu hoặc phím F2 để mở bảng.\n\nGõ tắt rồi bấm Tab để nhận gợi ý:\n   ^2 cho ²     _2 cho ₂\n   -> cho →     sqrt cho √\n   delta cho Δ\n\nGõ H2O hay x^2 cũng được, trò chơi tự đổi thành H₂O và x² khi nộp bài.",
            24, GameTheme.PaperInk, TextAnchor.UpperLeft, new Vector2(0, -30), new Vector2(480, 380));
    }

    // ------------------------------------------------------------------ bảng ký hiệu
    void TogglePalette()
    {
        if (input == null) return;
        paletteOpen = !paletteOpen;
        paletteHost.gameObject.SetActive(paletteOpen);
        tipHost.gameObject.SetActive(!paletteOpen);
        if (paletteOpen) BuildPalette();
    }

    void BuildPalette()
    {
        for (int i = paletteHost.childCount - 1; i >= 0; i--) Destroy(paletteHost.GetChild(i).gameObject);
        var frame = UIKit.CBox("PFrame", paletteHost, GameTheme.Iron, Vector2.zero, Vector2.zero, true); UIKit.Stretch(frame, 0, 0, 0, 0);
        var fill = UIKit.CBox("PFill", frame, GameTheme.Night, Vector2.zero, Vector2.zero, true); UIKit.Stretch(fill, 4, 4, 4, 4);

        for (int i = 0; i < EssaySymbols.Tabs.Length; i++)
        {
            int idx = i;
            UIKit.MakeButton(fill, "Tab" + i, TabLabels[i], new Vector2(-200 + i * 100, 205), new Vector2(94, 46),
                () => { tabIndex = idx; BuildPalette(); }, i == tabIndex ? ButtonStyle.Primary : ButtonStyle.Secondary, 20);
        }

        UIKit.Label("RecentLabel", fill, "Gần đây", 20, GameTheme.Fog, TextAnchor.MiddleLeft, new Vector2(-190, 160), new Vector2(120, 26));
        for (int i = 0; i < recent.Count; i++)
            SymbolCell(fill, recent[i], new Vector2(-230 + i * 42, 128), new Vector2(40, 42), 24);

        var syms = EssaySymbols.Tabs[tabIndex].symbols;
        const int cols = 8; const float cw = 56, ch = 54;
        for (int i = 0; i < syms.Length; i++)
        {
            float x = (-(cols - 1) / 2f + i % cols) * cw;
            float y = 62 - (i / cols) * ch;
            SymbolCell(fill, syms[i], new Vector2(x, y), new Vector2(cw - 4, ch - 4), 30);
        }

        infoLabel = UIKit.Label("Info", fill, "Rê chuột vào ký hiệu để xem tên và cách gõ tắt.", 22, GameTheme.Bone,
            TextAnchor.MiddleCenter, new Vector2(0, -200), new Vector2(480, 60));
    }

    void SymbolCell(Transform parent, string sym, Vector2 pos, Vector2 size, int font)
    {
        var frame = UIKit.CBox("Sym", parent, GameTheme.Bronze, pos, size, true);
        var fill = UIKit.CBox("Fill", frame, GameTheme.Slate, Vector2.zero, Vector2.zero, true); UIKit.Stretch(fill, 2, 2, 2, 2);
        var l = UIKit.Label("L", fill, sym, font, GameTheme.Rune, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);   // font thân (Body), không dùng Display vì thiếu glyph
        UIKit.Stretch(l.rectTransform, 0, 0, 0, 0);
        var img = fill.GetComponent<Image>();
        var btn = frame.gameObject.AddComponent<Button>(); btn.targetGraphic = img; btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => { GameAudio.Play(Snd.UiClick); InsertText(sym); AddRecent(sym); if (paletteOpen) BuildPalette(); });
        var et = frame.gameObject.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            img.color = GameTheme.SlateHi;
            if (infoLabel != null)
            {
                var d = EssaySymbols.Get(sym);
                infoLabel.text = d.symbol + "  " + d.name + (d.keys.Length > 0 ? "\nGõ tắt: " + d.KeysText : "");
            }
        });
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => img.color = GameTheme.Slate);
        et.triggers.Add(enter); et.triggers.Add(exit);
    }

    static void AddRecent(string sym)
    {
        recent.Remove(sym); recent.Insert(0, sym);
        while (recent.Count > 12) recent.RemoveAt(recent.Count - 1);
    }

    // ------------------------------------------------------------------ chèn ký tự và nhận gợi ý
    void SetCaret(int pos)
    {
        input.ActivateInputField(); input.Select();
        input.caretPosition = pos; input.selectionAnchorPosition = pos; input.selectionFocusPosition = pos;
        lastAnchor = lastFocus = pos;
    }

    void InsertText(string sym)
    {
        if (input == null) return;
        string t = input.text ?? "";
        int a = Mathf.Clamp(Mathf.Min(lastAnchor, lastFocus), 0, t.Length), b = Mathf.Clamp(Mathf.Max(lastAnchor, lastFocus), 0, t.Length);
        input.text = t.Substring(0, a) + sym + t.Substring(b);
        SetCaret(a + sym.Length);
    }

    void AcceptSuggestion()
    {
        if (input == null) return;
        string t = input.text ?? "";
        int caret = Mathf.Clamp(lastFocus, 0, t.Length);
        if (!EssaySymbols.Suggest(t, caret, out var def, out int len)) return;
        input.text = t.Substring(0, caret - len) + def.symbol + t.Substring(caret);
        SetCaret(caret - len + def.symbol.Length);
        AddRecent(def.symbol);
        if (paletteOpen) BuildPalette();
        sugBox.SetActive(false);
    }

    void Update()
    {
        if (busyLabel != null) { busyT += Time.unscaledDeltaTime; busyLabel.text = busyBase + new string('.', 1 + (int)(busyT * 2f) % 3); }
        if (input == null) return;
        var k = Keyboard.current;
        if (k != null && k.f2Key.wasPressedThisFrame) TogglePalette();
        // THÊM khối này:
        if (!input.isFocused && input.gameObject.activeInHierarchy)
        {
            input.ActivateInputField();
            input.caretPosition = Mathf.Clamp(lastFocus, 0, input.text.Length);
        }

        if (input.isFocused)
        {
            lastAnchor = input.selectionAnchorPosition; lastFocus = input.selectionFocusPosition;
            if (sugBox != null)   // THÊM: màn ShowTranscript không có thanh gợi ý
            {
                if (EssaySymbols.Suggest(input.text, input.caretPosition, out var def, out int len))
                {
                    sugBox.SetActive(true);
                    sugLabel.text = "Gợi ý: " + def.symbol + "  (" + def.name + ")  ·  bấm Tab hoặc bấm vào đây để nhận";
                    if (k != null && k.tabKey.wasPressedThisFrame) AcceptSuggestion();
                }
                else sugBox.SetActive(false);
            }
        }
    }

    static void CheckFontOnce()
    {
        if (fontChecked) return; fontChecked = true;
        var f = GameFont.Body; if (f == null) return;
        var missing = new StringBuilder();
        foreach (var s in EssaySymbols.AllSymbols())
            if (s.Length > 0 && !f.HasCharacter(s[0])) missing.Append(s).Append(' ');
        if (missing.Length > 0)
            Debug.LogWarning("[EssaySolveUI] Font thân thiếu glyph cho các ký hiệu: " + missing + ". Thêm font phụ hoặc đổi font trong GameFont.Body (mục rủi ro 'Font game thiếu glyph' của kế hoạch).");
    }

    // ================================================================== chờ AI
    Text busyLabel; float busyT;

    /// <summary>Màn chờ khi AI đang chấm hoặc đọc ảnh. onSkip (tùy chọn) cho phép bỏ chờ và dùng bộ chấm tạm.</summary>
    public void ShowBusy(string title, string message, Action onSkip = null, string skipLabel = "Dùng bộ chấm tạm")
    {
        var s = NewScreen(); Dim(s);
        UIKit.Panel(s, "BusyPanel", Vector2.zero, new Vector2(1100, 460), PanelStyle.Dark, out var c);
        UIKit.Label("T", c, title, 52, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, 140), new Vector2(980, 80), true, true, FontStyle.Bold);
        busyLabel = UIKit.Label("M", c, message, 30, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 20), new Vector2(980, 120)); busyT = 0f;
        UIKit.Label("W", c, "Thời gian trong trận vẫn đang đóng băng, em cứ yên tâm chờ.", 24, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, -80), new Vector2(980, 40), false, false, FontStyle.Italic);
        if (onSkip != null) UIKit.MakeButton(c, "Skip", skipLabel, new Vector2(0, -170), new Vector2(420, 70), onSkip, ButtonStyle.Secondary, 28, 8f);
        busyBase = message;
    }
    string busyBase;

    // ================================================================== xác nhận bản chép từ ảnh (E5, mục 9 B4)
    public void ShowTranscript(SolveTranscriptArgs a)
    {
        busyLabel = null; var s = NewScreen(); Dim(s);
        UIKit.Label("Title", s, a.title, 46, SubjectColor(a.subjectId), TextAnchor.MiddleCenter, new Vector2(0, 492), new Vector2(1700, 60), true, true, FontStyle.Bold);
        UIKit.Panel(s, "ImgPanel", new Vector2(-560, -30), new Vector2(620, 900), PanelStyle.Dark, out var ic);
        UIKit.Label("IT", ic, "Ảnh của em", 30, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 395), new Vector2(560, 40), false, false, FontStyle.Bold);
        if (a.preview != null)
        {
            var go = new GameObject("Preview", typeof(RectTransform), typeof(RawImage)); var rt = (RectTransform)go.transform; rt.SetParent(ic, false);
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f); rt.anchoredPosition = new Vector2(0, -10);
            float k = Mathf.Min(560f / a.preview.width, 740f / a.preview.height); rt.sizeDelta = new Vector2(a.preview.width * k, a.preview.height * k);
            var ri = go.GetComponent<RawImage>(); ri.texture = a.preview; ri.raycastTarget = false;
        }
        UIKit.Panel(s, "TextPanel", new Vector2(330, -30), new Vector2(1160, 900), PanelStyle.Parchment, out var pc);
        UIKit.Label("TT", pc, "Bản chép của AI: em đọc lại và sửa chỗ nào sai rồi mới nộp chấm", 30, GameTheme.PaperInk, TextAnchor.MiddleLeft, new Vector2(0, 385), new Vector2(1080, 44), false, false, FontStyle.Bold);
        var sbText = new StringBuilder(); var low = new List<int>();
        for (int i = 0; i < a.lines.Count; i++) { sbText.Append(a.lines[i].text); if (i + 1 < a.lines.Count) sbText.Append('\n'); if (a.lines[i].conf < .6f || a.lines[i].text.Contains("[không đọc được]")) low.Add(i + 1); }
        var frame = UIKit.CBox("InputFrame", pc, GameTheme.Bronze, new Vector2(0, 90), new Vector2(1100, 560), true);
        var bg = UIKit.CBox("Bg", frame, new Color(1f, 1f, 1f, .65f), Vector2.zero, Vector2.zero, true); UIKit.Stretch(bg, 3, 3, 3, 3);
        var text = UIKit.Label("Text", bg, "", 30, GameTheme.PaperInk, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero); UIKit.Stretch(text.rectTransform, 16, 12, 16, 12); text.verticalOverflow = VerticalWrapMode.Truncate;
        var ph = UIKit.Label("Placeholder", bg, "", 28, GameTheme.PaperMuted.WithAlpha(.5f), TextAnchor.UpperLeft, Vector2.zero, Vector2.zero); UIKit.Stretch(ph.rectTransform, 16, 14, 16, 12);
        input = bg.gameObject.AddComponent<InputField>(); input.targetGraphic = bg.GetComponent<Image>(); input.textComponent = text; input.placeholder = ph;
        input.lineType = InputField.LineType.MultiLineNewline; input.characterLimit = EssaySymbols.MaxChars + 200; input.text = sbText.ToString();
        input.caretBlinkRate = 0f; input.caretWidth = 3;          // THÊM
        lastAnchor = lastFocus = input.text.Length;               // THÊM: để lấy lại focus thì dấu nháy ở cuối bài, không về đầu
        string warn = low.Count > 0 ? "Dòng " + string.Join(", ", low) + " AI đọc chưa chắc, em kiểm tra kỹ nhé." : "AI đọc khá chắc, em vẫn nên đọc lại một lượt.";
        UIKit.Label("Warn", pc, warn, 26, low.Count > 0 ? GameTheme.Blood : GameTheme.EmberDeep, TextAnchor.MiddleLeft, new Vector2(0, -230), new Vector2(1080, 40), false, false, FontStyle.Bold);
        UIKit.Label("Note", pc, "Chỉ sau khi em xác nhận, bài mới được chấm. Ảnh được gửi thẳng cho AI để đọc và không được lưu lại.", 22, GameTheme.PaperMuted, TextAnchor.MiddleLeft, new Vector2(0, -275), new Vector2(1080, 36), false, false, FontStyle.Italic);
        UIKit.MakeButton(pc, "Cancel", "Quay lại gõ tay", new Vector2(-410, -372), new Vector2(300, 70), () => a.onCancel?.Invoke(), ButtonStyle.Secondary, 26);
        UIKit.MakeButton(pc, "Other", "Chọn ảnh khác", new Vector2(-70, -372), new Vector2(300, 70), () => a.onOtherImage?.Invoke(), ButtonStyle.Bronze, 26);
        UIKit.MakeButton(pc, "Ok", "Xác nhận và chấm", new Vector2(370, -372), new Vector2(400, 70), () => a.onConfirm?.Invoke(input != null ? input.text : ""), ButtonStyle.Primary, 32);
        CheckFontOnce();
    }

    // ================================================================== sửa công thức: công thức có sẵn, em điền số vào ô trống
    /// <summary>Công thức bước làm chưa đúng được viết sẵn, các số cần tìm là ô trống nằm ngay trong công thức. Chỉ đi tiếp (onDone) khi điền đúng hết.</summary>
    public void ShowFormulaFix(FormulaFixArgs a)
    {
        var s = NewScreen(); Dim(s);
        var it = a.item; int n = it.blanks.Length;
        UIKit.Panel(s, "FixPanel", Vector2.zero, new Vector2(1500, 800), PanelStyle.Dark, out var c);
        UIKit.Label("T", c, "Sửa công thức · Bước " + (it.stepIndex + 1) + ": " + it.label, 46, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, 335), new Vector2(1360, 70), true, true, FontStyle.Bold);
        UIKit.Label("S", c, "Công thức đã viết sẵn, em điền các số còn thiếu vào ô trống (" + a.stepNo + "/" + a.stepCount + ").", 28, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 275), new Vector2(1360, 44));
        UIKit.Label("Q", c, "Đề: " + a.statement, 24, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, 190), new Vector2(1360, 110));

        // ---- khung công thức: chữ và ô nhập xếp thành dòng, tự xuống dòng khi hết chỗ ----
        const float flowW = 1340f, rowH = 90f, inW = 170f, gap = 14f;
        var bg = UIKit.CBox("FormulaBg", c, GameTheme.Ink.WithAlpha(.6f), new Vector2(0, -25), new Vector2(1400, 300));
        var flow = UIKit.CBox("Flow", c, Color.clear, new Vector2(0, -25), new Vector2(flowW, 300));

        var toks = new List<FlowTok>(); bool space = false;
        for (int pi = 0; pi < it.parts.Length; pi++)
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(it.parts[pi], @"\s+|\S+"))
            {
                if (char.IsWhiteSpace(m.Value[0])) { space = true; continue; }
                var lb = UIKit.Label("W", flow, m.Value, 40, GameTheme.GoldHi, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(100, 70), false, false, FontStyle.Bold);
                lb.horizontalOverflow = HorizontalWrapMode.Overflow;
                float w = Mathf.Ceil(lb.preferredWidth) + 4f; lb.rectTransform.sizeDelta = new Vector2(w, 70);
                toks.Add(new FlowTok { rt = lb.rectTransform, w = w, spaceBefore = space }); space = false;
            }
            if (pi < n)
            {
                var inp = UIKit.MakeInput(flow, "In" + pi, it.blanks[pi].name, Vector2.zero, 10, inW);
                toks.Add(new FlowTok { rt = (RectTransform)inp.transform, w = inW, spaceBefore = space, blank = pi }); space = false;
            }
        }

        // gom thành nhóm không có khoảng trắng ở giữa (vd ô/ô trong "(1)/(2)") để không bị ngắt giữa chừng
        var rows = new List<List<FlowTok>>(); var cur = new List<FlowTok>(); float curW = 0f;
        for (int gi = 0; gi < toks.Count;)
        {
            int gj = gi + 1; float gw = toks[gi].w;
            while (gj < toks.Count && !toks[gj].spaceBefore) { gw += toks[gj].w; gj++; }
            float need = cur.Count == 0 ? gw : curW + gap + gw;
            if (cur.Count > 0 && need > flowW) { rows.Add(cur); cur = new List<FlowTok>(); curW = 0f; need = gw; }
            toks[gi].lead = cur.Count > 0 ? gap : 0f;
            for (int k = gi; k < gj; k++) cur.Add(toks[k]);
            curW = need; gi = gj;
        }
        if (cur.Count > 0) rows.Add(cur);
        float top = (rows.Count - 1) * rowH / 2f;
        for (int ri = 0; ri < rows.Count; ri++)
        {
            float total = 0f; foreach (var t in rows[ri]) total += t.lead + t.w;
            float x = -total / 2f;
            foreach (var t in rows[ri]) { x += t.lead; t.rt.anchoredPosition = new Vector2(x + t.w / 2f, top - ri * rowH); x += t.w; }
        }

        var ins = new InputField[n]; var frames = new Image[n];
        foreach (var t in toks) if (t.blank >= 0) { ins[t.blank] = t.rt.GetComponent<InputField>(); frames[t.blank] = (Image)ins[t.blank].targetGraphic; }

        var fb = UIKit.Label("FB", c, "Gõ dấu phẩy hoặc dấu chấm đều được. Chữ mờ trong ô là tên đại lượng cần điền.", 26, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, -232), new Vector2(1360, 90));
        int tries = 0; bool solved = false; Text btnLabel = null; Action check = null;
        check = () =>
        {
            if (solved) { a.onDone?.Invoke(); return; }
            var typed = new string[n];
            for (int i = 0; i < n; i++)
            {
                typed[i] = ins[i].text;
                if (string.IsNullOrWhiteSpace(typed[i])) { fb.text = "Em điền đủ tất cả các ô nhé."; fb.color = GameTheme.EmberHi; return; }
            }
            var ok = EssayFormulaFix.Check(it, typed); tries++;
            bool all = true; var bad = new List<int>();
            for (int i = 0; i < n; i++) { frames[i].color = ok[i] ? GameTheme.Moss : GameTheme.Blood; if (!ok[i]) { all = false; bad.Add(i); } }
            if (all)
            {
                solved = true;
                foreach (var f in ins) f.interactable = false;
                fb.text = "Chính xác! Công thức đã đúng."; fb.color = GameTheme.Moss;
                if (btnLabel != null) btnLabel.text = a.stepNo < a.stepCount ? "Bước tiếp theo" : "Xem kết quả";
                GameAudio.Play(Snd.QuizCorrect);
                return;
            }
            GameAudio.Play(Snd.QuizWrong);
            var sb = new StringBuilder("Ô ");
            for (int k = 0; k < bad.Count; k++) sb.Append(k > 0 ? ", " : "").Append('(').Append(bad[k] + 1).Append(')');
            sb.Append(" chưa đúng (viền đỏ, tính từ trái sang). Xem lại đề rồi tính lại nhé.");
            if (tries >= 3)
            {   // tránh kẹt: sau 3 lần sai thì cho xem số đúng, nhưng em vẫn phải tự gõ lại vào ô
                sb.Append("\nSố đúng: ");
                for (int k = 0; k < bad.Count; k++) sb.Append(k > 0 ? ";  " : "").Append('(').Append(bad[k] + 1).Append(") = ").Append(it.blanks[bad[k]].shown);
            }
            fb.text = sb.ToString(); fb.color = GameTheme.Blood;
        };

        for (int i = 0; i < n; i++)
        {
            int idx = i;
            ins[i].onValueChanged.AddListener(_ => { if (!solved) frames[idx].color = GameTheme.Iron; });
            ins[i].onEndEdit.AddListener(_ =>
            {
                var k = Keyboard.current;
                if (k == null || !(k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)) return;
                if (!solved)   // Enter: nhảy sang ô còn trống tiếp theo, hết ô trống thì kiểm tra
                    for (int j = 1; j <= n; j++)
                    {
                        int nx = (idx + j) % n;
                        if (string.IsNullOrWhiteSpace(ins[nx].text)) { ins[nx].Select(); ins[nx].ActivateInputField(); return; }
                    }
                check();
            });
        }
        var btn = UIKit.MakeButton(c, "CheckBtn", "Kiểm tra", new Vector2(0, -335), new Vector2(460, 76), () => check(), ButtonStyle.Primary, 34, .1f);
        btnLabel = btn.GetComponentInChildren<Text>();
        if (n > 0) { ins[0].Select(); ins[0].ActivateInputField(); }
    }

    class FlowTok { public RectTransform rt; public float w, lead; public bool spaceBefore; public int blank = -1; }

    // ================================================================== màn kết quả
    public void ShowResult(SolveResultArgs a)
    {
        var s = NewScreen(); Dim(s);
        Color sc = SubjectColor(a.subjectId);
        UIKit.Label("Title", s, "Kết quả chấm · " + SubjectName(a.subjectId), 46, sc, TextAnchor.MiddleCenter, new Vector2(0, 492), new Vector2(1700, 60), true, true, FontStyle.Bold);

        // ---- trái: bài làm, đánh số dòng ----
        UIKit.Panel(s, "WorkPanel", new Vector2(-470, -30), new Vector2(820, 900), PanelStyle.Parchment, out var wc);
        UIKit.Label("WorkTitle", wc, "Bài làm của em", 32, GameTheme.PaperMuted, TextAnchor.MiddleLeft, new Vector2(0, 385), new Vector2(740, 44), false, false, FontStyle.Bold);
        var sb = new StringBuilder();
        for (int i = 0; i < a.lines.Length; i++) sb.Append(i + 1).Append("   ").Append(a.lines[i]).Append('\n');
        bool hasAna = !string.IsNullOrEmpty(a.analysis);
        var work = UIKit.Label("Work", wc, sb.ToString().TrimEnd('\n'), hasAna ? 24 : 28, GameTheme.PaperInk, TextAnchor.UpperLeft, hasAna ? new Vector2(0, 170) : new Vector2(0, -30), hasAna ? new Vector2(740, 380) : new Vector2(740, 740));
        work.verticalOverflow = VerticalWrapMode.Truncate;
        if (hasAna)
        {
            UIKit.Label("AnaTitle", wc, "Thầy Cú nhận xét", 30, GameTheme.EmberDeep, TextAnchor.MiddleLeft, new Vector2(0, -35), new Vector2(740, 40), false, false, FontStyle.Bold);
            // THAY BẰNG:
            var anaContent = SinhMenuKit.MakeScroll(wc, new Vector2(0, -235), new Vector2(740, 300));
            var vlg = anaContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0, 12, 0, 12);
            anaContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var ana = UIKit.Label("Ana", anaContent, a.analysis, 22, GameTheme.PaperInk, TextAnchor.UpperLeft, Vector2.zero, new Vector2(740, 0));
            ana.verticalOverflow = VerticalWrapMode.Overflow;
        }

        // ---- phải: từng bước barem ----
        UIKit.Panel(s, "ResPanel", new Vector2(450, -30), new Vector2(860, 900), PanelStyle.Dark, out var rc);
        Color bandColor = a.band == GradeBand.Good ? GameTheme.Moss : a.band == GradeBand.Pass ? GameTheme.Gold : GameTheme.Blood;
        UIKit.Label("Band", rc, a.title, 52, bandColor, TextAnchor.MiddleCenter, new Vector2(0, 370), new Vector2(780, 70), true, true, FontStyle.Bold);
        UIKit.Label("Score", rc, "Điểm " + a.grade.score + "/" + a.grade.maxScore, 34, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, 318), new Vector2(780, 44));
        if (!string.IsNullOrEmpty(a.note)) UIKit.Label("Note", rc, a.note, 20, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, 290), new Vector2(800, 28), false, false, FontStyle.Italic);

        int n = a.problem.rubric.Length;
        for (int i = 0; i < n; i++)
        {
            var st = a.grade.steps[i];
            string tag; Color col;
            switch (st.status)
            {
                case EssayStepStatus.Correct: tag = "Đúng"; col = GameTheme.Moss; break;
                case EssayStepStatus.Partial: tag = "Một phần"; col = GameTheme.GoldHi; break;
                case EssayStepStatus.Wrong: tag = "Chưa đúng"; col = GameTheme.Blood; break;
                default: tag = "Chưa thấy"; col = GameTheme.Blood; break;
            }
            string where = st.lines != null && st.lines.Length > 0 ? "  ·  dòng " + string.Join(", ", st.lines) : "";
            float y = 255 - i * 84;
            UIKit.Label("StepHead" + i, rc, "Bước " + (i + 1) + " · " + a.problem.rubric[i].label + "  —  " + tag + where + "  ·  " + st.points + "/" + st.maxPoints,
                26, col, TextAnchor.MiddleLeft, new Vector2(0, y), new Vector2(780, 34), false, false, FontStyle.Bold);
            string body;
            string why = a.stepWhy != null && i < a.stepWhy.Length ? a.stepWhy[i] : null;
            if (st.status != EssayStepStatus.Correct && !string.IsNullOrEmpty(why)) body = why;
            else if (a.revealDesc || st.status == EssayStepStatus.Correct) body = a.variant.rubricDesc[i];
            else body = st.status == EssayStepStatus.Partial ? "Có một phần ý của bước này, còn thiếu ý khác." : "Chưa thấy ý của bước này trong bài làm.";
            UIKit.Label("StepBody" + i, rc, body, 22, GameTheme.Fog, TextAnchor.UpperLeft, new Vector2(0, y - 40), new Vector2(780, 48));
        }

        UIKit.Label("Footer", rc, a.footer, 26, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, -255), new Vector2(780, 100));

        int nb = a.buttons.Count;
        for (int i = 0; i < nb; i++)
        {
            var b = a.buttons[i];
            float x = nb == 1 ? 0 : nb == 2 ? (i == 0 ? -200 : 200) : (i - 1) * 270f;
            float bw = nb == 1 ? 520 : nb == 2 ? 380 : 256;
            UIKit.MakeButton(rc, "Btn" + i, b.label, new Vector2(x, -365), new Vector2(bw, 72), b.onClick, b.style, nb == 1 ? 34 : nb == 2 ? 26 : 22, .1f * i);
        }
    }
}
