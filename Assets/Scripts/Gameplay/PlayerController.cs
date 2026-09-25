using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public MazeGridGenerator maze;
    public RectTransform playerToken; // sprite "Thợ mỏ" trong ảnh mẫu

    private Vector2Int currentPos;
    private Vector2Int previousPos;

    void Start()
    {
        currentPos = maze.StartPosition;
        SnapToCell(currentPos);
    }

    public void TryMoveTo(GridCellData cell)
    {
        Vector2Int target = new Vector2Int(cell.row, cell.col);
        if (!IsAdjacent(currentPos, target)) return;
        if (cell.isBlocked) return; // không thể bước lên ô đá

        previousPos = currentPos;
        currentPos = target;
        SnapToCell(currentPos);

        if (cell.hasQuestion && !cell.isVisited)
        {
            var view = maze.GetCellView(cell.row, cell.col);
            GameManager.Instance.OnPlayerEnteredQuestionCell(cell, view);
        }
    }

    // Trả lời sai -> "phải quay lại đi đường khác" (theo luật trong ảnh mẫu)
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
