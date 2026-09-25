using System;

[Serializable]
public class GridCellData
{
    public int row;
    public int col;
    public bool isBlocked;   // ô đá - không thuộc đường đi
    public bool hasQuestion; // ô có dấu "?"
    public bool isVisited;   // người chơi đã trả lời đúng và đi qua
    public bool isGoal;      // ô rương báu vật (đích)

    public GridCellData(int row, int col)
    {
        this.row = row;
        this.col = col;
    }
}
