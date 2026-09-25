using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MazeGridGenerator : MonoBehaviour
{
    [Header("Kích thước lưới")]
    public int rows = 6;
    public int columns = 6;

    [Header("Prefab & container")]
    public GameObject cellPrefab;    // prefab có gắn CellView
    public Transform gridContainer;  // object có GridLayoutGroup

    [Header("Điểm bắt đầu / đích")]
    public Vector2Int start = new Vector2Int(0, 0);
    public Vector2Int goal;

    private GridCellData[,] cellData;
    private CellView[,] cellViews;

    public Vector2Int StartPosition => start;
    public int Rows => rows;
    public int Columns => columns;

    void Awake()
    {
        if (goal == Vector2Int.zero)
            goal = new Vector2Int(rows - 1, columns - 1);

        GenerateData();
        CarvePath();
        BuildView();
    }

    void GenerateData()
    {
        cellData = new GridCellData[rows, columns];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++)
                cellData[r, c] = new GridCellData(r, c) { isBlocked = true };
    }

    // Random-walk từ start đến goal, đảm bảo luôn có ít nhất một đường đi
    void CarvePath()
    {
        Vector2Int current = start;
        MarkPath(current);

        var rnd = new System.Random();
        int safety = (rows + columns) * 4;

        while (current != goal && safety-- > 0)
        {
            List<Vector2Int> options = new List<Vector2Int>();
            if (current.x < goal.x) options.Add(new Vector2Int(current.x + 1, current.y));
            if (current.y < goal.y) options.Add(new Vector2Int(current.x, current.y + 1));
            if (options.Count == 0) break;

            current = options[rnd.Next(options.Count)];
            MarkPath(current);
        }

        cellData[goal.x, goal.y].isGoal = true;
        cellData[goal.x, goal.y].hasQuestion = false;
    }

    void MarkPath(Vector2Int pos)
    {
        cellData[pos.x, pos.y].isBlocked = false;
        cellData[pos.x, pos.y].hasQuestion = true;
    }

    void BuildView()
    {
        cellViews = new CellView[rows, columns];
        var grid = gridContainer.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                GameObject go = Instantiate(cellPrefab, gridContainer);
                CellView view = go.GetComponent<CellView>();
                view.Setup(cellData[r, c]);
                cellViews[r, c] = view;
            }
        }
    }

    public GridCellData GetCell(int row, int col) => cellData[row, col];
    public CellView GetCellView(int row, int col) => cellViews[row, col];
}
