using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum TouchLayout
{
    /// <summary>Game Sinh (đánh boss): joystick trái + nút ĐÁNH (giữ) + nút LƯỚT.</summary>
    Melee,
    /// <summary>Tự luận (phòng quái): joystick trái đi, joystick phải ngắm và bắn (giữ), nút LƯỚT, ĐỔI vũ khí, MỞ rương.</summary>
    Shooter
}

/// <summary>
/// Điều khiển cảm ứng vẽ trên màn hình, chỉ xuất hiện trên điện thoại/tablet (hoặc Device Simulator trong Editor).
/// Đọc thẳng Touchscreen nên không phụ thuộc EventSystem; kích thước nút tính theo cạnh ngắn của màn hình
/// và tự né tai thỏ (Screen.safeArea), dùng được cả ngang lẫn dọc.
///
/// Cách dùng: TouchControls.Create(owner, layout, () => đang chơi?) rồi đọc các thuộc tính static bên dưới.
/// Tự huỷ khi owner bị huỷ. Các cờ *Pressed chỉ đúng trong đúng 1 frame (script này chạy trước các script khác).
/// </summary>
[DefaultExecutionOrder(-100)]
public class TouchControls : MonoBehaviour
{
    public static bool Active => Application.isMobilePlatform || (Application.isEditor && Touchscreen.current != null);

