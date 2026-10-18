using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    public MazeGridGenerator maze;
    public RectTransform playerToken;

    private Vector2Int currentPos;
    private Vector2Int previousPos;

    // ---- chuyển động + hoạt ảnh của hiệp sĩ ----
    private Image tokenImage;
    private Sprite shownSprite;
    private Vector3 smoothPos;        // vị trí đang hiển thị (đuổi theo ô đích)
    private bool placed;
    private float faceDir = 1f;       // 1 = quay phải, -1 = quay trái (lật sprite)
    private const float StepLen = 108f;   // ~ cỡ ô + khoảng cách (đơn vị canvas), dùng tính cung nhảy
    private float walkClock;          // đồng hồ chu kỳ đi bộ (chỉ chạy khi đang bước)

    // ---- ăn mừng chiến thắng ----
    private bool celebrating;
    private float celebrateClock;
    private Vector2 celebrateOffset;  // lệch khỏi tâm ô đích (đứng cạnh công chúa)
    private float celebrateScale = 1f;

    public Vector2Int CurrentPosition => currentPos;

    void Start()
    {
        currentPos = maze.StartPosition;
        playerToken.SetAsLastSibling(); // luôn vẽ đè lên lưới, không bị ô đất che
        tokenImage = playerToken.GetComponent<Image>();
        placed = false;                 // LateUpdate sẽ đặt nhân vật vào đúng ô xuất phát ngay frame đầu
    }

    /// <summary>Bắt đầu nhảy cẫng ăn mừng, đứng lệch offset (đơn vị canvas) so với tâm ô hiện tại.</summary>
    public void Celebrate(Vector2 offset)
    {
        celebrating = true;
        celebrateClock = 0f;
        celebrateOffset = offset;
        faceDir = 1f;
    }

    void SetSprite(Sprite s)
    {
        if (tokenImage == null || s == null || s == shownSprite) return;
        tokenImage.sprite = s;
        shownSprite = s;
    }

    // Vị trí ô lưới (toạ độ thế giới) chỉ đúng sau khi Canvas/GridLayout tính xong và đổi theo
    // kích thước cửa sổ Game, nên nhân vật bám theo ô hiện tại MỖI frame. Thay vì dịch chuyển tức
    // thì, nhân vật trượt mượt tới ô đích với cung nhảy + khung hình đi bộ; khi đứng yên thì "thở" nhẹ.
    void LateUpdate()
    {
        var cellView = maze.GetCellView(currentPos.x, currentPos.y);
        if (cellView == null) return;

        float dt = Time.unscaledDeltaTime;
        float sc = playerToken.parent != null ? playerToken.parent.lossyScale.x : 1f;   // canvas -> pixel

        Vector3 target = cellView.transform.position;
        if (celebrating) target += (Vector3)(celebrateOffset * sc);

        if (!placed) { smoothPos = target; placed = true; }
        else
        {
            smoothPos = Vector3.Lerp(smoothPos, target, 1f - Mathf.Exp(-16f * dt));
            if ((smoothPos - target).sqrMagnitude < (.5f * sc) * (.5f * sc)) smoothPos = target;
        }

        if (celebrating) { AnimateCelebrate(dt, sc); return; }

        float remain = (target - smoothPos).magnitude / sc;     // quãng còn phải đi (đơn vị canvas)
        bool moving = remain > 1f;
        float progress = Mathf.Clamp01(1f - remain / StepLen);
        float hop = moving ? Mathf.Sin(progress * Mathf.PI) * 18f : 0f;          // cung nhảy nhỏ
        float bob = moving ? 0f : Mathf.Sin(Time.unscaledTime * 2.4f) * 2.5f;    // thở khi đứng yên

        if (moving)
        {
            walkClock += dt * 12f;                                   // ~12 khung/giây
            SetSprite(LyGfx.KnightWalk[(int)walkClock % 4]);
        }
        else
        {
            walkClock = 0f;
            SetSprite(LyGfx.KnightIdle);
        }

        playerToken.position = smoothPos + Vector3.up * (hop + bob) * sc;

        // co giãn nhẹ: kéo dài khi đang bước, phồng xẹp khi đứng yên
        float sy = moving ? 1.04f : 1f + Mathf.Sin(Time.unscaledTime * 2.4f) * .02f;
        float sx = moving ? .97f : 1f - Mathf.Sin(Time.unscaledTime * 2.4f) * .012f;
        celebrateScale = 1f;
        playerToken.localScale = new Vector3(faceDir * sx, sy, 1f);
    }

    void AnimateCelebrate(float dt, float sc)
    {
        celebrateClock += dt;
        float t = celebrateClock;
        float s = Mathf.Sin(t * 7f);
        float up = Mathf.Abs(s);
        float jump = up * 46f;                                          // nhảy cao
        celebrateScale = Mathf.Lerp(celebrateScale, 1.5f, 1f - Mathf.Exp(-6f * dt));   // phóng to để thấy rõ mặt
        SetSprite(LyGfx.KnightCheer[(int)(t * 5f) % 2]);                // luân phiên cười tít mắt / mắt long lanh

        bool landing = up < .1f;
        float sy = landing ? .92f : 1.05f, sx = landing ? 1.07f : .97f;  // chạm đất bẹp, bay lên dài
        playerToken.position = smoothPos + Vector3.up * jump * sc;
        playerToken.localScale = new Vector3(faceDir * sx * celebrateScale, sy * celebrateScale, 1f);
        playerToken.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 3.5f) * 5f);   // lắc lư theo nhịp
    }

    public void TryMoveTo(GridCellData cell)
    {
        Vector2Int target = new Vector2Int(cell.row, cell.col);
        if (!IsAdjacent(currentPos, target)) return;
        if (cell.type == CellType.Rock) return;

        previousPos = currentPos;
        if (target.y != currentPos.y) faceDir = Mathf.Sign(target.y - currentPos.y);
        currentPos = target; GameAudio.Play(Snd.LyStep);

        bool needsQuestion = (cell.type == CellType.Question || cell.type == CellType.Goal) && !cell.isVisited;
        if (needsQuestion)
        {
            var view = maze.GetCellView(cell.row, cell.col);
            GameManager.Instance.OnPlayerEnteredQuestionCell(cell, view);
        }
    }

    public void StepBack()
    {
        if (previousPos.y != currentPos.y) faceDir = Mathf.Sign(previousPos.y - currentPos.y);
        currentPos = previousPos;
    }

    bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        int d = Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        return d == 1;
    }
}
