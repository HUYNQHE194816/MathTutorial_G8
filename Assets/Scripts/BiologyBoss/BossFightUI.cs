using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Toàn bộ giao diện (HUD, bảng câu hỏi, menu, kết thúc) được dựng bằng code với UnityEngine.UI.</summary>
public class BossFightUI : MonoBehaviour
{
    static readonly Vector2 C = new Vector2(.5f, .5f);
    static readonly Color Gold = new Color(.96f, .77f, .26f), Panel = new Color(.11f, .09f, .2f), Btn = new Color(.2f, .16f, .35f), BtnHi = new Color(.31f, .23f, .53f);

    Font font; RectTransform root;
    Image bossFill, playerFill, flashImg;
    Text qCounter, hpText, dashText, qNum, qText, feedback, endTitle, endSub, toastTitle, toastDesc;
    RectTransform buffBox, toastRt; CanvasGroup toastGroup;
    GameObject menuPanel, questionPanel, endPanel;
    Text menuSub, menuStatus, startLabel; Button startBtn;
    readonly List<Text> backLabels = new List<Text>();
    readonly Button[] optBtns = new Button[4]; readonly Text[] optTxt = new Text[4];
    GameObject buffPanel; Text buffSub;
    readonly RectTransform[] buffCards = new RectTransform[3]; readonly Image[] buffBand = new Image[3];
    readonly Text[] buffName = new Text[3], buffDesc = new Text[3]; readonly Image[] buffIcon = new Image[3]; Text rageText;
    float flashA; Color flashCol = Color.red; Coroutine toastCo;
    RectTransform worldRt; RawImage worldImg; Image bossTrail, playerTrail; float bossT = 1f, playerT = 1f;

    // ---------- helpers ----------
    static RectTransform Place(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    { r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size; return r; }
    static void Stretch(RectTransform r, float l, float t, float rr, float b)
    { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = C; r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rr, -t); }