    public static Vector2 Move { get; private set; }          // joystick trái, độ dài 0..1
    public static Vector2 Aim { get; private set; }           // hướng joystick phải (đã chuẩn hoá), (0,0) nếu chưa kéo ra khỏi vùng chết
    public static bool AimHeld { get; private set; }          // đang chạm joystick phải = đang bắn
    public static bool AttackHeld { get; private set; }       // đang giữ nút ĐÁNH (layout Melee)
    public static bool DashPressed { get; private set; }
    public static bool SwapPressed { get; private set; }
    public static bool InteractPressed { get; private set; }
    public static bool InteractVisible;                       // RoomManager bật khi đứng gần rương
    public static float DashCooldown01;                       // 0 = sẵn sàng, 1 = vừa lướt xong

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Move = Aim = Vector2.zero; AimHeld = AttackHeld = DashPressed = SwapPressed = InteractPressed = false;
        InteractVisible = false; DashCooldown01 = 0f;
    }

    sealed class Btn
    {
        public RectTransform root; public Image rim, face, cd; public Text label;
        public Vector2 c; public float r, press; public bool shown = true;
    }

    Component owner; TouchLayout layout; Func<bool> visible, swapAvailable;
    Canvas canvas; RectTransform rootRt;
    int moveId = -1, aimId = -1, atkId = -1;
    bool seenMove, seenAim, seenAtk;
    Vector2 moveOrigin, aimOrigin, moveHome, aimHome;
    Image moveRing, moveKnob, aimRing, aimKnob;
    Btn atk, dash, swap, use;
    float R, D, M;
    int lastW, lastH; Rect lastSafe;
    bool built;

    public static TouchControls Create(Component owner, TouchLayout layout, Func<bool> visible, Func<bool> swapAvailable = null)
    {
        if (!Active) return null;
        var go = new GameObject("TouchControls");
        var t = go.AddComponent<TouchControls>();
        t.owner = owner; t.layout = layout; t.visible = visible; t.swapAvailable = swapAvailable;
        return t;
    }

    void Start() { Build(); }

    void OnDestroy() { ResetStatics(); }

    // ------------------------------------------------------------------ dựng giao diện
    void Build()
    {
        var cgo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
        rootRt = (RectTransform)cgo.transform;

        moveRing = Disc("MoveRing", rootRt, new Color(1f, 1f, 1f, .10f));
        moveKnob = Disc("MoveKnob", rootRt, new Color(1f, 1f, 1f, .30f));
        if (layout == TouchLayout.Shooter)
        {
            aimRing = Disc("AimRing", rootRt, new Color(1f, .75f, .45f, .10f));
            aimKnob = Disc("AimKnob", rootRt, new Color(1f, .75f, .45f, .30f));
            dash = MakeBtn("LƯỚT"); swap = MakeBtn("ĐỔI"); use = MakeBtn("MỞ");
        }
        else
        {
            atk = MakeBtn("ĐÁNH"); dash = MakeBtn("LƯỚT");
        }
        built = true;
        Layout();
    }

    static Image Disc(string n, Transform p, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image)); go.transform.SetParent(p, false);
        var im = go.GetComponent<Image>(); im.sprite = ProcSprites.Circle; im.color = c; im.raycastTarget = false;
        var rt = im.rectTransform; rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(.5f, .5f);
        return im;
    }

    Btn MakeBtn(string text)
    {
        var b = new Btn();
        var go = new GameObject("Btn_" + text, typeof(RectTransform)); go.transform.SetParent(rootRt, false);
        b.root = (RectTransform)go.transform; b.root.anchorMin = b.root.anchorMax = Vector2.zero; b.root.pivot = new Vector2(.5f, .5f);
        b.rim = Disc("Rim", b.root, GameTheme.Bronze.WithAlpha(.75f));
        b.face = Disc("Face", b.root, GameTheme.Ink.WithAlpha(.6f));
        b.cd = Disc("Cooldown", b.root, new Color(0f, 0f, 0f, .6f));
        b.cd.type = Image.Type.Filled; b.cd.fillMethod = Image.FillMethod.Radial360; b.cd.fillOrigin = 2; b.cd.fillClockwise = false; b.cd.fillAmount = 0f;
        foreach (var im in new[] { b.rim, b.face, b.cd }) { im.rectTransform.anchorMin = im.rectTransform.anchorMax = new Vector2(.5f, .5f); im.rectTransform.anchoredPosition = Vector2.zero; }
        var tg = new GameObject("Label", typeof(RectTransform), typeof(Text)); tg.transform.SetParent(b.root, false);
        b.label = tg.GetComponent<Text>(); b.label.font = GameFont.Body; b.label.fontStyle = FontStyle.Bold; b.label.alignment = TextAnchor.MiddleCenter;
        b.label.color = GameTheme.Bone; b.label.text = text; b.label.raycastTarget = false;
        b.label.horizontalOverflow = HorizontalWrapMode.Overflow; b.label.verticalOverflow = VerticalWrapMode.Overflow;
        var lr = b.label.rectTransform; lr.anchorMin = lr.anchorMax = new Vector2(.5f, .5f); lr.anchoredPosition = Vector2.zero;
        return b;
    }

    // ------------------------------------------------------------------ bố cục theo kích thước màn hình
    void Layout()
    {
        lastW = Screen.width; lastH = Screen.height; lastSafe = Screen.safeArea;
        float W = lastW, H = lastH; var sa = lastSafe;
        float b = Mathf.Min(W, H);
        R = .11f * b; D = .15f * b; M = .045f * b;

        moveHome = new Vector2(sa.xMin + M + 1.3f * R, sa.yMin + M + 1.3f * R);
        if (layout == TouchLayout.Shooter)
        {
            aimHome = new Vector2(sa.xMax - M - 1.3f * R, sa.yMin + M + 1.3f * R);
            float rx = sa.xMax - M - D * .5f, y0 = sa.yMin + .5f * b;
            SetBtn(dash, new Vector2(rx, y0), D);
            SetBtn(swap, new Vector2(rx, y0 + 1.35f * D), D * .85f);
            SetBtn(use, new Vector2(sa.center.x, sa.yMin + M + D), D * 1.15f);
        }
        else
        {
            float ra = D * .85f;
            var ac = new Vector2(sa.xMax - M - ra, sa.yMin + M + ra + .1f * D);
            SetBtn(atk, ac, ra * 2f);
            SetBtn(dash, ac + new Vector2(-ra * .9f - .35f * D, ra + .45f * D), D);
        }
    }

    void SetBtn(Btn b, Vector2 c, float d)
    {
        b.c = c; b.r = d * .5f * 1.2f;
        b.root.anchoredPosition = c; b.root.sizeDelta = new Vector2(d, d);
        b.rim.rectTransform.sizeDelta = new Vector2(d, d);
        b.face.rectTransform.sizeDelta = new Vector2(d * .88f, d * .88f);
        b.cd.rectTransform.sizeDelta = new Vector2(d * .88f, d * .88f);
        b.label.fontSize = Mathf.RoundToInt(d * .24f);
        b.label.rectTransform.sizeDelta = new Vector2(d, d);
    }

    // ------------------------------------------------------------------ vòng lặp
    void Update()
    {
        DashPressed = SwapPressed = InteractPressed = false;
        if (owner == null) { Destroy(gameObject); return; }
        if (!built) return;

        bool show = visible == null || visible();
        if (canvas.enabled != show) { canvas.enabled = show; if (!show) ReleaseAll(); }
        if (!show) return;

        if (Screen.width != lastW || Screen.height != lastH || Screen.safeArea != lastSafe) Layout();

        if (swap != null) swap.shown = swapAvailable != null && swapAvailable();
        if (use != null) use.shown = InteractVisible;

        ReadTouches();
        Refresh();
    }

    void ReleaseAll()
    {
        moveId = aimId = atkId = -1;
        Move = Aim = Vector2.zero; AimHeld = AttackHeld = false;
    }

    void ReadTouches()
    {
        var ts = Touchscreen.current;
        seenMove = seenAim = seenAtk = false;
        if (ts != null)
        {
            var touches = ts.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                if (!t.isInProgress) continue;
                int id = t.touchId.ReadValue();
                Vector2 p = t.position.ReadValue();
                if (id == moveId) { seenMove = true; DragMove(p); }
                else if (id == aimId) { seenAim = true; DragAim(p); }
                else if (id == atkId) { seenAtk = true; }
                else if (t.press.wasPressedThisFrame) Begin(id, p);
            }
        }
        if (moveId != -1 && !seenMove) { moveId = -1; Move = Vector2.zero; }
        if (aimId != -1 && !seenAim) { aimId = -1; Aim = Vector2.zero; AimHeld = false; }
        if (atkId != -1 && !seenAtk) { atkId = -1; AttackHeld = false; }
    }

    void Begin(int id, Vector2 p)
    {
        // 1) nút bấm
        if (Hit(dash, p)) { DashPressed = true; dash.press = .12f; return; }
        if (Hit(swap, p)) { SwapPressed = true; swap.press = .12f; return; }
        if (Hit(use, p)) { InteractPressed = true; use.press = .12f; return; }
        if (Hit(atk, p)) { atkId = id; seenAtk = true; AttackHeld = true; return; }

        // 2) joystick trái (nửa trái, 65% chiều cao phía dưới); joystick phải (nửa phải, chỉ layout Shooter)
        float W = Screen.width, H = Screen.height;
        if (p.x < W * .45f && p.y < H * .65f)
        {
            if (moveId == -1) { moveId = id; seenMove = true; moveOrigin = p; Move = Vector2.zero; }
        }
        else if (layout == TouchLayout.Shooter && p.x >= W * .45f && p.y < H * .8f)
        {
            if (aimId == -1) { aimId = id; seenAim = true; aimOrigin = p; AimHeld = true; Aim = Vector2.zero; }
        }
    }

    static bool Hit(Btn b, Vector2 p) => b != null && b.shown && (p - b.c).sqrMagnitude <= b.r * b.r;

    Vector2 Drag(ref Vector2 origin, Vector2 p)
    {
        Vector2 d = p - origin; float len = d.magnitude;
        if (len > R) { origin = p - d / len * R; d = p - origin; len = R; }   // joystick nổi: gốc bám theo ngón tay
        return d / R;
    }

    void DragMove(Vector2 p)
    {
        Vector2 v = Drag(ref moveOrigin, p);
        Move = v.magnitude < .15f ? Vector2.zero : v;
    }

    void DragAim(Vector2 p)
    {
        Vector2 v = Drag(ref aimOrigin, p);
        Aim = v.magnitude < .2f ? Vector2.zero : v.normalized;
        AimHeld = true;
    }

    // ------------------------------------------------------------------ hiển thị
    void Refresh()
    {
        PlaceStick(moveRing, moveKnob, moveId != -1, moveOrigin, moveHome, Move, .10f, .30f);
        if (aimRing != null) PlaceStick(aimRing, aimKnob, aimId != -1, aimOrigin, aimHome, Aim, .10f, .30f);

        RefreshBtn(dash, DashCooldown01);
        RefreshBtn(swap, 0f);
        RefreshBtn(use, 0f);
        RefreshBtn(atk, 0f, AttackHeld);
    }

    void PlaceStick(Image ring, Image knob, bool active, Vector2 origin, Vector2 home, Vector2 v, float aIdle, float aActive)
    {
        Vector2 c = active ? origin : home;
        ring.rectTransform.anchoredPosition = c; ring.rectTransform.sizeDelta = new Vector2(2f * R, 2f * R);
        var k = c + (active ? v * R : Vector2.zero);
        knob.rectTransform.anchoredPosition = k; knob.rectTransform.sizeDelta = new Vector2(R * .85f, R * .85f);
        var rc = ring.color; rc.a = active ? aActive * .6f : aIdle; ring.color = rc;
        var kc = knob.color; kc.a = active ? .55f : aActive * .6f; knob.color = kc;
    }

    void RefreshBtn(Btn b, float cooldown, bool held = false)
    {
        if (b == null) return;
        if (b.root.gameObject.activeSelf != b.shown) b.root.gameObject.SetActive(b.shown);
        if (!b.shown) return;
        if (b.press > 0f) b.press -= Time.unscaledDeltaTime;
        bool down = held || b.press > 0f;
        b.root.localScale = Vector3.one * (down ? .92f : 1f);
        b.face.color = down ? GameTheme.Gold.WithAlpha(.55f) : GameTheme.Ink.WithAlpha(.6f);
        b.cd.fillAmount = Mathf.Clamp01(cooldown);
    }
}
