using System;

public enum CellType { Dirt, Question, Rock, Goal }

[Serializable]
public class GridCellData
{
    public int row;
    public int col;
    public CellType type;
    public bool isVisited; // đã trả lời đúng (chỉ có ý nghĩa với Question/Goal)

    public GridCellData(int row, int col)
    {
        this.row = row;
        this.col = col;
        this.type = CellType.Dirt;
    }
}