    RectTransform Box(string n, Transform p, Color col, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, bool ray = false)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image)); go.transform.SetParent(p, false);
        var im = go.GetComponent<Image>(); im.sprite = ProcSprites.Pixel; im.color = col; im.raycastTarget = ray;
        return Place((RectTransform)go.transform, aMin, aMax, pivot, pos, size);
    }
    RectTransform Full(string n, Transform p, Color col, bool ray = false) { var r = Box(n, p, col, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, ray); Stretch(r, 0, 0, 0, 0); return r; }

    Text Label(string n, Transform p, string s, int size, Color col, TextAnchor al, FontStyle st = FontStyle.Bold)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Text)); go.transform.SetParent(p, false);
        var t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.fontStyle = st; t.alignment = al; t.color = col; t.text = s;
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.lineSpacing = 1.1f;
        var o = go.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, .85f); o.effectDistance = new Vector2(2, -2);
        return t;
    }

    // Câu hỏi/đáp án nhập từ Sheet có thể rất dài: tự thu nhỏ chữ cho vừa khung thay vì tràn ra ngoài.
    static void FitText(Text t, int min, int max)
    { t.resizeTextForBestFit = true; t.resizeTextMinSize = min; t.resizeTextMaxSize = max; t.verticalOverflow = VerticalWrapMode.Truncate; }

    static void Skin(RectTransform r) { var im = r.GetComponent<Image>(); im.sprite = ProcSprites.Frame; im.type = Image.Type.Sliced; }

    static void BtnColors(Button b, Color n, Color h)
    {
        var cb = b.colors; cb.normalColor = n; cb.highlightedColor = h; cb.selectedColor = n;
        cb.pressedColor = new Color(n.r * .7f, n.g * .7f, n.b * .7f, 1f); cb.disabledColor = n; cb.colorMultiplier = 1f; cb.fadeDuration = .08f; b.colors = cb;
    }

    Button MakeButton(string n, Transform p, Vector2 pos, Vector2 size, Color norm, Color hi, string label, int fs, Color txt, System.Action cb)
    {
        var rt = Box(n, p, Color.white, C, C, C, pos, size, true); Skin(rt);
        var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = rt.GetComponent<Image>(); BtnColors(b, norm, hi);
        var clickSnd = n.StartsWith("Opt") ? Snd.None : (n.Contains("Back") ? Snd.UiBack : Snd.UiClick); b.onClick.AddListener(() => { GameAudio.Play(clickSnd); cb(); });
        var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .8f); sh.effectDistance = new Vector2(0, -7);
        var l = Label("Label", rt, label, fs, txt, TextAnchor.MiddleCenter); Stretch(l.rectTransform, 12, 0, 12, 0);
        return b;
    }

    // ---------- build ----------
    public void Build(System.Action onStart, System.Action<int> onAnswer, System.Action<int> onBuff, System.Action onRetry, System.Action onBack)
    {
        font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Roboto", "Noto Sans", "Helvetica Neue", "Helvetica" }, 48);
        var cgo = new GameObject("UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = .5f;
        root = (RectTransform)cgo.transform;

        var vig = Full("Vignette", root, Color.white); vig.GetComponent<Image>().sprite = ProcSprites.Vignette;

        // Thanh máu boss
        var boss = Box("BossHud", root, Color.clear, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0, -24), new Vector2(1100, 84));
        var nm = Label("Name", boss, "RỒNG LỬA KAIFAFNIR", 34, Color.white, TextAnchor.MiddleLeft); Place(nm.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(700, 40));
        qCounter = Label("Counter", boss, "Câu 1/15", 34, Gold, TextAnchor.MiddleRight); Place(qCounter.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(350, 40));
        var bar = Box("Bar", boss, Color.black, new Vector2(0, 0), new Vector2(1, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(0, 38));
        Box("Track", bar, new Color(.17f, .1f, .14f), Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero); Stretch(bar.Find("Track") as RectTransform, 4, 4, 4, 4);
        var trRt = Box("Trail", bar, new Color(1f, .93f, .6f, .9f), Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero); Stretch(trRt, 4, 4, 4, 4); bossTrail = trRt.GetComponent<Image>(); bossTrail.type = Image.Type.Filled; bossTrail.fillMethod = Image.FillMethod.Horizontal; var fillRt = Box("Fill", bar, new Color(.9f, .27f, .25f), Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero); Stretch(fillRt, 4, 4, 4, 4);
        bossFill = fillRt.GetComponent<Image>(); bossFill.sprite = ProcSprites.BarGrad; bossFill.type = Image.Type.Filled; bossFill.fillMethod = Image.FillMethod.Horizontal;
        rageText = Label("Rage", root, "", 30, new Color(1f, .55f, .3f), TextAnchor.MiddleCenter); Place(rageText.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0, -112), new Vector2(800, 40));
        for (int i = 1; i < 15; i++) Box("Tick" + i, bar, new Color(0, 0, 0, .8f), new Vector2(i / 15f, 0), new Vector2(i / 15f, 1), C, Vector2.zero, new Vector2(3, 0));

        // Máu người chơi
        var me = Box("PlayerHud", root, Color.clear, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(32, 30), new Vector2(520, 100));
        var pbar = Box("Bar", me, Color.black, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(0, 40));
        Box("Track", pbar, new Color(.17f, .1f, .14f), Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero); Stretch(pbar.Find("Track") as RectTransform, 4, 4, 4, 4);
        var ptRt = Box("Trail", pbar, new Color(1f, .93f, .6f, .9f), Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero); Stretch(ptRt, 4, 4, 4, 4); playerTrail = ptRt.GetComponent<Image>(); playerTrail.type = Image.Type.Filled; playerTrail.fillMethod = Image.FillMethod.Horizontal; var pf = Box("Fill", pbar, new Color(.3f, .8f, .4f), Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero); Stretch(pf, 4, 4, 4, 4);
        playerFill = pf.GetComponent<Image>(); playerFill.sprite = ProcSprites.BarGrad; playerFill.type = Image.Type.Filled; playerFill.fillMethod = Image.FillMethod.Horizontal;
        hpText = Label("Hp", pbar, "100 / 100", 28, Color.white, TextAnchor.MiddleCenter); Stretch(hpText.rectTransform, 0, 0, 0, 0);
        dashText = Label("Dash", me, "Shift: lướt né", 26, Color.white, TextAnchor.MiddleLeft); Place(dashText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(400, 40));

        buffBox = Box("Buffs", root, Color.clear, Vector2.one, Vector2.one, Vector2.one, new Vector2(-28, -130), new Vector2(300, 10));

        flashImg = Full("Flash", root, new Color(1, 0, 0, 0)).GetComponent<Image>();
        // Toast buff
        toastRt = Box("Toast", root, Gold, C, C, C, new Vector2(0, 300), new Vector2(760, 150)); Skin(toastRt);
        Box("In", toastRt, Panel, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero); Skin(toastRt.Find("In") as RectTransform); Stretch(toastRt.Find("In") as RectTransform, 6, 6, 6, 6);
        toastGroup = toastRt.gameObject.AddComponent<CanvasGroup>(); toastGroup.alpha = 0f; toastGroup.blocksRaycasts = false;
        toastTitle = Label("T", toastRt, "", 58, Gold, TextAnchor.MiddleCenter); Place(toastTitle.rectTransform, C, C, C, new Vector2(0, 28), new Vector2(720, 70));
        toastDesc = Label("D", toastRt, "", 34, Color.white, TextAnchor.MiddleCenter); Place(toastDesc.rectTransform, C, C, C, new Vector2(0, -34), new Vector2(720, 50)); FitText(toastDesc, 20, 34);

        // Bảng câu hỏi
        questionPanel = Full("QuestionPanel", root, new Color(.04f, .03f, .08f, .9f), true).gameObject;
        var qb = Box("Box", questionPanel.transform, Gold, C, C, C, Vector2.zero, new Vector2(1500, 780), true); Skin(qb);
        Box("In", qb, Panel, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero); Skin(qb.Find("In") as RectTransform); Stretch(qb.Find("In") as RectTransform, 8, 8, 8, 8);
        qNum = Label("Num", qb, "", 38, Gold, TextAnchor.MiddleLeft); Place(qNum.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -34), new Vector2(600, 50));
        qText = Label("Q", qb, "", 52, Color.white, TextAnchor.MiddleLeft); Place(qText.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -100), new Vector2(1400, 230));
        for (int i = 0; i < 4; i++)
        {
            int idx = i; var pos = new Vector2(i % 2 == 0 ? -350f : 350f, i < 2 ? -50f : -200f);
            optBtns[i] = MakeButton("Opt" + i, qb, pos, new Vector2(680, 130), Btn, BtnHi, "", 38, Color.white, () => onAnswer(idx));
            var lab = optBtns[i].GetComponentInChildren<Text>(); lab.alignment = TextAnchor.MiddleLeft; Stretch(lab.rectTransform, 100, 0, 16, 0); optTxt[i] = lab;
            var badge = Label("Badge", optBtns[i].transform, "ABCD"[i].ToString(), 50, Gold, TextAnchor.MiddleCenter);
            Place(badge.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(18, 0), new Vector2(70, 100));
        }
        FitText(qText, 28, 52);
        foreach (var ot in optTxt) FitText(ot, 20, 38);
        feedback = Label("Feedback", qb, "", 42, Color.white, TextAnchor.MiddleCenter); Place(feedback.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 28), new Vector2(1400, 70));
        questionPanel.SetActive(false);

        // Bảng chọn buff (cứ 3 câu được chọn 1 trong 3)
        buffPanel = Full("BuffPanel", root, new Color(.04f, .03f, .08f, .92f), true).gameObject;
        var bt = Label("Title", buffPanel.transform, "CHỌN 1 TRONG 3 BUFF", 84, Gold, TextAnchor.MiddleCenter); Place(bt.rectTransform, C, C, C, new Vector2(0, 360), new Vector2(1700, 120));
        buffSub = Label("Sub", buffPanel.transform, "", 34, new Color(.8f, .85f, 1f), TextAnchor.MiddleCenter, FontStyle.Normal); Place(buffSub.rectTransform, C, C, C, new Vector2(0, 270), new Vector2(1700, 60));
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var card = Box("Card" + i, buffPanel.transform, Color.white, C, C, C, new Vector2((i - 1) * 520f, -50f), new Vector2(460f, 560f), true);
            Skin(card); var cbtn = card.gameObject.AddComponent<Button>(); cbtn.targetGraphic = card.GetComponent<Image>(); BtnColors(cbtn, Btn, BtnHi);
            cbtn.onClick.AddListener(() => onBuff(idx));
            var csh = card.gameObject.AddComponent<Shadow>(); csh.effectColor = new Color(0, 0, 0, .8f); csh.effectDistance = new Vector2(0, -8);
            var col = card.gameObject.AddComponent<Outline>(); col.effectColor = Gold; col.effectDistance = new Vector2(3, -3);
            var band = Box("Band", card, Color.white, new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 26));
            buffBand[i] = band.GetComponent<Image>();
            var ic = Box("Icon", card, Color.white, C, C, C, new Vector2(0, 105), new Vector2(130, 130)); buffIcon[i] = ic.GetComponent<Image>(); buffIcon[i].preserveAspect = true;
            var key = Label("Key", card, "[" + (i + 1) + "]", 44, Gold, TextAnchor.MiddleCenter); Place(key.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -50), new Vector2(200, 60));
            buffName[i] = Label("Name", card, "", 52, Color.white, TextAnchor.MiddleCenter); Place(buffName[i].rectTransform, C, C, C, new Vector2(0, -25), new Vector2(420, 70));
            buffDesc[i] = Label("Desc", card, "", 36, new Color(.88f, .9f, 1f), TextAnchor.MiddleCenter, FontStyle.Normal); Place(buffDesc[i].rectTransform, C, C, C, new Vector2(0, -135), new Vector2(410, 190)); FitText(buffDesc[i], 24, 34);
            var pickTip = Label("Pick", card, "BẤM ĐỂ CHỌN", 28, Gold, TextAnchor.MiddleCenter); Place(pickTip.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 28), new Vector2(380, 44));
            buffCards[i] = card;
        }
        buffPanel.SetActive(false);

        // Menu
        menuPanel = Full("MenuPanel", root, new Color(.05f, .04f, .09f, .82f), true).gameObject;
        var mt = Label("Title", menuPanel.transform, "HIỆP SĨ SINH HỌC", 110, Gold, TextAnchor.MiddleCenter); Place(mt.rectTransform, C, C, C, new Vector2(0, 260), new Vector2(1700, 150));
        var ms = menuSub = Label("Sub", menuPanel.transform, "Đánh Rồng Lửa để mở 15 câu hỏi Sinh học 8.\nCứ xong 2 câu được chọn 1 trong 3 buff (tối đa 5, mỗi buff chỉ 1 lần).\nTrả lời sai: rồng hồi máu, mạnh hơn và phun lửa trừng phạt!", 42, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal);
        Place(ms.rectTransform, C, C, C, new Vector2(0, 90), new Vector2(1600, 150));
        var mc = Label("Controls", menuPanel.transform, "W A S D: di chuyển     Space / Chuột trái: chém (tự ngắm, chém tan đạn lửa)     Shift: lướt né", 34, new Color(.8f, .85f, 1f), TextAnchor.MiddleCenter, FontStyle.Normal);
        Place(mc.rectTransform, C, C, C, new Vector2(0, -50), new Vector2(1700, 90));
        startBtn = MakeButton("Start", menuPanel.transform, new Vector2(0, -230), new Vector2(560, 130), Gold, new Color(1f, .87f, .45f), "VÀO TRẬN", 56, new Color(.17f, .1f, .05f), onStart);
        startLabel = startBtn.GetComponentInChildren<Text>();
        backLabels.Add(MakeButton("MenuBack", menuPanel.transform, new Vector2(-700, -440), new Vector2(420, 90), Btn, BtnHi, "VỀ CHỌN MÔN", 34, Color.white, onBack).GetComponentInChildren<Text>());
        menuStatus = Label("Status", menuPanel.transform, "", 32, new Color(.65f, .92f, .7f), TextAnchor.MiddleCenter, FontStyle.Normal); Place(menuStatus.rectTransform, C, C, C, new Vector2(0, -345), new Vector2(1700, 60));

        // Kết thúc
        endPanel = Full("EndPanel", root, new Color(.05f, .04f, .09f, .88f), true).gameObject;
        endTitle = Label("Title", endPanel.transform, "", 100, Gold, TextAnchor.MiddleCenter); Place(endTitle.rectTransform, C, C, C, new Vector2(0, 200), new Vector2(1700, 140));
        endSub = Label("Sub", endPanel.transform, "", 44, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal); Place(endSub.rectTransform, C, C, C, new Vector2(0, 30), new Vector2(1500, 220));
        MakeButton("Retry", endPanel.transform, new Vector2(-320, -200), new Vector2(560, 120), Gold, new Color(1f, .87f, .45f), "CHƠI LẠI", 48, new Color(.17f, .1f, .05f), onRetry);
        backLabels.Add(MakeButton("Back", endPanel.transform, new Vector2(320, -200), new Vector2(560, 120), Btn, BtnHi, "VỀ CHỌN MÔN", 44, Color.white, onBack).GetComponentInChildren<Text>());
        endPanel.SetActive(false);
    }

    /// <summary>Hiển thị thế giới game (RenderTexture 480x300) đúng tỉ lệ 16:10, có viền đen nếu màn hình khác tỉ lệ.</summary>
    public void SetWorldView(RenderTexture rt)
    {
        var go = new GameObject("WorldView", typeof(RectTransform), typeof(RawImage)); go.transform.SetParent(root, false); go.transform.SetAsFirstSibling();
        worldImg = go.GetComponent<RawImage>(); worldImg.texture = rt; worldImg.raycastTarget = false; worldRt = (RectTransform)go.transform;
        worldRt.anchorMin = C; worldRt.anchorMax = C; worldRt.pivot = C; worldRt.anchoredPosition = Vector2.zero; FitWorld();
    }

    void FitWorld()
    {
        if (worldRt == null) return;
        var pr = root.rect; float k = Mathf.Min(pr.width / 1.6f, pr.height); worldRt.sizeDelta = new Vector2(k * 1.6f, k);
    }

    void Update()
    {
        FitWorld(); flashA = Mathf.Max(0f, flashA - Time.unscaledDeltaTime);
        var c = flashCol; c.a = Mathf.Clamp01(flashA * (flashCol == Color.white ? .8f : .6f)); if (flashImg != null) flashImg.color = c;
    }

    // ---------- API ----------
    public void FlashRed() { flashCol = Color.red; flashA = .35f; }
    public void FlashWhite() { flashCol = Color.white; flashA = 1.2f; }
    public void ShowMenu(bool on) { menuPanel.SetActive(on); }
    public void SetBackLabel(string s) { foreach (var l in backLabels) if (l != null) l.text = s; }

    /// <summary>Cập nhật menu: số câu hỏi của trận, dòng trạng thái nguồn câu hỏi, và khoá nút vào trận khi đang tải.</summary>
    public void SetMenuInfo(int total, string status, bool loading)
    {
        menuSub.text = $"Đánh Rồng Lửa để mở {total} câu hỏi Sinh học 8.\nCứ xong 3 câu được chọn 1 trong 3 buff (tối đa 5, mỗi buff chỉ 1 lần).\nTrả lời sai: rồng hồi máu, mạnh hơn và phun lửa trừng phạt!";
        menuStatus.text = loading ? "Đang tải câu hỏi…" : status;
        startBtn.interactable = !loading; startLabel.text = loading ? "ĐANG TẢI…" : "VÀO TRẬN";
    }

    public void SetHud(float boss01, float hp, float maxHp, int q, int total, bool dashReady)
    {
        float bt = Mathf.Clamp01(boss01), pt = Mathf.Clamp01(hp / maxHp); bossFill.fillAmount = bt; playerFill.fillAmount = pt;
        bossT = bt > bossT ? bt : Mathf.MoveTowards(bossT, bt, Time.unscaledDeltaTime * .2f); playerT = pt > playerT ? pt : Mathf.MoveTowards(playerT, pt, Time.unscaledDeltaTime * .35f);
        bossTrail.fillAmount = bossT; playerTrail.fillAmount = playerT;   // vệt máu vừa mất, trôi dần về
        hpText.text = Mathf.CeilToInt(hp) + " / " + Mathf.RoundToInt(maxHp); qCounter.text = "Câu " + q + "/" + total;
        dashText.color = dashReady ? Color.white : new Color(1, 1, 1, .35f);
    }

    /// <summary>Danh sách buff đang có (mỗi buff 1 lần): icon + tên.</summary>
    public void SetBuffs(List<BuffDef> list)
    {
        for (int i = buffBox.childCount - 1; i >= 0; i--) Destroy(buffBox.GetChild(i).gameObject);
        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            var chip = Box("Chip", buffBox, new Color(d.Color.r * .45f, d.Color.g * .45f, d.Color.b * .45f, .92f), Vector2.one, Vector2.one, Vector2.one, new Vector2(0, -i * 58f), new Vector2(320, 52));
            Box("Strip", chip, d.Color, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, .5f), Vector2.zero, new Vector2(8, 0));
            var ic = Box("Icon", chip, Color.white, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(14, 0), new Vector2(42, 42));
            var im = ic.GetComponent<Image>(); im.sprite = d.Icon; im.preserveAspect = true;
            var l = Label("T", chip, d.Name, 26, Color.white, TextAnchor.MiddleLeft); Stretch(l.rectTransform, 64, 0, 6, 0);
        }
    }

    /// <summary>Hiện hệ số sát thương của rồng dưới thanh máu boss (ẩn khi chưa tăng).</summary>
    public void SetRage(float mult)
    {
        if (rageText == null) return;
        rageText.text = mult > 1.001f ? "SÁT THƯƠNG RỒNG  ×" + mult.ToString("0.0") : "";
    }

    public void ShowToast(string title, string desc, Color col)
    {
        toastTitle.text = title; toastTitle.color = col; toastDesc.text = desc;
        if (toastCo != null) StopCoroutine(toastCo); toastCo = StartCoroutine(ToastRoutine());
    }

    IEnumerator ToastRoutine()
    {
        float e = 0f;
        while (e < 2f)
        {
            e += Time.unscaledDeltaTime;
            float pop = e < .15f ? Mathf.Lerp(.6f, 1.1f, e / .15f) : (e < .3f ? Mathf.Lerp(1.1f, 1f, (e - .15f) / .15f) : 1f);
            toastRt.localScale = Vector3.one * pop;
            toastRt.anchoredPosition = new Vector2(0, 300 + Mathf.Max(0f, e - 1.6f) * 60f);
            toastGroup.alpha = e < .1f ? e / .1f : (e > 1.6f ? Mathf.Clamp01((2f - e) / .4f) : 1f);
            yield return null;
        }
        toastGroup.alpha = 0f;
    }

    public void ShowQuestion(int n, int total, string q, string[] answers)
    {
        qNum.text = "CÂU " + n + " / " + total; qText.text = q; feedback.text = "";
        for (int i = 0; i < 4; i++) { optTxt[i].text = answers[i]; BtnColors(optBtns[i], Btn, BtnHi); }
        questionPanel.SetActive(true);
    }

    public void MarkAnswer(int picked, int correctIdx, bool ok)
    {
        var green = new Color(.18f, .62f, .31f); var red = new Color(.75f, .22f, .17f);
        BtnColors(optBtns[correctIdx], green, green);
        if (!ok) BtnColors(optBtns[picked], red, red);
        feedback.text = ok ? "Chính xác!" : "Sai rồi! Rồng hồi máu và mạnh hơn!"; feedback.color = ok ? new Color(.6f, 1f, .6f) : new Color(1f, .5f, .45f);
    }

    public void HideQuestion() { questionPanel.SetActive(false); }

    public void ShowBuffChoice(BuffDef[] opts, int have, int max)
    {
        buffSub.text = "Buff đã có: " + have + " / " + max + "     Bấm phím 1 / 2 / 3 hoặc click để chọn";
        for (int i = 0; i < buffCards.Length; i++)
        {
            bool on = i < opts.Length; buffCards[i].gameObject.SetActive(on); if (!on) continue;
            buffBand[i].color = opts[i].Color; buffIcon[i].sprite = opts[i].Icon; buffName[i].text = opts[i].Name; buffName[i].color = opts[i].Color; buffDesc[i].text = opts[i].Desc;
        }
        buffPanel.SetActive(true);
    }

    public void HideBuffChoice() { buffPanel.SetActive(false); }

    public void ShowEnd(bool win, string sub)
    {
        endTitle.text = win ? "RỒNG ĐÃ BỊ HẠ GỤC!" : "HIỆP SĨ ĐÃ GỤC NGÃ"; endTitle.color = win ? Gold : new Color(1f, .45f, .4f);
        endSub.text = sub; endPanel.SetActive(true);
    }

    public void Float(Vector2 world, string s, Color c, bool big)
    {
        var cam = Camera.main; if (cam == null || root == null) return;
        Vector3 vp = cam.WorldToViewportPoint(world);
        Vector2 lp = worldRt != null ? new Vector2((vp.x - .5f) * worldRt.sizeDelta.x, (vp.y - .5f) * worldRt.sizeDelta.y) : Vector2.zero;
        var t = Label("Float", root, s, big ? 70 : 48, c, TextAnchor.MiddleCenter);
        Place(t.rectTransform, C, C, C, lp, new Vector2(600, 110)); StartCoroutine(FloatRoutine(t, lp));
    }

    IEnumerator FloatRoutine(Text t, Vector2 start)
    {
        float e = 0f; var rt = t.rectTransform;
        while (e < .9f)
        {
            e += Time.unscaledDeltaTime; float k = e / .9f;
            rt.anchoredPosition = start + Vector2.up * (k * 100f);
            rt.localScale = Vector3.one * (1f + Mathf.Sin(Mathf.Min(1f, k * 4f) * 3.14f) * .25f);
            var col = t.color; col.a = 1f - Mathf.Clamp01((k - .5f) / .5f); t.color = col; yield return null;
        }
        Destroy(t.gameObject);
    }
}
