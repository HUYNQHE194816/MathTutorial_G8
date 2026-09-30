using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public MazeGridGenerator maze;
    public RectTransform playerToken;

    private Vector2Int currentPos;
    private Vector2Int previousPos;

    public Vector2Int CurrentPosition => currentPos;

    void Start()
    {
        currentPos = maze.StartPosition;
        playerToken.SetAsLastSibling(); // luôn vẽ đè lên lưới, không bị ô đất che
        SnapToCell(currentPos);
    }

    // Vị trí ô lưới (toạ độ thế giới) chỉ đúng sau khi Canvas/GridLayout tính xong và đổi theo
    // kích thước cửa sổ Game. Nếu chỉ đặt nhân vật 1 lần lúc Start, nhân vật dễ bị lệch ra ngoài
    // màn hình. Bám theo ô hiện tại mỗi frame để luôn hiển thị đúng chỗ.
    void LateUpdate()
    {
        SnapToCell(currentPos);
    }

    public void TryMoveTo(GridCellData cell)
    {
        Vector2Int target = new Vector2Int(cell.row, cell.col);
        if (!IsAdjacent(currentPos, target)) return;
        if (cell.type == CellType.Rock) return;

        previousPos = currentPos;
        currentPos = target;
        SnapToCell(currentPos);

        bool needsQuestion = (cell.type == CellType.Question || cell.type == CellType.Goal) && !cell.isVisited;
        if (needsQuestion)
        {
            var view = maze.GetCellView(cell.row, cell.col);
            GameManager.Instance.OnPlayerEnteredQuestionCell(cell, view);
        }
    }

    public void StepBack()
    {
        currentPos = previousPos;
        SnapToCell(currentPos);
    }

    bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        int d = Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        return d == 1;
    }

    void SnapToCell(Vector2Int pos)
    {
        var cellView = maze.GetCellView(pos.x, pos.y);
        playerToken.position = cellView.transform.position;
    }
}